using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 정확히 결속한 기존 r7 한 동의 단일 건물 전후 검토. Play에서 명시 호출만 허용한다.
    /// Scene 저장, 실제 통행 승인, 원본 세대 변경을 하지 않는다. 캡처 요청과 파일 확인은 별도다.
    /// </summary>
    [InitializeOnLoad]
    public static class 행정동건물내부Ring검토GameView검증
    {
        public const string CanonicalScene = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        public const int FocusedPresentationLayer = 28;
        private static readonly Dictionary<Behaviour, bool> isolatedPresentation = new();
        private static readonly Dictionary<GameObject, bool> preservedRoots = new();
        private static readonly Dictionary<사가정운영디오라마View, bool> legacyUiVisibility = new();
        private static readonly Dictionary<MonoBehaviour, bool> preservedControllers = new();
        private static GameObject host;
        private static 행정동디오라마검토View view;
        private static 행정동디오라마검토Snapshot snapshot;
        private static 행정동건물내부Ring검토Geometry geometry;
        private static string sourceFileHash, sceneBeforeHash;
        private static IDisposable lease;
        private static bool after;
        private static bool detail;
        private const string Generation = "52f93961d7628d5cf31faf75f2286fd7cec8a55d79c4cc77d67fa4e059f8ba86";
        public const string PinnedFileHash = "39B0DE341154FF7A0A1D847C3D8E54F6E9853EA22646720A148B3B0A1F9AA1D6";

        static 행정동건물내부Ring검토GameView검증()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode
                    || state == PlayModeStateChange.ExitingEditMode) Clear();
            };
        }

        public static string PinnedGeometryPath(string repositoryRoot) => Path.Combine(repositoryRoot,
            "artifacts/local/validation/admin-dong-focused-building-ring-review/r1/input/generations",
            Generation, "review.json");
        public static bool IsPrepared => view != null;

        [MenuItem("Ssalddel/디오라마/면목제3·8동 내부 경계 보완")]
        public static void OpenReview()
        {
            if (SceneManager.GetActiveScene().path != CanonicalScene)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode
                    || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                { Debug.LogWarning("내부 경계 검토 보류 · 기존 Scene 변경을 보존하고 Play를 종료한 뒤 다시 여세요."); return; }
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CanonicalScene);
            }
            EditorWindow.GetWindow<행정동건물내부Ring검토Window>("단일 건물 내부 경계");
        }

        public static string Prepare(string repositoryRoot, string geometryPath,
            string expectedFileSha256)
        {
            Require(EditorApplication.isPlaying, "InteriorRingRealPlayRequired");
            Require(host == null && !SceneManager.GetActiveScene().isDirty,
                "InteriorRingFreshCleanSceneRequired");
            Require(SceneManager.GetActiveScene().path == CanonicalScene,
                "InteriorRingCanonicalSceneRequired");
            Require(!Resources.FindObjectsOfTypeAll<Renderer>().Any(item =>
                    item != null && item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded
                    && item.gameObject.layer == FocusedPresentationLayer),
                "InteriorRingFocusedLayerAlreadyUsed");
            var bytes = File.ReadAllBytes(geometryPath);
            sourceFileHash = Hash(bytes);
            Require(string.Equals(sourceFileHash, PinnedFileHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(expectedFileSha256, PinnedFileHash,
                    StringComparison.OrdinalIgnoreCase), "InteriorRingFileHashMismatch");
            snapshot = 방문순서재생검토진입.LoadFrozenSnapshot(repositoryRoot);
            var data = JsonConvert.DeserializeObject<행정동건물내부Ring검토Data>(
                System.Text.Encoding.UTF8.GetString(bytes));
            Require(data != null && string.Equals(data.contentHashSha256, Generation,
                    StringComparison.OrdinalIgnoreCase)
                && data.baseBundleSha256 == "E1DBB3B99347B4139DE157274C84C881E9821A49BF064BB579813F347925E28A"
                && data.baseGenerationHashSha256 == "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753",
                "InteriorRingPinnedGenerationMismatch");
            Require(행정동건물내부Ring검토Geometry.TryCreate(data, snapshot,
                out geometry, out var diagnostic), diagnostic);
            Require(사가정GameView검증CaptureCoordinator.TryAcquire(
                "AdministrativeDongSingleBuildingInteriorRingR1", out lease),
                "InteriorRingCaptureAlreadyOwned");
            sceneBeforeHash = Hash(File.ReadAllBytes(CanonicalScene));
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                preservedRoots.Add(root, root.activeSelf);
            foreach (var item in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(item =>
                         item != null && item.gameObject.scene == SceneManager.GetActiveScene()
                         && (item.GetType().Name.Contains("Controller") || item.GetType().Name.Contains("CompositionRoot"))))
                preservedControllers.Add(item, item.enabled);
            host = new GameObject("면목제3·8동 · 단일 건물 내부ring 비공개 검토")
                { hideFlags = HideFlags.DontSave };
            view = host.AddComponent<행정동디오라마검토View>();
            view.CompactEvidenceHud = true;
            host.AddComponent<행정동건물내부Ring검토CaptureHud>();
            ShowBefore(false);
            return Evidence();
        }

        public static string ShowBefore(bool closeup = false)
        {
            Require(view != null, "InteriorRingCaptureNotPrepared");
            after = false; detail = closeup;
            view.BuildFromSnapshot(snapshot, true);
            ConfigureCamera();
            return Evidence();
        }

        public static string ShowAfter(bool closeup = false)
        {
            Require(view != null && geometry != null, "InteriorRingCaptureNotPrepared");
            after = true; detail = closeup;
            view.BuildFromSnapshot(snapshot, true, geometry);
            ConfigureCamera();
            return Evidence();
        }

        private static void ConfigureCamera()
        {
            Require(snapshot.TryGetBuilding(geometry.BuildingStableId, out var building),
                "InteriorRingTargetMissing");
            var camera = view.ObservationCamera;
            var rotation = Quaternion.Euler(detail ? 62f : 58f, 38f, 0f);
            var center = new Vector3((float)building.CentroidX, 2f,
                (float)building.CentroidZ);
            camera.orthographicSize = detail
                ? Mathf.Max(10f, (float)Math.Max(building.MaxX - building.MinX,
                    building.MaxZ - building.MinZ) * .85f)
                : 125f;
            camera.nearClipPlane = .3f; camera.farClipPlane = 1800f;
            camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * 750f,
                rotation);
            view.ClearSelection();
            foreach (var child in host.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = FocusedPresentationLayer;
            foreach (var item in Resources.FindObjectsOfTypeAll<사가정운영디오라마View>().Where(item =>
                         item != null && item.gameObject.scene == SceneManager.GetActiveScene()))
            {
                if (!legacyUiVisibility.ContainsKey(item)) legacyUiVisibility.Add(item, item.ExternalGeometryReviewUiHidden);
                item.ExternalGeometryReviewUiHidden = true;
            }
            camera.cullingMask = 1 << FocusedPresentationLayer;
            foreach (var light in host.GetComponentsInChildren<Light>(true))
                light.cullingMask = 1 << FocusedPresentationLayer;
            KeepPresentationIsolated();
        }

        internal static void KeepPresentationIsolated()
        {
            if (host == null || view == null) return;
            // 선택 윤곽 등 새로 생성된 검토 표시도 동일한 전용 레이어에 둔다.
            foreach (var child in host.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = FocusedPresentationLayer;
            foreach (var item in UnityEngine.Object.FindObjectsByType<Behaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item == null || item.transform.IsChildOf(host.transform)) continue;
                if (!(item is Camera) && !(item is Canvas)) continue;
                if (!isolatedPresentation.ContainsKey(item)) isolatedPresentation.Add(item, item.enabled);
                if (item.enabled) item.enabled = false;
            }
        }

        public static string VerifySelectionPresentation()
        {
            Require(view != null && EditorApplication.isPlaying, "InteriorRingRealPlayRequired");
            Require(view.SelectBuilding(geometry.BuildingStableId), "InteriorRingTargetSelectionFailed");
            KeepPresentationIsolated();
            var renderers = host.GetComponentsInChildren<Renderer>(true);
            var layers = renderers.Select(item => item.gameObject.layer).Distinct().ToArray();
            Require(layers.Length == 1 && layers[0] == FocusedPresentationLayer,
                "InteriorRingSelectionLayerMismatch");
            var json = JsonConvert.SerializeObject(new { selected = view.SelectedBuilding.BuildingStableId,
                rendererCount = renderers.Length, rendererLayers = layers,
                cameraMask = view.ObservationCamera.cullingMask, cameraIncludesSelection =
                    (view.ObservationCamera.cullingMask & (1 << FocusedPresentationLayer)) != 0 });
            view.ClearSelection();
            return json;
        }

        public static string Capture(string outputPath)
        {
            Require(view != null && EditorApplication.isPlaying,
                "InteriorRingRealPlayRequired");
            var path = Path.GetFullPath(outputPath);
            Require(!File.Exists(path), "InteriorRingFreshCapturePathRequired");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            return "Actual Game View screenshot requested: " + path;
        }

        public static string Evidence()
        {
            Require(view != null, "InteriorRingCaptureNotPrepared");
            var ring = geometry.InteriorRings.Single();
            // 링 꼭짓점 평균은 이 대상 안쪽인지 따로 검증하여 ray 검사에만 사용한다.
            var x = ring.Average(point => point.X);
            var z = ring.Average(point => point.Z);
            Require(!geometry.ContainsRoof(x, z), "InteriorRingProbeNotInsideHole");
            var origin = view.ModelRoot.TransformPoint(new Vector3((float)x, 500f, (float)z));
            var selected = view.TrySelectWorldRay(new Ray(origin, Vector3.down));
            var selectedId = view.SelectedBuilding?.BuildingStableId ?? "";
            view.ClearSelection();
            return JsonConvert.SerializeObject(new
            {
                evidenceKind = "ActualUnityPlayGameView;ProgrammaticPublicViewFunctions",
                actualPlay = EditorApplication.isPlaying,
                physicalInputTested = false,
                after, detail, sourceFileHash, geometry.ContentHashSha256,
                geometry.BuildingStableId, geometry.BaseProjectionHashSha256,
                snapshotBuildingCount = snapshot.Buildings.Count,
                snapshotRoadCount = snapshot.Roads.Count,
                innerUniquePoints = ring.Count,
                roofTriangles = geometry.RoofTriangleIndices.Count / 3,
                geometry.RoofArea, geometry.InteriorArea,
                holeRayHit = selected, holeRaySelectedBuilding = selectedId,
                holeTargetSelected = selected && selectedId == geometry.BuildingStableId,
                cameraPosition = Components(view.ObservationCamera.transform.position),
                cameraEuler = Components(view.ObservationCamera.transform.eulerAngles),
                orthographicSize = view.ObservationCamera.orthographicSize,
                cameraMask = view.ObservationCamera.cullingMask,
                backgroundRgba = new[] { view.ObservationCamera.backgroundColor.r,
                    view.ObservationCamera.backgroundColor.g, view.ObservationCamera.backgroundColor.b,
                    view.ObservationCamera.backgroundColor.a },
                lightMasks = host.GetComponentsInChildren<Light>(true).Select(light => light.cullingMask).ToArray(),
                isolation = isolatedPresentation.Select(p => new { type = p.Key == null ? "destroyed" : p.Key.GetType().FullName,
                    name = p.Key == null ? "destroyed" : p.Key.name, originalEnabled = p.Value,
                    currentEnabled = p.Key != null && p.Key.enabled }).ToArray(),
                rootActivationMismatch = preservedRoots.Count(p => p.Key != null && p.Key.activeSelf != p.Value),
                controllerEnabledMismatch = preservedControllers.Count(p => p.Key != null && p.Key.enabled != p.Value),
                legacyUiFlags = legacyUiVisibility.Select(p => new { name = p.Key == null ? "destroyed" : p.Key.name,
                    originalHidden = p.Value, currentHidden = p.Key != null && p.Key.ExternalGeometryReviewUiHidden }).ToArray(),
                sceneDirty = SceneManager.GetActiveScene().isDirty,
                sceneHashBefore = sceneBeforeHash,
                sceneHashCurrent = Hash(File.ReadAllBytes(CanonicalScene)),
                report = new
                {
                    view.BuildReport.BuildingCount, view.BuildReport.RoadSegmentCount,
                    view.BuildReport.TotalVertexCount, view.BuildReport.TotalIndexCount,
                    view.BuildReport.RendererBatchCount,
                    view.BuildReport.FocusedBuildingCount,
                    view.BuildReport.FocusedInteriorRingCount,
                    view.BuildReport.FocusedInteriorWallSegmentCount,
                    view.BuildReport.FocusedRoofTriangleCount
                }
            }, Formatting.Indented);
        }

        public static string Clear()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            host = null; view = null; snapshot = null; geometry = null;
            foreach (var item in isolatedPresentation.Where(p => !(p.Key is Camera)).ToArray())
                if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in isolatedPresentation.Where(p => p.Key is Camera).ToArray())
                if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in legacyUiVisibility)
                if (item.Key != null) item.Key.ExternalGeometryReviewUiHidden = item.Value;
            var states = isolatedPresentation.Select(p => new { type = p.Key == null ? "destroyed" : p.Key.GetType().FullName,
                name = p.Key == null ? "destroyed" : p.Key.name, originalEnabled = p.Value,
                restoredEnabled = p.Key != null && p.Key.enabled }).ToArray();
            var mismatches = isolatedPresentation.Count(p => p.Key != null && p.Key.enabled != p.Value);
            var rootMismatches = preservedRoots.Count(p => p.Key != null && p.Key.activeSelf != p.Value);
            var controllerStates = preservedControllers.Select(p => new { type = p.Key == null ? "destroyed" : p.Key.GetType().FullName,
                originalEnabled = p.Value, currentEnabled = p.Key != null && p.Key.enabled }).ToArray();
            var controllerMismatches = preservedControllers.Count(p => p.Key != null && p.Key.enabled != p.Value);
            var uiStates = legacyUiVisibility.Select(p => new { name = p.Key == null ? "destroyed" : p.Key.name, originalHidden = p.Value,
                restoredHidden = p.Key != null && p.Key.ExternalGeometryReviewUiHidden }).ToArray();
            var uiMismatches = legacyUiVisibility.Count(p => p.Key != null && p.Key.ExternalGeometryReviewUiHidden != p.Value);
            isolatedPresentation.Clear(); preservedRoots.Clear(); legacyUiVisibility.Clear(); preservedControllers.Clear();
            lease?.Dispose(); lease = null;
            return JsonConvert.SerializeObject(new { restoredPresentationCount = states.Length,
                restorationMismatchCount = mismatches, rootActivationMismatch = rootMismatches, states,
                controllerEnabledMismatch = controllerMismatches, controllerStates,
                legacyUiFlagMismatch = uiMismatches, uiStates,
                sceneDirty = SceneManager.GetActiveScene().isDirty,
                sceneHashBefore = sceneBeforeHash,
                sceneHashAfter = Hash(File.ReadAllBytes(CanonicalScene)) });
        }

        internal static string Title => after ? "원본 내부ring 복원 · 단일 대상" : "기존 표시 · 내부 빈 공간 누락";
        internal static string Subtitle => detail ? "동일 카메라 확대 · 높이 4m 기호값 유지" : "250m 집중 검토 · 다른 건물·도로·6종 overlay 유지";
        private static void Require(bool value, string diagnostic)
        { if (!value) throw new InvalidOperationException(diagnostic); }
        private static float[] Components(Vector3 value) => new[] { value.x, value.y, value.z };
        private static string Hash(byte[] bytes)
        { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    }

    /// <summary>기존 Scene의 임시 비공개 도형 비교. 게임 환경변수와 자동 재생을 변경하지 않는다.</summary>
    public sealed class 행정동건물내부Ring검토Window : EditorWindow
    {
        private string repositoryRoot = "";
        private string diagnostic = "원본 내부 경계 복원 · 높이4m 기호값 유지";
        private void OnEnable()
        {
            repositoryRoot = Environment.GetEnvironmentVariable("SSALDDEL_R54_REPO_ROOT")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source/repos/Hongdal");
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("면목제3·8동의 정확히 한 건물만 비교합니다. 내부 경계는 원본 도형 구조이며 현재 용도·출입·통행을 확정하지 않습니다.", MessageType.Info);
            EditorGUILayout.LabelField(diagnostic, EditorStyles.wordWrappedLabel);
            repositoryRoot = EditorGUILayout.TextField("Hongdal 자료 저장소", repositoryRoot);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("canonical Scene을 연 뒤 Unity의 Play를 누르세요. Play 중에만 비교를 준비합니다. 게임 환경변수는 변경하지 않습니다.", MessageType.Info);
                return;
            }
            if (!행정동건물내부Ring검토GameView검증.IsPrepared)
            {
                if (GUILayout.Button("비공개 비교 열기 · 기존 canonical Scene")) Guard(() =>
                {
                    var scene = SceneManager.GetActiveScene();
                    if (scene.isDirty) throw new InvalidOperationException("현재 Scene 변경을 먼저 보존하세요. Scene 저장은 자동으로 하지 않습니다.");
                    if (scene.path != 행정동건물내부Ring검토GameView검증.CanonicalScene)
                        throw new InvalidOperationException("Play 종료 후 검토 메뉴에서 canonical Scene을 열어 주세요.");
                    행정동건물내부Ring검토GameView검증.Prepare(repositoryRoot,
                        행정동건물내부Ring검토GameView검증.PinnedGeometryPath(repositoryRoot),
                        행정동건물내부Ring검토GameView검증.PinnedFileHash);
                    EditorApplication.ExecuteMenuItem("Window/General/Game");
                    diagnostic = "기존 표시 · 250m 비공개 비교 준비 완료";
                });
                return;
            }
            if (GUILayout.Button("기존 표시 · 250m")) Guard(() => { 행정동건물내부Ring검토GameView검증.ShowBefore(); diagnostic = "기존 표시 · 내부 경계 누락"; });
            if (GUILayout.Button("원본 내부 경계 복원 · 250m")) Guard(() => { 행정동건물내부Ring검토GameView검증.ShowAfter(); diagnostic = "원본 내부 경계 복원 · 높이4m 기호값 유지"; });
            if (GUILayout.Button("기존 표시 · 확대")) Guard(() => { 행정동건물내부Ring검토GameView검증.ShowBefore(true); diagnostic = "기존 표시 확대"; });
            if (GUILayout.Button("원본 내부 경계 복원 · 확대")) Guard(() => { 행정동건물내부Ring검토GameView검증.ShowAfter(true); diagnostic = "원본 내부 경계 복원 확대"; });
            if (GUILayout.Button("비교 닫기 · 기존 화면 복원")) Guard(() => { 행정동건물내부Ring검토GameView검증.Clear(); diagnostic = "비공개 비교 제거 · 기존 화면 복원"; });
        }
        private void Guard(Action action)
        { try { action(); } catch (Exception error) { diagnostic = error.Message; } }
        private void OnDisable()
        { if (행정동건물내부Ring검토GameView검증.IsPrepared) 행정동건물내부Ring검토GameView검증.Clear(); }
    }

    public sealed class 행정동건물내부Ring검토CaptureHud : MonoBehaviour
    {
        private void LateUpdate() => 행정동건물내부Ring검토GameView검증.KeepPresentationIsolated();
        private void OnGUI()
        {
            GUI.Box(new Rect(12, Screen.height - 92, 600, 78), "");
            GUI.Label(new Rect(24, Screen.height - 86, 580, 24), 행정동건물내부Ring검토GameView검증.Title);
            GUI.Label(new Rect(24, Screen.height - 61, 580, 22), 행정동건물내부Ring검토GameView검증.Subtitle);
            GUI.Label(new Rect(24, Screen.height - 38, 580, 20), "비공개 도형 검토 · 현재 높이·통행·지형의 실측 증거 아님");
        }
    }
}
