using System;

namespace Ssalddel.WindowsGame
{
    // 명1 명덕 → 빛 (2026-11-15, 사용자: 「명덕이 늘면은 뭐 크기도 뭐 세기도 뭐 둘 다 그냥 우상향 하는 식으로 만들면 좋을 것 같아」).
    // 기획 r205(명덕이 쌓일수록 기사 · 음식점의 빛이 커진다, 사용자 확인)에 이번 결정으로 세기(밝기)도 함께 커진다.
    // 곡선 · 상한은 r205 3절에서 미정이라 아래 값은 모두 시험값이다. Unity를 쓰지 않는다.
    //  - 쌓인 명덕이 늘면 빛의 크기와 세기가 함께 커진다. 쌓인 값만 읽으므로 줄지 않는다.
    //  - 처음의 작은 성장이 잘 보이고 오래 쌓을수록 완만해진다(r205 3절의 권함): t = 1 − e^(−명덕/Scale).
    //  - 명덕 값이 없으면(자료 준비 전) 지금의 빛 그대로다. 0으로 읽지 않는다(r209).
    //  - 렌더러가 명덕을 더하거나 만들지 않는다. 권위 있는 누적 기여를 읽기만 한다(r205 4절). 지금은 그 값이 커널에 없다.
    //  - 크기 = 쌓인 명덕, 잠깐 밝아짐 = 방금의 일(떴다 사라지는 알림). 잠깐 밝아짐은 이 배율과 따로 한 번 더 그린다.
    internal readonly struct 명덕빛
    {
        internal 명덕빛(float size, float strength, bool bound)
        {
            Size = size;
            Strength = strength;
            Bound = bound;
        }

        /// <summary>빛 지름의 배율. 1 = 지금의 빛.</summary>
        internal float Size { get; }
        /// <summary>빛 세기(불투명도)의 배율. 1 = 지금의 빛.</summary>
        internal float Strength { get; }
        /// <summary>명덕 값에 묶였는가. false면 자료 준비 전이다.</summary>
        internal bool Bound { get; }
    }

    internal static class 명덕빛Policy
    {
        internal const double Scale = 60;          // 시험값: 이만큼 쌓이면 남은 성장의 약 63%
        internal const float MaxSize = 1.8f;       // 시험값: 크기 상한(지금의 1.8배)
        internal const float MaxStrength = 1.6f;   // 시험값: 세기 상한(지금의 1.6배)

        internal static readonly 명덕빛 Base = new(1f, 1f, false);

        internal static 명덕빛 Of(double? mingde)
        {
            if (mingde == null || double.IsNaN(mingde.Value) || double.IsInfinity(mingde.Value)) return Base;
            var t = (float)(1 - Math.Exp(-Math.Max(0, mingde.Value) / Scale));
            return new 명덕빛(1f + (MaxSize - 1f) * t, 1f + (MaxStrength - 1f) * t, true);
        }
    }
}
