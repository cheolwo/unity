using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // r154·r157 (2026-11-13): 새 주문을 지도에서 알아차리게 하는 표현의 순수 규칙. Unity를 쓰지 않는다.
    // 업무 상태(주문·정산·시간)를 바꾸지 않고, 화면이 받은 상태 사본만 읽는다.
    //  - 회차를 새로 시작하거나 저장을 불러오면 그때 있는 주문을 기준선으로 삼는다. 떨어뜨리지 않는다.
    //  - 정상 진행에서 이 회차에 처음 보는 주문 중 접수 사건(order_received)이 확인된 것만 새 주문이다.
    //  - 다시 받은 같은 상태, 확대·이동, 지도 단계 전환으로는 다시 생기지 않는다.
    // Core 보장(Ssalddel.Sagajeong.Core): 주문마다 접수 사건은 한 번이고, 캠페인 주문 번호는 "day-N:"으로 회차 안에서 겹치지 않는다.

    /// <summary>상태 사본에서 읽은 주문 하나.</summary>
    internal readonly struct 지도주문관찰
    {
        internal 지도주문관찰(string orderId, string storeId, bool received)
        {
            OrderId = orderId;
            StoreId = storeId;
            Received = received;
        }

        internal string OrderId { get; }
        internal string StoreId { get; }
        /// <summary>그 주문의 order_received 사건이 상태 사본에 있다.</summary>
        internal bool Received { get; }
    }

    /// <summary>한 번의 관찰에서 같은 매장에 새로 들어온 주문들.</summary>
    internal sealed class 새주문묶음
    {
        internal 새주문묶음(string storeId) => StoreId = storeId;

        internal string StoreId { get; }
        internal List<string> OrderIds { get; } = new List<string>();
        internal int Count => OrderIds.Count;
    }

    /// <summary>회차 안에서 처음 보는 주문을 찾는다.</summary>
    internal sealed class 지도새주문표현Policy
    {
        private static readonly IReadOnlyList<새주문묶음> None = Array.Empty<새주문묶음>();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        internal bool Primed { get; private set; }
        internal int SeenCount => seen.Count;

        /// <summary>회차 시작·저장 복원. 지금 있는 주문은 안정 표식이며 연출하지 않는다. 이전 회차의 기록은 버린다.</summary>
        internal void Baseline(IEnumerable<지도주문관찰> orders)
        {
            seen.Clear();
            if (orders != null)
            {
                foreach (var order in orders)
                    if (!string.IsNullOrEmpty(order.OrderId)) seen.Add(order.OrderId);
            }
            Primed = true;
        }

        /// <summary>
        /// 정상 진행에서 받은 상태 사본. 처음 보는 주문을 매장별로 묶어 처음 나온 차례대로 돌려준다.
        /// 접수 사건을 확인할 수 없거나 매장이 없는 주문은 본 것으로만 적고 연출하지 않는다.
        /// </summary>
        internal IReadOnlyList<새주문묶음> Observe(IEnumerable<지도주문관찰> orders)
        {
            if (!Primed)
            {
                Baseline(orders);
                return None;
            }
            if (orders == null) return None;
            List<새주문묶음> groups = null;
            foreach (var order in orders)
            {
                if (string.IsNullOrEmpty(order.OrderId) || !seen.Add(order.OrderId)) continue;
                if (!order.Received || string.IsNullOrEmpty(order.StoreId)) continue;
                groups ??= new List<새주문묶음>();
                var group = groups.Find(item => string.Equals(item.StoreId, order.StoreId, StringComparison.Ordinal));
                if (group == null)
                {
                    group = new 새주문묶음(order.StoreId);
                    groups.Add(group);
                }
                group.OrderIds.Add(order.OrderId);
            }
            return groups ?? None;
        }
    }

    /// <summary>화면에 남아 있는 새 주문 연출 하나. 시간은 실제 초(배속과 무관)다.</summary>
    internal sealed class 새주문연출
    {
        internal string StoreId;
        internal int Count;
        internal double StartedAt;      // 낙하가 시작한 때
        internal double TagUntil;       // "새 주문 +N"이 사라지는 때
        internal bool Animated;         // 낙하·파동을 그린다. 간단히·화면 밖·늦은 연출은 false
        internal bool Resolved;         // 처음 그릴 때 화면 안인지·늦지 않았는지 정했다
    }

    /// <summary>새 주문 연출의 짧은 큐. 그리기는 화면이 하고, 언제 무엇을 그릴지만 정한다.</summary>
    internal sealed class 지도새주문연출큐
    {
        internal const double DropSeconds = 0.42;     // 핀이 내려와 꽂히는 시간 (r154 검토 시작값 0.35~0.45)
        internal const double RippleSeconds = 1.0;    // 착지 파동 (r154 검토 시작값 약 1초)
        internal const double PinSeconds = 2.0;       // 핀이 사라지기까지. 뒤에는 기존 매장 표식과 대기 건수만 남는다
        internal const double TagSeconds = 3.0;       // "새 주문 +N"을 보이는 시간
        internal const double MergeSeconds = 1.5;     // 같은 매장에 이 안에 또 들어오면 새로 떨어뜨리지 않고 수만 늘린다
        internal const double StaleSeconds = 0.5;     // 처음 그린 때가 이만큼 늦으면(창을 내렸다 올림 등) 연출을 생략한다
        internal const int MaxAnimated = 3;           // 동시에 움직이는 연출 수

        private readonly List<새주문연출> items = new List<새주문연출>();

        internal IReadOnlyList<새주문연출> Items => items;

        internal void Add(IReadOnlyList<새주문묶음> groups, double now, bool effectsOn)
        {
            if (groups == null) return;
            foreach (var group in groups)
            {
                if (group == null || group.Count <= 0 || string.IsNullOrEmpty(group.StoreId)) continue;
                var merged = items.Find(item => string.Equals(item.StoreId, group.StoreId, StringComparison.Ordinal) &&
                    now - item.StartedAt < MergeSeconds);
                if (merged != null)
                {
                    merged.Count += group.Count;
                    merged.TagUntil = now + TagSeconds;
                    continue;
                }
                items.Add(new 새주문연출
                {
                    StoreId = group.StoreId,
                    Count = group.Count,
                    StartedAt = now,
                    TagUntil = now + TagSeconds,
                    Animated = effectsOn && MovingCount(now) < MaxAnimated,
                });
            }
        }

        /// <summary>처음 그릴 때 한 번 부른다. 화면 밖이거나 늦었으면 움직임 없이 남은 정보만 보인다. 나중에 다시 재생하지 않는다.</summary>
        internal void Resolve(새주문연출 item, bool visible, double now)
        {
            if (item == null || item.Resolved) return;
            item.Resolved = true;
            if (!visible || now - item.StartedAt > StaleSeconds) item.Animated = false;
        }

        /// <summary>효과를 끈 순간. 움직임만 정리하고 "새 주문 +N"은 남긴다. 다시 켜도 지난 것을 재생하지 않는다.</summary>
        internal void StopMotion()
        {
            foreach (var item in items) item.Animated = false;
        }

        /// <summary>회차가 바뀌면 지난 연출은 버린다.</summary>
        internal void Clear() => items.Clear();

        internal void Expire(double now) => items.RemoveAll(item => now >= item.TagUntil && now - item.StartedAt >= PinSeconds);

        private int MovingCount(double now)
        {
            var count = 0;
            foreach (var item in items)
                if (item.Animated && now - item.StartedAt < DropSeconds + RippleSeconds) count++;
            return count;
        }
    }
}
