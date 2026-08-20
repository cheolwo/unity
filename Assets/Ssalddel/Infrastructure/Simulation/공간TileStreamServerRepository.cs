using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 공간TileStreamServerRepository : I공간TileStreamRepository,
        I공간TileLandscapeCompositionRepository,
        I공간AreaSetLandscapeGraphRepository,
        I실제E5AreaSetNetworkRepository,
        I공간LHWorldRepository
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

        public async Task<공간LandscapeCompositionTileData> LoadLandscapeCompositionsAsync(
            string tileKey, CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간LandscapeCompositionTileData>(
                "tiles/" + Uri.EscapeDataString(tileKey) + "/landscape-compositions",
                cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<공간AreaSetDefinitionData> LoadAreaSetAsync(
            string areaSetStableId,
            CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간AreaSetDefinitionData>(
                "area-sets/" + Uri.EscapeDataString(areaSetStableId), cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<공간LandscapeGraphIndexData> LoadGraphIndexAsync(
            string areaSetStableId,
            string centerTileKey,
            int radiusTiles,
            CancellationToken cancellationToken)
        {
            if (!공간AreaSetLandscapeGraphCodes.IsSupportedTileRef(centerTileKey)
                || radiusTiles < 0 || radiusTiles > 12)
                throw new InvalidOperationException("WorldLandscapeGraphIndexRequestInvalid");
            var value = await GetAsync<공간LandscapeGraphIndexData>(
                "area-sets/" + Uri.EscapeDataString(areaSetStableId)
                + "/landscape-graphs?tileKey=" + Uri.EscapeDataString(centerTileKey)
                + "&radiusTiles=" + radiusTiles,
                cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<공간LandscapeGraphData> LoadGraphAsync(
            string landscapeGraphStableId,
            CancellationToken cancellationToken)
        {
            var value = await GetAsync<공간LandscapeGraphData>(
                "landscape-graphs/" + Uri.EscapeDataString(landscapeGraphStableId),
                cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<실제E5AreaSetNetworkData> LoadAreaSetNetworkAsync(
            string networkStableId,
            CancellationToken cancellationToken)
        {
            var value = await GetAsync<실제E5AreaSetNetworkData>(
                "area-set-networks/" + Uri.EscapeDataString(networkStableId),
                cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<실제E5InteractionReadinessData> LoadInteractionReadinessAsync(
            string networkStableId,
            CancellationToken cancellationToken)
        {
            var value = await GetAsync<실제E5InteractionReadinessData>(
                "area-set-networks/" + Uri.EscapeDataString(networkStableId)
                + "/interaction-readiness", cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<공간TileArtifactPayloadData> LoadArtifactContentAsync(
            string tileKey,
            string layerCode,
            CancellationToken cancellationToken)
        {
            var descriptor = await GetAsync<공간TileStreamLayerData>(
                "tiles/" + Uri.EscapeDataString(tileKey)
                + "/artifacts/" + Uri.EscapeDataString(layerCode), cancellationToken);
            descriptor.Validate();
            if (descriptor.StatusCode != 공간TileStreamingCodes.Available)
                throw new InvalidOperationException("WorldTileStreamArtifactNotFound");
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = descriptor.ArtifactContentPath.TrimStart('/'),
                RequiresAuthentication = false,
                ExpectsBinaryResponse = true,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException(
                    "WorldTileStreamArtifactContentFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            var value = new 공간TileArtifactPayloadData
            {
                TileKey = tileKey,
                LayerCode = layerCode,
                ArtifactHashSha256 = descriptor.ArtifactHashSha256,
                ArtifactFormatCode = descriptor.ArtifactFormatCode,
                SampleWidth = descriptor.SampleWidth.GetValueOrDefault(),
                SampleHeight = descriptor.SampleHeight.GetValueOrDefault(),
                Bytes = response.BodyBytes,
            };
            value.Validate();
            return value;
        }

        public async Task<공간LHCellPreviewData> PreviewCellsAsync(
            공간LHCellPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "POST",
                RelativePath = BaseRoute + "lh/cells/preview",
                JsonBody = JsonConvert.SerializeObject(request),
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException(
                    "LHWorldCellPreviewRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            var value = JsonConvert.DeserializeObject<공간LHCellPreviewData>(response.Body)
                ?? throw new InvalidOperationException("LHWorldCellPreviewJsonInvalid");
            value.Validate(request.RequestEpoch);
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
