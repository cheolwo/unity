using System;
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
        [SerializeField] private bool 서버기준사용;

        public bool 서버기준사용중 => 서버기준사용;

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
        {
            controller = streamingController;
            objectController = visibilityObjectController;
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
            if (!서버기준사용)
                repository = new 대관령Farm공간TileStreamFixtureRepository();
            else
            {
                if (runtimeSettings == null)
                    throw new InvalidOperationException("UnitySimulationServerSettingsMissing");
                var client = new SimulationRehearsalUnityWebRequestApiClient(runtimeSettings.ToOptions());
                repository = new 공간TileStreamServerRepository(client);
            }

            await controller.InitializeAsync(repository);
            if (objectController != null)
                await objectController.InitializeAsync(repository);
        }
    }
}
