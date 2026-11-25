using System;
using System.Collections.Generic;
using System.IO;

namespace Ssalddel.WindowsGame
{
    // 길찾기와 기사 순찰의 검사 (R2 · R4, 2026-11-14). 손으로 만든 작은 그래프로 본다. 자원을 읽지 않는다.
    // 기사와 가게의 빛(기사불빛Policy)의 검사도 여기서 함께 본다.
    internal static class 길찾기Checks
    {
        // 마디: 0(0,0) 1(100,0) 2(0,30) 3(100,30) 4(500,0) 5(600,0)
        // 변: 0 = 0–1 골목 100 m, 1 = 0–2 큰길 30 m, 2 = 2–3 큰길 100 m, 3 = 3–1 큰길 30 m, 4 = 4–5 큰길(외딴 덩어리)
        // 건물의 출입구: 0 = 변0의 시작, 1 = 변0의 끝, 2 = 변1의 시작(마디 0), 3 = 변3의 끝(마디 1), 4 = 변4의 가운데(외딴 곳),
        //               5 = 없음, 6 = 변2의 1/4, 7 = 변2의 3/4
        private static 길그래프 Sample(out string problem) => 길그래프.From(
            new[] { 0f, 100f, 0f, 100f, 500f, 600f }, new[] { 0f, 0f, 30f, 30f, 0f, 0f },
            new[] { 0, 0, 2, 3, 4 }, new[] { 1, 2, 3, 1, 5 },
            new[] { 길그래프.Alley, 길그래프.Road, 길그래프.Road, 길그래프.Road, 길그래프.Road },
            new[] { 0, 0, 1, 3, 4, -1, 2, 2 }, new[] { 0f, 1f, 0f, 1f, 0.5f, 0f, 0.25f, 0.75f },
            new float[8], new float[8], out problem);

