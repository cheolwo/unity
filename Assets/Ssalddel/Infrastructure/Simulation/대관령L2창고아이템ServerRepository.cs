using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class 대관령L2창고아이템ServerRepository
        : I대관령L2창고아이템AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 대관령L2창고아이템ServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<대관령L2창고InventorySnapshot> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", InventoryRoute(sessionStableId),
                string.Empty, cancellationToken);
            var snapshot = JsonConvert.DeserializeObject<대관령L2창고InventorySnapshot>(
                               response.Body)
                           ?? throw new InvalidOperationException(
                               "DaegwallyeongInventoryJsonInvalid");
            snapshot.Validate();
            return snapshot;
        }

        public async Task<대관령L2아이템획득PreviewSnapshot> PreviewAsync(
            string sessionStableId,
            대관령L2아이템획득PreviewRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var response = await SendAsync("POST",
                InventoryRoute(sessionStableId) + "/item-acquisition-previews",
                JsonConvert.SerializeObject(request), cancellationToken);
            return JsonConvert.DeserializeObject<대관령L2아이템획득PreviewSnapshot>(
                       response.Body)
                   ?? throw new InvalidOperationException(
                       "DaegwallyeongInventoryPreviewJsonInvalid");
        }

        public async Task<대관령L2창고InventorySnapshot> ConfirmAndReloadAsync(
            string sessionStableId,
            대관령L2아이템획득ConfirmRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            await SendAsync("POST",
                InventoryRoute(sessionStableId) + "/item-acquisitions/confirm",
                JsonConvert.SerializeObject(request), cancellationToken);
            return await LoadAsync(sessionStableId, cancellationToken);
        }

        private async Task<UnityApiResponse> SendAsync(
            string method, string relativePath, string jsonBody,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = relativePath,
                JsonBody = jsonBody,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string InventoryRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException("SimulationSessionStableIdMissing",
                    nameof(sessionStableId));
            return "api/simulation/v1/sessions/"
                   + Uri.EscapeDataString(sessionStableId.Trim())
                   + "/world-inventory";
        }
    }
}
