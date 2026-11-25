using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 지도 자료 아이콘 (2026-11-23 사용자: 「코덱스도 자리별로 좀 어울리는 아이콘들 생성해서 붙이도록 해놨으니까 여기 클로드에서도 같은 작업을」).
    //  - 그림: Resources/HongdalMapIcons/<열쇠>.png — 흰 획 · 투명 48점. 색은 그릴 때 입힌다.
    //    · Codex r1 40개(hongdal-map-scale-index-20261123/symbols, 버스 정류소 · 지하철역 · 시장 · 쉼터 · 공원 · 자전거 · 출입 · 공항 …)
    //      같은 뜻의 Codex 아이콘이 있으면 그것을 쓴다(자전거 · 출입). Claude 것은 Codex 표에 없는 갈래만.
    //    · Claude 20개(같은 꼴: 24 격자 · 획 1.75 · 둥근 끝) — 중랑 생활 시설 24갈래 중 Codex 표에 없던 것(보행등 · 그늘막 · 화장실 …).
    //    원본 SVG · 대응표 · 지문: Hongdal artifacts/local/hongdal-unity-claude-20261112/map-icons-20261123-r1/.
    //  - 쓰는 곳: 구 지도의 시설 점(가까이 가면 점 → 아이콘 배지, 겹치면 점), 타일 창의 시설 줄, 자료 창의 갈래 줄,
    //    동 디오라마의 범례 줄과 이름표.
    //  - 아이콘은 「무엇이 있다고 보고된 곳」의 표지일 뿐이다. 자리 · 출입구 · 개방 상태를 뜻하지 않는다. 규칙 · 돈 · 주문과 상관없다.
    public sealed partial class SagajeongDispatchView
    {
        private const string MapIconFolder = "HongdalMapIcons/";
        private const float FacilityIconScale = 0.26f;     // 구 지도에서 이 배율부터 점 대신 아이콘 배지(타일 한 칸 ≈ 78점)
        private readonly Dictionary<string, Texture2D> mapIcons = new Dictionary<string, Texture2D>();
        private readonly HashSet<long> facilityIconCells = new HashSet<long>();
        private int mapIconsMissing;

        // 생활 시설 갈래(hongdal-data-facilities.json keys) → 아이콘 열쇠.
        private static string FacilityIconKey(string key) => key switch
        {
            "pedestrian_signal" => "pedestrian-signal",
            "bus_stop" => "bus-stop",
            "pedestrian_entrance" => "access",                // Codex 출입·이동 접근
            "street_bin" => "street-bin",
            "bicycle_parking" => "bicycle-parking",
            "heat_shade" => "heat-shade",
            "heat_shelter" => "heat-shelter",
            "public_toilet" => "public-toilet",
            "bike_share" => "bike",                          // Codex 자전거
            "drinking_water" => "drinking-water",
            "park" => "park",
            "open_public_facility" => "open-public-facility",
            "school_zone_facility" => "school-zone",
            "parking" => "parking",
            "seoul_market_inventory" => "market",
            "station_elevator" => "station-elevator",
            "metro_unnumbered_exit" => "metro-exit",
            "smart_pole" => "smart-pole",
            "city_ev_charging" => "ev-charging",
            "designated_specialty_street" => "specialty-street",
            "parcel_locker" => "parcel-locker",
            "tourism_site" => "tourism-site",
            "protected_tree" => "protected-tree",
            "campground" => "campground",
            _ => "building",
        };

        // 자료 창의 갈래(FacilityGroupNames 차례) → 대표 아이콘.
        private static string FacilityGroupIconKey(int group) => group switch
        {
            0 => "pedestrian-signal",
            1 => "bus-stop",
            2 => "access",
            3 => "heat-shade",
            4 => "bike",
            5 => "park",
            6 => "market",
            7 => "metro-exit",
            _ => "building",
        };

        private Texture2D MapIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (mapIcons.TryGetValue(key, out var found)) return found;
            var texture = Resources.Load<Texture2D>(MapIconFolder + key);
            if (texture == null && mapIconsMissing++ < 4) Debug.LogWarning($"Hongdal map icon missing: {key}");
            mapIcons[key] = texture;
            return texture;
        }

        // 흰 그림에 색을 입혀 그린다. 그림이 없으면 아무것도 그리지 않는다(false).
        private bool DrawMapIcon(Rect rect, string key, Color tint)
        {
            var texture = MapIcon(key);
            if (texture == null || Event.current.type != EventType.Repaint) return texture != null;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
            return true;
        }

        // 구 지도: 한 장면을 그리기 전에 겹침 칸을 비운다. 점을 먼저 다 그리고(DrawFacilityMark) 배지는 끝에 한 번에(EndFacilityMarks) —
        // 뒤에 오는 점이 앞의 배지를 덮지 않게.
        private readonly List<(Vector2 at, string icon, Color colour, float alpha, float badge)> facilityBadges =
            new List<(Vector2, string, Color, float, float)>();

        private void BeginFacilityMarks()
        {
            facilityIconCells.Clear();
            facilityBadges.Clear();
        }

        // 구 지도의 시설 한 곳. 멀면 예전처럼 점, 가까우면 어두운 원 위 갈래 색 아이콘(배지). 배지 칸이 이미 찼으면 작은 점으로 둔다.
        private void DrawFacilityMark(Vector2 at, string key, float radius, float alpha, Color colour) =>
            DrawIconMark(at, FacilityIconKey(key), radius, alpha, colour);

        private void DrawIconMark(Vector2 at, string icon, float radius, float alpha, Color colour)
        {
            var iconAlpha = 0f;
            var badge = Mathf.Clamp(guScale * 30f, 8.5f, 13f);
            if (guScale >= FacilityIconScale && MapIcon(icon) != null)
            {
                var cell = badge * 2.1f;
                var id = ((long)Mathf.FloorToInt(at.x / cell) << 32) ^ (uint)Mathf.FloorToInt(at.y / cell);
                if (facilityIconCells.Add(id))
                {
                    iconAlpha = alpha * Mathf.InverseLerp(FacilityIconScale, FacilityIconScale + 0.03f, guScale);
                    if (iconAlpha > 0.02f) facilityBadges.Add((at, icon, colour, iconAlpha, badge));
                }
                else radius *= Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(FacilityIconScale, FacilityIconScale + 0.03f, guScale));
            }
            var dotAlpha = alpha * (1f - iconAlpha);
            if (dotAlpha <= 0.02f) return;
            Disc(at, radius + 1.5f, Fade(Canvas, 0.75f * dotAlpha));
            Disc(at, radius, Fade(colour, 0.9f * dotAlpha));
        }

        private void EndFacilityMarks()
        {
            foreach (var (at, icon, colour, alpha, badge) in facilityBadges)
            {
                Disc(at, badge + 1.4f, Fade(colour, 0.9f * alpha));
                Disc(at, badge, Fade(Canvas, 0.95f * alpha));
                var side = badge * 1.4f;
                DrawMapIcon(new Rect(at.x - side / 2f, at.y - side / 2f, side, side), icon, Fade(colour, alpha));
            }
            facilityBadges.Clear();
        }

        // 타일 창: 시설 갈래를 아이콘 + 개수로 한 줄(많은 차례). 남으면 「외 n갈래」.
        private void DrawFacilityIconRow(Rect row, int[] counts, int top, int q = int.MinValue, int r = int.MinValue)
        {
            var x = row.x;
            var shown = 0;
            // 위치 확인 표식(Codex · 공표 대표점)이 이 칸에 있으면 먼저, 금테로.
            var ground = q == int.MinValue ? null : GroundCountsOfTile(q, r);
            if (ground != null)
                for (var g = 0; g < ground.Length; g++)
                {
                    if (ground[g] <= 0 || !GroundKindShown(g)) continue;
                    var text = $"{ground[g]}";
                    var width = 18f + text.Length * 8f + 8f;
                    var colour = GroundColour(g);
                    Paint(new Rect(x, row.y, width - 4f, row.height), Fade(Gold, 0.55f), 5);
                    Paint(new Rect(x + 1f, row.y + 1f, width - 6f, row.height - 2f), Fade(Canvas, 0.9f), 4);
                    DrawMapIcon(new Rect(x + 2f, row.y + (row.height - 14f) / 2f, 14f, 14f), groundPack.keys[g], colour);
                    Text(new Rect(x + 18f, row.y, width - 20f, row.height), text, 11, Paper, true);
                    x += width;
                    shown++;
                }
            if (counts == null || facilityPack?.keys == null) return;
            var order = new List<int>();
            for (var i = 0; i < counts.Length; i++) if (counts[i] > 0 && FacilityKeyShown(i)) order.Add(i);
            order.Sort((a, b) => counts[b].CompareTo(counts[a]));
            if (order.Count == 0 && shown == 0)
            {
                Text(row, "고른 갈래에 든 시설 없음 · 자료 창에서 갈래를 켜세요", 10, Soft);
                return;
            }
            foreach (var index in order)
            {
                var key = facilityPack.keys[index];
                var text = $"{counts[index]}";
                var width = 18f + text.Length * 8f + 8f;
                if (shown >= top + (ground == null ? 0 : 2) || x + width > row.xMax - 44f) break;
                var colour = FacilityColour(key);
                Paint(new Rect(x, row.y, width - 4f, row.height), Fade(colour, 0.14f), 5);
                DrawMapIcon(new Rect(x + 2f, row.y + (row.height - 14f) / 2f, 14f, 14f), FacilityIconKey(key), colour);
                Text(new Rect(x + 18f, row.y, width - 20f, row.height), text, 11, Paper, true);
                x += width;
                shown++;
            }
            if (order.Count > shown) Text(new Rect(x, row.y, row.xMax - x, row.height), $"외 {order.Count - shown}갈래", 10, Soft);
        }

        // ── 위치 확인 표식 (Codex ground-samples · 사가정역 둘레 3겹 37칸) ─────────────────────────────
        // Resources/hongdal-data-ground-marks.json — map-icons-20261123-r1/bake_ground_marks.py가 굽는다.
        // 횡단보도 후보(서울시 2026-08-24, 도색 외곽 · 방향 미확인) 157곳 · 따릉이 대여소(2026-06) 15곳. 번호와 갈래만, 이름 없음.
        // 자료 창의 시설 갈래를 따른다: 횡단보도 = 「보행등 · 횡단보도」(0), 따릉이 = 「자전거 · 따릉이」(4).

        [Serializable] private sealed class GroundPack
        {
            public string title; public string scope; public string note; public string[] keys; public string[] labels; public string[] asOf;
            public float[] x; public float[] y; public int[] k; public int[] q; public int[] r; public string[] id;
        }

        private GroundPack groundPack;
        private bool groundTried;
        private Dictionary<long, int[]> groundByTile;

        private GroundPack EnsureGroundMarks()
        {
            if (groundTried) return groundPack;
            groundTried = true;
            groundPack = LoadPack<GroundPack>("hongdal-data-ground-marks");
            Debug.Log($"Hongdal data: 위치 확인 표식 {groundPack?.k?.Length ?? 0}곳");
            return groundPack;
        }

        private static int GroundGroup(string key) => key == "bike" ? 4 : 0;

        private bool GroundKindShown(int kind) =>
            groundPack?.keys != null && kind >= 0 && kind < groundPack.keys.Length && (facilityGroupMask & (1 << GroundGroup(groundPack.keys[kind]))) != 0;

        private Color GroundColour(int kind) => groundPack != null && groundPack.keys[kind] == "bike" ? Ink(0x7CDB72) : Ink(0xF4F7F8);

        private int[] GroundCountsOfTile(int q, int r)
        {
            var pack = EnsureGroundMarks();
            if (pack?.k == null) return null;
            if (groundByTile == null)
            {
                groundByTile = new Dictionary<long, int[]>();
                for (var i = 0; i < pack.k.Length; i++)
                {
                    var id = ((long)pack.q[i] << 32) ^ (uint)pack.r[i];
                    if (!groundByTile.TryGetValue(id, out var counts)) groundByTile[id] = counts = new int[pack.keys.Length];
                    counts[pack.k[i]]++;
                }
            }
            return groundByTile.TryGetValue(((long)q << 32) ^ (uint)r, out var found) ? found : null;
        }

        // 자료 창 갈래 개수에 위치 확인 표식을 더한다.
        private void AddGroundTotals(int[] totals)
        {
            var pack = EnsureGroundMarks();
            if (pack?.k == null || totals == null) return;
            foreach (var kind in pack.k)
            {
                var group = GroundGroup(pack.keys[kind]);
                if (group < totals.Length) totals[group]++;
            }
        }

        // 구 지도(타일 배율): 시설 점과 같은 배지 규칙. DrawGuFacilities 안에서 부른다(BeginFacilityMarks와 EndFacilityMarks 사이).
        private void DrawGroundMarks(float alpha, float radius)
        {
            var pack = EnsureGroundMarks();
            if (pack?.k == null) return;
            for (var i = 0; i < pack.k.Length; i++)
            {
                if (!GroundKindShown(pack.k[i])) continue;
                var at = GuPoint(pack.x[i], pack.y[i]);
                if (at.x < -6f || at.y < -6f || at.x > Field.width + 6f || at.y > Field.height + 6f) continue;
                DrawIconMark(at, pack.keys[pack.k[i]], radius * 0.85f, alpha, GroundColour(pack.k[i]));
            }
        }

        // 동 디오라마: 땅 위 작은 배지(가까울 때만). 이름표는 갈래 · 번호 · 기준일. 범례 개수는 dongPropSeen에 더한다.
        private void DrawDongGroundMarks(Rect view, Vector2 mouse, bool mouseFree, int[] seen,
            ref float hoverBest, ref string hoverText, ref string hoverSub, ref string hoverIcon)
        {
            var pack = EnsureGroundMarks();
            if (pack?.k == null) return;
            for (var i = 0; i < pack.k.Length; i++)
            {
                var kind = pack.k[i];
                if (!GroundKindShown(kind)) continue;
                var foot3 = GuWorld(pack.x[i], pack.y[i], 0f);
                if (!WorldShown(foot3)) continue;
                if (!WorldScreen(foot3, out var foot) || !view.Contains(foot)) continue;
                if (!WorldScreen(foot3 + Vector3.up * PropExaggerate, out var metreUp)) continue;
                var ppm = (metreUp - foot).magnitude;
                if (ppm < 0.9f) continue;
                var fade = Mathf.InverseLerp(0.9f, 1.8f, ppm);
                var badge = Mathf.Clamp(ppm * 2.6f, 6f, 11f);
                var colour = GroundColour(kind);
                var at = foot + (metreUp - foot) * 0.6f;
                Disc(at, badge + 1.3f, Fade(Gold, 0.85f * fade));
                Disc(at, badge, Fade(Canvas, 0.92f * fade));
                var side = badge * 1.45f;
                DrawMapIcon(new Rect(at.x - side / 2f, at.y - side / 2f, side, side), pack.keys[kind], Fade(colour, fade));
                var group = GroundGroup(pack.keys[kind]);
                if (seen != null && group < seen.Length) seen[group]++;
                if (!mouseFree) continue;
                var distance = Vector2.Distance(mouse, at);
                if (distance >= hoverBest || distance > badge + 6f) continue;
                hoverBest = distance;
                hoverText = $"{pack.labels[kind]} · {pack.id[i]}";
                hoverSub = $"공표 위치 대표점 · 서울시 {pack.asOf[kind]} · " + (pack.keys[kind] == "bike" ? "대여소 자리만" : "도색 외곽 · 방향 미확인");
                hoverIcon = pack.keys[kind];
            }
        }

        // 타일 창 머리 줄 아이콘(안쪽 여백에 작게). Codex 주제 아이콘 대응(pack-icons.json · district_metric_symbols)을 따른다.
        private void DrawCardHeaderIcon(float x, float y, string key) =>
            DrawMapIcon(new Rect(x - 14f, y + 1f, 12f, 12f), key, Fade(Gold, 0.85f));
    }
}
