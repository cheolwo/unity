using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 00 경영(오행 하향식, 2026-11-17)의 순수 검사. 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 · 화면 없이 돈다.
    internal static class 오행경영Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // 1. 다섯 열: 목 → 화 → 토 → 금 → 수 순서 · r261 기본 관점의 업무 이름
            var 열 = 오행경영Policy.열;
            Check("다섯 열이 목 · 화 · 토 · 금 · 수 순서이고 오행이 겹치지 않음",
                열.Length == 5 && 열.Select(item => item.행).SequenceEqual(오행경영Policy.순서));
            Check("기본 관점: 주문(목) · 음식점(화) · 배차(토) · 창고(금) · 배달·운송(수)",
                string.Join("|", 열.Select(item => 오행경영Policy.표기(item.업무, item.행))) == "주문(목)|음식점(화)|배차(토)|창고(금)|배달·운송(수)");
            Check("열마다 아래로 가는 곳이 하나 이상 · 번호가 비지 않음",
                열.All(item => item.아래로.Length > 0 && item.아래로.All(down => !string.IsNullOrEmpty(down.번호) && !string.IsNullOrEmpty(down.이름))));
            Check("배차(토) 열의 첫 줄은 판 안의 1호지사(첫 하향 경로, r259 6절)",
                오행경영Policy.열찾기(오행.토).아래로[0].갈곳 == 경영갈곳.지사안쪽);

            // 2. 경로: 내려가기 · 위로 · 00으로 · 경로 조각
            var path = new 경영경로();
            Check("처음은 00 하나", path.깊이 == 0 && path.지금.층 == 경영층.루트 && path.줄글 == "00 경영");
            path.내려가기(경영층.지사, "branch-1", "1호지사");
            path.내려가기(경영층.기사, "driver-a", "기사 A");
            path.내려가기(경영층.주문, "order-1", "주문 1");
            Check("00 → 1호지사 → 기사 A → 주문 1", path.깊이 == 3 && path.줄글 == "00 경영 › 1호지사 › 기사 A › 주문 1");
            path.위로();
            Check("위로: 기사 A로", path.지금.층 == 경영층.기사 && path.지금.대상 == "driver-a");
            path.내려가기(경영층.주문, "order-2", "주문 2");
            path.내려가기(경영층.주문, "order-3", "주문 3");
            Check("같은 층으로 다시 내려가면 바꿔 끼움(쌓이지 않음)", path.깊이 == 3 && path.지금.대상 == "order-3");
            path.내려가기(경영층.지사, "branch-1", "1호지사");
            Check("더 위 층으로 내려가기를 부르면 그 층까지 올라가 바꿈", path.깊이 == 1 && path.지금.층 == 경영층.지사);
            path.내려가기(경영층.기사, "driver-b", "기사 B");
            Check("경로 조각 누름: 00으로", path.여기로(0) && path.깊이 == 0);
            Check("맨 위에서 위로는 아무 일도 없음", !path.위로() && path.깊이 == 0);
            Check("루트로 내려가기 · 빈 대상은 거절", !path.내려가기(경영층.루트, "x", "x") && !path.내려가기(경영층.기사, "", "기사") && path.깊이 == 0);
            path.내려가기(경영층.지사, "branch-1", "1호지사");
            path.내려가기(경영층.기사, "driver-a", "기사 A");
            path.맨위로();
            Check("00으로: 한 번에 맨 위", path.깊이 == 0);

            // 3. 사라진 대상은 그 마디부터 걷어 냄(다른 대상을 임의로 고르지 않음)
            path.내려가기(경영층.지사, "branch-1", "1호지사");
            path.내려가기(경영층.기사, "driver-a", "기사 A");
            path.내려가기(경영층.주문, "order-9", "주문 9");
            var 정리 = path.사라진대상정리(node => node.대상 != "order-9");
            Check("주문이 사라지면 기사 A까지 남음", 정리 && path.지금.대상 == "driver-a" && path.깊이 == 2);
            Check("모두 있으면 그대로", !path.사라진대상정리(_ => true) && path.깊이 == 2);

            // 4. 주문 단계 줄: 주문(목) → 배정(토) → 픽업(수) → 전달(수)
            Check("단계 넷 · 오행 표기", string.Join("|", 오행경영Policy.주문단계.Select(item => 오행경영Policy.표기(item.이름, item.행))) ==
                "주문(목)|배정(토)|픽업(수)|전달(수)");
            var stages = new[] { "Waiting", "Assigned", "Accepted", "ToPickup", "AtPickup", "ToDropoff", "Delivered", "Received", "Settled" };
            var order = stages.Select(오행경영Policy.단계순번).ToArray();
            Check("Core의 아홉 상태가 모두 단계에 들어가고 거꾸로 가지 않음",
                order.All(value => value >= 0) && order.Zip(order.Skip(1), (a, b) => a <= b).All(ok => ok), string.Join(",", order));
            Check("취소 · 모르는 상태는 -1", 오행경영Policy.단계순번("Cancelled") == -1 && 오행경영Policy.단계순번(null) == -1 && 오행경영Policy.단계순번("x") == -1);
            Check("진행 중 = 배정 ~ 픽업 · 배달 중", 오행경영Policy.진행중("Assigned") && 오행경영Policy.진행중("ToDropoff") &&
                !오행경영Policy.진행중("Waiting") && !오행경영Policy.진행중("Delivered"));
            return results;
        }
    }
}
