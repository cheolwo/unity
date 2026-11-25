using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 팔방 이웃 (2026-11-17 사용자 방향): 한 행정동 디오라마에서 위 · 아래 · 왼쪽 · 오른쪽과 대각선까지 여덟 방향으로
    // 맞닿은 동을 옮겨 다니며 관리하는 재미. 여기는 순수 규칙이다.
    //  - 두 동이 맞닿았는가: 한 동의 경계를 10 m 간격으로 나눈 점이 다른 동의 경계에 25 m 안으로 붙은 길이로 잰다(표현값).
    //    모서리만 닿은 동(붙은 길이 80 m 미만)은 이웃으로 치지 않는다(모서리 하나는 많아야 약 60 m로 잰다).
    //  - 방향은 화면 기준이다(지도를 돌려 보면 그만큼 돈다). 0 = 위, 시계 방향으로 45°씩 여덟 칸.
    //  - 한 칸에 여럿이면 붙은 길이가 긴 동이 그 칸을 갖고, 나머지는 비어 있는 옆 칸(±1)으로 간다. 자리가 없으면 뺀다.
    // 게임 상태를 읽지도 바꾸지도 않는다.
    internal static class 팔방이웃Policy
    {
        public const double TouchMetres = 25;
        public const double MinSharedMetres = 80;
        public const double StepMetres = 10;
        public static readonly string[] Names = { "위", "오른쪽 위", "오른쪽", "오른쪽 아래", "아래", "왼쪽 아래", "왼쪽", "왼쪽 위" };
        public static readonly string[] Arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

        // 두 경계(공통 원점 기준 m: 동 · 북)가 붙은 길이(m).
        public static double SharedLength(IReadOnlyList<(double E, double N)> a, IReadOnlyList<(double E, double N)> b,
            double touch = TouchMetres, double step = StepMetres)
        {
            if (a == null || b == null || a.Count < 2 || b.Count < 2) return 0;
            var (aw, asouth, ae, an) = Box(a);
            var (bw, bs, be, bn) = Box(b);
            if (aw > be + touch || bw > ae + touch || asouth > bn + touch || bs > an + touch) return 0;
            var touchSquared = touch * touch;
            double shared = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var p = a[i];
                var q = a[(i + 1) % a.Count];
                var length = Math.Sqrt((q.E - p.E) * (q.E - p.E) + (q.N - p.N) * (q.N - p.N));
                if (length <= 0) continue;
                var pieces = Math.Max(1, (int)Math.Ceiling(length / step));
                for (var k = 0; k < pieces; k++)
                {
                    var t = (k + 0.5) / pieces;
                    var x = p.E + (q.E - p.E) * t;
                    var y = p.N + (q.N - p.N) * t;
                    if (x < bw - touch || x > be + touch || y < bs - touch || y > bn + touch) continue;
                    if (DistanceSquaredToRing(x, y, b) <= touchSquared) shared += length / pieces;
                }
            }
            return shared;
        }

        // 화면 방향의 칸. dx = 화면 오른쪽, dy = 화면 위쪽. 0 = 위, 1 = 오른쪽 위 … 7 = 왼쪽 위.
        public static int Sector(double dx, double dy)
        {
            var angle = Math.Atan2(dx, dy) * 180 / Math.PI;      // 위 = 0°, 시계 방향이 +
            var sector = (int)Math.Round(angle / 45.0);
            return ((sector % 8) + 8) % 8;
        }

        // 이웃을 여덟 칸에 나눈다. 돌려준 배열의 칸마다 그 동의 ID(없으면 null).
        public static string[] Assign(IEnumerable<(string Id, double Dx, double Dy, double Shared)> neighbours)
        {
            var slots = new string[8];
            if (neighbours == null) return slots;
            var ordered = neighbours.Where(item => !string.IsNullOrEmpty(item.Id) && item.Shared >= MinSharedMetres)
                .OrderByDescending(item => item.Shared).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
            foreach (var item in ordered)
            {
                var angle = Math.Atan2(item.Dx, item.Dy) * 180 / Math.PI;
                var home = Sector(item.Dx, item.Dy);
                if (slots[home] == null)
                {
                    slots[home] = item.Id;
                    continue;
                }
                // 옆 칸: 각도가 기운 쪽부터
                var lean = angle - home * 45.0;
                lean -= 360 * Math.Round(lean / 360);
                var first = ((home + (lean >= 0 ? 1 : -1)) % 8 + 8) % 8;
                var second = ((home + (lean >= 0 ? -1 : 1)) % 8 + 8) % 8;
                if (slots[first] == null) slots[first] = item.Id;
                else if (slots[second] == null) slots[second] = item.Id;
            }
            return slots;
        }

        private static (double W, double S, double E, double N) Box(IReadOnlyList<(double E, double N)> ring)
        {
            double w = double.MaxValue, s = double.MaxValue, e = double.MinValue, n = double.MinValue;
            foreach (var p in ring)
            {
                w = Math.Min(w, p.E); e = Math.Max(e, p.E);
                s = Math.Min(s, p.N); n = Math.Max(n, p.N);
            }
            return (w, s, e, n);
        }

        private static double DistanceSquaredToRing(double x, double y, IReadOnlyList<(double E, double N)> ring)
        {
            var best = double.MaxValue;
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var dx = b.E - a.E;
                var dy = b.N - a.N;
                var length = dx * dx + dy * dy;
                var t = length > 0 ? Math.Max(0, Math.Min(1, ((x - a.E) * dx + (y - a.N) * dy) / length)) : 0;
                var px = a.E + dx * t - x;
                var py = a.N + dy * t - y;
                best = Math.Min(best, px * px + py * py);
            }
            return best;
        }
    }

    // 팔방 이웃 규칙의 검사. 행정동모듈Checks.Run이 부른다.
    internal static class 팔방이웃Checks
    {
        public static void Add(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            Check("팔방: 위 · 오른쪽 · 아래 · 왼쪽 칸",
                팔방이웃Policy.Sector(0, 1) == 0 && 팔방이웃Policy.Sector(1, 0) == 2 && 팔방이웃Policy.Sector(0, -1) == 4 && 팔방이웃Policy.Sector(-1, 0) == 6);
            Check("팔방: 대각선 칸",
                팔방이웃Policy.Sector(1, 1) == 1 && 팔방이웃Policy.Sector(1, -1) == 3 && 팔방이웃Policy.Sector(-1, -1) == 5 && 팔방이웃Policy.Sector(-1, 1) == 7);

            var square = new List<(double, double)> { (0, 0), (100, 0), (100, 100), (0, 100) };
            var right = new List<(double, double)> { (100, 0), (200, 0), (200, 100), (100, 100) };
            var corner = new List<(double, double)> { (100, 100), (200, 100), (200, 200), (100, 200) };
            var far = new List<(double, double)> { (500, 500), (600, 500), (600, 600), (500, 600) };
            var shared = 팔방이웃Policy.SharedLength(square, right);
            var cornerShared = 팔방이웃Policy.SharedLength(square, corner);
            Check("팔방: 한 변을 나눈 두 동은 그 변(+ 양 끝 붙는 거리)만큼 붙음", shared >= 100 && shared <= 100 + 2 * (팔방이웃Policy.TouchMetres + 팔방이웃Policy.StepMetres),
                shared.ToString("0.0"));
            Check("팔방: 모서리만 닿으면 이웃이 아님 · 먼 동은 0",
                cornerShared < 팔방이웃Policy.MinSharedMetres && 팔방이웃Policy.SharedLength(square, far) == 0, cornerShared.ToString("0.0"));

            var slots = 팔방이웃Policy.Assign(new[] { ("a", 0.0, 10.0, 200.0), ("b", 1.0, 10.0, 100.0), ("c", -1.0, 10.0, 90.0), ("d", 0.5, 10.0, 85.0) });
            Check("팔방: 같은 칸이면 긴 쪽이 갖고 나머지는 기운 옆 칸으로, 자리가 없으면 뺌",
                slots[0] == "a" && slots[1] == "b" && slots[7] == "c" && !slots.Contains("d"), string.Join(",", slots.Select(s => s ?? "-")));
            var tiny = 팔방이웃Policy.Assign(new[] { ("x", 10.0, 0.0, 10.0) });
            Check("팔방: 짧게 붙은 동은 칸에 넣지 않음", tiny.All(s => s == null));
            var again = 팔방이웃Policy.Assign(new[] { ("d", 0.5, 10.0, 85.0), ("c", -1.0, 10.0, 90.0), ("b", 1.0, 10.0, 100.0), ("a", 0.0, 10.0, 200.0) });
            Check("팔방: 넣는 순서가 달라도 같은 칸", slots.SequenceEqual(again));
        }
    }
}
