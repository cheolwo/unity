using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.Transport
{
    public sealed class UnityApiRequest
    {
        public string Method { get; set; } = "GET";
        public string RelativePath { get; set; } = string.Empty;
        public string JsonBody { get; set; } = string.Empty;
        public bool RequiresAuthentication { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Method))
            {
                throw new InvalidOperationException("HTTP method가 필요합니다.");
            }

            if (string.IsNullOrWhiteSpace(RelativePath)
                || Uri.TryCreate(RelativePath, UriKind.Absolute, out _)
                || RelativePath.Contains(".."))
            {
                throw new InvalidOperationException("API 경로는 안전한 상대 경로여야 합니다.");
            }

            var normalized = RelativePath.TrimStart('/');
            if (!normalized.StartsWith("api/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unity API 요청은 api/ 경계 안에서만 허용됩니다.");
            }
        }
    }

    public sealed class UnityApiResponse
    {
        public long StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public bool IsSuccess => StatusCode >= 200 && StatusCode <= 299;
    }

    public interface IUnityAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }

    public interface IUnityApiClient
    {
        Task<UnityApiResponse> SendAsync(
            UnityApiRequest request,
            CancellationToken cancellationToken);
    }

    public interface IOperationalUnityApiClient : IUnityApiClient
    {
    }

    public interface ISimulationRehearsalUnityApiClient : IUnityApiClient
    {
    }
}
