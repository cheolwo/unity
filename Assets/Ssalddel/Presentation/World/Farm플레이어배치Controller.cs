using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class Farm배치VisualEntry
    {
        public string BlueprintStableId = string.Empty;
        public string DisplayName = string.Empty;
        public string PlacementZoneStableId = string.Empty;
        public string TargetH2StableId = string.Empty;
        public string AccessConnectorStableId = string.Empty;
        public string FenceChainStableId = string.Empty;
        public bool HoldToPlaceContinuously;
        public int ContinuousSpacingCentimeters;
        public int ContinuousAnchorXCentimeters;
        public int ContinuousAnchorZCentimeters;
        public GameObject Prefab = null!;
    }

    /// <summary>
    /// 플레이어가 H2 허용 구역 안에서 H1 건설 후보를 고르는 표현 계층입니다.
    /// 비용·충돌·경사·연결 판정과 확정 상태는 authority가 반환한 결과만 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Farm플레이어배치Controller : MonoBehaviour
    {
        [SerializeField] private Camera placementCamera = null!;
        [SerializeField] private Transform farmPlanningRoot = null!;
        [SerializeField] private Transform candidateRoot = null!;
        [SerializeField] private Transform placedObjectRoot = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private LayerMask placementGroundMask = ~0;
        [SerializeField] private Farm배치VisualEntry[] visualEntries =
            Array.Empty<Farm배치VisualEntry>();
        [SerializeField] private Color pendingColor = new(1f, .72f, .1f, .55f);
        [SerializeField] private Color validColor = new(.18f, .85f, .36f, .58f);
        [SerializeField] private Color blockedColor = new(.95f, .18f, .14f, .58f);

        private readonly Dictionary<string, GameObject> placedInstances =
            new(StringComparer.Ordinal);
        private Farm플레이어배치Coordinator? coordinator;
        private CancellationTokenSource? lifetimeCancellation;
        private GameObject? candidateInstance;
        private Farm배치VisualEntry? selectedEntry;
        private Farm배치CandidateData? lastContinuousConfirmedCandidate;
        private readonly Farm연속배치Stroke continuousStroke = new();
        private bool candidateLocked;
        private bool busy;
        private bool continuousPlacementRunning;

        public bool PlacementModeActive { get; private set; }
        public bool CandidateLocked => candidateLocked;
        public bool Busy => busy;
        public bool ContinuousStrokeActive => continuousStroke.Active;
        public string Status { get; private set; } = "배치 모드 대기";
        public Farm배치CandidateData? CurrentCandidate
            => coordinator?.CurrentCandidate?.Clone();
        public Farm배치PreviewData? CurrentPreview => coordinator?.CurrentPreview;
        public Farm배치ContextData? CurrentContext => coordinator?.Current;
        public int BlueprintCount => visualEntries.Length;

        public string BlueprintDisplayName(int index)
            => index >= 0 && index < visualEntries.Length
                ? visualEntries[index].DisplayName
                : throw new ArgumentOutOfRangeException(nameof(index));

        public void SelectBlueprintAt(int index)
        {
            if (index < 0 || index >= visualEntries.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            SelectBlueprint(visualEntries[index].BlueprintStableId);
        }

        public void Configure(Camera camera, Transform planningRoot,
            Transform previewRoot, Transform canonicalRoot,
            플레이어경관Controller playerController,
            Farm배치VisualEntry[] entries, LayerMask groundMask)
        {
            placementCamera = camera;
            farmPlanningRoot = planningRoot;
            candidateRoot = previewRoot;
            placedObjectRoot = canonicalRoot;
            player = playerController;
            visualEntries = entries ?? Array.Empty<Farm배치VisualEntry>();
            placementGroundMask = groundMask;
        }

        public bool ValidateWiring()
            => placementCamera != null && farmPlanningRoot != null
               && candidateRoot != null && placedObjectRoot != null
               && player != null && visualEntries.Length > 0
               && visualEntries.All(value => value != null
                   && !string.IsNullOrWhiteSpace(value.BlueprintStableId)
                   && !string.IsNullOrWhiteSpace(value.PlacementZoneStableId)
                   && !string.IsNullOrWhiteSpace(value.TargetH2StableId)
                   && (!value.HoldToPlaceContinuously
                       || value.ContinuousSpacingCentimeters >=
                       Farm플레이어배치Codes.FineNudgeCentimeters)
                   && value.Prefab != null)
               && visualEntries.Select(value => value.BlueprintStableId)
                   .Distinct(StringComparer.Ordinal).Count() == visualEntries.Length;

        public async Task InitializeAsync(IFarm플레이어배치AuthorityClient authority,
            string sessionStableId, CancellationToken cancellationToken)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("FarmPlacementWiringInvalid");
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            coordinator = new Farm플레이어배치Coordinator(authority);
            var context = await coordinator.InitializeAsync(sessionStableId,
                lifetimeCancellation.Token);
            RenderCanonical(context);
            Status = "Farm H1 배치 준비 · 목록에서 배치 객체를 선택하세요.";
        }

        public void EnterPlacementMode()
        {
            EnsureReady();
            PlacementModeActive = true;
            player.SetPlacementInputBlocked(true);
            Status = "배치 모드 · 후보 선택 후 지면을 클릭하세요.";
        }

        public void ExitPlacementMode()
        {
            PlacementModeActive = false;
            player?.SetPlacementInputBlocked(false);
            CancelCandidate();
            Status = "배치 모드 종료";
        }

        public void SelectBlueprint(string blueprintStableId)
        {
            EnsureReady();
            EndContinuousStroke();
            selectedEntry = visualEntries.SingleOrDefault(value =>
                    value.BlueprintStableId == blueprintStableId)
                ?? throw new InvalidOperationException(
                    "FarmPlacementBlueprintVisualUnknown:" + blueprintStableId);
            DestroyCandidateVisual();
            candidateInstance = Instantiate(selectedEntry.Prefab, candidateRoot);
            candidateInstance.name = "FarmPlacementCandidate_"
                + selectedEntry.DisplayName.Replace(" ", string.Empty);
            candidateInstance.transform.localScale = Vector3.one;
            foreach (var collider in candidateInstance
                         .GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            coordinator!.SelectCandidate(new Farm배치CandidateData
            {
                BlueprintStableId = selectedEntry.BlueprintStableId,
                PlacementZoneStableId = selectedEntry.PlacementZoneStableId,
                TargetH2StableId = selectedEntry.TargetH2StableId,
                AccessConnectorStableId = selectedEntry.AccessConnectorStableId,
                FenceChainStableId = selectedEntry.FenceChainStableId,
            });
            candidateLocked = false;
            ApplyCandidateVisual(pendingColor);
            Status = selectedEntry.HoldToPlaceContinuously
                ? selectedEntry.DisplayName
                  + " 선택 · 왼쪽 버튼을 누른 채 드래그하면 연속 배치합니다."
                : selectedEntry.DisplayName + " 선택 · 지면 위치를 고르세요.";
        }

        public void SetCandidateFromWorldPoint(Vector3 worldPoint, bool lockCandidate)
        {
            EnsureCandidate();
            var local = farmPlanningRoot.InverseTransformPoint(worldPoint);
            var candidate = coordinator!.CurrentCandidate!;
            candidate.LocalXCentimeters = SnapCoordinate(local.x,
                selectedEntry?.ContinuousAnchorXCentimeters ?? 0);
            candidate.LocalZCentimeters = SnapCoordinate(local.z,
                selectedEntry?.ContinuousAnchorZCentimeters ?? 0);
            if (selectedEntry?.HoldToPlaceContinuously == true
                && continuousStroke.Active
                && lastContinuousConfirmedCandidate != null)
                candidate = Farm연속배치Grid.NextAdjacent(
                    lastContinuousConfirmedCandidate, candidate,
                    selectedEntry.ContinuousSpacingCentimeters);
            coordinator.SelectCandidate(candidate);
            candidateLocked = lockCandidate;
            ApplyCandidateTransform();
            ApplyCandidateVisual(pendingColor);
            Status = lockCandidate
                ? "후보 위치 고정 · 서버 Preview를 요청하세요."
                : "후보 위치 탐색 중";
        }

        public void RotateCandidate()
        {
            EnsureCandidate();
            EndContinuousStroke();
            coordinator!.RotateCandidate();
            ApplyCandidateTransform();
            ApplyCandidateVisual(pendingColor);
            Status = "후보 90도 회전 · Preview를 다시 요청하세요.";
        }

        public void NudgeCandidate(int deltaXCentimeters, int deltaZCentimeters)
        {
            EnsureCandidate();
            coordinator!.NudgeCandidate(deltaXCentimeters, deltaZCentimeters);
            candidateLocked = true;
            ApplyCandidateTransform();
            ApplyCandidateVisual(pendingColor);
            Status = "후보 25cm 미세 조정 · Preview를 다시 요청하세요.";
        }

        public async Task PreviewAsync()
        {
            EnsureCandidate();
            if (!candidateLocked)
                throw new InvalidOperationException("FarmPlacementCandidateNotLocked");
            await RunBusy(async token =>
            {
                var preview = await coordinator!.PreviewAsync(token);
                ApplyCandidateTransform();
                ApplyCandidateVisual(preview.CanConfirm ? validColor : blockedColor);
                Status = preview.CanConfirm
                    ? BuildCostText(preview)
                    : "배치 차단 · " + string.Join(", ",
                        preview.BlockingReasonCodes);
            });
        }

        public async Task ConfirmAsync()
        {
            EnsureCandidate();
            if (coordinator!.CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("FarmPlacementPreviewRequired");
            await RunBusy(async token =>
            {
                var commandId = "command:unity.farm-placement:"
                    + coordinator.Current!.WorldRevision + ":"
                    + coordinator.CurrentPreview!.PlacementProposalStableId;
                var context = await coordinator.ConfirmAsync(commandId, token);
                RenderCanonical(context);
                DestroyCandidateVisual();
                candidateLocked = false;
                selectedEntry = null;
                Status = "배치 확정 · 건설 Planned 상태를 재조회했습니다.";
            });
        }

        public async Task AdvanceOneTickAsync()
        {
            EnsureReady();
            await RunBusy(async token =>
            {
                var context = await coordinator!.AdvanceOneTickAsync(token);
                RenderCanonical(context);
                Status = "WorldTick 적용 · 건설 상태를 다시 조회했습니다.";
            });
        }

        public void CancelCandidate()
        {
            EndContinuousStroke();
            coordinator?.ClearCandidate();
            selectedEntry = null;
            candidateLocked = false;
            DestroyCandidateVisual();
        }

        private void Update()
        {
            if (!PlacementModeActive || coordinator == null) return;
            var mouse = Mouse.current;
            if (continuousStroke.Active
                && mouse?.leftButton.isPressed != true)
                EndContinuousStroke();
            if (busy) return;
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true)
            {
                ExitPlacementMode();
                return;
            }
            if (keyboard?.rKey.wasPressedThisFrame == true
                && coordinator.CurrentCandidate != null)
                RotateCandidate();
            if (mouse == null || candidateInstance == null
                || EventSystem.current?.IsPointerOverGameObject() == true) return;
            var continuous = selectedEntry?.HoldToPlaceContinuously == true;
            if (!continuous && candidateLocked) return;
            var ray = placementCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 500f, placementGroundMask,
                    QueryTriggerInteraction.Ignore)) return;
            SetCandidateFromWorldPoint(hit.point, !continuous
                && mouse.leftButton.wasPressedThisFrame);
            if (!continuous) return;
            if (mouse.leftButton.wasPressedThisFrame)
                BeginContinuousStroke();
            if (!mouse.leftButton.isPressed || continuousPlacementRunning
                || coordinator.CurrentCandidate == null
                || !continuousStroke.TryReserve(coordinator.CurrentCandidate)) return;
            PlaceContinuousCandidateAsync();
        }

        private async void PlaceContinuousCandidateAsync()
        {
            if (continuousPlacementRunning || coordinator?.CurrentCandidate == null
                || selectedEntry?.HoldToPlaceContinuously != true) return;
            continuousPlacementRunning = true;
            candidateLocked = true;
            try
            {
                await RunBusy(async token =>
                {
                    var preview = await coordinator.PreviewAsync(token);
                    ApplyCandidateTransform();
                    ApplyCandidateVisual(preview.CanConfirm
                        ? validColor : blockedColor);
                    if (!preview.CanConfirm)
                    {
                        Status = "연속 배치 차단 · " + string.Join(", ",
                            preview.BlockingReasonCodes);
                        return;
                    }

                    var repeatCandidate = coordinator.CurrentCandidate!.Clone();
                    var commandId = "command:unity.farm-continuous-placement:"
                        + coordinator.Current!.WorldRevision + ":"
                        + preview.PlacementProposalStableId;
                    var context = await coordinator.ConfirmAsync(commandId, token);
                    RenderCanonical(context);
                    lastContinuousConfirmedCandidate = repeatCandidate.Clone();
                    coordinator.SelectCandidate(repeatCandidate);
                    ApplyCandidateTransform();
                    ApplyCandidateVisual(pendingColor);
                    Status = selectedEntry!.DisplayName
                        + " 연속 배치 예약 · 누른 채 다음 칸으로 이동하세요.";
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Status = "연속 배치 오류 · " + exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                candidateLocked = false;
                continuousPlacementRunning = false;
            }
        }

        private async Task RunBusy(Func<CancellationToken, Task> action)
        {
            if (busy) throw new InvalidOperationException("FarmPlacementBusy");
            busy = true;
            try
            {
                var token = lifetimeCancellation?.Token
                    ?? CancellationToken.None;
                await action(token);
            }
            finally { busy = false; }
        }

        private void RenderCanonical(Farm배치ContextData context)
        {
            var visible = new HashSet<string>(context.Objects.Select(value =>
                value.FacilityStableId), StringComparer.Ordinal);
            foreach (var stale in placedInstances.Keys.Where(value =>
                         !visible.Contains(value)).ToArray())
            {
                Destroy(placedInstances[stale]);
                placedInstances.Remove(stale);
            }
            foreach (var item in context.Objects)
            {
                if (!placedInstances.TryGetValue(item.FacilityStableId,
                        out var instance))
                {
                    var entry = visualEntries.SingleOrDefault(value =>
                        value.BlueprintStableId == item.BlueprintStableId);
                    if (entry == null) continue;
                    instance = Instantiate(entry.Prefab, placedObjectRoot);
                    instance.name = "FarmPlaced_" + item.FacilityStableId
                        .Replace(":", "_");
                    placedInstances.Add(item.FacilityStableId, instance);
                }
                ApplyLocalTransform(instance.transform, item.LocalXCentimeters,
                    item.LocalZCentimeters, item.RotationQuarterTurns);
            }
        }

        private void ApplyCandidateTransform()
        {
            if (candidateInstance == null || coordinator?.CurrentCandidate == null)
                return;
            var candidate = coordinator.CurrentCandidate;
            ApplyLocalTransform(candidateInstance.transform,
                candidate.LocalXCentimeters, candidate.LocalZCentimeters,
                candidate.RotationQuarterTurns);
        }

        private void ApplyLocalTransform(Transform target, int xCentimeters,
            int zCentimeters, int rotationQuarterTurns)
        {
            var local = new Vector3(xCentimeters / 100f, 0f,
                zCentimeters / 100f);
            target.position = farmPlanningRoot.TransformPoint(local);
            target.rotation = farmPlanningRoot.rotation
                * Quaternion.Euler(0f, rotationQuarterTurns * 90f, 0f);
        }

        private void ApplyCandidateVisual(Color color)
        {
            if (candidateInstance == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            foreach (var renderer in candidateInstance
                         .GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(block);
        }

        private static string BuildCostText(Farm배치PreviewData preview)
            => "배치 가능 · 재료 " + string.Join(", ",
                   preview.MaterialRequirements.Select(value =>
                       value.ItemCode + " " + value.Quantity + value.UnitCode))
               + " · 작업 " + preview.ConstructionTicks + " Tick · Confirm 필요";

        private int SnapCoordinate(float value, int anchorCentimeters)
        {
            var centimeters = Mathf.RoundToInt(value * 100f);
            if (selectedEntry?.HoldToPlaceContinuously == true)
                return Farm연속배치Grid.SnapToAnchor(centimeters,
                    anchorCentimeters,
                    selectedEntry.ContinuousSpacingCentimeters);
            return Farm연속배치Grid.SnapToAnchor(centimeters, 0,
                Farm플레이어배치Codes.DefaultSnapCentimeters);
        }

        private void EndContinuousStroke()
        {
            if (!continuousStroke.Active) return;
            continuousStroke.End();
            lastContinuousConfirmedCandidate = null;
            if (selectedEntry?.HoldToPlaceContinuously == true)
                Status = selectedEntry.DisplayName
                    + " 연속 배치 종료 · 다시 누르면 새 배치를 시작합니다.";
        }

        private void BeginContinuousStroke()
        {
            continuousStroke.Begin();
            lastContinuousConfirmedCandidate = null;
        }

        private void EnsureReady()
        {
            if (coordinator?.Current == null)
                throw new InvalidOperationException("FarmPlacementNotInitialized");
        }

        private void EnsureCandidate()
        {
            EnsureReady();
            if (coordinator?.CurrentCandidate == null || candidateInstance == null)
                throw new InvalidOperationException("FarmPlacementCandidateMissing");
        }

        private void DestroyCandidateVisual()
        {
            if (candidateInstance == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(candidateInstance);
            else DestroyImmediate(candidateInstance);
            candidateInstance = null;
        }

        private void OnDestroy()
        {
            player?.SetPlacementInputBlocked(false);
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }
    }
}
