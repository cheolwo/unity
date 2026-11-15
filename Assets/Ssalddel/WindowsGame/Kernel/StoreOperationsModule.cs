using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WindowsGame.Kernel
{
    public static class ReceiptStages
    {
        public const string Waiting = "Waiting";         // 하차됨 · 검수 대기
        public const string Inspected = "Inspected";     // 인수·격리·거절로 나눔
    }

    public sealed class StoreReceiptState
    {
        public string Id = string.Empty;
        public string ChainId = string.Empty;
        public string TripId = string.Empty;
        public string OrderId = string.Empty;
        public string ItemId = string.Empty;
        public string ItemName = string.Empty;
        public string Unit = string.Empty;
        public decimal Unloaded;
        public decimal SuggestAccept;
        public decimal SuggestQuarantine;
        public string SuggestReason = string.Empty;
        public decimal Accepted;
        public decimal Quarantined;
        public decimal Rejected;
        public string Stage = string.Empty;
        public long ArrivedAtMs;
        public long InspectedAtMs;
    }

    public sealed class StoreProductionState
    {
        public string Id = string.Empty;
        public string ChainId = string.Empty;
        public string Menu = string.Empty;
        public decimal Cups;
        public decimal ContentUsed;
        public long AtMs;
    }

    public sealed class StoreOperationsSnapshot
    {
        public string StoreName = string.Empty;
        public string StorePlaceId = string.Empty;
        public string ItemName = string.Empty;
        public string OrderUnit = string.Empty;
        public string ContentUnit = string.Empty;
        public decimal ContentPerPack;
        public decimal AvailableContent;
        public decimal QuarantineContent;
        public decimal UsedContent;
        public string Menu = string.Empty;
        public decimal ContentPerCup;
        public decimal WaitingCups;
        public decimal Sales;
        public List<StoreReceiptState> Receipts = new List<StoreReceiptState>();
        public List<StoreProductionState> Productions = new List<StoreProductionState>();
    }

    /// <summary>
    /// 점포 업무의 최소형: 하차된 화물의 검수(인수·격리·거절)와 한 잔 제조.
    /// 인수 확정량만 점포 가용 재고가 된다(r71). 수량이 맞는지는 운영 플랫폼의 업무수량보존Policy로 본다.
    /// </summary>
    public sealed class StoreOperationsModule : IBusinessModule
    {
        private readonly FirstChainPack pack;
        private GameKernel kernel;
        private State state = new State();
        private long now;

        public StoreOperationsModule(FirstChainPack pack)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        }

        public string OsId { get { return BusinessIds.StoreOperations; } }
        private string Place { get { return pack.network.store.placeId; } }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            now = 0;
            state = new State { WaitingCups = pack.waitingCups };
        }

        public void Tick(long elapsedMs)
        {
            if (elapsedMs > now) now = elapsedMs;
            RefreshSignals();
        }

        private const long InspectionDangerMs = 180000;   // 하차하고 3분(가상)이 지나도록 검수하지 않으면 위험

        // 조건이 이어지는 동안 신호를 열어 둔다: 검수를 기다리는 화물, 손님이 기다리는데 떨어진 재료, 한 팩도 안 남은 재료.
        private void RefreshSignals()
        {
            if (kernel == null) return;
            var store = pack.network.store.name;
            var waiting = WaitingReceipt();
            var inspectId = "signal:store:inspect:" + Place;
            if (waiting != null)
            {
                var late = now - waiting.ArrivedAtMs >= InspectionDangerMs;
                kernel.RaiseSignal(new Signal
                {
                    Id = inspectId, OsId = OsId, PlaceId = Place,
                    Severity = late ? SignalSeverities.Danger : SignalSeverities.Caution,
                    ReasonCode = "inspection-waiting", Title = store,
                    Message = late ? "하차한 화물이 오래 기다리고 있습니다. 검수해야 쓸 수 있습니다." : "화물이 도착했습니다. 검수를 기다립니다.",
                    ActionLabel = "검수", SinceMs = now,
                });
            }
            else
                kernel.ClearSignal(inspectId, now);

            var stockId = "signal:store:stock:" + Place;
            var available = Balance(StockBuckets.Available);
            if (waiting == null && state.WaitingCups > 0 && available < pack.recipe.contentPerCup)
            {
                // 본부가 받은 발주가 있고 그 화물이 아직 하차 전이면 오는 중이다.
                var inbound = kernel.Handoffs.Any(item => item.Kind == HandoffKinds.SupplyOrderAccepted &&
                    item.Value("storePlaceId") == Place && state.Receipts.All(receipt => receipt.ChainId != item.ChainId));
                kernel.RaiseSignal(new Signal
                {
                    Id = stockId, OsId = OsId, PlaceId = Place,
                    Severity = inbound ? SignalSeverities.Caution : SignalSeverities.Danger,
                    ReasonCode = inbound ? "stock-inbound" : "stock-out", Title = store,
                    Message = inbound ? pack.item.name + " 화물이 오는 중입니다. 도착하면 검수하세요."
                        : pack.item.name + " 재고가 없습니다. 기다리는 손님이 있습니다. 보충 발주가 필요합니다.",
                    ActionLabel = inbound ? string.Empty : "보충 발주", SinceMs = now,
                });
            }
            else if (waiting == null && available >= pack.recipe.contentPerCup && available < pack.item.contentQuantity)
            {
                kernel.RaiseSignal(new Signal
                {
                    Id = stockId, OsId = OsId, PlaceId = Place, Severity = SignalSeverities.Caution,
                    ReasonCode = "stock-low", Title = store,
                    Message = pack.item.name + " 재고가 한 " + pack.item.orderUnit + "도 남지 않았습니다. 보충 발주가 필요합니다.",
                    ActionLabel = "보충 발주", SinceMs = now,
                });
            }
            else
                kernel.ClearSignal(stockId, now);
        }

        public GamePreview Preview(GameCommand command)
        {
            if (command.Action == "inspect")
            {
                var receipt = WaitingReceipt();
                if (receipt == null) return GamePreview.Blocked("검수를 기다리는 화물이 없습니다.", "NoWaitingReceipt");
                var problem = InspectionProblem(receipt, command.Number("accept"), command.Number("quarantine"), command.Number("reject"));
                var preview = new GamePreview
                {
                    CanConfirm = problem == null,
                    Message = problem ?? "하차 " + receipt.Unloaded.ToString("0") + receipt.Unit + "을 나눕니다.",
                    BlockCode = problem == null ? string.Empty : "InspectionQuantityMismatch",
                };
                preview.Candidates.Add(new GameCandidate
                {
                    Id = "suggested",
                    Title = "인수 " + receipt.SuggestAccept.ToString("0") + " · 격리 " + receipt.SuggestQuarantine.ToString("0"),
                    Reason = receipt.SuggestReason,
                    Recommended = true,
                });
                return preview;
            }
            if (command.Action == "produce")
            {
                var problem = ProductionProblem(Cups(command));
                return new GamePreview
                {
                    CanConfirm = problem == null,
                    Message = problem ?? pack.recipe.menu + " " + Cups(command).ToString("0") + "잔 · " + pack.item.name + " " +
                        (Cups(command) * pack.recipe.contentPerCup).ToString("0") + pack.item.contentUnit,
                    BlockCode = problem == null ? string.Empty : "ProductionBlocked",
                };
            }
            return GamePreview.Blocked("지원하지 않는 행동입니다.", "UnknownAction");
        }

        public GameResult Confirm(GameCommand command)
        {
            if (command.ElapsedMs > now) now = command.ElapsedMs;
            GameResult result;
            if (command.Action == "inspect") result = Inspect(command);
            else if (command.Action == "produce") result = Produce(command);
            else return GameResult.Fail("지원하지 않는 행동입니다.", "UnknownAction");
            RefreshSignals();   // 시계가 멈춰 있어도 행동한 결과가 신호에 바로 보이게 한다
            return result;
        }

        public object Snapshot()
        {
            return new StoreOperationsSnapshot
            {
                StoreName = pack.network.store.name,
                StorePlaceId = Place,
                ItemName = pack.item.name,
                OrderUnit = pack.item.orderUnit,
                ContentUnit = pack.item.contentUnit,
                ContentPerPack = pack.item.contentQuantity,
                AvailableContent = Balance(StockBuckets.Available),
                QuarantineContent = Balance(StockBuckets.Quarantine),
                UsedContent = Balance(StockBuckets.Used),
                Menu = pack.recipe.menu,
                ContentPerCup = pack.recipe.contentPerCup,
                WaitingCups = state.WaitingCups,
                Sales = kernel == null ? 0m : kernel.Balance(LedgerKinds.Money, Place, pack.recipe.menu, "sales"),
                Receipts = state.Receipts.ToList(),
                Productions = state.Productions.ToList(),
            };
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            if (handoff.Kind != HandoffKinds.CargoUnloaded) return HandoffReceipt.Refused("받을 수 없는 인계입니다.");
            if (handoff.ToPlaceId != Place) return HandoffReceipt.Refused("다른 점포의 화물입니다.");
            if (state.Receipts.Any(item => item.TripId == handoff.SubjectId)) return HandoffReceipt.Accepted("이미 받은 하차 기록입니다.");
            var accept = Math.Min(pack.inspection.suggestAccept, handoff.Quantity);
            var quarantine = Math.Min(pack.inspection.suggestQuarantine, handoff.Quantity - accept);
            state.Receipts.Add(new StoreReceiptState
            {
                Id = "receipt:" + handoff.SubjectId,
                ChainId = handoff.ChainId,
                TripId = handoff.SubjectId,
                OrderId = handoff.Value("orderId"),
                ItemId = handoff.ItemId,
                ItemName = handoff.ItemName,
                Unit = handoff.Unit,
                Unloaded = handoff.Quantity,
                SuggestAccept = accept,
                SuggestQuarantine = quarantine,
                SuggestReason = pack.inspection.reason,
                Stage = ReceiptStages.Waiting,
                ArrivedAtMs = handoff.AtMs,
            });
            RefreshSignals();
            return HandoffReceipt.Accepted("검수 대기");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state);
        }

        public void Restore(string saved)
        {
            var restored = JsonConvert.DeserializeObject<State>(saved);
            if (restored == null) return;
            state = restored;
            if (state.Receipts.Count > 0) now = Math.Max(now, state.Receipts.Max(item => Math.Max(item.ArrivedAtMs, item.InspectedAtMs)));
        }

        private GameResult Inspect(GameCommand command)
        {
            var receipt = WaitingReceipt();
            if (receipt == null) return GameResult.Fail("검수를 기다리는 화물이 없습니다.", "NoWaitingReceipt");
            var accept = command.Number("accept");
            var quarantine = command.Number("quarantine");
            var reject = command.Number("reject");
            var problem = InspectionProblem(receipt, accept, quarantine, reject);
            if (problem != null) return GameResult.Fail(problem, "InspectionQuantityMismatch");

            receipt.Accepted = accept;
            receipt.Quarantined = quarantine;
            receipt.Rejected = reject;
            receipt.Stage = ReceiptStages.Inspected;
            receipt.InspectedAtMs = now;
            // 인수 확정량만 가용 재고가 된다. 점포 재고는 내용량 단위(mL)로 적는다.
            Stock(StockBuckets.Available, accept * pack.item.contentQuantity, "검수 인수", receipt.Id);
            if (quarantine > 0) Stock(StockBuckets.Quarantine, quarantine * pack.item.contentQuantity, "검수 격리", receipt.Id);
            if (reject > 0) Stock(StockBuckets.Rejected, reject * pack.item.contentQuantity, "검수 거절", receipt.Id);

            kernel.Post(new Handoff
            {
                Id = "handoff:" + receipt.OrderId + ":" + HandoffKinds.StoreReceiptSigned,
                ChainId = receipt.ChainId, Kind = HandoffKinds.StoreReceiptSigned,
                FromOsId = OsId, ToOsId = BusinessIds.CargoTransport, FromPlaceId = Place, ToPlaceId = Place,
                SubjectId = receipt.TripId, ItemId = receipt.ItemId, ItemName = receipt.ItemName,
                Quantity = receipt.Unloaded, Unit = receipt.Unit, AtMs = now,
                Evidence = "점주가 하차 " + receipt.Unloaded.ToString("0") + receipt.Unit + "을 확인",
            });
            var recorded = new Handoff
            {
                Id = "handoff:" + receipt.OrderId + ":" + HandoffKinds.StoreInspectionRecorded,
                ChainId = receipt.ChainId, Kind = HandoffKinds.StoreInspectionRecorded,
                FromOsId = OsId, ToOsId = BusinessIds.FranchiseSupply, FromPlaceId = Place, ToPlaceId = pack.network.headquarters.id,
                SubjectId = receipt.OrderId, ItemId = receipt.ItemId, ItemName = receipt.ItemName,
                Quantity = accept, Unit = receipt.Unit, AtMs = now,
                Evidence = "인수 " + accept.ToString("0") + " · 격리 " + quarantine.ToString("0") + " · 거절 " + reject.ToString("0"),
            };
            recorded.Values["accepted"] = accept.ToString(System.Globalization.CultureInfo.InvariantCulture);
            recorded.Values["quarantined"] = quarantine.ToString(System.Globalization.CultureInfo.InvariantCulture);
            recorded.Values["rejected"] = reject.ToString(System.Globalization.CultureInfo.InvariantCulture);
            kernel.Post(recorded);
            return GameResult.Done("검수를 마쳤습니다. 인수 " + accept.ToString("0") + receipt.Unit + "만 쓸 수 있는 재고가 됩니다.");
        }

        private GameResult Produce(GameCommand command)
        {
            var cups = Cups(command);
            var problem = ProductionProblem(cups);
            if (problem != null) return GameResult.Fail(problem, "ProductionBlocked");
            var used = cups * pack.recipe.contentPerCup;
            var number = state.Productions.Count + 1;
            var chain = state.Receipts.Count == 0 ? string.Empty : state.Receipts[state.Receipts.Count - 1].ChainId;
            var production = new StoreProductionState
            {
                Id = "production:" + pack.network.store.code.ToLowerInvariant() + ":" + number.ToString("0000"),
                ChainId = chain,
                Menu = pack.recipe.menu,
                Cups = cups,
                ContentUsed = used,
                AtMs = now,
            };
            state.Productions.Add(production);
            state.WaitingCups -= cups;
            Stock(StockBuckets.Available, -used, pack.recipe.menu + " 제조", production.Id);
            Stock(StockBuckets.Used, used, pack.recipe.menu + " 제조", production.Id);
            kernel.Record(new LedgerEntry
            {
                Kind = LedgerKinds.Money, OsId = OsId, PlaceId = Place, ItemId = pack.recipe.menu, Bucket = "sales",
                Amount = cups * pack.recipe.price, Unit = "KRW", AtMs = now, Reason = pack.recipe.menu + " 판매 (가상)", RefId = production.Id,
            });
            kernel.Post(new Handoff
            {
                Id = "handoff:" + production.Id + ":" + HandoffKinds.FoodReadyForPickup,
                ChainId = chain, Kind = HandoffKinds.FoodReadyForPickup,
                FromOsId = OsId, ToOsId = BusinessIds.FoodDelivery, FromPlaceId = Place, ToPlaceId = string.Empty,
                SubjectId = production.Id, ItemId = pack.recipe.menu, ItemName = pack.recipe.menu,
                Quantity = cups, Unit = "잔", AtMs = now,
                Evidence = pack.recipe.menu + " " + cups.ToString("0") + "잔 준비 완료",
            });
            return GameResult.Done(pack.recipe.menu + " " + cups.ToString("0") + "잔을 만들었습니다. " + pack.item.name + " " +
                used.ToString("0") + pack.item.contentUnit + "를 썼습니다.");
        }

        private string InspectionProblem(StoreReceiptState receipt, decimal accept, decimal quarantine, decimal reject)
        {
            if (accept < 0 || quarantine < 0 || reject < 0) return "수량은 0보다 작을 수 없습니다.";
            if (accept != decimal.Truncate(accept) || quarantine != decimal.Truncate(quarantine) || reject != decimal.Truncate(reject))
                return "낱개 단위로 나눠야 합니다.";
            var decision = 업무수량보존Policy.판정(new 업무수량보존요청
            {
                입력수량 = receipt.Unloaded,
                결과수량 = accept,
                손실수량 = quarantine + reject,
                허용오차 = 0m,
                단위코드 = receipt.Unit,
            });
            return decision.보존여부 ? null
                : "인수·격리·거절의 합이 하차 " + receipt.Unloaded.ToString("0") + receipt.Unit + "과 같아야 합니다.";
        }

        private string ProductionProblem(decimal cups)
        {
            if (cups <= 0 || cups != decimal.Truncate(cups)) return "잔 수를 확인하세요.";
            if (state.WaitingCups < cups) return "기다리는 " + pack.recipe.menu + " 주문이 없습니다.";
            var needed = cups * pack.recipe.contentPerCup;
            var available = Balance(StockBuckets.Available);
            return available >= needed ? null
                : pack.item.name + "가 모자랍니다. 필요 " + needed.ToString("0") + pack.item.contentUnit + " · 가용 " +
                    available.ToString("0") + pack.item.contentUnit;
        }

        private static decimal Cups(GameCommand command)
        {
            var cups = command.Number("cups");
            return cups == 0m ? 1m : cups;
        }

        private StoreReceiptState WaitingReceipt()
        {
            return state.Receipts.FirstOrDefault(item => item.Stage == ReceiptStages.Waiting);
        }

        private void Stock(string bucket, decimal amount, string reason, string refId)
        {
            kernel.Record(new LedgerEntry
            {
                Kind = LedgerKinds.Stock, OsId = OsId, PlaceId = Place, ItemId = pack.item.productId,
                Bucket = bucket, Amount = amount, Unit = pack.item.contentUnit, AtMs = now, Reason = reason, RefId = refId,
            });
        }

        private decimal Balance(string bucket)
        {
            return kernel == null ? 0m : kernel.Balance(LedgerKinds.Stock, Place, pack.item.productId, bucket);
        }

        private sealed class State
        {
            public decimal WaitingCups;
            public List<StoreReceiptState> Receipts = new List<StoreReceiptState>();
            public List<StoreProductionState> Productions = new List<StoreProductionState>();
        }
    }
}
