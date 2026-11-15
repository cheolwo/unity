using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        // 실제 좌표·이름은 팩에서 읽는다. 아래 점유는 화면 판독에만 쓰고 커널·공간 팩에는 저장하지 않는다.
        private readonly 구지도이름배치Policy guNameLayout = new();
        private readonly Dictionary<(string, int, bool), Vector2> guNameSizes = new();
        private readonly Dictionary<string, Rect> guDongNameRects = new();
        private readonly Dictionary<string, Rect> guSigNameRects = new();
        private readonly List<GuPreparedName> guNaturalNames = new();
        private readonly List<GuPreparedName> guRoadNames = new();

        private struct GuPreparedName
        {
            public string Text;
            public Vector2 Point;
            public Vector2 Size;
            public float Angle;
            public int FontSize;
            public Color Color;
        }

        private Vector2 GuNameSize(string text, int size, bool bold = true)
        {
            var key = (text, size, bold);
            if (guNameSizes.TryGetValue(key, out var measured)) return measured;
            var styleKey = (size, bold, TextAnchor.MiddleCenter, false);
            if (!styles.TryGetValue(styleKey, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = bold ? boldFont : font,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = size,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    padding = new RectOffset(),
                };
                styles.Add(styleKey, style);
            }
            var actual = style.CalcSize(new GUIContent(text));
            measured = new Vector2(Mathf.Ceil(actual.x) + 6f, Mathf.Max(size + 4f, Mathf.Ceil(actual.y)));
            guNameSizes.Add(key, measured);
            return measured;
        }

        private Rect GuNamePlateRect(Vector2 point, string text, int size)
        {
            var measured = GuNameSize(text, size);
            return Centered(point, measured.x + 10f, measured.y + 6f);
        }

        private void ReserveGuOverlay(Rect rect)
        {
            rect.position -= Field.position;
            guNameLayout.Reserve(new 구지도이름영역(rect.center.x, rect.center.y, rect.width + 4f, rect.height + 4f));
        }

        private bool TryGuPlate(Vector2 point, string text, int size, out Rect rect)
        {
            rect = GuNamePlateRect(point, text, size);
            return guNameLayout.TryReserve(new 구지도이름영역(point.x, point.y, rect.width, rect.height));
        }

        private static float GuReadableAngle(float angle)
        {
            if (float.IsNaN(angle) || float.IsInfinity(angle)) return angle; // 잘못된 원본 방향은 점유 검사에서 제외한다.
            angle %= 180f;
            if (angle > 90f) angle -= 180f;
            if (angle < -90f) angle += 180f;
            return angle;
        }

        private bool TryGuName(List<GuPreparedName> output, string text, Vector2 point, float angle, int size, Color color)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var measured = GuNameSize(text, size);
            angle = GuReadableAngle(angle);
            if (!guNameLayout.TryReserve(new 구지도이름영역(point.x, point.y, measured.x, measured.y, angle))) return false;
            output.Add(new GuPreparedName { Text = text, Point = point, Size = measured, Angle = angle, FontSize = size, Color = color });
            return true;
        }

        // 먼저 업무 패널·선택 가능한 지명·표식을 보호한 뒤, 회전 글자의 실제 폭으로 지명과 도로를 배치한다.
        private void PrepareGuNames(TerritorySnapshot view)
        {
            guNameLayout.Reset(Field.width, Field.height);
            guDongNameRects.Clear();
            guSigNameRects.Clear();
            guNaturalNames.Clear();
            guRoadNames.Clear();
            ReserveGuOverlay(AttributionPanel);
            if (mapLevel != MapLevel.Dong) ReserveGuOverlay(LevelBanner);
            if (levelPromptOpen) ReserveGuOverlay(LevelPromptPanel);
            if (expansionView && !mapOnly)
            {
                ReserveGuOverlay(ExpansionHeader);
                if (!showDesk)
                {
                    ReserveGuOverlay(ExpansionRail);
                    if (SidePanelsVisible)
                    {
                        ReserveGuOverlay(ExpansionOverview);
                        ReserveGuOverlay(BusinessLayerPanel);
                    }
                    if (TidyScreen) { ReserveGuOverlay(SideToggleButton); ReserveGuOverlay(DailyReportButton); }     // 정리한 첫 화면: 도구 줄 대신 운영 현황 단추 하나
                    else
                    {
                        ReserveGuOverlay(ExpansionDongButton);
                        ReserveGuOverlay(ExpansionZoneButton);
                        ReserveGuOverlay(ExpansionDioramaButton);
                        ReserveGuOverlay(MapEffectsControl);       // r157 지도 효과 선택 (Claude 2026-11-13)
                        if (DioramaVisible) ReserveGuOverlay(ExpansionDioramaFacts);
                    }
                    if (ChainStripVisible) ReserveGuOverlay(ChainStrip);
                    if (SignalBarVisible) ReserveGuOverlay(SignalBar);
                    ReserveGuOverlay(CycleDockRect);           // 결정 순회 버튼
                }
                if (TerritoryPanelVisible || ExpansionPanelVisible || CycleCardVisible || SettingsVisible || DailyReportVisible) ReserveGuOverlay(ExpansionPanel);
                if (showDesk) ReserveGuOverlay(DeskPanel);
            }
            if (dongPanelOpen) ReserveGuOverlay(DongPanel);
            if (zonePanelOpen) ReserveGuOverlay(ZonePanel);
            ReserveGuOverlay(new Rect(560, 622, 160, 26)); // 영역 전체 보기 위치는 직접 이동 중에도 비운다.
            ReserveGuOverlay(new Rect(328, 624, 172, 30)); // 축척 글자와 막대.
            if (guScale >= 0.13f && dioramaRoot != null && dioramaEast > dioramaWest && dioramaNorth > dioramaSouth)
            {
                var northWest = GuPoint((float)((dioramaWest - guSpace.west) * 88224.0), (float)((dioramaNorth - guSpace.south) * 111320.0));
                var southEast = GuPoint((float)((dioramaEast - guSpace.west) * 88224.0), (float)((dioramaSouth - guSpace.south) * 111320.0));
                var point = new Vector2((northWest.x + southEast.x) * 0.5f, northWest.y - 13f);
                var rect = GuNamePlateRect(point, "사가정 일대 · 더 확대하면 동네 지도", 10);
                guNameLayout.Reserve(new 구지도이름영역(point.x, point.y, rect.width, rect.height));
            }

            var lit = view == null ? new HashSet<string>()
                : new HashSet<string>(view.Dongs.Where(zone => zone.Level > TerritoryLevels.Unknown).Select(zone => zone.Sig));
            if (view != null)
            {
                // 핀·숫자 배지·가까이에서 나오는 표식 이름은 도로 글자보다 우선한다.
                foreach (var zone in view.Dongs)
                    foreach (var pin in zone.Pins)
                    {
                        var point = GuPoint((float)pin.X, (float)pin.Y);
                        var titleSize = GuNameSize(pin.Title ?? string.Empty, 11);
                        guNameLayout.Reserve(new 구지도이름영역(point.x, point.y - 20f, Mathf.Max(56f, titleSize.x + 16f), 80f));
                    }
            }
            foreach (var sig in guSpace.sigs ?? Array.Empty<GuSig>())
            {
                var point = GuPoint(sig.x, sig.y);
                if (lit.Contains(sig.code))
                {
                    if (TryGuPlate(point, sig.name, 17, out var rect)) guSigNameRects[sig.code] = rect;
                }
            }
            if (view != null)
            {
                foreach (var zone in view.Dongs.OrderBy(zone => zone.Level <= TerritoryLevels.Unknown && !zone.Candidate ? 1 : 0)
                    .ThenBy(zone => zone.Code, StringComparer.Ordinal))
                {
                    var unknown = zone.Level <= TerritoryLevels.Unknown && !zone.Candidate;
                    if (unknown && guScale < GuNearScale) continue;
                    var title = zone.Candidate ? "+ " + zone.Name : zone.Name;
                    var size = unknown || zone.Candidate ? 11 : 12;
                    if (TryGuPlate(GuPoint((float)zone.X, (float)zone.Y), title, size, out var rect)) guDongNameRects[zone.Code] = rect;
                }
            }
            foreach (var name in guSpace.names ?? Array.Empty<GuName>())
            {
                var water = name.kind == "water";
                TryGuName(guNaturalNames, name.text, GuPoint(name.x, name.y), name.angle, water ? 12 : 11,
                    Fade(water ? Ink(0xB6DDF8) : Ink(0xB9DEC4), 0.88f));
            }
            foreach (var sig in guSpace.sigs ?? Array.Empty<GuSig>())
                if (!lit.Contains(sig.code))
                    TryGuName(guNaturalNames, sig.name, GuPoint(sig.x, sig.y), 0f, sig.focus ? 13 : 11, Fade(Soft, sig.focus ? 0.85f : 0.62f));

            var level = guScale >= 0.2f ? 3 : guScale >= 0.12f ? 2 : 1;
            var repeated = new Dictionary<string, List<Vector2>>(StringComparer.Ordinal);
            var centre = GuSafeArea.center;
            foreach (var road in (guSpace.roads ?? Array.Empty<GuRoad>())
                .Where(road => !string.IsNullOrEmpty(road.text) && road.level <= level)
                .OrderBy(road => GuKeyRoad(road.text) ? 0 : 1).ThenBy(road => road.level)
                .ThenBy(road => (GuPoint(road.x, road.y) - centre).sqrMagnitude)
                .ThenBy(road => road.text, StringComparer.Ordinal))
            {
                var point = GuPoint(road.x, road.y);
                var size = road.level == 1 ? 12 : 11;
                var spacing = Mathf.Max(95f, GuNameSize(road.text, size).x * 1.7f);
                if (repeated.TryGetValue(road.text, out var prior) && prior.Any(other => (other - point).sqrMagnitude < spacing * spacing)) continue;
                if (!TryGuName(guRoadNames, road.text, point, road.angle, size,
                    Fade(road.level == 1 ? Ink(0xF8EAC4) : Ink(0xD9E5ED), road.level == 1 ? 0.96f : 0.86f))) continue;
                if (prior == null) repeated.Add(road.text, prior = new List<Vector2>());
                prior.Add(point);
            }
        }

        private static bool GuKeyRoad(string text) => text is "동일로" or "망우로" or "사가정로" or "천호대로";

        private void DrawPreparedGuName(GuPreparedName name)
        {
            // 기존 제자리 회전·Overflow 교정을 보존한다. 원점 근처의 음수 사각형은 IMGUI 그룹에서 잘린다.
            GuTurnedText(name.Point, name.Angle, name.Text, name.FontSize, name.Color, true, Fade(Canvas, 0.95f));
        }
    }
}
