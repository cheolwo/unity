using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Runtime.Exhibition;

namespace Ssalddel.Unity.Application.Exhibition
{
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
