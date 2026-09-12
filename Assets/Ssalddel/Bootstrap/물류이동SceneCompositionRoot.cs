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
    public enum 물류이동AuthorityMode
    {
        ReviewFixture = 0,
        Server = 1,
    }

    [DefaultExecutionOrder(-910)]
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class 물류이동SceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 물류이동Presenter presenter = null!;
        [SerializeField] private 물류이동AuthorityMode authorityMode =
            물류이동AuthorityMode.Server;

        private CancellationTokenSource lifetimeCancellation;

        public bool 서버기준사용중 => authorityMode == 물류이동AuthorityMode.Server;
        public 물류이동AuthorityMode AuthorityMode => authorityMode;

        public void Configure(
            UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            물류이동Presenter movementPresenter,
            물류이동AuthorityMode mode)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = movementPresenter;
            authorityMode = mode;
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
            if (SimulationWorldLocalRuntimeScope.RestaurantObserverRequested) return;
            if (shell == null || presenter == null)
                throw new InvalidOperationException("LogisticsMovementSceneWiringMissing");

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();

            if (authorityMode == 물류이동AuthorityMode.ReviewFixture)
            {
                var initial = 물류이동FixtureAuthorityClient.CreateInitialSnapshot();
                presenter.InitializeAuthority(
                    new 물류이동FixtureAuthorityClient(initial), initial);
                return;
            }
            if (authorityMode != 물류이동AuthorityMode.Server)
                throw new InvalidOperationException("LogisticsMovementAuthorityModeInvalid");
            if (SimulationWorldLocalRuntimeScope.IsInstalled)
            {
                if (!SimulationWorldLocalRuntimeScope.TryGet(out var localScope))
                    throw new InvalidOperationException(
                        "SimulationLocalRuntimeScopeMissing");
                var localRepository = new LocalLogisticsMovementAuthorityRepository(
                    localScope.Runtime);
                var localSnapshot = await localRepository.RefreshAsync(
                    localScope.SessionStableId, lifetimeCancellation.Token);
                presenter.InitializeAuthority(localRepository, localSnapshot);
                Debug.Log(
                    "LogisticsMovementLocalSimulationRuntimeReady:network=0", this);
                return;
            }
            if (runtimeSettings == null)
                throw new InvalidOperationException("UnitySimulationServerSettingsMissing");

            var sessionStableId = await WaitForAuthoritativeSessionAsync(
                lifetimeCancellation.Token);
            var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            var repository = new 물류이동AuthorityRepository(apiClient);
            var initialSnapshot = await repository.RefreshAsync(
                sessionStableId, lifetimeCancellation.Token);
            presenter.InitializeAuthority(repository, initialSnapshot);
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
                    throw new InvalidOperationException(
                        "SimulationLogisticsMovementServerSessionMissing");
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
