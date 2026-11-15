using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WindowsGame.Kernel
{
    public static class ChainStepStates
    {
        public const string Done = "Done";
        public const string Now = "Now";
        public const string Next = "Next";
    }

    public sealed class ChainStep
    {
        public string Title = string.Empty;
        public string Line = string.Empty;
        public string Owner = string.Empty;
        public string State = ChainStepStates.Next;
    }

    /// <summary>화면이 그리는 인계 사슬 한 줄. 규칙 상태를 화면에 맞게 줄인 것이다(투영).</summary>
    public sealed class ChainView
    {
        public bool Started;
        public string Title = string.Empty;
        public string Headline = string.Empty;
        /// <summary>지금 운영자가 할 일: "order" | "inspect" | "produce" | "".</summary>
        public string ActionKey = string.Empty;
        public List<ChainStep> Steps = new List<ChainStep>();
        public bool TruckMoving;
        public float TruckProgress;
        public long TruckSecondsLeft;
        public string TruckName = string.Empty;
        public string TruckLoad = string.Empty;
        public bool WaitingInspection;
        public bool Finished;
    }

    public static class FirstChainProjection
    {
        public static ChainView Build(GameKernel kernel, long nowMs)
        {
            var view = new ChainView();
            if (kernel == null) return view;
            var supplyModule = kernel.Module<FranchiseSupplyModule>();
            var centerModule = kernel.Module<LogisticsCenterModule>();
            var cargoModule = kernel.Module<CargoTransportModule>();
            var storeModule = kernel.Module<StoreOperationsModule>();
            if (supplyModule == null || centerModule == null || cargoModule == null || storeModule == null) return view;
            var supply = (FranchiseSupplySnapshot)supplyModule.Snapshot();
            var center = (LogisticsCenterSnapshot)centerModule.Snapshot();
            var cargo = (CargoTransportSnapshot)cargoModule.Snapshot();
            var store = (StoreOperationsSnapshot)storeModule.Snapshot();

            var order = supply.Orders.LastOrDefault();
            var outbound = order == null ? null : center.Outbounds.LastOrDefault(item => item.OrderId == order.OrderId);
            var trip = order == null ? null : cargo.Trips.LastOrDefault(item => item.OrderId == order.OrderId);
            var receipt = order == null ? null : store.Receipts.LastOrDefault(item => item.OrderId == order.OrderId);
            var production = order == null ? null : store.Productions.LastOrDefault(item => item.ChainId == order.ChainId);
            var ready = production == null ? null
                : kernel.Handoffs.LastOrDefault(item => item.Kind == HandoffKinds.FoodReadyForPickup && item.SubjectId == production.Id);
            var unit = supply.Unit;

            view.Started = order != null;
            view.Title = order == null
                ? "첫 인계 사슬 · " + supply.ItemName
                : "첫 인계 사슬 · 보충 발주 " + order.OrderNumber + " · " + supply.ItemName;

            var steps = new[]
            {
                new ChainStep { Title = "본부 발주", Owner = "가맹 본부" },
                new ChainStep { Title = "센터 출고", Owner = "물류센터" },
                new ChainStep { Title = "화물 운송", Owner = "화물" },
                new ChainStep { Title = "점포 검수", Owner = store.StoreName },
                new ChainStep { Title = "제조", Owner = store.StoreName },
                new ChainStep { Title = "배달 · 수령", Owner = "음식 배달" },
            };
            var done = new bool[steps.Length];

            done[0] = order != null;
            steps[0].Line = order == null ? "점포에서 보충 발주를 냅니다" : order.AcceptedQuantity.ToString("0") + unit + " 수락";

            done[1] = outbound != null && outbound.Stage == OutboundStages.Loaded;
            if (outbound == null) steps[1].Line = "발주를 기다립니다";
            else if (outbound.Stage == OutboundStages.Reserved)
                steps[1].Line = outbound.Quantity.ToString("0") + unit + " 예약 · 피킹 중";
            else if (outbound.Stage == OutboundStages.Picked)
                steps[1].Line = outbound.Quantity.ToString("0") + unit + " 피킹 · 상차 중";
            else steps[1].Line = outbound.Quantity.ToString("0") + unit + " 피킹 · 상차";

            done[2] = trip != null && (trip.Stage == 화물운송상태코드.하차지도착 || trip.Stage == 화물운송상태코드.인수완료);
            if (trip == null) steps[2].Line = "상차를 기다립니다";
            else if (trip.Stage == 화물운송상태코드.상차완료)
            {
                var left = Math.Max(0, (trip.ArriveAtMs - nowMs + 999) / 1000);
                steps[2].Line = trip.VehicleName + " · 도착까지 " + (left / 60) + ":" + (left % 60).ToString("00");
                view.TruckMoving = true;
                view.TruckSecondsLeft = left;
                var span = Math.Max(1, trip.ArriveAtMs - trip.DepartedAtMs);
                view.TruckProgress = Math.Min(1f, Math.Max(0f, (nowMs - trip.DepartedAtMs) / (float)span));
            }
            else steps[2].Line = trip.Unloaded.ToString("0") + unit + " 하차";
            if (trip != null)
            {
                view.TruckName = trip.VehicleName;
                view.TruckLoad = trip.ItemName + " " + trip.Quantity.ToString("0") + unit;
            }

            done[3] = receipt != null && receipt.Stage == ReceiptStages.Inspected;
            if (receipt == null) steps[3].Line = "하차를 기다립니다";
            else if (receipt.Stage == ReceiptStages.Waiting) steps[3].Line = "검수 대기 · 하차 " + receipt.Unloaded.ToString("0") + unit;
            else
            {
                steps[3].Line = receipt.Accepted.ToString("0") + unit + " 인수";
                if (receipt.Quarantined > 0) steps[3].Line += " · " + receipt.Quarantined.ToString("0") + unit + " 격리";
                if (receipt.Rejected > 0) steps[3].Line += " · " + receipt.Rejected.ToString("0") + unit + " 거절";
            }
            view.WaitingInspection = receipt != null && receipt.Stage == ReceiptStages.Waiting;

            done[4] = production != null;
            steps[4].Line = production == null
                ? (store.WaitingCups > 0 ? store.Menu + " " + store.WaitingCups.ToString("0") + "잔 주문 대기" : "주문을 기다립니다")
                : production.Menu + " " + production.Cups.ToString("0") + "잔 " + production.ContentUsed.ToString("0") + store.ContentUnit +
                    " · 남은 " + store.AvailableContent.ToString("N0") + store.ContentUnit;

            done[5] = false;
            steps[5].Line = ready == null ? "준비 완료를 기다립니다"
                : ready.State == HandoffStates.Accepted ? "기사 배정으로 넘어갔습니다" : "준비 완료 · 기사 배정 대기";

            var current = Array.FindIndex(done, value => !value);
            for (var index = 0; index < steps.Length; index++)
                steps[index].State = done[index] ? ChainStepStates.Done : index == current ? ChainStepStates.Now : ChainStepStates.Next;
            view.Steps.AddRange(steps);
            view.Finished = production != null;

            if (order == null) { view.ActionKey = "order"; view.Headline = store.StoreName + "에 " + supply.ItemName + "가 없습니다. 보충 발주를 내세요."; }
            else if (view.WaitingInspection) { view.ActionKey = "inspect"; view.Headline = store.StoreName + "에 화물이 도착했습니다. 검수하세요."; }
            else if (done[3] && production == null && store.WaitingCups > 0) { view.ActionKey = "produce"; view.Headline = store.Menu + " 주문이 기다립니다. 제조하세요."; }
            else if (production != null) view.Headline = "제조까지 이어졌습니다. 배달 연결은 다음 단계입니다.";
            else if (view.TruckMoving) view.Headline = view.TruckName + "이 " + store.StoreName + "으로 가고 있습니다.";
            else view.Headline = "물류센터가 출고를 준비하고 있습니다.";
            return view;
        }
    }
}
