using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Exhibition;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 통합전시관SimulationSessionRepository
        : I통합전시관SimulationSessionRepository
    {
        private const string BaseRoute = "api/simulation/v1/sessions/";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 통합전시관SimulationSessionRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<통합전시관SimulationSessionState> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException("시뮬레이션 세션 고유 식별자가 필요합니다.", nameof(sessionStableId));

            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = BaseRoute + Uri.EscapeDataString(sessionStableId.Trim()),
                RequiresAuthentication = false,
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response == null || !response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
                throw new InvalidOperationException("IntegratedExhibitionSimulationSessionRequestFailed:"
                    + (response?.StatusCode ?? 0) + ":" + (response?.ErrorCode ?? "NoResponse"));

            var wire = JsonConvert.DeserializeObject<SessionWire>(response.Body)
                ?? throw new InvalidOperationException("IntegratedExhibitionSimulationSessionJsonInvalid");
            var world = wire.WorldContext
                ?? throw new InvalidOperationException("IntegratedExhibitionSimulationWorldContextMissing");
            var result = new 통합전시관SimulationSessionState
            {
                SessionStableId = wire.SessionStableId ?? string.Empty,
                ScenarioStableId = wire.ScenarioStableId ?? string.Empty,
                Revision = wire.Revision,
                WorldRevision = world.WorldRevision,
                WorldTick = world.WorldTick,
                GameDate = world.GameDate,
                ModeCode = wire.ModeCode ?? string.Empty,
                IsOperationalState = wire.IsOperationalState,
                FetchedAtUtc = DateTimeOffset.UtcNow,
            };
            result.Validate(sessionStableId);
            return result;
        }

        private sealed class SessionWire
        {
            [JsonProperty("sessionStableId")]
            public string SessionStableId { get; set; } = string.Empty;
            [JsonProperty("scenarioStableId")]
            public string ScenarioStableId { get; set; } = string.Empty;
            [JsonProperty("revision")]
            public long Revision { get; set; }
            [JsonProperty("modeCode")]
            public string ModeCode { get; set; } = string.Empty;
            [JsonProperty("isOperationalState")]
            public bool IsOperationalState { get; set; }
            [JsonProperty("worldContext")]
            public WorldWire WorldContext { get; set; } = null!;
        }

        private sealed class WorldWire
        {
            [JsonProperty("worldTick")]
            public int WorldTick { get; set; }
            [JsonProperty("worldRevision")]
            public long WorldRevision { get; set; }
            [JsonProperty("gameDate")]
            public DateTimeOffset GameDate { get; set; }
        }
    }
}
