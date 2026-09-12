using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public enum MindPresentationIntensity
    {
        Full,
        Reduced,
        Minimal,
    }

    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 오늘작업계획Presenter : MonoBehaviour
    {
        [SerializeField] private 농장경영시점Controller management = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 턴마감Presenter evening = null!;
        [SerializeField] private bool presentationOnly = true;
        [SerializeField] private bool hudVisible = true;
        [SerializeField] private Key coopContributionKey = Key.Digit2;

        private 오늘작업계획Coordinator? coordinator;
        private I오늘작업계획AuthorityClient? dailyAuthority;
        private Farm창고건설Coordinator? warehouseCoordinator;
        private Nature심상Coordinator? natureMindCoordinator;
        private AreaSet접근Coordinator? areaAccessCoordinator;
        private Hosted비콘Coordinator? hostedBeaconCoordinator;
        private 협동창고Coordinator? coopStorageCoordinator;
        private CancellationTokenSource? lifetimeCancellation;
        private string assignmentKindCode = 오늘작업계획Codes.PlayerDirect;
        private string actorStableId = 오늘작업계획Codes.PlayerActor;
        private string[] confirmedTargetIds = Array.Empty<string>();
        private string confirmedActionCode = string.Empty;
        private 정착지상호작용Presenter? farmChoicePresenter;
        private bool farmChoicePanelOpen;
        private bool hostedBeaconOpen;
        private string farmSaveReloadStatus = string.Empty;
        private bool busy;
        private string status = "전술 화면에서 수확할 밭을 선택하세요.";
        private MindPresentationIntensity mindPresentationIntensity =
            MindPresentationIntensity.Full;

        public string AssignmentKindCode => assignmentKindCode;
        public string Status => status;
        public bool PresentationOnly => presentationOnly;
        public bool HudVisible => hudVisible;
        public 오늘작업계획PreviewData? CurrentPreview => coordinator?.CurrentPreview;
        public 오늘작업CanonicalStateData? CurrentState => coordinator?.CurrentState;
        public bool HostedBeaconOpen => hostedBeaconOpen;
        public bool HostedOverlayPausesWorld => false;
        public Key CoopContributionKey => coopContributionKey;
        public MindPresentationIntensity MindIntensity => mindPresentationIntensity;

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

        public void SetHudVisible(bool visible) => hudVisible = visible;

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
            dailyAuthority = authority;
            coordinator.Bind(sessionStableId, revision);
            var state = await authority.RefreshAsync(
                sessionStableId, lifetimeCancellation.Token);
            coordinator.ApplyCanonicalState(state);
            ApplyCanonicalState(state);
            status = state.HarvestLots.Any(value => value.StateCode == "CollectedAtYard")
                ? "집하 완료 · 감자 수확 Lot을 선택해 비축·Hub·Town 판로를 미리보세요."
                : "오늘 목표 · 감자 300kg를 수확해 출하 준비를 시작하세요.";
        }

        public async Task InitializeFarmWarehouseAsync(
            IFarm창고건설AuthorityClient authority)
        {
            EnsureReady();
            warehouseCoordinator = new Farm창고건설Coordinator(authority);
            await warehouseCoordinator.InitializeAsync(
                coordinator!.SessionStableId, lifetimeCancellation!.Token);
        }

        public async Task InitializeNatureMindAsync(
            INature심상AuthorityClient authority, string playerStableId)
        {
            EnsureReady();
            natureMindCoordinator = new Nature심상Coordinator(authority);
            await natureMindCoordinator.InitializeAsync(
                coordinator!.SessionStableId, playerStableId,
                lifetimeCancellation!.Token);
        }

        public async Task InitializeAreaAccessAsync(
            IAreaSet접근AuthorityClient authority, string playerStableId)
        {
            EnsureReady();
            areaAccessCoordinator = new AreaSet접근Coordinator(authority);
            await areaAccessCoordinator.InitializeAsync(
                coordinator!.SessionStableId, playerStableId,
                lifetimeCancellation!.Token);
        }

        public async Task InitializeHostedBeaconAsync(
            IHosted비콘AuthorityClient authority)
        {
            EnsureReady();
            hostedBeaconCoordinator = new Hosted비콘Coordinator(authority);
            await hostedBeaconCoordinator.InitializeAsync(
                coordinator!.SessionStableId, lifetimeCancellation!.Token);
        }

        public async Task InitializeCoopStorageAsync(I협동창고AuthorityClient authority)
        {
            EnsureReady();
            coopStorageCoordinator = new 협동창고Coordinator(authority);
            await coopStorageCoordinator.InitializeAsync(coordinator!.SessionStableId,
                lifetimeCancellation!.Token);
        }

        public void ToggleHostedBeacon() => hostedBeaconOpen = !hostedBeaconOpen;

        public void CycleMindPresentationIntensity()
            => mindPresentationIntensity = mindPresentationIntensity switch
            {
                MindPresentationIntensity.Full => MindPresentationIntensity.Reduced,
                MindPresentationIntensity.Reduced => MindPresentationIntensity.Minimal,
                _ => MindPresentationIntensity.Full,
            };

        public async Task PreviewOpenHostedAsync()
        {
            EnsureHostedReady();
            var preview = await hostedBeaconCoordinator!.PreviewOpenAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "Hosted 비콘 공개 미리보기 · 초대형 · 1 Tick"
                : "Hosted 공개 차단 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task ConfirmOpenHostedAsync()
        {
            EnsureHostedReady();
            await hostedBeaconCoordinator!.ConfirmOpenAsync(lifetimeCancellation!.Token);
            status = "Hosted 비콘 공개 확정 · World Tick을 진행하세요.";
        }

        public async Task PreviewJoinHostedAsync()
        {
            EnsureHostedReady();
            var preview = await hostedBeaconCoordinator!.PreviewJoinAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm ? "초대 손님 참여 미리보기 · 1 Tick"
                : "참여 차단 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task ConfirmJoinHostedAsync()
        {
            EnsureHostedReady();
            await hostedBeaconCoordinator!.ConfirmJoinAsync(lifetimeCancellation!.Token);
            status = "손님 참여 확정 · World Tick 뒤 Farm 권한을 받습니다.";
        }

        public async Task PreviewHostedGuestWorkAsync()
        {
            EnsureHostedReady();
            var preview = await hostedBeaconCoordinator!.PreviewGuestActionAsync(
                Hosted비콘Codes.PerformWork, lifetimeCancellation!.Token);
            status = preview.CanConfirm ? "손님 Farm 작업 미리보기 · 명시 확정 필요"
                : "손님 작업 차단 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task PreviewHostedGuestBuildAsync()
        {
            EnsureHostedReady();
            var preview = await hostedBeaconCoordinator!.PreviewGuestActionAsync(
                Hosted비콘Codes.Build, lifetimeCancellation!.Token);
            status = preview.CanConfirm ? "손님 건설 미리보기"
                : "손님 건설 거부 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task ConfirmHostedGuestActionAsync()
        {
            EnsureHostedReady();
            await hostedBeaconCoordinator!.ConfirmGuestActionAsync(
                lifetimeCancellation!.Token);
            status = "손님 작업 확정 · 서버 감사 원장 반영 전 Tick 대기";
        }

        public async Task AdvanceHostedAsync()
        {
            EnsureHostedReady();
            var hosted = await hostedBeaconCoordinator!.AdvanceAsync(
                lifetimeCancellation!.Token);
            status = hosted.Guest?.ParticipantStateCode == Hosted비콘Codes.Active
                ? "Hosted 세계 진행 · 손님 권한/감사 원장을 재조회했습니다."
                : "Hosted 세계 공개 · 초대 손님의 참여를 기다립니다.";
        }

        public async Task PreviewCoopContributionAsync()
        {
            EnsureCoopReady();
            var preview = await coopStorageCoordinator!.PreviewNextContributionAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "[2] 공동 창고 기여 미리보기 · " + preview.PlayerStableId
                  + " · " + preview.AcceptedQuantity + "개 · "
                  + preview.ProjectedStageCode
                : "공동 건설 차단 · " + string.Join(", ",
                    preview.BlockingReasonCodes);
        }

        public async Task ConfirmCoopContributionAsync()
        {
            EnsureCoopReady();
            await coopStorageCoordinator!.ConfirmAsync(lifetimeCancellation!.Token);
            status = "공동 창고 기여 확정 · 실제 ResourceLot 차감 전 Tick 대기";
        }

        public async Task AdvanceCoopAsync()
        {
            EnsureCoopReady();
            var state = await coopStorageCoordinator!.AdvanceAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterCoopChangeAsync();
            status = state.Project?.StageCode == 협동창고Codes.Operational
                ? "공동 창고 완공 · 보관 Capability와 WI-FARM-STORE-01 개방"
                : state.Project?.StageCode == 협동창고Codes.Removed
                    ? "공동 창고 철거 적용 · 기록은 유지 · 복원 가능"
                    : "공동 창고 공정 적용 · " + (state.Project?.StageCode ?? "Planned");
        }

        public async Task PreviewCoopProtectedAsync(string action)
        {
            EnsureCoopReady();
            var preview = await coopStorageCoordinator!.PreviewProtectedAsync(action,
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? (action == 협동창고Codes.Restore
                    ? "복원 미리보기 · 기존 Effect 보존 · 보상 Effect 적용"
                    : "철거 미리보기 · 대상 보호점이 Confirm 직전에 생성됩니다.")
                : "보호 동작 차단 · " + string.Join(", ", preview.BlockingReasonCodes);
        }

        public async Task ConfirmCoopProtectedAsync()
        {
            EnsureCoopReady();
            await coopStorageCoordinator!.ConfirmProtectedAsync(lifetimeCancellation!.Token);
            status = "보호 동작 확정 · World Tick으로 적용하세요.";
        }

        public async Task PreviewHubAccessAsync()
        {
            EnsureReady();
            var preview = await areaAccessCoordinator!.PreviewHubAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "Hub 이동 미리보기 · 출하 실적 확인 · 1 Tick"
                : "Hub 이동 잠김 · " + string.Join(", ",
                    preview.BlockingReasonCodes);
        }

        public async Task ConfirmHubAccessAsync()
        {
            EnsureReady();
            await areaAccessCoordinator!.ConfirmHubAsync(
                lifetimeCancellation!.Token);
            status = "Hub Connector 이동 확정 · World Tick을 진행하세요.";
        }

        public async Task AdvanceHubAccessAsync()
        {
            EnsureReady();
            var access = await areaAccessCoordinator!.AdvanceToHubAsync(
                lifetimeCancellation!.Token);
            status = access.CurrentAreaSetStableId == AreaSet접근Codes.HubAreaSet
                ? "Hub 도착 · 새 작업 WI-MFG-01이 열렸습니다."
                : "Hub 이동 진행 중";
        }

        public async Task PreviewFarmWarehouseAsync()
        {
            EnsureWarehouseReady();
            var preview = await warehouseCoordinator!.PreviewAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "작은 농장 창고 미리보기 · 자재 2개 · 2 Tick · 용량 +500kg"
                : "창고 건설 불가 · " + string.Join(", ",
                    preview.BlockingReasonCodes);
        }

        public async Task PreviewFarmWarehouseResourceAsync()
        {
            EnsureWarehouseReady();
            var preview = await warehouseCoordinator!.PreviewResourceAsync(
                lifetimeCancellation!.Token);
            status = preview.CanConfirm
                ? "건설 자원 확보 미리보기 · Hub 원재료 2개 → 시설 부품 2개"
                : "건설 자원 확보 불가 · " + string.Join(", ",
                    preview.BlockingReasonCodes);
        }

        public async Task ConfirmFarmWarehouseResourceAsync()
        {
            EnsureWarehouseReady();
            var context = await warehouseCoordinator!.ConfirmResourceAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterWarehouseChangeAsync(context);
            status = "건설 자원 확보 확정 · Hub 제작과 검수를 Tick으로 진행하세요.";
        }

        public async Task AdvanceFarmWarehouseResourceAsync()
        {
            EnsureWarehouseReady();
            var context = await warehouseCoordinator!.AdvanceOneTickAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterWarehouseChangeAsync(context);
            status = context.ResourceStateCode == Farm창고건설Codes.ResourceReady
                ? "시설 부품 2개 확보 완료 · 작은 창고를 건설할 수 있습니다."
                : "시설 부품 제작·검수 진행 · 다음 Tick이 필요합니다.";
        }

        public async Task ConfirmFarmWarehouseAsync()
        {
            EnsureWarehouseReady();
            var context = await warehouseCoordinator!.ConfirmAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterWarehouseChangeAsync(context);
            status = "창고 건설 확정 · 서버 원장 Planned · Tick을 진행하세요.";
        }

        public async Task AdvanceFarmWarehouseAsync()
        {
            EnsureWarehouseReady();
            var context = await warehouseCoordinator!.AdvanceOneTickAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterWarehouseChangeAsync(context);
            status = context.StateCode == Farm창고건설Codes.Operational
                ? "작은 농장 창고 운영 시작 · 저장 용량 2,500kg · 비축 선택이 열렸습니다."
                : "창고 공사 진행 · 다음 Tick에 운영을 시작합니다.";
        }

        public async Task ConfirmFarmChoiceAsync()
        {
            if (farmChoicePresenter == null)
                throw new InvalidOperationException("DailyWorkPlanFarmChoicePresenterMissing");
            await farmChoicePresenter.ConfirmAsync();
            await RefreshAfterFarmChoiceChangeAsync();
        }

        public async Task AdvanceFarmChoiceToCompletionAsync()
        {
            if (farmChoicePresenter == null)
                throw new InvalidOperationException("DailyWorkPlanFarmChoicePresenterMissing");
            await farmChoicePresenter.AdvanceToCompletionAsync();
            await RefreshAfterFarmChoiceChangeAsync();
            farmChoicePanelOpen = false;
            farmChoicePresenter.SetFarmChoiceCardExplicitlyOpen(false);
            status = "판로 결과 적용 완료 · 다음 수확이나 시설 작업을 선택하세요.";
        }

        public async Task SaveAndReloadFarmLoopAsync()
        {
            EnsureWarehouseReady();
            var result = await warehouseCoordinator!.SaveAndReloadAsync(
                lifetimeCancellation!.Token);
            farmSaveReloadStatus = "저장·재로드 일치 · "
                + result.SchemaVersion + " · hash "
                + result.ReplayHash.Substring(0,
                    Math.Min(12, result.ReplayHash.Length));
            status = farmSaveReloadStatus;
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

        public void PrepareHarvestDraft()
        {
            EnsureReady();
            if (!management.IsActive)
            {
                player.EnterFarmManagementMode();
                status = "농장 경영 화면으로 이동 중 · 전환 뒤 수확 초안을 다시 선택하세요.";
                return;
            }
            var completedHarvestTargets = coordinator!.CurrentState?.WorkOrders
                .Where(value => value.ActionCode == 오늘작업계획Codes.Harvesting)
                .Select(value => value.TargetStableId)
                .ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);
            var target = management.Targets
                .Where(value => value.TargetKindCode == 농장경영대상종류Codes.Plot
                    && value.Allows(농장경영작업Codes.Harvest)
                    && !completedHarvestTargets.Contains(value.StableId))
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("DailyWorkPlanNoRemainingHarvestTarget");
            management.SelectAction(농장경영작업Codes.Harvest);
            management.SelectTarget(target, false);
            management.CreateWorkDraft(target.transform.position);
            status = "감자밭 수확 초안 · 미리보기 뒤 명시적으로 확정하세요.";
        }

        public async Task OpenFarmChoiceCardAsync()
        {
            EnsureReady();
            if (!RequiresFarmChoice())
                throw new InvalidOperationException("DailyWorkPlanFarmChoiceNotReady");
            var interaction = FindFirstObjectByType<정착지상호작용Presenter>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("DailyWorkPlanFarmChoicePresenterMissing");
            player.ExitPlayerMode();
            interaction.SetFarmChoiceCardExplicitlyOpen(true);
            await interaction.RefreshFarm선택ContextAsync();
            farmChoicePresenter = interaction;
            farmChoicePanelOpen = true;
            status = "실제 감자 수확 Lot 선택 · 서버 판로 카드를 확인하세요.";
        }

        public 오늘작업계획ItemData[] BuildPlanItems()
        {
            var state = coordinator?.CurrentState;
            var harvestLot = state?.HarvestLots
                .Where(value => value.StateCode == "HarvestedAtField")
                .OrderBy(value => value.HarvestLotStableId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (harvestLot != null)
                return BuildContinuationItem(harvestLot.HarvestLotStableId,
                    오늘작업계획Codes.HarvestCollection, "collection");
            if (RequiresFarmChoice())
                throw new InvalidOperationException("DailyWorkPlanFarmChoiceRequired");
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
                    PlanItemStableId = "plan-item:unity:harvest:"
                        + StableIdSuffix(draft.TargetStableIds[0]),
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
            if (RequiresFarmChoice())
            {
                status = "집하 완료 · 감자 수확 Lot을 선택해 비축·Hub·Town 판로를 미리보세요.";
                return;
            }
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
            if (RequiresFarmChoice())
            {
                status = "집하 완료 · 포장 전에 감자 수확 Lot의 판로를 먼저 확정하세요.";
                return;
            }
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
            await RefreshNatureMindAsync();
            await RefreshAreaAccessAsync();
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
                status = "집하 완료 · 감자 수확 Lot을 선택해 비축·Hub·Town 판로를 미리보세요.";
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
            var lots = FindObjectsByType<SimulationWorldNavigationTargetView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.ObjectStableId
                        == 정착지상호작용Presenter.수확LotObjectStableId)
                .ToArray();
            foreach (var lot in lots)
                lot.gameObject.SetActive(harvested);
        }

        public void SetAuthorityFailure(Exception exception)
        {
            status = "서버 연결 실패 · " + exception.Message;
            busy = false;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || coordinator == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) ToggleHostedBeacon();
            if (busy) return;
            if (hostedBeaconOpen && keyboard[coopContributionKey].wasPressedThisFrame
                && coopStorageCoordinator?.Current != null
                && coopStorageCoordinator.Current.Project?.StageCode
                    != 협동창고Codes.Operational)
                Run(PreviewCoopContributionAsync);
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

        private bool RequiresFarmChoice()
        {
            var hasCollectedLot = coordinator?.CurrentState?.HarvestLots.Any(value =>
                value.StateCode == "CollectedAtYard") == true;
            if (!hasCollectedLot) return false;
            return farmChoicePresenter == null
                || farmChoicePresenter.FarmChoiceSituationStateCode
                    == Farm선택Codes.AwaitingChoice;
        }

        private static string StableIdSuffix(string stableId)
            => new string((stableId ?? string.Empty)
                .Select(value => char.IsLetterOrDigit(value) ? value : '-')
                .ToArray());

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

        private void EnsureWarehouseReady()
        {
            EnsureReady();
            if (warehouseCoordinator?.Current == null)
                throw new InvalidOperationException("FarmStorageNotInitialized");
        }

        private void EnsureHostedReady()
        {
            EnsureReady();
            if (hostedBeaconCoordinator?.Current == null)
                throw new InvalidOperationException("HostedWorldNotInitialized");
        }

        private void EnsureCoopReady()
        {
            EnsureReady();
            if (coopStorageCoordinator?.Current == null)
                throw new InvalidOperationException("CoopConstructionNotInitialized");
        }

        private async Task SynchronizeAfterCoopChangeAsync()
        {
            if (dailyAuthority != null)
            {
                var state = await dailyAuthority.RefreshAsync(coordinator!.SessionStableId,
                    lifetimeCancellation!.Token);
                coordinator.ApplyCanonicalState(state);
                ApplyCanonicalState(state);
            }
            if (hostedBeaconCoordinator != null)
                await hostedBeaconCoordinator.RefreshAsync(lifetimeCancellation!.Token);
            if (warehouseCoordinator != null)
                await warehouseCoordinator.RefreshAsync(lifetimeCancellation!.Token);
        }

        private async Task SynchronizeAfterWarehouseChangeAsync(
            Farm창고건설ContextData context)
        {
            if (dailyAuthority == null)
                throw new InvalidOperationException("DailyWorkPlanAuthorityMissing");
            var state = await dailyAuthority.RefreshAsync(
                coordinator!.SessionStableId, lifetimeCancellation!.Token);
            if (state.WorldRevision != context.WorldRevision)
                throw new InvalidOperationException("FarmStorageCanonicalRevisionMismatch");
            coordinator.ApplyCanonicalState(state);
            ApplyCanonicalState(state);
            if (farmChoicePresenter != null)
                await farmChoicePresenter.RefreshFarm선택ContextAsync();
            await RefreshNatureMindAsync();
        }

        private async Task RefreshNatureMindAsync()
        {
            if (natureMindCoordinator != null)
                await natureMindCoordinator.RefreshAsync(
                    lifetimeCancellation!.Token);
        }

        private async Task RefreshAreaAccessAsync()
        {
            if (areaAccessCoordinator != null)
                await areaAccessCoordinator.RefreshAsync(
                    lifetimeCancellation!.Token);
        }

        private async Task RefreshAfterFarmChoiceChangeAsync()
        {
            EnsureWarehouseReady();
            var context = await warehouseCoordinator!.RefreshAsync(
                lifetimeCancellation!.Token);
            await SynchronizeAfterWarehouseChangeAsync(context);
        }

        private void OnGUI()
        {
            if (!hudVisible) return;
            if (hostedBeaconOpen)
            {
                DrawHostedBeaconPanel();
                DrawCoopStoragePanel();
            }
            DrawNatureMindPanel();
            DrawFarmWarehousePanel();
            DrawAreaAccessPanel();
            if (RequiresFarmChoice())
            {
                if (farmChoicePanelOpen && farmChoicePresenter != null)
                {
                    DrawFarmChoicePanel();
                    return;
                }
                GUI.enabled = !busy && coordinator != null;
                if (GUI.Button(new Rect(Screen.width * .5f - 120f,
                        Screen.height - 50f, 240f, 32f), "감자 판로 카드 열기"))
                    Run(OpenFarmChoiceCardAsync);
                GUI.enabled = true;
                return;
            }

            GUI.color = new Color(.055f, .04f, .025f, .94f);
            GUI.DrawTexture(new Rect(Screen.width - 378f, 84f, 360f, 220f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 362f, 96f, 330f, 24f),
                "기준 플레이 01 · 수확과 출하의 날");
            GUI.Label(new Rect(Screen.width - 362f, 122f, 330f, 22f),
                "담당: " + (assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                    ? "직접 수확" : "NPC 위임") + " · N 전환");
            GUI.Label(new Rect(Screen.width - 362f, 148f, 330f, 44f), status);
            GUI.Label(new Rect(Screen.width - 362f, 198f, 330f, 24f),
                RequiresFarmChoice()
                    ? "수확 Lot 선택 · 비축/Hub/Town 판로 미리보기"
                    : management.IsActive ? "P 미리보기 · Enter 오늘 작업 확정"
                        : "1인칭 현장 접근 · E 작업 한 Tick");

            GUI.enabled = !busy && coordinator != null && !RequiresFarmChoice();
            if (GUI.Button(new Rect(Screen.width - 362f, 226f, 104f, 28f),
                    "수확 초안"))
                PrepareHarvestDraft();
            if (GUI.Button(new Rect(Screen.width - 250f, 226f, 104f, 28f),
                    "미리보기"))
                Run(PreviewAsync);
            if (GUI.Button(new Rect(Screen.width - 138f, 226f, 104f, 28f),
                    "작업 확정"))
                Run(ConfirmAsync);

            GUI.enabled = !busy && coordinator != null
                && confirmedTargetIds.Length > 0;
            if (GUI.Button(new Rect(Screen.width - 250f, 262f, 216f, 28f),
                    "다음 Tick 진행"))
                Run(AdvanceOneTickAsync);
            GUI.enabled = !busy && coordinator != null;
            if (GUI.Button(new Rect(Screen.width - 362f, 262f, 104f, 28f),
                    assignmentKindCode == 오늘작업계획Codes.PlayerDirect
                        ? "NPC 위임" : "직접 수확"))
                ToggleAssignment();
            GUI.enabled = true;
        }

        private void DrawFarmChoicePanel()
        {
            var presenter = farmChoicePresenter!;
            var x = Screen.width - 558f;
            GUI.color = new Color(.035f, .05f, .06f, .97f);
            GUI.DrawTexture(new Rect(x, 20f, 540f, 286f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 18f, 34f, 504f, 24f),
                "감자 수확 Lot · 서버 판로 결정");
            GUI.Label(new Rect(x + 18f, 62f, 504f, 42f),
                "원장 " + presenter.ActualFarmHarvestLotStableId
                + "\n상태 " + presenter.FarmChoiceSituationStateCode
                + " · 단계 " + presenter.PhaseCode
                + " · revision은 서버 문맥에서 재조회");
            GUI.Label(new Rect(x + 18f, 108f, 504f, 62f),
                string.IsNullOrWhiteSpace(presenter.PreviewSummary)
                    ? "판로를 선택하면 서버 Preview가 표시됩니다."
                    : presenter.PreviewSummary);

            var canChoose = !busy
                && presenter.FarmChoiceSituationStateCode == Farm선택Codes.AwaitingChoice;
            GUI.enabled = canChoose && presenter.CanSelectFarmChoice(
                HarvestDispositionChoiceCodes.ReserveStorage);
            if (GUI.Button(new Rect(x + 18f, 178f, 158f, 34f), "비축 보관"))
                Run(() => presenter.SelectChoiceAsync(
                    HarvestDispositionChoiceCodes.ReserveStorage));
            GUI.enabled = canChoose && presenter.CanSelectFarmChoice(
                HarvestDispositionChoiceCodes.CooperativeShipment);
            if (GUI.Button(new Rect(x + 190f, 178f, 158f, 34f), "Hub 출하"))
                Run(() => presenter.SelectChoiceAsync(
                    HarvestDispositionChoiceCodes.CooperativeShipment));
            GUI.enabled = canChoose && presenter.CanSelectFarmChoice(
                HarvestDispositionChoiceCodes.DirectOnlineSale);
            if (GUI.Button(new Rect(x + 364f, 178f, 158f, 34f), "Town 직거래"))
                Run(() => presenter.SelectChoiceAsync(
                    HarvestDispositionChoiceCodes.DirectOnlineSale));

            GUI.enabled = !busy
                && presenter.PhaseCode == 정착지상호작용PhaseCodes.PreviewReady;
            if (GUI.Button(new Rect(x + 18f, 226f, 244f, 42f),
                    "판로 확정 · 서버 Confirm"))
                Run(ConfirmFarmChoiceAsync);
            GUI.enabled = !busy
                && presenter.PhaseCode == 정착지상호작용PhaseCodes.TaskReserved;
            if (GUI.Button(new Rect(x + 278f, 226f, 244f, 42f),
                    "WORLD TICK · 결과 적용"))
                Run(AdvanceFarmChoiceToCompletionAsync);
            GUI.enabled = true;
            GUI.Label(new Rect(x + 18f, 272f, 504f, 24f),
                "Unity 전송: ChoiceStableId + ExpectedRevision · 실제 판매/배송 없음");
        }

        private void DrawFarmWarehousePanel()
        {
            var context = warehouseCoordinator?.Current;
            if (context == null) return;
            var x = 18f;
            var y = Screen.height - 210f;
            GUI.color = new Color(.06f, .075f, .055f, .95f);
            GUI.DrawTexture(new Rect(x, y, 348f, 190f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14f, y + 12f, 320f, 24f),
                "농장 시설 · 작은 창고");
            GUI.Label(new Rect(x + 14f, y + 38f, 320f, 42f),
                "상태 " + context.StateCode + " · "
                + context.StorageOccupied + "/" + context.StorageCapacity
                + context.StorageUnitCode
                + " · 예약 " + context.StorageReserved + context.StorageUnitCode
                + " · 여유 " + context.StorageAvailable + context.StorageUnitCode);
            GUI.Label(new Rect(x + 14f, y + 72f, 320f, 32f),
                "건설 자원 " + context.ResourceStateCode + " · 시설 부품 "
                + context.FacilityComponentQuantity + "개");
            GUI.Label(new Rect(x + 14f, y + 94f, 320f, 22f),
                context.StateCode == Farm창고건설Codes.Operational
                    ? "운영 중 · 비축 판로가 새 용량을 사용합니다."
                    : "서버 Blueprint · 자재 2개 · 2 Tick · +500kg");

            GUI.enabled = !busy;
            if (context.StateCode == Farm창고건설Codes.NotStarted)
            {
                if (context.ResourceStateCode == Farm창고건설Codes.ResourceAvailable)
                {
                    var resourcePreviewReady = warehouseCoordinator!
                        .CurrentResourcePreview?.CanConfirm == true;
                    if (GUI.Button(new Rect(x + 14f, y + 118f, 154f, 26f),
                            "자원 확보 미리보기"))
                        Run(PreviewFarmWarehouseResourceAsync);
                    GUI.enabled = !busy && resourcePreviewReady;
                    if (GUI.Button(new Rect(x + 180f, y + 118f, 154f, 26f),
                            "자원 확보 확정"))
                        Run(ConfirmFarmWarehouseResourceAsync);
                }
                else if (context.ResourceStateCode
                         == Farm창고건설Codes.ResourceProducing)
                {
                    if (GUI.Button(new Rect(x + 14f, y + 118f, 320f, 26f),
                            "자원 제작·검수 Tick"))
                        Run(AdvanceFarmWarehouseResourceAsync);
                }
                else if (context.ResourceStateCode
                         == Farm창고건설Codes.ResourceReady)
                {
                    var previewReady = warehouseCoordinator!
                        .CurrentPreview?.CanConfirm == true;
                    if (GUI.Button(new Rect(x + 14f, y + 118f, 154f, 26f),
                            "건설 미리보기"))
                        Run(PreviewFarmWarehouseAsync);
                    GUI.enabled = !busy && previewReady;
                    if (GUI.Button(new Rect(x + 180f, y + 118f, 154f, 26f),
                            "건설 확정"))
                        Run(ConfirmFarmWarehouseAsync);
                }
            }
            else if (context.StateCode == Farm창고건설Codes.Planned
                     || context.StateCode == Farm창고건설Codes.Building)
            {
                if (GUI.Button(new Rect(x + 14f, y + 118f, 320f, 26f),
                        "공사 Tick 진행"))
                    Run(AdvanceFarmWarehouseAsync);
            }
            else if (context.StateCode == Farm창고건설Codes.Operational)
            {
                if (GUI.Button(new Rect(x + 14f, y + 118f, 320f, 26f),
                        "G4 상태 저장 · 재로드 검증"))
                    Run(SaveAndReloadFarmLoopAsync);
                GUI.Label(new Rect(x + 14f, y + 150f, 320f, 28f),
                    string.IsNullOrWhiteSpace(farmSaveReloadStatus)
                        ? "서버 save/replay hash로 영속성을 확인합니다."
                        : farmSaveReloadStatus);
            }
            GUI.enabled = true;
        }

        private void DrawNatureMindPanel()
        {
            var mind = natureMindCoordinator?.Current;
            if (mind == null) return;
            var x = 18f;
            var y = Screen.height - 388f;
            GUI.color = mindPresentationIntensity == MindPresentationIntensity.Minimal
                ? new Color(.075f, .075f, .09f, .98f)
                : mind.PeriodStateCode == "DarkAgePeriod"
                ? new Color(.19f, .045f, .035f, .96f)
                : mind.PeriodStateCode == "GwangbokPeriod"
                    ? new Color(.035f, .15f, .12f, .96f)
                    : mind.InterpretationBandCode == "RecoveryDominant"
                ? new Color(.035f, .12f, .095f, .95f)
                : mind.InterpretationBandCode == "ThreatDominant"
                    ? new Color(.17f, .055f, .04f, .95f)
                    : new Color(.075f, .075f, .09f, .95f);
            GUI.DrawTexture(new Rect(x, y, 348f, 164f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14f, y + 10f, 320f, 22f),
                "심리 영역 · 내 Nature 해석 · " + mindPresentationIntensity);
            GUI.Label(new Rect(x + 14f, y + 34f, 320f, 22f),
                "회복 " + decimal.Round(mind.RecoveryShare * 100m, 1)
                + "% · 위협 " + decimal.Round(mind.ThreatShare * 100m, 1)
                + "% · " + mind.InterpretationBandCode);
            GUI.Label(new Rect(x + 14f, y + 58f, 320f, 36f),
                mind.InferenceText);
            var periodName = mind.PeriodStateCode == "GwangbokPeriod" ? "광복기"
                : mind.PeriodStateCode == "DarkAgePeriod" ? "암흑기" : "평시";
            var periodBadge = mind.PeriodStateCode == "GwangbokPeriod" ? "[회복 충만]"
                : mind.PeriodStateCode == "DarkAgePeriod" ? "[위협 압박]" : "[균형]";
            GUI.Label(new Rect(x + 14f, y + 98f, 320f, 22f),
                periodBadge + " 기간 " + periodName + " · 회복 작업 "
                + mind.RecoveryWorkDurationTicks + " Tick · 보정 "
                + mind.WorkDurationModifierTicks);
            GUI.Label(new Rect(x + 14f, y + 122f, 320f, 34f),
                "[사실] 공유 Fact " + decimal.Round(mind.FactValue * 100m, 1)
                + "% · 값 변경 없음 · " + mind.MoodProjectionCode);
            if (GUI.Button(new Rect(x + 250f, y + 8f, 84f, 22f), "강도 전환"))
                CycleMindPresentationIntensity();
        }

        private void DrawAreaAccessPanel()
        {
            var access = areaAccessCoordinator?.Current;
            if (access == null) return;
            var hub = access.Hub;
            var x = Screen.width - 378f;
            var y = 318f;
            GUI.color = hub.AccessStateCode == AreaSet접근Codes.Entered
                ? new Color(.035f, .12f, .15f, .95f)
                : hub.AccessStateCode == AreaSet접근Codes.Granted
                    ? new Color(.1f, .1f, .035f, .95f)
                    : new Color(.075f, .065f, .065f, .95f);
            GUI.DrawTexture(new Rect(x, y, 360f, 232f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14f, y + 10f, 330f, 22f),
                "세계 확장 · Farm → Hub");
            GUI.Label(new Rect(x + 14f, y + 34f, 330f, 38f),
                "접근 " + hub.AccessStateCode + " · 현재 "
                + (access.CurrentAreaSetStableId == AreaSet접근Codes.HubAreaSet
                    ? "Hub" : "Farm")
                + "\n근거 " + (hub.GrantedByEvidenceIds.Length == 0
                    ? "감자 Hub 출하 실적 필요"
                    : string.Join(", ", hub.GrantedByEvidenceIds)));
            var previewReady = areaAccessCoordinator!.CurrentPreview?.CanConfirm == true;
            GUI.enabled = !busy && hub.AccessStateCode == AreaSet접근Codes.Granted
                && !previewReady;
            if (GUI.Button(new Rect(x + 14f, y + 80f, 104f, 30f), "이동 미리보기"))
                Run(PreviewHubAccessAsync);
            GUI.enabled = !busy && previewReady
                && access.CurrentAreaSetStableId != AreaSet접근Codes.HubAreaSet;
            if (GUI.Button(new Rect(x + 128f, y + 80f, 104f, 30f), "이동 확정"))
                Run(ConfirmHubAccessAsync);
            GUI.enabled = !busy && previewReady
                && access.CurrentAreaSetStableId != AreaSet접근Codes.HubAreaSet;
            if (GUI.Button(new Rect(x + 242f, y + 80f, 104f, 30f), "이동 Tick"))
                Run(AdvanceHubAccessAsync);
            GUI.enabled = true;
            GUI.Label(new Rect(x + 14f, y + 118f, 330f, 102f),
                hub.AccessStateCode == AreaSet접근Codes.Entered
                    ? "새로 열린 세계 행동\n"
                      + 한국어세계상호작용목록(hub.AvailableWorldInteractionIds)
                    : "H 정적 정의 불변 · 서버 Connector 통과 검증");
        }

        public static string 한국어세계상호작용목록(
            IEnumerable<string> worldInteractionIds)
        {
            var 한국어표시명들 = (worldInteractionIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(Simulation세계상호작용이름Catalog.한국어표시명)
                .ToArray();
            return 한국어표시명들.Length == 0
                ? "열린 세계 행동 없음"
                : string.Join("\n", 한국어표시명들);
        }

        public static string 한국어음양수행주체분류(
            SimulationWI음양주체분류Snapshot? 상태사본)
        {
            if (상태사본 == null
                || string.IsNullOrWhiteSpace(상태사본.사분면Code))
                return "음양·수행주체 미분류";
            if (상태사본.사분면Code ==
                SimulationWI사분면Codes.NotApplicable)
                return "사분면 적용 제외";
            if (상태사본.사분면Code ==
                SimulationWI사분면Codes.Unclassified)
                return "음양·수행주체 판정 대기";

            var 이름 = 상태사본.사분면Code ==
                SimulationWI사분면Codes.YangPlayer ? "양·플레이어"
                : 상태사본.사분면Code ==
                    SimulationWI사분면Codes.YangNpc ? "양·NPC"
                : 상태사본.사분면Code ==
                    SimulationWI사분면Codes.YinPlayer ? "음·플레이어"
                : 상태사본.사분면Code ==
                    SimulationWI사분면Codes.YinNpc ? "음·NPC"
                : "음양·수행주체 미분류";
            return string.IsNullOrWhiteSpace(상태사본.사분면기호)
                ? 이름
                : 상태사본.사분면기호 + " · " + 이름;
        }

        private void DrawHostedBeaconPanel()
        {
            var hosted = hostedBeaconCoordinator?.Current;
            if (hosted == null) return;
            var preview = hostedBeaconCoordinator!.CurrentPreview;
            var x = Screen.width * .5f - 250f;
            var y = 34f;
            GUI.color = new Color(.025f, .045f, .075f, .98f);
            GUI.DrawTexture(new Rect(x, y, 500f, 232f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 16f, y + 12f, 468f, 24f),
                "HOSTED 비콘 · ESC 닫기 · 세계 Tick은 계속됩니다");
            GUI.Label(new Rect(x + 16f, y + 38f, 468f, 42f),
                "모드 " + hosted.SessionModeCode + " · 손님 "
                + (hosted.Guest?.ParticipantStateCode ?? "미초대")
                + " · 서버 개정 " + hosted.WorldRevision
                + "\nFarm 작업 " + hosted.Grant(Hosted비콘Codes.PerformWork)
                + " · 건설 " + hosted.Grant(Hosted비콘Codes.Build)
                + " · 철거 " + hosted.Grant(Hosted비콘Codes.Demolish));
            GUI.Label(new Rect(x + 16f, y + 84f, 468f, 24f),
                preview == null ? "먼저 미리보기를 선택하세요."
                    : preview.ActionCode + " · " + preview.CapabilityCode
                      + " · " + (preview.CanConfirm ? "확정 가능" : "차단")
                      + " · " + string.Join(", ", preview.BlockingReasonCodes));

            GUI.enabled = !busy && hosted.SessionModeCode == Hosted비콘Codes.Solo;
            if (GUI.Button(new Rect(x + 16f, y + 116f, 108f, 30f), "비콘 미리보기"))
                Run(PreviewOpenHostedAsync);
            GUI.enabled = !busy && preview?.CanConfirm == true
                && preview.ActionCode == "OpenHostedWorld";
            if (GUI.Button(new Rect(x + 132f, y + 116f, 108f, 30f), "비콘 확정"))
                Run(ConfirmOpenHostedAsync);
            GUI.enabled = !busy && preview?.CanConfirm == true;
            if (GUI.Button(new Rect(x + 248f, y + 116f, 108f, 30f), "World Tick"))
                Run(AdvanceHostedAsync);
            GUI.enabled = !busy && hosted.SessionModeCode == Hosted비콘Codes.HostedMultiplayer
                && hosted.Guest?.ParticipantStateCode == Hosted비콘Codes.Invited;
            if (GUI.Button(new Rect(x + 364f, y + 116f, 120f, 30f), "손님 참여 보기"))
                Run(PreviewJoinHostedAsync);

            GUI.enabled = !busy && preview?.CanConfirm == true
                && preview.ActionCode == "JoinHostedWorld";
            if (GUI.Button(new Rect(x + 16f, y + 154f, 108f, 30f), "손님 참여 확정"))
                Run(ConfirmJoinHostedAsync);
            GUI.enabled = !busy && hosted.Guest?.ParticipantStateCode == Hosted비콘Codes.Active;
            if (GUI.Button(new Rect(x + 132f, y + 154f, 108f, 30f), "작업 미리보기"))
                Run(PreviewHostedGuestWorkAsync);
            if (GUI.Button(new Rect(x + 248f, y + 154f, 108f, 30f), "건설 권한 확인"))
                Run(PreviewHostedGuestBuildAsync);
            GUI.enabled = !busy && preview?.CanConfirm == true
                && preview.CapabilityCode == Hosted비콘Codes.PerformWork;
            if (GUI.Button(new Rect(x + 364f, y + 154f, 120f, 30f), "손님 작업 확정"))
                Run(ConfirmHostedGuestActionAsync);
            GUI.enabled = true;
            GUI.Label(new Rect(x + 16f, y + 194f, 468f, 24f),
                "감사 기록 " + hosted.AuditTrail.Length
                + "건 · Host 이탈 시 변경 차단 · Simulation 전용");
        }

        private void DrawCoopStoragePanel()
        {
            var state = coopStorageCoordinator?.Current;
            if (state == null) return;
            var project = state.Project;
            var preview = coopStorageCoordinator!.CurrentPreview;
            var x = Screen.width * .5f - 250f;
            var y = 274f;
            GUI.color = new Color(.055f, .075f, .035f, .98f);
            GUI.DrawTexture(new Rect(x, y, 500f, 186f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 16f, y + 10f, 468f, 24f),
                "G5 공동 창고 · [" + coopContributionKey
                + "] 유효 재료 한 묶음 미리보기");
            GUI.Label(new Rect(x + 16f, y + 34f, 468f, 38f),
                "단계 " + (project?.StageCode ?? "Planned")
                + " · 진행 " + decimal.Round((project?.ProgressValue ?? 0m) * 100m, 0)
                + "% · 기여자 " + state.Contributions.Length
                + "명\n실제 Lot 잔량 " + state.SourceLots.Where(value =>
                    value.LotStableId == 협동창고Codes.OwnerLot
                    || value.LotStableId == 협동창고Codes.GuestLot)
                    .Sum(value => value.RemainingQuantity) + "개");
            GUI.Label(new Rect(x + 16f, y + 76f, 468f, 22f),
                preview == null ? "Preview → Confirm → World Tick"
                    : preview.ActionCode + " · "
                      + (preview.CanConfirm ? "확정 가능" : "차단")
                      + " · " + preview.ProjectedStageCode);
            GUI.enabled = !busy && project?.StageCode != 협동창고Codes.Operational
                && project?.StageCode != 협동창고Codes.Removed;
            if (GUI.Button(new Rect(x + 16f, y + 106f, 108f, 28f), "[2] 기여 보기"))
                Run(PreviewCoopContributionAsync);
            GUI.enabled = !busy && preview?.CanConfirm == true
                && preview.ActionCode == 협동창고Codes.Contribution;
            if (GUI.Button(new Rect(x + 132f, y + 106f, 108f, 28f), "기여 확정"))
                Run(ConfirmCoopContributionAsync);
            GUI.enabled = !busy && preview?.CanConfirm == true;
            if (GUI.Button(new Rect(x + 248f, y + 106f, 108f, 28f), "World Tick"))
                Run(AdvanceCoopAsync);
            GUI.enabled = !busy && project?.StageCode == 협동창고Codes.Operational;
            if (GUI.Button(new Rect(x + 364f, y + 106f, 120f, 28f), "철거 보호 보기"))
                Run(() => PreviewCoopProtectedAsync(협동창고Codes.Demolition));
            GUI.enabled = !busy && project?.StageCode == 협동창고Codes.Removed;
            if (GUI.Button(new Rect(x + 16f, y + 142f, 108f, 28f), "복원 보기"))
                Run(() => PreviewCoopProtectedAsync(협동창고Codes.Restore));
            GUI.enabled = !busy && preview?.CanConfirm == true
                && (preview.ActionCode == 협동창고Codes.Demolition
                    || preview.ActionCode == 협동창고Codes.Restore);
            if (GUI.Button(new Rect(x + 132f, y + 142f, 108f, 28f), "보호 동작 확정"))
                Run(ConfirmCoopProtectedAsync);
            GUI.enabled = true;
            GUI.Label(new Rect(x + 248f, y + 144f, 236f, 24f),
                "보호점 " + state.ProtectionCheckpoints.Length
                + " · 복원 Effect " + state.RestoreEffects.Length);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }
    }
}
