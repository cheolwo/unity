using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 사업 창과 고른 동 (r198, 2026-11-14 첫 걸음 = 개발 계획의 사2).
    // 기획 방향: 동은 「어디를 경영하는가」, 아래 사업 버튼은 「무슨 일을 결정하는가」를 고른다. 사업 창은 고른 동의 이름을 늘 말하고
    //           그 동의 그 사업 현황을 보인다.
    //  - 여기서는 읽기만 한다. 창을 열거나 동을 바꾸는 것은 조회이고 시간·돈·고용·제휴를 바꾸지 않는다.
    //  - 숫자는 동별 수요의 사본에서 읽는다. 기사와 제휴는 동별 수의 집계이며 한 사람·한 점포의 객체가 아니다.
    //  - 고용·제휴 단추를 동 카드에서 이 창으로 옮기는 일(사3)과 음식점 추가의 자리 고르기(사4)는 시안 검토 뒤의 걸음이다.
    //  - 행정동 모듈을 쓰지 않는 화면(검증 촬영)에서는 고른 동이 없으므로 창은 전과 같이 나온다.
    public sealed partial class SagajeongDispatchView
    {
        // 사업 창이 말하는 동: 지금 고른 동. 역 판에서는 사업 칸이 열려 있는 동이다. 없으면 null.
        private DongModuleState BusinessDong()
        {
            if (mapLevel != MapLevel.Dong || !DongModulesActive) return null;
            var region = dongView == DongView.Station ? stationCardRegion ?? focusDongRegion : focusDongRegion ?? soloRegion;
            return string.IsNullOrEmpty(region) ? null : DongModuleById(region);
        }

        // 사업 창의 현황 칸: 고른 동의 기사 운영 또는 음식점. 그렸으면 true (그리지 않았으면 전의 흐름 칸이 나온다).
        private bool DrawBusinessDongStatus(int business, DongModuleState dong, float x, float y)
        {
            if (dong == null || dongDemand == null || business > BusinessRestaurant) return false;
            var trade = DongTrade(dong.Entry.regionId);
            Text(new Rect(x + 11, y + 220, 216, 18), dong.Entry.name + (business == BusinessFood ? "의 기사 운영" : "의 음식점"), 11, Gold, true);
            if (trade == null || !trade.Open)
            {
                Text(new Rect(x + 11, y + 241, 216, 22), "아직 운영하지 않는 동입니다", 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), "깃발을 꽂으면 이 동에 주문이 들어오기 시작합니다.", 10, Soft, wrap: true);
                return true;
            }
            var served = Mathf.RoundToInt((float)(1 - trade.UnmetRatio) * 100f);
            if (business == BusinessFood)
            {
                var lacking = trade.Bottleneck == Ssalddel.WindowsGame.Kernel.DongDemandBottlenecks.Rider;
                Text(new Rect(x + 11, y + 241, 216, 22),
                    "기사 " + trade.Riders + "명 · 지금 주문 " + trade.RateNow.ToString("0.0") + "건/시간", 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), lacking
                    ? "기사가 모자랍니다 · 최근 받아냄 " + served + "%"
                    : "최근 받아냄 " + served + "% · 기사 수는 동별 집계입니다", 10, lacking ? Gold : Soft, wrap: true);
            }
            else
            {
                var lacking = trade.Bottleneck == Ssalddel.WindowsGame.Kernel.DongDemandBottlenecks.Partner;
                Text(new Rect(x + 11, y + 241, 216, 22),
                    "제휴 " + trade.Partners + "/" + trade.PartnerLimit + "곳 · 음식점 " + trade.Restaurants + "곳", 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), lacking
                    ? "제휴가 모자랍니다 · 최근 받아냄 " + served + "%"
                    : "제휴 음식점은 가상 자리로 밝혀 둡니다 · 최근 받아냄 " + served + "%", 10, lacking ? Gold : Soft, wrap: true);
            }
            return true;
        }
    }
}
