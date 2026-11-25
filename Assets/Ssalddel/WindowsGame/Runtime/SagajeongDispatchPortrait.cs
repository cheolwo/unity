using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 세로 화면(폰) · 입장으로 자료 보기 (2026-11-25 08:45 사용자: 「모바일 Unity로 해가지고 각각의 입장에서 데이터들을 바라볼 수 있도록 하고 싶네」).
    //  - 창이 세로로 길면(폰 · 세로로 줄인 창) 넓은 화면(1280×720 손배치) 대신 이 화면을 그린다. 지도 · 하루 · 규칙은 같다.
    //  - 뼈대(모바일 제안 2026-11-24 「폰 화면 뼈대」): 위 띠(입장 이름 · 날 · 시각) → 입장 칩(옆으로 넘김) → 구 지도(그 입장의 자료만 켠 모습) → 아래 판.
    //  - 아래 판 3단(접힘 · 반 · 전체): 그 입장의 「지도에 켠 것」(필수 금빛 · 부가 푸른빛, 부가는 눌러서 빼고 넣음)과 「전국 장터 숫자」.
    //    묶음의 원본은 넓은 화면과 같다: Resources/hongdal-data-stance-info.json(SagajeongDispatchStanceInfo.cs) · hongdal-data-produce-flow.json.
    //  - 구 지도 그리기는 그대로 쓴다(SagajeongDispatchGu.cs). 역 창 · 타일 그릇 창 · 유동인구 카드는 자리만 아래 판 위로 옮긴다.
    //  - 손가락: 한 손가락 끌기 = 지도 옮기기(터치가 마우스로 들어옴) · 두 손가락 = 확대(PortraitPinch) · 누르기 = 고르기. 올림(hover)은 없다.
    //  - 논리 너비 412(폰의 dp와 비슷) — 글자 10이 폰에서 약 10dp가 되게 한다.
    //  - 아직 없는 것: 전국 지도(절기 장터)의 세로 화면 · 역 속 창 · 사업 창 · 하루 마감 카드. 동 디오라마는 넓은 화면(데스크탑)에서만(2026-11-24 18:08 결정).
    public sealed partial class SagajeongDispatchView
    {
        private const float PortraitWidth = 412f;
        private const float PortraitTopBar = 48f;
        private const float PortraitChipRow = 42f;
        private const float PortraitHandle = 54f;

        private bool PortraitScreen => Screen.height > Screen.width * 1.2f;
        private bool PortraitPickerOpen => stancePickerOpen || CurrentStance == null;
        private float PortraitMapTop => portraitTopInset + PortraitTopBar + PortraitChipRow;

        private float portraitScale = 1f;
        private float portraitTopInset, portraitBottomInset;
        private int portraitSheet = 1;                 // 아래 판: 0 접힘 · 1 반 · 2 전체
        private float portraitSheetScroll, portraitSheetMax, portraitChipScroll, portraitChipMax;
        private Rect portraitTop, portraitChips, portraitSheetRect, portraitZoomIn, portraitZoomOut, portraitFit;
        private int portraitZone;                      // 누르기 시작한 곳: 0 지도 · 1 판 손잡이 · 2 판 속 · 3 칩 · 4 확대 단추 · 5 위 띠 · 9 입장 고르기
        private Vector2 portraitPress;
        private float portraitPressScroll;
        private bool portraitDragged, portraitHandleMoved;
        private Vector2? portraitTap;                  // 이번 이벤트의 「누름」(끌지 않고 뗀 자리)
        private bool portraitCardWas;
        private float portraitPinch = -1f;

        private float PortraitSheetHeight(int level) =>
            level <= 0 ? PortraitHandle + 8f + portraitBottomInset :
            level == 1 ? Mathf.Round(Field.height * 0.44f) :
            Field.height - PortraitMapTop - 6f;

        // 내 영역을 맞춰 보일 자리: 위 띠 아래 ~ 반쯤 올린 판 위
        private Rect PortraitGuSafeArea => new Rect(14f, PortraitMapTop + 10f, PortraitWidth - 28f,
            Mathf.Max(120f, Field.height - PortraitSheetHeight(1) - PortraitMapTop - 20f));

        // 역 창 · 타일 그릇 창: 접힌 판 바로 위 가운데
        private Rect PortraitCardRect(float width, float height) => new Rect((PortraitWidth - width) * 0.5f,
            Mathf.Max(PortraitMapTop + 4f, Field.height - PortraitSheetHeight(0) - height - 8f), width, height);

        // 유동인구 카드: 지금 판 바로 위
        private Rect PortraitFloatRect(float width, float height) => new Rect((PortraitWidth - width) * 0.5f,
            Mathf.Max(PortraitMapTop + 4f, portraitSheetRect.y - height - 20f), width, height);

        private bool PortraitCardOpen => GuMapVisible && (GuStationNow() != null || HexCardVisible || CapitalHexCardVisible);      // 수도권 칸 창도(SagajeongDispatchCapitalHex.cs)
        private bool PortraitZoomShown => portraitSheet < 2 && !PortraitCardOpen;
        private string portraitChipKey;              // 칩 줄을 맞춘 입장(바뀌면 그 칩이 보이게 넘긴다)

        private bool PortraitFloatCardHidden => floatHover < 0 || GuStationNow() != null || HexCardVisible || portraitSheet == 2;

        private bool PortraitCovers(Vector2 point)
        {
            if (PortraitPickerOpen) return true;
            if (portraitTop.Contains(point) || portraitChips.Contains(point) || portraitSheetRect.Contains(point)) return true;
            return PortraitZoomShown && (portraitZoomIn.Contains(point) || portraitZoomOut.Contains(point) || portraitFit.Contains(point));
        }

        private void DrawPortraitScreen()
        {
            var scale = Screen.width / PortraitWidth;
            if (scale <= 0f) return;
            portraitScale = scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            SetMapField(new Rect(0f, 0f, PortraitWidth, Mathf.Ceil(Screen.height / scale)));
            RefreshBusiness();
            RefreshSignals();
            ReadDataControl();
            SyncDataUnit();

            // 역 창 · 타일 창이 새로 뜨면 판을 접어 자리를 내준다
            var card = GuMapVisible && (GuStationNow() != null || HexCardVisible);
            if (card && !portraitCardWas) portraitSheet = 0;
            portraitCardWas = card;

            PortraitLayout();
            PortraitKeys();
            PortraitPinch();
            PortraitPointer();

            var enabled = GUI.enabled;
            GUI.enabled = enabled && !PortraitPickerOpen;
            DrawMap();
            GUI.enabled = enabled;

            var stance = CurrentStance;
            if (stance != null) DrawPortraitChrome(stance);
            if (PortraitPickerOpen) DrawPortraitPicker();
            GUI.matrix = previous;
        }

        private void PortraitLayout()
        {
            var safe = Screen.safeArea;
            portraitTopInset = Mathf.Max(0f, (Screen.height - safe.yMax) / portraitScale);
            portraitBottomInset = Mathf.Max(0f, safe.yMin / portraitScale);
            portraitTop = new Rect(0f, 0f, PortraitWidth, portraitTopInset + PortraitTopBar);
            portraitChips = new Rect(0f, portraitTop.yMax, PortraitWidth, PortraitChipRow);
            var sheet = PortraitSheetHeight(portraitSheet);
            portraitSheetRect = new Rect(0f, Field.height - sheet, PortraitWidth, sheet);
            var x = PortraitWidth - 52f;
            var y = Mathf.Max(PortraitMapTop + 8f, portraitSheetRect.y - 172f);
            portraitZoomIn = new Rect(x, y, 42f, 42f);
            portraitZoomOut = new Rect(x, y + 48f, 42f, 42f);
            portraitFit = new Rect(x, y + 96f, 42f, 42f);
        }

        // Android 뒤로 가기(ESC): 고르기 창 → 역 창 · 타일 창 → 판 접기
        private void PortraitKeys()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape) return;
            if (stancePickerOpen && CurrentStance != null) stancePickerOpen = false;
            else if (GuStationNow() != null || HexCardVisible) PortraitCloseCards();
            else if (portraitSheet > 0) portraitSheet--;
            current.Use();
        }

        private void PortraitCloseCards()
        {
            guStation = null;
            stationInsideOpen = false;
            hexSelected = -1;
        }

        private void SetPortraitSheet(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            if (level > portraitSheet) PortraitCloseCards();      // 판을 올리면 지도 위 창은 걷는다
            portraitSheet = level;
        }

        // 두 손가락 벌리기 · 오므리기 = 구 지도 확대 · 축소(가운데를 붙잡고)
        private void PortraitPinch()
        {
            if (Input.touchCount < 2 || !GuMapVisible || !guCameraSet)
            {
                portraitPinch = -1f;
                return;
            }
            guPressed = false;
            guDragging = false;
            if (Event.current.type != EventType.Repaint) return;
            var first = Input.GetTouch(0).position;
            var second = Input.GetTouch(1).position;
            var distance = Vector2.Distance(first, second) / portraitScale;
            var middle = (first + second) * 0.5f;
            var point = new Vector2(middle.x / portraitScale, (Screen.height - middle.y) / portraitScale);
            if (portraitPinch > 0f && distance > 8f && !PortraitCovers(point))
            {
                var full = GuFullScale;
                ZoomGu(Mathf.Clamp(guScale * distance / portraitPinch, full, Mathf.Max(full, GuMaxScale)), point);
            }
            portraitPinch = distance;
        }

        // 판 · 칩 위의 누르기와 끌기. 지도보다 먼저 받는다(지도는 가린 자리의 누르기를 받지 않는다).
        private void PortraitPointer()
        {
            portraitTap = null;
            var current = Event.current;
            var point = current.mousePosition;
            if (current.type == EventType.ScrollWheel && portraitSheet > 0 && portraitSheetRect.Contains(point))
            {
                portraitSheetScroll = Mathf.Clamp(portraitSheetScroll + current.delta.y * 18f, 0f, portraitSheetMax);
                current.Use();
                return;
            }
            if (current.button != 0) return;
            if (current.type == EventType.MouseDown)
            {
                portraitZone = PortraitPickerOpen ? 9 :
                    portraitChips.Contains(point) ? 3 :
                    portraitTop.Contains(point) ? 5 :
                    portraitSheetRect.Contains(point) ? (point.y < portraitSheetRect.y + PortraitHandle ? 1 : 2) :
                    PortraitZoomShown && (portraitZoomIn.Contains(point) || portraitZoomOut.Contains(point) || portraitFit.Contains(point)) ? 4 : 0;
                portraitPress = point;
                portraitDragged = false;
                portraitHandleMoved = false;
                portraitPressScroll = portraitZone == 3 ? portraitChipScroll : portraitSheetScroll;
            }
            else if (current.type == EventType.MouseDrag && portraitZone != 0)
            {
                var moved = point - portraitPress;
                if (moved.sqrMagnitude > 64f) portraitDragged = true;
                if (portraitZone == 3) portraitChipScroll = Mathf.Clamp(portraitPressScroll - moved.x, 0f, portraitChipMax);
                else if (portraitZone == 2) portraitSheetScroll = Mathf.Clamp(portraitPressScroll - moved.y, 0f, portraitSheetMax);
                else if (portraitZone == 1 && !portraitHandleMoved && Mathf.Abs(moved.y) > 28f)
                {
                    portraitHandleMoved = true;
                    SetPortraitSheet(portraitSheet + (moved.y < 0f ? 1 : -1));
                }
                current.Use();
            }
            else if (current.type == EventType.MouseUp && portraitZone != 0)
            {
                if (!portraitDragged && !portraitHandleMoved) portraitTap = point;
                portraitZone = 0;
                current.Use();
            }
        }

        private bool PortraitTapped(Rect rect)
        {
            if (!portraitTap.HasValue || !rect.Contains(portraitTap.Value)) return false;
            portraitTap = null;
            return true;
        }

        private static float PortraitTextWidth(string text, int size)
        {
            var units = 0f;
            foreach (var letter in text ?? string.Empty) units += letter < 0x80 || letter == '·' ? 0.5f : 1f;
            return units * size;
        }

        private void PortraitChoose(Stance stance)
        {
            ApplyStance(stance, true);
            dataPanelOpen = false;
            stancePickerOpen = false;
            portraitSheet = 1;
            portraitSheetScroll = 0f;
            PortraitCloseCards();
        }

        private void DrawPortraitChrome(Stance stance)
        {
            // 출처 줄: 지도 아래쪽(판 바로 위)
            if (CapitalReady)      // 수도권 바탕이 들어오면 출처가 길어 두 줄(오늘 칩은 그만큼 위로, SagajeongDispatchDataPacks.cs)
            {
                Text(new Rect(8f, portraitSheetRect.y - 27f, PortraitWidth - 16f, 12f), "© ESA WorldCover CC BY 4.0 · Copernicus DEM · 국토교통부 표준노드링크", 8, Fade(Soft, 0.85f));
                Text(new Rect(8f, portraitSheetRect.y - 15f, PortraitWidth - 16f, 12f), "서울시 · 브이월드 · 통계청 SGIS · vuski/admdongkor (CC BY 4.0)", 8, Fade(Soft, 0.85f));
            }
            else
                Text(new Rect(8f, portraitSheetRect.y - 15f, PortraitWidth - 16f, 13f), "© ESA WorldCover CC BY 4.0 · 서울시 · 브이월드 · 통계청 SGIS · vuski/admdongkor", 8, Fade(Soft, 0.85f));

            // 위 띠
            Paint(new Rect(0f, 0f, PortraitWidth, PortraitMapTop), Fade(Canvas, 0.95f));
            var y = portraitTopInset;
            Text(new Rect(14f, y + 4f, 220f, 14f), "홍달 · 입장으로 자료 보기", 10, Mint, true);
            Text(new Rect(14f, y + 19f, PortraitWidth - 130f, 26f), stance.Name + "의 눈", 19, Paper, true);
            Text(new Rect(PortraitWidth - 150f, y + 5f, 136f, 15f), $"{GameDate():M월 d일} · {FloatHour()}시 · 구 지도", 10, Soft, false, TextAnchor.MiddleRight);
            var all = new Rect(PortraitWidth - 92f, y + 23f, 78f, 22f);
            Paint(all, Tile, 11f);
            Text(all, "입장 카드", 10, Paper, true, TextAnchor.MiddleCenter);
            if (PortraitTapped(new Rect(PortraitWidth - 140f, 0f, 140f, portraitTop.yMax))) stancePickerOpen = true;      // 손가락: 위 띠 오른쪽 전체가 단추

            // 입장 칩(옆으로 넘김) — 입장이 바뀌면 그 칩을 가운데로
            if (portraitChipKey != stance.Key)
            {
                portraitChipKey = stance.Key;
                var at = 10f;
                foreach (var item in Stances)
                {
                    var w = PortraitTextWidth(item.Name, 13) + 28f;
                    if (item == stance) { portraitChipScroll = at + w * 0.5f - PortraitWidth * 0.5f; break; }
                    at += w + 7f;
                }
                portraitChipScroll = Mathf.Max(0f, portraitChipScroll);      // 끝 쪽 한계는 아래에서 맞춘다
            }
            var x = 10f - portraitChipScroll;
            Stance picked = null;
            foreach (var item in Stances)
            {
                var width = PortraitTextWidth(item.Name, 13) + 28f;
                var chip = new Rect(x, portraitChips.y + 4f, width, 32f);
                var on = item == stance;
                Paint(chip, on ? item.Tint : Tile, 16f);
                Text(chip, item.Name, 13, on ? Canvas : Paper, true, TextAnchor.MiddleCenter);
                if (!on && PortraitTapped(chip)) picked = item;
                x += width + 7f;
            }
            portraitChipMax = Mathf.Max(0f, x + portraitChipScroll + 3f - PortraitWidth);
            portraitChipScroll = Mathf.Clamp(portraitChipScroll, 0f, portraitChipMax);

            // 확대 · 축소 · 전체
            if (PortraitZoomShown && GuMapVisible && guCameraSet)
            {
                PortraitRound(portraitZoomIn, "+", 20);
                PortraitRound(portraitZoomOut, "-", 22);
                PortraitRound(portraitFit, "전체", 10);
                var centre = new Vector2(PortraitWidth * 0.5f, (PortraitMapTop + portraitSheetRect.y) * 0.5f);
                var full = GuFullScale;
                if (PortraitTapped(portraitZoomIn)) ZoomGu(Mathf.Min(Mathf.Max(full, GuMaxScale), guScale * 1.4f), centre);
                else if (PortraitTapped(portraitZoomOut)) ZoomGu(Mathf.Max(full, guScale / 1.4f), centre);
                else if (PortraitTapped(portraitFit)) guManual = false;
            }

            DrawPortraitSheet(stance);
            if (picked != null) PortraitChoose(picked);
        }

        private void PortraitRound(Rect rect, string label, int size)
        {
            Paint(rect, Fade(Panel, 0.94f), 21f);
            Text(rect, label, size, Paper, true, TextAnchor.MiddleCenter);
        }

        private void DrawPortraitSheet(Stance stance)
        {
            var sheet = portraitSheetRect;
            Paint(new Rect(sheet.x, sheet.y, sheet.width, sheet.height + 20f), Fade(Panel, 0.985f), 18f);
            Paint(new Rect(sheet.center.x - 20f, sheet.y + 7f, 40f, 4f), Rim, 2f);
            var entries = stance.Info?.entries ?? Array.Empty<StanceInfoEntry>();
            var must = entries.Count(entry => entry.tier == 1);
            var extra = entries.Count(entry => entry.tier == 2);
            Text(new Rect(16f, sheet.y + 15f, sheet.width - 110f, 18f), stance.Name + "에게 필요한 자료", 14, Paper, true);
            Text(new Rect(16f, sheet.y + 34f, sheet.width - 32f, 14f),
                portraitSheet == 0 ? PortraitMustLine(entries) : $"필수 {must} · 부가 {extra} — 부가는 눌러서 빼고 넣습니다", 10, Soft);
            Text(new Rect(sheet.xMax - 92f, sheet.y + 15f, 78f, 18f), portraitSheet == 0 ? "펼치기 ▲" : portraitSheet == 1 ? "전체 ▲" : "접기 ▼", 11, Mint, true,
                TextAnchor.MiddleRight);
            if (PortraitTapped(new Rect(sheet.x, sheet.y, sheet.width, PortraitHandle))) SetPortraitSheet(portraitSheet == 2 ? 0 : portraitSheet + 1);
            if (portraitSheet == 0) return;

            var body = new Rect(0f, sheet.y + PortraitHandle, sheet.width, Mathf.Max(0f, sheet.height - PortraitHandle - portraitBottomInset));
            Vector2? tap = portraitTap.HasValue && body.Contains(portraitTap.Value) ? portraitTap.Value - body.position : (Vector2?)null;
            GUI.BeginGroup(body);
            var wide = body.width;
            var y = 2f - portraitSheetScroll;
            Text(new Rect(16f, y, wide - 32f, 18f), stance.Who, 13, Paper, true);
            y += 20f;
            Text(new Rect(16f, y, wide - 32f, 30f), "먼저 보는 것 · " + stance.Look, 11, Soft, false, TextAnchor.UpperLeft, true);
            y += 34f;

            // 지도에 켠 것(구 · 타일): 필수 먼저
            PortraitSection(ref y, wide, "지도에 켠 것", "금빛 = 필수(날마다) · 푸른빛 = 부가");
            var changed = false;
            foreach (var entry in entries.Where(entry => entry.id != null && !entry.id.StartsWith("fact:", StringComparison.Ordinal)).OrderBy(entry => entry.tier))
            {
                var item = StanceInfoItemOf(entry.id);
                if (item == null) continue;
                var nation = item.kind == "nation";
                var here = !nation && (entry.units == null || entry.units.Length == 0 || Array.IndexOf(entry.units, 0) >= 0 || Array.IndexOf(entry.units, 1) >= 0);
                var optional = entry.tier == 2;
                var chosen = !optional || StanceOptionalChosen(stance.Key, entry.id);
                var row = new Rect(12f, y, wide - 24f, 44f);
                if (row.yMax > 0f && row.y < body.height)
                {
                    Paint(row, Fade(Tile, here ? 0.55f : 0.26f), 8f);
                    Paint(new Rect(row.x + 1f, row.y + 7f, 3f, row.height - 14f), Fade(optional ? OptionalTint : Gold, here && chosen ? 1f : 0.45f), 1.5f);
                    var toggle = optional && here;
                    Text(new Rect(row.x + 12f, row.y + 5f, row.width - 150f, 18f), item.name, 13, here && chosen ? Paper : Soft, true);
                    Text(new Rect(row.x + 12f, row.y + 23f, row.width - (toggle ? 52f : 20f), 16f), entry.why, 10, Soft);
                    Text(new Rect(row.xMax - 148f - (toggle ? 34f : 0f), row.y + 6f, 140f, 16f),
                        nation ? "전국 지도 · 다음 판" : here ? entry.freq : "동 디오라마 · 넓은 화면", 9, here ? Fade(Soft, 0.9f) : Fade(Gold, 0.75f), false,
                        TextAnchor.MiddleRight);
                    if (toggle)
                    {
                        var box = new Rect(row.xMax - 32f, row.y + 11f, 22f, 22f);
                        Paint(box, Rim, 6f);
                        if (chosen) Paint(new Rect(box.x + 5f, box.y + 5f, 12f, 12f), Mint, 3f);
                        if (tap.HasValue && row.Contains(tap.Value))
                        {
                            tap = null;
                            portraitTap = null;
                            SetStanceSkip(stance, entry.id, chosen);      // 켜져 있으면 뺀다 · 빠져 있으면 넣는다
                            changed = true;
                        }
                    }
                }
                y += 48f;
            }

            // 전국 장터 숫자: 이 입장이 보는 자료 줄(값은 장터 묶음에서)
            var facts = entries.Where(entry => entry.id != null && entry.id.StartsWith("fact:", StringComparison.Ordinal)).OrderBy(entry => entry.tier).ToArray();
            if (facts.Length > 0 && LoadFlow())
            {
                PortraitSection(ref y, wide, "전국 장터 숫자", flow.item + " · " + flow.season);
                foreach (var entry in facts)
                {
                    var fact = FlowFactOf(entry.id.Substring(5));
                    if (fact == null) continue;
                    var row = new Rect(12f, y, wide - 24f, 48f);
                    if (row.yMax > 0f && row.y < body.height)
                    {
                        Paint(row, Fade(Tile, 0.55f), 8f);
                        Paint(new Rect(row.x + 1f, row.y + 7f, 3f, row.height - 14f), entry.tier == 2 ? OptionalTint : Gold, 1.5f);
                        Text(new Rect(row.x + 12f, row.y + 5f, row.width - 150f, 16f), fact.label, 11, Soft, true);
                        Text(new Rect(row.xMax - 150f, row.y + 3f, 140f, 22f), fact.value, 15, entry.tier == 1 ? Gold : Paper, true, TextAnchor.MiddleRight);
                        Text(new Rect(row.x + 12f, row.y + 26f, row.width - 110f, 16f), fact.sub, 10, Paper);
                        Text(new Rect(row.xMax - 100f, row.y + 27f, 90f, 14f), fact.tag + " · " + entry.freq, 8, Fade(Soft, 0.9f), false, TextAnchor.MiddleRight);
                    }
                    y += 52f;
                }
            }
            y += 6f;
            Text(new Rect(16f, y, wide - 32f, 28f), "가상 운영 · 실제 주문 및 결제와 연결되지 않습니다. 자료의 출처 · 날짜는 자료목록에 적혀 있습니다.", 9, Fade(Soft, 0.8f), false,
                TextAnchor.UpperLeft, true);
            y += 34f;
            GUI.EndGroup();
            portraitSheetMax = Mathf.Max(0f, y + portraitSheetScroll - body.height);
            portraitSheetScroll = Mathf.Clamp(portraitSheetScroll, 0f, portraitSheetMax);
            if (changed)
            {
                ApplyStance(stance, true);
                dataPanelOpen = false;
            }
        }

        private void PortraitSection(ref float y, float wide, string title, string note)
        {
            Text(new Rect(16f, y + 4f, 160f, 16f), title, 11, Gold, true);
            Text(new Rect(wide - 216f, y + 5f, 200f, 14f), note, 9, Fade(Soft, 0.9f), false, TextAnchor.MiddleRight);
            y += 26f;
        }

        private string PortraitMustLine(StanceInfoEntry[] entries)
        {
            var names = entries.Where(entry => entry.tier == 1 && entry.id != null && !entry.id.StartsWith("fact:", StringComparison.Ordinal))
                .Select(entry => StanceInfoItemOf(entry.id)?.brief).Where(name => !string.IsNullOrEmpty(name)).Take(5).ToArray();
            return names.Length == 0 ? "판을 올려 자료를 봅니다" : "필수 · " + string.Join(" · ", names);
        }

        // 입장 고르기(세로): 두 줄씩 카드. 켤 때마다 · 위 띠 「입장 카드」.
        private void DrawPortraitPicker()
        {
            Paint(new Rect(0f, 0f, PortraitWidth, Field.height), Fade(Ink(0x080D13), 0.97f));
            var y = portraitTopInset + 16f;
            Text(new Rect(0f, y, PortraitWidth, 16f), "홍달 경영 시뮬레이션", 11, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(0f, y + 18f, PortraitWidth, 30f), "누구의 눈으로 볼까요", 21, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(28f, y + 50f, PortraitWidth - 56f, 32f), "입장을 고르면 그 사람이 날마다 마주치는 자료가 지도에 켜집니다. 위쪽 칩으로 언제든 바꿉니다.", 11, Soft, false,
                TextAnchor.UpperCenter, true);
            var top = y + 90f;
            const float gap = 10f;
            var cardWidth = (PortraitWidth - 32f - gap) * 0.5f;
            var rows = (Stances.Length + 1) / 2;
            var cardHeight = Mathf.Clamp((Field.height - top - portraitBottomInset - 14f - gap * (rows - 1)) / rows, 112f, 172f);
            Stance picked = null;
            for (var index = 0; index < Stances.Length; index++)
            {
                var item = Stances[index];
                var card = new Rect(16f + index % 2 * (cardWidth + gap), top + index / 2 * (cardHeight + gap), cardWidth, cardHeight);
                Paint(card, Panel, 12f);
                Paint(new Rect(card.x + 12f, card.y + 15f, 10f, 10f), item.Tint, 5f);
                Text(new Rect(card.x + 28f, card.y + 8f, card.width - 36f, 24f), item.Name, 16, Paper, true);
                if (item.Key == stanceKey) Text(new Rect(card.xMax - 50f, card.y + 12f, 40f, 14f), "지난번", 9, Gold, true, TextAnchor.MiddleRight);
                Text(new Rect(card.x + 12f, card.y + 36f, card.width - 24f, 30f), item.Who, 10, Soft, false, TextAnchor.UpperLeft, true);
                var first = item.First?.FirstOrDefault(line => line.Unit == "구" || line.Unit == "모두").Text;
                if (!string.IsNullOrEmpty(first))
                    Text(new Rect(card.x + 12f, card.y + 70f, card.width - 24f, Mathf.Max(14f, card.height - 96f)), "구 지도 · " + first, 10, Paper, false,
                        TextAnchor.UpperLeft, true);
                var entries = item.Info?.entries;
                if (entries != null)
                    Text(new Rect(card.x + 12f, card.yMax - 22f, card.width - 24f, 14f),
                        $"필수 {entries.Count(entry => entry.tier == 1)} · 부가 {entries.Count(entry => entry.tier == 2)}", 9, Gold, true);
                if (PortraitTapped(card)) picked = item;
            }
            if (picked != null) PortraitChoose(picked);
        }

        // 기본 판 시험 촬영의 끝에 붙는다(SagajeongDispatchBaseProof.cs): 창을 세로로 바꿔 입장마다 찍는다. 저장한 입장은 건드리지 않는다.
        private IEnumerator CapturePortraitProof(string directory, BaseProofReceipt receipt)
        {
            int width = Screen.width, height = Screen.height;
            var mode = Screen.fullScreenMode;
            Screen.SetResolution(468, 1014, FullScreenMode.Windowed);
            yield return Until(() => PortraitScreen, Time.realtimeSinceStartupAsDouble + 8);
            for (var frame = 0; frame < 12; frame++) yield return null;
            receipt.portraitScreenWidth = Screen.width;
            receipt.portraitScreenHeight = Screen.height;
            if (!PortraitScreen)
            {
                Screen.SetResolution(width, height, mode);
                yield break;
            }
            var before = stanceKey;
            expansionPanelOpen = false;
            PortraitCloseCards();
            guManual = false;
            stanceKey = string.Empty;
            stancePickerOpen = true;
            for (var frame = 0; frame < 8; frame++) yield return null;
            yield return SaveFrame(directory, "portrait-01-picker.png");
            receipt.portraitShots++;

            var number = 10;
            foreach (var stance in Stances)
            {
                if (stance.Key == "custom") continue;
                ApplyStance(stance, false);
                dataPanelOpen = false;
                stancePickerOpen = false;
                portraitSheet = 1;
                portraitSheetScroll = 0f;
                guManual = false;
                yield return new WaitForSecondsRealtime(1.3f);
                yield return SaveFrame(directory, $"portrait-{number++}-{stance.Key}.png");
                receipt.portraitShots++;
            }

            // 음식점 사장: 판을 접고 사가정역을 고른 모습(역 창)
            var store = Array.Find(Stances, item => item.Key == "store");
            if (store != null && EnsureSubwayGu() != null && GuReady)
            {
                ApplyStance(store, false);
                dataPanelOpen = false;
                portraitSheet = 0;
                var home = 이어지는지도Policy.구미터(127.088502, 37.580912, guSpace.west, guSpace.south);
                ZoomGu(0.16f, GuPoint(home.x, home.y));
                guOrigin += new Vector2(Field.width * 0.5f, PortraitMapTop + 150f) - GuPoint(home.x, home.y);
                ClampGu();
                guManual = true;
                yield return new WaitForSecondsRealtime(0.8f);
                yield return SaveFrame(directory, "portrait-20-store-near.png");
                receipt.portraitShots++;
                guStation = "사가정역";
                yield return new WaitForSecondsRealtime(0.6f);
                yield return SaveFrame(directory, "portrait-21-store-station.png");
                receipt.portraitShots++;
                PortraitCloseCards();
            }

            // 도매상: 판 전체(전국 장터 숫자까지)
            var wholesale = Array.Find(Stances, item => item.Key == "wholesale");
            if (wholesale != null)
            {
                ApplyStance(wholesale, false);
                dataPanelOpen = false;
                portraitSheet = 2;
                portraitSheetScroll = 0f;
                yield return new WaitForSecondsRealtime(0.5f);
                yield return SaveFrame(directory, "portrait-22-wholesale-full.png");
                receipt.portraitShots++;
                portraitSheetScroll = 9999f;
                yield return new WaitForSecondsRealtime(0.3f);
                yield return SaveFrame(directory, "portrait-23-wholesale-full-end.png");
                receipt.portraitShots++;
            }

            // 수도권: 가장 넓게 본 모습(판 접음)
            if (CapitalReady && GuReady)
            {
                portraitSheet = 0;
                PortraitCloseCards();
                guScale = GuFullScale;
                var seoul = 이어지는지도Policy.구미터(126.99, 37.55, guSpace.west, guSpace.south);      // 서울을 가운데로
                guOrigin = new Vector2(Field.width * 0.5f - seoul.x * guScale, Field.height * 0.5f - (guSpace.height - seoul.y) * guScale);
                guManual = true;
                ClampGu();
                for (var frame = 0; frame < 40; frame++) yield return null;
                yield return SaveFrame(directory, "portrait-25-capital.png");
                receipt.portraitShots++;
                guManual = false;
                if (OpenPublicDong("1126056500", "면목본동", "중랑구"))      // 폰에서 동 디오라마(공개판)
                {
                    for (var frame = 0; frame < 30; frame++) yield return null;
                    yield return SaveFrame(directory, "portrait-26-pdong.png");
                    receipt.portraitShots++;
                    ClosePublicDong();
                    for (var frame = 0; frame < 4; frame++) yield return null;
                }
            }

            // 배달 기사: 판을 접고 지도만
            var rider = Array.Find(Stances, item => item.Key == "rider");
            if (rider != null)
            {
                ApplyStance(rider, false);
                dataPanelOpen = false;
                portraitSheet = 0;
                guManual = false;
                yield return new WaitForSecondsRealtime(1.5f);
                yield return SaveFrame(directory, "portrait-24-rider-map.png");
                receipt.portraitShots++;
            }

            stanceKey = before;
            stancePickerOpen = false;
            portraitSheet = 1;
            portraitSheetScroll = 0f;
            guManual = false;
            Screen.SetResolution(width, height, mode);
            for (var frame = 0; frame < 10; frame++) yield return null;
        }
    }
}
