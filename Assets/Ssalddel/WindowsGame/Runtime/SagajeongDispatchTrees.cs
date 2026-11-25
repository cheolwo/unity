using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 계절 · 변화 그림 구조 E5 (2026-11-23): 동 디오라마의 나무. 빽빽한 동네에는 녹지 땅이 거의 없어서, 철은 나무로 보여야 한다.
    //  - 자리: 굽힌 동 메시의 시설물대 띠(uv.x 9 · 가로수가 서는 보도 바깥 띠)와 녹지(1 산림지 · 2 조경녹지)에서 규칙으로 고른다.
    //    서울시 가로수 자리 자료(OA-23040)는 이용조건을 제공처와 대조하기 전이라 싣지 않는다(Codex r8 읽는법). 그래서 자리 · 수종 · 크기는 모두 표현이다.
    //  - 수종(규칙 · 시험 값): 길가 = 은행 · 벚 · 느티 · 버즘 · 그 밖 · 단풍이 60 m 구간마다 한 줄로, 산 = 참나무 · 소나무 · 벚 · 단풍,
    //    공원 = 섞어서. 철의 차이(벚꽃 · 은행 노랑 · 단풍 빨강 · 소나무는 늘 푸름)는 셰이더(HongdalTree)가 EnvState 값으로 그린다.
    //  - 「계절 바탕」을 끄면 나무는 여름 잎으로 선다(_HongdalEnvC.w = 0).
    public sealed partial class SagajeongDispatchView
    {
        private Material treeMaterial;
        private Material driftMaterial;
        private int dioramaTrees, dioramaDriftLeaves;
        private int streetTreesMoved, streetTreesDropped;      // 길가 나무 자리 다듬기(보도 쪽으로 민 것 · 버린 것)
        private StreetGrid lastStreetGrid;             // 방금 읽은 동의 칸 그림(현실감 덧붙이기가 이어 쓴다, SagajeongDispatchRealism.cs)
        private long dongTreeMs;                       // 나무 만들기에 든 시간(ms, 읽기 기록)
        private readonly List<Renderer> driftRenderers = new();
        private bool driftShown;
        private static readonly int DriftSizeId = Shader.PropertyToID("_HongdalDriftSize");
        private const int TreesPerModuleCap = 9000;

        // 수종 번호(셰이더와 같다): 1 은행 · 2 벚 · 3 느티 · 4 버즘 · 5 이팝 · 6 그 밖 · 7 단풍 · 8 소나무 · 9 참나무
        private static int StreetSpecies(float r) => r < 0.34f ? 1 : r < 0.54f ? 2 : r < 0.67f ? 3 : r < 0.79f ? 4 : r < 0.92f ? 5 : 7;      // 5 이팝(2026-11-24)
        private static int ForestSpecies(float r, float cluster) => cluster > 0.62f ? 8 : r < 0.62f ? 9 : r < 0.74f ? 2 : r < 0.86f ? 7 : 3;
        private static int ParkSpecies(float r) => r < 0.25f ? 3 : r < 0.50f ? 2 : r < 0.65f ? 7 : r < 0.80f ? 8 : r < 0.90f ? 1 : 9;

        private static float TreeRandom(int a, int b, int seed) => SeasonHash(a, b, seed) / 65536f;

        private Material EnsureTreeMaterial()
        {
            if (treeMaterial != null) return treeMaterial;
            var shader = Resources.Load<Shader>("HongdalTree");
            if (shader == null) return null;
            treeMaterial = new Material(shader) { name = "Hongdal trees" };
            var drift = Resources.Load<Shader>("HongdalDrift");
            if (drift != null) driftMaterial = new Material(drift) { name = "Hongdal drifting leaves" };
            return treeMaterial;
        }

        // 동 하나의 바탕 메시에서 나무 자리를 골라 나무 메시 하나로 묶는다. 자리가 없으면 null.
        private Mesh BuildDongTrees(Mesh ground, string code, out Mesh leaves)
        {
            leaves = null;
            var vertices = ground.vertices;
            var uv = ground.uv;
            var uv2 = ground.uv2;
            var triangles = ground.triangles;
            if (uv == null || uv.Length != vertices.Length || uv2 == null || uv2.Length != vertices.Length) return null;
            var seed = 0;
            foreach (var ch in code) seed = seed * 31 + ch;
            var taken = new HashSet<long>();
            var spots = new List<(Vector3 At, int Kind)>();
            var streetPlaced = new Dictionary<long, List<Vector3>>(EdgeKeys.Instance);      // 칸 열쇠도 고르게 섞는다
            var grid = StreetGrid.Around(vertices);          // 길가 나무 자리 다듬기: 차도 · 횡단보도 · 건물 칸 그림(먼저 다 칠한다)
            lastStreetGrid = grid;
            if (grid != null)
                for (var t = 0; t + 2 < triangles.Length; t += 3)
                    grid.Mark(vertices[triangles[t]], vertices[triangles[t + 1]], vertices[triangles[t + 2]], uv[triangles[t]], uv2[triangles[t]], uv[triangles[t + 1]], uv[triangles[t + 2]]);
            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (uv2[a].x > 0.5f || vertices[a].y > 0.2f) continue;
                var tag = uv[a].x;
                int kind;
                if (Mathf.Abs(tag - 9f) < 0.3f || grid != null && Mathf.Abs(tag - 5f) < 0.3f) kind = 0;     // 길가: 시설물대 · 보도(연석선 가까이만 남긴다)
                else if (Mathf.Abs(tag - 1f) < 0.3f && vertices[a].y < 0.05f) kind = 1;
                else if (Mathf.Abs(tag - 2f) < 0.3f && vertices[a].y < 0.05f) kind = 2;
                else continue;
                if (Mathf.Abs(uv[b].x - tag) > 0.3f || Mathf.Abs(uv[c].x - tag) > 0.3f) continue;
                Vector3 pa = vertices[a], pb = vertices[b], pc = vertices[c];
                var area = 0.5f * Vector3.Cross(pb - pa, pc - pa).magnitude;
                var cell = kind == 0 ? 8f : kind == 1 ? 5.5f : 7f;
                var density = kind == 0 ? 0.3f : kind == 1 ? 0.07f : 0.05f;
                var count = Mathf.Min(48, Mathf.CeilToInt(area * density));
                for (var k = 0; k < count; k++)
                {
                    var u = TreeRandom(t, k, seed + 1);
                    var v = TreeRandom(t, k, seed + 2);
                    if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                    var at = pa + (pb - pa) * u + (pc - pa) * v;
                    if (kind == 0 && grid != null)
                    {
                        if (!grid.Settle(ref at, StreetCrown)) continue;      // 연석선에서 1.4 ~ 2.4 m 띠로
                        if (!StreetSpacing(streetPlaced, at)) continue;        // 이웃 길가 나무와 6.5 m 넘게
                        spots.Add((new Vector3(at.x, 0f, at.z), kind));
                        continue;
                    }
                    var key = ((long)Mathf.FloorToInt(at.x / cell) << 32) ^ (uint)Mathf.FloorToInt(at.z / cell) ^ ((long)(kind == 0 ? 1 : 2) << 62);
                    if (!taken.Add(key)) continue;
                    spots.Add((new Vector3(at.x, 0f, at.z), kind));
                }
            }
            if (spots.Count == 0) return null;
            var stride = Mathf.Max(1, Mathf.CeilToInt(spots.Count / (float)TreesPerModuleCap));

            var positions = new List<Vector3>(spots.Count / stride * 24);
            var normals = new List<Vector3>(positions.Capacity);
            var parts = new List<Vector2>(positions.Capacity);      // uv: (0 줄기 · 1 잎, 수종)
            var extra = new List<Vector2>(positions.Capacity);      // uv2: (난수, 자리 갈래)
            var centres = new List<Vector3>(positions.Capacity);    // uv3: 잎 덩어리 가운데(잎이 지면 이쪽으로 줄어든다)
            var indices = new List<int>(spots.Count / stride * 36);
            // 날리는 잎 · 꽃잎(2026-11-24): 길가 나무 세 장, 공원 두 장, 산은 열에 일곱꼴로 한 장. 소나무는 없다.
            var leafCorners = new List<Vector2>();
            var leafTags = new List<Vector2>();
            var leafCentres = new List<Vector3>();
            var leafColours = new List<Color32>();
            var leafIndices = new List<int>();
            var made = 0;
            for (var s = 0; s < spots.Count; s += stride)
            {
                var (at, kind) = spots[s];
                var ix = Mathf.FloorToInt(at.x);
                var iz = Mathf.FloorToInt(at.z);
                var r = TreeRandom(ix, iz, seed + 7);
                int species;
                if (kind == 0) species = StreetSpecies(TreeRandom(Mathf.FloorToInt(at.x / 60f), Mathf.FloorToInt(at.z / 60f), 31));
                else if (kind == 1) species = ForestSpecies(r, SeasonValueNoise(Mathf.FloorToInt(at.x) + 4096, Mathf.FloorToInt(at.z) + 4096, 22, 53));
                else species = ParkSpecies(r);
                var size = kind == 1 ? 1.0f + 0.45f * TreeRandom(ix, iz, seed + 9)
                         : kind == 0 ? 0.8f + 0.25f * TreeRandom(ix, iz, seed + 9)
                         : 0.85f + 0.35f * TreeRandom(ix, iz, seed + 9);
                var (crown, radius) = AddTree(at, size, species, kind, r, positions, normals, parts, extra, centres, indices);
                made++;
                if (species == 8) continue;
                var count = kind == 0 ? 3 : kind == 2 ? 2 : TreeRandom(ix, iz, seed + 13) < 0.7f ? 1 : 0;
                for (var leaf = 0; leaf < count; leaf++)
                {
                    var r1 = TreeRandom(ix + leaf * 7, iz, seed + 21);
                    var r2 = (byte)(SeasonHash(ix, iz + leaf * 5, seed + 23) >> 8);
                    var r3 = (byte)(SeasonHash(ix + leaf * 3, iz, seed + 29) >> 8);
                    var baseIndex = leafCentres.Count;
                    for (var k = 0; k < 4; k++)
                    {
                        leafCorners.Add(new Vector2(k == 1 || k == 2 ? 1f : 0f, k >= 2 ? 1f : 0f));
                        leafTags.Add(new Vector2(r1, species));
                        leafCentres.Add(crown);
                        leafColours.Add(new Color32(r2, r3, (byte)Mathf.Clamp(radius / 4f * 255f, 0, 255), 255));
                    }
                    leafIndices.Add(baseIndex); leafIndices.Add(baseIndex + 1); leafIndices.Add(baseIndex + 2);
                    leafIndices.Add(baseIndex); leafIndices.Add(baseIndex + 2); leafIndices.Add(baseIndex + 3);
                }
            }
            if (grid != null)
            {
                streetTreesMoved += grid.Moved;
                streetTreesDropped += grid.Dropped;
            }
            var mesh = new Mesh { name = "Hongdal trees " + code, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, parts);
            mesh.SetUVs(1, extra);
            mesh.SetUVs(2, centres);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            dioramaMeshes.Add(mesh);
            dioramaTrees += made;
            if (leafCentres.Count > 0)
            {
                leaves = new Mesh { name = "Hongdal drifting leaves " + code, indexFormat = IndexFormat.UInt32 };
                leaves.SetVertices(leafCentres);          // 자리만 잡아 둔다. 셰이더가 옮긴다
                leaves.SetUVs(0, leafCorners);
                leaves.SetUVs(1, leafTags);
                leaves.SetUVs(2, leafCentres);
                leaves.SetColors(leafColours);
                leaves.SetTriangles(leafIndices, 0);
                leaves.RecalculateBounds();
                var bounds = leaves.bounds;
                bounds.Expand(new Vector3(90f, 20f, 90f));      // 바람에 밀려 나무 밖으로 날아간다
                leaves.bounds = bounds;
                leaves.UploadMeshData(true);
                dioramaMeshes.Add(leaves);
                dioramaDriftLeaves += leafCentres.Count / 4;
            }
            return mesh;
        }

        // 넓은 잎 나무의 잎 덩어리 반지름(m, 크기 1일 때). 길가 나무는 가지치기한 꼴로 조금 좁다(표현).
        // 길가 나무끼리 6.5 m 넘게(8 m 칸 아홉 곳을 본다). 넘으면 그 칸에 적어 두고 true.
        private static bool StreetSpacing(Dictionary<long, List<Vector3>> placed, Vector3 at)
        {
            const float Cell = 8f, Gap = 6.5f;
            var cx = Mathf.FloorToInt(at.x / Cell);
            var cz = Mathf.FloorToInt(at.z / Cell);
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!placed.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var list)) continue;
                foreach (var p in list)
                    if ((p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z) < Gap * Gap) return false;
            }
            var key = ((long)cx << 32) ^ (uint)cz;
            if (!placed.TryGetValue(key, out var mine)) placed[key] = mine = new List<Vector3>(2);
            mine.Add(at);
            return true;
        }

        private const float StreetCrown = 2.0f;      // 길가 나무 자리를 잡을 때 쓰는 잎 반지름(m, 크기 0.8 ~ 1.05 × 1.9 ~ 2.7 × 0.82의 가운데)

        private static float CrownWide(int species, int kind) =>
            (species == 3 || species == 4 ? 2.7f : species == 1 ? 1.9f : 2.3f) * (kind == 0 ? 0.82f : 1f);

        // 나무 하나를 붙이고 잎 덩어리 가운데 · 반지름을 돌려준다(날리는 잎이 여기서 떨어진다).
        private static (Vector3 Crown, float Radius) AddTree(Vector3 at, float size, int species, int kind, float random,
            List<Vector3> positions, List<Vector3> normals, List<Vector2> parts, List<Vector2> extra, List<Vector3> centres, List<int> indices)
        {
            var pine = species == 8;
            var trunkHeight = (pine ? 1.6f : 2.3f) * size;
            var trunkRadius = 0.16f * size;
            var turn = random * Mathf.PI * 2f;
            var tag = new Vector2(random, kind);
            // 줄기: 네모 기둥(뚜껑 없음)
            var trunkBase = positions.Count;
            for (var i = 0; i < 4; i++)
            {
                var angle = turn + i * Mathf.PI * 0.5f;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                positions.Add(at + side * trunkRadius);
                positions.Add(at + side * (trunkRadius * 0.7f) + Vector3.up * trunkHeight);
                normals.Add(side);
                normals.Add(side);
                for (var k = 0; k < 2; k++) { parts.Add(new Vector2(0f, species)); extra.Add(tag); centres.Add(at); }
            }
            for (var i = 0; i < 4; i++)
            {
                int a0 = trunkBase + i * 2, a1 = a0 + 1, b0 = trunkBase + (i + 1) % 4 * 2, b1 = b0 + 1;
                indices.Add(a0); indices.Add(a1); indices.Add(b1);
                indices.Add(a0); indices.Add(b1); indices.Add(b0);
            }
            // 잎: 넓은 잎 나무는 여섯 모 두 고리의 둥근 덩어리, 소나무는 두 단의 뿔
            var crownBase = positions.Count;
            Vector3 centre;
            if (pine)
            {
                var radius = 1.7f * size;
                var height = 5.2f * size;
                var bottom = trunkHeight * 0.55f;
                centre = at + Vector3.up * (bottom + height * 0.4f);
                AddRing(at + Vector3.up * bottom, radius, 6, turn, -0.15f, species, tag, centre, positions, normals, parts, extra, centres);
                AddRing(at + Vector3.up * (bottom + height * 0.5f), radius * 0.62f, 6, turn + 0.5f, 0.35f, species, tag, centre, positions, normals, parts, extra, centres);
                AddPoint(at + Vector3.up * (bottom + height), Vector3.up, species, tag, centre, positions, normals, parts, extra, centres);
                for (var i = 0; i < 6; i++)
                {
                    int a = crownBase + i, b = crownBase + (i + 1) % 6, c = crownBase + 6 + i, d = crownBase + 6 + (i + 1) % 6;
                    indices.Add(a); indices.Add(c); indices.Add(d);
                    indices.Add(a); indices.Add(d); indices.Add(b);
                    indices.Add(c); indices.Add(crownBase + 12); indices.Add(d);
                }
                return (centre, radius);
            }
            var wide = CrownWide(species, kind) * size;
            var tall = (species == 1 ? 3.6f : 2.8f) * size;
            var low = trunkHeight * 0.8f;
            centre = at + Vector3.up * (low + tall * 0.5f);
            AddRing(at + Vector3.up * (low + tall * 0.28f), wide, 6, turn, -0.25f, species, tag, centre, positions, normals, parts, extra, centres);
            AddRing(at + Vector3.up * (low + tall * 0.72f), wide * 0.78f, 6, turn + 0.5f, 0.45f, species, tag, centre, positions, normals, parts, extra, centres);
            AddPoint(at + Vector3.up * (low + tall), Vector3.up, species, tag, centre, positions, normals, parts, extra, centres);
            AddPoint(at + Vector3.up * low, Vector3.down, species, tag, centre, positions, normals, parts, extra, centres);
            var top = crownBase + 12;
            var under = crownBase + 13;
            for (var i = 0; i < 6; i++)
            {
                int a = crownBase + i, b = crownBase + (i + 1) % 6, c = crownBase + 6 + i, d = crownBase + 6 + (i + 1) % 6;
                indices.Add(a); indices.Add(c); indices.Add(d);
                indices.Add(a); indices.Add(d); indices.Add(b);
                indices.Add(c); indices.Add(top); indices.Add(d);
                indices.Add(a); indices.Add(b); indices.Add(under);
            }
            return (centre, wide);
        }

        private static void AddRing(Vector3 middle, float radius, int sides, float turn, float lift, int species, Vector2 tag, Vector3 centre,
            List<Vector3> positions, List<Vector3> normals, List<Vector2> parts, List<Vector2> extra, List<Vector3> centres)
        {
            for (var i = 0; i < sides; i++)
            {
                var angle = turn + i * Mathf.PI * 2f / sides;
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddPoint(middle + side * radius, (side + Vector3.up * lift).normalized, species, tag, centre, positions, normals, parts, extra, centres);
            }
        }

        private static void AddPoint(Vector3 at, Vector3 normal, int species, Vector2 tag, Vector3 centre,
            List<Vector3> positions, List<Vector3> normals, List<Vector2> parts, List<Vector2> extra, List<Vector3> centres)
        {
            positions.Add(at);
            normals.Add(normal);
            parts.Add(new Vector2(1f, species));
            extra.Add(tag);
            centres.Add(centre);
        }

        // SyncEnvironment가 매 화면 부른다: 동 바탕 재질의 회색 동네 · 역 창 값을 나무 재질에도 맞춘다.
        private void SyncTreeMaterial()
        {
            if (treeMaterial == null || dioramaMaterial == null) return;
            treeMaterial.SetFloat(DioramaGreyId, dioramaMaterial.GetFloat(DioramaGreyId));
            treeMaterial.SetVector(DioramaWindowId, dioramaMaterial.GetVector(DioramaWindowId));
            if (driftMaterial != null) driftMaterial.SetVector(DioramaWindowId, dioramaMaterial.GetVector(DioramaWindowId));
        }

        // 잎 · 꽃잎이 날리는 철에만 그린다(그 밖에는 그리기 자체를 끈다).
        private void SyncDriftLeaves(EnvState env)
        {
            var show = seasonOn && (env.LeafFall > 0.02f || env.PetalFall > 0.02f);
            if (show == driftShown) return;
            driftShown = show;
            foreach (var renderer in driftRenderers)
                if (renderer != null) renderer.enabled = show;
        }

        // 화면 배율에 맞춘 잎 크기(m). LateUpdate가 부른다. halfHeight = 화면 세로 절반에 담기는 길이(m).
        private static void SyncDriftSize(float halfHeight) => Shader.SetGlobalFloat(DriftSizeId, Mathf.Clamp(0.012f * halfHeight, 0.3f, 1.8f));      // 화면에서 3~4 픽셀(표현)
            // 길가 나무 자리 다듬기(2026-11-24 02:59 사용자: 「가로수 위치가 도로 밖으로 좀 더 튀어나와 있어가지고 … 어색해 보이는 감이 없지 않아」).
        // 동 메시를 읽을 때 1 m 칸 그림에 차도(높이 0.06 m 겹) · 횡단보도 줄(갈래 6) · 건물 지붕을 칠해 둔다.
        //  - 처음 판(시설물대 아무 데나)은 나무가 차도 위에 서는 일이 많았다. 이제 시설물대 · 보도에서 뽑은 자리 가운데
        //    연석선(가장 가까운 차도 칸)에서 잎 반지름의 반 + 0.4 m ~ 거기서 1 m 더 떨어진 띠만 남긴다(8 m 칸에 하나).
        //  - 그보다 가까우면 차도에서 멀어지는 쪽으로 최대 2.5 m 민다. 민 자리가 건물에서 1 m 안이거나 차도 위면 버린다.
        //  - 횡단보도 줄에서 3.5 m 안은 심지 않는다. 차도가 5 m 안에 없으면 길가 나무가 아니다.
        //  자리는 여전히 표현이다(실제 가로수 자료 OA-23040은 이용 조건 때문에 쓰지 않는다).
        private sealed class StreetGrid
        {
            private const byte Road = 1, Crossing = 2, Building = 4;
            private float x0, z0;
            private int w, h;
            private byte[] cells;
            public int Moved, Dropped;

            public static StreetGrid Around(Vector3[] vertices)
            {
                if (vertices.Length == 0) return null;
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                foreach (var v in vertices)
                {
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z;
                    if (v.z > maxZ) maxZ = v.z;
                }
                var gw = Mathf.CeilToInt(maxX - minX) + 3;
                var gh = Mathf.CeilToInt(maxZ - minZ) + 3;
                if (gw <= 0 || gh <= 0 || (long)gw * gh > 30_000_000L) return null;
                return new StreetGrid { x0 = minX - 1f, z0 = minZ - 1f, w = gw, h = gh, cells = new byte[gw * gh] };
            }

            private byte At(int x, int z) => x < 0 || z < 0 || x >= w || z >= h ? (byte)0 : cells[z * w + x];

            public void Mark(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uv2A, Vector2 uvB, Vector2 uvC)
            {
                if (uv2A.x > 0.5f) return;                                    // 벽
                byte flag;
                if (a.y > 0.5f && b.y > 0.5f && c.y > 0.5f && uv2A.y > -0.5f && Mathf.Abs(a.y - b.y) < 0.01f && Mathf.Abs(a.y - c.y) < 0.01f) flag = Building;   // 지붕
                else if (Mathf.Abs(uvA.x - 6f) < 0.3f) flag = Crossing;       // 횡단보도 줄
                else if (a.y > 0.05f && a.y < 0.075f && b.y > 0.05f && b.y < 0.075f && c.y > 0.05f && c.y < 0.075f &&
                         Mathf.Abs(uvA.x) < 0.3f && Mathf.Abs(uvB.x) < 0.3f && Mathf.Abs(uvC.x) < 0.3f) flag = Road;   // 차도
                else return;
                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - x0));
                var maxX = Mathf.Min(w - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - x0));
                var minZ = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - z0));
                var maxZ = Mathf.Min(h - 1, Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - z0));
                var area = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
                if (Mathf.Abs(area) < 1e-6f) return;
                for (var z = minZ; z <= maxZ; z++)
                {
                    var pz = z0 + z + 0.5f;
                    for (var x = minX; x <= maxX; x++)
                    {
                        var px = x0 + x + 0.5f;
                        var w0 = ((b.x - px) * (c.z - pz) - (c.x - px) * (b.z - pz)) / area;
                        var w1 = ((c.x - px) * (a.z - pz) - (a.x - px) * (c.z - pz)) / area;
                        var w2 = 1f - w0 - w1;
                        if (w0 >= -0.02f && w1 >= -0.02f && w2 >= -0.02f) cells[z * w + x] |= flag;
                    }
                }
            }

            // 잎 반지름 crown인 길가 나무를 차도에서 떼어 놓는다. 심을 수 없으면 false.
            public bool Settle(ref Vector3 at, float crown)
            {
                const int Reach = 5;
                var cx = Mathf.FloorToInt(at.x - x0);
                var cz = Mathf.FloorToInt(at.z - z0);
                var nearest = float.MaxValue;
                var away = Vector2.zero;
                for (var dz = -Reach; dz <= Reach; dz++)
                for (var dx = -Reach; dx <= Reach; dx++)
                {
                    var f = At(cx + dx, cz + dz);
                    if (f == 0) continue;
                    var off = new Vector2(at.x - (x0 + cx + dx + 0.5f), at.z - (z0 + cz + dz + 0.5f));
                    var d = off.magnitude;
                    if ((f & Crossing) != 0 && d <= 3.5f) return false;
                    if ((f & Road) == 0 || d > Reach) continue;
                    if (d < nearest) nearest = d;
                    away += off / Mathf.Max(0.25f, d * d);
                }
                var need = crown * 0.5f + 0.4f;
                if (nearest > need + 1.0f) { Dropped++; return false; }      // 차도에서 먼 보도 안쪽 · 광장: 길가 나무가 아니다
                if (nearest >= need) { if (Clear(at)) return true; Dropped++; return false; }
                if (away.sqrMagnitude < 1e-8f) return false;
                away.Normalize();
                var next = at + new Vector3(away.x, 0f, away.y) * Mathf.Min(need - nearest, 2.5f);
                if (!Clear(next)) { Dropped++; return false; }
                at = next;
                Moved++;
                return true;
            }

            // 가장 가까운 횡단보도 줄 칸까지의 거리(m). reach 칸 안에 없으면 큰 값.
            public float CrossingDistance(Vector3 p, int reach = 4)
            {
                var cx = Mathf.FloorToInt(p.x - x0);
                var cz = Mathf.FloorToInt(p.z - z0);
                var best = 99f;
                for (var dz = -reach; dz <= reach; dz++)
                for (var dx = -reach; dx <= reach; dx++)
                {
                    if ((At(cx + dx, cz + dz) & Crossing) == 0) continue;
                    var ox = p.x - Mathf.Clamp(p.x, x0 + cx + dx, x0 + cx + dx + 1f);
                    var oz = p.z - Mathf.Clamp(p.z, z0 + cz + dz, z0 + cz + dz + 1f);
                    best = Mathf.Min(best, Mathf.Sqrt(ox * ox + oz * oz));
                }
                return best;
            }

            // 보안등 기둥 자리: 그 칸이 차도 · 횡단보도 · 건물이면 4 m 안에서 가장 가까운 빈 칸으로 옮긴다.
            //  arm = 등을 내미는 쪽(가까운 차도 쪽, 없으면 가까운 건물의 반대쪽). 빈 칸이 없으면 false.
            public bool PoleSpot(ref Vector2 p, out Vector2 arm)
            {
                const int Reach = 4;
                arm = Vector2.right;
                var cx = Mathf.FloorToInt(p.x - x0);
                var cz = Mathf.FloorToInt(p.y - z0);
                var best = int.MaxValue;
                int bx = 0, bz = 0;
                for (var dz = -Reach; dz <= Reach; dz++)
                for (var dx = -Reach; dx <= Reach; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 1 || z < 1 || x >= w - 1 || z >= h - 1 || At(x, z) != 0) continue;
                    if (((At(x + 1, z) | At(x - 1, z) | At(x, z + 1) | At(x, z - 1)) & Building) != 0) continue;   // 벽에 붙지 않게
                    var d = dx * dx + dz * dz;
                    if (d >= best) continue;
                    best = d;
                    bx = dx;
                    bz = dz;
                }
                if (best == int.MaxValue) return false;
                if (best > 0 || At(cx, cz) != 0) p = new Vector2(x0 + cx + bx + 0.5f, z0 + cz + bz + 0.5f);
                cx += bx;
                cz += bz;
                Vector2 road = Vector2.zero, wall = Vector2.zero;
                float roadD = float.MaxValue, wallD = float.MaxValue;
                for (var dz = -6; dz <= 6; dz++)
                for (var dx = -6; dx <= 6; dx++)
                {
                    var f = At(cx + dx, cz + dz);
                    if (f == 0) continue;
                    var d = dx * dx + dz * dz;
                    if ((f & Road) != 0 && d < roadD) { roadD = d; road = new Vector2(dx, dz); }
                    if ((f & Building) != 0 && d < wallD) { wallD = d; wall = new Vector2(dx, dz); }
                }
                if (roadD < float.MaxValue && road.sqrMagnitude > 0f) arm = road.normalized;
                else if (wallD < float.MaxValue && wall.sqrMagnitude > 0f) arm = -wall.normalized;
                return true;
            }

            // 차도 칸을 2 m 칸 열쇠로 모은다(큰길의 차가 차도 위에만 서게, SagajeongDispatchRealism.cs SyncTraffic)
            public void CollectRoads(HashSet<long> into)
            {
                for (var z = 0; z < h; z++)
                for (var x = 0; x < w; x++)
                {
                    if ((cells[z * w + x] & Road) == 0) continue;
                    into.Add(RoadKey(x0 + x + 0.5f, z0 + z + 0.5f));
                }
            }

            // 공통 원점에서 radius 안의 건물 칸을 2 m 칸 열쇠로 모은다(역 앞 사람이 건물 안에 서지 않게)
            public void CollectBuildings(HashSet<long> into, float radius)
            {
                for (var z = 0; z < h; z++)
                {
                    var pz = z0 + z + 0.5f;
                    if (Mathf.Abs(pz) > radius) continue;
                    for (var x = 0; x < w; x++)
                    {
                        if ((cells[z * w + x] & Building) == 0) continue;
                        var px = x0 + x + 0.5f;
                        if (px * px + pz * pz > radius * radius) continue;
                        into.Add(RoadKey(px, pz));
                    }
                }
            }

            public static long RoadKey(float x, float z) => ((long)Mathf.FloorToInt(x * 0.5f) << 32) ^ (uint)Mathf.FloorToInt(z * 0.5f);

            // 차도도 건물도 아닌 칸인가(골목 주차 자리)
            public bool Open(float x, float z) => (At(Mathf.FloorToInt(x - x0), Mathf.FloorToInt(z - z0)) & (Road | Building | Crossing)) == 0;

            public bool RoadAt(float x, float z) => (At(Mathf.FloorToInt(x - x0), Mathf.FloorToInt(z - z0)) & Road) != 0;

            // 건물에서 0.8 m 넘게 떨어지고 차도 위가 아닌 자리인가
            private bool Clear(Vector3 p)
            {
                var cx = Mathf.FloorToInt(p.x - x0);
                var cz = Mathf.FloorToInt(p.z - z0);
                if ((At(cx, cz) & (Road | Building)) != 0) return false;
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((At(cx + dx, cz + dz) & Building) == 0) continue;
                    var ox = p.x - Mathf.Clamp(p.x, x0 + cx + dx, x0 + cx + dx + 1f);
                    var oz = p.z - Mathf.Clamp(p.z, z0 + cz + dz, z0 + cz + dz + 1f);
                    if (ox * ox + oz * oz < 1.0f) return false;
                }
                return true;
            }
        }
    }
}
