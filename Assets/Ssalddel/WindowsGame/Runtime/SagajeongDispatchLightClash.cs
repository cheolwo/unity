using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 빛의 번짐, 그리고 (나중에) 어둠의 공격 (2026-11-19 21:04 · 21:55 사용자 방향).
    //  - 21:55 결정: **평소에는 공격하지 않는다.** 빛이 그쪽으로 가니 서서히 밝아진다 — 거기까지만 기획해 둔다.
    //    **악의를 가진 NPC를 만나면** 그때 검은 알갱이가 공격도 한다(뒤의 일).
    //  - 그래서 평소: 내 운영이 닿은 역에서 가장 가까운 닿지 않은 역으로 밝은 알갱이가 흘러가고, 받는 역에 옅은 새벽빛이 돈다.
    //    맞섬(전선 · 불티 · 검은 알갱이)은 그 역이 적대일 때만 그린다. 지금은 적대인 역이 없다(StationHostile이 늘 false).
    //  - 표현뿐이다. 받는 역이 실제로 밝아지는 것은 영역 규칙(진출 · 운영)이 정한다. 여기는 StationDepth만 읽는다.
    public sealed partial class SagajeongDispatchView
    {
        private const float ClashReachMetres = 1900f;      // 이 거리 안의 이웃 역과 맞선다(표현값)
        private bool subwayClashOn = true;
        private readonly List<(SubwayStation Bright, SubwayStation Dark, float Depth)> clashPairs = new();
        private int clashPairsStamp = -1;

        private static readonly Color ClashDark = new(0.03f, 0.015f, 0.06f, 1f);       // 흑색 계열의 빛
        private static readonly Color ClashDarkRim = new(0.52f, 0.30f, 0.82f, 1f);     // 그 가장자리(검은 빛이 지도에서 보이게)

        private void RefreshClashPairs()
        {
            // 영역은 천천히 바뀐다. 1초에 한 번쯤만 다시 짝을 짓는다.
            var stamp = Mathf.FloorToInt(Time.unscaledTime);
            if (stamp == clashPairsStamp) return;
            clashPairsStamp = stamp;
            clashPairs.Clear();
            var pack = subwayGuPack;
            if (pack?.stations == null || guSpace == null) return;
            foreach (var dark in pack.stations)
            {
                if (dark == null || StationDepth(dark) > 0.05f) continue;
                var to = 이어지는지도Policy.구미터(dark.longitude, dark.latitude, guSpace.west, guSpace.south);
                SubwayStation nearest = null;
                var best = ClashReachMetres * ClashReachMetres;
                var bestDepth = 0f;
                foreach (var bright in pack.stations)
                {
                    if (bright == null || bright == dark) continue;
                    var depth = StationDepth(bright);
                    if (depth <= 0.05f) continue;
                    var from = 이어지는지도Policy.구미터(bright.longitude, bright.latitude, guSpace.west, guSpace.south);
                    var distance = (to - from).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = bright;
                    bestDepth = depth;
                }
                if (nearest != null) clashPairs.Add((nearest, dark, bestDepth));      // 받는 역마다 가장 가까운 내 역 하나
            }
        }

        // 그 역에 악의를 가진 이가 있는가. 아직 그런 NPC가 없다. 생기면 여기서 답한다.
        private static bool StationHostile(SubwayStation station) => false;

        // DrawGuSubway가 노선 다음, 역 고리 앞에 부른다(Repaint).
        private void DrawGuLightClash(float now)
        {
            if (!subwayClashOn || territory == null) return;
            RefreshClashPairs();
            var hazed = new HashSet<SubwayStation>();
            for (var index = 0; index < clashPairs.Count; index++)
            {
                var (bright, dark, depth) = clashPairs[index];
                var a = GuSubwayPoint(bright.latitude, bright.longitude);
                var b = GuSubwayPoint(dark.latitude, dark.longitude);
                var span = b - a;
                var length = span.magnitude;
                if (length < 24f) continue;
                var along = span / length;
                var side = new Vector2(-along.y, along.x);
                var seed = index * 37 + 11;

                if (!StationHostile(dark))
                {
                    // 평소: 빛이 흘러가고, 받는 역에 옅은 새벽빛이 돈다. 싸우지 않는다.
                    var dawn = 0.5f + 0.5f * Mathf.Sin(now * 0.8f + seed);
                    var glow = Mathf.Clamp(200f * guScale, 10f, 44f);
                    Disc(b, glow * 1.5f, Fade(Gold, 0.035f + 0.03f * dawn));
                    Disc(b, glow * 0.8f, Fade(Gold, 0.05f + 0.04f * dawn));
                    // K1b(11-20): 먹은 키워드가 많을수록 더 많은 빛이 흘러간다(시험 값 · SagajeongDispatchStationPlace.cs).
                    var drift = Mathf.Clamp(2 + PlaceOf(bright, depth).Eaten * 2, 2, 12);
                    for (var k = 0; k < drift; k++)
                    {
                        var t = Mathf.Repeat(now * 0.22f + k / (float)drift + SubwayHash(seed, k) * 0.2f, 1f);
                        var sway = side * (Mathf.Sin(now * 1.6f + k * 1.7f + seed) * 5f);
                        var fade = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.55f, 1f, depth);
                        var light = Vector2.Lerp(a, b, t) + sway;
                        Disc(light, 11f, Fade(Gold, 0.16f * fade));
                        Disc(light, 4f, Fade(k % 2 == 0 ? Paper : Gold, 0.95f * fade));
                    }
                    continue;
                }

                // 어두운 역의 검은 빛: 맞서는 역에만 한 번 깐다.
                if (hazed.Add(dark))
                {
                    var haze = Mathf.Clamp(260f * guScale, 12f, 60f);
                    Disc(b, haze * 1.7f, Fade(ClashDarkRim, 0.07f));
                    Disc(b, haze * 1.4f, Fade(ClashDark, 0.34f));
                    Disc(b, haze, Fade(ClashDark, 0.46f));
                    Disc(b, haze * 0.55f, Fade(ClashDark, 0.6f));
                    Hoop(b, haze * (0.95f + 0.06f * Mathf.Sin(now * 1.7f + seed)), Fade(ClashDarkRim, 0.7f), 1.8f);
                    Hoop(b, haze * (1.35f + 0.08f * Mathf.Sin(now * 1.1f + seed)), Fade(ClashDarkRim, 0.28f), 1.2f);
                }

                // 전선: 내 운영이 깊을수록 상대 쪽에 가 있다. 숨 쉬듯 밀고 밀린다.
                var push = Mathf.Lerp(0.36f, 0.68f, depth) + 0.05f * Mathf.Sin(now * 0.9f + seed * 0.7f);
                var front = a + span * push;

                // 흘러오는 알갱이
                const int motes = 7;
                for (var k = 0; k < motes; k++)
                {
                    var t = Mathf.Repeat(now * 0.32f + k / (float)motes + SubwayHash(seed, k) * 0.2f, 1f);
                    var sway = side * (Mathf.Sin(now * 2.3f + k * 1.7f + seed) * 4f);
                    var fade = Mathf.Sin(t * Mathf.PI);
                    var light = Vector2.Lerp(a, front, t) + sway;
                    Disc(light, 12f, Fade(Gold, 0.20f * fade));
                    Disc(light, 4.6f, Fade(k % 2 == 0 ? Paper : Gold, fade));
                    var shade = Vector2.Lerp(b, front, t) - sway;
                    Disc(shade, 10f, Fade(ClashDarkRim, 0.14f * fade));
                    Disc(shade, 6f, Fade(ClashDark, fade));
                    Hoop(shade, 6.4f, Fade(ClashDarkRim, 0.95f * fade), 1.6f);
                }

                // 부딪는 자리
                var flick = Mathf.Floor(now * 9f);
                var beat = 0.5f + 0.5f * Mathf.Sin(now * 6f + seed);
                Disc(front - along * 7f, 18f, Fade(Gold, 0.22f + 0.10f * beat));
                Disc(front + along * 7f, 18f, Fade(ClashDark, 0.75f));
                Hoop(front, 16f + 8f * beat, Fade(Paper, 0.5f * (1f - beat)), 1.6f);
                Line(front - side * 20f - along * 1.5f, front + side * 20f - along * 1.5f, Paper, 2.4f);
                Line(front - side * 20f + along * 2f, front + side * 20f + along * 2f, ClashDarkRim, 2.4f);
                for (var s = 0; s < 7; s++)
                {
                    var angle = SubwayHash(seed + (int)flick, s) * Mathf.PI * 2f;
                    var reach = 10f + 20f * SubwayHash(seed + (int)flick, s + 9);
                    var ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(front + ray * 3f, front + ray * reach, s % 2 == 0 ? Gold : ClashDarkRim, 1.8f);
                }
            }
        }
    }
}
