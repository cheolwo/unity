using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    public enum 벌목ProjectionDispatch { None, Apply, Reconstruct, Interrupt }
    public enum 벌목TerminalOutcome { Completed, Cancelled }

    /// <summary>개발 조회 연결부가 원장 계보를 확인한 뒤 제공한다. Projector는 원장을 조회하지 않는다.</summary>
    public sealed class 벌목TaskTerminalEvidence
    {
        public 벌목TaskTerminalEvidence(string sessionStableId, string actorStableId,
            string originCommandId, string targetStableId, long observationEpoch,
            long resultRevision, 벌목TerminalOutcome outcome, string evidenceStableId)
        {
            SessionStableId = sessionStableId; ActorStableId = actorStableId;
            OriginCommandId = originCommandId; TargetStableId = targetStableId;
            ObservationEpoch = observationEpoch; ResultRevision = resultRevision;
            Outcome = outcome; EvidenceStableId = evidenceStableId;
        }
        public string SessionStableId { get; }
        public string ActorStableId { get; }
        public string OriginCommandId { get; }
        public string TargetStableId { get; }
        public long ObservationEpoch { get; }
        public long ResultRevision { get; }
        public 벌목TerminalOutcome Outcome { get; }
        public string EvidenceStableId { get; }
    }

    public sealed class 벌목TaskObservationContext
    {
        public 벌목TaskObservationContext(long observationEpoch, bool workInputHeld,
            bool isReconstruction = false, 벌목ReturnIntent returnIntent = 벌목ReturnIntent.Idle,
            벌목TaskTerminalEvidence terminalEvidence = null)
        {
            ObservationEpoch = observationEpoch; WorkInputHeld = workInputHeld;
            IsReconstruction = isReconstruction; ReturnIntent = returnIntent;
            TerminalEvidence = terminalEvidence;
        }
        public long ObservationEpoch { get; }
        public bool WorkInputHeld { get; }
        public bool IsReconstruction { get; }
        public 벌목ReturnIntent ReturnIntent { get; }
        public 벌목TaskTerminalEvidence TerminalEvidence { get; }
    }

    public readonly struct 벌목TaskProjection
    {
        internal 벌목TaskProjection(bool accepted, 벌목ProjectionDispatch dispatch,
            벌목AnimationSnapshot snapshot, bool hasActiveTask, bool shouldTick,
            벌목ReturnIntent returnIntent, string originCommandId, string diagnostic)
        {
            Accepted = accepted; Dispatch = dispatch; Snapshot = snapshot;
            HasActiveTask = hasActiveTask; ShouldTick = shouldTick; ReturnIntent = returnIntent;
            OriginCommandId = originCommandId; Diagnostic = diagnostic;
        }
        public bool Accepted { get; }
        public 벌목ProjectionDispatch Dispatch { get; }
        public 벌목AnimationSnapshot Snapshot { get; }
        public bool HasActiveTask { get; }
        // 이미 lease를 가진 소비자의 시간 진행 허용값이지 Graph 생성/소유 요청이 아니다.
        public bool ShouldTick { get; }
        public 벌목ReturnIntent ReturnIntent { get; }
        public string OriginCommandId { get; }
        public string Diagnostic { get; }
        public bool PresentationOnly => true;
    }

    public readonly struct 벌목TaskObservationState
    {
        internal 벌목TaskObservationState(bool observed, long epoch, long generation,
            long revision, string origin, 벌목AnimationSnapshot snapshot, string diagnostic)
        {
            Observed = observed; ObservationEpoch = epoch; ObservationGeneration = generation;
            Revision = revision; OriginCommandId = origin; Snapshot = snapshot; Diagnostic = diagnostic;
        }
        public bool Observed { get; }
        public long ObservationEpoch { get; }
        public long ObservationGeneration { get; }
        public long Revision { get; }
        public string OriginCommandId { get; }
        public 벌목AnimationSnapshot Snapshot { get; }
        public string Diagnostic { get; }
    }

    /// <summary>
    /// 실제 상태의 scalar/노드 상태 사본만 보관하는 표현 투영기.
    /// 시계·권위 조회/쓰기·Graph·Transform·AudioFX 실행을 보유하지 않는다.
    /// </summary>
    public sealed class 벌목TaskObservationProjector
    {
        private 관측사본 previous;
        private 벌목AnimationSnapshot snapshot;
        private long epoch;
        private long generation;
        private string origin = string.Empty;
        private string diagnostic = "NotObserved";

        // 인스턴스 교체 때 호출자는 소비자/중복방지 수명도 함께 재구성해야 한다.
        public 벌목TaskObservationProjector(long initialGeneration = 0)
        {
            if (initialGeneration < 0) throw new ArgumentOutOfRangeException(nameof(initialGeneration));
            generation = initialGeneration;
        }

        public 벌목TaskProjection Project(Nature생존State current, 벌목TaskObservationContext context)
        {
            if (context == null || context.ObservationEpoch < 0
                || !Enum.IsDefined(typeof(벌목ReturnIntent), context.ReturnIntent))
                return Reject("ContextInvalid", context);
            if (!관측사본.TryCapture(current, out var next, out var error)) return Reject(error, context);
            // 명시 복원 요청을 중복 조회로 삼키지 않는다. 이미 수용한 epoch에서 재초기화도 금지한다.
            if (previous != null && context.IsReconstruction && context.ObservationEpoch <= epoch)
                return Reject("ReconstructionEpochNotAdvanced", context);
            if (previous != null && context.ObservationEpoch < epoch) return Reject("EpochRegressed", context);
            bool newEpoch = previous == null || context.ObservationEpoch > epoch;
            bool sourceChanged = previous != null
                && (previous.Session != next.Session || previous.Player != next.Player);
            // 다른 조회 소스의 revision을 비교하거나 상위 revision만으로 소스 교체를 승인하지 않는다.
            if (sourceChanged && !newEpoch) return Reject("SourceEpochNotAdvanced", context);
            if (!newEpoch && next.Revision < previous.Revision) return Reject("RevisionRegressed", context);
            if (!newEpoch && next.Revision == previous.Revision)
            {
                if (!next.SameAuthority(previous)) return Reject("SameRevisionConflict", context);
                // 일시정지·held·복귀의도는 같은 revision에서도 바뀔 수 있다. Snapshot은 불변.
                previous = next;
                return Result(벌목ProjectionDispatch.None, context, "DuplicateObservation");
            }

            bool newTask = next.Harvest && (previous == null || !previous.Harvest || !next.SameTask(previous));
            if (newEpoch || newTask)
            {
                if (generation == long.MaxValue) return Reject("GenerationOverflow", context);
                long nextGeneration = generation + 1;
                var baseline = CreateSnapshot(next, nextGeneration, next.Harvest ? 벌목WorkPhase.Working : 벌목WorkPhase.Idle, 0);
                generation = nextGeneration; epoch = context.ObservationEpoch;
                snapshot = baseline; origin = next.Harvest ? next.Origin : string.Empty; previous = next;
                return Result(벌목ProjectionDispatch.Reconstruct, context, "BaselineNoContactReplay");
            }

            if (next.Harvest)
            {
                if (next.Completed < previous.Completed) return Reject("ProgressRegressed", context);
                long token = next.Completed > previous.Completed ? next.Completed : snapshot.ProgressToken;
                snapshot = CreateSnapshot(next, generation, 벌목WorkPhase.Working, token);
                previous = next;
                return Result(벌목ProjectionDispatch.Apply, context, token > 0 ? "ObservedProgress" : "NoNewProgress");
            }

            if (previous.Harvest)
            {
                var evidence = context.TerminalEvidence;
                bool matches = evidence != null
                    && !string.IsNullOrWhiteSpace(evidence.EvidenceStableId)
                    && evidence.ObservationEpoch == epoch
                    && evidence.SessionStableId == previous.Session && evidence.ActorStableId == previous.Actor
                    && evidence.OriginCommandId == previous.Origin && evidence.TargetStableId == previous.Target
                    && evidence.ResultRevision > previous.Revision && evidence.ResultRevision <= next.Revision
                    && Enum.IsDefined(typeof(벌목TerminalOutcome), evidence.Outcome);
                string terminalDiagnostic;
                벌목ProjectionDispatch dispatch;
                if (!matches)
                {
                    terminalDiagnostic = evidence == null ? "CompletionEvidenceMissing" : "TerminalEvidenceMismatch";
                    dispatch = 벌목ProjectionDispatch.Interrupt;
                }
                else if (evidence.Outcome == 벌목TerminalOutcome.Completed
                    && previous.NodeState(previous.Target) == Nature생존Codes.Standing
                    && next.NodeState(previous.Target) == Nature생존Codes.Stump)
                {
                    snapshot = new 벌목AnimationSnapshot(previous.Session, previous.Actor, previous.Target,
                        generation, next.Revision, previous.Required, previous.Required,
                        벌목WorkPhase.Completed, previous.Required);
                    terminalDiagnostic = "CompletionEvidenceAccepted"; dispatch = 벌목ProjectionDispatch.Apply;
                }
                else if (evidence.Outcome == 벌목TerminalOutcome.Cancelled
                    && next.NodeState(previous.Target) == Nature생존Codes.Standing)
                {
                    snapshot = new 벌목AnimationSnapshot(previous.Session, previous.Actor, previous.Target,
                        generation, next.Revision, previous.Required, previous.Completed,
                        벌목WorkPhase.Cancelled, snapshot.ProgressToken);
                    terminalDiagnostic = "CancellationEvidenceAccepted"; dispatch = 벌목ProjectionDispatch.Apply;
                }
                else
                {
                    terminalDiagnostic = "TerminalStateEvidenceConflict"; dispatch = 벌목ProjectionDispatch.Interrupt;
                }
                // 완료근거 누락도 종료를 봉인한다. 뒤늦은 동일 상태 조회로 과거 접촉을 되살리지 않는다.
                previous = next;
                return Result(dispatch, context, terminalDiagnostic);
            }
            previous = next;
            return Result(벌목ProjectionDispatch.None, context, "NoActiveHarvest");
        }

        public 벌목TaskObservationState ReadState() => new 벌목TaskObservationState(previous != null,
            epoch, generation, previous?.Revision ?? -1, origin, snapshot, diagnostic);

        private 벌목TaskProjection Result(벌목ProjectionDispatch dispatch, 벌목TaskObservationContext context, string code)
        {
            diagnostic = code;
            return new 벌목TaskProjection(true, dispatch, snapshot, previous.Harvest,
                !previous.Paused && (!previous.Harvest || context.WorkInputHeld), context.ReturnIntent, origin, code);
        }
        private 벌목TaskProjection Reject(string code, 벌목TaskObservationContext context)
        {
            diagnostic = code;
            return new 벌목TaskProjection(false, 벌목ProjectionDispatch.None, snapshot, previous?.Harvest ?? false,
                false, context?.ReturnIntent ?? 벌목ReturnIntent.Idle, origin, code);
        }
        private static 벌목AnimationSnapshot CreateSnapshot(관측사본 value, long generation, 벌목WorkPhase phase, long token)
            => new 벌목AnimationSnapshot(value.Session, value.Harvest ? value.Actor : value.Player,
                value.Harvest ? value.Target : string.Empty, generation, value.Revision,
                value.Harvest ? value.Required : 0, value.Harvest ? value.Completed : 0, phase, token);

        private sealed class 관측사본
        {
            public string Session, Player, Kind, Actor, Origin, Target;
            public long Revision;
            public bool Paused, HasWork;
            public int Required, Completed;
            private readonly Dictionary<string, string> nodes = new Dictionary<string, string>(StringComparer.Ordinal);
            public bool Harvest => HasWork && Kind == Nature생존Codes.Harvest;
            public string NodeState(string id) => nodes.TryGetValue(id, out var state) ? state : null;
            public bool SameTask(관측사본 other) => Kind == other.Kind && Actor == other.Actor
                && Origin == other.Origin && Target == other.Target && Required == other.Required;
            public bool SameAuthority(관측사본 other)
            {
                if (Session != other.Session || Player != other.Player || Revision != other.Revision
                    || HasWork != other.HasWork || !SameTask(other) || Completed != other.Completed
                    || nodes.Count != other.nodes.Count) return false;
                foreach (var pair in nodes)
                    if (!other.nodes.TryGetValue(pair.Key, out var state) || pair.Value != state) return false;
                return true;
            }
            public static bool TryCapture(Nature생존State source, out 관측사본 value, out string error)
            {
                value = null; error = "ObservationInvalid";
                if (source == null || string.IsNullOrWhiteSpace(source.SessionStableId)
                    || string.IsNullOrWhiteSpace(source.PlayerStableId) || source.Revision < 0 || source.ResourceNodes == null)
                    return false;
                var work = source.ActiveWork;
                var copy = new 관측사본
                {
                    Session = source.SessionStableId, Player = source.PlayerStableId, Revision = source.Revision,
                    Paused = source.ClockPaused, HasWork = work != null, Kind = work?.KindCode ?? string.Empty,
                    Actor = work?.ActorStableId ?? string.Empty, Origin = work?.OriginCommandId ?? string.Empty,
                    Target = work?.TargetStableId ?? string.Empty, Required = work?.RequiredSeconds ?? 0,
                    Completed = work?.CompletedSeconds ?? 0,
                };
                foreach (var node in source.ResourceNodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.StableId) || copy.nodes.ContainsKey(node.StableId))
                    { error = "ResourceNodeIdentityInvalid"; return false; }
                    copy.nodes.Add(node.StableId, node.StateCode);
                }
                if (copy.Harvest)
                {
                    if (string.IsNullOrWhiteSpace(copy.Actor) || string.IsNullOrWhiteSpace(copy.Origin)
                        || string.IsNullOrWhiteSpace(copy.Target)) { error = "TaskIdentityMissing"; return false; }
                    if (copy.Actor != copy.Player) { error = "ActorBindingMismatch"; return false; }
                    if (copy.Required != 4 || copy.Completed < 0 || copy.Completed >= copy.Required)
                    { error = "TaskProgressInvalid"; return false; }
                    if (copy.NodeState(copy.Target) != Nature생존Codes.Standing)
                    { error = "ActiveHarvestTargetNotStanding"; return false; }
                }
                value = copy; error = string.Empty; return true;
            }
        }
    }
}
