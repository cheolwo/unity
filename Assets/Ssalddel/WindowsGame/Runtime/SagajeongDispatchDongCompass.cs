using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 팔방 이동 — 화면 층 (2026-11-17 사용자 방향). 한 행정동 디오라마(이 동만 보기)에서 위 · 아래 · 왼쪽 · 오른쪽과
    // 대각선까지 여덟 방향으로 맞닿은 동을 옮겨 다니며 관리하는 재미. 규칙은 팔방이웃Policy에 있다.
    //  - 그 동 경계 둘레의 여덟 자리에 「화살표 · 이웃 동 이름」 칸을 세운다. 방향은 화면 기준이라 지도를 돌리면 함께 돈다.
    //  - 누르면 그 동만 보기로 옮긴다. 동 관리 창이 열려 있었으면 새 동으로 따라간다. 「역 판으로」 단추는 그대로 남는다.
    //  - 이웃 관계는 읽은 동 모듈의 경계로 처음 한 번만 잰다. 읽지 않은(굽지 않은) 동은 칸에 들지 않는다.
    // 보기만 바꾼다. 시계 · 주문 · 돈 · 영역 규칙을 읽거나 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const float CompassGap = 26f;           // 동 경계의 화면 범위에서 칸까지 띄우는 거리(px)
        private Dictionary<string, List<(string Id, double Shared)>> dongNeighbours;

        // 읽은 동끼리 맞닿은 길이(m). 처음 한 번만 잰다.
        private List<(string Id, double Shared)> DongNeighbours(string regionId)
        {
            if (dongNeighbours == null)
            {
                dongNeighbours = new Dictionary<string, List<(string, double)>>(StringComparer.Ordinal);
                var rings = new List<(string Id, List<(double E, double N)> Ring)>();
                foreach (var module in dongModules)
                {
                    if (!module.Loaded || module.Latitudes == null || module.Latitudes.Length < 3) continue;
                    var ring = new List<(double E, double N)>(module.Latitudes.Length);
                    for (var i = 0; i < module.Latitudes.Length; i++) ring.Add(DongLocal(module.Latitudes[i], module.Longitudes[i]));
                    rings.Add((module.Entry.regionId, ring));
                    dongNeighbours[module.Entry.regionId] = new List<(string, double)>();
                }
                for (var i = 0; i < rings.Count; i++)
                    for (var j = i + 1; j < rings.Count; j++)
                    {
                        var shared = Math.Min(팔방이웃Policy.SharedLength(rings[i].Ring, rings[j].Ring),
                            팔방이웃Policy.SharedLength(rings[j].Ring, rings[i].Ring));
                        if (shared < 팔방이웃Policy.MinSharedMetres) continue;
                        dongNeighbours[rings[i].Id].Add((rings[j].Id, shared));
                        dongNeighbours[rings[j].Id].Add((rings[i].Id, shared));
                    }
                Debug.Log("Hongdal dong compass: neighbours " + string.Join(", ",
                    dongNeighbours.Select(pair => (DongModuleById(pair.Key)?.Entry.name ?? pair.Key) + " " + pair.Value.Count)));
            }
            return dongNeighbours.TryGetValue(regionId ?? string.Empty, out var list) ? list : new List<(string, double)>();
        }

        // 지금 화면 방향으로 나눈 여덟 칸(0 = 위, 시계 방향). 칸마다 이웃 동 ID 또는 null.
        private string[] DongCompassSlots(DongModuleState focus)
        {
            if (focus == null) return new string[8];
            var lens = BaseLens;
            var items = new List<(string, double, double, double)>();
            foreach (var (id, shared) in DongNeighbours(focus.Entry.regionId))
            {
                var other = DongModuleById(id);
                if (other == null || !other.Loaded) continue;
                var (u, v) = 행정동모듈Policy.Screen(other.CentreEast - focus.CentreEast, other.CentreNorth - focus.CentreNorth,
                    dioramaOn, lens.YawCos, lens.YawSin, lens.PitchSin);
                items.Add((id, u, -v, shared));                 // 화면 아래쪽 + → 위쪽 +
            }
            return 팔방이웃Policy.Assign(items);
        }

        // 그 동으로 옮긴다. 동 관리 창이 열려 있었으면 새 동으로 따라간다. 옮겼으면 true.
        private bool MoveDongAround(string regionId)
        {
            var deskOpen = DongDeskVisible;
            if (!PickDong(regionId)) return false;
            if (deskOpen) OpenDongDesk(regionId);
            Debug.Log("Hongdal dong compass: moved to " + regionId);
            return true;
        }

        // 이 동만 보기에서 그 동 경계 둘레에 여덟 방향 칸을 그린다. 눌렀으면 true(그 프레임의 나머지는 그리지 않는다).
        // points: 그 동 경계를 화면에 놓은 점(GUI.BeginGroup(Field) 안의 좌표).
        private bool DrawDongCompass(DongModuleState focus, Vector2[] points)
        {
            if (dongView != DongView.Solo || focus == null || points == null || points.Length < 3) return false;
            if (PlaceVisible || CloseNow > 0.5f || !DongModulesActive) return false;
            var slots = DongCompassSlots(focus);
            if (slots.All(slot => slot == null)) return false;
            float left = float.MaxValue, right = float.MinValue, top = float.MaxValue, bottom = float.MinValue;
            foreach (var point in points)
            {
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                top = Mathf.Min(top, point.y); bottom = Mathf.Max(bottom, point.y);
            }
            var centre = new Vector2((left + right) / 2f, (top + bottom) / 2f);
            var halfX = (right - left) / 2f + CompassGap;
            var halfY = (bottom - top) / 2f + CompassGap;
            // 칸이 설 수 있는 곳: 위쪽 띠 아래 · 아래 운영 띠 위. 동 관리 창 · 요약 지도 · 오른쪽 칸과 겹치면 그 옆으로 비킨다.
            var minX = 12f;
            var maxX = Field.width - 12f;
            var blocks = new List<Rect>();
            if (DongDeskVisible) blocks.Add(new Rect(DongDeskPanel.x - Field.x, DongDeskPanel.y - Field.y, DongDeskPanel.width, DongDeskPanel.height));
            if (MiniMapShown) blocks.Add(new Rect(MiniMapRect.x - Field.x, MiniMapRect.y - Field.y, MiniMapRect.width, MiniMapRect.height));
            if (selectedBuilding != null || dongPanelOpen || SettingsVisible) blocks.Add(new Rect(ExpansionPanel.x - Field.x, 0, Field.width, Field.height));
            var minY = DongTradeCardTop - Field.y + 14f;
            var maxY = Field.height - 130f;
            // 동 관리 창이 닫혀 있으면 동 위에 동 사업 칸이 선다(DrawDongBusinessCard와 같은 자리). 칸과 겹치는 방향 칸은 그 아래로 내린다.
            var card = Rect.zero;
            if (DongFlagsActive && !(DongDeskVisible && BusinessDong()?.Entry.regionId == focus.Entry.regionId))
            {
                var peak = points.OrderBy(point => point.y).First();
                var cardY = Mathf.Max(peak.y - DongTradeCardHeight - 8 - DongTradeCardLift, DongTradeCardTop - Field.y);
                card = new Rect(peak.x - DongTradeCardWidth / 2 - 40, cardY - 4, DongTradeCardWidth + 80, DongTradeCardHeight + 8);
            }
            for (var slot = 0; slot < 8; slot++)
            {
                var id = slots[slot];
                if (id == null) continue;
                var module = DongModuleById(id);
                if (module == null) continue;
                var angle = slot * 45f * Mathf.Deg2Rad;
                var at = new Vector2(centre.x + Mathf.Sin(angle) * halfX, centre.y - Mathf.Cos(angle) * halfY);
                var text = 팔방이웃Policy.Arrows[slot] + " " + module.Entry.name;
                var width = text.Length * 12.5f + 22f;
                var chip = new Rect(Mathf.Clamp(at.x - width / 2f, minX, Mathf.Max(minX, maxX - width)),
                    Mathf.Clamp(at.y - 13f, minY, Mathf.Max(minY, maxY)), width, 26);
                if (card.width > 0 && chip.Overlaps(card)) chip.y = Mathf.Min(card.yMax + 6f, Mathf.Max(minY, maxY));
                foreach (var block in blocks)
                {
                    if (!chip.Overlaps(block)) continue;
                    chip.x = chip.center.x < block.center.x ? block.x - chip.width - 8f : block.xMax + 8f;
                }
                chip.x = Mathf.Clamp(chip.x, minX, Mathf.Max(minX, maxX - chip.width));
                DongPlate(module, out var tone, out var rim, out _);
                var hover = chip.Contains(Event.current.mousePosition);
                Paint(new Rect(chip.x - 1.5f, chip.y - 1.5f, chip.width + 3, chip.height + 3), rim.a > 0f ? rim : Rim, 8);
                Paint(chip, hover ? Tile : Canvas, 7);
                Text(chip, text, 12, tone, true, TextAnchor.MiddleCenter);
                if (!HitMapMarker(chip)) continue;
                관찰지도수동이동();
                return MoveDongAround(id);
            }
            return false;
        }
    }
}
