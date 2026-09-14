using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 별도 그래픽 Unity 프로세스에서 canonical Scene을 저장하지 않고 건물 화면 선택과
    /// H 후보 카드가 기존 사가정 모형을 바꾸지 않는지 확인한다.
    /// </summary>
    [InitializeOnLoad]
    public static class SagajeongHSelectionCliCapture
    {
        public const string ExecuteMethod =
            "Ssalddel.Unity.Editor.SagajeongHSelectionCliCapture.Run";
        public const string CommandLineMarker = "-sagajeongHSelectionCliCapture";
        public const string OutputArgument = "-sagajeongHSelectionOutput";
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        public const string SelectedBuildingStableId = "osm:way:1256772531";

        private const string SessionPrefix = "Ssalddel.SagajeongHSelectionCliCapture.";
        private const string ActiveKey = SessionPrefix + "Active";
        private const string PhaseKey = SessionPrefix + "Phase";
        private const string FrameKey = SessionPrefix + "Frame";
        private const string PreparedKey = SessionPrefix + "Prepared";
        private const string OutputKey = SessionPrefix + "Output";
        private const string RunStartedKey = SessionPrefix + "RunStarted";
        private const string SceneHashKey = SessionPrefix + "SceneHash";
        private const string RendererCountKey = SessionPrefix + "RendererCount";
        private const string MaterialCountKey = SessionPrefix + "MaterialCount";
        private const string MeshFilterCountKey = SessionPrefix + "MeshFilterCount";
        private const string DeadlineKey = SessionPrefix + "Deadline";
        private const int FailureExitCode = 2;

        private enum Phase
        {
            None,
            AwaitingPlayMode,
            CaptureOverview,
            CaptureSelectedOverview,
            CaptureSelectedClose,
            ValidateAndStop,
            StoppingSuccess,
            StoppingFailure,
        }

        static SagajeongHSelectionCliCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!HasArgument(CommandLineMarker))
            {
                Debug.LogError("[SagajeongHSelectionCliCapture] 전용 명령줄 표식이 없습니다.");
                return;
            }
            try
            {
                if (UnityEngine.Application.isBatchMode || HasArgument("-nographics"))
                    throw new InvalidOperationException("SagajeongHSelectionCaptureRequiresGraphicsProcess");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("SagajeongHSelectionCaptureRequiresEditModeStart");
                if (EditorSceneManager.GetActiveScene().isDirty)
                    throw new InvalidOperationException("SagajeongHSelectionCaptureDirtySceneAtStart");

                var output = ReadArgument(OutputArgument);
                if (string.IsNullOrWhiteSpace(output))
                    output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "artifacts",
                        "local", "validation", "sagajeong-building-h-r6", "gameview"));
                Directory.CreateDirectory(output);
                var evidenceNames = new[]
                {
                    "01-overview-unselected.png", "02-overview-selected-h.png",
                    "03-closeup-selected-h.png", "capture-manifest.json",
                };
                if (evidenceNames.Any(name => File.Exists(Path.Combine(output, name))))
                    throw new InvalidOperationException("SagajeongHSelectionOutputMustBeFresh");
                var scenePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", CanonicalScenePath));
                if (!File.Exists(scenePath))
                    throw new FileNotFoundException("SagajeongHSelectionCanonicalSceneMissing", scenePath);

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(PhaseKey, (int)Phase.AwaitingPlayMode);
                SessionState.SetInt(FrameKey, 0);
                SessionState.SetBool(PreparedKey, false);
                SessionState.SetString(OutputKey, output);
                SessionState.SetString(RunStartedKey,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                SessionState.SetString(SceneHashKey, Sha256(scenePath));
                SessionState.SetString(DeadlineKey,
                    DateTime.UtcNow.AddMinutes(12).Ticks.ToString(CultureInfo.InvariantCulture));
                var scene = EditorSceneManager.OpenScene(CanonicalScenePath, OpenSceneMode.Single);
                if (!scene.IsValid() || scene.isDirty)
                    throw new InvalidOperationException("SagajeongHSelectionCanonicalSceneOpenFailed");
                Debug.Log("[SagajeongHSelectionCliCapture] 시작 · Scene 저장 없음 · "
                          + "표현 로컬 건물 선택 · 권위 상태 변경 없음");
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Fail("StartFailed", exception);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(DeadlineKey, "0"),
                        CultureInfo.InvariantCulture))
                    throw new TimeoutException("SagajeongHSelectionCaptureTimedOut");
                switch ((Phase)SessionState.GetInt(PhaseKey, 0))
                {
                    case Phase.AwaitingPlayMode:
                        AwaitPlayMode();
                        break;
                    case Phase.CaptureOverview:
                        CaptureOverview();
                        break;
                    case Phase.CaptureSelectedOverview:
                        CaptureSelectedOverview();
                        break;
                    case Phase.CaptureSelectedClose:
                        CaptureSelectedClose();
                        break;
                    case Phase.ValidateAndStop:
                        ValidateAndStop();
                        break;
                    case Phase.StoppingSuccess:
                        ExitWhenStopped(0);
                        break;
                    case Phase.StoppingFailure:
                        ExitWhenStopped(FailureExitCode);
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail("TickFailed", exception);
            }
        }

        private static void AwaitPlayMode()
        {
            if (!EditorApplication.isPlaying) return;
            var view = FindView();
            if (view == null) return;
            view.Initialize();
            if (view.MapStatus != "Ready" || view.ObservationCamera == null
                || view.HSelectionBuildingCount != 602) return;
            if (!WaitFrames(24)) return;

            view.GameOverlayVisible = false;
            view.Show(true);
            view.ShowWhole();
            SessionState.SetInt(RendererCountKey,
                view.GetComponentsInChildren<Renderer>(true).Length);
            SessionState.SetInt(MaterialCountKey, view.GetComponentsInChildren<Renderer>(true)
                .Select(value => value.sharedMaterial).Where(value => value != null).Distinct().Count());
            SessionState.SetInt(MeshFilterCountKey,
                view.GetComponentsInChildren<MeshFilter>(true).Length);
            SetPhase(Phase.CaptureOverview);
        }

        private static void CaptureOverview()
        {
            if (!WaitFrames(18)) return;
            Capture("01-overview-unselected.png");
            SetPhase(Phase.CaptureSelectedOverview);
        }

        private static void CaptureSelectedOverview()
        {
            if (!WaitForCapture("01-overview-unselected.png")) return;
            var view = RequireView();
            if (!SessionState.GetBool(PreparedKey, false))
            {
                if (!view.SelectBuilding(SelectedBuildingStableId))
                    throw new InvalidOperationException("SagajeongHSelectionBuildingLookupFailed");
                var value = view.SelectedBuilding;
                var world = view.transform.TransformPoint(new Vector3(value.LocalCentroid.x,
                    value.DisplayedHeight, value.LocalCentroid.y));
                var screen = view.ObservationCamera.WorldToScreenPoint(world);
                view.ClearBuildingSelection();
                if (screen.z <= 0f || !view.TrySelectBuildingAtScreen(new Vector2(screen.x, screen.y))
                    || view.SelectedBuilding?.BuildingStableId != SelectedBuildingStableId)
                    throw new InvalidOperationException("SagajeongHSelectionScreenRayFailed");
                BeginPreparedWait();
                return;
            }
            if (!WaitFrames(18)) return;
            Capture("02-overview-selected-h.png");
            SetPhase(Phase.CaptureSelectedClose);
        }

        private static void CaptureSelectedClose()
        {
            if (!WaitForCapture("02-overview-selected-h.png")) return;
            var view = RequireView();
            if (!SessionState.GetBool(PreparedKey, false))
            {
                view.FocusSelectedBuilding();
                BeginPreparedWait();
                return;
            }
            if (!WaitFrames(24)) return;
            Capture("03-closeup-selected-h.png");
            SetPhase(Phase.ValidateAndStop);
        }

        private static void ValidateAndStop()
        {
            if (!WaitForCapture("03-closeup-selected-h.png")) return;
            var view = RequireView();
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            var rendererCount = renderers.Length;
            var materialCount = renderers.Select(value => value.sharedMaterial)
                .Where(value => value != null).Distinct().Count();
            var meshFilterCount = view.GetComponentsInChildren<MeshFilter>(true).Length;
            var colliderCount = view.GetComponentsInChildren<Collider>(true).Length;
            if (rendererCount != SessionState.GetInt(RendererCountKey, -1)
                || materialCount != SessionState.GetInt(MaterialCountKey, -1)
                || meshFilterCount != SessionState.GetInt(MeshFilterCountKey, -1)
                || colliderCount != 0)
                throw new InvalidOperationException("SagajeongHSelectionVisualBudgetChanged");
            if (view.SelectedBuilding?.BuildingStableId != SelectedBuildingStableId
                || view.SelectedBuilding.H1BindingStatus != 사가정H공간선택Layer.CandidateBinding)
                throw new InvalidOperationException("SagajeongHSelectionCandidateBindingMissing");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("SagajeongHSelectionSceneBecameDirty");

            var report = new CaptureManifest
            {
                schemaVersion = "sagajeong-h-selection-gameview-evidence.v2",
                capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                mapRevision = 사가정H공간선택Layer.ExpectedMapRevision,
                mapSha256 = 사가정H공간선택Layer.ExpectedMapSha256,
                sceneSha256Before = SessionState.GetString(SceneHashKey, string.Empty),
                selectedBuildingStableId = view.SelectedBuilding.BuildingStableId,
                h1StableId = view.SelectedBuilding.H1StableId,
                h3StableId = view.SelectedBuilding.H3StableId,
                compositionStateCode = view.SelectedBuilding.CompositionStateCode,
                displayedHeight = view.SelectedBuilding.DisplayedHeight,
                displayGeometrySource = view.SelectedBuilding.DisplayGeometrySource,
                spatialPresentationStatus = view.SpatialPresentationStatus,
                spatialPresentationRevision = view.SpatialPresentationRevision,
                rendererCount = rendererCount,
                materialCount = materialCount,
                meshFilterCount = meshFilterCount,
                colliderCount = colliderCount,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                candidateOnly = true,
                actualE5 = false,
                traversalReady = false,
                gameplayReady = false,
                serverProjectionReady = false,
                programmaticScreenRaySelectionVerified = true,
                userMouseUpIntegrationVerified = false,
                selectionIsLocalPresentationState = true,
                authoritativeStateComparisonPerformed = false,
                publicReleaseEvidence = false,
                selectionGeometryAlignedWithDisplayedBaseline = true,
                sceneDirtyDuringPlay = false,
            };
            File.WriteAllText(Path.Combine(OutputDirectory(), "capture-manifest.json"),
                JsonUtility.ToJson(report, true));
            Debug.Log("[SagajeongHSelectionCliCapture] 화면 선택·표현 예산 검증 완료 · "
                      + $"renderer={rendererCount} · material={materialCount} · "
                      + $"meshFilter={meshFilterCount} · collider={colliderCount}");
            SetPhase(Phase.StoppingSuccess);
            EditorApplication.isPlaying = false;
        }

        private static void ExitWhenStopped(int code)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scenePath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", CanonicalScenePath));
            var before = SessionState.GetString(SceneHashKey, string.Empty);
            var after = Sha256(scenePath);
            if (code == 0 && (!string.Equals(before, after, StringComparison.Ordinal)
                              || EditorSceneManager.GetActiveScene().isDirty))
            {
                Debug.LogError("[SagajeongHSelectionCliCapture] 종료 뒤 Scene 불변 검사 실패");
                code = FailureExitCode;
            }
            var manifestPath = Path.Combine(OutputDirectory(), "capture-manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonUtility.FromJson<CaptureManifest>(File.ReadAllText(manifestPath));
                manifest.sceneSha256After = after;
                manifest.sceneHashUnchanged = string.Equals(before, after, StringComparison.Ordinal);
                manifest.sceneDirtyAfterPlay = EditorSceneManager.GetActiveScene().isDirty;
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            }
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("[SagajeongHSelectionCliCapture] 종료 · exit=" + code
                      + " · sceneHashUnchanged=" + string.Equals(before, after, StringComparison.Ordinal));
            EditorApplication.Exit(code);
        }

        private static void Fail(string code, Exception exception)
        {
            Debug.LogError("[SagajeongHSelectionCliCapture] " + code + " · " + exception);
            if (!SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.Exit(FailureExitCode);
                return;
            }
            SessionState.SetInt(PhaseKey, (int)Phase.StoppingFailure);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        }

        private static bool WaitFrames(int required)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            var frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            return frame >= required;
        }

        private static bool WaitForCapture(string fileName)
        {
            var path = Path.Combine(OutputDirectory(), fileName);
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
            var started = long.Parse(SessionState.GetString(RunStartedKey, "0"),
                CultureInfo.InvariantCulture);
            return File.GetLastWriteTimeUtc(path).Ticks >= started;
        }

        private static void BeginPreparedWait()
        {
            SessionState.SetBool(PreparedKey, true);
            SessionState.SetInt(FrameKey, 0);
        }

        private static void SetPhase(Phase value)
        {
            SessionState.SetInt(PhaseKey, (int)value);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetBool(PreparedKey, false);
        }

        private static void Capture(string fileName)
        {
            var path = Path.Combine(OutputDirectory(), fileName);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[SagajeongHSelectionCliCapture] 캡처 요청 · " + path);
        }

        private static 사가정운영디오라마View FindView()
            => Resources.FindObjectsOfTypeAll<사가정운영디오라마View>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());

        private static 사가정운영디오라마View RequireView()
            => FindView() ?? throw new InvalidOperationException("SagajeongHSelectionViewMissing");

        private static string OutputDirectory()
            => SessionState.GetString(OutputKey, string.Empty);

        private static bool HasArgument(string value)
            => Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, value, StringComparison.OrdinalIgnoreCase));

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(stream)
                .Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
        }

        [Serializable]
        private sealed class CaptureManifest
        {
            public string schemaVersion;
            public string capturedAtUtc;
            public string mapRevision;
            public string mapSha256;
            public string sceneSha256Before;
            public string sceneSha256After;
            public string selectedBuildingStableId;
            public string h1StableId;
            public string h3StableId;
            public string compositionStateCode;
            public float displayedHeight;
            public string displayGeometrySource;
            public string spatialPresentationStatus;
            public string spatialPresentationRevision;
            public int rendererCount;
            public int materialCount;
            public int meshFilterCount;
            public int colliderCount;
            public int screenWidth;
            public int screenHeight;
            public bool candidateOnly;
            public bool actualE5;
            public bool traversalReady;
            public bool gameplayReady;
            public bool serverProjectionReady;
            public bool programmaticScreenRaySelectionVerified;
            public bool userMouseUpIntegrationVerified;
            public bool selectionIsLocalPresentationState;
            public bool authoritativeStateComparisonPerformed;
            public bool publicReleaseEvidence;
            public bool selectionGeometryAlignedWithDisplayedBaseline;
            public bool sceneDirtyDuringPlay;
            public bool sceneHashUnchanged;
            public bool sceneDirtyAfterPlay;
        }
    }
}
