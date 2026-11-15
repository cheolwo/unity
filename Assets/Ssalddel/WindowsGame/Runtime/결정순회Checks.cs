using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 결정 순회 뼈대의 순수 검사. 빌드의 kernel-checks.txt와 Checks~/DecisionCycle이 같은 검사를 돌린다.
    internal static class 결정순회Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            // ── 첫 턴을 처음부터 끝까지 ──
            var cycle = 결정순회.First();
            var levels = cycle.Stops.Select(stop => (int)stop.Level).ToArray();
            Check("정거장은 아래에서 위로 (행정동 → 구 → 서울 → 전국 → 세계)",
                levels.Zip(levels.Skip(1), (a, b) => a <= b).All(ok => ok) && cycle.Stops.First().Level == 순회높이.행정동 &&
                cycle.Stops.Last().Level == 순회높이.세계, string.Join(",", levels));
            Check("처음은 1턴 계획 · 순회 시작 전", cycle.Turn == 1 && cycle.Phase == 순회단계.계획 && cycle.Current < 0);
            Check("시작 버튼은 가장 아래 정거장으로", cycle.Button.Kind == 버튼종류.시작 && cycle.Button.Main.Contains("면목본동"), cycle.Button.Main);

            cycle.Press();
            Check("누르면 면목본동 도착 · 필수가 남음", cycle.CurrentStop?.Id == 결정순회Policy.Bon && cycle.Button.Kind == 버튼종류.필수남음,
                cycle.Button.Main);
            cycle.Press();
            Check("필수가 남으면 눌러도 그 자리", cycle.CurrentStop?.Id == 결정순회Policy.Bon && cycle.Phase == 순회단계.계획);

            Check("고를 수 없는 선택지는 거절", !cycle.Choose("t1-bon-milk", 5) && !cycle.Choose("없는-결정", 0));
            Check("필수는 미룰 수 없음", !cycle.Defer("t1-bon-milk"));
            cycle.Choose("t1-bon-milk", 0);
            Check("필수를 정하면 선택만 남음", cycle.Button.Kind == 버튼종류.선택만, cycle.Button.Main);
            cycle.Press();
            var riderDeferred = cycle.All.FirstOrDefault(item => item.Id == "t1-bon-rider");
            Check("선택은 한 번 더 누르면 미루고 다음 행정동으로", riderDeferred != null && riderDeferred.Deferred &&
                cycle.CurrentStop?.Id == 결정순회Policy.Two);

            Check("올림은 떠나기 전에는 서울에 없음", !cycle.All.Any(item => item.StopId == 결정순회Policy.Seoul && item.Kind == 결정종류.알림));
            cycle.Choose("t1-two-slot", 0);
            Check("면목2동을 다 정하면 다음은 중랑구", cycle.Button.Kind == 버튼종류.다음 && cycle.Button.Main.Contains("중랑구"), cycle.Button.Main);
            cycle.Press();
            var raised = cycle.All.Where(item => item.StopId == 결정순회Policy.Seoul && item.Kind == 결정종류.알림).ToList();
            Check("떠나면 올림이 서울 알림으로 한 번 실림", raised.Count == 1 && cycle.CurrentStop?.Id == 결정순회Policy.Gu, raised.Count.ToString());

            cycle.Choose("t1-gu-riders", 0);
            cycle.Press();
            Check("서울에 도착하면 올라온 알림을 읽음", cycle.CurrentStop?.Id == 결정순회Policy.Seoul && raised.All(item => item.Read));
            Check("결정이 없는 전국은 건너뜀 (서울 다음은 세계)",
                cycle.Skipped(cycle.IndexOf(결정순회Policy.Nation)) && cycle.Button.Sub.Contains("세계"), cycle.Button.Sub);
            Check("필수가 남아 있는 동안 영업 시작이 나오지 않음", cycle.Button.Kind != 버튼종류.영업시작);
            cycle.Choose("t1-seoul-refill", 1);
            cycle.Press();
            Check("세계는 1턴에 열림", cycle.CurrentStop?.Id == 결정순회Policy.World);
            cycle.Choose("t1-world-beans", 0);
            Check("모든 지도가 끝나면 영업 시작", cycle.Button.Kind == 버튼종류.영업시작 && cycle.OpenTotal == 0, cycle.Button.Main);

            cycle.Press();
            Check("영업 단계", cycle.Phase == 순회단계.영업 && cycle.Button.Kind == 버튼종류.영업중);
            Check("영업 중에는 정하기·미루기·이동을 받지 않음", !cycle.Choose("t1-bon-milk", 1) && !cycle.Defer("t1-bon-rider") && !cycle.JumpTo(0));
            cycle.Press();
            Check("결산 단계", cycle.Phase == 순회단계.결산 && cycle.Button.Kind == 버튼종류.결산확인);
            cycle.Press();
            Check("다음 턴: 2턴 계획 · 시작 전", cycle.Turn == 2 && cycle.Phase == 순회단계.계획 && cycle.Current < 0);

            // ── 2턴: 미룬 것은 다시, 정한 것은 다시 묻지 않음 ──
            var carried = cycle.All.Where(item => item.Id == "t1-bon-rider").ToList();
            Check("미룬 선택은 다음 턴에 한 번만 다시 나옴", carried.Count == 1 && carried[0].Open && carried[0].DeferCount == 1);
            Check("정한 결정은 다음 턴에 다시 묻지 않음", !cycle.All.Any(item => item.Id == "t1-bon-milk" || item.Id == "t1-seoul-refill"));
            Check("결정 번호가 겹치지 않음", cycle.All.Select(item => item.Id).Distinct().Count() == cycle.All.Count());
            Check("세계는 2턴에 없음 (13턴마다)", cycle.Skipped(cycle.IndexOf(결정순회Policy.World)) && !결정순회Policy.WorldOpens(2) &&
                결정순회Policy.WorldOpens(14) && 결정순회Policy.WorldOpens(27));
            Check("2턴에는 전국 안건이 있어 건너뛰지 않음", !cycle.Skipped(cycle.IndexOf(결정순회Policy.Nation)));
            Check("2턴도 가장 아래(면목본동)부터", cycle.Button.Main.Contains("면목본동"), cycle.Button.Main);

            // ── 자유 이동: 길잡이지 레일이 아님 ──
            var free = 결정순회.First();
            Check("칩으로 서울에 바로 갈 수 있음", free.JumpTo(free.IndexOf(결정순회Policy.Seoul)) && free.CurrentStop?.Id == 결정순회Policy.Seoul);
            free.Choose("t1-seoul-refill", 0);
            Check("서울을 먼저 정해도 버튼은 가장 아래의 남은 곳(면목본동)으로", free.Button.Main.Contains("면목본동"), free.Button.Main);
            free.Press();
            Check("눌러서 면목본동으로", free.CurrentStop?.Id == 결정순회Policy.Bon);
            free.JumpTo(free.IndexOf(결정순회Policy.Two));
            free.JumpTo(free.IndexOf(결정순회Policy.Gu));
            Check("늦게 올라온 올림은 다 정한 서울을 다시 연다", free.OpenAt(free.IndexOf(결정순회Policy.Seoul)) == 1);
            Check("남은 선택은 이동만으로 미뤄지지 않음", free.All.Any(item => item.Id == "t1-two-slot" && item.Open && !item.Deferred));
            Check("같은 정거장으로 이동은 무시", !free.JumpTo(free.Current));

            // ── 안건이 하나도 없는 턴 ──
            var empty = new 결정순회(결정순회Policy.Stops(), _ => Enumerable.Empty<순회결정>());
            Check("안건이 없으면 버튼이 바로 영업 시작", empty.Button.Kind == 버튼종류.영업시작);
            empty.Press();
            Check("눌러서 영업으로", empty.Phase == 순회단계.영업);
            return results;
        }
    }
}
