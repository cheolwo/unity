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
    [DefaultExecutionOrder(-850)]
    [DisallowMultipleComponent]
    public sealed class 공간TileStreamingCompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private 공간TileStreamingController controller = null!;
        [SerializeField] private 공간시야ObjectStreamingController objectController = null!;
        [SerializeField] private 공간LHStreamingEngine lhStreamingEngine = null!;
        [SerializeField] private 실제E5AreaSetNetworkController 실제E5NetworkController = null!;
        [SerializeField] private bool 서버기준사용;
        [SerializeField] private string 로컬월드시드 = 공간LHWorldCodes.WorldSeed;
        [SerializeField, Min(1)] private int 로컬시작일 = 1;

        public bool 서버기준사용중 => 서버기준사용;
        public bool 실제E5Network연결됨 => 실제E5NetworkController != null;
        public string 로컬월드시드값 => 로컬월드시드;
        public int 로컬시작일값 => 로컬시작일;

        public void BindActualE5Network(
            실제E5AreaSetNetworkController actualE5NetworkController,
            UnityClientRuntimeSettings settings)
        {
            실제E5NetworkController = actualE5NetworkController;
            if (settings != null) runtimeSettings = settings;
        }

        public void Configure(
            공간TileStreamingController streamingController,
            UnityClientRuntimeSettings settings,
            bool useSimulationServer)
            => Configure(streamingController, null, settings, useSimulationServer);

        public void Configure(
            공간TileStreamingController streamingController,
            공간시야ObjectStreamingController visibilityObjectController,
            UnityClientRuntimeSettings settings,
            bool useSimulationServer)
            => Configure(streamingController, visibilityObjectController, null,
                settings, useSimulationServer);

        public void Configure(
            공간TileStreamingController streamingController,
            공간시야ObjectStreamingController visibilityObjectController,
            공간LHStreamingEngine lhWorldStreamingEngine,
            UnityClientRuntimeSettings settings,
            bool useSimulationServer)
            => Configure(streamingController, visibilityObjectController,
                lhWorldStreamingEngine, null, settings, useSimulationServer);

        public void Configure(
            공간TileStreamingController streamingController,
            공간시야ObjectStreamingController visibilityObjectController,
            공간LHStreamingEngine lhWorldStreamingEngine,
            실제E5AreaSetNetworkController actualE5NetworkController,
            UnityClientRuntimeSettings settings,
            bool useSimulationServer)
        {
            controller = streamingController;
            objectController = visibilityObjectController;
            lhStreamingEngine = lhWorldStreamingEngine;
            실제E5NetworkController = actualE5NetworkController;
            runtimeSettings = settings;
            서버기준사용 = useSimulationServer;
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public async Task InitializeAsync()
        {
            if (controller == null)
                throw new InvalidOperationException("WorldTileStreamingControllerMissing");
            I공간TileStreamRepository repository;
            I공간LHWorldRepository lhRepository;
            I실제E5AreaSetNetworkRepository actualE5NetworkRepository = null;
            I공간AreaSetLandscapeGraphRepository graphRepository = null;
            if (!서버기준사용)
            {
                repository = new 대관령Farm공간TileStreamFixtureRepository();
                lhRepository = new 로컬공간LHWorldEngine(로컬월드시드, 로컬시작일);
            }
            else
            {
                if (runtimeSettings == null)
                    throw new InvalidOperationException("UnitySimulationServerSettingsMissing");
                var client = new SimulationRehearsalUnityWebRequestApiClient(runtimeSettings.ToOptions());
                var serverRepository = new 공간TileStreamServerRepository(client);
                repository = serverRepository;
                lhRepository = serverRepository;
                actualE5NetworkRepository = serverRepository;
                graphRepository = serverRepository;
            }

            await controller.InitializeAsync(repository);
            if (objectController != null)
                await objectController.InitializeAsync(repository);
            if (lhStreamingEngine != null)
            {
                var shell = FindFirstObjectByType<SimulationWorldShellPresenter>(
                    FindObjectsInactive.Include);
                lhStreamingEngine.ConfigureAuthority(shell);
                if (서버기준사용)
                    await WaitForAuthoritativeSessionAsync(shell, CancellationToken.None);
                await lhStreamingEngine.InitializeAsync(lhRepository);
            }
            if (실제E5NetworkController != null)
            {
                if ((actualE5NetworkRepository == null || graphRepository == null)
                    && runtimeSettings != null)
                {
                    var actualE5Client = new SimulationRehearsalUnityWebRequestApiClient(
                        runtimeSettings.ToOptions());
                    var actualE5ServerRepository = new 공간TileStreamServerRepository(
                        actualE5Client);
                    actualE5NetworkRepository = actualE5ServerRepository;
                    graphRepository = actualE5ServerRepository;
                }
                if (actualE5NetworkRepository == null || graphRepository == null)
                {
                    실제E5NetworkController.ShowUnavailable(
                        "실제 E5 서버 설정이 없어 공간 결속을 대기합니다");
                    return;
                }
                try
                {
                    await 실제E5NetworkController.InitializeAsync(
                        actualE5NetworkRepository, graphRepository);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    실제E5NetworkController.ShowUnavailable(
                        "실제 E5 서버 응답 대기 · 로컬 타일 플레이는 유지됩니다");
                }
            }
        }

        private static async Task WaitForAuthoritativeSessionAsync(
            SimulationWorldShellPresenter shell,
            CancellationToken cancellationToken)
        {
            if (shell == null)
                throw new InvalidOperationException("LHWorldAuthoritativeShellMissing");
            var startedAt = Time.realtimeSinceStartup;
            while (string.IsNullOrWhiteSpace(shell.SessionStableId)
                   || shell.SessionStableId == SimulationWorldShellFixture.SessionStableId
                   || shell.WorldRevision < 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup - startedAt > 10f)
                    throw new InvalidOperationException("LHWorldAuthoritativeSessionMissing");
                await Task.Yield();
            }
        }
    }
}
