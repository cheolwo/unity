using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    public static class HandoffKinds
    {
        public const string SupplyOrderAccepted = "supply-order-accepted";
        public const string OutboundLoaded = "outbound-loaded";
        public const string OutboundShipped = "outbound-shipped";
        public const string CargoUnloaded = "cargo-unloaded";
        public const string CargoArrived = "cargo-arrived";
        public const string StoreReceiptSigned = "store-receipt-signed";
        public const string StoreInspectionRecorded = "store-inspection-recorded";
        public const string FoodReadyForPickup = "food-ready-for-pickup";
    }

    /// <summary>
    /// 가맹 공급망으로 나가는 문. 본부·공급사·매장 등록, 조달계약, 매장 공급안, 매장 발주의 네 단계는
    /// 큰 Simulation(경영SimulationSessionAggregate)에 이미 있다. 게임은 이 문으로만 부른다.
    /// </summary>
    public interface IFranchiseSupplyPort
    {
        string Kind { get; }
        /// <summary>등록 → 조달계약 → 매장 공급안까지 연다. 실패하면 예외를 던진다.</summary>
        void Initialize(FirstChainPack pack, long seed);
        SupplyOrderPreview PreviewOrder(string orderId, string orderNumber, decimal quantity);
        /// <summary>같은 발주를 다시 확정해도 한 번만 반영하고 같은 결과를 돌려준다.</summary>
        SupplyOrderReceipt ConfirmOrder(string orderId, string orderNumber, decimal quantity, string commandId);
    }

    public sealed class SupplyOrderPreview
    {
        public bool CanConfirm;
        public string BlockReason = string.Empty;
        public decimal ContentQuantity;
        public decimal Amount;
    }

    public sealed class SupplyOrderReceipt
    {
        public bool Ok;
        public string Status = string.Empty;
        public decimal Amount;
        public string Note = string.Empty;
    }

    /// <summary>
    /// 큰 Simulation 패키지가 없는 빌드에서 쓰는 대역. 품목 하나, 최소·최대 수량, 금액 계산만 한다.
    /// 규칙을 새로 만든 것이 아니라 공급안의 조건 검사만 흉내 낸다.
    /// </summary>
    public sealed class LocalFranchiseSupplyPort : IFranchiseSupplyPort
    {
        private FirstChainPack pack;
        private readonly Dictionary<string, SupplyOrderReceipt> orders = new Dictionary<string, SupplyOrderReceipt>(StringComparer.Ordinal);

        public string Kind { get { return "Local"; } }

        public void Initialize(FirstChainPack source, long seed)
        {
            pack = source ?? throw new ArgumentNullException(nameof(source));
            orders.Clear();
        }

        public SupplyOrderPreview PreviewOrder(string orderId, string orderNumber, decimal quantity)
        {
            var preview = new SupplyOrderPreview
            {
                ContentQuantity = quantity * pack.item.contentQuantity,
                Amount = quantity * pack.item.storePrice,
            };
            if (quantity != decimal.Truncate(quantity)) preview.BlockReason = "FranchiseSupplyOrderQuantityInvalid";
            else if (quantity < pack.item.minimum || quantity > pack.item.maximum)
                preview.BlockReason = "FranchiseSupplyStoreOrderQuantityOutsideOffer";
            else if (orders.ContainsKey(orderId)) preview.BlockReason = "FranchiseSupplyStoreOrderAlreadyExists";
            preview.CanConfirm = preview.BlockReason.Length == 0;
            return preview;
        }

        public SupplyOrderReceipt ConfirmOrder(string orderId, string orderNumber, decimal quantity, string commandId)
        {
            SupplyOrderReceipt existing;
            if (orders.TryGetValue(orderId, out existing)) return existing;
            var preview = PreviewOrder(orderId, orderNumber, quantity);
            if (!preview.CanConfirm) return new SupplyOrderReceipt { Ok = false, Note = preview.BlockReason };
            var receipt = new SupplyOrderReceipt { Ok = true, Status = "Submitted", Amount = preview.Amount };
            orders.Add(orderId, receipt);
            return receipt;
        }
    }

    public static class SupplyStages
    {
        public const string Accepted = "Accepted";       // 본부 수락
        public const string Shipped = "Shipped";         // 센터 상차 확정
        public const string Unloaded = "Unloaded";       // 점포 하차
        public const string Inspected = "Inspected";     // 점포 검수 끝
    }

    public sealed class SupplyOrderState
    {
        public string OrderId = string.Empty;
        public string OrderNumber = string.Empty;
        public string ChainId = string.Empty;
        public string Stage = string.Empty;
        public string PortStatus = string.Empty;
        public decimal RequestedQuantity;
        public decimal AcceptedQuantity;
        public decimal Amount;
        public long SubmittedAtMs;
        public decimal ReceivedAccepted;
        public decimal ReceivedQuarantined;
        public decimal ReceivedRejected;
        public decimal AdjustedAmount;
    }

    public sealed class FranchiseSupplySnapshot
    {
        public string PortKind = string.Empty;
        public string PortNote = string.Empty;
        public string ItemName = string.Empty;
        public string Unit = string.Empty;
        public decimal ContentQuantity;
        public string ContentUnit = string.Empty;
        public decimal UnitPrice;
        public decimal Minimum;
        public decimal Maximum;
        public decimal Recommended;
        public string StoreName = string.Empty;
        public string HeadquartersName = string.Empty;
        public List<SupplyOrderState> Orders = new List<SupplyOrderState>();
    }

    /// <summary>
    /// 가맹 공급: 점포의 보충 발주를 본부가 받아 센터의 출고 후보로 넘긴다.
    /// 발주까지는 공급망 문(IFranchiseSupplyPort)에 맡기고, 수락과 인계는 여기서 적는다.
    /// </summary>
    public sealed class FranchiseSupplyModule : IBusinessModule
    {
        private readonly FirstChainPack pack;
        private readonly IFranchiseSupplyPort preferredPort;
        private IFranchiseSupplyPort port;
        private GameKernel kernel;
        private State state = new State();
        private long now;

        public FranchiseSupplyModule(FirstChainPack pack, IFranchiseSupplyPort preferredPort)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
            this.preferredPort = preferredPort;
        }

        public string OsId { get { return BusinessIds.FranchiseSupply; } }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            now = 0;
            state = new State();
            OpenPort(context.Seed);
        }

        public void Tick(long elapsedMs)
        {
            if (elapsedMs > now) now = elapsedMs;
        }

        public GamePreview Preview(GameCommand command)
        {
            if (command.Action != "order") return GamePreview.Blocked("지원하지 않는 행동입니다.", "UnknownAction");
            if (ActiveOrder() != null) return GamePreview.Blocked("진행 중인 보충 발주가 있습니다.", "SupplyOrderInProgress");
            var quantity = command.Number("quantity");
            var preview = port.PreviewOrder(NextOrderId(), NextOrderNumber(), quantity);
            var result = new GamePreview
            {
                CanConfirm = preview.CanConfirm,
                BlockCode = preview.BlockReason,
                Message = preview.CanConfirm
                    ? pack.item.name + " " + quantity.ToString("0") + pack.item.orderUnit + " · " + preview.Amount.ToString("N0") + "원"
                    : ReasonText(preview.BlockReason),
            };
            result.Candidates.Add(new GameCandidate
            {
                Id = pack.recommendedOrderQuantity.ToString(),
                Title = pack.recommendedOrderQuantity + pack.item.orderUnit,
                Reason = "가용 재고가 없고 " + pack.recipe.menu + " 한 잔에 " + pack.recipe.contentPerCup.ToString("0") + pack.item.contentUnit + "가 듭니다.",
                Recommended = true,
            });
            return result;
        }

        public GameResult Confirm(GameCommand command)
        {
            if (command.Action != "order") return GameResult.Fail("지원하지 않는 행동입니다.", "UnknownAction");
            if (ActiveOrder() != null) return GameResult.Fail("진행 중인 보충 발주가 있습니다.", "SupplyOrderInProgress");
            if (command.ElapsedMs > now) now = command.ElapsedMs;
            var quantity = command.Number("quantity");
            var orderId = NextOrderId();
            var receipt = port.ConfirmOrder(orderId, NextOrderNumber(), quantity, "port:" + orderId);
            if (!receipt.Ok) return GameResult.Fail(ReasonText(receipt.Note), string.IsNullOrEmpty(receipt.Note) ? "SupplyOrderRefused" : receipt.Note);

            // 본부 수락: 첫 사슬에서는 요청 수량을 모두 받는다. 미수락 수량은 0이다.
            var order = new SupplyOrderState
            {
                OrderId = orderId,
                OrderNumber = NextOrderNumber(),
                ChainId = "chain:" + orderId,
                Stage = SupplyStages.Accepted,
                PortStatus = receipt.Status,
                RequestedQuantity = quantity,
                AcceptedQuantity = quantity,
                Amount = receipt.Amount,
                AdjustedAmount = receipt.Amount,
                SubmittedAtMs = now,
            };
            state.Orders.Add(order);
            kernel.Record(new LedgerEntry
            {
                Kind = LedgerKinds.Money, OsId = OsId, PlaceId = pack.network.store.placeId, ItemId = pack.item.productId,
                Bucket = "supply-payable", Amount = receipt.Amount, Unit = "KRW", AtMs = now,
                Reason = "보충 발주 수락", RefId = orderId,
            });
            var handoff = new Handoff
            {
                Id = "handoff:" + orderId + ":" + HandoffKinds.SupplyOrderAccepted,
                ChainId = order.ChainId, Kind = HandoffKinds.SupplyOrderAccepted,
                FromOsId = OsId, ToOsId = BusinessIds.LogisticsCenter,
                FromPlaceId = pack.network.headquarters.id, ToPlaceId = pack.center.id,
                SubjectId = orderId, ItemId = pack.item.productId, ItemName = pack.item.name,
                Quantity = quantity, Unit = pack.item.orderUnit, AtMs = now,
                Evidence = "본부가 발주 " + quantity.ToString("0") + pack.item.orderUnit + "을 모두 수락",
            };
            handoff.Values["storePlaceId"] = pack.network.store.placeId;
            handoff.Values["storage"] = pack.item.storage;
            kernel.Post(handoff);
            return GameResult.Done("보충 발주를 냈습니다. 본부가 " + quantity.ToString("0") + pack.item.orderUnit + "을 수락했습니다.");
        }

        public object Snapshot()
        {
            return new FranchiseSupplySnapshot
            {
                PortKind = state.PortKind,
                PortNote = state.PortNote,
                ItemName = pack.item.name,
                Unit = pack.item.orderUnit,
                ContentQuantity = pack.item.contentQuantity,
                ContentUnit = pack.item.contentUnit,
                UnitPrice = pack.item.storePrice,
                Minimum = pack.item.minimum,
                Maximum = pack.item.maximum,
                Recommended = pack.recommendedOrderQuantity,
                StoreName = pack.network.store.name,
                HeadquartersName = pack.network.headquarters.name,
                Orders = state.Orders.ToList(),
            };
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            var order = state.Orders.FirstOrDefault(item => item.OrderId == handoff.SubjectId);
            if (order == null) return HandoffReceipt.Refused("모르는 발주입니다.");
            if (handoff.Kind == HandoffKinds.OutboundShipped)
            {
                if (order.Stage == SupplyStages.Accepted) order.Stage = SupplyStages.Shipped;
                return HandoffReceipt.Accepted("센터 상차 확정을 받았습니다.");
            }
            if (handoff.Kind == HandoffKinds.CargoArrived)
            {
                if (order.Stage == SupplyStages.Shipped) order.Stage = SupplyStages.Unloaded;
                return HandoffReceipt.Accepted("점포 하차를 받았습니다.");
            }
            if (handoff.Kind == HandoffKinds.StoreInspectionRecorded)
            {
                if (order.Stage == SupplyStages.Inspected) return HandoffReceipt.Accepted("이미 받은 검수 결과입니다.");
                order.ReceivedAccepted = handoff.Number("accepted");
                order.ReceivedQuarantined = handoff.Number("quarantined");
                order.ReceivedRejected = handoff.Number("rejected");
                order.AdjustedAmount = order.ReceivedAccepted * pack.item.storePrice;
                order.Stage = SupplyStages.Inspected;
                var difference = order.AdjustedAmount - order.Amount;
                if (difference != 0m)
                {
                    // 점포가 일부만 인수하면 공급대금 조정의 근거가 된다 (r71).
                    kernel.Record(new LedgerEntry
                    {
                        Kind = LedgerKinds.Money, OsId = OsId, PlaceId = pack.network.store.placeId, ItemId = pack.item.productId,
                        Bucket = "supply-payable", Amount = difference, Unit = "KRW", AtMs = handoff.AtMs,
                        Reason = "점포 검수 결과에 따른 공급대금 조정 근거", RefId = order.OrderId,
                    });
                }
                return HandoffReceipt.Accepted("검수 결과를 받았습니다.");
            }
            return HandoffReceipt.Refused("받을 수 없는 인계입니다.");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state);
        }

        public void Restore(string saved)
        {
            var restored = JsonConvert.DeserializeObject<State>(saved);
            if (restored == null) return;
            var kind = state.PortKind;
            var note = state.PortNote;
            state = restored;
            state.PortKind = kind;
            state.PortNote = note;
            // 공급망 문은 새로 열렸다. 저장된 발주를 같은 번호로 다시 넣어 문 쪽 상태를 맞춘다.
            foreach (var order in state.Orders)
            {
                try
                {
                    port.ConfirmOrder(order.OrderId, order.OrderNumber, order.RequestedQuantity, "port:" + order.OrderId);
                }
                catch (Exception exception)
                {
                    state.PortNote = "저장된 발주를 공급망에 다시 넣지 못했습니다: " + exception.Message;
                }
            }
            if (state.Orders.Count > 0) now = Math.Max(now, state.Orders.Max(item => item.SubmittedAtMs));
        }

        private void OpenPort(long seed)
        {
            if (preferredPort != null)
            {
                try
                {
                    preferredPort.Initialize(pack, seed);
                    port = preferredPort;
                    state.PortKind = port.Kind;
                    return;
                }
                catch (Exception exception)
                {
                    state.PortNote = preferredPort.Kind + " 연결 실패: " + exception.GetType().Name + " · " + exception.Message;
                }
            }
            port = new LocalFranchiseSupplyPort();
            port.Initialize(pack, seed);
            state.PortKind = port.Kind;
        }

        private SupplyOrderState ActiveOrder()
        {
            return state.Orders.FirstOrDefault(item => item.Stage != SupplyStages.Inspected);
        }

        private string NextOrderId()
        {
            return "supply-order:" + pack.network.store.code.ToLowerInvariant() + ":" + (state.Orders.Count + 1).ToString("0000");
        }

        private string NextOrderNumber()
        {
            return "HD-ORD-" + (state.Orders.Count + 1).ToString("0000");
        }

        private static string ReasonText(string code)
        {
            switch (code)
            {
                case "FranchiseSupplyStoreOrderQuantityOutsideOffer": return "공급안의 최소·최대 수량을 벗어났습니다.";
                case "FranchiseSupplyOrderQuantityInvalid": return "수량을 확인하세요.";
                case "FranchiseSupplyStoreOrderAlreadyExists": return "이미 낸 발주입니다.";
                default: return string.IsNullOrEmpty(code) ? "발주를 낼 수 없습니다." : "발주를 낼 수 없습니다 (" + code + ")";
            }
        }

        private sealed class State
        {
            public string PortKind = string.Empty;
            public string PortNote = string.Empty;
            public List<SupplyOrderState> Orders = new List<SupplyOrderState>();
        }
    }
}
