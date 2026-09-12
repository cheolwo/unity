using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Contracts.Common.Metadata;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>공급자 Prefab을 저장하지 않고 소유 PreviewScene에서 측정한다. 실제 World 지면/플레이 증거가 아니다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "Farm 시각 후보의 실측과 기존 배치 검사 재현을 담당한다.",
        Boundary = "격리 실측·합성 지지면 검사는 실제 WI·Scene·Game View 또는 E5 승인 근거가 아니다.")]
    public static class FarmH2실측Tool
    {
        public const string Revision = "farm-h2-prefab-measurement.r1";
        private const string OutputRoot = "artifacts/local/validation/farm-h2-measurement";
        private const string VendorRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private static readonly string[] Assets = {
            "Buildings/SM_Bld_Barn_01.prefab", "Buildings/SM_Bld_Barn_02.prefab",
            "Environments/SM_Env_Dirt_Rows_01.prefab", "Plants/SM_Prop_Plant_Potato_01_S.prefab",
            "Plants/SM_Prop_Plant_Potato_01_L.prefab", "Plants/SM_Prop_Box_Potato_01.prefab",
            "Props/SM_Prop_Well_01.prefab", "Props/SM_Prop_Barrel_01.prefab",
            "Generic/SM_Generic_Grass_Patch_01.prefab" };
        private static readonly string[] CandidateNames = { "01-flat", "02-noise", "03-terrace", "04-rejected-slope" };
        private static readonly string[] CandidateHashes = {
            "7c3e72b8e937b7a2987b34e4aa8456e889b060340f4e5bc19cf3f1b9aff43bf3",
            "8ff572c8b12b50484e7df60de7075ef6a50c85db3ed28f6d6e83b9bfa857421b",
            "370d3df9e4d800a460111a51a4e828ff88fa60e3d4f9f6386855deba7dd0cd9c",
            "c161aa86c33e7ade19229f206cf6143a4a72f96c8bba1640140bb30d3ded0e70" };

        [MenuItem("Ssalddel/통합 월드/배치/Farm H2 격리 실측")]
        public static void 메뉴실행()
        {
            var root = EditorUtility.OpenFolderPanel("현행 Hongdal 저장소 선택 (읽기 전용)", "", "");
            if (!string.IsNullOrWhiteSpace(root)) Debug.Log(Run(root));
        }

        public static string Run(string hongdalRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("EditMode 안정 상태에서만 실측합니다.");
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            var directory = Path.Combine(projectRoot, OutputRoot, stamp);
            Directory.CreateDirectory(directory);
            var before = SceneState();
            var checks = new List<object>();
            var records = new List<object>();
            var dependencies = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var candidates = new List<object>();
            var failures = new List<string>();
            Scene ownedScene = default;
            try
            {
                var baseline = new Dictionary<string, string> {
                    ["docs/Architecture/PlayableLoops/Farm경작세계발현E5.md"] = "FF9D86E660486E77DCEA1D6E6D8A69669C7C7D8A885966D4651486861D477FFC",
                    ["docs/Architecture/PlayableLoops/Farm경작세계발현E5-공간배치연구.r1.md"] = "EADA84A0CA49E2D0BE986866420030612249CC622398F06C7BE83A72B57711B0",
                    ["Ssalddel.Simulation.Application/SimulationFarmH2PlacementAdapter.cs"] = "A62ADE3DEAE0F57D1037DBC5D21604381093D73459997175BFD6FEA037A4C191",
                    ["Ssalddel.Simulation.Contracts/UnityPackage/Runtime/SimulationFarmH2PlacementContracts.cs"] = "DDE27B7A30B092E0ACF4D1C07DC8F1B675A0339BC1E3A7BE84F345DCD8879941" };
                foreach (var item in baseline)
                {
                    var actual = FileHash(Path.Combine(hongdalRoot, item.Key));
                    Require(actual.Equals(item.Value, StringComparison.OrdinalIgnoreCase), "BaselineChanged:" + item.Key);
                }
                Write(directory, "consumed-baseline.json", baseline);
                for (var i = 0; i < CandidateNames.Length; i++)
                {
                    var file = Path.Combine(hongdalRoot, "Ssalddel.Simulation.Tests/Fixtures/FarmH2", CandidateNames[i] + ".plan.json");
                    var candidate = JObject.Parse(File.ReadAllText(file));
                    Require((string)candidate["ResultHash"] == CandidateHashes[i], "CandidateIdentityChanged:" + file);
                    candidates.Add(new { path = file, sha256 = FileHash(file), resultHash = CandidateHashes[i],
                        status = (string)candidate["Status"], placements = candidate["Placements"] });
                }
                // 이전 담당이 소유하는 PreviewScene은 열거/해제하지 않는다.
                ownedScene = EditorSceneManager.NewPreviewScene();
                foreach (var relative in Assets)
                {
                    var path = VendorRoot + relative;
                    try
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        Require(prefab != null, "PrefabMissing:" + path);
                        Require(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                            "ScriptedPrefabRequiresReview:" + path);
                        var deps = AssetDatabase.GetDependencies(path, true).OrderBy(p => p, StringComparer.Ordinal)
                            .Select(p => new { path = p, fileSha256 = RecordFile(p, projectRoot, dependencies),
                                metaSha256 = RecordFile(p + ".meta", projectRoot, dependencies) }).ToArray();
                        var wrapper = NewObject("FarmH2-measurement-owned-wrapper", ownedScene);
                        try
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ownedScene);
                            instance.transform.SetParent(wrapper.transform, false);
                            // 공급자 루트/자식 TRS를 그대로 둔다. identity wrapper가 측정 pivot이다.
                            var first = Measure(wrapper.transform);
                            var second = Measure(wrapper.transform);
                            Check(checks, failures, path + ":repeat-measurement", HashJson(first) == HashJson(second));
                            var box = VisibleBounds(wrapper.transform);
                            var colliders = wrapper.GetComponentsInChildren<Collider>(true);
                            var fingerprint = HashJson(deps);
                            var contract = new {
                                Revision, EvidenceKindCode = "MeasuredWrapper", EvidenceRef = OutputRoot + "/" + stamp + "/measurements.json#" + relative,
                                AssetFingerprintSha256 = fingerprint,
                                // 현행 Adapter.ComputeMeasurementHash가 봉인해야 한다. 다른 JSON hash를 위장하지 않는다.
                                MeasurementHashSha256 = "", CenterX = (double)box.center.x, CenterY = (double)box.center.y,
                                CenterZ = (double)box.center.z, SizeX = (double)box.size.x, SizeY = (double)box.size.y,
                                SizeZ = (double)box.size.z, UniformScale = 1d,
                                ActiveRenderer = wrapper.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy),
                                ActiveCollider = colliders.Any(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger) };
                            records.Add(new {
                                path, guid = AssetDatabase.AssetPathToGUID(path), prefabSha256 = FileHash(Path.Combine(projectRoot, path)),
                                metaSha256 = FileHash(Path.Combine(projectRoot, path + ".meta")), dependencies = deps,
                                dependencyFingerprintSha256 = fingerprint,
                                importerDependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                                axis = "XRightYUpZForward", units = "Unity units; consumer convention 1 unit = 1 meter; not surveyed real-world dimensions",
                                wrapperPosition = new[] { 0f, 0f, 0f }, wrapperYaw = 0, wrapperUniformScale = 1,
                                authoredRoot = Trs(prefab.transform), localMeasurement = first,
                                rendererFootprintSquareUnits = box.size.x * box.size.z,
                                productionAreaSquareMeters = (double?)null, h1WorkArea = "NotMeasured:requires approved composition and access corridors",
                                adapterMeasurementUnsealed = contract,
                                measurementRecordHashSha256 = HashJson(first),
                                adapterSealStatus = "PendingCurrentAdapter.ComputeMeasurementHash; not its canonical hash",
                                modelImporters = deps.Select(d => AssetImporter.GetAtPath(d.path)).OfType<ModelImporter>()
                                    .Select(m => new { m.assetPath, m.globalScale, m.useFileScale }).ToArray() });
                            foreach (var yaw in new[] { 0f, 90f, 180f, 270f })
                            {
                                wrapper.transform.SetPositionAndRotation(new Vector3(100, 20, -40), Quaternion.Euler(0, yaw, 0));
                                wrapper.transform.localScale = Vector3.one * 1.25f;
                                var transformed = VisibleBounds(wrapper.transform);
                                var expected = TransformBounds(box, wrapper.transform.localToWorldMatrix);
                                Check(checks, failures, path + ":cardinal-yaw-" + yaw,
                                    Vector3.Distance(transformed.center, expected.center) < .002f
                                    && Vector3.Distance(transformed.size, expected.size) < .002f);
                            }
                            wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            wrapper.transform.localScale = Vector3.one;
                            ReviewFixture(wrapper.transform, ownedScene, checks, failures, path);
                        }
                        finally { Object.DestroyImmediate(wrapper); }
                    }
                    catch (Exception ex) { failures.Add(path + ":" + ex.Message); records.Add(new { path, error = ex.ToString() }); }
                }
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                if (ownedScene.IsValid()) EditorSceneManager.ClosePreviewScene(ownedScene);
            }
            var after = SceneState();
            Check(checks, failures, "loaded-scenes-selection-dirty-transform-preserved", HashJson(before) == HashJson(after));
            foreach (var pair in dependencies)
                Check(checks, failures, "source-unchanged:" + pair.Key, FileHash(Path.Combine(projectRoot, pair.Key)) == pair.Value);
            Write(directory, "measurements.json", new { revision = Revision, utc = DateTime.UtcNow.ToString("O"),
                unityVersion = UnityEngine.Application.unityVersion, projectRoot, sourceToolSha256 = FileHash(Path.Combine(UnityEngine.Application.dataPath,
                    "Ssalddel/Editor/FarmH2Measurement/FarmH2실측Tool.cs")), records,
                evidence = "ActualEditorPrefabMeasurement; synthetic supports; no Session, terrain, traversal, GameView or E5 proof" });
            Write(directory, "candidate-identities.json", candidates);
            Write(directory, "checks.json", new { checks, failures, before, after,
                passed = failures.Count == 0, existingPreviewScenesRetained = true });
            return JsonConvert.SerializeObject(new { directory, recordCount = records.Count, checkCount = checks.Count,
                failureCount = failures.Count, failures }, Formatting.Indented);
        }

        private static object Measure(Transform wrapper)
        {
            Physics.SyncTransforms();
            return new {
                visibleBounds = BoundsData(VisibleBounds(wrapper)),
                renderers = wrapper.GetComponentsInChildren<Renderer>(true).Select(r => new {
                    path = AnimationUtility.CalculateTransformPath(r.transform, wrapper), type = r.GetType().Name,
                    r.enabled, active = r.gameObject.activeInHierarchy, localRendererBounds = BoundsData(r.localBounds),
                    wrapperBounds = BoundsData(TransformBounds(r.localBounds, wrapper.worldToLocalMatrix * r.localToWorldMatrix)),
                    materials = r.sharedMaterials.Select(m => m == null ? "" : AssetDatabase.GetAssetPath(m)).ToArray() }).ToArray(),
                colliders = wrapper.GetComponentsInChildren<Collider>(true).Select(c => new {
                    path = AnimationUtility.CalculateTransformPath(c.transform, wrapper), type = c.GetType().Name,
                    c.enabled, c.isTrigger, active = c.gameObject.activeInHierarchy,
                    wrapperAabb = BoundsData(TransformBounds(c.bounds, wrapper.worldToLocalMatrix)),
                    geometry = ColliderGeometry(c) }).ToArray() };
        }

        private static object ColliderGeometry(Collider c)
        {
            if (c is BoxCollider b) return new { center = V(b.center), size = V(b.size) };
            if (c is SphereCollider s) return new { center = V(s.center), s.radius };
            if (c is CapsuleCollider p) return new { center = V(p.center), p.radius, p.height, p.direction };
            if (c is MeshCollider m) return new { meshPath = AssetDatabase.GetAssetPath(m.sharedMesh),
                meshName = m.sharedMesh == null ? "" : m.sharedMesh.name, m.convex,
                bounds = m.sharedMesh == null ? null : BoundsData(m.sharedMesh.bounds) };
            return new { unsupportedExactShape = c.GetType().Name };
        }

        private static void ReviewFixture(Transform target, Scene scene, List<object> checks, List<string> failures, string path)
        {
            var bounds = VisibleBounds(target);
            var support = NewObject("FarmH2-owned-synthetic-support", scene);
            var blocker = NewObject("FarmH2-owned-protected-corridor", scene);
            try
            {
                var floor = support.AddComponent<BoxCollider>();
                floor.size = new Vector3(bounds.size.x + 10, 1, bounds.size.z + 10);
                var guard = blocker.AddComponent<BoxCollider>();
                guard.size = new Vector3(Mathf.Max(1, bounds.size.x), 3, Mathf.Max(1, bounds.size.z));
                guard.isTrigger = true;
                var policy = new 배치검토설정(); // 기존 도구 시험값이며 게임 규칙 승인이 아니다.
                target.position = new Vector3(0, policy.지면여유 - bounds.min.y, 0);
                support.transform.position = new Vector3(bounds.center.x, -.5f, bounds.center.z);
                blocker.transform.position = new Vector3(bounds.center.x, 1, bounds.center.z);
                foreach (var mode in new[] { "flat", "buried", "floating", "partial", "steep", "protected" })
                {
                    target.position = new Vector3(0, policy.지면여유 - bounds.min.y + (mode == "buried" ? -.5f : mode == "floating" ? .5f : 0), 0);
                    support.transform.rotation = Quaternion.Euler(mode == "steep" ? 20 : 0, 0, 0);
                    floor.size = new Vector3(mode == "partial" ? bounds.size.x * .4f : bounds.size.x + 10, 1, bounds.size.z + 10);
                    Physics.SyncTransforms();
                    var before = Trs(target);
                    var result = 배치반복검토Service.검사(target, floor, policy, null, mode == "protected" ? new[] { guard } : null);
                    var expected = mode == "flat" ? result.표본검사통과 : mode == "buried" ? result.문제.Contains("가시 하단 매몰")
                        : mode == "floating" ? result.문제.Contains("가시 하단 부유") : mode == "partial" ? result.지지표본.Count < 9
                        : mode == "steep" ? result.경사 > policy.최대경사 : result.문제.Any(x => x.StartsWith("통행 보호 구역 침범", StringComparison.Ordinal));
                    Check(checks, failures, path + ":existing-review:" + mode, expected && HashJson(before) == HashJson(Trs(target)),
                        new { result.문제, result.안내, result.경사, result.높이편차, sampleCount = result.지지표본.Count, policy,
                            syntheticSupport = true, actualWorldPlacement = false });
                }
            }
            finally { Object.DestroyImmediate(blocker); Object.DestroyImmediate(support); }
        }

        private static GameObject NewObject(string name, Scene scene)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        private static Bounds VisibleBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Require(renderers.Length > 0, "ActiveRendererMissing");
            var result = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) result.Encapsulate(r.bounds);
            Require(result.size.x > 0 && result.size.z > 0 && !float.IsNaN(result.size.y), "InvalidRendererBounds");
            return result;
        }
        private static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.min), Vector3.zero);
            for (var x = 0; x < 2; x++) for (var y = 0; y < 2; y++) for (var z = 0; z < 2; z++)
                result.Encapsulate(m.MultiplyPoint3x4(new Vector3(x == 0 ? b.min.x : b.max.x, y == 0 ? b.min.y : b.max.y, z == 0 ? b.min.z : b.max.z)));
            return result;
        }
        private static object SceneState() => new {
            activeHandle = SceneManager.GetActiveScene().handle,
            selection = Selection.entityIds.Select(x => x.ToString()).OrderBy(x => x).ToArray(), previewCount = EditorSceneManager.previewSceneCount,
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
                var s = SceneManager.GetSceneAt(i);
                return new { s.handle, s.path, s.isDirty, roots = s.GetRootGameObjects().Select(g => new {
                    id = g.GetEntityId().ToString(), tree = g.GetComponentsInChildren<Transform>(true).Select(t => new {
                        id = t.GetEntityId().ToString(), t.name, t.gameObject.activeSelf, trs = Trs(t),
                        components = t.GetComponents<Component>().Select(c => c == null ? "" : c.GetEntityId().ToString()).ToArray() }).ToArray() }).ToArray() }; }).ToArray() };
        private static object Trs(Transform t) => new { position = V(t.localPosition), rotation = new[] { t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w }, scale = V(t.localScale) };
        private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        private static object BoundsData(Bounds b) => new { center = V(b.center), size = V(b.size), min = V(b.min), max = V(b.max) };
        private static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        private static void Check(List<object> checks, List<string> failures, string name, bool pass, object detail = null)
        { checks.Add(new { name, pass, detail }); if (!pass) failures.Add(name); }
        private static string RecordFile(string path, string root, IDictionary<string, string> files)
        { var hash = FileHash(Path.Combine(root, path)); files[path] = hash; return hash; }
        private static string FileHash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return Hex(sha.ComputeHash(stream)); }
        private static string HashJson(object value)
        { using var sha = SHA256.Create(); return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None)))); }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static void Write(string directory, string filename, object value)
            => File.WriteAllText(Path.Combine(directory, filename), JsonConvert.SerializeObject(value, Formatting.Indented), new UTF8Encoding(false));
    }
}
