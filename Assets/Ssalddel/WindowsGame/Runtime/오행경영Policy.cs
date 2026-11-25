using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 00 경영 — 오행에서 위→아래로 내려가는 화면의 순수 규칙 (2026-11-17, Claude 제작실).
    // 계획: Hongdal docs/AI/Handoffs/2026-11-17-Claude-개발계획-오행총괄판-r2-하향식.md (T1~T3 첫 묶음)
    // 기획: r259 탑다운 연결 · r261 00 루트와 기본 관점(목=주문 · 화=음식점 · 토=배차 · 금=창고 · 수=배달/운송) · r267 자율 모집.
    // 사용자(11-17): 「오행은 그냥 색깔로서 상징으로 남겨놓고 … 괄호 열고 괄호 닫고 … 오행이라는 화면에서 하향식으로 내려갈 수 있도록」.
    //  - 화면에는 업무 이름이 먼저, 오행은 「배차(토)」처럼 괄호와 색으로만 쓴다. 오행으로 숫자를 만들거나 더하지 않는다.
    //  - 경로(00 → 지사 → 기사 → 주문)는 보기 위한 것이다. 내려가기 · 위로 · 00으로는 조회일 뿐 배차 · 고용 · 정산을 바꾸지 않는다.
    //  - 기사의 소속 경로는 기사가 지금 있는 동이 바뀌어도 그대로다. 기사에게 동 이동을 지시하지 않는다(r267).
    //  - Unity 없이 검사한다(오행경영Checks).
    internal enum 오행 { 목, 화, 토, 금, 수 }

    internal enum 경영층 { 루트, 지사, 기사, 주문 }

    // 「아래로」 한 줄이 가는 곳. 안쪽 = 00 판 안에서 한 층 내려감, 나머지 = 지금 있는 창을 연다.
    internal enum 경영갈곳
    {
        지사안쪽,        // 07 1호지사 (판 안)
        진행주문안쪽,    // 지금 진행 중인 배달 한 건 (판 안)
        대기주문안쪽,    // 기다리는 주문 한 건 (판 안)
        지사창,          // 07 지사 화면 (지금의 플랫폼 장부 지사 쪽)
        배차창,          // 08 배차 데스크
        내매장창,        // 09 매장 운영
        매장재료창,      // 09-2 재료 · 검수
        동관리음식점,    // 06 동 관리 · 제휴 음식점
        플레이스창,      // 10 홍달 플레이스(주문자 화면)
        결산창,          // 14 오늘 결산
        본사장부창,      // 15 본사 장부
        다음할일창,      // 12 다음 할 일
        다음단계,        // 아직 연결 전 — 이유만 보인다
    }

    internal sealed class 경영아래로
    {
        internal readonly string 이름;
        internal readonly string 번호;
        internal readonly 경영갈곳 갈곳;
        internal 경영아래로(string 이름, string 번호, 경영갈곳 갈곳) { this.이름 = 이름; this.번호 = 번호; this.갈곳 = 갈곳; }
    }

    internal sealed class 경영열
    {
        internal readonly 오행 행;
        internal readonly string 업무;
        internal readonly 경영아래로[] 아래로;
        internal 경영열(오행 행, string 업무, params 경영아래로[] 아래로) { this.행 = 행; this.업무 = 업무; this.아래로 = 아래로; }
    }

    internal sealed class 경로마디
    {
        internal readonly 경영층 층;
        internal readonly string 대상;      // 지사 이름 · 기사 id · 주문 id. 루트는 null
        internal readonly string 이름;      // 경로 줄에 쓰는 이름
        internal 경로마디(경영층 층, string 대상, string 이름) { this.층 = 층; this.대상 = 대상; this.이름 = 이름; }
    }

    // 00에서 아래로 내려간 경로. 맨 아래가 지금 보는 층이다. 위로 · 00으로만 줄어든다.
    internal sealed class 경영경로
    {
        private readonly List<경로마디> 마디 = new() { new 경로마디(경영층.루트, null, "00 경영") };

        internal IReadOnlyList<경로마디> 마디들 => 마디;
        internal 경로마디 지금 => 마디[마디.Count - 1];
        internal int 깊이 => 마디.Count - 1;

        // 한 층 아래로. 층은 루트 → 지사 → 기사 → 주문 순서로만 깊어진다(주문은 루트 · 지사 · 기사 어디서든 바로 갈 수 있다).
        // 같은 층이나 더 위 층으로 「내려가기」를 부르면 그 층까지 올라간 뒤 바꿔 끼운다.
        internal bool 내려가기(경영층 층, string 대상, string 이름)
        {
            if (층 == 경영층.루트 || string.IsNullOrEmpty(대상)) return false;
            while (마디.Count > 1 && 지금.층 >= 층) 마디.RemoveAt(마디.Count - 1);
            마디.Add(new 경로마디(층, 대상, string.IsNullOrEmpty(이름) ? 대상 : 이름));
            return true;
        }

        internal bool 위로()
        {
            if (마디.Count <= 1) return false;
            마디.RemoveAt(마디.Count - 1);
            return true;
        }

        internal void 맨위로()
        {
            if (마디.Count > 1) 마디.RemoveRange(1, 마디.Count - 1);
        }

        // 경로 줄의 한 조각을 누름: 그 층으로 올라간다.
        internal bool 여기로(int index)
        {
            if (index < 0 || index >= 마디.Count - 1) return false;
            마디.RemoveRange(index + 1, 마디.Count - index - 1);
            return true;
        }

        // 대상이 사라졌으면(주문 정리 · 기사 목록 변경) 그 마디부터 걷어 낸다. 걷어 냈으면 true.
        internal bool 사라진대상정리(Func<경로마디, bool> 있나)
        {
            for (var i = 1; i < 마디.Count; i++)
            {
                if (있나(마디[i])) continue;
                마디.RemoveRange(i, 마디.Count - i);
                return true;
            }
            return false;
        }

        internal string 줄글 => string.Join(" › ", 마디.Select(item => item.이름));
    }

    internal static class 오행경영Policy
    {
        internal static readonly 오행[] 순서 = { 오행.목, 오행.화, 오행.토, 오행.금, 오행.수 };

        // r261 기본 관점. 업무 이름이 먼저이고 오행은 괄호로.
        internal static readonly 경영열[] 열 =
        {
            new(오행.목, "주문",
                new 경영아래로("기다리는 주문 한 건", "08", 경영갈곳.대기주문안쪽),
                new 경영아래로("배차 데스크", "08", 경영갈곳.배차창),
                new 경영아래로("주문자 화면 · 플레이스", "10", 경영갈곳.플레이스창)),
            new(오행.화, "음식점",
                new 경영아래로("내 매장", "09", 경영갈곳.내매장창),
                new 경영아래로("제휴 음식점 · 동 관리", "06", 경영갈곳.동관리음식점)),
            new(오행.토, "배차",
                new 경영아래로("1호지사", "07", 경영갈곳.지사안쪽),
                new 경영아래로("배차 데스크", "08", 경영갈곳.배차창),
                new 경영아래로("지사 화면", "07", 경영갈곳.지사창)),
            new(오행.금, "창고",
                new 경영아래로("검수 · 재료", "09-2", 경영갈곳.매장재료창),
                new 경영아래로("센터 · 화물 관찰", "11", 경영갈곳.다음단계)),
            new(오행.수, "배달·운송",
                new 경영아래로("진행 중인 배달 한 건", "11-1", 경영갈곳.진행주문안쪽),
                new 경영아래로("오늘 결산", "14", 경영갈곳.결산창)),
        };

        internal static string 글자(오행 행) => 행 switch
        {
            오행.목 => "목", 오행.화 => "화", 오행.토 => "토", 오행.금 => "금", _ => "수",
        };

        // 「배차(토)」
        internal static string 표기(string 업무, 오행 행) => 업무 + "(" + 글자(행) + ")";

        internal static 경영열 열찾기(오행 행) => 열.First(item => item.행 == 행);

        // 주문 한 건의 단계. 화면의 단계 줄: 주문(목) → 배정(토) → 픽업(수) → 전달(수).
        internal static readonly (string 이름, 오행 행)[] 주문단계 =
        {
            ("주문", 오행.목), ("배정", 오행.토), ("픽업", 오행.수), ("전달", 오행.수),
        };

        // 주문 상태(Core의 stage) → 지금 몇 번째 단계까지 왔나. -1 = 모름 · 취소.
        internal static int 단계순번(string stage) => stage switch
        {
            "Waiting" => 0,
            "Assigned" or "Accepted" => 1,
            "ToPickup" or "AtPickup" or "ToDropoff" => 2,
            "Delivered" or "Received" or "Settled" => 3,
            _ => -1,
        };

        internal static bool 끝난주문(string stage) => stage is "Delivered" or "Received" or "Settled" or "Cancelled";

        internal static bool 진행중(string stage) => 단계순번(stage) is 1 or 2;
    }
}
