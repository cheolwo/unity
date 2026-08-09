using System;
using System.Linq;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트공급경영SurfaceView : MonoBehaviour,
        IUrbanMarketSupplyManagementPresentationTarget
    {
        [SerializeField] private TextMesh demandBriefingText = null!;
        [SerializeField] private TextMesh managementPreviewText = null!;
        [SerializeField] private TextMesh supplyPortfolioText = null!;

        public void ApplySupplyManagement(UrbanMarketSupplyManagementPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var demand = model.DemandAndOrders;
            demandBriefingText.text = "오늘 주문 " + demand.TodayOrderCount
                + " · 요청 " + demand.TodayRequestedQuantity
                + "\n대기 " + demand.PendingOrderQuantity
                + " · 현재 재고 " + demand.CurrentAvailableInventory
                + " · 예정 입고 " + demand.TodayScheduledInbound
                + "\n즉시 " + demand.ImmediatelyFulfillableQuantity
                + " · 처리 후 " + demand.InboundAfterProcessingPotentialQuantity
                + " · 부족 " + demand.CannotCoverQuantity
                + "\n" + demand.LimitationText;
            var preview = model.ManagementPreview;
            managementPreviewText.text = "충족 " + preview.FulfilledQuantity
                + " / " + preview.HardDemandQuantity
                + "\n미충족 " + preview.UnfulfilledQuantity
                + " · 폐기 " + preview.WasteQuantity
                + "\n구매비 " + preview.PurchaseCost
                + " · 잔액 " + preview.EndingCash
                + " · 미지급 " + preview.OutstandingPaymentAmount;
            supplyPortfolioText.text = string.Join("\n", model.SupplyPortfolio.Select(value =>
                value.SupplierStableId + " · " + value.AcceptedQuantity
                + " · " + (value.AcceptedSupplyShareRate * 100m).ToString("0.0") + "%"));
        }

        public bool ValidateWiring()
            => demandBriefingText != null && managementPreviewText != null
                && supplyPortfolioText != null;
    }
}
