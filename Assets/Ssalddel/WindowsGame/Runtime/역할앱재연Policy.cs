using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 역할 앱 휴대폰의 재연 대본 (2026-11-15, 사용자: 「AI 에이전트가 화면을 조작하듯 기사님이 수행 · 상태가 수락으로 바뀌면
    // 수락을 누르고 알림으로 넘어가게」, 같은 날 「기사뿐만 아니라 음식점 · 창고 · 화물 기사님도」). 순수 규칙이며 System만 쓴다.
    // 제안: Hongdal docs/AI/Handoffs/2026-11-15-Claude-제안-역할앱을게임속으로.md 9 · 11절.
    //  - 한 방향: 게임 단계가 바뀐 뒤에 휴대폰이 그 일을 재연한다. 대본은 상태를 바꾸지 않는다. 휴대폰은 관찰을 쉽게 하는 도구다(9절 「넣는 까닭」).
    //  - 앱마다 단계 이름은 그 업무 모듈의 것이고, 화면 이름과 단추 문구는 실제 역할 앱을 따른다.
    //      홍달 기사(음식 배달 기사) ← FDriverApp/Pages/MainPage.xaml: 배달 요청 · 수락 · 현장 처리 · 진행 상황 · 최근 완료 결과
    //      홍달 화물(화물 기사) ← DriverApp(배차 추천 · 추천 수락 · 진행 중 운송 · 상차 · 하차 · 상차지 도착 · 상차 완료 · 하차지 도착 · 하차 완료)
    //      홍달 창고(창고 관리자) ← WarehouseManagerApp(창고 작업 보드 · 피킹 작업 · 포장 작업 · 출고 인계 준비)
    //      홍달 가게(음식점) ← RestaurantDeskApp(주문 처리 · 신규 · 진행 · 「… 주문을 수신했습니다.」)
    //    토스트 문장은 앱에 같은 문장이 없어 게임이 덧붙인 짧은 안내다.
    //  - 가게는 조리 단계(주문 수락 · 조리 시작 · 픽업 준비 완료)가 게임에 아직 없어(카2 · 카3) 손가락이 누르지 않는다. 기사 · 배차가 한 일만 알림으로 보인다.
    internal static class 기사화면
    {
        public const string 대기 = "대기", 요청 = "요청", 진행 = "진행", 완료 = "완료";
    }

    internal static class 화물화면
    {
        public const string 대기 = "대기", 추천 = "추천", 진행 = "진행", 완료 = "완료";
    }

    internal static class 창고화면
    {
        public const string 대기 = "대기", 작업 = "작업", 완료 = "완료";
    }

    internal static class 가게화면
    {
        public const string 대기 = "대기", 신규 = "신규", 진행 = "진행", 완료 = "완료";
    }

    internal sealed class 재연장면
    {
        public string Screen;           // 이 장면에서 보일 화면(앱마다의 화면 이름)
        public int Step;                // 진행 칸
        public string Tap;              // 가상 인물이 누르는 단추(없으면 null)
        public string Toast;            // 아래 짧은 안내
        public string Push;             // 위 알림
        public float Seconds;           // 실제 시간(게임 배속과 무관)
        public string Stage;            // 이 장면이 보여 주는 게임 단계
    }

    internal static class 역할앱재연Policy
    {
        // ── 홍달 기사: 음식 배달 Core의 주문 단계 ──
        public static readonly string[] Stages = { "Waiting", "Assigned", "Accepted", "ToPickup", "AtPickup", "ToDropoff", "Delivered", "Received", "Settled" };
        public static readonly string[] StepNames = { "수락", "픽업지 이동", "픽업", "전달", "완료" };
        // ── 홍달 화물: 화물 운송 모듈의 운행 단계를 화면에 맞게 줄인 것(상차 완료 뒤 도착 전은 운송중) ──
        public static readonly string[] CargoStages = { "대기", "배차확정", "상차완료", "운송중", "하차지도착", "인수완료" };
        public static readonly string[] CargoStepNames = { "접수", "배차중", "배차확정", "상차완료", "운송중", "하차완료" };
        // ── 홍달 창고: 물류센터 출고 단계(OutboundStages) ──
        public static readonly string[] CenterStages = { "대기", "Reserved", "Picked", "Loaded" };
        public static readonly string[] CenterStepNames = { "출고 요청", "피킹", "포장", "출고 인계" };
        // ── 홍달 가게: 음식 배달 주문 단계를 가게 쪽에서 본 것 ──
        public static readonly string[] StoreStepNames = { "접수", "기사 배정", "기사 도착", "픽업 완료", "전달 완료" };

        public const float FastPace = 2f;          // 변화가 밀리면 두 배로 넘긴다
        public const int SkipAfter = 3;            // 밀린 변화가 이만큼이면 마지막 상태로 건너뛴다

        public static int IndexOf(string stage) => Array.IndexOf(Stages, stage ?? string.Empty);

        // from → to 사이에 들어선 단계를 차례로 재연한다. 모르는 단계거나 to가 from보다 앞이면(새 일) 처음부터 다시 시작한다.
        private static List<재연장면> Between(string[] stages, string from, string to, Func<string, List<재연장면>> enter)
        {
            var list = new List<재연장면>();
            var end = Array.IndexOf(stages, to ?? string.Empty);
            var start = Array.IndexOf(stages, from ?? string.Empty);
            if (end < 0 || start == end) return list;
            if (start < 0 || end < start) start = 0;
            for (var i = start + 1; i <= end; i++) list.AddRange(enter(stages[i]));
            return list;
        }

        private sealed class Builder
        {
            public readonly List<재연장면> List = new List<재연장면>();
            private readonly string stage;
            public Builder(string stage) { this.stage = stage; }
            public void Add(string screen, int step, float seconds, string tap = null, string toast = null, string push = null) =>
                List.Add(new 재연장면 { Screen = screen, Step = step, Seconds = seconds, Tap = tap, Toast = toast, Push = push, Stage = stage });
        }

        // ── 홍달 기사 ──

        // 재연 없이 그 단계에 머무를 때의 화면(휴대폰을 중간에 열 때 · 재연이 끝난 뒤).
        public static (string Screen, int Step) RiderScreen(string stage) => stage switch
        {
            "Assigned" => (기사화면.요청, 0),
            "Accepted" => (기사화면.진행, 0),
            "ToPickup" => (기사화면.진행, 1),
            "AtPickup" => (기사화면.진행, 2),
            "ToDropoff" => (기사화면.진행, 3),
            "Delivered" => (기사화면.진행, 4),
            "Received" => (기사화면.진행, 4),
            "Settled" => (기사화면.완료, 4),
            _ => (기사화면.대기, 0),
        };

        // 그 단계에 들어설 때 기사 휴대폰이 재연할 장면들.
        public static List<재연장면> RiderEnter(string stage, string settleText)
        {
            var b = new Builder(stage);
            switch (stage)
            {
                case "Assigned":
                    b.Add(기사화면.대기, 0, 0.9f, push: "새 배달 요청");
                    b.Add(기사화면.요청, 0, 0.8f, push: "새 배달 요청");
                    break;
                case "Accepted":
                    b.Add(기사화면.요청, 0, 1.0f, tap: "수락");
                    b.Add(기사화면.진행, 0, 0.9f, toast: "배달을 수락했어요");
                    break;
                case "ToPickup":
                    b.Add(기사화면.진행, 1, 0.6f);
                    break;
                case "AtPickup":
                    b.Add(기사화면.진행, 1, 0.9f, tap: "현장 처리");
                    b.Add(기사화면.진행, 2, 0.9f, toast: "픽업지에 도착했어요");
                    break;
                case "ToDropoff":
                    b.Add(기사화면.진행, 2, 0.9f, tap: "현장 처리");
                    b.Add(기사화면.진행, 3, 0.9f, toast: "픽업을 완료했어요");
                    break;
                case "Delivered":
                    b.Add(기사화면.진행, 3, 0.9f, tap: "현장 처리");
                    b.Add(기사화면.진행, 4, 0.9f, toast: "전달을 완료했어요");
                    break;
                case "Settled":
                    b.Add(기사화면.완료, 4, 1.4f, push: string.IsNullOrEmpty(settleText) ? "정산 · 가상" : settleText);
                    break;
            }
            return b.List;
        }

        public static List<재연장면> RiderScript(string from, string to, string settleText) =>
            Between(Stages, from ?? "Waiting", to, stage => RiderEnter(stage, settleText));

        // ── 홍달 화물 ──

        public static (string Screen, int Step) CargoScreen(string stage) => stage switch
        {
            "배차확정" => (화물화면.진행, 2),
            "상차완료" => (화물화면.진행, 3),
            "운송중" => (화물화면.진행, 4),
            "하차지도착" => (화물화면.진행, 5),
            "인수완료" => (화물화면.완료, 5),
            _ => (화물화면.대기, 0),
        };

        public static List<재연장면> CargoEnter(string stage)
        {
            var b = new Builder(stage);
            switch (stage)
            {
                case "배차확정":
                    b.Add(화물화면.대기, 0, 0.9f, push: "신규 배차 추천");
                    b.Add(화물화면.추천, 1, 0.8f);
                    b.Add(화물화면.추천, 1, 1.0f, tap: "추천 수락");
                    b.Add(화물화면.진행, 2, 0.9f, toast: "배차가 확정됐어요");
                    break;
                case "상차완료":
                    b.Add(화물화면.진행, 2, 0.9f, tap: "상차지 도착");
                    b.Add(화물화면.진행, 2, 0.9f, tap: "상차 완료");
                    b.Add(화물화면.진행, 3, 0.9f, toast: "상차를 완료했어요");
                    break;
                case "운송중":
                    b.Add(화물화면.진행, 4, 0.8f);
                    break;
                case "하차지도착":
                    b.Add(화물화면.진행, 4, 0.9f, tap: "하차지 도착");
                    b.Add(화물화면.진행, 5, 0.9f, toast: "하차지에 도착했어요");
                    break;
                case "인수완료":
                    b.Add(화물화면.진행, 5, 0.9f, tap: "하차 완료");
                    b.Add(화물화면.완료, 5, 1.2f, toast: "인수 확인을 받았어요");
                    break;
            }
            return b.List;
        }

        public static List<재연장면> CargoScript(string from, string to) => Between(CargoStages, from ?? "대기", to, CargoEnter);

        // ── 홍달 창고 ──

        public static (string Screen, int Step) CenterScreen(string stage) => stage switch
        {
            "Reserved" => (창고화면.작업, 1),
            "Picked" => (창고화면.작업, 2),
            "Loaded" => (창고화면.완료, 3),
            _ => (창고화면.대기, 0),
        };

        public static List<재연장면> CenterEnter(string stage)
        {
            var b = new Builder(stage);
            switch (stage)
            {
                case "Reserved":
                    b.Add(창고화면.대기, 0, 0.9f, push: "새 출고 요청");
                    b.Add(창고화면.작업, 0, 1.0f, tap: "피킹 작업 시작");
                    b.Add(창고화면.작업, 1, 0.8f, toast: "로트를 예약했어요");
                    break;
                case "Picked":
                    b.Add(창고화면.작업, 1, 0.9f, tap: "피킹 완료");
                    b.Add(창고화면.작업, 2, 0.9f, toast: "피킹 · 포장을 마쳤어요");
                    break;
                case "Loaded":
                    b.Add(창고화면.작업, 2, 0.9f, tap: "출고 인계");
                    b.Add(창고화면.완료, 3, 1.1f, toast: "화물차에 넘겼어요");
                    break;
            }
            return b.List;
        }

        public static List<재연장면> CenterScript(string from, string to) => Between(CenterStages, from ?? "대기", to, CenterEnter);

        // ── 홍달 가게 ──

        public static (string Screen, int Step) StoreScreen(string stage) => stage switch
        {
            "Waiting" => (가게화면.신규, 0),
            "Assigned" => (가게화면.진행, 1),
            "Accepted" => (가게화면.진행, 1),
            "ToPickup" => (가게화면.진행, 1),
            "AtPickup" => (가게화면.진행, 2),
            "ToDropoff" => (가게화면.진행, 3),
            "Delivered" => (가게화면.진행, 4),
            "Received" => (가게화면.진행, 4),
            "Settled" => (가게화면.완료, 4),
            _ => (가게화면.대기, 0),
        };

        // 가게는 누르지 않는다(조리 단계가 게임에 없음). 주문이 들어오고 기사 · 배차가 한 일을 알림과 안내로 보인다.
        public static List<재연장면> StoreEnter(string stage, string orderNumber)
        {
            var b = new Builder(stage);
            switch (stage)
            {
                case "Waiting":
                    b.Add(가게화면.대기, 0, 1.0f, push: (string.IsNullOrEmpty(orderNumber) ? "새" : orderNumber) + " 주문을 수신했습니다.");
                    b.Add(가게화면.신규, 0, 0.9f);
                    break;
                case "Assigned":
                    b.Add(가게화면.진행, 1, 1.0f, toast: "기사가 배정됐어요");
                    break;
                case "Accepted":
                    b.Add(가게화면.진행, 1, 0.8f, toast: "기사가 수락했어요");
                    break;
                case "AtPickup":
                    b.Add(가게화면.진행, 2, 0.9f, toast: "기사가 도착했어요");
                    break;
                case "ToDropoff":
                    b.Add(가게화면.진행, 3, 0.9f, toast: "기사가 픽업했어요");
                    break;
                case "Delivered":
                    b.Add(가게화면.진행, 4, 0.9f, toast: "전달을 완료했어요");
                    break;
                case "Settled":
                    b.Add(가게화면.완료, 4, 1.2f, push: "정산 · 가상");
                    break;
            }
            return b.List;
        }

        public static List<재연장면> StoreScript(string from, string to, string orderNumber) =>
            Between(Stages, from, to, stage => StoreEnter(stage, orderNumber), startAtFirst: true);

        // 가게는 주문이 생기는 것(Waiting) 자체도 재연해야 하므로 처음부터일 때는 Waiting부터 넣는다.
        private static List<재연장면> Between(string[] stages, string from, string to, Func<string, List<재연장면>> enter, bool startAtFirst)
        {
            if (!startAtFirst) return Between(stages, from, to, enter);
            var list = new List<재연장면>();
            var end = Array.IndexOf(stages, to ?? string.Empty);
            var start = Array.IndexOf(stages, from ?? string.Empty);
            if (end < 0 || start == end) return list;
            var first = start < 0 || end < start ? 0 : start + 1;
            for (var i = first; i <= end; i++) list.AddRange(enter(stages[i]));
            return list;
        }

        // ── 공통 ──

        // 밀린 변화를 건너뛸 때 보일 한 장면. 화면 · 칸은 부르는 쪽이 그 앱의 머무를 때 화면으로 준다.
        public static 재연장면 Skipped(string screen, int step, string stage, int count) =>
            new 재연장면 { Screen = screen, Step = step, Seconds = 1.2f, Toast = $"그사이 {count}가지 일을 했어요", Stage = stage };

        public static 재연장면 Skipped(string stage, int count)
        {
            var (screen, step) = RiderScreen(stage);
            return Skipped(screen, step, stage, count);
        }

        // 기다리는 변화 수에 따른 재생: 하나면 그대로, 둘이면 빠르게, 셋 이상이면 건너뛴다.
        public static (float Speed, bool Skip) Pace(int pendingTransitions) =>
            pendingTransitions >= SkipAfter ? (FastPace, true) : pendingTransitions >= 2 ? (FastPace, false) : (1f, false);
    }
}
