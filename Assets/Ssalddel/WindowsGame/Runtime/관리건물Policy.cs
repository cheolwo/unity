using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 회색 동네와 관리 건물 (기획 r143 첫 걸음, 2026-11-14).
    // 사용자 방향: 동 지도에서 어디에 집중할지 알 수 있게, 내가 관리하는 건물과 그렇지 않은 건물의 명암을 분명히 가른다.
    // 여기는 순수 규칙이다. 제휴 음식점을 밝힐 자리를 어느 건물로 둘지와, 밝힌 건물의 모양(외곽 넓히기·지붕 나누기)을 정한다.
    //  - 제휴 음식점은 동마다 수만 있고 자리 자료가 없다. 그래서 자리는 가상이다(표현값).
    //    그 동의 근린생활시설 건물 가운데 정해진 순서로 고른다. 같은 자료면 언제나 같은 건물이다.
    //  - 제휴가 늘면 앞에서부터 하나씩 더 켜지고, 줄면 뒤에서부터 꺼진다. 앞의 자리는 그대로다.
    //  - 역할표에 자리가 있는 매장(매장 A·B)은 제휴 수에 먼저 들어간다. 그 건물은 가상 자리로 다시 고르지 않는다.
    // 게임 상태를 읽지도 바꾸지도 않는다. 실제 점포나 건물의 쓰임과 무관하다.
    internal static class 관리건물Policy
    {
        public const double MinArea = 40;        // 가상 자리로 삼을 건물의 건축면적(㎡). 너무 작은 부속 건물은 뺀다
        public const double MaxArea = 600;       // 너무 큰 건물(학교·시장 같은 덩어리)은 뺀다
        private const string NeighbourhoodOne = "al-d010:03000";     // 제1종 근린생활시설
        private const string NeighbourhoodTwo = "al-d010:04000";     // 제2종 근린생활시설

        // 가상 자리로 삼을 수 있는 건물인가. 주택과 용도 미상, 예시 건물은 고르지 않는다.
        public static bool Eligible(string kind, double area, bool fill) =>
            !fill && area >= MinArea && area <= MaxArea && (kind == NeighbourhoodOne || kind == NeighbourhoodTwo);

        // 건물 ID에서 얻는 고른 순서(FNV-1a). 가까운 건물끼리 몰리지 않고 동 안에 흩어진다.
        public static uint Order(string id)
        {
            var hash = 2166136261u;
            foreach (var letter in id ?? string.Empty)
            {
                hash ^= letter;
                hash *= 16777619u;
            }
            return hash;
        }

        // 고르는 순서의 열쇠 (2026-11-17 중랑구 J1). 건물통합식별번호가 있으면 옛 판(2026-08-09) id 꼴로 만든다.
        // 건물 판을 바꿔도 같은 건물이 같은 순서에 서서, 밝힌 제휴 자리가 다른 건물로 옮겨 가지 않는다.
        public static string OrderKey(string id, string sourceId) =>
            string.IsNullOrEmpty(sourceId) ? id : "vworld:al-d010:" + sourceId;

        // 한 동의 후보 건물을 고르는 순서대로. 넣는 순서가 달라도 결과는 같다.
        public static List<int> Candidates(IEnumerable<(int Index, string Id, string Kind, double Area, bool Fill)> buildings) =>
            buildings.Where(item => Eligible(item.Kind, item.Area, item.Fill))
                .OrderBy(item => Order(item.Id)).ThenBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => item.Index).ToList();

        // 가상 자리가 필요한 제휴 수. 자리가 있는 매장은 먼저 뺀다.
        public static int VirtualCount(int partners, int placedStores) => Math.Max(0, partners - Math.Max(0, placedStores));

        // 제휴 음식점의 가상 자리. taken(이미 역할이 있는 건물)은 건너뛴다. 후보가 모자라면 있는 만큼만.
        public static List<int> PartnerSites(IReadOnlyList<int> candidates, int partners, int placedStores, ICollection<int> taken)
        {
            var need = VirtualCount(partners, placedStores);
            var sites = new List<int>(need);
            if (candidates == null) return sites;
            foreach (var index in candidates)
            {
                if (sites.Count >= need) break;
                if (taken != null && taken.Contains(index)) continue;
                sites.Add(index);
            }
            return sites;
        }

        // ── 밝힌 건물의 모양 ──
        // 원래 건물 메시 위에 조금 큰 건물을 덧그린다. 외곽을 바깥으로 넓히고 지붕을 삼각형으로 나눈다.

        // 반시계면 양수.
        public static double SignedArea(IReadOnlyList<(double X, double Y)> ring)
        {
            double sum = 0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++) sum += ring[j].X * ring[i].Y - ring[i].X * ring[j].Y;
            return sum * 0.5;
        }

        // 외곽을 distance만큼 바깥으로 넓힌다. 오목한 모서리도 건물 바깥쪽으로 간다.
        public static List<(double X, double Y)> Outset(IReadOnlyList<(double X, double Y)> ring, double distance)
        {
            var count = ring.Count;
            var counterClockwise = SignedArea(ring) >= 0;
            var result = new List<(double X, double Y)>(count);
            for (var i = 0; i < count; i++)
            {
                var here = ring[i];
                var (ax, ay) = Outward(ring[(i + count - 1) % count], here, counterClockwise);
                var (bx, by) = Outward(here, ring[(i + 1) % count], counterClockwise);
                var mx = ax + bx;
                var my = ay + by;
                var length = Math.Sqrt(mx * mx + my * my);
                if (length < 1e-6)
                {
                    result.Add(here);       // 길이가 없는 변 둘 사이거나 되접힌 모서리: 그대로 둔다
                    continue;
                }
                mx /= length;
                my /= length;
                // 두 변 모두에서 distance만큼 떨어지게. 뾰족한 모서리에서 너무 멀리 나가지 않게 막는다.
                var reach = distance / Math.Max(0.35, mx * ax + my * ay);
                result.Add((here.X + mx * reach, here.Y + my * reach));
            }
            return result;
        }

        // 변의 바깥쪽 단위 벡터. 반시계 외곽의 바깥은 진행 방향의 오른쪽이다.
        public static (double X, double Y) Outward((double X, double Y) start, (double X, double Y) end, bool counterClockwise)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) return (0, 0);
            return counterClockwise ? (dy / length, -dx / length) : (-dy / length, dx / length);
        }

        // 지붕을 삼각형으로 나눈다(귀 자르기). 돌려주는 값은 ring의 점 번호 세 개씩이다.
        public static List<int> Triangulate(IReadOnlyList<(double X, double Y)> ring)
        {
            var count = ring.Count;
            var triangles = new List<int>(Math.Max(0, (count - 2) * 3));
            if (count < 3) return triangles;
            var order = new List<int>(count);
            if (SignedArea(ring) >= 0)
                for (var i = 0; i < count; i++) order.Add(i);
            else
                for (var i = count - 1; i >= 0; i--) order.Add(i);     // 반시계로 맞춘다

            var guard = count * count + 8;
            while (order.Count > 3 && guard-- > 0)
            {
                var clipped = false;
                for (var i = 0; i < order.Count; i++)
                {
                    var before = order[(i + order.Count - 1) % order.Count];
                    var at = order[i];
                    var after = order[(i + 1) % order.Count];
                    if (Cross(ring[before], ring[at], ring[after]) <= 1e-12) continue;      // 오목하거나 일직선
                    var empty = true;
                    foreach (var other in order)
                    {
                        if (other == before || other == at || other == after) continue;
                        if (!InsideTriangle(ring[other], ring[before], ring[at], ring[after])) continue;
                        empty = false;
                        break;
                    }
                    if (!empty) continue;
                    triangles.Add(before);
                    triangles.Add(at);
                    triangles.Add(after);
                    order.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;      // 귀를 못 찾았다(겹치거나 일직선인 외곽). 남은 것은 부채꼴로 닫는다
            }
            for (var i = 1; i + 1 < order.Count; i++)
            {
                triangles.Add(order[0]);
                triangles.Add(order[i]);
                triangles.Add(order[i + 1]);
            }
            return triangles;
        }

        private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        private static bool InsideTriangle((double X, double Y) point, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
            Cross(a, b, point) > 1e-12 && Cross(b, c, point) > 1e-12 && Cross(c, a, point) > 1e-12;
    }
}
