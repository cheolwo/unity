using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 고정한 기존 동네 모형과 영상 근거 세 점포를 한 비공개 연구 장면에서 비교한다.
    /// PreviewScene만 소유하며 정본 건물 대응, Scene 저장, 사용자 승인 또는 실제 운행을 만들지 않는다.
    /// </summary>
    public static partial class 사가정영상선분디오라마DemoCapture
    {
        public const string InputSchema = "private-video-segment-diorama-demo-input.v1";
        public const string RoadSchema = "private-video-road-candidate-demo-layer.v1";
        public const string CandidateStatus = "AI 원본 탐색 후보 · 편집 불가";
        public const int ReviewLayer = 31;
        public const float SymbolicHeight = 4f;
        private const int Width = 1920, Height = 1080;

        private sealed class Building
        {
            public JObject Evidence, Reference, Frame, PreviousCase;
            public string Id, Slug, EvidenceHash;
            public Vector2[] Ring;
            public Bounds Bounds;
            public int FrontEdge;
            public Vector3 Face, Normal;
            public float FacadeWidth;
        }

        private sealed class Inputs
        {
            public readonly Dictionary<string, JObject> References = new(StringComparer.OrdinalIgnoreCase);
            public JObject Read(JToken reference)
            {
                var bytes = Pin(reference, 128 * 1024 * 1024, ".json");
                return JObject.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            }
            public byte[] Pin(JToken reference, long maximum, string suffix = null)
            {
                var path = reference?.Value<string>("path"); var expected = reference?.Value<string>("sha256");
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !Sha(expected)
                    || suffix != null && !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("NeighborhoodDemoPinnedArtifactRequired");
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > maximum
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("NeighborhoodDemoArtifactBudgetOrFileRejected");
                var bytes = File.ReadAllBytes(path);
                if (!string.Equals(Hash(bytes), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("NeighborhoodDemoArtifactHashMismatch");
                References[Path.GetFullPath(path)] = new JObject { ["path"] = Path.GetFullPath(path), ["sha256"] = expected };
                return bytes;
            }
            public void Recheck()
            {
                foreach (var reference in References.Values)
                    if (!string.Equals(Hash(File.ReadAllBytes(reference.Value<string>("path"))),
                        reference.Value<string>("sha256"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("NeighborhoodDemoInputChangedDuringCapture");
            }
        }

        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || EditorApplication.isPlaying)
                throw new InvalidOperationException("NeighborhoodDemoGraphicsEditModeRequired");
            var inputPath = Environment.GetEnvironmentVariable("SSALDDEL_ROUTE_DIORAMA_DEMO_MANIFEST");
            var inputHash = Environment.GetEnvironmentVariable("SSALDDEL_ROUTE_DIORAMA_DEMO_SHA256");
            var output = Environment.GetEnvironmentVariable("SSALDDEL_ROUTE_DIORAMA_DEMO_OUTPUT");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)
                || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("NeighborhoodDemoNewOutputRequired");
            var inputs = new Inputs();
            var inputReference = new JObject { ["path"] = inputPath, ["sha256"] = inputHash };
            var input = inputs.Read(inputReference);
            ValidateInput(input);
            var styleKey = Environment.GetEnvironmentVariable("SSALDDEL_ROUTE_DIORAMA_STYLE") ?? input.Value<string>("renderStyle");
            if (!string.IsNullOrWhiteSpace(styleKey) && styleKey != 사가정비공개현실감Style.StyleKey)
                throw new InvalidOperationException("NeighborhoodDemoRenderStyleRejected");
            var useRealism = styleKey == 사가정비공개현실감Style.StyleKey;
            var detailKey = input.Value<string>("detailedBuildingStyle");
            if (!string.IsNullOrWhiteSpace(detailKey) && detailKey != "storefront-r1")
                throw new InvalidOperationException("NeighborhoodDemoDetailedStyleRejected");
            var baseAsset = Resources.Load<TextAsset>("SagajeongReference");
            if (baseAsset == null) throw new InvalidOperationException("NeighborhoodDemoBaseMapMissing");
            var map = JsonUtility.FromJson<공간참고자료>(baseAsset.text);
            var overlay = inputs.Read(input["baseOverlay"]);
            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                Encoding.UTF8.GetBytes(overlay.ToString(Formatting.None)), map, baseAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);
            if (!composition.OverlayApplied || composition.Buildings.Count != 2247
                || composition.Buildings.Any(b => !b.Id.StartsWith("overture:", StringComparison.Ordinal)))
                throw new InvalidOperationException("NeighborhoodDemoOriginalOverture2247Required");
            var index = inputs.Read(input["evidenceIndex"]);
            var previous = inputs.Read(input["previousStudy"]);
            var signLabels = inputs.Read(input["signLabels"]);
            var modulePreview = inputs.Read(input["modulePreview"]);
            var roadLayer = inputs.Read(input["roadCandidateLayer"]);
            foreach (var key in new[] { "videoEvidence", "inputValidation" })
                if (input[key] != null) inputs.Read(input[key]);
            ValidateRoadLayer(roadLayer);
            var topology = inputs.Read(roadLayer["topology"]);
            var roadGeometry = ReadRoadGeometry(roadLayer, topology, inputs);
            var buildings = ReadBuildings(inputs, input, index, previous, composition);
            var buildingBindings = input["buildingBindingManifest"] == null ? null : inputs.Read(input["buildingBindingManifest"]);
            if(buildingBindings!=null)
                foreach(var building in buildings)
                    사가정영상건물BindingGate.ValidateJson(buildingBindings.ToString(),building.Id,building.EvidenceHash,
                        building.Evidence["buildingCandidate"].Value<string>("geometryHash"),Hash(baseAsset.bytes),
                        input["baseOverlay"].Value<string>("sha256"),building.Ring);
            var previewMap = map;
            JObject previewMask = null;
            if (input["previewOcclusionMask"] != null)
            {
                previewMask = inputs.Read(input["previewOcclusionMask"]);
                ValidatePreviewMask(previewMask);
                if (previewMask.Value<string>("baseMapSha256") != Hash(baseAsset.bytes)
                    || previewMask.Value<string>("baseOverlaySha256") != input["baseOverlay"].Value<string>("sha256")
                    || previewMask.Value<string>("evidenceIndexSha256") != input["evidenceIndex"].Value<string>("sha256"))
                    throw new InvalidOperationException("NeighborhoodDemoPreviewMaskInputBindingMismatch");
                var rows = (JArray)previewMask["suppressedLegacyBuildings"];
                foreach (var row in rows)
                    if (!buildings.Any(b => b.Id == row.Value<string>("studyTargetBuildingId"))
                        || composition.TryResolveLegacyBuilding(row.Value<string>("legacyBuildingId"), out _))
                        throw new InvalidOperationException("NeighborhoodDemoMaskMustNotReplaceOverlayIdentity");
                previewMap = CreatePreviewBaselineCopy(map, rows.Values<JObject>()
                    .Select(r => r.Value<string>("legacyBuildingId")).ToArray());
            }
            if (((JArray)roadLayer["candidates"]).Any(r => !buildings.Any(b => b.Frame.Value<string>("sourceSha256") == r.Value<string>("sourceSha256"))))
                throw new InvalidOperationException("NeighborhoodDemoRoadAndObservedVideoSourceMismatch");
            var facingRoads = roadGeometry.Concat((map.roads ?? Array.Empty<참고도로>())
                .Where(r => r != null).Select(r => new[] { new Vector2(r.x1, r.z1), new Vector2(r.x2, r.z2) })).ToArray();
            foreach (var building in buildings) SetFront(building, facingRoads);
            var studyHash = Hash(Encoding.UTF8.GetBytes(inputHash + "|PrivateDetachedNeighborhood|SymbolicFallback4m"));
            var catalog = 사가정외관비공개검토준비.CreateTransientCandidates();
            var scene = EditorSceneManager.NewPreviewScene();
            var owned = new List<Object>();
            사가정외관OverlayRenderer appearance = null;
            사가정비공개현실감Style.Scope realism = null;
            사가정비공개현실감Style.Report realismReport = null;
            var results = new JArray();
            var originalScene = SceneManager.GetActiveScene();
            var cleanup = false;
            Directory.CreateDirectory(output);
            try
            {
                ConfigureReviewedModules(inputs, input, modulePreview, catalog);
                var root = new GameObject("통합 비공개 동네 시안 · 정본 미반영") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var lightObject = Child(root.transform, "비공개 연구 장면 조명");
                var light = lightObject.gameObject.AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1; light.cullingMask = 1 << ReviewLayer; light.shadows = LightShadows.None;
                lightObject.rotation = Quaternion.Euler(45, -30, 0);
                var baseRoot = Child(root.transform, "기존 Overture 2247 · 원문 유지");
                var baseReport = 사가정모형MeshBuilder.BuildBatches(previewMap, null, composition,
                    batch => AddBatch(baseRoot, batch, owned), false);
                var studyRoot = Child(root.transform, "미반영 실제 GIS 3건물 연구 레이어 · 높이 4m 기호값");
                foreach (var b in buildings) BuildStudyMass(studyRoot, b, owned);
                var routeRoot = Child(root.transform, "빨강·흰테두리 점선 · 선분 후보이며 실제 운행 아님");
                DrawRoadCandidates(routeRoot, roadGeometry, owned);
                var detail = Child(studyRoot, "영상 관측 외관 · 정면·치수 실측 아님");
                appearance = new 사가정외관OverlayRenderer(detail,
                    사가정외관검토Gate.CreatePrivateStudy(studyHash,
                        buildings.ToDictionary(b => b.Id, b => b.Bounds, StringComparer.Ordinal)), ReviewLayer);
                var applied = new JArray();
                foreach (var b in buildings)
                    BuildAppearance(detail, b, signLabels, inputs, catalog, appearance, studyHash, applied, owned);
                detail.gameObject.SetActive(false);
                var allBounds = buildings[0].Bounds;
                foreach (var b in buildings.Skip(1)) allBounds.Encapsulate(b.Bounds);
                var averageNormal = buildings.Aggregate(Vector3.zero, (sum, b) => sum + b.Normal);
                if (averageNormal.sqrMagnitude < .1f) averageNormal = new Vector3(1, 0, -1);
                var views = new[] { ("overview", allBounds, averageNormal.normalized + Vector3.up * 1.2f, 1.65f) }
                    .Concat(buildings.Select(b => (b.Slug, b.Bounds, b.Normal + Vector3.up * .95f, 2.2f))).ToArray();
                var cameras = views.Select(v => CameraFor(root.transform, v.Item2, v.Item3, v.Item4)).ToArray();
                var beforeChecks = new JObject[views.Length]; var afterChecks = new JObject[views.Length];
                for (var i = 0; i < views.Length; i++)
                    beforeChecks[i] = Capture(cameras[i], Path.Combine(output, views[i].Item1 + "-before.png"));
                detail.gameObject.SetActive(true);
                for (var i = 0; i < views.Length; i++)
                    afterChecks[i] = Capture(cameras[i], Path.Combine(output, views[i].Item1 + "-after.png"));
                var styleChecks = new JObject[views.Length]; var styleRestoredChecks = new JObject[views.Length];
                if (useRealism)
                {
                    realism = 사가정비공개현실감Style.Apply(root.transform, light, cameras,
                        views.Select(v => v.Item2.center).ToArray(), routeRoot);
                    realismReport = realism.Result;
                    for (var i = 0; i < views.Length; i++)
                        styleChecks[i] = Capture(cameras[i], Path.Combine(output, views[i].Item1 + "-after-style.png"));
                    realism.Dispose(); realism = null;
                    for (var i = 0; i < views.Length; i++)
                    {
                        var path = Path.Combine(output, views[i].Item1 + "-after-style-restored.png");
                        styleRestoredChecks[i] = Capture(cameras[i], path);
                        if (Hash(File.ReadAllBytes(path)) != Hash(File.ReadAllBytes(Path.Combine(output, views[i].Item1 + "-after.png"))))
                            throw new InvalidOperationException("NeighborhoodDemoStyleRestorePixelsDiffer");
                    }
                }
                var detailedResult = detailKey == "storefront-r1"
                    ? CaptureDetailedBuildings(root.transform, studyRoot, routeRoot, light, cameras, views.Select(v => v.Item2.center).ToArray(),
                        buildings, inputs, input, signLabels, catalog, studyHash, output) : null;
                var removedModules = appearance.Count;
                appearance.Clear();
                detail.gameObject.SetActive(false);
                for (var i = 0; i < views.Length; i++)
                {
                    var (name, bounds, direction, _) = views[i];
                    var before = Path.Combine(output, name + "-before.png");
                    var after = Path.Combine(output, name + "-after.png");
                    var restored = Path.Combine(output, name + "-restored.png");
                    var restoredCheck = Capture(cameras[i], restored);
                    if (Hash(File.ReadAllBytes(before)) != Hash(File.ReadAllBytes(restored)))
                        throw new InvalidOperationException("NeighborhoodDemoRestorePixelsDiffer");
                    if (Hash(File.ReadAllBytes(before)) == Hash(File.ReadAllBytes(after)))
                        throw new InvalidOperationException("NeighborhoodDemoAppearanceNotVisible");
                    results.Add(new JObject { ["view"] = name, ["before"] = Reference(before), ["after"] = Reference(after),
                        ["restored"] = Reference(restored), ["restoredPixelsEqualBefore"] = true,
                        ["appearancePixelsDiffer"] = true, ["pixelChecks"] = new JArray(beforeChecks[i], afterChecks[i], restoredCheck),
                        ["restorationMode"] = "ReviewedModulesRemovedAndDetachedColorLabelLayerDisabled",
                        ["afterRemovalOverlayCount"] = appearance.Count,
                        ["cameraCenter"] = Vector(bounds.center), ["cameraDirection"] = Vector(direction),
                        ["styleAfter"] = styleChecks[i]?["image"]?.DeepClone(),
                        ["styleRestored"] = styleRestoredChecks[i]?["image"]?.DeepClone(),
                        ["styleRestoredPixelsEqualFlatAfter"] = useRealism ? JToken.FromObject(true) : JValue.CreateNull(),
                        ["existingNeighborhoodBaseVisible"] = true, ["notAnIsolatedBuildingStage"] = true });
                    Object.DestroyImmediate(cameras[i].gameObject);
                }
                inputs.Recheck();
                WriteNew(Path.Combine(output, "demo-result.private.json"), new JObject
                {
                    ["schema"] = "private-video-segment-neighborhood-demo-render.v1", ["input"] = inputReference,
                    ["inputReferences"] = new JArray(inputs.References.Values), ["views"] = results,
                    ["baseMapHash"] = Hash(baseAsset.bytes), ["baseOverlayBuildingCount"] = composition.Buildings.Count,
                    ["baseOverlayContentHash"] = composition.ContentHash, ["baseRenderedBuildingCount"] = baseReport.RenderedBuildingCount,
                    ["originalBaselineBuildingCount"] = map.buildings.Length,
                    ["previewBaselineBuildingCount"] = previewMap.buildings.Length,
                    ["previewOcclusionMask"] = input["previewOcclusionMask"]?.DeepClone(),
                    ["suppressedLegacyBuildings"] = previewMask?["suppressedLegacyBuildings"]?.DeepClone() ?? new JArray(),
                    ["suppressionIsPreviewOnlyGeometricOcclusionNotIdentityBinding"] = previewMask != null,
                    ["overlapMetricsRecomputedByCapture"] = false, ["originalBaseMapModified"] = false,
                    ["studyScope"] = "DetachedUnappliedExactGisBuildingResearchLayer", ["studySpatialHash"] = studyHash,
                    ["buildings"] = new JArray(buildings.Select(b => BuildingReport(b))),
                    ["buildingBindingManifest"] = input["buildingBindingManifest"]?.DeepClone(),
                    ["sourceBackedPrivateLayerBindingCount"] = buildingBindings==null?0:buildings.Length,
                    ["roadCandidateLayer"] = input["roadCandidateLayer"].DeepClone(),
                    ["roadCandidateCount"] = ((JArray)roadLayer["candidates"]).Count,
                    ["roadStyle"] = "StaticDashedRedWithWhiteBorder", ["roadIsActualDrivenRoute"] = false,
                    ["individualSegmentPassageTimingConfirmed"] = false, ["drivingAnimationCreated"] = false,
                    ["appliedStudyModules"] = applied, ["removedStudyModuleCount"] = removedModules,
                    ["afterRemovalOverlayCount"] = appearance.Count, ["canonicalAppliedCount"] = 0,
                    ["userMapApprovalCreated"] = false, ["sceneSaved"] = false, ["editorPrefsModified"] = false,
                    ["sourceAssetsModified"] = false, ["offscreenCameraRendering"] = true,
                    ["gameViewVerified"] = false, ["publicationReady"] = false,
                    ["originalActiveSceneUnchanged"] = SceneManager.GetActiveScene() == originalScene,
                    ["consoleErrorsMustBeCheckedByRunner"] = true,
                    ["renderStyle"] = useRealism ? styleKey : "original-flat-default",
                    ["realismStyle"] = realismReport == null ? null : JObject.Parse(사가정비공개현실감Style.SerializeReport(realismReport)),
                    ["detailedBuildings"] = detailedResult,
                });
            }
            finally
            {
                realism?.Dispose();
                appearance?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
                Object.DestroyImmediate(catalog);
                cleanup = !scene.IsValid() || !scene.isLoaded;
                WriteNew(Path.Combine(output, "cleanup.private.json"), new JObject
                { ["previewSceneClosed"] = cleanup, ["sourceSceneSaved"] = false,
                    ["originalActiveSceneUnchanged"] = SceneManager.GetActiveScene() == originalScene });
            }
        }

        public static void ValidateInputJson(string json) => ValidateInput(JObject.Parse(json));
        private static void ValidateInput(JObject input)
        {
            if (input.Value<string>("schema") != InputSchema) throw new InvalidOperationException("NeighborhoodDemoSchemaRejected");
            foreach (var key in new[] { "baseOverlay", "evidenceIndex", "previousStudy", "signLabels", "modulePreview", "roadCandidateLayer" })
                if (!Sha(input[key]?.Value<string>("sha256")) || string.IsNullOrWhiteSpace(input[key]?.Value<string>("path")))
                    throw new InvalidOperationException("NeighborhoodDemoReferenceRequired:" + key);
            if (input["reviewedModuleKeys"] is not JArray keys || keys.Count == 0
                || input.Value<bool?>("canonicalApplyRequested") == true || input.Value<bool?>("userApprovalRequested") == true)
                throw new InvalidOperationException("NeighborhoodDemoResearchBoundaryRequired");
        }

        public static void ValidateRoadLayerJson(string json) => ValidateRoadLayer(JObject.Parse(json));
        private static void ValidateRoadLayer(JObject layer)
        {
            if (layer.Value<string>("schema") != RoadSchema || layer["candidates"] is not JArray rows || rows.Count == 0
                || rows.Count > 200 || layer.Value<bool?>("actualDrivenRouteConfirmed") == true
                || layer.Value<bool?>("actualRouteConfirmed") == true || layer.Value<bool?>("timeSynchronizedDrivingPath") == true
                || layer.Value<bool?>("userApproved") == true)
                throw new InvalidOperationException("NeighborhoodDemoCandidateRoadBoundaryRequired");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
                if (string.IsNullOrWhiteSpace(row.Value<string>("candidateId")) || !Sha(row.Value<string>("sourceSha256"))
                    || string.IsNullOrWhiteSpace(row.Value<string>("segmentId")) || !Sha(row.Value<string>("geometryHash"))
                    || string.IsNullOrWhiteSpace(row.Value<string>("segmentCode")) || row.Value<string>("status") != CandidateStatus
                    || row.Value<bool?>("userApproved") == true || row.Value<bool?>("individualTimingConfirmed") == true
                    || row.Value<bool?>("actualRouteConfirmed") == true || row.Value<bool?>("wholeSegmentTraversalConfirmed") == true
                    || row.Value<bool?>("individualSegmentEntryExitTimesConfirmed") == true || row.Value<bool?>("sourceWindowEvenlyDivided") == true
                    || !identities.Add(row.Value<string>("candidateId") + "|" + row.Value<string>("segmentId")))
                    throw new InvalidOperationException("NeighborhoodDemoCandidateIdentityOrStatusRejected");
        }

        public static void ValidatePreviewMaskJson(string json) => ValidatePreviewMask(JObject.Parse(json));
        private static void ValidatePreviewMask(JObject mask)
        {
            if (mask.Value<string>("schema") != "private-neighborhood-demo-preview-occlusion-mask.v1"
                || mask.Value<string>("suppressionBasis") != "GeometricOverlapOnlyNotIdentityBinding"
                || !Sha(mask.Value<string>("baseMapSha256")) || !Sha(mask.Value<string>("baseOverlaySha256"))
                || !Sha(mask.Value<string>("evidenceIndexSha256"))
                || mask["suppressedLegacyBuildings"] is not JArray rows || rows.Count == 0 || rows.Count > 32)
                throw new InvalidOperationException("NeighborhoodDemoExplicitPreviewOcclusionMaskRequired");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var area = row.Value<double?>("intersectionAreaSquareMeters");
                var iou = row.Value<double?>("intersectionOverUnion");
                var coverage = row.Value<double?>("smallerFootprintCoverage");
                if (string.IsNullOrWhiteSpace(row.Value<string>("legacyBuildingId"))
                    || string.IsNullOrWhiteSpace(row.Value<string>("studyTargetBuildingId"))
                    || !ids.Add(row.Value<string>("legacyBuildingId"))
                    || !area.HasValue || !double.IsFinite(area.Value) || area <= 0
                    || !iou.HasValue || !double.IsFinite(iou.Value) || iou <= 0 || iou > 1
                    || !coverage.HasValue || !double.IsFinite(coverage.Value) || coverage <= 0 || coverage > 1)
                    throw new InvalidOperationException("NeighborhoodDemoPreviewOcclusionRowRejected");
            }
        }

        /// <summary>기하 충돌을 숨기는 새 사본일 뿐 원래 건물 ID를 새 GIS ID로 바꾸지 않는다.</summary>
        public static 공간참고자료 CreatePreviewBaselineCopy(공간참고자료 original, string[] suppressedIds)
        {
            if (original?.buildings == null || suppressedIds == null || suppressedIds.Length == 0
                || suppressedIds.Length > 32 || suppressedIds.Distinct(StringComparer.Ordinal).Count() != suppressedIds.Length
                || suppressedIds.Any(id => string.IsNullOrWhiteSpace(id)
                    || original.buildings.Count(b => b != null && b.id == id) != 1))
                throw new ArgumentException("NeighborhoodDemoUniqueExistingLegacyMaskRequired");
            var copy = JsonUtility.FromJson<공간참고자료>(JsonUtility.ToJson(original));
            var ids = suppressedIds.ToHashSet(StringComparer.Ordinal);
            copy.buildings = copy.buildings.Where(b => !ids.Contains(b.id)).ToArray();
            return copy;
        }

        private static Vector2[][] ReadRoadGeometry(JObject layer, JObject topology, Inputs inputs)
        {
            var segments = (JArray)topology["segments"];
            var result = new List<Vector2[]>();
            foreach (var row in (JArray)layer["candidates"])
            {
                var matched = segments.Where(s => s.Value<string>("segmentId") == row.Value<string>("segmentId")
                    && s.Value<string>("segmentCode") == row.Value<string>("segmentCode")).ToArray();
                if (matched.Length != 1 || !JToken.DeepEquals(matched[0]["geometry"], row["geometry"]))
                    throw new InvalidOperationException("NeighborhoodDemoExactTopologyGeometryRequired");
                var observed = inputs.Read(row["evidenceReference"]);
                var observedSegment = ReadPointer(observed, row["evidenceReference"].Value<string>("jsonPointer"));
                if (observed.Value<string>("sourceSha256") != row.Value<string>("sourceSha256")
                    || observedSegment.Value<string>("segmentId") != row.Value<string>("segmentId")
                    || observedSegment.Value<string>("segmentCode") != row.Value<string>("segmentCode")
                    || !JToken.DeepEquals(observedSegment["geometry"], row["geometry"]))
                    throw new InvalidOperationException("NeighborhoodDemoVideoCandidateEvidenceMismatch");
                var geometry = row["geometry"];
                var lines = geometry.Value<string>("type") == "LineString"
                    ? new[] { (JArray)geometry["coordinates"] }
                    : geometry.Value<string>("type") == "MultiLineString"
                        ? ((JArray)geometry["coordinates"]).Cast<JArray>().ToArray()
                        : throw new InvalidOperationException("NeighborhoodDemoRoadGeometryRejected");
                foreach (var line in lines)
                {
                    var points = line.Select(p => ProjectWgs84((double)p[1], (double)p[0])).ToArray();
                    if (points.Length < 2 || points.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y)))
                        throw new InvalidOperationException("NeighborhoodDemoRoadCoordinatesRejected");
                    result.Add(points);
                }
            }
            return result.ToArray();
        }

        private static Building[] ReadBuildings(Inputs inputs, JObject input, JObject index, JObject previous,
            사가정공간표현Composition composition)
        {
            if (index.Value<string>("schema") != "private-diorama-facade-evidence-index.v1"
                || previous.Value<string>("schema") != "private-building-facade-study-render.v1"
                || previous.Value<bool?>("canonicalDioramaApplied") != false
                || !SameReference(previous["evidenceIndex"], input["evidenceIndex"]))
                throw new InvalidOperationException("NeighborhoodDemoPriorStudyBindingRequired");
            var result = new List<Building>();
            foreach (var reference in (JArray)index["candidateEvidence"])
            {
                var data = inputs.Read(reference);
                var target = data["buildingCandidate"];
                if (data.Value<string>("schema") != "private-diorama-facade-observation-evidence.v1"
                    || target?.Value<string>("bindingStatus") != "reviewed_exact_building_id")
                    throw new InvalidOperationException("NeighborhoodDemoExactStudyBuildingRequired");
                var publicData = inputs.Read(target["geometryReference"]);
                var candidates = ReadPointer(publicData, target["geometryReference"].Value<string>("jsonPointer")) as JArray;
                var matches = candidates?.Where(c => c.Value<string>("building_id") == target.Value<string>("placeId")
                    && c.Value<string>("gisBuildingId") == target.Value<string>("gisBuildingId")).ToArray();
                if (matches?.Length != 1 || !JToken.DeepEquals(matches[0]["geometry"], target["geometry"]))
                    throw new InvalidOperationException("NeighborhoodDemoOriginalGeometryMismatch");
                var id = target.Value<string>("placeId");
                if (composition.Buildings.Any(b => b.Id == id || b.SourceFeatureId == target.Value<string>("gisBuildingId")))
                    throw new InvalidOperationException("NeighborhoodDemoRequiresDetachedUnappliedTarget");
                var priorCases = ((JArray)previous["cases"]).Where(c => c.Value<string>("targetBuildingId") == id
                    && c.Value<string>("evidenceSha256") == reference.Value<string>("sha256")).ToArray();
                if (priorCases.Length != 1 || priorCases[0].Value<bool?>("restoredPixelsEqualBefore") != true
                    || priorCases[0].Value<int?>("afterRemovalOverlayCount") != 0)
                    throw new InvalidOperationException("NeighborhoodDemoPriorCaseAndRestoreRequired");
                var prior = (JObject)priorCases[0];
                var frames = ((JArray)data["frames"]).Where(f => f.Value<bool?>("directlyViewed") == true
                    && f.Value<bool?>("protectedOverlap") == false
                    && f.Value<long?>("actualPts") == prior.Value<long?>("actualPts")
                    && f.Value<string>("timeBase") == prior.Value<string>("timeBase")
                    && f.Value<string>("sourceSha256") == prior.Value<string>("sourceSha256")
                    && SameReference(f["frame"], prior["observedFrame"])).ToArray();
                if (frames.Length != 1) throw new InvalidOperationException("NeighborhoodDemoExactObservedFrameRequired");
                inputs.Pin(frames[0]["frame"], 32 * 1024 * 1024, ".jpg");
                var geometry = target["geometry"];
                if (geometry.Value<string>("type") != "Polygon" || ((JArray)geometry["coordinates"]).Count != 1)
                    throw new InvalidOperationException("NeighborhoodDemoSimpleStudyFootprintRequired");
                var ring = ((JArray)geometry["coordinates"][0]).Select(p => ProjectWgs84((double)p[1], (double)p[0])).ToArray();
                if (ring.Length < 4 || Vector2.Distance(ring[0], ring[^1]) > .01f)
                    throw new InvalidOperationException("NeighborhoodDemoClosedFootprintRequired");
                var min = new Vector3(ring.Min(p => p.x), .16f, ring.Min(p => p.y));
                var max = new Vector3(ring.Max(p => p.x), .16f + SymbolicHeight, ring.Max(p => p.y));
                result.Add(new Building { Id = id, Evidence = data, Reference = (JObject)reference.DeepClone(),
                    Slug = prior.Value<string>("slug"), EvidenceHash = reference.Value<string>("sha256"),
                    Frame = (JObject)frames[0], PreviousCase = prior, Ring = ring, Bounds = new Bounds((min + max) / 2, max - min) });
            }
            if (result.Count != 3 || result.Select(b => b.Id).Distinct().Count() != 3)
                throw new InvalidOperationException("NeighborhoodDemoThreeUniqueStudyBuildingsRequired");
            return result.ToArray();
        }

        public static int SelectRoadFacingEdge(Vector2[] ring, Vector2[][] roads)
        {
            if (ring == null || ring.Length < 4 || Vector2.Distance(ring[0], ring[^1]) > .01f)
                throw new ArgumentException("NeighborhoodDemoClosedRingRequired");
            if (roads == null || !roads.Any(r => r != null && r.Length >= 2))
                throw new ArgumentException("NeighborhoodDemoPublicRoadReferenceRequired");
            return Enumerable.Range(0, ring.Length - 1).Where(i => Vector2.Distance(ring[i], ring[i + 1]) > .05f)
                .OrderBy(i => roads.Where(r => r != null).SelectMany(r => Enumerable.Range(0, r.Length - 1)
                    .Select(j => DistanceToSegment((ring[i] + ring[i + 1]) * .5f, r[j], r[j + 1]))).Min())
                .ThenByDescending(i => (ring[i + 1] - ring[i]).sqrMagnitude).First();
        }
        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        { var delta = b - a; return Vector2.Distance(point, a + delta * (delta.sqrMagnitude < .0001f ? 0
                : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude))); }
        private static void SetFront(Building b, Vector2[][] roads)
        {
            b.FrontEdge = SelectRoadFacingEdge(b.Ring, roads);
            var edge = (b.Ring[b.FrontEdge + 1] - b.Ring[b.FrontEdge]).normalized;
            var area = Enumerable.Range(0, b.Ring.Length - 1).Sum(i => b.Ring[i].x * b.Ring[i + 1].y - b.Ring[i + 1].x * b.Ring[i].y);
            b.Normal = new Vector3(edge.y, 0, -edge.x) * (area > 0 ? 1 : -1);
            var midpoint = (b.Ring[b.FrontEdge] + b.Ring[b.FrontEdge + 1]) / 2;
            b.Face = new Vector3(midpoint.x, .16f, midpoint.y);
            b.FacadeWidth = Vector2.Distance(b.Ring[b.FrontEdge], b.Ring[b.FrontEdge + 1]) * .78f;
        }

        private static void ConfigureReviewedModules(Inputs inputs, JObject input, JObject preview, 사가정외관ModuleCatalog catalog)
        {
            if (preview.Value<string>("schema") != "private-appearance-module-previews.v1"
                || preview.Value<string>("catalogRevision") != catalog.CatalogRevision)
                throw new InvalidOperationException("NeighborhoodDemoModulePreviewVersionRequired");
            foreach (var key in ((JArray)input["reviewedModuleKeys"]).Values<string>())
            {
                var rows = ((JArray)preview["modules"]).Where(m => m.Value<string>("visualKey") == key).ToArray();
                if (rows.Length != 1 || rows[0]["pixelValidation"]?.Value<bool?>("passed") != true)
                    throw new InvalidOperationException("NeighborhoodDemoCapturedReviewedModuleRequired");
                inputs.Pin(rows[0]["image"], 8 * 1024 * 1024, ".png");
                var entry = catalog.Resolve(key);
                entry.reviewState = 사가정건물외관Profile.ReviewedState;
                entry.reviewerKind = "CodexVisualReview";
                entry.reviewReference = "private-neighborhood-demo:explicit-module-selection";
            }
        }

        private static void BuildStudyMass(Transform parent, Building b, List<Object> owned)
        {
            var map = new 공간참고자료 { revision = "private-detached-three-building-study", offsetX = 550,
                offsetZ = 8, halfExtent = 500, roads = Array.Empty<참고도로>(),
                buildings = new[] { new 참고건물 { id = b.Id, height = SymbolicHeight,
                    points = b.Ring.Select(p => new 참고점 { x = p.x, z = p.y }).ToArray() } } };
            사가정모형MeshBuilder.BuildBatches(map, null, batch =>
            { if (!batch.IsShadow && !batch.IsStationDetail) AddBatch(parent, batch, owned, true); }, false);
            for (var i = 0; i < b.Ring.Length - 1; i++)
                Strip(parent, b.Ring[i], b.Ring[i + 1], .28f, .18f, new Color(1, .72f, .15f), owned);
        }
        private static void BuildAppearance(Transform parent, Building b, JObject labels, Inputs inputs,
            사가정외관ModuleCatalog catalog, 사가정외관OverlayRenderer renderer, string studyHash,
            JArray applied, List<Object> owned)
        {
            var data = b.Evidence; var color = Hex(data["appearance"]?["facadeColor"]?.Value<string>("approximateHex"), Color.gray);
            Box(parent, b.Face + b.Normal * .045f + Vector3.up * 1.8f, new Vector3(b.FacadeWidth, 3.35f, .08f), b.Normal, color, owned);
            var sign = data["appearance"]?["signBand"];
            Box(parent, b.Face + b.Normal * .11f + Vector3.up * 3.15f, new Vector3(b.FacadeWidth * .96f, .65f, .10f),
                b.Normal, Hex(sign?.Value<string>("backgroundApproximateHex"), color), owned);
            AddSign(parent, b, labels, inputs, owned);
            if (data["appearance"]?["windows"]?.Value<bool?>("visible") == true)
                Box(parent, b.Face + b.Normal * .10f + Vector3.up * 1.35f, new Vector3(b.FacadeWidth * .77f, 1.65f, .10f),
                    b.Normal, new Color(.12f, .20f, .22f), owned);
            if (data["appearance"]?["awning"]?.Value<bool?>("visible") != true) return;
            var previousProfiles = ((JArray)b.PreviousCase["appliedModules"]).Select(v => v["profile"]).ToArray();
            if (previousProfiles.Length != 1) throw new InvalidOperationException("NeighborhoodDemoPreviousAwningProfileRequired");
            var previous = previousProfiles[0];
            var key = previous.Value<string>("visualKey");
            var module = catalog.Resolve(key);
            if (module.moduleKind != "Awning" || module.reviewState != 사가정건물외관Profile.ReviewedState)
                throw new InvalidOperationException("NeighborhoodDemoReviewedAwningRequired");
            var clock = b.Frame.Value<string>("timeBase").Split('/');
            var profile = new 사가정건물외관Profile
            {
                scopeKind = 사가정건물외관Profile.PrivateStudyScope, profileStableId = "private-demo:" + b.Slug + ":awning",
                targetBuildingId = b.Id, baseMapHashSha256 = studyHash, spatialContentHashSha256 = studyHash,
                reviewState = 사가정건물외관Profile.ReviewedState, reviewerKind = "CodexVisualReview",
                reviewReference = "private-demo:unmeasured-road-facing-edge", evidenceObservationId = b.Frame.Value<string>("sampleId"),
                evidenceSourceSha256 = b.Frame.Value<string>("sourceSha256"), evidenceSourcePts = b.Frame.Value<long>("actualPts"),
                evidenceTimeBaseNumerator = long.Parse(clock[0]), evidenceTimeBaseDenominator = long.Parse(clock[1]),
                visualKey = key, visualCatalogRevision = catalog.CatalogRevision, moduleKind = "Awning",
                localPosition = b.Face + b.Normal * .2f + Vector3.up * 2.6f,
                localEulerAngles = Quaternion.LookRotation(b.Normal).eulerAngles,
                localScale = Vector3.one * Mathf.Min(1f, b.FacadeWidth / Mathf.Max(.1f, PrefabWidth(catalog.ResolvePrefab(key)))),
            };
            if (!renderer.TryApply(profile, catalog, out var status)) throw new InvalidOperationException(status);
            var awningColor = Hex(data["appearance"]?["awning"]?.Value<string>("approximateHex"), Color.gray);
            var appliedRoot = renderer.VisualRoot.Cast<Transform>().Last();
            foreach (var mesh in appliedRoot.GetComponentsInChildren<MeshRenderer>())
            { var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", awningColor); block.SetColor("_Color", awningColor); mesh.SetPropertyBlock(block); }
            applied.Add(new JObject { ["targetBuildingId"] = b.Id, ["visualKey"] = key, ["status"] = status,
                ["scopeKind"] = "PrivateBuildingStudy", ["unmeasuredPlacement"] = true });
        }

        private static void AddSign(Transform parent, Building b, JObject labels, Inputs inputs, List<Object> owned)
        {
            if (labels.Value<string>("schema") != "private-observed-sign-label-images.v1")
                throw new InvalidOperationException("NeighborhoodDemoStaticSignManifestRequired");
            var rows = ((JArray)labels["labels"]).Where(r => r.Value<string>("slug") == b.Slug
                && r.Value<string>("evidenceSha256") == b.EvidenceHash
                && r.Value<string>("text") == b.Evidence["appearance"]?["signBand"]?.Value<string>("text")).ToArray();
            if (rows.Length != 1) throw new InvalidOperationException("NeighborhoodDemoObservedSignTextBindingRequired");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            owned.Add(texture);
            if (!texture.LoadImage(inputs.Pin(rows[0]["image"], 2 * 1024 * 1024, ".png"), false)
                || texture.width > 4096 || texture.height > 1024) throw new InvalidOperationException("NeighborhoodDemoStaticSignImageRejected");
            var material = Material(Color.white, owned); material.renderQueue = 3000;
            material.SetTexture("_BaseMap", texture); material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
            var h = Mathf.Min(.5f, b.FacadeWidth * .86f * texture.height / texture.width); var w = h * texture.width / texture.height;
            var mesh = new Mesh { hideFlags = HideFlags.DontSave,
                vertices = new[] { new Vector3(-w/2,-h/2,0), new Vector3(w/2,-h/2,0), new Vector3(w/2,h/2,0), new Vector3(-w/2,h/2,0) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            mesh.RecalculateBounds(); owned.Add(mesh);
            var go = Child(parent, "직접 판독 상호 · 정적 PNG 시안");
            go.localPosition = b.Face + b.Normal * .195f + Vector3.up * 3.15f;
            go.localRotation = Quaternion.LookRotation(-b.Normal, Vector3.up);
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void DrawRoadCandidates(Transform parent, Vector2[][] lines, List<Object> owned)
        {
            foreach (var line in lines) for (var i = 0; i < line.Length - 1; i++)
            {
                var length = Vector2.Distance(line[i], line[i + 1]);
                for (var start = 0f; start < length; start += 7f)
                {
                    var a = Vector2.Lerp(line[i], line[i + 1], start / length);
                    var b = Vector2.Lerp(line[i], line[i + 1], Mathf.Min(start + 4.5f, length) / length);
                    Strip(parent, a, b, 2.5f, .30f, Color.white, owned);
                    Strip(parent, a, b, 1.25f, .32f, new Color(.85f, .06f, .08f), owned);
                }
            }
        }
        private static void Strip(Transform parent, Vector2 a, Vector2 b, float width, float y, Color color, List<Object> owned)
        {
            if (Vector2.Distance(a, b) < .01f) return;
            var center = (a + b) / 2;
            Box(parent, new Vector3(center.x, y, center.y), new Vector3(width, .025f, Vector2.Distance(a, b)),
                new Vector3(b.x - a.x, 0, b.y - a.y).normalized, color, owned);
        }
        public static int[] FilterStudyBuildingTriangles(Vector3[] vertices, int[] triangles)
        {
            if (vertices == null || triangles == null || triangles.Length % 3 != 0
                || triangles.Any(i => i < 0 || i >= vertices.Length))
                throw new ArgumentException("NeighborhoodDemoStudyTriangleStreamRejected");
            var result = new List<int>();
            for (var i = 0; i < triangles.Length; i += 3)
                if (Enumerable.Range(0, 3).All(j => vertices[triangles[i + j]].y
                    >= 사가정모형MeshBuilder.BuildingBaseElevation - .001f))
                    result.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
            return result.ToArray();
        }
        private static void AddBatch(Transform parent, 사가정모형MeshBuilder.MeshBatch batch, List<Object> owned,
            bool studyBuildingsOnly = false)
        {
            if (batch.Vertices.Count == 0) return;
            // 연구용 단일 건물 BuildBatches의 중립 바탕판은 기존 동네 위에 중복 추가하지 않는다.
            var triangles = studyBuildingsOnly ? FilterStudyBuildingTriangles(batch.Vertices.ToArray(), batch.Triangles.ToArray())
                : batch.Triangles.ToArray();
            if (triangles.Length == 0) return;
            var go = Child(parent, batch.Name);
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(batch.Vertices); mesh.SetTriangles(triangles, 0); mesh.SetColors(batch.Colors);
            mesh.SetUVs(1, batch.SurfaceData); mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Resources.Load<Shader>(batch.IsShadow ? "사가정모형그림자" : "사가정모형명암");
            if (shader == null) throw new InvalidOperationException("NeighborhoodDemoMassingShaderRequired");
            var material = new Material(shader) { color = Color.white, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_VertexColorWeight")) material.SetFloat("_VertexColorWeight", 1);
            owned.Add(material); go.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static void Box(Transform parent, Vector3 position, Vector3 size, Vector3 normal, Color color, List<Object> owned)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.hideFlags = HideFlags.DontSave; go.layer = ReviewLayer;
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(normal); go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<MeshRenderer>().sharedMaterial = Material(color, owned);
        }
        private static Material Material(Color color, List<Object> owned)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("NeighborhoodDemoUnlitShaderRequired");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", color); owned.Add(material); return material;
        }
        private static Transform Child(Transform parent, string name)
        { var go = new GameObject(name) { layer = ReviewLayer, hideFlags = HideFlags.DontSave }; go.transform.SetParent(parent, false); return go.transform; }

        private static Camera CameraFor(Transform parent, Bounds bounds, Vector3 direction, float margin)
        {
            var go = Child(parent, "비공개 통합 시안 Camera"); var camera = go.gameObject.AddComponent<Camera>();
            camera.enabled = false; camera.scene = parent.gameObject.scene; camera.cullingMask = 1 << ReviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.86f, .88f, .89f);
            camera.orthographic = true; camera.aspect = (float)Width / Height;
            camera.orthographicSize = Mathf.Max(12f, bounds.extents.magnitude * margin);
            camera.nearClipPlane = .1f; camera.farClipPlane = 4000f;
            go.position = bounds.center + direction.normalized * 1600f; go.rotation = Quaternion.LookRotation(bounds.center - go.position);
            go.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            return camera;
        }
        private static JObject Capture(Camera camera, string path)
        {
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32(); var bg = (Color32)camera.backgroundColor;
                double sum = 0, square = 0; var foreground = 0;
                foreach (var p in pixels)
                { var value = (p.r + p.g + p.b) / 3d; sum += value; square += value * value;
                    if (Math.Abs(p.r - bg.r) + Math.Abs(p.g - bg.g) + Math.Abs(p.b - bg.b) > 18) foreground++; }
                var variance = Math.Max(0, square / pixels.Length - Math.Pow(sum / pixels.Length, 2));
                if (foreground < 1000 || variance < 4) throw new InvalidOperationException("NeighborhoodDemoBlankCaptureRejected");
                if (File.Exists(path)) throw new IOException("NeighborhoodDemoImageAlreadyExists");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return new JObject { ["passed"] = true, ["foregroundPixelCount"] = foreground, ["brightnessVariance"] = variance,
                    ["cameraBoundToPreviewScene"] = camera.scene.IsValid(), ["image"] = Reference(path) };
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.DestroyImmediate(texture); Object.DestroyImmediate(target); }
        }

        private static JObject BuildingReport(Building b) => new()
        {
            ["slug"] = b.Slug, ["targetBuildingId"] = b.Id, ["gisBuildingId"] = b.Evidence["buildingCandidate"]["gisBuildingId"].DeepClone(),
            ["geometryHash"] = b.Evidence["buildingCandidate"]["geometryHash"].DeepClone(), ["evidence"] = b.Reference,
            ["sourceSha256"] = b.Frame["sourceSha256"].DeepClone(), ["sampleId"] = b.Frame["sampleId"].DeepClone(),
            ["actualPts"] = b.Frame["actualPts"].DeepClone(), ["timeBase"] = b.Frame["timeBase"].DeepClone(), ["frame"] = b.Frame["frame"].DeepClone(),
            ["scope"] = "DetachedUnappliedStudyBuilding", ["canonicalApplied"] = false,
            ["heightPolicy"] = "SymbolicFallback4m", ["actualHeightKnown"] = false, ["symbolicHeightMeters"] = SymbolicHeight,
            ["frontPlacementHypothesis"] = "NearestPublicRoadToFootprintEdgeMidpoint", ["frontEdgeIndex"] = b.FrontEdge,
            ["frontFacingConfirmed"] = false, ["videoCameraPoseRecovered"] = false, ["originalPhotoUsedAsTexture"] = false,
        };
        private static float PrefabWidth(GameObject prefab)
        {
            var points = new List<Vector3>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix; var b = filter.sharedMesh.bounds;
                for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                    points.Add(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            }
            if (points.Count == 0) throw new InvalidOperationException("NeighborhoodDemoAwningMeshRequired");
            return points.Max(p => p.x) - points.Min(p => p.x);
        }
        public static Vector2 ProjectWgs84(double latitude, double longitude)
        {
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
                throw new ArgumentException("NeighborhoodDemoWgs84CoordinateRejected");
            double[] Ecef(double lat, double lon)
            { var l = lat * Math.PI / 180; var o = lon * Math.PI / 180; const double a = 6378137, f = 1 / 298.257223563;
                var e = f * (2 - f); var n = a / Math.Sqrt(1 - e * Math.Sin(l) * Math.Sin(l));
                return new[] { n * Math.Cos(l) * Math.Cos(o), n * Math.Cos(l) * Math.Sin(o), n * (1 - e) * Math.Sin(l) }; }
            var origin = Ecef(사가정공간표현OverlayComposer.OriginLatitude, 사가정공간표현OverlayComposer.OriginLongitude);
            var at = Ecef(latitude, longitude); var d = at.Zip(origin, (x, y) => x - y).ToArray();
            var l0 = 사가정공간표현OverlayComposer.OriginLatitude * Math.PI / 180; var o0 = 사가정공간표현OverlayComposer.OriginLongitude * Math.PI / 180;
            return new Vector2((float)(550 - Math.Sin(o0) * d[0] + Math.Cos(o0) * d[1]),
                (float)(8 - Math.Sin(l0) * Math.Cos(o0) * d[0] - Math.Sin(l0) * Math.Sin(o0) * d[1] + Math.Cos(l0) * d[2]));
        }
        private static JToken ReadPointer(JToken root, string pointer)
        {
            if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidOperationException("NeighborhoodDemoExactGeometryPointerRequired");
            foreach (var raw in pointer.Split('/').Skip(1))
            { var key = raw.Replace("~1", "/").Replace("~0", "~"); root = root is JObject obj ? obj[key]
                    : root is JArray array && int.TryParse(key, out var index) && index >= 0 && index < array.Count ? array[index] : null;
                if (root == null) throw new InvalidOperationException("NeighborhoodDemoGeometryPointerRejected"); }
            return root;
        }
        private static bool SameReference(JToken a, JToken b) => a != null && b != null
            && a.Value<string>("sha256") == b.Value<string>("sha256")
            && string.Equals(Path.GetFullPath(a.Value<string>("path")), Path.GetFullPath(b.Value<string>("path")), StringComparison.OrdinalIgnoreCase);
        private static Color Hex(string value, Color fallback) => ColorUtility.TryParseHtmlString(value, out var color) ? color : fallback;
        private static bool Sha(string value) => value?.Length == 64 && value.All(c => Uri.IsHexDigit(c));
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static JObject Reference(string path) => new() { ["path"] = path, ["sha256"] = Hash(File.ReadAllBytes(path)) };
        private static JArray Vector(Vector3 value) => new(value.x, value.y, value.z);
        private static void WriteNew(string path, JObject data)
        { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write); using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.Write(data.ToString(Formatting.Indented)); }
    }
}
