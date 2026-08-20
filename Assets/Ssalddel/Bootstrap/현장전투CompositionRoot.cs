using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Survival;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 사건 규모에 따라 현재 H5/LH 공간의 현장 전투 또는 파생 전장 표현을 선택합니다.
    /// 피해·승패·WorldTick은 서버가 확정하며 이 구성요소는 입력 의도와 표현만 담당합니다.
    /// </summary>
    [DefaultExecutionOrder(-845)]
    [DisallowMultipleComponent]
    public sealed class 현장전투CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 전투시점Controller combat = null!;
        [SerializeField] private Transform battlefieldParent = null!;
        [SerializeField] private 공간LHStreamingEngine lhStreamingEngine = null!;
        [SerializeField] private string actorStableId = "actor:sim:player-survivor";
        [SerializeField] private bool 서버기준사용 = true;

        private ISimulationUnifiedBattleAuthorityClient? authority;
        private CancellationTokenSource? lifetime;
        private BattleInstanceApiModel? current;
        private string sessionStableId = string.Empty;
        private bool commandInFlight;
        private float tickAccumulator;
        private int commandSequence;
        private GameObject? derivedBattlefieldRoot;
        private string lastResolvedBattleStableId = string.Empty;

        public event Action<string> WorldLocalBattleResolved = delegate { };
        public event Action<string, string> WorldLocalBattleRequestFailed
            = delegate { };

        public BattleInstanceApiModel? Current => current;
        public bool ServerAuthorityEnabled => 서버기준사용;
        public bool AuthorityReady => authority != null
            && !string.IsNullOrWhiteSpace(sessionStableId);
        public bool PinsLhWindow => current?.CombatSpaceCode ==
            BattlePresentationCodes.WorldLocal && current.PhaseCode ==
            BattlePresentationCodes.Active;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell, 플레이어경관Controller playerController,
            전투시점Controller combatController, Transform derivedBattlefieldParent,
            string actorId, bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            player = playerController;
            combat = combatController;
            battlefieldParent = derivedBattlefieldParent;
            actorStableId = actorId ?? string.Empty;
            서버기준사용 = useServerAuthority;
            Bind();
            if (!ValidateWiring())
                throw new ArgumentException("LocalCombatCompositionWiringInvalid");
        }

        public bool ValidateWiring() => runtimeSettings != null && shell != null
            && player != null && combat != null && battlefieldParent != null
            && !string.IsNullOrWhiteSpace(actorStableId);

        private void Awake()
        {
            shell ??= FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            player ??= FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            combat ??= FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            lhStreamingEngine ??= FindFirstObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);
            battlefieldParent ??= transform;
            Bind();
        }

        private void Bind()
        {
            if (combat == null) return;
            combat.CombatEntryRequested -= OnCombatEntryRequested;
            combat.LocalActionCommandPrepared -= OnLocalActionPrepared;
            combat.CombatEntryRequested += OnCombatEntryRequested;
            combat.LocalActionCommandPrepared += OnLocalActionPrepared;
        }

        private async void Start()
        {
            if (!서버기준사용) return;
            try
            {
                lifetime = new CancellationTokenSource();
                sessionStableId = await WaitForSessionAsync(lifetime.Token);
                authority = new SimulationUnifiedBattleServerRepository(
                    new SimulationRehearsalUnityWebRequestApiClient(
                        runtimeSettings.ToOptions()));
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                combat.SetAuthorityFailure(error.Message);
                Debug.LogError("LocalCombatInitializationFailed:" + error.Message);
            }
        }

        private async void Update()
        {
            if (current == null || current.CombatSpaceCode !=
                    BattlePresentationCodes.WorldLocal
                || current.PhaseCode != BattlePresentationCodes.Active
                || authority == null || commandInFlight)
                return;
            var desiredControlMode = combat.DesiredLocalControlModeCode;
            if (current.LocalCombat.ControlModeCode != desiredControlMode)
            {
                commandInFlight = true;
                try
                {
                    Apply(await authority.ConfirmLocalControlModeAsync(sessionStableId,
                        current.BattleStableId, new LocalCombatControlModeCommandDraft
                        {
                            CommandId = NextCommandId("control-mode"),
                            ExpectedBattleRevision = current.BattleRevision,
                            RequestingActorStableId = actorStableId,
                            ControlModeCode = desiredControlMode,
                        }, lifetime?.Token ?? CancellationToken.None));
                }
                catch (Exception error) { combat.SetAuthorityFailure(error.Message); }
                finally { commandInFlight = false; }
                return;
            }
            tickAccumulator += Time.unscaledDeltaTime;
            // 서버 전투 틱은 100ms다. 한 번에 5틱을 넘기면 첫 틱의 공격 예고가
            // 같은 응답 안에서 해소되어 플레이어가 방어·회피할 기회를 잃는다.
            if (tickAccumulator < .1f) return;
            tickAccumulator = 0f;
            commandInFlight = true;
            try
            {
                Apply(await authority.AdvanceAsync(sessionStableId,
                    current.BattleStableId, NextCommandId("ticks"),
                    current.BattleRevision, 1,
                    lifetime?.Token ?? CancellationToken.None));
            }
            catch (Exception error) { combat.SetAuthorityFailure(error.Message); }
            finally { commandInFlight = false; }
        }

        private async void OnCombatEntryRequested(string encounterStableId)
        {
            if (authority == null || commandInFlight) return;
            commandInFlight = true;
            try
            {
                var preview = await authority.PreviewAsync(sessionStableId,
                    new BattleCreatePreviewCommandDraft
                    {
                        ExpectedWorldRevision = shell.WorldRevision,
                        EncounterStableId = encounterStableId,
                        RequestingActorStableId = actorStableId,
                    }, lifetime?.Token ?? CancellationToken.None);
                if (!preview.CanConfirm)
                    throw new InvalidOperationException("BattlePreviewBlocked:"
                        + string.Join(",", preview.BlockingReasonCodes));
                var battlefield = preview.ScaleDecision.CombatSpaceCode ==
                    BattlePresentationCodes.DerivedBattlefield;
                Apply(await authority.ConfirmAsync(sessionStableId,
                    new BattleCreateConfirmDraft
                    {
                        CommandId = NextCommandId("create"),
                        ExpectedWorldRevision = preview.WorldRevision,
                        EncounterStableId = encounterStableId,
                        RequestingActorStableId = actorStableId,
                        ExpectedBattleWorldContextHashSha256 = battlefield
                            ? preview.BattlefieldDerivation.WorldContext.ContextHashSha256
                            : preview.LocalWorldContext.ContextHashSha256,
                        ExpectedBattlefieldDerivationInputHashSha256 = battlefield
                            ? preview.BattlefieldDerivation
                                .BattlefieldDerivationInputHashSha256 : string.Empty,
                    }, lifetime?.Token ?? CancellationToken.None));
            }
            catch (Exception error)
            {
                combat.SetAuthorityFailure(error.Message);
                WorldLocalBattleRequestFailed(encounterStableId, error.Message);
            }
            finally { commandInFlight = false; }
        }

        private async void OnLocalActionPrepared(LocalCombatActionCommandDraft request)
        {
            if (authority == null || current == null || commandInFlight) return;
            commandInFlight = true;
            try
            {
                Apply(await authority.ConfirmLocalActionAsync(sessionStableId,
                    current.BattleStableId, request,
                    lifetime?.Token ?? CancellationToken.None));
            }
            catch (Exception error) { combat.SetAuthorityFailure(error.Message); }
            finally { commandInFlight = false; }
        }

        private void Apply(BattleInstanceApiModel value)
        {
            current = value ?? throw new InvalidOperationException("BattleStateMissing");
            if (current.CombatSpaceCode == BattlePresentationCodes.WorldLocal)
            {
                if (current.PhaseCode != BattlePresentationCodes.Active)
                {
                    lhStreamingEngine?.ReleaseFocusPin();
                    combat.ApplyUnifiedBattleState(current, actorStableId);
                    player.EnterExplorationMode();
                    if (lastResolvedBattleStableId != current.BattleStableId)
                    {
                        lastResolvedBattleStableId = current.BattleStableId;
                        WorldLocalBattleResolved(current.EncounterStableId);
                    }
                    return;
                }
                if (lhStreamingEngine != null)
                {
                    var requested = current.LocalCombat.WorldContext.FocusL3CellKey;
                    var focus = 공간LHCellKey.TryParseL3(requested, out _, out _)
                        ? requested : lhStreamingEngine.PlayerCellKey;
                    if (공간LHCellKey.TryParseL3(focus, out _, out _))
                        lhStreamingEngine.PinFocusCell(focus);
                }
                combat.ApplyUnifiedBattleState(current, actorStableId);
                return;
            }
            lhStreamingEngine?.ReleaseFocusPin();
            combat.ClearUnifiedBattle();
            if (derivedBattlefieldRoot != null) Destroy(derivedBattlefieldRoot);
            derivedBattlefieldRoot = new 전장파생공간Assembler()
                .Build(current, battlefieldParent);
            player.EnterCombatMode(FarmCombatPresentationCodes.ThirdPersonAwareness);
        }

        private string NextCommandId(string kind) => "command:unity:unified-battle:"
            + kind + ":" + (++commandSequence).ToString();

        private async Task<string> WaitForSessionAsync(CancellationToken cancellationToken)
        {
            var started = Time.realtimeSinceStartup;
            while (string.IsNullOrWhiteSpace(shell?.SessionStableId)
                || shell.SessionStableId == SimulationWorldShellFixture.SessionStableId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - started > 10f)
                    throw new InvalidOperationException("LocalCombatServerSessionMissing");
                await Task.Yield();
            }
            return shell.SessionStableId;
        }

        private void OnDestroy()
        {
            if (combat != null)
            {
                combat.CombatEntryRequested -= OnCombatEntryRequested;
                combat.LocalActionCommandPrepared -= OnLocalActionPrepared;
            }
            lifetime?.Cancel();
            lifetime?.Dispose();
            lhStreamingEngine?.ReleaseFocusPin();
        }
    }
}
