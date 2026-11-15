using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ssalddel.Sagajeong.Core;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WindowsGame.Kernel
{
    public sealed class KernelCheckReport
    {
        public readonly List<string> Lines = new List<string>();
        public int Passed;
        public int Failed;
        public int Skipped;

        public void Check(string name, bool ok, string detail)
        {
            if (ok) Passed++; else Failed++;
            Lines.Add((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
        }

        public void Skip(string name, string detail)
        {
            Skipped++;
            Lines.Add("[SKIP] " + name + " — " + detail);
        }

        public override string ToString()
        {
            return "Hongdal kernel checks: " + Passed + " passed, " + Failed + " failed, " + Skipped + " skipped\n" +
                string.Join("\n", Lines);
        }
    }

    /// <summary>
    /// 뼈대 관문을 코드로 본다. 빌드할 때 Editor가 돌리고 결과를 출력 폴더에 적는다.
    /// 1) 음식 배달을 모듈로 감싸도 Core를 직접 부를 때와 응답이 글자 하나까지 같다.
    /// 2) 첫 인계 사슬의 수량(5팩 출고·하차, 4팩 인수, 1팩 격리, 남은 3,830mL)이 맞고 재시도해도 한 번만 반영된다.
    /// 3) 저장했다가 되살려도 같은 상태다.
    /// 4) 큰 Simulation의 가맹 공급망을 문(IFranchiseSupplyPort)으로 부를 수 있다.
    /// </summary>
    public static class KernelChecks
    {
        public static KernelCheckReport Run(string sampleJson, string roadJson, string packJson, Func<IFranchiseSupplyPort> sessionPort)
        {
            return Run(sampleJson, roadJson, packJson, sessionPort, null);
        }

        public static KernelCheckReport Run(string sampleJson, string roadJson, string packJson, Func<IFranchiseSupplyPort> sessionPort,
            string territoryJson)
        {
            var report = new KernelCheckReport();
            Guard(report, "음식 배달 모듈", () => FoodEquivalence(report, sampleJson, roadJson));
            Guard(report, "첫 인계 사슬", () => FirstChain(report, packJson, null, "대역 공급망"));
            Guard(report, "큰 Simulation 공급망", () =>
            {
                var port = sessionPort == null ? null : sessionPort();
                if (port == null)
                {
                    report.Skip("큰 Simulation 공급망", "이 빌드에 Simulation 패키지나 연결 어셈블리가 없습니다");
                    return;
                }
                FirstChain(report, packJson, port, "큰 Simulation 공급망");
            });
            Guard(report, "위험 신호", () => Signals(report, packJson));
            Guard(report, "위험 신호: 기사 대기", () => WaitingSignal(report, sampleJson, roadJson));
            Guard(report, "영역", () =>
            {
                if (string.IsNullOrEmpty(territoryJson))
                {
                    report.Skip("영역", "이 빌드에 영역 팩이 없습니다");
                    return;
                }
                Territory(report, territoryJson);
            });
            return report;
        }

        // 신호는 조건이 이어지는 동안 열려 있고, 조건이 풀리면 닫힌다. 저장했다 되살려도 같다.
        private static void Signals(KernelCheckReport report, string packJson)
        {
            var pack = FirstChainPack.Parse(packJson);
            var place = pack.network.store.placeId;
            var stockId = "signal:store:stock:" + place;
            var inspectId = "signal:store:inspect:" + place;
            var kernel = ChainKernel(pack, null);
            kernel.Start(20261112);
            report.Check("신호: 시작할 때는 없다", kernel.Signals.Count == 0, kernel.Signals.Count + "건");
            kernel.Tick(500);
            var stock = kernel.FindSignal(stockId);
            report.Check("신호: 손님이 기다리는데 재료가 없으면 위험", stock != null && stock.IsDanger && stock.PlaceId == place && stock.SinceMs == 500,
                stock == null ? "없음" : stock.Severity + " · " + stock.Message);

            var placed = kernel.Confirm(new GameCommand
            {
                OsId = BusinessIds.FranchiseSupply, Action = "order", CommandId = "signal-order", ElapsedMs = 1000,
            }.With("quantity", 5m));
            kernel.Tick(2000);
            stock = kernel.FindSignal(stockId);
            report.Check("신호: 발주하면 '오는 중' 주의로 내려간다", placed.Ok && stock != null && !stock.IsDanger && stock.SinceMs == 500 && stock.EscalatedMs == 0,
                stock == null ? "없음" : stock.Severity + " · " + stock.Message);

            var loadedAt = 1000 + (pack.center.pickSeconds + pack.center.loadSeconds) * 1000L;
            var arriveAt = loadedAt + pack.transport.travelSeconds * 1000L;
            kernel.Tick(loadedAt - 1);
            kernel.Tick(loadedAt);
            kernel.Tick(arriveAt - 1);
            kernel.Tick(arriveAt);
            var inspect = kernel.FindSignal(inspectId);
            report.Check("신호: 화물이 닿으면 검수 대기 주의, 재고 신호는 닫힌다",
                inspect != null && !inspect.IsDanger && kernel.FindSignal(stockId) == null && kernel.ResolvedSignals.Any(item => item.Id == stockId),
                inspect == null ? "없음" : inspect.Message);
            kernel.Tick(arriveAt + 180000);
            inspect = kernel.FindSignal(inspectId);
            report.Check("신호: 검수를 3분 미루면 위험", inspect != null && inspect.IsDanger && inspect.EscalatedMs == arriveAt + 180000,
                inspect == null ? "없음" : inspect.Severity + " · 올라간 시각 " + inspect.EscalatedMs);

            var saved = kernel.Save();
            var restored = ChainKernel(pack, null);
            var restoredOk = restored.Restore(saved);
            report.Check("신호: 저장했다가 되살려도 같다", restoredOk && restored.Signals.Count == kernel.Signals.Count &&
                restored.FindSignal(inspectId) != null && restored.FindSignal(inspectId).IsDanger,
                restoredOk ? "열림 " + restored.Signals.Count + "건 · 닫힘 " + restored.ResolvedSignals.Count + "건" : "되살리지 못했습니다");

            var inspected = kernel.Confirm(new GameCommand
            {
                OsId = BusinessIds.StoreOperations, Action = "inspect", CommandId = "signal-inspect", ElapsedMs = arriveAt + 181000,
            }.With("accept", 4m).With("quarantine", 1m).With("reject", 0m));
            report.Check("신호: 검수하면 시계가 멈춰 있어도 바로 꺼진다", inspected.Ok && kernel.Signals.Count == 0,
                "열림 " + kernel.Signals.Count + "건 · " + string.Join(", ", kernel.Signals.Select(item => item.ReasonCode)));
        }

        // 기사를 기다리는 주문: 2분(가상)이면 주의, 4분이면 위험, 기사를 지정하면 꺼진다.
        private static void WaitingSignal(KernelCheckReport report, string sampleJson, string roadJson)
        {
            var kernel = new GameKernel();
            var food = new FoodDeliveryModule(sampleJson, roadJson);
            kernel.Register(food);
            kernel.Start(1);
            const string start = "{\"action\":\"start\",\"sessionId\":\"kernel-check-signal\"}";
            kernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "start", PayloadJson = start });
            long elapsed = 0, cautionAt = 0, dangerAt = 0;
            var place = string.Empty;
            while (elapsed < 300000 && dangerAt == 0)
            {
                elapsed += 1000;
                if (!kernel.Tick(elapsed)) break;
                var open = kernel.Signals.Where(item => item.ReasonCode == "dispatch-waiting").ToList();
                if (open.Count == 0) continue;
                place = open[0].PlaceId;
                if (cautionAt == 0) cautionAt = elapsed;
                if (open.Any(item => item.IsDanger)) dangerAt = elapsed;
            }
            report.Check("신호: 기사를 2분 넘게 기다린 주문은 주의, 4분이면 위험",
                cautionAt >= 120000 && cautionAt < 130000 && dangerAt >= 240000 && dangerAt < 250000 &&
                place.StartsWith("store:", StringComparison.Ordinal),
                "주의 " + cautionAt / 1000 + "초 · 위험 " + dangerAt / 1000 + "초 · 자리 " + place);

            var state = JObject.Parse(food.LastReplyJson)["state"];
            var orders = state == null ? new JArray() : state["orders"] as JArray ?? new JArray();
            var couriers = state == null ? new JArray() : state["couriers"] as JArray ?? new JArray();
            foreach (var order in orders)
            {
                if ((string)order["stage"] != "Waiting") continue;
                var orderId = (string)order["id"];
                var storeId = (string)order["storeId"] ?? string.Empty;
                var letter = storeId.Length == 0 ? string.Empty : storeId.Substring(storeId.Length - 1);
                var driver = couriers.Select(courier => (string)courier["id"])
                    .FirstOrDefault(id => !string.IsNullOrEmpty(id) && id.EndsWith(letter, StringComparison.OrdinalIgnoreCase))
                    ?? (string)order["recommendedDriverId"];
                var assign = new JObject
                {
                    ["action"] = "assign",
                    ["elapsedMs"] = elapsed,
                    ["orderId"] = orderId,
                    ["driverId"] = driver,
                    ["commandId"] = "kernel-check-signal-" + orderId,
                    ["revision"] = (long?)order["revision"] ?? 0,
                    ["policy"] = string.Empty,
                }.ToString(Newtonsoft.Json.Formatting.None);
                kernel.Confirm(new GameCommand
                {
                    OsId = BusinessIds.FoodDelivery, Action = "assign", CommandId = "kernel-check-signal-" + orderId, PayloadJson = assign,
                });
            }
            kernel.Tick(elapsed + 1000);
            report.Check("신호: 기사를 지정하면 꺼진다", kernel.Signals.All(item => item.ReasonCode != "dispatch-waiting"),
                "열림 " + kernel.Signals.Count(item => item.ReasonCode == "dispatch-waiting") + "건");
        }

        // 영역: 맞닿아야 가고, 시간이 쌓이면 깊어지고, 위험을 놓치면 한 단계 어두워진다.
        private static void Territory(KernelCheckReport report, string territoryJson)
        {
            var pack = TerritoryPack.Parse(territoryJson);
            var rules = pack.rules;
            var kernel = new GameKernel();
            var module = new TerritoryModule(pack);
            kernel.Register(module);
            kernel.Start(20261112);
            Func<TerritorySnapshot> look = () => (TerritorySnapshot)module.Snapshot();
            var home = pack.Find(pack.homeDong);
            var first = look();
            report.Check("영역: 시작은 홈 동 하나, 맞닿은 동만 후보",
                first.Operating == 1 && first.Entering == 0 && first.Find(pack.homeDong).Level == TerritoryLevels.Operating &&
                first.Dongs.Count(item => item.Candidate) == home.near.Count && first.Dongs.Where(item => item.Candidate).All(item => home.near.Contains(item.Code)),
                home.name + " · 후보 " + string.Join(", ", first.Dongs.Where(item => item.Candidate).Select(item => item.Name)));

            Func<string, string, long, GameCommand> enter = (id, code, at) => new GameCommand
            {
                OsId = BusinessIds.Territory, Action = "enter", CommandId = id, ElapsedMs = at,
            }.With("dong", code);
            var farCode = pack.dongs.First(item => item.code != pack.homeDong && !home.near.Contains(item.code)).code;
            var far = kernel.Confirm(enter("territory-far", farCode, 1000));
            report.Check("영역: 맞닿지 않은 동에는 못 간다", !far.Ok, far.Message);

            var target = pack.Find(home.near[0]);
            var entered = kernel.Confirm(enter("territory-enter", target.code, 1000));
            var again = kernel.Confirm(enter("territory-enter", target.code, 1000));
            var view = look().Find(target.code);
            report.Check("영역: 진출하면 첫 음식점이 꽂힌다", entered.Ok && again.Replayed && view.Level == TerritoryLevels.Entering &&
                view.Pins.Count == 1 && view.Pins[0].Kind == TerritoryPinKinds.Food, entered.Message);
            var second = home.near.Count > 1 ? kernel.Confirm(enter("territory-second", home.near[1], 2000)) : null;
            report.Check("영역: 한 번에 한 곳씩 진출한다", second != null && !second.Ok, second == null ? "후보가 하나뿐" : second.Message);

            var before = look().Dongs.Count(item => item.Candidate);
            long clock = 1000;
            Action<long> run = until => { while (clock < until) { clock += 30000; kernel.Tick(clock); } };
            kernel.Tick(1000 + 10 * 3600000L);   // 시각이 열 시간 건너뛰어도 한 걸음만 쌓인다
            clock = 1000 + 10 * 3600000L;
            var jumped = look().Find(target.code);
            report.Check("영역: 시각이 크게 건너뛰어도 한 걸음만 쌓인다", jumped.Level == TerritoryLevels.Entering && jumped.Progress < 0.2,
                "진행 " + jumped.Progress.ToString("0.00"));
            var open = kernel.FindSignal(TerritoryModule.SignalId(target.code));
            if (open != null) kernel.Confirm(new GameCommand { OsId = BusinessIds.Territory, Action = "respond", CommandId = "territory-clear", ElapsedMs = clock }.With("dong", target.code));

            run(clock + rules.operateAfterMs + 60000);
            view = look().Find(target.code);
            var grown = look();
            report.Check("영역: 시간이 쌓이면 운영으로 오르고 지사·카페가 생긴다", view.Level == TerritoryLevels.Operating &&
                view.Pins.Any(pin => pin.Kind == TerritoryPinKinds.Cafe) &&
                grown.Dongs.Where(item => item.Sig == target.sig).SelectMany(item => item.Pins).Any(pin => pin.Kind == TerritoryPinKinds.Branch),
                target.name + " · 표식 " + string.Join(", ", view.Pins.Select(pin => pin.Title)));
            report.Check("영역: 운영하는 동이 늘면 후보도 늘어난다", grown.Dongs.Count(item => item.Candidate) > before,
                "후보 " + before + " → " + grown.Dongs.Count(item => item.Candidate));

            // 신호가 생길 때까지, 위험으로 오를 때까지, 어두워질 때까지 둔다.
            var signalId = TerritoryModule.SignalId(target.code);
            var guard = 0;
            while (kernel.FindSignal(signalId) == null && guard++ < 400) run(clock + 30000);
            var caution = kernel.FindSignal(signalId);
            report.Check("영역: 가상 매장에서 주의 신호가 생긴다", caution != null && !caution.IsDanger && caution.PlaceId == "dong:" + target.code,
                caution == null ? "없음" : caution.Message);
            run(clock + rules.escalateAfterMs + 30000);
            var danger = kernel.FindSignal(signalId);
            report.Check("영역: 주의를 두면 위험으로 오른다", danger != null && danger.IsDanger, danger == null ? "없음" : danger.Message);
            run(clock + rules.neglectAfterMs + 30000);
            view = look().Find(target.code);
            report.Check("영역: 위험을 놓치면 한 단계 어두워지고 회색으로는 돌아가지 않는다",
                view.Level == TerritoryLevels.Entering && view.DimmedAtMs > 0 && kernel.FindSignal(signalId) == null && view.Pins.Count >= 2,
                TerritoryLevels.Name(view.Level) + " · 표식 " + view.Pins.Count + "개");

            guard = 0;
            while (kernel.FindSignal(signalId) == null && guard++ < 400) run(clock + 30000);
            var respond = kernel.Confirm(new GameCommand { OsId = BusinessIds.Territory, Action = "respond", CommandId = "territory-respond", ElapsedMs = clock }.With("dong", target.code));
            report.Check("영역: 신호를 처리하면 꺼진다", respond.Ok && kernel.FindSignal(signalId) == null, respond.Message);

            var saved = kernel.Save();
            var restoredKernel = new GameKernel();
            var restoredModule = new TerritoryModule(pack);
            restoredKernel.Register(restoredModule);
            var restoredOk = restoredKernel.Restore(saved);
            var a = look();
            var b = restoredOk ? (TerritorySnapshot)restoredModule.Snapshot() : null;
            report.Check("영역: 저장했다가 되살려도 같다", restoredOk && a.Dongs.Count == b.Dongs.Count &&
                a.Dongs.Zip(b.Dongs, (x, y) => x.Level == y.Level && x.Pins.Count == y.Pins.Count && x.Candidate == y.Candidate).All(same => same),
                restoredOk ? "운영 " + b.Operating + " · 진출 " + b.Entering + " · 지사 " + b.Branches : "되살리지 못했습니다");
        }

        private static void Guard(KernelCheckReport report, string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                report.Check(name, false, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void FoodEquivalence(KernelCheckReport report, string sampleJson, string roadJson)
        {
            var direct = new SagajeongSimulationCore(sampleJson, roadJson);
            var kernel = new GameKernel();
            var food = new FoodDeliveryModule(sampleJson, roadJson);
            kernel.Register(food);
            kernel.Start(1);

            var same = true;
            const string start = "{\"action\":\"start\",\"sessionId\":\"kernel-check\"}";
            var a = direct.Execute(start);
            var b = kernel.Confirm(new GameCommand { OsId = BusinessIds.FoodDelivery, Action = "start", PayloadJson = start }).RawJson;
            same &= a == b;

            long elapsed = 0;
            var assigned = new HashSet<string>();
            JToken settlement = null;
            var commands = 0;
            for (var step = 0; step < 3600 && settlement == null; step++)
            {
                elapsed += 1000;
                a = direct.Execute(FoodDeliveryModule.TickRequest(elapsed));
                var accepted = kernel.Tick(elapsed);
                b = food.LastReplyJson;
                same &= a == b;
                var state = JObject.Parse(a)["state"];
                if (!accepted || state == null || state.Type != JTokenType.Object) break;
                var orders = state["orders"] as JArray ?? new JArray();
                var couriers = state["couriers"] as JArray ?? new JArray();
                foreach (var order in orders)
                {
                    var orderId = (string)order["id"];
                    if ((string)order["stage"] != "Waiting" || !assigned.Add(orderId)) continue;
                    var storeId = (string)order["storeId"] ?? string.Empty;
                    var letter = storeId.Length == 0 ? string.Empty : storeId.Substring(storeId.Length - 1);
                    var driver = couriers.Select(courier => (string)courier["id"])
                        .FirstOrDefault(id => !string.IsNullOrEmpty(id) && id.EndsWith(letter, StringComparison.OrdinalIgnoreCase))
                        ?? (string)order["recommendedDriverId"];
                    var assign = new JObject
                    {
                        ["action"] = "assign",
                        ["elapsedMs"] = elapsed,
                        ["orderId"] = orderId,
                        ["driverId"] = driver,
                        ["commandId"] = "kernel-check-" + orderId,
                        ["revision"] = (long?)order["revision"] ?? 0,
                        ["policy"] = string.Empty,
                    }.ToString(Newtonsoft.Json.Formatting.None);
                    a = direct.Execute(assign);
                    b = kernel.Confirm(new GameCommand
                    {
                        OsId = BusinessIds.FoodDelivery, Action = "assign", CommandId = "kernel-check-" + orderId, PayloadJson = assign,
                    }).RawJson;
                    same &= a == b;
                    commands++;
                }
                if ((bool?)state["settled"] == true) settlement = state["settlement"];
            }
            report.Check("음식 배달: 모듈을 거친 응답이 Core 직접 호출과 같다", same, "명령 " + commands + "건 · 가상 " + elapsed / 1000 + "초");
            if (settlement == null || settlement.Type != JTokenType.Object)
            {
                report.Check("음식 배달: 두 주문 정산", false, "정산까지 가지 못했습니다");
            }
            else
            {
                var revenue = (int?)settlement["revenue"] ?? 0;
                var pay = (int?)settlement["driverPay"] ?? 0;
                var balance = (int?)settlement["balance"] ?? 0;
                report.Check("음식 배달: 두 주문 정산 9,600 / 7,200 / 3,600원", revenue == 9600 && pay == 7200 && balance == 3600,
                    "수입 " + revenue + " · 기사 보수 " + pay + " · 차액 " + balance);
            }

            // 다른 사업이 넘긴 준비 완료는 지금 Core가 받지 못한다. 잃어버리지 않고 적어 두는지 본다.
            kernel.Post(new Handoff
            {
                Id = "handoff:kernel-check:ready", Kind = HandoffKinds.FoodReadyForPickup,
                FromOsId = BusinessIds.StoreOperations, ToOsId = BusinessIds.FoodDelivery, SubjectId = "kernel-check", Quantity = 1, Unit = "잔",
            });
            kernel.Tick(elapsed + 1000);
            report.Check("음식 배달: 바깥 주문 인계는 보류로 남는다",
                kernel.Handoffs.Any(item => item.Id == "handoff:kernel-check:ready" && item.State == HandoffStates.Held) && food.HeldHandoffs.Count == 1,
                string.Empty);
        }

        private static GameKernel ChainKernel(FirstChainPack pack, IFranchiseSupplyPort port)
        {
            var kernel = new GameKernel();
            kernel.Register(new FranchiseSupplyModule(pack, port));
            kernel.Register(new LogisticsCenterModule(pack));
            kernel.Register(new CargoTransportModule(pack));
            kernel.Register(new StoreOperationsModule(pack));
            return kernel;
        }

        private static void FirstChain(KernelCheckReport report, string packJson, IFranchiseSupplyPort port, string label)
        {
            var pack = FirstChainPack.Parse(packJson);
            var kernel = ChainKernel(pack, port);
            kernel.Start(20261112);
            var supply = (FranchiseSupplySnapshot)kernel.Module<FranchiseSupplyModule>().Snapshot();
            var expectedKind = port == null ? "Local" : port.Kind;
            report.Check(label + ": 공급망 문이 열린다", supply.PortKind == expectedKind,
                "문 " + supply.PortKind + (string.IsNullOrEmpty(supply.PortNote) ? string.Empty : " · " + supply.PortNote));
            if (supply.PortKind != expectedKind) return;

            Func<string, GameCommand> order = id => new GameCommand
            {
                OsId = BusinessIds.FranchiseSupply, Action = "order", CommandId = id, ElapsedMs = 1000,
            }.With("quantity", 5m);
            var tooMany = kernel.Preview(new GameCommand { OsId = BusinessIds.FranchiseSupply, Action = "order" }.With("quantity", pack.item.maximum + 1));
            report.Check(label + ": 공급안을 벗어난 수량은 막힌다", !tooMany.CanConfirm, tooMany.BlockCode);
            var preview = kernel.Preview(order("check-order"));
            var placed = kernel.Confirm(order("check-order"));
            report.Check(label + ": 보충 발주 5" + pack.item.orderUnit, preview.CanConfirm && placed.Ok, placed.Message);
            if (!placed.Ok) return;
            var again = kernel.Confirm(order("check-order"));
            supply = (FranchiseSupplySnapshot)kernel.Module<FranchiseSupplyModule>().Snapshot();
            report.Check(label + ": 같은 발주를 다시 보내도 한 건", again.Ok && again.Replayed && supply.Orders.Count == 1,
                "발주 " + supply.Orders.Count + "건 · 금액 " + supply.Orders[0].Amount.ToString("0") + " · 공급망 상태 " + supply.Orders[0].PortStatus);
            report.Check(label + ": 발주 금액", supply.Orders[0].Amount == 5m * pack.item.storePrice, supply.Orders[0].Amount.ToString("0"));

            var loadedAt = 1000 + (pack.center.pickSeconds + pack.center.loadSeconds) * 1000L;
            var arriveAt = loadedAt + pack.transport.travelSeconds * 1000L;
            kernel.Tick(loadedAt - 1);
            var center = (LogisticsCenterSnapshot)kernel.Module<LogisticsCenterModule>().Snapshot();
            report.Check(label + ": 상차 전에는 예약만", center.Reserved == 5m && center.Shipped == 0m, "예약 " + center.Reserved + " · 출고 " + center.Shipped);
            kernel.Tick(loadedAt);
            kernel.Tick(arriveAt - 1);
            var cargo = (CargoTransportSnapshot)kernel.Module<CargoTransportModule>().Snapshot();
            report.Check(label + ": 화물이 달리는 중", cargo.Trips.Count == 1 && cargo.Trips[0].Stage == 화물운송상태코드.상차완료,
                cargo.Trips.Count == 0 ? "운송 없음" : cargo.Trips[0].Stage + " " + cargo.Trips[0].StageNote);
            kernel.Tick(arriveAt);
            center = (LogisticsCenterSnapshot)kernel.Module<LogisticsCenterModule>().Snapshot();
            cargo = (CargoTransportSnapshot)kernel.Module<CargoTransportModule>().Snapshot();
            report.Check(label + ": 출고 5 · 하차 5", center.Shipped == 5m && cargo.Trips.Count == 1 && cargo.Trips[0].Unloaded == 5m,
                "출고 " + center.Shipped + " · 하차 " + (cargo.Trips.Count == 0 ? 0m : cargo.Trips[0].Unloaded));

            Func<string, decimal, decimal, decimal, GameCommand> inspect = (id, accept, quarantine, reject) => new GameCommand
            {
                OsId = BusinessIds.StoreOperations, Action = "inspect", CommandId = id, ElapsedMs = arriveAt + 5000,
            }.With("accept", accept).With("quarantine", quarantine).With("reject", reject);
            var wrong = kernel.Preview(inspect(string.Empty, 5m, 1m, 0m));
            report.Check(label + ": 합이 맞지 않는 검수는 막힌다", !wrong.CanConfirm, wrong.Message);
            var inspected = kernel.Confirm(inspect("check-inspect", 4m, 1m, 0m));
            var replay = kernel.Confirm(inspect("check-inspect", 4m, 1m, 0m));
            var second = kernel.Confirm(inspect("check-inspect-2", 4m, 1m, 0m));
            var store = (StoreOperationsSnapshot)kernel.Module<StoreOperationsModule>().Snapshot();
            report.Check(label + ": 검수 4 인수 · 1 격리", inspected.Ok && store.AvailableContent == 4m * pack.item.contentQuantity &&
                store.QuarantineContent == 1m * pack.item.contentQuantity,
                "가용 " + store.AvailableContent + pack.item.contentUnit + " · 격리 " + store.QuarantineContent + pack.item.contentUnit);
            report.Check(label + ": 검수를 다시 보내도 한 번만 반영", replay.Ok && replay.Replayed && !second.Ok, second.Message);
            cargo = (CargoTransportSnapshot)kernel.Module<CargoTransportModule>().Snapshot();
            supply = (FranchiseSupplySnapshot)kernel.Module<FranchiseSupplyModule>().Snapshot();
            report.Check(label + ": 화물은 인수 완료, 본부는 조정 근거를 받는다",
                cargo.Trips[0].Stage == 화물운송상태코드.인수완료 && supply.Orders[0].Stage == SupplyStages.Inspected &&
                supply.Orders[0].AdjustedAmount == 4m * pack.item.storePrice,
                cargo.Trips[0].Stage + " · 조정 뒤 " + supply.Orders[0].AdjustedAmount.ToString("0"));

            // 저장했다가 새 커널에서 되살린다. 그 뒤의 제조는 되살린 쪽에서 한다.
            var saved = kernel.Save();
            var restored = ChainKernel(pack, port == null ? null : port);
            var restoredOk = restored.Restore(saved);
            var restoredStore = restoredOk ? (StoreOperationsSnapshot)restored.Module<StoreOperationsModule>().Snapshot() : null;
            report.Check(label + ": 저장했다가 되살려도 같은 재고", restoredOk && restoredStore.AvailableContent == store.AvailableContent &&
                restoredStore.QuarantineContent == store.QuarantineContent && restored.Handoffs.Count == kernel.Handoffs.Count,
                restoredOk ? "가용 " + restoredStore.AvailableContent + " · 인계 " + restored.Handoffs.Count + "건" : "되살리지 못했습니다");
            if (!restoredOk) return;

            Func<string, GameCommand> produce = id => new GameCommand
            {
                OsId = BusinessIds.StoreOperations, Action = "produce", CommandId = id, ElapsedMs = arriveAt + 60000,
            }.With("cups", 1m);
            var made = restored.Confirm(produce("check-produce"));
            var madeAgain = restored.Confirm(produce("check-produce"));
            restoredStore = (StoreOperationsSnapshot)restored.Module<StoreOperationsModule>().Snapshot();
            var remaining = 4m * pack.item.contentQuantity - pack.recipe.contentPerCup;
            report.Check(label + ": " + pack.recipe.menu + " 한 잔 뒤 남은 " + remaining.ToString("0") + pack.item.contentUnit,
                made.Ok && madeAgain.Replayed && restoredStore.AvailableContent == remaining && restoredStore.Productions.Count == 1,
                "가용 " + restoredStore.AvailableContent + pack.item.contentUnit + " · 제조 " + restoredStore.Productions.Count + "건");
            var ready = restored.Handoffs.LastOrDefault(item => item.Kind == HandoffKinds.FoodReadyForPickup);
            report.Check(label + ": 준비 완료 인계가 남는다", ready != null && ready.State == HandoffStates.Held, ready == null ? "없음" : ready.StateNote);
            var chain = FirstChainProjection.Build(restored, arriveAt + 60000);
            report.Check(label + ": 사슬 여섯 단계", chain.Steps.Count == 6 && chain.Steps.Take(5).All(step => step.State == ChainStepStates.Done),
                string.Join(" | ", chain.Steps.Select(step => step.Title + ": " + step.Line)));
        }
    }
}
