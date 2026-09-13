using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 실제 HTTP 검증 Runner와 분리된 동결 SyntheticFixture를 Play Mode에서 재생합니다.
    /// 운영 Controller를 일시 정지하지만 Command를 호출하거나 Scene을 저장하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 다중Os생명주기GameView검증Driver : MonoBehaviour
    {
        public const string CliCaptureCommandLineMarker =
            "-sagajeongMultiOsLifecycleCliCapture";
        public const string CaptureOwner = "SagajeongMultiOsLifecycle";
        public const string SourceMode = "FrozenPresentationTimeline";
        public const string EvidenceManifestFileName = "capture-manifest.json";

        private OperationalWorldSceneInterpreter interpreter;
        private OperationalWorldScenePlacementPlanner planner;
        private 운영관찰WorldView worldView;
        private 사가정운영디오라마View diorama;
        private 다중Os생명주기검증Layer lifecycleLayer;
        private 운영관찰WorldController liveController;
        private IDisposable captureLease;
        private string runStableId = string.Empty;
        private DateTime startedAtUtc;
        private bool liveControllerWasEnabled;
        private bool initialized;
        private bool automatic;
        private float automaticStartedAt;
        private int lastAutomaticSecond = -1;
        private Coroutine captureRoutine;

        public bool Initialized => initialized;
        public bool UsesActualHttp => false;
        public bool OperationalEffectsAllowed => false;
        public bool SceneSaveAllowed => false;
        public bool Automatic => automatic;
        public int CurrentSecond { get; private set; }
        public 다중Os생명주기검증Moment CurrentMoment { get; private set; }
        public string StatusCode { get; private set; } = "NotInitialized";
        public string EvidenceDirectory { get; private set; } = string.Empty;
        public string EvidenceCaptureFailure { get; private set; } = string.Empty;
        public bool EvidenceCaptureComplete { get; private set; }
        public int VisibleLifecycleCount => lifecycleLayer != null
            ? lifecycleLayer.StateCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SuppressUnrelatedRuntimeRootsForCliCapture()
        {
            if (!CliCaptureRequested) return;
            var suppressed = 0;
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid()) continue;
                if (behaviour is not 턴마감SceneCompositionRoot
                    && behaviour is not 오늘작업계획SceneCompositionRoot
                    && behaviour is not 운영관찰WorldController)
                    continue;
                if (!behaviour.enabled) continue;
                behaviour.enabled = false;
                suppressed++;
            }
            Debug.Log("[사가정 네 OS 생명주기] CLI 동결 캡처 격리 · "
                      + $"기존 실행 Root {suppressed}개 비활성 · 운영 Command 없음");
        }

        public static bool CliCaptureRequested
            => Environment.GetCommandLineArgs().Any(value => string.Equals(
                value, CliCaptureCommandLineMarker, StringComparison.Ordinal));

        public void Initialize(
            운영관찰WorldView observations,
            사가정운영디오라마View mapView,
            string requestedRunStableId = null,
            DateTime? requestedStartUtc = null)
        {
            if (initialized) return;
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("MultiOsLifecyclePlayModeRequired");
            worldView = observations != null
                ? observations : throw new ArgumentNullException(nameof(observations));
            diorama = mapView != null
                ? mapView : throw new ArgumentNullException(nameof(mapView));
            if (!사가정GameView검증CaptureCoordinator.TryAcquire(CaptureOwner, out captureLease))
                throw new InvalidOperationException("SagajeongCaptureAlreadyOwned:"
                                                    + 사가정GameView검증CaptureCoordinator.ActiveOwner);

            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            runStableId = string.IsNullOrWhiteSpace(requestedRunStableId)
                ? "multi-os-lifecycle:" + Guid.NewGuid().ToString("N")
                : requestedRunStableId;
            startedAtUtc = requestedStartUtc ?? DateTime.UtcNow;
            if (startedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("MultiOsLifecycleStartTimeMustBeUtc",
                    nameof(requestedStartUtc));

            liveController = GetComponent<운영관찰WorldController>();
            liveControllerWasEnabled = liveController != null && liveController.enabled;
            if (liveControllerWasEnabled) liveController.enabled = false;
            lifecycleLayer = GetComponent<다중Os생명주기검증Layer>()
                             ?? gameObject.AddComponent<다중Os생명주기검증Layer>();
            lifecycleLayer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            lifecycleLayer.Configure(worldView, diorama);
            interpreter = new OperationalWorldSceneInterpreter();
            planner = new OperationalWorldScenePlacementPlanner();
            initialized = true;
            SetMoment(다중Os생명주기검증Moment.Overview);
            StatusCode = "FrozenPresentationTimelineReady";
        }

        public void BeginAutomatic()
        {
            RequireInitialized();
            ApplySecond(0, true);
            automatic = true;
            automaticStartedAt = Time.unscaledTime;
            lastAutomaticSecond = CurrentSecond;
            StatusCode = "FrozenPresentationTimelinePlaying";
        }

        public void PauseAutomatic()
        {
            automatic = false;
            StatusCode = "FrozenPresentationTimelinePaused";
        }

        public void SetMoment(다중Os생명주기검증Moment moment)
        {
            RequireInitialized();
            automatic = false;
            CurrentMoment = moment;
            ApplySecond(다중Os생명주기검증Fixture.MomentSecond(moment), true);
            ApplyCamera(moment);
            StatusCode = "FrozenPresentationMoment:" + moment;
        }

        public void ApplySecond(int elapsedSeconds, bool resetMemory = false)
        {
            RequireInitialized();
            var second = Math.Max(0, Math.Min(
                다중Os생명주기검증Policy.DurationSeconds - 1, elapsedSeconds));
            if (resetMemory || second < CurrentSecond)
            {
                interpreter.Clear();
                worldView.Clear();
                lifecycleLayer.ClearLifecycleStates();
            }

            var response = 다중Os생명주기검증Fixture.CreateResponse(
                runStableId, second, startedAtUtc);
            var applied = interpreter.Apply(response, startedAtUtc.AddSeconds(second));
            if (!applied.Accepted)
                throw new InvalidOperationException("MultiOsLifecycleFixtureRejected:"
                                                    + applied.ErrorCode);
            var placement = planner.Create(applied);
            if (!placement.Accepted)
                throw new InvalidOperationException("MultiOsLifecyclePlacementRejected");
            worldView.Apply(placement, applied.Diagnostics);
            lifecycleLayer.Apply(placement);
            if (lifecycleLayer.StateCount != response.Items.Length)
                throw new InvalidOperationException("MultiOsLifecycleVisibleCountMismatch:"
                                                    + lifecycleLayer.StateCount + "/"
                                                    + response.Items.Length);
            CurrentSecond = second;
        }

        public bool ReloadRestaurantFixture(string absolutePath = null)
        {
            RequireInitialized();
            return lifecycleLayer.TryLoadRestaurantFixture(absolutePath);
        }

        public void BeginEvidenceCapture(string absoluteDirectory)
        {
            RequireInitialized();
            if (captureRoutine != null)
                throw new InvalidOperationException("MultiOsLifecycleCaptureAlreadyRunning");
            if (string.IsNullOrWhiteSpace(absoluteDirectory)
                || !Path.IsPathRooted(absoluteDirectory))
                throw new ArgumentException("MultiOsLifecycleCapturePathInvalid",
                    nameof(absoluteDirectory));
            EvidenceDirectory = absoluteDirectory;
            EvidenceCaptureFailure = string.Empty;
            EvidenceCaptureComplete = false;
            captureRoutine = StartCoroutine(CaptureEvidence());
        }

        private void Update()
        {
            if (!initialized || !automatic) return;
            var elapsed = Mathf.FloorToInt(Time.unscaledTime - automaticStartedAt)
                          % 다중Os생명주기검증Policy.DurationSeconds;
            if (elapsed == lastAutomaticSecond) return;
            if (elapsed < lastAutomaticSecond)
                ApplySecond(elapsed, true);
            else
                ApplySecond(elapsed);
            lastAutomaticSecond = elapsed;
        }

        private IEnumerator CaptureEvidence()
        {
            Directory.CreateDirectory(EvidenceDirectory);
            var manifestPath = Path.Combine(EvidenceDirectory, EvidenceManifestFileName);
            if (File.Exists(manifestPath))
            {
                FailCapture("MultiOsLifecycleCapturePathAlreadyExists:" + manifestPath);
                yield break;
            }
            var movementSamples = new List<MovementSampleEvidence>();
            yield return CaptureMovementSamples(movementSamples);
            if (!string.IsNullOrEmpty(EvidenceCaptureFailure)) yield break;

            var capturedMoments = new List<CaptureMomentEvidence>();
            var stages = new[]
            {
                (다중Os생명주기검증Moment.Overview, "01-overview-all-os.png"),
                (다중Os생명주기검증Moment.FoodRecovery, "02-food-recovery.png"),
                (다중Os생명주기검증Moment.WarehouseCargoMartHandoff,
                    "03-warehouse-cargo-mart-handoff.png")
            };
            foreach (var stage in stages)
            {
                var path = Path.Combine(EvidenceDirectory, stage.Item2);
                if (File.Exists(path))
                {
                    FailCapture("MultiOsLifecycleCapturePathAlreadyExists:" + path);
                    yield break;
                }
                SetMoment(stage.Item1);
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(path, 1);
                var remainingFrames = 180;
                while ((!File.Exists(path) || new FileInfo(path).Length == 0)
                       && remainingFrames-- > 0)
                    yield return null;
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    FailCapture("MultiOsLifecycleCaptureWriteTimedOut:" + path);
                    yield break;
                }
                capturedMoments.Add(CreateMomentEvidence(stage.Item1, stage.Item2));
            }
            var manifest = new CaptureManifest
            {
                fixtureRevision = 다중Os생명주기검증Policy.FixtureRevision,
                evidenceSource = SourceMode,
                movementMode = 다중Os생명주기검증Policy.MovementMode,
                liveHttpEndToEnd = false,
                operationalEffectsAllowed = false,
                routeAuthority = 다중Os생명주기검증Policy.RouteAuthority,
                sceneSaved = false,
                runStableId = runStableId,
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                restaurantIconInputStatus = lifecycleLayer.RestaurantIconInputStatus,
                restaurantIconCount = lifecycleLayer.RestaurantIconCount,
                restaurantFixtureHashSha256 = lifecycleLayer.RestaurantFixtureHash,
                movementSamples = movementSamples.ToArray(),
                moments = capturedMoments.ToArray()
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true),
                new UTF8Encoding(false));
            captureRoutine = null;
            EvidenceCaptureComplete = true;
            StatusCode = "FrozenPresentationEvidenceCaptured";
        }

        private IEnumerator CaptureMovementSamples(List<MovementSampleEvidence> samples)
        {
            var probes = new[]
            {
                new MovementProbe(OperationalWorldOperatingSystemIds.FoodDelivery,
                    "food-normal", 55, 65),
                new MovementProbe(OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                    "cargo-normal", 155, 170),
                new MovementProbe(
                    OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                    "warehouse-normal", 280, 295),
                new MovementProbe(
                    OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
                    "mart-normal", 420, 435)
            };

            // 새 Editor 프로세스의 검색/asset 초기화가 첫 보간 frame을 삼키지 않도록
            // 실제 Play Mode frame을 먼저 안정화합니다.
            yield return new WaitForSecondsRealtime(1.5f);

            foreach (var probe in probes)
            {
                ApplySecond(probe.FromSecond, true);
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                var from = FindCaseState(probe.CaseCode);
                if (!lifecycleLayer.TryGetMarkerPosition(from.ObjectStableId,
                        out var fromPosition))
                {
                    FailCapture("MultiOsLifecycleMovementFromMarkerMissing:"
                                + probe.CaseCode);
                    yield break;
                }

                ApplySecond(probe.ToSecond);
                var to = FindCaseState(probe.CaseCode);
                var marker = lifecycleLayer.RuntimeRoot
                    .GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                    .SingleOrDefault(value => string.Equals(value.ObjectStableId,
                        to.ObjectStableId, StringComparison.Ordinal));
                if (marker == null || !marker.IsInterpolating)
                {
                    FailCapture("MultiOsLifecycleMovementInterpolationMissing:"
                                + probe.CaseCode);
                    yield break;
                }

                var frames = 0;
                var targetPosition = marker.transform.parent.TransformPoint(
                    marker.TargetLocalPosition);
                var targetDistance = PlanarDistance(fromPosition, targetPosition);
                var intermediatePosition = fromPosition;
                var intermediateObserved = false;
                var closestToHalf = float.PositiveInfinity;
                while (marker.IsInterpolating && frames++ < 360)
                {
                    yield return null;
                    if (!lifecycleLayer.TryGetMarkerPosition(to.ObjectStableId,
                            out var observedPosition))
                        continue;
                    var observedDistance = PlanarDistance(fromPosition,
                        observedPosition);
                    var remaining = PlanarDistance(observedPosition, targetPosition);
                    var halfDelta = Mathf.Abs(observedDistance - targetDistance * .5f);
                    if (observedDistance > .01f && remaining > .01f
                                                   && halfDelta < closestToHalf)
                    {
                        intermediatePosition = observedPosition;
                        intermediateObserved = true;
                        closestToHalf = halfDelta;
                    }
                }
                yield return new WaitForEndOfFrame();
                if (marker.IsInterpolating
                    || !lifecycleLayer.TryGetMarkerPosition(to.ObjectStableId,
                        out var toPosition))
                {
                    FailCapture("MultiOsLifecycleMovementCompletionMissing:"
                                + probe.CaseCode);
                    yield break;
                }

                var distance = PlanarDistance(fromPosition, toPosition);
                var intermediateDistance = PlanarDistance(fromPosition,
                    intermediatePosition);
                var remainingDistance = PlanarDistance(intermediatePosition,
                    toPosition);
                if (!intermediateObserved || distance <= .01f
                                      || intermediateDistance <= .01f
                                      || remainingDistance <= .01f)
                {
                    FailCapture("MultiOsLifecycleMovementDistanceInvalid:"
                                + probe.CaseCode + ":" + distance.ToString("F3")
                                + ":" + intermediateDistance.ToString("F3")
                                + ":" + remainingDistance.ToString("F3")
                                + ":frames=" + frames);
                    yield break;
                }

                samples.Add(new MovementSampleEvidence
                {
                    operatingSystemId = probe.OperatingSystemId,
                    caseCode = probe.CaseCode,
                    objectStableId = to.ObjectStableId,
                    fromElapsedSecond = probe.FromSecond,
                    toElapsedSecond = probe.ToSecond,
                    fromStageCode = from.LifecycleStageCode,
                    toStageCode = to.LifecycleStageCode,
                    fromSemanticPlaceStableId = from.SemanticPlaceStableId,
                    toSemanticPlaceStableId = to.SemanticPlaceStableId,
                    fromPosition = PositionEvidence.From(fromPosition),
                    intermediatePosition = PositionEvidence.From(intermediatePosition),
                    toPosition = PositionEvidence.From(toPosition),
                    distance = distance,
                    intermediateDistance = intermediateDistance,
                    intermediateProgress01 = intermediateDistance / distance,
                    sampledInPlayMode = true,
                    observedFrameCount = frames
                });
            }
        }

        private 다중Os생명주기표현State FindCaseState(string caseCode)
        {
            var suffix = ":" + caseCode;
            return lifecycleLayer.CurrentStates.Single(value =>
                value.ObjectStableId.EndsWith(suffix, StringComparison.Ordinal));
        }

        private static float PlanarDistance(Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ApplyCamera(다중Os생명주기검증Moment moment)
        {
            diorama.Show(true);
            if (moment == 다중Os생명주기검증Moment.Overview)
            {
                diorama.ShowWhole();
                return;
            }
            if (moment == 다중Os생명주기검증Moment.FoodRecovery)
            {
                lifecycleLayer.FocusCase("food-recovery");
                return;
            }

            var targets = lifecycleLayer.CurrentStates
                .Where(value => value.OperatingSystemId
                                != OperationalWorldOperatingSystemIds.FoodDelivery)
                .Select(value => lifecycleLayer.TryGetMarkerPosition(
                    value.ObjectStableId, out var position) ? (Vector3?)position : null)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToArray();
            if (targets.Length == 0) return;
            var center = targets.Aggregate(Vector3.zero, (sum, value) => sum + value)
                         / targets.Length;
            diorama.FocusPoint(center);
        }

        private void FailCapture(string error)
        {
            EvidenceCaptureFailure = error;
            StatusCode = "FrozenPresentationEvidenceCaptureFailed";
            captureRoutine = null;
        }

        private void RequireInitialized()
        {
            if (!initialized)
                throw new InvalidOperationException("MultiOsLifecycleDriverNotInitialized");
        }

        private void OnDestroy()
        {
            if (captureRoutine != null) StopCoroutine(captureRoutine);
            captureRoutine = null;
            automatic = false;
            lifecycleLayer?.ClearLifecycleStates();
            worldView?.Clear();
            interpreter?.Clear();
            captureLease?.Dispose();
            captureLease = null;
            initialized = false;
            if (UnityEngine.Application.isPlaying && liveController != null
                                                  && liveControllerWasEnabled)
                liveController.enabled = true;
        }

        private CaptureMomentEvidence CreateMomentEvidence(
            다중Os생명주기검증Moment moment, string fileName)
            => new()
            {
                moment = moment.ToString(),
                fileName = fileName,
                elapsedSecond = 다중Os생명주기검증Fixture.MomentSecond(moment),
                visibleLifecycleCount = lifecycleLayer.StateCount,
                states = lifecycleLayer.CurrentStates.Select(value =>
                    value.ObjectStableId + "|" + value.OperatingSystemId + "|"
                    + value.LifecycleStageCode + "|" + value.ActivityCode + "|"
                    + value.AttentionStateCode + "|r" + value.Revision).ToArray()
            };

        [Serializable]
        private sealed class CaptureManifest
        {
            public string schema = "sagajeong-multi-os-lifecycle-game-view-evidence.v1";
            public string fixtureRevision = string.Empty;
            public string evidenceSource = string.Empty;
            public string movementMode = string.Empty;
            public bool liveHttpEndToEnd;
            public bool operationalEffectsAllowed;
            public bool routeAuthority;
            public bool sceneSaved;
            public string scenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
            public string runStableId = string.Empty;
            public string capturedAtUtc = string.Empty;
            public string restaurantIconInputStatus = string.Empty;
            public int restaurantIconCount;
            public string restaurantFixtureHashSha256 = string.Empty;
            public MovementSampleEvidence[] movementSamples =
                Array.Empty<MovementSampleEvidence>();
            public CaptureMomentEvidence[] moments = Array.Empty<CaptureMomentEvidence>();
        }

        private readonly struct MovementProbe
        {
            public MovementProbe(string operatingSystemId, string caseCode,
                int fromSecond, int toSecond)
            {
                OperatingSystemId = operatingSystemId;
                CaseCode = caseCode;
                FromSecond = fromSecond;
                ToSecond = toSecond;
            }

            public string OperatingSystemId { get; }
            public string CaseCode { get; }
            public int FromSecond { get; }
            public int ToSecond { get; }
        }

        [Serializable]
        private sealed class MovementSampleEvidence
        {
            public string operatingSystemId = string.Empty;
            public string caseCode = string.Empty;
            public string objectStableId = string.Empty;
            public int fromElapsedSecond;
            public int toElapsedSecond;
            public string fromStageCode = string.Empty;
            public string toStageCode = string.Empty;
            public string fromSemanticPlaceStableId = string.Empty;
            public string toSemanticPlaceStableId = string.Empty;
            public PositionEvidence fromPosition = new();
            public PositionEvidence intermediatePosition = new();
            public PositionEvidence toPosition = new();
            public float distance;
            public float intermediateDistance;
            public float intermediateProgress01;
            public bool sampledInPlayMode;
            public int observedFrameCount;
        }

        [Serializable]
        private sealed class PositionEvidence
        {
            public float x;
            public float y;
            public float z;

            public static PositionEvidence From(Vector3 value)
                => new() { x = value.x, y = value.y, z = value.z };
        }

        [Serializable]
        private sealed class CaptureMomentEvidence
        {
            public string moment = string.Empty;
            public string fileName = string.Empty;
            public int elapsedSecond;
            public int visibleLifecycleCount;
            public string[] states = Array.Empty<string>();
        }
    }
}
