using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 턴마감ServerAuthorityRepository : I타로턴마감AuthorityClient
    {
        private const string BaseRoute = "api/simulation/v1/sessions/";
        public const string BootstrapSessionStableId =
            "simulation-session:706a236b17e544e2a070a0785ae42d19";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 턴마감ServerAuthorityRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<턴마감ResultData> 서버기준Session확보Async(
            CancellationToken cancellationToken)
        {
            var response = await SendAsync(
                "POST", "api/simulation/v1/sessions",
                JsonConvert.SerializeObject(CreateSessionRequest()), cancellationToken);
            var created = ParseSession(response.Body);
            if (created.SessionStableId != BootstrapSessionStableId)
                throw new InvalidOperationException("TurnClosingBootstrapSessionMismatch");
            return created;
        }

        public async Task<턴마감ContextData> GetContextAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync(
                "GET", SessionRoute(sessionStableId) + "/turn-closing-context",
                string.Empty, cancellationToken);
            var wire = JsonConvert.DeserializeObject<ContextWire>(response.Body)
                ?? throw new InvalidOperationException("TurnClosingContextJsonInvalid");
            var context = new 턴마감ContextData
            {
                SessionStableId = wire.SessionStableId ?? string.Empty,
                TurnNumber = wire.TurnNumber,
                GameDateLabel = FormatGameDate(wire.GameDate),
                Revision = wire.Revision,
                PendingTaskCount = wire.PendingTaskCount,
                CanCloseTurn = wire.CanCloseTurn,
                AvailableCards = (wire.AvailableCards ?? Array.Empty<CardWire>())
                    .Select(MapCard).ToArray(),
                TarotDraw = MapTarotDraw(wire.TarotDraw),
            };
            ValidateContext(context, sessionStableId);
            return context;
        }

        public async Task<턴마감PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision, string selectedCardStableId,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync(
                "POST", SessionRoute(sessionStableId) + "/turn-closing-previews",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    SelectedCardStableIds = SelectedCards(selectedCardStableId),
                }), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("TurnClosingPreviewJsonInvalid");
            var preview = new 턴마감PreviewData
            {
                PreviewStableId = wire.PreviewStableId ?? string.Empty,
                BaseRevision = wire.BaseRevision,
                ClosingTurnNumber = wire.ClosingTurnNumber,
                NextTurnNumber = wire.NextTurnNumber,
                NextGameDateLabel = FormatGameDate(wire.NextGameDate),
                PendingTaskCount = wire.PendingTaskCount,
                SelectedCards = (wire.SelectedCards ?? Array.Empty<CardWire>())
                    .Select(MapCard).ToArray(),
            };
            if (preview.BaseRevision != expectedRevision
                || preview.NextTurnNumber != preview.ClosingTurnNumber + 1
                || preview.SelectedCards.Length != SelectedCards(selectedCardStableId).Length)
                throw new InvalidOperationException("TurnClosingPreviewAuthorityMismatch");
            return preview;
        }

        public async Task<턴마감ResultData> ConfirmAsync(
            string sessionStableId, string commandId, long expectedRevision,
            string selectedCardStableId, CancellationToken cancellationToken)
        {
            var selected = SelectedCards(selectedCardStableId);
            await SendAsync(
                "POST", SessionRoute(sessionStableId) + "/turn-closings/confirm",
                JsonConvert.SerializeObject(new
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    Preview = new
                    {
                        ExpectedRevision = expectedRevision,
                        SelectedCardStableIds = selected,
                    },
                }), cancellationToken);

            // 최종 확인 응답을 화면 권위로 삼지 않고 서버 기준 세션을 다시 읽는다.
            var canonical = await RefreshSessionAsync(sessionStableId, cancellationToken);
            if (canonical.Revision != expectedRevision + 1
                || canonical.ActiveCardStableId != (selectedCardStableId ?? string.Empty))
                throw new InvalidOperationException("TurnClosingCanonicalSessionMismatch");
            return canonical;
        }

        public async Task<타로객체반응PreviewData> Preview타로객체반응Async(
            string sessionStableId, long expectedRevision, string drawStableId,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync(
                "POST", SessionRoute(sessionStableId) + "/tarot-object-reaction-previews",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    DrawStableId = drawStableId,
                }), cancellationToken);
            var wire = JsonConvert.DeserializeObject<TarotObjectReactionPreviewWire>(response.Body)
                ?? throw new InvalidOperationException("TarotObjectReactionPreviewJsonInvalid");
            var result = new 타로객체반응PreviewData
            {
                PreviewStableId = wire.PreviewStableId ?? string.Empty,
                BaseRevision = wire.BaseRevision,
                TurnNumber = wire.TurnNumber,
                DrawStableId = wire.DrawStableId ?? string.Empty,
                ObjectCatalogRevision = wire.ObjectCatalogRevision ?? string.Empty,
                IsCandidateOnly = wire.IsCandidateOnly,
                DoesNotMutateSession = wire.DoesNotMutateSession,
                CardReactions = (wire.CardReactions ?? Array.Empty<TarotCardReactionWire>())
                    .Select(value => new 타로Card객체반응Data
                    {
                        OfferStableId = value.OfferStableId ?? string.Empty,
                        CardStableId = value.CardStableId ?? string.Empty,
                        OrientationCode = value.OrientationCode ?? string.Empty,
                        HighlightObjectStableIds = value.HighlightObjectStableIds
                            ?? Array.Empty<string>(),
                        ObjectReactions = (value.ObjectReactions
                                ?? Array.Empty<TarotObjectReactionWire>())
                            .Select(reaction => new 타로객체반응Data
                            {
                                ObjectStableId = reaction.ObjectStableId ?? string.Empty,
                                PlacementStableId = reaction.PlacementStableId ?? string.Empty,
                                ReactionStateCode = reaction.ReactionStateCode ?? string.Empty,
                                CanHighlightInWorld = reaction.CanHighlightInWorld,
                                KoreanSummary = reaction.KoreanSummary ?? string.Empty,
                                StateSourceStableIds = reaction.StateSourceStableIds
                                    ?? Array.Empty<string>(),
                                BlockReasonCodes = reaction.BlockReasonCodes
                                    ?? Array.Empty<string>(),
                            }).ToArray(),
                    }).ToArray(),
            };
            if (result.BaseRevision != expectedRevision
                || result.DrawStableId != drawStableId
                || !result.IsCandidateOnly || !result.DoesNotMutateSession
                || result.CardReactions.Length != 3)
                throw new InvalidOperationException("TarotObjectReactionPreviewAuthorityMismatch");
            return result;
        }

        public async Task<턴마감PreviewData> Preview타로Async(
            string sessionStableId, long expectedRevision, 턴마감타로SelectionData selection,
            CancellationToken cancellationToken)
        {
            ValidateSelection(selection);
            var response = await SendAsync(
                "POST", SessionRoute(sessionStableId) + "/turn-closing-previews",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    SelectedCardStableIds = Array.Empty<string>(),
                    SelectedTarotCard = selection,
                }), cancellationToken);
            return ParsePreview(response.Body, expectedRevision, selection.CardStableId);
        }

        public async Task<턴마감ResultData> Confirm타로Async(
            string sessionStableId, string commandId, long expectedRevision,
            턴마감타로SelectionData selection, CancellationToken cancellationToken)
        {
            ValidateSelection(selection);
            await SendAsync(
                "POST", SessionRoute(sessionStableId) + "/turn-closings/confirm",
                JsonConvert.SerializeObject(new
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    Preview = new
                    {
                        ExpectedRevision = expectedRevision,
                        SelectedCardStableIds = Array.Empty<string>(),
                        SelectedTarotCard = selection,
                    },
                }), cancellationToken);
            var canonical = await RefreshSessionAsync(sessionStableId, cancellationToken);
            if (canonical.Revision != expectedRevision + 1
                || canonical.ActiveCardStableId != selection.CardStableId)
                throw new InvalidOperationException("TarotTurnClosingCanonicalSessionMismatch");
            return canonical;
        }

        public async Task<턴마감ResultData> RefreshSessionAsync(
            string sessionStableId, CancellationToken cancellationToken)
            => ParseSession((await SendAsync(
                "GET", SessionRoute(sessionStableId), string.Empty, cancellationToken)).Body);

        private async Task<UnityApiResponse> SendAsync(
            string method, string relativePath, string body,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = relativePath,
                JsonBody = body,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("TurnClosingServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("TurnClosingSessionStableIdMissing");
            return BaseRoute + Uri.EscapeDataString(sessionStableId.Trim());
        }

        private static string[] SelectedCards(string stableId)
            => string.IsNullOrEmpty(stableId) ? Array.Empty<string>() : new[] { stableId };

        private static void ValidateSelection(턴마감타로SelectionData selection)
        {
            if (selection == null || string.IsNullOrWhiteSpace(selection.OfferStableId)
                || string.IsNullOrWhiteSpace(selection.CardStableId)
                || (selection.OrientationCode != 턴마감타로OrientationCodes.Upright
                    && selection.OrientationCode != 턴마감타로OrientationCodes.Reversed))
                throw new InvalidOperationException("TarotTurnSelectionInvalid");
        }

        private static 턴마감PreviewData ParsePreview(
            string json, long expectedRevision, string selectedCardStableId)
        {
            var wire = JsonConvert.DeserializeObject<PreviewWire>(json)
                ?? throw new InvalidOperationException("TurnClosingPreviewJsonInvalid");
            var preview = new 턴마감PreviewData
            {
                PreviewStableId = wire.PreviewStableId ?? string.Empty,
                BaseRevision = wire.BaseRevision,
                ClosingTurnNumber = wire.ClosingTurnNumber,
                NextTurnNumber = wire.NextTurnNumber,
                NextGameDateLabel = FormatGameDate(wire.NextGameDate),
                PendingTaskCount = wire.PendingTaskCount,
                SelectedCards = (wire.SelectedCards ?? Array.Empty<CardWire>())
                    .Select(MapCard).ToArray(),
            };
            if (preview.BaseRevision != expectedRevision
                || preview.NextTurnNumber != preview.ClosingTurnNumber + 1
                || preview.SelectedCards.Length != 1
                || preview.SelectedCards[0].CardStableId != selectedCardStableId)
                throw new InvalidOperationException("TarotTurnClosingPreviewAuthorityMismatch");
            return preview;
        }

        private static 턴마감타로DrawData MapTarotDraw(TarotDrawWire? wire)
        {
            if (wire == null) return new 턴마감타로DrawData();
            var draw = new 턴마감타로DrawData
            {
                DrawStableId = wire.DrawStableId ?? string.Empty,
                DeckStableId = wire.DeckStableId ?? string.Empty,
                DeckRevision = wire.DeckRevision ?? string.Empty,
                DrawRuleRevision = wire.DrawRuleRevision ?? string.Empty,
                TurnNumber = wire.TurnNumber,
                TurnHistoryHash = wire.TurnHistoryHash ?? string.Empty,
                Offers = (wire.Offers ?? Array.Empty<TarotOfferWire>())
                    .Select(value => new 턴마감타로OfferData
                    {
                        OfferStableId = value.OfferStableId ?? string.Empty,
                        OfferSlotNumber = value.OfferSlotNumber,
                        CardCopyStableId = value.CardCopyStableId ?? string.Empty,
                        OrientationCode = value.OrientationCode ?? string.Empty,
                        Card = MapCard(value.Card ?? new CardWire()),
                    }).OrderBy(value => value.OfferSlotNumber).ToArray(),
            };
            if (!draw.IsAvailable
                || draw.Offers.Select(value => value.OfferStableId).Distinct().Count() != 3
                || draw.Offers.Any(value => value.OrientationCode
                    != 턴마감타로OrientationCodes.Upright
                    && value.OrientationCode != 턴마감타로OrientationCodes.Reversed))
                throw new InvalidOperationException("TarotDrawAuthorityMismatch");
            return draw;
        }

        private static 턴마감CardData MapCard(CardWire wire)
        {
            var card = new 턴마감CardData
            {
                CardStableId = wire.CardStableId ?? string.Empty,
                CardRevision = wire.CardRevision ?? string.Empty,
                CardKindCode = wire.CardKindCode ?? string.Empty,
                Title = wire.Title ?? string.Empty,
                Summary = wire.Summary ?? string.Empty,
                EffectCode = wire.EffectCode ?? string.Empty,
                TargetStatCode = wire.TargetStatCode ?? string.Empty,
                StatDelta = wire.StatDelta,
                SourceStableId = wire.SourceStableId ?? string.Empty,
                RegionKey = wire.RegionKey ?? string.Empty,
                AvailableFromGameDate = ParseOptionalDate(wire.AvailableFromGameDate),
                AvailableThroughGameDate = ParseOptionalDate(wire.AvailableThroughGameDate),
                CalendarRevision = wire.CalendarRevision ?? string.Empty,
                EffectRuleRevision = wire.EffectRuleRevision ?? string.Empty,
                SourceUrl = wire.SourceUrl ?? string.Empty,
                EvidenceCheckedAtUtc = ParseOptionalDate(wire.EvidenceCheckedAtUtc),
            };
            턴마감FixtureAuthorityClient.ValidateCard(card);
            return card;
        }

        private static void ValidateContext(턴마감ContextData context, string expectedSession)
        {
            if (context.SessionStableId != expectedSession || context.TurnNumber <= 0
                || context.Revision < 0 || context.PendingTaskCount < 0
                || context.AvailableCards.Select(value => value.CardStableId).Distinct().Count()
                    != context.AvailableCards.Length)
                throw new InvalidOperationException("TurnClosingContextAuthorityMismatch");
        }

        private static 턴마감ResultData ParseSession(string json)
        {
            var wire = JsonConvert.DeserializeObject<SessionWire>(json)
                ?? throw new InvalidOperationException("TurnClosingSessionJsonInvalid");
            var world = wire.WorldContext
                ?? throw new InvalidOperationException("TurnClosingWorldContextMissing");
            var settlement = wire.Settlement
                ?? throw new InvalidOperationException("TurnClosingSettlementMissing");
            var effect = (wire.ActiveTurnCardEffects ?? Array.Empty<ActiveEffectWire>())
                .SingleOrDefault();
            var marketFood = (settlement.MarketSupplyByProduct ?? Array.Empty<MarketWire>())
                .Where(value => value.ProductStableId == "product:potato")
                .Sum(value => value.Quantity);
            var districts = (settlement.Districts ?? Array.Empty<DistrictWire>())
                .Select(value => new SimulationWorldDistrictNode(
                    value.DistrictStableId ?? string.Empty, Array.Empty<string>()))
                .ToArray();
            var snapshot = new SimulationWorldShellSnapshot(
                wire.SessionStableId ?? string.Empty,
                wire.Revision,
                world.WorldTick,
                FormatGameDate(world.GameDate),
                settlement.TreasuryBalance,
                settlement.LaborAvailable,
                settlement.LaborReserved,
                marketFood,
                settlement.FoodReserveEquivalent,
                settlement.FoodSecurityDays,
                settlement.ActiveTaskStableIds?.Length ?? 0,
                "SimulationServer",
                new[]
                {
                    new SimulationWorldSettlementNode(
                        settlement.SettlementStableId ?? string.Empty, districts),
                },
                MapRegionalCausality(wire.RegionalCausality));
            return new 턴마감ResultData
            {
                SessionStableId = snapshot.SessionStableId,
                Revision = wire.Revision,
                WorldTick = world.WorldTick,
                ActiveTurnNumber = effect?.ActiveTurnNumber ?? world.WorldTick + 1,
                ActiveCardStableId = effect?.CardStableId ?? string.Empty,
                ActiveEffectCode = effect?.EffectCode ?? string.Empty,
                WorldSnapshot = snapshot,
            };
        }

        private static string FormatGameDate(string value)
            => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToString("Year 1 · MM-dd", CultureInfo.InvariantCulture)
                : throw new InvalidOperationException("TurnClosingGameDateInvalid");

        private static 실제E5RegionalCausalityData MapRegionalCausality(
            RegionalCausalityWire wire)
        {
            var value = wire == null
                ? new 실제E5RegionalCausalityData()
                : new 실제E5RegionalCausalityData
                {
                    Revision = wire.Revision,
                    ThreatScore = wire.ThreatScore,
                    RecoveryScore = wire.RecoveryScore,
                    NetPressureModifier = wire.NetPressureModifier,
                    OutcomeCode = wire.OutcomeCode ?? "Normal",
                };
            value.Validate();
            return value;
        }

        private static DateTimeOffset? ParseOptionalDate(string value)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed
                    : throw new InvalidOperationException("TurnClosingCardDateInvalid");

        private static object CreateSessionRequest()
            => new
            {
                ClientRequestId = "706a236b-17e5-44e2-a070-a0785ae42d19",
                ScenarioStableId = "scenario:unity.turn-closing-server",
                ScenarioDataRevision = "simulation-data:turn-closing-server:1",
                ScenarioSeed = 240811,
                RuleRevision = "turn-closing-rule:1",
                DurationTicks = 28,
                WorldContext = new
                {
                    FactionStableId = "faction:sim.borderland-1",
                    TerritoryStableId = "territory:sim.borderland-1",
                    SettlementStableId = SimulationWorldShellFixture.SettlementStableId,
                    GameDateStartsOn = "2026-04-12T00:00:00+00:00",
                },
                Settlement = new
                {
                    TreasuryBalance = 1000000m, CurrencyCode = "KRW",
                    LaborCapacityTotal = 100m, LaborReserved = 25m,
                    StorageCapacity = 2000m, StorageOccupied = 1200m,
                    StorageUnitCode = "KGM", PopulationCount = 100,
                    PopulationFoodDemandPerTick = 100m, GarrisonCount = 20,
                    GarrisonFoodDemandPerTick = 20m,
                    FoodEquivalentUnitCode = "FoodEquivalentUnit",
                    FoodEquivalentRuleRevision = "food-equivalent:fixture-r1",
                    Districts = new[]
                    {
                        District("district:farm", "FarmDistrict"),
                        District("district:town", "TownDistrict"),
                        District("district:market", "MarketDistrict"),
                        District("district:storage", "StorageDistrict"),
                        District("district:logistics", "LogisticsDistrict"),
                        District("district:residential", "ResidentialDistrict"),
                        District("district:garrison", "GarrisonDistrict"),
                        District("district:gate", "GateDistrict"),
                    },
                    Facilities = new[]
                    {
                        new { FacilityStableId = "facility:unity:harvest-day:farm", FacilityTypeCode = "Farm", DistrictStableId = "district:farm", SourceStableIds = Sources() },
                        new { FacilityStableId = "facility:sim.storage", FacilityTypeCode = "Storage", DistrictStableId = "district:storage", SourceStableIds = Sources() },
                        new { FacilityStableId = "facility:sim.market", FacilityTypeCode = "Market", DistrictStableId = "district:market", SourceStableIds = Sources() },
                    },
                    MarketSupplyByProduct = new[]
                    {
                        new { ProductStableId = "product:potato", Quantity = 300m, UnitCode = "KGM", SourceStableIds = Sources() },
                    },
                    ReserveStockLots = new[]
                    {
                        new { StockLotStableId = "stock-lot:sim.food-1", ProductStableId = "product:potato", StorageFacilityStableId = "facility:sim.storage", Quantity = 1200m, OutboundReservedQuantity = 0m, UnitCode = "KGM", FoodEquivalentQuantity = 1200m, OutboundReservedFoodEquivalentQuantity = 0m, SourceStableIds = Sources() },
                    },
                    SourceStableIds = Sources(),
                },
                SpatialWorld = CreateHarvestDaySpatialWorld(),
                FarmSurvival = CreateHarvestDayFarmState(),
            };

        private static object CreateHarvestDaySpatialWorld()
            => new
            {
                Definitions = new[]
                {
                    new
                    {
                        SpatialStableId = "spatial:unity:harvest-day:production-plot",
                        FacilityStableId = "facility:unity:harvest-day:farm",
                        AreaStableId = "area:sim:pyeongchang:daegwallyeong-farm",
                        AreaSetStableId = "area-set:sim:pyeongchang:farm-hub-town.v1",
                        LandscapeGraphStableId = "landscape-graph:sim:pyeongchang:daegwallyeong-farm.v1",
                        LandscapeNodeStableId = "candidate:harvest-day:production-plot",
                        EvidenceKindCode = "Scenario",
                        AccessStateCode = "Available",
                        CapabilityCodes = new[]
                        {
                            "Spatial.WorkerAccessible", "Spatial.CropProduction",
                            "Spatial.CargoAccessible", "Spatial.HarvestWorkArea",
                        },
                        BaseCapacities = new[]
                        {
                            new { CapacityCode = "WorkArea", Quantity = 1m, UnitCode = "slot" },
                        },
                        DefinitionRevision = "wi-spatial-seedbed:farm-production.v1:r1",
                        DefinitionHashSha256 = new string('A', 64),
                        SourceStableIds = new[]
                        {
                            "wi-spatial-seedbed:farm-production.v1",
                            "landscape-block-candidate:sim:pyeongchang:daegwallyeong-harvest-day.v1",
                        },
                    },
                    new
                    {
                        SpatialStableId = 오늘작업계획Codes.WorkYardSpatial,
                        FacilityStableId = "facility:unity:harvest-day:farm",
                        AreaStableId = "area:sim:pyeongchang:daegwallyeong-farm",
                        AreaSetStableId = "area-set:sim:pyeongchang:farm-hub-town.v1",
                        LandscapeGraphStableId = "landscape-graph:sim:pyeongchang:daegwallyeong-farm.v1",
                        LandscapeNodeStableId = "candidate:harvest-day:work-yard",
                        EvidenceKindCode = "Scenario",
                        AccessStateCode = "Available",
                        CapabilityCodes = new[]
                        {
                            "Spatial.WorkerAccessible", "Spatial.CargoAccessible",
                            "Spatial.CollectionWorkArea", "Spatial.PackingWorkArea",
                        },
                        BaseCapacities = new[]
                        {
                            new { CapacityCode = "WorkArea", Quantity = 1m, UnitCode = "slot" },
                        },
                        DefinitionRevision = "wi-spatial-seedbed:farm-work-yard.v1:r1",
                        DefinitionHashSha256 = new string('B', 64),
                        SourceStableIds = new[]
                        {
                            "wi-spatial-seedbed:farm-work-yard.v1",
                            "landscape-block-candidate:sim:pyeongchang:daegwallyeong-harvest-day.v1",
                        },
                    },
                },
            };

        private static object CreateHarvestDayFarmState()
            => new
            {
                RuleRevision = "farm-survival.scenic-season.r1",
                RegionStableId = "region:legal-dong:5176031000",
                AreaStableId = "area:sim:pyeongchang:daegwallyeong-farm",
                TileKey = "kr5186:l2:700:1145",
                FarmBuildingStableId = "facility:unity:harvest-day:farm",
                SupplyUnits = 8m,
                RepairMaterialUnits = 4m,
                SeedUnits = 2m,
                WaterUnits = 2m,
                Actors = new[]
                {
                    new
                    {
                        ActorStableId = 오늘작업계획Codes.PlayerActor,
                        ActorKindCode = "Player",
                        KoreanName = "나",
                        Health = 100m,
                        Stamina = 100m,
                        CapabilityCodes = new[]
                        {
                            "FarmHarvest", "FarmCollection", "FarmPacking",
                        },
                    },
                    new
                    {
                        ActorStableId = 오늘작업계획Codes.NpcActor,
                        ActorKindCode = "Npc",
                        KoreanName = "농장 일꾼",
                        Health = 100m,
                        Stamina = 100m,
                        CapabilityCodes = new[]
                        {
                            "FarmHarvest", "FarmCollection", "FarmPacking",
                        },
                    },
                },
                SoilTiles = Enumerable.Range(0, 2)
                    .SelectMany(row => Enumerable.Range(0, 5)
                        .Select(column => new
                        {
                            SoilTileStableId = $"farm-soil:pyeongchang:{row}:{column}",
                            GridX = row,
                            GridY = column,
                            StateCode = "Tilled",
                            PhysicalAreaSquareMeters = 100m,
                        }))
                    .ToArray(),
                CultivationUnits = Enumerable.Range(0, 2)
                    .SelectMany(row => Enumerable.Range(0, 5)
                        .Select(column => new
                        {
                            CultivationUnitStableId = $"farm-plot:pyeongchang:{row}:{column}",
                            Revision = 1,
                            TileStableId = $"farm-soil:pyeongchang:{row}:{column}",
                            CultivationStableId = $"cultivation:unity:harvest-day:potato:{row}:{column}",
                            ProductStableId = "product:potato",
                            CropVariantStableId = "crop-variant:potato.fixture",
                            StateCode = "HarvestReady",
                            PhysicalAreaSquareMeters = 100m,
                            EffectiveCultivationAreaRatio = 1m,
                            SourceStableIds = new[] { "source:fixture.harvest-day-cultivation" },
                        }))
                    .ToArray(),
                Defenses = Array.Empty<object>(),
                PotatoProductionRule = new
                {
                    RuleStableId = "rule:potato-production.fixture.v1",
                    RuleRevision = 1,
                    SourceTypeCode = "Fixture",
                    ProductStableId = "product:potato",
                    CropVariantStableId = "crop-variant:potato.fixture",
                    BaseYieldKilogramsPerSquareMeter = 3m,
                    MinimumEnvironmentFactor = .5m,
                    MaximumEnvironmentFactor = 1m,
                    MinimumInputFactor = .8m,
                    MaximumInputFactor = 1.2m,
                    MinimumFacilityFactor = .8m,
                    MaximumFacilityFactor = 1.2m,
                    MinimumLossFactor = .1m,
                    MaximumLossFactor = 1m,
                    SourceStableIds = new[] { "source:fixture.potato-yield-rule" },
                    Limitations = new[] { "실제 생산량 또는 운영 수확량으로 사용하지 않는다." },
                },
            };

        private static object District(string id, string type)
            => new { DistrictStableId = id, DistrictTypeCode = type, SourceStableIds = Sources() };
        private static string[] Sources() => new[] { "source:unity.turn-closing-server-r1" };

        [Serializable] private sealed class ContextWire { public string SessionStableId; public int TurnNumber; public string GameDate; public long Revision; public int PendingTaskCount; public bool CanCloseTurn; public CardWire[] AvailableCards; public TarotDrawWire TarotDraw; }
        [Serializable] private sealed class PreviewWire { public string PreviewStableId; public long BaseRevision; public int ClosingTurnNumber; public int NextTurnNumber; public string NextGameDate; public int PendingTaskCount; public CardWire[] SelectedCards; }
        [Serializable] private sealed class CardWire { public string CardStableId; public string CardRevision; public string CardKindCode; public string Title; public string Summary; public string EffectCode; public string TargetStatCode; public int StatDelta; public string SourceStableId; public string RegionKey; public string AvailableFromGameDate; public string AvailableThroughGameDate; public string CalendarRevision; public string EffectRuleRevision; public string SourceUrl; public string EvidenceCheckedAtUtc; }
        [Serializable] private sealed class SessionWire { public string SessionStableId; public long Revision; public WorldWire WorldContext; public SettlementWire Settlement; public ActiveEffectWire[] ActiveTurnCardEffects; public RegionalCausalityWire RegionalCausality; }
        [Serializable] private sealed class RegionalCausalityWire { public long Revision; public int ThreatScore; public int RecoveryScore; public int NetPressureModifier; public string OutcomeCode; }
        [Serializable] private sealed class WorldWire { public int WorldTick; public string GameDate; }
        [Serializable] private sealed class SettlementWire { public string SettlementStableId; public decimal TreasuryBalance; public decimal LaborAvailable; public decimal LaborReserved; public decimal FoodReserveEquivalent; public decimal FoodSecurityDays; public string[] ActiveTaskStableIds; public MarketWire[] MarketSupplyByProduct; public DistrictWire[] Districts; }
        [Serializable] private sealed class MarketWire { public string ProductStableId; public decimal Quantity; }
        [Serializable] private sealed class DistrictWire { public string DistrictStableId; }
        [Serializable] private sealed class ActiveEffectWire { public string CardStableId; public string EffectCode; public int ActiveTurnNumber; }
        [Serializable] private sealed class TarotDrawWire { public string DrawStableId; public string DeckStableId; public string DeckRevision; public string DrawRuleRevision; public int TurnNumber; public string TurnHistoryHash; public TarotOfferWire[] Offers; }
        [Serializable] private sealed class TarotOfferWire { public string OfferStableId; public int OfferSlotNumber; public string CardCopyStableId; public string OrientationCode; public CardWire Card; }
        [Serializable] private sealed class TarotObjectReactionPreviewWire { public string PreviewStableId; public long BaseRevision; public int TurnNumber; public string DrawStableId; public string ObjectCatalogRevision; public bool IsCandidateOnly; public bool DoesNotMutateSession; public TarotCardReactionWire[] CardReactions; }
        [Serializable] private sealed class TarotCardReactionWire { public string OfferStableId; public string CardStableId; public string OrientationCode; public TarotObjectReactionWire[] ObjectReactions; public string[] HighlightObjectStableIds; }
        [Serializable] private sealed class TarotObjectReactionWire { public string ObjectStableId; public string PlacementStableId; public string ReactionStateCode; public bool CanHighlightInWorld; public string KoreanSummary; public string[] StateSourceStableIds; public string[] BlockReasonCodes; }
    }
}
