using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 역할 앱 휴대폰 재연 대본의 순수 검사 (2026-11-15). 빌드의 kernel-checks.txt에 붙는다.
    internal static class 역할앱재연Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            string Taps(List<재연장면> list) => string.Join(",", list.Where(scene => scene.Tap != null).Select(scene => scene.Tap));
            string Toasts(List<재연장면> list) => string.Join(",", list.Where(scene => scene.Toast != null).Select(scene => scene.Toast));
            bool Rising(List<재연장면> list)
            {
                for (var i = 1; i < list.Count; i++) if (list[i].Step < list[i - 1].Step) return false;
                return true;
            }

            // ── 홍달 기사 ──
            var accept = 역할앱재연Policy.RiderScript("Waiting", "Accepted", null);
            Check("배정 → 수락: 알림 · 요청 카드 · 「수락」 누름 · 수락 안내", accept.Count == 4 && accept[0].Push == "새 배달 요청" &&
                accept[1].Screen == 기사화면.요청 && accept[2].Tap == "수락" && accept[3].Toast == "배달을 수락했어요" && accept[3].Screen == 기사화면.진행);
            var move = 역할앱재연Policy.RiderScript("Accepted", "ToPickup", null);
            Check("픽업지 이동은 누르지 않고 진행 칸만 넘어감", move.Count == 1 && move[0].Tap == null && move[0].Step == 1);
            var run = 역할앱재연Policy.RiderScript("ToPickup", "Delivered", null);
            Check("도착 · 픽업 · 전달은 「현장 처리」를 차례로 세 번", Taps(run) == "현장 처리,현장 처리,현장 처리" &&
                Toasts(run) == "픽업지에 도착했어요,픽업을 완료했어요,전달을 완료했어요");
            Check("진행 칸은 뒤로 가지 않음", Rising(run) && run.Last().Step == 4);
            var settle = 역할앱재연Policy.RiderScript("Delivered", "Settled", "정산 · 3,600원 (가상)");
            Check("수령 확인은 기사 휴대폰을 바꾸지 않고, 정산은 알림 하나", settle.Count == 1 && settle[0].Push == "정산 · 3,600원 (가상)" &&
                settle[0].Screen == 기사화면.완료 && 역할앱재연Policy.RiderEnter("Received", null).Count == 0);
            var again = 역할앱재연Policy.RiderScript("Settled", "Assigned", null);
            Check("정산 뒤 새 주문은 대기에서 다시 시작", again.Count == 2 && again[0].Push == "새 배달 요청");
            Check("같은 단계 · 모르는 단계는 재연 없음", 역할앱재연Policy.RiderScript("ToPickup", "ToPickup", null).Count == 0 &&
                역할앱재연Policy.RiderScript("ToPickup", "Lost", null).Count == 0);
            var whole = 역할앱재연Policy.RiderScript(null, "Settled", null);
            Check("처음부터 정산까지 누르는 곳은 수락 한 번 · 현장 처리 세 번", Taps(whole) == "수락,현장 처리,현장 처리,현장 처리" &&
                whole.All(scene => scene.Seconds > 0 && scene.Seconds <= 2f));
            Check("머무를 때의 화면: 대기 · 요청 · 진행 칸 · 완료", 역할앱재연Policy.RiderScreen("Waiting").Screen == 기사화면.대기 &&
                역할앱재연Policy.RiderScreen("Assigned").Screen == 기사화면.요청 && 역할앱재연Policy.RiderScreen("AtPickup").Step == 2 &&
                역할앱재연Policy.RiderScreen("Settled").Screen == 기사화면.완료 && 역할앱재연Policy.RiderScreen(null).Screen == 기사화면.대기);
            var one = 역할앱재연Policy.Pace(1);
            var two = 역할앱재연Policy.Pace(2);
            var three = 역할앱재연Policy.Pace(3);
            Check("밀리면: 하나는 그대로 · 둘은 빠르게 · 셋부터 건너뜀", one.Speed == 1f && !one.Skip && two.Speed > 1f && !two.Skip && three.Skip);
            var skipped = 역할앱재연Policy.Skipped("ToDropoff", 3);
            Check("건너뛸 때는 마지막 화면과 「그사이 n가지 일」", skipped.Screen == 기사화면.진행 && skipped.Step == 3 && skipped.Toast == "그사이 3가지 일을 했어요");

            // ── 홍달 화물 ──
            var cargo = 역할앱재연Policy.CargoScript(null, "인수완료");
            Check("화물: 추천 수락 → 상차지 도착 → 상차 완료 → 하차지 도착 → 하차 완료 순서로 누름",
                Taps(cargo) == "추천 수락,상차지 도착,상차 완료,하차지 도착,하차 완료" && cargo[0].Push == "신규 배차 추천" &&
                cargo.Last().Screen == 화물화면.완료 && Rising(cargo));
            var haul = 역할앱재연Policy.CargoScript("상차완료", "운송중");
            Check("화물: 운송중은 누르지 않고 진행 칸만 넘어감", haul.Count == 1 && haul[0].Tap == null && haul[0].Step == 4);
            Check("화물: 머무를 때의 화면(대기 · 운송중 · 하차지 도착 · 인수 완료)", 역할앱재연Policy.CargoScreen(null).Screen == 화물화면.대기 &&
                역할앱재연Policy.CargoScreen("운송중").Step == 4 && 역할앱재연Policy.CargoScreen("하차지도착").Screen == 화물화면.진행 &&
                역할앱재연Policy.CargoScreen("인수완료").Screen == 화물화면.완료 && 역할앱재연Policy.CargoStepNames.Length == 6);

            // ── 홍달 창고 ──
            var center = 역할앱재연Policy.CenterScript(null, "Loaded");
            Check("창고: 새 출고 요청 → 피킹 작업 시작 → 피킹 완료 → 출고 인계", center[0].Push == "새 출고 요청" &&
                Taps(center) == "피킹 작업 시작,피킹 완료,출고 인계" && center.Last().Screen == 창고화면.완료 && Rising(center));
            Check("창고: 머무를 때의 화면과 같은 단계는 재연 없음", 역할앱재연Policy.CenterScreen("Picked").Step == 2 &&
                역할앱재연Policy.CenterScreen("Loaded").Screen == 창고화면.완료 && 역할앱재연Policy.CenterScript("Picked", "Picked").Count == 0);

            // ── 홍달 가게 ──
            var store = 역할앱재연Policy.StoreScript(null, "Accepted", "D3-0007");
            Check("가게: 주문 수신 알림(주문 번호) → 신규 → 기사 배정 · 수락 안내", store.Count == 4 && store[0].Push == "D3-0007 주문을 수신했습니다." &&
                store[1].Screen == 가게화면.신규 && Toasts(store) == "기사가 배정됐어요,기사가 수락했어요");
            var storeWhole = 역할앱재연Policy.StoreScript(null, "Settled", "D3-0007");
            Check("가게: 조리 단계가 게임에 없어 가게는 누르지 않음", Taps(storeWhole) == string.Empty && storeWhole.Last().Screen == 가게화면.완료 &&
                Rising(storeWhole) && 역할앱재연Policy.StoreScript("Waiting", "Waiting", "x").Count == 0);
            Check("가게: 머무를 때의 화면(신규 · 기사 도착 · 완료)", 역할앱재연Policy.StoreScreen("Waiting").Screen == 가게화면.신규 &&
                역할앱재연Policy.StoreScreen("AtPickup").Step == 2 && 역할앱재연Policy.StoreScreen("Settled").Screen == 가게화면.완료 &&
                역할앱재연Policy.StoreScreen(null).Screen == 가게화면.대기);
            return results;
        }
    }
}
