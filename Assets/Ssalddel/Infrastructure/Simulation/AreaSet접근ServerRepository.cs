using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class AreaSet접근ServerRepository : IAreaSet접근AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;
        public AreaSet접근ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<AreaSet접근Data> RefreshAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", SessionRoute(sessionStableId)
                + "/players/" + Uri.EscapeDataString(playerStableId)
                + "/area-access", string.Empty, cancellationToken);
            return JsonConvert.DeserializeObject<AreaSet접근Data>(response.Body)
                ?? throw new InvalidOperationException("AreaSetAccessJsonInvalid");
        }

        public async Task<AreaSet이동PreviewData> PreviewAsync(string sessionStableId,
            string playerStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST", SessionRoute(sessionStableId)
                + "/area-traversal-previews", TraversalJson(playerStableId,
                    expectedRevision, null), cancellationToken);
            return JsonConvert.DeserializeObject<AreaSet이동PreviewData>(response.Body)
                ?? throw new InvalidOperationException("AreaSetTraversalPreviewJsonInvalid");
        }

        public async Task<AreaSet접근Data> ConfirmAsync(string sessionStableId,
            string playerStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", SessionRoute(sessionStableId)
                + "/area-traversals/confirm", TraversalJson(playerStableId,
                    expectedRevision, "command:unity.area-traversal:"
                    + expectedRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, playerStableId,
                cancellationToken);
        }

        public async Task<AreaSet접근Data> AdvanceAsync(string sessionStableId,
            string playerStableId, long expectedRevision, int tickCount,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", SessionRoute(sessionStableId) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.area-traversal.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = tickCount,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, playerStableId,
                cancellationToken);
        }

        private async Task<UnityApiResponse> SendAsync(string method,
            string relativePath, string json, CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = relativePath,
                JsonBody = json,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("AreaSetAccessServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string TraversalJson(string playerStableId,
            long expectedRevision, string? commandId)
        {
            var request = new
            {
                CommandId = commandId,
                ExpectedRevision = expectedRevision,
                PlayerStableId = playerStableId,
                TargetAreaSetStableId = AreaSet접근Codes.HubAreaSet,
                ConnectorStableId = AreaSet접근Codes.FarmToHubConnector,
            };
            return JsonConvert.SerializeObject(request,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                });
        }

        private static string SessionRoute(string sessionStableId)
            => "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim());
    }
}
