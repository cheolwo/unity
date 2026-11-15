using System;
using System.Collections.Generic;
using System.IO;

namespace Ssalddel.WindowsGame
{
    // 길 그래프와 길찾기, 기사 순찰 (R1 · R2 · R4, 2026-11-14).
    // 사용자 방향: 고용한 기사가 지도의 큰길과 건물 사이 골목을 계속 돌아다니는 모습을 보고 싶다. 출입구는 우선 가상 출입구로 한다.
    // 여기는 순수 규칙이다. 화면도 커널도 모른다. 게임 상태를 읽지도 바꾸지도 않는다.
    //  - 그래프는 굽는 도구(Tools~/make_dong_modules.py)가 낸 Resources/hongdal-dong-routes.bytes(HRTE v1)다.
    //    큰길은 건물 사이에 맞춘 차도의 가운데, 골목은 건물 배치에서 계산한 가운데 선이다. 도로 자료가 아니라 표현값이다.
    //  - 건물마다의 출입구는 「가상 출입구」다: 그 건물에서 가장 가까운 길 위의 자리. 공식 출입구 자료가 아니다.
    internal sealed class 길그래프
    {
        public const byte Road = 0, Alley = 1, Link = 2;

        public float[] X = Array.Empty<float>(), Z = Array.Empty<float>();     // 마디
        public int[] A = Array.Empty<int>(), B = Array.Empty<int>();           // 변의 두 끝
        public byte[] Kind = Array.Empty<byte>();                              // 변의 종류: 큰길 · 골목 · 이음
        public int[] DoorEdge = Array.Empty<int>();                            // 건물마다: 출입구가 닿는 변(-1이면 없음)
        public float[] DoorT = Array.Empty<float>();                           // 그 변 위의 자리(0~1)
        public float[] DoorX = Array.Empty<float>(), DoorZ = Array.Empty<float>();   // 건물 쪽 문 자리

        private int[] first = Array.Empty<int>(), nearEdge = Array.Empty<int>();     // 마디마다 닿는 변
        private float[] cost = Array.Empty<float>();                           // 길찾기에 쓰는 자리 (한 번에 한 길만 찾는다)
        private int[] cameEdge = Array.Empty<int>(), stamp = Array.Empty<int>();
        private int query;
        private float[] heapCost = new float[64];
        private int[] heapNode = new int[64];
        private int heapCount;

        public int Nodes => X.Length;
        public int Edges => A.Length;
        public int Buildings => DoorEdge.Length;
        public bool HasDoor(int building) => building >= 0 && building < DoorEdge.Length && DoorEdge[building] >= 0;

