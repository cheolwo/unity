using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Runtime.Configuration;

namespace Ssalddel.Unity.Runtime.Exhibition
{
    public sealed class 통합전시관SimulationSessionState
    {
        public string SessionStableId { get; set; } = string.Empty;
        public string ScenarioStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public long WorldRevision { get; set; }
        public int WorldTick { get; set; }
        public DateTimeOffset GameDate { get; set; }
        public string ModeCode { get; set; } = string.Empty;
        public bool IsOperationalState { get; set; }
        public DateTimeOffset FetchedAtUtc { get; set; }

        public void Validate(string expectedSessionStableId)
        {
            if (string.IsNullOrWhiteSpace(expectedSessionStableId)
                || !string.Equals(SessionStableId, expectedSessionStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("IntegratedExhibitionSimulationSessionIdentityMismatch");
            if (string.IsNullOrWhiteSpace(ScenarioStableId)
                || Revision < 0 || WorldRevision < 0 || WorldTick < 0)
                throw new InvalidOperationException("IntegratedExhibitionSimulationSessionStateInvalid");
            if (!string.Equals(ModeCode, UnityExecutionModeCodes.Simulation, StringComparison.Ordinal)
                || IsOperationalState)
                throw new InvalidOperationException("IntegratedExhibitionOperationalStateForbidden");
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface I통합전시관SimulationSessionRepository
    {
        Task<통합전시관SimulationSessionState> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken);
    }

    public sealed class 통합전시관ServerBoundSnapshot
    {
        public 통합전시관Snapshot Snapshot { get; set; } = null!;
        public 통합전시관SimulationSessionState Session { get; set; } = null!;

        public void Validate()
        {
            if (Snapshot == null || Snapshot.Exhibits == null || Snapshot.Exhibits.Length == 0
                || Session == null)
                throw new InvalidOperationException("IntegratedExhibitionServerBoundSnapshotInvalid");
            Session.Validate(Session.SessionStableId);
        }
    }
}
