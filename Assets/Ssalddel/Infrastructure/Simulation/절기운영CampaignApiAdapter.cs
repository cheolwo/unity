using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 공통 Simulation 예행연습 Client를 절기 운영 Campaign의 인증 GET
    /// 전송 계약에 맞추는 얇은 Adapter입니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "절기 운영 Campaign의 인증된 읽기 요청을 Simulation RemoteHost에 전달한다.",
        Boundary = "GET만 허용하며 Campaign Command·운영 업무 변경·응답 영속을 수행하지 않는다.")]
    public sealed class 절기운영CampaignApiAdapter
        : IOperationalWorldProjectionTransport
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public 절기운영CampaignApiAdapter(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client
                ?? throw new ArgumentNullException(nameof(client));

        public async Task<string> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            if (allowNotFound || !requiresAuthentication)
                throw new InvalidOperationException(
                    "SeasonalCampaignReadBoundaryInvalid");
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = relativeRoute,
                RequiresAuthentication = true,
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response == null || !response.IsSuccess
                || string.IsNullOrWhiteSpace(response.Body))
                throw new InvalidOperationException(
                    "SeasonalCampaignReadFailed:"
                    + (response?.StatusCode ?? 0) + ":"
                    + (response?.ErrorCode ?? "NoResponse"));
            return response.Body;
        }
    }
}
