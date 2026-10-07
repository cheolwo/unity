using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Runtime.WorldMap;
using Ssalddel.WorkflowRules.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 전용 graphics 프로세스에서 r46의 짧은 표현 수명만 관찰합니다.
    /// 명시적 VerificationSample을 기존 해석기·배치 계획기로 좁히며 Scene을 저장하지 않습니다.
    /// 실행: -executeMethod Ssalddel.Unity.Editor.생활관찰표현검증.Run
    /// -livingObservationCapture -livingObservationOutput &lt;비어 있는 출력 폴더&gt;
    /// -batchmode, -nographics, -quit 없이 실행해야 합니다.
    /// </summary>
    [InitializeOnLoad]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "공식 Scene의 짧은 실제 관찰·캡처 절차와 판본·해제·만료 증거를 기록한다.",
        Boundary = "도구 준비는 실제 실행 증거가 아니며 운영 API·Scene 저장·E 승격을 수행하지 않는다.")]
    public static class 생활관찰표현검증
    {
        public const string CommandLineMarker = "-livingObservationCapture";
        public const string OutputArgument = "-livingObservationOutput";
        public const string CanonicalScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string Prefix = "Ssalddel.LivingObservationCapture.";
        private const string FoodOs = OperationalWorldOperatingSystemIds.FoodDelivery;
        private const string CargoOs = OperationalWorldOperatingSystemIds.DomesticCargoTransport;
        private static 생활관찰표현Layer layer;
        private static 다중Os생명주기검증Layer lifecycle;
        private static 운영관찰WorldView world;
        private static 사가정운영디오라마View diorama;
        private static 세계지구본View globe;
        private static OperationalWorldSceneInterpreter interpreter;
        private static IDisposable captureLease;
        private static JArray checkpoints;
        private static JArray captures;
        private static 다중Os생명주기MarkerView cargoInstance;
        private static double phaseStarted;
        private static double hiddenStarted;
        private static double playStarted;
        private static long[] expectedRevisions;

        static 생활관찰표현검증()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            UnityEngine.Application.logMessageReceived -= ObserveLog;
            UnityEngine.Application.logMessageReceived += ObserveLog;
        }

        public static void Run()
        {
            if (!HasArgument(CommandLineMarker)) return;
            SessionState.SetInt(Prefix + "OwnerPid", System.Diagnostics.Process.GetCurrentProcess().Id);
            try
            {
                Require(!UnityEngine.Application.isBatchMode && !HasArgument("-nographics")
                    && !HasArgument("-quit"), "LivingObservationGraphicsProcessRequired");
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "LivingObservationEditModeStartRequired");
                Require(!SessionState.GetBool(Prefix + "Active", false), "LivingObservationAlreadyActive");
                for (var index = 0; index < SceneManager.sceneCount; index++)
                    Require(!SceneManager.GetSceneAt(index).isDirty, "LivingObservationDirtySceneAtStart");
                var output = Path.GetFullPath(Argument(OutputArgument));
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(),
                    "LivingObservationFreshOutputRequired");
                Directory.CreateDirectory(output);
                SessionState.SetString(Prefix + "Output", output);
                SessionState.SetString(Prefix + "SceneHash", Hash(SceneAbsolutePath));
                SessionState.SetString(Prefix + "StartedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                SessionState.SetString(Prefix + "DeadlineUtc", DateTime.UtcNow.AddMinutes(2).ToString("O", CultureInfo.InvariantCulture));
                SessionState.SetString(Prefix + "RunId", "living-observation-r46:" + Guid.NewGuid().ToString("N"));
                SessionState.SetString(Prefix + "Checkpoints", "[]");
                SessionState.SetString(Prefix + "Captures", "[]");
                SessionState.SetString(Prefix + "CameraEvidence", "{}");
                SessionState.SetString(Prefix + "FailureCode", string.Empty);
                foreach (var key in new[] { "Error", "Exception", "Assert", "Warning", "DisabledControllers" })
                    SessionState.SetInt(Prefix + key, 0);
                SessionState.SetInt(Prefix + "ConsoleErrorsAtStart", ConsoleErrors());
                SessionState.SetBool(Prefix + "Active", true);
                SessionState.SetBool(Prefix + "ProfilePassed", false);
                SessionState.SetInt(Prefix + "Phase", 1);
                EditorSceneManager.OpenScene(CanonicalScenePath, OpenSceneMode.Single);
                Require(SceneManager.GetActiveScene().path == CanonicalScenePath,
                    "LivingObservationCanonicalSceneRequired");
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(SafeFailure(error)); }
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!OwnedActiveProcess() || state != PlayModeStateChange.EnteredPlayMode) return;
            // 첫 Update 전에 이 검증과 같은 운영 관찰 Controller만 중지합니다.
            // 다른 서버/Simulation Controller의 기존 오류는 Console 집계에 그대로 남깁니다.
            foreach (var controller in SceneComponents<운영관찰WorldController>())
            {
                if (!controller.enabled) continue;
                controller.enabled = false;
                SessionState.SetInt(Prefix + "DisabledControllers",
                    SessionState.GetInt(Prefix + "DisabledControllers", 0) + 1);
            }
        }

        private static void Tick()
        {
            if (!OwnedActiveProcess()) return;
            var phase = SessionState.GetInt(Prefix + "Phase", 0);
            if (phase == 90) { ExitWhenStopped(); return; }
            try
            {
                Require(DateTime.UtcNow <= DateTime.Parse(SessionState.GetString(Prefix + "DeadlineUtc", ""),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), "LivingObservationStartupTimeout");
                if (phase == 1)
                {
                    if (!EditorApplication.isPlaying) return;
                    InitializePlay();
                    return;
                }
                Require(EditorApplication.isPlaying && layer != null, "LivingObservationUnexpectedPlayStopOrReload");
                Require(Time.unscaledTimeAsDouble - playStarted <= 60, "LivingObservationProfileTimeout");
                Require(!SceneComponents<운영관찰WorldController>().Any(value => value.enabled),
                    "LivingObservationHttpControllerReenabled");
                switch (phase)
                {
                    case 15:
                        if (Time.unscaledTimeAsDouble - phaseStarted < 1.2 || globe.IsDioramaTransitioning) return;
                        ValidateCaptureCamera();
                        Request(생활관찰단계.Street, 2);
                        break;
                    case 2:
                        if (!StageReady(생활관찰단계.Street)) return;
                        Require(layer.WorkCount == 2 && FoodMarkers().Length == 2, "LivingObservationStreetCountMismatch");
                        lifecycle.FocusCase("food-recovery");
                        Checkpoint("street-initial");
                        Capture("01-street.png", 3, 생활관찰단계.Street);
                        break;
                    case 3:
                        if (!CaptureReady()) return;
                        Request(생활관찰단계.Neighborhood, 4);
                        break;
                    case 4:
                        if (!StageReady(생활관찰단계.Neighborhood)) return;
                        Require(!layer.DetailVisible && layer.DetailRetained, "LivingObservationHideGraceMissing");
                        Require(FoodMarkers().All(value => !value.gameObject.activeInHierarchy), "LivingObservationHiddenMarkerActive");
                        hiddenStarted = Time.unscaledTimeAsDouble;
                        Checkpoint("neighborhood-hidden-retained");
                        Capture("02-neighborhood.png", 5, 생활관찰단계.Neighborhood);
                        break;
                    case 5:
                        if (!CaptureReady()) return;
                        Request(생활관찰단계.District, 6);
                        break;
                    case 6:
                        if (!StageReady(생활관찰단계.District)) return;
                        Require(layer.WorkCount == 2 && !layer.DetailVisible, "LivingObservationDistrictSummaryMismatch");
                        Checkpoint("district-summary");
                        Capture("03-district.png", 7, 생활관찰단계.District);
                        break;
                    case 7:
                        if (!CaptureReady()) return;
                        SetPhase(8);
                        break;
                    case 8:
                        if (Time.unscaledTimeAsDouble - hiddenStarted < 5.2) return;
                        Require(!layer.DetailRetained && FoodMarkers().Length == 0, "LivingObservationGraceReleaseFailed");
                        Require(layer.WorkCount == 2, "LivingObservationReleaseDeletedState");
                        Checkpoint("released-after-real-five-seconds");
                        ApplyFixture(125, 120, 0);
                        Request(생활관찰단계.Street, 9);
                        break;
                    case 9:
                        if (!StageReady(생활관찰단계.Street)) return;
                        Require(FoodMarkers().Length == 2 && layer.DetailVisible, "LivingObservationReentryMissing");
                        Require(FoodMarkers().Select(value => value.Revision).OrderBy(value => value)
                            .SequenceEqual(expectedRevisions), "LivingObservationReentryRevisionMismatch");
                        lifecycle.FocusCase("food-recovery");
                        Checkpoint("street-latest-revision-reentered");
                        Capture("04-street-latest-revision.png", 10, 생활관찰단계.Street);
                        break;
                    case 10:
                        if (!CaptureReady()) return;
                        // 명시된 TTL 시험 revision이며 업무 실행이나 가상 Tick 전진이 아닙니다.
                        ApplyFixture(160, 2, 100);
                        SetPhase(11);
                        break;
                    case 11:
                        if (Time.unscaledTimeAsDouble - phaseStarted < 2.5) return;
                        Require(layer.WorkCount == 0 && layer.CurrentItems.Count == 0 && FoodMarkers().Length == 0,
                            "LivingObservationRealTtlFailed");
                        Checkpoint("expired-on-real-utc-clock");
                        Capture("05-expired.png", 12, 생활관찰단계.Street);
                        break;
                    case 12:
                        if (!CaptureReady()) return;
                        ApplyFixture(160, 120, 200);
                        Require(layer.WorkCount == 2, "LivingObservationClearSetupMissing");
                        layer.Clear();
                        // Clear 직후 계약을 기록하고 다음 프레임의 지연 파기까지 별도 확인합니다.
                        Require(layer.WorkCount == 0 && !layer.DetailRetained
                            && layer.CurrentStage == 생활관찰단계.Neighborhood, "LivingObservationClearStateFailed");
                        layer.RequestStage(생활관찰단계.Neighborhood, Time.unscaledTimeAsDouble, true);
                        SetPhase(13);
                        break;
                    case 13:
                        if (Time.unscaledTimeAsDouble - phaseStarted < .25) return;
                        Require(layer.CurrentItems.Count == 0 && FoodMarkers().Length == 0,
                            "LivingObservationClearObjectsFailed");
                        Checkpoint("cleared-with-other-os-preserved");
                        Capture("06-cleared.png", 14, 생활관찰단계.Neighborhood);
                        break;
                    case 14:
                        if (!CaptureReady()) return;
                        SessionState.SetBool(Prefix + "ProfilePassed", true);
                        Stop();
                        break;
                    default: throw new InvalidOperationException("LivingObservationUnknownPhase");
                }
            }
            catch (Exception error) { Fail(SafeFailure(error)); }
        }

        private static void InitializePlay()
        {
            Require(SceneManager.GetActiveScene().path == CanonicalScenePath, "LivingObservationCanonicalSceneRequired");
            world = SceneComponents<운영관찰WorldView>().Single();
            diorama = world.GetComponent<사가정운영디오라마View>();
            Require(diorama != null, "LivingObservationDioramaMissing");
            diorama.Initialize();
            Require(diorama.MapStatus == "Ready" && diorama.ObservationCamera != null,
                "LivingObservationDioramaNotReady");
            Require(사가정GameView검증CaptureCoordinator.TryAcquire("LivingObservationR46", out captureLease),
                "LivingObservationCaptureAlreadyOwned");
            lifecycle = world.GetComponent<다중Os생명주기검증Layer>()
                ?? world.gameObject.AddComponent<다중Os생명주기검증Layer>();
            lifecycle.Configure(world, diorama);
            layer = world.GetComponent<생활관찰표현Layer>() ?? world.gameObject.AddComponent<생활관찰표현Layer>();
            layer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            layer.Configure(world, lifecycle, diorama, 다중Os생명주기검증Policy.AreaStableId);
            world.Clear();
            lifecycle.ClearLifecycleStates();
            layer.Clear();
            interpreter = new OperationalWorldSceneInterpreter();
            checkpoints = JArray.Parse(SessionState.GetString(Prefix + "Checkpoints", "[]"));
            captures = JArray.Parse(SessionState.GetString(Prefix + "Captures", "[]"));
            playStarted = Time.unscaledTimeAsDouble;
            SessionState.SetString(Prefix + "PlayStartedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            diorama.GameOverlayVisible = true;
            diorama.SetStatus("VerificationSample · 음식 정상/회복 · r46 격리 표현 검증");
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            Require(gameViewType != null, "LivingObservationGameViewUnavailable");
            var window = EditorWindow.GetWindow(gameViewType);
            ConfigureGameViewSize(window, gameViewType);
            window.Show();
            window.Focus();
            ApplyFixture(80, 120, 0);
            var cargo = lifecycle.GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                .Single(value => value.OperatingSystemId == CargoOs);
            cargoInstance = cargo;
            // 상태 플래그만 바꾸면 depth 1000의 지구본 카메라가 Game View를 계속 덮습니다.
            // 기존 공식 전환이 카메라·Canvas·관찰 계층을 함께 전환하도록 요청합니다.
            globe = SceneComponents<세계지구본View>().Single();
            Require(globe.OpenRegionalDiorama(), "LivingObservationOfficialDioramaTransitionRejected");
            SetPhase(15);
        }

        private static void ApplyFixture(int moment, int ttlSeconds, int revisionOffset)
        {
            var now = DateTime.UtcNow;
            var epoch = DateTime.Parse(SessionState.GetString(Prefix + "StartedUtc", ""),
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).AddMinutes(-10);
            var runId = SessionState.GetString(Prefix + "RunId", "");
            var response = 다중Os생명주기검증Fixture.CreateResponse(runId, moment, epoch);
            var food = response.Items.Where(value => value.OperatingSystemId == FoodOs).ToArray();
            Require(food.Length == 2, "LivingObservationFoodFixtureCountMismatch");
            foreach (var item in food)
            {
                Require(item.SourceKindCode == OperationalWorldSceneSourceKinds.VerificationSample,
                    "LivingObservationVerificationSampleRequired");
                item.Revision += revisionOffset;
                item.ExpiresAtUtc = now.AddSeconds(ttlSeconds);
            }
            var cargo = 다중Os생명주기검증Fixture.CreateResponse(runId, 125, epoch).Items
                .Single(value => value.SnapshotStableId.EndsWith(":cargo-normal", StringComparison.Ordinal));
            cargo.ExpiresAtUtc = now.AddMinutes(2);
            response.Items = food.Concat(new[] { cargo }).ToArray();
            response.AsOfUtc = now;
            var result = interpreter.Apply(response, now);
            Require(result.Accepted && result.Diagnostics.Length == 0, "LivingObservationInterpreterRejected");
            var plan = new OperationalWorldScenePlacementPlanner().Create(result);
            Require(plan.Accepted && plan.Diagnostics.Length == 0, "LivingObservationPlacementRejected");
            Require(layer.Apply(plan, result.Diagnostics, now), "LivingObservationLayerRejected");
            expectedRevisions = food.Select(value => value.Revision).OrderBy(value => value).ToArray();
        }

        private static void Request(생활관찰단계 stage, int nextPhase)
        {
            layer.RequestStage(stage, Time.unscaledTimeAsDouble, true);
            SetPhase(nextPhase);
        }

        private static bool StageReady(생활관찰단계 stage)
            => Time.unscaledTimeAsDouble - phaseStarted >= 1.5 && layer.CurrentStage == stage;

        private static void SetPhase(int phase)
        {
            phaseStarted = Time.unscaledTimeAsDouble;
            SessionState.SetInt(Prefix + "Phase", phase);
        }

        private static 다중Os생명주기MarkerView[] FoodMarkers()
            => lifecycle.GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                .Where(value => value.OperatingSystemId == FoodOs).ToArray();

        private static void Checkpoint(string code)
        {
            var markers = lifecycle.GetComponentsInChildren<다중Os생명주기MarkerView>(true);
            var cargo = markers.SingleOrDefault(value => value.OperatingSystemId == CargoOs);
            Require(cargo != null && ReferenceEquals(cargo, cargoInstance), "LivingObservationOtherOsChanged");
            Require(markers.Length <= 3 && layer.CurrentItems.Count <= 128, "LivingObservationObjectBudgetExceeded");
            checkpoints.Add(new JObject
            {
                ["code"] = code,
                ["observedUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["playElapsedSeconds"] = Time.unscaledTimeAsDouble - playStarted,
                ["stage"] = layer.CurrentStage.ToString(),
                ["detailVisible"] = layer.DetailVisible,
                ["detailRetained"] = layer.DetailRetained,
                ["workCount"] = layer.WorkCount,
                ["copyCount"] = layer.CurrentItems.Count,
                ["foodMarkerCount"] = markers.Count(value => value.OperatingSystemId == FoodOs),
                ["activeFoodMarkerCount"] = markers.Count(value => value.OperatingSystemId == FoodOs && value.gameObject.activeInHierarchy),
                ["worldObjectCount"] = world.ObjectCount,
                ["lifecycleMarkerCount"] = markers.Length,
                ["boundMaterialCount"] = markers.SelectMany(value => value.GetComponentsInChildren<Renderer>(true))
                    .SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count(),
                ["globalMaterialCount"] = Resources.FindObjectsOfTypeAll<Material>().Length,
                ["otherOsSameInstance"] = true,
                ["camera"] = CameraEvidence(),
                ["revisions"] = new JArray(layer.CurrentItems.OrderBy(value => value.ObjectStableId).Select(value => new JObject
                {
                    ["objectStableId"] = value.ObjectStableId, ["revision"] = value.Revision,
                    ["sourceKindCode"] = value.SourceKindCode, ["scenarioRunStableId"] = value.ScenarioRunStableId
                }))
            });
            SessionState.SetString(Prefix + "Checkpoints", checkpoints.ToString(Formatting.None));
        }

        private static void Capture(string fileName, int nextPhase, 생활관찰단계 expectedStage)
        {
            ValidateCaptureCamera();
            Require(layer.CurrentStage == expectedStage, "LivingObservationCaptureStageMismatch");
            var path = Path.Combine(SessionState.GetString(Prefix + "Output", ""), fileName);
            Require(!File.Exists(path), "LivingObservationCaptureAlreadyExists");
            SessionState.SetString(Prefix + "PendingCapture", path);
            SessionState.SetInt(Prefix + "CaptureFrame", Time.frameCount);
            SessionState.SetString(Prefix + "PendingCaptureExpectedStage", expectedStage.ToString());
            SessionState.SetString(Prefix + "PendingCaptureCamera",
                SessionState.GetString(Prefix + "CameraEvidence", "{}"));
            ScreenCapture.CaptureScreenshot(path);
            SetPhase(nextPhase);
        }

        private static bool CaptureReady()
        {
            ValidateCaptureCamera();
            var path = SessionState.GetString(Prefix + "PendingCapture", "");
            if (Time.frameCount <= SessionState.GetInt(Prefix + "CaptureFrame", 0) + 1 || !File.Exists(path)) return false;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 32) return false;
            Require(bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71,
                "LivingObservationCaptureNotPng");
            var width = BigEndianInt(bytes, 16);
            var height = BigEndianInt(bytes, 20);
            Require(width >= 640 && height >= 360, "LivingObservationCaptureTooSmall");
            captures.Add(new JObject { ["fileName"] = Path.GetFileName(path), ["sha256"] = Hash(path),
                ["bytes"] = bytes.Length, ["width"] = width, ["height"] = height, ["actualScreenCapture"] = true,
                ["expectedStage"] = SessionState.GetString(Prefix + "PendingCaptureExpectedStage", ""),
                ["cameraAtCaptureRequest"] = JObject.Parse(SessionState.GetString(Prefix + "PendingCaptureCamera", "{}")),
                ["cameraAtCaptureCompletion"] = CameraEvidence() });
            SessionState.SetString(Prefix + "Captures", captures.ToString(Formatting.None));
            return true;
        }

        private static void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (!OwnedActiveProcess()) return;
            var code = type == LogType.Exception ? "Exception" : type == LogType.Error ? "Error"
                : type == LogType.Assert ? "Assert" : type == LogType.Warning ? "Warning" : null;
            if (code == null) return;
            // 원문 condition/stackTrace에는 주소·토큰·원장 정보가 포함될 수 있어 기록하지 않습니다.
            SessionState.SetInt(Prefix + code, SessionState.GetInt(Prefix + code, 0) + 1);
        }

        private static void Fail(string code)
        {
            SessionState.SetString(Prefix + "FailureCode", code);
            SessionState.SetBool(Prefix + "ProfilePassed", false);
            if (!SessionState.GetBool(Prefix + "Active", false))
            {
                try { RestoreGameViewSize(); WriteManifest(); }
                finally { ExitOwned(2); }
                return;
            }
            Stop();
        }

        private static void Stop()
        {
            SessionState.SetString(Prefix + "ProfileFinishedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetInt(Prefix + "Phase", 90);
            captureLease?.Dispose();
            captureLease = null;
            WriteManifest();
            EditorApplication.isPlaying = false;
        }

        private static void ExitWhenStopped()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (Hash(SceneAbsolutePath) != SessionState.GetString(Prefix + "SceneHash", ""))
                {
                    SessionState.SetBool(Prefix + "ProfilePassed", false);
                    SessionState.SetString(Prefix + "FailureCode", "LivingObservationSavedSceneHashChanged");
                }
                try { RestoreGameViewSize(); }
                catch
                {
                    SessionState.SetBool(Prefix + "ProfilePassed", false);
                    SessionState.SetString(Prefix + "FailureCode", "LivingObservationGameViewRestorationFailed");
                }
                WriteManifest();
            }
            finally
            {
                SessionState.SetBool(Prefix + "Active", false);
                ExitOwned(SessionState.GetBool(Prefix + "ProfilePassed", false) ? 0 : 2);
            }
        }

        private static void WriteManifest()
        {
            var output = SessionState.GetString(Prefix + "Output", "");
            if (string.IsNullOrEmpty(output) || !Directory.Exists(output)) return;
            var before = SessionState.GetString(Prefix + "SceneHash", "");
            var after = File.Exists(SceneAbsolutePath) ? Hash(SceneAbsolutePath) : string.Empty;
            var errors = SessionState.GetInt(Prefix + "Error", 0);
            var exceptions = SessionState.GetInt(Prefix + "Exception", 0);
            var asserts = SessionState.GetInt(Prefix + "Assert", 0);
            var manifest = new JObject
            {
                ["schema"] = "living-observation-capture.r46.v2",
                ["profilePassed"] = SessionState.GetBool(Prefix + "ProfilePassed", false),
                ["failureCode"] = SessionState.GetString(Prefix + "FailureCode", ""),
                ["sourceKindCode"] = OperationalWorldSceneSourceKinds.VerificationSample,
                ["sourceMode"] = "ExplicitIsolatedFixtureThroughInterpreterAndPlacementPlanner",
                ["scenarioRunStableId"] = SessionState.GetString(Prefix + "RunId", ""),
                ["scope"] = "FoodNormalAndRecoveryTwoWorksWithOneCargoPreservationProbe",
                ["maximumPlayProfileSeconds"] = 60,
                ["fiveMinuteOrTenMinuteStabilityClaimed"] = false,
                ["usesActualHttp"] = false, ["operationalEffectsAllowed"] = false,
                ["sceneSaveCalled"] = false, ["evidenceStageClaimed"] = null,
                ["gameViewTargetWidth"] = 1280, ["gameViewTargetHeight"] = 720,
                ["gameViewSettingsRestored"] = !SessionState.GetBool(Prefix + "GameViewSizeChanged", false),
                ["officialDioramaTransitionRequired"] = true,
                ["captureCameraGuardRequired"] = true,
                ["humanVisualReviewRequired"] = true,
                ["humanVisualReviewStatus"] = "Pending",
                ["lastCameraEvidence"] = JObject.Parse(SessionState.GetString(Prefix + "CameraEvidence", "{}")),
                ["startedUtc"] = SessionState.GetString(Prefix + "StartedUtc", ""),
                ["playStartedUtc"] = SessionState.GetString(Prefix + "PlayStartedUtc", ""),
                ["finishedUtc"] = SessionState.GetString(Prefix + "ProfileFinishedUtc", ""),
                ["scenePath"] = CanonicalScenePath, ["sceneHashBefore"] = before,
                ["sceneHashAfter"] = after, ["savedSceneUnchanged"] = before == after && before.Length > 0,
                ["disabledOperationalObservationControllers"] = SessionState.GetInt(Prefix + "DisabledControllers", 0),
                ["otherControllersDisabledByHelper"] = 0,
                ["unrelatedSceneControllersMayAttemptHttp"] = true,
                ["consoleErrorsAtStart"] = SessionState.GetInt(Prefix + "ConsoleErrorsAtStart", -1),
                ["consoleErrorsAtEnd"] = ConsoleErrors(),
                ["runErrorCount"] = errors, ["runExceptionCount"] = exceptions,
                ["runAssertCount"] = asserts, ["runErrorAndExceptionTotal"] = errors + exceptions + asserts,
                ["runWarningCount"] = SessionState.GetInt(Prefix + "Warning", 0),
                ["consoleRawMessagesStored"] = false,
                ["consoleCleanDuringProfile"] = errors + exceptions + asserts == 0,
                ["checkpoints"] = JArray.Parse(SessionState.GetString(Prefix + "Checkpoints", "[]")),
                ["captures"] = JArray.Parse(SessionState.GetString(Prefix + "Captures", "[]"))
            };
            File.WriteAllText(Path.Combine(output, "capture-manifest.json"), manifest.ToString(Formatting.Indented));
        }

        private static int ConsoleErrors()
        {
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
                var method = type?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) return -1;
                var values = new object[] { 0, 0, 0 };
                method.Invoke(null, values);
                return (int)values[0];
            }
            catch { return -1; }
        }

        private static Camera GlobeCamera()
            => globe == null ? null : globe.GetComponentsInChildren<Camera>(true)
                .SingleOrDefault(value => value.name == 세계지구본View.GlobeCameraName);

        private static void ValidateCaptureCamera()
        {
            SessionState.SetString(Prefix + "CameraEvidence", CameraEvidence().ToString(Formatting.None));
            var globeCamera = GlobeCamera();
            var observation = diorama != null ? diorama.ObservationCamera : null;
            Require(globe != null && !globe.IsDioramaTransitioning
                && 세계지도표현상태.CurrentLayer == 세계지도표현계층.Diorama
                && !세계지도표현상태.GlobeOverlayVisible, "LivingObservationDioramaTransitionIncomplete");
            Require(globeCamera != null && !globeCamera.enabled, "LivingObservationGlobeCameraStillEnabled");
            Require(observation != null && observation.enabled && observation.gameObject.activeInHierarchy
                && observation.targetTexture == null, "LivingObservationCaptureCameraInactive");
        }

        private static JObject CameraEvidence()
        {
            var observation = diorama != null ? diorama.ObservationCamera : null;
            var globeCamera = GlobeCamera();
            return new JObject
            {
                ["frame"] = Time.frameCount,
                ["actualStage"] = layer != null ? layer.CurrentStage.ToString() : "NotConfigured",
                ["currentObservationLayer"] = 세계지도표현상태.CurrentLayer.ToString(),
                ["globeOverlayVisible"] = 세계지도표현상태.GlobeOverlayVisible,
                ["dioramaTransitioning"] = globe != null && globe.IsDioramaTransitioning,
                ["globeCameraFound"] = globeCamera != null,
                ["globeCameraEnabled"] = globeCamera != null && globeCamera.enabled,
                ["observationCameraEnabled"] = observation != null && observation.enabled,
                ["observationCameraDepth"] = observation != null ? observation.depth : 0,
                ["observationOrthographicSize"] = observation != null ? observation.orthographicSize : 0,
                ["wholeViewSize"] = diorama != null ? diorama.WholeViewSize : 0,
                ["activeCameras"] = new JArray(SceneComponents<Camera>()
                    .Where(value => value.enabled && value.gameObject.activeInHierarchy)
                    .OrderBy(value => value.depth).Select(value => new JObject
                    {
                        ["name"] = value.name, ["depth"] = value.depth,
                        ["isObservationCamera"] = value == observation, ["isGlobeCamera"] = value == globeCamera,
                        ["cullingMask"] = value.cullingMask, ["targetDisplay"] = value.targetDisplay,
                        ["rendersToTexture"] = value.targetTexture != null,
                        ["orthographic"] = value.orthographic, ["orthographicSize"] = value.orthographicSize,
                        ["pixelWidth"] = value.pixelWidth, ["pixelHeight"] = value.pixelHeight
                    }))
            };
        }

        private static void ConfigureGameViewSize(EditorWindow window, Type viewType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var selected = viewType.GetProperty("selectedSizeIndex", flags);
            Require(selected != null && selected.CanRead && selected.CanWrite, "LivingObservationGameViewSizeApiUnavailable");
            SessionState.SetInt(Prefix + "OriginalGameViewSize", (int)selected.GetValue(window));
            SessionState.SetInt(Prefix + "AddedGameViewSize", -1);
            var group = GameViewSizeGroup();
            var groupType = group.GetType();
            var builtinCount = (int)groupType.GetMethod("GetBuiltinCount", flags).Invoke(group, null);
            var totalCount = (int)groupType.GetMethod("GetTotalCount", flags).Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize", flags);
            var selectedIndex = -1;
            for (var index = 0; index < totalCount; index++)
            {
                var size = getSize.Invoke(group, new object[] { index });
                var sizeType = size.GetType();
                if ((int)sizeType.GetProperty("width", flags).GetValue(size) == 1280
                    && (int)sizeType.GetProperty("height", flags).GetValue(size) == 720)
                { selectedIndex = index; break; }
            }
            if (selectedIndex < 0)
            {
                var assembly = typeof(EditorWindow).Assembly;
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                var enumType = assembly.GetType("UnityEditor.GameViewSizeType");
                Require(sizeType != null && enumType != null, "LivingObservationGameViewFixedSizeUnavailable");
                var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { Enum.Parse(enumType, "FixedResolution"), (object)1280, 720, "r46 observation capture" },
                    CultureInfo.InvariantCulture);
                groupType.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                selectedIndex = totalCount;
                SessionState.SetInt(Prefix + "AddedGameViewSize", selectedIndex - builtinCount);
            }
            selected.SetValue(window, selectedIndex);
            SessionState.SetBool(Prefix + "GameViewSizeChanged", true);
        }

        private static object GameViewSizeGroup()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            Require(type != null, "LivingObservationGameViewSizesUnavailable");
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(type);
            var instance = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var currentGroup = type.GetProperty("currentGroupType", flags).GetValue(instance);
            return type.GetMethod("GetGroup", flags).Invoke(instance, new[] { currentGroup });
        }

        private static void RestoreGameViewSize()
        {
            if (!SessionState.GetBool(Prefix + "GameViewSizeChanged", false)) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(window,
                SessionState.GetInt(Prefix + "OriginalGameViewSize", 0));
            var added = SessionState.GetInt(Prefix + "AddedGameViewSize", -1);
            if (added >= 0)
            {
                var group = GameViewSizeGroup();
                group.GetType().GetMethod("RemoveCustomSize", flags).Invoke(group, new object[] { added });
            }
            SessionState.SetBool(Prefix + "GameViewSizeChanged", false);
        }

        private static string SafeFailure(Exception error)
            => error is InvalidOperationException && error.Message.StartsWith("LivingObservation", StringComparison.Ordinal)
                ? error.Message : "LivingObservation" + error.GetType().Name;

        private static bool OwnedActiveProcess() => HasArgument(CommandLineMarker)
            && SessionState.GetBool(Prefix + "Active", false)
            && SessionState.GetInt(Prefix + "OwnerPid", -1) == System.Diagnostics.Process.GetCurrentProcess().Id;

        private static void ExitOwned(int code)
        {
            if (HasArgument(CommandLineMarker)
                && SessionState.GetInt(Prefix + "OwnerPid", -1) == System.Diagnostics.Process.GetCurrentProcess().Id)
                EditorApplication.Exit(code);
        }

        private static T[] SceneComponents<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>().Where(value => value != null
                && value.gameObject.scene.IsValid() && value.gameObject.scene.path == CanonicalScenePath).ToArray();

        private static bool HasArgument(string name) => Environment.GetCommandLineArgs().Contains(name);
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            Require(index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal),
                "LivingObservationOutputArgumentRequired");
            return args[index + 1];
        }
        private static string SceneAbsolutePath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", CanonicalScenePath));
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static int BigEndianInt(byte[] value, int index)
            => value[index] << 24 | value[index + 1] << 16 | value[index + 2] << 8 | value[index + 3];
        private static void Require(bool condition, string code)
        {
            if (!condition) throw new InvalidOperationException(code);
        }
    }
}
