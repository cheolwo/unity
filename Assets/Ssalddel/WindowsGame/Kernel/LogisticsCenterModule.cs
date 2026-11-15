using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.WorkflowRules;

namespace Ssalddel.WindowsGame.Kernel
{
    public static class OutboundStages
    {
        public const string Reserved = "Reserved";     // 가용 로트 예약
        public const string Picked = "Picked";         // 피킹·포장 끝
        public const string Loaded = "Loaded";         // 상차 확정
    }

    public sealed class OutboundState
    {
        public string Id = string.Empty;
        public string OrderId = string.Empty;
        public string ChainId = string.Empty;
        public string ItemId = string.Empty;
        public string ItemName = string.Empty;
        public string Unit = string.Empty;
        public decimal Requested;
        public decimal Quantity;
        public decimal Short;
        public string LotId = string.Empty;
        public string Stage = string.Empty;
        public string StorePlaceId = string.Empty;
        public long ReservedAtMs;
        public long PickedAtMs;
        public long LoadedAtMs;
    }

    public sealed class CenterLotState
    {
        public string Id = string.Empty;
        public long Number;
        public int Available;
    }

    public sealed class LogisticsCenterSnapshot
    {
        public string CenterName = string.Empty;
        public string CenterPlaceId = string.Empty;
        public string Unit = string.Empty;
        public decimal Available;
        public decimal Reserved;
        public decimal Shipped;
        public List<OutboundState> Outbounds = new List<OutboundState>();
    }

    /// <summary>
    /// 물류센터 출고의 최소형. 수락된 발주를 받아 로트를 예약하고, 피킹과 상차를 시간에 맞춰 끝낸 뒤 화물로 넘긴다.
    /// 로트 배분 순서는 운영 서버와 같은 창고출고배분Policy를 쓴다.
    /// 첫 사슬은 센터 하나·로트 하나라 후보가 하나뿐이어서 확정을 따로 묻지 않는다.
    /// </summary>
    public sealed class LogisticsCenterModule : IBusinessModule
    {
        private readonly FirstChainPack pack;
        private GameKernel kernel;
        private State state = new State();

        public LogisticsCenterModule(FirstChainPack pack)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        }

