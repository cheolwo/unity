using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // r236 M키 행정동 디오라마 선택창 (2026-11-15, 사용자: 「선택창부터」. 기획 r236 확정 · Claude 시안 A).
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-Codex기획-r217-r236.md 3절 묶음 1(동1~동4).
    //  - M을 누르면 화면 가운데 직사각형 창에 이 실행판이 읽는 행정동 모듈이 카드로 뜬다. 카드: 정적 미리보기 · 동 이름 · 구 · 코드 ·
    //    「지금 보는 곳」. 목록은 실행판의 지도 명세(hongdal-dong-modules.json)에서 읽는다. 따로 적은 동 이름 목록을 쓰지 않는다.
    //  - 미리보기는 Tools~/make_dong_thumbs.py가 그 동의 실제 경계 · 건물 · 큰길로 구운 그림이다(Resources/hongdal-dong-thumb-*.bytes).
    //    표(hongdal-dong-thumbs.json)의 모듈 판본(moduleContentHash)이 읽은 모듈과 다르면 그림을 쓰지 않는다. 다른 동의 그림을 대신 쓰지 않는다.
    //  - 카드를 누르면 창을 닫고 그 동의 「이 동만」 보기로 실제로 옮긴다(FocusDongModule로 경계에 시점을 맞춤). 같은 동을 다시 누르면
    //    경계에 다시 맞춘다. 읽지 못한 동은 카드에 이유를 적고 옮기지 않는다. 옮기지 못하면 창을 닫지 않고 이유를 적는다.
    //  - M을 다시 누르거나 ×를 누르면 옮기지 않고 닫는다. Esc는 창이 열려 있으면 창 닫기가 먼저다(설정이 함께 열리지 않음).
    //  - 창이 떠 있는 동안 뒤의 지도 · 단추는 누르기 · 휠을 받지 않는다. 지도를 옮길 뿐 고용 · 입점 · 발주 · 정산 · 시간을 바꾸지 않는다.
    //    동 관리 창은 옮길 때 닫는다(r236: 단순 이동에 관리 창 열기를 섞지 않음). 매장 창과 초안은 원래 매장의 것이라 그대로다.
    public sealed partial class SagajeongDispatchView
    {
        private const string DongThumbIndexResource = "hongdal-dong-thumbs";
        private static readonly Rect DongPicker = new(232, 88, 816, 548);
        private const int DongPickerColumns = 3;
        private const int DongPickerRows = 2;
        private const float DongPickerCardHeight = 202f;
        private const float DongPickerCardGap = 14f;

        [Serializable] private sealed class DongThumbIndex { public string schema; public string renderer; public DongThumb[] thumbs; }
        [Serializable] private sealed class DongThumb { public string regionId; public string code; public string resource; public string moduleContentHash; public string pngSha256; }

        private bool dongPickerOpen;
        private bool dongThumbsTried;
        private readonly Dictionary<string, Texture2D> dongThumbs = new(StringComparer.Ordinal);
        private readonly HashSet<string> dongThumbStale = new(StringComparer.Ordinal);    // 표의 판본이 모듈과 다름
        private int dongPickerTop;                     // 맨 위에 보이는 줄 (동이 여섯보다 많을 때 휠로)
        private string dongPickerNote;                 // 옮기지 못한 이유
        private int dongPickerCardsDrawn;              // 이번 그림의 카드 수 (촬영이 읽는다)
        private int dongPickerThumbsDrawn;             // 그중 미리보기를 그린 카드 수

        private bool DongPickerAvailable => TidyScreen && DongModulesActive && dongModules.Any(module => module.Entry != null);
        private bool DongPickerVisible => dongPickerOpen && DongPickerAvailable && !SettingsVisible;

        private List<DongModuleState> DongPickerList() =>
            dongModules.Where(module => module.Entry != null)
                .OrderBy(module => module.Entry.district ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(module => module.Entry.name ?? string.Empty, StringComparer.Ordinal)
                .ToList();

        private string DongPickerCurrent => BusinessDong()?.Entry.regionId;

        private bool ToggleDongPicker()
        {
            if (!DongPickerAvailable)
            {
                notice = "M · 이 화면에서는 행정동 이동 창을 열 수 없습니다.";
                return false;
            }
            dongPickerOpen = !dongPickerOpen;
            if (dongPickerOpen)
            {
                helpOpen = false;
                dongPickerNote = null;
                EnsureDongThumbs();
                var list = DongPickerList();
                var at = list.FindIndex(module => module.Entry.regionId == DongPickerCurrent);
                dongPickerTop = Mathf.Clamp(at < 0 ? 0 : at / DongPickerColumns - (DongPickerRows - 1), 0, MaxDongPickerTop(list.Count));
            }
            return dongPickerOpen;
        }

        private static int MaxDongPickerTop(int count) => Math.Max(0, (count + DongPickerColumns - 1) / DongPickerColumns - DongPickerRows);

        // 미리보기 표와 그림을 한 번 읽는다. 판본이 다르거나 그림이 깨졌으면 그 동은 그림 없이 둔다.
        private void EnsureDongThumbs()
        {
            if (dongThumbsTried) return;
            dongThumbsTried = true;
            var index = Resources.Load<TextAsset>(DongThumbIndexResource);
            if (index == null) return;
            DongThumbIndex table;
            try { table = JsonUtility.FromJson<DongThumbIndex>(index.text); }
            catch (ArgumentException) { return; }
            if (table?.thumbs == null) return;
            foreach (var thumb in table.thumbs)
            {
                var module = DongModuleById(thumb.regionId);
                if (module?.Entry == null) continue;
                if (!string.Equals(module.Entry.moduleContentHash, thumb.moduleContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    dongThumbStale.Add(thumb.regionId);
                    continue;
                }
                var asset = Resources.Load<TextAsset>(thumb.resource);
                if (asset == null) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                if (!texture.LoadImage(asset.bytes, true))
                {
                    Destroy(texture);
                    continue;
                }
                dongThumbs[thumb.regionId] = texture;
            }
        }

        // 카드를 누름: 그 동의 「이 동만」 보기로 옮긴다. 옮겼으면 true.
        private bool PickDong(string regionId)
        {
            var module = DongModuleById(regionId);
            if (module?.Entry == null)
            {
                dongPickerNote = "이 동의 모듈이 목록에 없어 옮기지 않았어요.";
                return false;
            }
            if (!module.Loaded)
            {
                dongPickerNote = $"{module.Entry.name} · 모듈 {module.Status} · 옮기지 않았어요.";
                return false;
            }
            dongDeskOpen = false;
            focusDongRegion = regionId;                      // SetDongView(이 동만)가 이 동을 고른다
            if (dongView != DongView.Solo) SetDongView(DongView.Solo);
            else FocusDongModule(regionId);
            var moved = DongSoloView && soloRegion == regionId && focusDongRegion == regionId && mapLevel == MapLevel.Dong;
            if (!moved)
            {
                dongPickerNote = $"{module.Entry.name}(으)로 옮기지 못했어요. 지도는 그대로예요.";
                return false;
            }
            관찰지도수동이동();
            dongPickerOpen = false;
            dongPickerNote = null;
            notice = $"{module.Entry.name}만 보기로 옮겼어요.";
            return true;
        }

        // Esc: 창이 열려 있으면 닫고 true(설정은 열지 않는다).
        private bool CloseDongPickerByEscape()
        {
            if (!dongPickerOpen) return false;
            dongPickerOpen = false;
            return true;
        }

        private void DrawDongPicker()
        {
            if (Event.current.type == EventType.Repaint)
            {
                dongPickerCardsDrawn = 0;
                dongPickerThumbsDrawn = 0;
            }
            if (!DongPickerVisible) return;
            var list = DongPickerList();
            var maxTop = MaxDongPickerTop(list.Count);
            var wheel = Event.current;
            if (wheel.type == EventType.ScrollWheel && DongPicker.Contains(wheel.mousePosition))
            {
                if (maxTop > 0) dongPickerTop = Mathf.Clamp(dongPickerTop + (wheel.delta.y > 0 ? 1 : -1), 0, maxTop);
                wheel.Use();
            }
            dongPickerTop = Mathf.Clamp(dongPickerTop, 0, maxTop);

            Paint(new Rect(0, 0, Width, Height), Fade(Canvas, 0.6f));
            var box = DongPicker;
            Paint(box, Mint, 14);
            Paint(new Rect(box.x + 2, box.y + 2, box.width - 4, box.height - 4), Canvas, 13);
            Text(new Rect(box.x + 24, box.y + 16, 300, 30), "행정동 이동", 20, Paper, true);
            Text(new Rect(box.x + 24, box.y + 46, box.width - 48, 20), "카드를 누르면 창이 닫히고 그 동만 보기로 옮겨 가요. 지금 보는 곳은 민트 표시.", 11, Soft);
            var keys = new Rect(box.xMax - 170, box.y + 18, 104, 22);
            Paint(keys, Rim, 11);
            Text(keys, "M · Esc  닫기", 10, Soft, align: TextAnchor.MiddleCenter);
            var close = new Rect(box.xMax - 50, box.y + 14, 30, 30);
            Text(close, "×", 20, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                dongPickerOpen = false;
                return;
            }

            var current = DongPickerCurrent;
            var cardWidth = (box.width - 48 - DongPickerCardGap * (DongPickerColumns - 1)) / DongPickerColumns;
            var mouse = Event.current.mousePosition;
            for (var index = dongPickerTop * DongPickerColumns; index < list.Count && index < (dongPickerTop + DongPickerRows) * DongPickerColumns; index++)
            {
                var module = list[index];
                var slot = index - dongPickerTop * DongPickerColumns;
                var card = new Rect(box.x + 24 + slot % DongPickerColumns * (cardWidth + DongPickerCardGap),
                    box.y + 78 + slot / DongPickerColumns * (DongPickerCardHeight + DongPickerCardGap), cardWidth, DongPickerCardHeight);
                var region = module.Entry.regionId;
                var isCurrent = region == current;
                var hover = card.Contains(mouse) && module.Loaded;
                Paint(card, hover ? Gold : isCurrent ? Mint : Rim, 10);
                Paint(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), Tile, 9);
                var picture = new Rect(card.x + 7, card.y + 7, card.width - 14, (card.width - 14) * 272f / 456f);
                if (dongThumbs.TryGetValue(region, out var texture))
                {
                    GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit, false);
                    if (Event.current.type == EventType.Repaint) dongPickerThumbsDrawn++;
                }
                else
                {
                    Paint(picture, Canvas, 6);
                    Text(picture, dongThumbStale.Contains(region) ? "미리보기 판본이 모듈과 달라요" : "미리보기 없음", 10, Soft, align: TextAnchor.MiddleCenter);
                }
                if (!module.Loaded)
                {
                    Paint(picture, Fade(Canvas, 0.7f), 6);
                    Text(picture, $"모듈 {module.Status} · 옮길 수 없음", 11, Gold, true, TextAnchor.MiddleCenter);
                }
                else if (hover)
                {
                    var bar = new Rect(picture.x, picture.yMax - 24, picture.width, 24);
                    Paint(bar, Gold, 6);
                    Text(bar, "누르면 이 동만 보기로  →", 11, Canvas, true, TextAnchor.MiddleCenter);
                }
                Text(new Rect(card.x + 12, picture.yMax + 8, card.width - 24, 22), module.Entry.name, 15, Paper, true);
                Text(new Rect(card.x + 12, picture.yMax + 32, card.width - 24, 16), $"{module.Entry.district} · {module.Entry.code}", 10, Soft);
                if (isCurrent)
                {
                    var tag = new Rect(card.xMax - 92, picture.yMax + 10, 80, 20);
                    Paint(tag, Mint, 10);
                    Text(tag, "지금 보는 곳", 10, Canvas, true, TextAnchor.MiddleCenter);
                }
                if (Event.current.type == EventType.Repaint) dongPickerCardsDrawn++;
                if (module.Loaded && Hit(card) && PickDong(region)) return;
            }
            var foot = new Rect(box.x + 24, box.yMax - 34, box.width - 48, 20);
            if (!string.IsNullOrEmpty(dongPickerNote)) Text(foot, dongPickerNote, 11, Gold, true);
            else Text(foot, maxTop > 0 ? $"휠로 더 보기 · {dongPickerTop + 1}/{maxTop + 1}줄 · 미리보기는 그 동의 실제 경계 · 건물 · 큰길로 구운 그림(모듈 판본에 묶음)" :
                "미리보기는 그 동의 실제 경계 · 건물 · 큰길로 구운 그림이에요(모듈 판본에 묶음). 2023 경계 후보 · 내부 검토용.", 10, Soft);
        }

        // 방향 촬영: M으로 열고(카드 · 미리보기 수), 다른 동을 눌러 그 동만 보기로 옮기고(경계 안에 시점), 같은 동을 다시 누르고,
        // Esc로 닫을 때 설정이 열리지 않는지 본다. 업무 상태는 그대로여야 한다.
        private IEnumerator ViewProofDongPicker(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            dongPickerOpen = false;
            yield return null;
            var before = DongPickerCurrent;
            receipt.pickerBefore = DongModuleById(before)?.Entry.name ?? string.Empty;
            receipt.pickerOpened = PressShortcut(KeyCode.M) && DongPickerVisible;
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.pickerCards = dongPickerCardsDrawn;
            receipt.pickerThumbs = dongPickerThumbsDrawn;
            receipt.pickerModules = dongModules.Count(module => module.Entry != null);
            yield return SaveFrame(directory, "91-dong-picker.png");

            var target = DongPickerList().FirstOrDefault(module => module.Loaded && module.Entry.regionId != before);
            receipt.pickerTarget = target?.Entry.name ?? string.Empty;
            if (target != null)
            {
                receipt.pickerMoved = PickDong(target.Entry.regionId) && !dongPickerOpen && DongSoloView && soloRegion == target.Entry.regionId;
                for (var frame = 0; frame < 4; frame++) yield return null;
                var centre = PlotScene(target.Entry.centerLatitude, target.Entry.centerLongitude);
                receipt.pickerTargetOnScreen = centre.x > 0 && centre.y > 0 && centre.x < Field.width && centre.y < Field.height;
                receipt.pickerAfter = BusinessDong()?.Entry.name ?? string.Empty;
                yield return SaveFrame(directory, "92-dong-picked.png");
                // 같은 동을 다시: 경계에 다시 맞추고 그대로
                PressShortcut(KeyCode.M);
                yield return null;
                receipt.pickerSameAgain = PickDong(target.Entry.regionId) && soloRegion == target.Entry.regionId && !dongPickerOpen;
            }
            // Esc: 창만 닫고 설정은 열지 않는다
            PressShortcut(KeyCode.M);
            yield return null;
            var wasOpen = dongPickerOpen;
            receipt.pickerEscClosed = wasOpen && CloseDongPickerByEscape() && !dongPickerOpen && !settingsOpen;
            yield return null;
            receipt.pickerUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
