using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static partial class 사가정영상선분디오라마DemoCapture
    {
        private static void 공통표면깊이CaptureIfRequested(Transform root, Camera overview, Building[] buildings)
        {
            var output = Environment.GetEnvironmentVariable("SSALDDEL_SAGAJEONG_SURFACE_DEPTH_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) return;
            if (!Path.IsPathRooted(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("SurfaceDepthNewAbsoluteOutputRequired");
            var common = root.Find("기존 Overture 2247 · 원문 유지");
            if (common == null) throw new InvalidOperationException("SurfaceDepthExistingCommonRootRequired");
            Directory.CreateDirectory(output);
            Transform cameraRoot = null;
            var before = new Dictionary<string, JObject>(); var after = new Dictionary<string, JObject>(); var restored = new Dictionary<string, JObject>();
            사가정공통표면깊이Style.Scope scope = null;
            사가정공통표면깊이Style.Report report = null;
            var journeyRequested = Environment.GetEnvironmentVariable("SSALDDEL_PICKUP_RIDE_PREVIEW_OUTPUT");
            var journeyAfter = string.IsNullOrWhiteSpace(journeyRequested) ? null : Path.Combine(output, "journey-after");
            var pixelsRestored = false;
            try
            {
                cameraRoot = Child(root, "공통 표면 깊이 비공개 비교 카메라");
                var cameras = new List<(string Key, Camera Camera)> { ("overview", overview) };
                var road = 픽업라이딩시안Road선택(buildings);
                var direction = (road.PointB - road.PointA).normalized;
                var side = Vector3.Cross(Vector3.up, direction).normalized;
                foreach (var entry in new[] { ("journey-a", 0f), ("journey-middle", .5f), ("journey-b", 1f) })
                {
                    var point = Vector3.Lerp(road.PointA, road.PointB, entry.Item2);
                    var node = Child(cameraRoot, entry.Item1);
                    var camera = node.gameObject.AddComponent<Camera>(); camera.enabled = false;
                    camera.scene = root.gameObject.scene; camera.cullingMask = 1 << ReviewLayer;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = overview.backgroundColor;
                    camera.orthographic = true; camera.orthographicSize = 11; camera.aspect = 16f / 9f;
                    camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                    camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                    node.position = point - direction * 4 + side * 3 + Vector3.up * 32;
                    node.rotation = Quaternion.LookRotation(point + Vector3.up * .8f - node.position, Vector3.up);
                    cameras.Add((entry.Item1, camera));
                }
                var sourceMeshes = common.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToArray();
                var sourceFingerprints = sourceMeshes.Select(m => new JObject { ["name"] = m.name, ["sha256"] = 사가정공통표면깊이Style.Fingerprint(m) }).ToArray();
                var originalRenderers = common.GetComponentsInChildren<MeshRenderer>(true).Length;
                foreach (var pair in cameras) before[pair.Key] = Capture(pair.Camera, Path.Combine(output, pair.Key + "-before.png"));
                scope = 사가정공통표면깊이Style.Apply(common.gameObject); report = scope.Result;
                foreach (var pair in cameras) after[pair.Key] = Capture(pair.Camera, Path.Combine(output, pair.Key + "-candidate.png"));
                WriteNew(Path.Combine(output, "candidate-ready.private.json"), new JObject
                { ["revision"] = report.revision, ["views"] = new JArray(cameras.Select(p => p.Key)), ["report"] = JObject.Parse(JsonUtility.ToJson(report)) });
                Debug.Log("SurfaceDepthCandidateReady:" + output);
                if (journeyAfter != null)
                {
                    try
                    {
                        Environment.SetEnvironmentVariable("SSALDDEL_PICKUP_RIDE_PREVIEW_OUTPUT", journeyAfter);
                        픽업라이딩시안CaptureIfRequested(root, buildings, overview);
                    }
                    finally { Environment.SetEnvironmentVariable("SSALDDEL_PICKUP_RIDE_PREVIEW_OUTPUT", journeyRequested); }
                }
                scope.Dispose(); scope = null;
                foreach (var pair in cameras) restored[pair.Key] = Capture(pair.Camera, Path.Combine(output, pair.Key + "-restored.png"));
                pixelsRestored = cameras.All(p => before[p.Key]["image"].Value<string>("sha256") == restored[p.Key]["image"].Value<string>("sha256"));
                if (!pixelsRestored) throw new InvalidOperationException("SurfaceDepthSourcePixelsNotRestored");
                var geometryPreserved = sourceMeshes.Select((m, i) => 사가정공통표면깊이Style.Fingerprint(m) == sourceFingerprints[i].Value<string>("sha256")).All(v => v);
                if (!geometryPreserved || !report.sourceReferencesRestored || !report.originalMaterialReferencesPreserved)
                    throw new InvalidOperationException("SurfaceDepthSourceStateNotRestored");
                WriteNew(Path.Combine(output, "surface-depth-result.private.json"), new JObject
                {
                    ["schemaVersion"] = "private-sagajeong-common-surface-depth-preview.v1",
                    ["revision"] = report.revision, ["report"] = JObject.Parse(JsonUtility.ToJson(report)),
                    ["views"] = new JArray(cameras.Select(p => new JObject
                    {
                        ["id"] = p.Key, ["before"] = before[p.Key]["image"].DeepClone(), ["candidate"] = after[p.Key]["image"].DeepClone(),
                        ["restored"] = restored[p.Key]["image"].DeepClone(), ["sameCameraAndLighting"] = true,
                        ["restoredExactly"] = true, ["cameraPosition"] = 픽업라이딩시안Vector(p.Camera.transform.position),
                        ["orthographicSize"] = p.Camera.orthographicSize
                    })),
                    ["sourceMeshes"] = new JArray(sourceFingerprints), ["sourceMeshFingerprintsPreserved"] = geometryPreserved,
                    ["originalRendererCount"] = originalRenderers, ["restoredRendererCount"] = common.GetComponentsInChildren<MeshRenderer>(true).Length,
                    ["sourcePixelsRestoredExactly"] = pixelsRestored, ["journeyBeforeOutput"] = journeyRequested,
                    ["journeyAfterOutput"] = journeyAfter, ["buildingHeightChanged"] = false, ["roadWidthChanged"] = false,
                    ["roadSurfaceLiftMeters"] = 사가정공통표면깊이Style.RoadFanLift,
                    ["roofRibbonLiftMeters"] = 사가정공통표면깊이Style.RoofRibbonLift,
                    ["sourceMeshGeometryPreserved"] = geometryPreserved,
                    ["captureKind"] = "UnityPreviewSceneCameraRender", ["gameViewVerified"] = false, ["playModeVerified"] = false,
                    ["canonicalApplied"] = false, ["publicationReady"] = false
                });
            }
            finally
            {
                scope?.Dispose();
                if (cameraRoot != null) Object.DestroyImmediate(cameraRoot.gameObject);
                WriteNew(Path.Combine(output, "cleanup.private.json"), new JObject
                { ["scopeDisposed"] = report?.disposed ?? false, ["sourceReferencesRestored"] = report?.sourceReferencesRestored ?? false,
                    ["sourcePixelsRestoredExactly"] = pixelsRestored, ["comparisonCamerasDestroyed"] = cameraRoot == null,
                    ["sourceSceneSaved"] = false, ["canonicalApplied"] = false });
            }
        }
    }
}
