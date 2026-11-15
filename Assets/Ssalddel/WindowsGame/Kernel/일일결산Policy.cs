using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// r174 일일 결산의 순수 규칙 (걸음 2, 2026-11-14).
    /// 공통 원장의 줄을, 그 줄을 적은 현실 시각(LedgerEntry.RealAtUtcMs)의 한국표준시 날짜로 묶어 읽는다.
    /// 읽기만 한다. 원장·시계·돈·재고를 바꾸지 않고, 날짜를 만들거나 넘기지 않는다.
    /// 결산은 보고다. 정산(금액 확정)이나 지급(돈의 이동)을 여기서 만들지 않고, 원장에 적힌 묶음 이름을 그대로 보여 준다.
    /// 가상 시각(AtMs)은 날짜로 읽지 않는다. 현실 시각이 없는 줄은 날짜 미연결로 따로 묶는다.
    /// </summary>
    public static class 일일결산Policy
    {
        /// <summary>현실 시각이 없는 줄의 날짜 키.</summary>
        public const string Unlinked = "";
        private const long KstOffsetMs = 9L * 60 * 60 * 1000;
        private const long DayMs = 24L * 60 * 60 * 1000;

        /// <summary>현실 시각(UTC 밀리초) → 한국표준시 날짜 "yyyy-MM-dd". 0 이하면 날짜 미연결. PC의 지역 시각에 기대지 않는다.</summary>
        public static string DateKey(long realAtUtcMs)
        {
            if (realAtUtcMs <= 0) return Unlinked;
            var days = (realAtUtcMs + KstOffsetMs) / DayMs;
            return new DateTime(1970, 1, 1).AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>원장을 날짜별로 묶는다. 줄이 있는 날짜만 나온다. 날짜 오름차순이고 날짜 미연결이 맨 앞이다.</summary>
        public static List<일일결산> ByDate(IEnumerable<LedgerEntry> ledger)
        {
            var reports = new List<일일결산>();
            if (ledger == null) return reports;
            var days = ledger.Where(entry => entry != null).GroupBy(entry => DateKey(entry.RealAtUtcMs))
                .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (var day in days) reports.Add(Build(day.Key, day));
            return reports;
        }

        /// <summary>한 날짜의 보고. 그 날짜에 줄이 없으면 줄 0개인 보고를 돌려준다(0원 줄을 만들지 않는다).</summary>
        public static 일일결산 For(IEnumerable<LedgerEntry> ledger, string dateKey)
        {
            var key = dateKey ?? Unlinked;
            var entries = ledger == null
                ? new List<LedgerEntry>()
                : ledger.Where(entry => entry != null && DateKey(entry.RealAtUtcMs) == key).ToList();
            return Build(key, entries);
        }

        /// <summary>"yyyy-MM-dd" 모양의 날짜인가.</summary>
        public static bool IsDateKey(string value)
        {
            DateTime parsed;
            return !string.IsNullOrEmpty(value) &&
                DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
        }

        /// <summary>
        /// 보고의 판본. 묶인 줄이 같으면 같은 글자이고, 줄이 더 생기면 달라진다.
        /// 저장하고 되살려도 같도록 금액은 소수 자릿수와 무관하게 적는다.
        /// </summary>
        public static string Version(일일결산 report)
        {
            if (report == null) return string.Empty;
            var parts = report.Lines.Select(line => line.Kind + "|" + line.OsId + "|" + line.Bucket + "|" + line.Unit + "=" +
                line.Amount.ToString("0.####", CultureInfo.InvariantCulture) + "x" + line.Count.ToString(CultureInfo.InvariantCulture));
            return report.Entries.ToString(CultureInfo.InvariantCulture) + ";" + string.Join(";", parts);
        }

        /// <summary>읽음 상태. readVersion은 확인할 때 적어 둔 판본이고, 확인한 적이 없으면 null이나 빈 문자열이다.</summary>
        public static string Reading(일일결산 report, string readVersion)
        {
            if (report == null || report.DateKey == Unlinked) return 일일결산읽음.NotApplicable;
            if (string.IsNullOrEmpty(readVersion)) return 일일결산읽음.Unread;
            return readVersion == Version(report) ? 일일결산읽음.Read : 일일결산읽음.Changed;
        }

        /// <summary>
        /// 오늘보다 앞선 날짜 가운데 아직 확인하지 않은 보고의 수(읽지 않음과, 확인한 뒤 줄이 더 생긴 것). r174 3절의 조용한 안내에만 쓴다.
        /// 줄이 있는 날짜만 들어오므로 빈 날짜는 세지 않는다. 오늘과 날짜 미연결 묶음도 세지 않는다. 이 수는 아무것도 막지 않는다.
        /// </summary>
        public static int PendingPast(IEnumerable<(string DateKey, string Reading)> days, string todayKey)
        {
            if (days == null || !IsDateKey(todayKey)) return 0;
            return days.Count(day => IsDateKey(day.DateKey) && string.CompareOrdinal(day.DateKey, todayKey) < 0 &&
                (day.Reading == 일일결산읽음.Unread || day.Reading == 일일결산읽음.Changed));
        }

        private static 일일결산 Build(string dateKey, IEnumerable<LedgerEntry> entries)
        {
            var report = new 일일결산 { DateKey = dateKey };
            foreach (var entry in entries)
            {
                report.Entries++;
                var line = report.Lines.FirstOrDefault(item =>
                    item.Kind == entry.Kind && item.OsId == entry.OsId && item.Bucket == entry.Bucket && item.Unit == entry.Unit);
                if (line == null)
                {
                    line = new 일일결산줄 { Kind = entry.Kind, OsId = entry.OsId, Bucket = entry.Bucket, Unit = entry.Unit };
                    report.Lines.Add(line);
                }
                line.Amount += entry.Amount;
                line.Count++;
            }
            report.Lines.Sort(Compare);
            return report;
        }

        private static int Compare(일일결산줄 a, 일일결산줄 b)
        {
            var order = string.CompareOrdinal(a.Kind, b.Kind);
            if (order == 0) order = string.CompareOrdinal(a.OsId, b.OsId);
            if (order == 0) order = string.CompareOrdinal(a.Bucket, b.Bucket);
            if (order == 0) order = string.CompareOrdinal(a.Unit, b.Unit);
            return order;
        }
    }

    /// <summary>한 날짜(한국표준시)의 결산 보고. 진행 중인 날도 같은 모양이다.</summary>
    public sealed class 일일결산
    {
        /// <summary>"yyyy-MM-dd". 빈 문자열이면 날짜 미연결.</summary>
        public string DateKey = string.Empty;
        /// <summary>이 날짜에 묶인 원장 줄 수.</summary>
        public int Entries;
        public List<일일결산줄> Lines = new List<일일결산줄>();
    }

    /// <summary>결산 보고의 읽음 상태. 읽음은 보고를 봤다는 표시일 뿐이고 정산·지급과 무관하다.</summary>
    public static class 일일결산읽음
    {
        /// <summary>확인한 적이 없다.</summary>
        public const string Unread = "unread";
        /// <summary>확인한 판본과 지금 보고가 같다.</summary>
        public const string Read = "read";
        /// <summary>확인한 뒤 줄이 더 생겼다(진행 중인 날).</summary>
        public const string Changed = "changed";
        /// <summary>날짜 미연결 묶음. 확인 대상이 아니다.</summary>
        public const string NotApplicable = "none";
    }

    /// <summary>같은 종류·사업·묶음·단위의 합. 묶음 이름은 원장에 적힌 그대로다(sales, spent, supply-payable …).</summary>
    public sealed class 일일결산줄
    {
        public string Kind = string.Empty;
        public string OsId = string.Empty;
        public string Bucket = string.Empty;
        public string Unit = string.Empty;
        public decimal Amount;
        public int Count;
    }
}
