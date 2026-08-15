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
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class 진부Hub입고UiSceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 진부Hub입고UiPresenter presenter = null!;
        [SerializeField] private bool 서버기준사용 = true;

        private CancellationTokenSource lifetimeCancellation;

        public bool 서버기준사용중 => 서버기준사용;

        public void Configure(
            UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            진부Hub입고UiPresenter inboundPresenter,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = inboundPresenter;
            서버기준사용 = useServerAuthority;
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
                throw new InvalidOperationException("JinbuInboundUiSceneWiringMissing");

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();

            if (!서버기준사용)
            {
                await presenter.InitializeAsync(
                    new 진부Hub입고UiFixtureAuthorityClient(),
                    SimulationWorldShellFixture.SessionStableId);
                return;
            }

            if (runtimeSettings == null)
                throw new InvalidOperationException("UnitySimulationServerSettingsMissing");
            var sessionStableId = await WaitForAuthoritativeSessionAsync(
                lifetimeCancellation.Token);
            var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            await presenter.InitializeAsync(
                new 진부Hub입고UiServerRepository(apiClient),
                sessionStableId);
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
                    throw new InvalidOperationException("SimulationInboundUiServerSessionMissing");
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
