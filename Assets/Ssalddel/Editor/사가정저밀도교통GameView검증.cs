using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical SimulationWorldShell의 기존 사가정 View에만 합성 상태 사본을 직접
    /// 적용한다. 새 Scene, 실제 신호 주기, 통행 승인, 운영 주문을 만들지 않는다.
    /// </summary>
    public static class 사가정저밀도교통GameView검증
    {
        public const string RuntimeRootName =
            "사가정 저밀도 교통 Game View 검증 · runtime only";

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/1 자동 대로→생활도로→골목")]
        public static void 자동재생()
            => EnsureDriver().SetStage(사가정저밀도교통검증Stage.Automatic);

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/2 전체 보기 고정")]
        public static void 전체보기()
            => EnsureDriver().SetStage(사가정저밀도교통검증Stage.Overview);

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/3 생활도로 확대 고정")]
        public static void 생활도로확대()
            => EnsureDriver().SetStage(사가정저밀도교통검증Stage.NeighborhoodRoad);

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/4 골목 후보 종점 확대 고정")]
        public static void 골목후보종점확대()
            => EnsureDriver().SetStage(사가정저밀도교통검증Stage.CandidateEndpoint);

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/5 증거 사진 3장 캡처")]
        public static void 증거사진캡처()
            => EnsureDriver().BeginEvidenceCapture();

        [MenuItem("Ssalddel/검증/사가정 저밀도 교통/9 임시 검증 객체 제거")]
        public static void 정리()
        {
            var root = GameObject.Find(RuntimeRootName);
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        private static 사가정저밀도교통GameView검증Driver EnsureDriver()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficValidationRequiresPlayMode");
            var existing = Resources
                .FindObjectsOfTypeAll<사가정저밀도교통GameView검증Driver>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());
            if (existing != null) return existing;

            var view = Resources.FindObjectsOfTypeAll<사가정운영디오라마View>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());
            if (view == null)
                throw new InvalidOperationException("SagajeongOperationalDioramaViewMissing");
            view.Initialize();
            if (view.ObservationCamera == null || view.MapStatus != "Ready")
                throw new InvalidOperationException("SagajeongOperationalDioramaNotReady");

            var root = new GameObject(RuntimeRootName)
            {
                layer = 사가정운영디오라마View.ObservationLayer,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            root.transform.SetParent(view.transform, false);
            try
            {
                var driver = root.AddComponent<사가정저밀도교통GameView검증Driver>();
                driver.Initialize(view);
                Debug.Log("사가정 저밀도 교통 검증 준비: 스쿠터 1 + 승용차 3 · "
                          + "합성 상태 사본 직접 적용 · HTTP/API·Scene 저장 호출 없음");
                return driver;
            }
            catch
            {
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }
    }

    /// <summary>
    /// 그래픽이 활성화된 별도 Unity 프로세스에서 canonical Scene을 저장하지 않고
    /// 연속 기사 후보 경로의 Play Mode 동작과 Game View 캡처를 자동 검증한다.
    /// </summary>
    [InitializeOnLoad]
    public static class SagajeongLowDensityTrafficCliCapture
    {
        public const string ExecuteMethod =
            "Ssalddel.Unity.Editor.SagajeongLowDensityTrafficCliCapture.Run";
        public const string CommandLineMarker =
            "-sagajeongContinuousTrafficCliCapture";
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        private const string SessionPrefix =
            "Ssalddel.SagajeongContinuousTrafficCliCapture.";
        private const string ActiveKey = SessionPrefix + "Active";
        private const string PhaseKey = SessionPrefix + "Phase";
        private const string DeadlineUtcTicksKey = SessionPrefix + "DeadlineUtcTicks";
        private const string ResultMessageKey = SessionPrefix + "ResultMessage";
        private const string PlayModeWarmupKey = SessionPrefix + "PlayModeWarmup";
        private const int FailureExitCode = 2;
        private const int RequiredPlayModeWarmupUpdates = 2;

        private enum CliPhase
        {
            None = 0,
            AwaitingPlayMode = 1,
            AutomaticValidation = 2,
            EvidenceCapture = 3,
            StoppingSuccess = 4,
            StoppingFailure = 5,
        }

        static SagajeongLowDensityTrafficCliCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// 실행 예:
        /// Unity.exe -projectPath &lt;격리 작업트리&gt;
        /// -executeMethod Ssalddel.Unity.Editor.SagajeongLowDensityTrafficCliCapture.Run
        /// -sagajeongContinuousTrafficCliCapture -logFile &lt;로그 경로&gt;
        /// -batchmode, -nographics, -quit은 사용하지 않는다.
        /// </summary>
        public static void Run()
        {
            if (!HasCommandLineMarker())
            {
                Debug.LogError("[SagajeongContinuousTrafficCliCapture] "
                               + "전용 명령줄 표식이 없어 실행하지 않음: "
                               + CommandLineMarker);
                return;
            }
            try
            {
                RequireDedicatedGraphicsProcess();
                RequireLinkedGitWorktree();
                if (SessionState.GetBool(ActiveKey, false))
                    throw new InvalidOperationException(
                        "SagajeongContinuousTrafficCliCaptureAlreadyActive");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException(
                        "SagajeongContinuousTrafficCliCaptureRequiresEditModeStart");
                RequireNoDirtyLoadedScene();

                var sceneAbsolutePath = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..", CanonicalScenePath));
                if (!File.Exists(sceneAbsolutePath))
                    throw new FileNotFoundException(
                        "SagajeongContinuousTrafficCanonicalSceneMissing",
                        sceneAbsolutePath);

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(PhaseKey, (int)CliPhase.AwaitingPlayMode);
                SessionState.SetString(DeadlineUtcTicksKey,
                    DateTime.UtcNow.AddMinutes(10).Ticks.ToString(
                        CultureInfo.InvariantCulture));
                SessionState.SetString(ResultMessageKey, string.Empty);
                SessionState.SetInt(PlayModeWarmupKey, 0);

                var scene = EditorSceneManager.OpenScene(CanonicalScenePath,
                    OpenSceneMode.Single);
                if (!scene.IsValid()
                    || !string.Equals(scene.path, CanonicalScenePath,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "SagajeongContinuousTrafficCanonicalSceneOpenFailed");
                if (scene.isDirty)
                    throw new InvalidOperationException(
                        "SagajeongContinuousTrafficCanonicalSceneUnexpectedlyDirty");

                Debug.Log("[SagajeongContinuousTrafficCliCapture] 시작 · "
                          + "canonical Scene 저장 호출 없음 · "
                          + "합성 상태 사본 직접 적용 · 실제 주문/통행 권위 없음");
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                RequestFailure("CliStartFailed", exception);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                RequireDeadlineNotExceeded();
                var phase = (CliPhase)SessionState.GetInt(PhaseKey,
                    (int)CliPhase.None);
                switch (phase)
                {
                    case CliPhase.AwaitingPlayMode:
                        AwaitPlayModeAndStartValidation();
                        break;
                    case CliPhase.AutomaticValidation:
                        PollAutomaticValidation();
                        break;
                    case CliPhase.EvidenceCapture:
                        PollEvidenceCapture();
                        break;
                    case CliPhase.StoppingSuccess:
                        CompleteExitWhenEditMode(0);
                        break;
                    case CliPhase.StoppingFailure:
                        CompleteExitWhenEditMode(FailureExitCode);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "SagajeongContinuousTrafficCliCapturePhaseInvalid:" + phase);
                }
            }
            catch (Exception exception)
            {
                RequestFailure("CliPollingFailed", exception);
            }
        }

        private static void AwaitPlayModeAndStartValidation()
        {
            if (!EditorApplication.isPlaying) return;
            var warmup = SessionState.GetInt(PlayModeWarmupKey, 0) + 1;
            SessionState.SetInt(PlayModeWarmupKey, warmup);
            if (warmup < RequiredPlayModeWarmupUpdates) return;

            var viewReady = Resources.FindObjectsOfTypeAll<사가정운영디오라마View>()
                .Any(value => value != null && value.gameObject.scene.IsValid());
            if (!viewReady) return;

            사가정저밀도교통GameView검증.자동재생();
            var driver = FindDriver()
                         ?? throw new InvalidOperationException(
                             "SagajeongContinuousTrafficCliDriverMissingAfterStart");
            driver.PreserveForCliPolling = true;
            SessionState.SetInt(PhaseKey, (int)CliPhase.AutomaticValidation);
            Debug.Log("[SagajeongContinuousTrafficCliCapture] Play Mode 진입 · "
                      + "자동 Transform 관찰 시작");
        }

        private static void PollAutomaticValidation()
        {
            var driver = FindDriver()
                         ?? throw new InvalidOperationException(
                             "SagajeongContinuousTrafficCliDriverMissingDuringAutomaticValidation");
            if (!string.IsNullOrEmpty(driver.EvidenceCaptureFailure))
            {
                LogObservation("AutomaticValidationFailure", driver);
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficAutomaticValidationFailed:"
                    + driver.EvidenceCaptureFailure);
            }
            if (!driver.AutomaticValidationComplete) return;

            LogObservation("AutomaticValidationComplete", driver);
            사가정저밀도교통GameView검증.증거사진캡처();
            SessionState.SetInt(PhaseKey, (int)CliPhase.EvidenceCapture);
            Debug.Log("[SagajeongContinuousTrafficCliCapture] "
                      + "EvidenceCaptureComplete/Failure 폴링 시작");
        }

        private static void PollEvidenceCapture()
        {
            var driver = FindDriver()
                         ?? throw new InvalidOperationException(
                             "SagajeongContinuousTrafficCliDriverMissingDuringEvidenceCapture");
            if (!string.IsNullOrEmpty(driver.EvidenceCaptureFailure))
            {
                LogObservation("EvidenceCaptureFailure", driver);
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficEvidenceCaptureFailed:"
                    + driver.EvidenceCaptureFailure);
            }
            if (!driver.EvidenceCaptureComplete) return;

            var result = BuildObservation("EvidenceCaptureComplete", driver);
            SessionState.SetString(ResultMessageKey, result);
            Debug.Log("[SagajeongContinuousTrafficCliCapture] " + result);
            SessionState.SetInt(PhaseKey, (int)CliPhase.StoppingSuccess);
            EditorApplication.isPlaying = false;
        }

        private static 사가정저밀도교통GameView검증Driver FindDriver()
            => Resources
                .FindObjectsOfTypeAll<사가정저밀도교통GameView검증Driver>()
                .FirstOrDefault(value => value != null
                                         && value.gameObject.scene.IsValid());

        private static void LogObservation(string eventCode,
            사가정저밀도교통GameView검증Driver driver)
            => Debug.Log("[SagajeongContinuousTrafficCliCapture] "
                         + BuildObservation(eventCode, driver));

        private static string BuildObservation(string eventCode,
            사가정저밀도교통GameView검증Driver driver)
            => string.Join("; ", new[]
            {
                "event=" + eventCode,
                "EvidenceCaptureComplete=" + driver.EvidenceCaptureComplete,
                "EvidenceCaptureFailure=" + driver.EvidenceCaptureFailure,
                "EvidenceDirectory=" + driver.EvidenceDirectory,
                "redStop=" + driver.RedStopObserved,
                "greenRestart=" + driver.GreenRestartObserved,
                "mainRoad=" + driver.MainRoadObserved,
                "neighborhoodRoad=" + driver.NeighborhoodRoadObserved,
                "alley=" + driver.AlleyFinalApproachObserved,
                "candidateEndpoint=" + driver.CandidateEndpointObserved,
                "candidateEndpointReached=" + driver.CurrentCandidateEndpointReached,
                "actorCount=" + driver.ActiveActorCount.ToString(
                    CultureInfo.InvariantCulture),
                "snapshotCount=" + driver.AppliedSnapshotCount.ToString(
                    CultureInfo.InvariantCulture),
                "tick=" + driver.SimulationTick.ToString(CultureInfo.InvariantCulture),
                "routeLeg=" + (driver.CurrentCourierRouteLegIndex + 1).ToString(
                    CultureInfo.InvariantCulture) + "/"
                    + driver.CourierRouteLegCount.ToString(CultureInfo.InvariantCulture),
                "routeProgressMeters=" + driver.CumulativeProgressMeters.ToString(
                    "0.000", CultureInfo.InvariantCulture) + "/"
                    + driver.TotalRouteDistanceMeters.ToString(
                        "0.000", CultureInfo.InvariantCulture),
                "mainMovementMeters=" + driver.MainRoadMovementMeters.ToString(
                    "0.000", CultureInfo.InvariantCulture),
                "neighborhoodMovementMeters="
                    + driver.NeighborhoodRoadMovementMeters.ToString(
                        "0.000", CultureInfo.InvariantCulture),
                "alleyMovementMeters=" + driver.AlleyMovementMeters.ToString(
                    "0.000", CultureInfo.InvariantCulture),
            });

        private static void RequireDedicatedGraphicsProcess()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (UnityEngine.Application.isBatchMode
                || arguments.Any(value => string.Equals(value, "-nographics",
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficCliCaptureRequiresGraphicsProcess");
            if (arguments.Any(value => string.Equals(value, "-quit",
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficCliCaptureControlsProcessExit");
        }

        private static bool HasCommandLineMarker()
            => Environment.GetCommandLineArgs().Any(value => string.Equals(
                value, CommandLineMarker, StringComparison.OrdinalIgnoreCase));

        private static void RequireLinkedGitWorktree()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, ".."));
            var gitWorktreeMarker = Path.Combine(projectRoot, ".git");
            if (!File.Exists(gitWorktreeMarker))
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficCliCaptureRequiresLinkedGitWorktree");
            var markerText = File.ReadAllText(gitWorktreeMarker).TrimStart();
            if (!markerText.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "SagajeongContinuousTrafficCliCaptureGitWorktreeMarkerInvalid");
        }

        private static void RequireNoDirtyLoadedScene()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isDirty)
                    throw new InvalidOperationException(
                        "SagajeongContinuousTrafficCliCaptureRefusesDirtyScene:"
                        + scene.path);
            }
        }

        private static void RequireDeadlineNotExceeded()
        {
            var raw = SessionState.GetString(DeadlineUtcTicksKey, string.Empty);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var deadlineUtcTicks)
                || DateTime.UtcNow.Ticks > deadlineUtcTicks)
                throw new TimeoutException(
                    "SagajeongContinuousTrafficCliCaptureTimedOut");
        }

        private static void RequestFailure(string code, Exception exception)
        {
            var message = code + ":" + exception.GetType().Name + ":"
                          + exception.Message;
            Debug.LogError("[SagajeongContinuousTrafficCliCapture] " + message);
            if (!SessionState.GetBool(ActiveKey, false))
            {
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetString(ResultMessageKey, message);
            }
            else if (string.IsNullOrEmpty(
                         SessionState.GetString(ResultMessageKey, string.Empty)))
            {
                SessionState.SetString(ResultMessageKey, message);
            }

            SessionState.SetInt(PhaseKey, (int)CliPhase.StoppingFailure);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = false;
            else
                CompleteExitWhenEditMode(FailureExitCode);
        }

        private static void CompleteExitWhenEditMode(int exitCode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var result = SessionState.GetString(ResultMessageKey, string.Empty);
            ClearSession();
            if (exitCode == 0)
                Debug.Log("[SagajeongContinuousTrafficCliCapture] SUCCESS; " + result);
            else
                Debug.LogError("[SagajeongContinuousTrafficCliCapture] FAILURE; "
                               + result);
            EditorApplication.Exit(exitCode);
        }

        private static void ClearSession()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(DeadlineUtcTicksKey);
            SessionState.EraseString(ResultMessageKey);
            SessionState.EraseInt(PlayModeWarmupKey);
        }
    }

    public enum 사가정저밀도교통검증Stage
    {
        Automatic,
        Overview,
        NeighborhoodRoad,
        CandidateEndpoint,
    }

    [DisallowMultipleComponent]
    public sealed class 사가정저밀도교통GameView검증Driver : MonoBehaviour
    {
        public const long OverviewTick = 0L;
        public const long NeighborhoodCaptureTick = 180L;
        public const long CandidateEndpointCaptureTick = 286L;
        public const long AutomaticMaximumTick = 400L;
        private const double AutomaticPlaybackRate = 4d;
        private const float NeighborhoodCloseSize = 26f;
        private const float AlleyCloseSize = 11f;
        private const float AlleyCameraPitch = 82f;

        private readonly 사가정저밀도교통Interpreter interpreter = new();
        private readonly List<CameraState> cameraStates = new();
        private readonly List<CanvasState> canvasStates = new();
        private readonly List<BehaviourState> overlayBehaviourStates = new();
        private 사가정운영디오라마View view = null!;
        private 사가정저밀도교통PlaybackLayer playback = null!;
        private Transform trafficRoot = null!;
        private 사가정저밀도교통ProjectionContext context = null!;
        private bool previousGameOverlayVisible;
        private bool previousViewVisible;
        private bool externalStateRestored;
        private bool captureInProgress;
        private string captureFailure = string.Empty;
        private double tickAccumulator;
        private long simulationTick;
        private Vector3 previousSignalActorPosition;
        private Vector3 previousCourierPosition;
        private bool hasPreviousSignalActorPosition;
        private bool hasPreviousCourierPosition;
        private int previousCourierRouteLegIndex;
        private double previousCourierCumulativeProgress;
        private bool loggedRedStop;
        private bool loggedGreenRestart;
        private bool loggedNeighborhoodRoad;
        private bool loggedAlleyFinalApproach;
        private bool loggedCandidateEndpoint;
        private GUIStyle titleStyle = null!;
        private GUIStyle labelStyle = null!;
        private GUIStyle accentStyle = null!;

        public 사가정저밀도교통검증Stage Stage { get; private set; }
        public int AppliedSnapshotCount { get; private set; }
        public bool RedStopObserved { get; private set; }
        public bool GreenRestartObserved { get; private set; }
        public bool MainRoadObserved { get; private set; }
        public bool NeighborhoodRoadObserved { get; private set; }
        public bool AlleyFinalApproachObserved { get; private set; }
        public bool CandidateEndpointObserved { get; private set; }
        public float SignalActorMovementMeters { get; private set; }
        public float MainRoadMovementMeters { get; private set; }
        public float NeighborhoodRoadMovementMeters { get; private set; }
        public float AlleyMovementMeters { get; private set; }
        public Vector3 CurrentCourierWorldPosition { get; private set; }
        public string CurrentCourierState { get; private set; } = string.Empty;
        public string CurrentCourierDisplayPolylineOrder { get; private set; } = string.Empty;
        public string CurrentCourierRouteLegStableId { get; private set; } = string.Empty;
        public string CurrentCourierStage { get; private set; } = string.Empty;
        public string CurrentCourierLaneStableId { get; private set; } = string.Empty;
        public int CurrentCourierRouteLegIndex { get; private set; }
        public int CourierRouteLegCount { get; private set; }
        public double CurrentLegProgressMeters { get; private set; }
        public double CumulativeProgressMeters { get; private set; }
        public double TotalRouteDistanceMeters { get; private set; }
        public bool CurrentCandidateEndpointReached { get; private set; }
        public string CurrentSignalActorState { get; private set; } = string.Empty;
        public string CurrentSignalPhaseCode { get; private set; } = string.Empty;
        public long AppliedRevision => playback == null ? -1L : playback.AppliedRevision;
        public long SimulationTick => simulationTick;
        public int ActiveActorCount => playback == null ? 0 : playback.ActiveActorCount;
        public bool AutomaticValidationComplete => RedStopObserved
                                                   && GreenRestartObserved
                                                   && MainRoadObserved
                                                   && NeighborhoodRoadObserved
                                                   && AlleyFinalApproachObserved
                                                   && CandidateEndpointObserved;
        public bool EvidenceCaptureComplete { get; private set; }
        public string EvidenceDirectory { get; private set; } = string.Empty;
        public string EvidenceCaptureFailure => captureFailure;
        public bool PreserveForCliPolling { get; set; }

        public void Initialize(사가정운영디오라마View value)
        {
            if (view != null)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficValidationAlreadyInitialized");
            view = value != null ? value : throw new ArgumentNullException(nameof(value));
            previousGameOverlayVisible = view.GameOverlayVisible;
            previousViewVisible = 사가정운영디오라마View.IsVisible;
            externalStateRestored = false;
            SaveAndIsolateExternalState();
            try
            {
                view.GameOverlayVisible = true;
                view.Show(true);
                trafficRoot = new GameObject("Mobility · 저밀도 연속 기사 후보 경로")
                {
                    layer = 사가정운영디오라마View.ObservationLayer,
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                }.transform;
                trafficRoot.SetParent(transform, false);
                var initial = BuildSnapshotForTests(0L);
                context = CreateContextForTests(initial);
                playback = gameObject.AddComponent<사가정저밀도교통PlaybackLayer>();
                playback.Configure(view.ObservationCamera, trafficRoot);
                SetStage(사가정저밀도교통검증Stage.Automatic);
            }
            catch
            {
                RestoreExternalState();
                throw;
            }
        }

        public void BeginEvidenceCapture()
        {
            if (captureInProgress)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficEvidenceCaptureAlreadyRunning");
            if (!AutomaticValidationComplete)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficAutomaticValidationRequired");
            try
            {
                EvidenceDirectory = CreateEvidenceDirectory();
                captureFailure = string.Empty;
                EvidenceCaptureComplete = false;
                captureInProgress = true;
                StartCoroutine(CaptureEvidenceRoutine());
            }
            catch (Exception exception)
            {
                captureInProgress = false;
                FailAndRestore("EvidenceCaptureStartFailed", exception,
                    destroyImmediatelyAfterFrame: true);
                throw;
            }
        }

        public void SetStage(사가정저밀도교통검증Stage value)
        {
            try
            {
                Stage = value;
                interpreter.Reset();
                playback.ClearDynamic();
                switch (value)
                {
                    case 사가정저밀도교통검증Stage.Automatic:
                        ResetAutomaticEvidence();
                        ApplyTick(0L);
                        FocusAutomaticCourier();
                        Debug.Log("사가정 저밀도 교통: 자동 검증 시작 · 4배속 관찰 · "
                                  + "대로→생활도로→골목→후보 종점 · 합성 상태 사본 직접 적용");
                        break;
                    case 사가정저밀도교통검증Stage.Overview:
                        ApplyTick(OverviewTick);
                        RequireRouteSample(0,
                            사가정저밀도교통WireCodes.MainRoadApproach, false);
                        view.ShowWhole();
                        playback.RefreshVisibilityForTests(
                            사가정저밀도교통PlaybackLayer.DefaultOverviewThreshold + 1f);
                        Debug.Log("사가정 저밀도 교통: 전체 1km · 대로 접근 tick 0 고정");
                        break;
                    case 사가정저밀도교통검증Stage.NeighborhoodRoad:
                        ApplyTick(NeighborhoodCaptureTick);
                        RequireRouteSample(1,
                            사가정저밀도교통WireCodes.NeighborhoodRoadApproach, false);
                        FocusCourier(NeighborhoodCloseSize,
                            사가정운영디오라마View.WholeViewPitch);
                        Debug.Log("사가정 저밀도 교통: 생활도로 연속 접근 tick 180 확대 고정");
                        break;
                    case 사가정저밀도교통검증Stage.CandidateEndpoint:
                        ApplyTick(CandidateEndpointCaptureTick);
                        RequireRouteSample(2,
                            사가정저밀도교통WireCodes.CandidateEndpointReached, true);
                        FocusCourier(AlleyCloseSize, AlleyCameraPitch);
                        Debug.Log("사가정 저밀도 교통: 골목 후보 종점 정차 tick 286 확대 고정");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
            catch (Exception exception)
            {
                FailAndRestore("StageApplyFailed:" + value, exception,
                    destroyImmediatelyAfterFrame: !captureInProgress);
                throw;
            }
        }

        private void Update()
        {
            try
            {
                if (Stage != 사가정저밀도교통검증Stage.Automatic
                    || AutomaticValidationComplete)
                    return;
                tickAccumulator += Time.unscaledDeltaTime * AutomaticPlaybackRate;
                while (tickAccumulator >= 사가정저밀도교통Fixture.TickSeconds)
                {
                    tickAccumulator -= 사가정저밀도교통Fixture.TickSeconds;
                    ApplyTick(++simulationTick);
                    FocusAutomaticCourier();
                    if (AutomaticValidationComplete)
                    {
                        Debug.Log("사가정 저밀도 교통 자동 동작 검증 완료: "
                                  + $"redStop={RedStopObserved}, greenRestart={GreenRestartObserved}, "
                                  + $"main={MainRoadObserved}, neighborhood={NeighborhoodRoadObserved}, "
                                  + $"alley={AlleyFinalApproachObserved}, "
                                  + $"candidateEndpoint={CandidateEndpointObserved}, "
                                  + $"distance={CumulativeProgressMeters:0.000}/"
                                  + $"{TotalRouteDistanceMeters:0.000}m");
                        break;
                    }
                    if (simulationTick < AutomaticMaximumTick) continue;
                    throw new InvalidOperationException(
                        "SagajeongContinuousCourierCandidateEndpointNotReached");
                }
            }
            catch (Exception exception)
            {
                FailAndRestore("AutomaticPlaybackFailed", exception,
                    destroyImmediatelyAfterFrame: true);
            }
        }

        private void LateUpdate()
        {
            if (view == null || view.ObservationCamera == null) return;
            IsolateNewExternalState();
            view.GameOverlayVisible = true;
            view.ObservationCamera.enabled = true;
            foreach (var state in cameraStates)
                if (state.Camera != null && state.Camera != view.ObservationCamera)
                    state.Camera.enabled = false;
            foreach (var state in canvasStates)
                if (state.Canvas != null) state.Canvas.enabled = false;
            foreach (var state in overlayBehaviourStates)
                if (state.Behaviour != null) state.Behaviour.enabled = false;
        }

        private void ApplyTick(long tick)
        {
            simulationTick = tick;
            var snapshot = BuildSnapshotForTests(tick);
            if (!interpreter.TryInterpret(snapshot, context, out var model,
                    out var failureCode))
                throw new InvalidOperationException(failureCode);
            playback.Apply(model);
            AppliedSnapshotCount++;
            CurrentSignalPhaseCode = model.SignalPhaseCode;

            var courier = model.Actors.Single(value =>
                value.StableId == 사가정저밀도교통Fixture.CourierActorId);
            CurrentCourierState = courier.StateCode;
            CurrentCourierDisplayPolylineOrder = courier.DisplayPolylineOrderCode;
            var routeState = model.CourierRouteState
                             ?? throw new InvalidOperationException(
                                 "SagajeongContinuousCourierRouteStateMissing");
            CurrentCourierRouteLegStableId = routeState.CurrentRouteLegStableId;
            CurrentCourierStage = routeState.StageCode;
            CurrentCourierLaneStableId = routeState.CurrentLaneStableId;
            CurrentCourierRouteLegIndex = routeState.CurrentRouteLegIndex;
            CourierRouteLegCount = routeState.RouteLegCount;
            CurrentLegProgressMeters = routeState.LegProgressMeters;
            CumulativeProgressMeters = routeState.CumulativeProgressMeters;
            TotalRouteDistanceMeters = routeState.TotalDistanceMeters;
            CurrentCandidateEndpointReached = routeState.CandidateEndpointReached;
            if (!playback.TryGetActorVisualState(courier.StableId, out var courierVisual))
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficCourierVisualMissing");
            var expectedLocalPosition = new Vector3((float)courier.Position.X, .38f,
                (float)courier.Position.Z);
            if (Vector3.Distance(courierVisual.LocalPosition, expectedLocalPosition) > .001f
                || courierVisual.DisplayPolylineOrderCode !=
                courier.DisplayPolylineOrderCode)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficCourierVisualStateMismatch");
            if (playback.CurrentRouteLegStableId != routeState.CurrentRouteLegStableId
                || Math.Abs(playback.CourierRouteCumulativeProgressMeters
                            - routeState.CumulativeProgressMeters) > .000001d
                || Math.Abs(playback.CourierRouteTotalDistanceMeters
                            - routeState.TotalDistanceMeters) > .000001d
                || playback.CandidateEndpointReached != routeState.CandidateEndpointReached)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficPlaybackRouteStateMismatch");
            CurrentCourierWorldPosition = trafficRoot.TransformPoint(
                courierVisual.LocalPosition);

            if (Stage == 사가정저밀도교통검증Stage.Automatic)
            {
                ObserveSignalVehicle(model);
                ObserveCourierRoute(courier, CurrentCourierWorldPosition,
                    routeState.CurrentRouteLegIndex, routeState.RouteLegCount,
                    routeState.StageCode, routeState.CumulativeProgressMeters,
                    routeState.TotalDistanceMeters, routeState.CandidateEndpointReached);
            }
        }

        private void ObserveSignalVehicle(사가정저밀도교통PresentationModel model)
        {
            var actor = model.Actors.FirstOrDefault(value =>
                value.StableId == 사가정저밀도교통Fixture.AmbientActor1);
            if (actor == null
                || !playback.TryGetActorVisualState(actor.StableId, out var visual))
                return;
            CurrentSignalActorState = actor.StateCode;
            var current = trafficRoot.TransformPoint(visual.LocalPosition);
            var movement = hasPreviousSignalActorPosition
                ? Vector3.Distance(previousSignalActorPosition, current)
                : 0f;
            if (hasPreviousSignalActorPosition) SignalActorMovementMeters += movement;
            var waiting = actor.StateCode == 사가정저밀도교통WireCodes.WaitingAtSignal
                          || actor.StateCode == 사가정저밀도교통WireCodes.Queueing;
            if (hasPreviousSignalActorPosition && waiting && movement <= .001f)
            {
                RedStopObserved = true;
                if (!loggedRedStop)
                {
                    loggedRedStop = true;
                    Debug.Log("사가정 저밀도 교통 동작 증거: 주변차 Transform 적색 정지 확인");
                }
            }
            if (RedStopObserved && actor.StateCode == 사가정저밀도교통WireCodes.Moving
                                && movement > .01f
                                && CurrentSignalPhaseCode ==
                                사가정저밀도교통WireCodes.EastWestGreen)
            {
                GreenRestartObserved = true;
                if (!loggedGreenRestart)
                {
                    loggedGreenRestart = true;
                    Debug.Log("사가정 저밀도 교통 동작 증거: 주변차 Transform 녹색 재출발 확인");
                }
            }
            previousSignalActorPosition = current;
            hasPreviousSignalActorPosition = true;
        }

        private void ObserveCourierRoute(사가정저밀도교통ActorModel courier,
            Vector3 currentWorldPosition, int legIndex, int legCount, string stageCode,
            double cumulativeProgress, double totalDistance, bool candidateEndpointReached)
        {
            var movement = hasPreviousCourierPosition
                ? Vector3.Distance(previousCourierPosition, currentWorldPosition)
                : 0f;
            if (legCount != 3 || legIndex < 0 || legIndex >= legCount)
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierRouteLegInvalid");
            if (hasPreviousCourierPosition
                && (legIndex < previousCourierRouteLegIndex
                    || legIndex > previousCourierRouteLegIndex + 1
                    || cumulativeProgress + .000001d < previousCourierCumulativeProgress))
            {
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierRouteProgressRegressed");
            }
            if (!string.Equals(courier.LaneStableId, CurrentCourierLaneStableId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierLaneMismatch");

            var expectedLegStableId = legIndex switch
            {
                0 => 사가정저밀도교통WireCodes.MainRoadRouteLeg,
                1 => 사가정저밀도교통WireCodes.NeighborhoodRoadRouteLeg,
                2 => 사가정저밀도교통WireCodes.AlleyFinalRouteLeg,
                _ => string.Empty,
            };
            var expectedLaneStableId = legIndex switch
            {
                0 => 사가정저밀도교통WireCodes.EastWestLane,
                1 => 사가정저밀도교통WireCodes.NeighborhoodAccessLane,
                2 => 사가정저밀도교통WireCodes.AlleyCandidateLane,
                _ => string.Empty,
            };
            var expectedStageCode = legIndex switch
            {
                0 => 사가정저밀도교통WireCodes.MainRoadApproach,
                1 => 사가정저밀도교통WireCodes.NeighborhoodRoadApproach,
                2 when candidateEndpointReached =>
                    사가정저밀도교통WireCodes.CandidateEndpointReached,
                2 => 사가정저밀도교통WireCodes.AlleyFinalApproach,
                _ => string.Empty,
            };
            if (!string.Equals(CurrentCourierRouteLegStableId, expectedLegStableId,
                    StringComparison.Ordinal)
                || !string.Equals(CurrentCourierLaneStableId, expectedLaneStableId,
                    StringComparison.Ordinal)
                || !string.Equals(stageCode, expectedStageCode,
                    StringComparison.Ordinal)
                || (CurrentCourierDisplayPolylineOrder !=
                    사가정저밀도교통WireCodes.AsDefined
                    && CurrentCourierDisplayPolylineOrder !=
                    사가정저밀도교통WireCodes.ReverseDefined))
            {
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierRouteLegBindingMismatch");
            }

            switch (legIndex)
            {
                case 0:
                    MainRoadMovementMeters += movement;
                    if (movement > .001f) MainRoadObserved = true;
                    break;
                case 1:
                    NeighborhoodRoadMovementMeters += movement;
                    if (movement > .001f)
                    {
                        NeighborhoodRoadObserved = true;
                        if (!loggedNeighborhoodRoad)
                        {
                            loggedNeighborhoodRoad = true;
                            Debug.Log("사가정 저밀도 교통 동작 증거: 같은 기사 Transform이 생활도로 leg로 연속 진입");
                        }
                    }
                    break;
                case 2:
                    AlleyMovementMeters += movement;
                    if (movement > .001f)
                    {
                        AlleyFinalApproachObserved = true;
                        if (!loggedAlleyFinalApproach)
                        {
                            loggedAlleyFinalApproach = true;
                            Debug.Log("사가정 저밀도 교통 동작 증거: 같은 기사 Transform이 골목 마지막 leg로 연속 진입");
                        }
                    }
                    break;
            }

            if (candidateEndpointReached)
            {
                if (legIndex != 2
                    || stageCode != 사가정저밀도교통WireCodes.CandidateEndpointReached
                    || courier.StateCode !=
                    사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint
                    || courier.SpeedMetersPerSecond > .000001d
                    || Math.Abs(cumulativeProgress - totalDistance) > .000001d)
                {
                    throw new InvalidOperationException(
                        "SagajeongContinuousCourierCandidateEndpointInvalid");
                }
                CandidateEndpointObserved = true;
                if (!loggedCandidateEndpoint)
                {
                    loggedCandidateEndpoint = true;
                    Debug.Log("사가정 저밀도 교통 동작 증거: 후보 종점 정차 확인 · "
                              + "Delivered/주문 완료 권위 없음");
                }
            }
            previousCourierPosition = currentWorldPosition;
            hasPreviousCourierPosition = true;
            previousCourierRouteLegIndex = legIndex;
            previousCourierCumulativeProgress = cumulativeProgress;
        }

        private void ResetAutomaticEvidence()
        {
            tickAccumulator = 0d;
            simulationTick = 0L;
            AppliedSnapshotCount = 0;
            RedStopObserved = false;
            GreenRestartObserved = false;
            MainRoadObserved = false;
            NeighborhoodRoadObserved = false;
            AlleyFinalApproachObserved = false;
            CandidateEndpointObserved = false;
            SignalActorMovementMeters = 0f;
            MainRoadMovementMeters = 0f;
            NeighborhoodRoadMovementMeters = 0f;
            AlleyMovementMeters = 0f;
            CurrentCandidateEndpointReached = false;
            CurrentCourierRouteLegStableId = string.Empty;
            CurrentCourierStage = string.Empty;
            CurrentCourierLaneStableId = string.Empty;
            CurrentCourierDisplayPolylineOrder = string.Empty;
            CurrentCourierRouteLegIndex = 0;
            CourierRouteLegCount = 0;
            CurrentLegProgressMeters = 0d;
            CumulativeProgressMeters = 0d;
            TotalRouteDistanceMeters = 0d;
            hasPreviousSignalActorPosition = false;
            hasPreviousCourierPosition = false;
            previousCourierRouteLegIndex = -1;
            previousCourierCumulativeProgress = 0d;
            loggedRedStop = false;
            loggedGreenRestart = false;
            loggedNeighborhoodRoad = false;
            loggedAlleyFinalApproach = false;
            loggedCandidateEndpoint = false;
        }

        private void FocusAutomaticCourier()
        {
            if (CurrentCourierRouteLegIndex <= 0)
                FocusCourier(NeighborhoodCloseSize,
                    사가정운영디오라마View.WholeViewPitch);
            else if (CurrentCourierRouteLegIndex == 1)
                FocusCourier(NeighborhoodCloseSize,
                    사가정운영디오라마View.WholeViewPitch);
            else
                FocusCourier(AlleyCloseSize, AlleyCameraPitch);
        }

        private void FocusCourier(float orthographicSize, float pitch)
        {
            var focus = CurrentCourierWorldPosition;
            view.FocusPoint(focus);
            var rotation = Quaternion.Euler(pitch,
                사가정운영디오라마View.WholeViewYaw, 0f);
            view.ObservationCamera.transform.SetPositionAndRotation(
                focus - rotation * Vector3.forward * 1800f, rotation);
            view.ObservationCamera.orthographicSize = orthographicSize;
            playback.RefreshVisibilityForTests(orthographicSize);
        }

        private void RequireRouteSample(int legIndex, string stageCode,
            bool endpointReached)
        {
            var expectedLegStableId = legIndex switch
            {
                0 => 사가정저밀도교통WireCodes.MainRoadRouteLeg,
                1 => 사가정저밀도교통WireCodes.NeighborhoodRoadRouteLeg,
                2 => 사가정저밀도교통WireCodes.AlleyFinalRouteLeg,
                _ => string.Empty,
            };
            var expectedLaneStableId = legIndex switch
            {
                0 => 사가정저밀도교통WireCodes.EastWestLane,
                1 => 사가정저밀도교통WireCodes.NeighborhoodAccessLane,
                2 => 사가정저밀도교통WireCodes.AlleyCandidateLane,
                _ => string.Empty,
            };
            if (CurrentCourierRouteLegIndex != legIndex
                || CourierRouteLegCount != 3
                || CurrentCourierRouteLegStableId != expectedLegStableId
                || CurrentCourierLaneStableId != expectedLaneStableId
                || !string.Equals(CurrentCourierStage, stageCode,
                    StringComparison.Ordinal)
                || CurrentCandidateEndpointReached != endpointReached)
            {
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierCaptureSampleMismatch");
            }
            if (endpointReached
                && CurrentCourierState !=
                사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint)
                throw new InvalidOperationException(
                    "SagajeongContinuousCourierCaptureEndpointMissing");
        }

        private IEnumerator CaptureEvidenceRoutine()
        {
            var success = false;
            try
            {
                yield return CaptureStage(사가정저밀도교통검증Stage.Overview,
                    "01-overview-main-road.png");
                if (captureFailure.Length > 0) yield break;
                yield return CaptureStage(사가정저밀도교통검증Stage.NeighborhoodRoad,
                    "02-neighborhood-road.png");
                if (captureFailure.Length > 0) yield break;
                yield return CaptureStage(사가정저밀도교통검증Stage.CandidateEndpoint,
                    "03-alley-candidate-endpoint.png");
                if (captureFailure.Length > 0) yield break;
                success = true;
            }
            finally
            {
                captureInProgress = false;
                EvidenceCaptureComplete = success;
                if (!success && captureFailure.Length == 0)
                    captureFailure = "CaptureRoutineInterrupted";
                if (success)
                    Debug.Log("사가정 연속 기사 경로 공간 판독용 정지 사진 3장 완료: "
                              + EvidenceDirectory
                              + " · 실제 움직임 증거는 자동 Transform 로그와 구분");
                else
                    Debug.LogError("사가정 저밀도 교통 캡처 실패: " + captureFailure);
                try
                {
                    if (playback != null) playback.ClearAll();
                }
                catch (Exception exception)
                {
                    if (captureFailure.Length == 0)
                        captureFailure = "CaptureCleanupFailed:"
                                         + exception.GetType().Name + ":"
                                         + exception.Message;
                    Debug.LogException(exception, this);
                }
                finally
                {
                    try
                    {
                        RestoreExternalState();
                    }
                    finally
                    {
                        enabled = false;
                        if (!PreserveForCliPolling) Destroy(gameObject);
                    }
                }
            }
        }

        private IEnumerator CaptureStage(사가정저밀도교통검증Stage value,
            string fileName)
        {
            Exception stageFailure = null;
            try
            {
                SetStage(value);
            }
            catch (Exception exception)
            {
                stageFailure = exception;
            }
            if (stageFailure != null)
            {
                captureFailure = "StageApplyFailed:" + fileName + ":"
                                 + stageFailure.GetType().Name + ":"
                                 + stageFailure.Message;
                yield break;
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(EvidenceDirectory, fileName);
            if (File.Exists(path))
            {
                captureFailure = "CapturePathAlreadyExists:" + fileName;
                yield break;
            }
            Exception captureException = null;
            try
            {
                ScreenCapture.CaptureScreenshot(path, 1);
            }
            catch (Exception exception)
            {
                captureException = exception;
            }
            if (captureException != null)
            {
                captureFailure = "CaptureStartFailed:" + fileName + ":"
                                 + captureException.GetType().Name + ":"
                                 + captureException.Message;
                yield break;
            }
            for (var frame = 0; frame < 240; frame++)
            {
                yield return null;
                if (CaptureFileReady(path, out var readFailure))
                    yield break;
                if (readFailure.Length > 0)
                {
                    captureFailure = "CaptureReadFailed:" + fileName + ":" + readFailure;
                    yield break;
                }
            }
            captureFailure = "CaptureTimedOut:" + fileName;
        }

        private static bool CaptureFileReady(string path, out string failure)
        {
            failure = string.Empty;
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 1024L;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
        }

        private static string CreateEvidenceDirectory()
        {
            var now = DateTime.Now;
            var root = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Documentation", "Changes",
                now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + "-sagajeong-low-density-traffic"));
            var stamp = now.ToString("yyyyMMdd-HHmmss-fff",
                CultureInfo.InvariantCulture);
            var candidate = Path.Combine(root, stamp);
            var suffix = 1;
            while (Directory.Exists(candidate))
                candidate = Path.Combine(root, stamp + "-" + (suffix++)
                    .ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(candidate);
            return candidate;
        }

        private void FailAndRestore(string code, Exception exception,
            bool destroyImmediatelyAfterFrame)
        {
            if (captureFailure.Length == 0)
                captureFailure = code + ":" + exception.GetType().Name + ":"
                                 + exception.Message;
            Debug.LogException(exception, this);
            try
            {
                if (playback != null) playback.ClearAll();
            }
            catch (Exception cleanupException)
            {
                Debug.LogException(cleanupException, this);
            }
            finally
            {
                try
                {
                    RestoreExternalState();
                }
                finally
                {
                    enabled = false;
                    if (destroyImmediatelyAfterFrame && !PreserveForCliPolling
                                                     && gameObject != null)
                        Destroy(gameObject);
                }
            }
        }

        private void SaveAndIsolateExternalState()
        {
            cameraStates.Clear();
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera == null || !camera.gameObject.scene.IsValid()) continue;
                cameraStates.Add(new CameraState(camera));
                camera.enabled = camera == view.ObservationCamera;
            }
            canvasStates.Clear();
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                canvasStates.Add(new CanvasState(canvas, canvas.enabled));
                canvas.enabled = false;
            }
            overlayBehaviourStates.Clear();
            foreach (var overlay in Resources.FindObjectsOfTypeAll<역방어준비View>())
            {
                if (overlay == null || !overlay.gameObject.scene.IsValid()) continue;
                overlayBehaviourStates.Add(new BehaviourState(overlay, overlay.enabled));
                overlay.enabled = false;
            }
        }

        private void IsolateNewExternalState()
        {
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera == null || !camera.gameObject.scene.IsValid()) continue;
                if (!cameraStates.Any(state => state.Camera == camera))
                    cameraStates.Add(new CameraState(camera));
                if (camera != view.ObservationCamera) camera.enabled = false;
            }
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                if (!canvasStates.Any(state => state.Canvas == canvas))
                    canvasStates.Add(new CanvasState(canvas, canvas.enabled));
                canvas.enabled = false;
            }
            foreach (var overlay in Resources.FindObjectsOfTypeAll<역방어준비View>())
            {
                if (overlay == null || !overlay.gameObject.scene.IsValid()) continue;
                if (!overlayBehaviourStates.Any(state => state.Behaviour == overlay))
                    overlayBehaviourStates.Add(new BehaviourState(overlay, overlay.enabled));
                overlay.enabled = false;
            }
        }

        private void RestoreExternalState()
        {
            if (externalStateRestored) return;
            externalStateRestored = true;
            try
            {
                if (view != null)
                {
                    view.Show(previousViewVisible);
                    view.GameOverlayVisible = previousGameOverlayVisible;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            foreach (var state in cameraStates)
            {
                try
                {
                    state.Restore();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            cameraStates.Clear();
            foreach (var state in canvasStates)
            {
                try
                {
                    if (state.Canvas != null) state.Canvas.enabled = state.Enabled;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            canvasStates.Clear();
            foreach (var state in overlayBehaviourStates)
            {
                try
                {
                    if (state.Behaviour != null) state.Behaviour.enabled = state.Enabled;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            overlayBehaviourStates.Clear();
        }

        private void OnDestroy()
        {
            RestoreExternalState();
        }

        private void OnDisable()
        {
            RestoreExternalState();
        }

        private void OnGUI()
        {
            if (view == null || view.ObservationCamera == null
                             || !view.ObservationCamera.enabled)
                return;
            GUI.depth = -10000;
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(.94f, .96f, .98f) },
            };
            accentStyle ??= new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, .78f, .12f) },
            };
            var previous = GUI.color;
            GUI.color = new Color(.07f, .10f, .14f, .93f);
            GUI.Box(new Rect(16, 16, 700, 286), GUIContent.none);
            GUI.color = previous;
            GUI.Label(new Rect(30, 26, 660, 30),
                "사가정 연속 기사 후보 경로 · Game View 검증", titleStyle);
            GUI.Label(new Rect(30, 60, 660, 24),
                $"화면: {StageText(Stage)} | tick {simulationTick} · revision {AppliedRevision}",
                accentStyle);
            GUI.Label(new Rect(30, 88, 660, 44),
                $"신호 {PhaseText(CurrentSignalPhaseCode)} | 주체 {ActiveActorCount}/4"
                + $" | 기사 {MotionText(CurrentCourierState)}\n"
                + $"표시 경로 {CurrentCourierRouteLegIndex + 1}/{CourierRouteLegCount} · "
                + $"{RouteStageText(CurrentCourierStage)} · "
                + $"점 순서 {CurrentCourierDisplayPolylineOrder}",
                labelStyle);
            GUI.Label(new Rect(30, 136, 660, 44),
                $"구간 {CurrentLegProgressMeters:0.0}m · 누적 "
                + $"{CumulativeProgressMeters:0.0}/{TotalRouteDistanceMeters:0.0}m · "
                + $"후보 종점={CurrentCandidateEndpointReached}\n"
                + "저밀도: 스쿠터 1 + 주변차 최대 3 · 0.25초 Tick · seed 20260913",
                labelStyle);
            GUI.Label(new Rect(30, 184, 660, 44),
                $"실제 Transform 측정 redStop={RedStopObserved} · "
                + $"greenRestart={GreenRestartObserved} · endpoint={CandidateEndpointObserved}\n"
                + $"대로 {MainRoadMovementMeters:0.0}m · 생활도로 "
                + $"{NeighborhoodRoadMovementMeters:0.0}m · 골목 {AlleyMovementMeters:0.0}m",
                labelStyle);
            GUI.Label(new Rect(30, 232, 660, 58),
                "합성 상태 사본 직접 적용 · HTTP/API 호출 없음\n"
                + "OSM 중심선 후보=관찰 · source direction=Unknown · "
                + "access=PendingHumanReview\n"
                + "runtimeAuthorized=false · 주문 완료/배차/통행 승인 권위 없음",
                labelStyle);

            GUI.color = new Color(.07f, .10f, .14f, .93f);
            GUI.Box(new Rect(16, Screen.height - 78, Screen.width - 32f, 62),
                GUIContent.none);
            GUI.color = previous;
            GUI.Label(new Rect(30, Screen.height - 69, Screen.width - 60f, 48),
                "SyntheticFixture · 실제 사가정 신호 주기/교통량 아님 · "
                + "distributionApproved=false · traversalReady=false · gameplayReady=false · "
                + "Scene 저장 호출 없음 · 운영 주문 변경 권한 없음",
                labelStyle);

            DrawCourierScreenMarker();
        }

        private void DrawCourierScreenMarker()
        {
            var camera = view.ObservationCamera;
            var screenPoint = camera.WorldToScreenPoint(CurrentCourierWorldPosition);
            if (screenPoint.z <= 0f
                || !float.IsFinite(screenPoint.x)
                || !float.IsFinite(screenPoint.y))
                return;

            var x = screenPoint.x;
            var y = Screen.height - screenPoint.y;
            if (x < 0f || x > Screen.width || y < 0f || y > Screen.height)
                return;

            var previous = GUI.color;
            GUI.color = new Color(1f, .36f, .05f, .98f);
            GUI.DrawTexture(new Rect(x - 13f, y - 3f, 26f, 6f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 3f, y - 13f, 6f, 26f),
                Texture2D.whiteTexture);

            var label = CurrentCandidateEndpointReached
                ? "기사 정차 · 후보 종점"
                : "기사 위치 · 실제 Transform";
            var labelX = Mathf.Clamp(x + 18f, 16f, Screen.width - 242f);
            var labelY = Mathf.Clamp(y - 18f, 308f, Screen.height - 118f);
            GUI.color = new Color(.07f, .10f, .14f, .93f);
            GUI.Box(new Rect(labelX, labelY, 226f, 36f), GUIContent.none);
            GUI.color = previous;
            GUI.Label(new Rect(labelX + 10f, labelY + 7f, 208f, 24f),
                label + " · 화면 투시 표식", accentStyle);
        }

        public static 사가정저밀도교통ProjectionContext CreateContextForTests(
            사가정저밀도교통SnapshotData snapshot)
            => new(snapshot.stationStableId, snapshot.regionStableId,
                snapshot.administrativeAreaStableId,
                snapshot.sourceObservationRevision,
                snapshot.sourceMobilityGraphStableId,
                snapshot.sourceMobilityGraphRevision,
                snapshot.sourceMobilityGraphProjectionHashSha256,
                snapshot.sourceMobilityGraphContentHashSha256,
                snapshot.laneGraphRevision,
                snapshot.laneGraphHash, snapshot.signalPlanRevision,
                snapshot.trafficProfileRevision, snapshot.courierJourneyId,
                snapshot.routeFingerprint, snapshot.courierDisplayRouteRevision,
                snapshot.courierDisplayRouteFingerprint,
                snapshot.configurationFingerprint,
                snapshot.inputStateFingerprint, snapshot.scenarioSeed,
                snapshot.tickSeconds, 사가정저밀도교통Fixture.EpochUtc, true);

        public static 사가정저밀도교통SnapshotData BuildSnapshotForTests(long tick)
        {
            var input = 사가정저밀도교통검증Fixture.만들기();
            var snapshot = new 사가정저밀도교통Engine().생성(input, tick);
            return FromServerSnapshot(snapshot);
        }

        private static 사가정저밀도교통SnapshotData FromServerSnapshot(
            Ssalddel.Simulation.Contracts.사가정저밀도교통Snapshot snapshot)
            => new()
            {
                schemaVersion = snapshot.SchemaVersion,
                stationStableId = snapshot.StationStableId,
                regionStableId = snapshot.RegionStableId,
                administrativeAreaStableId = snapshot.AdministrativeAreaStableId,
                sourceKindCode = snapshot.SourceKindCode,
                sourceObservationRevision = snapshot.SourceObservationRevision,
                sourceMobilityGraphStableId = snapshot.SourceMobilityGraphStableId,
                sourceMobilityGraphRevision = snapshot.SourceMobilityGraphRevision,
                sourceMobilityGraphProjectionHashSha256 =
                    snapshot.SourceMobilityGraphProjectionHashSha256,
                sourceMobilityGraphContentHashSha256 =
                    snapshot.SourceMobilityGraphContentHashSha256,
                sourceAccessReviewCode = snapshot.SourceAccessReviewCode,
                sourceDirectionAuthorityCode = snapshot.SourceDirectionAuthorityCode,
                sourceRuntimeAuthorized = snapshot.SourceRuntimeAuthorized,
                laneGraphRevision = snapshot.LaneGraphRevision,
                laneGraphHash = snapshot.LaneGraphHash,
                signalPlanRevision = snapshot.SignalPlanRevision,
                trafficProfileRevision = snapshot.TrafficProfileRevision,
                courierJourneyId = snapshot.CourierJourneyId,
                routeFingerprint = snapshot.RouteFingerprint,
                courierDisplayRouteRevision = snapshot.CourierDisplayRouteRevision,
                courierDisplayRouteFingerprint = snapshot.CourierDisplayRouteFingerprint,
                configurationFingerprint = snapshot.ConfigurationFingerprint,
                inputStateFingerprint = snapshot.InputStateFingerprint,
                scenarioSeed = snapshot.ScenarioSeed,
                simulationTick = snapshot.SimulationTick,
                tickSeconds = snapshot.TickSeconds,
                revision = snapshot.Revision,
                observedAt = snapshot.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
                observationPresentationOnly = snapshot.ObservationPresentationOnly,
                distributionApproved = snapshot.DistributionApproved,
                traversalReady = snapshot.TraversalReady,
                gameplayReady = snapshot.GameplayReady,
                journeyBindingStateCode = snapshot.JourneyBindingStateCode,
                operationalBindingReady = snapshot.OperationalBindingReady,
                canonicalDeliveryMutationAllowed =
                    snapshot.CanonicalDeliveryMutationAllowed,
                readinessCode = snapshot.ReadinessCode,
                signals = snapshot.Signals.Select(value => new 사가정저밀도교통SignalData
                {
                    stableId = value.StableId,
                    axisCode = value.AxisCode,
                    stateCode = value.StateCode,
                    secondsUntilTransition = value.SecondsUntilTransition,
                    stopLineStableId = value.StopLineStableId,
                }).ToArray(),
                lanes = snapshot.Lanes.Select(value => new 사가정저밀도교통LaneData
                {
                    stableId = value.StableId,
                    sourceWayStableId = value.SourceWayStableId,
                    axisCode = value.AxisCode,
                    routeKindCode = value.RouteKindCode,
                    sourceDirectionCode = value.SourceDirectionCode,
                    sourceAccessReviewCode = value.SourceAccessReviewCode,
                    sourceRuntimeAuthorized = value.SourceRuntimeAuthorized,
                    hasStopLine = value.HasStopLine,
                    stopLineProgressMeters = value.StopLineProgressMeters,
                    pingPong = value.PingPong,
                    points = value.Points.Select(point => new 사가정저밀도교통PointData
                    {
                        x = point.X,
                        z = point.Z,
                    }).ToArray(),
                }).ToArray(),
                actors = snapshot.Actors.Select(value => new 사가정저밀도교통ActorData
                {
                    stableId = value.StableId,
                    kindCode = value.KindCode,
                    laneStableId = value.LaneStableId,
                    routeKindCode = value.RouteKindCode,
                    stateCode = value.StateCode,
                    displayPolylineOrderCode = value.DisplayPolylineOrderCode,
                    progressMeters = value.ProgressMeters,
                    speedMetersPerSecond = value.SpeedMetersPerSecond,
                    queueIndex = value.QueueIndex,
                    blockedReasonCode = value.BlockedReasonCode,
                    position = new 사가정저밀도교통PointData
                    {
                        x = value.Position.X,
                        z = value.Position.Z,
                    },
                    headingDegrees = value.HeadingDegrees,
                }).ToArray(),
                courierRouteLegs = snapshot.CourierRouteLegs.Select(value =>
                    new 사가정저밀도교통CourierRouteLegData
                    {
                        stableId = value.StableId,
                        index = value.Index,
                        laneStableId = value.LaneStableId,
                        stageCode = value.StageCode,
                        displayGeometryDistanceMeters =
                            value.DisplayGeometryDistanceMeters,
                        sourceDistanceMeters = value.SourceDistanceMeters,
                        sourceEdges = value.SourceEdges.Select(edge =>
                            new 사가정저밀도교통SourceEdgeData
                            {
                                edgeStableId = edge.EdgeStableId,
                                displayGeometryOrderCode = edge.DisplayGeometryOrderCode,
                                sourceDirectionCode = edge.SourceDirectionCode,
                                accessReviewCode = edge.AccessReviewCode,
                                runtimeAuthorized = edge.RuntimeAuthorized,
                                sourceDistanceMeters = edge.SourceDistanceMeters,
                                displayFrom = new 사가정저밀도교통PointData
                                {
                                    x = edge.DisplayFrom.X,
                                    z = edge.DisplayFrom.Z,
                                },
                                displayTo = new 사가정저밀도교통PointData
                                {
                                    x = edge.DisplayTo.X,
                                    z = edge.DisplayTo.Z,
                                },
                            }).ToArray(),
                    }).ToArray(),
                courierRouteState = new 사가정저밀도교통CourierRouteStateData
                {
                    currentRouteLegStableId =
                        snapshot.CourierRouteState.CurrentRouteLegStableId,
                    currentRouteLegIndex = snapshot.CourierRouteState.CurrentRouteLegIndex,
                    routeLegCount = snapshot.CourierRouteState.RouteLegCount,
                    currentLaneStableId = snapshot.CourierRouteState.CurrentLaneStableId,
                    stageCode = snapshot.CourierRouteState.StageCode,
                    legProgressMeters = snapshot.CourierRouteState.LegProgressMeters,
                    cumulativeProgressMeters =
                        snapshot.CourierRouteState.CumulativeProgressMeters,
                    totalDistanceMeters = snapshot.CourierRouteState.TotalDistanceMeters,
                    candidateEndpointReached =
                        snapshot.CourierRouteState.CandidateEndpointReached,
                },
            };

        private static string StageText(사가정저밀도교통검증Stage value)
            => value switch
            {
                사가정저밀도교통검증Stage.Automatic =>
                    "자동 · 대로→생활도로→골목→후보 종점",
                사가정저밀도교통검증Stage.Overview => "전체 · 대로 접근",
                사가정저밀도교통검증Stage.NeighborhoodRoad => "생활도로 접근",
                사가정저밀도교통검증Stage.CandidateEndpoint => "골목 후보 종점",
                _ => value.ToString(),
            };

        private static string RouteStageText(string value)
            => value switch
            {
                사가정저밀도교통WireCodes.MainRoadApproach => "대로 접근",
                사가정저밀도교통WireCodes.NeighborhoodRoadApproach => "생활도로 접근",
                사가정저밀도교통WireCodes.AlleyFinalApproach => "골목 최종 접근",
                사가정저밀도교통WireCodes.CandidateEndpointReached => "후보 종점 도달",
                _ => value,
            };

        private static string MotionText(string state)
            => state switch
            {
                사가정저밀도교통WireCodes.MovingInAlley => "골목 이동",
                사가정저밀도교통WireCodes.MovingOnNeighborhoodRoad => "생활도로 이동",
                사가정저밀도교통WireCodes.WaitingForAlley => "골목 진입 대기",
                사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint => "후보 종점 정차",
                사가정저밀도교통WireCodes.Moving => "이동",
                _ => state,
            };

        private static string PhaseText(string value)
            => value switch
            {
                사가정저밀도교통WireCodes.NorthSouthGreen => "남북 녹색",
                사가정저밀도교통WireCodes.NorthSouthAmber => "남북 황색",
                사가정저밀도교통WireCodes.AllRedBeforeEastWest => "전방향 적색",
                사가정저밀도교통WireCodes.EastWestGreen => "동서 녹색",
                사가정저밀도교통WireCodes.EastWestAmber => "동서 황색",
                사가정저밀도교통WireCodes.AllRedBeforeNorthSouth => "전방향 적색",
                _ => value,
            };

        private sealed class CameraState
        {
            public CameraState(Camera camera)
            {
                Camera = camera;
                Enabled = camera.enabled;
                Position = camera.transform.position;
                Rotation = camera.transform.rotation;
                Orthographic = camera.orthographic;
                OrthographicSize = camera.orthographicSize;
                FieldOfView = camera.fieldOfView;
            }

            public Camera Camera { get; }
            public bool Enabled { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public bool Orthographic { get; }
            public float OrthographicSize { get; }
            public float FieldOfView { get; }

            public void Restore()
            {
                if (Camera == null) return;
                Camera.enabled = Enabled;
                Camera.transform.SetPositionAndRotation(Position, Rotation);
                Camera.orthographic = Orthographic;
                Camera.orthographicSize = OrthographicSize;
                Camera.fieldOfView = FieldOfView;
            }
        }

        private sealed class CanvasState
        {
            public CanvasState(Canvas canvas, bool enabled)
            {
                Canvas = canvas;
                Enabled = enabled;
            }
            public Canvas Canvas { get; }
            public bool Enabled { get; }
        }

        private sealed class BehaviourState
        {
            public BehaviourState(Behaviour behaviour, bool enabled)
            {
                Behaviour = behaviour;
                Enabled = enabled;
            }
            public Behaviour Behaviour { get; }
            public bool Enabled { get; }
        }
    }
}
