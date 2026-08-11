using System;
using System.Linq;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.PotatoJourney;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class PotatoJourneyFarmSlicePresenter : MonoBehaviour
    {
        [SerializeField] private Transform farmAnchor = null!;
        [SerializeField] private Transform cargoAnchor = null!;
        [SerializeField] private GameObject farmSelectionRing = null!;
        [SerializeField] private GameObject cargoSelectionRing = null!;
        [SerializeField] private Text selectionTitle = null!;
        [SerializeField] private Text modeBadge = null!;
        [SerializeField] private Text priceValue = null!;
        [SerializeField] private Text priceEvidence = null!;
        [SerializeField] private Text linkageSummary = null!;
        [SerializeField] private Text sourceLineage = null!;
        [SerializeField] private Text helpText = null!;

        private readonly PotatoJourneyMapper mapper = new PotatoJourneyMapper();
        private readonly PotatoJourneyInterpreter interpreter = new PotatoJourneyInterpreter();
        private string currentAnchorKind = string.Empty;
        private PotatoJourneyPresentationModel? currentModel;

        public string CurrentAnchorKind => currentAnchorKind;
        public PotatoJourneyPresentationModel? CurrentModel => currentModel;
        public bool ValidateWiring()
            => farmAnchor != null && cargoAnchor != null
               && farmSelectionRing != null && cargoSelectionRing != null
               && selectionTitle != null && modeBadge != null && priceValue != null
               && priceEvidence != null && linkageSummary != null
               && sourceLineage != null && helpText != null;

        public void Configure(
            Transform configuredFarmAnchor,
            Transform configuredCargoAnchor,
            GameObject configuredFarmRing,
            GameObject configuredCargoRing,
            Text configuredSelectionTitle,
            Text configuredModeBadge,
            Text configuredPriceValue,
            Text configuredPriceEvidence,
            Text configuredLinkageSummary,
            Text configuredSourceLineage,
            Text configuredHelpText)
        {
            farmAnchor = configuredFarmAnchor;
            cargoAnchor = configuredCargoAnchor;
            farmSelectionRing = configuredFarmRing;
            cargoSelectionRing = configuredCargoRing;
            selectionTitle = configuredSelectionTitle;
            modeBadge = configuredModeBadge;
            priceValue = configuredPriceValue;
            priceEvidence = configuredPriceEvidence;
            linkageSummary = configuredLinkageSummary;
            sourceLineage = configuredSourceLineage;
            helpText = configuredHelpText;
            ApplyFarmSelection();
        }

        private void Start()
        {
            if (ValidateWiring()) ApplyFarmSelection();
        }

        public void ApplyFarmSelection()
            => Apply(PotatoJourneyAnchorKindCodes.FarmPlot, farmAnchor, SimulationFarmFixture());

        public void ApplyCargoSelection()
            => Apply(PotatoJourneyAnchorKindCodes.FarmYardCargo, cargoAnchor, ProductOnlyFixture());

        private void Apply(string anchorKind, Transform anchor, PotatoJourneyApiModel apiModel)
        {
            currentAnchorKind = anchorKind;
            var snapshot = mapper.Map(apiModel);
            currentModel = interpreter.Interpret(new PotatoJourneyInterpretationInput
            {
                Snapshot = snapshot,
                AnchorKindCode = anchorKind,
                AnchorWorldObjectRef = new WorldObjectRef(
                    new WorldContextId("world:farm-potato-journey:1"),
                    new WorldStableId(anchorKind == PotatoJourneyAnchorKindCodes.FarmPlot
                        ? "world-object:farm-potato-plot"
                        : "world-object:farm-yard-potato-box")),
            });

            farmSelectionRing.SetActive(anchorKind == PotatoJourneyAnchorKindCodes.FarmPlot);
            cargoSelectionRing.SetActive(anchorKind == PotatoJourneyAnchorKindCodes.FarmYardCargo);
            var cards = currentModel.CardDeck.Cards;
            var product = cards.First(card => card.CardKindCode == "Concept");
            var price = cards.First(card => card.CardKindCode == "Status");
            var linkage = cards.First(card => card.CardKindCode == "Reason");
            selectionTitle.text = anchorKind == PotatoJourneyAnchorKindCodes.FarmPlot
                ? "POTATO FIELD · CULTIVATION VIEW"
                : "POTATO BOX · PRODUCT VIEW";
            modeBadge.text = currentModel.ModeLabel.Length > 0
                ? currentModel.ModeLabel + " · READ ONLY"
                : "SERVER PROJECTION · READ ONLY";
            priceValue.text = price.PrimaryValueText;
            priceEvidence.text = price.EvidenceRows.Length == 0
                ? "No verified price range"
                : string.Join("   |   ", price.EvidenceRows.Select(row => row.LabelText + "  " + row.ValueText));
            linkageSummary.text = linkage.PrimaryValueText + "\n" + linkage.SummaryText;
            sourceLineage.text = "SOURCE  " + string.Join("  +  ", product.SourceLineage.Select(item => item.SourceStableId))
                                 + "\nHS 0701 · KRW/kg · observed 2026-08-09";
            helpText.text = "CLICK FIELD / BOX   ·   [1] FIELD   [2] BOX\nVisuals never confirm harvest, cargo, inventory or price.";
        }

        private static PotatoJourneyApiModel ProductOnlyFixture()
            => BaseFixture(PotatoJourneySourceModeCodes.SimulationFixture,
                PotatoJourneyLinkageStatusCodes.ProductOnly, null,
                new[] { PriceLineage() },
                new[] { "Market observation only; not a contract, farm-gate or retail price." });

        private static PotatoJourneyApiModel SimulationFarmFixture()
            => BaseFixture(PotatoJourneySourceModeCodes.SimulationFixture,
                PotatoJourneyLinkageStatusCodes.SimulationLinked,
                new PotatoCultivationApiModel
                {
                    FarmStableId = "farm:simulation-a",
                    FarmRevision = 4,
                    PlotStableId = "farm-plot:simulation-a.1",
                    PlotRevision = 5,
                    CultivationStableId = "cultivation:simulation-a.potato.2026",
                    CultivationRevision = 6,
                    CropName = "Potato",
                    CropReferenceStableId = "crop-reference-category:fc01",
                    CropReferenceSourceKey = "nongsaro:crop-ebook",
                    GrowthStatusCode = "Growing",
                    ProductLinkageStatusCode = PotatoJourneyLinkageStatusCodes.SimulationLinked,
                    Sensors = new[]
                    {
                        new PotatoSensorApiModel
                        {
                            StableId = "sensor:simulation-a.soil-moisture.1",
                            Revision = 7,
                            SensorTypeCode = "SoilMoisture",
                            StatusCode = "Active",
                            LatestObservation = new PotatoSensorObservationApiModel
                            {
                                Value = 18.5m,
                                UnitCode = "Percent",
                                ObservedAt = new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero),
                                FreshnessStatusCode = "Fresh",
                                ConditionCode = "Dry",
                                AssessmentRuleRevision = "soil-water-rule:3",
                            },
                        },
                    },
                },
                new[] { FarmLineage(), PriceLineage() },
                new[] { "Cultivation and product are linked only inside this simulation scene." });

        private static PotatoJourneyApiModel BaseFixture(
            string sourceMode,
            string linkage,
            PotatoCultivationApiModel? farm,
            PotatoJourneySourceLineageApiModel[] lineage,
            string[] limitations)
            => new PotatoJourneyApiModel
            {
                StableId = "world-slice:potato-journey",
                Revision = farm == null ? "fixture-product-only:1" : "fixture-simulation-linked:1",
                GeneratedAt = new DateTimeOffset(2026, 8, 10, 2, 0, 0, TimeSpan.Zero),
                AuthorizedRoleCode = "Producer",
                ViewerScopeCode = "AuthorizedParty",
                AuthorizationDecisionId = "simulation-fixture:potato-farm-slice",
                SourceModeCode = sourceMode,
                LinkageStatusCode = linkage,
                Product = new PotatoProductApiModel
                {
                    ProductStableId = "product:potato",
                    DisplayName = "POTATO",
                    HsPrefix = "0701",
                    MappingQualityCode = "ExactCommodity",
                    MappingQualityLabel = "Exact commodity",
                    MappingEvidence = "KAMIS potato market observation crosswalk.",
                    InformationOnly = true,
                },
                Farm = farm,
                DomesticPrice = new PotatoPriceObservationApiModel
                {
                    StatusCode = PotatoPriceObservationStatusCodes.Ready,
                    HsCode = "0701",
                    UnitCode = "KRW_PER_KG",
                    CurrencyCode = "KRW",
                    DataSource = "Korea Agro-Fisheries & Food Trade Corporation daily market observation",
                    StartDate = "20260801",
                    EndDate = "20260809",
                    Wholesale = new PotatoPriceRangeApiModel
                    {
                        MarketStageCode = "Wholesale",
                        MarketStageLabel = "WHOLESALE",
                        AverageKrwPerKg = 2450m,
                        MinimumKrwPerKg = 2200m,
                        MaximumKrwPerKg = 2700m,
                        SampleCount = 8,
                        LatestSurveyDate = "20260809",
                    },
                    Notices = new[] { "Information only. Market stage and unit must remain visible." },
                    InformationOnly = true,
                },
                SourceLineage = lineage,
                Limitations = limitations,
                IsReadOnly = true,
            };

        private static PotatoJourneySourceLineageApiModel PriceLineage()
            => new PotatoJourneySourceLineageApiModel
            {
                SourceKey = "public-data:kamis-domestic-price",
                SourceStableId = "price-observation:potato.0701",
                SourceRevision = "20260801:20260809:Ready",
                ObservedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                SourceModeCode = PotatoJourneySourceModeCodes.OperationalProjection,
            };

        private static PotatoJourneySourceLineageApiModel FarmLineage()
            => new PotatoJourneySourceLineageApiModel
            {
                SourceKey = "ssalddel:farm-producer-perspective",
                SourceStableId = "cultivation:simulation-a.potato.2026",
                SourceRevision = "6",
                SourceModeCode = PotatoJourneySourceModeCodes.SimulationFixture,
            };
    }

}
