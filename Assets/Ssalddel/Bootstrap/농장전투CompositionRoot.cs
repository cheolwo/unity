using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// Simulation 서버의 전투 상태와 1인칭 입력 표현을 연결합니다. 같은 명령은 같은
    /// CommandId로 한 번만 재시도하고 개정 충돌에서는 상태만 다시 읽습니다.
    /// </summary>
    [DefaultExecutionOrder(-850)]
    [DisallowMultipleComponent]
    public sealed class 농장전투CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 전투시점Controller combat = null!;
        [SerializeField] private string actorStableId =
            "actor:sim:player-survivor";
        [SerializeField] private bool 서버기준사용 = true;

        private CancellationTokenSource? lifetimeCancellation;
        private ISimulationFarmCombatAuthorityClient? authority;
        private FarmCombatStateApiModel? current;
        private string sessionStableId = string.Empty;
        private bool commandInFlight;
        private int commandSequence;
        private 전투시점Controller? boundCombat;

        public FarmCombatStateApiModel? CurrentState => current;
        public bool CommandInFlight => commandInFlight;
        public bool ServerAuthorityEnabled => 서버기준사용;
        public string ActorStableId => actorStableId;

        public bool ValidateWiring()
            => runtimeSettings != null
                && shell != null
                && player != null
                && combat != null
                && combat.ValidateWiring()
                && !string.IsNullOrWhiteSpace(actorStableId);

        public void Configure(
            UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            플레이어경관Controller playerController,
            전투시점Controller combatController,
            string actorId,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            player = playerController;
            combat = combatController;
            actorStableId = actorId ?? string.Empty;
            서버기준사용 = useServerAuthority;
            BindCombat(combatController);
            if (!ValidateWiring())
                throw new ArgumentException("FarmCombatCompositionWiringInvalid");
        }

        private void Awake()
        {
            player ??= FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            combat ??= FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            BindCombat(combat);
        }

        private void BindCombat(전투시점Controller value)
        {
            if (boundCombat == value) return;
            if (boundCombat != null)
            {
                boundCombat.CombatEntryRequested -= OnCombatEntryRequested;
                boundCombat.ReactionCommandPrepared -= OnReactionCommandPrepared;
            }
            boundCombat = value;
            if (boundCombat == null) return;
            boundCombat.CombatEntryRequested += OnCombatEntryRequested;
            boundCombat.ReactionCommandPrepared += OnReactionCommandPrepared;
        }

        private async void Start()
        {
            if (!서버기준사용) return;
            try
            {
                if (runtimeSettings == null)
                    throw new InvalidOperationException(
                        "FarmCombatServerSettingsMissing");
                lifetimeCancellation = new CancellationTokenSource();
                var session = await WaitForAuthoritativeSessionAsync(
                    lifetimeCancellation.Token);
                var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                    runtimeSettings.ToOptions());
                await InitializeAsync(
                    new SimulationFarmCombatServerRepository(apiClient),
                    session,
                    actorStableId,
                    lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                combat?.SetAuthorityFailure(error.Message);
                Debug.LogError("FarmCombatInitializationFailed:" + error.Message);
            }
        }

        public async Task InitializeAsync(
            ISimulationFarmCombatAuthorityClient client,
            string session,
            string actorId,
            CancellationToken cancellationToken)
        {
            if (client == null || string.IsNullOrWhiteSpace(session)
                || string.IsNullOrWhiteSpace(actorId) || combat == null
                || player == null)
                throw new InvalidOperationException("FarmCombatCompositionWiringMissing");
            authority = client;
            sessionStableId = session.Trim();
            actorStableId = actorId.Trim();
            Apply(await authority.LoadAsync(sessionStableId, cancellationToken));
        }

        public async Task StartCombatAsync(
            string encounterStableId,
            CancellationToken cancellationToken)
        {
            EnsureReady();
            if (commandInFlight) return;
            commandInFlight = true;
            try
            {
                player.EnterCombatMode(
                    FarmCombatPresentationCodes.FirstPersonPrecision);
                await WaitForCameraTransitionAsync(cancellationToken);

                if (!(current!.Perspectives
                    ?? Array.Empty<FarmCombatPerspectiveApiModel>())
                    .Any(value => value.ActorStableId == actorStableId
                        && value.PerspectiveCode
                            == FarmCombatPresentationCodes.FirstPersonPrecision))
                {
                    var perspective = FarmCombatInputCommandFactory.CreatePerspective(
                        current, actorStableId,
                        FarmCombatPresentationCodes.FirstPersonPrecision,
                        NextCommandId("perspective"));
                    Apply(await ExecuteWithSingleRetryAsync(
                        token => authority!.ConfirmPerspectiveAsync(
                            sessionStableId, perspective, token),
                        cancellationToken));
                }

                var start = FarmCombatInputCommandFactory.CreateBeatStart(
                    current!, actorStableId, encounterStableId,
                    NextCommandId("beat-start"));
                Apply(await ExecuteWithSingleRetryAsync(
                    token => authority!.StartBeatAsync(
                        sessionStableId, start, token),
                    cancellationToken));
            }
            catch (SimulationFarmCombatRequestException error)
                when (error.IsRevisionConflict)
            {
                await RefreshAfterFailureAsync(error.ErrorCode, cancellationToken);
            }
            catch (Exception error)
            {
                await RefreshAfterFailureAsync(error.Message, cancellationToken);
            }
            finally
            {
                commandInFlight = false;
            }
        }

        public async Task SubmitReactionAsync(
            FarmCombatReactionCommandDraft request,
            CancellationToken cancellationToken)
        {
            EnsureReady();
            if (commandInFlight) return;
            commandInFlight = true;
            try
            {
                Apply(await ExecuteWithSingleRetryAsync(
                    token => authority!.ConfirmReactionAsync(
                        sessionStableId, request, token),
                    cancellationToken));
            }
            catch (SimulationFarmCombatRequestException error)
                when (error.IsRevisionConflict)
            {
                await RefreshAfterFailureAsync(error.ErrorCode, cancellationToken);
            }
            catch (Exception error)
            {
                await RefreshAfterFailureAsync(error.Message, cancellationToken);
            }
            finally
            {
                commandInFlight = false;
            }
        }

        private async Task<FarmCombatStateApiModel> ExecuteWithSingleRetryAsync(
            Func<CancellationToken, Task<FarmCombatStateApiModel>> command,
            CancellationToken cancellationToken)
        {
            try
            {
                return await command(cancellationToken);
            }
            catch (SimulationFarmCombatRequestException error)
                when (error.IsRevisionConflict)
            {
                throw;
            }
            catch
            {
                return await command(cancellationToken);
            }
        }

        private async Task RefreshAfterFailureAsync(
            string errorCode,
            CancellationToken cancellationToken)
        {
            combat.SetAuthorityFailure(errorCode);
            if (authority == null) return;
            try
            {
                Apply(await authority.LoadAsync(
                    sessionStableId, cancellationToken));
            }
            catch
            {
                // 마지막 서버 상태를 보존하고 실패 표시를 유지한다.
            }
        }

        private void Apply(FarmCombatStateApiModel state)
        {
            current = state ?? throw new InvalidOperationException(
                "FarmCombatStateMissing");
            combat.ApplyServerState(current, actorStableId);
        }

        private async Task WaitForCameraTransitionAsync(
            CancellationToken cancellationToken)
        {
            while (player.IsCameraTransitioning)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private async Task<string> WaitForAuthoritativeSessionAsync(
            CancellationToken cancellationToken)
        {
            var startedAt = Time.realtimeSinceStartup;
            while (shell == null || string.IsNullOrWhiteSpace(shell.SessionStableId)
                   || shell.SessionStableId
                       == SimulationWorldShellFixture.SessionStableId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - startedAt > 10f)
                    throw new InvalidOperationException(
                        "FarmCombatServerSessionMissing");
                await Task.Yield();
            }
            return shell.SessionStableId;
        }

        private string NextCommandId(string kind)
            => "command:unity:farm-combat:" + kind + ":"
                + (++commandSequence).ToString();

        private void EnsureReady()
        {
            if (authority == null || current == null
                || string.IsNullOrWhiteSpace(sessionStableId)
                || string.IsNullOrWhiteSpace(actorStableId))
                throw new InvalidOperationException("FarmCombatAuthorityNotReady");
        }

        private async void OnCombatEntryRequested(string encounterStableId)
        {
            if (!서버기준사용) return;
            try
            {
                await StartCombatAsync(encounterStableId,
                    lifetimeCancellation?.Token ?? CancellationToken.None);
            }
            catch (Exception error)
            {
                combat.SetAuthorityFailure(error.Message);
            }
        }

        private async void OnReactionCommandPrepared(
            FarmCombatReactionCommandDraft request)
        {
            try
            {
                await SubmitReactionAsync(request,
                    lifetimeCancellation?.Token ?? CancellationToken.None);
            }
            catch (Exception error)
            {
                combat.SetAuthorityFailure(error.Message);
            }
        }

        private void OnDestroy()
        {
            if (boundCombat != null)
            {
                boundCombat.CombatEntryRequested -= OnCombatEntryRequested;
                boundCombat.ReactionCommandPrepared -= OnReactionCommandPrepared;
                boundCombat = null;
            }
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
