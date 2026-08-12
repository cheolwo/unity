using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.Configuration;
using Ssalddel.Unity.Runtime.Transport;
using UnityEngine.Networking;

namespace Ssalddel.Unity.Infrastructure.Transport
{
    public sealed class UnityWebRequestApiClient : IUnityApiClient
    {
        private readonly Uri _baseAddress;
        private readonly IUnityAccessTokenProvider _accessTokenProvider;

        public UnityWebRequestApiClient(
            string apiBaseUrl,
            IUnityAccessTokenProvider accessTokenProvider = null)
        {
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseAddress)
                || (baseAddress.Scheme != Uri.UriSchemeHttp
                    && baseAddress.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    "API 기준 주소는 HTTP 또는 HTTPS 절대 주소여야 합니다.",
                    nameof(apiBaseUrl));
            }

            _baseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            _accessTokenProvider = accessTokenProvider;
        }

        public async Task<UnityApiResponse> SendAsync(
            UnityApiRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Validate();
            var url = new Uri(_baseAddress, request.RelativePath.TrimStart('/'));
            using var webRequest = new UnityWebRequest(url, request.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
            };
            webRequest.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(request.JsonBody))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.JsonBody));
                webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }

            if (request.RequiresAuthentication)
            {
                if (_accessTokenProvider == null)
                {
                    throw new InvalidOperationException("인증이 필요한 API 요청에 token provider가 없습니다.");
                }

                var token = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("인증 token을 가져오지 못했습니다.");
                }

                webRequest.SetRequestHeader("Authorization", "Bearer " + token);
            }

            // UnityWebRequest의 responseCode/result/downloadHandler는 Unity 메인 스레드에서 읽어야 합니다.
            // Unity SynchronizationContext를 유지해 실제 PlayMode 통신에서도 안전하게 복귀합니다.
            await SendAsync(webRequest, cancellationToken);
            return new UnityApiResponse
            {
                StatusCode = webRequest.responseCode,
                Body = webRequest.downloadHandler?.text ?? string.Empty,
                ErrorCode = webRequest.result == UnityWebRequest.Result.Success
                    ? string.Empty
                    : webRequest.result.ToString(),
            };
        }

        private static Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            if (!cancellationToken.CanBeCanceled)
            {
                return completion.Task;
            }

            return AwaitWithCancellationAsync(request, completion, cancellationToken);
        }

        private static async Task AwaitWithCancellationAsync(
            UnityWebRequest request,
            TaskCompletionSource<bool> completion,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() =>
                   {
                       request.Abort();
                       completion.TrySetCanceled(cancellationToken);
                   }))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }
    }

    public sealed class OperationalUnityWebRequestApiClient : IOperationalUnityApiClient
    {
        private readonly UnityWebRequestApiClient inner;

        public OperationalUnityWebRequestApiClient(
            UnityClientRuntimeOptions options,
            IUnityAccessTokenProvider accessTokenProvider = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.ValidateOperationalConnection();
            inner = new UnityWebRequestApiClient(
                options.OperationalApiBaseUrl,
                accessTokenProvider);
        }

        public Task<UnityApiResponse> SendAsync(
            UnityApiRequest request,
            CancellationToken cancellationToken)
            => inner.SendAsync(request, cancellationToken);
    }

    public sealed class SimulationRehearsalUnityWebRequestApiClient
        : ISimulationRehearsalUnityApiClient
    {
        private readonly UnityWebRequestApiClient inner;

        public SimulationRehearsalUnityWebRequestApiClient(
            UnityClientRuntimeOptions options,
            IUnityAccessTokenProvider accessTokenProvider = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.ValidateSimulationRehearsalConnection();
            inner = new UnityWebRequestApiClient(
                options.SimulationRehearsalApiBaseUrl,
                accessTokenProvider);
        }

        public Task<UnityApiResponse> SendAsync(
            UnityApiRequest request,
            CancellationToken cancellationToken)
            => inner.SendAsync(request, cancellationToken);
    }
}
