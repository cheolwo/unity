using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
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
        Task<BattleInstanceApiModel> ConfirmObserverInterventionAsync(
            string sessionStableId, string battleStableId,
            LocalCombatObserverInterventionCommandDraft request,
            CancellationToken cancellationToken);
        Task<BattleInstanceApiModel> AdvanceAsync(string sessionStableId,
            string battleStableId, string commandId, long expectedRevision,
            int combatTickCount, CancellationToken cancellationToken);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
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

        public Task<BattleInstanceApiModel> ConfirmObserverInterventionAsync(
            string sessionStableId, string battleStableId,
            LocalCombatObserverInterventionCommandDraft request,
            CancellationToken cancellationToken)
            => SendAsync<BattleInstanceApiModel>("POST", Base(sessionStableId) + "/"
                + Required(battleStableId) + "/observer-interventions/confirm", request,
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
