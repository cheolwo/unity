using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 동별 수요(행정동)와 행정동 영역 팩의 관문 검사. 빌드할 때 Editor가 돌리고 kernel-checks.txt에 붙는다.
    /// 규칙 검사는 코드 안의 작은 시험 팩(동 셋, 시간대 하나)으로 한다. 실제 팩의 숫자가 바뀌어도 규칙 검사는 그대로다.
    /// 실제 팩은 형식·서로 맞물림·한 번 굴려 보기만 본다.
    /// </summary>
    public static class DongDemandChecks
    {
        private const long Minute = 60000;
        private const long Step = 30000;

        public static string Run(string territoryJson, string demandJson)
        {
            var lines = new List<string>();
            var failed = 0;
            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (!ok) failed++;
                lines.Add((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
            };
            Guard(check, "규칙", () => Rules(check));
            Guard(check, "실제 팩", () => Packs(check, territoryJson, demandJson));
            return "Hongdal dong demand checks: " + (lines.Count - failed) + " passed, " + failed + " failed\n" + string.Join("\n", lines);
        }

        private static void Guard(Action<string, bool, string> check, string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                check(name + ": 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message);
            }
        }

        // ── 시험 팩: A(처음 동) — B — C 가 한 줄로 맞닿는다. 시간대는 하루 한 칸이라 주문이 고르게 생긴다. ──

        private static TerritoryPack TestTerritory()
        {
            var pack = new TerritoryPack
            {
                schema = TerritoryPack.Schema, unit = "hjd", homeDong = "A",
                rules = new TerritoryRules
                {
                    operateAfterMs = 10 * Minute, settleAfterMs = 10 * Minute, maxEntering = 1, eventEveryMs = 0,
                    escalateAfterMs = 10 * Minute, neglectAfterMs = 10 * Minute, maxStepMs = Minute,
                },
            };
            pack.dongs.Add(new TerritoryPackDong { code = "A", name = "가동", sig = "S", sigName = "시험구", legal = "L", near = new List<string> { "B" } });
            pack.dongs.Add(new TerritoryPackDong { code = "B", name = "나동", sig = "S", sigName = "시험구", legal = "L", near = new List<string> { "A", "C" } });
            pack.dongs.Add(new TerritoryPackDong { code = "C", name = "다동", sig = "S", sigName = "시험구", legal = "L", near = new List<string> { "B" } });
            pack.startDongs.Add(new TerritoryPackStart { code = "A" });
            return pack;
        }

        private static DongDemandPack TestDemand(double ordersA, long startFunds)
        {
            var pack = new DongDemandPack
            {
                schema = DongDemandPack.Schema, label = "시험", bandHours = new List<double> { 0, 24 },
                rules = new DongDemandRules
                {
                    riderOrdersPerHour = 3.5, partnerOrdersPerHour = 4, revenuePerOrder = 4800, riderPayPerOrder = 3600,
                    recruitCost = 20000, contractCost = 6000, engineCost = 60000, engineBoostPerLevel = 0.12, maxEngineLevel = 5,
                    startFunds = startFunds, maxRidersPerDong = 12, maxPartnerShare = 0.5, windowMs = 10 * Minute, minWindowOrders = 0.3,
                    cautionUnmetRatio = 0.35, clearUnmetRatio = 0.15, escalateAfterMs = 5 * Minute, deepenServeRatio = 0.5,
                    maxStepMs = Minute, startHour = 11.5, startRiders = 2, startPartners = 2,
                },
            };
            var even = new List<double> { 1, 1, 1, 1, 1, 1.5, 1 };      // 토요일만 1.5배
            pack.dongs.Add(new DongDemandPackDong { code = "A", name = "가동", restaurants = 10, weight = 0.5, dailyOrders = ordersA, bands = new List<double> { 1 }, weekdays = even, start = true });
            pack.dongs.Add(new DongDemandPackDong { code = "B", name = "나동", restaurants = 10, weight = 0.3, dailyOrders = 120, bands = new List<double> { 1 }, weekdays = even });
            pack.dongs.Add(new DongDemandPackDong { code = "C", name = "다동", restaurants = 2, weight = 0.2, dailyOrders = 96, bands = new List<double> { 1 }, weekdays = even });
            return pack;
        }

        private sealed class Bench
        {
            public GameKernel Kernel = new GameKernel();
            public TerritoryModule Territory;
            public DongDemandModule Demand;
            public long Clock;

            public Bench(double ordersA, long startFunds, bool withTerritory)
            {
                if (withTerritory)
                {
                    Territory = new TerritoryModule(TestTerritory());
                    Kernel.Register(Territory);
                }
                Demand = new DongDemandModule(TestDemand(ordersA, startFunds));
                Kernel.Register(Demand);
                Kernel.Start(7);
            }

            public void RunTo(long minutes)
            {
                var target = minutes * Minute;
                while (Clock < target)
                {
                    Clock = Math.Min(target, Clock + Step);
                    Kernel.Tick(Clock);
                }
            }

            public DongDemandSnapshot Look() { return (DongDemandSnapshot)Demand.Snapshot(); }
            public TerritorySnapshot Land() { return (TerritorySnapshot)Territory.Snapshot(); }
            public Signal SignalOf(string code) { return Kernel.FindSignal(TerritoryModule.SignalId(code)); }

            public GameResult Send(string osId, string action, string id, string dong)
            {
                var command = new GameCommand { OsId = osId, Action = action, CommandId = id, ElapsedMs = Clock };
                if (dong != null) command.With("dong", dong);
                return Kernel.Confirm(command);
            }
        }

        private static bool Near(double value, double expected, double tolerance)
        {
            return Math.Abs(value - expected) <= tolerance;
        }

        private static void Rules(Action<string, bool, string> check)
        {
            // 1) 처음 상태와 깃발 없는 동
            var bench = new Bench(144, 100000, true);       // 가동: 하루 144건 = 한 시간 6건. 기사 2명(7건)·제휴 2곳(8건)이 다 받아낸다
            var first = bench.Look();
            check("처음에는 처음 동만 열려 있고 자금은 팩의 값이다",
                first.Find("A").Open && !first.Find("B").Open && !first.Find("C").Open && first.Funds == 100000 &&
                first.Find("A").Riders == 2 && first.Find("A").Partners == 2 && first.EngineLevel == 1, "자금 " + first.Funds);
            bench.RunTo(20);
            var calm = bench.Look();
            check("깃발 없는 동에는 주문도 신호도 생기지 않는다",
                calm.Find("B").Demand == 0 && calm.Find("C").Demand == 0 && bench.SignalOf("B") == null && bench.SignalOf("C") == null, null);
            check("처음 동은 생긴 주문을 다 받아낸다 (한 시간 6건 × 20분 = 2건)",
                Near(calm.Find("A").Demand, 2.0, 0.01) && Near(calm.Find("A").Served, 2.0, 0.01) && calm.Find("A").Unmet < 1e-9 &&
                bench.SignalOf("A") == null, calm.Find("A").Served.ToString("0.000") + "건");
            check("받아낸 주문만큼 자금이 는다 (건당 수입 4,800 − 보수 3,600)", Math.Abs(calm.Funds - 102400) <= 1, "자금 " + calm.Funds);

            // 2) 깃발을 꽂으면 주문이 생기고, 기사가 없어 놓친다 → 주의 → 위험
            var entered = bench.Send(BusinessIds.Territory, "enter", "check-enter-b", "B");
            bench.RunTo(26);
            var opened = bench.Look().Find("B");
            var caution = bench.SignalOf("B");
            check("깃발을 꽂은 동에 주문이 생긴다 (첫 제휴 한 곳, 기사 없음)",
                entered.Ok && opened.Open && opened.Partners == 1 && opened.Riders == 0 && Near(opened.Demand, 0.5, 0.05) && opened.Served == 0,
                opened.Demand.ToString("0.000") + "건");
            check("기사가 없어 놓치면 주의 신호가 오른다",
                caution != null && !caution.IsDanger && caution.OsId == BusinessIds.DongDemand && caution.ReasonCode == DongDemandBottlenecks.Rider &&
                caution.PlaceId == "dong:B", caution == null ? "신호 없음" : caution.Message);
            check("주문을 받아내지 못하는 동은 깊어지지 않는다", bench.Land().Find("B").Level == TerritoryLevels.Entering && bench.Land().Find("B").Progress == 0, null);
            var closed = bench.Send(BusinessIds.DongDemand, DongDemandActions.Recruit, "check-recruit-c", "C");
            check("깃발 없는 동에는 기사를 모집할 수 없다", !closed.Ok, closed.Message);
            bench.RunTo(33);
            var danger = bench.SignalOf("B");
            check("주의를 두면 위험으로 오른다", danger != null && danger.IsDanger && danger.EscalatedMs > 0 && danger.OsId == BusinessIds.DongDemand,
                danger == null ? "신호 없음" : danger.Message);

            // 3) 기사·제휴를 늘리면 바로 꺼지고, 그 뒤에 깊어진다
            var before = bench.Look().Funds;
            var one = bench.Send(BusinessIds.DongDemand, DongDemandActions.Recruit, "check-recruit-b1", "B");
            var again = bench.Send(BusinessIds.DongDemand, DongDemandActions.Recruit, "check-recruit-b1", "B");
            var two = bench.Send(BusinessIds.DongDemand, DongDemandActions.Recruit, "check-recruit-b2", "B");
            var partner = bench.Send(BusinessIds.DongDemand, DongDemandActions.Contract, "check-contract-b1", "B");
            var grown = bench.Look();
            check("모집·제휴는 비용을 내고, 같은 명령은 한 번만 반영된다",
                one.Ok && again.Replayed && two.Ok && partner.Ok && grown.Find("B").Riders == 2 && grown.Find("B").Partners == 2 &&
                grown.Funds == before - 46000 && grown.Spent == 46000 && bench.Kernel.Ledger.Count(entry => entry.OsId == BusinessIds.DongDemand) == 3,
                "자금 " + before + " → " + grown.Funds);
            bench.RunTo(34);
            check("받아내기 시작하면 신호가 바로 꺼진다", bench.SignalOf("B") == null && bench.Kernel.ResolvedSignals.Any(item => item.PlaceId == "dong:B"), null);
            bench.RunTo(58);
            var deep = bench.Land().Find("B");
            check("받아내는 동은 시간이 쌓여 운영으로 오른다", deep.Level >= TerritoryLevels.Operating && deep.DimmedAtMs == 0, TerritoryLevels.Name(deep.Level));

            // 4) 위험을 놓치면 영역 모듈이 그 동을 어둡게 한다
            var next = bench.Send(BusinessIds.Territory, "enter", "check-enter-c", "C");
            bench.RunTo(84);
            var dimmed = bench.Land().Find("C");
            check("위험을 놓치면 그 동이 어두워진다(처음부터 다시 쌓는다)", next.Ok && dimmed.Level == TerritoryLevels.Entering && dimmed.DimmedAtMs > 0,
                "어두워진 시각 " + dimmed.DimmedAtMs / Minute + "분");
            var limit = bench.Look().Find("C");
            var third = bench.Send(BusinessIds.DongDemand, DongDemandActions.Contract, "check-contract-c1", "C");
            check("제휴는 그 동 음식점 수의 비율까지만 (2곳 × 0.5 = 1곳)", limit.PartnerLimit == 1 && limit.Partners == 1 && !third.Ok, third.Message);

            // 5) 저장했다가 되살려도 같다
            var saved = bench.Kernel.Save();
            var copy = new Bench(144, 100000, true);
            var restored = copy.Kernel.Restore(saved);
            check("저장했다가 되살려도 같은 상태다",
                restored && JsonConvert.SerializeObject(copy.Look()) == JsonConvert.SerializeObject(bench.Look()) &&
                JsonConvert.SerializeObject(copy.Land()) == JsonConvert.SerializeObject(bench.Land()), null);

            // 6) 처리량·엔진·요일·건너뛰기 (영역 모듈 없이: 처음 동만 운영 중으로 본다)
            var solo = new Bench(240, 100000, false);       // 가동: 한 시간 10건. 기사 2명은 7건까지만
            solo.RunTo(60);
            var bound = solo.Look().Find("A");
            check("받아낸 주문은 기사의 처리량을 넘지 않는다 (10건 가운데 7건)",
                Near(bound.Demand, 10, 0.02) && Near(bound.Served, 7, 0.02) && Near(bound.Unmet, 3, 0.02) &&
                bound.Bottleneck == DongDemandBottlenecks.Rider && solo.SignalOf("A") == null, bound.Served.ToString("0.000") + "건");
            check("영역 모듈이 없으면 처음 동만 열린다", !solo.Look().Find("B").Open && solo.Look().Find("B").Demand == 0, null);
            var engine = solo.Send(BusinessIds.DongDemand, DongDemandActions.Engine, "check-engine-1", null);
            var costly = solo.Send(BusinessIds.DongDemand, DongDemandActions.Engine, "check-engine-2", null);
            solo.RunTo(120);
            var boosted = solo.Look();
            check("배차 엔진을 올리면 기사의 처리량이 는다 (7 → 7.84건), 다음 단계는 곱절이라 자금이 모자라면 막힌다",
                engine.Ok && boosted.EngineLevel == 2 && !costly.Ok && boosted.Spent == 60000 &&
                Near(boosted.Find("A").Served - bound.Served, 7.84, 0.03),
                (boosted.Find("A").Served - bound.Served).ToString("0.000") + "건 · " + costly.Message);
            var calendar = solo.Kernel.Confirm(new GameCommand { OsId = BusinessIds.DongDemand, Action = DongDemandActions.Calendar, CommandId = "check-calendar", ElapsedMs = solo.Clock }
                .With("weekday", 5m).With("date", "2026-11-14"));
            var bad = solo.Kernel.Confirm(new GameCommand { OsId = BusinessIds.DongDemand, Action = DongDemandActions.Calendar, CommandId = "check-calendar-bad", ElapsedMs = solo.Clock }
                .With("weekday", 9m));
            var monday = solo.Look().Find("A").Demand;
            solo.RunTo(180);
            var saturday = solo.Look();
            check("요일을 알리면 그 요일의 모양을 따른다 (토요일 1.5배: 한 시간 15건)",
                calendar.Ok && !bad.Ok && saturday.Weekday == 5 && saturday.DateKey == "2026-11-14" && Near(saturday.Find("A").Demand - monday, 15, 0.03),
                (saturday.Find("A").Demand - monday).ToString("0.000") + "건");
            var jumpFrom = saturday.Find("A").Demand;
            solo.Clock += 600 * Minute;                       // 10시간을 한 번에 넘긴다
            solo.Kernel.Tick(solo.Clock);
            var jumped = solo.Look().Find("A").Demand - jumpFrom;
            check("시각이 크게 건너뛰어도 한 걸음만 쌓인다", jumped > 0 && jumped <= 15.0 / 60 + 1e-6, jumped.ToString("0.000") + "건");

            // 7) 자금이 없으면 막힌다
            var poor = new Bench(144, 0, true);
            var refused = poor.Send(BusinessIds.DongDemand, DongDemandActions.Recruit, "check-poor", "A");
            check("자금이 모자라면 모집할 수 없다", !refused.Ok && poor.Look().Find("A").Riders == 2 && !poor.Look().Find("A").CanRecruit, refused.Message);
        }

        private static void Packs(Action<string, bool, string> check, string territoryJson, string demandJson)
        {
            if (string.IsNullOrEmpty(territoryJson) || string.IsNullOrEmpty(demandJson))
            {
                check("실제 팩: 행정동 영역 팩과 동별 수요 팩이 함께 있다", false, "없는 팩이 있습니다");
                return;
            }
            var territory = TerritoryPack.Parse(territoryJson);
            var demand = DongDemandPack.Parse(demandJson);
            check("실제 팩: 영역의 단위가 행정동이고 지어낸 사건이 꺼져 있다", territory.unit == "hjd" && territory.rules.eventEveryMs <= 0, "unit " + territory.unit);
            check("실제 팩: 모든 동에 법정동 대응과 맞닿은 동이 있다",
                territory.dongs.All(dong => !string.IsNullOrEmpty(dong.legal) && dong.near.Count > 0 && dong.near.All(code => territory.Find(code) != null)), null);
            check("실제 팩: 수요 팩의 동이 영역 팩의 동과 같다",
                demand.dongs.Count == territory.dongs.Count && demand.dongs.All(dong => territory.Find(dong.code) != null), demand.dongs.Count + "개 동");
            check("실제 팩: 처음 동이 두 팩에서 같다", demand.dongs.All(dong => dong.start == territory.IsStart(dong.code)),
                string.Join(", ", demand.dongs.Where(dong => dong.start).Select(dong => dong.name)));
            check("실제 팩: 비율의 합은 1, 시간대 비율의 합은 1, 요일 값의 평균은 1",
                Near(demand.dongs.Sum(dong => dong.weight), 1, 1e-3) && demand.dongs.All(dong => Near(dong.bands.Sum(), 1, 1e-3) && Near(dong.weekdays.Average(), 1, 1e-3)), null);
            check("실제 팩: 인구·소득 바탕이 무엇인지 적혀 있다",
                !string.IsNullOrEmpty(demand.basis.population.kind) && !string.IsNullOrEmpty(demand.basis.income.kind),
                "인구 " + demand.basis.population.kind + " · 소득 " + demand.basis.income.kind);

            // 한 번 굴려 보기: 처음 동만 주문이 생기고, 값이 망가지지 않는다
            var kernel = new GameKernel();
            var land = new TerritoryModule(territory);
            var module = new DongDemandModule(demand);
            kernel.Register(land);
            kernel.Register(module);
            kernel.Start(11);
            for (var clock = Step; clock <= 30 * Minute; clock += Step) kernel.Tick(clock);
            var view = (DongDemandSnapshot)module.Snapshot();
            var open = view.Dongs.Where(dong => dong.Open).ToList();
            check("실제 팩: 굴리면 처음 동에만 주문이 생긴다",
                open.Count == demand.dongs.Count(dong => dong.start) && open.All(dong => dong.Demand > 0 && dong.Served <= dong.Demand + 1e-9) &&
                view.Dongs.Where(dong => !dong.Open).All(dong => dong.Demand == 0) &&
                !double.IsNaN(view.Demand) && !double.IsInfinity(view.Demand) && view.Funds >= demand.rules.startFunds,
                "열린 동 " + open.Count + " · 30분 주문 " + view.Demand.ToString("0.00") + "건 · 받아냄 " + view.Served.ToString("0.00") + "건");
            var levels = (TerritorySnapshot)land.Snapshot();
            check("실제 팩: 처음 동은 운영 단계, 맞닿은 동은 후보다",
                levels.Dongs.Where(dong => territory.IsStart(dong.Code)).All(dong => dong.Level == TerritoryLevels.Operating && dong.IsHome) &&
                levels.Dongs.Any(dong => dong.Candidate && dong.CanEnter) && levels.Unit == "hjd",
                "후보 " + levels.Dongs.Count(dong => dong.Candidate) + "개 동");
        }
    }
}
