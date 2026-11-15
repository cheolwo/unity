using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 2026-11-13 결정 순회 뼈대 (Claude 보완 제안 「결정 순회와 한 버튼」, 사용자 선택: 아래에서 위로).
    // 같은 자리의 한 버튼이 이번 턴에 결정할 지도를 아래(행정동)에서 위(세계)로 차례로 데려가고,
    // 모든 지도의 결정이 끝나야 영업 → 결산 → 다음 턴으로 넘어간다.
    //  - 필수: 정해야 다음 정거장으로 간다.
    //  - 선택: 버튼을 한 번 더 누르면 미루고, 미룬 것은 다음 턴에 다시 나온다(한 번만).
    //  - 올림: 그 정거장을 떠날 때 위 정거장의 알림으로 실린다.
    //  - 알림: 도착하면 읽은 것으로 친다.
    //  - 안건이 없는 정거장은 건너뛴다. 세계는 13턴마다(1·14·27…) 열린다.
    //  - 길잡이일 뿐이다: 칩을 눌러 다른 정거장으로 바로 가도 되고, 버튼은 늘 가장 아래의 남은 정거장으로 데려간다.
    // 순수 상태 기계다. 게임 시계·주문·업무 상태를 읽거나 바꾸지 않는다. 안건은 가상 고정 자료다.
    internal enum 순회높이 { 행정동 = 0, 구 = 1, 서울 = 2, 전국 = 3, 세계 = 4 }

    internal enum 결정종류 { 필수, 선택, 올림, 알림 }

    internal enum 순회단계 { 계획, 영업, 결산 }

    internal enum 버튼종류 { 시작, 필수남음, 선택만, 다음, 영업시작, 영업중, 결산확인 }

    internal sealed class 순회정거장
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Short;
        public readonly 순회높이 Level;
        public readonly string Note;
        public readonly string RegionId;     // 행정동 정거장이 읽을 행정동 모듈(region:kr:hjd:…). 다른 높이는 null

        public 순회정거장(string id, string name, string shortName, 순회높이 level, string note = null, string regionId = null)
        {
            Id = id;
            Name = name;
            Short = shortName;
            Level = level;
            Note = note;
            RegionId = regionId;
        }
    }

    internal sealed class 순회결정
    {
        public string Id;
        public string StopId;
        public 결정종류 Kind;
        public string Title;
        public string Detail;
        public string[] Options = Array.Empty<string>();
        public string RaiseTo;      // 올림이 실릴 정거장
        public int Chosen = -1;
        public bool Deferred;       // 선택을 이번 턴에 미룸
        public bool Read;           // 올림은 떠날 때, 알림은 도착할 때
        public int DeferCount;
        public int Turn;            // 처음 나온 턴

        public bool IsChoice => Kind == 결정종류.필수 || Kind == 결정종류.선택;
        public bool Open => IsChoice ? Chosen < 0 && !Deferred : !Read;
        public bool Blocking => Kind == 결정종류.필수 && Chosen < 0;
        public string ChosenLabel => Chosen >= 0 && Chosen < Options.Length ? Options[Chosen] : null;
    }

    internal readonly struct 순회버튼
    {
        public readonly 버튼종류 Kind;
        public readonly string Main;
        public readonly string Sub;

        public 순회버튼(버튼종류 kind, string main, string sub)
        {
            Kind = kind;
            Main = main;
            Sub = sub;
        }
    }

    internal sealed class 결정순회
    {
        private readonly List<순회정거장> stops;
        private readonly List<순회결정> decisions = new();
        private readonly HashSet<int> visited = new();
        private readonly Func<int, IEnumerable<순회결정>> fixture;

        public int Turn { get; private set; } = 1;
        public 순회단계 Phase { get; private set; } = 순회단계.계획;
        public int Current { get; private set; } = -1;     // -1 = 이번 턴 순회를 아직 시작하지 않음
        public IReadOnlyList<순회정거장> Stops => stops;
        public 순회정거장 CurrentStop => Current >= 0 ? stops[Current] : null;
        public IEnumerable<순회결정> All => decisions;

        public 결정순회(IEnumerable<순회정거장> stopList, Func<int, IEnumerable<순회결정>> turnFixture)
        {
            // 아래에서 위로: 행정동 → 구 → 서울 → 전국 → 세계. 같은 높이는 주어진 순서를 지킨다.
            stops = stopList.Select((stop, index) => (stop, index)).OrderBy(item => (int)item.stop.Level)
                .ThenBy(item => item.index).Select(item => item.stop).ToList();
            fixture = turnFixture;
            AddTurnDecisions();
        }

        public static 결정순회 First() => new 결정순회(결정순회Policy.Stops(), 결정순회Policy.Fixture);

        public int IndexOf(string stopId) => stops.FindIndex(stop => stop.Id == stopId);

        public IEnumerable<순회결정> At(int stop) =>
            stop < 0 || stop >= stops.Count ? Enumerable.Empty<순회결정>() : decisions.Where(item => item.StopId == stops[stop].Id);

        public int OpenAt(int stop) => At(stop).Count(item => item.Open);
        public int ChoicesOpenAt(int stop) => At(stop).Count(item => item.IsChoice && item.Open);
        public int BlockingAt(int stop) => At(stop).Count(item => item.Blocking);
        public bool Visited(int stop) => visited.Contains(stop);
        public bool Skipped(int stop) => !At(stop).Any();      // 이번 턴에 안건이 아예 없는 정거장
        public int OpenTotal => decisions.Count(item => item.Open);

        // 가장 아래(앞)의 남은 정거장. except는 빼고 본다. deliverFrom을 주면 그 정거장의 올림이 이미 실린 것으로 본다.
        public int NextOpen(int except = -1, int deliverFrom = -1)
        {
            var raised = deliverFrom < 0 ? new HashSet<string>() :
                new HashSet<string>(At(deliverFrom).Where(item => item.Kind == 결정종류.올림 && !item.Read && item.RaiseTo != null)
                    .Select(item => item.RaiseTo));
            for (var index = 0; index < stops.Count; index++)
            {
                if (index == except) continue;
                if (OpenAt(index) > 0 || raised.Contains(stops[index].Id)) return index;
            }
            return -1;
        }

        public 순회버튼 Button
        {
            get
            {
                if (Phase == 순회단계.영업)
                    return new 순회버튼(버튼종류.영업중, "영업 중 · 마치면 결산", "하루 영업은 위쪽 운영 시작·배속 그대로");
                if (Phase == 순회단계.결산)
                    return new 순회버튼(버튼종류.결산확인, "결산 확인 · 다음 턴", (Turn + 1) + "턴 계획으로");
                if (Current < 0)
                {
                    var first = NextOpen();
                    return first < 0
                        ? new 순회버튼(버튼종류.영업시작, "결정 없음 · 영업 시작", "영업 → 결산")
                        : new 순회버튼(버튼종류.시작, "순회 시작 · " + stops[first].Name,
                            $"정거장 {stops.Count(stop => !Skipped(IndexOf(stop.Id)))}곳 · 결정 {decisions.Count(item => item.IsChoice && item.Open)}건");
                }
                var name = stops[Current].Name;
                var next = NextOpen(Current, Current);
                var nextName = next < 0 ? "영업 시작" : stops[next].Name;
                var blocking = BlockingAt(Current);
                var choices = ChoicesOpenAt(Current);
                if (blocking > 0)
                    return new 순회버튼(버튼종류.필수남음, $"결정 {choices} · {name}", "다 정하면 → " + nextName);
                if (choices > 0)
                    return new 순회버튼(버튼종류.선택만, $"선택 {choices} · 미루고 다음", "→ " + nextName);
                return next < 0
                    ? new 순회버튼(버튼종류.영업시작, "모든 지도 끝 · 영업 시작", "영업 → 결산")
                    : new 순회버튼(버튼종류.다음, "다음: " + nextName,
                        결정순회Policy.LevelName(stops[next].Level) + " · 결정 " + Math.Max(OpenAt(next), 1));
            }
        }

        // 같은 자리의 버튼을 한 번 눌렀다. 무엇을 했는지 짧게 돌려준다.
        public string Press()
        {
            if (Phase == 순회단계.영업)
            {
                Phase = 순회단계.결산;
                return Turn + "턴 결산";
            }
            if (Phase == 순회단계.결산)
            {
                NewTurn();
                return Turn + "턴 계획 · 순회 시작 전";
            }
            if (Current < 0)
            {
                var first = NextOpen();
                if (first < 0)
                {
                    Phase = 순회단계.영업;
                    return "결정할 곳이 없어 영업을 시작합니다";
                }
                Arrive(first);
                return stops[first].Name + " 도착";
            }
            var here = Current;
            if (BlockingAt(here) > 0) return "필수 결정이 남았습니다 · " + stops[here].Name;
            var deferred = 0;
            foreach (var item in At(here).Where(entry => entry.Kind == 결정종류.선택 && entry.Open).ToList())
            {
                item.Deferred = true;
                item.DeferCount++;
                deferred++;
            }
            Leave(here);
            var next = NextOpen(here);
            if (next < 0)
            {
                Phase = 순회단계.영업;
                return (deferred > 0 ? $"선택 {deferred}건을 미루고 " : string.Empty) + "모든 지도 끝 · 영업 시작";
            }
            Arrive(next);
            return (deferred > 0 ? $"선택 {deferred}건을 미루고 " : string.Empty) + stops[next].Name + " 도착";
        }

        public bool Choose(string id, int option)
        {
            if (Phase != 순회단계.계획) return false;
            var item = decisions.FirstOrDefault(entry => entry.Id == id);
            if (item == null || !item.IsChoice || option < 0 || option >= item.Options.Length) return false;
            item.Chosen = option;
            item.Deferred = false;
            return true;
        }

        public bool Defer(string id)
        {
            if (Phase != 순회단계.계획) return false;
            var item = decisions.FirstOrDefault(entry => entry.Id == id);
            if (item == null || item.Kind != 결정종류.선택 || !item.Open) return false;
            item.Deferred = true;
            item.DeferCount++;
            return true;
        }

        // 칩을 눌러 바로 간다. 남은 선택은 미루지 않고 그대로 둔다(나중에 버튼이 다시 데려온다).
        public bool JumpTo(int stop)
        {
            if (Phase != 순회단계.계획 || stop < 0 || stop >= stops.Count || stop == Current) return false;
            if (Current >= 0) Leave(Current);
            Arrive(stop);
            return true;
        }

        private void Arrive(int stop)
        {
            Current = stop;
            visited.Add(stop);
            foreach (var item in At(stop).Where(entry => entry.Kind == 결정종류.알림)) item.Read = true;
        }

        private void Leave(int stop)
        {
            foreach (var item in At(stop).Where(entry => entry.Kind == 결정종류.올림 && !entry.Read).ToList())
            {
                item.Read = true;
                if (item.RaiseTo == null || IndexOf(item.RaiseTo) < 0) continue;
                var id = item.Id + ">up";
                if (decisions.Any(entry => entry.Id == id)) continue;
                decisions.Add(new 순회결정
                {
                    Id = id, StopId = item.RaiseTo, Kind = 결정종류.알림, Title = "올라옴 · " + item.Title,
                    Detail = stops[stop].Name + "에서 올라온 안건", Turn = Turn,
                });
            }
        }

        private void NewTurn()
        {
            // 정했거나 읽은 것은 지우고, 미룬 선택만 다음 턴으로 넘긴다. 같은 결정을 두 번 묻지 않는다.
            decisions.RemoveAll(item => !(item.Kind == 결정종류.선택 && item.Deferred && item.Chosen < 0));
            foreach (var item in decisions) item.Deferred = false;
            Turn++;
            Phase = 순회단계.계획;
            Current = -1;
            visited.Clear();
            AddTurnDecisions();
        }

        private void AddTurnDecisions()
        {
            foreach (var item in fixture(Turn) ?? Enumerable.Empty<순회결정>())
            {
                if (IndexOf(item.StopId) < 0 || decisions.Any(entry => entry.Id == item.Id)) continue;
                item.Turn = Turn;
                decisions.Add(item);
            }
        }
    }

    internal static class 결정순회Policy
    {
        public const string Bon = "myeonmok-bon";
        public const string Two = "myeonmok-2";
        public const string Gu = "jungnang";
        public const string Seoul = "seoul";
        public const string Nation = "nation";
        public const string World = "world";

        // 행정동 정거장은 r54 행정동 모듈(2023 역사 경계 후보)의 지역 ID로 그 동의 공간을 고른다.
        // 모듈을 읽지 못하면 화면이 이 메모를 보여 주고 지도를 다른 동으로 바꾸지 않는다.
        public static List<순회정거장> Stops() => new()
        {
            new 순회정거장(Bon, "면목본동", "본동", 순회높이.행정동, "행정동 모듈 없음 · 지도는 그대로", "region:kr:hjd:1126056500"),
            new 순회정거장(Two, "면목2동", "2동", 순회높이.행정동, "행정동 모듈 없음 · 지도는 그대로", "region:kr:hjd:1126052000"),
            new 순회정거장(Gu, "중랑구", "구", 순회높이.구),
            new 순회정거장(Seoul, "서울", "서울", 순회높이.서울),
            new 순회정거장(Nation, "전국", "전국", 순회높이.전국),
            new 순회정거장(World, "세계", "세계", 순회높이.세계, "세계 지도는 아직 없음 · 전국 지도 위에서 카드만"),
        };

        public static bool WorldOpens(int turn) => turn % 13 == 1;    // 분기 첫 턴: 1·14·27…

        public static string LevelName(순회높이 level) => level switch
        {
            순회높이.행정동 => "행정동",
            순회높이.구 => "구",
            순회높이.서울 => "서울",
            순회높이.전국 => "전국",
            _ => "세계",
        };

        public static string KindName(결정종류 kind) => kind switch
        {
            결정종류.필수 => "필수",
            결정종류.선택 => "선택",
            결정종류.올림 => "위로 올림",
            _ => "알림",
        };

        // 가상 안건. 고른 결과는 아직 업무에 반영하지 않는다(뼈대).
        public static IEnumerable<순회결정> Fixture(int turn)
        {
            var t = "t" + turn + "-";
            if (turn == 1)
            {
                yield return Choice(t + "bon-milk", Bon, 결정종류.필수, "카페 1호점 우유 보충", "냉장 우유 0 · 라떼 주문이 막힘", "보충 발주", "라떼 잠시 닫기");
                yield return Choice(t + "bon-rider", Bon, 결정종류.선택, "점심 피크에 기사 1명 더?", "매장 A 대기가 늘어남", "붙임");
                yield return Choice(t + "two-slot", Two, 결정종류.선택, "빈 슬롯 개설 검토 시작", "가맹 개설 절차 1단계 · 지사 여력을 씀", "검토 시작");
                yield return new 순회결정
                {
                    Id = t + "two-milk-up", StopId = Two, Kind = 결정종류.올림, Title = "매장 D 우유 부족",
                    Detail = "떠날 때 서울 보충 회차 안건으로 올라감", RaiseTo = Seoul,
                };
                yield return Choice(t + "gu-riders", Gu, 결정종류.필수, "주말 기사 배분", "면목2동 토·일 부족 예상 · 면목본동 기사 1명 지원 대기?", "지원 대기", "그대로");
                yield return Choice(t + "seoul-refill", Seoul, 결정종류.필수, "수도권 센터 오후 보충 순서", "보충 · 출고 · 거점 · 시간창", "중랑 먼저", "광진 먼저");
            }
            else
            {
                yield return Choice(t + "bon-edu", Bon, 결정종류.필수, "본부 교육 일정 확정", "카페 2호점 점주 · 개설 절차", "이번 주", "다음 주");
                yield return Choice(t + "gu-check", Gu, 결정종류.선택, "동네별 기사 여력 다시 보기", "구 대표 질문", "확인");
                if (turn % 2 == 0)
                    yield return Choice(t + "nation-run", Nation, 결정종류.필수, "수도권 → 충청 운송 1건", "유제품 4팔레트 · 가상", "차량 1", "차량 2");
            }
            if (WorldOpens(turn))
                yield return Choice(t + "world-beans", World, 결정종류.필수, "원두 다음 분기 공급안", "비교 · 검토 · 분산 · 보류", "분산", "한 곳에", "보류");
        }

        private static 순회결정 Choice(string id, string stop, 결정종류 kind, string title, string detail, params string[] options) =>
            new 순회결정 { Id = id, StopId = stop, Kind = kind, Title = title, Detail = detail, Options = options };
    }
}
