using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // K1 (2026-11-20 12:12 사용자: 「구 지도는 시안 6 그대로」) — 역을 고르면 오른쪽에 홍달 플레이스 키워드 창이 뜬다.
    //  - 키워드 = 곳 + 업종. 월 검색량에 비례해 경쟁 세력이 몰린다(11:01 결정). 자본금 투입 = 내보낼 분대 수(11:03 결정).
    //  - 이 파일의 숫자는 모두 **시험 값**이다. 역 이름으로 정해져 늘 같다. 규칙(먹는다 · 자동 결산)은 K2에서 Kernel에 넣는다.
    //  - 읽기만 한다. 단을 고르는 것은 화면의 고름일 뿐 돈 · 주문 · 영역을 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private sealed class PlaceKeyword
        {
            public string Name;
            public int Volume;        // 월 검색량
            public int Rank;          // 0 = 순위 밖
            public string State;      // 거점 · 먹음 · 다투는 중 · 멀다 · 닿지 않음
            public bool Mine;
        }

        private sealed class PlaceRead
        {
            public PlaceKeyword[] Keywords;
            public int Eaten, OrdersPerHour, OrdersToday, SharePercent, Awake;
        }

        private static readonly string[] PlaceKinds = { "", " 치킨", " 분식", " 카페", " 맛집" };
        private static readonly float[] PlaceKindWeight = { 3f, 0.9f, 0.35f, 1.1f, 2f };
        private static readonly int[] PlaceTierSquads = { 2, 4, 6 };
        private static readonly int[] PlaceTierCost = { 3000, 8000, 18000 };
        private const int PlaceRivalPerThousand = 20;     // 월 검색량 1,000회마다 한 물결의 경쟁 세력
        private string stationPlaceOf;
        private int stationPlaceKeyword = 3, stationPlaceTier = 1;

        private PlaceRead PlaceOf(SubwayStation station, float depth)
        {
            var seed = 0;
            foreach (var letter in station.name) seed = (seed * 31 + letter) & 0x7fffffff;
            var basis = 1200 + seed % 1300;
            var read = new PlaceRead { Keywords = new PlaceKeyword[PlaceKinds.Length] };
            for (var i = 0; i < PlaceKinds.Length; i++)
            {
                var word = new PlaceKeyword { Name = station.name + PlaceKinds[i], Volume = Mathf.RoundToInt(basis * PlaceKindWeight[i] / 100f) * 100 };
                if (depth <= 0f) { word.State = "닿지 않음"; }
                else if (i == 0) { word.State = "거점"; word.Rank = 1; word.Mine = true; }
                else if (i == 2 || i == 1 && depth >= 0.5f) { word.State = "먹음"; word.Rank = 1; word.Mine = true; }
                else if (i == 4) { word.State = "멀다"; word.Rank = 7; }
                else { word.State = "다투는 중"; word.Rank = i == 1 ? 3 : 4; }
                if (word.Mine) read.Eaten++;
                read.Keywords[i] = word;
            }
            read.OrdersPerHour = 6 + seed % 12;
            read.OrdersToday = read.OrdersPerHour * 7 - seed % 5;
            read.SharePercent = Mathf.RoundToInt(depth * 40f + read.Eaten * 4f);
            long people = 0;
            foreach (var cell in StationCellsOf(station.name)) people += cell.pop;
            read.Awake = (int)(people * (0.002f + depth * 0.01f));
            return read;
        }

        private void DrawStationFigure(Rect box, string label, string value, Color tint)
        {
            Paint(box, Tile, 7);
            Text(new Rect(box.x + 8f, box.y + 4f, box.width - 12f, 14f), label, 9, Soft);
            Text(new Rect(box.x + 8f, box.y + 18f, box.width - 12f, 22f), value, 15, tint, true);
        }

        // 오른쪽 창. 영역 패널 자리에 그린다(SagajeongDispatchExpansion.cs).
        private void DrawStationPlacePanel()
        {
            var station = GuStationNow();
            if (station == null) return;
            if (stationPlaceOf != station.name) { stationPlaceOf = station.name; stationPlaceKeyword = 3; stationPlaceTier = 1; }
            var read = PlaceOf(station, StationDepth(station));
            var panel = ExpansionPanel;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            var x = panel.x + 14f;
            var y = panel.y;
            var wide = panel.width - 28f;
            Text(new Rect(x, y + 11f, wide, 16f), "홍달 플레이스 · 시험 값", 10, Gold, true);
            Text(new Rect(x, y + 27f, wide, 22f), station.name + " 키워드", 15, Paper, true);
            Text(new Rect(x, y + 54f, 100f, 14f), "키워드", 9, Soft);
            Text(new Rect(x + 96f, y + 54f, 56f, 14f), "월 검색", 9, Soft, false, TextAnchor.MiddleRight);
            Text(new Rect(x + 152f, y + 54f, 34f, 14f), "순위", 9, Soft, false, TextAnchor.MiddleRight);
            Text(new Rect(x + 186f, y + 54f, wide - 186f, 14f), "상태", 9, Soft, false, TextAnchor.MiddleRight);
            for (var i = 0; i < read.Keywords.Length; i++)
            {
                var word = read.Keywords[i];
                var row = new Rect(x - 6f, y + 70f + i * 26f, wide + 12f, 24f);
                if (i == stationPlaceKeyword) Paint(row, Tile, 6);
                var tint = word.Mine ? Gold : word.State == "다투는 중" ? Ink(0xFF9F5A) : Soft;
                Text(new Rect(x, row.y, 100f, 24f), word.Name, 11, Paper, i == stationPlaceKeyword);
                Text(new Rect(x + 96f, row.y, 56f, 24f), word.Volume.ToString("N0"), 11, Paper, false, TextAnchor.MiddleRight);
                Text(new Rect(x + 152f, row.y, 34f, 24f), word.Rank > 0 ? word.Rank + "위" : "—", 11, Paper, false, TextAnchor.MiddleRight);
                Text(new Rect(x + 186f, row.y, wide - 186f, 24f), word.State, 10, tint, true, TextAnchor.MiddleRight);
                if (Hit(row)) stationPlaceKeyword = i;
            }

            // 고른 키워드: 검색량 → 경쟁 세력, 자본금 투입 → 내보낼 병력
            var chosen = read.Keywords[Mathf.Clamp(stationPlaceKeyword, 0, read.Keywords.Length - 1)];
            var box = new Rect(x - 6f, y + 206f, wide + 12f, 172f);
            Paint(box, chosen.Mine ? Fade(Gold, 0.8f) : Ink(0xFF9F5A), 9);
            Paint(new Rect(box.x + 1.5f, box.y + 1.5f, box.width - 3f, box.height - 3f), Panel, 8);
            Text(new Rect(x, box.y + 8f, wide, 18f), chosen.Name, 13, Paper, true);
            var rivals = chosen.Volume * PlaceRivalPerThousand / 1000;
            Text(new Rect(x, box.y + 27f, wide, 14f), $"월 {chosen.Volume:N0}회 → 경쟁 세력 물결당 {rivals} × 3", 10, Soft);
            Text(new Rect(x, box.y + 46f, wide, 14f), "자본금 투입 → 내보낼 병력", 10, Gold, true);
            var tierWide = (wide - 8f) / 3f;
            for (var tier = 0; tier < 3; tier++)
            {
                var cell = new Rect(x + tier * (tierWide + 4f), box.y + 63f, tierWide, 44f);
                Paint(cell, tier == stationPlaceTier ? Gold : Rim, 7);
                Paint(new Rect(cell.x + 1.5f, cell.y + 1.5f, cell.width - 3f, cell.height - 3f), Tile, 6);
                Text(new Rect(cell.x, cell.y + 5f, cell.width, 16f), $"{tier + 1}단 · {PlaceTierCost[tier]:N0}원", 10, tier == stationPlaceTier ? Gold : Paper, true, TextAnchor.MiddleCenter);
                Text(new Rect(cell.x, cell.y + 23f, cell.width, 14f), $"분대 {PlaceTierSquads[tier]}", 10, Soft, false, TextAnchor.MiddleCenter);
                if (Hit(cell)) stationPlaceTier = tier;
            }
            Text(new Rect(x, box.y + 112f, wide, 28f), "돈은 수, 사람은 질. 깨어 있는 사람이 있어야 분대가 선다.", 9, Soft, wrap: true);
            Paint(new Rect(x, box.y + 142f, wide, 22f), Tile, 6);
            Text(new Rect(x, box.y + 142f, wide, 22f), "자동 결산 · 규칙 준비 중", 10, Soft, true, TextAnchor.MiddleCenter);

            Text(new Rect(x, y + 386f, wide, 36f), "검색량에 비례해 그 키워드를 차지하려는 경쟁 세력이 몰린다. 키워드를 먹어 갈수록 이웃 역으로 빛이 흐른다.", 9, Soft, wrap: true);
        }
    }
}
