using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using Ssalddel.Unity.PresentationContracts;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed partial class Nature감각표현Presenter
    {
        private readonly 벌목TaskObservationProjector woodcuttingProjector = new();
        private readonly HashSet<string> consumedWoodcuttingCues = new(StringComparer.Ordinal);
        private readonly HashSet<string> consumedWoodcuttingCompletions = new(StringComparer.Ordinal);
        private 벌목AnimationAdapter woodcuttingAdapter;
        private 공용AnimationAdapter woodcuttingCommon;
        private Animator woodcuttingAnimator;
        private string woodcuttingLeaseToken;
        private bool woodcuttingLeaseHeld;
        private bool woodcuttingReleasePending;
        private bool woodcuttingNeedsBaseline = true;
        private bool woodcuttingReconstructionPending = true;
        private bool woodcuttingTickAllowed;
        private bool woodcuttingReturning;
        private long woodcuttingEpoch;
        private long woodcuttingSourceEpoch = -1;
        private string woodcuttingSession = string.Empty;
        private string woodcuttingPlayer = string.Empty;
        private 벌목TaskTerminalEvidence pendingWoodcuttingCompletion;
        private string pendingWoodcuttingRecordHash = string.Empty;
        private long pendingWoodcuttingGeneration;
        private Transform preparedTreeFallVisual;
        private long preparedTreeFallGeneration;
        private Transform activeTreeFallVisual;
        private Coroutine activeTreeFallRoutine;
        private bool woodcuttingClockPaused;
        public string WoodcuttingDiagnostic { get; private set; } = "NotObserved";
        public bool WoodcuttingHasOwnership => woodcuttingLeaseHeld;
        public long WoodcuttingObservationEpoch => woodcuttingEpoch;

        // Configure/재활성화/권위 연결 교체의 수명을 읽기 revision과 분리한다.
        private void InvalidateWoodcuttingObservation()
        {
            woodcuttingReconstructionPending = true;
            DropWoodcuttingCompletion();
            ClearPreparedTreeFall();
            ClearActiveTreeFall();
            ReleaseWoodcuttingOwnership();
        }

        private bool PrepareWoodcuttingBindings()
        {
            try { return PrepareWoodcuttingBindingsCore(); }
            catch (Exception exception) { return WoodcuttingFailure("BindingException:" + exception.GetType().Name); }
        }

        private bool PrepareWoodcuttingBindingsCore()
        {
            var animator = player.VisualRoot.GetComponentInChildren<Animator>(true);
            if (animator == null) return WoodcuttingFailure("AnimatorMissing");
            if (woodcuttingAnimator == animator && woodcuttingAdapter != null
                && woodcuttingAdapter.Animator == animator && woodcuttingAdapter.ReadState().Configured
                && woodcuttingCommon != null && woodcuttingCommon.Animator == animator)
                return true;
            if (!ReleaseWoodcuttingOwnership()) return false;
            if (!RetirePreviousWoodcuttingAdapter(animator)) return false;
            woodcuttingReconstructionPending = true;
            woodcuttingAnimator = animator;
            woodcuttingCommon = player.GetComponentsInChildren<공용AnimationAdapter>(true)
                .FirstOrDefault(value => value.Animator == animator);
            var sources = Resources.Load<벌목AnimationSources>(벌목AnimationSources.ResourceKey);
            if (woodcuttingCommon == null || sources == null)
                return WoodcuttingFailure("CommonLeaseOrSourcesMissing");
            var existing = animator.GetComponent<벌목AnimationAdapter>();
            if (existing != null && existing != woodcuttingAdapter)
                return WoodcuttingFailure("ForeignWoodcuttingAdapterPresent");
            if (existing != null && existing.ReadState().HasOwnership)
                return WoodcuttingFailure("ExistingAdapterOwnershipUnresolved");
            woodcuttingAdapter = existing ?? animator.gameObject.AddComponent<벌목AnimationAdapter>();
            if (!woodcuttingAdapter.Configure(animator, sources))
                return WoodcuttingFailure(woodcuttingAdapter.ReadState().Diagnostic);
            woodcuttingLeaseToken ??= "nature-woodcutting-presenter:" + Guid.NewGuid().ToString("N");
            woodcuttingNeedsBaseline = true;
            return true;
        }

        private bool RetirePreviousWoodcuttingAdapter(Animator nextAnimator)
        {
            if (woodcuttingAdapter == null || woodcuttingAdapter.Animator == nextAnimator) return true;
            if (woodcuttingAdapter.ReadState().HasOwnership)
            {
                WoodcuttingDiagnostic = "PreviousAdapterExternallyOwned";
                return false;
            }
            woodcuttingAdapter.Dispose();
            if (UnityEngine.Application.isPlaying) Destroy(woodcuttingAdapter);
            else DestroyImmediate(woodcuttingAdapter);
            woodcuttingAdapter = null;
            return true;
        }

        private bool RefreshWoodcutting(Nature생존State current, bool held, bool moving, long sourceEpoch)
        {
            if (sourceEpoch < 0 || (!woodcuttingReconstructionPending
                && current.SessionStableId == woodcuttingSession && current.PlayerStableId == woodcuttingPlayer
                && sourceEpoch < woodcuttingSourceEpoch))
            {
                WoodcuttingDiagnostic = "SourceEpochRegressed";
                woodcuttingTickAllowed = false;
                return false;
            }
            try { return RefreshWoodcuttingCore(current, held, moving, sourceEpoch); }
            catch (Exception exception)
            {
                if (woodcuttingCommon != null && woodcuttingCommon.IsExternalPoseOwnedBy(woodcuttingLeaseToken))
                    woodcuttingLeaseHeld = true;
                return WoodcuttingFailure("DispatchException:" + exception.GetType().Name);
            }
        }

        private bool RefreshWoodcuttingCore(Nature생존State current, bool held, bool moving, long sourceEpoch)
        {
            woodcuttingClockPaused = current.ClockPaused;
            if (woodcuttingReleasePending && !ReleaseWoodcuttingOwnership()) return false;
            bool reconstruct = woodcuttingReconstructionPending || sourceEpoch != woodcuttingSourceEpoch
                || current.SessionStableId != woodcuttingSession || current.PlayerStableId != woodcuttingPlayer;
            if (reconstruct)
            {
                if (woodcuttingEpoch == long.MaxValue) return WoodcuttingFailure("ObservationEpochOverflow");
                if (!ReleaseWoodcuttingOwnership()) return false;
                woodcuttingEpoch++;
                woodcuttingSourceEpoch = sourceEpoch;
                woodcuttingSession = current.SessionStableId;
                woodcuttingPlayer = current.PlayerStableId;
                woodcuttingReconstructionPending = false;
                DropWoodcuttingCompletion();
                // 같은 표현기의 세대는 단조 증가하며 과거 접촉을 재발행하지 않는다.
            }
            var previous = woodcuttingProjector.ReadState();
            벌목TaskTerminalEvidence evidence = null;
            string recordHash = string.Empty;
            string selectionDiagnostic = string.Empty;
            if (!reconstruct)
                벌목완료근거선택.TrySelect(current, previous, woodcuttingEpoch,
                    out evidence, out recordHash, out selectionDiagnostic);
            var projection = woodcuttingProjector.Project(current, new 벌목TaskObservationContext(
                woodcuttingEpoch, held, reconstruct, moving ? 벌목ReturnIntent.Walk : 벌목ReturnIntent.Idle, evidence));
            if (!projection.Accepted)
            {
                WoodcuttingDiagnostic = projection.Diagnostic;
                woodcuttingTickAllowed = false;
                return false;
            }
            woodcuttingTickAllowed = !current.ClockPaused && (projection.ShouldTick || woodcuttingReturning);
            if (projection.Dispatch == 벌목ProjectionDispatch.Reconstruct)
            {
                DropWoodcuttingCompletion();
                consumedWoodcuttingCues.Clear();
                consumedWoodcuttingCompletions.Clear();
                ClearPreparedTreeFall();
                ClearActiveTreeFall();
            }
            if (moving)
            {
                DropWoodcuttingCompletion();
                ClearPreparedTreeFall();
                woodcuttingNeedsBaseline = true;
                if (woodcuttingLeaseHeld && !woodcuttingReturning) woodcuttingAdapter.InterruptToIdle();
                woodcuttingReturning = woodcuttingLeaseHeld;
                woodcuttingTickAllowed = !current.ClockPaused;
                return true;
            }
            if (!woodcuttingLeaseHeld)
            {
                // 종료를 놓친 뒤 Graph를 만들어 과거 완료를 재생하지 않는다.
                if (!projection.HasActiveTask || !held) { DropWoodcuttingCompletion(); return true; }
                if (woodcuttingCommon == null || woodcuttingAdapter == null
                    || !woodcuttingCommon.TryAcquireExternalPose(woodcuttingLeaseToken, woodcuttingAnimator))
                    return WoodcuttingFailure("CommonPoseLeaseDenied");
                woodcuttingLeaseHeld = true;
                if (!woodcuttingAdapter.Acquire(woodcuttingLeaseToken))
                    return WoodcuttingFailure("WoodcuttingPoseLeaseDenied");
                woodcuttingNeedsBaseline = true;
            }
            if (woodcuttingReturning) return true;
            if (woodcuttingNeedsBaseline)
            {
                DropWoodcuttingCompletion();
                if (!woodcuttingAdapter.Reconstruct(projection.Snapshot))
                    return WoodcuttingFailure("AdapterBaselineRejected");
                woodcuttingNeedsBaseline = false;
            }
            else if (projection.Dispatch == 벌목ProjectionDispatch.Reconstruct)
            {
                if (!woodcuttingAdapter.Reconstruct(projection.Snapshot))
                    return WoodcuttingFailure("AdapterReconstructionRejected");
            }
            else if (projection.Dispatch == 벌목ProjectionDispatch.Apply)
            {
                if (!woodcuttingAdapter.ApplySnapshot(projection.Snapshot))
                    return WoodcuttingFailure("AdapterSnapshotRejected");
                if (projection.Snapshot.Phase == 벌목WorkPhase.Completed && evidence != null)
                {
                    pendingWoodcuttingCompletion = evidence;
                    pendingWoodcuttingRecordHash = recordHash;
                    pendingWoodcuttingGeneration = projection.Snapshot.ObservationGeneration;
                }
            }
            else if (projection.Dispatch == 벌목ProjectionDispatch.Interrupt)
            {
                DropWoodcuttingCompletion();
                ClearPreparedTreeFall();
                woodcuttingAdapter.InterruptToIdle();
                woodcuttingReturning = true;
                woodcuttingTickAllowed = !current.ClockPaused;
            }
            WoodcuttingDiagnostic = string.IsNullOrEmpty(selectionDiagnostic)
                ? projection.Diagnostic : projection.Diagnostic + ":" + selectionDiagnostic;
            if (projection.HasActiveTask) PrepareTreeFallVisual(projection.Snapshot);
            return true;
        }

        private bool WoodcuttingFailure(string diagnostic)
        {
            DropWoodcuttingCompletion();
            ClearPreparedTreeFall();
            woodcuttingNeedsBaseline = true;
            woodcuttingTickAllowed = false;
            WoodcuttingDiagnostic = diagnostic;
            ReleaseWoodcuttingOwnership();
            return false;
        }

        private bool ReleaseWoodcuttingOwnership()
        {
            try { return ReleaseWoodcuttingOwnershipCore(); }
            catch (Exception exception)
            {
                woodcuttingReleasePending = woodcuttingLeaseHeld;
                WoodcuttingDiagnostic = "ReleaseException:" + exception.GetType().Name;
                return false;
            }
        }

        private bool ReleaseWoodcuttingOwnershipCore()
        {
            if (!woodcuttingLeaseHeld) return true;
            if (woodcuttingAdapter != null && woodcuttingAdapter.ReadState().HasOwnership
                && !woodcuttingAdapter.Release(woodcuttingLeaseToken))
            { woodcuttingReleasePending = true; WoodcuttingDiagnostic = "SpecialistReleasePending"; return false; }
            if (woodcuttingCommon != null && !woodcuttingCommon.ReleaseExternalPose(woodcuttingLeaseToken))
            { woodcuttingReleasePending = true; WoodcuttingDiagnostic = "CommonReleasePending"; return false; }
            woodcuttingLeaseHeld = false;
            woodcuttingReleasePending = false;
            woodcuttingReturning = false;
            woodcuttingNeedsBaseline = true;
            return true;
        }

        private void TickWoodcutting(float deltaTime)
        {
            try { TickWoodcuttingCore(deltaTime); }
            catch (Exception exception) { WoodcuttingFailure("TickException:" + exception.GetType().Name); }
        }

        private void TickWoodcuttingCore(float deltaTime)
        {
            if (!woodcuttingLeaseHeld || woodcuttingReleasePending || !woodcuttingTickAllowed) return;
            if (!woodcuttingAdapter.TickPresentation(deltaTime))
            { WoodcuttingFailure("AdapterTickFailed:" + woodcuttingAdapter.ReadState().Diagnostic); return; }
            if (woodcuttingAdapter.TryConsumeContact(out var cue)) ConsumeWoodcuttingContact(cue);
            if (woodcuttingAdapter.ReadState().ReturnReady) ReleaseWoodcuttingOwnership();
        }

        private void ConsumeWoodcuttingContact(벌목ContactCue cue)
        {
            var observed = woodcuttingProjector.ReadState();
            var value = cue.Snapshot;
            if (value.ObservationGeneration != observed.ObservationGeneration
                || value.SessionStableId != woodcuttingSession || value.ActorStableId != woodcuttingPlayer
                || !consumedWoodcuttingCues.Add(cue.StableId)) return;
            // 접촉을 먼저 소비 확정해 한 채널 실패로 다른 채널을 중복 재생하지 않는다.
            PlayWoodcuttingEffects(감각표현CueCodes.AxeImpact, 감각표현CueCodes.WoodChip, value.TargetStableId, false);
            var completion = pendingWoodcuttingCompletion;
            if (value.Phase != 벌목WorkPhase.Completed || cue.ProgressToken != value.RequiredSeconds
                || completion == null || pendingWoodcuttingGeneration != value.ObservationGeneration
                || completion.SessionStableId != value.SessionStableId || completion.ActorStableId != value.ActorStableId
                || completion.TargetStableId != value.TargetStableId || completion.OriginCommandId != observed.OriginCommandId
                || string.IsNullOrEmpty(pendingWoodcuttingRecordHash)
                || !consumedWoodcuttingCompletions.Add(completion.EvidenceStableId)) return;
            DropWoodcuttingCompletion();
            PlayWoodcuttingEffects(감각표현CueCodes.TreeFall, 감각표현CueCodes.TreeFallDust, value.TargetStableId, true);
        }

        private void PlayWoodcuttingEffects(string audio, string fx, string target, bool treeFall)
        {
            try { lastAudioCueCode = audio; PlayAudio(audio, target); }
            catch (Exception exception) { WoodcuttingDiagnostic = "AudioFailed:" + exception.GetType().Name; }
            try { lastFxCueCode = fx; PlayFx(fx, target); }
            catch (Exception exception) { WoodcuttingDiagnostic = "FxFailed:" + exception.GetType().Name; }
            if (!treeFall) return;
            try
            {
                if (preparedTreeFallVisual != null)
                {
                    var pivot = preparedTreeFallVisual;
                    preparedTreeFallVisual = null;
                    pivot.gameObject.SetActive(true);
                    ClearActiveTreeFall();
                    activeTreeFallVisual = pivot;
                    activeTreeFallRoutine = StartCoroutine(AnimateOwnedTreeFall(pivot));
                }
                else WoodcuttingDiagnostic = "CompletedTreeVisualUnavailable";
            }
            catch (Exception exception) { WoodcuttingDiagnostic = "TreeFallFailed:" + exception.GetType().Name; }
        }

        private void DropWoodcuttingCompletion()
        { pendingWoodcuttingCompletion = null; pendingWoodcuttingRecordHash = string.Empty; }

        private void RebindWoodcuttingAnchors(Nature생존State current)
        {
            foreach (var source in spatialSources.Values) if (source != null) source.Stop();
            foreach (var fx in fxSystems.Values) if (fx != null) fx.Clear(true);
            foreach (var anchor in anchors)
            {
                if (anchor == null || anchor.RoleCode == 배치표현AnchorRoleCodes.ToolSocket
                    || !markers.TryGetValue(anchor.PlacementStableId, out var marker) || marker == null) continue;
                bool tree = marker.Kind == Nature생존상호작용Kind.HarvestTree;
                var position = anchor.RoleCode switch
                {
                    배치표현AnchorRoleCodes.CameraFocus => MarkerBoundsCenter(marker) + (tree ? Vector3.up * .3f : Vector3.zero),
                    배치표현AnchorRoleCodes.ActorWork => marker.transform.position - marker.transform.forward * 1.5f,
                    배치표현AnchorRoleCodes.AudioEmitter => tree ? marker.transform.position + Vector3.up * .9f : MarkerBoundsCenter(marker),
                    배치표현AnchorRoleCodes.Fx => marker.transform.position + Vector3.up * .8f,
                    _ => anchor.transform.position,
                };
                var h1 = current.ResourceNodes.FirstOrDefault(value => value.StableId == marker.StableId)?.H1StableId;
                anchor.Configure(anchor.AnchorStableId, anchor.PlacementStableId,
                    string.IsNullOrWhiteSpace(h1) ? anchor.OwningH1StableId : h1, anchor.RoleCode,
                    anchor.transform.parent.InverseTransformPoint(position));
            }
        }

        private System.Collections.IEnumerator AnimateOwnedTreeFall(Transform pivot)
        {
            float elapsed = 0f;
            while (pivot != null && elapsed < .9f)
            {
                if (!woodcuttingClockPaused) elapsed += Time.unscaledDeltaTime;
                pivot.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 84f, Mathf.Clamp01(elapsed / .9f)), 0f, 0f);
                yield return null;
            }
            if (pivot != null) Destroy(pivot.gameObject);
            activeTreeFallVisual = null;
            activeTreeFallRoutine = null;
        }

        private void ClearActiveTreeFall()
        {
            if (activeTreeFallRoutine != null) StopCoroutine(activeTreeFallRoutine);
            activeTreeFallRoutine = null;
            if (activeTreeFallVisual == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(activeTreeFallVisual.gameObject);
            else DestroyImmediate(activeTreeFallVisual.gameObject);
            activeTreeFallVisual = null;
        }

        private void PrepareTreeFallVisual(벌목AnimationSnapshot snapshot)
        {
            if (preparedTreeFallVisual != null && preparedTreeFallGeneration == snapshot.ObservationGeneration) return;
            ClearPreparedTreeFall();
            if (!markers.TryGetValue(snapshot.TargetStableId, out var marker) || marker == null
                || marker.Kind != Nature생존상호작용Kind.HarvestTree
                || marker.GetComponentsInChildren<MeshRenderer>(true).Length == 0) return;
            // 권위 완료 뒤에는 원목이 비활성/해제될 수 있어 작업 중의 표현만 복사해 숨겨 둔다.
            if (!TryCreateTreeFallVisual(marker, out preparedTreeFallVisual)) return;
            preparedTreeFallGeneration = snapshot.ObservationGeneration;
            preparedTreeFallVisual.gameObject.SetActive(false);
        }

        private void ClearPreparedTreeFall()
        {
            if (preparedTreeFallVisual == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(preparedTreeFallVisual.gameObject);
            else DestroyImmediate(preparedTreeFallVisual.gameObject);
            preparedTreeFallVisual = null;
        }
    }
}
