using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 오늘작업계획Presenter : MonoBehaviour
    {
        [SerializeField] private 농장경영시점Controller management = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 턴마감Presenter evening = null!;
        [SerializeField] private bool presentationOnly = true;

        private 오늘작업계획Coordinator? coordinator;
        private CancellationTokenSource? lifetimeCancellation;
        private string assignmentKindCode = 오늘작업계획Codes.PlayerDirect;
        private string actorStableId = 오늘작업계획Codes.PlayerActor;
        private string[] confirmedTargetIds = Array.Empty<string>();
        private string confirmedActionCode = string.Empty;
        private bool busy;
        private string status = "전술 화면에서 수확할 밭을 선택하세요.";

        public string AssignmentKindCode => assignmentKindCode;
        public string Status => status;
        public bool PresentationOnly => presentationOnly;
        public 오늘작업계획PreviewData? CurrentPreview => coordinator?.CurrentPreview;
        public 오늘작업CanonicalStateData? CurrentState => coordinator?.CurrentState;

        public void Configure(
            농장경영시점Controller managementController,
            플레이어경관Controller playerController,
            턴마감Presenter? eveningPresenter = null)
        {
            management = managementController;
            player = playerController;
            evening = eveningPresenter != null
                ? eveningPresenter
                : GetComponent<턴마감Presenter>();
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("DailyWorkPlanPresenterWiringInvalid");
        }

        public bool ValidateWiring()
            => management != null && player != null && player.PresentationOnly
                && presentationOnly;

        public async Task InitializeAsync(
            I오늘작업계획AuthorityClient authority,
            string sessionStableId,
            long revision)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("DailyWorkPlanPresenterWiringInvalid");
            evening ??= GetComponent<턴마감Presenter>();
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();
            coordinator = new 오늘작업계획Coordinator(authority);
            coordinator.Bind(sessionStableId, revision);
            var state = await authority.RefreshAsync(
                sessionStableId, lifetimeCancellation.Token);
            coordinator.Bind(sessionStableId, state.WorldRevision);
            ApplyCanonicalState(state);
            status = "오늘 목표 · 감자 300kg를 수확해 출하 준비를 시작하세요.";
        }

        public void ToggleAssignment()
        {
            assignmentKindCode = assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                ? 오늘작업계획Codes.NpcDelegated
                : 오늘작업계획Codes.PlayerDirect;
            actorStableId = assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                ? 오늘작업계획Codes.PlayerActor
                : 오늘작업계획Codes.NpcActor;
            status = assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                ? "직접 수확 · 현장에서 E로 한 Tick 작업합니다."
                : "NPC 위임 · 두 Tick 뒤 수확이 완료됩니다.";
        }

        public 오늘작업계획ItemData[] BuildPlanItems()
        {
            var state = coordinator?.CurrentState;
            var harvestLot = state?.HarvestLots.FirstOrDefault();
            if (harvestLot != null && harvestLot.StateCode == "HarvestedAtField")
                return BuildContinuationItem(harvestLot.HarvestLotStableId,
                    오늘작업계획Codes.HarvestCollection, "collection");
            if (harvestLot != null && harvestLot.StateCode == "CollectedAtYard")
                return BuildContinuationItem(harvestLot.HarvestLotStableId,
                    오늘작업계획Codes.OutboundPacking, "packing");
            if (state?.PackageLots.Any(value =>
                    value.StateCode == "PreparedForShipment") == true)
                throw new InvalidOperationException("DailyWorkPlanAlreadyPreparedForShipment");

            var draft = management.CurrentDraft
                ?? throw new InvalidOperationException("DailyWorkPlanDraftRequired");
            if (draft.ActionCode != 농장경영작업Codes.Harvest)
                throw new InvalidOperationException("DailyWorkPlanHarvestOnly");
            if (draft.TargetStableIds.Length != 1)
                throw new InvalidOperationException("DailyWorkPlanOneTargetRequired");
            return new[]
            {
                new 오늘작업계획ItemData
                {
                    PlanItemStableId = "plan-item:unity:harvest:1",
                    Priority = 10,
                    ActorStableId = actorStableId,
                    TargetStableId = draft.TargetStableIds[0],
                    ActionCode = 오늘작업계획Codes.Harvesting,
                    AssignmentKindCode = assignmentKindCode,
                    PreferredSpatialStableId = 오늘작업계획Codes.ProductionPlotSpatial,
                },
            };
        }

        public async Task PreviewAsync()
        {
            EnsureReady();
            var preview = await coordinator!.PreviewAsync(
                BuildPlanItems(), lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "미리보기 완료 · " + preview.Items.Sum(value => value.ProjectedQuantity)
                    + "kg · Enter로 오늘 작업 확정"
                : "확정 불가 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task ConfirmAsync()
        {
            EnsureReady();
            var items = BuildPlanItems();
            var state = await coordinator!.ConfirmAsync(
                "command:unity.daily-work.plan:" + coordinator.Revision,
                items, lifetimeCancellation!.Token);
            confirmedTargetIds = items.Select(value => value.TargetStableId).ToArray();
            confirmedActionCode = items[0].ActionCode;
            ApplyCanonicalState(state);
            status = confirmedActionCode == 오늘작업계획Codes.Harvesting
                && assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                ? "오늘 작업 확정 · 밭으로 걸어가 E로 직접 수확하세요."
                : "작업 확정 · E로 다음 Tick을 진행하세요.";
        }

        public async Task AdvanceOneTickAsync()
        {
            EnsureReady();
            if (confirmedTargetIds.Length == 0)
                throw new InvalidOperationException("DailyWorkPlanConfirmRequired");
            if (confirmedActionCode == 오늘작업계획Codes.Harvesting
                && assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                && !IsPlayerNearConfirmedTarget())
                throw new InvalidOperationException("DailyWorkPlanFieldInteractionRequired");
            var state = await coordinator!.AdvanceOneTickAsync(
                lifetimeCancellation!.Token);
            ApplyCanonicalState(state);
            var actionStillInProgress = state.WorkOrders.Any(value =>
                value.ActionCode == confirmedActionCode
                && value.StatusCode != "Completed");
            if (actionStillInProgress)
            {
                status = "작업 진행 · 완료까지 다음 Tick이 필요합니다.";
                return;
            }
            if (state.PackageLots.Any(value => value.StateCode == "PreparedForShipment"))
            {
                status = "출하 준비 완료 · 포장 감자 "
                    + state.PackageLots.Sum(value => value.Quantity)
                    + "kg · 저녁 결과와 카드를 확인하고 하루를 마감하세요.";
                if (evening != null)
                {
                    await evening.LoadAsync();
                    evening.SetContextVisible(true);
                }
            }
            else if (state.HarvestLots.Any(value => value.StateCode == "CollectedAtYard"))
                status = "집하 완료 · 전술 화면에서 P로 포장 계획을 미리보세요.";
            else if (state.HarvestLots.Any(value => value.StateCode == "HarvestedAtField"))
                status = "수확 완료 · 감자 " + state.HarvestLots.Sum(value => value.Quantity)
                    + "kg · 전술 화면에서 P로 집하 계획을 미리보세요.";
            confirmedTargetIds = Array.Empty<string>();
            confirmedActionCode = string.Empty;
        }

        public void ApplyCanonicalState(오늘작업CanonicalStateData state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var harvested = state.HarvestLots.Length > 0;
            foreach (var target in FindObjectsByType<농장경영선택대상View>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!state.WorkOrders.Any(value => value.TargetStableId == target.StableId))
                    continue;
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", harvested
                        ? new Color(.36f, .25f, .12f, 1f)
                        : new Color(.42f, .62f, .22f, 1f));
                    renderer.SetPropertyBlock(block);
                }
            }
            var lot = FindObjectsByType<Transform>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name == "HarvestLot_Potato_001");
            if (lot != null) lot.gameObject.SetActive(harvested);
        }

        public void SetAuthorityFailure(Exception exception)
        {
            status = "서버 연결 실패 · " + exception.Message;
            busy = false;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || busy || coordinator == null) return;
            if (keyboard.nKey.wasPressedThisFrame) ToggleAssignment();
            if (management.IsActive && keyboard.pKey.wasPressedThisFrame)
                Run(PreviewAsync);
            if (management.IsActive && keyboard.enterKey.wasPressedThisFrame)
                Run(ConfirmAsync);
            if (!management.IsActive && keyboard.eKey.wasPressedThisFrame
                && confirmedTargetIds.Length > 0)
                Run(AdvanceOneTickAsync);
        }

        private async void Run(Func<Task> action)
        {
            busy = true;
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { SetAuthorityFailure(exception); }
            finally { busy = false; }
        }

        private bool IsPlayerNearConfirmedTarget()
        {
            var targets = FindObjectsByType<농장경영선택대상View>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return targets.Where(value => confirmedTargetIds.Contains(
                    value.StableId, StringComparer.Ordinal))
                .Any(value => Vector3.Distance(player.transform.position,
                    value.transform.position) <= 8f);
        }

        private 오늘작업계획ItemData[] BuildContinuationItem(
            string targetStableId, string actionCode, string stage)
            => new[]
            {
                new 오늘작업계획ItemData
                {
                    PlanItemStableId = "plan-item:unity:" + stage + ":1",
                    Priority = 10,
                    ActorStableId = actorStableId,
                    TargetStableId = targetStableId,
                    ActionCode = actionCode,
                    AssignmentKindCode = assignmentKindCode,
                    PreferredSpatialStableId = 오늘작업계획Codes.WorkYardSpatial,
                },
            };

        private void EnsureReady()
        {
            if (coordinator == null || lifetimeCancellation == null)
                throw new InvalidOperationException("DailyWorkPlanNotInitialized");
        }

        private void OnGUI()
        {
            GUI.color = new Color(.055f, .04f, .025f, .94f);
            GUI.DrawTexture(new Rect(Screen.width - 378f, 84f, 360f, 154f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 362f, 96f, 330f, 24f),
                "기준 플레이 01 · 수확과 출하의 날");
            GUI.Label(new Rect(Screen.width - 362f, 122f, 330f, 22f),
                "담당: " + (assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                    ? "직접 수확" : "NPC 위임") + " · N 전환");
            GUI.Label(new Rect(Screen.width - 362f, 148f, 330f, 44f), status);
            GUI.Label(new Rect(Screen.width - 362f, 198f, 330f, 24f),
                management.IsActive ? "P 미리보기 · Enter 오늘 작업 확정"
                    : "1인칭 현장 접근 · E 작업 한 Tick");
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }
    }
}
