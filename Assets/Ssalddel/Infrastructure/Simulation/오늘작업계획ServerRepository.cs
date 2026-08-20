using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 오늘작업계획ServerRepository : I오늘작업계획AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 오늘작업계획ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<오늘작업계획PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision,
            오늘작업계획ItemData[] items, CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST",
                FarmRoute(sessionStableId) + "/work-plans/preview",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    Items = items,
                }), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PlanPreviewWire>(response.Body)
                ?? throw new InvalidOperationException("DailyWorkPlanPreviewJsonInvalid");
            return new 오늘작업계획PreviewData
            {
                ExpectedRevision = wire.ExpectedRevision,
                Items = (wire.Items ?? Array.Empty<PlanItemPreviewWire>()).Select(value =>
                    new 오늘작업계획ItemPreviewData
                    {
                        PlanItemStableId = value.PlanItemStableId ?? string.Empty,
                        Priority = value.Priority,
                        ActorStableId = value.Work?.ActorStableId ?? string.Empty,
                        TargetStableId = value.Work?.TargetStableId ?? string.Empty,
                        ActionCode = value.Work?.ActionCode ?? string.Empty,
                        AssignmentKindCode = value.Work?.AssignmentKindCode ?? string.Empty,
                        ProjectedQuantity = value.Work?.ProjectedQuantity ?? 0m,
                        ProjectedQuantityUnitCode = value.Work?.ProjectedQuantityUnitCode
                            ?? string.Empty,
                        DurationTicks = value.Work?.DurationTicks ?? 0,
                        EstimatedCompletionWorldTick = value.Work?.EstimatedCompletionWorldTick
                            ?? 0,
                        CanConfirm = value.Work?.CanConfirm ?? false,
                        BlockingReasonCodes = value.BlockingReasonCodes ?? Array.Empty<string>(),
                    }).ToArray(),
                TotalRequiredLabor = wire.TotalRequiredLabor,
                TotalStaminaCost = wire.TotalStaminaCost,
                EstimatedCompletionWorldTick = wire.EstimatedCompletionWorldTick,
                CanConfirm = wire.CanConfirm,
                BlockingReasonCodes = wire.BlockingReasonCodes ?? Array.Empty<string>(),
            };
        }

        public async Task<오늘작업CanonicalStateData> ConfirmAsync(
            string sessionStableId, string commandId, long expectedRevision,
            오늘작업계획ItemData[] items, CancellationToken cancellationToken)
        {
            await SendAsync("POST", FarmRoute(sessionStableId) + "/work-plans/confirm",
                JsonConvert.SerializeObject(new
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    Items = items,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<오늘작업CanonicalStateData> AdvanceOneTickAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", SessionRoute(sessionStableId) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.daily-work.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = 1,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<오늘작업CanonicalStateData> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", FarmRoute(sessionStableId),
                string.Empty, cancellationToken);
            var wire = JsonConvert.DeserializeObject<StateWire>(response.Body)
                ?? throw new InvalidOperationException("DailyWorkCanonicalStateJsonInvalid");
            return new 오늘작업CanonicalStateData
            {
                WorldRevision = wire.WorldRevision,
                WorldTick = wire.WorldTick,
                WorkOrders = (wire.WorkOrders ?? Array.Empty<WorkOrderWire>()).Select(value =>
                    new 오늘작업OrderData
                    {
                        WorkOrderStableId = value.WorkOrderStableId ?? string.Empty,
                        TargetStableId = value.TargetStableId ?? string.Empty,
                        ActionCode = value.ActionCode ?? string.Empty,
                        AssignmentKindCode = value.AssignmentKindCode ?? string.Empty,
                        StatusCode = value.StatusCode ?? string.Empty,
                        CompletesWorldTick = value.CompletesWorldTick,
                    }).ToArray(),
                HarvestLots = (wire.HarvestLots ?? Array.Empty<HarvestLotWire>()).Select(value =>
                    new 오늘수확LotData
                    {
                        HarvestLotStableId = value.HarvestLotStableId ?? string.Empty,
                        Quantity = value.Quantity,
                        UnitCode = value.UnitCode ?? string.Empty,
                        StateCode = value.StateCode ?? string.Empty,
                    }).ToArray(),
                PackageLots = (wire.PackageLots ?? Array.Empty<PackageLotWire>()).Select(value =>
                    new 오늘포장LotData
                    {
                        PackageLotStableId = value.PackageLotStableId ?? string.Empty,
                        Quantity = value.Quantity,
                        UnitCode = value.UnitCode ?? string.Empty,
                        StateCode = value.StateCode ?? string.Empty,
                    }).ToArray(),
            };
        }

        private async Task<UnityApiResponse> SendAsync(
            string method, string route, string jsonBody,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = jsonBody,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("DailyWorkServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("DailyWorkSessionInvalid");
            return "api/simulation/v1/sessions/" + Uri.EscapeDataString(sessionStableId.Trim());
        }

        private static string FarmRoute(string sessionStableId)
            => SessionRoute(sessionStableId) + "/farm-survival";

        [Serializable] private sealed class PlanPreviewWire { public long ExpectedRevision; public PlanItemPreviewWire[] Items; public decimal TotalRequiredLabor; public decimal TotalStaminaCost; public int EstimatedCompletionWorldTick; public bool CanConfirm; public string[] BlockingReasonCodes; }
        [Serializable] private sealed class PlanItemPreviewWire { public string PlanItemStableId; public int Priority; public WorkPreviewWire Work; public string[] BlockingReasonCodes; }
        [Serializable] private sealed class WorkPreviewWire { public string ActorStableId; public string TargetStableId; public string ActionCode; public string AssignmentKindCode; public decimal ProjectedQuantity; public string ProjectedQuantityUnitCode; public int DurationTicks; public int EstimatedCompletionWorldTick; public bool CanConfirm; }
        [Serializable] private sealed class StateWire { public long WorldRevision; public int WorldTick; public WorkOrderWire[] WorkOrders; public HarvestLotWire[] HarvestLots; public PackageLotWire[] PackageLots; }
        [Serializable] private sealed class WorkOrderWire { public string WorkOrderStableId; public string TargetStableId; public string ActionCode; public string AssignmentKindCode; public string StatusCode; public int CompletesWorldTick; }
        [Serializable] private sealed class HarvestLotWire { public string HarvestLotStableId; public decimal Quantity; public string UnitCode; public string StateCode; }
        [Serializable] private sealed class PackageLotWire { public string PackageLotStableId; public decimal Quantity; public string UnitCode; public string StateCode; }
    }
}
