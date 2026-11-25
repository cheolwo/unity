using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 배달 플랫폼 패키지 뼈대의 순수 검사 (2026-11-16). 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 · 화면 없이 돈다.
    //  숫자는 게임 동 수요표(가상)의 면목 여섯 동 값이다: 면목본동 54.61 · 면목제2동 43.0 · 면목제3.8동 45.3 · 면목제7동 43.8 · 면목제5동 19.1 · 면목제4동 34.2.
    internal static class 배달플랫폼Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            bool Near(double a, double b, double eps) => Math.Abs(a - b) <= eps;

            // 1. 패키지 표: 넷 · 저장값 겹침 없음 · 열린 것은 배달 플랫폼 하나
            var packs = 배달플랫폼Policy.패키지들;
            Check("패키지 넷 · 저장값 겹침 없음 · 열린 것은 배달 플랫폼 하나",
                packs.Length == 4 && packs.Select(item => item.저장값).Distinct().Count() == 4 &&
                packs.Count(item => item.열림) == 1 && packs.Single(item => item.열림).종류 == 패키지종류.배달플랫폼);

            // 2. 저장값 왕복: 열린 패키지만 되돌아온다. 빈 값 · 모르는 값 · 아직 닫힌 패키지는 「없음」
            Check("저장값 왕복: 배달 플랫폼만 되돌아오고 나머지는 없음",
                배달플랫폼Policy.읽기(배달플랫폼Policy.저장(패키지종류.배달플랫폼)) == 패키지종류.배달플랫폼 &&
                배달플랫폼Policy.읽기(string.Empty) == 패키지종류.없음 && 배달플랫폼Policy.읽기("unknown") == 패키지종류.없음 &&
                배달플랫폼Policy.읽기(배달플랫폼Policy.저장(패키지종류.프랜차이즈)) == 패키지종류.없음);

            // 3. 아래 줄: 여섯 칸 · 이름 겹침 없음
            var menu = 배달플랫폼Policy.플랫폼메뉴;
            Check("배달 플랫폼 아래 줄: 지사 · 기사 · 배차 · 제휴 매장 · 정산 · 본사 장부",
                menu.Length == 6 && menu.Distinct().Count() == 6 && menu[0] == "지사" && menu[5] == "본사 장부");

            // 4. 시간대: 0시부터 24시까지 빈틈 · 겹침 없이 여섯
            var bands = 배달플랫폼Policy.시간대;
            var joined = bands.Length == 6 && bands[0].시작 == 0 && bands[5].끝 == 24;
            for (var i = 1; i < bands.Length; i++) joined &= bands[i].시작 == bands[i - 1].끝 && bands[i].끝 > bands[i].시작;
            Check("시간대 여섯 칸이 0~24시를 빈틈없이 덮음", joined);

            // 5. 필요 기사: 사가정 지사(세 동) 하루 143.7건 · 기사 1명 시간당 3.5건
            const double 사가정 = 54.61 + 45.3 + 43.8;
            var 저녁 = 배달플랫폼Policy.필요기사(사가정, 0.38, 4, 3.5, 1.0);
            var 점심 = 배달플랫폼Policy.필요기사(사가정, 0.27, 3, 3.5, 1.0);
            var 오후 = 배달플랫폼Policy.필요기사(사가정, 0.12, 3, 3.5, 1.0);
            var 밤 = 배달플랫폼Policy.필요기사(사가정, 0.10, 3, 3.5, 1.0);
            Check("필요 기사(사가정 세 동): 점심 4 · 오후 2 · 저녁 4 · 밤 2",
                점심 == 4 && 오후 == 2 && 저녁 == 4 && 밤 == 2, $"{점심} · {오후} · {저녁} · {밤}");

            // 6. 엔진 배율 · 주문 없음
            Check("필요 기사: 엔진 Lv2(×1.12)여도 저녁 4 · 주문 0이면 0 · 처리량 0이면 0",
                배달플랫폼Policy.필요기사(사가정, 0.38, 4, 3.5, 1.12) == 4 && 배달플랫폼Policy.필요기사(0, 0.38, 4, 3.5, 1) == 0 &&
                배달플랫폼Policy.필요기사(사가정, 0.38, 4, 0, 1) == 0);

            // 7. 지사 묶기: 운영 동 셋은 담당, 나머지 셋은 지사 밖
            var rows = new List<플랫폼동줄>
            {
                new() { 코드 = "1126056500", 이름 = "면목본동", 운영 = true, 기사 = 2, 제휴 = 3, 하루주문 = 54.61, 놓친비율 = 0.05 },
                new() { 코드 = "1126057500", 이름 = "면목제3.8동", 운영 = true, 기사 = 2, 제휴 = 4, 하루주문 = 45.3, 놓친비율 = 0.12 },
                new() { 코드 = "1126057000", 이름 = "면목제7동", 운영 = true, 기사 = 2, 제휴 = 2, 하루주문 = 43.8, 놓친비율 = 0.07 },
                new() { 코드 = "1126052000", 이름 = "면목제2동", 하루주문 = 43.0 },
                new() { 코드 = "1126055000", 이름 = "면목제5동", 하루주문 = 19.1 },
                new() { 코드 = "1126054000", 이름 = "면목제4동", 하루주문 = 34.2 },
            };
            var 지사 = 배달플랫폼Policy.지사묶기(rows);
            Check("지사 묶기: 담당 3 · 기사 6 · 제휴 9 · 하루 143.7 · 지사 밖 3 · 96.3",
                지사.담당.Count == 3 && 지사.기사 == 6 && 지사.제휴 == 9 && Near(지사.하루주문, 143.71, 0.01) &&
                지사.지사밖.Count == 3 && Near(지사.지사밖하루주문, 96.3, 0.01) && 지사.지사밖[0].이름 == "면목제2동",
                $"{지사.하루주문:0.0} · {지사.지사밖하루주문:0.0}");

            // 8. 받아낸 비율은 하루 주문으로 가중: (54.61·0.95 + 45.3·0.88 + 43.8·0.93) / 143.71 ≈ 0.922
            Check("받아냄 = 하루 주문으로 가중한 1 − 놓친 비율 ≈ 92%",
                지사.받아냄 != null && Near(지사.받아냄.Value, 0.9218, 0.001) && 배달플랫폼Policy.비율글(지사.받아냄) == "92%",
                배달플랫폼Policy.비율글(지사.받아냄));

            // 9. 운영 동이 없으면 받아냄은 0이 아니라 모름
            var 빈 = 배달플랫폼Policy.지사묶기(rows.Where(row => !row.운영));
            Check("운영 동이 없으면 받아냄은 null(자료 준비 전) · 지사 밖은 셋",
                빈.받아냄 == null && 배달플랫폼Policy.비율글(빈.받아냄) == "자료 준비 전" && 빈.지사밖.Count == 3 && 빈.기사 == 0);

            // 10. 오늘 정할 일: 저녁 부족 → 지사 밖 → 제휴 여유, 많아야 셋
            var todo = 배달플랫폼Policy.정할일(지사, 8, 2);
            var calm = 배달플랫폼Policy.정할일(지사, 4, 0);
            Check("오늘 정할 일: 저녁 기사 2명 부족 · 지사 밖 96건 · 제휴 2곳 / 부족 없으면 지사 밖만",
                todo.Count == 3 && todo[0] == "사가정 지사 · 저녁 기사 2명 부족" && todo[1].Contains("96건") && todo[2].Contains("2곳") &&
                calm.Count == 1 && calm[0].StartsWith("지사 밖"), string.Join(" / ", todo));
            return results;
        }
    }
}
