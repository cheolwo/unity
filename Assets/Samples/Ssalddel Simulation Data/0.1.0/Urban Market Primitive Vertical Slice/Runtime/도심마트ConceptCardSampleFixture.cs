using System;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.PresentationContracts.LearningCards;
using Ssalddel.Unity.UrbanMarket;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    /// <summary>실제 주민·주문·계약을 만들지 않는 CC3 표시 전용 fixture입니다.</summary>
    public static class 도심마트ConceptCardSampleFixture
    {
        private static readonly DateTimeOffset EvidenceAsOf =
            DateTimeOffset.Parse("2026-08-09T00:00:00Z");

        public static ConceptCardDeckPresentationModel CreateDeck()
        {
            var visit = ResidentialGroupRepresentativeVisitFixture.Create();
            return new UrbanMarketResidentialGroupConceptCardAdapter().Project(
                new UrbanMarketResidentialGroupConceptCardProjectionInput
                {
                    WorldId = new WorldContextId("world:urban-market:sim:1"),
                    ProjectionRevision = 7,
                    InterpretationRevision = "interpretation:residential-group-supply:7",
                    SelectedCardStableId =
                        "concept-card:confirmed-demand:orderer-group:residential:potato:1",
                    GroupWorldId = new WorldStableId("world:orderer-group:residential-potato:1"),
                    ProductWorldId = new WorldStableId("world:product:potato"),
                    PickupWorldId = new WorldStableId("world:pickup-point:residential-sample-1"),
                    SupplyWorldId = new WorldStableId("world:supply-management:potato:1"),
                    InquiryWorldId = new WorldStableId("world:market-inquiry:potato:1"),
                    Visit = visit,
                    GroupDemand = new UrbanMarketResidentialGroupDemandMapper().Map(GroupDemand()),
                    SupplyManagement = new UrbanMarketSupplyManagementPresentationMapper().Map(Supply()),
                }) ?? throw new InvalidOperationException("ResidentialGroupConceptCardFixtureUnauthorized");
        }

        public static ResidentialGroupRepresentativeDialoguePresentationModel CreateDialogue()
            => new ResidentialGroupRepresentativeDialoguePresentationModel
            {
                InquiryStableId = "market-inquiry:sim:potato:1",
                TitleText = "공동주택 감자 같이 주문",
                DemandText = "의향 410kg · 확정 385kg",
                BoundaryText = "대표는 주민 대신 주문·결제·계약을 확정하지 않습니다.",
                CommandEffectCode = RepresentativeVisitCommandEffectCodes.None,
            };

        private static UrbanMarketResidentialGroupDemandApiModel GroupDemand()
            => new UrbanMarketResidentialGroupDemandApiModel
            {
                Revision = 7,
                PerspectiveRevision = "market-manager-group-perspective:7",
                ModeCode = "Simulation",
                IsRoleAuthorized = true,
                OrdererGroupStableId = "orderer-group:residential:potato:1",
                ProductStableId = "product:potato",
                RepresentativeNpcStableId = "npc:sim:residential-group-representative:1",
                InquiryStableId = "market-inquiry:sim:potato:1",
                IntentParticipantCount = 67,
                IntentQuantity = 410m,
                ConfirmedParticipantCount = 61,
                ConfirmedQuantity = 385m,
                QuantityUnitCode = "kg",
                InquiryStateCode = "Submitted",
                PickupPointStableId = "pickup-point:residential:sample-1",
                PickupPointStateCode = "Candidate",
                AvailableActionCodes = new[]
                {
                    UrbanMarketResidentialGroupConceptCardCodes.ReviewOrdererGroupDemand,
                    UrbanMarketResidentialGroupConceptCardCodes.PreviewSupplyPlan,
                    UrbanMarketResidentialGroupConceptCardCodes.CompareSupplyOffers,
                },
                IntentSourceLineage = new[] { Source("group-purchase:sim:potato:1", "group-purchase-revision:7") },
                ConfirmedSourceLineage = new[] { Source("group-order:sim:potato:1", "group-order-revision:7") },
                PickupSourceLineage = new[] { Source("pickup-point:residential:sample-1", "pickup-point-revision:1") },
                InquirySourceLineage = new[] { Source("market-inquiry:sim:potato:1", "market-inquiry-revision:4") },
            };

        private static UrbanMarketSupplyManagementApiModel Supply()
            => new UrbanMarketSupplyManagementApiModel
            {
                Revision = 7,
                PresentationRevision = "supply-presentation:7",
                ModeCode = "Simulation",
                ProductStableId = "product:potato",
                QuantityUnitCode = "kg",
                DemandAndOrders = new UrbanMarketDemandBriefingApiModel
                {
                    AsOfTick = 7,
                    TodayOrderCount = 37,
                    TodayRequestedQuantity = 385m,
                    PendingOrderQuantity = 385m,
                    CurrentAvailableInventory = 80m,
                    TodayScheduledInbound = 230m,
                    ImmediatelyFulfillableQuantity = 80m,
                    InboundAfterProcessingPotentialQuantity = 230m,
                    CannotCoverQuantity = 75m,
                    ReasonCodes = new[] { "SupplyCoverageGap" },
                    LimitationText = "Simulation · 입고는 검수와 작업 이후에만 사용할 수 있습니다.",
                },
                ManagementPreview = new UrbanMarketManagementPreviewApiModel
                {
                    HardDemandQuantity = 2105m,
                    FulfilledQuantity = 1730m,
                    UnfulfilledQuantity = 375m,
                    PurchaseCost = 900000m,
                    EndingCash = 4100000m,
                    OutstandingPaymentAmount = 0m,
                    WasteQuantity = 0m,
                    ReceivingWorkload = 500m,
                },
                SupplyPortfolio = new[]
                {
                    Supplier("supplier:local-coop", .4m),
                    Supplier("supplier:national-wholesaler", .5m),
                    Supplier("supplier:spot-market", .1m),
                },
                SourceLineage = new[]
                {
                    Source("simulation-result:urban-market-potato:1", "simulation-result-revision:7"),
                },
            };

        private static UrbanMarketSupplierPortfolioApiModel Supplier(string id, decimal share)
            => new UrbanMarketSupplierPortfolioApiModel
            {
                SupplierStableId = id,
                AcceptedQuantity = 100m,
                AcceptedSupplyShareRate = share,
                PurchaseCost = 100000m,
            };

        private static UrbanMarketConceptCardSourceApiModel Source(string id, string revision)
            => new UrbanMarketConceptCardSourceApiModel
            {
                SourceStableId = id,
                Revision = revision,
                EvidenceAsOfUtc = EvidenceAsOf,
                QualityCode = DataQualityCodes.Observed,
            };
    }
}
