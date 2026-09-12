using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 산수몽지도ServerRepository
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 산수몽지도ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<SimulationWorldExplorationStateSnapshot> LoadAsync(
            string sessionStableId, string actorStableId,
            CancellationToken cancellationToken)
        {
            var route = "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId)
                + "/exploration-state?actorStableId="
                + Uri.EscapeDataString(actorStableId);
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = route,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("MengMapRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return JsonConvert.DeserializeObject<SimulationWorldExplorationStateSnapshot>(
                       response.Body)
                   ?? throw new InvalidOperationException("MengMapJsonInvalid");
        }
    }
}
