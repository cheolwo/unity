using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical SimulationWorldShell의 저장되지 않는 표현 계층을 조작합니다.
    /// 이 메뉴가 만드는 화면은 동결 표현 타임라인 증거이며 실제 HTTP E2E가 아닙니다.
    /// </summary>
    public static class 다중Os생명주기GameView검증
    {
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/1 전체 보기")]
        public static void 전체보기()
            => EnsureDriver().SetMoment(다중Os생명주기검증Moment.Overview);

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/2 음식배달 회복 확대")]
        public static void 음식배달회복확대()
            => EnsureDriver().SetMoment(다중Os생명주기검증Moment.FoodRecovery);

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/3 창고·화물·마트 인계 확대")]
        public static void 창고화물마트인계확대()
            => EnsureDriver().SetMoment(
                다중Os생명주기검증Moment.WarehouseCargoMartHandoff);

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/4 600초 자동 재생")]
        public static void 자동재생() => EnsureDriver().BeginAutomatic();

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/5 전체+확대 사진 3장")]
        public static void 증거사진캡처()
        {
            var driver = EnsureDriver();
            var root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..",
                "Documentation", "Changes",
                "2026-09-13-sagajeong-multi-os-lifecycle",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            driver.BeginEvidenceCapture(root);
            Debug.Log("[사가정 네 OS 생명주기] 동결 표현 타임라인 캡처 시작: " + root
                      + " · liveHttpEndToEnd=false · Scene 저장 없음");
        }

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/6 Temp 음식점 아이콘 다시 읽기")]
        public static void 음식점아이콘다시읽기()
        {
            var driver = EnsureDriver();
            var loaded = driver.ReloadRestaurantFixture();
            Debug.Log("[사가정 네 OS 생명주기] 음식점 아이콘 입력: "
                      + (loaded ? "sanitized local fixture 적용" : "restaurant-icon input pending"));
        }

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/8 실제 HTTP 읽기 계층으로 복귀")]
        public static void 실제Http읽기계층으로복귀()
        {
            RemoveDriver();
            var controller = FindSceneComponent<운영관찰WorldController>();
            if (controller == null)
                throw new InvalidOperationException("OperationalWorldControllerMissing");
            controller.enabled = true;
            Debug.Log("[사가정 네 OS 생명주기] 기존 v2 GET Controller를 다시 활성화했습니다. "
                      + "Success 상태와 서버 실행을 별도로 확인해야 HTTP E2E 증거가 됩니다.");
        }

        [MenuItem("Ssalddel/검증/사가정 네 OS 생명주기/9 임시 검증 Driver 제거")]
        public static void 정리() => RemoveDriver();

        internal static 다중Os생명주기GameView검증Driver EnsureDriverForAutomation()
            => EnsureDriver();

        private static 다중Os생명주기GameView검증Driver EnsureDriver()
        {
            RequirePlayModeAndCanonicalScene();
            var existing = FindSceneComponent<다중Os생명주기GameView검증Driver>();
            if (existing != null) return existing;

            var controller = FindSceneComponent<운영관찰WorldController>()
                             ?? throw new InvalidOperationException(
                                 "OperationalWorldControllerMissing");
            var worldView = controller.GetComponent<운영관찰WorldView>()
                            ?? FindSceneComponent<운영관찰WorldView>()
                            ?? throw new InvalidOperationException(
                                "OperationalWorldViewMissing");
            var diorama = controller.GetComponent<사가정운영디오라마View>()
                           ?? FindSceneComponent<사가정운영디오라마View>()
                           ?? throw new InvalidOperationException(
                               "SagajeongOperationalDioramaMissing");
            diorama.Initialize();
            if (diorama.MapStatus != "Ready" || diorama.ObservationCamera == null)
                throw new InvalidOperationException("SagajeongOperationalDioramaNotReady");

            var driver = controller.gameObject.AddComponent<다중Os생명주기GameView검증Driver>();
            driver.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            try
            {
                driver.Initialize(worldView, diorama);
                Debug.Log("[사가정 네 OS 생명주기] "
                          + 다중Os생명주기검증Policy.FixtureRevision
                          + " 동결 표현 타임라인 준비 · HTTP/Command/Scene 저장 없음");
                return driver;
            }
            catch
            {
                UnityEngine.Object.Destroy(driver);
                throw;
            }
        }

        private static void RemoveDriver()
        {
            var driver = FindSceneComponent<다중Os생명주기GameView검증Driver>();
            if (driver != null) UnityEngine.Object.Destroy(driver);
        }

        private static T FindSceneComponent<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());

        private static void RequirePlayModeAndCanonicalScene()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("MultiOsLifecyclePlayModeRequired");
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid()
                || !string.Equals(active.path, CanonicalScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MultiOsLifecycleCanonicalSceneRequired:" + active.path);
        }
    }

    /// <summary>
    /// 그래픽이 활성화된 격리 Unity 프로세스에서 canonical Scene을 저장하지 않고
    /// Play Mode 진입, 사진 3장과 manifest 생성, 종료까지 자동 수행합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SagajeongMultiOsLifecycleCliCapture
    {
        public const string ExecuteMethod =
            "Ssalddel.Unity.Editor.SagajeongMultiOsLifecycleCliCapture.Run";
        public const string CommandLineMarker =
            다중Os생명주기GameView검증Driver.CliCaptureCommandLineMarker;
        public const string OutputArgument =
            "-sagajeongMultiOsLifecycleOutput";
        public const string OutputEnvironmentVariable =
            "SSALDDEL_MULTI_OS_LIFECYCLE_CAPTURE_DIR";
        public const string RestaurantFixtureSourceEnvironmentVariable =
            "SSALDDEL_SAGAJEONG_RESTAURANT_FIXTURE_SOURCE";
        public const string RestaurantFixtureSourceHashEnvironmentVariable =
            "SSALDDEL_SAGAJEONG_RESTAURANT_FIXTURE_SOURCE_SHA256";

        private const string SessionPrefix =
            "Ssalddel.SagajeongMultiOsLifecycleCliCapture.";
        private const string ActiveKey = SessionPrefix + "Active";
        private const string PhaseKey = SessionPrefix + "Phase";
        private const string OutputKey = SessionPrefix + "Output";
        private const string DeadlineKey = SessionPrefix + "Deadline";
        private const string WarmupKey = SessionPrefix + "Warmup";
        private const string ResultKey = SessionPrefix + "Result";
        private const string RestaurantFixtureHashKey =
            SessionPrefix + "RestaurantFixtureHash";
        private const int RequiredRestaurantIconCount = 12;
        private const int MaximumRestaurantFixtureBytes = 256 * 1024;

        [Serializable]
        private sealed class CaptureManifestCheck
        {
            public string evidenceSource = string.Empty;
            public string movementMode = string.Empty;
            public bool liveHttpEndToEnd;
            public bool operationalEffectsAllowed;
            public bool routeAuthority;
            public bool sceneSaved;
            public string restaurantIconInputStatus = string.Empty;
            public int restaurantIconCount;
            public string restaurantFixtureHashSha256 = string.Empty;
            public MovementSampleCheck[] movementSamples =
                Array.Empty<MovementSampleCheck>();
        }

        [Serializable]
        private sealed class MovementSampleCheck
        {
            public string operatingSystemId = string.Empty;
            public string fromStageCode = string.Empty;
            public string toStageCode = string.Empty;
            public string fromSemanticPlaceStableId = string.Empty;
            public string toSemanticPlaceStableId = string.Empty;
            public PositionCheck fromPosition = new();
            public PositionCheck intermediatePosition = new();
            public PositionCheck toPosition = new();
            public float distance;
            public float intermediateDistance;
            public float intermediateProgress01;
            public bool sampledInPlayMode;
            public int observedFrameCount;
        }

        [Serializable]
        private sealed class PositionCheck
        {
            public float x;
            public float y;
            public float z;
        }

        private enum Phase
        {
            None,
            AwaitingPlayMode,
            Capturing,
            StoppingSuccess,
            StoppingFailure
        }

        static SagajeongMultiOsLifecycleCliCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Unity.exe -projectPath &lt;isolated-project&gt; -executeMethod
        /// Ssalddel.Unity.Editor.SagajeongMultiOsLifecycleCliCapture.Run
        /// -sagajeongMultiOsLifecycleCliCapture
        /// -sagajeongMultiOsLifecycleOutput &lt;new-absolute-directory&gt; -logFile &lt;log&gt;
        /// -batchmode, -nographics, -quit은 사용하지 않습니다.
        /// </summary>
        public static void Run()
        {
            if (!HasArgument(CommandLineMarker))
            {
                Debug.LogError("[SagajeongMultiOsLifecycleCliCapture] 전용 표식 없음: "
                               + CommandLineMarker);
                return;
            }
            try
            {
                if (UnityEngine.Application.isBatchMode)
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliRequiresGraphicsNonBatchProcess");
                if (SessionState.GetBool(ActiveKey, false))
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliAlreadyActive");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliRequiresEditModeStart");
                if (Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt).Any(value => value.isDirty))
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliDirtyLoadedSceneRejected");

                var output = ResolveOutputDirectory();
                if (Directory.Exists(output) || File.Exists(output))
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliOutputAlreadyExists:" + output);
                var sceneAbsolute = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..",
                    다중Os생명주기GameView검증.CanonicalScenePath));
                if (!File.Exists(sceneAbsolute))
                    throw new FileNotFoundException(
                        "MultiOsLifecycleCanonicalSceneMissing", sceneAbsolute);
                var restaurantFixtureHash = StageExternalRestaurantFixture();

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(PhaseKey, (int)Phase.AwaitingPlayMode);
                SessionState.SetString(OutputKey, output);
                SessionState.SetString(DeadlineKey,
                    DateTime.UtcNow.AddMinutes(10).Ticks.ToString(
                        CultureInfo.InvariantCulture));
                SessionState.SetInt(WarmupKey, 0);
                SessionState.SetString(ResultKey, string.Empty);
                SessionState.SetString(RestaurantFixtureHashKey,
                    restaurantFixtureHash);

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    다중Os생명주기GameView검증.CanonicalScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
                if (!scene.IsValid() || scene.isDirty)
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCanonicalSceneOpenRejected");
                Debug.Log("[SagajeongMultiOsLifecycleCliCapture] 시작 · "
                          + "evidenceSource=FrozenPresentationTimeline · "
                          + "liveHttpEndToEnd=false · Scene 저장 없음 · " + output);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                RequestFailure("CliStartFailed", error);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                RequireBeforeDeadline();
                var phase = (Phase)SessionState.GetInt(PhaseKey, (int)Phase.None);
                switch (phase)
                {
                    case Phase.AwaitingPlayMode:
                        AwaitPlayModeAndCapture();
                        break;
                    case Phase.Capturing:
                        PollCapture();
                        break;
                    case Phase.StoppingSuccess:
                        ExitWhenEditMode(0);
                        break;
                    case Phase.StoppingFailure:
                        ExitWhenEditMode(2);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "MultiOsLifecycleCliPhaseInvalid:" + phase);
                }
            }
            catch (Exception error)
            {
                RequestFailure("CliPollingFailed", error);
            }
        }

        private static void AwaitPlayModeAndCapture()
        {
            if (!EditorApplication.isPlaying) return;
            var warmup = SessionState.GetInt(WarmupKey, 0) + 1;
            SessionState.SetInt(WarmupKey, warmup);
            if (warmup < 2) return;
            var driver = 다중Os생명주기GameView검증.EnsureDriverForAutomation();
            driver.BeginEvidenceCapture(SessionState.GetString(OutputKey, string.Empty));
            SessionState.SetInt(PhaseKey, (int)Phase.Capturing);
        }

        private static void PollCapture()
        {
            var driver = Resources
                .FindObjectsOfTypeAll<다중Os생명주기GameView검증Driver>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid())
                         ?? throw new InvalidOperationException(
                             "MultiOsLifecycleCliDriverMissing");
            if (!string.IsNullOrEmpty(driver.EvidenceCaptureFailure))
                throw new InvalidOperationException(driver.EvidenceCaptureFailure);
            if (!driver.EvidenceCaptureComplete) return;
            var output = SessionState.GetString(OutputKey, string.Empty);
            var manifest = Path.Combine(output,
                다중Os생명주기GameView검증Driver.EvidenceManifestFileName);
            var images = Directory.GetFiles(output, "*.png");
            if (!File.Exists(manifest) || images.Length != 3)
                throw new InvalidOperationException(
                    "MultiOsLifecycleCliEvidenceIncomplete");
            ValidateManifest(manifest,
                SessionState.GetString(RestaurantFixtureHashKey, string.Empty));
            var result = "captured=3; manifest=" + manifest
                         + "; evidenceSource=FrozenPresentationTimeline"
                         + "; movementMode=" + 다중Os생명주기검증Policy.MovementMode
                         + "; routeAuthority=false; movementSamples=4"
                         + "; liveHttpEndToEnd=false; sceneSaved=false";
            SessionState.SetString(ResultKey, result);
            Debug.Log("[SagajeongMultiOsLifecycleCliCapture] " + result);
            SessionState.SetInt(PhaseKey, (int)Phase.StoppingSuccess);
            EditorApplication.isPlaying = false;
        }

        private static void RequestFailure(string code, Exception error)
        {
            var message = code + ":" + error.GetType().Name + ":" + error.Message;
            Debug.LogError("[SagajeongMultiOsLifecycleCliCapture] " + message);
            SessionState.SetString(ResultKey, message);
            if (!SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.Exit(2);
                return;
            }
            SessionState.SetInt(PhaseKey, (int)Phase.StoppingFailure);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void ExitWhenEditMode(int code)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Debug.Log("[SagajeongMultiOsLifecycleCliCapture] 종료 "
                      + SessionState.GetString(ResultKey, string.Empty));
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(OutputKey);
            SessionState.EraseString(DeadlineKey);
            SessionState.EraseInt(WarmupKey);
            SessionState.EraseString(RestaurantFixtureHashKey);
            EditorApplication.Exit(code);
        }

        private static string StageExternalRestaurantFixture()
        {
            var source = Environment.GetEnvironmentVariable(
                RestaurantFixtureSourceEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (!Path.IsPathRooted(source)
                || !string.Equals(Path.GetExtension(source), ".json",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "RestaurantIconFixtureExternalSourcePathInvalid");
            var fullSource = Path.GetFullPath(source);
            if (!File.Exists(fullSource))
                throw new FileNotFoundException(
                    "RestaurantIconFixtureExternalSourceMissing", fullSource);
            var bytes = File.ReadAllBytes(fullSource);
            if (bytes.Length == 0 || bytes.Length > MaximumRestaurantFixtureBytes)
                throw new FormatException(
                    "RestaurantIconFixtureExternalSourceSizeInvalid");
            var hash = Hash(bytes);
            var expectedHash = Environment.GetEnvironmentVariable(
                RestaurantFixtureSourceHashEnvironmentVariable)?.Trim();
            if (!string.IsNullOrEmpty(expectedHash)
                && !string.Equals(hash, expectedHash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "RestaurantIconFixtureExternalSourceHashMismatch");

            var temp = Path.GetDirectoryName(
                사가정음식점아이콘FixtureLoader.DefaultPath())
                       ?? throw new InvalidOperationException(
                           "RestaurantIconFixtureTempPathInvalid");
            Directory.CreateDirectory(temp);
            var destination = Path.Combine(temp,
                "sagajeong-restaurant-icons." + hash + ".local.json");
            if (File.Exists(destination))
            {
                if (!string.Equals(Hash(File.ReadAllBytes(destination)), hash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "RestaurantIconFixtureStagingCollision");
            }
            else
            {
                File.WriteAllBytes(destination, bytes);
            }

            Environment.SetEnvironmentVariable(
                사가정음식점아이콘FixtureLoader.EnvironmentVariable, destination);
            if (!사가정음식점아이콘FixtureLoader.TryLoad(out var loaded,
                    out var status, destination)
                || loaded?.Data?.entries == null
                || loaded.Data.entries.Length != RequiredRestaurantIconCount
                || !string.Equals(loaded.ContentHashSha256, hash,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "RestaurantIconFixtureStagingRejected:" + status);
            Debug.Log("[SagajeongMultiOsLifecycleCliCapture] 음식점 입력 staging · "
                      + $"count={RequiredRestaurantIconCount} · sha256={hash}");
            return hash;
        }

        private static void ValidateManifest(string path, string stagedHash)
        {
            var manifest = JsonUtility.FromJson<CaptureManifestCheck>(
                File.ReadAllText(path))
                           ?? throw new InvalidOperationException(
                               "MultiOsLifecycleCliManifestInvalid");
            if (!string.Equals(manifest.evidenceSource,
                    다중Os생명주기GameView검증Driver.SourceMode,
                    StringComparison.Ordinal)
                || !string.Equals(manifest.movementMode,
                    다중Os생명주기검증Policy.MovementMode,
                    StringComparison.Ordinal)
                || manifest.liveHttpEndToEnd
                || manifest.operationalEffectsAllowed
                || manifest.routeAuthority
                || manifest.sceneSaved)
                throw new InvalidOperationException(
                    "MultiOsLifecycleCliManifestEvidenceBoundaryInvalid");
            ValidateMovementSamples(manifest.movementSamples);
            if (string.IsNullOrEmpty(stagedHash)) return;
            if (manifest.restaurantIconCount != RequiredRestaurantIconCount
                || !string.Equals(manifest.restaurantFixtureHashSha256,
                    stagedHash, StringComparison.OrdinalIgnoreCase)
                || !manifest.restaurantIconInputStatus.EndsWith(
                    ":" + RequiredRestaurantIconCount,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MultiOsLifecycleCliRestaurantEvidenceInvalid");
        }

        private static void ValidateMovementSamples(MovementSampleCheck[] samples)
        {
            var expected = 다중Os생명주기검증Policy.SupportedOperatingSystemIds
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var actual = (samples ?? Array.Empty<MovementSampleCheck>())
                .Where(value => value != null)
                .Select(value => value.operatingSystemId)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (actual.Length != expected.Length || !actual.SequenceEqual(expected))
                throw new InvalidOperationException(
                    "MultiOsLifecycleCliMovementOperatingSystemsInvalid");

            foreach (var sample in samples)
            {
                var measuredDistance = PlanarDistance(sample.fromPosition,
                    sample.toPosition);
                var intermediateDistance = PlanarDistance(sample.fromPosition,
                    sample.intermediatePosition);
                var remainingDistance = PlanarDistance(sample.intermediatePosition,
                    sample.toPosition);
                if (string.IsNullOrWhiteSpace(sample.fromStageCode)
                    || string.IsNullOrWhiteSpace(sample.toStageCode)
                    || string.Equals(sample.fromStageCode, sample.toStageCode,
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(sample.fromSemanticPlaceStableId)
                    || string.IsNullOrWhiteSpace(sample.toSemanticPlaceStableId)
                    || string.Equals(sample.fromSemanticPlaceStableId,
                        sample.toSemanticPlaceStableId, StringComparison.Ordinal)
                    || !Finite(measuredDistance) || measuredDistance <= .01f
                    || Math.Abs(measuredDistance - sample.distance) > .05f
                    || !Finite(intermediateDistance) || intermediateDistance <= .01f
                    || !Finite(remainingDistance) || remainingDistance <= .01f
                    || Math.Abs(intermediateDistance - sample.intermediateDistance) > .05f
                    || !Finite(sample.intermediateProgress01)
                    || sample.intermediateProgress01 < .2f
                    || sample.intermediateProgress01 > .8f
                    || !sample.sampledInPlayMode
                    || sample.observedFrameCount < 2)
                    throw new InvalidOperationException(
                        "MultiOsLifecycleCliMovementSampleInvalid:"
                        + sample.operatingSystemId);
            }
        }

        private static float PlanarDistance(PositionCheck from, PositionCheck to)
        {
            if (from == null || to == null) return float.NaN;
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool Finite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void RequireBeforeDeadline()
        {
            var raw = SessionState.GetString(DeadlineKey, string.Empty);
            if (!long.TryParse(raw, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var ticks)
                || DateTime.UtcNow.Ticks > ticks)
                throw new TimeoutException("MultiOsLifecycleCliTimedOut");
        }

        private static string ResolveOutputDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            var value = string.Empty;
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], OutputArgument,
                        StringComparison.Ordinal))
                    value = args[index + 1];
            if (string.IsNullOrWhiteSpace(value))
                value = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
                throw new ArgumentException(
                    "MultiOsLifecycleCliAbsoluteOutputRequired");
            return Path.GetFullPath(value);
        }

        private static bool HasArgument(string expected)
            => Environment.GetCommandLineArgs().Any(value =>
                string.Equals(value, expected, StringComparison.Ordinal));
    }
}
