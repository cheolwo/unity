using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Battles;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// Solo에서 HTTP 없이 공통 Simulation Core의 현장 전투 포트를 호출합니다.
    /// 원격 저장소와 같은 Unity 상태 사본을 반환하며 피해·승패·성과를 재계산하지 않습니다.
    /// </summary>
    public sealed class LocalSimulationUnifiedBattleAuthorityClient
        : ISimulationUnifiedBattleAuthorityClient
    {
        private readonly ISimulationBattleRuntime battles;

        public LocalSimulationUnifiedBattleAuthorityClient(
            ISimulationRuntimeModules runtime)
            => battles = runtime?.Battles
                ?? throw new ArgumentNullException(nameof(runtime));

        public async Task<BattleCreatePreviewApiModel> PreviewAsync(
            string sessionStableId, BattleCreatePreviewCommandDraft request,
            CancellationToken cancellationToken)
            => Map<BattleCreatePreviewApiModel>(await battles.PreviewBattleAsync(
                sessionStableId, new SimulationBattleCreatePreviewRequest
                {
                    ExpectedWorldRevision = request.ExpectedWorldRevision,
                    EncounterStableId = request.EncounterStableId,
                    RequestingActorStableId = request.RequestingActorStableId,
                }, cancellationToken));

        public async Task<BattleInstanceApiModel> ConfirmAsync(
            string sessionStableId, BattleCreateConfirmDraft request,
            CancellationToken cancellationToken)
            => Map<BattleInstanceApiModel>(await battles.ConfirmBattleAsync(
                sessionStableId, new SimulationBattleCreateConfirmRequest
                {
                    CommandId = request.CommandId,
                    ExpectedWorldRevision = request.ExpectedWorldRevision,
                    EncounterStableId = request.EncounterStableId,
                    RequestingActorStableId = request.RequestingActorStableId,
                    ExpectedBattleWorldContextHashSha256 =
                        request.ExpectedBattleWorldContextHashSha256,
                    ExpectedBattlefieldDerivationInputHashSha256 =
                        request.ExpectedBattlefieldDerivationInputHashSha256,
                }, cancellationToken));

        public async Task<BattleInstanceApiModel> ConfirmLocalActionAsync(
            string sessionStableId, string battleStableId,
            LocalCombatActionCommandDraft request,
            CancellationToken cancellationToken)
            => Map<BattleInstanceApiModel>(await battles.ConfirmBattleActionAsync(
                sessionStableId, battleStableId,
                new SimulationLocalCombatActionConfirmRequest
                {
                    CommandId = request.CommandId,
                    ExpectedBattleRevision = request.ExpectedBattleRevision,
                    RequestingActorStableId = request.RequestingActorStableId,
                    TargetActorStableId = request.TargetActorStableId,
                    ActionCode = request.ActionCode,
                    ReactionOffsetMs = request.ReactionOffsetMs,
                }, cancellationToken));

        public async Task<BattleInstanceApiModel> ConfirmLocalControlModeAsync(
            string sessionStableId, string battleStableId,
            LocalCombatControlModeCommandDraft request,
            CancellationToken cancellationToken)
            => Map<BattleInstanceApiModel>(await battles
                .ConfirmBattleControlModeAsync(sessionStableId, battleStableId,
                    new SimulationLocalCombatControlModeConfirmRequest
                    {
                        CommandId = request.CommandId,
                        ExpectedBattleRevision = request.ExpectedBattleRevision,
                        RequestingActorStableId = request.RequestingActorStableId,
                        ControlModeCode = request.ControlModeCode,
                        ExpectedCardLoadoutHashSha256 =
                            request.ExpectedCardLoadoutHashSha256,
                    }, cancellationToken));

        public async Task<BattleInstanceApiModel> ConfirmObserverInterventionAsync(
            string sessionStableId, string battleStableId,
            LocalCombatObserverInterventionCommandDraft request,
            CancellationToken cancellationToken)
            => Map<BattleInstanceApiModel>(await battles
                .ConfirmObserverInterventionAsync(sessionStableId, battleStableId,
                    new SimulationLocalCombatObserverInterventionConfirmRequest
                    {
                        CommandId = request.CommandId,
                        ExpectedBattleRevision = request.ExpectedBattleRevision,
                        RequestingActorStableId = request.RequestingActorStableId,
                        ActionCode = request.ActionCode,
                        CardCopyStableId = request.CardCopyStableId,
                    }, cancellationToken));

        public async Task<BattleInstanceApiModel> AdvanceAsync(
            string sessionStableId, string battleStableId, string commandId,
            long expectedRevision, int combatTickCount,
            CancellationToken cancellationToken)
            => Map<BattleInstanceApiModel>(await battles.AdvanceBattleAsync(
                sessionStableId, battleStableId, new SimulationBattleAdvanceRequest
                {
                    CommandId = commandId,
                    ExpectedBattleRevision = expectedRevision,
                    CombatTickCount = combatTickCount,
                }, cancellationToken));

        private static T Map<T>(object source)
            => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source))
                ?? throw new InvalidOperationException(
                    "LocalUnifiedBattleStateMapFailed");
    }
}
