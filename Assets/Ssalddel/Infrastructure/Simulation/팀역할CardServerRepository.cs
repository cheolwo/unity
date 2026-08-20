using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.TeamRoles;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>Simulation 서버의 공동 카드함 상태만 읽고 변경 요청을 전달한다.</summary>
    public sealed class 팀역할CardServerRepository : ITeamRoleCardAuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 팀역할CardServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public Task<TeamRoleCardStateApiModel> LoadAsync(
            string sessionStableId, string actorStableId,
            CancellationToken cancellationToken)
            => SendAsync("GET", TeamRoleCardApiRoutes.Get(sessionStableId,
                actorStableId), null, cancellationToken);

        public Task<TeamRoleCardStateApiModel> EquipAsync(
            string sessionStableId, TeamRoleCardEquipApiRequest request,
            CancellationToken cancellationToken)
            => SendAsync("POST", TeamRoleCardApiRoutes.Equip(sessionStableId),
                request, cancellationToken);

        public Task<TeamRoleCardStateApiModel> StartActivityAsync(
            string sessionStableId, TeamActivityStartApiRequest request,
            CancellationToken cancellationToken)
            => SendAsync("POST",
                TeamRoleCardApiRoutes.StartActivity(sessionStableId), request,
                cancellationToken);

        public Task<TeamRoleCardStateApiModel> EndActivityAsync(
            string sessionStableId, TeamActivityEndApiRequest request,
            CancellationToken cancellationToken)
            => SendAsync("POST", TeamRoleCardApiRoutes.EndActivity(sessionStableId),
                request, cancellationToken);

        public Task<TeamRoleCardStateApiModel> SetCombatLoadoutAsync(
            string sessionStableId, TeamCombatCardLoadoutSetApiRequest request,
            CancellationToken cancellationToken)
            => SendAsync("POST",
                TeamRoleCardApiRoutes.SetCombatLoadout(sessionStableId), request,
                cancellationToken);

        private async Task<TeamRoleCardStateApiModel> SendAsync(
            string method, string route, object body,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route.TrimStart('/'),
                JsonBody = body == null ? string.Empty
                    : JsonConvert.SerializeObject(body),
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException(
                    "TeamRoleCardRequestFailed:" + response.StatusCode
                    + ":" + response.ErrorCode);
            return JsonConvert.DeserializeObject<TeamRoleCardStateApiModel>(
                       response.Body)
                   ?? throw new InvalidOperationException(
                       "TeamRoleCardJsonInvalid");
        }
    }
}
