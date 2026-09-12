using System;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>업무 서버 재조회 결과. Simulation Session이나 업무 실행 권한이 아니다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "같은 주문의 서버 관찰 결과", Boundary = "Unity가 주문·배차·완료를 확정하지 않는다")]
    public sealed class 업무배달관찰Model
    {
        public string RunId = "", Status = "Idle", OrderNo = "", OrderStatus = "", DispatchStatus = "", Message = "";
        public long Revision;
        public double ElapsedSeconds, DurationSeconds;
        public 업무배달관찰Actor[] Actors = Array.Empty<업무배달관찰Actor>();
        public 업무배달관찰Event[] Events = Array.Empty<업무배달관찰Event>();
        public 업무배달메뉴선택 MenuSelection;
        public bool IsTerminal => Status == "Completed" || Status == "Failed" || Status == "TimedOut" || Status == "Waiting";
    }
    public sealed class 업무배달메뉴선택
    {
        public string SnapshotHash = "", Title = "", Reason = "", Source = "", Reference = "", PriceBasis = "";
        public long RecipeId, MenuId;
        public double SalePrice;
        public string[] Candidates = Array.Empty<string>();
    }
    public sealed class 업무배달관찰Actor
    {
        public string Id = "", Name = "", Role = "", State = "", NextAction = "", WaitReason = "", LastAction = "";
        public float X, Z;
    }
    public sealed class 업무배달관찰Event
    {
        public long Sequence;
        public string ActorId = "", Action = "", Result = "", OccurredAtUtc = "";
    }

    /// <summary>최신 성공 상태를 보존한다. 낮은 판본·다른 주문·예기치 않은 실행 교체는 거부한다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "조회 판본·주문 식별자·연결 상태 검증", Boundary = "오류 시 마지막 성공 결과를 오래된 자료로 표시하며 fallback을 만들지 않는다")]
    public sealed class 업무배달관찰Store
    {
        public 업무배달관찰Model State { get; private set; }
        public DateTime? LastSuccessUtc { get; private set; }
        public string Error { get; private set; } = "첫 조회 대기";
        public bool InFlight { get; private set; }
        public 업무배달관찰Model PendingIdle { get; private set; }
        private DateTime pendingIdleUtc;
        private bool allowNewRun;
        public bool TryBegin() { if (InFlight) return false; InFlight = true; return true; }
        public void End() => InFlight = false;
        public void AllowExplicitStart()
        { if (State?.Status != "Idle") throw new InvalidOperationException("ObserverStartRequiresIdle"); allowNewRun = true; }
        public bool Accept(업무배달관찰Model incoming, DateTime now)
        {
            if (incoming == null) { Fail("서버 조회 결과 없음"); return false; }
            if (incoming.Status != "Idle") PendingIdle = null;
            if (State != null)
            {
                if (State.RunId == incoming.RunId && incoming.Revision < State.Revision)
                { Fail("오래된 조회 거부 · 기존 판본 유지"); return false; }
                if (State.RunId != incoming.RunId && State.RunId.Length > 0 && !allowNewRun)
                {
                    if (State.IsTerminal && incoming.Status == "Idle" && incoming.RunId.Length == 0 && incoming.OrderNo.Length == 0)
                    { PendingIdle = incoming; pendingIdleUtc = now; Fail("새 서버 결과 확인 필요 · 기존 결과 유지"); }
                    else Fail("다른 실행 ID · 기존 결과 유지");
                    return false;
                }
                if (State.RunId == incoming.RunId && State.OrderNo.Length > 0 && State.OrderNo != incoming.OrderNo)
                { Fail("주문번호 불일치 · 갱신 거부"); return false; }
                if (State.RunId == incoming.RunId && State.MenuSelection != null &&
                    (incoming.MenuSelection == null || State.MenuSelection.SnapshotHash != incoming.MenuSelection.SnapshotHash
                    || State.MenuSelection.MenuId != incoming.MenuSelection.MenuId || State.MenuSelection.RecipeId != incoming.MenuSelection.RecipeId))
                { Fail("메뉴 자료 결속 불일치 · 갱신 거부"); return false; }
            }
            State = incoming; LastSuccessUtc = now; Error = ""; allowNewRun = false; PendingIdle = null; return true;
        }
        public bool CanAcceptPendingIdle(DateTime now) => !InFlight && PendingIdle != null && State != null && State.IsTerminal
            && Error == "새 서버 결과 확인 필요 · 기존 결과 유지" && (now - pendingIdleUtc).TotalSeconds <= 3;
        public bool AcceptPendingIdle(DateTime now)
        {
            if (!CanAcceptPendingIdle(now)) return false;
            State = PendingIdle; LastSuccessUtc = pendingIdleUtc; PendingIdle = null; Error = ""; allowNewRun = false; return true;
        }
        public void Fail(string safeMessage) => Error = safeMessage;
        public string ConnectionLabel(DateTime now)
        {
            if (!LastSuccessUtc.HasValue) return Error.Length > 0 ? Error : "서버 조회 중";
            var age = Math.Max(0, (now - LastSuccessUtc.Value).TotalSeconds);
            if (Error.Length > 0) return $"갱신 실패 · 마지막 성공 {age:0}초 전 · {Error}";
            return age > 3 ? $"오래된 조회 · {age:0}초 전 · 재시도 중" : "업무 서버 연결 · 재조회 확인";
        }
        public static string 상태이름(string status) => status switch {
            "Idle" => "시작 대기", "Running" => "실행 중", "Completed" => "검증 완료",
            "Failed" => "실행 실패", "Waiting" => "메뉴 조건 대기 · 주문 없음", "TimedOut" => "5분 종료 · 미완료", _ => "알 수 없는 상태" };
        public static string 역할이름(string role) => role switch { "orderer" => "주문자", "restaurant" => "음식점", "driver" => "기사", _ => "관찰 대상" };
    }
}
