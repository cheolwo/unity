using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 진부Hub입고UiServerRepository : I진부Hub입고UiAuthorityClient
    {
        private const string ReceiptPreviewContract = "SimulationFreightReceiptPreviewRequest";
        private const string ReceiptConfirmContract = "SimulationFreightReceiptConfirmRequest";
        private const string PutAwayPreviewContract = "SimulationWarehousePutAwayPreviewRequest";
        private const string PutAwayConfirmContract = "SimulationWarehousePutAwayConfirmRequest";

        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 진부Hub입고UiServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<진부Hub입고UiProjectionData> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", SurfaceRoute(sessionStableId), string.Empty,
                cancellationToken);
            var projection = JsonConvert.DeserializeObject<진부Hub입고UiProjectionData>(response.Body)
                ?? throw new InvalidOperationException("JinbuInboundUiProjectionJsonInvalid");
            projection.Validate();
            return projection;
        }

        public async Task<진부Hub입고UiPreviewData> PreviewAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken)
        {
            ValidateProjectionAndAction(projection, action, 진부Hub입고UiCodes.PreviewAction);
            var invocation = action.Invocation
                ?? throw new InvalidOperationException("JinbuInboundUiInvocationMissing");
            string route;
            object request;
            switch (action.RequestContractKey)
            {
                case ReceiptPreviewContract:
                    route = SessionRoute(projection.SessionStableId) + "/freight-receipt-previews";
                    request = Receipt(invocation);
                    break;
                case PutAwayPreviewContract:
                    route = SessionRoute(projection.SessionStableId) + "/warehouse-put-away-previews";
                    request = PutAway(invocation);
                    break;
                default:
                    throw new InvalidOperationException("JinbuInboundUiPreviewContractNotAllowed");
            }

            var response = await SendAsync("POST", route, JsonConvert.SerializeObject(request),
                cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("JinbuInboundUiPreviewJsonInvalid");
            var preview = new 진부Hub입고UiPreviewData
            {
                ActionStableId = action.StableId,
                ActionLabel = action.KoreanLabel,
                TargetStableId = invocation.TargetStableId,
                ActorStableId = invocation.ActorStableId,
                DurationTicks = wire.TaskPlan?.DurationTicks ?? invocation.DurationTicks,
                TaskStableId = wire.TaskPlan?.TaskStableId ?? string.Empty,
                SpatialStableId = wire.SpatialInteraction?.SelectedSpatialStableId ?? string.Empty,
                SpatialEvidenceKindCode = wire.SpatialInteraction?.EvidenceKindCode ?? string.Empty,
                BlockReasonCodes = wire.Decision?.BlockReasonCodes ?? Array.Empty<string>(),
            };
            if (preview.BlockReasonCodes.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("JinbuInboundUiPreviewBlockReasonInvalid");
            return preview;
        }

        public async Task<진부Hub입고UiProjectionData> ConfirmAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken)
        {
            ValidateProjectionAndAction(projection, action, 진부Hub입고UiCodes.ConfirmAction);
            var invocation = action.Invocation
                ?? throw new InvalidOperationException("JinbuInboundUiInvocationMissing");
            if (!action.RequiresPreview || !action.RequiresExplicitConfirmation
                || !action.RequiresExpectedRevision
                || invocation.ExpectedStateRevision != projection.StateRevision)
                throw new InvalidOperationException("JinbuInboundUiConfirmBoundaryInvalid");

            string route;
            object command;
            var commandId = "command:unity.inbound-ui:"
                + action.CanonicalActionCode.ToLowerInvariant() + ":" + projection.StateRevision;
            switch (action.RequestContractKey)
            {
                case ReceiptConfirmContract:
                    route = SessionRoute(projection.SessionStableId) + "/freight-receipts/confirm";
                    command = new
                    {
                        CommandId = commandId,
                        ExpectedRevision = invocation.ExpectedStateRevision,
                        Receipt = Receipt(invocation),
                    };
                    break;
                case PutAwayConfirmContract:
                    route = SessionRoute(projection.SessionStableId) + "/warehouse-put-aways/confirm";
                    command = new
                    {
                        CommandId = commandId,
                        ExpectedRevision = invocation.ExpectedStateRevision,
                        PutAway = PutAway(invocation),
                    };
                    break;
                default:
                    throw new InvalidOperationException("JinbuInboundUiConfirmContractNotAllowed");
            }

            await SendAsync("POST", route, JsonConvert.SerializeObject(command), cancellationToken);
            var canonical = await LoadAsync(projection.SessionStableId, cancellationToken);
            if (canonical.StateRevision <= projection.StateRevision)
                throw new InvalidOperationException("JinbuInboundUiConfirmCanonicalRequeryInvalid");
            return canonical;
        }

        public async Task<진부Hub입고UiProjectionData> AdvanceAsync(
            진부Hub입고UiProjectionData projection,
            CancellationToken cancellationToken)
        {
            projection.Validate();
            var route = SessionRoute(projection.SessionStableId) + "/ticks";
            await SendAsync("POST", route, JsonConvert.SerializeObject(new
            {
                CommandId = "command:unity.inbound-ui.tick:" + projection.StateRevision,
                ExpectedRevision = projection.StateRevision,
                TickCount = 1,
            }), cancellationToken);
            var canonical = await LoadAsync(projection.SessionStableId, cancellationToken);
            if (canonical.StateRevision <= projection.StateRevision)
                throw new InvalidOperationException("JinbuInboundUiTickCanonicalRequeryInvalid");
            return canonical;
        }

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
                throw new InvalidOperationException(
                    "SimulationInboundUiRequestFailed:" + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static void ValidateProjectionAndAction(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            string expectedKind)
        {
            if (projection == null || action == null)
                throw new ArgumentNullException(projection == null ? nameof(projection) : nameof(action));
            projection.Validate();
            if (action.ActionKindCode != expectedKind || !action.Enabled
                || action.HttpMethod != "POST")
                throw new InvalidOperationException("JinbuInboundUiActionNotAllowed");
        }

        private static object Receipt(진부Hub입고UiInvocationData invocation)
            => new
            {
                TransportRequestStableId = invocation.TargetStableId,
                TransportRevision = invocation.TargetRevision,
                ActorStableId = invocation.ActorStableId,
                ReceiptDurationTicks = invocation.DurationTicks,
                SourceStableIds = invocation.SourceStableIds ?? Array.Empty<string>(),
            };

        private static object PutAway(진부Hub입고UiInvocationData invocation)
            => new
            {
                InventoryStableId = invocation.TargetStableId,
                InventoryRevision = invocation.TargetRevision,
                ActorStableId = invocation.ActorStableId,
                PutAwayDurationTicks = invocation.DurationTicks,
                SourceStableIds = invocation.SourceStableIds ?? Array.Empty<string>(),
            };

        private static string SurfaceRoute(string sessionStableId)
            => SessionRoute(sessionStableId) + "/world-ui/surfaces/"
                + Uri.EscapeDataString(진부Hub입고UiCodes.SurfaceStableId);

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("SimulationSessionStableIdMissing");
            return "api/simulation/v1/sessions/" + Uri.EscapeDataString(sessionStableId.Trim());
        }

        [Serializable]
        private sealed class PreviewWire
        {
            public DecisionWire Decision;
            public TaskPlanWire TaskPlan;
            public SpatialInteractionWire SpatialInteraction;
        }

        [Serializable]
        private sealed class DecisionWire
        {
            public string[] BlockReasonCodes = Array.Empty<string>();
        }

        [Serializable]
        private sealed class TaskPlanWire
        {
            public string TaskStableId = string.Empty;
            public int DurationTicks;
        }

        [Serializable]
        private sealed class SpatialInteractionWire
        {
            public string SelectedSpatialStableId = string.Empty;
            public string EvidenceKindCode = string.Empty;
        }
    }
}
