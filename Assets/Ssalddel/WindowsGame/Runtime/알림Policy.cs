using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 떴다 사라지는 알림 (2026-11-15, 사용자: 「정보들이 그냥 일어났다 사라지는 … 빛이 좀 더 밝아졌다든지 … 은유적으로」).
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-떴다사라지는알림.md
    // 사용자 결정(같은 날): 빛 + 짧은 글이 기본 · 배달이 정산되면 금액을 띄운다 · 맡김이 못 하는 일만 남긴다 · 하루 마감은 누르면 카드.
    // 이 파일은 무엇을 얼마 동안 보일지의 순수 규칙이다. Unity를 쓰지 않는다. 업무 상태(주문 · 정산 · 시간)를 바꾸지 않는다.
    //  - 시간은 새 주문 표현(r154 · r157, 지도새주문표현Policy.cs)의 틀을 따른다: 나타남 · 머묾 · 사라짐, 짧은 사이의 같은 일은 합친다.
    //  - 수치는 모두 시험값이다.
    internal enum 알림종류 { 배차, 맡김배차, 전달, 정산, 마감, 영업시작, 맡김점검, 맡김방침 }

    /// <summary>알림 정도. 그대로 = 떴다 사라지는 알림을 쓰지 않는다(검증 촬영 · 지금까지의 화면).</summary>
    internal enum 알림정도 { 조용히, 보통, 그대로 }

    internal sealed class 알림
    {
        internal 알림종류 Kind;
        /// <summary>그 자리(매장 · 배송지 · 기사의 ID). 비었으면 화면 위쪽 줄에 뜬다.</summary>
        internal string Key;
        internal string Text;
        internal int Count;
        /// <summary>정산 금액(원). 엔진이 내준 값을 더하기만 한다.</summary>
        internal long Amount;
        internal double StartedAt;
        internal double LastAt;
        internal bool Long;
    }

    internal sealed class 알림큐
    {
        internal const double InSeconds = 0.2;
        internal const double HoldSeconds = 2.5;
        internal const double OutSeconds = 0.8;
        internal const double LongHoldSeconds = 7.0;   // 하루 마감처럼 눌러 볼 수 있는 알림
        internal const double MergeSeconds = 1.5;      // 같은 자리 · 같은 종류가 이 안에 또 오면 수만 늘린다
        internal const int MaxShown = 6;               // 넘치면 가장 오래된 것부터 뺀다

        private readonly List<알림> items = new();
        internal IReadOnlyList<알림> Items => items;

        internal 알림 Add(알림종류 kind, string key, string text, long amount, double now, bool longHold = false)
        {
            key ??= string.Empty;
            foreach (var item in items)
            {
                if (item.Kind != kind || item.Key != key || now - item.LastAt > MergeSeconds || now >= EndOf(item)) continue;
                item.Count++;
                item.Amount += amount;
                item.LastAt = now;
                if (!string.IsNullOrEmpty(text)) item.Text = text;
                return item;
            }
            var added = new 알림 { Kind = kind, Key = key, Text = text ?? string.Empty, Count = 1, Amount = amount, StartedAt = now, LastAt = now, Long = longHold };
            items.Add(added);
            while (items.Count > MaxShown) items.RemoveAt(0);
            return added;
        }

        /// <summary>마지막으로 더해진 때부터 머문다. 합쳐지면 그만큼 더 머문다.</summary>
        internal static double EndOf(알림 item) => item.LastAt + InSeconds + (item.Long ? LongHoldSeconds : HoldSeconds) + OutSeconds;

        internal void Expire(double now) => items.RemoveAll(item => now >= EndOf(item));

        internal void Remove(알림 item) => items.Remove(item);

        internal void Clear() => items.Clear();

        /// <summary>0 → 1(나타남) → 1(머묾) → 0(사라짐).</summary>
        internal static float Alpha(알림 item, double now)
        {
            var age = now - item.StartedAt;
            if (age < 0) return 0f;
            if (age < InSeconds) return (float)(age / InSeconds);
            var end = EndOf(item);
            if (now >= end) return 0f;
            var fadeFrom = end - OutSeconds;
            return now <= fadeFrom ? 1f : (float)((end - now) / OutSeconds);
        }

        /// <summary>보이는 글. 정산은 금액을, 합친 것은 수를 붙인다.</summary>
        internal static string Line(알림 item)
        {
            if (item.Kind == 알림종류.정산)
            {
                var money = item.Amount > 0 ? $"+{item.Amount:N0}원" : "기록";
                return item.Count > 1 ? $"정산 {item.Count}건 {money}" : $"정산 {money}";
            }
            return item.Count > 1 ? $"{item.Text} ×{item.Count}" : item.Text;
        }

        /// <summary>정도에 따라 글을 보이는가. 빛은 조용히에서도 보인다.</summary>
        internal static bool ShowsText(알림정도 level) => level == 알림정도.보통;

        internal static bool Active(알림정도 level) => level != 알림정도.그대로;
    }

    /// <summary>엔진이 내준 「정산을 마친 주문들의 가상 차액 합」에서 이번에 늘어난 몫을 센다. 금액을 스스로 계산하지 않는다.</summary>
    internal sealed class 정산몫
    {
        private int day = int.MinValue;
        private int completed;
        private long balance;

        internal void Baseline(int campaignDay, int completedOrders, long baseBalance)
        {
            day = campaignDay;
            completed = completedOrders;
            balance = baseBalance;
        }

        /// <summary>새 날이면 0부터 다시 센다(엔진의 요약은 그날의 주문만 담는다). 늘어난 금액을 돌려준다. 줄었거나 모르면 0.</summary>
        internal long Take(int campaignDay, int completedOrders, long baseBalance)
        {
            if (campaignDay != day)
            {
                day = campaignDay;
                completed = 0;
                balance = 0;
            }
            var delta = completedOrders > completed && baseBalance > balance ? baseBalance - balance : 0;
            completed = completedOrders;
            balance = baseBalance;
            return delta;
        }
    }
}
