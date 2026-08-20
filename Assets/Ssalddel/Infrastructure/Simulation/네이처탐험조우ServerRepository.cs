using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 세션 상태 사본에서 네이처 조우만 읽습니다. 전투 결과와 적 수는 Unity가 만들지 않습니다.
    /// </summary>
    public sealed class 네이처탐험조우ServerRepository
        : I네이처탐험조우AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 네이처탐험조우ServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<네이처탐험조우StateApiModel> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException("NatureEncounterSessionMissing",
                    nameof(sessionStableId));
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = "api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(sessionStableId.Trim()),
                JsonBody = string.Empty,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("NatureEncounterRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            var wire = JsonConvert.DeserializeObject<SessionWire>(response.Body)
                ?? throw new InvalidOperationException("NatureEncounterJsonInvalid");
            var threat = wire.NatureThreat ?? new NatureThreatWire();
            var model = new 네이처탐험조우StateApiModel
            {
                SessionStableId = wire.SessionStableId ?? string.Empty,
                WorldRevision = wire.Revision,
                SimulationOnly = threat.SimulationOnly,
                IsOperationalState = threat.IsOperationalState,
                Encounters = Array.ConvertAll(threat.Encounters
                        ?? Array.Empty<EncounterWire>(), value =>
                    new 네이처탐험조우ApiModel
                    {
                        EncounterStableId = value.EncounterStableId ?? string.Empty,
                        EncounterRevision = value.EncounterRevision,
                        NatureRouteCode = value.NatureRouteCode ?? string.Empty,
                        StateCode = value.StateCode ?? string.Empty,
                        RiskBandCode = value.RiskBandCode ?? string.Empty,
                        ThreatUnitCount = value.ThreatUnitCount,
                        PresentationKey = value.PresentationKey ?? string.Empty,
                    }),
            };
            model.Validate();
            return model;
        }

        [Serializable]
        private sealed class SessionWire
        {
            public string? SessionStableId;
            public long Revision;
            public NatureThreatWire? NatureThreat;
        }

        [Serializable]
        private sealed class NatureThreatWire
        {
            public EncounterWire[]? Encounters;
            public bool SimulationOnly = true;
            public bool IsOperationalState;
        }

        [Serializable]
        private sealed class EncounterWire
        {
            public string? EncounterStableId;
            public long EncounterRevision;
            public string? NatureRouteCode;
            public string? StateCode;
            public string? RiskBandCode;
            public int ThreatUnitCount;
            public string? PresentationKey;
        }
    }
}