        public string OsId { get { return BusinessIds.LogisticsCenter; } }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            state = new State();
            long number = 0;
            foreach (var lot in pack.center.lots)
            {
                state.Lots.Add(new CenterLotState { Id = lot.id, Number = ++number, Available = lot.available });
                Stock(StockBuckets.Available, lot.available, 0, "기초 재고 (가상)", lot.id);
            }
        }

        public void Tick(long elapsedMs)
        {
            foreach (var outbound in state.Outbounds)
            {
                if (outbound.Stage == OutboundStages.Reserved && elapsedMs >= outbound.ReservedAtMs + pack.center.pickSeconds * 1000L)
                {
                    outbound.Stage = OutboundStages.Picked;
                    outbound.PickedAtMs = outbound.ReservedAtMs + pack.center.pickSeconds * 1000L;
                }
                if (outbound.Stage == OutboundStages.Picked && elapsedMs >= outbound.PickedAtMs + pack.center.loadSeconds * 1000L)
                    Load(outbound);
            }
        }

        public GamePreview Preview(GameCommand command)
        {
            // 어느 로트에서 낼지 후보를 보여 준다. 첫 사슬에서는 보기만 한다.
            var preview = new GamePreview { CanConfirm = false, Message = "출고 로트 후보" };
            foreach (var lot in state.Lots)
            {
                preview.Candidates.Add(new GameCandidate
                {
                    Id = lot.Id,
                    Title = lot.Id,
                    Reason = "가용 " + lot.Available + pack.item.orderUnit,
                    Eligible = lot.Available > 0,
                    Recommended = lot == state.Lots.FirstOrDefault(item => item.Available > 0),
                });
            }
            return preview;
        }

        public GameResult Confirm(GameCommand command)
        {
            return GameResult.Fail("첫 사슬에서는 센터 출고가 시간에 맞춰 스스로 진행됩니다.", "AutomaticStage");
        }

        public object Snapshot()
        {
            return new LogisticsCenterSnapshot
            {
                CenterName = pack.center.name,
                CenterPlaceId = pack.center.id,
                Unit = pack.item.orderUnit,
                Available = Balance(StockBuckets.Available),
                Reserved = Balance(StockBuckets.Reserved),
                Shipped = Balance(StockBuckets.Shipped),
                Outbounds = state.Outbounds.ToList(),
            };
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            if (handoff.Kind != HandoffKinds.SupplyOrderAccepted) return HandoffReceipt.Refused("받을 수 없는 인계입니다.");
            if (state.Outbounds.Any(item => item.OrderId == handoff.SubjectId)) return HandoffReceipt.Accepted("이미 받은 발주입니다.");

            var requested = (int)handoff.Quantity;
            var lots = state.Lots.ToArray();
            var plan = 창고출고배분Policy.판정(
                new[] { requested },
                new IReadOnlyList<CenterLotState>[] { lots },
                lot => lot.Number,
                lot => 1L,
                lot => lot.Available,
                lot => -(decimal)lot.Number,
                lot => true);
            var picked = 0;
            var lotId = string.Empty;
            foreach (var pick in plan)
            {
                pick.Candidate.Available -= pick.Quantity;
                picked += pick.Quantity;
                if (lotId.Length == 0) lotId = pick.Candidate.Id;
            }
            var outbound = new OutboundState
            {
                Id = "outbound:" + handoff.SubjectId,
                OrderId = handoff.SubjectId,
                ChainId = handoff.ChainId,
                ItemId = handoff.ItemId,
                ItemName = handoff.ItemName,
                Unit = handoff.Unit,
                Requested = requested,
                Quantity = picked,
                Short = requested - picked,
                LotId = lotId,
                Stage = OutboundStages.Reserved,
                StorePlaceId = handoff.Value("storePlaceId"),
                ReservedAtMs = handoff.AtMs,
            };
            state.Outbounds.Add(outbound);
            Stock(StockBuckets.Available, -picked, handoff.AtMs, "발주 예약", outbound.Id);
            Stock(StockBuckets.Reserved, picked, handoff.AtMs, "발주 예약", outbound.Id);
            return picked == requested
                ? HandoffReceipt.Accepted(picked + handoff.Unit + " 예약")
                : HandoffReceipt.Accepted(picked + handoff.Unit + " 예약 · " + (requested - picked) + handoff.Unit + " 부족");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state);
        }

        public void Restore(string saved)
        {
            var restored = JsonConvert.DeserializeObject<State>(saved);
            if (restored != null) state = restored;
        }

        private void Load(OutboundState outbound)
        {
            outbound.Stage = OutboundStages.Loaded;
            outbound.LoadedAtMs = outbound.PickedAtMs + pack.center.loadSeconds * 1000L;
            Stock(StockBuckets.Reserved, -outbound.Quantity, outbound.LoadedAtMs, "상차 확정", outbound.Id);
            Stock(StockBuckets.Shipped, outbound.Quantity, outbound.LoadedAtMs, "상차 확정", outbound.Id);
            var cargo = new Handoff
            {
                Id = "handoff:" + outbound.OrderId + ":" + HandoffKinds.OutboundLoaded,
                ChainId = outbound.ChainId, Kind = HandoffKinds.OutboundLoaded,
                FromOsId = OsId, ToOsId = BusinessIds.CargoTransport,
                FromPlaceId = pack.center.id, ToPlaceId = outbound.StorePlaceId,
                SubjectId = outbound.Id, ItemId = outbound.ItemId, ItemName = outbound.ItemName,
                Quantity = outbound.Quantity, Unit = outbound.Unit, AtMs = outbound.LoadedAtMs,
                Evidence = "센터와 기사가 " + outbound.Quantity.ToString("0") + outbound.Unit + " 상차를 확인",
            };
            cargo.Values["orderId"] = outbound.OrderId;
            cargo.Values["lotId"] = outbound.LotId;
            kernel.Post(cargo);
            kernel.Post(new Handoff
            {
                Id = "handoff:" + outbound.OrderId + ":" + HandoffKinds.OutboundShipped,
                ChainId = outbound.ChainId, Kind = HandoffKinds.OutboundShipped,
                FromOsId = OsId, ToOsId = BusinessIds.FranchiseSupply,
                FromPlaceId = pack.center.id, ToPlaceId = pack.network.headquarters.id,
                SubjectId = outbound.OrderId, ItemId = outbound.ItemId, ItemName = outbound.ItemName,
                Quantity = outbound.Quantity, Unit = outbound.Unit, AtMs = outbound.LoadedAtMs,
                Evidence = "출고 " + outbound.Quantity.ToString("0") + outbound.Unit,
            });
        }

        private void Stock(string bucket, decimal amount, long atMs, string reason, string refId)
        {
            kernel.Record(new LedgerEntry
            {
                Kind = LedgerKinds.Stock, OsId = OsId, PlaceId = pack.center.id, ItemId = pack.item.productId,
                Bucket = bucket, Amount = amount, Unit = pack.item.orderUnit, AtMs = atMs, Reason = reason, RefId = refId,
            });
        }

        private decimal Balance(string bucket)
        {
            return kernel == null ? 0m : kernel.Balance(LedgerKinds.Stock, pack.center.id, pack.item.productId, bucket);
        }

        private sealed class State
        {
            public List<CenterLotState> Lots = new List<CenterLotState>();
            public List<OutboundState> Outbounds = new List<OutboundState>();
        }
    }
}
