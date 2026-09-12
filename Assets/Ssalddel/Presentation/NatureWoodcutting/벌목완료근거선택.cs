using System;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    /// <summary>
    /// 같은 조회 경계의 원장 후보를 이전 관측 작업에 연결한다. 권위 조회/쓰기와 완료 판정은 하지 않는다.
    /// 호출자는 Project 전에 호출하고, Load에는 증가한 epoch를 전달한다.
    /// hash는 불투명한 계보 값으로 보존하며 여기서 재계산하거나 진위를 인증하지 않는다.
    /// </summary>
    public static class 벌목완료근거선택
    {
        public static bool TrySelect(Nature생존State current, 벌목TaskObservationState previous,
            long epoch, out 벌목TaskTerminalEvidence evidence, out string recordHash, out string diagnostic)
        {
            evidence = null;
            recordHash = string.Empty;
            diagnostic = "Missing";
            var snapshot = previous.Snapshot;
            if (current == null || !previous.Observed || snapshot == null
                || snapshot.Phase != 벌목WorkPhase.Working || epoch < 0
                || epoch != previous.ObservationEpoch || previous.Revision < 0
                // Interrupt 뒤에는 Working Snapshot이 남을 수 있다. 이미 봉인된 종료를 되살리지 않는다.
                || snapshot.Revision != previous.Revision || current.Revision <= previous.Revision
                || string.IsNullOrWhiteSpace(snapshot.SessionStableId)
                || string.IsNullOrWhiteSpace(snapshot.ActorStableId)
                || string.IsNullOrWhiteSpace(snapshot.TargetStableId)
                || string.IsNullOrWhiteSpace(previous.OriginCommandId)
                || !Same(current.SessionStableId, snapshot.SessionStableId)
                || !Same(current.PlayerStableId, snapshot.ActorStableId))
                return false;

            var records = current.HarvestCompletionRecords;
            if (records == null) return false;
            Nature벌목CompletionEvidence selected = null;
            foreach (var candidate in records)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.EvidenceStableId)
                    || string.IsNullOrWhiteSpace(candidate.RecordHashSha256)
                    || !Same(candidate.SessionStableId, snapshot.SessionStableId)
                    || !Same(candidate.ActorStableId, snapshot.ActorStableId)
                    || !Same(candidate.OriginCommandId, previous.OriginCommandId)
                    || !Same(candidate.TargetStableId, snapshot.TargetStableId)
                    || candidate.ResultRevision <= previous.Revision
                    || candidate.ResultRevision > current.Revision)
                    continue;
                if (selected != null && !SameRecord(selected, candidate))
                {
                    diagnostic = "Conflict";
                    return false;
                }
                selected = candidate;
            }
            if (selected == null) return false;

            // 적격 후보와 같은 기록 ID인데 내용이 다른 행은 필터 밖이어도 모순이다.
            // 순서에 따라 누락 hash/잘못된 ID의 사본이 정상 사본으로 가려지는 것을 막는다.
            foreach (var candidate in records)
            {
                if (candidate != null && Same(candidate.EvidenceStableId, selected.EvidenceStableId)
                    && !SameRecord(selected, candidate))
                {
                    diagnostic = "Conflict";
                    return false;
                }
            }

            // 새 불변 DTO와 string 값만 반환한다. 입력 배열/가변 기록 참조는 저장하지 않는다.
            evidence = new 벌목TaskTerminalEvidence(selected.SessionStableId, selected.ActorStableId,
                selected.OriginCommandId, selected.TargetStableId, epoch, selected.ResultRevision,
                벌목TerminalOutcome.Completed, selected.EvidenceStableId);
            recordHash = selected.RecordHashSha256;
            diagnostic = "Selected";
            return true;
        }

        private static bool SameRecord(Nature벌목CompletionEvidence left, Nature벌목CompletionEvidence right)
            => Same(left.SessionStableId, right.SessionStableId)
                && Same(left.ActorStableId, right.ActorStableId)
                && Same(left.OriginCommandId, right.OriginCommandId)
                && Same(left.TargetStableId, right.TargetStableId)
                && Same(left.EvidenceStableId, right.EvidenceStableId)
                && Same(left.RecordHashSha256, right.RecordHashSha256)
                && left.ResultRevision == right.ResultRevision;

        private static bool Same(string left, string right)
            => string.Equals(left, right, StringComparison.Ordinal);
    }
}
