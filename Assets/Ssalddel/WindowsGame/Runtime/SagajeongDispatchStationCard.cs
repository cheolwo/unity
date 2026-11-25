using System;
using System.Collections.Generic;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 구 지도의 역 창과 역 둘레의 운영 깊이 (2026-11-19 20:22 사용자: 1번 · 2번 진행).
    //  1. 역을 누르면 역 창이 뜬다. 읽기만 한다: 이름 · 노선 · 내력 · 둘레의 운영 깊이 · 둘레의 내 곳 · 딸린 동.
    //  2. 역 번화가의 불은 꾸민 값이 아니라 그 역 둘레의 운영 깊이를 따른다(SagajeongDispatchSubwayGu.cs가 StationDepth를 읽는다).
    //     운영 깊이 = 역 자리와 둘레 여덟 곳(350 m)이 든 동의 밝기(GuLightOf)를 평균한 값. 0 = 닿지 않음, 1 = 자리 잡음.
    //  - 인구 · 깨어 있는 인구 · 키워드 순위는 규칙이 정해지기 전이라 빈칸으로 둔다.
    // 영역 규칙(TerritoryModule)을 읽기만 한다. 명령을 보내지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const float StationDepthReach = 350f;       // 둘레 표본의 거리(m, 표현값)
        private const float StationNearMetres = 600f;       // 역 창이 세는 "둘레"(m, 표현값. 역 판의 반경과 같다)
        private string guStation;                            // 고른 역(이름). 없으면 null
        private readonly Dictionary<string, string[]> stationDepthCodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> guDongLightNow = new(StringComparer.Ordinal);
        private int guDongLightFrame = -1;

        // 역 둘레의 운영 깊이(0~1).
        private float StationDepth(SubwayStation station)
        {
            if (station?.name == null || guSpace == null || territory == null) return 0f;
            if (!stationDepthCodes.TryGetValue(station.name, out var codes))
            {
                var centre = 이어지는지도Policy.구미터(station.longitude, station.latitude, guSpace.west, guSpace.south);
                codes = new string[10];
                codes[0] = codes[1] = GuDongAt(centre)?.code;      // 역 자리는 두 번 센다
                for (var i = 0; i < 8; i++)
                {
                    var angle = Mathf.PI * 2f * i / 8f;
                    codes[i + 2] = GuDongAt(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * StationDepthReach)?.code;
                }
                stationDepthCodes[station.name] = codes;
            }
            if (guDongLightFrame != Time.frameCount)
            {
                guDongLightFrame = Time.frameCount;
                guDongLightNow.Clear();
                foreach (var zone in territory.Dongs) guDongLightNow[zone.Code] = GuLightOf(zone);
            }
            var sum = 0f;
            foreach (var code in codes)
                if (code != null && guDongLightNow.TryGetValue(code, out var light)) sum += light;
            return Mathf.Clamp01(sum / codes.Length);
        }

        private SubwayStation GuStationNow()
        {
            var pack = subwayGuPack;
            if (guStation == null || pack?.stations == null) return null;
            foreach (var station in pack.stations)
                if (station != null && station.name == guStation) return station;
            return null;
        }

        // 누른 자리에 역이 있으면 그 역을 고른다. 없으면 고른 역을 푼다.
        private bool PickGuStation(Vector2 pointer)
        {
            guStation = null;
            var pack = EnsureSubwayGu();
            if (pack?.stations == null || !(subwayLinesOn || subwayLoreOn)) return false;
            var best = 18f * 18f;
            foreach (var station in pack.stations)
            {
                if (station?.name == null) continue;
                if (!subwayLinesOn && !StationLores.ContainsKey(station.name)) continue;      // 보이는 역만 눌린다
                var distance = (GuSubwayPoint(station.latitude, station.longitude) - pointer).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                guStation = station.name;
            }
            return guStation != null;
        }

        private Rect GuStationCardRect => PortraitScreen ? PortraitCardRect(318f, 372f) : new Rect(22f - Field.x, 216f - Field.y, 318f, 372f);   // K1(11-20): 시안 6의 역 창으로 넓혔다

        // 고른 역의 고리. DrawGuSubway가 역을 그린 뒤 부른다(Repaint).
        private void DrawGuStationMark(float now)
        {
            var station = GuStationNow();
            if (station == null) return;
            var at = GuSubwayPoint(station.latitude, station.longitude);
            Hoop(at, StationNearMetres * guScale, Fade(Gold, 0.55f), 1.6f);
            Hoop(at, 15f + Mathf.Sin(now * 3f) * 1.5f, Gold, 2.4f);
        }

        // 역 창. DrawGuMap이 부른다. 창 위의 누름은 지도로 넘기지 않는다(true).
        private bool DrawGuStationCard()
        {
            var station = GuStationNow();
            if (station == null || dataPanelOpen) return false;      // 자료 창이 열려 있으면 접는다 (SagajeongDispatchDataControl.cs)
            var card = GuStationCardRect;
            var current = Event.current;
            var over = card.Contains(current.mousePosition);
            Paint(new Rect(card.x - 1.5f, card.y - 1.5f, card.width + 3f, card.height + 3f), Fade(Gold, 0.7f), 13);
            Paint(card, Panel, 12);
            var x = card.x + 16f;
            var y = card.y + 12f;
            var wide = card.width - 32f;
            Text(new Rect(x, y, wide, 14f), StationDepth(station) > 0f ? "역 · 내 거점" : "역", 10, Gold, true);
            Text(new Rect(x, y + 16f, wide - 96f, 30f), station.name, 22, Paper, true);
            if (ExpansionButton(new Rect(card.xMax - 40f, card.y + 10f, 28f, 24f), "×", true, Tile)) { guStation = null; stationInsideOpen = false; return true; }
            if (!PortraitScreen && ExpansionButton(new Rect(card.xMax - 106f, card.y + 10f, 60f, 24f), stationInsideOpen ? "역 속 ▼" : "역 속", true, stationInsideOpen ? Gold : Tile)) { stationInsideOpen = !stationInsideOpen; return true; }      // 역으로 내려가기 (SagajeongDispatchStationInside.cs)

            // 노선
            var lineX = x;
            if (station.lines != null)
                foreach (var line in station.lines)
                {
                    var colour = subwayColours.TryGetValue(line ?? "", out var found) ? found : StationLine;
                    var chip = new Rect(lineX, y + 50f, (line?.Length ?? 2) * 11.5f + 14f, 20f);
                    if (chip.xMax > card.xMax - 12f) break;
                    Paint(chip, colour, 10);
                    Text(chip, line, 10, Paper, true, TextAnchor.MiddleCenter);
                    lineX = chip.xMax + 5f;
                }
            // 내력: 노선 칩 옆에
            var hasLore = StationLores.TryGetValue(station.name, out var lore);
            Text(new Rect(lineX + 6f, y + 50f, card.xMax - 14f - (lineX + 6f), 20f), hasLore ? "내력 · " + lore.Line : "내력 · 조사 전", 11, hasLore ? lore.Tint : Soft, hasLore);

            var depth = StationDepth(station);
            var cells = StationCellsOf(station.name);
            var place = PlaceOf(station, depth);                         // 시험 값 (SagajeongDispatchStationPlace.cs)

            // 주문 (시험 값)
            Text(new Rect(x, y + 76f, wide, 14f), "주문 · 시험 값", 10, Gold, true);
            var boxWide = (wide - 12f) / 3f;
            DrawStationFigure(new Rect(x, y + 91f, boxWide, 40f), "시간당", place.OrdersPerHour + "건", Paper);
            DrawStationFigure(new Rect(x + boxWide + 6f, y + 91f, boxWide, 40f), "오늘", place.OrdersToday + "건", Paper);
            DrawStationFigure(new Rect(x + (boxWide + 6f) * 2f, y + 91f, boxWide, 40f), "내가 닫은 몫", place.SharePercent + "%", Gold);

            // 역의 하루: 시간대 승하차 (실제 값 · SagajeongDispatchDataPacks.cs)
            var row = y + 138f;
            row += DrawStationHours(station, x, row, wide) + 4f;

            // 사람: 인구 · 세대는 실제 값(주민등록), 깨어 있는 인구는 시험 값
            long people = 0, households = 0, single = 0;
            var cellNames = new List<string>();
            foreach (var cell in cells) { people += cell.pop; households += cell.hh; single += cell.one; cellNames.Add(cell.name); }
            Text(new Rect(x, row, 60f, 14f), "사람", 10, Gold, true);
            Text(new Rect(x + 60f, row, wide - 60f, 14f), people > 0 ? $"깨어 있는 인구 {place.Awake:N0}명 · 시험 값" : "", 10, Mint, false, TextAnchor.MiddleRight);
            Text(new Rect(x, row + 15f, wide, 16f), people > 0 ? $"인구 {people:N0}명 · {households:N0}세대 · 1인 세대 {single * 100.0 / households:0.0}%" : "인구 · 자료 없음", 11, people > 0 ? Paper : Soft, people > 0);
            row += 36f;

            // 둘레의 내 곳
            int branches = 0, foods = 0, cafes = 0, stores = 0;
            if (territory != null)
            {
                var centre = 이어지는지도Policy.구미터(station.longitude, station.latitude, guSpace.west, guSpace.south);
                foreach (var zone in territory.Dongs)
                    foreach (var pin in zone.Pins)
                    {
                        var dx = (float)pin.X - centre.x;
                        var dy = (float)pin.Y - centre.y;
                        if (dx * dx + dy * dy > StationNearMetres * StationNearMetres) continue;
                        if (pin.Kind == TerritoryPinKinds.Branch) branches++;
                        else if (pin.Kind == TerritoryPinKinds.Food) foods++;
                        else if (pin.Kind == TerritoryPinKinds.Cafe) cafes++;
                        else stores++;
                    }
            }
            Text(new Rect(x, row, wide, 16f), $"둘레 600 m의 내 곳 · 지사 {branches} · 음식점 {foods} · 카페 {cafes} · 창고 {stores}", 11,
                branches + foods + cafes + stores > 0 ? Paper : Soft);
            row += 19f;

            // 거점의 칸 = 행정동 (SagajeongDispatchStationCells.cs) · 생활인구 시간대 지수 (SagajeongDispatchStationInside.cs)
            Text(new Rect(x, row, wide, 16f), cells.Count > 0 ? "딸린 동 · " + string.Join(" · ", cellNames) : "딸린 동 · 없음(가까운 다른 역의 칸이다)", 11, cells.Count > 0 ? Gold : Soft, cells.Count > 0);
            var codes = new List<string>();
            foreach (var cell in cells) codes.Add(cell.code);
            var life = LifeLine(codes, cellNames);
            Text(new Rect(x, row + 18f, wide, 16f), life ?? "생활인구 지수 · 자료 없음", 10, Ink(0x9CC4E4));
            row += 40f;

            // 운영 깊이 · 먹은 키워드 (시험 값)
            Text(new Rect(x, row, wide, 16f), $"운영 깊이 {Mathf.RoundToInt(depth * 100f)}% · 먹은 키워드 {place.Eaten} / {place.Keywords.Length} · 시험 값", 11, place.Eaten > 0 ? Gold : Soft);
            Paint(new Rect(x, row + 18f, wide, 5f), Tile, 3);
            if (place.Eaten > 0) Paint(new Rect(x, row + 18f, wide * place.Eaten / place.Keywords.Length, 5f), Gold, 3);
            Text(new Rect(x, row + 25f, wide, 14f), "출처 · 주민등록 10-31 · 서울시 승하차 · 서울시 · KT 생활인구 · 그 밖 시험 값", 9, Soft);

            if (!over || current.type == EventType.Repaint || current.type == EventType.Layout) return false;
            if (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag || current.type == EventType.ScrollWheel)
            {
                current.Use();
                return true;
            }
            return false;
        }
    }
}
