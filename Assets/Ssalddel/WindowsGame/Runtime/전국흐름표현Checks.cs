using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // r155 N1 전국 흐름 표현 규칙의 순수 검사. 빌드의 kernel-checks.txt와 Checks~/NationFlows가 같은 검사를 돌린다.
    internal static class 전국흐름표현Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // 월 m의 값 = (m+1) * 10 + 칸 번호. 입TEU 1월 = 10, 출BL 12월 = 123
            var monthly = new double[48];
            for (var month = 0; month < 12; month++)
                for (var field = 0; field < 4; field++)
                    monthly[month * 4 + field] = (month + 1) * 10 + field;
            var whole = 전국흐름표현Policy.Sum(monthly, 0, 0);
            Check("전체 기간은 열두 달의 합", Math.Abs(whole - Enumerable.Range(1, 12).Sum(m => m * 10)) < 1e-9, whole.ToString());
            Check("한 달은 그 달 칸만", 전국흐름표현Policy.Sum(monthly, 7, 3) == 73 && 전국흐름표현Policy.Sum(monthly, 12, 2) == 122);
            Check("입항 + 출항", 전국흐름표현Policy.Both(monthly, 3, true) == 30 + 31 && 전국흐름표현Policy.Both(monthly, 3, false) == 32 + 33);
            Check("자료가 모자라거나 기간이 틀리면 0", 전국흐름표현Policy.Sum(new double[47], 0, 0) == 0 &&
                전국흐름표현Policy.Sum(monthly, 13, 0) == 0 && 전국흐름표현Policy.Sum(null, 0, 0) == 0);

            Check("항공은 지표가 TEU여도 BL로 잰다", !전국흐름표현Policy.UsesTeu(true, 전국지표.Teu) &&
                전국흐름표현Policy.FlowValue(monthly, true, 전국지표.Teu, 1) == 12 + 13);
            Check("해상은 고른 지표를 따른다", 전국흐름표현Policy.FlowValue(monthly, false, 전국지표.Teu, 1) == 10 + 11 &&
                전국흐름표현Policy.FlowValue(monthly, false, 전국지표.Bl, 1) == 12 + 13);
            Check("지표에 중량이 없다", Enum.GetNames(typeof(전국지표)).SequenceEqual(new[] { "Teu", "Bl" }));

            Check("TEU 연간 등급 경계", 전국흐름표현Policy.Grade(149_999, true, 0) == 0 && 전국흐름표현Policy.Grade(150_000, true, 0) == 1 &&
                전국흐름표현Policy.Grade(500_000, true, 0) == 2 && 전국흐름표현Policy.Grade(1_500_000, true, 0) == 3);
            Check("BL 연간 등급 경계", 전국흐름표현Policy.Grade(999_999, false, 0) == 0 && 전국흐름표현Policy.Grade(1_000_000, false, 0) == 1 &&
                전국흐름표현Policy.Grade(5_000_000, false, 0) == 2 && 전국흐름표현Policy.Grade(20_000_000, false, 0) == 3);
            Check("한 달은 12로 나눈 경계", 전국흐름표현Policy.Grade(12_500, true, 5) == 1 && 전국흐름표현Policy.Grade(12_499, true, 5) == 0 &&
                전국흐름표현Policy.Grade(125_000, true, 5) == 3);
            Check("0과 자료 없음은 그리지 않는다", 전국흐름표현Policy.Grade(0, true, 0) == 0 && 전국흐름표현Policy.Grade(double.NaN, false, 0) == 0 &&
                전국흐름표현Policy.Width(0) == 0f);
            Check("굵기는 등급마다 커진다", 전국흐름표현Policy.Width(1) > 0 && 전국흐름표현Policy.Width(2) > 전국흐름표현Policy.Width(1) &&
                전국흐름표현Policy.Width(3) > 전국흐름표현Policy.Width(2) && 전국흐름표현Policy.Width(4) == 0f);
            Check("거점 크기 단계", 전국흐름표현Policy.HubTier(799_999, true, 0) == 0 && 전국흐름표현Policy.HubTier(800_000, true, 0) == 1 &&
                전국흐름표현Policy.HubTier(3_000_000, true, 0) == 2 && 전국흐름표현Policy.HubTier(1_700_000, false, 1) == 2);

            var outward = new double[48];
            outward[0] = 5;      // 1월 입TEU
            outward[1] = 7;      // 1월 출TEU
            outward[6] = 9;      // 2월 입BL
            outward[7] = 2;      // 2월 출BL
            Check("많은 쪽 방향", 전국흐름표현Policy.OutwardMore(outward, true, 1) && !전국흐름표현Policy.OutwardMore(outward, false, 2) &&
                전국흐름표현Policy.OutwardMore(new double[48], true, 0));

            var flows = new List<(bool, IReadOnlyList<double>)> { (false, monthly), (true, monthly), (false, outward) };
            var gate = 전국흐름표현Policy.GateTotals(flows, 전국지표.Teu, 1);
            Check("출입구 합계: 해상은 지표, 항공은 BL", gate.Sea == 10 + 11 + 5 + 7 && gate.Air == 12 + 13, $"{gate.Sea} / {gate.Air}");
            var bars = 전국흐름표현Policy.MonthTotals(flows, 전국지표.Teu);
            var blBars = 전국흐름표현Policy.MonthTotals(flows, 전국지표.Bl);
            Check("자료 시계: TEU는 해상 거점만, BL은 모든 거점", bars.Length == 12 && bars[0] == 21 + 12 && bars[11] == 120 + 121 &&
                blBars[1] == (22 + 23) * 2 + 9 + 2, $"{bars[0]} · {bars[11]} · {blBars[1]}");

            Check("기간 이동은 전체 → 1월 … 12월 → 전체로 돈다", 전국흐름표현Policy.StepPeriod(12, 1) == 0 &&
                전국흐름표현Policy.StepPeriod(0, -1) == 12 && 전국흐름표현Policy.StepPeriod(0, 1) == 1 && 전국흐름표현Policy.StepPeriod(5, 26) == 5);
            Check("기간 이름", 전국흐름표현Policy.PeriodLabel(0) == "2025 전체" && 전국흐름표현Policy.PeriodLabel(7) == "2025.07");
            Check("짧은 수 표기", 전국흐름표현Policy.Short(9_999) == "9,999" && 전국흐름표현Policy.Short(25_000) == "3만" &&
                전국흐름표현Policy.Short(6_579_529) == "658만" && 전국흐름표현Policy.Short(135_343_314) == "1.4억",
                전국흐름표현Policy.Short(25_000) + " · " + 전국흐름표현Policy.Short(135_343_314));
            return results;
        }
    }
}
