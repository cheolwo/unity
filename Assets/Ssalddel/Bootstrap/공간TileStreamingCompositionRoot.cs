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
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
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
        [SerializeField] private bool 로컬노이즈지형사용;

        public bool 서버기준사용중 => 서버기준사용;
        public bool 실제E5Network연결됨 => 실제E5NetworkController != null;
        public string 로컬월드시드값 => 로컬월드시드;
        public int 로컬시작일값 => 로컬시작일;
        public bool 로컬노이즈지형사용중 => 로컬노이즈지형사용;
        public string 활성Tile자료원Code { get; private set; } = "NotInitialized";
        public string 활성LH자료원Code { get; private set; } = "NotInitialized";
        public bool Solo로컬공간파이프라인준비됨
            => 활성Tile자료원Code == 공간TileStreamingCodes.Fixture
               && 활성LH자료원Code != 공간TileStreamingCodes.SimulationServer;

        public void ConfigureLocalNoiseSurface(bool enabled)
            => 로컬노이즈지형사용 = enabled;

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
            if (SimulationWorldLocalRuntimeScope.RestaurantObserverRequested) return;
            if (controller == null)
                throw new InvalidOperationException("WorldTileStreamingControllerMissing");
            I공간TileStreamRepository repository;
            I공간LHWorldRepository lhRepository;
            I실제E5AreaSetNetworkRepository actualE5NetworkRepository = null;
            I공간AreaSetLandscapeGraphRepository graphRepository = null;
            // AfterSceneLoad 자동 조립과 MonoBehaviour.Start의 호출 순서는
            // Test Runner·도메인 재진입에서 같은 프레임 안에서도 달라질 수 있다.
            // Solo Runtime의 설치 기회를 먼저 보장한 뒤 Local/Remote Adapter를
            // 선택해야 로컬 플레이가 일시적으로 HTTP Repository로 새지 않는다.
            var localProcess = await WaitForLocalRuntimeBootstrapAsync();
            if (!서버기준사용 || localProcess)
            {
                repository = new 대관령Farm공간TileStreamFixtureRepository();
                var useNoiseSurface = 로컬노이즈지형사용
                                      || string.Equals(Environment.GetEnvironmentVariable(
                                              "SSALDDEL_LH_NOISE_SURFACE"), "1",
                                          StringComparison.Ordinal);
                I공간동적표면Provider surfaceProvider = useNoiseSurface
                    ? new 결정적ValueNoise공간표면Provider(로컬월드시드,
                        amplitude: 3.5d, wavelength: 110d, octaves: 3)
                    : new 평면공간동적표면Provider();
                var localLhEngine = new 로컬공간LHWorldEngine(로컬월드시드,
                    로컬시작일, localSurfaceProvider: surfaceProvider);
                if (localProcess
                    && SimulationWorldLocalRuntimeScope.TryGet(
                        out var localRuntimeScope)
                    && lhStreamingEngine != null)
                {
                    var nature = FindFirstObjectByType<Nature생존Controller>(
                        FindObjectsInactive.Include);
                    if (nature == null)
                        throw new InvalidOperationException(
                            "NatureWorldPlacementAnchorMissing");
                    var natureOrigin = nature.WorldAssetPlacementOrigin;
                    var ownerCellKey = lhStreamingEngine.CellKeyAtPosition(
                        natureOrigin.position);
                    var ownerCellOrigin = lhStreamingEngine
                        .CellOriginWorldPosition(ownerCellKey);
                    var ownerOffset = natureOrigin.position - ownerCellOrigin;
                    lhRepository = new Nature세계자산배치LHRepository(
                        localLhEngine, localRuntimeScope, ownerCellKey,
                        ownerOffset.x, ownerOffset.z);
                }
                else lhRepository = localLhEngine;
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

            활성Tile자료원Code = repository.SourceModeCode;
            활성LH자료원Code = lhRepository.SourceModeCode;

            await controller.InitializeAsync(repository);
            if (objectController != null)
                await objectController.InitializeAsync(repository);
            if (lhStreamingEngine != null)
            {
                var shell = FindFirstObjectByType<SimulationWorldShellPresenter>(
                    FindObjectsInactive.Include);
                lhStreamingEngine.ConfigureAuthority(shell);
                if (서버기준사용 && !localProcess)
                    await WaitForAuthoritativeSessionAsync(shell, CancellationToken.None);
                await lhStreamingEngine.InitializeAsync(lhRepository);
            }
            if (실제E5NetworkController != null)
            {
                if ((actualE5NetworkRepository == null || graphRepository == null)
                    && runtimeSettings != null && !localProcess)
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
                        localProcess
                            ? "Solo 로컬 공간은 유지 · 실제 E5 Network 이관 대기"
                            : "실제 E5 서버 설정이 없어 공간 결속을 대기합니다");
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

        private static async Task<bool> WaitForLocalRuntimeBootstrapAsync()
        {
            const int maximumFrames = 32;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (SimulationWorldLocalRuntimeScope.IsInstalled) return true;
                await Task.Yield();
            }
            return SimulationWorldLocalRuntimeScope.IsInstalled;
        }
    }
}
