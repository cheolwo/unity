using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Application.WorldMap
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class LoadPublicWorldMapUseCase
    {
        private readonly ICommunityWorldMapRepository repository;

        public LoadPublicWorldMapUseCase(ICommunityWorldMapRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<PublicWorldMapSnapshot> ExecuteAsync(string datasetCode, CancellationToken cancellationToken)
        {
            var snapshot = await repository.LoadAsync(datasetCode ?? string.Empty, cancellationToken);
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Revision))
                throw new InvalidOperationException("공개 세계지도 snapshot 또는 revision이 없습니다.");

            foreach (var marker in snapshot.Markers ?? Array.Empty<PublicWorldMarker>()) marker.Validate();
            return snapshot;
        }
    }
}
