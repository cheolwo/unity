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
    public sealed class 물류이동AuthorityRepository : I물류이동AuthorityClient
    {
        private const string BaseRoute = "api/simulation/v1/sessions/";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 물류이동AuthorityRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<물류이동PreviewData> PreviewAsync(
            string sessionStableId,
            물류이동PreviewRequestData request,
            CancellationToken cancellationToken)
        {
            var before = await GetAsync(sessionStableId, cancellationToken);
            var response = await SendAsync("POST", Route(sessionStableId)
                + "/freight-dispatch-previews", JsonConvert.SerializeObject(new
                {
                    Dispatch = 화물배차Fixture.CreateRequest(),
                    Movement = request,
                }), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("SimulationFreightDispatchPreviewJsonInvalid");
            var logistics = wire.LogisticsMovement
                ?? throw new InvalidOperationException("SimulationLogisticsPreviewMissing");
            return new 물류이동PreviewData
            {
                SessionStableId = before.SessionStableId,
                ObservedRevision = wire.ObservedRevision,
                ObservedWorldTick = wire.ObservedWorldTick,
                CargoStableId = logistics.CargoStableId ?? string.Empty,
                Quantity = logistics.Quantity,
                UnitCode = logistics.UnitCode ?? string.Empty,
                RequiredRouteTicks = logistics.RequiredRouteTicks,
                DestinationStockCandidateStableId = logistics.DestinationStockCandidateStableId
                    ?? string.Empty,
                TransportRequestStableId = wire.TransportRequestStableId ?? string.Empty,
                DispatchOfferStableId = wire.DispatchOfferStableId ?? string.Empty,
                RecommendedCarrierCandidateStableId =
                    wire.RecommendedCarrierCandidateStableId ?? string.Empty,
                DispatchRuleRevision = wire.RuleRevision ?? string.Empty,
                CandidateEvaluations = (wire.CandidateEvaluations ?? Array.Empty<CandidateWire>())
                    .Select(ParseCandidate).ToArray(),
                BoundaryCodes = logistics.BoundaryCodes ?? Array.Empty<string>(),
                Request = request,
            };
        }

        public async Task<물류이동AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            물류이동PreviewData preview,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST", Route(sessionStableId)
                + "/freight-dispatches/confirm", JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.freight-dispatch.confirm:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    SelectedCarrierCandidateStableId = preview.RecommendedCarrierCandidateStableId,
                    FreightDispatch = new
                    {
                        Dispatch = 화물배차Fixture.CreateRequest(),
                        Movement = preview.Request,
                    },
                }), cancellationToken);
            return ParseSnapshot(response.Body);
        }

        public async Task<물류이동AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST", Route(sessionStableId) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.logistics.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = 1,
                }), cancellationToken);
            return ParseSnapshot(response.Body);
        }

        private async Task<물류이동AuthoritySnapshot> GetAsync(
            string sessionStableId,
            CancellationToken cancellationToken)
            => ParseSnapshot((await SendAsync("GET", Route(sessionStableId), string.Empty,
                cancellationToken)).Body);

        private async Task<UnityApiResponse> SendAsync(
            string method,
            string path,
            string body,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = path,
                JsonBody = body,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("SimulationAuthorityRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string Route(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("SimulationSessionStableIdMissing");
            return BaseRoute + Uri.EscapeDataString(sessionStableId.Trim());
        }

        private static 물류이동AuthoritySnapshot ParseSnapshot(string json)
        {
            var wire = JsonConvert.DeserializeObject<SessionWire>(json)
                ?? throw new InvalidOperationException("SimulationSessionSnapshotJsonInvalid");
            var settlement = wire.Settlement
                ?? throw new InvalidOperationException("SimulationSettlementSnapshotMissing");
            var movement = wire.LogisticsMovements?.SingleOrDefault();
            var freight = wire.FreightTransports?.SingleOrDefault();
            var task = wire.Tasks?.FirstOrDefault(value => value.TaskStableId == movement?.TaskStableId);
            var allocation = settlement.HarvestLotAllocations?.FirstOrDefault(
                value => value.AllocationStableId == movement?.SourceAllocationStableId);
            var worldTick = wire.WorldContext?.WorldTick ?? wire.CurrentTick;
            var gameDate = DateTimeOffset.TryParse(wire.WorldContext?.GameDate,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "Simulation date unavailable";
            var settlementSnapshot = new 정착지상호작용AuthoritySnapshot
            {
                SessionStableId = wire.SessionStableId ?? string.Empty,
                Revision = wire.Revision,
                WorldTick = worldTick,
                GameDateLabel = gameDate,
                TreasuryBalance = settlement.TreasuryBalance,
                TreasuryReserved = settlement.TreasuryReserved,
                LaborAvailable = settlement.LaborAvailable,
                LaborReserved = settlement.LaborReserved,
                MarketFoodSupplyKg = settlement.MarketSupplyByProduct?
                    .Where(value => value.ProductStableId == "product:potato").Sum(value => value.Quantity) ?? 0m,
                ReserveFoodEquivalent = settlement.FoodReserveEquivalent,
                StorageOccupied = settlement.StorageOccupied,
                StorageReserved = settlement.StorageReserved,
                FoodSecurityDays = settlement.FoodSecurityDays,
                ActiveTaskCount = settlement.ActiveTaskStableIds?.Length ?? 0,
                AllocationStateCode = allocation?.StateCode ?? string.Empty,
                TaskStateCode = task?.StateCode ?? string.Empty,
                SourceModeCode = "SimulationServer",
            };
            return new 물류이동AuthoritySnapshot
            {
                SessionStableId = settlementSnapshot.SessionStableId,
                Revision = wire.Revision,
                WorldTick = worldTick,
                GameDateLabel = gameDate,
                CargoStableId = movement?.CargoStableId ?? 물류이동Fixture.CargoStableId,
                MovementStateCode = movement?.StateCode ?? string.Empty,
                TaskStateCode = task?.StateCode ?? string.Empty,
                Quantity = movement?.Quantity ?? 300m,
                ReservedQuantity = movement?.ReservedQuantity ?? 0m,
                SourceAvailableQuantity = allocation?.AvailableQuantity ?? 300m,
                CompletedRouteTicks = movement?.CompletedRouteTicks ?? 0,
                RequiredRouteTicks = movement?.RequiredRouteTicks ?? 3,
                RouteStableId = movement?.RouteStableId ?? "route:sim.farm-hub-1",
                DestinationStockCandidateStableId = movement?.DestinationStockCandidateStableId ?? string.Empty,
                TransportRequestStableId = freight?.TransportRequestStableId ?? string.Empty,
                DispatchStateCode = freight?.DispatchStateCode ?? string.Empty,
                CarrierCandidateStableId = freight?.CarrierCandidateStableId ?? string.Empty,
                VehicleStableId = freight?.VehicleStableId ?? string.Empty,
                DispatchRuleRevision = freight?.DispatchDecision?.RuleRevision ?? string.Empty,
                SourceModeCode = "SimulationServer",
                Settlement = settlementSnapshot,
            };
        }

        private static 화물배차후보평가Data ParseCandidate(CandidateWire value)
            => new()
            {
                CarrierCandidateStableId = value.CarrierCandidateStableId ?? string.Empty,
                VehicleStableId = value.VehicleStableId ?? string.Empty,
                IsEligible = value.IsEligible,
                IsRecommended = value.IsRecommended,
                IsSelected = value.IsSelected,
                Rank = value.Rank,
                PickupDistanceKm = value.PickupDistanceKm,
                VehicleCapacity = value.VehicleCapacity,
                VehicleCapacityUnitCode = value.VehicleCapacityUnitCode ?? string.Empty,
                Reason = value.Reason ?? string.Empty,
                BlockReasonCodes = value.BlockReasonCodes ?? Array.Empty<string>(),
                BaseScore = value.Score?.BaseScore ?? 0m,
                DriverWaitingScore = value.Score?.DriverWaitingScore ?? 0m,
                TotalScore = value.Score?.TotalScore ?? 0m,
            };

        [Serializable] private sealed class PreviewWire { public long ObservedRevision; public int ObservedWorldTick; public string? TransportRequestStableId; public string? DispatchOfferStableId; public string? RecommendedCarrierCandidateStableId; public string? RuleRevision; public CandidateWire[]? CandidateEvaluations; public LogisticsPreviewWire? LogisticsMovement; }
        [Serializable] private sealed class LogisticsPreviewWire { public string? CargoStableId; public decimal Quantity; public string? UnitCode; public int RequiredRouteTicks; public string? DestinationStockCandidateStableId; public string[]? BoundaryCodes; }
        [Serializable] private sealed class CandidateWire { public string? CarrierCandidateStableId; public string? VehicleStableId; public bool IsEligible; public bool IsRecommended; public bool IsSelected; public int Rank; public decimal? PickupDistanceKm; public decimal VehicleCapacity; public string? VehicleCapacityUnitCode; public string? Reason; public string[]? BlockReasonCodes; public ScoreWire? Score; }
        [Serializable] private sealed class ScoreWire { public decimal BaseScore; public decimal DriverWaitingScore; public decimal TotalScore; }
        [Serializable] private sealed class SessionWire { public string? SessionStableId; public int CurrentTick; public long Revision; public WorldWire? WorldContext; public SettlementWire? Settlement; public MovementWire[]? LogisticsMovements; public FreightWire[]? FreightTransports; public TaskWire[]? Tasks; }
        [Serializable] private sealed class WorldWire { public int WorldTick; public string? GameDate; }
        [Serializable] private sealed class SettlementWire { public decimal TreasuryBalance; public decimal TreasuryReserved; public decimal LaborAvailable; public decimal LaborReserved; public decimal StorageOccupied; public decimal StorageReserved; public decimal FoodReserveEquivalent; public decimal FoodSecurityDays; public string[]? ActiveTaskStableIds; public MarketWire[]? MarketSupplyByProduct; public AllocationWire[]? HarvestLotAllocations; }
        [Serializable] private sealed class MarketWire { public string? ProductStableId; public decimal Quantity; }
        [Serializable] private sealed class AllocationWire { public string? AllocationStableId; public string? StateCode; public decimal AvailableQuantity; }
        [Serializable] private sealed class MovementWire { public string? CargoStableId; public string? StateCode; public string? SourceAllocationStableId; public string? TaskStableId; public decimal Quantity; public decimal ReservedQuantity; public int CompletedRouteTicks; public int RequiredRouteTicks; public string? RouteStableId; public string? DestinationStockCandidateStableId; }
        [Serializable] private sealed class FreightWire { public string? TransportRequestStableId; public string? DispatchStateCode; public string? CarrierCandidateStableId; public string? VehicleStableId; public DispatchDecisionWire? DispatchDecision; }
        [Serializable] private sealed class DispatchDecisionWire { public string? RuleRevision; }
        [Serializable] private sealed class TaskWire { public string? TaskStableId; public string? StateCode; }
    }
}
