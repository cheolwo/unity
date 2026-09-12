using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.TeamObservation;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class 팀원관전ServerRepository : ITeamObservationAuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 팀원관전ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public Task<TeamObservationSessionApiModel> StartAsync(
            string sessionStableId,
            TeamObservationSessionStartApiModel request,
            CancellationToken cancellationToken)
            => SendAsync<TeamObservationSessionApiModel>("POST",
                SimulationTeamObservationApiRoutes.Start(sessionStableId),
                request, cancellationToken);

        public Task<TeamObservationFrameApiModel> LoadFrameAsync(
            string sessionStableId,
            string observationSessionStableId,
            CancellationToken cancellationToken)
            => SendAsync<TeamObservationFrameApiModel>("GET",
                SimulationTeamObservationApiRoutes.Frame(sessionStableId,
                    observationSessionStableId), null, cancellationToken);

        public Task<TeamObservationSessionApiModel> EndAsync(
            string sessionStableId,
            string observationSessionStableId,
            TeamObservationSessionEndApiModel request,
            CancellationToken cancellationToken)
            => SendAsync<TeamObservationSessionApiModel>("POST",
                SimulationTeamObservationApiRoutes.End(sessionStableId,
                    observationSessionStableId), request, cancellationToken);

        public Task<TeamObserverIndicatorApiModel> LoadObserversAsync(
            string sessionStableId,
            string targetActorStableId,
            CancellationToken cancellationToken)
            => SendAsync<TeamObserverIndicatorApiModel>("GET",
                SimulationTeamObservationApiRoutes.Observers(sessionStableId,
                    targetActorStableId), null, cancellationToken);

        private async Task<T> SendAsync<T>(
            string method,
            string route,
            object body,
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
                    "TeamObservationRequestFailed:" + response.StatusCode
                    + ":" + response.ErrorCode);
            return JsonConvert.DeserializeObject<T>(response.Body)
                ?? throw new InvalidOperationException(
                    "TeamObservationJsonInvalid");
        }
    }
}
