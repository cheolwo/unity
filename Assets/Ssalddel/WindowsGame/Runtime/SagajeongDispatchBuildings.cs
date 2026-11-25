using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2A-2 (2026-11-12): 지도의 건물을 눌러 주소를 본다.
    // 굽기 스크립트가 메시와 같은 자료로 낸 건물 색인(Resources/hongdal-buildings.json)을 읽는다.
    // 물리 엔진 없이 고른다: 누른 지점의 시선이 건물 높이에서 땅까지 내려오는 동안 지면에 짧은 선분을 그리고,
    // 그 선분이 건물 외곽과 처음 만나는 높이가 가장 높은 건물(카메라에 가장 가까운 건물)을 고른다.
    // 실행 중에는 인터넷을 쓰지 않는다. 주소는 팩을 구울 때 들어간다.
    public sealed partial class SagajeongDispatchView
    {
        private const string BuildingIndexResource = "hongdal-buildings";
        private const float BuildingCell = 32f;           // 후보를 좁히는 격자 칸 (m)

        private BuildingIndex buildingIndex;
        private bool buildingIndexTried;
        private BuildingShape[] buildingShapes = Array.Empty<BuildingShape>();
        private readonly Dictionary<Vector2Int, List<int>> buildingGrid = new();
        private float buildingMaxHeight;
        private int? selectedBuilding;

        [Serializable]
        private sealed class BuildingIndex
        {
            public string schema;
            public string source;
            public string sourceDate;
            public string licence;
            public BuildingOrigin origin;
            public double metresPerDegreeLongitude;
            public double metresPerDegreeLatitude;
            public BuildingRecord[] buildings;
        }

        [Serializable]
        private sealed class BuildingOrigin
        {
            public double latitude;
            public double longitude;
        }

        [Serializable]
        private sealed class BuildingRecord
        {
            public string id;
            public bool fill;
            public string kind;
            public float height;
            public string heightSource;
            public float[] xz;
            public string road;
            public string number;
            public string postcode;
            public string dong;
            public string district;
            public string city;
            public string name;
            public string region;        // 행정동 모듈 건물만: region:kr:hjd:…
            public int floors;           // 지상 층수 (0 = 자료 없음)
            public float area;           // 건축면적 ㎡
            public string sourceId;      // 건물통합식별번호(2026-11-09 판 색인만). 판이 바뀌어도 같은 건물을 잇는 열쇠
            public string door;          // "juso"면 문이 공식 건물 출입구(도로명주소 2026-10). 비어 있으면 가상 출입구 (길 그래프의 문과 같은 건물 순서)
        }

        // 색인 한 건을 고르기 좋게 바꾼 것. 외곽은 자료 원점 기준 미터(x 동쪽, z 북쪽), 위경도도 미리 둔다.
        private sealed class BuildingShape
        {
            public BuildingRecord Record;
            public Vector2[] Ring;
            public double[] Latitudes;
            public double[] Longitudes;
            public Vector2 Min, Max;
        }

        private bool BuildingCardCovers(Vector2 point) => selectedBuilding != null && actorFocus == null &&
            BuildingCardRect.Contains(point);

        // 역할이 있는 건물(내가 운영하는 곳)은 카드가 길다: 주소 아래에 그곳의 일과 행동이 붙는다.
        private Rect BuildingCardRect => expansionView ? new Rect(986, 106, 272, SelectedRole() == null ? 236 : 486)
            : new Rect(970, 209, 250, 236);

        private void LoadBuildingIndex()
        {
            if (buildingIndexTried) return;
            buildingIndexTried = true;
            // r54: 행정동 모듈을 쓰면 모듈 건물(국토부 GIS) 색인을, 아니면 사가정 OSM 색인을 읽는다. 메시와 같은 자료여야 고를 수 있다.
            buildingIndex = ReadMapResource<BuildingIndex>(DongModulesActive ? dongManifest.buildingIndexResource : BuildingIndexResource);
            if (buildingIndex?.buildings == null || buildingIndex.origin == null || buildingIndex.metresPerDegreeLongitude <= 0)
            {
                buildingIndex = null;
                Debug.LogWarning("Hongdal buildings: index not found");
                return;
            }

            var shapes = new List<BuildingShape>(buildingIndex.buildings.Length);
            foreach (var record in buildingIndex.buildings)
            {
                if (record.xz == null || record.xz.Length < 6) continue;
                var count = record.xz.Length / 2;
                var shape = new BuildingShape
                {
                    Record = record,
                    Ring = new Vector2[count],
                    Latitudes = new double[count],
                    Longitudes = new double[count],
                    Min = new Vector2(float.MaxValue, float.MaxValue),
                    Max = new Vector2(float.MinValue, float.MinValue),
                };
                for (var i = 0; i < count; i++)
                {
                    var point = new Vector2(record.xz[i * 2], record.xz[i * 2 + 1]);
                    shape.Ring[i] = point;
                    shape.Longitudes[i] = buildingIndex.origin.longitude + point.x / buildingIndex.metresPerDegreeLongitude;
                    shape.Latitudes[i] = buildingIndex.origin.latitude + point.y / buildingIndex.metresPerDegreeLatitude;
                    shape.Min = Vector2.Min(shape.Min, point);
                    shape.Max = Vector2.Max(shape.Max, point);
                }
                buildingMaxHeight = Mathf.Max(buildingMaxHeight, record.height);
                var index = shapes.Count;
                shapes.Add(shape);
                foreach (var cell in Cells(shape.Min, shape.Max))
                {
                    if (!buildingGrid.TryGetValue(cell, out var list)) buildingGrid[cell] = list = new List<int>();
                    list.Add(index);
                }
            }
            buildingShapes = shapes.ToArray();
            Debug.Log($"Hongdal buildings: {buildingShapes.Length} shapes, " +
                $"{buildingShapes.Count(shape => !shape.Record.fill)} real, max height {buildingMaxHeight:0.0} m");
        }

        private static IEnumerable<Vector2Int> Cells(Vector2 min, Vector2 max)
        {
            var x0 = Mathf.FloorToInt(min.x / BuildingCell);
            var x1 = Mathf.FloorToInt(max.x / BuildingCell);
            var z0 = Mathf.FloorToInt(min.y / BuildingCell);
            var z1 = Mathf.FloorToInt(max.y / BuildingCell);
            for (var x = x0; x <= x1; x++)
                for (var z = z0; z <= z1; z++)
                    yield return new Vector2Int(x, z);
        }

        // 위경도 → 색인 원점 기준 미터
        private Vector2 BuildingLocal(double latitude, double longitude) => new(
            (float)((longitude - buildingIndex.origin.longitude) * buildingIndex.metresPerDegreeLongitude),
            (float)((latitude - buildingIndex.origin.latitude) * buildingIndex.metresPerDegreeLatitude));

        // 장면 세계 좌표(x 동쪽, z 북쪽, 지도 중심 기준 m) → 색인 원점 기준 미터
        private Vector2 BuildingLocal(Vector3 world) => BuildingLocal(
            centerLatitude + world.z / 111200, centerLongitude + world.x / longitudeScale);

        // 누른 지점(Field 그룹 안 좌표)에서 건물을 고른다.
        private void PickBuilding(Vector2 point)
        {
            mapClick = false;
            LoadBuildingIndex();
            if (buildingIndex == null || mapLevel != MapLevel.Dong) return;

            var picked = DioramaVisible ? PickBuildingInScene(point) : PickBuildingOnBoard(point);
            if (picked == null)
            {
                if (selectedBuilding != null) Debug.Log("Hongdal building: cleared");
                selectedBuilding = null;
                return;
            }
            selectedBuilding = picked;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            var record = buildingShapes[picked.Value].Record;
            Debug.Log($"Hongdal building: {record.id} {record.road} {record.number}");
        }

        private int? PickBuildingOnBoard(Vector2 point)
        {
            // PlotOnBoard의 역: 지도 좌표 → 위경도
            var plot = (point - mapOffset) / mapZoom + Field.position;
            var longitude = centerLongitude + (plot.x - Field.center.x) * metresPerPixel / longitudeScale;
            var latitude = centerLatitude - (plot.y - Field.center.y) * metresPerPixel / 111200;
            var local = BuildingLocal(latitude, longitude);
            if (!buildingGrid.TryGetValue(Cell(local), out var candidates)) return null;
            foreach (var index in candidates)
            {
                var shape = buildingShapes[index];
                if (shape.Record.fill) continue;           // 평면 지도에는 예시 건물이 그려지지 않는다
                if (Inside(shape.Ring, local)) return index;
            }
            return null;
        }

        private int? PickBuildingInScene(Vector2 point)
        {
            var left = (Screen.width - Width * dioramaGuiScale) * 0.5f;
            var top = (Screen.height - Height * dioramaGuiScale) * 0.5f;
            var screen = new Vector3((point.x + Field.x) * dioramaGuiScale + left,
                Screen.height - ((point.y + Field.y) * dioramaGuiScale + top), 0);
            var ray = dioramaCamera.ScreenPointToRay(screen);
            if (ray.direction.y >= -0.01f) return null;

            var high = buildingMaxHeight + 0.5f;
            var groundWorld = ray.GetPoint((0 - ray.origin.y) / ray.direction.y);
            var roofWorld = ray.GetPoint((high - ray.origin.y) / ray.direction.y);
            int? best = null;
            var bestHeight = float.MinValue;
            // 행정동 모듈을 띄워 보면 동마다 자리가 다르다. 보이는 동마다 그 동이 옮겨진 만큼 되돌려 찾는다(숨긴 동은 고르지 않는다).
            foreach (var (shift, region) in BuildingPickLayers())
            {
                var offset = new Vector3(shift.x, 0, shift.y);
                var ground = BuildingLocal(groundWorld - offset);
                var roof = BuildingLocal(roofWorld - offset);
                var checkedShapes = new HashSet<int>();
                foreach (var cell in Cells(Vector2.Min(ground, roof), Vector2.Max(ground, roof)))
                {
                    if (!buildingGrid.TryGetValue(cell, out var candidates)) continue;
                    foreach (var index in candidates)
                    {
                        if (!checkedShapes.Add(index)) continue;
                        var shape = buildingShapes[index];
                        if (shape.Record.fill && !dioramaFillOn) continue;
                        if (region != null && shape.Record.region != region) continue;
                        if (StationView && !StationShowsBuilding(shape)) continue;      // 역 판 밖의 건물은 고르지 않는다
                        var h = shape.Record.height;
                        // 이 건물 높이에서 땅까지 시선이 지나가는 지면 위 선분
                        var from = Vector2.Lerp(ground, roof, h / high);
                        var entry = EntryHeight(shape.Ring, from, ground, h);
                        if (entry > bestHeight)
                        {
                            bestHeight = entry;
                            best = index;
                        }
                    }
                }
            }
            return best;
        }

        // 선분(from: 높이 h, to: 높이 0)이 외곽 안으로 처음 들어가는 높이. 만나지 않으면 float.MinValue.
        private static float EntryHeight(Vector2[] ring, Vector2 from, Vector2 to, float h)
        {
            if (Inside(ring, from)) return h;          // 옥상을 눌렀다
            var first = float.MaxValue;
            for (var i = 0; i < ring.Length; i++)
            {
                if (SegmentCross(from, to, ring[i], ring[(i + 1) % ring.Length], out var s) && s < first) first = s;
            }
            return first <= 1f ? h * (1f - first) : float.MinValue;
        }

        private static bool SegmentCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float s)
        {
            s = 0;
            var r = b - a;
            var q = d - c;
            var denominator = r.x * q.y - r.y * q.x;
            if (Mathf.Abs(denominator) < 1e-6f) return false;
            var ac = c - a;
            s = (ac.x * q.y - ac.y * q.x) / denominator;
            var t = (ac.x * r.y - ac.y * r.x) / denominator;
            return s >= 0 && s <= 1 && t >= 0 && t <= 1;
        }

        private static bool Inside(Vector2[] ring, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                var a = ring[i];
                var b = ring[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static Vector2Int Cell(Vector2 local) =>
            new(Mathf.FloorToInt(local.x / BuildingCell), Mathf.FloorToInt(local.y / BuildingCell));

        // 고른 건물의 윤곽을 그린다. 2.5D에서는 옥상 테두리와 모서리 기둥, 평면에서는 바닥 테두리.
        private void DrawBuildingHighlight()
        {
            if (selectedBuilding == null || actorFocus != null || mapLevel != MapLevel.Dong) return;
            if (Event.current.type != EventType.Repaint) return;
            DrawBuildingOutline(buildingShapes[selectedBuilding.Value], Gold, 3f, 1.5f);
        }

        private void DrawBuildingCard()
        {
            if (selectedBuilding == null) return;
            if (actorFocus != null || mapLevel != MapLevel.Dong)
            {
                selectedBuilding = null;     // 기사·매장을 고르거나 넓은 지도로 가면 건물 선택을 거둔다
                return;
            }
            var record = buildingShapes[selectedBuilding.Value].Record;
            var role = expansionView ? SelectedRole() : null;
            var card = BuildingCardRect;
            var x = card.x + 17;
            var y = card.y;
            Paint(card, Rim, 11);
            Paint(new Rect(card.x + 1, y + 1, card.width - 2, card.height - 2), Canvas, 10);
            var module = !string.IsNullOrEmpty(record.region);
            Text(new Rect(x, y + 12, 180, 20), record.fill ? "예시 건물" : "건물 · " + KindLabel(record.kind), 11, Gold, true);
            var close = new Rect(card.xMax - 35, y + 9, 23, 24);
            Text(close, "×", 19, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                selectedBuilding = null;
                return;
            }

            if (module)
            {
                // 행정동 모듈 건물: 주소는 없고 층수·면적·용도가 있다. 높이를 어디서 얻었는지 밝힌다.
                var size = record.floors > 0 ? $"지상 {record.floors}층" : "층수 자료 없음";
                if (record.area > 0) size += $" · {record.area:N0}㎡";
                Text(new Rect(x, y + 38, card.width - 34, 30), size, 20, Paper, true);
                var moduleAddress = !string.IsNullOrEmpty(record.road) && !string.IsNullOrEmpty(record.number);      // 공식 판(도로명주소 2026-10)부터 주소가 붙는다
                Text(new Rect(x, y + 70, card.width - 34, 22), moduleAddress
                    ? record.road + " " + record.number + (string.IsNullOrEmpty(record.name) ? string.Empty : " · " + record.name)
                    : "도로명주소 자료 없음", 13, moduleAddress ? Mint : Soft, true);
                var area = string.Join(" ", new[] { record.city, record.district, record.dong }.Where(part => !string.IsNullOrEmpty(part)));
                Text(new Rect(x, y + 96, card.width - 34, 20), string.IsNullOrEmpty(area) ? "행정동 자료 없음" : area + " (행정동)", 12, Soft);
                Text(new Rect(x, y + 117, card.width - 34, 20), 행정동모듈Policy.HeightNote(record.heightSource), 12, Soft);
                if (role == null)
                {
                    Paint(new Rect(x, y + 146, card.width - 34, 44), Tile, 7);
                    var partnerSite = IsPartnerSite(selectedBuilding.Value);
                    Text(new Rect(x + 9, y + 149, card.width - 52, 38), partnerSite
                        ? "제휴 음식점의 가상 자리로 밝혀 둔 건물입니다. 실제 점포와 무관합니다. 내부 검토용."
                        : (record.door == "juso" ? "문: 공식 건물 출입구(도로명주소). 점포의 픽업문이 아닙니다. " : "문: 가상 출입구(가까운 길에 붙인 자리). ") +
                            "실제 쓰임 · 점포와 무관한 내부 검토용 자료입니다.", 11, partnerSite ? Mint : Soft, wrap: true);
                }
            }
            else if (record.fill)
            {
                Text(new Rect(x, y + 40, card.width - 34, 28), "주소 자료 없음", 19, Paper, true);
                Text(new Rect(x, y + 74, card.width - 34, role == null ? 80 : 62),
                    "지도 자료에 건물 외곽이 없는 블록을 채운 예시 건물입니다. 실제 건물이 아닙니다. 공공 건물 자료를 붙이면 실제 건물로 바뀝니다.",
                    12, Soft, wrap: true);
            }
            else
            {
                var hasAddress = !string.IsNullOrEmpty(record.road) && !string.IsNullOrEmpty(record.number);
                Text(new Rect(x, y + 38, card.width - 34, 30), hasAddress ? record.road + " " + record.number : "도로명주소 없음",
                    hasAddress ? 20 : 17, Paper, true);
                Text(new Rect(x, y + 70, card.width - 34, 22), string.IsNullOrEmpty(record.name) ? "건물 이름 없음" : record.name,
                    13, string.IsNullOrEmpty(record.name) ? Soft : Mint, true);
                var area = string.Join(" ", new[] { record.city, record.district, record.dong }.Where(part => !string.IsNullOrEmpty(part)));
                Text(new Rect(x, y + 96, card.width - 34, 20), string.IsNullOrEmpty(area) ? "행정구역 자료 없음" : area, 12, Soft);
                Text(new Rect(x, y + 117, card.width - 34, 20),
                    string.IsNullOrEmpty(record.postcode) ? "우편번호 자료 없음" : "우편번호 " + record.postcode, 12, Soft);
                if (role == null)
                {
                    Paint(new Rect(x, y + 146, card.width - 34, 44), Tile, 7);
                    Text(new Rect(x + 9, y + 149, card.width - 52, 38),
                        "용도·층수·사용승인 연도는 공공 건물 자료를 붙인 뒤 보입니다.", 11, Soft, wrap: true);
                }
            }
            if (role != null) DrawRoleSection(role, x, y + 144, card.width - 34);
            Text(new Rect(x, y + card.height - 38, card.width - 34, 30), module
                    ? $"출처 국토부 GIS건물(AL_D010) · {buildingIndex?.sourceDate ?? "기준일 미상"} · 내부 검토용"
                    : $"출처 OpenStreetMap · {buildingIndex?.sourceDate ?? "기준일 미상"} 기준 · 실제와 다를 수 있습니다", 10, Soft, wrap: true);
        }

        private static string KindLabel(string kind) => kind != null && kind.StartsWith("al-d010:", StringComparison.Ordinal)
            ? 행정동모듈Policy.UseLabel(kind)
            : kind switch
        {
            "house" => "단독주택",
            "detached" => "단독주택",
            "apartments" => "아파트",
            "residential" => "주거",
            "commercial" => "상업",
            "retail" => "상업",
            "school" => "학교",
            "civic" => "공공",
            "church" => "종교",
            "roof" => "지붕 구조물",
            _ => "용도 미상",
        };
    }
}
