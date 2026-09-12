using System;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    public enum 벌목WorkPhase { Idle, Working, Completed, Cancelled }
    public enum 벌목ReturnIntent { Idle, Walk }
    public enum 벌목PlaybackSegment { Idle, WindUp, AwaitingProgress, Hit, FollowThrough, ReturnToIdle }

    /// <summary>개발의 권위 조회 결과를 복사한 입력. 세대와 진행 토큰은 호출자가 제공한다.</summary>
    public sealed class 벌목AnimationSnapshot
    {
        public 벌목AnimationSnapshot(string sessionStableId, string actorStableId, string targetStableId,
            long observationGeneration, long revision, int requiredSeconds, int completedSeconds,
            벌목WorkPhase phase, long progressToken = 0, 벌목ReturnIntent returnIntent = 벌목ReturnIntent.Idle)
        {
            SessionStableId = sessionStableId; ActorStableId = actorStableId; TargetStableId = targetStableId;
            ObservationGeneration = observationGeneration; Revision = revision;
            RequiredSeconds = requiredSeconds; CompletedSeconds = completedSeconds;
            Phase = phase; ProgressToken = progressToken; ReturnIntent = returnIntent;
        }
        public string SessionStableId { get; }
        public string ActorStableId { get; }
        public string TargetStableId { get; }
        public long ObservationGeneration { get; }
        public long Revision { get; }
        public int RequiredSeconds { get; }
        public int CompletedSeconds { get; }
        public 벌목WorkPhase Phase { get; }
        public long ProgressToken { get; }
        public 벌목ReturnIntent ReturnIntent { get; }
        internal bool IsValid => !string.IsNullOrWhiteSpace(SessionStableId)
            && !string.IsNullOrWhiteSpace(ActorStableId) && TargetStableId != null
            && (Phase == 벌목WorkPhase.Idle || !string.IsNullOrWhiteSpace(TargetStableId))
            && ObservationGeneration >= 0 && Revision >= 0 && ProgressToken >= 0
            && RequiredSeconds >= 0 && CompletedSeconds >= 0 && CompletedSeconds <= RequiredSeconds
            && (Phase == 벌목WorkPhase.Idle || RequiredSeconds == 4)
            && (Phase != 벌목WorkPhase.Completed || CompletedSeconds == RequiredSeconds)
            && (Phase != 벌목WorkPhase.Working || CompletedSeconds < RequiredSeconds)
            && Enum.IsDefined(typeof(벌목WorkPhase), Phase) && Enum.IsDefined(typeof(벌목ReturnIntent), ReturnIntent);
        internal bool Same(벌목AnimationSnapshot other) => SessionStableId == other.SessionStableId
            && ActorStableId == other.ActorStableId && TargetStableId == other.TargetStableId
            && ObservationGeneration == other.ObservationGeneration && Revision == other.Revision
            && RequiredSeconds == other.RequiredSeconds && CompletedSeconds == other.CompletedSeconds
            && Phase == other.Phase && ProgressToken == other.ProgressToken && ReturnIntent == other.ReturnIntent;
    }

    public sealed class 벌목ContactCue
    {
        internal 벌목ContactCue(벌목AnimationSnapshot snapshot) { Snapshot = snapshot; }
        public 벌목AnimationSnapshot Snapshot { get; }
        public long ProgressToken => Snapshot.ProgressToken;
        public string StableId => $"woodcut:{Snapshot.SessionStableId}:{Snapshot.ActorStableId}:"
            + $"{Snapshot.TargetStableId}:{Snapshot.ObservationGeneration}:{ProgressToken}";
        public bool PresentationOnly => true;
    }

    public readonly struct 벌목PlaybackState
    {
        internal 벌목PlaybackState(벌목AnimationSnapshot snapshot, 벌목PlaybackSegment segment,
            float normalizedTime, long pendingToken, long consumedToken, bool returnReady, string diagnostic)
        {
            Snapshot = snapshot; Segment = segment; NormalizedTime = normalizedTime;
            PendingProgressToken = pendingToken; ConsumedProgressToken = consumedToken;
            ReturnReady = returnReady; Diagnostic = diagnostic;
        }
        public 벌목AnimationSnapshot Snapshot { get; }
        public 벌목PlaybackSegment Segment { get; }
        public float NormalizedTime { get; }
        public long PendingProgressToken { get; }
        public long ConsumedProgressToken { get; }
        public bool ReturnReady { get; }
        public string Diagnostic { get; }
        public bool PresentationOnly => true;
    }

    /// <summary>Unity/시계/권위 쓰기가 없는 상태 소비기. 확인되지 않은 타격을 만들지 않는다.</summary>
    public sealed class 벌목AnimationModel
    {
        private 벌목AnimationSnapshot snapshot;
        private 벌목AnimationSnapshot pending;
        private 벌목AnimationSnapshot striking;
        private 벌목ContactCue contact;
        private 벌목PlaybackSegment segment;
        private float elapsed;
        private long observedToken;
        private long consumedToken;
        private bool interrupted;
        private string diagnostic = "NotObserved";
        public const float WindUpSeconds = .35f;
        public const float HitSeconds = .20f;
        public const float FollowThroughSeconds = .20f;
        public const float ReturnSeconds = .25f;
        public const float InterruptSeconds = .15f;

        public bool ApplySnapshot(벌목AnimationSnapshot value)
        {
            if (value == null || !value.IsValid) return Reject("SnapshotInvalid");
            if (snapshot == null) return Reconstruct(value);
            if (value.SessionStableId != snapshot.SessionStableId || value.ActorStableId != snapshot.ActorStableId)
                return Reject("ReconstructRequired");
            if (value.ObservationGeneration < snapshot.ObservationGeneration || value.Revision < snapshot.Revision)
                return Reject("ObservationRegressed");
            if (value.Revision == snapshot.Revision)
            {
                if (!value.Same(snapshot)) return Reject("SameRevisionConflict");
                diagnostic = "DuplicateSnapshot";
                return true;
            }
            if (value.ObservationGeneration > snapshot.ObservationGeneration) return Reconstruct(value);
            if (value.TargetStableId != snapshot.TargetStableId || value.RequiredSeconds != snapshot.RequiredSeconds)
                return Reject("GenerationRequired");
            if (value.CompletedSeconds < snapshot.CompletedSeconds || (value.ProgressToken > 0 && value.ProgressToken < observedToken))
                return Reject("ProgressRegressed");
            if ((snapshot.Phase == 벌목WorkPhase.Completed || snapshot.Phase == 벌목WorkPhase.Cancelled)
                && value.Phase != snapshot.Phase) return Reject("TerminalGenerationRequired");
            bool newToken = value.ProgressToken > observedToken;
            if (newToken && (value.CompletedSeconds <= snapshot.CompletedSeconds
                || (value.Phase != 벌목WorkPhase.Working && value.Phase != 벌목WorkPhase.Completed)))
                return Reject("TokenWithoutProgress");
            var previousPhase = snapshot.Phase;
            snapshot = value;
            diagnostic = string.Empty;
            if (value.Phase == 벌목WorkPhase.Cancelled || value.Phase == 벌목WorkPhase.Idle)
            {
                if (previousPhase != value.Phase) InterruptToIdle();
                return true;
            }
            if (newToken)
            {
                observedToken = value.ProgressToken;
                pending = value; // 여러 초가 건너뛰어도 최신 한 건만 보유한다.
            }
            if (value.Phase == 벌목WorkPhase.Completed && previousPhase != value.Phase && pending == null && striking == null)
                Begin(벌목PlaybackSegment.ReturnToIdle);
            return true;
        }

        public bool Reconstruct(벌목AnimationSnapshot value)
        {
            if (value == null || !value.IsValid) return Reject("SnapshotInvalid");
            snapshot = value;
            pending = null; striking = null; contact = null;
            observedToken = value.ProgressToken; consumedToken = 0; interrupted = false;
            diagnostic = "BaselineNoContactReplay";
            Begin(value.Phase == 벌목WorkPhase.Working ? 벌목PlaybackSegment.WindUp : 벌목PlaybackSegment.Idle);
            return true;
        }

        public void InterruptToIdle()
        {
            pending = null; striking = null; contact = null; interrupted = true;
            Begin(벌목PlaybackSegment.ReturnToIdle);
        }

        public void Reset()
        {
            snapshot = null; pending = null; striking = null; contact = null;
            observedToken = consumedToken = 0; interrupted = false;
            diagnostic = "NotObserved"; Begin(벌목PlaybackSegment.Idle);
        }

        public bool TickPresentation(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                return Reject("DeltaTimeInvalid");
            // 장시간 중단 후 과거 주기를 몰아서 재생하지 않는다. 시간은 표현에만 쓴다.
            float remaining = Math.Min(deltaTime, .25f);
            for (int guard = 0; guard < 12; guard++)
            {
                if (segment == 벌목PlaybackSegment.Idle) return true;
                if (segment == 벌목PlaybackSegment.AwaitingProgress)
                {
                    if (pending == null) return true;
                    striking = pending; pending = null; Begin(벌목PlaybackSegment.Hit);
                }
                float duration = Duration();
                float step = Math.Min(remaining, Math.Max(0, duration - elapsed));
                elapsed += step; remaining -= step;
                if (segment == 벌목PlaybackSegment.Hit && striking != null
                    && elapsed >= HitSeconds * .5f && striking.ProgressToken > consumedToken)
                {
                    consumedToken = striking.ProgressToken;
                    contact = new 벌목ContactCue(striking); // 단일 소비 슬롯, 지난 알림을 누적하지 않는다.
                }
                if (elapsed + .000001f < duration) return true;
                switch (segment)
                {
                    case 벌목PlaybackSegment.WindUp: Begin(벌목PlaybackSegment.AwaitingProgress); break;
                    case 벌목PlaybackSegment.Hit: Begin(벌목PlaybackSegment.FollowThrough); break;
                    case 벌목PlaybackSegment.FollowThrough: striking = null; Begin(벌목PlaybackSegment.ReturnToIdle); break;
                    case 벌목PlaybackSegment.ReturnToIdle:
                        Begin(!interrupted && snapshot != null && (snapshot.Phase == 벌목WorkPhase.Working || pending != null)
                            ? 벌목PlaybackSegment.WindUp : 벌목PlaybackSegment.Idle);
                        break;
                }
                if (remaining <= 0) return true;
            }
            return Reject("TransitionLimit");
        }

        public bool TryConsumeContact(out 벌목ContactCue value)
        {
            value = contact;
            contact = null;
            return value != null;
        }

        public 벌목PlaybackState ReadState() => new 벌목PlaybackState(snapshot, segment,
            segment == 벌목PlaybackSegment.AwaitingProgress ? 1 : Math.Min(1, elapsed / Duration()),
            pending?.ProgressToken ?? 0, consumedToken,
            segment == 벌목PlaybackSegment.Idle && (interrupted || snapshot == null || snapshot.Phase != 벌목WorkPhase.Working), diagnostic);

        private float Duration() => segment switch
        {
            벌목PlaybackSegment.WindUp => WindUpSeconds,
            벌목PlaybackSegment.Hit => HitSeconds,
            벌목PlaybackSegment.FollowThrough => FollowThroughSeconds,
            벌목PlaybackSegment.ReturnToIdle => interrupted ? InterruptSeconds : ReturnSeconds,
            _ => 1f,
        };
        private void Begin(벌목PlaybackSegment value) { segment = value; elapsed = 0; }
        private bool Reject(string value) { diagnostic = value; return false; }
    }
}
