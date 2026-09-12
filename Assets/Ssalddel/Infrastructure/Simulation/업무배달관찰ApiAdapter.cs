using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>격리 로컬 검증 서버에만 연결한다. 비밀·응답 오류 본문을 로그로 내보내지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "업무 서버 관찰 HTTP 연결", Boundary = "GET 재조회와 명시적 시작 POST만 허용. 운영 서버·Local Runtime fallback 없음")]
    public sealed class 업무배달관찰ApiAdapter : IDisposable
    {
        private sealed class Connection { public string baseUrl, accessKey; }
        private readonly HttpClient client;
        private readonly string connectionPath;
        public 업무배달관찰ApiAdapter(string path = null, HttpMessageHandler handler = null)
        {
            connectionPath = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "source", "repos", "Hongdal", "artifacts", "local", "verification", "food-observer", "connection.json");
            client = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false }) : new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(4);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }
        public Task<업무배달관찰Model> 조회Async(CancellationToken cancel) => Request(false, cancel);
        public Task<업무배달관찰Model> 시작Async(CancellationToken cancel) => Request(true, cancel);
        private async Task<업무배달관찰Model> Request(bool start, CancellationToken cancel)
        {
            // 파일 실패를 기본 키나 fixture로 대체하지 않는다. 읽기 작업도 Unity 프레임에서 분리한다.
            var json = await Task.Run(() => File.ReadAllText(connectionPath), cancel).ConfigureAwait(false);
            var config = JsonConvert.DeserializeObject<Connection>(json);
            if (config == null || !AllowedEndpoint(config.baseUrl) || string.IsNullOrWhiteSpace(config.accessKey)
                || config.accessKey.Contains("\r") || config.accessKey.Contains("\n")) throw new InvalidOperationException("ObserverConnectionInvalid");
            var uri = new Uri(config.baseUrl.TrimEnd('/') + "/verification/food-delivery" + (start ? "/start" : ""));
            using var request = new HttpRequestMessage(start ? HttpMethod.Post : HttpMethod.Get, uri);
            request.Headers.Add("X-Verification-Key", config.accessKey);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("ObserverHttpFailed");
            if (start) return await Request(false, cancel).ConfigureAwait(false); // 시작 응답을 업무 완료로 해석하지 않고 GET 재조회
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (body.Length > 1024 * 1024) throw new FormatException("ObserverResponseTooLarge");
            return 업무배달관찰ApiMapper.Map(JsonConvert.DeserializeObject<업무배달관찰Dto>(body));
        }
        public static bool AllowedEndpoint(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == 5215
            && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
        public void Dispose() => client.Dispose();
    }
}
