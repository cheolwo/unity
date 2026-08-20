using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public interface ISimulationUnifiedBattleAuthorityClient
    {
        Task<BattleCreatePreviewApiModel> PreviewAsync(string sessionStableId,
            BattleCreatePreviewCommandDraft request, CancellationToken cancellationToken);
        Task<BattleInstanceApiModel> ConfirmAsync(string sessionStableId,
            BattleCreateConfirmDraft request, CancellationToken cancellationToken);
        Task<BattleInstanceApiModel> ConfirmLocalActionAsync(string sessionStableId,
            string battleStableId, LocalCombatActionCommandDraft request,
            CancellationToken cancellationToken);
        Task<BattleInstanceApiModel> ConfirmLocalControlModeAsync(string sessionStableId,
            string battleStableId, LocalCombatControlModeCommandDraft request,
            CancellationToken cancellationToken);
        Task<BattleInstanceApiModel> AdvanceAsync(string sessionStableId,
            string battleStableId, string commandId, long expectedRevision,
            int combatTickCount, CancellationToken cancellationToken);
    }

    public sealed class SimulationUnifiedBattleServerRepository
        : ISimulationUnifiedBattleAuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public SimulationUnifiedBattleServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public Task<BattleCreatePreviewApiModel> PreviewAsync(string sessionStableId,
            BattleCreatePreviewCommandDraft request, CancellationToken cancellationToken)
            => SendAsync<BattleCreatePreviewApiModel>("POST",
                Base(sessionStableId) + "/previews", request, cancellationToken);

        public Task<BattleInstanceApiModel> ConfirmAsync(string sessionStableId,
            BattleCreateConfirmDraft request, CancellationToken cancellationToken)
            => SendAsync<BattleInstanceApiModel>("POST",
                Base(sessionStableId) + "/confirm", request, cancellationToken);

        public Task<BattleInstanceApiModel> ConfirmLocalActionAsync(string sessionStableId,
            string battleStableId, LocalCombatActionCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync<BattleInstanceApiModel>("POST", Base(sessionStableId) + "/"
                + Required(battleStableId) + "/local-actions/confirm", request,
                cancellationToken);

        public Task<BattleInstanceApiModel> ConfirmLocalControlModeAsync(
            string sessionStableId, string battleStableId,
            LocalCombatControlModeCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync<BattleInstanceApiModel>("POST", Base(sessionStableId) + "/"
                + Required(battleStableId) + "/local-control-mode/confirm", request,
                cancellationToken);

        public Task<BattleInstanceApiModel> AdvanceAsync(string sessionStableId,
            string battleStableId, string commandId, long expectedRevision,
            int combatTickCount, CancellationToken cancellationToken)
            => SendAsync<BattleInstanceApiModel>("POST", Base(sessionStableId) + "/"
                + Required(battleStableId) + "/ticks", new
                {
                    CommandId = commandId,
                    ExpectedBattleRevision = expectedRevision,
                    CombatTickCount = combatTickCount,
                }, cancellationToken);

        private async Task<T> SendAsync<T>(string method, string route, object body,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = JsonConvert.SerializeObject(body),
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
            {
                var error = JsonConvert.DeserializeObject<ErrorWire>(response.Body);
                throw new InvalidOperationException("UnifiedBattleRequestFailed:"
                    + response.StatusCode + ":" + (error?.ErrorCode ?? response.ErrorCode));
            }
            return JsonConvert.DeserializeObject<T>(response.Body)
                ?? throw new InvalidOperationException("UnifiedBattleJsonInvalid");
        }

        private static string Base(string sessionStableId)
            => "api/simulation/v1/sessions/" + Required(sessionStableId) + "/battles";

        private static string Required(string value)
            => !string.IsNullOrWhiteSpace(value) ? Uri.EscapeDataString(value.Trim())
                : throw new ArgumentException("UnifiedBattleRouteValueRequired");

        private sealed class ErrorWire
        {
            public string ErrorCode { get; set; } = string.Empty;
        }
    }
}
