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
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class 턴마감SceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 턴마감Presenter presenter = null!;
        [SerializeField] private bool 서버기준사용 = true;

        private CancellationTokenSource? lifetimeCancellation;

        public bool 서버기준사용중 => 서버기준사용;

        public void Configure(
            UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            턴마감Presenter turnClosingPresenter,
            bool useServerAuthority)
        {
            runtimeSettings = settings;
            shell = worldShell;
            presenter = turnClosingPresenter;
            서버기준사용 = useServerAuthority;
        }

        private void Awake()
        {
            if (presenter == null)
                throw new InvalidOperationException("턴 마감 Presenter가 연결되지 않았습니다.");
            presenter.Set자동시작(false);
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
                presenter?.SetContextVisible(false);
                Debug.LogException(exception, this);
            }
        }

        public async Task InitializeAsync()
        {
            if (shell == null || presenter == null)
                throw new InvalidOperationException("턴 마감 Scene 연결이 완전하지 않습니다.");

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();

            if (!서버기준사용)
            {
                await presenter.InitializeAsync(new 턴마감FixtureAuthorityClient());
                return;
            }

            if (runtimeSettings == null)
                throw new InvalidOperationException("Unity 서버 연결 설정이 없습니다.");
            var options = runtimeSettings.ToOptions();
            var apiClient = new SimulationRehearsalUnityWebRequestApiClient(options);
            var repository = new 턴마감ServerAuthorityRepository(apiClient);
            var serverSession = await repository.서버기준Session확보Async(
                lifetimeCancellation.Token);
            shell.ApplyAuthoritativeSnapshot(serverSession.WorldSnapshot);
            await presenter.InitializeAsync(repository);
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
