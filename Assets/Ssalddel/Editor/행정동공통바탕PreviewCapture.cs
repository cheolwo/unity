using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>기존 도형 전체에 공통 바탕을 시험하는 격리된 그래픽 연구. Scene을 저장하지 않는다.</summary>
    public static class 행정동공통바탕PreviewCapture
    {
        private const int Width = 1600, Height = 1000, Layer = 31;

        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || EditorApplication.isPlaying)
                throw new InvalidOperationException("CommonBasePreviewGraphicsRequired");
            var inputPath = Argument("-commonStyleInput");
            var output = Argument("-commonStyleOutput");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("CommonBasePreviewOutputNotEmpty");
            Directory.CreateDirectory(output);
            var input = JObject.Parse(File.ReadAllText(inputPath));
            Require(input.Value<string>("schema") == "private-common-base-layer-input.v1", "CommonBaseInputSchemaInvalid");
            var originalScene = SceneManager.GetActiveScene();
            var originalScenePath = originalScene.path;
            var originalSceneHandle = originalScene.handle;
            var ambient = RenderSettings.ambientLight;
            var modules = new JArray();
            foreach (var source in (JArray)input["sources"])
                modules.Add(CaptureModule((JObject)source, output));
            Require(SceneManager.GetActiveScene().handle == originalSceneHandle, "CommonBaseActiveSceneChanged");
            Require(RenderSettings.ambientLight == ambient, "CommonBaseAmbientChanged");
            var result = new JObject
            {
                ["schema"] = "private-common-base-layer-result.v1",
                ["inputSha256"] = FileHash(inputPath), ["modules"] = modules,
                ["implementationFiles"] = new JArray(new[] {
                    "Ssalddel/Editor/행정동공통바탕Style.cs",
                    "Ssalddel/Editor/행정동공통바탕PreviewCapture.cs",
                    "Ssalddel/Shaders/행정동공통바탕.shader"
                }.Select(relative => new JObject { ["path"] = relative,
                    ["sha256"] = FileHash(Path.Combine(UnityEngine.Application.dataPath, relative)) })),
                ["originalActiveScenePath"] = originalScenePath,
                ["activeScenePreserved"] = true, ["ambientPreserved"] = true,
                ["captureKind"] = "UnityPreviewSceneCameraRender",
                ["geometryOrAuthorityChanged"] = false, ["canonicalApplied"] = false,
                ["gameViewVerified"] = false, ["playModeVerified"] = false,
                ["realtimeFpsMeasured"] = false, ["publicDistributionApproved"] = false
            };
            File.WriteAllText(Path.Combine(output, "result.private.json"), result.ToString(Formatting.Indented));
            UnityEngine.Debug.Log("CommonBaseLayerPreviewPassed:Modules=" + modules.Count);
        }

        private static JObject CaptureModule(JObject source, string output)
        {
            var path = source.Value<string>("bundlePath");
            Require(FileHash(path).Equals(source.Value<string>("bundleSha256"), StringComparison.OrdinalIgnoreCase),
                "CommonBaseBundleHashMismatch");
            var raw = JObject.Parse(File.ReadAllText(path));
            Require(raw.Value<string>("schemaVersion") == "administrative-dong-diorama-unity-review-bundle.v7",
                "CommonBaseBundleSchemaInvalid");
            foreach (var key in new[] { "publishBlocked", "observationPresentationOnly" }) RequireBoolean(raw, key, true);
            foreach (var key in new[] { "distributionApproved", "traversalReady", "gameplayReady", "currentPointerUpdated", "personalDataIncluded" })
                RequireBoolean(raw, key, false);
            var applied = new AdministrativeDongDioramaInterpreter().Apply(
                raw["manifest"].ToObject<AdministrativeDongDioramaManifest>(),
                raw["tiles"].ToObject<AdministrativeDongDioramaTile[]>(),
                raw["displayOverlays"].ToObject<AdministrativeDongDisplayOverlayResponse>());
            Require(applied.Accepted, applied.ErrorCode);
            Require(행정동디오라마검토Projection.TryCreate(applied, out var snapshot, out var diagnostic), diagnostic);
            Require(snapshot.AdministrativeAreaStableId == source.Value<string>("areaStableId"), "CommonBaseAreaMismatch");
            행정동건물내부Ring검토Geometry focused = null;
            if (source["focusedGeometryPath"] != null)
            {
                var focusedPath = source.Value<string>("focusedGeometryPath");
                Require(FileHash(focusedPath).Equals(source.Value<string>("focusedGeometrySha256"), StringComparison.OrdinalIgnoreCase),
                    "CommonBaseFocusedGeometryHashMismatch");
                Require(행정동건물내부Ring검토Geometry.TryCreate(
                    JsonConvert.DeserializeObject<행정동건물내부Ring검토Data>(File.ReadAllText(focusedPath)), snapshot,
                    out focused, out diagnostic), diagnostic);
            }
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("공통 바탕 비공개 연구") { hideFlags = HideFlags.DontSave, layer = Layer };
            SceneManager.MoveGameObjectToScene(root, scene);
            var owned = new List<Object>();
            행정동공통바탕Style.Scope style = null;
            JObject result;
            try
            {
                var model = Child(root.transform, "기존 도형 메모리 사본");
                var lightNode = Child(root.transform, "기존 검토 조명");
                lightNode.localRotation = Quaternion.Euler(46f, -32f, 0f);
                var light = lightNode.gameObject.AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.15f;
                light.color = new Color(1f, .96f, .88f); light.cullingMask = 1 << Layer;
                light.shadows = LightShadows.None;
                var shader = Resources.Load<Shader>("역세권공간모형") ?? Shader.Find("Ssalddel/StationDiorama");
                Require(shader != null, "CommonBaseOriginalShaderMissing");
                var material = new Material(shader) { name = "기존 공통 vertex color", color = Color.white };
                owned.Add(material);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                var build = 행정동디오라마검토MeshBuilder.Build(snapshot, batch =>
                {
                    var child = Child(model, batch.Name);
                    var mesh = new Mesh { name = batch.Name, indexFormat = batch.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    owned.Add(mesh);
                    mesh.SetVertices(batch.Vertices); mesh.SetTriangles(batch.Triangles, 0); mesh.SetColors(batch.Colors);
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }, focused);
                Require(build.BuildingCount == raw.Value<int>("buildingCount"), "CommonBaseBuildingCountMismatch");
                var sourceMeshes = model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                var sourceHash = MeshHash(model.gameObject, true);
                var geometryHash = MeshHash(model.gameObject, false);
                var cameras = MakeCameras(root.transform, snapshot, build.ModelBounds, source.Value<bool>("includeWindow"));
                var code = source.Value<string>("code");
                var folder = Path.Combine(output, code); Directory.CreateDirectory(folder);
                var captures = new JArray();
                foreach (var camera in cameras)
                    captures.Add(new JObject { ["view"] = camera.name, ["camera"] = CameraRecord(camera),
                        ["before"] = Capture(camera, Path.Combine(folder, camera.name + "-before.png")) });
                style = 행정동공통바탕Style.Apply(model.gameObject);
                Require(MeshHash(model.gameObject, false) == geometryHash, "CommonBaseStyledGeometryChanged");
                foreach (var pair in cameras.Select((camera, index) => (camera, index)))
                {
                    var item = (JObject)captures[pair.index];
                    item["after"] = Capture(pair.camera, Path.Combine(folder, pair.camera.name + "-after.png"));
                    Require(item["before"].Value<string>("sha256") != item["after"].Value<string>("sha256"), "CommonBaseImageNotChanged");
                }
                style.Dispose();
                Require(MeshHash(model.gameObject, true) == sourceHash, "CommonBaseRestoreStreamMismatch");
                Require(model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).SequenceEqual(sourceMeshes),
                    "CommonBaseOriginalMeshReferencesNotRestored");
                foreach (var pair in cameras.Select((camera, index) => (camera, index)))
                {
                    var item = (JObject)captures[pair.index];
                    item["restored"] = Capture(pair.camera, Path.Combine(folder, pair.camera.name + "-restored.png"));
                    Require(item["before"].Value<string>("sha256") == item["restored"].Value<string>("sha256"),
                        "CommonBaseRestoredImageMismatch");
                }
                result = new JObject
                {
                    ["areaStableId"] = snapshot.AdministrativeAreaStableId,
                    ["bundlePath"] = path, ["bundleSha256"] = FileHash(path),
                    ["buildingCount"] = build.BuildingCount, ["roadSegmentCount"] = build.RoadSegmentCount,
                    ["focusedInteriorRingCount"] = build.FocusedInteriorRingCount,
                    ["sourceCenterX"] = snapshot.SourceCenterX, ["sourceCenterZ"] = snapshot.SourceCenterZ,
                    ["metersPerUnit"] = snapshot.MetersPerUnit,
                    ["rendererCount"] = build.RendererBatchCount, ["vertexCount"] = build.TotalVertexCount,
                    ["triangleCount"] = build.TotalIndexCount / 3, ["geometrySha256"] = geometryHash,
                    ["geometryPreserved"] = true, ["sourceStreamsAndReferencesRestored"] = true,
                    ["baselineLight"] = "ExistingViewDirectionIntensityAndColor;NoShadowComparison",
                    ["style"] = JToken.FromObject(style.Result), ["captures"] = captures
                };
            }
            finally
            {
                style?.Dispose();
                Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            result["previewSceneClosed"] = !scene.IsValid();
            Require(!scene.IsValid(), "CommonBasePreviewSceneNotClosed");
            return result;
        }

        private static Camera[] MakeCameras(Transform parent, 행정동디오라마검토Snapshot snapshot, Bounds bounds, bool includeWindow)
        {
            var cameras = new List<Camera>();
            var rotation = Quaternion.Euler(48, 35, 0);
            cameras.Add(MakeCamera(parent, "whole", bounds.center,
                행정동디오라마검토View.WholeViewOrthographicSize(bounds, rotation, (float)Width / Height), rotation));
            if (includeWindow)
            {
                // 연구 창의 원본 ENU 중심을 기존 검토 사본의 재중심 좌표로 바꾼다.
                var windowCenter = new Vector3((float)((28.5 - snapshot.SourceCenterX) / snapshot.MetersPerUnit),
                    7, (float)((140.6 - snapshot.SourceCenterZ) / snapshot.MetersPerUnit));
                cameras.Add(MakeCamera(parent, "window250", windowCenter, (float)(155 / snapshot.MetersPerUnit), rotation));
                var edge = snapshot.Buildings.OrderBy(b => b.CentroidX).Skip(snapshot.Buildings.Count * 9 / 10).First();
                cameras.Add(MakeCamera(parent, "periphery", new Vector3((float)edge.CentroidX, 7, (float)edge.CentroidZ), 125f, rotation));
            }
            else
            {
                var building = snapshot.Buildings.OrderBy(b => b.CentroidX).ElementAt(snapshot.Buildings.Count / 2);
                cameras.Add(MakeCamera(parent, "comparison250", new Vector3((float)building.CentroidX, 7, (float)building.CentroidZ), 155f, rotation));
            }
            return cameras.ToArray();
        }

        private static Camera MakeCamera(Transform parent, string name, Vector3 center, float size, Quaternion rotation)
        {
            var node = Child(parent, name); var camera = node.gameObject.AddComponent<Camera>();
            camera.enabled = false; camera.scene = parent.gameObject.scene; camera.cullingMask = 1 << Layer;
            camera.orthographic = true; camera.orthographicSize = size; camera.aspect = (float)Width / Height;
            camera.nearClipPlane = .1f; camera.farClipPlane = 8000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.86f, .89f, .91f);
            camera.transform.rotation = rotation;
            camera.transform.position = center - rotation * Vector3.forward * 3500;
            node.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            return camera;
        }

        private static JObject Capture(Camera camera, string path)
        {
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.Render();
                var watch = Stopwatch.StartNew(); camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); watch.Stop();
                var pixels = texture.GetPixels32(); var bg = (Color32)camera.backgroundColor;
                double sum = 0, squares = 0; var foreground = 0;
                foreach (var pixel in pixels)
                {
                    var value = (pixel.r + pixel.g + pixel.b) / 3d; sum += value; squares += value * value;
                    if (Math.Abs(pixel.r - bg.r) + Math.Abs(pixel.g - bg.g) + Math.Abs(pixel.b - bg.b) > 18) foreground++;
                }
                var variance = squares / pixels.Length - Math.Pow(sum / pixels.Length, 2);
                Require(foreground >= 1000 && variance >= 4, "CommonBaseBlankCaptureRejected");
                Require(!File.Exists(path), "CommonBaseCaptureExists");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return new JObject { ["path"] = path, ["sha256"] = FileHash(path), ["foregroundPixels"] = foreground,
                    ["brightnessVariance"] = variance, ["renderAndReadbackMilliseconds"] = watch.Elapsed.TotalMilliseconds };
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
            }
        }

        private static string MeshHash(GameObject root, bool includeColors)
        {
            using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                foreach (var i in mesh.triangles) writer.Write(i);
                foreach (var n in mesh.normals) { writer.Write(n.x); writer.Write(n.y); writer.Write(n.z); }
                foreach (var uv in mesh.uv) { writer.Write(uv.x); writer.Write(uv.y); }
                if (includeColors) foreach (var c in mesh.colors) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            }
            writer.Flush(); using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
        private static JObject CameraRecord(Camera c) => new JObject
        {
            ["position"] = new JArray(c.transform.position.x, c.transform.position.y, c.transform.position.z),
            ["rotation"] = new JArray(c.transform.rotation.x, c.transform.rotation.y, c.transform.rotation.z, c.transform.rotation.w),
            ["orthographicSize"] = c.orthographicSize, ["width"] = Width, ["height"] = Height
        };
        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = Layer };
            go.transform.SetParent(parent, false); return go.transform;
        }
        private static string FileHash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string diagnostic)
        { if (!value) throw new InvalidOperationException(diagnostic); }
        private static void RequireBoolean(JObject raw, string key, bool expected)
        { Require(raw[key]?.Type == JTokenType.Boolean && raw.Value<bool>(key) == expected, "CommonBaseAuthorityFlagInvalid:" + key); }
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("CommonBaseArgumentMissing:" + name);
            return Path.GetFullPath(args[index + 1]);
        }
    }
}
