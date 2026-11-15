using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 명1 명덕 → 빛 규칙의 순수 검사 (2026-11-15). 빌드의 kernel-checks.txt에 붙는다.
    internal static class 명덕빛Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            var none = 명덕빛Policy.Of(null);
            Check("명덕 자료가 없으면 지금의 빛 그대로(0으로 읽지 않음)", none.Size == 1f && none.Strength == 1f && !none.Bound);
            var zero = 명덕빛Policy.Of(0);
            Check("명덕 0은 지금의 빛과 같은 크기 · 세기, 다만 값에 묶임", zero.Size == 1f && zero.Strength == 1f && zero.Bound);

            var rising = true;
            var last = 명덕빛Policy.Of(0);
            for (var mingde = 5; mingde <= 2000; mingde += 5)
            {
                var now = 명덕빛Policy.Of(mingde);
                if (now.Size < last.Size || now.Strength < last.Strength) rising = false;
                last = now;
            }
            Check("명덕이 늘면 크기도 세기도 줄지 않음(우상향)", rising);
            var mid = 명덕빛Policy.Of(명덕빛Policy.Scale);
            Check("쌓이면 크기와 세기가 함께 커짐", mid.Size > 1.3f && mid.Strength > 1.3f,
                $"명덕 {명덕빛Policy.Scale}: 크기 {mid.Size:0.00} · 세기 {mid.Strength:0.00}");
            var early = 명덕빛Policy.Of(10);
            var later = 명덕빛Policy.Of(310);
            Check("처음의 작은 성장이 보이고 오래 쌓을수록 완만해짐",
                early.Size - 1f > (명덕빛Policy.MaxSize - 1f) * 0.1f && later.Size - 명덕빛Policy.Of(300).Size < early.Size - 1f);
            var huge = 명덕빛Policy.Of(1e9);
            Check("상한을 넘지 않음", huge.Size <= 명덕빛Policy.MaxSize && huge.Strength <= 명덕빛Policy.MaxStrength);
            var negative = 명덕빛Policy.Of(-50);
            Check("음수는 지금의 빛 밑으로 줄이지 않음", negative.Size == 1f && negative.Strength == 1f);
            var broken = 명덕빛Policy.Of(double.NaN);
            Check("깨진 값은 자료 준비 전과 같음", broken.Size == 1f && broken.Strength == 1f && !broken.Bound);
            return results;
        }
    }
}
