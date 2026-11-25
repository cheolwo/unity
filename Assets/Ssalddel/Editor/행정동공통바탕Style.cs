using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 행정동 기초 도형의 메모리 사본에만 공통 바탕색을 적용하는 비공개 Editor 연구.
    /// 고정 스튜디오 명암이며 실제 재질·물리 그림자·새 공간 근거를 만들지 않는다.
    /// </summary>
    public static class 행정동공통바탕Style
    {
        public const string Revision = "private-administrative-dong-common-base.r1";
        public const string ShaderName = "Ssalddel/PrivateAdministrativeDongCommonBase";
        private const string OwnedPrefix = "PrivateAdministrativeCommonBase:";
        private static readonly HashSet<GameObject> ActiveRoots = new();
        private static readonly HashSet<MeshFilter> ActiveFilters = new();

        private enum SurfaceRole { Unknown, Ground, Road, Boundary, Roof, Wall }

        [Serializable]
        public sealed class Report
        {
            public string revision = Revision;
            public int styledRendererCount, sourceMeshCount, candidateMeshCount;
            public int vertexCount, triangleCount;
            public int groundTriangleCount, roadTriangleCount, boundaryTriangleCount;
            public int roofTriangleCount, wallTriangleCount, unchangedTriangleCount;
            public bool fixedStudioShadingOnly = true;
            public bool actualMaterialOrPhysicalShadowClaimed;
            public bool geometryAdded, windowPatternAdded, sourceMaterialModified;
            public bool renderSettingsWritten, pipelineSettingsWritten, sceneSaved;
            public bool disposed, rendererReferencesRestored, sourceDataPreserved;
            public bool ownedResourcesDestroyed;
        }

        private sealed class MeshState
        {
            public Mesh Source, Candidate;
            public Vector3[] Vertices, Normals;
            public Vector4[] Tangents;
            public Color[] Colors;
            public Bounds Bounds;
            public int[][] Indices;
            public MeshTopology[] Topologies;
            public readonly List<Vector4>[] Uvs = new List<Vector4>[8];

            public bool SourceUnchanged()
            {
                if (Source == null) return false;
                if (!Source.vertices.SequenceEqual(Vertices) || !Source.normals.SequenceEqual(Normals)
                    || !Source.tangents.SequenceEqual(Tangents) || !Source.colors.SequenceEqual(Colors)
                    || !Source.bounds.Equals(Bounds) || Source.subMeshCount != Indices.Length) return false;
                for (var index = 0; index < Indices.Length; index++)
                    if (Source.GetTopology(index) != Topologies[index]
                        || !Source.GetIndices(index).SequenceEqual(Indices[index])) return false;
                for (var channel = 0; channel < Uvs.Length; channel++)
                {
                    var current = new List<Vector4>(); Source.GetUVs(channel, current);
                    if (!current.SequenceEqual(Uvs[channel])) return false;
                }
                return true;
            }
        }

        private sealed class RendererState
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Source;
            public Material[] Materials;
        }

        public sealed class Scope : IDisposable
        {
            private readonly List<RendererState> states = new();
            private readonly Dictionary<Mesh, MeshState> meshes = new();
            private readonly List<Object> owned = new();
            private readonly GameObject sourceRoot;
            private bool registered;
            public Report Result { get; } = new();

            internal Scope(GameObject root)
            {
                if (root == null) throw new ArgumentNullException(nameof(root));
                sourceRoot = root;
                if (ActiveRoots.Contains(root))
                    throw new InvalidOperationException("AdministrativeCommonBaseAlreadyApplied");
                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0)
                    throw new InvalidOperationException("AdministrativeCommonBaseMeshRequired");
                if (filters.Any(filter => ActiveFilters.Contains(filter)))
                    throw new InvalidOperationException("AdministrativeCommonBaseOverlappingScope");
                if (root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                    throw new InvalidOperationException("AdministrativeCommonBaseStaticMeshRequired");
                try
                {
                    // 모든 입력과 사본을 먼저 준비한 뒤 참조를 교체한다.
                    foreach (var filter in filters)
                    {
                        var source = filter.sharedMesh;
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (source == null || !source.isReadable || renderer == null)
                            throw new InvalidOperationException("AdministrativeCommonBaseReadableMeshRendererRequired");
                        if (renderer.sharedMaterials.Length != source.subMeshCount
                            || renderer.sharedMaterials.Any(material => material == null))
                            throw new InvalidOperationException("AdministrativeCommonBaseMaterialBindingInvalid");
                        states.Add(new RendererState { Filter = filter, Renderer = renderer,
                            Source = source, Materials = renderer.sharedMaterials });
                        if (!meshes.ContainsKey(source)) meshes.Add(source, PrepareMesh(source));
                    }
                    var shader = Shader.Find(ShaderName);
                    if (shader == null || !shader.isSupported
                        || global::UnityEditor.ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("AdministrativeCommonBaseShaderUnavailable");
                    var material = new Material(shader) { name = OwnedPrefix + "CommonPalette",
                        hideFlags = HideFlags.DontSave, enableInstancing = true };
                    owned.Add(material);
                    ActiveRoots.Add(root);
                    foreach (var state in states) ActiveFilters.Add(state.Filter);
                    registered = true;
                    foreach (var state in states)
                    {
                        state.Filter.sharedMesh = meshes[state.Source].Candidate;
                        state.Renderer.sharedMaterials = Enumerable.Repeat(material, state.Materials.Length).ToArray();
                    }
                    Result.styledRendererCount = states.Count;
                    Result.sourceMeshCount = meshes.Count;
                    Result.candidateMeshCount = meshes.Count;
                    Result.sourceDataPreserved = meshes.Values.All(state => state.SourceUnchanged());
                }
                catch { Dispose(); throw; }
            }

            private MeshState PrepareMesh(Mesh source)
            {
                var state = new MeshState { Source = source, Vertices = source.vertices,
                    Normals = source.normals, Tangents = source.tangents, Colors = source.colors,
                    Bounds = source.bounds, Indices = new int[source.subMeshCount][],
                    Topologies = new MeshTopology[source.subMeshCount] };
                if (state.Vertices.Length == 0 || state.Colors.Length != state.Vertices.Length
                    || state.Normals.Length != state.Vertices.Length)
                    throw new InvalidOperationException("AdministrativeCommonBaseVertexStreamsInvalid");
                for (var channel = 0; channel < state.Uvs.Length; channel++)
                {
                    state.Uvs[channel] = new List<Vector4>(); source.GetUVs(channel, state.Uvs[channel]);
                }
                var roles = Enumerable.Repeat(-1, state.Vertices.Length).ToArray();
                for (var submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    state.Topologies[submesh] = source.GetTopology(submesh);
                    if (state.Topologies[submesh] != MeshTopology.Triangles)
                        throw new InvalidOperationException("AdministrativeCommonBaseTrianglesRequired");
                    var indices = source.GetIndices(submesh); state.Indices[submesh] = indices;
                    if (indices.Length % 3 != 0)
                        throw new InvalidOperationException("AdministrativeCommonBaseTriangleIndicesInvalid");
                    for (var index = 0; index < indices.Length; index += 3)
                    {
                        var a = indices[index]; var b = indices[index + 1]; var c = indices[index + 2];
                        var role = Classify(state.Vertices[a], state.Vertices[b], state.Vertices[c]);
                        foreach (var vertex in new[] { a, b, c })
                        {
                            if (roles[vertex] >= 0 && roles[vertex] != (int)role)
                                throw new InvalidOperationException("AdministrativeCommonBaseSharedRoleVertexUnsupported");
                            roles[vertex] = (int)role;
                        }
                        Count(role); Result.triangleCount++;
                    }
                }
                var colors = state.Colors.ToArray();
                for (var index = 0; index < colors.Length; index++)
                    if (roles[index] > (int)SurfaceRole.Unknown)
                        colors[index] = Palette((SurfaceRole)roles[index], state.Colors[index]);
                var candidate = Object.Instantiate(source);
                candidate.name = OwnedPrefix + source.name;
                candidate.hideFlags = HideFlags.DontSave;
                owned.Add(candidate); state.Candidate = candidate;
                candidate.colors = colors;
                Result.vertexCount += state.Vertices.Length;
                return state;
            }

            private void Count(SurfaceRole role)
            {
                switch (role)
                {
                    case SurfaceRole.Ground: Result.groundTriangleCount++; break;
                    case SurfaceRole.Road: Result.roadTriangleCount++; break;
                    case SurfaceRole.Boundary: Result.boundaryTriangleCount++; break;
                    case SurfaceRole.Roof: Result.roofTriangleCount++; break;
                    case SurfaceRole.Wall: Result.wallTriangleCount++; break;
                    default: Result.unchangedTriangleCount++; break;
                }
            }

            public void Dispose()
            {
                if (Result.disposed) return;
                foreach (var state in states)
                {
                    if (state.Filter != null) state.Filter.sharedMesh = state.Source;
                    if (state.Renderer != null) state.Renderer.sharedMaterials = state.Materials;
                }
                Result.rendererReferencesRestored = states.All(state =>
                    (state.Filter == null || state.Filter.sharedMesh == state.Source)
                    && (state.Renderer == null || state.Renderer.sharedMaterials.SequenceEqual(state.Materials)));
                Result.sourceDataPreserved = meshes.Values.All(state => state.SourceUnchanged());
                if (registered)
                {
                    ActiveRoots.Remove(sourceRoot);
                    foreach (var state in states) ActiveFilters.Remove(state.Filter);
                    registered = false;
                }
                foreach (var value in owned.AsEnumerable().Reverse())
                    if (value != null) Object.DestroyImmediate(value);
                Result.ownedResourcesDestroyed = owned.All(value => value == null);
                Result.disposed = true;
            }
        }

        public static Scope Apply(GameObject root) => new(root);

        private static SurfaceRole Classify(Vector3 a, Vector3 b, Vector3 c)
        {
            var maximum = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            var minimum = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var horizontal = maximum - minimum < .001f;
            if (horizontal && Mathf.Abs(maximum - .02f) < .001f) return SurfaceRole.Ground;
            if (horizontal && Mathf.Abs(maximum - .08f) < .001f) return SurfaceRole.Road;
            if (horizontal && Mathf.Abs(maximum - .12f) < .001f) return SurfaceRole.Boundary;
            if (maximum >= 2.159f && minimum >= .159f)
            {
                if (horizontal && Mathf.Abs(normal.y) > .99f) return SurfaceRole.Roof;
                if (maximum - minimum >= 1.999f && Mathf.Abs(normal.y) < .01f) return SurfaceRole.Wall;
            }
            // 다른 비공개 관찰 기호는 새 표면 의미로 분류하지 않는다.
            return SurfaceRole.Unknown;
        }

        private static Color Palette(SurfaceRole role, Color source)
        {
            var variation = Mathf.Clamp(((source.r + source.g + source.b) / 3f - .67f) * .28f, -.035f, .035f);
            var color = role switch
            {
                SurfaceRole.Ground => new Color(.30f, .34f, .36f),
                SurfaceRole.Road => new Color(.12f, .16f, .19f),
                SurfaceRole.Boundary => new Color(.75f, .62f, .34f),
                SurfaceRole.Roof => new Color(.68f, .46f, .32f),
                SurfaceRole.Wall => new Color(.82f, .77f, .68f),
                _ => source
            };
            if (role == SurfaceRole.Roof || role == SurfaceRole.Wall)
                color += new Color(variation, variation, variation, 0f);
            color.a = source.a;
            return color;
        }
    }
}
