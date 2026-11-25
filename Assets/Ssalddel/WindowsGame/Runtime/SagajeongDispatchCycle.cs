using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 결정 순회 뼈대의 화면 (2026-11-13, Claude 보완 제안 「결정 순회와 한 버튼」).
    // 오른쪽 아래 같은 자리의 버튼 하나(Enter도 같다)가 이번 턴에 결정할 지도를 아래(행정동)에서 위(세계)로 차례로 데려간다.
    // 계획 단계에서는 오른쪽 칸이 지금 정거장의 결정 카드가 된다. 접으면 원래 패널(사업·영역·전국)이 돌아온다.
    // 업무 상태와 게임 시계는 읽지도 바꾸지도 않는다(r162). 안건은 가상 고정 자료다(결정순회Policy).
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect CycleCard = new(986, 106, 272, 430);
        private static readonly Rect CycleDockFull = new(986, 542, 272, 84);
        private static readonly Rect CycleDockCompact = new(986, 608, 272, 20);
        private static readonly Color CycleDone = Ink(0x17332E);

        private 결정순회 cycle;
        private bool cycleCardFolded = TidyScreen;       // 정리한 첫 화면에서는 접힌 채로 시작한다(한 줄 버튼)
        private string cycleNotice;
        private float cycleNoticeUntil;

        private 결정순회 Cycle => cycle ??= 결정순회.First();

        private bool CycleVisible => expansionView && !mapOnly && !showDesk;

        private bool CycleCardVisible => !다음할일사용 && CycleVisible && !cycleCardFolded && Cycle.Phase != 순회단계.영업 &&
            actorFocus == null && selectedBuilding == null && !dongPanelOpen && !zonePanelOpen && !SettingsVisible && !DailyReportVisible;

        // 오른쪽에 아래로 긴 패널(전국 패널·건물 카드·기사·동·구성 패널)이 보일 때는 버튼을 한 줄로 줄여 그 아래에 둔다.
        private bool CycleDockCrowded => NationPanelVisible || actorFocus != null || selectedBuilding != null || dongPanelOpen || zonePanelOpen;

        // 정리한 첫 화면에서는 접어 둔 동안에도 한 줄이다. 버튼을 누르거나 「펼치기」를 누르면 칩과 카드가 나온다.
        private Rect CycleDockRect => CycleDockCrowded || TidyScreen && cycleCardFolded ? CycleDockCompact : CycleDockFull;

        private bool CycleCovers(Vector2 point) => 다음할일사용 ? 다음할일Covers(point) :
            CycleVisible && (CycleDockRect.Contains(point) || CycleCardVisible && CycleCard.Contains(point));

        private void DrawCycle()
        {
            턴시계맞추기();                        // 다음 할 일: 화에서 계획하는 동안의 시계 (SagajeongDispatchNextTodo.cs)
            if (다음할일사용)
            {
                DrawNextTodo();                    // 일반 실행에서는 다음 할 일이 이 자리와 Enter를 이어받는다(가상 안건은 끈다)
                return;
            }
            if (!CycleVisible) return;
            HandleCycleKey();
            if (CycleCardVisible) DrawCycleCard();
            DrawCycleDock();
        }

        private void HandleCycleKey()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown) return;
            if (current.keyCode != KeyCode.Return && current.keyCode != KeyCode.KeypadEnter) return;
            if (CampaignPolicyOpen || SettingsVisible || GUIUtility.keyboardControl != 0) return;
            current.Use();
            PressCycle();
        }

        private void PressCycle()
        {
            var before = Cycle.Current;
            var message = Cycle.Press();
            if (Cycle.Phase == 순회단계.계획 && Cycle.Current >= 0 && Cycle.Current != before)
            {
                cycleCardFolded = false;
                NavigateCycle(Cycle.CurrentStop);
            }
            else if (Cycle.Phase == 순회단계.결산)
                cycleCardFolded = false;
            ShowCycleNotice(message);
        }

        private void JumpCycle(int stop)
        {
            if (!Cycle.JumpTo(stop)) return;
            cycleCardFolded = false;
            NavigateCycle(Cycle.CurrentStop);
            ShowCycleNotice(Cycle.CurrentStop.Name + "로 이동");
        }

        private void ShowCycleNotice(string message)
        {
            cycleNotice = message;
            cycleNoticeUntil = Time.realtimeSinceStartup + 3f;
            Debug.Log("Hongdal decision cycle: turn " + Cycle.Turn + " · " + message);
        }

        // 정거장의 높이로 지도를 옮긴다. 행정동 두 곳은 같은 동네 지도를 쓴다(가상 구분). 세계 지도는 아직 없어 전국 지도 위에 카드만 둔다.
        private void NavigateCycle(순회정거장 stop)
        {
            if (stop == null) return;
            actorFocus = null;
            selectedBuilding = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            CloseLevelPrompt();
            switch (stop.Level)
            {
                case 순회높이.행정동:
                    // r54: 그 행정동의 모듈이 있으면 그 동의 공간으로. 없으면 지도는 동네 전체 그대로(카드 메모가 이유를 말한다).
                    if (FocusDongModule(stop.RegionId)) break;
                    if (mapLevel != MapLevel.Dong) ReturnToDong();
                    else ShowWholeDong();
                    break;
                case 순회높이.구:
                    if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
                    break;
                case 순회높이.서울:
                    // r184 · 이어지는 지도: 서울 단계는 없다. 서울 정거장의 안건은 구 지도에서 본다.
                    if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
                    break;
                default:
                    if (mapLevel != MapLevel.Nation) LoadMapLevel(MapLevel.Nation);
                    break;
            }
        }

        private static string CyclePhaseName(순회단계 phase) => phase switch
        {
            순회단계.영업 => "영업",
            순회단계.결산 => "결산",
            _ => "계획",
        };

        private Color CycleButtonColor(버튼종류 kind) => kind switch
        {
            버튼종류.영업시작 => Mint,
            버튼종류.영업중 => Tile,
            버튼종류.결산확인 => Lilac,
            버튼종류.선택만 => Soft,
            _ => Gold,
        };

        private void DrawCycleDock()
        {
            var dock = CycleDockRect;
            var button = Cycle.Button;
            var fill = CycleButtonColor(button.Kind);
            var ink = button.Kind == 버튼종류.영업중 ? Paper : Canvas;
            if (dock.height < 40)
            {
                // 접어 둔 한 줄: 오른쪽에 「펼치기」를 둔다. 긴 패널 때문에 줄어든 한 줄에는 두지 않는다.
                var unfold = TidyScreen && cycleCardFolded && !CycleDockCrowded;
                var line = unfold ? new Rect(dock.x, dock.y, dock.width - 58, dock.height) : dock;
                Paint(line, fill, 7);
                Text(line, button.Main, 11, ink, true, TextAnchor.MiddleCenter);
                if (unfold && ExpansionButton(new Rect(dock.xMax - 54, dock.y, 54, dock.height), "펼치기", true, Tile))
                {
                    cycleCardFolded = false;
                    return;
                }
                if (Hit(line)) PressCycle();
                return;
            }

            Paint(dock, Rim, 11);
            Paint(new Rect(dock.x + 1, dock.y + 1, dock.width - 2, dock.height - 2), Canvas, 10);
            var x = dock.x + 8;
            var notice = cycleNotice != null && Time.realtimeSinceStartup < cycleNoticeUntil;
            Text(new Rect(x, dock.y + 4, 198, 18), notice ? cycleNotice : $"경영 {Cycle.Turn}턴 · {CyclePhaseName(Cycle.Phase)} · 결정 순회",
                10, notice ? Gold : Soft, true);
            if (Cycle.Phase != 순회단계.영업 &&
                ExpansionButton(new Rect(dock.xMax - 58, dock.y + 4, 52, 18), cycleCardFolded ? "펼치기" : "접기", true, Tile))
                cycleCardFolded = !cycleCardFolded;

            // 정거장 칩: 아래에서 위로. 누르면 그 정거장으로 바로 간다(계획 단계에서만).
            var count = Cycle.Stops.Count;
            var gap = 3f;
            var chipWidth = (dock.width - 16 - gap * (count - 1)) / count;
            for (var index = 0; index < count; index++)
            {
                var chip = new Rect(x + index * (chipWidth + gap), dock.y + 25, chipWidth, 17);
                DrawCycleChip(chip, index);
            }

            var press = new Rect(dock.x + 6, dock.y + 46, dock.width - 12, 33);
            Paint(press, fill, 8);
            Text(new Rect(press.x + 10, press.y + 2, press.width - 56, 17), button.Main, 13, ink, true);
            Text(new Rect(press.x + 10, press.y + 18, press.width - 56, 13), button.Sub, 10, ink);
            Text(new Rect(press.xMax - 48, press.y, 40, press.height), "Enter", 10, ink, false, TextAnchor.MiddleRight);
            if (Hit(press)) PressCycle();
        }

        private void DrawCycleChip(Rect chip, int index)
        {
            var stop = Cycle.Stops[index];
            var skipped = Cycle.Skipped(index);
            var current = Cycle.Phase == 순회단계.계획 && Cycle.Current == index;
            var open = Cycle.OpenAt(index);
            var done = !skipped && open == 0 && (Cycle.Visited(index) || Cycle.Phase != 순회단계.계획);
            var fill = current ? Gold : done ? CycleDone : skipped ? Panel : Tile;
            var ink = current ? Canvas : done ? Mint : skipped ? Fade(Soft, 0.5f) : Paper;
            Paint(chip, fill, 5);
            var label = stop.Short + (done ? " ✓" : !skipped && open > 0 ? " " + open : string.Empty);
            Text(chip, label, 10, ink, true, TextAnchor.MiddleCenter);
            if (skipped) Paint(new Rect(chip.x + 5, chip.center.y, chip.width - 10, 1), Fade(Soft, 0.5f));
            if (Cycle.Phase == 순회단계.계획 && !skipped && Hit(chip)) JumpCycle(index);
        }

        private void DrawCycleCard()
        {
            var card = CycleCard;
            Paint(card, Rim, 11);
            Paint(new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2), Canvas, 10);
            var x = card.x + 14;
            var width = card.width - 28;
            Text(new Rect(x, card.yMax - 24, width, 16), "가상 안건 · 고른 결과는 아직 업무에 반영되지 않음 (뼈대)", 9, Fade(Soft, 0.85f));
            if (Cycle.Phase == 순회단계.결산)
            {
                DrawCycleSummary(card, x, width);
                return;
            }
            if (Cycle.Current < 0)
            {
                DrawCycleIntro(card, x, width);
                return;
            }

            var stop = Cycle.CurrentStop;
            var items = Cycle.At(Cycle.Current).ToList();
            var choices = items.Count(item => item.IsChoice && item.Open);
            var raised = items.Count(item => item.Kind == 결정종류.올림);
            Text(new Rect(x, card.y + 10, width, 18), $"정거장 {Cycle.Current + 1}/{Cycle.Stops.Count} · " +
                결정순회Policy.LevelName(stop.Level) + " · " + stop.Name, 11, Gold, true);
            Text(new Rect(x, card.y + 30, width, 26), choices > 0 ? $"결정 {choices}" + (raised > 0 ? $" · 올림 {raised}" : string.Empty) :
                "여기서 정할 것은 끝", 19, Paper, true);
            var y = card.y + 58f;
            var note = CycleStopNote(stop);
            if (!string.IsNullOrEmpty(note))
            {
                Text(new Rect(x, y, width, 16), note, 10, Soft);
                y += 18;
            }
            foreach (var item in items)
            {
                if (y > card.yMax - 40) break;
                y = DrawCycleDecision(item, x, y, width) + 6;
            }
        }

        private float DrawCycleDecision(순회결정 item, float x, float y, float width)
        {
            var accent = item.Kind switch
            {
                결정종류.필수 => SignalRed,
                결정종류.선택 => Soft,
                결정종류.올림 => Lilac,
                _ => Ink(0x7CC6F0),
            };
            var hasButtons = item.IsChoice && item.Open;
            var height = hasButtons ? 92f : 74f;     // 닫힌 줄도 설명 아래에 상태 글자가 들어가게
            var row = new Rect(x - 4, y, width + 8, height);
            Paint(row, Tile, 8);
            Paint(new Rect(row.x, row.y, 3, row.height), item.Open ? accent : Fade(accent, 0.4f), 2);
            var tag = 결정순회Policy.KindName(item.Kind);
            Paint(new Rect(row.xMax - 62, y + 6, 56, 16), Fade(accent, item.Open ? 1f : 0.35f), 4);
            Text(new Rect(row.xMax - 62, y + 6, 56, 16), tag, 9, Canvas, true, TextAnchor.MiddleCenter);
            Text(new Rect(x + 6, y + 5, width - 72, 18), item.Title, 12, item.Open ? Paper : Soft, true);
            Text(new Rect(x + 6, y + 24, width - 10, 28), item.Detail ?? string.Empty, 10, Soft, wrap: true);
            var bottom = new Rect(x + 6, y + height - 36, width - 10, 28);
            if (hasButtons)
            {
                var deferWidth = item.Kind == 결정종류.선택 ? 52f : 0f;
                var count = item.Options.Length;
                var optionWidth = (bottom.width - deferWidth - 4 * count) / count;
                for (var index = 0; index < count; index++)
                {
                    var rect = new Rect(bottom.x + index * (optionWidth + 4), bottom.y, optionWidth, bottom.height);
                    if (ExpansionButton(rect, item.Options[index], true, index == 0 ? Mint : Rim))
                    {
                        Cycle.Choose(item.Id, index);
                        ShowCycleNotice(item.Title + " → " + item.Options[index]);
                    }
                }
                if (deferWidth > 0 && ExpansionButton(new Rect(bottom.xMax - deferWidth, bottom.y, deferWidth, bottom.height), "미룸", true, Panel))
                {
                    Cycle.Defer(item.Id);
                    ShowCycleNotice(item.Title + " → 다음 턴으로");
                }
            }
            else
            {
                var (status, color) = item.Kind switch
                {
                    결정종류.올림 => (item.Read ? "올림 완료" : "떠날 때 위 정거장으로 올라감", Lilac),
                    결정종류.알림 => ("확인함", Soft),
                    _ => item.Deferred ? ("다음 턴으로 미룸", Gold) : ("✓ " + item.ChosenLabel, Mint),
                };
                Text(new Rect(bottom.x, y + height - 24, bottom.width, 18), status, 11, color, true);
            }
            return y + height;
        }

        private void DrawCycleIntro(Rect card, float x, float width)
        {
            Text(new Rect(x, card.y + 10, width, 18), $"경영 {Cycle.Turn}턴 · 계획 · 결정 순회", 11, Gold, true);
            var total = Cycle.All.Count(item => item.IsChoice && item.Open);
            Text(new Rect(x, card.y + 30, width, 26), $"결정 {total}건 · 아래에서 위로", 19, Paper, true);
            Text(new Rect(x, card.y + 58, width, 32), "오른쪽 아래 버튼(Enter)을 누르면 결정할 곳으로 차례로 데려갑니다. 칩을 눌러 바로 가도 됩니다.",
                10, Soft, wrap: true);
            var y = card.y + 98f;
            for (var index = 0; index < Cycle.Stops.Count; index++)
            {
                var stop = Cycle.Stops[index];
                var skipped = Cycle.Skipped(index);
                var row = new Rect(x - 4, y, width + 8, 30);
                Paint(row, skipped ? Panel : Tile, 7);
                Text(new Rect(x + 4, y, 70, 30), 결정순회Policy.LevelName(stop.Level), 10, Soft);
                Text(new Rect(x + 64, y, width - 120, 30), stop.Name, 13, skipped ? Fade(Soft, 0.6f) : Paper, true);
                Text(new Rect(row.xMax - 96, y, 90, 30), skipped ? "없음 · 건너뜀" : $"결정 {Cycle.ChoicesOpenAt(index)}" +
                    (Cycle.OpenAt(index) > Cycle.ChoicesOpenAt(index) ? " · 올림" : string.Empty), 10, skipped ? Fade(Soft, 0.6f) : Gold,
                    true, TextAnchor.MiddleRight);
                y += 34;
            }
        }

        private void DrawCycleSummary(Rect card, float x, float width)
        {
            Text(new Rect(x, card.y + 10, width, 18), $"{Cycle.Turn}턴 결산 · 결정 순회", 11, Lilac, true);
            var chosen = Cycle.All.Where(item => item.IsChoice && item.Chosen >= 0).ToList();
            var deferred = Cycle.All.Where(item => item.Kind == 결정종류.선택 && item.Deferred && item.Chosen < 0).ToList();
            var skipped = Enumerable.Range(0, Cycle.Stops.Count).Where(index => Cycle.Skipped(index)).Select(index => Cycle.Stops[index].Name).ToList();
            Text(new Rect(x, card.y + 30, width, 26), $"정한 것 {chosen.Count}", 19, Paper, true);
            var y = card.y + 62f;
            foreach (var item in chosen.Take(7))
            {
                var stop = Cycle.Stops[Cycle.IndexOf(item.StopId)];
                Text(new Rect(x, y, 70, 20), stop.Name, 10, Soft);
                Text(new Rect(x + 66, y, width - 66, 20), item.Title + " → " + item.ChosenLabel, 10, Paper, true);
                y += 22;
            }
            y += 6;
            Text(new Rect(x, y, width, 20), $"미룬 것 {deferred.Count}" + (deferred.Count > 0 ? " · " + string.Join(", ", deferred.Select(item => item.Title)) : string.Empty),
                11, deferred.Count > 0 ? Gold : Soft, true);
            y += 22;
            Text(new Rect(x, y, width, 20), "건너뛴 곳 " + (skipped.Count == 0 ? "없음" : string.Join(", ", skipped)), 11, Soft, true);
            y += 26;
            Text(new Rect(x, y, width, 32), "미룬 결정은 다음 턴 순회에 다시 나옵니다. 버튼을 누르면 다음 턴 계획으로 넘어갑니다.", 10, Soft, wrap: true);
        }
    }
}
