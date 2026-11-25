using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 배달 플랫폼 패키지 뼈대(2026-11-16, 개발계획 「배달 플랫폼 패키지」 단계 0). 순수 규칙: 패키지 표 · 아래 줄 메뉴 · 지사 = 동 묶음 · 시간대별 필요 기사.
    //  - 화면 · 시계 · 커널을 모른다. 검사(배달플랫폼Checks)가 이것만 돌린다.
    //  - 지사는 아직 커널의 단위가 아니다. 지금 운영 중인 행정동을 한 지사로 묶어 보여 주는 「보기」다(Kernel 변경 없음).
    //  - 모르는 값은 null로 돌려준다. 화면은 0이 아니라 「자료 준비 전」으로 쓴다.
    internal enum 패키지종류 { 없음, 배달플랫폼, 프랜차이즈, 창고물류, 자유시작 }

    internal sealed class 패키지정의
    {
        internal 패키지종류 종류;
        internal string 저장값 = string.Empty;
        internal string 이름 = string.Empty;
        internal string 나는 = string.Empty;
        internal string 키우는것 = string.Empty;
        internal string 처음자리 = string.Empty;
        internal string 성과 = string.Empty;
        internal bool 열림;
    }

    internal sealed class 플랫폼동줄
    {
        internal string 코드 = string.Empty;
        internal string 이름 = string.Empty;
        internal bool 운영;
        internal int 기사;
        internal int 제휴;
        internal double 하루주문;
        internal double 놓친비율;      // 최근 창에서 놓친 비율(0~1)
    }

    internal sealed class 지사요약
    {
        internal string 이름 = string.Empty;
        internal readonly List<플랫폼동줄> 담당 = new();
        internal readonly List<플랫폼동줄> 지사밖 = new();
        internal int 기사;
        internal int 제휴;
        internal double 하루주문;
        internal double 지사밖하루주문;
        internal double? 받아냄;       // 담당 동의 하루 주문으로 가중한 최근 받아낸 비율. 담당 동이 없으면 null
    }

    internal static class 배달플랫폼Policy
    {
        internal const string 저장키 = "hongdal.package";
        internal const string 첫지사이름 = "사가정 지사";

        internal static readonly 패키지정의[] 패키지들 =
        {
            new() { 종류 = 패키지종류.배달플랫폼, 저장값 = "delivery-platform", 이름 = "배달 플랫폼", 나는 = "나는 가상 배달 플랫폼의 본사",
                키우는것 = "지사 → 기사 → 받아낸 주문 → 다음 지사", 처음자리 = "사가정 · 면목제3·8동 · 기사 2명 · 제휴 매장 2곳 (지금 게임의 시작과 같음)",
                성과 = "지사 수 · 소속 기사 · 받아낸 주문 · 본사 차액", 열림 = true },
            new() { 종류 = 패키지종류.프랜차이즈, 저장값 = "franchise", 이름 = "프랜차이즈", 나는 = "나는 카페 본부",
                키우는것 = "직영점 → 가맹점 → 물류", 처음자리 = "카페 1호점 운영 창 · 가맹 개설 절차 기획", 성과 = "가맹점 수 · 점포 매출 · 절차 진행" },
            new() { 종류 = 패키지종류.창고물류, 저장값 = "logistics", 이름 = "창고 · 물류", 나는 = "나는 물류센터 운영자",
                키우는것 = "센터 → 화물 노선 → 거점", 처음자리 = "첫 인계 사슬(본부 발주 → 센터 출고 → 화물 운송 → 점포 검수)", 성과 = "제때 들어간 물량 · 재고 · 운송비" },
            new() { 종류 = 패키지종류.자유시작, 저장값 = "free-start", 이름 = "자유 시작", 나는 = "한 동에서 사업 하나로",
                키우는것 = "배달 · 카페 · 광고 · 전자상거래 · 무역 중 하나 (기획 r183 · r222)", 처음자리 = "고른 사업마다 다름", 성과 = "고른 사업마다 다름" },
        };

        // 배달 플랫폼 패키지의 아래 줄. 순서가 곧 자리다.
        internal static readonly string[] 플랫폼메뉴 = { "지사", "기사", "배차", "제휴 매장", "정산", "본사 장부" };

        // 동 수요표(hongdal-dong-demand.json)의 bandHours [0, 6, 11, 14, 17, 21, 24]와 같은 여섯 시간대
        internal static readonly (string 이름, int 시작, int 끝)[] 시간대 =
        {
            ("새벽", 0, 6), ("오전", 6, 11), ("점심", 11, 14), ("오후", 14, 17), ("저녁", 17, 21), ("밤", 21, 24),
        };

        internal static 패키지정의 정의(패키지종류 종류) => 패키지들.FirstOrDefault(item => item.종류 == 종류);

        internal static 패키지종류 읽기(string 저장값)
        {
            if (string.IsNullOrEmpty(저장값)) return 패키지종류.없음;
            var found = 패키지들.FirstOrDefault(item => item.저장값 == 저장값);
            return found != null && found.열림 ? found.종류 : 패키지종류.없음;      // 아직 열리지 않은 패키지는 고른 것으로 치지 않는다
        }

        internal static string 저장(패키지종류 종류) => 정의(종류)?.저장값 ?? string.Empty;

        // 한 시간대에 필요한 기사 수. 하루 주문 × 그 시간대 몫 ÷ 시간 = 시간당 주문, 이것을 기사 한 명의 처리량(엔진 배율 포함)으로 나눠 올린다.
        internal static int 필요기사(double 하루주문, double 몫, double 시간, double 기사시간당, double 엔진배율)
        {
            var perHour = 시간당주문(하루주문, 몫, 시간);
            var capacity = 기사시간당 * Math.Max(0.0001, 엔진배율);
            if (perHour <= 0 || capacity <= 0) return 0;
            return (int)Math.Ceiling(perHour / capacity - 1e-9);
        }

        internal static double 시간당주문(double 하루주문, double 몫, double 시간) =>
            하루주문 <= 0 || 몫 <= 0 || 시간 <= 0 ? 0 : 하루주문 * 몫 / 시간;

        // 지금 운영 중인 동을 한 지사로 묶는다. 나머지는 「지사 밖」이다.
        internal static 지사요약 지사묶기(IEnumerable<플랫폼동줄> 동들, string 이름 = 첫지사이름)
        {
            var 요약 = new 지사요약 { 이름 = 이름 };
            if (동들 == null) return 요약;
            double weight = 0, missed = 0;
            foreach (var 동 in 동들)
            {
                if (동 == null) continue;
                if (!동.운영)
                {
                    요약.지사밖.Add(동);
                    요약.지사밖하루주문 += Math.Max(0, 동.하루주문);
                    continue;
                }
                요약.담당.Add(동);
                요약.기사 += Math.Max(0, 동.기사);
                요약.제휴 += Math.Max(0, 동.제휴);
                var w = Math.Max(0, 동.하루주문);
                요약.하루주문 += w;
                weight += w;
                missed += w * Math.Min(1, Math.Max(0, 동.놓친비율));
            }
            요약.받아냄 = weight > 0 ? (double?)(1 - missed / weight) : null;
            요약.지사밖.Sort((a, b) => b.하루주문.CompareTo(a.하루주문));
            return 요약;
        }

        // 본사 띠의 「오늘 정할 일」. 많아야 셋. 바꾸는 것은 없고 글만 만든다.
        internal static List<string> 정할일(지사요약 지사, int 저녁필요, int 제휴부족동)
        {
            var list = new List<string>();
            if (지사 == null) return list;
            if (지사.담당.Count > 0 && 지사.기사 < 저녁필요)
                list.Add($"{지사.이름} · 저녁 기사 {저녁필요 - 지사.기사}명 부족");
            if (지사.지사밖.Count > 0)
                list.Add($"지사 밖 주문 하루 {지사.지사밖하루주문:0}건 · 다음 지사 후보");
            if (제휴부족동 > 0 && list.Count < 3)
                list.Add($"제휴 매장이 모자란 동 {제휴부족동}곳");      // 최근에 놓친 주문의 막힘이 기사가 아니라 제휴인 동
            return list;
        }

        internal static string 비율글(double? ratio) => ratio == null ? "자료 준비 전" : $"{Math.Round(ratio.Value * 100):0}%";
    }
}
