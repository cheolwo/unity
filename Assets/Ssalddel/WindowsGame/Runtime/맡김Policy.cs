using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 켜 두기 성장 — 맡김의 규칙 뼈대 (2026-11-15).
    // 사용자: 「그냥 켜놓기만 하더라도 알아서 성장 … 경영을 하면 더 잘 성장」, 「너무 구체적으로는 정하지 말고 뼈대 정도만 … 구현까지」.
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-켜두기성장.md (켜1 · 켜2의 뼈대).
    //  - 손이 빈 동안, 플레이어가 아직 하지 않은 일만, 플레이어가 누를 수 있는 명령으로만 한다. 커널 · Core 계약은 그대로다.
    //  - 맡김은 늘 안전한 쪽(엔진이 권한 기사, 지난 방침)만 고른다. 숨은 감속은 없다. 직접 고르면 더 잘 자란다.
    //  - 아래 수치는 모두 시험값이다(정할 것: 계획 7절). 여기서 바꾸면 화면 쪽은 그대로 따른다.
    internal static class 맡김Policy
    {
        internal const double HandsIdleSeconds = 120;       // 손이 빈 뒤 맡기기까지(실제 초)
        internal const long OrderWaitMs = 180_000;          // 주문이 이만큼(게임 시간) 기다리면 맡김이 배차한다
        internal const double PolicyWaitSeconds = 60;       // 방침 국면에서 이만큼(실제 초) 고르지 않으면 지난 방침으로
        internal const double ReturnSummarySeconds = 300;   // 이만큼 손이 빈 뒤 돌아오면 「다녀오신 사이」
        internal const string FirstPolicy = "temperance";   // 지난 방침이 없으면 절제

        internal static bool HandsIdle(double idleSeconds) => idleSeconds >= HandsIdleSeconds;
        internal static bool OrderDue(long waitedMs) => waitedMs >= OrderWaitMs;
        internal static bool PolicyDue(double waitedSeconds, double limit) => waitedSeconds >= limit;
        internal static bool ShowReturn(double idleSeconds, int actions) => idleSeconds >= ReturnSummarySeconds && actions > 0;

        // 배차할 기사: 엔진이 권한 기사가 비어 있고 막히지 않았으면 그 기사, 아니면 목록 순서로 처음 빈 기사. 없으면 null(기다리는 일).
        internal static string PickRider(string recommended, IReadOnlyList<string> free, Func<string, bool> blocked)
        {
            if (free == null || free.Count == 0) return null;
            if (!string.IsNullOrEmpty(recommended) && free.Contains(recommended) && !blocked(recommended)) return recommended;
            return free.FirstOrDefault(id => !blocked(id));
        }

        // 이어 갈 방침: 지난 방침이 아는 카드면 그대로, 아니면 절제, 그것도 없으면 첫 카드.
        internal static string ContinuePolicy(string last, IReadOnlyList<string> known)
        {
            if (known == null || known.Count == 0) return null;
            if (!string.IsNullOrEmpty(last) && known.Contains(last)) return last;
            return known.Contains(FirstPolicy) ? FirstPolicy : known[0];
        }
    }

    internal enum 맡김종류 { 배차, 점검, 방침 }

    // 맡김이 보내 받아들여진 명령의 일지. 화면 쪽에만 있다(커널 원장에는 같은 명령으로 적힌다 — 구분 표시는 Codex · D7이 정할 일).
    internal sealed class 맡김일지
    {
        private readonly List<(long GameMs, 맡김종류 Kind, string Text)> entries = new();
        internal int Count => entries.Count;
        internal IReadOnlyList<(long GameMs, 맡김종류 Kind, string Text)> Entries => entries;
        internal void Add(long gameMs, 맡김종류 kind, string text) => entries.Add((gameMs, kind, text));
        internal int CountSince(int mark, 맡김종류 kind) => entries.Skip(Math.Max(0, mark)).Count(entry => entry.Kind == kind);
        internal void Clear() => entries.Clear();
    }

    // 맡김이 하지 않고 남긴 일(기다리는 일). 같은 열쇠는 한 번만 담는다. 시한 · 벌점이 없다.
    internal sealed class 기다리는일목록
    {
        private readonly List<(string Key, string Text)> items = new();
        internal int Count => items.Count;
        internal IReadOnlyList<(string Key, string Text)> Items => items;

        internal bool Add(string key, string text)
        {
            if (items.Any(item => item.Key == key)) return false;
            items.Add((key, text));
            return true;
        }

        internal bool Remove(string key) => items.RemoveAll(item => item.Key == key) > 0;
        internal void RemoveWhere(Func<string, bool> stale) => items.RemoveAll(item => stale(item.Key));
        internal void Clear() => items.Clear();
    }
}
