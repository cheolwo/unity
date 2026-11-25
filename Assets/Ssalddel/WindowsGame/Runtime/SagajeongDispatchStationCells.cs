using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 역 거점의 칸 = 행정동 (2026-11-20 09:01 사용자 결정: 거칠지만 행정동 단위로).
    //  - 자료: Resources/hongdal-hjd-gu.json — vuski/admdongkor ver20261001(원본 통계청 SGIS 행정동 경계, CC BY 4.0).
    //    중랑 · 동대문 · 광진 46개 행정동, 구 지도 좌표(미터), 10 m 단순화. 출처 표기는 화면 아래에 붙인다.
    //  - 동마다 station = 동 중심에서 가장 가까운 역(가안). 역 창은 그 역의 칸을 보이고, 지도에는 고른 역의 칸을 테두리로 그린다.
    //  - 보기만 한다. 영역 규칙(진출 · 운영)은 아직 법정동 판으로 돈다 — 행정동 판으로 옮기는 것은 커널(Kernel/) 일이라 따로 정한다.
    public sealed partial class SagajeongDispatchView
    {
        private const string StationCellsResource = "hongdal-hjd-gu";

        [Serializable]
        private sealed class CellPack
        {
            public string schema;
            public string source;
            public string license;
            public CellDong[] dongs;
        }

        [Serializable]
        private sealed class CellDong
        {
            public string code;
            public string name;
            public string sig;
            public float x;
            public float y;
            public float area;
            public string station;
            public long pop;      // 주민등록 인구 (2026-10-31)
            public long hh;       // 세대
            public long one;      // 1인 세대
            public GuRing[] rings;
        }

        private CellPack cellPack;
        private bool cellTried;

        private CellPack EnsureCells()
        {
            if (cellTried) return cellPack;
            cellTried = true;
            var asset = Resources.Load<TextAsset>(StationCellsResource);
            if (asset == null) { Debug.Log("Hongdal cells: no 행정동 pack"); return null; }
            try { cellPack = JsonUtility.FromJson<CellPack>(asset.text); }
            catch (Exception error) { Debug.LogWarning("Hongdal cells: " + error.Message); }
            Debug.Log($"Hongdal cells: 행정동 {cellPack?.dongs?.Length ?? 0}");
            return cellPack;
        }

        private List<CellDong> StationCellsOf(string station)
        {
            var list = new List<CellDong>();
            var pack = EnsureCells();
            if (pack?.dongs == null || station == null) return list;
            foreach (var dong in pack.dongs)
                if (dong != null && dong.station == station) list.Add(dong);
            return list;
        }

        private static string CellShortName(string name) => name != null && name.EndsWith("동") ? name : name ?? string.Empty;

        // DrawGuSubway가 번화가 다음, 노선 앞에 부른다(Repaint). 고른 역의 칸만 그린다.
        private void DrawStationCells(float now)
        {
            var station = GuStationNow();
            if (station == null) return;
            var keep = 1f - HexTilesAlpha;      // 타일 지도가 떠오르면 행정동 칸은 물러난다(칸 = 타일 제안, SagajeongDispatchHexTiles.cs)
            if (keep <= 0.02f) return;
            var cells = StationCellsOf(station.name);
            if (cells.Count == 0) return;
            var breath = 0.75f + 0.25f * Mathf.Sin(now * 2f);
            foreach (var cell in cells)
            {
                if (cell.rings == null) continue;
                foreach (var ring in cell.rings)
                {
                    var p = ring?.p;
                    if (p == null || p.Length < 6) continue;
                    var previous = GuPoint(p[p.Length - 2], p[p.Length - 1]);
                    for (var i = 0; i + 1 < p.Length; i += 2)
                    {
                        var point = GuPoint(p[i], p[i + 1]);
                        Line(previous, point, Fade(Canvas, 0.8f * keep), 5f);
                        Line(previous, point, Fade(Gold, 0.95f * breath * keep), 2.4f);
                        previous = point;
                    }
                }
                var at = GuPoint(cell.x, cell.y);
                var title = CellShortName(cell.name);
                var width = title.Length * 11.5f + 16f;
                var plate = new Rect(at.x - width / 2f, at.y - 10f, width, 20f);
                Paint(plate, Fade(Canvas, 0.85f * keep), 6);
                Paint(new Rect(plate.x, plate.y, 3f, plate.height), Fade(Gold, keep), 1);
                Text(plate, title, 11, Fade(Gold, keep), true, TextAnchor.MiddleCenter);
            }
        }
    }
}
