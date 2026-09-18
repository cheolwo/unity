using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Infrastructure.Transport
{
    /// <summary>
    /// Unity 실행 중에만 환경 변수에서 접근 token을 읽습니다. token을 Asset,
    /// PlayerPrefs, 로그 또는 상태 사본에 저장하지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "Simulation RemoteHost의 읽기 요청에 필요한 휘발성 접근 token을 제공한다.",
        Boundary = "환경 변수 메모리 조회만 수행하며 token을 Unity 자산·로그·로컬 저장에 남기지 않는다.")]
    public sealed class EnvironmentUnityAccessTokenProvider
        : IUnityAccessTokenProvider
    {
        public const string EnvironmentVariableName =
            "SSALDDEL_UNITY_ACCESS_TOKEN";

        public bool IsAvailable => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(EnvironmentVariableName));

        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = Environment.GetEnvironmentVariable(
                EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(token)
                || token.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidOperationException(
                    "SeasonalCampaignAccessTokenUnavailable");
            return Task.FromResult(token.Trim());
        }
    }
}
