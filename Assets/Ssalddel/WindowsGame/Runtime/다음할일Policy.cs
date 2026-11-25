using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;

namespace Ssalddel.WindowsGame
{
    // 다음 할 일 · 이번 턴 계획 · 턴 넘기기의 순수 규칙 (2026-11-16, Codex r247 · Claude 개발 계획
    // docs/AI/Handoffs/2026-11-16-Claude-개발계획-다음할일-위험알림과고를일-r247.md).
    //  - 항목은 커널 신호와 첫 사슬의 다음 행동에서만 만든다. 지어낸 안건이 없다.
    //  - 묶음: 위험 알림(위험 + 고를 것) · 고를 일(주의 + 고를 것, 사슬 행동) · 지켜볼 것(고를 것 없음, 모르는 사유). 수는 앞의 둘만.
    //  - 계획은 화면에만 있는 임시 목록이다. 같은 명령 키를 다시 담으면 바꾼다. 넘길 때 위험 → 고를 일, 담은 순서로 한 번씩 보낸다.
    //  - 화면 · 시계 · 커널을 모른다. 검사(다음할일Checks)가 이것만 돌린다.
    public enum 할일묶음 { 위험, 고를일, 지켜볼것 }

    public enum 할일창 { 매장재료, 배차데스크, 동관리, 보기만 }

    public enum 계획종류 { 발주, 검수, 제조, 배차, 모집, 제휴 }

    // 항목을 만드는 재료. 신호 한 줄 또는 사슬의 다음 행동 하나.
    public sealed class 할일원천
    {
        public string 키 = string.Empty;          // 신호 Id, 사슬이면 "chain:" + 행동
        public string 업무 = string.Empty;        // 신호의 OsId (사슬은 StoreOperations)
        public string 사유 = string.Empty;        // ReasonCode (사슬은 order · inspect · produce)
        public string 장소 = string.Empty;        // PlaceId ("store:…" · "dong:…" · 매장 자리)
        public bool 위험;
        public string 단추말 = string.Empty;      // ActionLabel
        public string 제목 = string.Empty;
        public string 문구 = string.Empty;
        public long 시작Ms;
        public long 판본;                         // 신호가 다시 올라가면 바뀐다(SinceMs · EscalatedMs)
        public bool 사슬;
        public string 주문Id;                     // 배차 대기면 원 주문 ID
    }

    public sealed class 다음할일항목
    {
        public string 키 = string.Empty;
        public string 명령키;                     // 계획과 잇는 열쇠. 고를 것이 없으면 null
        public 할일묶음 묶음;
        public 할일창 창;
        public string 창이름 = string.Empty;
        public string 고를것 = string.Empty;
        public string 업무 = string.Empty;
        public string 사유 = string.Empty;
        public string 장소 = string.Empty;
        public string 제목 = string.Empty;
        public string 문구 = string.Empty;
        public long 시작Ms;
        public long 판본;
        public bool 지난턴미룸;
        public string 주문Id;
    }

    public sealed class 턴계획항목
    {
        public string 명령키 = string.Empty;
        public 계획종류 종류;
        public string 제목 = string.Empty;
        public string 예상 = string.Empty;
        public string 대상 = string.Empty;        // 매장 자리 · 동 코드 · 원 주문 ID
        public string 기사;
        public long 판본;                         // 주문 revision · 검수할 입고 표 등
        public string 입고Id;
        public int 수량;
        public int 인수;
        public int 격리;
        public int 거절;
        public int 순번;
    }

    public sealed class 이번턴계획
    {
        private readonly List<턴계획항목> 항목들 = new List<턴계획항목>();
        private int 다음순번 = 1;

        public int Count => 항목들.Count;
        public IReadOnlyList<턴계획항목> 항목 => 항목들;

        public 턴계획항목 찾기(string 명령키) => 항목들.FirstOrDefault(item => item.명령키 == 명령키);

        // 같은 명령 키면 바꾸고 처음 담은 순번을 지킨다. 바꿨으면 false, 새로 담았으면 true.
        public bool 담기(턴계획항목 새것)
        {
            if (새것 == null || string.IsNullOrEmpty(새것.명령키)) return false;
            var 있던 = 항목들.FindIndex(item => item.명령키 == 새것.명령키);
            if (있던 >= 0)
            {
                새것.순번 = 항목들[있던].순번;
                항목들[있던] = 새것;
                return false;
            }
            새것.순번 = 다음순번++;
            항목들.Add(새것);
            return true;
        }

        public bool 빼기(string 명령키) => 항목들.RemoveAll(item => item.명령키 == 명령키) > 0;

