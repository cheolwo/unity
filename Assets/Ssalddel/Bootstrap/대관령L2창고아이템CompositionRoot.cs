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
    [DefaultExecutionOrder(-880)]
    [DisallowMultipleComponent]
    public sealed class 대관령L2창고아이템CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 대관령L2창고아이템Presenter presenter = null!;
        [SerializeField] private bool 서버기준사용 = true;

        private CancellationTokenSource lifetimeCancellation;
        private bool initializing;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            대관령L2창고아이템Presenter inventoryPresenter,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = inventoryPresenter;
            서버기준사용 = useServerAuthority;
        }

        public async Task InitializeAsync()
        {
            if (presenter != null && presenter.IsReady) return;
            if (initializing)
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryInitializationInProgress");
            if (shell == null || presenter == null)
                throw new InvalidOperationException(
                    "DaegwallyeongInventorySceneWiringMissing");
            initializing = true;
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();

            try
            {
                if (!서버기준사용)
                {
                    await presenter.InitializeAsync(
                        new 대관령L2창고아이템FixtureAuthorityClient(),
                        SimulationWorldShellFixture.SessionStableId);
                    return;
                }

                if (runtimeSettings == null)
                    throw new InvalidOperationException(
                        "UnitySimulationServerSettingsMissing");
                var sessionStableId = await WaitForAuthoritativeSessionAsync(
                    lifetimeCancellation.Token);
                var apiClient = new SimulationRehearsalUnityWebRequestApiClient(
                    runtimeSettings.ToOptions());
                await presenter.InitializeAsync(
                    new 대관령L2창고아이템ServerRepository(apiClient),
                    sessionStableId);
            }
            finally
            {
                initializing = false;
            }
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception error)
            {
                Debug.LogError("DaegwallyeongInventoryInitializationFailed:"
                               + error.Message);
            }
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
                        "DaegwallyeongInventoryServerSessionMissing");
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
