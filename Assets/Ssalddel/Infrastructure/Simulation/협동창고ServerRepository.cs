using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 협동창고ServerRepository : I협동창고AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;
        public 협동창고ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<협동창고StateData> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken)
            => ReadState(await SendAsync("GET", Route(sessionStableId), string.Empty,
                cancellationToken));

        public async Task<협동창고PreviewData> PreviewContributionAsync(
            string sessionStableId, long expectedRevision, string playerStableId,
            string lotStableId, long expectedLotRevision,
            CancellationToken cancellationToken)
            => ReadPreview(await SendAsync("POST", Route(sessionStableId)
                + "/contribution-previews", ContributionJson(expectedRevision,
                    playerStableId, lotStableId, expectedLotRevision, null),
                cancellationToken));

        public async Task<협동창고StateData> ConfirmContributionAsync(
            string sessionStableId, 협동창고PreviewData preview,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", Route(sessionStableId) + "/contributions/confirm",
                ContributionJson(preview.BaseRevision, preview.PlayerStableId,
                    preview.SourceLotStableId, preview.SourceLotRevision,
                    "command:unity.coop.contribution:" + preview.PlayerStableId
                    + ":" + preview.BaseRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<협동창고PreviewData> PreviewProtectedActionAsync(
            string sessionStableId, long expectedRevision, string actionCode,
            CancellationToken cancellationToken)
        {
            var suffix = actionCode == 협동창고Codes.Restore
                ? "/restore-previews" : "/demolition-previews";
            return ReadPreview(await SendAsync("POST", Route(sessionStableId) + suffix,
                ProtectedJson(expectedRevision, null), cancellationToken));
        }

        public async Task<협동창고StateData> ConfirmProtectedActionAsync(
            string sessionStableId, 협동창고PreviewData preview,
            CancellationToken cancellationToken)
        {
            var suffix = preview.ActionCode == 협동창고Codes.Restore
                ? "/restores/confirm" : "/demolitions/confirm";
            await SendAsync("POST", Route(sessionStableId) + suffix,
                ProtectedJson(preview.BaseRevision,
                    "command:unity.coop." + preview.ActionCode.ToLowerInvariant()
                    + ":" + preview.BaseRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<협동창고StateData> AdvanceAsync(string sessionStableId,
            long expectedRevision, int ticks, CancellationToken cancellationToken)
        {
            await SendAsync("POST", "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim()) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.coop.tick:" + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = ticks,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        private async Task<UnityApiResponse> SendAsync(string method, string route,
            string json, CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = json,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("CoopConstructionServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string ContributionJson(long revision, string player,
            string lot, long lotRevision, string? commandId)
            => JsonConvert.SerializeObject(new
            {
                CommandId = commandId,
                ExpectedRevision = revision,
                PlayerStableId = player,
                ProjectStableId = 협동창고Codes.Project,
                BlueprintStableId = 협동창고Codes.Blueprint,
                BuildSiteH1StableId = 협동창고Codes.BuildSite,
                SourceLotStableId = lot,
                ExpectedSourceLotRevision = lotRevision,
                RequestedQuantity = 20m,
            }, IgnoreNulls);
        private static string ProtectedJson(long revision, string? commandId)
            => JsonConvert.SerializeObject(new
            {
                CommandId = commandId,
                ExpectedRevision = revision,
                OwnerPlayerStableId = Hosted비콘Codes.Owner,
                ProjectStableId = 협동창고Codes.Project,
            }, IgnoreNulls);
        private static 협동창고StateData ReadState(UnityApiResponse response)
            => JsonConvert.DeserializeObject<협동창고StateData>(response.Body)
               ?? throw new InvalidOperationException("CoopConstructionJsonInvalid");
        private static 협동창고PreviewData ReadPreview(UnityApiResponse response)
            => JsonConvert.DeserializeObject<협동창고PreviewData>(response.Body)
               ?? throw new InvalidOperationException("CoopConstructionPreviewJsonInvalid");
        private static string Route(string sessionStableId)
            => "api/simulation/v1/sessions/" + Uri.EscapeDataString(sessionStableId.Trim())
                + "/coop-construction";
        private static readonly JsonSerializerSettings IgnoreNulls = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
}
