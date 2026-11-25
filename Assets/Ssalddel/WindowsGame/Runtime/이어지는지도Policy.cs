using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 이어지는 지도(2026-11-16, 개발 계획 「이어지는 지도 r253」): 전국 · 권역 · 구 · 동을 한 배율 축으로 잇는 순수 규칙.
    //  - 배율은 「m/점」(논리 화면 한 점이 땅에서 몇 m인가, 남북)으로 잰다. 띠: 전국 2,000~400 · 권역 400~60 · 구 60~3 · 동 3 아래.
    //  - 전국 지도는 3배까지는 어디서나, 수도권 권역 바탕이 있는 곳은 21배(60 m/점)까지, 구 지도 자료 안은 넘김 배율까지 확대한다.
    //    넘김 배율 = 구 지도가 가장 넓게 보일 때(구 자료가 화면을 채울 때)와 같은 m/점. 거기서 같은 자리의 구 지도로 바꾼다.
    //  - 넓어질수록 바탕이 단계마다 다른 그림이 되지 않게, 전국 바탕을 무채색으로 서서히 덮고 길 · 구 바탕을 겹쳐 띄운다(바탕은 무채색 · 뜻은 색).
    //  - 화면 · 시계 · 커널을 모른다. 검사(이어지는지도Checks)가 이것만 돌린다.
    internal enum 지도띠 { 전국, 권역, 구, 동 }

    internal static class 이어지는지도Policy
    {
        internal const double 전국경도점 = 88.0 * 0.80901699437494742;   // make_nation_pack.py의 P(): 경도 1° = 88·cos36° 세계 단위
        internal const double 전국위도점 = 88.0;
        internal const double 구경도미터 = 88224.0;                        // 구 지도 팩의 지역 미터(SagajeongDispatchGu.cs와 같은 값)
        internal const double 구위도미터 = 111320.0;
        internal const float 전국한단위미터 = (float)(구위도미터 / 전국위도점);   // 전국 세계 1단위의 남북 길이 ≈ 1,265 m
        internal const float 전국권역경계 = 400f;
        internal const float 권역구경계 = 60f;
        internal const float 구동경계 = 3f;
        internal const float 전국보통최대 = 3f;                             // NationMaxScale
        internal const float 권역최대배율 = 21f;                            // ≈ 60 m/점. 구 지도 자료 밖에서는 이보다 확대하지 않는다

        internal static Vector2 전국세계(double longitude, double latitude) =>
            new((float)(600.0 + (longitude - 127.6) * 전국경도점), (float)(337.0 - (latitude - 36.0) * 전국위도점));

        internal static (double Longitude, double Latitude) 전국경위도(Vector2 world) =>
            (127.6 + (world.x - 600.0) / 전국경도점, 36.0 - (world.y - 337.0) / 전국위도점);

        internal static Vector2 구미터(double longitude, double latitude, double west, double south) =>
            new((float)((longitude - west) * 구경도미터), (float)((latitude - south) * 구위도미터));

        internal static (double Longitude, double Latitude) 구경위도(Vector2 metres, double west, double south) =>
            (west + metres.x / 구경도미터, south + metres.y / 구위도미터);

        internal static float 전국미터(float nationScale) => 전국한단위미터 / Mathf.Max(0.0001f, nationScale);
        internal static float 구미터점(float guScale) => 1f / Mathf.Max(0.000001f, guScale);
        internal static float 전국배율(float metresPerPoint) => 전국한단위미터 / Mathf.Max(0.0001f, metresPerPoint);
        internal static float 넘김배율(float guFullScale) => 전국한단위미터 * guFullScale;

        // 전국 투영은 북위 φ에서 동서가 cos36°/cosφ 배 늘어난다. 넘김 앞뒤 화면 가장자리의 어긋남을 재는 데 쓴다.
        internal static float 동서늘어남(double latitude) => (float)(Math.Cos(36.0 * Math.PI / 180.0) / Math.Cos(latitude * Math.PI / 180.0));

        internal const double 맞춤위도 = 37.575;      // 구 지도 자료의 가운데쯤

        // 정밀 1(2026-11-16): 깊이 들어갈수록 전국 투영의 동서 늘어남을 걷어 낸다. 4배까지 1(지금과 같음), 21배부터 1/1.021 —
        // 구 지도의 실제 미터와 같은 비율이 되어, 같은 자리에서 넘길 때 화면 가장자리까지 맞는다.
        internal static float 동서맞춤(float nationScale) => 1f / Mathf.Lerp(1f, 동서늘어남(맞춤위도), 부드럽게(4f, 권역최대배율, nationScale));

        internal const float 넘김가림초 = 0.3f;        // 전국 → 구로 넘긴 뒤 구 지도의 표식 · 색이 나타나는 시간

        // 정밀 2: 전국 안에서 겹쳐 그리는 구 지도 바탕의 가장자리 흐림 너비(바탕 크기에 대한 비율). 넘김 배율에서는 0(구 지도와 같은 그림).
        internal static float 가장자리흐림(float nationScale, float handoffScale) => 0.08f * (1f - 구로모으기(nationScale, handoffScale));

        // 화면 한 점(실제 화면 점)에 원본 그림 점이 몇 개 들어가나. 1보다 작으면 늘여 그려 흐려진다.
        internal static float 원본점(float texelsPerUnit, float nationScale, float guiScale) => texelsPerUnit / Mathf.Max(0.0001f, nationScale * guiScale);

        internal static 지도띠 띠(float metresPerPoint) =>
            metresPerPoint >= 전국권역경계 ? 지도띠.전국 : metresPerPoint >= 권역구경계 ? 지도띠.권역 : metresPerPoint >= 구동경계 ? 지도띠.구 : 지도띠.동;

        // 배율 축 위의 자리: 0 = 가장 넓음(2,000 m/점), 1 = 가장 가까움(0.4 m/점). 로그 축.
        internal static float 축자리(float metresPerPoint) =>
            Mathf.Clamp01((Mathf.Log(2000f) - Mathf.Log(Mathf.Max(0.4f, metresPerPoint))) / (Mathf.Log(2000f) - Mathf.Log(0.4f)));

        internal static float 부드럽게(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / Mathf.Max(0.0001f, to - from));
            return t * t * (3f - 2f * t);
        }

        // 전국 바탕(남색)을 구 지도와 같은 무채색으로 덮는 정도. 4배까지 0, 30배에서 0.95.
        internal static float 무채색덮기(float nationScale) => 0.95f * 부드럽게(4f, 30f, nationScale);

        // 수도권 길 바탕이 나타나는 정도. 2.4배에서 0, 4.5배에서 다 보인다.
        internal static float 권역길보이기(float nationScale) => 부드럽게(2.4f, 4.5f, nationScale);

        // 구 지도 바탕(그 자리의 실제 구 바탕 그림)이 전국 지도 안에서 나타나는 정도. 넘김 배율의 1/4에서 0, 1/2에서 다 보인다.
        internal static float 구바탕보이기(float nationScale, float handoffScale) => 부드럽게(handoffScale * 0.25f, handoffScale * 0.5f, nationScale);

        // 전국 지도에서 휠로 더 확대할 수 있는 한계. 상세 자료가 있는 곳에서만 깊어진다.
        internal static float 확대한계(bool 권역안, bool 구안, float handoffScale) =>
            구안 ? Mathf.Max(권역최대배율, handoffScale) : 권역안 ? 권역최대배율 : 전국보통최대;

        // 권역 최대에서 넘김 배율까지, 보이는 범위를 구 지도 자료 안으로 모으는 정도(0 = 자유, 1 = 구 자료 안).
        internal static float 구로모으기(float nationScale, float handoffScale) => 부드럽게(권역최대배율, Mathf.Max(권역최대배율 + 1f, handoffScale), nationScale);

        private static readonly float[] 축척후보 = { 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f, 50000f, 100000f, 200000f };

        // 축척 자: 화면에서 min점 이상이 되는 가장 짧은 반듯한 거리.
        internal static float 축척거리(float metresPerPoint, float min = 34f)
        {
            foreach (var candidate in 축척후보)
                if (candidate / Mathf.Max(0.0001f, metresPerPoint) >= min) return candidate;
            return 축척후보[축척후보.Length - 1];
        }

        internal static string 축척글(float metres) => metres >= 1000f ? $"{metres / 1000f:0.#} km" : $"{metres:0} m";

        internal static string 띠글(지도띠 band) => band switch
        {
            지도띠.전국 => "전국",
            지도띠.권역 => "권역",
            지도띠.구 => "구",
            _ => "동",
        };
    }
}