        public void 비우기() => 항목들.Clear();

        // 같은 기사를 이번 턴 계획의 다른 배차에 이미 담았는가.
        public bool 기사겹침(string 기사, string 제외명령키) =>
            !string.IsNullOrEmpty(기사) && 항목들.Any(item => item.종류 == 계획종류.배차 && item.기사 == 기사 && item.명령키 != 제외명령키);

        // 넘길 때 보내는 순서: 위험 → 고를 일 → 그 밖(안내판에 없는 직접 담기), 같은 묶음은 담은 순서.
        public List<턴계획항목> 실행순서(Func<string, 할일묶음?> 묶음찾기)
        {
            int 차례(턴계획항목 item)
            {
                var 묶음 = 묶음찾기?.Invoke(item.명령키);
                return 묶음 == 할일묶음.위험 ? 0 : 묶음 == 할일묶음.고를일 ? 1 : 2;
            }
            return 항목들.OrderBy(차례).ThenBy(item => item.순번).ToList();
        }
    }

    public enum 결과상태 { 진행중, 완료, 대기, 실패, 미룸 }

    public static class 다음할일Policy
    {
        public static string 발주키(string 매장) => "order:" + 매장;
        public static string 검수키(string 매장) => "inspect:" + 매장;
        public static string 제조키(string 매장) => "produce:" + 매장;
        public static string 배차키(string 주문Id) => "assign:" + 주문Id;
        public static string 모집키(string 동코드) => "recruit:" + 동코드;
        public static string 제휴키(string 동코드) => "contract:" + 동코드;

        // 사유마다 고를 것과 여는 창. 모르는 사유는 고를 것을 지어내지 않는다(보기만).
        public static (string 고를것, 할일창 창, string 창이름, string 명령키) 고를곳(할일원천 원천, string 매장자리, string 동코드)
        {
            if (원천 == null) return (null, 할일창.보기만, "가서 보기", null);
            var 매장 = string.IsNullOrEmpty(매장자리) ? 원천.장소 : 매장자리;
            if (원천.업무 == BusinessIds.StoreOperations || 원천.사슬)
            {
                switch (원천.사유)
                {
                    case "stock-out":
                    case "stock-low":
                    case "order":
                        return ("본부 발주 수량", 할일창.매장재료, "재료 탭", 발주키(매장));
                    case "inspection-waiting":
                    case "inspect":
                        return ("검수 수량", 할일창.매장재료, "재료 탭", 검수키(매장));
                    case "produce":
                        return ("제조", 할일창.매장재료, "재료 탭", 제조키(매장));
                }
                return (null, 할일창.보기만, "가서 보기", null);
            }
            if (원천.업무 == BusinessIds.FoodDelivery && 원천.사유 == "dispatch-waiting" && !string.IsNullOrEmpty(원천.주문Id))
                return ("기사 지정", 할일창.배차데스크, "배차 데스크", 배차키(원천.주문Id));
            if (원천.업무 == BusinessIds.DongDemand && !string.IsNullOrEmpty(동코드))
            {
                if (원천.사유 == DongDemandBottlenecks.Rider) return ("기사 모집", 할일창.동관리, "동 관리 · 고용", 모집키(동코드));
                if (원천.사유 == DongDemandBottlenecks.Partner) return ("제휴 맺기", 할일창.동관리, "동 관리 · 건설", 제휴키(동코드));
            }
            return (null, 할일창.보기만, "가서 보기", null);
        }

        // 원천에서 항목을 만든다. 사슬의 행동은 같은 명령 키의 신호가 있으면 그 신호 하나로 합친다.
        public static List<다음할일항목> 모으기(IEnumerable<할일원천> 원천들, Func<할일원천, (string 매장, string 동코드)> 자리,
            ICollection<string> 미룬키)
        {
            var 결과 = new List<다음할일항목>();
            var 명령키들 = new HashSet<string>();
            var 차례 = (원천들 ?? Enumerable.Empty<할일원천>()).Where(item => item != null)
                .OrderBy(item => item.사슬 ? 1 : 0).ToList();          // 신호를 먼저 보고 사슬은 나중에 합친다
            foreach (var 원천 in 차례)
            {
                string 매장 = null, 동코드 = null;
                if (자리 != null) (매장, 동코드) = 자리(원천);
                var 곳 = 고를곳(원천, 매장, 동코드);
                if (곳.명령키 != null && !명령키들.Add(곳.명령키)) continue;
                var 고를것있음 = 곳.명령키 != null;
                결과.Add(new 다음할일항목
                {
                    키 = 원천.키,
                    명령키 = 곳.명령키,
                    묶음 = !고를것있음 ? 할일묶음.지켜볼것 : 원천.위험 ? 할일묶음.위험 : 할일묶음.고를일,
                    창 = 곳.창,
                    창이름 = 곳.창이름,
                    고를것 = 곳.고를것 ?? (string.IsNullOrEmpty(원천.단추말) ? "고를 것 없음" : "자료 미확인 · 신호 줄의 단추"),
                    업무 = 원천.업무,
                    사유 = 원천.사유,
                    장소 = 원천.장소,
                    제목 = 원천.제목,
                    문구 = 원천.문구,
                    시작Ms = 원천.시작Ms,
                    판본 = 원천.판본,
                    지난턴미룸 = 곳.명령키 != null && 미룬키 != null && 미룬키.Contains(곳.명령키),
                    주문Id = 원천.주문Id,
                });
            }
            return 정렬(결과);
        }

