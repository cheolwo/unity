using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>기존 공통 모형의 표면 판독용 사본. 실제 난간·높이·보도 자료를 생성하지 않는다.</summary>
    public static class 사가정공통표면깊이Style
    {
        public const string Revision = "private-sagajeong-common-surface-depth.r1";
        public const float RoofRibbonWidth = .14f;
        public const float RoadFanLift = .003f;
        public const float RoofRibbonLift = .018f;
        private static readonly HashSet<MeshFilter> ActiveFilters = new();

        [Serializable] public sealed class Report
        {
            public string revision = Revision;
            public int selectedRendererCount, excludedRendererCount, sourceMeshCount, candidateMeshCount;
            public int roofTriangleCount, roofBoundaryEdgeCount, excludedInternalRoofEdgeCount, roofComponentCount;
            public int wallBaseVertexCount, roadFanCount, roadFanVertexCount, roadFan12Count, roadFan8Count;
            public int addedRendererCount, addedVertexCount, addedTriangleCount;
            public bool originalMeshDataPreserved, originalMaterialReferencesPreserved, sourceReferencesRestored;
            public bool candidateIndexAndXzPreserved = true, candidateRoofAndWallPositionsPreserved = true;
            public bool disposed, actualParapetClaimed, actualRoadWidthChanged, canonicalApplied;
        }

        private sealed class Edge
        {
            public int A, B, Third, Count;
        }
        private sealed class Analysis
        {
            public Mesh Source, Candidate;
            public Vector3[] Vertices;
            public Vector2[] Surface;
            public int[] Triangles;
            public List<Edge> Boundary = new();
            public int RoofTriangles, InternalEdges, Components, WallVertices, FanCount, FanVertices, Fan12, Fan8;
            public string Fingerprint;
        }
        private sealed class State
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material[] Materials;
            public Analysis Analysis;
        }

        public sealed class Scope : IDisposable
        {
            private readonly List<State> states = new();
            private readonly List<Object> owned = new();
            private GameObject ribbonRoot;
            private bool committed;
            public Report Result { get; } = new();

            internal Scope(GameObject commonRoot)
            {
                if (commonRoot == null) throw new ArgumentNullException(nameof(commonRoot));
                var all = commonRoot.GetComponentsInChildren<MeshRenderer>(true);
                var selected = all.Where(r => r.enabled && r.gameObject.activeInHierarchy
                    && r.name.EndsWith(" · 불투명", StringComparison.Ordinal)).ToArray();
                Result.excludedRendererCount = all.Length - selected.Length;
                if (selected.Length == 0) throw new InvalidOperationException("SurfaceDepthCommonOpaqueBatchRequired");
                var cache = new Dictionary<Mesh, Analysis>();
                try
                {
                    // 뒤쪽 입력도 먼저 검증한다. 원본 참조 교체는 모든 사본 준비 뒤 한 번에 한다.
                    foreach (var renderer in selected)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                            throw new InvalidOperationException("SurfaceDepthReadableMeshRequired");
                        if (ActiveFilters.Contains(filter)) throw new InvalidOperationException("SurfaceDepthOverlappingScope");
                        if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterials[0] == null)
                            throw new InvalidOperationException("SurfaceDepthSingleMaterialRequired");
                        if (!cache.TryGetValue(filter.sharedMesh, out var analysis))
                        {
                            analysis = Analyze(filter.sharedMesh);
                            cache.Add(filter.sharedMesh, analysis);
                            owned.Add(analysis.Candidate);
                        }
                        states.Add(new State { Filter = filter, Renderer = renderer, Materials = renderer.sharedMaterials, Analysis = analysis });
                    }
                    var vertices = new List<Vector3>(); var triangles = new List<int>();
                    foreach (var state in states)
                    {
                        var a = state.Analysis;
                        Result.roofTriangleCount += a.RoofTriangles;
                        Result.roofBoundaryEdgeCount += a.Boundary.Count;
                        Result.excludedInternalRoofEdgeCount += a.InternalEdges;
                        Result.roofComponentCount += a.Components;
                        Result.wallBaseVertexCount += a.WallVertices;
                        Result.roadFanCount += a.FanCount; Result.roadFanVertexCount += a.FanVertices;
                        Result.roadFan12Count += a.Fan12; Result.roadFan8Count += a.Fan8;
                        foreach (var edge in a.Boundary)
                            AddRibbon(state.Filter.transform, commonRoot.transform, a.Vertices, edge, vertices, triangles);
                    }
                    if (vertices.Count == 0) throw new InvalidOperationException("SurfaceDepthRoofBoundaryRequired");
                    ribbonRoot = new GameObject("공통 지붕 경계 명암띠 · 실측 난간 아님") { hideFlags = HideFlags.DontSave };
                    ribbonRoot.transform.SetParent(commonRoot.transform, false);
                    ribbonRoot.layer = selected[0].gameObject.layer;
                    var mesh = new Mesh { name = "공통 지붕 경계 명암띠", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                    owned.Add(mesh); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    ribbonRoot.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) throw new InvalidOperationException("SurfaceDepthRibbonShaderRequired");
                    var material = new Material(shader) { name = "표면 깊이 평면 명암", hideFlags = HideFlags.DontSave, renderQueue = 3000 };
                    owned.Add(material);
                    material.SetColor("_BaseColor", new Color(.12f, .17f, .20f, .36f));
                    material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_Cull", (float)CullMode.Off);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    var ribbonRenderer = ribbonRoot.AddComponent<MeshRenderer>(); ribbonRenderer.sharedMaterial = material;
                    ribbonRenderer.shadowCastingMode = ShadowCastingMode.Off; ribbonRenderer.receiveShadows = false;
                    foreach (var state in states) { state.Filter.sharedMesh = state.Analysis.Candidate; ActiveFilters.Add(state.Filter); }
                    committed = true;
                    Result.selectedRendererCount = states.Count; Result.sourceMeshCount = cache.Count; Result.candidateMeshCount = cache.Count;
                    Result.addedRendererCount = 1; Result.addedVertexCount = vertices.Count; Result.addedTriangleCount = triangles.Count / 3;
                    Result.originalMeshDataPreserved = cache.Values.All(a => Fingerprint(a.Source) == a.Fingerprint);
                    Result.originalMaterialReferencesPreserved = states.All(s => s.Renderer.sharedMaterials.SequenceEqual(s.Materials));
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                if (Result.disposed) return;
                foreach (var state in states)
                {
                    if (committed && state.Filter != null && state.Filter.sharedMesh == state.Analysis.Candidate)
                        state.Filter.sharedMesh = state.Analysis.Source;
                    ActiveFilters.Remove(state.Filter);
                }
                if (ribbonRoot != null) Object.DestroyImmediate(ribbonRoot);
                foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
                Result.originalMeshDataPreserved = states.All(s => Fingerprint(s.Analysis.Source) == s.Analysis.Fingerprint);
                Result.originalMaterialReferencesPreserved = states.All(s => s.Renderer == null || s.Renderer.sharedMaterials.SequenceEqual(s.Materials));
                Result.sourceReferencesRestored = states.All(s => s.Filter == null || s.Filter.sharedMesh == s.Analysis.Source);
                Result.disposed = true;
            }
        }

        public static Scope Apply(GameObject commonRoot) => new(commonRoot);

        private static Analysis Analyze(Mesh source)
        {
            var v = source.vertices; var surface = new List<Vector2>(); source.GetUVs(1, surface);
            var colors = source.colors; var triangles = source.triangles;
            if (source.subMeshCount != 1 || source.GetTopology(0) != MeshTopology.Triangles || surface.Count != v.Length || colors.Length != v.Length)
                throw new InvalidOperationException("SurfaceDepthSurfaceStreamRequired");
            if (triangles.Length % 3 != 0 || triangles.Any(i => i < 0 || i >= v.Length)
                || v.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)))
                throw new InvalidOperationException("SurfaceDepthMeshDataInvalid");
            var a = new Analysis { Source = source, Vertices = v, Surface = surface.ToArray(), Triangles = triangles, Fingerprint = Fingerprint(source) };
            var edges = new Dictionary<(int, int), Edge>(); var parent = new Dictionary<int, int>();
            int Find(int n) { if (!parent.ContainsKey(n)) parent[n] = n; return parent[n] == n ? n : parent[n] = Find(parent[n]); }
            void Join(int x, int y) { var px = Find(x); var py = Find(y); if (px != py) parent[py] = px; }
            void Edge(int x, int y, int third)
            {
                var key = x < y ? (x, y) : (y, x);
                if (!edges.TryGetValue(key, out var edge)) edges[key] = edge = new Edge { A = x, B = y, Third = third };
                edge.Count++;
            }
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var x = triangles[i]; var y = triangles[i + 1]; var z = triangles[i + 2];
                if (!Role(surface[x], 2) || !Role(surface[y], 2) || !Role(surface[z], 2)) continue;
                if (Mathf.Max(v[x].y, v[y].y, v[z].y) - Mathf.Min(v[x].y, v[y].y, v[z].y) > .001f)
                    continue;
                a.RoofTriangles++; Join(x, y); Join(y, z); Edge(x, y, z); Edge(y, z, x); Edge(z, x, y);
            }
            if (edges.Values.Any(e => e.Count > 2)) throw new InvalidOperationException("SurfaceDepthNonManifoldRoofRejected");
            a.Boundary = edges.Values.Where(e => e.Count == 1).ToList(); a.InternalEdges = edges.Values.Count(e => e.Count == 2);
            a.Components = parent.Keys.ToArray().Select(Find).Distinct().Count();
            var candidateVertices = (Vector3[])v.Clone(); var candidateColors = (Color[])colors.Clone();
            for (var i = 0; i < v.Length; i++)
                if (Role(surface[i], 1) && v[i].y <= .17f)
                { candidateColors[i] = colors[i] * new Color(.9f, .9f, .9f, 1); a.WallVertices++; }
            RaiseVerifiedRoadFans(v, surface, triangles, candidateVertices, a);
            var candidate = Object.Instantiate(source); candidate.name = source.name + " · 표면깊이 사본"; candidate.hideFlags = HideFlags.DontSave;
            candidate.vertices = candidateVertices; candidate.colors = candidateColors;
            a.Candidate = candidate;
            return a;
        }

        private static bool Role(Vector2 data, float role) => Mathf.Abs(data.x - role) < .0001f;

        private static void RaiseVerifiedRoadFans(Vector3[] v, List<Vector2> surface, int[] triangles, Vector3[] candidate, Analysis report)
        {
            var uses = new Dictionary<int, List<int>>();
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var center = triangles[i];
                if (!uses.TryGetValue(center, out var group)) uses[center] = group = new List<int>();
                group.Add(i);
            }
            var lifted = new HashSet<int>();
            foreach (var group in uses)
            {
                var count = group.Value.Count;
                if (count != 8 && count != 12) continue;
                var center = v[group.Key];
                if (!(count == 12 && (Mathf.Abs(center.y - .07f) < .0001f || Mathf.Abs(center.y - .10f) < .0001f)
                    || count == 8 && Mathf.Abs(center.y - .125f) < .0001f)) continue;
                if (!Role(surface[group.Key], 0)) continue;
                var indices = new HashSet<int> { group.Key }; var valid = true; float radius = -1;
                for (var j = 0; j < count; j++)
                {
                    var at = group.Value[j]; var b = triangles[at + 1]; var c = triangles[at + 2];
                    if (!Role(surface[b], 0) || !Role(surface[c], 0) || Mathf.Abs(v[b].y - center.y) > .0001f || Mathf.Abs(v[c].y - center.y) > .0001f) { valid = false; break; }
                    var distance = Vector3.Distance(center, v[b]); if (radius < 0) radius = distance;
                    if (radius <= .01f || Mathf.Abs(distance - radius) > .0005f || Mathf.Abs(Vector3.Distance(center, v[c]) - radius) > .0005f) { valid = false; break; }
                    var next = triangles[group.Value[(j + 1) % count] + 1];
                    if ((v[c] - v[next]).sqrMagnitude > .0000001f) { valid = false; break; }
                    indices.Add(b); indices.Add(c);
                }
                if (!valid) continue;
                // 각 vertex가 이 fan 밖의 triangle에도 쓰이면 원본 strip를 건드릴 수 있어 제외한다.
                var groupTriangles = new HashSet<int>(group.Value);
                for (var at = 0; at < triangles.Length && valid; at += 3)
                    if (!groupTriangles.Contains(at) && (indices.Contains(triangles[at]) || indices.Contains(triangles[at + 1]) || indices.Contains(triangles[at + 2]))) valid = false;
                if (!valid || indices.Overlaps(lifted)) continue;
                foreach (var index in indices) { candidate[index].y += RoadFanLift; lifted.Add(index); }
                report.FanCount++; if (count == 12) report.Fan12++; if (count == 8) report.Fan8++;
            }
            report.FanVertices = lifted.Count;
        }

        private static void AddRibbon(Transform source, Transform target, Vector3[] v, Edge edge, List<Vector3> vertices, List<int> triangles)
        {
            var a = target.InverseTransformPoint(source.TransformPoint(v[edge.A]));
            var b = target.InverseTransformPoint(source.TransformPoint(v[edge.B]));
            var third = target.InverseTransformPoint(source.TransformPoint(v[edge.Third]));
            var direction = b - a; direction.y = 0;
            if (direction.sqrMagnitude < .000001f) return;
            var inward = Vector3.Cross(Vector3.up, direction.normalized);
            if (Vector3.Dot(inward, third - (a + b) * .5f) < 0) inward = -inward;
            var width = Mathf.Min(RoofRibbonWidth, direction.magnitude * .2f);
            var altitude = Vector3.Dot(inward, third - a);
            if (altitude <= .00001f) return;
            // 경계 owner triangle 쪽으로만 좁힌다. 뾰족한 모서리에서도 원래 footprint 밖으로 나가지 않는다.
            var amount = Mathf.Min(.25f, width / altitude);
            a.y += RoofRibbonLift; b.y += RoofRibbonLift; third.y += RoofRibbonLift;
            var first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(Vector3.Lerp(b, third, amount)); vertices.Add(Vector3.Lerp(a, third, amount));
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        public static string Fingerprint(Mesh mesh)
        {
            if (mesh == null) return "Missing";
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount); writer.Write((int)mesh.indexFormat);
            foreach (var p in mesh.vertices) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            foreach (var p in mesh.normals) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
            foreach (var p in mesh.colors) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); writer.Write(p.a); }
            for (var channel = 0; channel < 8; channel++) { var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count); foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); } }
            foreach (var t in mesh.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
            for (var s = 0; s < mesh.subMeshCount; s++) { writer.Write((int)mesh.GetTopology(s)); foreach (var index in mesh.GetIndices(s)) writer.Write(index); }
            var bounds = mesh.bounds; writer.Write(bounds.center.x); writer.Write(bounds.center.y); writer.Write(bounds.center.z); writer.Write(bounds.size.x); writer.Write(bounds.size.y); writer.Write(bounds.size.z);
            writer.Flush(); using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
}
