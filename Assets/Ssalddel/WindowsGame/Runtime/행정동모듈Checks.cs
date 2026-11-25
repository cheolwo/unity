using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.WindowsGame
{
    // 행정동 모듈 디오라마(r54 첫 조각)의 순수 검사. 빌드의 kernel-checks.txt와 Checks~/DongModules가 같은 검사를 돌린다.
    internal static class 행정동모듈Checks
    {
        // 화면 각도는 SagajeongDispatchDiorama와 같다(yaw 30°, pitch 55°).
        private static readonly double YawCos = Math.Cos(30 * Math.PI / 180);
        private static readonly double YawSin = Math.Sin(30 * Math.PI / 180);
        private static readonly double PitchSin = Math.Sin(55 * Math.PI / 180);

        // ── 동 메시 풀기 (지도 자료 적용 S2 첫 조각, 2026-11-16) ──
        // 굽은 메시를 손실 없이 싸고(HDIZ) 게임이 풀 때, 같은 바이트로 돌아오고 바뀐 것은 거절하는지 본다.
        private static void 동메시풀기Checks(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            static bool Throws(Func<byte[]> act)
            {
                try { act(); return false; }
                catch (InvalidDataException) { return true; }
            }
            var raw = new byte[600];
            raw[0] = (byte)'H'; raw[1] = (byte)'D'; raw[2] = (byte)'I'; raw[3] = (byte)'O';
            for (var i = 4; i < raw.Length; i++) raw[i] = (byte)(i * 7 % 13);
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(raw);
            byte[] body;
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionMode.Compress, true)) deflate.Write(raw, 0, raw.Length);
                body = output.ToArray();
            }
            var packed = new byte[행정동모듈Policy.PackHeaderBytes + body.Length];
            packed[0] = (byte)'H'; packed[1] = (byte)'D'; packed[2] = (byte)'I'; packed[3] = (byte)'Z';
            packed[4] = 1; packed[8] = (byte)(raw.Length & 0xFF); packed[9] = (byte)(raw.Length >> 8);
            Array.Copy(hash, 0, packed, 12, 32);
            Array.Copy(body, 0, packed, 행정동모듈Policy.PackHeaderBytes, body.Length);
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty);
            Check("동 메시 풀기: 싼 것을 풀면 원래와 같음", 행정동모듈Policy.UnpackModule(packed, raw.Length, hex).SequenceEqual(raw), $"{packed.Length} → {raw.Length}");
            Check("동 메시 풀기: 안 싼 메시(HDIO)는 그대로", ReferenceEquals(행정동모듈Policy.UnpackModule(raw, 0, null), raw));
            var tampered = (byte[])packed.Clone(); tampered[20] ^= 0xFF;
            Check("동 메시 풀기: 머리의 해시와 다르면 거절", Throws(() => 행정동모듈Policy.UnpackModule(tampered, raw.Length, null)));
            Check("동 메시 풀기: 목록의 원래 길이 · 해시와 다르면 거절",
                Throws(() => 행정동모듈Policy.UnpackModule(packed, raw.Length + 1, null)) && Throws(() => 행정동모듈Policy.UnpackModule(packed, raw.Length, new string('0', 64))));
        }

        // ── 회색 동네와 관리 건물 (r143 첫 걸음, 2026-11-14) ──
        // 제휴 음식점의 가상 자리를 고르는 규칙과, 밝힌 건물의 모양(외곽 넓히기·지붕 나누기)을 본다.
        private static void 관리건물Checks(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // 가상의 건물 열 채. 실제 건물과 무관한 검사용 값이다.
            var buildings = new List<(int Index, string Id, string Kind, double Area, bool Fill)>
            {
                (0, "b:house", "al-d010:01000", 120, false),
                (1, "b:shop-1", "al-d010:03000", 90, false),
                (2, "b:shop-2", "al-d010:04000", 150, false),
                (3, "b:tiny", "al-d010:03000", 12, false),
                (4, "b:huge", "al-d010:04000", 5000, false),
                (5, "b:fill", "al-d010:03000", 100, true),
                (6, "b:unknown", "al-d010:unknown", 100, false),
                (7, "b:shop-3", "al-d010:03000", 200, false),
                (8, "b:shop-4", "al-d010:04000", 60, false),
                (9, "b:shop-5", "al-d010:03000", 300, false),
            };
            var candidates = 관리건물Policy.Candidates(buildings);
            Check("관리 건물: 가상 자리는 근린생활시설만 (주택·용도 미상·예시 건물·너무 작거나 큰 건물 제외)",
                candidates.Count == 5 && candidates.OrderBy(index => index).SequenceEqual(new[] { 1, 2, 7, 8, 9 }),
                string.Join(",", candidates));
            var reversed = 관리건물Policy.Candidates(Enumerable.Reverse(buildings));
            Check("관리 건물: 넣는 순서가 달라도 고르는 순서는 같음", candidates.SequenceEqual(reversed));

            var none = new HashSet<int>();
            var three = 관리건물Policy.PartnerSites(candidates, 3, 0, none);
            var four = 관리건물Policy.PartnerSites(candidates, 4, 0, none);
            Check("관리 건물: 제휴가 늘면 하나 더 켜지고 앞의 자리는 그대로 (줄면 뒤에서 꺼짐)",
                three.Count == 3 && four.Count == 4 && four.Take(3).SequenceEqual(three));
            Check("관리 건물: 자리가 있는 매장은 제휴 수에 먼저 들어감",
                관리건물Policy.PartnerSites(candidates, 2, 2, none).Count == 0 && 관리건물Policy.PartnerSites(candidates, 5, 2, none).Count == 3 &&
                관리건물Policy.PartnerSites(candidates, 1, 2, none).Count == 0);
            var taken = new HashSet<int> { candidates[0] };
            var skipping = 관리건물Policy.PartnerSites(candidates, 2, 0, taken);
            Check("관리 건물: 이미 역할이 있는 건물은 가상 자리로 다시 고르지 않음", skipping.Count == 2 && !skipping.Contains(candidates[0]));
            Check("관리 건물: 후보보다 제휴가 많으면 있는 만큼만, 제휴가 없으면 아무것도 켜지 않음",
                관리건물Policy.PartnerSites(candidates, 99, 0, none).Count == 5 && 관리건물Policy.PartnerSites(candidates, 0, 0, none).Count == 0 &&
                관리건물Policy.PartnerSites(null, 3, 0, none).Count == 0);

            // 모양. 네모(10 m)와 ㄱ자 외곽은 검사용 도형이다.
            var box = new List<(double X, double Y)> { (0, 0), (10, 0), (10, 10), (0, 10) };
            var bend = new List<(double X, double Y)> { (0, 0), (30, 0), (30, 10), (10, 10), (10, 30), (0, 30) };
            var boxBack = Enumerable.Reverse(box).ToList();
            var bendBack = Enumerable.Reverse(bend).ToList();
            var wide = 관리건물Policy.SignedArea(관리건물Policy.Outset(box, 0.5));
            var wideBack = Math.Abs(관리건물Policy.SignedArea(관리건물Policy.Outset(boxBack, 0.5)));
            Check("관리 건물: 외곽을 바깥으로 넓힘 (도는 방향과 무관)", Math.Abs(wide - 121) < 1e-6 && Math.Abs(wideBack - 121) < 1e-6,
                $"{wide:0.00} · {wideBack:0.00}");
            // ㄱ자: 둘레 120 m × 0.5 m에, 볼록한 모서리 다섯의 귀퉁이를 더하고 오목한 모서리 하나를 뺀다 = 61 ㎡.
            var bendWide = 관리건물Policy.Outset(bend, 0.5);
            Check("관리 건물: ㄱ자 외곽은 오목한 모서리도 건물 바깥쪽으로",
                Math.Abs(관리건물Policy.SignedArea(bendWide) - 561) < 1e-6 && bendWide[3].X > 10 && bendWide[3].Y > 10,
                $"{관리건물Policy.SignedArea(bendWide):0.00} · {bendWide[3].X:0.00}, {bendWide[3].Y:0.00}");

            double Covered(IReadOnlyList<(double X, double Y)> ring)
            {
                var corners = 관리건물Policy.Triangulate(ring);
                double sum = 0;
                for (var i = 0; i + 2 < corners.Count; i += 3)
                    sum += Math.Abs(관리건물Policy.SignedArea(new[] { ring[corners[i]], ring[corners[i + 1]], ring[corners[i + 2]] }));
                return sum;
            }
            Check("관리 건물: 지붕 삼각형이 외곽을 빈틈없이 덮음 (네모·ㄱ자·도는 방향 무관)",
                Math.Abs(Covered(box) - 100) < 1e-6 && Math.Abs(Covered(bend) - 500) < 1e-6 && Math.Abs(Covered(bendBack) - 500) < 1e-6 &&
                관리건물Policy.Triangulate(bend).Count == 12 && 관리건물Policy.Triangulate(box).Count == 6,
                $"{Covered(box):0.0} · {Covered(bend):0.0} · {Covered(bendBack):0.0}");
        }

        // ── 역 중심 보기 (2026-11-14) ──
        // 둥근 창의 안팎, 판의 둘레를 화면에 맞추기, 자료가 덮는 비율, 역 넘기기를 본다.
        private static void 역중심Checks(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            const double radius = 역중심Policy.Radius;
            const double fade = 역중심Policy.Fade;
            var halfway = 역중심Policy.Outside(radius + fade * 0.5, radius, fade);
            Check("역 중심: 반경 안은 그대로, 바깥으로 흐려지고 띠 끝에서 끊김",
                역중심Policy.Outside(0, radius, fade) == 0 && 역중심Policy.Outside(radius, radius, fade) == 0 && halfway > 0.49 && halfway < 0.51 &&
                역중심Policy.Outside(radius + fade, radius, fade) == 1 && 역중심Policy.Outside(radius * 3, radius, fade) == 1);
            Check("역 중심: 표식은 흐려지는 띠의 절반까지만 그림",
                역중심Policy.Shows(radius, radius, fade) && 역중심Policy.Shows(radius + fade * 0.4, radius, fade) &&
                !역중심Policy.Shows(radius + fade * 0.6, radius, fade));

            var ring = 역중심Policy.Ring(100, -50, radius, 48);
            var onCircle = ring.Count == 48 && ring.All(point =>
                Math.Abs(Math.Sqrt(Math.Pow(point.East - 100, 2) + Math.Pow(point.North + 50, 2)) - radius) < 1e-6);
            var (metres, east, north) = 행정동모듈Policy.Fit(ring, 756, 500, 0.08, true, YawCos, YawSin, PitchSin);
            Check("역 중심: 판의 둘레를 화면에 맞추면 역이 가운데에 옴",
                onCircle && Math.Abs(east - 100) < 1e-6 && Math.Abs(north + 50) < 1e-6 && metres > 0, $"{metres:0.000} m/px");

            Check("역 중심: 자료가 덮는 비율 (모두·절반·없음)",
                Math.Abs(역중심Policy.Coverage(0, 0, radius, (x, y) => true) - 1) < 1e-9 &&
                Math.Abs(역중심Policy.Coverage(0, 0, radius, (x, y) => x >= 0) - 0.5) < 0.02 &&
                역중심Policy.Coverage(0, 0, radius, (x, y) => false) == 0 && 역중심Policy.Coverage(0, 0, radius, null) == 0);
            Check("역 중심: 역을 넘기면 끝에서 처음으로 돎",
                역중심Policy.Step(0, 1, 2) == 1 && 역중심Policy.Step(1, 1, 2) == 0 && 역중심Policy.Step(0, -1, 2) == 1 && 역중심Policy.Step(0, 1, 0) == 0);
            var stations = 역중심Policy.Stations;
            Check("역 중심: 역마다 이름과 자리가 있고 서로 다른 자리",
                stations.Count >= 2 && stations.All(station => !string.IsNullOrEmpty(station.Name) &&
                    station.Latitude > 33 && station.Latitude < 39 && station.Longitude > 124 && station.Longitude < 132) &&
                stations.Select(station => (station.Latitude, station.Longitude)).Distinct().Count() == stations.Count,
                string.Join(", ", stations.Select(station => station.Name)));
        }

        // ── 원근 보기 (3D 기반 A6, 2026-11-14) ──
        // 화면 가운데의 크기를 평행 보기와 맞추는지, 보이는 땅이 카메라의 가까운 면과 먼 면 사이에 드는지 본다.
        private static void 원근Checks(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            var lens = 디오라마시점.Default;
            Check("원근: 화면 가운데의 땅은 평행 보기와 같은 크기", Math.Abs(lens.PerspectiveScale(500, 0) - 1) < 1e-9);
            var nearScale = lens.PerspectiveScale(500, -1);
            var farScale = lens.PerspectiveScale(500, 1);
            Check("원근: 가까운 쪽(아래)은 크게, 먼 쪽(위)은 작게", nearScale > 1.05 && nearScale < 1.3 && farScale < 0.95 && farScale > 0.7,
                $"아래 {nearScale:0.00}배 · 위 {farScale:0.00}배");
            var inside = true;
            foreach (var half in new[] { 70.0, 500.0, 1400.0 })
            {
                var (clipNear, clipFar) = 디오라마시점.PerspectiveClip(half);
                foreach (var pitch in new[] { 25.0, 35.0, 55.0, 70.0, 90.0 })
                {
                    var turned = new 디오라마시점(30, pitch);
                    foreach (var v in new[] { -1.0, 0.0, 1.0 })
                    {
                        var depth = turned.PerspectiveGroundDepth(half, v);
                        // 화면 아래 끝에 60 m 건물이 서 있어도 가까운 면보다 멀어야 한다.
                        inside &= depth - 60 > clipNear && depth < clipFar;
                    }
                }
            }
            Check("원근: 보이는 땅과 건물이 가까운 면과 먼 면 사이에 듦 (25°~90°, 가까이·멀리)", inside);
            Check("원근: 화면에 담기는 땅이 넓을수록 카메라가 멀어짐 (가운데 크기를 맞춤)",
                Math.Abs(디오라마시점.PerspectiveDistance(1000) - 2 * 디오라마시점.PerspectiveDistance(500)) < 1e-6,
                $"{디오라마시점.PerspectiveDistance(500):0} m");
        }

        // ── 보는 방향 (디오라마시점, 3D 기반 A1, 2026-11-14) ──
        // 화면 코드에 흩어져 있던 식(FlatDelta, DongMinZoom, ClampDioramaOffset)을 디오라마시점으로 모았다.
        // 기본 방향에서 예전 식과 같은 값을 내는지, 방향을 바꿔도 계산이 서로 맞는지 본다.
        private static void 시점Checks(List<(string, bool, string)> results)
        {
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            var lens = 디오라마시점.Default;
            Check("시점 기본값은 전과 같은 30°·55°",
                Math.Abs(lens.YawCos - YawCos) < 1e-12 && Math.Abs(lens.YawSin - YawSin) < 1e-12 && Math.Abs(lens.PitchSin - PitchSin) < 1e-12,
                $"{lens.YawDegrees:0}°, {lens.PitchDegrees:0}°");

            var sameAsBefore = true;
            foreach (var (u, v) in new[] { (0.0, 0.0), (37.5, -12.0), (-640.0, 360.0), (1.0, 1.0) })
            {
                var w = v / PitchSin;
                var (x, y) = lens.FlatDelta(u, v);
                sameAsBefore &= Math.Abs(x - (YawCos * u - YawSin * w)) < 1e-9 && Math.Abs(y - (YawSin * u + YawCos * w)) < 1e-9;
            }
            foreach (var (width, depth) in new[] { (2382.0, 1301.0), (100.0, 100.0), (640.0, 360.0) })
            {
                var (spanX, spanY) = lens.ScreenSpan(width, depth);
                sameAsBefore &= Math.Abs(spanX - (width * YawCos + depth * YawSin)) < 1e-9 &&
                    Math.Abs(spanY - (width * YawSin + depth * YawCos) * PitchSin) < 1e-9;
                var deep = depth / PitchSin;
                var (east, north) = lens.GroundSpan(width, depth);
                sameAsBefore &= Math.Abs(east - (width * YawCos + deep * YawSin)) < 1e-9 && Math.Abs(north - (width * YawSin + deep * YawCos)) < 1e-9;
            }
            Check("시점 계산이 예전 화면 식과 같음 (이동량·화면 폭·땅 폭)", sameAsBefore);

            var turnedHolds = true;
            foreach (var (yaw, pitch) in new[] { (30.0, 55.0), (120.0, 55.0), (210.0, 40.0), (300.0, 70.0) })
            {
                var turned = new 디오라마시점(yaw, pitch);
                const double width = 800, depth = 300;
                var (spanX, spanY) = turned.ScreenSpan(width, depth);
                double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
                foreach (var (east, north) in new[] { (0.0, 0.0), (width, 0.0), (width, depth), (0.0, depth) })
                {
                    var (u, v) = turned.Screen(east, north);
                    minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
                    minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
                    var (backEast, backNorth) = turned.Ground(u, v);
                    turnedHolds &= Math.Abs(east - backEast) < 1e-6 && Math.Abs(north - backNorth) < 1e-6;
                }
                turnedHolds &= Math.Abs(maxU - minU - spanX) < 1e-6 && Math.Abs(maxV - minV - spanY) < 1e-6;
            }
            Check("방향을 바꿔도 화면 폭과 왕복 계산이 맞음 (네 방향)", turnedHolds);

            // 받은 값을 쓸 수 있는 범위로 다듬는다 (A3)
            var wrapped = 디오라마시점.Of(-90, 10);
            var capped = 디오라마시점.Of(390, 120);
            Check("회전은 0°~360°로, 내려다보는 각은 25°~90°로 다듬음",
                Math.Abs(wrapped.YawDegrees - 270) < 1e-9 && Math.Abs(wrapped.PitchDegrees - 디오라마시점.MinPitchDegrees) < 1e-9 &&
                Math.Abs(capped.YawDegrees - 30) < 1e-9 && Math.Abs(capped.PitchDegrees - 90) < 1e-9,
                $"{wrapped.YawDegrees:0}°·{wrapped.PitchDegrees:0}°, {capped.YawDegrees:0}°·{capped.PitchDegrees:0}°");
            // 북쪽을 위로 두면 가로가 동서, 90° 돌리면 가로가 남북이 된다
            var northUp = 디오라마시점.Of(0, 90).ScreenSpan(800, 300);
            var quarter = 디오라마시점.Of(90, 90).ScreenSpan(800, 300);
            Check("90° 돌리면 화면의 가로와 세로가 바뀜",
                Math.Abs(northUp.X - 800) < 1e-6 && Math.Abs(northUp.Y - 300) < 1e-6 && Math.Abs(quarter.X - 300) < 1e-6 && Math.Abs(quarter.Y - 800) < 1e-6,
                $"{northUp.X:0}×{northUp.Y:0} → {quarter.X:0}×{quarter.Y:0}");
        }

        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // ── 화면 ↔ 땅 ──
            var roundTrip = true;
            foreach (var tilted in new[] { true, false })
                foreach (var (e, n) in new[] { (0.0, 0.0), (120.5, -40.25), (-900.0, 1300.0), (2.0, 3.0) })
                {
                    var (u, v) = 행정동모듈Policy.Screen(e, n, tilted, YawCos, YawSin, PitchSin);
                    var (e2, n2) = 행정동모듈Policy.Ground(u, v, tilted, YawCos, YawSin, PitchSin);
                    roundTrip &= Math.Abs(e - e2) < 1e-6 && Math.Abs(n - n2) < 1e-6;
                }
            Check("화면 좌표와 땅 좌표가 서로 되돌아감 (기울임·평면)", roundTrip);
            var (northU, northV) = 행정동모듈Policy.Screen(0, 100, true, YawCos, YawSin, PitchSin);
            Check("북쪽은 화면 위로 (기울여도)", northV < 0, $"{northU:0.0}, {northV:0.0}");
            시점Checks(results);
            원근Checks(results);

            // ── 한 동을 영역에 맞추기 ──
            // ㄱ자 모양의 가상 경계(m). 실제 동 경계와 무관한 검사용 도형이다.
            var ring = new List<(double, double)> { (-500, 300), (560, 300), (560, 1470), (0, 1470), (0, 800), (-500, 800) };
            foreach (var tilted in new[] { true, false })
            {
                const double width = 756, height = 500, margin = 0.06;
                var (metres, east, north) = 행정동모듈Policy.Fit(ring, width, height, margin, tilted, YawCos, YawSin, PitchSin);
                var (cu, cv) = 행정동모듈Policy.Screen(east, north, tilted, YawCos, YawSin, PitchSin);
                var inside = ring.All(point =>
                {
                    var (u, v) = 행정동모듈Policy.Screen(point.Item1, point.Item2, tilted, YawCos, YawSin, PitchSin);
                    var x = (u - cu) / metres;
                    var y = (v - cv) / metres;
                    return Math.Abs(x) <= width * 0.5 + 1e-6 && Math.Abs(y) <= height * 0.5 + 1e-6;
                });
                var label = tilted ? "기울임" : "평면";
                Check($"동 경계가 맞춘 영역 안에 모두 들어옴 ({label})", inside, $"{metres:0.000} m/px");
                var touches = ring.Any(point =>
                {
                    var (u, v) = 행정동모듈Policy.Screen(point.Item1, point.Item2, tilted, YawCos, YawSin, PitchSin);
                    return Math.Abs((u - cu) / metres) >= width * 0.5 * (1 - 2 * margin) - 1e-3 ||
                           Math.Abs((v - cv) / metres) >= height * 0.5 * (1 - 2 * margin) - 1e-3;
                });
                Check($"여백만 남기고 꽉 차게 맞춤 ({label})", touches);
            }
            var small = ring.Select(point => (point.Item1 * 0.4, point.Item2 * 0.4)).ToList();
            var big = 행정동모듈Policy.Fit(ring, 756, 500, 0.06, true, YawCos, YawSin, PitchSin).MetresPerPixel;
            var smaller = 행정동모듈Policy.Fit(small, 756, 500, 0.06, true, YawCos, YawSin, PitchSin).MetresPerPixel;
            Check("작은 동은 더 가까이 본다", smaller < big, $"{smaller:0.000} < {big:0.000}");
            var dot = 행정동모듈Policy.Fit(new List<(double, double)> { (10, 10) }, 756, 500, 0.06, true, YawCos, YawSin, PitchSin);
            Check("점 하나여도 배율이 0이 되지 않음", dot.MetresPerPixel > 0 && !double.IsInfinity(dot.MetresPerPixel));

            // ── 순회 정거장 ↔ 행정동 모듈 ──
            var stops = 결정순회Policy.Stops();
            var dongStops = stops.Where(stop => stop.Level == 순회높이.행정동).ToList();
            Check("행정동 정거장마다 지역 ID가 있음", dongStops.Count >= 2 && dongStops.All(stop => 행정동모듈Policy.IsRegionId(stop.RegionId)),
                string.Join(", ", dongStops.Select(stop => stop.RegionId)));
            Check("두 행정동 정거장은 서로 다른 지역 (같은 지도 재사용 아님)",
                dongStops.Select(stop => stop.RegionId).Distinct().Count() == dongStops.Count);
            Check("행정동이 아닌 정거장에는 지역 ID가 없음", stops.Where(stop => stop.Level != 순회높이.행정동).All(stop => stop.RegionId == null));

            // ── 이웃 동 어둡게 ──
            Check("고른 동은 밝게, 이웃 동은 어둡게",
                행정동모듈Policy.DimFor("region:kr:hjd:1126056500", "region:kr:hjd:1126056500") == 1f &&
                행정동모듈Policy.DimFor("region:kr:hjd:1126056500", "region:kr:hjd:1126052000") < 1f);
            Check("고른 동이 없으면 모두 밝게", 행정동모듈Policy.DimFor(null, "region:kr:hjd:1126052000") == 1f);

            // ── 행정동 깃발과 밝기 ──
            var darkLight = 행정동모듈Policy.Brightness(0, false, 0);
            var nearLight = 행정동모듈Policy.Brightness(0, true, 0);
            var flagLight = 행정동모듈Policy.Brightness(1, false, 0);
            var flagDeepLight = 행정동모듈Policy.Brightness(1, false, 1);
            var operatingLight = 행정동모듈Policy.Brightness(2, false, 0);
            var settledLight = 행정동모듈Policy.Brightness(3, false, 0);
            Check("밝기: 어둠 < 흐림 < 깃발 < 운영 < 자리 잡음",
                darkLight > 0 && darkLight < nearLight && nearLight < flagLight && flagLight < flagDeepLight && flagDeepLight <= operatingLight &&
                operatingLight < 행정동모듈Policy.Brightness(2, false, 1) && 행정동모듈Policy.Brightness(2, false, 1) < settledLight && settledLight == 1f,
                $"{darkLight:0.00} · {nearLight:0.00} · {flagLight:0.00}~{flagDeepLight:0.00} · {operatingLight:0.00}~ · {settledLight:0.00}");
            Check("밝기: 운영의 깊이는 0~1로 잘라 쓴다",
                행정동모듈Policy.Brightness(1, false, 5) == flagDeepLight && 행정동모듈Policy.Brightness(1, false, -3) == flagLight);
            Check("밝기: 다른 동을 보고 있을 때만 조금 어둡게",
                행정동모듈Policy.FocusFactor("a", "b") < 1f && 행정동모듈Policy.FocusFactor("a", "a") == 1f && 행정동모듈Policy.FocusFactor(null, "b") == 1f);

            // ── 목록 한 줄과 판본 결속 ──
            var hash = new string('A', 64);
            Check("올바른 목록 줄은 통과",
                행정동모듈Policy.ValidateEntry("region:kr:hjd:1126056500", "hongdal-dong-1126056500", 10, hash, 127.08, 37.58, 127.09, 37.59) == null);
            Check("지역 ID가 틀리면 거절", 행정동모듈Policy.ValidateEntry("region:kr:bjd:1126010100", "x", 10, hash, 0, 0, 1, 1) != null);
            Check("해시가 없거나 짧으면 거절", 행정동모듈Policy.ValidateEntry("region:kr:hjd:1126056500", "x", 10, "ABC", 0, 0, 1, 1) != null);
            Check("범위가 비면 거절", 행정동모듈Policy.ValidateEntry("region:kr:hjd:1126056500", "x", 10, hash, 1, 0, 1, 1) != null);
            Check("메시 원점이 다르면 같은 장면에 놓지 않음",
                행정동모듈Policy.SameOrigin(37.580912, 127.088502, 37.580912, 127.088502) &&
                !행정동모듈Policy.SameOrigin(37.5846879, 127.0880211, 37.580912, 127.088502));

            // ── 역할표 대응 ──
            var map = new List<(string, string)> { ("osm:way/1", "vworld:al-d010:9"), ("example:2", "") };
            Check("역할 건물은 대응표대로 옮김", 행정동모듈Policy.MapRoleBuilding(map, "osm:way/1") == "vworld:al-d010:9");
            Check("대응이 없으면 역할을 얹지 않음",
                행정동모듈Policy.MapRoleBuilding(map, "example:2") == null && 행정동모듈Policy.MapRoleBuilding(map, "osm:way/3") == null);

            // ── 제휴 자리 순서 열쇠(판 바꾸기) ──
            Check("새 판 건물은 건물통합식별번호로 옛 판과 같은 열쇠",
                관리건물Policy.OrderKey("al-d010:20261109:11:170501", "2016208187304535077200000000") == "vworld:al-d010:2016208187304535077200000000" &&
                관리건물Policy.Order(관리건물Policy.OrderKey("al-d010:20261109:11:170501", "2016208187304535077200000000")) ==
                관리건물Policy.Order("vworld:al-d010:2016208187304535077200000000"));
            Check("번호가 없으면 id 그대로", 관리건물Policy.OrderKey("vworld:al-d010:9", null) == "vworld:al-d010:9" &&
                관리건물Policy.OrderKey("b:shop-1", "") == "b:shop-1");

            // ── 건물 카드 글자 ──
            Check("주용도 코드를 이름으로", 행정동모듈Policy.UseLabel("al-d010:01000") == "단독주택" &&
                행정동모듈Policy.UseLabel("al-d010:04000") == "제2종 근린생활" && 행정동모듈Policy.UseLabel("al-d010:unknown") == "용도 미상");
            Check("상징 높이는 그렇다고 밝힘", 행정동모듈Policy.HeightNote("symbolic").Contains("상징"));

            // ── 동 보기 방식: 띄워 보기·이 동만·골라 보기 ──
            var still = 행정동모듈Policy.ExplodeOffset(300, 400, 100, 100, 0);
            Check("붙여 보기는 동을 옮기지 않음", still.East == 0 && still.North == 0);
            const double spread = 행정동모듈Policy.SeparatedSpread;
            var a = 행정동모듈Policy.ExplodeOffset(-400, 0, 0, 0, spread);
            var b = 행정동모듈Policy.ExplodeOffset(500, 300, 0, 0, spread);
            var before = Math.Sqrt(900 * 900 + 300 * 300);
            var after = Math.Sqrt(Math.Pow(500 + b.East - (-400 + a.East), 2) + Math.Pow(300 + b.North - a.North, 2));
            Check("띄워 보기는 두 동 사이를 벌림 (모양은 그대로)", Math.Abs(after - before * (1 + spread)) < 1e-6 && after > before,
                $"{before:0} m → {after:0} m");
            var square = new List<(double, double)> { (0, 0), (100, 0), (100, 100), (0, 100) };
            var centroid = 행정동모듈Policy.Centroid(square);
            Check("넓이 중심", Math.Abs(centroid.East - 50) < 1e-9 && Math.Abs(centroid.North - 50) < 1e-9);
            var lShape = new List<(double, double)> { (0, 0), (300, 0), (300, 100), (100, 100), (100, 300), (0, 300) };
            var lCentre = 행정동모듈Policy.Centroid(lShape);
            Check("ㄱ자 동의 넓이 중심은 경계 상자 가운데보다 안쪽", lCentre.East < 150 && lCentre.North < 150,
                $"{lCentre.East:0}, {lCentre.North:0}");

            var grid = new 행정동격자(new List<IReadOnlyList<(double, double)>>
            {
                square,
                new List<(double, double)> { (100, 0), (200, 0), (200, 100), (100, 100) },
                lShape.Select(point => (point.Item1 + 400, point.Item2)).ToList(),
            }, 10);
            Check("격자: 점이 든 동을 찾음", grid.At(50, 50) == 0 && grid.At(150, 50) == 1 && grid.At(450, 250) == 2,
                $"{grid.At(50, 50)}, {grid.At(150, 50)}, {grid.At(450, 250)}");
            Check("격자: 동 밖은 -1 (ㄱ자의 빈 모서리 포함)", grid.At(250, 250) == -1 && grid.At(650, 250) == -1 && grid.At(-50, 50) == -1,
                $"{grid.At(250, 250)}, {grid.At(650, 250)}, {grid.At(-50, 50)}");

            var hidden = new HashSet<string> { "region:kr:hjd:1126055000" };
            Check("골라 보기: 숨긴 동만 안 보임",
                !행정동모듈Policy.Visible(false, null, hidden, "region:kr:hjd:1126055000") &&
                행정동모듈Policy.Visible(false, null, hidden, "region:kr:hjd:1126052000"));
            Check("이 동만: 고른 한 동만 보임",
                행정동모듈Policy.Visible(true, "region:kr:hjd:1126052000", hidden, "region:kr:hjd:1126052000") &&
                !행정동모듈Policy.Visible(true, "region:kr:hjd:1126052000", null, "region:kr:hjd:1126056500"));
            Check("마지막 한 동은 숨기지 않음", 행정동모듈Policy.CanHide(6, 4) && !행정동모듈Policy.CanHide(6, 5) && !행정동모듈Policy.CanHide(1, 0));
            동메시풀기Checks(results);
            관리건물Checks(results);
            역중심Checks(results);
            팔방이웃Checks.Add(results);
            return results;
        }
    }
}
