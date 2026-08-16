using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Survival;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class SimulationFarmCombatRequestException : Exception
    {
        public SimulationFarmCombatRequestException(
            long statusCode, string errorCode)
            : base("FarmCombatRequestFailed:" + statusCode + ":" + errorCode)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode ?? string.Empty;
        }

        public long StatusCode { get; }
        public string ErrorCode { get; }
        public bool IsRevisionConflict
            => StatusCode == 409
                || ErrorCode == "SimulationExpectedRevisionMismatch";
    }

    /// <summary>
    /// 농장 전투 전용 Simulation API adapter입니다. 전체 생존 상태에서 전투·교전만
    /// 투영하며 운영 서버나 실제 업무 원장을 호출하지 않습니다.
    /// </summary>
    public sealed class SimulationFarmCombatServerRepository
        : ISimulationFarmCombatAuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public SimulationFarmCombatServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client
                ?? throw new ArgumentNullException(nameof(client));

        public Task<FarmCombatStateApiModel> LoadAsync(
            string sessionStableId,
            CancellationToken cancellationToken)
            => SendAsync("GET", SimulationFarmCombatApiRoutes.State(sessionStableId),
                null, cancellationToken);

        public Task<FarmCombatStateApiModel> ConfirmPerspectiveAsync(
            string sessionStableId,
            FarmCombatPerspectiveCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync("POST",
                SimulationFarmCombatApiRoutes.Perspective(sessionStableId),
                request, cancellationToken);

        public Task<FarmCombatStateApiModel> StartBeatAsync(
            string sessionStableId,
            FarmCombatBeatStartCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync("POST",
                SimulationFarmCombatApiRoutes.StartBeat(sessionStableId),
                request, cancellationToken);

        public Task<FarmCombatStateApiModel> ConfirmReactionAsync(
            string sessionStableId,
            FarmCombatReactionCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync("POST",
                SimulationFarmCombatApiRoutes.Reaction(
                    sessionStableId, request.BeatStableId),
                request, cancellationToken);

        private async Task<FarmCombatStateApiModel> SendAsync(
            string method,
            string route,
            object body,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = body == null ? string.Empty
                    : JsonConvert.SerializeObject(body),
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
            {
                var error = JsonConvert.DeserializeObject<ErrorWire>(response.Body);
                throw new SimulationFarmCombatRequestException(
                    response.StatusCode,
                    !string.IsNullOrWhiteSpace(error?.ErrorCode)
                        ? error.ErrorCode
                        : response.ErrorCode);
            }

            var wire = JsonConvert.DeserializeObject<FarmSurvivalWire>(response.Body)
                ?? throw new InvalidOperationException("FarmCombatJsonInvalid");
            return Map(wire);
        }

        private static FarmCombatStateApiModel Map(FarmSurvivalWire wire)
        {
            var state = wire.Combat
                ?? throw new InvalidOperationException("FarmCombatStateMissing");
            state.WorldRevision = wire.WorldRevision;
            state.Engagements = Array.ConvertAll(
                wire.Encounters ?? Array.Empty<EncounterWire>(),
                value => new FarmCombatEngagementApiModel
                {
                    EncounterStableId = value.EncounterStableId ?? string.Empty,
                    StateCode = value.StateCode ?? string.Empty,
                    PresentationKey = value.PresentationKey ?? string.Empty,
                });
            state.Perspectives ??= Array.Empty<FarmCombatPerspectiveApiModel>();
            state.Beats ??= Array.Empty<FarmCombatBeatApiModel>();
            state.Reactions ??= Array.Empty<FarmCombatReactionApiModel>();
            state.Tactical ??= new FarmTacticalCombatStateApiModel();
            state.SimulationOnly = wire.SimulationOnly && state.SimulationOnly;
            state.IsOperationalState = wire.IsOperationalState
                || state.IsOperationalState;
            if (state.WorldRevision < 0 || !state.SimulationOnly
                || state.IsOperationalState)
                throw new InvalidOperationException("FarmCombatAuthorityBoundaryInvalid");
            return state;
        }

        private sealed class FarmSurvivalWire
        {
            public long WorldRevision { get; set; }
            public EncounterWire[] Encounters { get; set; }
                = Array.Empty<EncounterWire>();
            public FarmCombatStateApiModel Combat { get; set; }
                = new FarmCombatStateApiModel();
            public bool SimulationOnly { get; set; } = true;
            public bool IsOperationalState { get; set; }
        }

        private sealed class EncounterWire
        {
            public string EncounterStableId { get; set; } = string.Empty;
            public string StateCode { get; set; } = string.Empty;
            public string PresentationKey { get; set; } = string.Empty;
        }

        private sealed class ErrorWire
        {
            public string ErrorCode { get; set; } = string.Empty;
        }
    }
}
