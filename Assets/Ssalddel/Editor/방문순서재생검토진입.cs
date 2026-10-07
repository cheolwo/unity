using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>기존 Scene에 선택동 지도를 명시적으로 준비하거나 읽힌 지도에 연결한다. 저장·위치 승인은 없다.</summary>
    public sealed class 방문순서재생검토진입 : EditorWindow
    {
        private const string Generation = "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753";
        private const string IndexHash = "C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9";
        private const string CompletionHash = "26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D";
        private const string BundleHash = "E1DBB3B99347B4139DE157274C84C881E9821A49BF064BB579813F347925E28A";
        private const string FocusHash = "30E44F71DDA3319FD73C195CD60F14FB7FE3349292798C8EFB86CEA9C30F8713";
        private const string FocusFileHash = "9E53A5968E06421565EE1028EDF13673B39A00950753A8637E4F30D53B72A133";
        private const string Area = "region:kr:hjd:1126057500";
        private const string BundleRelative = "bundles/1126057500.json";
        private string repositoryRoot = string.Empty, recordPath = string.Empty, evidencePath = string.Empty;
        private string diagnostic = "기존 행정동 지도와 비공개 JSON을 명시적으로 선택하세요.";
        private bool trustedEvidence;
        private 행정동디오라마검토View targetView;
        private 방문순서재생검토Presenter presenter;
        private 방문재생공간검토 context;
        private 방문재생준비Result prepared;
        private 방문재생준비Result appliedPrepared;
        private string selectedVisit = string.Empty;
        private Vector2 scroll;
        private GameObject ownedMapHost;
        private 행정동디오라마검토View ownedMapView;
        private string lastError = string.Empty;

        public 행정동디오라마검토View PreparedMap => ownedMapView;
        public 방문순서재생검토Presenter Presenter => presenter;
        public string DiagnosticCode => lastError;

        [MenuItem("Ssalddel/검토/방문 순서 재생")]
        public static void Open() => GetWindow<방문순서재생검토진입>("방문 순서 검토");

        private void OnEnable()
        {
            hideFlags = HideFlags.DontSave;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ClearOwned(); repositoryRoot = recordPath = evidencePath = string.Empty; trustedEvidence = false;
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.ExitingPlayMode) return;
            ClearOwned(); diagnostic = "Play 전환으로 임시 검토 자료 제거 · 전환 후 지도를 명시적으로 다시 준비하세요.";
        }
        private void Tick()
        {
            if (presenter == null || UnityEngine.Application.isPlaying || presenter.CurrentFrame.State != 방문재생상태.Playing) return;
            presenter.Advance(방문순서재생검토Presenter.ClockSeconds); Repaint(); SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("비공개 기록 순서 검토입니다. 표시 이동은 실제 주행선·기사 위치·픽업 완료가 아닙니다. 새 Scene 생성이나 저장은 하지 않습니다.", MessageType.Info);
            targetView = (행정동디오라마검토View)EditorGUILayout.ObjectField("이미 읽힌 행정동 지도", targetView, typeof(행정동디오라마검토View), true);
            if (GUILayout.Button("Hongdal 저장소 폴더 선택")) Pick(ref repositoryRoot, EditorUtility.OpenFolderPanel("동결 자료 저장소", repositoryRoot, ""));
            EditorGUILayout.LabelField(repositoryRoot, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("동결 선택동 지도 직접 준비 · 현재 Scene만 사용")) TryPrepareFrozenMap(repositoryRoot);
            if (GUILayout.Button("방문 기록 JSON 선택")) Pick(ref recordPath, EditorUtility.OpenFilePanel("비공개 방문 기록", "", "json"));
            EditorGUILayout.LabelField(recordPath, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("신뢰 근거 JSON 선택 (선택 사항)"))
            { Pick(ref evidencePath, EditorUtility.OpenFilePanel("검토된 위치 결속 근거", "", "json")); trustedEvidence = false; }
            EditorGUILayout.LabelField(evidencePath, EditorStyles.wordWrappedMiniLabel);
            var nextTrusted = EditorGUILayout.ToggleLeft("선택한 근거의 위치·현재 경계 검토를 별도로 확인했음 (자동 승인 아님)", trustedEvidence);
            if (nextTrusted != trustedEvidence) { trustedEvidence = nextTrusted; prepared = null; }
            if (GUILayout.Button("위치 준비 검사 · 표시하지 않음")) Guard(() =>
            {
                prepared = PrepareRecordFile(recordPath, evidencePath, trustedEvidence);
                diagnostic = prepared.CanReplay ? "위치 준비 통과. 지도 범위 검사는 적용할 때 별도 수행합니다."
                    : "재생 보류 · " + string.Join(", ", prepared.Diagnostics);
            });
            if (GUILayout.Button("선택 지도에 250m 검토 범위 표시")) Guard(() =>
            {
                Require(targetView != null && targetView.gameObject.scene.path == CanonicalScenePath
                    && targetView.ModelRoot != null, "VisitReviewExistingCanonicalMapRequired");
                var candidate = LoadFrozenFocus(repositoryRoot, targetView.Snapshot);
                var created = presenter == null;
                if (created)
                {
                    var host = new GameObject("방문 순서 비공개 검토") { hideFlags = HideFlags.DontSave };
                    host.transform.SetParent(targetView.transform, false);
                    presenter = host.AddComponent<방문순서재생검토Presenter>();
                }
                if (!presenter.TryShowFocus(targetView, candidate))
                {
                    var code = presenter.DiagnosticCode; if (created) ClearOwned();
                    throw new InvalidOperationException(code);
                }
                context = candidate;
                diagnostic = "동결 전체 지도 유지 · 250m 참조 범위 · 선택 건물 " + context.SelectedBuildings.Count + " / 도로 " + context.SelectedRoads.Count;
            });
            using (new EditorGUI.DisabledScope(presenter == null || presenter.BoundView != targetView
                || prepared == null || !prepared.CanReplay))
                if (GUILayout.Button("준비된 방문을 선택 지도에 적용")) Guard(() =>
                {
                    Require(presenter.BoundView == targetView, "VisitReviewSelectedMapMismatch");
                    if (!presenter.TryLoad(prepared, 방문순서재생검토Presenter.ClockSeconds)) throw new InvalidOperationException(presenter.DiagnosticCode);
                    appliedPrepared = prepared; selectedVisit = string.Empty;
                    diagnostic = "준비된 방문 적용 · 명시적 재생 대기";
                });
            EditorGUILayout.HelpBox(diagnostic, MessageType.Info);
            if (presenter != null)
            {
                if (targetView != presenter.BoundView)
                    EditorGUILayout.HelpBox("선택한 지도와 현재 재생 지도가 다릅니다. 검토 범위를 먼저 적용하세요.", MessageType.Warning);
                EditorGUILayout.LabelField("표시 수 / 상태", presenter.MarkerCount + " / " + presenter.CurrentFrame.State);
                if (!string.IsNullOrEmpty(presenter.DiagnosticCode)) EditorGUILayout.LabelField(presenter.DiagnosticCode);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("재생")) presenter.Play(방문순서재생검토Presenter.ClockSeconds);
                if (GUILayout.Button("정지")) presenter.Pause(방문순서재생검토Presenter.ClockSeconds);
                EditorGUILayout.EndHorizontal();
                if (appliedPrepared != null)
                    foreach (var visit in appliedPrepared.Visits)
                        if (GUILayout.Button("방문 " + visit.Sequence + " 선택"))
                        { selectedVisit = visit.VisitId; presenter.SeekVisit(visit.VisitId, 방문순서재생검토Presenter.ClockSeconds); }
                var selected = appliedPrepared?.Visits.FirstOrDefault(v => v.VisitId == selectedVisit);
                if (selected != null) EditorGUILayout.HelpBox(selected.Name + "\n" + selected.Menu, MessageType.None);
            }
            if (GUILayout.Button("이 창이 만든 검토 표시 지우기")) { ClearOwned(); diagnostic = "검토 표시 제거 · 원본 지도 유지"; }
            EditorGUILayout.EndScrollView();
        }

        private void Pick(ref string field, string value) { if (!string.IsNullOrEmpty(value)) { field = value; prepared = null; selectedVisit = string.Empty; } }
        public void ClearReview() { ClearOwned(); diagnostic = "검토 표시 제거 · 외부 지도 유지"; }
        private void ClearOwned()
        {
            if (targetView == ownedMapView) targetView = null;
            if (presenter != null) DestroyImmediate(presenter.gameObject);
            if (ownedMapHost != null) DestroyImmediate(ownedMapHost);
            ownedMapHost = null; ownedMapView = null; lastError = string.Empty;
            presenter = null; context = null; prepared = null; appliedPrepared = null; selectedVisit = string.Empty;
        }
        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                // JSON 내용과 개인 주소를 Console/Scene에 남기지 않는다.
                diagnostic = error is InvalidOperationException && error.Message.StartsWith("Visit", StringComparison.Ordinal)
                    ? error.Message : "VisitReviewInputReadFailed";
                lastError = diagnostic;
            }
        }

        /// <summary>지도 생성 없이 정확히 선택동 1개와 index/completion/focus만 읽는 독립 검사 진입점.</summary>
        public static 방문검토범위입력 ValidateFrozenSources(string repositoryRoot)
            => ValidateFrozenSources(repositoryRoot, out _);

        private static 방문검토범위입력 ValidateFrozenSources(string repositoryRoot, out JObject bundle)
        {
            var basePath = "artifacts/local/validation/admin-dong-diorama-observation-review/r7/input/generations/" + Generation.ToLowerInvariant() + "/";
            var index = ReadPinned(repositoryRoot, basePath + "index.json", IndexHash);
            var complete = ReadPinned(repositoryRoot, basePath + "complete.json", CompletionHash);
            bundle = ReadPinned(repositoryRoot, basePath + BundleRelative, BundleHash);
            var focus = ReadPinned(repositoryRoot, "artifacts/local/validation/admin-dong-road-focus-private-review/r1/generations/" + FocusHash.ToLowerInvariant() + "/focus.json", FocusFileHash);
            var descriptor = index["bundles"].Single(v => (string)v["administrativeAreaStableId"] == Area);
            var completed = complete["files"].Single(v => (string)v["relativePath"] == BundleRelative);
            var completedIndex = complete["files"].Single(v => (string)v["relativePath"] == "index.json");
            Require((string)descriptor["sha256"] == BundleHash && (string)completed["sha256"] == BundleHash
                && (string)completedIndex["sha256"] == IndexHash && (long)descriptor["byteLength"] == 7863199
                && (long)completed["byteLength"] == 7863199, "VisitReviewSourceIndexMismatch");
            var source = focus["sourceBinding"]; var authority = focus["authority"]; var manifest = bundle["manifest"];
            Require((string)focus["schemaVersion"] == "administrative-dong-road-focus-private-review.v1"
                && (string)focus["administrativeAreaStableId"] == Area && (string)focus["contentHashSha256"] == FocusHash
                && (string)source["generationHashSha256"] == Generation && (string)source["indexSha256"] == IndexHash
                && (string)source["completionSha256"] == CompletionHash && (string)source["bundleSha256"] == BundleHash
                && (string)source["bundleRelativePath"] == basePath + BundleRelative && (bool)source["completeSelectedDongValidated"]
                && (int)source["otherDongBundlesRead"] == 0, "VisitReviewFocusSourceMismatch");
            Require((bool)authority["privateReviewOnly"] && (bool)authority["referenceSidecarOnly"]
                && (bool)authority["sourceGeometryUnchanged"] && !(bool)authority["completeTilePayloadReplaced"]
                && !(bool)authority["distributionApproved"] && !(bool)authority["runtimeAuthorized"]
                && !(bool)authority["traversalReady"] && !(bool)authority["gameplayReady"], "VisitReviewAuthorityInvalid");
            Require(JToken.DeepEquals(focus["coordinateFrame"], manifest["coordinateFrame"])
                && (string)source["projectionHashSha256"] == (string)manifest["projectionHashSha256"], "VisitReviewCoordinateFrameMismatch");
            var tiles = bundle["tiles"].ToArray(); var manifestTiles = manifest["tiles"].ToArray();
            Require(tiles.Length == 12 && manifestTiles.Length == tiles.Length
                && tiles.Select(v => (string)v["tileStableId"]).OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(
                    manifestTiles.Select(v => (string)v["tileStableId"]).OrderBy(v => v, StringComparer.Ordinal)), "VisitReviewCompleteSnapshotRequired");
            foreach (var tile in tiles)
            {
                var tileSummary = manifestTiles.Single(v => (string)v["tileStableId"] == (string)tile["tileStableId"]);
                Require((string)tile["projectionHashSha256"] == (string)manifest["projectionHashSha256"]
                    && (string)tile["tileHashSha256"] == (string)tileSummary["tileHashSha256"], "VisitReviewTileMismatch");
            }
            ValidateSelections(focus["selectedBuildings"], tiles, "buildings", "buildingStableId");
            ValidateSelections(focus["selectedRoads"], tiles, "roads", "roadStableId");
            var bounds = manifest["bounds"]; var box = focus["selection"]["boundsCommonEnuMeters"]; var counts = focus["counts"];
            Require((double)focus["selection"]["widthMeters"] == 250 && tiles.Sum(v => v["buildings"].Count()) == (int)counts["sourceBuildings"]
                && tiles.Sum(v => v["roads"].Count()) == (int)counts["sourceRoads"], "VisitReviewCompleteSnapshotRequired");
            return new 방문검토범위입력
            {
                AdministrativeAreaStableId = Area, ProjectionHashSha256 = (string)manifest["projectionHashSha256"],
                FocusContentHashSha256 = FocusHash, SourceBundleHashSha256 = BundleHash,
                CoordinateFrameKey = 방문재생공간검토.FrameKey(manifest["coordinateFrame"].ToObject<AdministrativeDongDioramaCoordinateFrame>()),
                SourceCenterX = ((double)bounds["minX"] + (double)bounds["maxX"]) / 2,
                SourceCenterZ = ((double)bounds["minZ"] + (double)bounds["maxZ"]) / 2,
                MetersPerUnit = (double)manifest["coordinateFrame"]["metersPerUnit"],
                MinX = (double)box["minX"], MinZ = (double)box["minZ"], MaxX = (double)box["maxX"], MaxZ = (double)box["maxZ"],
                SourceTileCount = (int)counts["sourceTiles"], SourceBuildingCount = (int)counts["sourceBuildings"], SourceRoadCount = (int)counts["sourceRoads"],
                SelectedBuildingIds = focus["selectedBuildings"].Select(v => (string)v["buildingStableId"]).ToArray(),
                SelectedRoadIds = focus["selectedRoads"].Select(v => (string)v["roadStableId"]).ToArray(), PrivateReviewOnly = true
            };
        }

        public static 방문재생공간검토 LoadFrozenFocus(string repositoryRoot, 행정동디오라마검토Snapshot snapshot)
            => 방문재생공간검토.Create(snapshot, ValidateFrozenSources(repositoryRoot));

        /// <summary>검증한 동일 바이트로 전체 12타일과 기존 6종 overlay를 해석한다. Scene 객체는 만들지 않는다.</summary>
        public static 행정동디오라마검토Snapshot LoadFrozenSnapshot(string repositoryRoot)
            => LoadFrozenSnapshot(repositoryRoot, out _);

        public static 행정동디오라마검토Snapshot LoadFrozenSnapshot(string repositoryRoot, out 방문재생공간검토 focus)
        {
            var input = ValidateFrozenSources(repositoryRoot, out var bundle);
            var snapshot = CreateFrozenSnapshot(bundle);
            focus = 방문재생공간검토.Create(snapshot, input);
            return snapshot;
        }

        private static 행정동디오라마검토Snapshot CreateFrozenSnapshot(JObject bundle)
        {
            var manifest = bundle["manifest"].ToObject<AdministrativeDongDioramaManifest>();
            var tiles = bundle["tiles"].ToObject<AdministrativeDongDioramaTile[]>();
            var display = bundle["displayOverlays"].ToObject<AdministrativeDongDisplayOverlayResponse>();
            var applied = new AdministrativeDongDioramaInterpreter().Apply(manifest, tiles, display);
            Require(applied.Accepted, "VisitReviewFrozenInterpreterRejected");
            return 행정동디오라마검토Projection.Create(applied,
                bundle["privateObservationOverlays"].ToObject<행정동디오라마비공개관찰OverlaySetData>(),
                bundle["privateWalkNetworkOverlays"].ToObject<행정동디오라마비공개보행망OverlaySetData>(),
                bundle["privateRoadDirectionOverlays"].ToObject<행정동디오라마비공개도로방향OverlaySetData>(),
                bundle["privateBiotopeOverlays"].ToObject<행정동디오라마비공개비오톱OverlaySetData>(),
                bundle["privateLaneMarkingOverlays"].ToObject<행정동디오라마비공개차선표시OverlaySetData>(),
                bundle["privateHorizontalContourTraceOverlays"].ToObject<행정동디오라마비공개수평등고선추적OverlaySetData>());
        }

        private const string CanonicalScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        /// <summary>명시적 호출만 허용한다. 실패 시 기존 View·재생을 남기고 동일 판본 재호출은 객체를 재사용한다.</summary>
        public bool TryPrepareFrozenMap(string repositoryRoot)
        {
            GameObject pending = null;
            try
            {
                var scene = SceneManager.GetActiveScene();
                Require(scene.IsValid() && scene.isLoaded && scene.path == CanonicalScenePath, "VisitReviewCanonicalSceneRequired");
                var input = ValidateFrozenSources(repositoryRoot, out var bundle);
                if (ownedMapView != null && ownedMapView.gameObject.scene == scene && ownedMapView.ModelRoot != null
                    && presenter != null && presenter.BoundView == ownedMapView && presenter.FocusRoot != null
                    && presenter.FocusRoot.parent == ownedMapView.ModelRoot)
                {
                    // 재검증 실패는 기존 표시를 손대지 않는다. 성공해도 재생 시계와 선택을 초기화하지 않는다.
                    context = 방문재생공간검토.Create(ownedMapView.Snapshot, input);
                    targetView = ownedMapView; lastError = string.Empty;
                    diagnostic = "검증된 임시 지도 재사용 · 기존 방문 재생 유지";
                    return true;
                }
                var snapshot = CreateFrozenSnapshot(bundle);
                var focus = 방문재생공간검토.Create(snapshot, input);
                if (appliedPrepared != null)
                {
                    var issue = focus.ValidatePrepared(appliedPrepared);
                    Require(issue.Length == 0, issue);
                }
                // 파일·좌표·목록·overlay·방문 범위의 준비를 모두 마친 뒤에만 객체를 만든다.
                pending = new GameObject("방문 검토 · 동결 선택동 임시 지도") { hideFlags = HideFlags.DontSave };
                pending.SetActive(false);
                SceneManager.MoveGameObjectToScene(pending, scene);
                var nextView = pending.AddComponent<행정동디오라마검토View>();
                nextView.BuildFromSnapshot(snapshot, true);
                var nextPresenter = pending.AddComponent<방문순서재생검토Presenter>();
                Require(nextPresenter.TryShowFocus(nextView, focus), nextPresenter.DiagnosticCode);
                if (appliedPrepared != null)
                    Require(nextPresenter.TryLoad(appliedPrepared, 방문순서재생검토Presenter.ClockSeconds), nextPresenter.DiagnosticCode);
                MarkTransient(pending);
                var previousPresenter = presenter;
                var previousMap = ownedMapHost;
                // 후보가 완성된 뒤 외부 View는 남기고 이 창 소유 표현만 교체한다.
                presenter = nextPresenter; ownedMapHost = pending; ownedMapView = nextView;
                targetView = nextView; context = focus; pending = null;
                if (previousPresenter != null) DestroyImmediate(previousPresenter.gameObject);
                if (previousMap != null) DestroyImmediate(previousMap);
                ownedMapHost.SetActive(true); lastError = string.Empty;
                diagnostic = "동결 선택동 임시 지도 준비 · 12타일/2700건물/291도로 · 기존 6종 overlay · 실제 방문 위치 승인 아님";
                return true;
            }
            catch (Exception error)
            {
                if (pending != null) DestroyImmediate(pending);
                lastError = error is InvalidOperationException && error.Message.StartsWith("Visit", StringComparison.Ordinal)
                    ? error.Message : "VisitReviewMapPreparationFailed";
                diagnostic = lastError;
                return false;
            }
        }

        private static void MarkTransient(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                component.hideFlags |= HideFlags.DontSave;
                component.gameObject.hideFlags |= HideFlags.DontSave;
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) filter.sharedMesh.hideFlags |= HideFlags.DontSave;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) material.hideFlags |= HideFlags.DontSave;
        }

        /// <summary>근거 파일을 명시적으로 신뢰한 경우만 결속을 전달한다. 기록의 Verified 상태를 바꾸지 않는다.</summary>
        public static 방문재생준비Result PrepareRecordFile(string recordPath, string evidencePath, bool trustedEvidence)
        {
            var bytes = ReadBounded(recordPath, 2 * 1024 * 1024);
            var record = new 방문기록JsonDecoder().Decode(new UTF8Encoding(false, true).GetString(bytes));
            var revision = Hash(bytes);
            var bindings = Array.Empty<방문위치결속>();
            if (trustedEvidence)
            {
                Require(!string.IsNullOrWhiteSpace(evidencePath), "VisitReviewEvidenceRequired");
                var evidence = Parse(ReadBounded(evidencePath, 2 * 1024 * 1024));
                Require((string)evidence["schemaVersion"] == "visit-location-bindings.private-review.v1"
                    && (string)evidence["recordRevision"] == revision && (string)evidence["recordId"] == record.RecordId,
                    "VisitReviewEvidenceRevisionMismatch");
                bindings = evidence["items"].Select(v => new 방문위치결속(record.RecordId, revision,
                    (string)v["visitId"], (string)v["storeId"], (string)v["address"], (double)v["latitude"], (double)v["longitude"],
                    (string)v["administrativeAreaStableId"], (string)v["coordinateFrameKey"], (double)v["x"], (double)v["z"],
                    (string)v["coordinateRevision"], (string)v["boundaryRevision"], (string)v["evidenceReference"])).ToArray();
            }
            return 방문재생준비.Prepare(record, revision, bindings);
        }

        private static void ValidateSelections(JToken selections, JToken[] tiles, string collection, string id)
        {
            var values = selections.ToArray();
            Require(values.Length > 0 && values.Select(v => (string)v[id]).Distinct(StringComparer.Ordinal).Count() == values.Length, "VisitReviewSelectionIdsInvalid");
            foreach (var selected in values)
            {
                var tile = tiles.SingleOrDefault(v => (string)v["tileStableId"] == (string)selected["sourceTileStableId"]);
                Require(tile != null && tile[collection].Any(v => (string)v[id] == (string)selected[id]), "VisitReviewSelectedIdMissing");
            }
        }
        private static JObject ReadPinned(string repositoryRoot, string relative, string hash)
        {
            Require(!string.IsNullOrWhiteSpace(repositoryRoot), "VisitReviewRepositoryRequired");
            var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, relative));
            Require(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), "VisitReviewPathInvalid");
            for (var part = new FileInfo(path) as FileSystemInfo; part != null; part = part is FileInfo file ? file.Directory : ((DirectoryInfo)part).Parent)
            {
                Require((part.Attributes & FileAttributes.ReparsePoint) == 0, "VisitReviewLinkedPathRejected");
                if (part.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
            }
            var bytes = ReadBounded(path, 20 * 1024 * 1024);
            Require(Hash(bytes) == hash, "VisitReviewFrozenFileHashMismatch");
            return Parse(bytes);
        }
        private static byte[] ReadBounded(string path, long maximum)
        {
            Require(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "VisitReviewFileRequired");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Require(stream.Length > 0 && stream.Length <= maximum, "VisitReviewFileSizeInvalid");
            var bytes = new byte[(int)stream.Length]; var offset = 0;
            while (offset < bytes.Length) { var count = stream.Read(bytes, offset, bytes.Length - offset); Require(count > 0, "VisitReviewFileReadIncomplete"); offset += count; }
            return bytes;
        }
        private static JObject Parse(byte[] bytes)
        {
            using var input = new StringReader(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'));
            using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal, MaxDepth = 64 };
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Require(!reader.Read(), "VisitReviewJsonTrailingContent"); return result;
        }
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Require(bool value, string code) { if (!value) throw new InvalidOperationException(code); }
    }
}
