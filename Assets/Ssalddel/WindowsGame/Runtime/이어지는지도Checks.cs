using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 이어지는 지도의 순수 검사 (2026-11-16). 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 · 화면 없이 돈다.
    internal static class 이어지는지도Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            bool Near(double a, double b, double eps) => Math.Abs(a - b) <= eps;

            // 1. 전국 투영이 전국 팩(make_nation_pack.py)과 같다: 서울 기준점 (127.0, 37.56) → (557.28, 199.72)
            var seoul = 이어지는지도Policy.전국세계(127.0, 37.56);
            Check("전국 투영 = 전국 팩의 P()", Near(seoul.x, 557.28, 0.02) && Near(seoul.y, 199.72, 0.02), $"{seoul.x:0.00}, {seoul.y:0.00}");

            // 2. 사가정 경위도 → 전국 세계 → 경위도 → 구 미터 → 경위도 왕복
            const double lon = 127.0884106, lat = 37.5806971, west = 126.94, south = 37.523;
            var (lon1, lat1) = 이어지는지도Policy.전국경위도(이어지는지도Policy.전국세계(lon, lat));
            var (lon2, lat2) = 이어지는지도Policy.구경위도(이어지는지도Policy.구미터(lon1, lat1, west, south), west, south);
            var errorMetres = Math.Max(Math.Abs(lon2 - lon) * 이어지는지도Policy.구경도미터, Math.Abs(lat2 - lat) * 이어지는지도Policy.구위도미터);
            Check("경위도 → 전국 → 구 → 경위도 왕복 1 m 이하", errorMetres <= 1.0, $"{errorMetres:0.000} m");

            // 3. 넘김 배율: 구 지도가 가장 넓을 때(화면 1371×720, 구 자료 20,732 m × 11,577 m)와 같은 m/점
            var guFull = Mathf.Max(1371f / 20732f, 720f / 11577f);
            var handoff = 이어지는지도Policy.넘김배율(guFull);
            Check("넘김 배율의 m/점 = 구 지도 가장 넓을 때의 m/점",
                Near(이어지는지도Policy.전국미터(handoff), 이어지는지도Policy.구미터점(guFull), 0.01) && handoff > 60f && handoff < 110f,
                $"전국 {handoff:0.0}배 · {이어지는지도Policy.전국미터(handoff):0.0} m/점");

            // 4. 띠 판정
            Check("띠: 1,260 · 420 = 전국, 90 = 권역, 15 · 3 = 구, 1 = 동",
                이어지는지도Policy.띠(1260f) == 지도띠.전국 && 이어지는지도Policy.띠(420f) == 지도띠.전국 && 이어지는지도Policy.띠(90f) == 지도띠.권역 &&
                이어지는지도Policy.띠(15f) == 지도띠.구 && 이어지는지도Policy.띠(3f) == 지도띠.구 && 이어지는지도Policy.띠(1f) == 지도띠.동);

            // 5. 배율 축 자리는 가까울수록 커진다
            var order = new[] { 1260f, 420f, 90f, 60f, 15f, 3f, 1f };
            var rising = true;
            for (var i = 1; i < order.Length; i++) rising &= 이어지는지도Policy.축자리(order[i]) > 이어지는지도Policy.축자리(order[i - 1]);
            Check("배율 축: 가까울수록 오른쪽", rising);

            // 6. 바탕 덮기 · 길 · 구 바탕의 나타남
            Check("3배까지는 전국 바탕 그대로(덮기 0 · 길 0 이하)",
                이어지는지도Policy.무채색덮기(3f) == 0f && 이어지는지도Policy.권역길보이기(2.4f) == 0f);
            Check("깊이 들어가면 무채색 바탕 · 길 · 구 바탕이 다 보인다",
                Near(이어지는지도Policy.무채색덮기(40f), 0.95, 0.001) && 이어지는지도Policy.권역길보이기(5f) == 1f &&
                이어지는지도Policy.구바탕보이기(handoff, handoff) == 1f && 이어지는지도Policy.구바탕보이기(handoff * 0.2f, handoff) == 0f);

            // 7. 확대 한계: 자료가 있는 곳에서만 깊어진다
            Check("확대 한계: 자료 밖 3배 · 수도권 21배 · 구 자료 안 넘김 배율",
                이어지는지도Policy.확대한계(false, false, handoff) == 3f && 이어지는지도Policy.확대한계(true, false, handoff) == 21f &&
                Near(이어지는지도Policy.확대한계(true, true, handoff), handoff, 0.001));

            // 8. 구로 모으기: 21배까지 자유, 넘김 배율에서 다 모음
            Check("구로 모으기: 21배 0 · 넘김 배율 1",
                이어지는지도Policy.구로모으기(21f, handoff) == 0f && 이어지는지도Policy.구로모으기(handoff, handoff) == 1f);

            // 9. 축척 자: 반듯한 거리, 34점 이상
            var bar15 = 이어지는지도Policy.축척거리(15.1f);
            var bar1260 = 이어지는지도Policy.축척거리(1265f);
            Check("축척 자: 15 m/점 → 1 km · 1,265 m/점 → 50 km",
                bar15 == 1000f && bar1260 == 50000f && 이어지는지도Policy.축척글(bar15) == "1 km" && 이어지는지도Policy.축척글(500f) == "500 m",
                $"{bar15} · {bar1260}");

            // 10. 투영 차이: 북위 37.58°에서 전국 투영의 동서 늘어남 ≈ 2.1%
            var stretch = 이어지는지도Policy.동서늘어남(lat);
            Check("전국 투영의 동서 늘어남(사가정) ≈ 1.021", Near(stretch, 1.0208, 0.002), $"{stretch:0.0000}");

            // 11. 정밀 1: 깊이 들어가면 동서 늘어남을 걷어 내 구 지도의 실제 미터와 맞춘다(4배까지는 지금과 같음)
            var fixDeep = 이어지는지도Policy.동서맞춤(handoff) * 이어지는지도Policy.동서늘어남(이어지는지도Policy.맞춤위도);
            Check("동서 맞춤: 3배 1 · 21배와 넘김 배율에서 늘어남을 다 걷어 냄",
                이어지는지도Policy.동서맞춤(3f) == 1f && Near(fixDeep, 1.0, 0.0005) &&
                Near(이어지는지도Policy.동서맞춤(21f) * 이어지는지도Policy.동서늘어남(이어지는지도Policy.맞춤위도), 1.0, 0.0005), $"{fixDeep:0.00000}");

            // 12. 넘김 배율에서 전국의 동서 m/점 = 구 지도 m/점(맞춤 전 2.1% 차이)
            var eastMetres = (float)(이어지는지도Policy.구경도미터 / 이어지는지도Policy.전국경도점 / (handoff * 이어지는지도Policy.동서맞춤(handoff)));
            Check("넘김 배율의 동서 m/점 = 구 지도(0.1% 안)", Near(eastMetres / 이어지는지도Policy.구미터점(guFull), 1.0, 0.001),
                $"{eastMetres:0.00} / {이어지는지도Policy.구미터점(guFull):0.00} m/점");

            // 13. 정밀 2: 가장자리 흐림은 21배에서 8%, 넘김 배율에서 0
            Check("구 바탕 가장자리 흐림: 21배 8% · 넘김 배율 0",
                Near(이어지는지도Policy.가장자리흐림(21f, handoff), 0.08, 0.0001) && 이어지는지도Policy.가장자리흐림(handoff, handoff) == 0f);

            // 14. 정밀 2: 화면 한 점당 원본 점(화면 배율 1.4): 수도권 길 24점/단위는 10배에서 1 이상, 가운데 64점/단위는 30배에서 1 이상
            var region10 = 이어지는지도Policy.원본점(24f, 10f, 1.4f);
            var region30 = 이어지는지도Policy.원본점(24f, 30f, 1.4f);
            var core30 = 이어지는지도Policy.원본점(64f, 30f, 1.4f);
            Check("원본 점: 길 10배 ≥ 1 · 길 30배 < 1(그래서 가운데 그림) · 가운데 30배 ≥ 1",
                region10 >= 1f && region30 < 1f && core30 >= 1f, $"{region10:0.00} · {region30:0.00} · {core30:0.00}");
            return results;
        }
    }
}
