using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 서버의 네이처 조우 상태 사본을 경관 표현에 연결합니다.
    /// 접근은 전투 Preview 요청의 계기일 뿐 전투 생성이나 승리를 확정하지 않습니다.
    /// </summary>
    [DefaultExecutionOrder(-840)]
    [DisallowMultipleComponent]
    public sealed class 네이처탐험조우CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 네이처조우Presenter presenter = null!;
        [SerializeField] private 전투시점Controller combat = null!;
        [SerializeField] private 현장전투CompositionRoot battle = null!;
        [SerializeField, Min(.25f)] private float refreshIntervalSeconds = 1f;
        [SerializeField] private bool 서버기준사용 = true;

        private I네이처탐험조우AuthorityClient? authority;
        private CancellationTokenSource? lifetime;
        private string sessionStableId = string.Empty;
        private float refreshElapsed;
        private bool requestInFlight;

        public bool ServerAuthorityEnabled => 서버기준사용;
        public bool RequestInFlight => requestInFlight;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell, 네이처조우Presenter view,
            전투시점Controller combatController, 현장전투CompositionRoot battleRoot,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = view;
            combat = combatController;
            battle = battleRoot;
            서버기준사용 = useServerAuthority;
            Bind();
            if (!ValidateWiring())
                throw new ArgumentException("NatureExplorationCompositionWiringInvalid");
        }

        public bool ValidateWiring()
            => runtimeSettings != null && shell != null && presenter != null
                && presenter.ValidateWiring() && combat != null && battle != null
                && battle.ValidateWiring();

        private void Awake()
        {
            shell ??= FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            presenter ??= FindFirstObjectByType<네이처조우Presenter>(
                FindObjectsInactive.Include);
            combat ??= FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            battle ??= FindFirstObjectByType<현장전투CompositionRoot>(
                FindObjectsInactive.Include);
            Bind();
        }

        private void Bind()
        {
            if (presenter != null)
            {
                presenter.EncounterResponseRequested -= OnEncounterResponseRequested;
                presenter.EncounterResponseRequested += OnEncounterResponseRequested;
            }
            if (battle != null)
            {
                battle.WorldLocalBattleResolved -= OnBattleResolved;
                battle.WorldLocalBattleResolved += OnBattleResolved;
                battle.WorldLocalBattleRequestFailed -= OnBattleRequestFailed;
                battle.WorldLocalBattleRequestFailed += OnBattleRequestFailed;
            }
        }

        private async void Start()
        {
            if (!서버기준사용) return;
            try
            {
                lifetime = new CancellationTokenSource();
                sessionStableId = await WaitForSessionAsync(lifetime.Token);
                authority = new 네이처탐험조우ServerRepository(
                    new SimulationRehearsalUnityWebRequestApiClient(
                        runtimeSettings.ToOptions()));
                await RefreshAsync(lifetime.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogError("NatureExplorationEncounterInitializationFailed:"
                    + error.Message);
            }
        }

        private async void Update()
        {
            if (!서버기준사용 || authority == null || requestInFlight) return;
            refreshElapsed += Time.unscaledDeltaTime;
            if (refreshElapsed < refreshIntervalSeconds) return;
            refreshElapsed = 0f;
            try
            {
                await RefreshAsync(lifetime?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogWarning("NatureExplorationEncounterRefreshFailed:"
                    + error.Message);
            }
        }

        public async Task InitializeAsync(I네이처탐험조우AuthorityClient client,
            string session, CancellationToken cancellationToken)
        {
            authority = client ?? throw new ArgumentNullException(nameof(client));
            sessionStableId = !string.IsNullOrWhiteSpace(session)
                ? session.Trim()
                : throw new ArgumentException("NatureEncounterSessionMissing",
                    nameof(session));
            await RefreshAsync(cancellationToken);
        }

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            if (authority == null || string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("NatureEncounterAuthorityNotReady");
            if (requestInFlight) return;
            requestInFlight = true;
            try
            {
                presenter.Apply(await authority.LoadAsync(sessionStableId,
                    cancellationToken));
            }
            finally
            {
                requestInFlight = false;
            }
        }

        private void OnEncounterResponseRequested(string encounterStableId)
        {
            if (!battle.AuthorityReady
                || !combat.TryRequestLocalEncounter(encounterStableId))
            {
                presenter.AllowResponseRetry(encounterStableId,
                    battle.AuthorityReady ? "시점 전환 완료 대기" : "전투 서버 준비 대기");
                Debug.LogWarning("NatureEncounterResponseDeferred:"
                    + encounterStableId);
            }
        }

        private void OnBattleResolved(string encounterStableId)
            => presenter.MarkResolved(encounterStableId);

        private void OnBattleRequestFailed(string encounterStableId, string reasonCode)
            => presenter.AllowResponseRetry(encounterStableId, reasonCode);

        private async Task<string> WaitForSessionAsync(
            CancellationToken cancellationToken)
        {
            var startedAt = Time.realtimeSinceStartup;
            while (string.IsNullOrWhiteSpace(shell?.SessionStableId)
                || shell.SessionStableId == SimulationWorldShellFixture.SessionStableId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - startedAt > 10f)
                    throw new InvalidOperationException(
                        "NatureExplorationServerSessionMissing");
                await Task.Yield();
            }
            return shell.SessionStableId;
        }

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.EncounterResponseRequested -= OnEncounterResponseRequested;
            if (battle != null)
            {
                battle.WorldLocalBattleResolved -= OnBattleResolved;
                battle.WorldLocalBattleRequestFailed -= OnBattleRequestFailed;
            }
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
