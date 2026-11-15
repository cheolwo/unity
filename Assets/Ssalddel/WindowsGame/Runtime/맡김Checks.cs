using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    // 켜 두기 성장 — 맡김 규칙의 순수 검사 (2026-11-15). 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 없이 돈다.
    internal static class 맡김Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            Check("손이 정한 시간만큼 비기 전에는 맡기지 않음", !맡김Policy.HandsIdle(맡김Policy.HandsIdleSeconds - 0.1) &&
                맡김Policy.HandsIdle(맡김Policy.HandsIdleSeconds));
            Check("주문은 정한 게임 시간만큼 기다린 뒤에만 맡김이 배차함", !맡김Policy.OrderDue(맡김Policy.OrderWaitMs - 1) &&
                맡김Policy.OrderDue(맡김Policy.OrderWaitMs));

            var free = new List<string> { "a", "b", "c" };
            Check("엔진이 권한 기사가 비어 있으면 그 기사", 맡김Policy.PickRider("b", free, _ => false) == "b");
            Check("권한 기사가 막혀 있으면 처음으로 빈 다른 기사", 맡김Policy.PickRider("a", free, id => id == "a") == "b");
            Check("권한 기사가 빈 목록에 없으면 처음 빈 기사", 맡김Policy.PickRider("z", free, _ => false) == "a");
            Check("모두 막혔거나 빈 기사가 없으면 고르지 않음(기다리는 일로)",
                맡김Policy.PickRider("a", free, _ => true) == null && 맡김Policy.PickRider("a", new List<string>(), _ => false) == null);

            var known = new List<string> { "chariot", "temperance", "empress" };
            Check("지난 방침을 이어 감", 맡김Policy.ContinuePolicy("empress", known) == "empress");
            Check("지난 방침이 없거나 모르는 카드면 절제",
                맡김Policy.ContinuePolicy(null, known) == "temperance" && 맡김Policy.ContinuePolicy("unknown", known) == "temperance");
            Check("카드가 없으면 방침을 고르지 않음", 맡김Policy.ContinuePolicy("empress", new List<string>()) == null);
            Check("방침 기다림이 지나야 이어 감", !맡김Policy.PolicyDue(59.9, 60) && 맡김Policy.PolicyDue(60, 60));
            Check("다녀오신 사이는 오래 비었고 맡김이 한 일이 있을 때만",
                !맡김Policy.ShowReturn(맡김Policy.ReturnSummarySeconds - 1, 3) && !맡김Policy.ShowReturn(맡김Policy.ReturnSummarySeconds + 100, 0) &&
                맡김Policy.ShowReturn(맡김Policy.ReturnSummarySeconds, 1));

            var log = new 맡김일지();
            log.Add(1, 맡김종류.배차, "앞");
            var mark = log.Count;
            log.Add(2, 맡김종류.배차, "뒤");
            log.Add(3, 맡김종류.방침, "절제");
            Check("일지는 표시 뒤의 것만 셈", log.CountSince(mark, 맡김종류.배차) == 1 && log.CountSince(mark, 맡김종류.방침) == 1 &&
                log.CountSince(0, 맡김종류.배차) == 2 && log.CountSince(-1, 맡김종류.점검) == 0);

            var waits = new 기다리는일목록();
            Check("기다리는 일은 같은 열쇠를 한 번만 담고, 지울 수 있음",
                waits.Add("k", "가") && !waits.Add("k", "나") && waits.Count == 1 && waits.Remove("k") && waits.Count == 0);
            waits.Add("rider:1", "가");
            waits.Add("other", "나");
            waits.RemoveWhere(key => key.StartsWith("rider:"));
            Check("끝난 일은 열쇠로 골라 지움", waits.Count == 1 && waits.Items[0].Key == "other");
            return results;
        }
    }
}
