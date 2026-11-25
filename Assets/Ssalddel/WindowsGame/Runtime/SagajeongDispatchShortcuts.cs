using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 화면 단축키 (r219 확정 · 2026-11-15, 키1 · 키2).
    //  - F1 조작 도움말 · F2 지금 동의 관리 창 · F3 고른 내 매장의 운영 창 · F4 · F5 · F6 그 매장의 재료 · 설비 · 인력 탭.
    //    M 행정동 이동 창(r236 확정, 2026-11-15, SagajeongDispatchDongPicker.cs). 작은 지도 ↔ 업무 요약(키3)은 「업무」 · 「지도」 단추로 한다.
    //  - 키는 창 · 탭을 **열기만** 한다. 발주 · 고용 · 구입 · 배정 · 작업 시작을 확정하지 않는다(r219).
    //  - Esc(설정)와 Enter(결정 순회)는 그대로다. 이 파일은 그 둘을 받지 않는다.
    //  - 받지 않는 때: Ctrl · Alt · Shift · Cmd와 함께 누름, 글을 쓰는 칸에 커서가 있음, 설정 화면 · 정책 창이 떠 있음, 검증 촬영 화면.
    //    누르고 있는 동안 되풀이되는 입력은 한 번으로 본다.
    //  - F3은 같은 매장을 다시 열 때 보던 탭을 그대로 연다(첫 진입은 요약). 발주 수량 같은 초안은 원래 창의 값이라 그대로다.
    //  - 대상이 없거나 이 화면에서 열 수 없으면 왼쪽 아래 한 줄로 알린다. 다른 매장 · 동의 창을 대신 열지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private readonly HashSet<KeyCode> shortcutHeld = new();
        private bool helpOpen;                       // F1 도움말
        private string shortcutStorePlace;           // F3이 다시 열 매장 (마지막으로 본 매장 창)
        private int shortcutStoreTab;                // 그 매장 창에서 보던 탭

        private static readonly (KeyCode Key, string Cap, string Icon, string Text, string Note)[] ShortcutRows =
        {
            (KeyCode.F1, "F1", "search", "이 도움말 열기 · 닫기", null),
            (KeyCode.F2, "F2", "home", "지금 동의 관리 창", null),
            (KeyCode.F3, "F3", "store", "고른 내 매장의 운영 창", "처음엔 요약 · 다시 열면 보던 탭 그대로"),
            (KeyCode.F4, "F4", "milk", "그 매장의 재료 · 발주 탭", null),
            (KeyCode.F5, "F5", "machine", "그 매장의 설비 탭", null),
            (KeyCode.F6, "F6", "people", "그 매장의 인력 탭", null),
            (KeyCode.M, "M", "pin", "행정동 이동 창 열기 · 닫기", "카드를 누르면 그 동만 보기로 · 업무 요약은 「업무」 단추"),
            (KeyCode.None, "Esc", "tool", "설정 열기 · 닫기", "이동 창이 열려 있으면 창 닫기가 먼저"),
            (KeyCode.None, "Enter", "history", "결정 순회", "지금과 같다 · 운영 회차 마감이 아님"),
        };

        // OnGUI의 처음(Esc 다음)에서 부른다.
        private void HandleShortcutKeys()
        {
            if (StoreDeskVisible)
            {
                shortcutStorePlace = storeDeskPlace;
                shortcutStoreTab = storeDeskTab;
            }
            var current = Event.current;
            if (current == null || !TidyScreen) return;
            var key = current.keyCode;
            if (key != KeyCode.F1 && key != KeyCode.F2 && key != KeyCode.F3 && key != KeyCode.F4 && key != KeyCode.F5 && key != KeyCode.F6 && key != KeyCode.M) return;
            if (current.type == EventType.KeyUp)
            {
                shortcutHeld.Remove(key);
                return;
            }
            if (current.type != EventType.KeyDown) return;
            if (경영판보임 && key != KeyCode.M) return;    // 00 경영 판이 떠 있으면 F키로 뒤의 창을 바꾸지 않는다 (M은 행정동 이동 창으로)
            if (current.control || current.alt || current.shift || current.command) return;
            if (GUIUtility.keyboardControl != 0) return;
            current.Use();
            if (!shortcutHeld.Add(key)) return;           // 누르고 있는 동안의 되풀이
            PressShortcut(key);
        }

        // 키 하나를 누른 것과 같은 일. 연 창이 있으면 true. (방향 촬영도 이 함수를 부른다)
        private bool PressShortcut(KeyCode key)
        {
            if (SettingsVisible || CampaignPolicyOpen) return false;
            if (dongPickerOpen && key != KeyCode.M) return false;      // 행정동 이동 창이 떠 있는 동안 다른 키는 받지 않는다(r236)
            if (placeOpen) return false;                               // 홍달 플레이스가 떠 있는 동안 단축키는 받지 않는다(새 키 없음, r238)
            switch (key)
            {
                case KeyCode.F1:
                    helpOpen = !helpOpen;
                    return helpOpen;
                case KeyCode.M:
                    return ToggleDongPicker();          // r236: 행정동 이동 창 (SagajeongDispatchDongPicker.cs). 업무 요약은 「업무」 단추로
                case KeyCode.F2:
                {
                    var dong = BusinessDong();
                    if (!DongDeskReady || dong == null)
                    {
                        notice = "F2 · 이 화면에서는 동 관리 창을 열 수 없습니다. 동네 지도에서 누르세요.";
                        return false;
                    }
                    OpenDongDesk(dong.Entry.regionId);
                    return DongDeskVisible;
                }
                case KeyCode.F3:
                case KeyCode.F4:
                case KeyCode.F5:
                case KeyCode.F6:
                {
                    var place = ShortcutStore();
                    if (place == null)
                    {
                        notice = key == KeyCode.F3 ? "F3 · 먼저 지도에서 내 매장을 고르세요." : key + " · 먼저 지도에서 내 매장을 고르세요.";
                        return false;
                    }
                    if (!StoreDeskReady)
                    {
                        notice = key + " · 이 화면에서는 매장 창을 열 수 없습니다. 동네 지도에서 누르세요.";
                        return false;
                    }
                    var again = place == shortcutStorePlace;
                    OpenStoreDesk(place);
                    if (key == KeyCode.F3)
                    {
                        if (again) storeDeskTab = shortcutStoreTab;      // 다시 열면 보던 탭
                    }
                    else StoreTab(key == KeyCode.F4 ? StoreTabMaterials : key == KeyCode.F5 ? StoreTabEquipment : StoreTabStaff);
                    shortcutStorePlace = place;
                    shortcutStoreTab = storeDeskTab;
                    return StoreDeskVisible;
                }
            }
            return false;
        }

        // F3~F6이 열 매장: 지금 열린 매장 → 마지막으로 본 매장 → 매장 창이 있는 내 매장이 하나뿐이면 그 매장.
        private string ShortcutStore()
        {
            if (storeDeskPlace != null && StoreRole(storeDeskPlace) != null) return storeDeskPlace;
            if (shortcutStorePlace != null && StoreRole(shortcutStorePlace) != null) return shortcutStorePlace;
            var only = engine?.Pack?.network?.store?.placeId;
            return only != null && StoreRole(only) != null ? only : null;
        }

        // ── F1 도움말 (설정 화면보다 아래, 다른 것보다 위) ──
        private static readonly Rect HelpPanel = new(820, 96, 420, 490);

        private bool HelpCovers(Vector2 point) => helpOpen && TidyScreen && HelpPanel.Contains(point);

        private void DrawShortcutHelp()
        {
            if (!helpOpen || !TidyScreen) return;
            var box = HelpPanel;
            Paint(box, Rim, 12);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 11);
            Icon(new Rect(box.x + 18, box.y + 16, 20, 20), "search", Mint);
            Text(new Rect(box.x + 46, box.y + 12, 240, 28), "조작 도움말", 17, Paper, true);
            var close = new Rect(box.xMax - 40, box.y + 12, 26, 26);
            Text(close, "×", 18, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                helpOpen = false;
                return;
            }
            var y = box.y + 50;
            foreach (var row in ShortcutRows)
            {
                var cap = new Rect(box.x + 18, y + 6, row.Cap.Length > 2 ? 48 : 34, 22);
                Paint(cap, Paper, 5);
                Text(cap, row.Cap, 11, Canvas, true, TextAnchor.MiddleCenter);
                Icon(new Rect(box.x + 76, y + 8, 18, 18), row.Icon, row.Key == KeyCode.None ? Soft : Mint);
                Text(new Rect(box.x + 100, y + (row.Note == null ? 6 : 1), box.width - 116, 22), row.Text, 12, Paper);
                if (row.Note != null) Text(new Rect(box.x + 100, y + 19, box.width - 116, 16), row.Note, 9, Soft);
                Paint(new Rect(box.x + 16, y + 39, box.width - 32, 1), Fade(Rim, 0.7f));
                y += 40;
            }
            var foot = new Rect(box.x + 16, y + 8, box.width - 32, 52);
            Paint(foot, Tile, 8);
            Icon(new Rect(foot.x + 10, foot.y + 8, 16, 16), "check", Mint);
            Text(new Rect(foot.x + 32, foot.y + 4, foot.width - 40, 22), "키는 창 · 탭을 열기만 합니다. 확정은 창 안의 단추로.", 10, Paper);
            Icon(new Rect(foot.x + 10, foot.y + 28, 16, 16), "alert", Gold);
            Text(new Rect(foot.x + 32, foot.y + 24, foot.width - 40, 22), "고른 매장이 없으면 F3~F6은 왼쪽 아래 한 줄로 알립니다.", 10, Soft);
        }

        // 방향 촬영: 키 함수를 차례로 불러 창 · 탭이 열리는지, 다시 열면 보던 탭인지, 설정 화면에서는 받지 않는지, 업무가 그대로인지 본다.
        private IEnumerator ViewProofShortcuts(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            storeDeskPlace = null;
            dongDeskOpen = false;
            selectedBuilding = null;
            shortcutStorePlace = null;
            yield return null;
            receipt.keyF2 = PressShortcut(KeyCode.F2) && DongDeskVisible && !StoreDeskVisible;
            yield return null;
            receipt.keyF3 = PressShortcut(KeyCode.F3) && StoreDeskVisible && !DongDeskVisible && storeDeskTab == StoreTabSummary;
            yield return null;
            receipt.keyF4 = PressShortcut(KeyCode.F4) && storeDeskTab == StoreTabMaterials;
            receipt.keyF5 = PressShortcut(KeyCode.F5) && storeDeskTab == StoreTabEquipment;
            receipt.keyF6 = PressShortcut(KeyCode.F6) && storeDeskTab == StoreTabStaff;
            yield return null;
            // 닫았다가 F3으로 다시 열면 보던 탭(인력)
            storeDeskPlace = null;
            yield return null;
            receipt.keyF3Again = PressShortcut(KeyCode.F3) && storeDeskTab == StoreTabStaff;
            // 도움말
            receipt.keyF1 = PressShortcut(KeyCode.F1) && helpOpen;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "76-shortcut-help.png");
            receipt.keyF1Closed = !PressShortcut(KeyCode.F1) && !helpOpen;
            // M: 행정동 이동 창 열기 → 닫기(r236). 업무 요약은 「업무」 단추와 같은 값으로 연다.
            receipt.keyM = PressShortcut(KeyCode.M) && dongPickerOpen;
            yield return null;
            receipt.keyMBack = !PressShortcut(KeyCode.M) && !dongPickerOpen;      // 닫으면 연 창이 없으니 false가 돌아온다
            miniWork = true;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "77-work-summary.png");
            receipt.workSummaryDrawn = workSummaryDrawn;
            miniWork = false;
            // 설정 화면이 떠 있으면 받지 않는다
            settingsOpen = true;
            yield return null;
            var dongBefore = DongDeskVisible;
            receipt.keyIgnoredInSettings = !PressShortcut(KeyCode.F2) && DongDeskVisible == dongBefore;
            settingsOpen = false;
            yield return null;
            receipt.keyUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            storeDeskPlace = null;
            dongDeskOpen = false;
            helpOpen = false;
            yield return null;
            holdProofClock = false;
        }
    }
}