        public float Length(int edge)
        {
            var dx = X[A[edge]] - X[B[edge]];
            var dz = Z[A[edge]] - Z[B[edge]];
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public (float X, float Z) Along(int edge, float t) =>
            (X[A[edge]] + (X[B[edge]] - X[A[edge]]) * t, Z[A[edge]] + (Z[B[edge]] - Z[A[edge]]) * t);

        public static 길그래프 From(float[] x, float[] z, int[] a, int[] b, byte[] kind, int[] doorEdge, float[] doorT, float[] doorX,
            float[] doorZ, out string problem)
        {
            problem = null;
            if (x == null || z == null || a == null || b == null || kind == null || doorEdge == null || doorT == null || doorX == null ||
                doorZ == null || x.Length != z.Length || a.Length != b.Length || a.Length != kind.Length ||
                doorEdge.Length != doorT.Length || doorEdge.Length != doorX.Length || doorEdge.Length != doorZ.Length)
            {
                problem = "길 그래프의 배열 길이가 맞지 않습니다.";
                return null;
            }
            for (var edge = 0; edge < a.Length; edge++)
            {
                if (a[edge] >= 0 && a[edge] < x.Length && b[edge] >= 0 && b[edge] < x.Length && a[edge] != b[edge]) continue;
                problem = "길 그래프의 변 " + edge + "이(가) 없는 마디를 가리킵니다.";
                return null;
            }
            for (var building = 0; building < doorEdge.Length; building++)
            {
                if (doorEdge[building] < a.Length && (doorEdge[building] < 0 || (doorT[building] >= 0f && doorT[building] <= 1f))) continue;
                problem = "건물 " + building + "의 출입구가 없는 변을 가리킵니다.";
                return null;
            }
            var graph = new 길그래프 { X = x, Z = z, A = a, B = b, Kind = kind, DoorEdge = doorEdge, DoorT = doorT, DoorX = doorX, DoorZ = doorZ };
            graph.first = new int[x.Length + 1];
            for (var edge = 0; edge < a.Length; edge++)
            {
                graph.first[a[edge] + 1]++;
                graph.first[b[edge] + 1]++;
            }
            for (var node = 0; node < x.Length; node++) graph.first[node + 1] += graph.first[node];
            graph.nearEdge = new int[a.Length * 2];
            var fill = new int[x.Length];
            for (var edge = 0; edge < a.Length; edge++)
            {
                graph.nearEdge[graph.first[a[edge]] + fill[a[edge]]++] = edge;
                graph.nearEdge[graph.first[b[edge]] + fill[b[edge]]++] = edge;
            }
            graph.cost = new float[x.Length];
            graph.cameEdge = new int[x.Length];
            graph.stamp = new int[x.Length];
            return graph;
        }

        // HRTE v1: 'HRTE', 판, 원점(위도·경도)과 1도의 미터, 마디, 변, 건물의 출입구.
        public static 길그래프 Read(byte[] bytes, out string problem)
        {
            problem = null;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes ?? Array.Empty<byte>()));
                if (reader.ReadByte() != 'H' || reader.ReadByte() != 'R' || reader.ReadByte() != 'T' || reader.ReadByte() != 'E' ||
                    reader.ReadInt32() != 1)
                {
                    problem = "길 그래프의 형식이 다릅니다.";
                    return null;
                }
                for (var k = 0; k < 4; k++) reader.ReadDouble();
                var nodes = reader.ReadInt32();
                if (nodes < 0 || nodes > 2000000) { problem = "길 그래프의 마디 수가 이상합니다."; return null; }
                var x = new float[nodes];
                var z = new float[nodes];
                for (var k = 0; k < nodes; k++) { x[k] = reader.ReadSingle(); z[k] = reader.ReadSingle(); }
                var edges = reader.ReadInt32();
                if (edges < 0 || edges > 4000000) { problem = "길 그래프의 변 수가 이상합니다."; return null; }
                var a = new int[edges];
                var b = new int[edges];
                for (var k = 0; k < edges; k++) { a[k] = reader.ReadInt32(); b[k] = reader.ReadInt32(); }
                var kind = reader.ReadBytes(edges);
                var buildings = reader.ReadInt32();
                if (buildings < 0 || buildings > 2000000 || kind.Length != edges) { problem = "길 그래프의 건물 수가 이상합니다."; return null; }
                var doorEdge = new int[buildings];
                var doorT = new float[buildings];
                var doorX = new float[buildings];
                var doorZ = new float[buildings];
                for (var k = 0; k < buildings; k++) doorEdge[k] = reader.ReadInt32();
                for (var k = 0; k < buildings; k++) doorT[k] = reader.ReadSingle();
                for (var k = 0; k < buildings; k++) { doorX[k] = reader.ReadSingle(); doorZ[k] = reader.ReadSingle(); }
                return From(x, z, a, b, kind, doorEdge, doorT, doorX, doorZ, out problem);
            }
            catch (Exception)
            {
                problem = "길 그래프를 끝까지 읽지 못했습니다.";
                return null;
            }
        }

        // 변 위의 한 자리에서 다른 변 위의 한 자리까지 가장 빨리 가는 길. path의 각 점은 (자리, 그 점으로 들어오는 구간의 종류).
        internal bool Find(int fromEdge, float fromT, int toEdge, float toT, List<(float X, float Z, byte Kind)> path, out float metres)
        {
            path.Clear();
            metres = 0f;
            if (fromEdge < 0 || toEdge < 0 || fromEdge >= Edges || toEdge >= Edges) return false;
            var begin = Along(fromEdge, fromT);
            var end = Along(toEdge, toT);
            path.Add((begin.X, begin.Z, Kind[fromEdge]));
            if (fromEdge == toEdge)
            {
                Step(path, end.X, end.Z, Kind[toEdge], ref metres);
                return true;
            }
            query++;
            heapCount = 0;
            var fromLength = Length(fromEdge);
            var toLength = Length(toEdge);
            Offer(A[fromEdge], fromT * fromLength / 길찾기Policy.Speed(Kind[fromEdge]), -1);
            Offer(B[fromEdge], (1f - fromT) * fromLength / 길찾기Policy.Speed(Kind[fromEdge]), -1);
            var best = float.MaxValue;
            var bestNode = -1;
            while (heapCount > 0)
            {
                var reached = heapCost[0];
                var node = heapNode[0];
                Take();
                if (reached > cost[node]) continue;          // 더 싼 길이 이미 지나간 낡은 줄
                if (reached >= best) break;
                if (node == A[toEdge] || node == B[toEdge])
                {
                    var tail = (node == A[toEdge] ? toT : 1f - toT) * toLength / 길찾기Policy.Speed(Kind[toEdge]);
                    if (reached + tail < best) { best = reached + tail; bestNode = node; }
                }
                for (var k = first[node]; k < first[node + 1]; k++)
                {
                    var edge = nearEdge[k];
                    Offer(A[edge] == node ? B[edge] : A[edge], reached + Length(edge) / 길찾기Policy.Speed(Kind[edge]), edge);
                }
            }
            if (bestNode < 0) { path.Clear(); return false; }
            var back = new List<int> { bestNode };
            while (cameEdge[back[back.Count - 1]] >= 0)
            {
                var at = back[back.Count - 1];
                var edge = cameEdge[at];
                back.Add(A[edge] == at ? B[edge] : A[edge]);
            }
            for (var k = back.Count - 1; k >= 0; k--)
            {
                var node = back[k];
                Step(path, X[node], Z[node], k == back.Count - 1 ? Kind[fromEdge] : Kind[cameEdge[node]], ref metres);
            }
            Step(path, end.X, end.Z, Kind[toEdge], ref metres);
            return true;
        }

        private static void Step(List<(float X, float Z, byte Kind)> path, float x, float z, byte kind, ref float metres)
        {
            var last = path[path.Count - 1];
            var length = (float)Math.Sqrt((x - last.X) * (x - last.X) + (z - last.Z) * (z - last.Z));
            if (length < 0.0001f) return;                    // 같은 자리는 다시 넣지 않는다
            path.Add((x, z, kind));
            metres += length;
        }

        private void Offer(int node, float reached, int via)
        {
            if (stamp[node] != query) { stamp[node] = query; cost[node] = float.MaxValue; cameEdge[node] = -1; }
            if (reached >= cost[node]) return;
            cost[node] = reached;
            cameEdge[node] = via;
            if (heapCount == heapCost.Length)
            {
                Array.Resize(ref heapCost, heapCount * 2);
                Array.Resize(ref heapNode, heapCount * 2);
            }
            var at = heapCount++;
            while (at > 0)
            {
                var up = (at - 1) / 2;
                if (heapCost[up] <= reached) break;
                heapCost[at] = heapCost[up];
                heapNode[at] = heapNode[up];
                at = up;
            }
            heapCost[at] = reached;
            heapNode[at] = node;
        }

        private void Take()
        {
            heapCount--;
            if (heapCount == 0) return;
            var lastCost = heapCost[heapCount];
            var lastNode = heapNode[heapCount];
            var at = 0;
            while (true)
            {
                var down = at * 2 + 1;
                if (down >= heapCount) break;
                if (down + 1 < heapCount && heapCost[down + 1] < heapCost[down]) down++;
                if (heapCost[down] >= lastCost) break;
                heapCost[at] = heapCost[down];
                heapNode[at] = heapNode[down];
                at = down;
            }
            heapCost[at] = lastCost;
            heapNode[at] = lastNode;
        }
    }

    internal static class 길찾기Policy
    {
        public const float RoadSpeed = 8.3f;      // 큰길에서의 빠르기 (30 km/h, 표현값)
        public const float AlleySpeed = 4.2f;     // 골목과 이음에서의 빠르기 (15 km/h, 표현값)

        public static float Speed(byte kind) => kind == 길그래프.Road ? RoadSpeed : AlleySpeed;

        // 건물의 가상 출입구에서 다른 건물의 가상 출입구까지 가장 빨리 가는 길.
        public static bool Route(길그래프 graph, int from, int to, List<(float X, float Z, byte Kind)> path, out float metres)
        {
            metres = 0f;
            if (graph == null || !graph.HasDoor(from) || !graph.HasDoor(to))
            {
                path.Clear();
                return false;
            }
            return graph.Find(graph.DoorEdge[from], graph.DoorT[from], graph.DoorEdge[to], graph.DoorT[to], path, out metres);
        }
    }

    // 기사 순찰: 길을 따라 나아가기와, 다음 갈 곳을 고르는 차례. 같은 씨앗이면 같은 차례가 나온다.
    internal static class 기사순찰Policy
    {
        public const float Pace = 3f;             // 보기 좋게 실제보다 빠르게 움직인다 (표현값)
        public const float PickupWait = 2.5f;     // 픽업지에서 멈춰 있는 시간 (초, 표현값)
        public const float DropWait = 2f;         // 전달지에서 멈춰 있는 시간 (초, 표현값)

        public static uint Seed(string region, int index)
        {
            var hash = 2166136261u;
            foreach (var letter in region ?? string.Empty) hash = (hash ^ letter) * 16777619u;
            hash = (hash ^ (uint)index) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        public static uint Next(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        public static int Pick(ref uint seed, int count) => count <= 0 ? -1 : (int)(Next(ref seed) % (uint)count);

        // 길을 따라 seconds만큼 나아간다. 큰길과 골목의 빠르기가 다르다. 돌려주는 값: 끝에 닿았는가.
        public static bool Advance(IReadOnlyList<(float X, float Z, byte Kind)> path, ref int leg, ref float along, float seconds,
            out float x, out float z, out float heading)
        {
            x = z = heading = 0f;
            if (path == null || path.Count == 0) return true;
            leg = Math.Max(0, leg);
            while (leg < path.Count - 1)
            {
                var from = path[leg];
                var to = path[leg + 1];
                var dx = to.X - from.X;
                var dz = to.Z - from.Z;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                var speed = 길찾기Policy.Speed(to.Kind);
                var room = Math.Max(0f, length - along);
                if (seconds * speed < room || seconds <= 0f)
                {
                    along += Math.Max(0f, seconds) * speed;
                    var share = length < 0.0001f ? 0f : along / length;
                    x = from.X + dx * share;
                    z = from.Z + dz * share;
                    heading = (float)Math.Atan2(dz, dx);
                    return false;
                }
                seconds -= room / speed;
                leg++;
                along = 0f;
            }
            var last = path[path.Count - 1];
            x = last.X;
            z = last.Z;
            if (path.Count > 1)
            {
                var before = path[path.Count - 2];
                heading = (float)Math.Atan2(last.Z - before.Z, last.X - before.X);
            }
            return true;
        }
    }
}
