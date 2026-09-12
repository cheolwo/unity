using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Runtime.Exhibition;

namespace Ssalddel.Unity.Application.Exhibition
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 통합전시관SimulationLoadUseCase
    {
        private readonly I통합전시관SimulationSessionRepository repository;
        private readonly Func<통합전시관Snapshot> snapshotFactory;

        public 통합전시관SimulationLoadUseCase(
            I통합전시관SimulationSessionRepository sessionRepository,
            Func<통합전시관Snapshot> seedbedSnapshotFactory)
        {
            repository = sessionRepository
                ?? throw new ArgumentNullException(nameof(sessionRepository));
            snapshotFactory = seedbedSnapshotFactory
                ?? throw new ArgumentNullException(nameof(seedbedSnapshotFactory));
        }

        public async Task<통합전시관ServerBoundSnapshot> ExecuteAsync(
            string sessionStableId, long minimumAcceptedRevision,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException("시뮬레이션 세션 고유 식별자가 필요합니다.", nameof(sessionStableId));

            var session = await repository.LoadAsync(sessionStableId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            session.Validate(sessionStableId);
            if (session.Revision < minimumAcceptedRevision)
                throw new InvalidOperationException("IntegratedExhibitionSimulationRevisionRegressed");

            var result = new 통합전시관ServerBoundSnapshot
            {
                Snapshot = snapshotFactory()
                    ?? throw new InvalidOperationException("IntegratedExhibitionSeedbedSnapshotMissing"),
                Session = session,
            };
            result.Validate();
            return result;
        }
    }
}
