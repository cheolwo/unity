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
        public bool ExpectsBinaryResponse { get; set; }

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
        public byte[] BodyBytes { get; set; } = Array.Empty<byte>();
        public bool IsSuccess => StatusCode >= 200 && StatusCode <= 299;
    }

    public interface IUnityAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface IUnityApiClient
    {
        Task<UnityApiResponse> SendAsync(
            UnityApiRequest request,
            CancellationToken cancellationToken);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface IOperationalUnityApiClient : IUnityApiClient
    {
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface ISimulationRehearsalUnityApiClient : IUnityApiClient
    {
    }
}
