using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 요약 지도 (품2, 2026-11-14. 사용자: 「우측 하단에 요약 지도 느낌, 보다 상위 지도에서의 내 현재 위치를 나타내는 쪽으로」 → 제안서의 권함대로).
    //  - 큰 지도가 동이나 건물을 볼 때, 오른쪽 아래에 한 단계 위의 지도(구)를 북쪽이 위인 채로 보인다.
    //    그 위에 지금 보는 곳(금색 점과 보는 방향), 그곳이 든 동(민트), 내 곳(색 점)을 찍는다. 옆에는 글로도 적는다.
    //  - 경계는 게임에 이미 있는 자료다: 준비된 행정동(행정동 모듈 목록의 boundary)과 구(구 공간 팩의 sigs). 새 자료가 없다.
    //    구 안에서 준비되지 않은 곳은 빗금이다(「자료 준비 전」). 위치를 모르는 곳은 찍지 않는다.
    //  - 금색 네모는 지금 화면에 보이는 땅이다(화면 네 모서리를 지금 시점으로 땅에 내린 것). 작은 지도에서 9칸보다 작으면 점만 둔다.
    //  - 오른쪽 칸에 카드(건물 · 기사 · 결산 · 순회)가 떠 있으면 자리를 내주고 숨는다. 「접기」를 누르면 한 줄이 된다.
    //  - 누르기 · 휠은 뒤의 큰 지도로 새지 않는다. 「내 매장으로」는 큰 지도를 옮길 뿐이다. 시계 · 주문 · 돈 · 운영권은 바뀌지 않는다.
    //  - 품7(같은 날): 머리의 「동 · 구」로 단계를 고른다. 「동」은 보는 동과 이웃 동을 크게 본다. 준비된 동의 한 곳을 누르면
    //    큰 지도가 그곳으로 간다(배율은 그대로). 준비 전인 곳 · 역 판 밖은 한 줄로 알리고 큰 지도는 그대로 둔다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect MiniMapPanel = new(986, 362, 272, 242);
        private static readonly Rect MiniMapLine = new(986, 582, 272, 22);
        private static readonly Rect MiniMapArea = new(994, 392, 162, 172);       // 구 지도가 서는 자리
        private static readonly Rect MiniInsetArea = new(1164, 494, 86, 70);      // 권역(서울 동북권) 안의 구
        private const int MiniTexel = 2;                                         // 그림은 논리 좌표의 두 배로 굽는다
        private const float MiniViewBoxFloor = 9f;                               // 보이는 땅의 네모가 이보다 작으면 그리지 않는다(칸)
        private static readonly Color MiniGround = Ink(0x0D1620);
        private static readonly Color MiniNeighbour = Ink(0x111B25);
        private static readonly Color MiniNeighbourEdge = Ink(0x2B4152);
        private static readonly Color MiniHatchBase = Ink(0x15222E);
        private static readonly Color MiniHatchLine = Ink(0x22384A);
        private static readonly Color MiniGuEdge = Ink(0x4F7286);
        private static readonly Color MiniDong = Ink(0x1D3440);
        private static readonly Color MiniDongEdge = Ink(0x3F6675);

        private sealed class MiniShape
        {
            public string Name;
            public string Region;          // 행정동이면 regionId
            public bool Focus;             // 구: 경계가 든 구
            public List<Vector2> Ring;     // 공통 원점 기준 m (동, 북)
        }

        private bool miniMapFolded;
        private bool miniDongLevel;                      // 머리의 「동 · 구」: 동을 고르면 보는 동과 이웃 동을 크게 본다
        private string miniNote;                         // 누른 곳에 대한 한 줄(잠깐 뜬다)
        private float miniNoteUntil;
        private Rect miniHomeBox;                        // 구의 범위(m). 「중랑구 남쪽」을 말할 때 쓴다
        private bool miniTried;
        private List<MiniShape> miniGus;                 // 경계가 든 구
        private List<MiniShape> miniDongs;               // 준비된 행정동
        private readonly List<(Vector2 At, Color Colour, string Title, string Place)> miniPlaces = new();
        private Texture2D miniBase;                      // 구 지도
        private Texture2D miniInset;                     // 권역 안의 구
        private Texture2D miniWedge;                     // 보는 방향(부채꼴)
        private string miniBaseFor;                      // 그 그림의 단계와 강조한 동 (「G:」 구 · 「D:」 동 + regionId)
        private Rect miniBox;                            // 구 지도의 범위(m): x 동, y 북
        private float miniScale;                         // 논리 좌표 한 칸의 길이(1/m)
        private Vector2 miniOrigin;                      // 범위의 남서 모서리가 놓이는 자리(논리 좌표, MiniMapArea 안)
        private Rect miniInsetBox;
        private float miniInsetScale;
        private Vector2 miniInsetOrigin;
        private string miniGuName = string.Empty;

        private bool MiniMapReady => TidyScreen && expansionView && !mapOnly && !showDesk && mapLevel == MapLevel.Dong &&
            DioramaVisible && DongModulesLoaded && dongManifest != null;
        // 오른쪽 칸에 다른 것이 떠 있으면 자리를 내준다.
        private bool MiniMapCrowded => selectedBuilding != null || actorFocus != null || dongPanelOpen || zonePanelOpen ||
            DailyReportVisible || CycleCardVisible || ExpansionPanelVisible || !cycleCardFolded;
        private bool MiniMapShown => MiniMapReady && !MiniMapCrowded && LoadMiniMap();
        private bool MiniMapOpen => MiniMapShown && !miniMapFolded;
        private Rect MiniMapRect => miniMapFolded ? MiniMapLine : MiniMapPanel;
        private bool MiniMapCovers(Vector2 point) => MiniMapShown && MiniMapRect.Contains(point);

        // ── 자료 ──
        private bool LoadMiniMap()
        {
            if (miniTried) return miniGus != null;
            miniTried = true;
            var space = ReadMapResource<GuSpace>(GuSpaceResource);
            if (space?.sigs == null || dongManifest?.modules == null) return false;
            var gus = new List<MiniShape>();
            foreach (var sig in space.sigs)
            {
                if (sig.rings == null) continue;
                foreach (var ring in sig.rings)
                {
                    if (ring?.p == null || ring.p.Length < 6) continue;
                    var points = new List<Vector2>(ring.p.Length / 2);
                    for (var i = 0; i + 1 < ring.p.Length; i += 2)
                    {
                        // 구 공간 팩의 좌표(남서 모서리 기준 m) → 위경도 → 행정동 모듈의 공통 원점 기준 m (SagajeongDispatchGu.cs와 같은 값)
                        var longitude = space.west + ring.p[i] / 88224.0;
                        var latitude = space.south + ring.p[i + 1] / 111320.0;
                        var (east, north) = DongLocal(latitude, longitude);
                        points.Add(new Vector2((float)east, (float)north));
                    }
                    gus.Add(new MiniShape { Name = sig.name, Focus = sig.focus, Ring = points });
                }
            }
            var dongs = new List<MiniShape>();
            foreach (var module in dongModules)
            {
                var boundary = module.Entry.boundary;
                if (!module.Loaded || boundary == null || boundary.Length < 6) continue;
                var points = new List<Vector2>(boundary.Length / 2);
                for (var i = 0; i + 1 < boundary.Length; i += 2)
                {
                    var (east, north) = DongLocal(boundary[i], boundary[i + 1]);
                    points.Add(new Vector2((float)east, (float)north));
                }
                dongs.Add(new MiniShape { Name = module.Entry.name, Region = module.Entry.regionId, Ring = points });
            }
            miniGuName = dongModules.FirstOrDefault(module => module.Loaded)?.Entry.district ?? string.Empty;
            var home = gus.Where(shape => shape.Name == miniGuName).ToList();
            if (home.Count == 0 || dongs.Count == 0)
            {
                Debug.LogWarning("Hongdal mini map: 구 경계나 준비된 동이 없어 요약 지도를 띄우지 않습니다.");
                return false;
            }
            miniGus = gus;
            miniDongs = dongs;
            (miniBox, miniScale, miniOrigin) = MiniFit(home.Select(shape => shape.Ring), MiniMapArea.size, 9f);
            miniHomeBox = miniBox;
            (miniInsetBox, miniInsetScale, miniInsetOrigin) = MiniFit(gus.Where(shape => shape.Focus).Select(shape => shape.Ring), MiniInsetArea.size, 6f);
            Debug.Log($"Hongdal mini map: {miniGuName}, 구 경계 {gus.Count}, 준비된 동 {dongs.Count}, 한 칸 {1f / miniScale:0} m");
            return true;
        }

        // 여러 경계가 다 들어오는 범위와 배율, 그리고 남서 모서리가 놓일 자리(그 칸의 왼쪽 위에서 잰 논리 좌표).
        private static (Rect Box, float Scale, Vector2 Origin) MiniFit(IEnumerable<List<Vector2>> rings, Vector2 size, float pad)
        {
            float west = float.MaxValue, south = float.MaxValue, east = float.MinValue, north = float.MinValue;
            foreach (var ring in rings)
                foreach (var point in ring)
                {
                    west = Mathf.Min(west, point.x);
                    east = Mathf.Max(east, point.x);
                    south = Mathf.Min(south, point.y);
                    north = Mathf.Max(north, point.y);
                }
            var scale = Mathf.Min((size.x - 2f * pad) / Mathf.Max(1f, east - west), (size.y - 2f * pad) / Mathf.Max(1f, north - south));
            var origin = new Vector2((size.x - (east - west) * scale) * 0.5f, (size.y - (north - south) * scale) * 0.5f);
            return (Rect.MinMaxRect(west, south, east, north), scale, origin);
        }

        // 공통 원점 기준 m → 구 지도 칸 안의 논리 좌표(왼쪽 위에서 잼)
        private Vector2 MiniPoint(Vector2 metres) => new(miniOrigin.x + (metres.x - miniBox.xMin) * miniScale,
            MiniMapArea.height - miniOrigin.y - (metres.y - miniBox.yMin) * miniScale);

        private Vector2 MiniInsetPoint(Vector2 metres) => new(miniInsetOrigin.x + (metres.x - miniInsetBox.xMin) * miniInsetScale,
            MiniInsetArea.height - miniInsetOrigin.y - (metres.y - miniInsetBox.yMin) * miniInsetScale);

        // 내 곳(건물 역할표에서 내 것)의 자리. 건물 색인이 읽힌 뒤 한 번 모은다.
        private void SyncMiniPlaces()
        {
            if (miniPlaces.Count > 0 || roleBook?.roles == null || buildingIndex == null) return;
            foreach (var role in roleBook.roles)
            {
                if (role.owner != "mine" || !buildingByPlace.TryGetValue(role.placeId, out var building) ||
                    building < 0 || building >= buildingShapes.Length) continue;
                var mid = BuildingMid(building);
                var latitude = buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude;
                var longitude = buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude;
                var (east, north) = DongLocal(latitude, longitude);
                var colour = role.business == "franchise" ? Lilac : role.business == "warehouse" ? Sky : StoreHalo;
                miniPlaces.Add((new Vector2((float)east, (float)north), colour, role.title, role.placeId));
            }
        }

        // ── 지금 보는 곳 ──
        // 지도 영역 안의 화면 점 → 그 땅의 자리(공통 원점 기준 m). 평행 보기의 계산이다(원근에서는 가운데가 맞는다).
        private Vector2 MiniGroundAt(Vector2 screenPoint)
        {
            var flat = FlatPoint(screenPoint);
            var board = (flat - mapOffset) / mapZoom + Field.position - Field.center;
            var longitude = centerLongitude + board.x * metresPerPixel / longitudeScale;
            var latitude = centerLatitude - board.y * metresPerPixel / 111200;
            var (east, north) = DongLocal(latitude, longitude);
            return new Vector2((float)east, (float)north);
        }

        private Vector2 MiniViewCentre() => MiniGroundAt(DongFitArea.center - Field.position);

        private string MiniSide(Vector2 metres)
        {
            var home = miniHomeBox;
            var dx = (metres.x - home.center.x) / Mathf.Max(1f, home.width * 0.5f);
            var dy = (metres.y - home.center.y) / Mathf.Max(1f, home.height * 0.5f);
            var ns = dy > 0.25f ? "북" : dy < -0.25f ? "남" : string.Empty;
            var ew = dx > 0.25f ? "동" : dx < -0.25f ? "서" : string.Empty;
            return ns.Length + ew.Length == 0 ? "가운데" : ns + ew + "쪽";
        }

        private static string MiniWidthLabel(float metres) => metres >= 950f
            ? "가로 약 " + (metres / 1000f).ToString("0.0") + " km"
            : "가로 약 " + (Mathf.Round(metres / 10f) * 10f).ToString("0") + " m";

        // 큰 지도를 그 지점으로 옮긴다. 배율은 그대로다. 보기만 바뀐다.
        private void MoveViewTo(double latitude, double longitude)
        {
            if (metresPerPixel <= 0 || mapZoom <= 0) return;
            var metres = (float)metresPerPixel / mapZoom;
            var east = (float)((longitude - centerLongitude) * longitudeScale);
            var north = (float)((latitude - centerLatitude) * 111200);
            var centre = Field.size * 0.5f;
            mapOffset = new Vector2(centre.x * (1f - mapZoom) - east / metres, centre.y * (1f - mapZoom) + north / metres);
            mapOffset += FlatDeltaFor(dioramaOn, DongFitArea.center - Field.position - centre);
            mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
            localTextureDirty = true;
        }

        // 「내 매장으로」: 매장 창이 떠 있으면 그 매장, 아니면 내 곳의 첫 번째.
        private string MiniHomePlace()
        {
            SyncMiniPlaces();
            if (miniPlaces.Count == 0) return null;
            if (StoreDeskVisible && miniPlaces.Any(item => item.Place == storeDeskPlace)) return storeDeskPlace;
            return miniPlaces[0].Place;
        }

        private bool MiniMapHome()
        {
            var place = MiniHomePlace();
            if (place == null || !buildingByPlace.TryGetValue(place, out var building) || building < 0 || building >= buildingShapes.Length) return false;
            var mid = BuildingMid(building);
            MoveViewTo(buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude,
                buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude);
            return true;
        }

        // ── 그림 굽기 ──
        private void BuildMiniBase(string focusRegion)
        {
            var width = Mathf.RoundToInt(MiniMapArea.width) * MiniTexel;
            var height = Mathf.RoundToInt(MiniMapArea.height) * MiniTexel;
            var pixels = new Color32[width * height];
            var ground = (Color32)MiniGround;
            for (var i = 0; i < pixels.Length; i++) pixels[i] = ground;
            // 단계: 「동」이면 보는 동을 여백을 두고 맞춘다(이웃 동이 함께 보인다). 보는 곳이 준비된 동 밖이면 구로 본다.
            var focus = miniDongs.FirstOrDefault(shape => shape.Region == focusRegion);
            var dongLevel = miniDongLevel && focus != null;
            if (dongLevel) (miniBox, miniScale, miniOrigin) = MiniFit(new[] { focus.Ring }, MiniMapArea.size, 28f);
            else (miniBox, miniScale, miniOrigin) = MiniFit(miniGus.Where(shape => shape.Name == miniGuName).Select(shape => shape.Ring), MiniMapArea.size, 9f);
            List<Vector2> On(List<Vector2> ring) => ring.Select(point => MiniPoint(point) * MiniTexel).ToList();
            var hatchBase = (Color32)MiniHatchBase;
            var hatchLine = (Color32)MiniHatchLine;
            var neighbour = (Color32)MiniNeighbour;
            foreach (var gu in miniGus.Where(shape => shape.Name != miniGuName))
            {
                var ring = On(gu.Ring);
                MiniFill(pixels, width, height, ring, (x, y) => neighbour);
                MiniStroke(pixels, width, height, ring, MiniNeighbourEdge, 0.8f);
            }
            foreach (var gu in miniGus.Where(shape => shape.Name == miniGuName))     // 준비 전인 곳은 빗금으로 남는다
                MiniFill(pixels, width, height, On(gu.Ring), (x, y) => (x + y) % 10 < 3 ? hatchLine : hatchBase);
            var lit = (Color32)Color.Lerp(MiniDong, Mint, 0.34f);
            foreach (var dong in miniDongs)
            {
                var ring = On(dong.Ring);
                var on = dong.Region == focusRegion;
                var fill = on ? lit : (Color32)MiniDong;
                MiniFill(pixels, width, height, ring, (x, y) => fill);
            }
            foreach (var dong in miniDongs.Where(shape => shape.Region != focusRegion))
                MiniStroke(pixels, width, height, On(dong.Ring), MiniDongEdge, 0.7f);
            foreach (var gu in miniGus.Where(shape => shape.Name == miniGuName))
                MiniStroke(pixels, width, height, On(gu.Ring), MiniGuEdge, 1.2f);
            foreach (var dong in miniDongs.Where(shape => shape.Region == focusRegion))
                MiniStroke(pixels, width, height, On(dong.Ring), Mint, 1.5f);
            if (miniBase == null)
                miniBase = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Hongdal mini map", wrapMode = TextureWrapMode.Clamp };
            miniBase.SetPixels32(pixels);
            miniBase.Apply(false, false);
            miniBaseFor = (dongLevel ? "D:" : "G:") + (focusRegion ?? string.Empty);
        }

        private void BuildMiniInset()
        {
            var width = Mathf.RoundToInt(MiniInsetArea.width) * MiniTexel;
            var height = Mathf.RoundToInt(MiniInsetArea.height) * MiniTexel;
            var pixels = new Color32[width * height];
            var ground = (Color32)MiniGround;
            for (var i = 0; i < pixels.Length; i++) pixels[i] = ground;
            var lit = (Color32)Color.Lerp(MiniDong, Mint, 0.3f);
            foreach (var gu in miniGus.Where(shape => shape.Focus))
            {
                var ring = gu.Ring.Select(point => MiniInsetPoint(point) * MiniTexel).ToList();
                var home = gu.Name == miniGuName;
                var fill = home ? lit : (Color32)MiniNeighbour;
                MiniFill(pixels, width, height, ring, (x, y) => fill);
                MiniStroke(pixels, width, height, ring, home ? Mint : MiniNeighbourEdge, home ? 1.3f : 0.8f);
            }
            miniInset = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Hongdal mini map inset", wrapMode = TextureWrapMode.Clamp };
            miniInset.SetPixels32(pixels);
            miniInset.Apply(false, false);
        }

        // 위쪽(-y)을 보는 부채꼴. 가운데에서 멀어질수록 옅어진다.
        private void BuildMiniWedge()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = size * 0.5f - (y + 0.5f);              // 위쪽이 +
                    var radius = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    var angle = Mathf.Abs(Mathf.Atan2(dx, dy));     // 위쪽에서 벌어진 각
                    var inside = Mathf.Clamp01((0.44f - angle) * 14f) * Mathf.Clamp01((1f - radius) * 10f);
                    var alpha = inside * Mathf.Lerp(0.62f, 0.12f, radius);
                    pixels[(size - 1 - y) * size + x] = new Color(1f, 0.83f, 0.58f, alpha);
                }
            miniWedge = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Hongdal mini map wedge", wrapMode = TextureWrapMode.Clamp };
            miniWedge.SetPixels32(pixels);
            miniWedge.Apply(false, false);
        }

        // 다각형 채우기(가로줄마다 짝수 · 홀수). 좌표는 그림 칸(왼쪽 위에서 잼).
        private static void MiniFill(Color32[] pixels, int width, int height, List<Vector2> ring, Func<int, int, Color32> ink)
        {
            if (ring.Count < 3) return;
            float top = float.MaxValue, bottom = float.MinValue;
            foreach (var point in ring)
            {
                top = Mathf.Min(top, point.y);
                bottom = Mathf.Max(bottom, point.y);
            }
            var first = Mathf.Max(0, Mathf.CeilToInt(top - 0.5f));
            var last = Mathf.Min(height - 1, Mathf.FloorToInt(bottom - 0.5f));
            var crossings = new List<float>();
            for (var y = first; y <= last; y++)
            {
                var line = y + 0.5f;
                crossings.Clear();
                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    var a = ring[i];
                    var b = ring[j];
                    if (a.y > line != b.y > line) crossings.Add(a.x + (line - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    var from = Mathf.Max(0, Mathf.CeilToInt(crossings[k] - 0.5f));
                    var to = Mathf.Min(width - 1, Mathf.FloorToInt(crossings[k + 1] - 0.5f));
                    var row = (height - 1 - y) * width;
                    for (var x = from; x <= to; x++) pixels[row + x] = ink(x, y);
                }
            }
        }

        // 닫힌 선. 선에서 떨어진 거리로 가장자리를 부드럽게 한다.
        private static void MiniStroke(Color32[] pixels, int width, int height, List<Vector2> ring, Color colour, float radius)
        {
            var ink = (Color32)colour;
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                var x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1f));
                var x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1f));
                var y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1f));
                var y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1f));
                var along = b - a;
                var length = along.sqrMagnitude;
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var t = length <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, along) / length);
                        var alpha = Mathf.Clamp01(radius + 0.5f - (point - (a + along * t)).magnitude);
                        if (alpha <= 0f) continue;
                        var at = (height - 1 - y) * width + x;
                        pixels[at] = Color32.Lerp(pixels[at], ink, alpha);
                    }
            }
        }

        // ── 그리기 (OnGUI) ──
        private void DrawMiniMap()
        {
            if (!MiniMapShown) return;
            SyncMiniPlaces();
            var centre = MiniViewCentre();
            var here = DongModuleAtViewCentre();
            var dongName = here?.Entry.name ?? "동 밖";
            if (miniMapFolded)
            {
                var line = MiniMapLine;
                Paint(line, Rim, 7);
                Paint(new Rect(line.x + 1, line.y + 1, line.width - 2, line.height - 2), Fade(Canvas, 0.96f), 6);
                Disc(new Vector2(line.x + 13, line.center.y), 4f, Gold);
                Text(new Rect(line.x + 24, line.y, line.width - 86, line.height), dongName + " · " + miniGuName, 11, Paper, true);
                if (ExpansionButton(new Rect(line.xMax - 56, line.y + 2, 54, line.height - 4), "펼치기", true, Tile)) miniMapFolded = false;
                return;
            }
            if (miniWork)      // 업무 요약 (r217 · M 키, SagajeongDispatchWorkSummary.cs)
            {
                DrawWorkSummary();
                return;
            }

            var focusRegion = here?.Entry.regionId ?? string.Empty;
            var wanted = (miniDongLevel && miniDongs.Any(shape => shape.Region == focusRegion) ? "D:" : "G:") + focusRegion;
            if (miniBase == null || miniBaseFor != wanted) BuildMiniBase(focusRegion);
            var dongLevel = miniBaseFor.StartsWith("D:", StringComparison.Ordinal);
            if (miniInset == null) BuildMiniInset();
            if (miniWedge == null) BuildMiniWedge();

            var panel = MiniMapPanel;
            Paint(panel, Ink(0x3B5669), 9);
            Paint(new Rect(panel.x + 1.2f, panel.y + 1.2f, panel.width - 2.4f, panel.height - 2.4f), Fade(Canvas, 0.96f), 8);
            Text(new Rect(panel.x + 10, panel.y + 5, 64, 22), "요약 지도", 12, Paper, true);
            // 단계: 동(보는 동과 이웃) · 구(구 전체)
            if (ExpansionButton(new Rect(panel.x + 78, panel.y + 6, 28, 19), "동", true, miniDongLevel ? Mint : Tile)) miniDongLevel = true;
            if (ExpansionButton(new Rect(panel.x + 109, panel.y + 6, 28, 19), "구", true, miniDongLevel ? Tile : Mint)) miniDongLevel = false;
            MiniWorkToggle(panel);      // 「업무」: 같은 자리를 업무 요약으로
            if (ExpansionButton(new Rect(panel.xMax - 50, panel.y + 6, 42, 19), "접기", true, Tile)) miniMapFolded = true;

            // 구 지도
            var area = MiniMapArea;
            Paint(area, MiniGround, 7);
            GUI.DrawTexture(area, miniBase, ScaleMode.StretchToFill, true);
            var guRing = miniGus.First(shape => shape.Name == miniGuName).Ring;
            var note = MiniPoint(new Vector2(miniBox.center.x + miniBox.width * 0.08f, miniBox.yMin + miniBox.height * 0.72f));
            if (!dongLevel && miniDongs.Count < 16 && guRing.Count > 0)
                Text(new Rect(area.x + note.x - 40, area.y + note.y - 8, 80, 16), "자료 준비 전", 9, Ink(0x6F8A9A), false, TextAnchor.MiddleCenter);
            if (dongLevel)
                foreach (var dong in miniDongs)
                {
                    // 동 단계에서는 동 이름을 적는다(칸 가장자리에 걸리는 것은 뺀다)
                    var at = area.position + MiniPoint(MiniCentroid(dong.Ring));
                    if (at.x < area.x + 24 || at.x > area.xMax - 24 || at.y < area.y + 9 || at.y > area.yMax - 9) continue;
                    var on = dong.Region == focusRegion;
                    Text(new Rect(at.x - 38, at.y - 8, 76, 16), dong.Name, on ? 10 : 9, on ? Paper : Ink(0x7E98A6), on, TextAnchor.MiddleCenter);
                }
            foreach (var place in miniPlaces)
            {
                var at = area.position + MiniPoint(place.At);
                if (!area.Contains(at)) continue;
                Disc(at, 3.6f, Fade(place.Colour, 0.3f));
                Disc(at, 2f, place.Colour);
            }

            // 지금 보는 곳: 보이는 땅(커야 그린다) · 보는 방향 · 점
            var spot = area.position + MiniPoint(centre);
            var ground = MiniViewCorners();
            // 역 판을 통째로 볼 때는 화면이 아니라 둥근 판이 보이는 땅이다: 그 판을 그리고, 옆 글도 판을 말한다.
            var board = StationView && (ground[0] - ground[2]).magnitude > 역중심Policy.Radius * 2.6;
            if (Event.current.type == EventType.Repaint && area.Contains(spot))
            {
                var corners = ground.Select(point => area.position + MiniPoint(point)).ToArray();
                if (board)
                {
                    var station = Station;
                    var (stationEast, stationNorth) = DongLocal(station.Latitude, station.Longitude);
                    Hoop(area.position + MiniPoint(new Vector2((float)stationEast, (float)stationNorth)), (float)역중심Policy.Radius * miniScale, Fade(Gold, 0.9f), 1.3f);
                }
                else if ((corners[0] - corners[2]).magnitude >= MiniViewBoxFloor)
                {
                    // 칸 밖으로 나가는 모서리는 칸 가장자리에 붙인다
                    for (var i = 0; i < 4; i++)
                        corners[i] = new Vector2(Mathf.Clamp(corners[i].x, area.xMin + 1, area.xMax - 1), Mathf.Clamp(corners[i].y, area.yMin + 1, area.yMax - 1));
                    for (var i = 0; i < 4; i++) MiniLine(corners[i], corners[(i + 1) % 4], Fade(Gold, 0.9f), 1.3f);
                }
                var original = GUI.matrix;
                GUI.matrix = original * Matrix4x4.TRS(new Vector3(spot.x, spot.y, 0), Quaternion.Euler(0, 0, (float)dioramaLens.YawDegrees), Vector3.one) *
                    Matrix4x4.Translate(new Vector3(-spot.x, -spot.y, 0));
                GUI.DrawTexture(new Rect(spot.x - 21, spot.y - 21, 42, 42), miniWedge, ScaleMode.StretchToFill, true);
                GUI.matrix = original;
                Hoop(spot, 10f, Fade(Gold, 0.55f), 1f);
                Disc(spot, 4.4f, Canvas);
                Disc(spot, 3f, Gold);
            }

            // 옆: 글로도 적는다
            var x = area.xMax + 8;
            Text(new Rect(x, area.y - 2, 88, 14), "지금 보는 곳", 9, Soft);
            Text(new Rect(x, area.y + 12, 90, 20), dongName, 13, Gold, true);
            Text(new Rect(x, area.y + 33, 90, 15), miniGuName + " " + MiniSide(centre), 10, Paper);
            if (board)
            {
                Text(new Rect(x, area.y + 48, 90, 15), Station.Name + " 판", 10, Paper);
                Text(new Rect(x, area.y + 63, 90, 15), "반경 " + 역중심Policy.Radius.ToString("0") + " m", 10, Soft);
            }
            else
            {
                var near = MiniNearPlace(centre);
                Text(new Rect(x, area.y + 48, 90, 15), near != null ? near + " 둘레" : string.Empty, 10, Paper);
                Text(new Rect(x, area.y + (near != null ? 63 : 48), 90, 15), MiniWidthLabel(ViewWidthMetres(mapZoom)), 10, Soft);
            }

            // 권역 안의 구
            var inset = MiniInsetArea;
            Paint(inset, MiniGround, 6);
            GUI.DrawTexture(inset, miniInset, ScaleMode.StretchToFill, true);
            var insetSpot = inset.position + MiniInsetPoint(centre);
            if (inset.Contains(insetSpot))
            {
                Disc(insetSpot, 4.2f, Fade(Gold, 0.3f));
                Disc(insetSpot, 2.2f, Gold);
            }
            Text(new Rect(inset.x + 5, inset.yMax - 15, 80, 13), "서울 동북권", 8, Soft);

            // 아래: 범례와 「내 매장으로」
            var foot = panel.yMax - 30;
            if (miniNote != null && Time.unscaledTime < miniNoteUntil)
                Text(new Rect(panel.x + 10, foot + 2, 164, 18), miniNote, 9, Gold);       // 방금 누른 곳에 대한 한 줄
            else
            {
                Disc(new Vector2(panel.x + 14, foot + 11), 3.2f, Gold);
                Text(new Rect(panel.x + 21, foot + 2, 44, 18), "보는 곳", 9, Soft);
                Disc(new Vector2(panel.x + 70, foot + 11), 3.2f, Lilac);
                Text(new Rect(panel.x + 77, foot + 2, 40, 18), "내 곳", 9, Soft);
            }
            if (ExpansionButton(new Rect(panel.xMax - 96, foot, 88, 22), "내 매장으로", miniPlaces.Count > 0, Tile)) MiniMapHome();
            // 지도의 한 곳을 누르면 큰 지도가 그곳으로 간다(준비된 동일 때)
            if (Hit(area)) MiniMapPick(MiniMetres(Event.current.mousePosition - area.position));
        }

        // 구 지도 칸 안의 논리 좌표 → 공통 원점 기준 m (MiniPoint의 거꾸로)
        private Vector2 MiniMetres(Vector2 inArea) => new(miniBox.xMin + (inArea.x - miniOrigin.x) / miniScale,
            miniBox.yMin + (MiniMapArea.height - miniOrigin.y - inArea.y) / miniScale);

        private static Vector2 MiniCentroid(List<Vector2> ring)
        {
            var sum = Vector2.zero;
            foreach (var point in ring) sum += point;
            return ring.Count == 0 ? sum : sum / ring.Count;
        }

        private static bool MiniInside(List<Vector2> ring, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var a = ring[i];
                var b = ring[j];
                if (a.y > point.y != b.y > point.y && point.x < a.x + (point.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
            }
            return inside;
        }

        private void MiniSay(string text)
        {
            miniNote = text;
            miniNoteUntil = Time.unscaledTime + 2.6f;
        }

        // 요약 지도의 한 곳(공통 원점 기준 m)을 눌렀다. 준비된 동이면 큰 지도를 그곳으로 옮긴다(배율은 그대로). 보기만 바뀐다.
        // 돌려주는 값: moved(옮김) · unready(구 안, 자료 준비 전) · outside(구 밖) · board(역 판 밖).
        private string MiniMapPick(Vector2 metres)
        {
            var latitude = dongManifest.originLatitude + metres.y / dongManifest.metresPerDegreeLatitude;
            var longitude = dongManifest.originLongitude + metres.x / dongManifest.metresPerDegreeLongitude;
            var dong = DongModuleAt(latitude, longitude);
            if (dong == null)
            {
                var inside = miniGus.Any(shape => shape.Name == miniGuName && MiniInside(shape.Ring, metres));
                MiniSay(inside ? "자료 준비 전인 곳입니다" : "준비된 동 밖입니다");
                return inside ? "unready" : "outside";
            }
            if (StationView && !StationShows(latitude, longitude))
            {
                MiniSay("이 역 판 밖입니다 · ◀ ▶ 로 역을 바꿉니다");
                return "board";
            }
            MoveViewTo(latitude, longitude);
            MiniSay(dong.Entry.name + "으로 옮겼습니다");
            return "moved";
        }

        // 지도 묶음 밖(화면 좌표)에서 긋는 선. Line은 지도 묶음 안에서 쓰는 것이라 따로 둔다.
        private static void MiniLine(Vector2 a, Vector2 b, Color colour, float width)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length <= 0.01f) return;
            var original = GUI.matrix;
            GUI.matrix = original * Matrix4x4.TRS(new Vector3(a.x, a.y, 0), Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            Paint(new Rect(0, -width / 2, length, width), colour);
            GUI.matrix = original;
        }

        // 화면에 보이는 땅의 네 모서리(공통 원점 기준 m). 가려지지 않는 지도 영역으로 잰다.
        private Vector2[] MiniViewCorners()
        {
            var view = DongFitArea;
            var shift = Field.position;
            return new[]
            {
                MiniGroundAt(new Vector2(view.xMin, view.yMin) - shift), MiniGroundAt(new Vector2(view.xMax, view.yMin) - shift),
                MiniGroundAt(new Vector2(view.xMax, view.yMax) - shift), MiniGroundAt(new Vector2(view.xMin, view.yMax) - shift),
            };
        }

        // 보는 곳에서 가까운 내 곳의 이름(화면 가로의 0.4배 안). 없으면 null.
        private string MiniNearPlace(Vector2 centre)
        {
            var reach = ViewWidthMetres(mapZoom) * 0.4f;
            string best = null;
            var bestDistance = float.MaxValue;
            foreach (var place in miniPlaces)
            {
                var distance = (place.At - centre).magnitude;
                if (distance > reach || distance >= bestDistance) continue;
                bestDistance = distance;
                best = place.Title;
            }
            if (StoreDeskVisible)
            {
                var chosen = miniPlaces.FirstOrDefault(item => item.Place == storeDeskPlace);
                if (chosen.Title != null && (chosen.At - centre).magnitude <= reach) best = chosen.Title;
            }
            return best;
        }

        // 방향 촬영: 가까이 보기와 동 전체에서 요약 지도를 찍는다. 보는 곳의 동이 맞는지, 지도가 눌리지 않는지,
        // 접기 · 내 매장으로 · 자리 내주기가 되는지, 그것만으로 시계 · 원장이 그대로인지 본다.
        private IEnumerator ViewProofMiniMap(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            lightMenu = null;
            miniMapFolded = false;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var place = engine?.Pack?.network.store.placeId;
            if (place == null || !buildingByPlace.TryGetValue(place, out var cafe) || cafe < 0 || cafe >= buildingShapes.Length)
            {
                holdProofClock = false;
                yield break;
            }
            var mid = BuildingMid(cafe);
            var latitude = buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude;
            var longitude = buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude;
            GoToPlace(place);
            ViewProofCentre(latitude, longitude, MaxMapZoom);
            yield return null;
            yield return null;
            yield return null;
            receipt.miniShown = MiniMapOpen;
            receipt.miniDongs = miniDongs?.Count ?? 0;
            receipt.miniPlaces = miniPlaces.Count;
            receipt.miniDong = DongModuleAtViewCentre()?.Entry.name ?? string.Empty;
            receipt.miniCafeDong = buildingShapes[cafe].Record.dong ?? string.Empty;
            receipt.miniCoversMap = MiniMapCovers(MiniMapPanel.center) && MapOverlayCovers(MiniMapPanel.center);
            if (MiniMapOpen)
            {
                var spot = MiniPoint(MiniViewCentre());
                receipt.miniSpotInside = spot.x >= 0 && spot.y >= 0 && spot.x <= MiniMapArea.width && spot.y <= MiniMapArea.height;
                var (east, north) = DongLocal(latitude, longitude);
                receipt.miniCentreOffMetres = (MiniViewCentre() - new Vector2((float)east, (float)north)).magnitude;
            }
            yield return SaveFrame(directory, "65-minimap-close.png");

            // 동 전체(역 판): 보이는 땅의 네모가 뜬다
            storeDeskPlace = null;
            ShowWholeDong();
            yield return null;
            yield return null;
            yield return null;
            if (MiniMapOpen)
            {
                var corners = MiniViewCorners().Select(MiniPoint).ToArray();
                receipt.miniViewBoxCells = (corners[0] - corners[2]).magnitude;
            }
            yield return SaveFrame(directory, "66-minimap-whole.png");

            // 내 매장으로: 다른 곳을 보다가 누르면 그 매장이 가운데로 온다(배율은 그대로)
            ViewProofCentre(latitude + 0.0025, longitude + 0.002, (float)(Width * metresPerPixel / 560f));
            yield return null;
            yield return null;
            var zoomBefore = mapZoom;
            receipt.miniHomeMoved = MiniMapHome();
            yield return null;
            yield return null;
            var home = MiniHomePlace();
            if (home != null && buildingByPlace.TryGetValue(home, out var homeBuilding))
            {
                var homeMid = BuildingMid(homeBuilding);
                var (east, north) = DongLocal(buildingIndex.origin.latitude + homeMid.y / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + homeMid.x / buildingIndex.metresPerDegreeLongitude);
                receipt.miniHomeOffMetres = (MiniViewCentre() - new Vector2((float)east, (float)north)).magnitude;
            }
            receipt.miniHomeKeptZoom = Mathf.Approximately(zoomBefore, mapZoom);

            // 접기
            miniMapFolded = true;
            yield return null;
            yield return null;
            receipt.miniFoldedLine = MiniMapShown && !MiniMapOpen && MiniMapCovers(MiniMapLine.center) && !MiniMapCovers(MiniMapArea.center);
            yield return SaveFrame(directory, "67-minimap-folded.png");
            miniMapFolded = false;

            // 동 단계와 누르기: 카페 둘레를 보다가, 요약 지도에서 매장 A 자리를 누른 것과 같은 함수로 옮긴다
            miniDongLevel = true;
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / 560f));
            yield return null;
            yield return null;
            yield return null;
            receipt.miniDongLevelShown = MiniMapOpen && miniBaseFor != null && miniBaseFor.StartsWith("D:", StringComparison.Ordinal);
            yield return SaveFrame(directory, "68-minimap-dong.png");
            if (miniPlaces.Count > 0)
            {
                var target = miniPlaces[0].At;
                var zoomAtPick = mapZoom;
                receipt.miniPick = MiniMapPick(target);
                yield return null;
                yield return null;
                receipt.miniPickOffMetres = (MiniViewCentre() - target).magnitude;
                receipt.miniPickKeptZoom = Mathf.Approximately(zoomAtPick, mapZoom);
                var stay = MiniViewCentre();
                receipt.miniPickUnready = MiniMapPick(new Vector2(miniHomeBox.center.x + miniHomeBox.width * 0.08f, miniHomeBox.yMin + miniHomeBox.height * 0.72f));
                yield return null;
                receipt.miniPickUnreadyStayed = (MiniViewCentre() - stay).magnitude < 0.5f;
            }
            miniDongLevel = false;
            miniNote = null;

            // 오른쪽 칸에 건물 카드가 뜨면 자리를 내준다
            SelectPlace(place);
            yield return null;
            yield return null;
            receipt.miniGivesWay = selectedBuilding != null && !MiniMapShown;
            selectedBuilding = null;
            ShowWholeDong();
            yield return null;
            yield return null;
            receipt.miniUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
