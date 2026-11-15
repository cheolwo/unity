using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ssalddel.WindowsGame
{
    // 굵기의 지표. 중량은 원본 단위('통')가 확인될 때까지 두지 않는다(r16).
    internal enum 전국지표
    {
        Teu,
        Bl
    }

    // r155 N1 (2026-11-13): 전국 지도의 해상·항공 흐름 표현 규칙.
    // 화면 표현이며 업무 상태를 바꾸지 않는다. UnityEngine을 쓰지 않는다(Checks~/NationFlows가 따로 돌린다).
    // 팩의 월별 값은 48칸이다: 월 m(0~11)의 [입TEU, 출TEU, 입BL, 출BL]이 m*4 .. m*4+3에 있다.
    // 기간(period): 0 = 2025년 전체, 1~12 = 그 달.
    internal static class 전국흐름표현Policy
    {
        public const int Months = 12;
        public const int Whole = 0;
        public const int InTeu = 0;
        public const int OutTeu = 1;
        public const int InBl = 2;
        public const int OutBl = 3;

        // 선 굵기 등급의 연간(입+출) 경계. 값에 비례하지 않는 3단이다. 한 달만 볼 때는 12로 나눈 경계를 쓴다.
        public static readonly double[] TeuSteps = { 150_000, 500_000, 1_500_000 };
        public static readonly double[] BlSteps = { 1_000_000, 5_000_000, 20_000_000 };
        // 거점 핀 크기(작은 점 / 중간 / 큰 핀)의 연간 경계.
        public static readonly double[] HubTeuSteps = { 800_000, 3_000_000 };
        public static readonly double[] HubBlSteps = { 1_000_000, 20_000_000 };

        public static int MonthsIn(int period) => period == Whole ? Months : 1;

        public static double Sum(IReadOnlyList<double> monthly, int period, int field)
        {
            if (monthly == null || monthly.Count < Months * 4 || field < 0 || field > 3) return 0;
            if (period >= 1 && period <= Months) return monthly[(period - 1) * 4 + field];
            if (period != Whole) return 0;
            var total = 0.0;
            for (var month = 0; month < Months; month++) total += monthly[month * 4 + field];
            return total;
        }

        // 입항 + 출항.
        public static double Both(IReadOnlyList<double> monthly, int period, bool teu) =>
            Sum(monthly, period, teu ? InTeu : InBl) + Sum(monthly, period, teu ? OutTeu : OutBl);

        // 항공은 늘 BL 건수로 잰다(항공 TEU는 원본에 없다). 해상은 고른 지표를 따른다.
        public static bool UsesTeu(bool air, 전국지표 indicator) => !air && indicator == 전국지표.Teu;

        public static double FlowValue(IReadOnlyList<double> monthly, bool air, 전국지표 indicator, int period) =>
            Both(monthly, period, UsesTeu(air, indicator));

        // 0이면 그리지 않는다(자료가 없거나 작은 흐름을 0 굵기로 그리지 않는다).
        public static int Grade(double value, bool teu, int period) => Step(value, teu ? TeuSteps : BlSteps, period);

        public static int HubTier(double value, bool teu, int period) => Step(value, teu ? HubTeuSteps : HubBlSteps, period);

        private static int Step(double value, double[] steps, int period)
        {
            if (!(value > 0)) return 0;
            var grade = 0;
            foreach (var step in steps)
                if (value * Months >= step * MonthsIn(period)) grade++;      // 나누지 않고 곱해서 비교한다(경계값이 정확히 맞게)
            return grade;
        }

        public static float Width(int grade) => grade switch
        {
            1 => 1.4f,
            2 => 2.4f,
            3 => 3.6f,
            _ => 0f
        };

        // 많은 쪽 방향. true = 출항(거점 → 나라)이 많거나 같다. 쐐기와 흐르는 빛의 방향만 정한다.
        public static bool OutwardMore(IReadOnlyList<double> monthly, bool teu, int period) =>
            Sum(monthly, period, teu ? OutTeu : OutBl) >= Sum(monthly, period, teu ? InTeu : InBl);

        // 출입구 합계: 그린 선만이 아니라 그 방향의 모든 흐름(입+출). 해상은 고른 지표, 항공은 BL.
        public static (double Sea, double Air) GateTotals(IEnumerable<(bool Air, IReadOnlyList<double> Monthly)> flows,
            전국지표 indicator, int period)
        {
            double sea = 0, air = 0;
            if (flows == null) return (0, 0);
            foreach (var (isAir, monthly) in flows)
            {
                var value = FlowValue(monthly, isAir, indicator, period);
                if (isAir) air += value;
                else sea += value;
            }
            return (sea, air);
        }

        // 자료 시계의 달 막대. TEU를 고르면 해상 거점의 TEU 합, BL을 고르면 모든 거점의 BL 합이다.
        public static double[] MonthTotals(IEnumerable<(bool Air, IReadOnlyList<double> Monthly)> hubs, 전국지표 indicator)
        {
            var totals = new double[Months];
            if (hubs == null) return totals;
            foreach (var (isAir, monthly) in hubs)
            {
                if (indicator == 전국지표.Teu && isAir) continue;
                for (var month = 1; month <= Months; month++)
                    totals[month - 1] += Both(monthly, month, indicator == 전국지표.Teu);
            }
            return totals;
        }

        // 기간을 한 칸 옮긴다. 전체(0) → 1월 … 12월 → 전체로 돈다.
        public static int StepPeriod(int period, int delta)
        {
            var count = Months + 1;
            var next = ((period + delta) % count + count) % count;
            return next;
        }

        public static string PeriodLabel(int period) => period == Whole ? "2025 전체" : $"2025.{period:00}";

        // 짧은 수: 1억 이상은 소수 한 자리 억, 1만 이상은 반올림한 만, 그 밖은 쉼표.
        public static string Short(double value)
        {
            if (value >= 100_000_000)
                return (value / 100_000_000).ToString("0.0", CultureInfo.InvariantCulture) + "억";
            if (value >= 10_000)
                return Math.Round(value / 10_000, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture) + "만";
            return Full(value);
        }

        public static string Full(double value) =>
            Math.Round(value, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
    }
}
