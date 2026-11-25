using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 비공개 PreviewScene의 재질·조명 사본만 소유한다. 지리·업무 권위나 실측 복원 도구가 아니다.
    /// 원본 mesh/material, RenderSettings, URP/Quality/GraphicsSettings는 쓰지 않는다.
    /// </summary>
    public static class 사가정비공개현실감Style
    {
        public const string Revision = "private-neighborhood-realism.r1";
        public const string StyleKey = "realism-r1";
        public const string ShaderName = "Ssalddel/PrivateNeighborhoodRealism";
        public const float ProposedCameraDistance = 65f;

        [Serializable]
        public sealed class CameraReview
        {
            public string cameraName;
            public Vector3 originalPosition, styledPosition;
            public float originalDistance, styledDistance, originalMinimumVisibleDepth, styledMinimumVisibleDepth;
            public int clippedVisibleTriangleCount, unreadableMeshBoundsFallbackCount;
            public bool originalVisibleGeometryFitsNearPlane, cameraDistanceChanged, orthographicProjectionPreserved;
            public bool realtimeShadowRangeCoversTarget;
        }
        [Serializable]
        public sealed class Report
        {
            public string revision = Revision;
            public string textureSource = "DeterministicCodeNativeMicrotextureNotPhotographicSurfaceScan";
            public bool hdriUsed, originalPhotographUsedAsTexture, measuredMaterialClaimed;
            public bool originalMeshReferencesPreserved = true, originalMaterialsModified;
            public bool originalWindowPatternPreserved = true, realWindowCountsInferred;
            public bool glassUsesOpaqueReflectionAndMicroNormalOnly = true, interiorReconstructed;
            public bool renderSettingsWritten, pipelineSettingsWritten, sourceSceneSaved;
            public int styledRendererCount, materialCopyCount, preservedTransparentLabelCount, excludedCandidateRendererCount;
            public int proceduralTextureCount, localReflectionProbeCount;
            public bool localShadowLightRequested, physicalShadowVisibilityRequiresImageReview = true;
            public bool disposed, rendererReferencesRestored, lightRestored, cameraPosesRestored;
            public CameraReview[] cameras;
        }

        private sealed class RendererState
        {
            public MeshRenderer Renderer;
            public Material[] Materials;
            public MeshFilter Filter;
            public Mesh Mesh;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public ReflectionProbeUsage Reflection;
        }
        private sealed class CameraState
        {
            public Camera Camera;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public sealed class Scope : IDisposable
        {
            private readonly List<RendererState> renderers = new();
            private readonly List<CameraState> cameras = new();
            private readonly List<Object> owned = new();
            private readonly Light key;
            private readonly Color originalLightColor;
            private readonly float originalIntensity, originalBias, originalNormalBias, originalNearPlane;
            private readonly LightShadows originalShadows;
            private readonly LightShadowResolution originalResolution;
            public Report Result { get; } = new();

            internal Scope(Transform root, Light main, Camera[] viewCameras, Vector3[] centers, Transform[] exclusions)
            {
                if (root == null || main == null || viewCameras == null || centers == null
                    || viewCameras.Length == 0 || viewCameras.Length != centers.Length
                    || !root.gameObject.scene.IsValid() || main.gameObject.scene != root.gameObject.scene
                    || viewCameras.Any(c => c == null || c.scene != root.gameObject.scene || !c.orthographic))
                    throw new ArgumentException("PrivateRealismOwnedPreviewContextRequired");
                key = main; originalLightColor = main.color; originalIntensity = main.intensity;
                originalShadows = main.shadows; originalBias = main.shadowBias; originalNormalBias = main.shadowNormalBias;
                originalNearPlane = main.shadowNearPlane; originalResolution = main.shadowResolution;
                try
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader == null || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !shader.isSupported)
                        throw new InvalidOperationException("PrivateRealismShaderRequired");
                    var all = root.GetComponentsInChildren<MeshRenderer>(true);
                    var visible = all.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                    Result.cameras = new CameraReview[viewCameras.Length];
                    var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    for (var i = 0; i < viewCameras.Length; i++)
                    {
                        var camera = viewCameras[i];
                        cameras.Add(new CameraState { Camera = camera, Position = camera.transform.position, Rotation = camera.transform.rotation });
                        var review = ReviewCamera(camera, centers[i], visible);
                        review.realtimeShadowRangeCoversTarget = review.cameraDistanceChanged && pipeline != null
                            && pipeline.supportsMainLightShadows && pipeline.shadowDistance > review.styledDistance;
                        Result.cameras[i] = review;
                        if (review.cameraDistanceChanged) camera.transform.position = review.styledPosition;
                    }
                    var textures = CreateTextures();
                    foreach (var renderer in all)
                    {
                        if (exclusions != null && exclusions.Any(t => t != null && (renderer.transform == t || renderer.transform.IsChildOf(t))))
                        { Result.excludedCandidateRendererCount++; continue; }
                        var sources = renderer.sharedMaterials;
                        if (sources.Length == 0 || sources.Any(m => m == null)) continue;
                        if (sources.Any(IsTransparentLabel)) { Result.preservedTransparentLabelCount++; continue; }
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null) continue;
                        renderers.Add(new RendererState { Renderer = renderer, Materials = sources, Filter = filter,
                            Mesh = filter.sharedMesh, Shadows = renderer.shadowCastingMode,
                            ReceiveShadows = renderer.receiveShadows, Reflection = renderer.reflectionProbeUsage });
                        renderer.sharedMaterials = sources.Select(source => CopyMaterial(source, shader, textures)).ToArray();
                        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                        Result.styledRendererCount++;
                    }
                    main.color = new Color(1f, .94f, .85f); main.intensity = 1.15f; main.shadows = LightShadows.Soft;
                    main.shadowBias = .06f; main.shadowNormalBias = .15f; main.shadowNearPlane = .2f;
                    main.shadowResolution = LightShadowResolution.High;
                    Result.localShadowLightRequested = true;
                    var fillObject = OwnedChild(root, "비공개 현실감 보조광 · 실제 촬영광 복원 아님");
                    var fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional;
                    fill.color = new Color(.72f, .83f, 1); fill.intensity = .34f; fill.shadows = LightShadows.None;
                    fill.cullingMask = main.cullingMask; fillObject.transform.rotation = Quaternion.Euler(65, 145, 0);
                    var environment = CreateGradientEnvironment();
                    var probeObject = OwnedChild(root, "비공개 코드 생성 환경 반사 · HDRI 아님");
                    probeObject.transform.position = centers.Aggregate(Vector3.zero, (sum, p) => sum + p) / centers.Length;
                    var probe = probeObject.AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom;
                    probe.customBakedTexture = environment; probe.size = new Vector3(2000, 1000, 2000);
                    probe.center = Vector3.zero; probe.boxProjection = true; probe.intensity = .65f; probe.blendDistance = 4;
                    probe.cullingMask = main.cullingMask; Result.localReflectionProbeCount = 1;
                }
                catch { Dispose(); throw; }
            }

            private Dictionary<string, Texture2D> CreateTextures()
            {
                var result = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
                foreach (var kind in new[] { "Concrete", "Asphalt", "Roof" })
                    foreach (var normal in new[] { false, true })
                    {
                        var seed = kind == "Concrete" ? 20111 : kind == "Asphalt" ? 20112 : 20113;
                        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, true)
                        { name = "CodeNativeUnmeasured:" + kind + (normal ? "Normal" : "Modulation"),
                            hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                        texture.SetPixels32(CreateNoisePixels(128, seed, normal)); texture.Apply(true, false);
                        owned.Add(texture); result.Add(kind + (normal ? "Normal" : "Map"), texture);
                    }
                Result.proceduralTextureCount = result.Count;
                return result;
            }
            private Material CopyMaterial(Material source, Shader shader, Dictionary<string, Texture2D> textures)
            {
                if (source.shader.name.Contains("MiniatureShadow", StringComparison.Ordinal))
                {
                    var contact = new Material(source) { hideFlags = HideFlags.DontSave };
                    var contactColor = contact.color; contactColor.a *= .35f; contact.color = contactColor;
                    owned.Add(contact); Result.materialCopyCount++; return contact;
                }
                var material = new Material(shader) { name = "PrivateRealismCopy:" + source.name, hideFlags = HideFlags.DontSave };
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                    : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                material.SetColor("_Color", color); material.SetColor("_BaseColor", color);
                material.SetFloat("_VertexColorWeight", source.HasProperty("_VertexColorWeight") ? source.GetFloat("_VertexColorWeight") : 0);
                if (source.HasProperty("_WindowColor")) material.SetColor("_WindowColor", source.GetColor("_WindowColor"));
                material.SetFloat("_GlassWeight", color.r < .18f && color.g < .27f && color.b < .33f && color.b > color.r * 1.15f ? 1 : 0);
                foreach (var pair in textures) material.SetTexture("_" + pair.Key, pair.Value);
                material.SetFloat("_NormalStrength", .16f);
                owned.Add(material); Result.materialCopyCount++; return material;
            }
            private Cubemap CreateGradientEnvironment()
            {
                const int size = 16;
                var cube = new Cubemap(size, TextureFormat.RGBA32, false)
                { name = "CodeNativeDaylightGradientNotHdri", hideFlags = HideFlags.DontSave };
                for (var face = 0; face < 6; face++)
                {
                    var pixels = new Color[size * size];
                    for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                    {
                        var up = face == (int)CubemapFace.PositiveY ? 1f : face == (int)CubemapFace.NegativeY ? 0f : (y + .5f) / size;
                        pixels[y * size + x] = Color.Lerp(new Color(.16f, .17f, .16f), new Color(.52f, .67f, .84f), up);
                    }
                    cube.SetPixels(pixels, (CubemapFace)face);
                }
                cube.Apply(false, false); owned.Add(cube); return cube;
            }
            private GameObject OwnedChild(Transform parent, string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = key.gameObject.layer };
                go.transform.SetParent(parent, false); owned.Add(go); return go;
            }
            public void Dispose()
            {
                if (Result.disposed) return;
                foreach (var state in renderers)
                {
                    if (state.Renderer == null) continue;
                    state.Renderer.sharedMaterials = state.Materials; state.Renderer.shadowCastingMode = state.Shadows;
                    state.Renderer.receiveShadows = state.ReceiveShadows; state.Renderer.reflectionProbeUsage = state.Reflection;
                }
                foreach (var state in cameras) if (state.Camera != null)
                { state.Camera.transform.SetPositionAndRotation(state.Position, state.Rotation); }
                if (key != null)
                {
                    key.color = originalLightColor; key.intensity = originalIntensity; key.shadows = originalShadows;
                    key.shadowBias = originalBias; key.shadowNormalBias = originalNormalBias;
                    key.shadowNearPlane = originalNearPlane; key.shadowResolution = originalResolution;
                }
                foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
                Result.disposed = true;
                Result.rendererReferencesRestored = renderers.All(s => s.Renderer == null
                    || s.Renderer.sharedMaterials.SequenceEqual(s.Materials) && s.Filter.sharedMesh == s.Mesh);
                Result.originalMeshReferencesPreserved = renderers.All(s => s.Filter == null || s.Filter.sharedMesh == s.Mesh);
                Result.lightRestored = key == null || key.color == originalLightColor && key.intensity == originalIntensity
                    && key.shadows == originalShadows && key.shadowBias == originalBias && key.shadowNormalBias == originalNormalBias;
                Result.cameraPosesRestored = cameras.All(s => s.Camera == null
                    || s.Camera.transform.position == s.Position && s.Camera.transform.rotation == s.Rotation);
            }
        }

        public static Scope Apply(Transform root, Light main, Camera[] cameras, Vector3[] centers, params Transform[] excluded)
            => new(root, main, cameras, centers, excluded);

        // Vector3.normalized 등 계산 속성을 따라가지 않고 Unity의 저장 필드만 기록한다.
        public static string SerializeReport(Report report)
            => report == null ? throw new ArgumentNullException(nameof(report)) : JsonUtility.ToJson(report);

        private static bool IsTransparentLabel(Material material) => material.renderQueue >= 3000
            && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;

        public static Color32[] CreateNoisePixels(int size, int seed, bool normal)
        {
            if (size < 4 || size > 512) throw new ArgumentOutOfRangeException(nameof(size));
            float HeightAt(int x, int y)
            {
                unchecked { var hash = (uint)(seed * 374761393 + (x + size) % size * 668265263 + (y + size) % size * 69069);
                    hash = (hash ^ hash >> 13) * 1274126177; return ((hash ^ hash >> 16) & 65535) / 65535f; }
            }
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                if (normal)
                {
                    var n = new Vector3((HeightAt(x - 1, y) - HeightAt(x + 1, y)) * .22f,
                        (HeightAt(x, y - 1) - HeightAt(x, y + 1)) * .22f, 1).normalized;
                    pixels[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
                else
                { var value = .5f + (HeightAt(x, y) - .5f) * .16f; pixels[y * size + x] = new Color(value, value, value, 1); }
            }
            return pixels;
        }

        /// <summary>원래 정사영 화면에 걸친 삼각형만 XY로 잘라 새 near plane에 가려지는지 보수적으로 검사한다.</summary>
        public static CameraReview ReviewCamera(Camera camera, Vector3 center, MeshRenderer[] renderers)
        {
            if (camera == null || !camera.orthographic || renderers == null)
                throw new ArgumentException("PrivateRealismOrthographicCameraRequired");
            var original = camera.transform.position; var distance = Vector3.Distance(original, center);
            var proposed = center + (original - center).normalized * ProposedCameraDistance;
            var minimum = float.PositiveInfinity; var count = 0; var unreadable = 0;
            var matrix = camera.worldToCameraMatrix;
            var halfY = camera.orthographicSize; var halfX = halfY * camera.aspect;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter?.sharedMesh == null || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                if (!filter.sharedMesh.isReadable)
                {
                    // 읽기 불가인 기존 부품도 생략하지 않고 보수적인 bounds 전부로 near plane을 검사한다.
                    var bounds = renderer.bounds;
                    for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                        minimum = Mathf.Min(minimum, -matrix.MultiplyPoint3x4(bounds.center
                            + Vector3.Scale(bounds.extents, new Vector3(x, y, z))).z);
                    unreadable++; count++; continue;
                }
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var indices = mesh.triangles;
                var transform = matrix * filter.transform.localToWorldMatrix;
                var points = vertices.Select(transform.MultiplyPoint3x4).ToArray();
                for (var i = 0; i < indices.Length; i += 3)
                {
                    var a = points[indices[i]]; var b = points[indices[i + 1]]; var c = points[indices[i + 2]];
                    if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > halfX || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < -halfX
                        || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > halfY || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < -halfY) continue;
                    var polygon = new List<Vector3> { a, b, c };
                    polygon = Clip(polygon, 0, -halfX, true); polygon = Clip(polygon, 0, halfX, false);
                    polygon = Clip(polygon, 1, -halfY, true); polygon = Clip(polygon, 1, halfY, false);
                    if (polygon.Count < 3) continue;
                    var depth = polygon.Min(p => -p.z);
                    if (depth < camera.nearClipPlane || depth > camera.farClipPlane) continue;
                    minimum = Mathf.Min(minimum, depth); count++;
                }
            }
            var proposedMinimum = minimum + Vector3.Dot(original - proposed, camera.transform.forward);
            var safe = count > 0 && float.IsFinite(proposedMinimum) && proposedMinimum > camera.nearClipPlane + .25f;
            return new CameraReview { cameraName = camera.name, originalPosition = original,
                styledPosition = safe ? proposed : original, originalDistance = distance,
                styledDistance = safe ? ProposedCameraDistance : distance,
                originalMinimumVisibleDepth = float.IsFinite(minimum) ? minimum : -1,
                styledMinimumVisibleDepth = safe ? proposedMinimum : float.IsFinite(minimum) ? minimum : -1,
                clippedVisibleTriangleCount = count, unreadableMeshBoundsFallbackCount = unreadable,
                originalVisibleGeometryFitsNearPlane = safe,
                cameraDistanceChanged = safe, orthographicProjectionPreserved = true };
        }
        private static List<Vector3> Clip(List<Vector3> points, int axis, float boundary, bool greater)
        {
            var result = new List<Vector3>();
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                var insideA = greater ? a[axis] >= boundary : a[axis] <= boundary;
                var insideB = greater ? b[axis] >= boundary : b[axis] <= boundary;
                if (insideA) result.Add(a);
                if (insideA != insideB) result.Add(Vector3.LerpUnclamped(a, b, (boundary - a[axis]) / (b[axis] - a[axis])));
            }
            return result;
        }
    }
}
