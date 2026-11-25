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
    /// 실제 공개 footprint와 비공개 영상 관측을 별도 연구 장면에서 비교한다.
    /// canonical View 적용·Scene 저장·Game View·실측 복원 또는 공개 승인 증거가 아니다.
    /// </summary>
    public static class 사가정외관연구Capture
    {
        private const int Layer = 31;
        private const float SymbolicHeight = 4f;

        public static void RunModulePreviews()
        {
            RequireGraphics();
            var output = OutputDirectory();
            var catalog = 사가정외관비공개검토준비.CreateTransientCandidates();
            var rows = new JArray();
            try
            {
                foreach (var entry in catalog.Entries)
                {
                    var scene = EditorSceneManager.NewPreviewScene();
                    var owned = new List<Object>();
                    try
                    {
                        var root = Root(scene);
                        CopyCandidateMeshes(catalog.ResolvePrefab(entry.visualKey).transform, root.transform);
                        var bounds = RendererBounds(root);
                        var camera = CameraFor(root, bounds.center, new Vector3(1, .65f, -1),
                            Mathf.Max(2, bounds.extents.magnitude * 1.25f));
                        var file = Path.Combine(output, entry.visualKey + ".png");
                        var pixelValidation = Capture(camera, file);
                        rows.Add(new JObject { ["visualKey"] = entry.visualKey, ["moduleKind"] = entry.moduleKind,
                            ["reviewState"] = "Candidate", ["image"] = Reference(file),
                            ["boundsSize"] = Vector(bounds.size), ["pixelValidation"] = pixelValidation,
                            ["candidatePreviewNotApproval"] = true });
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); foreach (var o in owned) if (o != null) Object.DestroyImmediate(o); }
                }
                WriteNew(Path.Combine(output, "module-previews.private.json"), new JObject
                {
                    ["schema"] = "private-appearance-module-previews.v1", ["catalogRevision"] = catalog.CatalogRevision,
                    ["modules"] = rows, ["moduleReviewPerformed"] = false, ["gameViewVerified"] = false,
                    ["offscreenCameraRendering"] = true, ["sourceAssetsModified"] = false,
                });
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        public static void RunStudy()
        {
            RequireGraphics();
            var indexPath = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_EVIDENCE_INDEX");
            var expectedIndexHash = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_EVIDENCE_INDEX_SHA256");
            if (string.IsNullOrWhiteSpace(indexPath) || !File.Exists(indexPath)
                || !string.Equals(Hash(File.ReadAllBytes(indexPath)), expectedIndexHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PrivateStudyEvidenceIndexHashRequired");
            var index = JObject.Parse(File.ReadAllText(indexPath));
            if (index.Value<string>("schema") != "private-diorama-facade-evidence-index.v1")
                throw new InvalidOperationException("PrivateStudyEvidenceSchemaRejected");
            var selectedKeys = (Environment.GetEnvironmentVariable("SSALDDEL_FACADE_REVIEWED_MODULE_KEYS") ?? "")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            if (selectedKeys.Count == 0) throw new InvalidOperationException("PrivateStudyActualModuleReviewRequired");
            var output = OutputDirectory();
            var catalog = 사가정외관비공개검토준비.CreateTransientCandidates();
            var rows = new JArray();
            try
            {
                if (selectedKeys.Any(k => !catalog.Entries.Any(e => e.visualKey == k)))
                    throw new InvalidOperationException("PrivateStudyReviewedModuleKeyUnknown");
                var moduleReview = VerifyReviewedModulePreview(catalog, selectedKeys);
                var signImages = LoadSignImageManifest();
                foreach (var entry in catalog.Entries.Where(e => selectedKeys.Contains(e.visualKey)))
                {
                    entry.reviewState = 사가정건물외관Profile.ReviewedState;
                    entry.reviewerKind = "CodexVisualReview";
                    entry.reviewReference = "module-preview-review:" + moduleReview["manifest"].Value<string>("sha256")[..16];
                }
                foreach (var input in (JArray)index["candidateEvidence"])
                {
                    VerifyReference(input);
                    var file = input.Value<string>("path");
                    var data = JObject.Parse(File.ReadAllText(file));
                    var slug = Path.GetFileName(file).Replace(".private.json", "");
                    rows.Add(RenderOne(data, slug, expectedIndexHash, input.Value<string>("sha256"), catalog, output, signImages));
                }
                WriteNew(Path.Combine(output, "study-result.private.json"), new JObject
                {
                    ["schema"] = "private-building-facade-study-render.v1", ["scopeKind"] = "PrivateBuildingStudy",
                    ["evidenceIndex"] = Reference(indexPath), ["cases"] = rows,
                    ["modulePreviewReview"] = moduleReview,
                    ["generatedSignImageManifest"] = signImages?["reference"]?.DeepClone(),
                    ["canonicalDioramaApplied"] = false, ["gameViewVerified"] = false,
                    ["offscreenCameraRendering"] = true, ["sceneSaved"] = false, ["sourceAssetsModified"] = false,
                    ["userMapApprovalCreated"] = false, ["publicationReady"] = false,
                });
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        private static JObject RenderOne(JObject data, string slug, string indexHash, string evidenceHash,
            사가정외관ModuleCatalog catalog, string output, JObject signImages)
        {
            if (data.Value<string>("schema") != "private-diorama-facade-observation-evidence.v1"
                || data["buildingCandidate"]?.Value<string>("bindingStatus") != "reviewed_exact_building_id")
                throw new InvalidOperationException("PrivateStudyBuildingIdentityReviewRequired");
            var building = (JObject)data["buildingCandidate"];
            VerifyReference(building["geometryReference"]);
            var geometry = building["geometry"];
            var referenceData = JObject.Parse(File.ReadAllText(building["geometryReference"].Value<string>("path")));
            var pointer = building["geometryReference"].Value<string>("jsonPointer");
            var candidates = ReadPointer(referenceData, pointer) as JArray;
            var matches = candidates?.Where(c => c.Value<string>("building_id") == building.Value<string>("placeId")
                && c.Value<string>("gisBuildingId") == building.Value<string>("gisBuildingId")).ToArray();
            if (matches == null || matches.Length != 1 || !JToken.DeepEquals(matches[0]["geometry"], geometry))
                throw new InvalidOperationException("PrivateStudyExactPublicGeometryBindingRequired");
            if (geometry?.Value<string>("type") != "Polygon" || ((JArray)geometry["coordinates"]).Count != 1)
                throw new InvalidOperationException("PrivateStudySimplePublicPolygonRequired");
            var points = ((JArray)geometry["coordinates"][0]).Select(p => GeographicPoint((double)p[1], (double)p[0])).ToArray();
            if (points.Length < 4 || Vector2.Distance(points[0], points[^1]) > .01f)
                throw new InvalidOperationException("PrivateStudyClosedGeometryRequired");
            var frame = data["frames"]?.FirstOrDefault(f => f.Value<bool?>("directlyViewed") == true
                && f.Value<bool?>("protectedOverlap") == false);
            if (frame == null) throw new InvalidOperationException("PrivateStudyActualFrameRequired");
            VerifyReference(frame["frame"]);
            var targetId = building.Value<string>("placeId");
            var geometryHash = building.Value<string>("geometryHash");
            if (string.IsNullOrEmpty(geometryHash) || geometryHash.Length != 64)
                throw new InvalidOperationException("PrivateStudyGeometryHashRequired");
            var studyHash = Hash(Encoding.UTF8.GetBytes(indexHash + "|" + evidenceHash + "|" + targetId
                + "|" + geometryHash + "|SymbolicFallback4m|WGS84-ECEF-ENU-at-zero-altitude"));
            var minimum = new Vector3(points.Min(p => p.x), .16f, points.Min(p => p.y));
            var maximum = new Vector3(points.Max(p => p.x), .16f + SymbolicHeight, points.Max(p => p.y));
            var bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum);
            var edgeIndex = Enumerable.Range(0, points.Length - 1)
                .OrderByDescending(i => (points[i + 1] - points[i]).sqrMagnitude).First();
            var edge = (points[edgeIndex + 1] - points[edgeIndex]).normalized;
            var area = Enumerable.Range(0, points.Length - 1)
                .Sum(i => points[i].x * points[i + 1].y - points[i + 1].x * points[i].y);
            var normal = new Vector3(edge.y, 0, -edge.x) * (area > 0 ? 1 : -1);
            var midpoint2 = (points[edgeIndex] + points[edgeIndex + 1]) * .5f;
            var face = new Vector3(midpoint2.x, .16f, midpoint2.y);
            var width = Vector2.Distance(points[edgeIndex], points[edgeIndex + 1]) * .78f;
            var scene = EditorSceneManager.NewPreviewScene();
            var owned = new List<Object>();
            사가정외관OverlayRenderer overlay = null;
            try
            {
                var root = Root(scene);
                BuildMass(root.transform, targetId, points, owned);
                var camera = CameraFor(root, bounds.center, normal + Vector3.up * .7f - new Vector3(edge.x, 0, edge.y) * .22f,
                    Mathf.Max(8, bounds.extents.magnitude * 1.15f));
                var detail = new GameObject("관측 외관 연구 · 실측 아님") { layer = Layer, hideFlags = HideFlags.DontSave };
                detail.transform.SetParent(root.transform, false);
                var color = Hex(data["appearance"]?["facadeColor"]?.Value<string>("approximateHex"), Color.gray);
                Box(detail.transform, face + normal * .045f + Vector3.up * 1.8f,
                    new Vector3(width, 3.35f, .08f), normal, color, owned);
                var sign = data["appearance"]?["signBand"];
                Box(detail.transform, face + normal * .11f + Vector3.up * 3.15f,
                    new Vector3(width * .96f, .65f, .10f), normal,
                    Hex(sign?.Value<string>("backgroundApproximateHex"), color), owned);
                var signImageReference = AddGeneratedSignImage(detail.transform,
                    face + normal * .195f + Vector3.up * 3.15f, normal, width * .86f,
                    slug, evidenceHash, sign?.Value<string>("text"), signImages, owned);
                if (data["appearance"]?["windows"]?.Value<bool?>("visible") == true)
                    Box(detail.transform, face + normal * .10f + Vector3.up * 1.35f,
                        new Vector3(width * .77f, 1.65f, .10f), normal, new Color(.12f, .20f, .22f), owned);
                detail.SetActive(false);
                var before = Path.Combine(output, slug + "-before.png");
                var after = Path.Combine(output, slug + "-after.png");
                var restored = Path.Combine(output, slug + "-restored.png");
                var beforePixels = Capture(camera, before);
                var gate = 사가정외관검토Gate.CreatePrivateStudy(studyHash,
                    new Dictionary<string, Bounds> { [targetId] = bounds });
                overlay = new 사가정외관OverlayRenderer(root.transform, gate, Layer);
                var appliedModules = new JArray();
                var awningVisible = data["appearance"]?["awning"]?.Value<bool?>("visible") == true;
                if (awningVisible)
                {
                    var module = catalog.Entries.FirstOrDefault(e => e.moduleKind == "Awning"
                        && e.reviewState == 사가정건물외관Profile.ReviewedState);
                    if (module == null) throw new InvalidOperationException("PrivateStudyReviewedAwningRequired");
                    var sourceBounds = PrefabLocalBounds(catalog.ResolvePrefab(module.visualKey));
                    var scale = Mathf.Min(1f, width / Mathf.Max(.1f, sourceBounds.size.x));
                    var clock = frame.Value<string>("timeBase").Split('/');
                    var profile = new 사가정건물외관Profile
                    {
                        scopeKind = 사가정건물외관Profile.PrivateStudyScope,
                        profileStableId = "private-study:" + slug + ":awning", targetBuildingId = targetId,
                        baseMapHashSha256 = studyHash, spatialContentHashSha256 = studyHash,
                        reviewState = 사가정건물외관Profile.ReviewedState, reviewerKind = "CodexVisualReview",
                        reviewReference = "private-study:" + slug + ":unmeasured-layout",
                        evidenceObservationId = frame.Value<string>("sampleId"),
                        evidenceSourceSha256 = data["source"].Value<string>("sourceSha256"),
                        evidenceSourcePts = frame.Value<long>("actualPts"),
                        evidenceTimeBaseNumerator = long.Parse(clock[0]), evidenceTimeBaseDenominator = long.Parse(clock[1]),
                        visualKey = module.visualKey, visualCatalogRevision = catalog.CatalogRevision, moduleKind = "Awning",
                        localPosition = face + normal * .2f + Vector3.up * 2.6f,
                        localEulerAngles = Quaternion.LookRotation(normal).eulerAngles, localScale = Vector3.one * scale,
                    };
                    if (!overlay.TryApply(profile, catalog, out var status)) throw new InvalidOperationException(status);
                    // 연구용 복사본의 표시색만 관측 근사색으로 바꾸며 원본 Material은 변경하지 않는다.
                    var awningColor = Hex(data["appearance"]?["awning"]?.Value<string>("approximateHex"), Color.gray);
                    foreach (var renderer in overlay.VisualRoot.GetComponentsInChildren<MeshRenderer>())
                    {
                        var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", awningColor); block.SetColor("_Color", awningColor);
                        renderer.SetPropertyBlock(block);
                    }
                    appliedModules.Add(new JObject { ["profile"] = JObject.Parse(JsonUtility.ToJson(profile)), ["status"] = status });
                }
                detail.SetActive(true);
                var afterPixels = Capture(camera, after);
                var removed = overlay.RemoveBuilding(targetId);
                detail.SetActive(false);
                var restoredPixels = Capture(camera, restored);
                return new JObject
                {
                    ["slug"] = slug, ["scopeKind"] = "PrivateBuildingStudy", ["studySpatialHashSha256"] = studyHash,
                    ["targetBuildingId"] = targetId, ["geometryHash"] = geometryHash,
                    ["geometryReference"] = building["geometryReference"].DeepClone(), ["evidenceSha256"] = evidenceHash,
                    ["exactPublicGeometryDeepEquals"] = true,
                    ["sourceSha256"] = frame.Value<string>("sourceSha256"), ["actualPts"] = frame.Value<long>("actualPts"),
                    ["timeBase"] = frame.Value<string>("timeBase"), ["observedFrame"] = frame["frame"].DeepClone(),
                    ["heightPolicy"] = "SymbolicFallback4m", ["actualHeightKnown"] = false,
                    ["frontEdgeIndex"] = edgeIndex, ["frontFacingConfirmed"] = false,
                    ["layoutBasis"] = "LongestFootprintEdgeUnmeasuredStudyHypothesis",
                    ["windowCountReconstructed"] = false, ["signTextReconstructed"] = signImageReference != null,
                    ["signTextSource"] = "DirectlyReviewedAppearanceSignBandText",
                    ["generatedSignLabelImage"] = signImageReference,
                    ["originalPhotoUsedAsTexture"] = false,
                    ["signFontAndPlacementAreUnmeasuredStudyStyle"] = true,
                    ["observedSignTextReferenceOnly"] = sign?.Value<string>("text"),
                    ["appliedModules"] = appliedModules, ["removedModuleCount"] = removed,
                    ["afterRemovalOverlayCount"] = overlay.Count, ["before"] = Reference(before), ["after"] = Reference(after),
                    ["restored"] = Reference(restored), ["restoredPixelsEqualBefore"] = Hash(File.ReadAllBytes(before)) == Hash(File.ReadAllBytes(restored)),
                    ["pixelValidation"] = new JObject { ["before"] = beforePixels,
                        ["after"] = afterPixels, ["restored"] = restoredPixels },
                    ["canonicalDioramaApplied"] = false, ["gameViewVerified"] = false, ["publicationReady"] = false,
                };
            }
            finally
            {
                overlay?.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
                foreach (var o in owned) if (o != null) Object.DestroyImmediate(o);
            }
        }

        private static void BuildMass(Transform parent, string id, Vector2[] points, List<Object> owned)
        {
            var map = new 공간참고자료 { revision = "private-building-study", offsetX = 550, offsetZ = 8, halfExtent = 500,
                roads = Array.Empty<참고도로>(), buildings = new[] { new 참고건물 { id = id, height = SymbolicHeight,
                    points = points.Select(p => new 참고점 { x = p.x, z = p.y }).ToArray() } } };
            사가정모형MeshBuilder.BuildBatches(map, null, batch =>
            {
                if (batch.IsShadow || batch.IsStationDetail) return;
                var go = new GameObject("공개 원형 · 높이 미확인 4m") { layer = Layer };
                go.transform.SetParent(parent, false);
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(batch.Vertices); mesh.SetTriangles(batch.Triangles, 0);
                mesh.SetColors(batch.Colors); mesh.RecalculateNormals();
                owned.Add(mesh); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var shader = Resources.Load<Shader>("사가정모형명암");
                if (shader == null) throw new InvalidOperationException("PrivateStudyMassingShaderMissing");
                var material = new Material(shader) { color = Color.white, hideFlags = HideFlags.DontSave };
                material.SetFloat("_VertexColorWeight", 1f);
                owned.Add(material); go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }, false);
        }

        private static GameObject Root(Scene scene)
        {
            var root = new GameObject("Private Building Study · 정본 아님") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static Camera CameraFor(GameObject root, Vector3 center, Vector3 direction, float size)
        {
            var go = new GameObject("Private Study Camera"); go.transform.SetParent(root.transform, false);
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << Layer;
            camera.scene = root.scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.86f, .88f, .89f);
            camera.orthographic = true; camera.orthographicSize = size; camera.aspect = 1.5f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.transform.position = center + direction.normalized * 100f;
            camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position);
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var lightObject = new GameObject("Private Study Light"); lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1f;
            light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            return camera;
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size, Vector3 normal,
            Color color, List<Object> owned)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "관측 색·띠의 비실측 배치";
            go.layer = Layer; go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(normal); go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = Material(color, owned);
        }

        private static JObject LoadSignImageManifest()
        {
            var path = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_SIGN_IMAGE_MANIFEST");
            if (string.IsNullOrWhiteSpace(path)) return null;
            var expected = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_SIGN_IMAGE_SHA256");
            if (!File.Exists(path) || !string.Equals(Hash(File.ReadAllBytes(path)), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PrivateStudySignImageManifestHashRequired");
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (manifest.Value<string>("schema") != "private-observed-sign-label-images.v1"
                || manifest["labels"] is not JArray)
                throw new InvalidOperationException("PrivateStudyGeneratedSignImageSchemaRequired");
            return new JObject { ["reference"] = Reference(path), ["data"] = manifest };
        }

        private static JObject AddGeneratedSignImage(Transform parent, Vector3 position, Vector3 normal,
            float width, string slug, string evidenceHash, string observedText, JObject signImages, List<Object> owned)
        {
            if (signImages == null) return null;
            var entries = ((JArray)signImages["data"]["labels"]).Where(e => e.Value<string>("slug") == slug).ToArray();
            if (entries.Length != 1 || entries[0].Value<string>("text") != observedText
                || entries[0].Value<string>("evidenceSha256") != evidenceHash)
                throw new InvalidOperationException("PrivateStudyObservedSignLabelBindingRequired");
            var reference = entries[0]["image"];
            VerifyReference(reference);
            var bytes = File.ReadAllBytes(reference.Value<string>("path"));
            if (bytes.Length > 2 * 1024 * 1024) throw new InvalidOperationException("PrivateStudySignImageTooLarge");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            owned.Add(texture);
            if (!texture.LoadImage(bytes, false) || texture.width > 4096 || texture.height > 1024)
                throw new InvalidOperationException("PrivateStudySignImageRejected");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("PrivateStudySignShaderMissing");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, renderQueue = 3000 };
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); owned.Add(material);
            var height = Mathf.Min(.5f, width * texture.height / texture.width);
            width = height * texture.width / texture.height;
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-width / 2, -height / 2, 0), new Vector3(width / 2, -height / 2, 0),
                new Vector3(width / 2, height / 2, 0), new Vector3(-width / 2, height / 2, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateBounds(); owned.Add(mesh);
            var go = new GameObject("직접 판독 상호의 정적 글자 이미지 · 시안") { layer = Layer, hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            // 로컬 -Z 면을 카메라 쪽으로 둬 글자가 좌우 반전되지 않게 한다.
            go.transform.localRotation = Quaternion.LookRotation(-normal, Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return (JObject)reference.DeepClone();
        }

        private static Material Material(Color color, List<Object> owned)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("PrivateStudyShaderMissing");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            owned.Add(material); return material;
        }

        private static void CopyCandidateMeshes(Transform source, Transform parent)
        {
            var go = new GameObject("후보 부품 정적 표시") { layer = Layer }; go.transform.SetParent(parent, false);
            go.transform.localPosition = source.localPosition; go.transform.localRotation = source.localRotation; go.transform.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && renderer.enabled)
            { go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; go.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials; }
            foreach (Transform child in source) CopyCandidateMeshes(child, go.transform);
        }

        private static Bounds PrefabLocalBounds(GameObject prefab)
        {
            var result = new Bounds(); var first = true;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                { var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p); }
            }
            if (first) throw new InvalidOperationException("PrivateStudyModuleMeshMissing"); return result;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var all = root.GetComponentsInChildren<Renderer>(); if (all.Length == 0) throw new InvalidOperationException("PrivateStudyRendererMissing");
            var bounds = all[0].bounds; foreach (var renderer in all.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }

        private static JObject Capture(Camera camera, string file)
        {
            if (File.Exists(file)) throw new IOException("PrivateStudyOutputAlreadyExists");
            var target = new RenderTexture(960, 640, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(960, 640, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32(); var background = pixels[0];
                double sum = 0, square = 0; var foreground = 0;
                foreach (var p in pixels)
                {
                    var brightness = (p.r + p.g + p.b) / 3d; sum += brightness; square += brightness * brightness;
                    if (Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) > 18)
                        foreground++;
                }
                var variance = Math.Max(0, square / pixels.Length - Math.Pow(sum / pixels.Length, 2));
                var passed = foreground >= 64 && variance >= 4d;
                File.WriteAllBytes(file, texture.EncodeToPNG());
                var validation = new JObject
                {
                    ["passed"] = passed, ["foregroundPixelCount"] = foreground, ["brightnessVariance"] = variance,
                    ["pixelCount"] = pixels.Length, ["cameraBoundToPreviewScene"] = camera.scene.IsValid(),
                    ["image"] = Reference(file), ["gameViewVerified"] = false,
                };
                WriteNew(file + ".validation.private.json", validation);
                if (!passed) throw new InvalidOperationException("PrivateStudyBlankOrUniformCaptureRejected");
                return validation;
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; Object.DestroyImmediate(texture); Object.DestroyImmediate(target); }
        }

        private static Vector2 GeographicPoint(double latitude, double longitude)
        {
            // 기존 사가정음식생활공간Binding의 ECEF→ENU 변환식과 동일한 기준·단위다.
            double[] Ecef(double lat, double lon)
            { var l = lat * Math.PI / 180; var o = lon * Math.PI / 180; const double a = 6378137, f = 1 / 298.257223563;
                var e = f * (2 - f); var n = a / Math.Sqrt(1 - e * Math.Sin(l) * Math.Sin(l));
                return new[] { n * Math.Cos(l) * Math.Cos(o), n * Math.Cos(l) * Math.Sin(o), n * (1 - e) * Math.Sin(l) }; }
            var origin = Ecef(사가정공간표현OverlayComposer.OriginLatitude, 사가정공간표현OverlayComposer.OriginLongitude);
            var at = Ecef(latitude, longitude); var d = at.Zip(origin, (x, y) => x - y).ToArray();
            var l0 = 사가정공간표현OverlayComposer.OriginLatitude * Math.PI / 180;
            var o0 = 사가정공간표현OverlayComposer.OriginLongitude * Math.PI / 180;
            return new Vector2((float)(550 - Math.Sin(o0) * d[0] + Math.Cos(o0) * d[1]),
                (float)(8 - Math.Sin(l0) * Math.Cos(o0) * d[0] - Math.Sin(l0) * Math.Sin(o0) * d[1] + Math.Cos(l0) * d[2]));
        }

        private static string OutputDirectory()
        { var path = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_STUDY_OUTPUT");
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new InvalidOperationException("PrivateStudyOutputDirectoryRequired");
            Directory.CreateDirectory(path); return path; }

        private static JObject VerifyReviewedModulePreview(사가정외관ModuleCatalog catalog,
            HashSet<string> selectedKeys)
        {
            var path = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_MODULE_PREVIEW_MANIFEST");
            var expected = Environment.GetEnvironmentVariable("SSALDDEL_FACADE_MODULE_PREVIEW_SHA256");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)
                || !string.Equals(Hash(File.ReadAllBytes(path)), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PrivateStudyModulePreviewHashRequired");
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (manifest.Value<string>("schema") != "private-appearance-module-previews.v1"
                || manifest.Value<string>("catalogRevision") != catalog.CatalogRevision)
                throw new InvalidOperationException("PrivateStudyModulePreviewVersionMismatch");
            var reviews = new JArray();
            foreach (var key in selectedKeys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var rows = ((JArray)manifest["modules"]).Where(m => m.Value<string>("visualKey") == key).ToArray();
                if (rows.Length != 1 || rows[0]["pixelValidation"]?.Value<bool?>("passed") != true)
                    throw new InvalidOperationException("PrivateStudyModulePreviewNotVisuallyCaptured");
                VerifyReference(rows[0]["image"]);
                var entry = catalog.Resolve(key);
                reviews.Add(new JObject
                {
                    ["visualKey"] = key, ["image"] = rows[0]["image"].DeepClone(),
                    ["reviewerKind"] = "CodexVisualReview", ["selectionWasExplicit"] = true,
                    ["currentSourceAssetFingerprintSha256"] = entry.sourceFingerprintSha256,
                    ["previewSourceFingerprintWasRecordedAtCapture"] = false,
                });
            }
            return new JObject
            {
                ["manifest"] = Reference(path), ["selected"] = reviews,
                ["currentSourceCatalogRevision"] = catalog.SourceCatalogRevision,
                ["currentSourceCatalogHashSha256"] = catalog.SourceCatalogHashSha256,
                ["userMapApprovalCreated"] = false,
            };
        }

        private static JToken ReadPointer(JToken root, string pointer)
        {
            if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidOperationException("PrivateStudyGeometryPointerRequired");
            var current = root;
            foreach (var raw in pointer.Split('/').Skip(1))
            {
                var key = raw.Replace("~1", "/").Replace("~0", "~");
                if (current is JObject value) current = value[key];
                else if (current is JArray values && int.TryParse(key, out var index)
                    && index >= 0 && index < values.Count) current = values[index];
                else throw new InvalidOperationException("PrivateStudyGeometryPointerRejected");
            }
            return current;
        }
        private static void RequireGraphics()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || EditorApplication.isPlaying)
                throw new InvalidOperationException("PrivateStudyGraphicsEditModeRequired"); }
        private static void VerifyReference(JToken reference)
        { var path = reference?.Value<string>("path"); if (path == null || !File.Exists(path)
                || !string.Equals(Hash(File.ReadAllBytes(path)), reference.Value<string>("sha256"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PrivateStudyReferenceHashMismatch"); }
        private static JObject Reference(string path) => new() { ["path"] = path, ["sha256"] = Hash(File.ReadAllBytes(path)) };
        private static JArray Vector(Vector3 p) => new(p.x, p.y, p.z);
        private static Color Hex(string value, Color fallback) => ColorUtility.TryParseHtmlString(value, out var color) ? color : fallback;
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void WriteNew(string path, JObject value)
        { if (File.Exists(path)) throw new IOException("PrivateStudyOutputAlreadyExists"); File.WriteAllText(path, value.ToString(Formatting.Indented), new UTF8Encoding(false)); }
    }
}
