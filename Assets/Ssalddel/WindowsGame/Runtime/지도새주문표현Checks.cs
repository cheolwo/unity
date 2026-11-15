using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    /// <summary>
    /// 새 주문 표현 규칙(r154·r157)의 순수 검사. Unity를 쓰지 않는다.
    /// 빌드할 때 관문 검사(kernel-checks.txt) 뒤에 붙고, `dotnet run --project Checks~/MapOrderEvents`로도 돈다.
    /// </summary>
    internal static class 지도새주문표현Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string Name, bool Ok, string Detail)>();
            void Check(string name, Func<string> body)
            {
                try
                {
                    var failure = body();
                    results.Add((name, failure == null, failure ?? string.Empty));
                }
                catch (Exception exception)
                {
                    results.Add((name, false, exception.GetType().Name + ": " + exception.Message));
                }
            }

            Check("처음 받은 상태는 기준선이고 떨어뜨리지 않는다", () =>
            {
                var policy = new 지도새주문표현Policy();
                var groups = policy.Observe(Orders(("day-1:A", "a", true), ("day-1:B", "b", true)));
                return groups.Count == 0 && policy.Primed && policy.SeenCount == 2 ? null : Describe(groups);
            });

            Check("저장을 불러온 회차의 주문도 기준선이다", () =>
            {
                var policy = new 지도새주문표현Policy();
                policy.Baseline(Orders(("day-3:A", "a", true)));
                var groups = policy.Observe(Orders(("day-3:A", "a", true)));
                return groups.Count == 0 ? null : Describe(groups);
            });

            Check("정상 진행에서 처음 보는 주문 하나", () =>
            {
                var policy = Primed(("day-1:A", "a", true));
                var groups = policy.Observe(Orders(("day-1:A", "a", true), ("day-2:A", "a", true)));
                return groups.Count == 1 && groups[0].StoreId == "a" && groups[0].Count == 1 &&
                    groups[0].OrderIds[0] == "day-2:A" ? null : Describe(groups);
            });

            Check("같은 상태를 다시 받아도(재조회·확대·지도 전환) 다시 생기지 않는다", () =>
            {
                var policy = Primed(("day-1:A", "a", true));
                var orders = Orders(("day-1:A", "a", true), ("day-2:A", "a", true));
                policy.Observe(orders);
                var again = policy.Observe(orders);
                var third = policy.Observe(orders);
                return again.Count == 0 && third.Count == 0 ? null : Describe(again) + " / " + Describe(third);
            });

            Check("같은 매장의 여러 건은 한 묶음 +N", () =>
            {
                var policy = Primed();
                var groups = policy.Observe(Orders(("day-2:A", "a", true), ("day-2:B", "b", true), ("day-2:C", "a", true)));
                return groups.Count == 2 && groups[0].StoreId == "a" && groups[0].Count == 2 &&
                    groups[1].StoreId == "b" && groups[1].Count == 1 ? null : Describe(groups);
            });

            Check("새 회차는 같은 주문 번호도 다시 새 주문이 될 수 있다", () =>
            {
                var policy = Primed(("day-1:A", "a", true));
                var first = policy.Observe(Orders(("day-1:A", "a", true), ("day-2:A", "a", true)));
                policy.Baseline(Orders(("day-1:A", "a", true)));      // 처음부터 새로 시작
                var second = policy.Observe(Orders(("day-1:A", "a", true), ("day-2:A", "a", true)));
                return first.Count == 1 && second.Count == 1 && second[0].OrderIds[0] == "day-2:A"
                    ? null : Describe(first) + " / " + Describe(second);
            });

            Check("접수 사건을 확인할 수 없는 주문은 지어내지 않고 본 것으로만 적는다", () =>
            {
                var policy = Primed();
                var unknown = policy.Observe(Orders(("day-2:A", "a", false)));
                var later = policy.Observe(Orders(("day-2:A", "a", true)));
                return unknown.Count == 0 && later.Count == 0 && policy.SeenCount == 1 ? null : Describe(unknown) + " / " + Describe(later);
            });

            Check("번호나 매장이 빈 주문은 연출하지 않는다", () =>
            {
                var policy = Primed();
                var groups = policy.Observe(Orders((null, "a", true), ("", "a", true), ("day-2:A", null, true), ("day-2:B", "", true)));
                return groups.Count == 0 ? null : Describe(groups);
            });

            Check("한 상태 사본 안의 같은 번호는 한 번만 센다", () =>
            {
                var policy = Primed();
                var groups = policy.Observe(Orders(("day-2:A", "a", true), ("day-2:A", "a", true)));
                return groups.Count == 1 && groups[0].Count == 1 ? null : Describe(groups);
            });

            Check("연출: 효과 사용이면 움직이고, 간단히면 문구만", () =>
            {
                var on = new 지도새주문연출큐();
                on.Add(Group("a", 1), 10, effectsOn: true);
                var off = new 지도새주문연출큐();
                off.Add(Group("a", 1), 10, effectsOn: false);
                return on.Items.Count == 1 && on.Items[0].Animated && off.Items.Count == 1 && !off.Items[0].Animated &&
                    off.Items[0].TagUntil > 10 ? null : "on " + on.Items.Count + " off " + off.Items.Count;
            });

            Check("연출: 같은 매장에 곧 또 오면 다시 떨어뜨리지 않고 수만 늘린다", () =>
            {
                var queue = new 지도새주문연출큐();
                queue.Add(Group("a", 1), 10, true);
                queue.Add(Group("a", 2), 10 + 지도새주문연출큐.MergeSeconds * 0.5, true);
                queue.Add(Group("a", 1), 10 + 지도새주문연출큐.MergeSeconds + 0.1, true);
                return queue.Items.Count == 2 && queue.Items[0].Count == 3 && queue.Items[1].Count == 1
                    ? null : string.Join(",", queue.Items.Select(item => item.StoreId + item.Count));
            });

            Check("연출: 동시에 움직이는 것은 세 개까지, 넘치면 문구만", () =>
            {
                var queue = new 지도새주문연출큐();
                queue.Add(new[] { One("a"), One("b"), One("c"), One("d") }, 10, true);
                var moving = queue.Items.Count(item => item.Animated);
                return queue.Items.Count == 4 && moving == 지도새주문연출큐.MaxAnimated && !queue.Items[3].Animated
                    ? null : "moving " + moving;
            });

            Check("연출: 화면 밖이거나 늦게 그려지면 생략하고 나중에 재생하지 않는다", () =>
            {
                var queue = new 지도새주문연출큐();
                queue.Add(new[] { One("a"), One("b"), One("c") }, 10, true);
                queue.Resolve(queue.Items[0], visible: false, now: 10.05);
                queue.Resolve(queue.Items[1], visible: true, now: 10 + 지도새주문연출큐.StaleSeconds + 0.1);
                queue.Resolve(queue.Items[2], visible: true, now: 10.05);
                queue.Resolve(queue.Items[0], visible: true, now: 10.1);        // 다시 화면에 들어와도
                return !queue.Items[0].Animated && !queue.Items[1].Animated && queue.Items[2].Animated
                    ? null : string.Join(",", queue.Items.Select(item => item.Animated));
            });

            Check("연출: 효과를 끄면 움직임만 멈추고 문구는 남고, 다시 켜도 재생하지 않는다", () =>
            {
                var queue = new 지도새주문연출큐();
                queue.Add(Group("a", 2), 10, true);
                queue.StopMotion();
                var tag = queue.Items[0].TagUntil;
                queue.Add(Array.Empty<새주문묶음>(), 10.2, true);                 // 다시 켬: 새 사건만 대상
                return queue.Items.Count == 1 && !queue.Items[0].Animated && queue.Items[0].Count == 2 && tag > 10.2
                    ? null : "animated " + queue.Items[0].Animated;
            });

            Check("연출: 시간이 지나면 사라지고 업무 수는 바뀌지 않는다", () =>
            {
                var policy = Primed();
                var groups = policy.Observe(Orders(("day-2:A", "a", true), ("day-2:B", "a", true)));
                var queue = new 지도새주문연출큐();
                queue.Add(groups, 10, true);
                queue.Expire(10 + 지도새주문연출큐.TagSeconds - 0.1);
                var kept = queue.Items.Count;
                queue.Expire(10 + Math.Max(지도새주문연출큐.TagSeconds, 지도새주문연출큐.PinSeconds) + 0.1);
                return kept == 1 && queue.Items.Count == 0 && policy.SeenCount == 2 ? null : "kept " + kept + " left " + queue.Items.Count;
            });

            return results;
        }

        private static 지도새주문표현Policy Primed(params (string Id, string Store, bool Received)[] orders)
        {
            var policy = new 지도새주문표현Policy();
            policy.Baseline(Orders(orders));
            return policy;
        }

        private static List<지도주문관찰> Orders(params (string Id, string Store, bool Received)[] orders) =>
            orders.Select(item => new 지도주문관찰(item.Id, item.Store, item.Received)).ToList();

        private static 새주문묶음 One(string store)
        {
            var group = new 새주문묶음(store);
            group.OrderIds.Add(store + "-1");
            return group;
        }

        private static 새주문묶음[] Group(string store, int count)
        {
            var group = new 새주문묶음(store);
            for (var index = 0; index < count; index++) group.OrderIds.Add(store + "-" + index);
            return new[] { group };
        }

        private static string Describe(IReadOnlyList<새주문묶음> groups) =>
            groups.Count == 0 ? "없음" : string.Join(", ", groups.Select(group => group.StoreId + "×" + group.Count));
    }
}