        private static byte[] SampleBytes()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)'H'); writer.Write((byte)'R'); writer.Write((byte)'T'); writer.Write((byte)'E');
                writer.Write(1);
                for (var k = 0; k < 4; k++) writer.Write(0d);
                var x = new[] { 0f, 100f, 0f, 100f, 500f, 600f };
                var z = new[] { 0f, 0f, 30f, 30f, 0f, 0f };
                writer.Write(x.Length);
                for (var k = 0; k < x.Length; k++) { writer.Write(x[k]); writer.Write(z[k]); }
                var a = new[] { 0, 0, 2, 3, 4 };
                var b = new[] { 1, 2, 3, 1, 5 };
                writer.Write(a.Length);
                for (var k = 0; k < a.Length; k++) { writer.Write(a[k]); writer.Write(b[k]); }
                writer.Write(new[] { 길그래프.Alley, 길그래프.Road, 길그래프.Road, 길그래프.Road, 길그래프.Road });
                var doorEdge = new[] { 0, 0, 1, 3, 4, -1, 2, 2 };
                var doorT = new[] { 0f, 1f, 0f, 1f, 0.5f, 0f, 0.25f, 0.75f };
                writer.Write(doorEdge.Length);
                foreach (var edge in doorEdge) writer.Write(edge);
                foreach (var t in doorT) writer.Write(t);
                for (var k = 0; k < doorEdge.Length; k++) { writer.Write(0f); writer.Write(0f); }
            }
            return stream.ToArray();
        }

        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            bool Close(float a, float b) => Math.Abs(a - b) < 0.01f;

            var graph = Sample(out var problem);
            Check("작은 그래프를 만들 수 있음", graph != null && problem == null && graph.Nodes == 6 && graph.Edges == 5 && graph.Buildings == 8, problem);
            if (graph == null) return results;
            var path = new List<(float X, float Z, byte Kind)>();

            var same = 길찾기Policy.Route(graph, 0, 1, path, out var sameMetres);
            Check("같은 변 위의 두 출입구는 그 변을 따라 감", same && Close(sameMetres, 100f) && path.Count == 2, sameMetres.ToString("0.0"));

            var part = 길찾기Policy.Route(graph, 6, 7, path, out var partMetres);
            Check("변의 가운데에서 가운데로는 그 사이만 감", part && Close(partMetres, 50f) && Close(path[0].X, 25f) && Close(path[path.Count - 1].X, 75f),
                partMetres.ToString("0.0"));

            var fast = 길찾기Policy.Route(graph, 2, 3, path, out var fastMetres);
            var roadsOnly = true;
            for (var k = 1; k < path.Count; k++) roadsOnly &= path[k].Kind == 길그래프.Road;
            Check("골목이 더 짧아도 큰길이 더 빠르면 큰길로 감", fast && Close(fastMetres, 160f) && roadsOnly && path.Count == 4,
                fastMetres.ToString("0.0") + " m · 점 " + path.Count);

            var joined = true;
            for (var k = 1; k < path.Count; k++)
                joined &= Close(path[k].X, path[k - 1].X) || Close(path[k].Z, path[k - 1].Z);      // 이 그래프의 변은 모두 가로나 세로다
            Check("길의 점들이 변을 따라 이어짐", joined);

            var again = new List<(float X, float Z, byte Kind)>();
            길찾기Policy.Route(graph, 2, 3, again, out var againMetres);
            var equal = again.Count == path.Count && Close(againMetres, fastMetres);
            for (var k = 0; equal && k < path.Count; k++) equal &= Close(again[k].X, path[k].X) && Close(again[k].Z, path[k].Z);
            Check("같은 물음에는 같은 길이 나옴", equal);

            Check("끊긴 덩어리로는 길이 없음", !길찾기Policy.Route(graph, 0, 4, path, out _) && path.Count == 0);
            Check("출입구가 없는 건물로는 길이 없음", !길찾기Policy.Route(graph, 0, 5, path, out _) && !graph.HasDoor(5) && !graph.HasDoor(99));
            Check("그래프가 없으면 길이 없음", !길찾기Policy.Route(null, 0, 1, path, out _));

            var read = 길그래프.Read(SampleBytes(), out var readProblem);
            var readOk = read != null && readProblem == null && read.Nodes == 6 && read.Edges == 5 && read.Buildings == 8 &&
                길찾기Policy.Route(read, 2, 3, path, out var readMetres) && Close(readMetres, 160f);
            Check("자원 형식(HRTE v1)을 읽으면 같은 그래프가 됨", readOk, readProblem);
            var cut = SampleBytes();
            Array.Resize(ref cut, cut.Length - 9);
            Check("잘린 자원은 받지 않음", 길그래프.Read(cut, out var cutProblem) == null && cutProblem != null, cutProblem);
            Check("없는 마디를 가리키는 변은 받지 않음", 길그래프.From(new[] { 0f, 1f }, new[] { 0f, 0f }, new[] { 0 }, new[] { 7 },
                new[] { 길그래프.Road }, new int[0], new float[0], new float[0], new float[0], out var badProblem) == null && badProblem != null, badProblem);

            // 길을 따라 나아가기: 큰길 160 m는 160 / 8.3초
            길찾기Policy.Route(graph, 2, 3, path, out _);
            var leg = 0;
            var along = 0f;
            var arrived = 기사순찰Policy.Advance(path, ref leg, ref along, 80f / 길찾기Policy.RoadSpeed, out var x, out var z, out _);
            Check("절반의 시간에는 길의 절반에 있음", !arrived && Close(x, 50f) && Close(z, 30f), x.ToString("0.0") + ", " + z.ToString("0.0"));
            arrived = 기사순찰Policy.Advance(path, ref leg, ref along, 80f / 길찾기Policy.RoadSpeed + 0.01f, out x, out z, out _);
            Check("남은 시간을 더 가면 끝에 닿음", arrived && Close(x, 100f) && Close(z, 0f), x.ToString("0.0") + ", " + z.ToString("0.0"));
            길찾기Policy.Route(graph, 0, 1, path, out _);
            leg = 0;
            along = 0f;
            기사순찰Policy.Advance(path, ref leg, ref along, 10f, out x, out _, out _);
            Check("골목에서는 더 느림", Close(x, 10f * 길찾기Policy.AlleySpeed) && 길찾기Policy.AlleySpeed < 길찾기Policy.RoadSpeed, x.ToString("0.0"));
            leg = 0;
            along = 0f;
            Check("시간이 흐르지 않으면 제자리", !기사순찰Policy.Advance(path, ref leg, ref along, 0f, out x, out _, out _) && Close(x, 0f) && leg == 0);

            var seedA = 기사순찰Policy.Seed("region:kr:hjd:1126057500", 0);
            var seedB = 기사순찰Policy.Seed("region:kr:hjd:1126057500", 0);
            var seedC = 기사순찰Policy.Seed("region:kr:hjd:1126057500", 1);
            var order = true;
            var inRange = true;
            for (var k = 0; k < 20; k++)
            {
                var first = 기사순찰Policy.Pick(ref seedA, 7);
                order &= first == 기사순찰Policy.Pick(ref seedB, 7);
                inRange &= first >= 0 && first < 7;
            }
            Check("같은 기사는 같은 차례로 고름", order && inRange && seedC != 기사순찰Policy.Seed("region:kr:hjd:1126057500", 0));
            Check("고를 것이 없으면 고르지 않음", 기사순찰Policy.Pick(ref seedC, 0) == -1);

            // ── 기사와 가게의 빛 ──
            var radius = 기사불빛Policy.RiderRadius;
            var falling = true;
            for (var k = 1; k <= 26; k++) falling &= 기사불빛Policy.Falloff(k, radius) <= 기사불빛Policy.Falloff(k - 1, radius);
            Check("빛은 가운데가 가장 밝고 닿는 거리 밖은 어두움", Close(기사불빛Policy.Falloff(0f, radius), 1f) && falling &&
                기사불빛Policy.Falloff(radius, radius) == 0f && 기사불빛Policy.Falloff(radius + 5f, radius) == 0f);

            var board = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);      // 80 × 80칸. 칸의 가운데는 2.5, 7.5, …
            board.Ride(202.5f, 202.5f, 0.25f);
            var here = board.At(기사불빛Policy.Rider, 202.5f, 202.5f);
            Check("기사가 있는 자리가 밝아지고 먼 곳은 그대로 어두움", here == 255 && board.At(기사불빛Policy.Rider, 262.5f, 202.5f) == 0 &&
                board.At(기사불빛Policy.Rider, 212.5f, 202.5f) > 0 && board.At(기사불빛Policy.Rider, 212.5f, 202.5f) < here, here.ToString());

            board.Fade(0f);
            var still = board.At(기사불빛Policy.Rider, 202.5f, 202.5f) == here;
            board.Fade(3f);
            var dimmer = board.At(기사불빛Policy.Rider, 202.5f, 202.5f);
            board.Fade(30f);
            Check("시간이 흐르지 않으면 그대로이고, 흐르면 어두워지다 꺼짐", still && dimmer > 0 && dimmer < here &&
                board.At(기사불빛Policy.Rider, 202.5f, 202.5f) == 0 && board.Lit(기사불빛Policy.Rider) == 0, dimmer.ToString());

            var one = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);
            one.Ride(102.5f, 102.5f, 0.25f);
            var three = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);
            three.Ride(102.5f, 102.5f, 0.25f);
            three.Ride(302.5f, 102.5f, 0.25f);
            three.Ride(202.5f, 302.5f, 0.25f);
            Check("기사가 많을수록 더 넓게 밝음", one.Awake() > 0 && three.Awake() == one.Awake() * 3,
                one.Awake() + " / " + three.Awake());

            var twice = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);
            twice.Ride(102.5f, 102.5f, 0.25f);
            var warmOnce = twice.At(기사불빛Policy.Warmth, 102.5f, 102.5f);
            twice.Ride(102.5f, 102.5f, 0.25f);
            Check("겹친 자취는 넘치지 않고, 자주 다닌 자리에는 온기가 쌓임", twice.At(기사불빛Policy.Rider, 102.5f, 102.5f) == 255 &&
                twice.Lit(기사불빛Policy.Rider) == one.Lit(기사불빛Policy.Rider) && warmOnce > 0 &&
                twice.At(기사불빛Policy.Warmth, 102.5f, 102.5f) > warmOnce, warmOnce + " → " + twice.At(기사불빛Policy.Warmth, 102.5f, 102.5f));

            var kinds = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);
            kinds.Light(기사불빛Policy.Store, 102.5f, 102.5f, 기사불빛Policy.StoreRadius, 기사불빛Policy.StoreRest);
            kinds.Light(기사불빛Policy.Drop, 302.5f, 302.5f, 기사불빛Policy.DropRadius, 1f);
            var rest = kinds.At(기사불빛Policy.Store, 102.5f, 102.5f);
            kinds.Light(기사불빛Policy.Store, 102.5f, 102.5f, 기사불빛Policy.StoreRadius, 1f);
            Check("종류가 다른 빛은 서로 다른 겹에 적히고, 픽업이 닿은 음식점은 더 밝음", rest > 0 && rest < 255 &&
                kinds.At(기사불빛Policy.Store, 102.5f, 102.5f) == 255 && kinds.At(기사불빛Policy.Drop, 302.5f, 302.5f) == 255 &&
                kinds.Lit(기사불빛Policy.Rider) == 0 && kinds.Lit(기사불빛Policy.Warmth) == 0 &&
                kinds.At(기사불빛Policy.Drop, 102.5f, 102.5f) == 0 && kinds.At(기사불빛Policy.Store, 302.5f, 302.5f) == 0, rest.ToString());

            var edge = new 기사불빛판(0f, 0f, 400f, 400f, 5f, 0f);
            edge.Ride(1f, 1f, 0.25f);
            edge.Ride(399f, 399f, 0.25f);
            edge.Ride(-500f, 200f, 0.25f);
            edge.Light(기사불빛Policy.Drop, 200f, 399f, 기사불빛Policy.DropRadius, 1f);
            Check("판의 가장자리와 판 밖은 늘 어두움", !edge.EdgeLit() && edge.Lit(기사불빛Policy.Rider) > 0 &&
                edge.At(기사불빛Policy.Rider, -10f, 200f) == 0 && edge.At(기사불빛Policy.Rider, 200f, 900f) == 0);

            var wide = new 기사불빛판(0f, 0f, 20000f, 9000f, 5f, 80f);
            Check("넓은 범위에서는 칸을 키워 판이 커지지 않음", wide.Width <= 1024 && wide.Height <= 1024 && wide.Cell > 5f &&
                wide.Cells.Length == wide.Width * wide.Height * 4, wide.Width + " × " + wide.Height);

            // ── 빛이 몰렸을 때 ──
            var phaseSame = 기사불빛Policy.Phase("store:a") == 기사불빛Policy.Phase("store:a") && 기사불빛Policy.Phase(7u) == 기사불빛Policy.Phase(7u) &&
                기사불빛Policy.Phase("store:a") != 기사불빛Policy.Phase("store:b");
            var phaseInside = true;
            var phaseGapSum = 0f;
            for (var phaseKey = 0u; phaseKey < 200u; phaseKey++)
            {
                var phaseNow = 기사불빛Policy.Phase(phaseKey);
                phaseInside &= phaseNow >= 0f && phaseNow < 6.2832f;
                phaseGapSum += 기사불빛Policy.PhaseGap(phaseNow, 기사불빛Policy.Phase(phaseKey + 1u));
            }
            Check("빛마다의 숨의 엇갈림은 같은 열쇠에 늘 같고 0~2π 안", phaseSame && phaseInside);
            Check("이웃한 빛끼리 숨의 박자가 고르게 흩어짐", phaseGapSum / 200f > 0.35f && phaseGapSum / 200f < 0.65f && Close(기사불빛Policy.PhaseGap(0.5f, 0.5f), 0f) &&
                Close(기사불빛Policy.PhaseGap(0f, (float)Math.PI), 1f) && Close(기사불빛Policy.PhaseGap(0.1f, 6.2f), 0.0571f), (phaseGapSum / 200f).ToString("0.00"));
            var crowdFalls = true;
            for (var crowdN = 1; crowdN <= 30; crowdN++) crowdFalls &= 기사불빛Policy.Crowd(crowdN) <= 기사불빛Policy.Crowd(crowdN - 1);
            Check("빛이 몰릴수록 연한 부분이 옅어지되 바닥 아래로는 내려가지 않음", Close(기사불빛Policy.Crowd(0), 1f) && crowdFalls &&
                기사불빛Policy.Crowd(2) < 1f && Close(기사불빛Policy.Crowd(100), 기사불빛Policy.CrowdFloor) && Close(기사불빛Policy.Crowd(-3), 1f));
            길안내Checks.Add(results);          // 길안내: 주소 찾기 · 거리 · 시간의 글 (길안내Policy.cs)
            return results;
        }
    }
}
