using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    // 기사 무념 규칙의 순수 검사. 빌드의 kernel-checks.txt와 Checks~/RiderMind가 같은 검사를 돌린다.
    public static class 기사무념Checks
    {
        private const string A = "management-driver-a";
        private const string B = "management-driver-b";

        public static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // ── 시작 값 ──
            var state = new 기사무념State();
            기사무념Policy.Observe(state, Seen(1, 690));
            var a = state.FindRider(A);
            var b = state.FindRider(B);
            Check("시작: 기사 A는 신사 6분·전차, 기사 B는 문사 8분·절제",
                a != null && b != null && a.Grade == 0 && a.Capacity == 6 && a.TarotId == "chariot" &&
                b.Grade == 1 && b.Capacity == 8 && b.TarotId == "temperance",
                a == null || b == null ? "기사 없음" : a.Capacity + "분 · " + b.Capacity + "분");
            var gwangjin = 기사무념Policy.Rider(new 기사무념State(), "management-driver-gwangjin-a");
            Check("시작: 광진 기사 A는 학사 10분", gwangjin.Grade == 2 && gwangjin.Capacity == 10);

            // ── 업무 중 ──
            기사무념Policy.Observe(state, Seen(1, 692, Order("o1", A, false), Departed("o1", 690)));
            var live = 기사무념Policy.Live(state, A, 1, 692);
            Check("업무 중: 출발 뒤 흐른 2분만큼 경 시간이 준다",
                live.Working && Near(live.Used, 2) && Near(live.Remaining, 4) && !live.Broken, Text(live));
            live = 기사무념Policy.Live(state, A, 1, 689.99);
            Check("업무 중: 지금 시각이 출발보다 살짝 앞서도 0분이다(1,440분을 더하지 않는다)",
                Near(live.Used, 0) && !live.Broken, Text(live));

            // ── 전달·정산 ──
            var done = Seen(1, 700, Order("o1", A, true), Departed("o1", 690), Delivered("o1", 692.5), Paid("o1", "가상 기사 보수 발생 · 3,600원"));
            기사무념Policy.Observe(state, done);
            live = 기사무념Policy.Live(state, A, 1, 700);
            Check("정산: 2분 30초 유지 · 보수 3,600원이 모두 무념 구간에 이어진다",
                !live.Working && Near(live.Maintained, 2.5) && live.Settled == 1 && live.Pay == 3600 && live.Linked == 3600, Text(live));
            기사무념Policy.Observe(state, done);
            var again = 기사무념Policy.Live(state, A, 1, 700);
            Check("같은 상태를 다시 받아도 결과가 같다", state.Works.Count == 1 && Near(again.Used, live.Used) && again.Linked == live.Linked,
                "업무 " + state.Works.Count + "건");
            var snapshot = 기사무념Policy.Snapshot(state, done);
            Check("사본: 경 유지 2분 30초 · 경 시간당 86,400원",
                Near(snapshot.MaintainedMinutes, 2.5) && snapshot.LinkedIncome == 3600 && snapshot.BrokenCount == 0 &&
                Near(snapshot.LinkedIncome / (snapshot.MaintainedMinutes / 60), 86400),
                기사무념Policy.Duration(snapshot.MaintainedMinutes) + " · " + snapshot.LinkedIncome + "원");

            // ── 배차 판정 ──
            Check("배차 판정: 남은 3분 30초는 괜찮다", 기사무념Policy.Assignable(live) == 기사무념배차판정.Ok, Text(live));
            var tight = new 기사무념State();
            기사무념Policy.Observe(tight, Seen(1, 700, Order("o1", A, true), Departed("o1", 690), Delivered("o1", 693)));
            Check("배차 판정: 남은 3분은 주의(대표 업무 2분 30초 + 여유 1분보다 적다)",
                기사무념Policy.Assignable(기사무념Policy.Live(tight, A, 1, 700)) == 기사무념배차판정.Low);

            // ── 무념 깨짐 ──
            var broken = new 기사무념State();
            var over = Seen(1, 700, Order("o1", A, true), Departed("o1", 690), Delivered("o1", 697), Paid("o1", "3,600원"));
            기사무념Policy.Observe(broken, over);
            live = 기사무념Policy.Live(broken, A, 1, 700);
            Check("깨짐: 7분 업무는 6분에서 깨지고 보수는 6/7만 이어진다",
                live.Broken && live.BrokenAt.HasValue && Near(live.BrokenAt.Value, 696) && Near(live.Maintained, 6) &&
                Near(live.Remaining, 0) && live.Linked == 3086, Text(live) + " · 이어진 보수 " + live.Linked);
            Check("깨짐: 새 배차를 받지 않는다", 기사무념Policy.Assignable(live) == 기사무념배차판정.Broken);
            var flow = 기사무념Policy.Snapshot(broken, over);
            Check("흐름: 깨진 기사는 맡은 주문이 있을 때만 배송 여력으로 센다",
                flow.FlowOrders == 1 && flow.FlowPrepare == 2 && flow.FlowDeliver == 2 && flow.BrokenCount == 1,
                "주문 " + flow.FlowOrders + " · 준비 " + flow.FlowPrepare + " · 배송 " + flow.FlowDeliver);

            // ── 점검 ──
            Check("점검: 처음은 적히고 같은 날 다시는 적히지 않는다",
                기사무념Policy.Check(state, A) && !기사무념Policy.Check(state, A) && state.FindRider(A).CheckedDay == 1);
            Check("점검: 없는 기사는 거절", !기사무념Policy.Check(state, "없는-기사"));

            // ── 하루 마감 ──
            기사무념Policy.Observe(state, Seen(2, 690));
            a = state.FindRider(A);
            b = state.FindRider(B);
            Check("마감: 점검하고 정산한 근무는 유효 · 유지 가능 시간 +1분",
                a.ValidDays == 1 && a.Capacity == 7 && a.Grade == 0 && a.Qualifying == 0 && a.LastDayResult.Contains("유효 근무"), a.LastDayResult);
            Check("마감: 배달이 없던 기사는 기록 없음", b.ValidDays == 0 && b.Capacity == 8 && b.LastDayResult.Contains("배달 기록 없음"), b.LastDayResult);
            live = 기사무념Policy.Live(state, A, 2, 690);
            Check("마감: 다음 날은 경 시간이 회복된다", Near(live.Remaining, 7) && Near(live.Used, 0) && !live.Broken, Text(live));
            Check("마감: 점검은 날마다 새로 한다", !기사무념Policy.Snapshot(state, Seen(2, 690)).Find(A).CheckedToday);

            var unchecked1 = new 기사무념State();
            기사무념Policy.Observe(unchecked1, done);
            기사무념Policy.Observe(unchecked1, Seen(2, 690));
            Check("마감: 점검 없이 한 근무는 유효 기록이 아니다",
                unchecked1.FindRider(A).ValidDays == 0 && unchecked1.FindRider(A).Capacity == 6, unchecked1.FindRider(A).LastDayResult);
            기사무념Policy.Check(broken, A);
            기사무념Policy.Observe(broken, Seen(2, 690));
            Check("마감: 무념이 깨진 근무는 유효 기록이 아니고 휴식으로 회복된다",
                broken.FindRider(A).ValidDays == 0 && !기사무념Policy.Live(broken, A, 2, 690).Broken, broken.FindRider(A).LastDayResult);

            // ── 승급 ──
            var grow = new 기사무념State();
            기사무념Policy.Observe(grow, Seen(1, 600));
            grow.FindRider(A).Capacity = 9;   // 유지 가능 시간이 이미 9분까지 자란 신사
            for (var day = 1; day <= 2; day++)
            {
                기사무념Policy.Observe(grow, Seen(day, 600));
                기사무념Policy.Check(grow, A);
                var id = "day-" + day + ":o1";
                기사무념Policy.Observe(grow, Seen(day, 700, Order(id, A, true), Departed(id, 690), Delivered(id, 698.5)));
            }
            var first = grow.FindRider(A).Qualifying;
            기사무념Policy.Observe(grow, Seen(3, 600));
            a = grow.FindRider(A);
            Check("승급: 다음 등급 기준(8분) 이상을 유지한 유효 근무 두 번이면 문사",
                first == 1 && a.Grade == 1 && a.Qualifying == 0 && a.ValidDays == 2 && a.Capacity == 11 && a.LastDayResult.Contains("문사 승급"),
                a.LastDayResult);
            Check("일차: 주문 번호의 day-N으로 그날 업무에 넣는다",
                grow.Works.Count == 2 && grow.Works.Any(work => work.Day == 1) && grow.Works.Any(work => work.Day == 2));

            // ── 새 회차 ──
            기사무념Policy.Observe(grow, Seen(1, 600));
            Check("새 회차: 일차가 뒤로 가면 기록을 버리고 처음 값으로",
                grow.Day == 1 && grow.Works.Count == 0 && grow.FindRider(A).Grade == 0 && grow.FindRider(A).Capacity == 6);

            // ── 시각·글자 ──
            Check("시각: 11:30:30과 ISO 시각을 같은 분으로 읽는다",
                Near(기사무념Policy.ClockMinutes("11:30:30"), 690.5) && Near(기사무념Policy.ClockMinutes("2026-11-12T11:30:30Z"), 690.5));
            Check("글자: 3.73분은 3분 44초, 6분은 6분",
                기사무념Policy.Duration(3.7333) == "3분 44초" && 기사무념Policy.Duration(6) == "6분",
                기사무념Policy.Duration(3.7333) + " · " + 기사무념Policy.Duration(6));
            return results;
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) < 0.0001;

        private static string Text(기사무념Live live) =>
            "사용 " + 기사무념Policy.Duration(live.Used) + " · 유지 " + 기사무념Policy.Duration(live.Maintained) + " · 남음 " +
            기사무념Policy.Duration(live.Remaining) + (live.Broken ? " · 깨짐" : string.Empty);

        private static 기사무념관측 Seen(int day, double now, params object[] items)
        {
            var seen = new 기사무념관측 { Day = day, NowMinutes = now, StoreCount = 2 };
            seen.CourierIds.Add(A);
            seen.CourierIds.Add(B);
            foreach (var item in items)
            {
                if (item is 기사무념관측Order order) seen.Orders.Add(order);
                else if (item is 기사무념관측Event entry) seen.Events.Add(entry);
            }
            return seen;
        }

        private static 기사무념관측Order Order(string id, string driver, bool settled) =>
            new 기사무념관측Order { Id = id, DriverId = driver, Settled = settled };

        private static 기사무념관측Event Departed(string order, double at) =>
            new 기사무념관측Event { OrderId = order, Code = "driver_departed", AtMinutes = at };

        private static 기사무념관측Event Delivered(string order, double at) =>
            new 기사무념관측Event { OrderId = order, Code = "food_delivered", AtMinutes = at };

        private static 기사무념관측Event Paid(string order, string text) =>
            new 기사무념관측Event { OrderId = order, Code = "driver_pay_accrued", AtMinutes = 0, Text = text };
    }
}
