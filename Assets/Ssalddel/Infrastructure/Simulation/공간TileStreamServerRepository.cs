using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 공간TileStreamServerRepository : I공간TileStreamRepository
    {
        private const string BaseRoute = "api/simulation/v1/world-stream/";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 공간TileStreamServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public string SourceModeCode => 공간TileStreamingCodes.SimulationServer;

        public async Task<공간TileStreamRecipeData> LoadRecipeAsync(
            string recipeStableId, CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간TileStreamRecipeData>(
                "recipes/" + Uri.EscapeDataString(recipeStableId), cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<공간TileStreamManifestData> LoadManifestAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간TileStreamManifestData>(
                "tiles/" + Uri.EscapeDataString(tileKey) + "/manifest", cancellationToken);
            value.Validate();
            return value;
        }

        public Task<공간TileActivityData> LoadActivitiesAsync(
            string tileKey, CancellationToken cancellationToken)
            => GetAsync<공간TileActivityData>(
                "tiles/" + Uri.EscapeDataString(tileKey) + "/activities", cancellationToken);

        public async Task<공간TileObjectProjectionData> LoadObjectsAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간TileObjectProjectionData>(
                "tiles/" + Uri.EscapeDataString(tileKey) + "/objects", cancellationToken);
            value.Validate();
            return value;
        }

        private async Task<T> GetAsync<T>(string suffix, CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = BaseRoute + suffix,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException(
                    "WorldTileStreamRequestFailed:" + response.StatusCode + ":" + response.ErrorCode);
            return JsonConvert.DeserializeObject<T>(response.Body)
                ?? throw new InvalidOperationException("WorldTileStreamJsonInvalid");
        }

    }
}