        // 위험 → 고를 일 → 지켜볼 것, 같은 묶음 안에서는 오래된 것 먼저, 같으면 키 순서.
        public static List<다음할일항목> 정렬(IEnumerable<다음할일항목> 항목) =>
            항목.OrderBy(item => (int)item.묶음).ThenBy(item => item.시작Ms).ThenBy(item => item.키, StringComparer.Ordinal).ToList();

        public static int 셀수(IEnumerable<다음할일항목> 항목) => 항목?.Count(item => item.묶음 != 할일묶음.지켜볼것) ?? 0;

        // 아직 고르지 않은(계획에 없는) 고를 일. 턴을 넘기면 이것들이 미룸이 된다.
        public static List<다음할일항목> 고르지않은(IEnumerable<다음할일항목> 항목, 이번턴계획 계획) =>
            (항목 ?? Enumerable.Empty<다음할일항목>())
                .Where(item => item.묶음 != 할일묶음.지켜볼것 && item.명령키 != null && 계획?.찾기(item.명령키) == null).ToList();

        // 본부 발주의 진행: 발주 → 센터 출고 → 화물 → 검수. 지금 칸(0~3)과 상태 · 문장.
        public static (int 칸, 결과상태 상태, string 문장) 발주진행(bool 출고있음, bool 상차끝, bool 운행있음, bool 도착, bool 검수대기, bool 검수끝)
        {
            if (검수끝) return (3, 결과상태.완료, "검수까지 끝났습니다");
            if (검수대기 || 도착) return (3, 결과상태.대기, "화물이 도착했습니다 · 검수는 직접(화)");
            if (운행있음) return (2, 결과상태.진행중, "화물이 매장으로 가는 중");
            if (출고있음 && 상차끝) return (2, 결과상태.진행중, "상차를 마치고 출발을 기다립니다");
            if (출고있음) return (1, 결과상태.진행중, "센터가 출고를 준비합니다");
            return (1, 결과상태.진행중, "본부가 발주를 받았습니다 · 센터 출고 대기");
        }

        // 배차의 진행: 지정 → 가게 도착 → 픽업 → 전달. 다른 기사가 맡았으면 실패로 읽는다.
        public static (int 칸, 결과상태 상태, string 문장) 배달진행(string 단계, string 맡은기사, string 고른기사)
        {
            if (string.IsNullOrEmpty(단계)) return (0, 결과상태.실패, "주문 기록을 찾을 수 없습니다");
            if (단계 == "Waiting") return (0, 결과상태.대기, "아직 배정 전입니다");
            if (!string.IsNullOrEmpty(고른기사) && 맡은기사 != 고른기사) return (0, 결과상태.실패, "다른 기사가 맡았습니다");
            switch (단계)
            {
                case "Assigned":
                case "Accepted":
                case "ToPickup":
                    return (1, 결과상태.진행중, "기사가 가게로 가는 중");
                case "AtPickup":
                    return (2, 결과상태.진행중, "가게에서 픽업 대기");
                case "ToDropoff":
                    return (3, 결과상태.진행중, "전달지로 가는 중");
                case "Delivered":
                case "Received":
                case "Settled":
                    return (3, 결과상태.완료, "전달을 마쳤습니다");
            }
            return (1, 결과상태.진행중, "다음 기록 대기");
        }

        // 화에서 안내판이나 계획이 열린 동안 시계를 멈춘다(D7). 넘기면 원래 배속으로.
        public static bool 시계멈춤(bool 화, bool 판열림, int 계획수) => 화 && (판열림 || 계획수 > 0);
    }
}
