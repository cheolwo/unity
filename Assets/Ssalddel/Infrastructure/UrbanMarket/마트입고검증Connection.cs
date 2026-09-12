using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.UrbanMarket
{
    /// <summary>명시적으로 지정한 로컬 검증 연결만 사용. 비밀을 Scene/로그에 저장하지 않으며 GET 이외는 거부한다.</summary>
    public sealed class 마트입고검증Connection : IOperationalUnityApiClient, IDisposable
    {
        private sealed class Settings { public string baseUrl, accessToken; public long warehouseId; public string[] storageLocations; }
        private readonly HttpClient client;
        private readonly Settings settings;
        public long WarehouseId => settings.warehouseId;
        public string[] StorageLocations => (string[])settings.storageLocations.Clone();
        private 마트입고검증Connection(Settings settings)
        {
            this.settings = settings;
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false }) {
                Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 1024 * 1024 };
        }
        public static async Task<마트입고검증Connection> LoadAsync(string path, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("WarehouseConnectionPathInvalid");
            var json = await Task.Run(() => File.ReadAllText(path), cancel);
            var settings = JsonConvert.DeserializeObject<Settings>(json);
            if (settings == null || !업무배달관찰ApiAdapter.AllowedEndpoint(settings.baseUrl) || settings.warehouseId <= 0
                || string.IsNullOrWhiteSpace(settings.accessToken) || settings.accessToken.Length > 16384
                || settings.accessToken.Contains("\r") || settings.accessToken.Contains("\n")
                || settings.storageLocations == null || settings.storageLocations.Length != 2
                || string.IsNullOrWhiteSpace(settings.storageLocations[0]) || string.IsNullOrWhiteSpace(settings.storageLocations[1])
                || settings.storageLocations[0] == settings.storageLocations[1]) throw new FormatException("WarehouseConnectionInvalid");
            return new 마트입고검증Connection(settings);
        }
        public async Task<UnityApiResponse> SendAsync(UnityApiRequest request, CancellationToken cancel)
        {
            var path = "api/v1/warehouse-operations/world/zones/warehouse?warehouseId=" + WarehouseId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (request == null || request.Method != "GET" || request.RelativePath != path || !request.RequiresAuthentication
                || !string.IsNullOrEmpty(request.JsonBody)) throw new InvalidOperationException("WarehouseConnectionReadOnly");
            using var message = new HttpRequestMessage(HttpMethod.Get, settings.baseUrl.TrimEnd('/') + "/" + path);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.accessToken);
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancel);
            return new UnityApiResponse { StatusCode = (long)response.StatusCode,
                Body = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : "", ErrorCode = response.IsSuccessStatusCode ? "" : "WarehouseRequestFailed" };
        }
        public void Dispose() => client.Dispose();
    }
}
