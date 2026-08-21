using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ssalddel.Unity.Bootstrap
{
    public enum 정착지상호작용AuthorityMode
    {
        ReviewFixture = 0,
        Server = 1,
    }

    [DefaultExecutionOrder(-925)]
    [DisallowMultipleComponent]
    public sealed class 정착지상호작용SceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 정착지상호작용Presenter presenter = null!;
        [FormerlySerializedAs("서버기준사용")]
        [SerializeField] private 정착지상호작용AuthorityMode authorityMode =
            정착지상호작용AuthorityMode.Server;

        private CancellationTokenSource? lifetimeCancellation;

        public bool 서버기준사용중 => authorityMode ==
            정착지상호작용AuthorityMode.Server;
        public 정착지상호작용AuthorityMode AuthorityMode => authorityMode;

        public void Configure(
            UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            정착지상호작용Presenter interactionPresenter,
            정착지상호작용AuthorityMode mode)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = interactionPresenter;
            authorityMode = mode;
        }

        private void Awake()
        {
            if (presenter == null)
                throw new InvalidOperationException("정착지 상호작용 Presenter가 연결되지 않았습니다.");
            presenter.Set자동Fixture시작(false);
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public async Task InitializeAsync()
        {
            if (shell == null || presenter == null)
                throw new InvalidOperationException("정착지 상호작용 Scene 연결이 완전하지 않습니다.");

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();

            if (authorityMode == 정착지상호작용AuthorityMode.ReviewFixture)
            {
                var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
                presenter.InitializeAuthority(
                    new 정착지상호작용FixtureAuthorityClient(initial), initial);
                return;
            }
            if (authorityMode != 정착지상호작용AuthorityMode.Server)
                throw new InvalidOperationException(
                    "SettlementInteractionAuthorityModeInvalid");

            if (runtimeSettings == null)
                throw new InvalidOperationException("Unity 서버 연결 설정이 없습니다.");
            var sessionStableId = await WaitForAuthoritativeSessionAsync(
                lifetimeCancellation.Token);
            var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            var repository = new 정착지상호작용AuthorityRepository(apiClient);
            var snapshot = await repository.RefreshAsync(
                sessionStableId, lifetimeCancellation.Token);
            var context = await repository.GetFarmChoiceContextAsync(
                sessionStableId, lifetimeCancellation.Token);
            presenter.InitializeFarm선택Coordinator(new Farm선택Coordinator(
                repository, repository, snapshot, context));
        }

        private async Task<string> WaitForAuthoritativeSessionAsync(
            CancellationToken cancellationToken)
        {
            var startedAt = Time.realtimeSinceStartup;
            while (string.IsNullOrWhiteSpace(shell.SessionStableId)
                   || shell.SessionStableId == SimulationWorldShellFixture.SessionStableId)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - startedAt > 10f)
                    throw new InvalidOperationException("SimulationFarmChoiceServerSessionMissing");
                await Task.Yield();
            }
            return shell.SessionStableId;
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
