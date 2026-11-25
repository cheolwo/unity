using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 이어지는 지도 (2026-11-16, 개발 계획 「이어지는 지도 r253」 M0 · M2 · M3 첫 조각, 사용자: 「제안해준 대로 진행」).
    //  - 한 배율 축: 전국 → 권역(수도권 길 바탕) → 구 → 동. 단계 안내 창 없이 같은 자리 · 같은 m/점에서 넘긴다.
    //    · 전국 지도는 수도권에서 3배보다 더 확대된다. 바탕을 무채색으로 덮고 길(ITS 표준노드링크)을 띄우며,
    //      구 지도 자료 안에서는 구 지도 바탕 그림을 그 자리에 겹친다. 넘김 배율에서 같은 자리의 구 지도로 바꾼다.
    //    · 구 지도는 첫 맞춤(내 영역)보다 넓게, 구 자료가 화면을 채우는 데까지 축소한다. 거기서 더 축소하면 같은 자리의 전국 지도로.
    //    · 동네 지도 전체 보기에서 더 축소하면 같은 자리의 구 지도로(알림 창 없이).
    //  - 지도 왼쪽 위의 「이어 보기 띠」: 전국 › 수도권 › 구 › 동과 축척 자가 모든 높이에서 같은 자리에 있다. 누르면 그 높이로.
    //  - 보기만 바뀐다. 시계 · 돈 · 주문 · 저장은 그대로다. 지형 음영과 하천은 자료가 준비되기 전이라 그리지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string RegionSpaceResource = "hongdal-region-space";
        private const string RegionRoadsResource = "hongdal-region-roads";
        private static readonly Color MapGrey = new(27f / 255f, 27f / 255f, 27f / 255f, 1f);     // 구 지도 바탕의 땅 색
        private static readonly Rect ContinuityStrip = new(22, 96, 360, 26);      // 11-22: 「타일」 칸이 들어가 330 → 360
        private const double HomeLongitude = 127.0884106;
        private const double HomeLatitude = 37.5806971;

        [Serializable]
        private sealed class RegionSpace
        {
            public string schema;
            public string version;
            public string source;
            public double west;
            public double east;
            public double south;
            public double north;
            public float worldX;
            public float worldY;
            public float worldWidth;
            public float worldHeight;
            public float coreWorldX;          // 정밀 2: 사가정 둘레를 더 촘촘하게 구운 가운데 그림의 범위(없으면 0)
            public float coreWorldY;
            public float coreWorldWidth;
            public float coreWorldHeight;
        }

        private const string RegionCoreResource = "hongdal-region-roads-core";
        private RegionSpace regionSpace;
        private Texture2D regionRoads;
        private Texture2D regionCore;
        private bool regionTried;
        private float guVeilUntil;            // 전국 → 구로 넘긴 뒤 구 지도 표식 · 색이 나타나는 동안

        // 정밀 1: 전국 지도의 가로 · 세로 배율. 깊이 들어갈수록 동서 늘어남을 걷어 내 구 지도의 실제 미터와 맞춘다.
        private float NationScaleX => nationScale * 이어지는지도Policy.동서맞춤(nationScale);
        private Vector2 NationScaleXY => new(NationScaleX, nationScale);

        private bool RegionReady => regionSpace != null && regionRoads != null;

        private bool LoadRegion()
        {
            if (regionTried) return RegionReady;
            regionTried = true;
            var space = ReadMapResource<RegionSpace>(RegionSpaceResource);
            var asset = Resources.Load<TextAsset>(RegionRoadsResource);
            if (space == null || space.worldWidth <= 0 || space.worldHeight <= 0 || asset == null)
            {
                if (asset != null) Resources.UnloadAsset(asset);
                Debug.LogWarning("Hongdal continuity: 수도권 길 바탕이 없어 전국 지도는 3배까지만 확대합니다.");
                return false;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Hongdal region roads", wrapMode = TextureWrapMode.Clamp };
            var loaded = texture.LoadImage(asset.bytes, true);
            Resources.UnloadAsset(asset);
            if (!loaded)
            {
                Destroy(texture);
                Debug.LogWarning("Hongdal continuity: 수도권 길 바탕 그림을 읽지 못했습니다.");
                return false;
            }
            texture.filterMode = FilterMode.Trilinear;
            regionSpace = space;
            regionRoads = texture;
            var core = space.coreWorldWidth > 0 && space.coreWorldHeight > 0 ? Resources.Load<TextAsset>(RegionCoreResource) : null;
            if (core != null)
            {
                var coreTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Hongdal region roads core", wrapMode = TextureWrapMode.Clamp };
                if (coreTexture.LoadImage(core.bytes, true))
                {
                    coreTexture.filterMode = FilterMode.Trilinear;
                    regionCore = coreTexture;
                }
                else Destroy(coreTexture);
                Resources.UnloadAsset(core);
            }
            Debug.Log($"Hongdal continuity: 수도권 길 바탕 {texture.width}×{texture.height}" +
                (regionCore == null ? string.Empty : $" · 가운데 {regionCore.width}×{regionCore.height}") + $" · {space.version}");
            return true;
        }

        // 전국 지도가 3배보다 깊이 들어갈 수 있나: 수도권 길 바탕과 구 지도 자료가 다 있어야 한다.
        private bool NationDeepReady => LoadRegion() && LoadGu();

        // 구 지도의 가장 넓은 보기. 수도권 바탕이 있으면 수도권 전체(SagajeongDispatchCapital.cs).
        private float GuFullScale => guSpace == null ? 0.066f : CapitalReady ? CapitalFullScale : Mathf.Max(Field.width / guSpace.width, Field.height / guSpace.height);

        private float NationHandoffScale => 이어지는지도Policy.넘김배율(GuFullScale);

        private float NationScaleCap => NationDeepReady ? Mathf.Max(이어지는지도Policy.권역최대배율, NationHandoffScale) : NationMaxScale;

        private double GuEast => guSpace.west + guSpace.width / 이어지는지도Policy.구경도미터;
        private double GuNorth => guSpace.south + guSpace.height / 이어지는지도Policy.구위도미터;

        private bool InRegion(Vector2 world) => RegionReady &&
            world.x >= regionSpace.worldX && world.x <= regionSpace.worldX + regionSpace.worldWidth &&
            world.y >= regionSpace.worldY && world.y <= regionSpace.worldY + regionSpace.worldHeight;

        private bool InGuSpace(Vector2 world)
        {
            if (guSpace == null) return false;
            var (lon, lat) = 이어지는지도Policy.전국경위도(world);
            if (CapitalReady) return InCapital(lon, lat);      // 수도권 어디서나 구 지도로 넘어간다
            return lon >= guSpace.west && lon <= GuEast && lat >= guSpace.south && lat <= GuNorth;
        }

        // 구 지도 자료 범위(전국 세계 좌표).
        private Rect GuWorldRect()
        {
            var a = 이어지는지도Policy.전국세계(guSpace.west, GuNorth);
            var b = 이어지는지도Policy.전국세계(GuEast, guSpace.south);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // 구 지도 미터 사각형(남쪽이 y = 0) → 전국 지도 영역 좌표.
        private Rect GuMetresInNation(Rect metres)
        {
            var (lonA, latA) = 이어지는지도Policy.구경위도(new Vector2(metres.xMin, metres.yMax), guSpace.west, guSpace.south);
            var (lonB, latB) = 이어지는지도Policy.구경위도(new Vector2(metres.xMax, metres.yMin), guSpace.west, guSpace.south);
            var a = NationPoint(이어지는지도Policy.전국세계(lonA, latA));
            var b = NationPoint(이어지는지도Policy.전국세계(lonB, latB));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // 전국 지도에서 이 자리로 더 확대할 수 있는 한계.
        private float NationZoomCapAt(Vector2 world)
        {
            if (!NationDeepReady) return NationMaxScale;
            return 이어지는지도Policy.확대한계(InRegion(world), InGuSpace(world), NationHandoffScale);
        }

        // 권역 최대(21배)보다 깊으면 보이는 범위를 구 지도 자료 쪽으로 모은다. 넘김 배율에서는 구 지도의 가장 넓은 보기와 같은 범위가 된다.
        private void FunnelNationToGu()
        {
            if (nationScale <= 이어지는지도Policy.권역최대배율 || !NationDeepReady) return;
            var g = CapitalReady
                ? Rect.MinMaxRect(이어지는지도Policy.전국세계(capital.west, capital.north).x, 이어지는지도Policy.전국세계(capital.west, capital.north).y,
                    이어지는지도Policy.전국세계(capital.east, capital.south).x, 이어지는지도Policy.전국세계(capital.east, capital.south).y)
                : GuWorldRect();
            var t = 이어지는지도Policy.구로모으기(nationScale, NationHandoffScale);
            var view = new Vector2(Field.width / NationScaleX, Field.height / nationScale);
            var centre = NationWorld(new Vector2(Field.width * 0.5f, Field.height * 0.5f));
            var slack = (1f - t) * 2f;
            var minX = g.xMin + view.x * 0.5f - slack * view.x;
            var maxX = g.xMax - view.x * 0.5f + slack * view.x;
            var minY = g.yMin + view.y * 0.5f - slack * view.y;
            var maxY = g.yMax - view.y * 0.5f + slack * view.y;
            var cx = minX <= maxX ? Mathf.Clamp(centre.x, minX, maxX) : g.center.x;
            var cy = minY <= maxY ? Mathf.Clamp(centre.y, minY, maxY) : g.center.y;
            nationOrigin = new Vector2(Field.width * 0.5f - cx * NationScaleX, Field.height * 0.5f - cy * nationScale);
        }

        // 전국 바탕 위: 무채색 덮기 → 수도권 길 → 구 지도 바탕(그 자리). DrawNationMap의 바탕 바로 뒤에서 부른다(지도 영역 좌표).
        private void DrawNationDeep()
        {
            var s = nationScale;
            if (s <= 2.4f || !NationDeepReady) return;
            var grey = 이어지는지도Policy.무채색덮기(s);
            if (grey > 0f) Paint(new Rect(0, 0, Field.width, Field.height), Fade(MapGrey, grey));
            var roads = 이어지는지도Policy.권역길보이기(s);
            if (roads > 0f)
            {
                // 수도권 길(넓게, 세계 1단위당 24점) + 사가정 둘레 가운데 그림(64점). 두 그림은 굽을 때 서로 흐리게 나눠 이음매가 없다.
                var rect = new Rect(NationPoint(regionSpace.worldX, regionSpace.worldY),
                    Vector2.Scale(new Vector2(regionSpace.worldWidth, regionSpace.worldHeight), NationScaleXY));
                GUI.DrawTexture(rect, regionRoads, ScaleMode.StretchToFill, true, 0, new Color(1f, 1f, 1f, roads), 0, 0);
                if (regionCore != null)
                {
                    var core = new Rect(NationPoint(regionSpace.coreWorldX, regionSpace.coreWorldY),
                        Vector2.Scale(new Vector2(regionSpace.coreWorldWidth, regionSpace.coreWorldHeight), NationScaleXY));
                    GUI.DrawTexture(core, regionCore, ScaleMode.StretchToFill, true, 0, new Color(1f, 1f, 1f, roads), 0, 0);
                }
            }
            var gu = 이어지는지도Policy.구바탕보이기(s, NationHandoffScale);
            if (gu > 0f)
            {
                // 정밀 2: 가장자리를 흐리게(넘김 배율에 가까울수록 흐림이 줄어 구 지도와 같은 그림이 된다)
                var feather = 이어지는지도Policy.가장자리흐림(s, NationHandoffScale);
                DrawFeathered(GuMetresInNation(new Rect(0, 0, guSpace.width, guSpace.height)), guGrey, gu, feather);
                if (guDetail != null) DrawFeathered(GuMetresInNation(guDetailRect), guDetail, gu, feather);
            }
        }

        // 가장자리가 흐린 그림: 바깥에서 안쪽으로 줄어드는 사각형 여러 장을 차례로 덮는다. 맨 안쪽은 alpha 그대로.
        private static void DrawFeathered(Rect rect, Texture texture, float alpha, float feather, int steps = 6)
        {
            if (feather <= 0.0005f)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true, 0, new Color(1f, 1f, 1f, alpha), 0, 0);
                return;
            }
            var previous = 0f;
            var colour = GUI.color;
            for (var i = 0; i < steps; i++)
            {
                var inset = feather * i / steps;                       // 바깥(0)에서 안쪽(feather)으로
                var target = alpha * (i + 1) / steps;                  // 이 띠까지 쌓인 덮임
                var layer = previous >= 0.999f ? 1f : Mathf.Clamp01(1f - (1f - target) / (1f - previous));
                previous = target;
                var uv = new Rect(inset, inset, 1f - inset * 2f, 1f - inset * 2f);
                var part = new Rect(rect.x + rect.width * inset, rect.y + rect.height * inset, rect.width * (1f - inset * 2f), rect.height * (1f - inset * 2f));
                GUI.color = new Color(colour.r, colour.g, colour.b, colour.a * layer);
                GUI.DrawTextureWithTexCoords(part, texture, uv, true);
            }
            GUI.color = colour;
        }

        // 전국 → 구로 넘긴 직후: 구 지도의 영역 색 · 이름표 · 표식 위에 구 바탕 그림을 덮었다가 0.3초에 걸쳐 걷는다.
        // 넘김 직전의 전국 화면(같은 구 바탕)에서 구 지도로 부드럽게 바뀐다. DrawGuMap의 표식까지 그린 뒤에 부른다.
        private void DrawGuHandoffVeil(float now)
        {
            if (now >= guVeilUntil || guGrey == null) return;
            var veil = Mathf.Clamp01((guVeilUntil - now) / 이어지는지도Policy.넘김가림초);
            var tint = new Color(1f, 1f, 1f, veil);
            GUI.DrawTexture(new Rect(guOrigin.x, guOrigin.y, guSpace.width * guScale, guSpace.height * guScale), guGrey, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
            if (guDetail != null) GUI.DrawTexture(GuRect(guDetailRect), guDetail, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        }

        // ── 넘김 ────────────────────────────────────────────────────────────────

        // 전국 → 구: pointer(지도 영역 좌표)의 땅을 같은 화면 자리에 두고, 같은 m/점으로 구 지도를 연다.
        private void NationToGu(Vector2 pointer)
        {
            var (lon, lat) = 이어지는지도Policy.전국경위도(NationWorld(pointer));
            var perMetre = nationScale / 이어지는지도Policy.전국한단위미터;
            LoadMapLevel(MapLevel.District);
            if (!GuMapVisible) return;
            PlaceGuCamera(lon, lat, pointer, perMetre);
            guVeilUntil = Time.unscaledTime + 이어지는지도Policy.넘김가림초;
            Debug.Log($"Hongdal continuity: 전국 → 구 · {이어지는지도Policy.구미터점(guScale):0.0} m/점");
        }

        // 구 → 전국: 같은 자리 · 같은 m/점의 전국 지도로.
        private void GuToNation(Vector2 pointer)
        {
            var (lon, lat) = 이어지는지도Policy.구경위도(GuWorld(pointer), guSpace.west, guSpace.south);
            var scale = 이어지는지도Policy.넘김배율(guScale);
            LoadMapLevel(MapLevel.Nation);
            if (!NationMapVisible) return;
            PlaceNationCamera(lon, lat, pointer, scale);
            Debug.Log($"Hongdal continuity: 구 → 전국 · {이어지는지도Policy.전국미터(nationScale):0.0} m/점");
        }

        // 동네 전체 보기 → 구: 동네 자료 가운데를 화면 가운데에 두고, 지금 m/점에 가까운 구 지도로(구 지도의 한계 안).
        private void DongToGu()
        {
            double lon = centerLongitude, lat = centerLatitude;
            if (dioramaRoot != null && dioramaEast > dioramaWest && dioramaNorth > dioramaSouth)
            {
                lon = (dioramaWest + dioramaEast) * 0.5;
                lat = (dioramaSouth + dioramaNorth) * 0.5;
            }
            var perMetre = metresPerPixel > 0 ? mapZoom / (float)metresPerPixel : GuMaxScale;
            LoadMapLevel(MapLevel.District);
            if (!GuMapVisible) return;
            PlaceGuCamera(lon, lat, new Vector2(Field.width * 0.5f, Field.height * 0.5f), perMetre);
            Debug.Log($"Hongdal continuity: 동 → 구 · {이어지는지도Policy.구미터점(guScale):0.0} m/점");
            ReturnToHexTile();       // 타일에서 두 번 눌러 내려온 동이면 같은 타일로 (SagajeongDispatchHexTiles.cs)
        }

        private void PlaceGuCamera(double lon, double lat, Vector2 pointer, float perMetre)
        {
            var at = 이어지는지도Policy.구미터(lon, lat, guSpace.west, guSpace.south);
            guScale = Mathf.Clamp(perMetre, GuFullScale, Mathf.Max(GuFullScale, GuMaxScale));
            guOrigin = new Vector2(pointer.x - at.x * guScale, pointer.y - (guSpace.height - at.y) * guScale);
            guCameraSet = true;
            guManual = true;
            ClampGu();
        }

        private void PlaceNationCamera(double lon, double lat, Vector2 pointer, float scale)
        {
            var world = 이어지는지도Policy.전국세계(lon, lat);
            nationScale = Mathf.Clamp(scale, NationFloor, Mathf.Max(NationFloor, NationScaleCap));
            nationOrigin = pointer - Vector2.Scale(world, NationScaleXY);
            nationCameraSet = true;
            nationManual = true;
            ClampNation();
        }

        // ── 이어 보기 띠 ────────────────────────────────────────────────────────

        private bool ContinuityVisible => expansionView && !mapOnly && !showDesk && !PlaceVisible && !SettingsVisible;

        private bool ContinuityCovers(Vector2 point) => ContinuityVisible && ContinuityStrip.Contains(point);

        // 지금 보는 높이의 m/점(논리 화면 한 점).
        private float CurrentMetresPerPoint() => mapLevel switch
        {
            MapLevel.Nation when NationReady => 이어지는지도Policy.전국미터(nationScale),
            MapLevel.District when GuReady && guScale > 0f => 이어지는지도Policy.구미터점(guScale),
            MapLevel.Dong when metresPerPixel > 0 && mapZoom > 0f => (float)metresPerPixel / mapZoom,
            _ => metresPerPixel > 0 ? (float)metresPerPixel : 50f,
        };

        // 화면 가운데의 경위도.
        private (double Longitude, double Latitude) ContinuityCentre()
        {
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            if (mapLevel == MapLevel.Nation && NationReady && nationCameraSet) return 이어지는지도Policy.전국경위도(NationWorld(centre));
            if (mapLevel == MapLevel.District && GuReady && guCameraSet) return 이어지는지도Policy.구경위도(GuWorld(centre), guSpace.west, guSpace.south);
            return (HomeLongitude, HomeLatitude);
        }

        private string ContinuityGuName()
        {
            if (guSpace?.dongs == null || guSpace.sigs == null) return "구";
            var (lon, lat) = mapLevel == MapLevel.District ? ContinuityCentre() : (HomeLongitude, HomeLatitude);
            var dong = GuDongAt(이어지는지도Policy.구미터(lon, lat, guSpace.west, guSpace.south));
            if (dong == null) return "구";
            foreach (var sig in guSpace.sigs)
                if (sig.code == dong.sig) return sig.name;
            return "구";
        }

        private void DrawMapContinuity()
        {
            if (!ContinuityVisible) return;
            var metres = CurrentMetresPerPoint();
            var band = mapLevel switch
            {
                MapLevel.Dong => 지도띠.동,
                MapLevel.District => 지도띠.구,
                MapLevel.Seoul => 지도띠.구,
                _ => 이어지는지도Policy.띠(metres) == 지도띠.전국 ? 지도띠.전국 : 지도띠.권역,
            };
            // 구와 동 사이의 타일 겹(2026-11-22): 구 다음에 「타일」 칸을 둔다. 타일이 드러나 있으면 구 대신 그 칸이 켜진다(SagajeongDispatchHexTiles.cs).
            var hexReady = EnsureHexTiles() != null;
            var hexOn = hexReady && mapLevel == MapLevel.District && HexTilesActive;
            var last = dongMapOpen ? viewedDong?.name ?? "동네" : "다음 판";
            var names = hexReady ? new[] { "전국", "수도권", ContinuityGuName(), "타일", last } : new[] { "전국", "수도권", ContinuityGuName(), last };
            var pad = hexReady ? 10f : 12f;
            var per = hexReady ? 10.5f : 11.5f;
            var strip = ContinuityStrip;
            Paint(strip, Fade(Canvas, 0.9f), 8);
            var x = strip.x + 6f;
            for (var i = 0; i < names.Length; i++)
            {
                var tileChip = hexReady && i == 3;
                var level = hexReady && i > 3 ? i - 1 : i;      // 띠 차례(타일 칸은 띠가 아니다)
                var width = pad + names[i].Length * per;
                var chip = new Rect(x, strip.y + 3, width, strip.height - 6);
                var on = tileChip ? hexOn : (int)band == level && !(hexOn && level == (int)지도띠.구);
                if (on) Paint(chip, Mint, 6);
                else if (chip.Contains(Event.current.mousePosition)) Paint(chip, Tile, 6);
                Text(chip, names[i], 11, on ? Canvas : Soft, on, TextAnchor.MiddleCenter);
                if (!on && Hit(chip))
                {
                    if (tileChip) JumpToHexTiles();
                    else JumpToBand((지도띠)level);
                }
                x += width;
                if (i < names.Length - 1)
                {
                    Text(new Rect(x, strip.y, pad, strip.height), "›", 12, Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                    x += pad;
                }
            }
            // 축척 자
            var distance = 이어지는지도Policy.축척거리(metres);
            var length = Mathf.Min(strip.xMax - 8f - (x + 44f), distance / Mathf.Max(0.0001f, metres));
            var left = strip.xMax - 8f - length;
            Text(new Rect(left - 46f, strip.y, 42f, strip.height), 이어지는지도Policy.축척글(distance), 10, Paper, true, TextAnchor.MiddleRight);
            var colour = Fade(Paper, 0.85f);
            Paint(new Rect(left, strip.y + 14f, length, 2f), colour);
            Paint(new Rect(left, strip.y + 10f, 2f, 6f), colour);
            Paint(new Rect(left + length - 2f, strip.y + 10f, 2f, 6f), colour);
        }

        private void JumpToBand(지도띠 band)
        {
            var (lon, lat) = ContinuityCentre();
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            switch (band)
            {
                case 지도띠.전국:
                    if (mapLevel != MapLevel.Nation) LoadMapLevel(MapLevel.Nation);
                    nationManual = false;
                    break;
                case 지도띠.권역:
                    if (mapLevel != MapLevel.Nation) LoadMapLevel(MapLevel.Nation);
                    if (NationMapVisible) PlaceNationCamera(lon, lat, centre, 10f);
                    break;
                case 지도띠.구:
                    if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
                    guManual = false;
                    break;
                default:
                    ReturnToDong();
                    break;
            }
            Debug.Log("Hongdal continuity: 이어 보기 띠 → " + 이어지는지도Policy.띠글(band));
        }
    }
}
