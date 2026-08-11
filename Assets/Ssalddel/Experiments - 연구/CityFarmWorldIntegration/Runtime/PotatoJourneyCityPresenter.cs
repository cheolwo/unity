using System;
using Ssalddel.Unity.PotatoJourney;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class PotatoJourneyCityPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject marketAnchorVisual = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text observedPriceText = null!;
        [SerializeField] private Text salePriceText = null!;
        [SerializeField] private Text availabilityText = null!;
        [SerializeField] private Text boundaryText = null!;

        public PotatoJourneyCityPresentationModel? CurrentModel { get; private set; }

        public void Configure(GameObject anchorVisual, Text title, Text observedPrice,
            Text salePrice, Text availability, Text boundary)
        {
            marketAnchorVisual = anchorVisual;
            titleText = title;
            observedPriceText = observedPrice;
            salePriceText = salePrice;
            availabilityText = availability;
            boundaryText = boundary;
            ApplyProjection();
        }

        private void Start()
        {
            if (ValidateWiring()) ApplyProjection();
        }

        public void ApplyProjection()
        {
            CurrentModel = new PotatoJourneyCityProjector().Project(new PotatoJourneySnapshot
            {
                SourceModeCode = PotatoJourneySourceModeCodes.SimulationFixture,
                LinkageStatusCode = PotatoJourneyLinkageStatusCodes.SimulationLinked,
                DomesticPrice = new PotatoPriceObservationSnapshot
                {
                    StatusCode = PotatoPriceObservationStatusCodes.Ready,
                    UnitCode = "KRW_PER_KG",
                    CurrencyCode = "KRW",
                    DataSource = "KAMIS",
                    Wholesale = new PotatoPriceRangeSnapshot
                    {
                        MarketStageCode = "Wholesale",
                        MarketStageLabel = "도매",
                        AverageKrwPerKg = 2450m,
                    },
                },
                CargoJourney = new PotatoCargoApiModel
                {
                    CargoStableId = "cargo:simulation-potato-city-1",
                    TransportTaskStableId = "transport-task:simulation-potato-city-1",
                    InboundTaskStableId = "inbound-task:simulation-potato-city-1",
                    HandoffStateCode = "AvailableAtMarket",
                },
                Market = new PotatoMarketApiModel
                {
                    PublicProductStableId = "mart-product:simulation-potato-20kg",
                    SalePrice = 35000m,
                    SaleUnit = "20kg box",
                    CurrencyCode = "KRW",
                    AvailableQuantity = 12,
                    QuantityUnit = "boxes",
                    QuantityMeaningCode = PotatoJourneyCityQuantityMeaningCodes.ProjectedSaleAvailability,
                    IsSaleAvailable = true,
                    InventoryObservedAt = new DateTimeOffset(2026, 8, 10, 8, 55, 0, TimeSpan.Zero),
                    SourceStableId = "market:urban-demo-001",
                    SourceRevision = "simulation:1",
                },
            });

            marketAnchorVisual.SetActive(CurrentModel.IsVisible);
            titleText.text = "POTATO · CITY PUBLIC PRODUCT";
            observedPriceText.text = "KAMIS OBSERVATION\n" + CurrentModel.ObservedPriceText;
            salePriceText.text = "STORE SALE PRICE\n" + CurrentModel.SalePriceText;
            availabilityText.text = CurrentModel.AvailabilityText
                                    + "\n" + CurrentModel.QuantityMeaningCode;
            boundaryText.text = CurrentModel.ModeLabel + " · " + CurrentModel.PublicProductStableId
                                + "\n" + CurrentModel.PriceSeparationText
                                + "\nCanonical Farm→Cargo→Market relation: NOT AVAILABLE";
        }

        public bool ValidateWiring()
            => marketAnchorVisual != null && titleText != null && observedPriceText != null
               && salePriceText != null && availabilityText != null && boundaryText != null;
    }
}
