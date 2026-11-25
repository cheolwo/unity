using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 역 중심 보기 (2026-11-14). 사용자 방향: 동 단위로도, 역을 중심으로도 사업을 생각할 수 있다.
    // 사용자 스타일에 맞게 고르는 것이니 우선 옵션으로 둔다.
    // 여기는 순수 규칙이다: 역 목록(임시), 한 판의 둥근 창, 판의 둘레, 자료가 덮는 비율.
    //  - 보는 판만 역 중심이다. 깃발·수요·제휴는 행정동 그대로다.
    //    (기존 기획 정본과 같다: 행정동은 수집·집계의 정본이고 역 중심 창은 조회·표현 단위다.)
    //  - 역 자리는 임시 값이다. 공식 역 기준점·역 식별자와 맞춘 것이 아니다. 역 목록 자료가 들어오면 그것으로 바꾼다.
    // 게임 상태를 읽지도 바꾸지도 않는다.
    internal sealed class 역자리
    {
        public string Id;
        public string Name;
        public string Line;
        public double Latitude;
        public double Longitude;
        public string Basis;         // 이 자리를 어디서 얻었는가
    }

    internal static class 역중심Policy
    {
        public const double Radius = 600;        // 한 판의 반경(m, 표현값)
        // 역 세 겹(2026-11-19). 서울시 역세권 기준(승강장 경계에서 1차 250 m · 2차 500 m)에서 빌린 표현값이다. 승강장이 아니라 역 자리에서 잰다.
        public const double Core = 250;          // 역 핵심: 가장 정밀하고 밝다
        public const double Area = 500;          // 역세권: 그 너머는 낮고 가라앉는다
        public const double FarHeight = 0.38;    // 역세권 너머 건물의 높이 배율(표현값)
        public const double Fade = 170;          // 그 바깥으로 흐려지며 이어지는 폭(m, 표현값)

        public static readonly IReadOnlyList<역자리> Stations = new[]
        {
            new 역자리
            {
                Id = "station:temp:sagajeong", Name = "사가정역", Line = "7호선",
                Latitude = 37.580912, Longitude = 127.088502, Basis = "행정동 모듈의 자료 원점",
            },
            new 역자리
            {
                Id = "station:temp:myeonmok", Name = "면목역", Line = "7호선",
                Latitude = 37.588671, Longitude = 127.087503, Basis = "게임 코드에 있던 값",
            },
        };

        // 창의 바깥 정도: 반경 안은 0, 반경에서 fade만큼 나가면 1. 셰이더가 같은 식으로 흐리게 하고 1에서 끊는다.
        public static double Outside(double distance, double radius, double fade) =>
            Math.Max(0, Math.Min(1, (distance - radius) / Math.Max(1, fade)));

        // 그 점의 표식을 그리는가. 흐려지는 띠의 절반까지는 그린다.
        public static bool Shows(double distance, double radius, double fade) => Outside(distance, radius, fade) <= 0.5;

        // 판의 둘레. 화면 맞춤과 테두리에 쓴다. 첫 점은 정북이고 시계 방향으로 돈다.
        public static List<(double East, double North)> Ring(double east, double north, double radius, int points)
        {
            var count = Math.Max(8, points);
            var ring = new List<(double East, double North)>(count);
            for (var i = 0; i < count; i++)
            {
                var angle = 2 * Math.PI * i / count;
                ring.Add((east + radius * Math.Sin(angle), north + radius * Math.Cos(angle)));
            }
            return ring;
        }

        // 판 안에서 자료가 덮는 넓이의 비율(0~1). covered는 그 점에 읽어 둔 자료가 있는지 답한다. 격자 표본으로 센다.
        public static double Coverage(double east, double north, double radius, Func<double, double, bool> covered, int steps = 40)
        {
            if (covered == null || radius <= 0) return 0;
            var count = Math.Max(4, steps);
            var cell = 2 * radius / count;
            var total = 0;
            var hit = 0;
            for (var row = 0; row < count; row++)
            {
                var dy = -radius + (row + 0.5) * cell;
                for (var column = 0; column < count; column++)
                {
                    var dx = -radius + (column + 0.5) * cell;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    total++;
                    if (covered(east + dx, north + dy)) hit++;
                }
            }
            return total == 0 ? 0 : hit / (double)total;
        }

        // 역 넘기기. 끝에서 처음으로 돈다.
        public static int Step(int index, int step, int count) => count <= 0 ? 0 : ((index + step) % count + count) % count;
    }
}
