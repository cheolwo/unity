using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;

namespace Ssalddel.WindowsGame
{
    // 다음 할 일 · 이번 턴 계획의 순수 검사 (2026-11-16). 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 · 화면 없이 돈다.
    internal static class 다음할일Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            const string 카페 = "place:cafe";
            할일원천 신호(string 키, string 업무, string 사유, bool 위험, string 단추, long 시작, string 장소 = 카페, string 주문 = null) => new 할일원천
            {
                키 = 키, 업무 = 업무, 사유 = 사유, 위험 = 위험, 단추말 = 단추, 시작Ms = 시작, 판본 = 시작, 장소 = 장소, 제목 = 키, 주문Id = 주문,
            };
            (string, string) 자리(할일원천 원천) => (원천.업무 == BusinessIds.StoreOperations || 원천.사슬 ? 카페 : null,
                원천.장소.StartsWith("dong:") ? 원천.장소.Substring(5) : null);

            var 원천 = new List<할일원천>
            {
                신호("signal:food:waiting:o2", BusinessIds.FoodDelivery, "dispatch-waiting", false, "기사 지정", 300, "store:a", "o2"),
                신호("signal:store:stock:" + 카페, BusinessIds.StoreOperations, "stock-out", true, "보충 발주", 500),
                신호("signal:dong:7", BusinessIds.DongDemand, DongDemandBottlenecks.Rider, false, "기사 모집", 100, "dong:7"),
                신호("signal:store:stock-in", BusinessIds.StoreOperations, "stock-inbound", false, string.Empty, 50),
                신호("signal:territory:x", BusinessIds.Territory, "kitchen-delay", false, "주문 잠시 닫기", 40, "dong:9"),
                new 할일원천 { 키 = "chain:order", 업무 = BusinessIds.StoreOperations, 사유 = "order", 사슬 = true, 장소 = 카페, 시작Ms = 0 },
            };
            var 항목 = 다음할일Policy.모으기(원천, 자리, null);
            Check("위험 신호에 고를 것이 있으면 위험 알림", 항목.Any(i => i.묶음 == 할일묶음.위험 && i.명령키 == 다음할일Policy.발주키(카페)));
            Check("주의 신호의 기사 지정 · 기사 모집은 고를 일",
                항목.Count(i => i.묶음 == 할일묶음.고를일) == 2 && 항목.Any(i => i.명령키 == 다음할일Policy.배차키("o2")) &&
                항목.Any(i => i.명령키 == 다음할일Policy.모집키("7")));
            Check("단추 말이 없는 신호와 모르는 사유는 지켜볼 것(고를 것을 지어내지 않음)",
                항목.Count(i => i.묶음 == 할일묶음.지켜볼것) == 2 && 항목.Where(i => i.묶음 == 할일묶음.지켜볼것).All(i => i.명령키 == null));
            Check("사슬의 같은 행동은 같은 매장 신호 하나로 합침", 항목.Count(i => i.명령키 == 다음할일Policy.발주키(카페)) == 1 &&
                항목.First(i => i.명령키 == 다음할일Policy.발주키(카페)).키 == "signal:store:stock:" + 카페);
            Check("버튼의 수는 위험 + 고를 일", 다음할일Policy.셀수(항목) == 3);
            Check("위험 → 고를 일 → 지켜볼 것, 묶음 안에서는 오래된 것 먼저",
                항목[0].묶음 == 할일묶음.위험 && 항목[1].명령키 == 다음할일Policy.모집키("7") && 항목[2].명령키 == 다음할일Policy.배차키("o2") &&
                항목[3].묶음 == 할일묶음.지켜볼것);
            Check("배차 대기는 배차 데스크, 재고는 매장 재료 탭, 기사 모자람은 동 관리",
                항목.First(i => i.명령키 == 다음할일Policy.배차키("o2")).창 == 할일창.배차데스크 &&
                항목.First(i => i.명령키 == 다음할일Policy.발주키(카페)).창 == 할일창.매장재료 &&
                항목.First(i => i.명령키 == 다음할일Policy.모집키("7")).창 == 할일창.동관리);
            var 사슬만 = 다음할일Policy.모으기(new[] { new 할일원천 { 키 = "chain:inspect", 업무 = BusinessIds.StoreOperations, 사유 = "inspect", 사슬 = true } }, 자리, null);
            Check("신호가 없어도 사슬의 다음 행동은 고를 일", 사슬만.Count == 1 && 사슬만[0].묶음 == 할일묶음.고를일 &&
                사슬만[0].명령키 == 다음할일Policy.검수키(카페));

