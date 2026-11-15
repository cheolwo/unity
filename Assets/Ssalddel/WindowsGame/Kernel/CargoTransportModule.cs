using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WindowsGame.Kernel
{
    public sealed class CargoTripState
    {
        public string Id = string.Empty;
        public string ChainId = string.Empty;
        public string OrderId = string.Empty;
        public string OutboundId = string.Empty;
        public string VehicleId = string.Empty;
        public string VehicleName = string.Empty;
        public string VehicleKind = string.Empty;
        public string ItemId = string.Empty;
        public string ItemName = string.Empty;
        public string Unit = string.Empty;
        public decimal Quantity;
        public decimal Unloaded;
        public string FromPlaceId = string.Empty;
        public string ToPlaceId = string.Empty;
        /// <summary>업무흐름규칙Catalog의 화물운송 상태 코드.</summary>
        public string Stage = string.Empty;
        public string StageNote = string.Empty;
        public long DepartedAtMs;
        public long ArriveAtMs;
        public long ReceivedAtMs;
    }

    public sealed class CargoTransportSnapshot
    {
        public List<CargoTripState> Trips = new List<CargoTripState>();
        public string LoadedStage = 화물운송상태코드.상차완료;
        public string ArrivedStage = 화물운송상태코드.하차지도착;
        public string ReceivedStage = 화물운송상태코드.인수완료;
    }

    /// <summary>
    /// 화물 운송의 최소형. 센터가 상차를 확정한 화물을 점포까지 옮기고 하차를 기록한다.
    /// 단계 이름과 허용 전이는 운영 플랫폼의 업무흐름규칙Catalog(화물운송)를 그대로 쓴다.
    /// 첫 사슬은 차량이 한 대라 배차 후보를 따로 묻지 않는다.
    /// </summary>
    public sealed class CargoTransportModule : IBusinessModule
    {
        private readonly FirstChainPack pack;
        private GameKernel kernel;
        private State state = new State();

        public CargoTransportModule(FirstChainPack pack)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        }

        public string OsId { get { return BusinessIds.CargoTransport; } }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            state = new State();
        }

        public void Tick(long elapsedMs)
        {
            foreach (var trip in state.Trips)
            {
                if (trip.Stage != 화물운송상태코드.상차완료 || elapsedMs < trip.ArriveAtMs) continue;
                if (!Move(trip, 화물운송상태코드.하차지도착)) continue;
                trip.Unloaded = trip.Quantity;
                kernel.Record(new LedgerEntry
                {
                    Kind = LedgerKinds.Stock, OsId = OsId, PlaceId = trip.ToPlaceId, ItemId = trip.ItemId,
                    Bucket = StockBuckets.Unloaded, Amount = trip.Unloaded, Unit = trip.Unit, AtMs = trip.ArriveAtMs,
                    Reason = "점포 하차", RefId = trip.Id,
                });
                var unloaded = new Handoff
                {
                    Id = "handoff:" + trip.OrderId + ":" + HandoffKinds.CargoUnloaded,
                    ChainId = trip.ChainId, Kind = HandoffKinds.CargoUnloaded,
                    FromOsId = OsId, ToOsId = BusinessIds.StoreOperations,
                    FromPlaceId = trip.FromPlaceId, ToPlaceId = trip.ToPlaceId,
                    SubjectId = trip.Id, ItemId = trip.ItemId, ItemName = trip.ItemName,
                    Quantity = trip.Unloaded, Unit = trip.Unit, AtMs = trip.ArriveAtMs,
                    Evidence = trip.VehicleName + " 기사가 " + trip.Unloaded.ToString("0") + trip.Unit + " 하차를 기록",
                };
                unloaded.Values["orderId"] = trip.OrderId;
                kernel.Post(unloaded);
                kernel.Post(new Handoff
                {
                    Id = "handoff:" + trip.OrderId + ":" + HandoffKinds.CargoArrived,
                    ChainId = trip.ChainId, Kind = HandoffKinds.CargoArrived,
                    FromOsId = OsId, ToOsId = BusinessIds.FranchiseSupply,
                    FromPlaceId = trip.FromPlaceId, ToPlaceId = trip.ToPlaceId,
                    SubjectId = trip.OrderId, ItemId = trip.ItemId, ItemName = trip.ItemName,
                    Quantity = trip.Unloaded, Unit = trip.Unit, AtMs = trip.ArriveAtMs,
                    Evidence = "하차 " + trip.Unloaded.ToString("0") + trip.Unit,
                });
            }
        }

        public GamePreview Preview(GameCommand command)
        {
            var preview = new GamePreview { CanConfirm = false, Message = "차량 후보" };
            preview.Candidates.Add(new GameCandidate
            {
                Id = pack.transport.vehicleId,
                Title = pack.transport.vehicleName + " · " + pack.transport.vehicleKind,
                Reason = pack.item.storage + " 화물을 실을 수 있는 차량",
                Recommended = true,
            });
            return preview;
        }

        public GameResult Confirm(GameCommand command)
        {
            return GameResult.Fail("첫 사슬에서는 화물 운송이 시간에 맞춰 스스로 진행됩니다.", "AutomaticStage");
        }

        public object Snapshot()
        {
            return new CargoTransportSnapshot { Trips = state.Trips.ToList() };
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            if (handoff.Kind == HandoffKinds.OutboundLoaded)
            {
                if (state.Trips.Any(item => item.OutboundId == handoff.SubjectId)) return HandoffReceipt.Accepted("이미 받은 화물입니다.");
                var trip = new CargoTripState
                {
                    Id = "trip:" + handoff.SubjectId,
                    ChainId = handoff.ChainId,
                    OrderId = handoff.Value("orderId"),
                    OutboundId = handoff.SubjectId,
                    VehicleId = pack.transport.vehicleId,
                    VehicleName = pack.transport.vehicleName,
                    VehicleKind = pack.transport.vehicleKind,
                    ItemId = handoff.ItemId,
                    ItemName = handoff.ItemName,
                    Unit = handoff.Unit,
                    Quantity = handoff.Quantity,
                    FromPlaceId = handoff.FromPlaceId,
                    ToPlaceId = handoff.ToPlaceId,
                    Stage = 화물운송상태코드.배차확정,
                    DepartedAtMs = handoff.AtMs,
                    ArriveAtMs = handoff.AtMs + pack.transport.travelSeconds * 1000L,
                };
                state.Trips.Add(trip);
                // 센터와 기사가 상차를 이미 확인했다. 상차지 도착과 상차 완료를 이어서 적는다.
                Move(trip, 화물운송상태코드.상차지도착);
                Move(trip, 화물운송상태코드.상차완료);
                return HandoffReceipt.Accepted(trip.VehicleName + " 출발");
            }
            if (handoff.Kind == HandoffKinds.StoreReceiptSigned)
            {
                var trip = state.Trips.FirstOrDefault(item => item.Id == handoff.SubjectId);
                if (trip == null) return HandoffReceipt.Refused("모르는 운송입니다.");
                if (trip.Stage == 화물운송상태코드.인수완료) return HandoffReceipt.Accepted("이미 인수 완료입니다.");
                if (!Move(trip, 화물운송상태코드.인수완료)) return HandoffReceipt.Refused(trip.StageNote);
                trip.ReceivedAtMs = handoff.AtMs;
                return HandoffReceipt.Accepted("인수 완료");
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
            if (restored != null) state = restored;
        }

        // 허용 전이는 업무흐름규칙Catalog가 정한다. 막히면 단계를 그대로 두고 까닭을 적는다.
        private static bool Move(CargoTripState trip, string target)
        {
            var decision = 업무상태전이Policy.판정(업무흐름코드.화물운송, trip.Stage, target);
            if (!decision.허용여부)
            {
                trip.StageNote = "단계 전이가 막혔습니다: " + trip.Stage + " → " + target;
                return false;
            }
            trip.Stage = target;
            trip.StageNote = string.Empty;
            return true;
        }

        private sealed class State
        {
            public List<CargoTripState> Trips = new List<CargoTripState>();
        }
    }
}
