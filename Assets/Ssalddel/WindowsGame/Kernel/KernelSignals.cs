namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-12 위험 신호(G3c). 업무 모듈이 올리고 커널이 보관·저장한다. 모듈은 화면을 직접 부르지 않는다.
    // 신호는 조건이 이어지는 동안 열려 있다. 모듈이 조건을 다시 볼 때마다 같은 ID로 올리면 처음 올린 시각이 유지된다.

    public static class SignalSeverities
    {
        /// <summary>주의: 곧 문제가 된다.</summary>
        public const string Caution = "caution";
        /// <summary>위험: 지금 손봐야 한다.</summary>
        public const string Danger = "danger";
    }

    public sealed class Signal
    {
        public string Id = string.Empty;
        public string OsId = string.Empty;
        /// <summary>울리는 자리. 건물 역할의 placeId("store:…", "place:…") 또는 법정동("dong:" + 10자리 코드).</summary>
        public string PlaceId = string.Empty;
        public string Severity = SignalSeverities.Caution;
        public string ReasonCode = string.Empty;
        public string Title = string.Empty;
        public string Message = string.Empty;
        /// <summary>화면의 단추에 쓸 짧은 말. 비어 있으면 "가서 보기"만 보인다.</summary>
        public string ActionLabel = string.Empty;
        public long SinceMs;
        /// <summary>위험으로 올라간 가상 시각. 0이면 아직 주의다.</summary>
        public long EscalatedMs;
        public long ResolvedMs;

        public bool IsDanger { get { return Severity == SignalSeverities.Danger; } }
    }
}