            // 이번 턴 계획
            var 계획 = new 이번턴계획();
            var 새로 = 계획.담기(new 턴계획항목 { 명령키 = 다음할일Policy.배차키("o2"), 종류 = 계획종류.배차, 기사 = "A" });
            계획.담기(new 턴계획항목 { 명령키 = 다음할일Policy.발주키(카페), 종류 = 계획종류.발주, 수량 = 5 });
            var 바꿈 = !계획.담기(new 턴계획항목 { 명령키 = 다음할일Policy.배차키("o2"), 종류 = 계획종류.배차, 기사 = "B" });
            Check("같은 명령 키를 다시 담으면 바뀌고 쌓이지 않음", 새로 && 바꿈 && 계획.Count == 2 &&
                계획.찾기(다음할일Policy.배차키("o2")).기사 == "B" && 계획.찾기(다음할일Policy.배차키("o2")).순번 == 1);
            Check("같은 기사를 다른 배차에 또 담으려 하면 겹침", 계획.기사겹침("B", 다음할일Policy.배차키("o9")) &&
                !계획.기사겹침("B", 다음할일Policy.배차키("o2")) && !계획.기사겹침("C", null));
            var 순서 = 계획.실행순서(키 => 항목.FirstOrDefault(i => i.명령키 == 키)?.묶음);
            Check("넘길 때는 위험 먼저, 그다음 담은 순서", 순서.Count == 2 && 순서[0].명령키 == 다음할일Policy.발주키(카페) &&
                순서[1].명령키 == 다음할일Policy.배차키("o2"));
            var 남음 = 다음할일Policy.고르지않은(항목, 계획);
            Check("계획에 없는 고를 일만 고르지 않은 일(지켜볼 것 제외)", 남음.Count == 1 && 남음[0].명령키 == 다음할일Policy.모집키("7"));
            Check("빼면 다시 고르지 않은 일로", 계획.빼기(다음할일Policy.배차키("o2")) && 계획.Count == 1 &&
                다음할일Policy.고르지않은(항목, 계획).Count == 2 && !계획.빼기("none"));

            // 미룸: 다음 턴에도 열려 있으면 꼬리표, 해결됐으면 목록에서 사라짐
            var 미룬 = new HashSet<string> { 다음할일Policy.모집키("7"), 다음할일Policy.배차키("gone") };
            var 다음턴 = 다음할일Policy.모으기(원천, 자리, 미룬);
            Check("미룬 일이 그대로 열려 있으면 「지난 턴 미룸」", 다음턴.First(i => i.명령키 == 다음할일Policy.모집키("7")).지난턴미룸 &&
                다음턴.Count(i => i.지난턴미룸) == 1);

            // 결과 읽기
            Check("발주 진행: 센터 → 화물 → 도착(검수는 직접) → 완료",
                다음할일Policy.발주진행(false, false, false, false, false, false).칸 == 1 &&
                다음할일Policy.발주진행(true, false, false, false, false, false).상태 == 결과상태.진행중 &&
                다음할일Policy.발주진행(true, true, true, false, false, false).칸 == 2 &&
                다음할일Policy.발주진행(true, true, true, true, true, false).상태 == 결과상태.대기 &&
                다음할일Policy.발주진행(true, true, true, true, false, true).상태 == 결과상태.완료);
            Check("배달 진행: 고른 기사면 진행 · 다른 기사면 실패 · 정산이면 완료",
                다음할일Policy.배달진행("Assigned", "A", "A").상태 == 결과상태.진행중 &&
                다음할일Policy.배달진행("AtPickup", "A", "A").칸 == 2 &&
                다음할일Policy.배달진행("Assigned", "B", "A").상태 == 결과상태.실패 &&
                다음할일Policy.배달진행("Settled", "A", "A").상태 == 결과상태.완료 &&
                다음할일Policy.배달진행(null, null, "A").상태 == 결과상태.실패 &&
                다음할일Policy.배달진행("Waiting", null, "A").상태 == 결과상태.대기);
            Check("시계는 화에서 안내판 · 계획이 열린 동안만 멈춤",
                다음할일Policy.시계멈춤(true, true, 0) && 다음할일Policy.시계멈춤(true, false, 1) &&
                !다음할일Policy.시계멈춤(true, false, 0) && !다음할일Policy.시계멈춤(false, true, 3));
            return results;
        }
    }
}
