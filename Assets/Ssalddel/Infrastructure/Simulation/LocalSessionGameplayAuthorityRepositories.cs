using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class LocalTurnClosingAuthorityRepository
        : I타로턴마감AuthorityClient
    {
        private readonly ISimulationRuntime runtime;

        public LocalTurnClosingAuthorityRepository(ISimulationRuntime simulationRuntime)
            => runtime = simulationRuntime
                ?? throw new ArgumentNullException(nameof(simulationRuntime));

        public async Task<턴마감ResultData> RefreshSessionAsync(string sessionStableId,
            CancellationToken cancellationToken)
            => MapSession(await runtime.Sessions.GetAsync(sessionStableId,
                cancellationToken));

        public async Task<턴마감ContextData> GetContextAsync(string sessionStableId,
            CancellationToken cancellationToken)
        {
            var source = await runtime.Gameplay.GetTurnClosingContextAsync(
                sessionStableId, cancellationToken);
            var result = JsonConvert.DeserializeObject<턴마감ContextData>(
                JsonConvert.SerializeObject(source))
                ?? throw new InvalidOperationException("TurnClosingContextMapFailed");
            result.GameDateLabel = FormatGameDate(source.GameDate);
            return result;
        }

        public async Task<턴마감PreviewData> PreviewAsync(string sessionStableId,
            long expectedRevision, string selectedCardStableId,
            CancellationToken cancellationToken)
            => MapPreview(await runtime.Gameplay.PreviewTurnClosingAsync(sessionStableId,
                new SimulationTurnClosingPreviewRequest
                {
                    ExpectedRevision = expectedRevision,
                    SelectedCardStableIds = string.IsNullOrWhiteSpace(selectedCardStableId)
                        ? Array.Empty<string>() : new[] { selectedCardStableId.Trim() },
                }, cancellationToken));

        public async Task<턴마감ResultData> ConfirmAsync(string sessionStableId,
            string commandId, long expectedRevision, string selectedCardStableId,
            CancellationToken cancellationToken)
        {
            await runtime.Gameplay.ConfirmTurnClosingAsync(sessionStableId,
                new SimulationTurnClosingConfirmRequest
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    Preview = new SimulationTurnClosingPreviewRequest
                    {
                        ExpectedRevision = expectedRevision,
                        SelectedCardStableIds = string.IsNullOrWhiteSpace(selectedCardStableId)
                            ? Array.Empty<string>() : new[] { selectedCardStableId.Trim() },
                    },
                }, cancellationToken);
            return await RefreshSessionAsync(sessionStableId, cancellationToken);
        }

        public async Task<타로객체반응PreviewData> Preview타로객체반응Async(
            string sessionStableId, long expectedRevision, string drawStableId,
            CancellationToken cancellationToken)
            => JsonConvert.DeserializeObject<타로객체반응PreviewData>(
                JsonConvert.SerializeObject(await runtime.Gameplay
                    .PreviewTarotObjectReactionAsync(sessionStableId,
                        new Simulation타로객체반응PreviewRequest
                        {
                            ExpectedRevision = expectedRevision,
                            DrawStableId = drawStableId,
                        }, cancellationToken)))
                ?? throw new InvalidOperationException("TarotObjectReactionMapFailed");

        public async Task<턴마감PreviewData> Preview타로Async(string sessionStableId,
            long expectedRevision, 턴마감타로SelectionData selection,
            CancellationToken cancellationToken)
            => MapPreview(await runtime.Gameplay.PreviewTurnClosingAsync(sessionStableId,
                new SimulationTurnClosingPreviewRequest
                {
                    ExpectedRevision = expectedRevision,
                    SelectedTarotCard = new Simulation타로CardSelectionRequest
                    {
                        OfferStableId = selection.OfferStableId,
                        CardStableId = selection.CardStableId,
                        OrientationCode = selection.OrientationCode,
                    },
                }, cancellationToken));

        public async Task<턴마감ResultData> Confirm타로Async(string sessionStableId,
            string commandId, long expectedRevision, 턴마감타로SelectionData selection,
            CancellationToken cancellationToken)
        {
            await runtime.Gameplay.ConfirmTurnClosingAsync(sessionStableId,
                new SimulationTurnClosingConfirmRequest
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    Preview = new SimulationTurnClosingPreviewRequest
                    {
                        ExpectedRevision = expectedRevision,
                        SelectedTarotCard = new Simulation타로CardSelectionRequest
                        {
                            OfferStableId = selection.OfferStableId,
                            CardStableId = selection.CardStableId,
                            OrientationCode = selection.OrientationCode,
                        },
                    },
                }, cancellationToken);
            return await RefreshSessionAsync(sessionStableId, cancellationToken);
        }

        private static 턴마감PreviewData MapPreview(
            SimulationTurnClosingPreviewSnapshot source)
        {
            var result = JsonConvert.DeserializeObject<턴마감PreviewData>(
                JsonConvert.SerializeObject(source))
                ?? throw new InvalidOperationException("TurnClosingPreviewMapFailed");
            result.NextGameDateLabel = FormatGameDate(source.NextGameDate);
            return result;
        }

        private static 턴마감ResultData MapSession(
            경영SimulationSessionSnapshot source)
        {
            var result = 턴마감ServerAuthorityRepository.ParseSession(
                JsonConvert.SerializeObject(source), "LocalSimulation");
            return result;
        }

        private static string FormatGameDate(DateTimeOffset value)
            => value.ToString("Year 1 · MM-dd", CultureInfo.InvariantCulture);
    }

    public sealed class LocalFarmChoiceAuthorityRepository
        : IFarm선택AuthorityClient, I정착지상호작용AuthorityClient
    {
        private readonly ISimulationRuntime runtime;

        public LocalFarmChoiceAuthorityRepository(ISimulationRuntime simulationRuntime)
            => runtime = simulationRuntime
                ?? throw new ArgumentNullException(nameof(simulationRuntime));

        public async Task<정착지상호작용AuthoritySnapshot> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
            => 정착지상호작용AuthorityRepository.ParseSnapshot(
                JsonConvert.SerializeObject(await runtime.Sessions.GetAsync(
                    sessionStableId, cancellationToken)), "LocalSimulation");

        public async Task<Farm선택ContextData> GetFarmChoiceContextAsync(
            string sessionStableId, CancellationToken cancellationToken)
            => 정착지상호작용AuthorityRepository.NormalizeFarmChoiceContext(
                JsonConvert.DeserializeObject<Farm선택ContextData>(
                    JsonConvert.SerializeObject(await runtime.Gameplay
                        .GetFarmChoiceContextAsync(sessionStableId, cancellationToken)))
                ?? throw new InvalidOperationException("FarmChoiceContextMapFailed"));

        public async Task<Farm선택PreviewData> PreviewFarmChoiceAsync(
            string sessionStableId, long expectedRevision, string choiceStableId,
            CancellationToken cancellationToken)
        {
            var source = await runtime.Gameplay.PreviewFarmChoiceAsync(sessionStableId,
                new SimulationFarmChoicePreviewRequest
                {
                    ExpectedRevision = expectedRevision,
                    ChoiceStableId = choiceStableId,
                }, cancellationToken);
            var impact = source.Impact;
            return new Farm선택PreviewData
            {
                SituationStableId = source.SituationStableId,
                ChoiceStableId = source.ChoiceStableId,
                BaseRevision = source.BaseRevision,
                IsCandidateOnly = source.IsCandidateOnly,
                RequiresExplicitConfirm = source.RequiresExplicitConfirm,
                Impact = new 정착지상호작용PreviewData
                {
                    ChoiceCode = impact.ChoiceCode,
                    NextWorkflowCode = impact.NextWorkflowCode,
                    HarvestLotStableId = impact.HarvestLotStableId,
                    Quantity = impact.Quantity,
                    UnitCode = impact.CanonicalQuantityUnitCode,
                    RequiredLabor = impact.RequiredLabor,
                    SimulationCost = impact.SimulationCost,
                    ProjectedRevenue = impact.ProjectedRevenue,
                    DurationTicks = impact.DurationTicks,
                    FoodSecurityDaysBefore = impact.FoodSecurityDaysBefore,
                    FoodSecurityDaysCandidate = impact.FoodSecurityDaysCandidate,
                    ExpectedStoredQuantity = impact.StorageCandidate?.ExpectedStoredQuantity,
                    PolicyRevision = impact.PolicyRevision,
                    RiskCodes = impact.RiskCodes,
                    BoundaryCodes = impact.BoundaryCodes,
                },
            };
        }

        public async Task<정착지상호작용AuthoritySnapshot> ConfirmFarmChoiceAsync(
            string sessionStableId, long expectedRevision, string choiceStableId,
            CancellationToken cancellationToken)
        {
            await runtime.Gameplay.ConfirmFarmChoiceAsync(sessionStableId,
                new SimulationFarmChoiceConfirmRequest
                {
                    CommandId = "command:unity.local.farm-choice:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    ChoiceStableId = choiceStableId,
                }, cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public Task<정착지상호작용PreviewData> PreviewAsync(string sessionStableId,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken) => NotMigrated<정착지상호작용PreviewData>();
        public Task<정착지상호작용AuthoritySnapshot> ConfirmAsync(
            string sessionStableId, long expectedRevision,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken) => NotMigrated<정착지상호작용AuthoritySnapshot>();
        public async Task<정착지상호작용AuthoritySnapshot> AdvanceAsync(
            string sessionStableId, long expectedRevision, int tickCount,
            CancellationToken cancellationToken)
        {
            await runtime.Sessions.AdvanceWorldTickAsync(sessionStableId,
                new 경영SimulationTick진행Request
                {
                    CommandId = "command:unity.local.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = tickCount,
                }, cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }
        public Task<수확판로결과Data> Get수확판로결과Async(string sessionStableId,
            string harvestLotStableId, CancellationToken cancellationToken)
            => NotMigrated<수확판로결과Data>();
        public Task<수확판로결과Data[]> Get수확판로결과목록Async(
            string sessionStableId, CancellationToken cancellationToken)
            => NotMigrated<수확판로결과Data[]>();

        private static Task<T> NotMigrated<T>() => Task.FromException<T>(
            new InvalidOperationException("LocalSimulationCapabilityNotMigrated"));
    }

    /// <summary>
    /// Solo Session 안에서 준비된 포장 Lot을 찾아 공통 배차·물류 규칙을 직접 실행한다.
    /// 후보 점수와 선택 결과는 Unity가 계산하지 않는다.
    /// </summary>
    public sealed class LocalLogisticsMovementAuthorityRepository
        : I물류이동AuthorityClient
    {
        private readonly ISimulationRuntime runtime;

        public LocalLogisticsMovementAuthorityRepository(
            ISimulationRuntime simulationRuntime)
            => runtime = simulationRuntime
                ?? throw new ArgumentNullException(nameof(simulationRuntime));

        public async Task<물류이동AuthoritySnapshot> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
            => 물류이동AuthorityRepository.ParseSnapshot(
                JsonConvert.SerializeObject(await runtime.Sessions.GetAsync(
                    sessionStableId, cancellationToken)), "LocalSimulation");

        public async Task<물류이동PreviewData> PreviewAsync(
            string sessionStableId, 물류이동PreviewRequestData request,
            CancellationToken cancellationToken)
        {
            var session = await runtime.Sessions.GetAsync(sessionStableId,
                cancellationToken);
            var movement = CreateMovementRequest(session);
            var freight = CreateFreightRequest();
            var preview = await runtime.Gameplay.PreviewFreightDispatchAsync(
                sessionStableId, new SimulationFreightDispatchPreviewRequest
                {
                    Dispatch = freight,
                    Movement = movement,
                }, cancellationToken);
            var logistics = preview.LogisticsMovement;
            return new 물류이동PreviewData
            {
                SessionStableId = sessionStableId,
                ObservedRevision = preview.ObservedRevision,
                ObservedWorldTick = preview.ObservedWorldTick,
                CargoStableId = logistics.CargoStableId,
                Quantity = logistics.Quantity,
                UnitCode = logistics.UnitCode,
                RequiredRouteTicks = logistics.RequiredRouteTicks,
                DestinationStockCandidateStableId =
                    logistics.DestinationStockCandidateStableId,
                TransportRequestStableId = preview.TransportRequestStableId,
                DispatchOfferStableId = preview.DispatchOfferStableId,
                RecommendedCarrierCandidateStableId =
                    preview.RecommendedCarrierCandidateStableId ?? string.Empty,
                DispatchRuleRevision = preview.RuleRevision,
                CandidateEvaluations = preview.CandidateEvaluations.Select(value =>
                    new 화물배차후보평가Data
                    {
                        CarrierCandidateStableId = value.CarrierCandidateStableId,
                        VehicleStableId = value.VehicleStableId,
                        IsEligible = value.IsEligible,
                        IsRecommended = value.IsRecommended,
                        IsSelected = value.IsSelected,
                        Rank = value.Rank,
                        PickupDistanceKm = value.PickupDistanceKm,
                        VehicleCapacity = value.VehicleCapacity,
                        VehicleCapacityUnitCode = value.VehicleCapacityUnitCode,
                        Reason = value.Reason,
                        BlockReasonCodes = value.BlockReasonCodes,
                        BaseScore = value.Score.BaseScore,
                        DriverWaitingScore = value.Score.DriverWaitingScore,
                        TotalScore = value.Score.TotalScore,
                    }).ToArray(),
                BoundaryCodes = logistics.BoundaryCodes,
                Request = MapMovement(movement),
            };
        }

        public async Task<물류이동AuthoritySnapshot> ConfirmAsync(
            string sessionStableId, long expectedRevision,
            물류이동PreviewData preview, CancellationToken cancellationToken)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            await runtime.Gameplay.ConfirmFreightDispatchAsync(sessionStableId,
                new SimulationFreightDispatchConfirmRequest
                {
                    CommandId = "command:unity.local.freight-dispatch.confirm:"
                        + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    SelectedCarrierCandidateStableId =
                        preview.RecommendedCarrierCandidateStableId,
                    FreightDispatch = new SimulationFreightDispatchPreviewRequest
                    {
                        Dispatch = CreateFreightRequest(),
                        Movement = MapMovement(preview.Request),
                    },
                }, cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<물류이동AuthoritySnapshot> AdvanceAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await runtime.Sessions.AdvanceWorldTickAsync(sessionStableId,
                new 경영SimulationTick진행Request
                {
                    CommandId = "command:unity.local.logistics.tick:"
                        + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = 1,
                }, cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        private static SimulationLogisticsMovementPreviewRequest
            CreateMovementRequest(경영SimulationSessionSnapshot session)
        {
            var farm = session.FarmSurvival
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsFarmNotReady");
            var package = farm.PackageLots.FirstOrDefault(value =>
                value.StateCode == Simulation포장Lot상태Codes.PreparedForShipment)
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsCargoNotReady");
            var origin = farm.FarmBuildingStableId;
            var destination = session.Settlement.Facilities.FirstOrDefault(value =>
                value.FacilityTypeCode == SimulationSettlementFacilityTypeCodes.Storage)
                ?.FacilityStableId
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsDestinationNotReady");
            var actor = farm.Actors.FirstOrDefault(value =>
                value.ActorKindCode == SimulationFarmSurvivalCodes.Player)
                ?.ActorStableId
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsActorNotReady");
            return new SimulationLogisticsMovementPreviewRequest
            {
                CargoStableId = package.CargoStableId,
                CargoRevision = package.Revision,
                SourceAllocationStableId = package.SourceAllocationStableId,
                HarvestLotStableId = package.HarvestLotStableId,
                PackageLotStableId = package.PackageLotStableId,
                ProductStableId = package.ProductStableId,
                Quantity = package.Quantity,
                UnitCode = package.UnitCode,
                RouteStableId = "route:solo-local:farm-storage",
                OriginFacilityStableId = origin,
                DestinationFacilityStableId = destination,
                ActorStableId = actor,
                RequiredRouteTicks = 3,
                SourceStableIds = package.SourceStableIds,
            };
        }

        private static SimulationFreightDispatchRequest CreateFreightRequest()
            => JsonConvert.DeserializeObject<SimulationFreightDispatchRequest>(
                JsonConvert.SerializeObject(화물배차Fixture.CreateRequest()))
                ?? throw new InvalidOperationException(
                    "LocalSimulationFreightDispatchFixtureInvalid");

        private static 물류이동PreviewRequestData MapMovement(
            SimulationLogisticsMovementPreviewRequest source)
            => JsonConvert.DeserializeObject<물류이동PreviewRequestData>(
                JsonConvert.SerializeObject(source))
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsMovementMapFailed");

        private static SimulationLogisticsMovementPreviewRequest MapMovement(
            물류이동PreviewRequestData source)
            => JsonConvert.DeserializeObject<SimulationLogisticsMovementPreviewRequest>(
                JsonConvert.SerializeObject(source))
                ?? throw new InvalidOperationException(
                    "LocalSimulationLogisticsMovementMapFailed");
    }
}
