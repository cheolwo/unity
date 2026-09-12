using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class Hosted비콘ServerRepository : IHosted비콘AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;
        public Hosted비콘ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<Hosted세계Data> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken)
            => ReadState(await SendAsync("GET", Route(sessionStableId), string.Empty,
                cancellationToken));

        public async Task<Hosted행동PreviewData> PreviewOpenAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken)
            => ReadPreview(await SendAsync("POST", Route(sessionStableId) + "/open-previews",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    OwnerPlayerStableId = Hosted비콘Codes.Owner,
                    InvitedGuestPlayerStableId = Hosted비콘Codes.Guest,
                }), cancellationToken));

        public async Task<Hosted세계Data> ConfirmOpenAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken)
        {
            await SendAsync("POST", Route(sessionStableId) + "/open/confirm",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.hosted.open:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    OwnerPlayerStableId = Hosted비콘Codes.Owner,
                    InvitedGuestPlayerStableId = Hosted비콘Codes.Guest,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Hosted행동PreviewData> PreviewJoinAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken)
            => ReadPreview(await SendAsync("POST", Route(sessionStableId) + "/join-previews",
                JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    GuestPlayerStableId = Hosted비콘Codes.Guest,
                }), cancellationToken));

        public async Task<Hosted세계Data> ConfirmJoinAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken)
        {
            await SendAsync("POST", Route(sessionStableId) + "/join/confirm",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.hosted.join:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    GuestPlayerStableId = Hosted비콘Codes.Guest,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Hosted행동PreviewData> PreviewGuestActionAsync(
            string sessionStableId, long expectedRevision, string capabilityCode,
            CancellationToken cancellationToken)
            => ReadPreview(await SendAsync("POST", Route(sessionStableId)
                + "/guest-action-previews", GuestActionJson(expectedRevision,
                    capabilityCode, null), cancellationToken));

        public async Task<Hosted세계Data> ConfirmGuestActionAsync(
            string sessionStableId, long expectedRevision, string capabilityCode,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", Route(sessionStableId) + "/guest-actions/confirm",
                GuestActionJson(expectedRevision, capabilityCode,
                    "command:unity.hosted.guest-action:" + capabilityCode.ToLowerInvariant()
                    + ":" + expectedRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Hosted세계Data> AdvanceAsync(string sessionStableId,
            long expectedRevision, int tickCount, CancellationToken cancellationToken)
        {
            await SendAsync("POST", "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim()) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.hosted.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = tickCount,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
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
                throw new InvalidOperationException("HostedWorldServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static Hosted세계Data ReadState(UnityApiResponse response)
            => JsonConvert.DeserializeObject<Hosted세계Data>(response.Body)
               ?? throw new InvalidOperationException("HostedWorldJsonInvalid");
        private static Hosted행동PreviewData ReadPreview(UnityApiResponse response)
            => JsonConvert.DeserializeObject<Hosted행동PreviewData>(response.Body)
               ?? throw new InvalidOperationException("HostedPreviewJsonInvalid");
        private static string Route(string sessionStableId)
            => "api/simulation/v1/sessions/" + Uri.EscapeDataString(sessionStableId.Trim())
                + "/hosted-world";
        private static string GuestActionJson(long revision, string capability,
            string? commandId) => JsonConvert.SerializeObject(new
            {
                CommandId = commandId,
                ExpectedRevision = revision,
                GuestPlayerStableId = Hosted비콘Codes.Guest,
                ScopeStableId = Hosted비콘Codes.FarmAreaSet,
                CapabilityCode = capability,
                TargetStableId = "facility:sim.farm",
            }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    }
}
