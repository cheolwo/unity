using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect 플랫폼장부단추 = new(1016, 666, 242, 32);
        private static readonly Rect 플랫폼장부칸 = new(22, 130, 300, 514);
        private bool 플랫폼장부열림;
        private SagajeongDispatchEngine.ExportSummary 플랫폼개별캐시;
        private float 플랫폼개별조회시각 = -1f;
        private int 플랫폼개별회차 = -1;

        private bool 플랫폼장부가능 => expansionView && !mapOnly && !showDesk && state != null;
        private bool 플랫폼장부표시 => 플랫폼장부열림 && 플랫폼장부가능 && !역할목록표시 &&
            !DongDeskVisible && !StoreDeskVisible && !sidePanelsOpen && !placeOpen;

        private void 플랫폼장부열기()
        {
            if (!플랫폼장부가능) return;
            관찰선택해제();
            actorFocus = null;
            selectedBuilding = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            dongDeskOpen = false;
            storeDeskPlace = null;
            sidePanelsOpen = false;
            역할목록열림 = false;
            expansionPanelOpen = false;
            플랫폼장부열림 = true;
        }

        private void 플랫폼장부단추그리기()
        {
            if (!플랫폼장부가능) return;
            if (ExpansionButton(플랫폼장부단추, "플랫폼 장부", true, 플랫폼장부표시 ? Gold : Tile))
            {
                if (플랫폼장부표시) 플랫폼장부열림 = false;
                else
                {
                    본사쪽 = 1;                 // 이 단추는 늘 본사 장부 쪽 (SagajeongDispatchPlatformPackage.cs)
                    플랫폼장부열기();
                }
            }
        }

        // 개별 주문은 현재 회차의 표시 주문, 동 수요는 운영 시작 이후 누계다.
        private void 플랫폼장부그리기()
        {
            if (플랫폼패키지 && 본사쪽 == 0)
            {
                지사쪽그리기();                // 배달 플랫폼 패키지: 같은 칸에 지사 쪽 (SagajeongDispatchPlatformPackage.cs)
                return;
            }
            var panel = 플랫폼장부칸;
            Paint(panel, Rim, 10);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 9);
            var x = panel.x + 12;
            var width = panel.width - 24;
            Text(new Rect(x, panel.y + 10, width - 40, 26), "플랫폼 장부", 19, Paper, true);
            if (ExpansionButton(new Rect(panel.xMax - 51, panel.y + 12, 39, 23), "닫기", true, Tile))
                플랫폼장부열림 = false;
            Text(new Rect(x, panel.y + 40, width, 18), "기록 출처별 · 집계 기간 확인", 11, Soft);

            var 회차 = state.campaign?.day ?? -1;
            if (플랫폼개별회차 != 회차 || Time.unscaledTime - 플랫폼개별조회시각 >= 0.5f)
            {
                플랫폼개별캐시 = engine?.CompletedSummary();
                플랫폼개별조회시각 = Time.unscaledTime;
                플랫폼개별회차 = 회차;
            }
            var 개별 = 플랫폼개별캐시;
            var 동집계 = ModuleSnapshot<DongDemandSnapshot>(BusinessIds.DongDemand);
            var y = panel.y + 69;
            Paint(new Rect(x, y, width, 141), Tile, 7);
            Text(new Rect(x + 9, y + 7, width - 18, 19), "개별 주문 · 현재 회차의 표시 주문", 12, Gold, true);
            장부줄(x, y + 31, width, "정산 완료", 개별 == null ? "자료 준비 전" : 개별.completedOrders + "건");
            장부줄(x, y + 52, width, "수입", 개별 == null ? "자료 준비 전" : 개별.virtualRevenue.ToString("N0") + "원");
            장부줄(x, y + 73, width, "기사 보수", 개별 == null ? "자료 준비 전" : 개별.virtualDriverPay.ToString("N0") + "원");
            장부줄(x, y + 94, width, "기본 차액", 개별 == null ? "자료 준비 전" : 개별.virtualBaseBalance.ToString("N0") + "원");
            Text(new Rect(x + 9, y + 115, width - 18, 18), "단건 총거래액·수수료  자료 준비 전", 10, Soft);

            y += 149;
            Paint(new Rect(x, y, width, 119), Tile, 7);
            Text(new Rect(x + 9, y + 7, width - 18, 19), "동 수요 · 전체 운영 동 · 누계", 12, Mint, true);
            장부줄(x, y + 31, width, "처리 추정", 동집계 == null ? "자료 준비 전" : 동집계.Served.ToString("N1") + "건");
            장부줄(x, y + 52, width, "수입", 동집계 == null ? "자료 준비 전" : 동집계.Revenue.ToString("N0") + "원");
            장부줄(x, y + 73, width, "기사 보수", 동집계 == null ? "자료 준비 전" : 동집계.RiderPay.ToString("N0") + "원");
            장부줄(x, y + 94, width, "고용·제휴 등 지출", 동집계 == null ? "자료 준비 전" : 동집계.Spent.ToString("N0") + "원");

            y += 129;
            Text(new Rect(x, y, width, 18), "참여자 신호", 12, Gold, true);
            var 주문 = state.orders;
            Text(new Rect(x, y + 24, width, 18),
                "음식점 · 기사 지정 대기 " + (주문 == null ? "자료 준비 전" : 주문.Count(order => order?.stage == "Waiting") + "건"), 11, Paper);
            Text(new Rect(x, y + 44, width, 18),
                "기사 · 이동·픽업 중 " + (주문 == null ? "자료 준비 전" : 주문.Count(order => order?.stage is "ToPickup" or "AtPickup" or "ToDropoff") + "건"), 11, Paper);
            Text(new Rect(x, y + 64, width, 18),
                "주문자 · 수령 확인 대기 " + (주문 == null ? "자료 준비 전" : 주문.Count(order => order?.stage == "Delivered") + "건"), 11, Paper);
            Text(new Rect(x, y + 87, width, 30),
                "내 카페도 입점점 · 상품 매출·수수료는 자료 준비 전", 11, Soft, wrap: true);
            Text(new Rect(x, panel.yMax - 36, width, 28),
                "오늘의 기여 · 통합 잔액  자료 준비 전", 10, Soft, wrap: true);
        }

        private void 장부줄(float x, float y, float width, string 이름, string 값)
        {
            Text(new Rect(x + 9, y, 128, 18), 이름, 11, Soft);
            Text(new Rect(x + 120, y, width - 129, 18), 값, 12, Paper, true, TextAnchor.MiddleRight);
        }
    }
}
