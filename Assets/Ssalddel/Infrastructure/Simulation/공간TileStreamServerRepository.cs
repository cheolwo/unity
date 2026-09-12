using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 실제 HTTP 공간 요청이 발생했는지 런타임·시험 증거에서 판독하기 위한
    /// 과정 진단 계기입니다. Simulation 상태나 WorldRevision은 변경하지 않습니다.
    /// </summary>
    public static class 공간TileStreamRemoteRequestDiagnostics
    {
        private static int requestCount;

        public static int RequestCount => Volatile.Read(ref requestCount);

        public static void ResetForTests()
            => Interlocked.Exchange(ref requestCount, 0);

        internal static void RecordRequest()
            => Interlocked.Increment(ref requestCount);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 공간TileStreamServerRepository : I공간TileStreamRepository,
        I공간TileLandscapeCompositionRepository,
        I공간AreaSetLandscapeGraphRepository,
        I실제E5AreaSetNetworkRepository,
        IH5세계배치Repository,
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

        public async Task<H5세계배치DefinitionData> LoadWorldLayoutAsync(
            string worldLayoutStableId, CancellationToken cancellationToken)
        {
            var value = await GetAsync<H5세계배치DefinitionData>(
                "world-layouts/" + Uri.EscapeDataString(worldLayoutStableId), cancellationToken);
            value.Validate();
            return value;
        }

        public async Task<H5현실결속BindingData> LoadGroundingBindingAsync(
            string worldLayoutStableId, CancellationToken cancellationToken)
            => await GetAsync<H5현실결속BindingData>(
                "world-layouts/" + Uri.EscapeDataString(worldLayoutStableId)
                + "/grounding-binding", cancellationToken);

        public async Task<H5현실결속준비도Data> LoadGroundingReadinessAsync(
            string worldLayoutStableId, CancellationToken cancellationToken)
            => await GetAsync<H5현실결속준비도Data>(
                "world-layouts/" + Uri.EscapeDataString(worldLayoutStableId)
                + "/grounding-readiness", cancellationToken);

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
            공간TileStreamRemoteRequestDiagnostics.RecordRequest();
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
            공간TileStreamRemoteRequestDiagnostics.RecordRequest();
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
            공간TileStreamRemoteRequestDiagnostics.RecordRequest();
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
