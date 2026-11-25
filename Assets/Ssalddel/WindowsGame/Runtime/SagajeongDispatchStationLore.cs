using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 역의 내력 문양 (2026-11-19 18:20 사용자 방향: 역 둘레 지역의 역사에 따라 구 지도의 역을 꾸민다. 면목역이면 달리는 말).
    //  - 역 이름 · 동네 이름의 유래를 조사해 문양 하나로 줄이고, 구 지도의 역 위에 둥근 패로 세운다.
    //  - 문양은 선 그림이다(가로세로 -1~1, 위가 +). 모양과 색은 표현이다. 유래 한 줄은 조사한 것을 줄인 말이다.
    //  - 유래(조사 2026-11-19, 서울시 · 자치구 · 언론 자료. 설이 여럿인 곳은 하나를 골랐다):
    //      면목(목장의 문 앞) · 마장(양마장) · 서울숲(옛 뚝섬 경마장) · 사가정(서거정의 호) · 압구정(누정) · 용마산(용마 전설)
    //      망우(태조의 말) · 봉화산(아차산 봉수대) · 버티고개 · 무악재(호랑이 고개) · 돌곶이(돌을 꿴 듯한 산) · 먹골(먹)
    //      왕십리(무학대사 전설) · 답십리(도성에서 십 리 설) · 제기동(제례) · 청량리(절) · 암사(절터) · 동묘앞(동관왕묘)
    //      회기(회묘) · 태릉입구(태릉) · 정릉 · 효창공원앞(효창원) · 뚝섬(둑기) · 광나루 · 한강진 · 마포(나루) · 굽은다리
    //      동대문 · 서대문 · 광화문 · 독립문 · 혜화(성문) · 경복궁 · 종각
    //  - 구 지도 범위 122역 가운데 45역. 나머지는 유래가 뚜렷하지 않거나(새로 붙인 이름 · 학교 이름) 아직 조사 전이다.
    // 보기만 더한다. 규칙을 읽거나 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private sealed class StationLore
        {
            public string Line;              // 유래 한 줄
            public Color Tint;
            public float[][] Strokes;        // 선마다 x0, y0, x1, y1 …
        }

        private static float[] Arc(float x, float y, float r, float fromDegrees, float toDegrees, int n = 10)
        {
            var points = new float[(n + 1) * 2];
            for (var i = 0; i <= n; i++)
            {
                var angle = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, i / (float)n);
                points[i * 2] = x + r * Mathf.Cos(angle);
                points[i * 2 + 1] = y + r * Mathf.Sin(angle);
            }
            return points;
        }

        private static float[] Ring(float x, float y, float r, int n = 10) => Arc(x, y, r, 0f, 360f, n);

        private static float[] S(params float[] points) => points;

        // ── 문양(선 그림). 같은 내력은 같은 문양을 쓴다.
        private static float[][] MarkHorse() => new[]      // 말 머리(옆모습)와 날리는 갈기
        {
            S(-0.5f, -0.85f, -0.42f, -0.2f, -0.28f, 0.35f, -0.05f, 0.72f, 0.08f, 0.96f, 0.2f, 0.7f, 0.5f, 0.45f, 0.86f, 0.05f, 0.8f, -0.16f, 0.55f, -0.1f, 0.3f, 0.1f, 0.2f, -0.2f, 0.34f, -0.55f, 0.45f, -0.85f, -0.5f, -0.85f),
            Ring(0.3f, 0.4f, 0.06f, 6),
            S(-0.28f, 0.35f, -0.6f, 0.34f), S(-0.36f, 0.08f, -0.7f, 0.02f), S(-0.42f, -0.2f, -0.74f, -0.3f), S(-0.14f, 0.6f, -0.42f, 0.66f),
        };

        private static float[][] MarkPavilion() => new[]
        {
            S(-0.95f, 0.22f, -0.5f, 0.4f, 0f, 0.8f, 0.5f, 0.4f, 0.95f, 0.22f, 0.55f, 0.18f, -0.55f, 0.18f, -0.95f, 0.22f),
            S(0f, 0.8f, 0f, 0.96f), S(-0.45f, 0.18f, -0.45f, -0.5f), S(0.45f, 0.18f, 0.45f, -0.5f), S(-0.45f, -0.2f, 0.45f, -0.2f),
            S(-0.7f, -0.5f, 0.7f, -0.5f), S(-0.82f, -0.66f, 0.82f, -0.66f),
        };

        private static float[][] MarkShrine() => new[]      // 사당: 지붕과 문
        {
            S(-0.95f, 0.22f, -0.5f, 0.4f, 0f, 0.8f, 0.5f, 0.4f, 0.95f, 0.22f, 0.55f, 0.18f, -0.55f, 0.18f, -0.95f, 0.22f),
            S(-0.6f, 0.18f, -0.6f, -0.6f, 0.6f, -0.6f, 0.6f, 0.18f), S(-0.18f, -0.6f, -0.18f, -0.05f, 0.18f, -0.05f, 0.18f, -0.6f), S(0f, -0.05f, 0f, -0.6f),
            S(-0.85f, -0.75f, 0.85f, -0.75f),
        };

        private static float[][] MarkPeaks() => new[]       // 봉우리 위로 날아가는 것
        {
            S(-0.95f, -0.6f, -0.45f, 0.15f, -0.15f, -0.2f, 0.3f, 0.45f, 0.95f, -0.6f),
            S(-0.6f, 0.6f, -0.35f, 0.82f, -0.1f, 0.62f), S(-0.1f, 0.62f, 0.15f, 0.9f, 0.42f, 0.74f), S(-0.3f, -0.6f, 0.3f, -0.6f),
        };

        private static float[][] MarkInk() => new[]
        {
            S(0.25f, -0.55f, 0.7f, -0.55f, 0.7f, 0.35f, 0.25f, 0.35f, 0.25f, -0.55f), S(0.25f, 0.1f, 0.7f, 0.1f),
            S(0.15f, 0.92f, -0.25f, -0.15f), S(-0.25f, -0.15f, -0.4f, -0.28f, -0.36f, -0.62f, -0.14f, -0.28f, -0.25f, -0.15f), S(-0.85f, -0.75f, 0.88f, -0.75f),
        };

        private static float[][] MarkPassSun() => new[]
        {
            S(-0.95f, -0.55f, -0.5f, -0.05f, -0.1f, -0.3f, 0.4f, 0.1f, 0.95f, -0.55f), Ring(0f, 0.52f, 0.26f), S(-0.1f, -0.3f, 0.02f, -0.52f, -0.14f, -0.78f),
        };

        private static float[][] MarkPass() => new[]        // 고개: 두 봉우리 사이로 넘는 길
        {
            S(-0.95f, -0.6f, -0.45f, 0.5f, -0.05f, -0.15f, 0.4f, 0.62f, 0.95f, -0.6f), S(-0.05f, -0.15f, 0.12f, -0.4f, -0.1f, -0.62f, 0.06f, -0.88f),
        };

        private static float[][] MarkBeacon() => new[]
        {
            S(-0.5f, -0.75f, -0.3f, -0.05f, 0.3f, -0.05f, 0.5f, -0.75f, -0.5f, -0.75f), S(-0.4f, -0.4f, 0.4f, -0.4f),
            S(0f, -0.05f, -0.25f, 0.28f, -0.08f, 0.33f, -0.15f, 0.62f, 0.05f, 0.95f, 0.1f, 0.6f, 0.28f, 0.4f, 0.12f, 0.3f, 0.2f, 0f),
        };

        private static float[][] MarkMilestone() => new[]   // 십 리: '十'을 새긴 표석과 굽은 길
        {
            S(-0.5f, -0.7f, -0.5f, 0.55f, -0.2f, 0.85f, 0.1f, 0.55f, 0.1f, -0.7f), S(-0.38f, 0.15f, -0.02f, 0.15f), S(-0.2f, 0.42f, -0.2f, -0.15f),
            S(-0.95f, -0.7f, 0.95f, -0.7f), S(0.35f, -0.7f, 0.55f, -0.35f, 0.42f, 0.05f, 0.62f, 0.4f, 0.9f, 0.6f), S(0.6f, -0.7f, 0.78f, -0.38f, 0.66f, 0f, 0.82f, 0.28f, 0.95f, 0.38f),
        };

        private static float[][] MarkWell() => new[]        // 우물 · 약수
        {
            S(-0.5f, -0.65f, -0.5f, -0.15f, 0.5f, -0.15f, 0.5f, -0.65f, -0.5f, -0.65f), S(-0.5f, -0.4f, 0.5f, -0.4f),
            S(-0.5f, -0.15f, -0.5f, 0.5f), S(0.5f, -0.15f, 0.5f, 0.5f), S(-0.78f, 0.48f, 0f, 0.9f, 0.78f, 0.48f, -0.78f, 0.48f),
            S(0f, 0.48f, 0f, 0.14f), Ring(0f, 0.02f, 0.12f, 6), S(-0.9f, -0.82f, 0.9f, -0.82f),
        };

        private static float[][] MarkHouses() => new[]      // 마을: 지붕 셋
        {
            S(-0.9f, -0.6f, -0.9f, -0.2f, -0.62f, 0.05f, -0.34f, -0.2f, -0.34f, -0.6f), S(-0.28f, -0.6f, -0.28f, 0.1f, 0f, 0.45f, 0.28f, 0.1f, 0.28f, -0.6f),
            S(0.34f, -0.6f, 0.34f, -0.2f, 0.62f, 0.05f, 0.9f, -0.2f, 0.9f, -0.6f), S(-0.95f, -0.6f, 0.95f, -0.6f), S(-0.08f, -0.6f, -0.08f, -0.25f, 0.08f, -0.25f, 0.08f, -0.6f),
            S(0.12f, 0.3f, 0.12f, 0.62f, 0.2f, 0.62f),
        };

        private static float[][] MarkCrown() => new[]       // 왕실
        {
            S(-0.7f, -0.4f, -0.8f, 0.4f, -0.4f, 0f, 0f, 0.6f, 0.4f, 0f, 0.8f, 0.4f, 0.7f, -0.4f, -0.7f, -0.4f), S(-0.7f, -0.62f, 0.7f, -0.62f), Ring(0f, 0.74f, 0.1f, 6),
        };

        private static float[][] MarkFort() => new[]        // 보루 · 산성
        {
            S(-0.9f, -0.2f, -0.9f, 0.2f, -0.6f, 0.2f, -0.6f, 0f, -0.3f, 0f, -0.3f, 0.2f, 0f, 0.2f, 0f, 0f, 0.3f, 0f, 0.3f, 0.2f, 0.6f, 0.2f, 0.6f, 0f, 0.9f, 0f, 0.9f, -0.2f, -0.9f, -0.2f),
            S(-0.95f, -0.78f, -0.55f, -0.2f), S(0.55f, -0.2f, 0.95f, -0.78f), S(-0.15f, 0.2f, -0.15f, 0.85f, 0.25f, 0.68f, -0.15f, 0.52f),
        };

        private static float[][] MarkAltar() => new[]       // 제단과 향
        {
            S(-0.9f, -0.7f, 0.9f, -0.7f), S(-0.7f, -0.7f, -0.7f, -0.4f, 0.7f, -0.4f, 0.7f, -0.7f), S(-0.45f, -0.4f, -0.45f, -0.1f, 0.45f, -0.1f, 0.45f, -0.4f),
            S(-0.2f, -0.1f, -0.12f, 0.1f, 0.12f, 0.1f, 0.2f, -0.1f), S(0f, 0.15f, -0.12f, 0.4f, 0.1f, 0.62f, -0.05f, 0.92f),
        };

        private static float[][] MarkPagoda() => new[]      // 절: 삼층탑
        {
            S(-0.6f, -0.7f, 0.6f, -0.7f), S(-0.35f, -0.7f, -0.35f, -0.3f), S(0.35f, -0.7f, 0.35f, -0.3f),
            S(-0.6f, -0.3f, 0.6f, -0.3f, 0.4f, -0.15f, -0.4f, -0.15f, -0.6f, -0.3f), S(-0.25f, -0.15f, -0.25f, 0.15f), S(0.25f, -0.15f, 0.25f, 0.15f),
            S(-0.45f, 0.15f, 0.45f, 0.15f, 0.3f, 0.3f, -0.3f, 0.3f, -0.45f, 0.15f), S(-0.15f, 0.3f, -0.15f, 0.55f), S(0.15f, 0.3f, 0.15f, 0.55f),
            S(-0.32f, 0.55f, 0.32f, 0.55f, 0f, 0.75f, -0.32f, 0.55f), S(0f, 0.75f, 0f, 0.98f),
        };

        private static float[][] MarkTomb() => new[]        // 능 · 묘: 봉분과 비석
        {
            Arc(0.1f, -0.5f, 0.62f, 180f, 0f), S(-0.95f, -0.5f, 0.95f, -0.5f), Arc(0.1f, -0.5f, 0.4f, 150f, 95f, 4),
            S(-0.88f, -0.5f, -0.88f, 0f, -0.7f, 0f, -0.7f, -0.5f), S(-0.3f, -0.72f, 0.5f, -0.72f),
        };

        private static float[][] MarkFlag() => new[]        // 둑기: 큰 깃발
        {
            S(-0.3f, -0.85f, -0.3f, 0.92f), S(-0.3f, 0.82f, 0.62f, 0.66f, 0.45f, 0.42f, 0.66f, 0.16f, -0.3f, 0.06f), S(-0.62f, -0.85f, 0.02f, -0.85f),
            S(-0.1f, 0.06f, -0.14f, -0.2f), S(0.12f, 0.08f, 0.1f, -0.16f), S(0.34f, 0.12f, 0.34f, -0.1f),
        };

        private static float[][] MarkBoat() => new[]        // 나루: 돛단배
        {
            S(-0.8f, -0.2f, -0.55f, -0.5f, 0.55f, -0.5f, 0.8f, -0.2f, -0.8f, -0.2f), S(0f, -0.2f, 0f, 0.82f), S(0f, 0.76f, 0.55f, 0f, 0f, 0f),
            S(-0.95f, -0.78f, -0.6f, -0.66f, -0.3f, -0.78f, 0f, -0.66f, 0.3f, -0.78f, 0.6f, -0.66f, 0.95f, -0.78f),
        };

        private static float[][] MarkStones() => new[]      // 돌곶이: 꿰어 놓은 돌
        {
            S(-0.95f, 0.36f, 0.95f, -0.36f), Ring(-0.55f, 0.2f, 0.24f, 8), Ring(0f, 0f, 0.3f, 8), Ring(0.55f, -0.2f, 0.24f, 8),
        };

        private static float[][] MarkBridge() => new[]      // 굽은 다리
        {
            S(-0.95f, -0.1f, -0.5f, 0.25f, 0f, 0.38f, 0.5f, 0.25f, 0.95f, -0.1f), S(-0.6f, -0.6f, -0.4f, -0.15f, 0f, 0.02f, 0.4f, -0.15f, 0.6f, -0.6f),
            S(-0.5f, 0.25f, -0.5f, 0.05f), S(0f, 0.38f, 0f, 0.02f), S(0.5f, 0.25f, 0.5f, 0.05f),
            S(-0.95f, -0.8f, -0.6f, -0.7f, -0.3f, -0.8f, 0f, -0.7f, 0.3f, -0.8f, 0.6f, -0.7f, 0.95f, -0.8f),
        };

        private static float[][] MarkGate() => new[]        // 성문
        {
            S(-0.8f, -0.75f, -0.8f, -0.05f, 0.8f, -0.05f, 0.8f, -0.75f), S(-0.25f, -0.75f, -0.25f, -0.4f, 0f, -0.22f, 0.25f, -0.4f, 0.25f, -0.75f),
            S(-0.95f, 0.2f, -0.5f, 0.3f, 0f, 0.6f, 0.5f, 0.3f, 0.95f, 0.2f, 0.6f, 0.12f, -0.6f, 0.12f, -0.95f, 0.2f), S(-0.5f, -0.05f, -0.5f, 0.12f), S(0.5f, -0.05f, 0.5f, 0.12f),
            S(-0.95f, -0.75f, 0.95f, -0.75f),
        };

        private static float[][] MarkPalace() => new[]      // 궁: 겹지붕 전각
        {
            S(-0.85f, -0.75f, 0.85f, -0.75f), S(-0.7f, -0.75f, -0.7f, -0.55f, 0.7f, -0.55f, 0.7f, -0.75f),
            S(-0.5f, -0.55f, -0.5f, -0.1f), S(0.5f, -0.55f, 0.5f, -0.1f), S(-0.17f, -0.55f, -0.17f, -0.1f), S(0.17f, -0.55f, 0.17f, -0.1f),
            S(-0.95f, -0.02f, -0.5f, 0.08f, 0.5f, 0.08f, 0.95f, -0.02f, 0.6f, -0.1f, -0.6f, -0.1f, -0.95f, -0.02f), S(-0.38f, 0.08f, -0.38f, 0.32f), S(0.38f, 0.08f, 0.38f, 0.32f),
            S(-0.75f, 0.4f, -0.4f, 0.5f, 0f, 0.82f, 0.4f, 0.5f, 0.75f, 0.4f, 0.45f, 0.32f, -0.45f, 0.32f, -0.75f, 0.4f),
        };

        private static float[][] MarkBell() => new[]        // 종
        {
            S(-0.5f, -0.6f, -0.42f, 0.2f, -0.2f, 0.6f, 0.2f, 0.6f, 0.42f, 0.2f, 0.5f, -0.6f, -0.5f, -0.6f), S(-0.12f, 0.6f, 0f, 0.86f, 0.12f, 0.6f),
            S(-0.45f, -0.2f, 0.45f, -0.2f), S(-0.9f, 0.86f, 0.9f, 0.86f), Ring(0f, -0.4f, 0.1f, 6),
        };

        // 색(Gold · Mint …)이 다른 파일에 있어 정적 초기화 순서를 믿을 수 없다. 처음 쓸 때 만든다.
        private static Dictionary<string, StationLore> stationLores;
        private static Dictionary<string, StationLore> StationLores => stationLores ??= BuildStationLores();

        private static Dictionary<string, StationLore> BuildStationLores()
        {
            var flame = Ink(0xFF9F5A);
            var water = Ink(0x7FC8FF);
            var lores = new Dictionary<string, StationLore>(StringComparer.Ordinal);
            void Add(string station, string line, Color tint, float[][] strokes) => lores[station + "역"] = new StationLore { Line = line, Tint = tint, Strokes = strokes };

            // 말의 길: 한강 변과 아차산 둘레는 조선 때 나라의 말 목장이었다.
            Add("면목", "말 목장의 문 앞", Gold, MarkHorse());
            Add("마장", "말을 기르던 양마장", Gold, MarkHorse());
            Add("서울숲", "옛 뚝섬 경마장", Gold, MarkHorse());
            // 사람과 정자
            Add("사가정", "문인 서거정의 호", Mint, MarkPavilion());
            Add("압구정", "정자에서 온 이름", Mint, MarkPavilion());
            // 산과 고개
            Add("용마산", "용마가 날아간 산", Gold, MarkPeaks());
            Add("망우", "근심을 잊은 고개", Gold, MarkPassSun());
            Add("봉화산", "봉수대가 있던 산", flame, MarkBeacon());
            Add("버티고개", "도성 남쪽의 고개", Soft, MarkPass());
            Add("무악재", "호랑이 나던 고개", Soft, MarkPass());
            Add("돌곶이", "돌을 꿰어 놓은 산", Soft, MarkStones());
            // 일과 살림
            Add("먹골", "먹을 만들던 마을", Soft, MarkInk());
            // 십 리 길
            Add("왕십리", "십 리를 더 가라", Paper, MarkMilestone());
            Add("상왕십리", "십 리를 더 가라", Paper, MarkMilestone());
            Add("답십리", "도성에서 십 리", Paper, MarkMilestone());
            // 제사와 절, 사당
            Add("제기동", "제사를 올리던 터", flame, MarkAltar());
            Add("청량리", "절 이름에서 온 마을", Mint, MarkPagoda());
            Add("암사", "바위 곁의 절터", Mint, MarkPagoda());
            Add("동묘앞", "관우를 모신 사당", flame, MarkShrine());
            // 능과 묘
            Add("회기", "회묘가 있던 곳", Soft, MarkTomb());
            Add("태릉입구", "문정왕후의 능", Soft, MarkTomb());
            Add("정릉", "왕비의 능", Soft, MarkTomb());
            Add("효창공원앞", "왕실의 묘 효창원", Soft, MarkTomb());
            // 군사
            Add("뚝섬", "둑기를 세운 곳", flame, MarkFlag());
            // 나루와 다리
            Add("광나루", "한강의 큰 나루", water, MarkBoat());
            Add("한강진", "한강의 나루", water, MarkBoat());
            Add("마포", "말과 짐이 모인 나루", water, MarkBoat());
            Add("굽은다리", "굽은 다리", water, MarkBridge());
            // 도성과 궁
            Add("동대문", "도성의 동쪽 문", Paper, MarkGate());
            Add("서대문", "도성의 서쪽 문", Paper, MarkGate());
            Add("광화문", "경복궁의 정문", Paper, MarkGate());
            Add("독립문", "독립을 새긴 문", Paper, MarkGate());
            Add("혜화", "도성의 동북쪽 문", Paper, MarkGate());
            Add("경복궁", "조선의 으뜸 궁", Gold, MarkPalace());
            Add("종각", "시각을 알리던 종", Gold, MarkBell());
            // 2차 조사(11-19 18:48 뒤)
            Add("신당", "무당의 신당", flame, MarkShrine());
            Add("약수", "약수가 솟던 곳", water, MarkWell());
            Add("솔샘", "솔숲의 샘", water, MarkWell());
            Add("응봉", "매를 놓던 봉우리", Gold, MarkPeaks());
            Add("둔촌동", "학자 이집의 호", Soft, MarkInk());
            Add("천호", "천 집이 살 터", Gold, MarkHouses());
            Add("군자", "왕자가 태어난 곳", Gold, MarkCrown());
            Add("어린이대공원", "능이 있던 능동", Soft, MarkTomb());
            Add("아차산", "고구려의 보루", Soft, MarkFort());
            Add("북한산보국문", "북한산성의 문", Paper, MarkGate());
            return lores;
        }

        private readonly List<Rect> loreTaken = new();      // 이번 그림에서 이미 선 패와 표식의 자리

        private bool LoreFree(Rect spot)
        {
            foreach (var taken in loreTaken)
                if (taken.Overlaps(spot)) return false;
            return true;
        }

        // 역 곁에 문양 패를 세운다. at은 역 자리, top은 역 이름표의 윗변. 구 지도 좌표.
        // 먼저 선 패나 가게 · 기사 표식과 겹치면 옆으로, 그래도 겹치면 아래로 비켜 선다.
        private void DrawStationLore(string station, Vector2 at, float top, float now)
        {
            if (station == null || !StationLores.TryGetValue(station, out var lore)) return;
            var radius = Mathf.Clamp(guScale * 190f, 17f, 26f);
            var reach = radius + 4f;
            var up = at.y - top + radius + 7f;
            var tries = new[]
            {
                new Vector2(0f, -up), new Vector2(reach * 1.9f, -up * 0.75f), new Vector2(-reach * 1.9f, -up * 0.75f),
                new Vector2(reach * 2.4f, 0f), new Vector2(-reach * 2.4f, 0f), new Vector2(0f, reach + 14f),
                new Vector2(reach * 1.9f, reach + 8f), new Vector2(-reach * 1.9f, reach + 8f),
            };
            var centre = at + tries[0];
            foreach (var offset in tries)
            {
                var spot = at + offset;
                if (!LoreFree(new Rect(spot.x - reach, spot.y - reach, reach * 2f, reach * 2f))) continue;
                centre = spot;
                break;
            }
            loreTaken.Add(new Rect(centre.x - reach, centre.y - reach, reach * 2f, reach * 2f));
            centre.y += Mathf.Sin(now * 1.1f + at.x * 0.05f) * 1.2f;
            var toward = (at - centre).normalized;
            Line(at, centre + toward * radius, Fade(lore.Tint, 0.7f), 1.6f);
            Disc(centre, radius + 7f, Fade(lore.Tint, 0.10f));
            Disc(centre, radius + 2.2f, lore.Tint);
            Disc(centre, radius, Canvas);
            var scale = radius * 0.74f;
            foreach (var stroke in lore.Strokes)
                for (var i = 2; i + 1 < stroke.Length; i += 2)
                    Line(new Vector2(centre.x + stroke[i - 2] * scale, centre.y - stroke[i - 1] * scale),
                        new Vector2(centre.x + stroke[i] * scale, centre.y - stroke[i + 1] * scale), Paper, radius > 21f ? 1.9f : 1.5f);
            if (guScale < GuStationNameScale) return;
            var width = lore.Line.Length * 10.5f + 14f;
            var below = centre.y > at.y;
            var plate = new Rect(centre.x - width / 2f, below ? centre.y + radius + 5f : centre.y - radius - 21f, width, 16f);
            Paint(plate, Fade(Canvas, 0.88f), 5);
            Text(plate, lore.Line, 10, lore.Tint, false, TextAnchor.MiddleCenter);
        }
    }
}
