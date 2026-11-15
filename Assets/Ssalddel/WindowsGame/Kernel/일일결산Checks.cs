using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// r174 일일 결산(걸음 2)의 관문 검사. 빌드할 때 Editor가 돌리고 kernel-checks.txt에 붙는다.
    /// 현실 시각을 고정해 넣고 본다(r174 6절 5번). 사업 모듈 없이 커널과 원장만으로 돌린다.
    /// </summary>
    public static class 일일결산Checks
    {
        public static string Run()
        {
            var lines = new List<string>();
            var failed = 0;
            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (!ok) failed++;
                lines.Add((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
            };
            try
            {
                Rules(check);
            }
            catch (Exception exception)
            {
                check("일일 결산: 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message);
            }
            try
            {
                Reading(check);
            }
            catch (Exception exception)
            {
                check("결산 확인: 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message);
            }
            try
            {
                Past(check);
            }
            catch (Exception exception)
            {
                check("지난 결산 안내: 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message);
            }
            return "Hongdal daily report checks: " + (lines.Count - failed) + " passed, " + failed + " failed\n" + string.Join("\n", lines);
        }

        private static long Utc(int year, int month, int day, int hour, int minute)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc) - epoch).TotalMilliseconds;
        }

        private static LedgerEntry Money(string bucket, decimal amount, long atMs)
        {
            return new LedgerEntry
            {
                Kind = LedgerKinds.Money, OsId = "os:check", PlaceId = "place:check", ItemId = "krw-simulation",
                Bucket = bucket, Amount = amount, Unit = "원", AtMs = atMs, Reason = "검사", RefId = "check",
            };
        }

        private static GameCommand Confirm(string date, string commandId)
        {
            return new GameCommand { OsId = 일일결산Module.Id, Action = 일일결산Module.ConfirmAction, CommandId = commandId }.With("date", date);
        }

        private static List<일일결산일> Days(일일결산Module module)
        {
            return ((일일결산Snapshot)module.Snapshot()).Days;
        }

        // r174 결산 확인(걸음 4): 읽음 상태만 바뀌고, 지난 보고를 확인하지 않아도 오늘 일이 막히지 않는다.
        private static void Reading(Action<string, bool, string> check)
        {
            var kernel = new GameKernel();
            var module = new 일일결산Module();
            kernel.Register(module);
            kernel.Start(1);
            kernel.RealNowUtcMs = Utc(2026, 11, 14, 3, 0);          // 한국표준시 14일 12:00
            kernel.Record(Money("sales", 4500m, 1000));
            var before = Days(module);
            check("확인하기 전에는 읽지 않음으로 보임", before.Count == 1 && before[0].Reading == 일일결산읽음.Unread, null);

            var ledgerCount = kernel.Ledger.Count;
            var elapsed = kernel.ElapsedMs;
            var confirmed = kernel.Confirm(Confirm("2026-11-14", "read-1"));
            check("결산을 확인하면 읽음 상태만 바뀜 (원장·시계 그대로)",
                confirmed.Ok && Days(module)[0].Reading == 일일결산읽음.Read && kernel.Ledger.Count == ledgerCount && kernel.ElapsedMs == elapsed,
                confirmed.Message);

            var stateBefore = module.Save();
            var again = kernel.Confirm(Confirm("2026-11-14", "read-2"));
            check("같은 날짜·같은 판본을 다시 확인해도 달라지는 것이 없음 (r174 3절)",
                again.Ok && again.Code == "AlreadyRead" && module.Save() == stateBefore && kernel.Ledger.Count == ledgerCount, again.Message);

            kernel.Record(Money("sales", 4500m, 2000));               // 같은 날 줄이 더 생긴다
            var changed = Days(module)[0].Reading;
            var reread = kernel.Confirm(Confirm("2026-11-14", "read-3"));
            check("확인한 뒤 줄이 더 생기면 바뀜으로 보이고, 다시 확인하면 읽음이 됨",
                changed == 일일결산읽음.Changed && reread.Ok && Days(module)[0].Reading == 일일결산읽음.Read, changed);

            var empty = kernel.Confirm(Confirm("2026-11-13", "read-4"));
            var unreadable = kernel.Confirm(Confirm("어제", "read-5"));
            check("기록이 없는 날짜나 읽을 수 없는 날짜는 확인으로 남기지 않음",
                !empty.Ok && empty.Code == "NoRecords" && !unreadable.Ok && unreadable.Code == "InvalidDate" && Days(module).Count == 1,
                empty.Code + ", " + unreadable.Code);

            var saved = kernel.Save();
            var copy = new GameKernel();
            var copyModule = new 일일결산Module();
            copy.Register(copyModule);
            var restoredOk = copy.Restore(saved);
            check("저장하고 되살려도 확인 상태가 남음",
                restoredOk && Days(copyModule).Count == 1 && Days(copyModule)[0].Reading == 일일결산읽음.Read, null);

            kernel.RealNowUtcMs = Utc(2026, 11, 15, 3, 0);
            kernel.Record(Money("sales", 100m, 3000));
            kernel.RealNowUtcMs = Utc(2026, 11, 16, 3, 0);
            kernel.Record(Money("sales", 100m, 4000));
            var today = kernel.Confirm(Confirm("2026-11-16", "read-6"));
            var days = Days(module);
            check("지난 보고를 확인하지 않아도 오늘 보고를 확인할 수 있음 (r174 3절)",
                today.Ok && days.Count == 3 && days[1].Reading == 일일결산읽음.Unread && days[2].Reading == 일일결산읽음.Read,
                string.Join(", ", days.Select(day => day.Report.DateKey + " " + day.Reading)));

            var plain = new GameKernel();
            var plainModule = new 일일결산Module();
            plain.Register(plainModule);
            plain.Start(1);
            plain.Record(Money("sales", 100m, 1));
            var plainDays = Days(plainModule);
            check("날짜 미연결 묶음은 확인 대상이 아님",
                plainDays.Count == 1 && plainDays[0].Reading == 일일결산읽음.NotApplicable && !plain.Confirm(Confirm(string.Empty, "read-7")).Ok, null);
        }

        // r174 3절: 날짜가 지난 뒤 아직 확인하지 않은 보고를 조용히 알린다. 세기만 하고 아무것도 막지 않는다.
        private static void Past(Action<string, bool, string> check)
        {
            var kernel = new GameKernel();
            var module = new 일일결산Module();
            kernel.Register(module);
            kernel.Start(1);
            kernel.RealNowUtcMs = Utc(2026, 11, 12, 3, 0);            // 한국표준시 12일 12:00
            kernel.Record(Money("sales", 4500m, 1000));
            kernel.RealNowUtcMs = Utc(2026, 11, 13, 3, 0);
            kernel.Record(Money("sales", 4500m, 2000));
            kernel.RealNowUtcMs = Utc(2026, 11, 14, 3, 0);
            kernel.Record(Money("sales", 4500m, 3000));
            kernel.RealNowUtcMs = 0;                                  // 현실 시각을 모르는 줄(날짜 미연결)
            kernel.Record(Money("sales", 4500m, 4000));

            Func<string, int> pending = today =>
                일일결산Policy.PendingPast(Days(module).Select(day => (day.Report.DateKey, day.Reading)), today);
            check("지난 결산 안내: 오늘보다 앞선 날짜의 확인하지 않은 보고만 셈 (오늘·날짜 미연결 제외)",
                pending("2026-11-14") == 2 && Days(module).Count == 4, pending("2026-11-14").ToString());

            var elapsed = kernel.ElapsedMs;
            var ledgerCount = kernel.Ledger.Count;
            var confirmed = kernel.Confirm(Confirm("2026-11-12", "past-1"));
            check("지난 결산 안내: 확인하면 줄고, 세는 일은 시계와 원장을 바꾸지 않음",
                confirmed.Ok && pending("2026-11-14") == 1 && kernel.ElapsedMs == elapsed && kernel.Ledger.Count == ledgerCount, confirmed.Message);

            check("지난 결산 안내: 날짜가 넘어가면 어제가 지난 날이 되고, 줄이 없는 날짜는 세지 않음",
                pending("2026-11-17") == 2 && pending("2026-11-13") == 0 && pending("2026-11-12") == 0 && pending(null) == 0,
                pending("2026-11-17").ToString());

            kernel.Confirm(Confirm("2026-11-13", "past-2"));
            var none = pending("2026-11-14");
            kernel.RealNowUtcMs = Utc(2026, 11, 13, 5, 0);            // 지난 날짜에 줄이 더 생긴다
            kernel.Record(Money("sales", 4500m, 5000));
            check("지난 결산 안내: 확인한 뒤 줄이 더 생긴 지난 날도 다시 셈", none == 0 && pending("2026-11-14") == 1, null);
        }

        private static void Rules(Action<string, bool, string> check)
        {
            // 한국표준시 11월 14일 23:55와 15일 00:10. UTC로는 둘 다 14일이다(14:55, 15:10).
            var lateNight = Utc(2026, 11, 14, 14, 55);
            var afterMidnight = Utc(2026, 11, 14, 15, 10);
            var noon17 = Utc(2026, 11, 17, 3, 0);
            check("현실 시각을 한국표준시 날짜로 읽음",
                일일결산Policy.DateKey(lateNight) == "2026-11-14" && 일일결산Policy.DateKey(afterMidnight) == "2026-11-15" &&
                일일결산Policy.DateKey(Utc(2026, 11, 14, 15, 0) - 1) == "2026-11-14" && 일일결산Policy.DateKey(Utc(2026, 11, 14, 15, 0)) == "2026-11-15",
                일일결산Policy.DateKey(lateNight) + ", " + 일일결산Policy.DateKey(afterMidnight));

            var kernel = new GameKernel();
            kernel.Start(1);
            kernel.RealNowUtcMs = lateNight;
            var first = Money("sales", 4500m, 5000);
            kernel.Record(first);
            check("커널이 원장 줄에 현실 시각을 함께 적고 가상 시각은 그대로 둠", first.RealAtUtcMs == lateNight && first.AtMs == 5000, null);

            kernel.Record(Money("sales", 4500m, 9000));
            kernel.Record(Money("spent", 46000m, 9500));
            var sameDay = 일일결산Policy.ByDate(kernel.Ledger);
            var sales = sameDay.Count == 1 ? sameDay[0].Lines.FirstOrDefault(line => line.Bucket == "sales") : null;
            check("같은 날 여러 번 적어도 한 날짜에 누적됨 (r174 6절 1)",
                sameDay.Count == 1 && sameDay[0].DateKey == "2026-11-14" && sameDay[0].Entries == 3 &&
                sales != null && sales.Amount == 9000m && sales.Count == 2,
                sales == null ? null : sales.Amount.ToString("0") + "원 · " + sales.Count + "줄");

            kernel.RealNowUtcMs = afterMidnight;
            kernel.Record(Money("sales", 4500m, 12000));
            var twoDays = 일일결산Policy.ByDate(kernel.Ledger);
            check("자정을 넘긴 줄은 다음 날짜에 한 번만 들어감 (r174 6절 2)",
                twoDays.Count == 2 && twoDays[0].DateKey == "2026-11-14" && twoDays[0].Entries == 3 &&
                twoDays[1].DateKey == "2026-11-15" && twoDays[1].Entries == 1 && twoDays.Sum(day => day.Entries) == kernel.Ledger.Count, null);

            kernel.RealNowUtcMs = noon17;
            kernel.Record(Money("sales", 4500m, 20000));
            var withGap = 일일결산Policy.ByDate(kernel.Ledger);
            check("줄이 없는 날짜는 만들지 않음 (r174 6절 3)",
                withGap.Count == 3 && withGap.All(day => day.Entries > 0) && 일일결산Policy.For(kernel.Ledger, "2026-11-16").Entries == 0,
                string.Join(", ", withGap.Select(day => day.DateKey)));

            var before = kernel.Save();
            var elapsed = kernel.ElapsedMs;
            일일결산Policy.ByDate(kernel.Ledger);
            일일결산Policy.For(kernel.Ledger, "2026-11-14");
            check("결산을 몇 번 읽어도 원장·시계·저장이 그대로임 (r174 6절 5)",
                kernel.Save() == before && kernel.ElapsedMs == elapsed && kernel.Ledger.Count == 5, null);

            var restored = new GameKernel();
            var restoredOk = restored.Restore(before);
            check("저장하고 되살려도 현실 시각이 남음",
                restoredOk && restored.Ledger.Count == 5 && restored.Ledger[0].RealAtUtcMs == lateNight && restored.Ledger[4].RealAtUtcMs == noon17, null);

            // 이 칸이 생기기 전의 저장본: 원장 줄에 RealAtUtcMs가 없다.
            var old = JObject.Parse(before);
            foreach (var entry in (JArray)old["ledger"]) ((JObject)entry).Remove("RealAtUtcMs");
            var oldKernel = new GameKernel();
            var oldOk = oldKernel.Restore(old.ToString(Newtonsoft.Json.Formatting.None));
            var oldReports = 일일결산Policy.ByDate(oldKernel.Ledger);
            check("예전 저장본의 줄은 오늘로 넣지 않고 날짜 미연결로 묶음",
                oldOk && oldReports.Count == 1 && oldReports[0].DateKey == 일일결산Policy.Unlinked && oldReports[0].Entries == 5, null);

            var plain = new GameKernel();
            plain.Start(1);
            plain.Record(Money("sales", 100m, 1));
            check("현실 시각을 넣지 않으면 0으로 남음 (다른 검사의 결과가 달라지지 않게)", plain.Ledger[0].RealAtUtcMs == 0, null);

            var mixed = new GameKernel();
            mixed.Start(1);
            mixed.RealNowUtcMs = lateNight;
            mixed.Record(Money("sales", 100m, 1));
            mixed.Record(new LedgerEntry
            {
                Kind = LedgerKinds.Stock, OsId = "os:check", PlaceId = "place:check", ItemId = "item:check",
                Bucket = "available", Amount = 3m, Unit = "개", AtMs = 2,
            });
            var report = 일일결산Policy.For(mixed.Ledger, "2026-11-14");
            check("돈과 재고를 섞지 않고, 원장에 없는 지급 줄을 만들지 않음",
                report.Lines.Count == 2 &&
                report.Lines.Any(line => line.Kind == LedgerKinds.Money && line.Bucket == "sales" && line.Amount == 100m) &&
                report.Lines.Any(line => line.Kind == LedgerKinds.Stock && line.Bucket == "available" && line.Amount == 3m), null);
        }
    }
}
