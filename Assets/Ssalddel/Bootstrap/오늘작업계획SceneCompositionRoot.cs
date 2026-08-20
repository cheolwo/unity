using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class 오늘작업계획SceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private 오늘작업계획Presenter presenter = null!;
        [SerializeField] private bool 서버기준사용 = true;

        private CancellationTokenSource? lifetimeCancellation;
        public bool 서버기준사용중 => 서버기준사용;

        public void Configure(
            UnityClientRuntimeSettings settings,
            오늘작업계획Presenter dailyWorkPresenter,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            presenter = dailyWorkPresenter;
            서버기준사용 = useServerAuthority;
        }

        private async void Start()
        {
            try { await InitializeAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                presenter?.SetAuthorityFailure(exception);
                Debug.LogException(exception, this);
            }
        }

        public async Task InitializeAsync()
        {
            if (presenter == null || runtimeSettings == null)
                throw new InvalidOperationException("DailyWorkPlanSceneWiringInvalid");
            if (!서버기준사용)
                throw new InvalidOperationException("DailyWorkPlanServerAuthorityRequired");

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();
            var client = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            var repository = new 오늘작업계획ServerRepository(client);
            Exception? last = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    await presenter.InitializeAsync(repository,
                        턴마감ServerAuthorityRepository.BootstrapSessionStableId,
                        0);
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    last = exception;
                    await Task.Delay(250, lifetimeCancellation.Token);
                }
            }
            throw new InvalidOperationException("DailyWorkPlanSessionBootstrapTimeout", last);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }
    }
}
