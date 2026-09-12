using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 전용 로컬 검증 호스트의 v2 상태 사본만 읽습니다. 응답·키를 파일이나 PlayerPrefs에 쓰지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "전용 관찰 운영 검증 API의 v2 상태 사본을 읽는다.",
        Boundary = "127.0.0.1:53216 GET만 허용하며 운영 Command, 외부 HTTP, 로컬 사본 저장을 제공하지 않는다.")]
    public sealed class 운영관찰검증ApiTransport : IOperationalWorldProjectionTransport, IDisposable
    {
        [Serializable]
        private sealed class Connection
        {
            public string baseUrl = string.Empty;
            public string accessKey = string.Empty;
        }

        private readonly HttpClient client;
        private readonly string connectionPath;

        public 운영관찰검증ApiTransport(string path = null, HttpMessageHandler handler = null)
        {
            connectionPath = path ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "source", "repos", "Hongdal", "artifacts", "local", "verification",
                "observable-operations", "connection.json");
            client = handler == null
                ? new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseProxy = false,
                    UseCookies = false
                })
                : new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(4);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }

        public async Task<string> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            if (allowNotFound || !requiresAuthentication)
                throw new InvalidOperationException("OperationalObservationReadBoundaryInvalid");
            var cursor = ReadQueryValue(relativeRoute, "cursor");
            var schema = ReadQueryValue(relativeRoute, "schemaVersion");
            if (schema != OperationalWorldScenePolicy.SchemaVersionV2)
                throw new InvalidOperationException("OperationalObservationV2Required");

            var json = await Task.Run(() => File.ReadAllText(connectionPath), cancellationToken)
                .ConfigureAwait(false);
            var configuration = JsonConvert.DeserializeObject<Connection>(json);
            if (configuration == null
                || !AllowedEndpoint(configuration.baseUrl)
                || string.IsNullOrWhiteSpace(configuration.accessKey)
                || configuration.accessKey.Any(character => character is '\r' or '\n'))
                throw new InvalidOperationException("OperationalObservationConnectionInvalid");

            var uri = new Uri(configuration.baseUrl.TrimEnd('/')
                              + "/verification/observable-operations/scene-snapshots?cursor="
                              + Uri.EscapeDataString(cursor));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Verification-Key", configuration.accessKey);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("OperationalObservationHttpFailed");
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (body.Length > 1024 * 1024)
                throw new FormatException("OperationalObservationResponseTooLarge");
            return body;
        }

        public static bool ConnectionExists(string path = null)
            => File.Exists(path ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "source", "repos", "Hongdal", "artifacts", "local", "verification",
                "observable-operations", "connection.json"));

        public static bool AllowedEndpoint(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && uri.Scheme == "http"
               && uri.Host == "127.0.0.1"
               && uri.Port == 53216
               && uri.UserInfo.Length == 0
               && uri.AbsolutePath == "/"
               && uri.Query.Length == 0
               && uri.Fragment.Length == 0;

        public void Dispose() => client.Dispose();

        private static string ReadQueryValue(string relativeRoute, string key)
        {
            var queryIndex = relativeRoute.IndexOf('?');
            if (queryIndex < 0) return string.Empty;
            foreach (var part in relativeRoute.Substring(queryIndex + 1).Split('&'))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (pair.Length == 2 && string.Equals(pair[0], key, StringComparison.Ordinal))
                    return Uri.UnescapeDataString(pair[1]);
            }
            return string.Empty;
        }
    }

    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "v2 JSON을 공유 운영 지역 장면 계약으로 명시적으로 변환한다.",
        Boundary = "역직렬화만 수행하며 서버 완료를 재판정하거나 응답을 영속하지 않는다.")]
    public sealed class 운영관찰검증JsonDecoder : IOperationalWorldSceneDecoder
    {
        public OperationalWorldSceneResponse Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("OperationalObservationJsonEmpty");
            var response = JsonConvert.DeserializeObject<OperationalWorldSceneResponse>(json)
                           ?? throw new FormatException("OperationalObservationJsonInvalid");
            if (!string.Equals(response.SchemaVersion, OperationalWorldScenePolicy.SchemaVersionV2,
                    StringComparison.Ordinal))
                throw new FormatException("OperationalObservationSchemaMismatch");
            return response;
        }
    }
}
