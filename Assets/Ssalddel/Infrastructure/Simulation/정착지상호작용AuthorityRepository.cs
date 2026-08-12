using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 정착지상호작용AuthorityRepository
        : I정착지상호작용AuthorityClient
    {
        private const string BaseRoute = "api/simulation/v1/sessions/";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 정착지상호작용AuthorityRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<정착지상호작용AuthoritySnapshot> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", SessionRoute(sessionStableId), string.Empty,
                cancellationToken);
            return ParseSnapshot(response.Body);
        }

        public async Task<정착지상호작용PreviewData> PreviewAsync(
            string sessionStableId,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            var current = await RefreshAsync(sessionStableId, cancellationToken);
            var response = await SendAsync(
                "POST",
                SessionRoute(sessionStableId) + "/harvest-disposition-impact-previews",
                JsonConvert.SerializeObject(request),
                cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("SimulationHarvestImpactPreviewJsonInvalid");
            return new 정착지상호작용PreviewData
            {
                SessionStableId = current.SessionStableId,
                ObservedRevision = current.Revision,
                ObservedWorldTick = current.WorldTick,
                ChoiceCode = wire.ChoiceCode ?? string.Empty,
                NextWorkflowCode = wire.NextWorkflowCode ?? string.Empty,
                HarvestLotStableId = wire.HarvestLotStableId ?? string.Empty,
                Quantity = wire.Quantity,
                UnitCode = wire.CanonicalQuantityUnitCode ?? string.Empty,
                RequiredLabor = wire.RequiredLabor,
                SimulationCost = wire.SimulationCost,
                ProjectedRevenue = wire.ProjectedRevenue,
                DurationTicks = wire.DurationTicks,
                FoodSecurityDaysBefore = wire.FoodSecurityDaysBefore,
                FoodSecurityDaysCandidate = wire.FoodSecurityDaysCandidate,
                ExpectedStoredQuantity = wire.StorageCandidate?.ExpectedStoredQuantity,
                PolicyRevision = wire.PolicyRevision ?? string.Empty,
                RiskCodes = wire.RiskCodes ?? Array.Empty<string>(),
                BoundaryCodes = wire.BoundaryCodes ?? Array.Empty<string>(),
                Request = request,
            };
        }

        public async Task<정착지상호작용AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            var body = JsonConvert.SerializeObject(new
            {
                CommandId = "command:unity.harvest-impact.confirm:" + expectedRevision,
                ExpectedRevision = expectedRevision,
                Impact = request,
            });
            var response = await SendAsync(
                "POST",
                SessionRoute(sessionStableId) + "/harvest-disposition-impacts/confirm",
                body,
                cancellationToken);
            return ParseSnapshot(response.Body);
        }

        public async Task<정착지상호작용AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            int tickCount,
            CancellationToken cancellationToken)
        {
            if (tickCount <= 0)
                throw new InvalidOperationException("SimulationTickCountInvalid");
            var body = JsonConvert.SerializeObject(new
            {
                CommandId = "command:unity.harvest-impact.tick:" + expectedRevision,
                ExpectedRevision = expectedRevision,
                TickCount = tickCount,
            });
            var response = await SendAsync(
                "POST",
                SessionRoute(sessionStableId) + "/ticks",
                body,
                cancellationToken);
            return ParseSnapshot(response.Body);
        }

        public async Task<수확판로결과Data> Get수확판로결과Async(
            string sessionStableId,
            string harvestLotStableId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(harvestLotStableId))
                throw new InvalidOperationException("SimulationHarvestLotStableIdMissing");
            var response = await SendAsync(
                "GET",
                SessionRoute(sessionStableId) + "/harvest-route-outcomes/"
                    + Uri.EscapeDataString(harvestLotStableId.Trim()),
                string.Empty,
                cancellationToken);
            var result = JsonConvert.DeserializeObject<수확판로결과Data>(response.Body)
                ?? throw new InvalidOperationException("SimulationHarvestRouteOutcomeJsonInvalid");
            return NormalizeOutcome(result);
        }

        public async Task<수확판로결과Data[]> Get수확판로결과목록Async(
            string sessionStableId,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync(
                "GET",
                SessionRoute(sessionStableId) + "/harvest-route-outcomes",
                string.Empty,
                cancellationToken);
            var results = JsonConvert.DeserializeObject<수확판로결과Data[]>(response.Body)
                ?? throw new InvalidOperationException("SimulationHarvestRouteOutcomeListJsonInvalid");
            return results.Select(NormalizeOutcome).ToArray();
        }

        private async Task<UnityApiResponse> SendAsync(
            string method,
            string relativePath,
            string body,
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
                throw new InvalidOperationException(
                    "SimulationAuthorityRequestFailed:" + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("SimulationSessionStableIdMissing");
            return BaseRoute + Uri.EscapeDataString(sessionStableId.Trim());
        }

        private static 수확판로결과Data NormalizeOutcome(수확판로결과Data result)
        {
            result.Routes ??= Array.Empty<수확판로선택지결과Data>();
            result.BoundaryCodes ??= Array.Empty<string>();
            result.SourceStableIds ??= Array.Empty<string>();
            foreach (var route in result.Routes)
            {
                if (route == null)
                    throw new InvalidOperationException("SimulationHarvestRouteOutcomeRouteJsonInvalid");
                route.RiskCodes ??= Array.Empty<string>();
                route.RelatedStableIds ??= Array.Empty<string>();
                route.SourceStableIds ??= Array.Empty<string>();
            }
            return result;
        }

        private static 정착지상호작용AuthoritySnapshot ParseSnapshot(string json)
        {
            var wire = JsonConvert.DeserializeObject<SessionWire>(json)
                ?? throw new InvalidOperationException("SimulationSessionSnapshotJsonInvalid");
            var settlement = wire.Settlement
                ?? throw new InvalidOperationException("SimulationSettlementSnapshotMissing");
            var worldTick = wire.WorldContext?.WorldTick ?? wire.CurrentTick;
            var tasks = wire.Tasks ?? Array.Empty<TaskWire>();
            var effects = wire.Effects ?? Array.Empty<EffectWire>();
            var lotTasks = (settlement.HarvestLotAllocations ?? Array.Empty<AllocationWire>())
                .Select(allocation =>
                {
                    var task = tasks.SingleOrDefault(value =>
                        value.TaskStableId == allocation.TaskStableId);
                    var effect = effects.FirstOrDefault(value =>
                        value.CausedByTaskStableId == allocation.TaskStableId);
                    return new 수확LotTaskAuthorityData
                    {
                        HarvestLotStableId = allocation.HarvestLotStableId ?? string.Empty,
                        AllocationStateCode = allocation.StateCode ?? string.Empty,
                        TaskStableId = allocation.TaskStableId ?? string.Empty,
                        TaskStateCode = task?.StateCode ?? string.Empty,
                        TaskScheduledStartTick = task?.ScheduledStartTick ?? 0,
                        TaskExpectedEndTick = task?.ExpectedEndTick ?? 0,
                        TaskRemainingTicks = task == null
                            ? 0
                            : Math.Max(0, task.ExpectedEndTick - worldTick),
                        EffectStateCode = effect?.StateCode ?? string.Empty,
                    };
                })
                .ToArray();
            var snapshot = new 정착지상호작용AuthoritySnapshot
            {
                SessionStableId = wire.SessionStableId ?? string.Empty,
                Revision = wire.Revision,
                WorldTick = worldTick,
                GameDateLabel = FormatGameDate(wire.WorldContext?.GameDate),
                TreasuryBalance = settlement.TreasuryBalance,
                TreasuryReserved = settlement.TreasuryReserved,
                LaborAvailable = settlement.LaborAvailable,
                LaborReserved = settlement.LaborReserved,
                MarketFoodSupplyKg = (settlement.MarketSupplyByProduct ?? Array.Empty<MarketSupplyWire>())
                    .Where(value => value.ProductStableId == "product:potato")
                    .Sum(value => value.Quantity),
                ReserveFoodEquivalent = settlement.FoodReserveEquivalent,
                StorageOccupied = settlement.StorageOccupied,
                StorageReserved = settlement.StorageReserved,
                FoodSecurityDays = settlement.FoodSecurityDays,
                ActiveTaskCount = settlement.ActiveTaskStableIds?.Length ?? 0,
                HarvestLotTasks = lotTasks,
                SourceModeCode = "SimulationServer",
            };
            return lotTasks.Length == 1
                ? snapshot.ForHarvestLot(lotTasks[0].HarvestLotStableId)
                : snapshot;
        }

        private static string FormatGameDate(string? value)
            => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "Simulation date unavailable";

        [Serializable]
        private sealed class PreviewWire
        {
            public string? ChoiceCode;
            public string? NextWorkflowCode;
            public string? HarvestLotStableId;
            public decimal Quantity;
            public string? CanonicalQuantityUnitCode;
            public decimal RequiredLabor;
            public decimal SimulationCost;
            public decimal? ProjectedRevenue;
            public int DurationTicks;
            public decimal FoodSecurityDaysBefore;
            public decimal FoodSecurityDaysCandidate;
            public string? PolicyRevision;
            public string[]? RiskCodes;
            public string[]? BoundaryCodes;
            public StorageCandidateWire? StorageCandidate;
        }

        [Serializable] private sealed class StorageCandidateWire { public decimal ExpectedStoredQuantity; }
        [Serializable]
        private sealed class SessionWire
        {
            public string? SessionStableId;
            public int CurrentTick;
            public long Revision;
            public WorldContextWire? WorldContext;
            public SettlementWire? Settlement;
            public TaskWire[]? Tasks;
            public EffectWire[]? Effects;
        }
        [Serializable] private sealed class WorldContextWire { public int WorldTick; public string? GameDate; }
        [Serializable]
        private sealed class SettlementWire
        {
            public decimal TreasuryBalance;
            public decimal TreasuryReserved;
            public decimal LaborAvailable;
            public decimal LaborReserved;
            public decimal StorageOccupied;
            public decimal StorageReserved;
            public decimal FoodReserveEquivalent;
            public decimal FoodSecurityDays;
            public string[]? ActiveTaskStableIds;
            public MarketSupplyWire[]? MarketSupplyByProduct;
            public AllocationWire[]? HarvestLotAllocations;
        }
        [Serializable] private sealed class MarketSupplyWire { public string? ProductStableId; public decimal Quantity; }
        [Serializable]
        private sealed class AllocationWire
        {
            public string? HarvestLotStableId;
            public string? TaskStableId;
            public string? StateCode;
        }
        [Serializable]
        private sealed class TaskWire
        {
            public string? TaskStableId;
            public string? StateCode;
            public int ScheduledStartTick;
            public int ExpectedEndTick;
        }
        [Serializable]
        private sealed class EffectWire
        {
            public string? CausedByTaskStableId;
            public string? StateCode;
        }
    }
}
