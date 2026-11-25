using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-12 구 지도(G1)와 넓어지는 영역(G3a·b)의 화면.
    //  - 바탕: 미리 구운 그림 한 장(토지피복 + 차선 + 구 경계)의 무채색 판을 깐다. 북쪽이 위인 평면이다.
    //  - 밝기: 법정동마다 색 바탕을 오려 두고, 운영이 깊을수록 진하게 덮는다. 가 보지 않은 곳은 회색으로 남는다.
    //  - 범위: 한 화면에는 "내 영역 + 갈 수 있는 동"이 들어온다. 영역이 넓어지면 화면도 넓어진다.
    //  - 표식: 지사·음식점·카페·창고가 꽂히고, 가까이 모인 것은 하나로 묶어 수를 적는다.
    //  - 확대(G2): 구 지도 안에서 휠로 확대하고 끌어서 옮긴다. 가까이 볼수록 길 이름, 모든 동의 경계와 이름이 더 보인다.
    //    세 구는 더 촘촘한 바탕(1점 = 2.5m)을 따로 깐다. 끝까지 확대한 곳이 면목동이면 동네 지도로 내려간다.
    // 규칙은 커널의 영역 모듈(TerritoryModule)에 있다. 화면은 상태 사본을 읽고 명령(enter, respond)만 보낸다.
    public sealed partial class SagajeongDispatchView
    {
        private const string GuSpaceResource = "hongdal-gu-space";
        private const string GuBaseResource = "hongdal-gu-base";
        private const string GuGreyResource = "hongdal-gu-base-grey";
        private const string GuDetailGreyResource = "hongdal-gu-detail-grey";
        private const float GuMaxScale = 0.34f;          // 가장 가까이: 화면 1점이 약 3m
        private const float GuNearScale = 0.11f;         // 이보다 가까우면 모든 동의 경계와 이름을 보인다
        private const float GuPinDrop = 0.45f;           // 표식이 떨어져 꽂히는 시간(초)
        private const float GuClusterRadius = 22f;       // 이보다 가까운 표식은 하나로 묶는다(화면 점)
        private static readonly Color[] GuLegend = { Ink(0x22292E), Ink(0x3A4046), Ink(0x3D5A52), Ink(0x3F7A67), Ink(0x6FD3B0) };
        private static readonly string[] GuLegendNames = { "미지", "후보", "진출 중", "운영", "자리 잡음" };

        [Serializable]
        private sealed class GuSpace
        {
            public string schema;
            public double west;
            public double south;
            public float width;
            public float height;
            public GuArea detail;      // 촘촘한 바탕이 덮는 범위(미터). 색 바탕과 상세 무채색 바탕이 이 범위다
            public GuDong[] dongs;
            public GuSig[] sigs;
            public GuName[] names;
            public GuRoad[] roads;
        }

        [Serializable]
        private sealed class GuArea
        {
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        private sealed class GuRoad
        {
            public string text;
            public float x;
            public float y;
            public float angle;
            public int level;       // 1: 언제나, 2: 조금 확대하면, 3: 가까이에서
        }

        [Serializable]
        private sealed class GuRing
        {
            public int[] p;      // x0, y0, x1, y1 … (미터, 남서쪽 모서리 기준)
        }

        [Serializable]
        private sealed class GuDong
        {
            public string code;
            public string name;
            public string sig;
            public float x;
            public float y;
            public GuRing[] rings;
            [NonSerialized] public Texture2D fill;      // 이 동 모양으로 오린 색 바탕
            [NonSerialized] public Rect fillBounds;     // 오린 그림이 덮는 범위(미터)
            [NonSerialized] public Rect bounds;         // 경계의 범위(미터)
        }

        [Serializable]
        private sealed class GuSig
        {
            public string code;
            public string name;
            public float x;
            public float y;
            public bool focus;
            public GuRing[] rings;
        }

        [Serializable]
        private sealed class GuName
        {
            public string text;
            public string kind;
            public float x;
            public float y;
            public float angle;
        }

        private sealed class GuWave
        {
            public float X, Y, Start, Radius, Seconds;
        }

        private sealed class GuCluster
        {
            public Vector2 Point;
            public TerritoryPinState Lead;
            public TerritoryDongView Dong;
            public int Count;
            public float Seen;
        }

        private GuSpace guSpace;
        private bool guTried;
        private Texture2D guGrey;
        private Texture2D guDetail;          // 세 구의 촘촘한 무채색 바탕
        private Rect guDetailRect;
        private bool guManual;               // 운영자가 직접 확대·이동했다. 영역 전체 보기로 돌아가면 풀린다
        private float guFitScale;            // 내 영역과 후보가 다 들어오는 배율. 축소의 바닥이다
        private bool guPressed;
        private bool guDragging;
        private Vector2 guPressPoint;
        private Vector2 guPressOrigin;
        private string guNotice;
        private float guNoticeUntil;
        private float guScale;
        private Vector2 guOrigin;
        private bool guCameraSet;
        private bool guPrimed;
        private float guFrameAt;
        private string guSelected;
        private string guHover;
        private readonly Dictionary<string, float> guLight = new();
        private readonly Dictionary<string, float> guPinSeen = new();
        private readonly Dictionary<string, int> guLevelSeen = new();
        private readonly List<GuWave> guWaves = new();
        private readonly List<GuCluster> guClusters = new();

        private bool GuReady => guSpace != null && guGrey != null;
        private bool GuMapVisible => mapLevel == MapLevel.District && GuReady;
        private bool TerritoryPanelVisible => GuMapVisible && territory != null && expansionView && !mapOnly && !showDesk &&
            !dongPanelOpen && !zonePanelOpen && !CycleCardVisible;

        // ── 자료 ────────────────────────────────────────────────────────────────

        private bool LoadGu()
        {
            if (guTried) return GuReady;
            guTried = true;
            var space = ReadMapResource<GuSpace>(GuSpaceResource);
            var colorAsset = Resources.Load<TextAsset>(GuBaseResource);
            var greyAsset = Resources.Load<TextAsset>(GuGreyResource);
            if (space?.dongs == null || space.width <= 0 || space.height <= 0 || colorAsset == null || greyAsset == null)
            {
                Debug.LogWarning("Hongdal gu map: 구 공간 팩이 없어 예전 구 지도를 씁니다.");
                return false;
            }

            var started = Time.realtimeSinceStartup;
            var color = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Hongdal gu base" };
            var grey = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "Hongdal gu base grey", wrapMode = TextureWrapMode.Clamp };
            var loaded = color.LoadImage(colorAsset.bytes, false) && grey.LoadImage(greyAsset.bytes, true);
            Resources.UnloadAsset(colorAsset);
            Resources.UnloadAsset(greyAsset);
            if (!loaded)
            {
                Destroy(color);
                Destroy(grey);
                Debug.LogWarning("Hongdal gu map: 바탕 그림을 읽지 못했습니다.");
                return false;
            }
            grey.filterMode = FilterMode.Trilinear;
            // 색 바탕은 촘촘한 범위(세 구)만 덮는다. 범위가 적혀 있지 않은 팩은 전체를 덮는 것으로 본다.
            var area = space.detail != null && space.detail.width > 0 && space.detail.height > 0
                ? new Rect(space.detail.x, space.detail.y, space.detail.width, space.detail.height)
                : new Rect(0, 0, space.width, space.height);
            CutDongFills(space, color, area);
            ReadGuSeasonMasks(color);      // 계절 바탕: 숲 · 공원 · 하천 · 길의 자리 (SagajeongDispatchGuSeason.cs)
            Destroy(color);     // 색 바탕은 동 모양으로 오린 조각만 남긴다

            var detailAsset = Resources.Load<TextAsset>(GuDetailGreyResource);
            if (detailAsset != null)
            {
                var detail = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "Hongdal gu detail grey", wrapMode = TextureWrapMode.Clamp };
                if (detail.LoadImage(detailAsset.bytes, true))
                {
                    detail.filterMode = FilterMode.Trilinear;
                    guDetail = detail;
                    guDetailRect = area;
                }
                else
                    Destroy(detail);
                Resources.UnloadAsset(detailAsset);
            }
            guSpace = space;
            guGrey = grey;
            Debug.Log($"Hongdal gu map: 법정동 {space.dongs.Length}곳 · 바탕 {grey.width}×{grey.height}" +
                (guDetail == null ? string.Empty : $" · 상세 {guDetail.width}×{guDetail.height}") +
                $" · 길 이름 {space.roads?.Length ?? 0}곳 · 준비 {(Time.realtimeSinceStartup - started) * 1000f:0}ms");
            return true;
        }

        // 색 바탕에서 법정동 모양만 오려 둔다. 가장자리는 덮인 만큼만 비치게 해서 계단이 덜 보인다.
        // area: 색 바탕 그림이 덮는 범위(미터).
        private static void CutDongFills(GuSpace space, Texture2D color, Rect area)
        {
            var pixels = color.GetPixels32();     // 아래 줄부터 온다. 그림의 아래가 남쪽이라 미터 좌표와 방향이 같다
            var width = color.width;
            var height = color.height;
            var sx = width / area.width;
            var sy = height / area.height;
            var crossings = new List<float>();
            foreach (var dong in space.dongs)
            {
                if (dong.rings == null || dong.rings.Length == 0) continue;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var ring in dong.rings)
                {
                    for (var i = 0; i + 1 < ring.p.Length; i += 2)
                    {
                        minX = Mathf.Min(minX, ring.p[i]);
                        maxX = Mathf.Max(maxX, ring.p[i]);
                        minY = Mathf.Min(minY, ring.p[i + 1]);
                        maxY = Mathf.Max(maxY, ring.p[i + 1]);
                    }
                }
                dong.bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
                var x0 = Mathf.Clamp(Mathf.FloorToInt((minX - area.x) * sx) - 1, 0, width - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - area.x) * sx) + 1, 1, width);
                var y0 = Mathf.Clamp(Mathf.FloorToInt((minY - area.y) * sy) - 1, 0, height - 1);
                var y1 = Mathf.Clamp(Mathf.CeilToInt((maxY - area.y) * sy) + 1, 1, height);
                var w = x1 - x0;
                var h = y1 - y0;
                if (w <= 0 || h <= 0) continue;

                var fill = new Color32[w * h];
                for (var row = 0; row < h; row++)
                {
                    var offset = (y0 + row) * width + x0;
                    for (var col = 0; col < w; col++)
                    {
                        var source = pixels[offset + col];
                        fill[row * w + col] = new Color32(source.r, source.g, source.b, 0);
                    }
                }
                for (var row = 0; row < h; row++)
                {
                    var y = area.y + (y0 + row + 0.5f) / sy;
                    crossings.Clear();
                    foreach (var ring in dong.rings)
                    {
                        var p = ring.p;
                        for (int i = 0, j = p.Length - 2; i + 1 < p.Length; j = i, i += 2)
                        {
                            float ay = p[i + 1], by = p[j + 1];
                            if ((ay > y) == (by > y)) continue;
                            crossings.Add((p[i] + (y - ay) * (p[j] - p[i]) / (by - ay) - area.x) * sx - x0);
                        }
                    }
                    crossings.Sort();
                    for (var c = 0; c + 1 < crossings.Count; c += 2)
                    {
                        var a = Mathf.Max(0f, crossings[c]);
                        var b = Mathf.Min(w, crossings[c + 1]);
                        if (b <= a) continue;
                        var last = Mathf.Min(w - 1, Mathf.FloorToInt(b));
                        for (var col = Mathf.FloorToInt(a); col <= last; col++)
                        {
                            var cover = Mathf.Min(b, col + 1f) - Mathf.Max(a, col);
                            if (cover <= 0f) continue;
                            var index = row * w + col;
                            fill[index].a = (byte)Mathf.Min(255, fill[index].a + Mathf.RoundToInt(cover * 255f));
                        }
                    }
                }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Hongdal gu " + dong.name, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels32(fill);
                texture.Apply(false, true);
                dong.fill = texture;
                dong.fillBounds = new Rect(area.x + x0 / sx, area.y + y0 / sy, w / sx, h / sy);
            }
        }

        private void ReleaseGu()
        {
            ReleaseCapital();      // 수도권 바탕 조각(SagajeongDispatchCapital.cs)
            if (guGrey != null) Destroy(guGrey);
            guGrey = null;
            if (guDetail != null) Destroy(guDetail);
            guDetail = null;
            ReleaseGuSeason();
            if (guSpace?.dongs == null) return;
            foreach (var dong in guSpace.dongs)
            {
                if (dong.fill != null) Destroy(dong.fill);
                dong.fill = null;
            }
        }

        // ── 좌표와 화면 범위 ────────────────────────────────────────────────────────

        private Vector2 GuPoint(float x, float y) => new(guOrigin.x + x * guScale, guOrigin.y + (guSpace.height - y) * guScale);

        private Rect GuRect(Rect metres) => new(guOrigin.x + metres.x * guScale, guOrigin.y + (guSpace.height - metres.yMax) * guScale,
            metres.width * guScale, metres.height * guScale);

        private Vector2 GuWorld(Vector2 local) => new((local.x - guOrigin.x) / guScale, guSpace.height - (local.y - guOrigin.y) / guScale);

        private GuDong GuDongOf(string code)
        {
            if (code == null) return null;
            foreach (var dong in guSpace.dongs)
                if (dong.code == code) return dong;
            return null;
        }

        private GuDong GuDongAt(Vector2 world)
        {
            foreach (var dong in guSpace.dongs)
            {
                if (dong.rings == null || !dong.bounds.Contains(world)) continue;
                var inside = false;
                foreach (var ring in dong.rings)
                {
                    var p = ring.p;
                    for (int i = 0, j = p.Length - 2; i + 1 < p.Length; j = i, i += 2)
                    {
                        float ax = p[i], ay = p[i + 1], bx = p[j], by = p[j + 1];
                        if ((ay > world.y) != (by > world.y) && world.x < (bx - ax) * (world.y - ay) / (by - ay) + ax) inside = !inside;
                    }
                }
                if (inside) return dong;
            }
            return null;
        }

        // 지도 영역 안에서 패널에 가려지지 않는 부분. 내 영역을 여기에 맞춘다.
        private Rect GuSafeArea => PortraitScreen ? PortraitGuSafeArea : expansionView && !mapOnly
            ? new Rect(222 - Field.x, 180 - Field.y, 754, 468)
            : new Rect(60, 60, Field.width - 120, Field.height - 120);

        // 한 화면의 범위는 "내 영역 + 갈 수 있는 동"이다. 영역이 넓어지면 천천히 물러난다.
        private void UpdateGuCamera(float delta)
        {
            var space = guSpace;
            var full = GuFullScale;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            if (territory != null)
            {
                foreach (var zone in territory.Dongs)
                {
                    if (zone.Level <= TerritoryLevels.Unknown && !zone.Candidate) continue;
                    var dong = GuDongOf(zone.Code);
                    if (dong == null) continue;
                    // 내 동은 경계 전체가, 후보는 이름 자리가 들어오면 된다.
                    var area = zone.Level > TerritoryLevels.Unknown ? dong.bounds : new Rect(dong.x, dong.y, 0, 0);
                    minX = Mathf.Min(minX, area.xMin);
                    maxX = Mathf.Max(maxX, area.xMax);
                    minY = Mathf.Min(minY, area.yMin);
                    maxY = Mathf.Max(maxY, area.yMax);
                }
            }
            if (minX > maxX)
            {
                minX = 0;
                minY = 0;
                maxX = space.width;
                maxY = space.height;
            }
            var safe = GuSafeArea;
            var scale = Mathf.Clamp(Mathf.Min(safe.width / Mathf.Max(1f, maxX - minX), safe.height / Mathf.Max(1f, maxY - minY)) * 0.9f,
                full, Mathf.Max(full, Mathf.Min(full * 3.2f, GuMaxScale)));
            var origin = new Vector2(safe.center.x - (minX + maxX) * 0.5f * scale,
                safe.center.y - (space.height - (minY + maxY) * 0.5f) * scale);
            guFitScale = scale;
            if (!guCameraSet)
            {
                guScale = scale;
                guOrigin = origin;
                guCameraSet = true;
                guManual = false;
            }
            else if (!guManual)
            {
                var ease = 1f - Mathf.Exp(-delta * 2.6f);
                guScale = Mathf.Lerp(guScale, scale, ease);
                guOrigin = Vector2.Lerp(guOrigin, origin, ease);
            }
            ClampGu();
        }

        private void ClampGu()
        {
            var full = GuFullScale;
            guScale = Mathf.Clamp(guScale, full, Mathf.Max(full, GuMaxScale));
            if (CapitalReady)      // 수도권 끝까지 끌 수 있다(SagajeongDispatchCapital.cs)
            {
                guOrigin.x = Mathf.Clamp(guOrigin.x, Field.width - CapitalX1 * guScale, -CapitalX0 * guScale);
                var low = Field.height - (guSpace.height - CapitalY0) * guScale;
                var high = GuTopSlack - (guSpace.height - CapitalY1) * guScale;
                guOrigin.y = low <= high ? Mathf.Clamp(guOrigin.y, low, high) : (low + high) * 0.5f;
                return;
            }
            guOrigin.x = Mathf.Clamp(guOrigin.x, Field.width - guSpace.width * guScale, 0f);
            // 북쪽 끝의 동(묵동·신내동)이 머리줄과 알림 줄에 가리지 않도록, 그 줄들 아래까지는 지도의 북쪽 끝을 내려도 된다.
            guOrigin.y = Mathf.Clamp(guOrigin.y, Field.height - guSpace.height * guScale, GuTopSlack);
        }

        // 구 지도 안의 확대·축소. 돌려주는 값: 0 = 지도 안에서 처리했다, 1 = 더 넓은 지도로 넘어갈 차례, -1 = 동네 지도로 내려갈 차례.
        // delta는 휠 값(아래로 굴리면 +, 축소), pointer는 지도 영역 안의 좌표다.
        private bool wheelToDongOpen = 판Policy.휠로동지도;      // 검증(이어 보기 증명)은 잠깐 열고 본다

        private int GuWheel(float delta, Vector2 pointer)
        {
            if (!guCameraSet) return 0;
            var full = GuFullScale;
            if (delta < 0)
            {
                if (guScale >= GuMaxScale - 0.0005f)
                {
                    var dong = GuDongAt(GuWorld(pointer));
                    var home = dong == null ? null : territory?.Find(dong.code);
                    if (!wheelToDongOpen)      // 기본 판: 구 지도가 바탕이다. 휠로는 동네 지도에 내려가지 않는다(판Policy.cs)
                    {
                        GuSay("여기가 가장 가까운 보기입니다 · 동네 지도는 다음 판에서 열립니다");
                        return 0;
                    }
                    if (home != null && home.IsHome) return -1;
                    GuSay("이곳은 아직 동네 지도가 없습니다 · 면목동에서 더 확대하면 동네 지도로 내려갑니다");
                    return 0;
                }
                ZoomGu(Mathf.Min(GuMaxScale, guScale * Mathf.Pow(1.08f, -delta)), pointer);
                return 0;
            }
            // 이어지는 지도(2026-11-16, r184): 첫 맞춤(내 영역)보다 넓게, 구 지도 자료가 화면을 채우는 데까지 축소한다.
            // 거기서 더 축소하면 같은 자리 · 같은 m/점의 전국 지도로 이어 간다.
            if (guScale <= full * 1.002f) return PortraitScreen ? 0 : 1;      // 세로 화면은 아직 구 지도까지만
            ZoomGu(Mathf.Max(full, guScale * Mathf.Pow(1.08f, -delta)), pointer);
            return 0;
        }

        private void ZoomGu(float scale, Vector2 pointer)
        {
            var world = GuWorld(pointer);
            guScale = scale;
            guManual = true;
            guOrigin = new Vector2(pointer.x - world.x * scale, pointer.y - (guSpace.height - world.y) * scale);
            ClampGu();
        }

        private void GuSay(string text)
        {
            guNotice = text;
            guNoticeUntil = Time.unscaledTime + 3.2f;
        }

        private float GuTopSlack => PortraitScreen ? PortraitMapTop : expansionView && !mapOnly ? 176f - Field.y : 0f;

        private void ShowTerritoryDong(string code)
        {
            if (mapLevel != MapLevel.District)
            {
                CloseLevelPrompt();
                LoadMapLevel(MapLevel.District);
            }
            if (!GuMapVisible) return;
            guManual = false;
            guSelected = code;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
        }

        private void SendTerritory(string action, string code)
        {
            if (engine?.Kernel == null || string.IsNullOrEmpty(code)) return;
            SendBusiness(BusinessIds.Territory, action, command => command.With("dong", code));
            // 시계가 멈춰 있어도 결과가 바로 보이게 사본을 다시 읽는다.
            territory = ReadTerritory();
            signals = engine.Kernel.Signals.OrderByDescending(item => item.IsDanger).ThenBy(item => item.SinceMs).ToList();
        }

        // ── 지도 ────────────────────────────────────────────────────────────────

        // GUI.BeginGroup(Field) 안에서 부른다. 좌표는 지도 영역의 왼쪽 위가 (0, 0)이다.
        private void DrawGuMap()
        {
            var current = Event.current;
            var repaint = current.type == EventType.Repaint;
            var now = Time.unscaledTime;
            if (repaint)
            {
                var delta = guFrameAt <= 0f ? 0f : Mathf.Min(0.1f, now - guFrameAt);
                guFrameAt = now;
                UpdateGuCamera(delta);
                if (CapitalReady) DrawCapitalBase();      // 수도권 바탕 조각(SagajeongDispatchCapital.cs) — 세 구 바탕은 그 위에
                else if (guOrigin.y > 0f) Paint(new Rect(0, 0, Field.width, guOrigin.y + 1f), Ink(0x1A1D20));   // 자료 범위의 북쪽 밖
                GUI.DrawTexture(new Rect(guOrigin.x, guOrigin.y, guSpace.width * guScale, guSpace.height * guScale), guGrey);
                if (guDetail != null) GUI.DrawTexture(GuRect(guDetailRect), guDetail);     // 세 구는 촘촘한 바탕을 덧깐다
                DrawGuSky(now);          // 실제 달력의 낮과 밤: 그날 가로등 점등 시각에 저문다 (SagajeongDispatchDataPacks.cs)
                var free = !MapOverlayCovers(current.mousePosition + Field.position);
                guHover = free && !guDragging ? GuDongAt(GuWorld(current.mousePosition))?.code : null;
                if (territory != null) DrawGuGround(territory, now, delta);
                else DrawGuSeason();     // 철: 숲 · 공원 · 하천 · 눈 · 젖음 (SagajeongDispatchGuSeason.cs)
                DrawGuHexTiles(now);     // 구와 동 사이의 타일 지도: 다가가면 떠오른다 (SagajeongDispatchHexTiles.cs)
                DrawCapitalHex(now);     // 수도권 육각 타일(공개판): 두 번 누르면 동 디오라마 (SagajeongDispatchCapitalHex.cs)
                DrawGuRoadSpeed();       // 길: 그 시각의 10월 평일 평균 통행속도 (SagajeongDispatchDataPacks.cs)
                DrawGuBusLines();        // 버스: 중랑을 지나는 47노선의 확인된 선 조각
                DrawGuStreetLights(now); // 밤 불빛: 그날 점등 시각에 하나씩 켜진다 (SagajeongDispatchDataPacks.cs)
                DrawGuSubway(now);       // 지하철 노선도와 역 번화가 (SagajeongDispatchSubwayGu.cs)
                DrawGuFacilities();      // 가까이에서 생활 시설 참고점(중랑 16동, SagajeongDispatchDataPacks.cs)
                DrawGuFloating(now);     // 유동인구: 행정동 생활인구 원(SagajeongDispatchFloating.cs)
                PrepareGuNames(territory);
                DrawCapitalNames();      // 수도권 시군구 · 행정동 이름(세 구 밖)
                DrawGuNames(now);
                DrawGuRoads();           // 축척 자는 이어 보기 띠로 옮겼다. 동네 범위 점선 상자는 없앴다(사용자 11-16)
            }
            if (!guCameraSet) return;
            // 폰 빌드에는 영역 팩(주소 자료 파생)이 없다(2026-11-25) → 표식 · 이름표만 건너뛰고, 누르기(수도권 칸 · 공개판 디오라마)는 받는다.
            if (territory != null)
            {
                DrawGuPins(territory, now, repaint);
                DrawGuLabels(territory);
            }
            if (repaint) DrawGuWeather(now);     // 그날의 비 · 눈과 「오늘」 칩 (SagajeongDispatchDataPacks.cs)
            if (repaint) DrawGuFloatingCard();   // 유동인구 카드 (SagajeongDispatchFloating.cs)
            if (repaint) guPrimed = true;
            if (repaint) DrawGuHandoffVeil(now);     // 정밀 1: 전국에서 넘긴 직후 0.3초 동안 구 바탕으로 덮었다 걷는다(SagajeongDispatchMapContinuity.cs)

            // 직접 확대하거나 옮겼을 때만 보이는 단추: 내 영역이 다 보이는 자리로 돌아간다.
            if (guManual && !PortraitScreen && ExpansionButton(new Rect(560 - Field.x, 622 - Field.y, 160, 26), "영역 전체 보기", true, Mint))
            {
                guManual = false;
                guPressed = false;
                return;
            }
            if (DrawGuStationCard()) { guPressed = false; return; }       // 역 창 (SagajeongDispatchStationCard.cs)
            if (DrawStationInside()) { guPressed = false; return; }       // 역 속 창 (SagajeongDispatchStationInside.cs)
            if (DrawHexTileCard()) { guPressed = false; return; }         // 타일 그릇 창 (SagajeongDispatchHexTiles.cs)
            if (DrawCapitalHexCard()) { guPressed = false; return; }      // 수도권 칸 창 (SagajeongDispatchCapitalHex.cs)
            if (DrawGuSubwayOptions()) { guPressed = false; return; }      // 자료 창 · 분야별 켜고 끄기 (SagajeongDispatchDataControl.cs)
            if (repaint && guNotice != null)
            {
                if (now > guNoticeUntil) guNotice = null;
                else GuPlate(PortraitScreen ? new Vector2(Field.width * 0.5f, portraitSheetRect.y - 36f) : new Vector2(640 - Field.x, 596 - Field.y), guNotice, 12, Paper, 0.92f);
            }

            // 끌면 지도를 옮기고, 표식이나 이름표가 아닌 곳을 그냥 누르면 그 자리의 법정동을 고른다.
            if (current.button != 0) return;
            var covered = MapOverlayCovers(current.mousePosition + Field.position);
            if (current.type == EventType.MouseDown)
            {
                guPressed = !covered;
                guDragging = false;
                guPressPoint = current.mousePosition;
                guPressOrigin = guOrigin;
            }
            else if (current.type == EventType.MouseDrag && guPressed)
            {
                var moved = current.mousePosition - guPressPoint;
                if (!guDragging && moved.sqrMagnitude < 36f) return;
                guDragging = true;
                guManual = true;
                guOrigin = guPressOrigin + moved;
                ClampGu();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && guPressed)
            {
                guPressed = false;
                if (!guDragging && !covered)
                {
                    if (!dongMapOpen) expansionPanelOpen = false;      // 기본 판: 지도에서 고르면 사업 창을 걷고 영역 패널로
                    if (PickGuStation(current.mousePosition)) hexSelected = -1;
                    else if (!PickHexTile(current.mousePosition) && !PickCapitalHex(current.mousePosition)) guSelected = GuDongAt(GuWorld(current.mousePosition))?.code;      // 타일 지도에서는 타일을 고른다
                }
                guDragging = false;
                current.Use();
            }
        }

        // 큰길 이름. 길의 방향에 맞춰 눕혀 쓴다. 가까이 볼수록 작은 길까지 보이고, 동 이름표나 표식과 겹치는 것은 건너뛴다.
        private void DrawGuRoads()
        {
            foreach (var name in guRoadNames) DrawPreparedGuName(name);
        }

        private readonly Dictionary<(int, bool), GUIStyle> guTurnedStyles = new();

        // 돌려 쓰는 글자(길·하천 이름). IMGUI는 글자를 CPU에서 자르는데, 이 자르기는 GUI.matrix의 회전을 모른다.
        // 원점을 기준으로 돌리면 글자 칸이 그룹의 (0, 0) 근처 음수 좌표에 놓여 통째로 잘려 나간다(G2 첫 빌드에서 확인).
        // 그래서 글자는 제자리(point)에 두고 그 점을 중심으로 돌린다. 글자 칸으로도 자르지 않는다(Overflow).
        // halo가 있으면 네 방향으로 한 점씩 어둡게 먼저 깔아 길 위에서도 읽히게 한다.
        private void GuTurnedText(Vector2 point, float angle, string text, int size, Color color, bool bold, Color halo)
        {
            var key = (size, bold);
            if (!guTurnedStyles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = bold ? boldFont : font,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = size,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow,
                    wordWrap = false,
                    padding = new RectOffset(),
                };
                guTurnedStyles.Add(key, style);
            }
            var original = GUI.matrix;
            RotateLocal(angle, point);
            var rect = Centered(point, text.Length * size * 1.2f + 24f, size + 8f);
            if (halo.a > 0f)
            {
                style.normal.textColor = halo;
                GUI.Label(new Rect(rect.x - 1, rect.y, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x + 1, rect.y, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x, rect.y - 1, rect.width, rect.height), text, style);
                GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, rect.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            GUI.matrix = original;
        }

        private static float GuLightOf(TerritoryDongView zone) =>
            zone.Level >= TerritoryLevels.Settled ? 1f : zone.Level == TerritoryLevels.Operating ? 0.86f :
            zone.Level == TerritoryLevels.Entering ? 0.5f : zone.Candidate ? 0.16f : 0f;

        // 밝기, 경계, 배달권, 파동. 그리기만 한다(Repaint).
        private void DrawGuGround(TerritorySnapshot view, float now, float delta)
        {
            // 1) 밝기: 동마다 색 바탕을 운영 깊이만큼 덮는다. 목표 밝기로 천천히 다가간다.
            foreach (var zone in view.Dongs)
            {
                var dong = GuDongOf(zone.Code);
                if (dong?.fill == null) continue;
                var target = GuLightOf(zone);
                if (!guLight.TryGetValue(zone.Code, out var light) || !guPrimed) light = target;
                light = Mathf.MoveTowards(light, target, delta * 0.55f);
                guLight[zone.Code] = light;

                guLevelSeen.TryGetValue(zone.Code, out var seen);
                if (guPrimed && zone.Level > seen && mapEffects)     // r157: 간단히면 퍼지는 파동 없이 밝기만 바뀐다
                {
                    // 새로 진출했거나 한 단계 올랐다. 첫 표식에서 민트 파동이 한 번 퍼진다.
                    var origin = zone.Pins.Count > 0 ? new Vector2((float)zone.Pins[0].X, (float)zone.Pins[0].Y) : new Vector2(dong.x, dong.y);
                    guWaves.Add(new GuWave { X = origin.x, Y = origin.y, Start = now, Radius = Mathf.Max(dong.bounds.width, dong.bounds.height) * 0.7f, Seconds = 1.5f });
                }
                guLevelSeen[zone.Code] = zone.Level;

                if (light <= 0.004f) continue;
                var breathe = zone.Candidate ? (mapEffects ? 0.035f * Mathf.Sin(now * 2.4f) : 0f) + (guHover == zone.Code ? 0.08f : 0f) : 0f;
                GUI.DrawTexture(GuRect(dong.fillBounds), dong.fill, ScaleMode.StretchToFill, true, 0,
                    new Color(1f, 1f, 1f, Mathf.Clamp01(light + breathe)), 0, 0);
            }
            DrawGuSeason();          // 철: 동의 색 바탕 위, 표식 아래 (SagajeongDispatchGuSeason.cs)

            // 2) 지사의 배달권과 매장의 불빛
            foreach (var zone in view.Dongs)
            {
                foreach (var pin in zone.Pins)
                {
                    var age = PinAge(pin, now);
                    if (age < GuPinDrop) continue;
                    var grow = Mathf.Clamp01((age - GuPinDrop) / 0.9f);
                    var point = GuPoint((float)pin.X, (float)pin.Y);
                    if (pin.Kind == TerritoryPinKinds.Branch)
                    {
                        var radius = 1000f * guScale * (1f - Mathf.Pow(1f - grow, 3f));
                        Disc(point, radius, Fade(Mint, 0.07f));
                        Disc(point, radius * 0.55f, Fade(Mint, 0.06f));
                        Hoop(point, radius, Fade(Mint, 0.6f), 1.3f);
                    }
                    else
                    {
                        Disc(point, 380f * guScale, Fade(Gold, 0.07f * grow));
                        Disc(point, 190f * guScale, Fade(Gold, 0.08f * grow));
                    }
                }
            }

            // 3) 경계: 내 구, 후보(금색 점선), 내 동(흰 선), 고른 동, 위험이 울리는 동
            var litSigs = new HashSet<string>(view.Dongs.Where(item => item.Level > TerritoryLevels.Unknown).Select(item => item.Sig));
            foreach (var sig in guSpace.sigs)
                if (sig.rings != null && litSigs.Contains(sig.code)) DrawGuRings(sig.rings, Fade(Paper, 0.5f), 1.6f, false);
            foreach (var zone in view.Dongs)
            {
                var dong = GuDongOf(zone.Code);
                if (dong?.rings == null) continue;
                if (zone.Candidate)
                    DrawGuRings(dong.rings, Fade(Gold, guHover == zone.Code ? 1f : 0.85f), guHover == zone.Code ? 2.2f : 1.4f, true);
                else if (zone.Level <= TerritoryLevels.Unknown)
                {
                    if (guScale >= GuNearScale) DrawGuRings(dong.rings, Fade(Paper, 0.16f), 1f, false);
                }
                else if (zone.Level > TerritoryLevels.Unknown)
                {
                    DrawGuRings(dong.rings, Fade(Mint, 0.05f * zone.Level), 7f, false);
                    DrawGuRings(dong.rings, zone.Level == TerritoryLevels.Entering ? Fade(Paper, 0.55f) : Paper,
                        zone.Level >= TerritoryLevels.Settled ? 2.1f : 1.5f, zone.Level == TerritoryLevels.Entering);
                }
                if (zone.Signal != null && zone.Signal.IsDanger)
                    DrawGuRings(dong.rings, Fade(SignalRed, mapEffects ? 0.3f + 0.3f * (0.5f + 0.5f * Mathf.Sin(now * 6f)) : 0.5f), 3f, false);
                if (guSelected == zone.Code) DrawGuRings(dong.rings, Gold, 2.6f, false);
            }

            // 4) 기사: 지사 둘레를 도는 점
            foreach (var zone in view.Dongs)
            {
                foreach (var pin in zone.Pins)
                {
                    if (pin.Kind != TerritoryPinKinds.Branch || PinAge(pin, now) < 1f) continue;
                    var seed = (pin.Id.GetHashCode() & 1023) * 0.01f;
                    var clock = mapEffects ? now : 0f;       // r157 간단히: 같은 자리에 멈춘 점
                    for (var rider = 0; rider < 3; rider++)
                    {
                        var speed = 0.16f + rider * 0.05f;
                        var phase = seed + rider * 1.7f;
                        var spot = GuPoint((float)pin.X + Mathf.Cos(clock * speed + phase) * (300f + rider * 140f),
                            (float)pin.Y + Mathf.Sin(clock * speed * 1.3f + phase * 0.7f) * (240f + rider * 110f));
                        Disc(spot, 3.6f, Canvas);
                        Disc(spot, 2.3f, Mint);
                    }
                }
            }

            // 5) 파동: 신호는 계속 울리고, 새로 꽂히거나 밝아질 때는 한 번 퍼진다
            foreach (var signal in signals)
            {
                if (TryGuSignalPoint(view, signal, out var point)) DrawRipple(point + new Vector2(0, -12), signal, 1f);
            }
            if (!mapEffects) guWaves.Clear();
            for (var index = guWaves.Count - 1; index >= 0; index--)
            {
                var wave = guWaves[index];
                var spread = (now - wave.Start) / wave.Seconds;
                if (spread >= 1f)
                {
                    guWaves.RemoveAt(index);
                    continue;
                }
                if (spread < 0f) continue;
                var centre = GuPoint(wave.X, wave.Y);
                var eased = 1f - Mathf.Pow(1f - spread, 2.2f);
                Hoop(centre, wave.Radius * guScale * eased, Fade(Mint, (1f - spread) * 0.8f), 2f);
                if (spread > 0.16f) Hoop(centre, wave.Radius * guScale * (1f - Mathf.Pow(1f - (spread - 0.16f), 2.2f)), Fade(Mint, (1f - spread) * 0.4f), 1.5f);
            }
        }

        private void DrawGuRings(GuRing[] rings, Color color, float width, bool dashed)
        {
            foreach (var ring in rings)
            {
                var p = ring.p;
                if (p == null || p.Length < 4) continue;
                var previous = GuPoint(p[p.Length - 2], p[p.Length - 1]);
                for (var i = 0; i + 1 < p.Length; i += 2)
                {
                    var next = GuPoint(p[i], p[i + 1]);
                    // 화면 밖에 온전히 나간 선분은 건너뛴다.
                    if (!(Mathf.Max(previous.x, next.x) < 0 || Mathf.Min(previous.x, next.x) > Field.width ||
                          Mathf.Max(previous.y, next.y) < 0 || Mathf.Min(previous.y, next.y) > Field.height))
                        Line(previous, next, color, width, dashed);
                    previous = next;
                }
            }
        }

        // 신호가 울리는 자리: 법정동 신호는 그 동의 첫 표식, 홈 동의 신호는 그 건물의 표식.
        private bool TryGuSignalPoint(TerritorySnapshot view, Signal signal, out Vector2 point)
        {
            point = default;
            var code = SignalDong(signal);
            // 행정동 신호는 그 행정동의 가운데에서 울린다. 구 지도의 판은 법정동이지만 좌표계는 같다.
            var flagDong = code == null ? null : dongTerritory?.Find(code);
            if (flagDong != null)
            {
                point = GuPoint((float)flagDong.X, (float)flagDong.Y);
                return true;
            }
            foreach (var zone in view.Dongs)
            {
                if (code != null)
                {
                    if (zone.Code != code) continue;
                    point = zone.Pins.Count > 0 ? GuPoint((float)zone.Pins[0].X, (float)zone.Pins[0].Y) : GuPoint((float)zone.X, (float)zone.Y);
                    return true;
                }
                foreach (var pin in zone.Pins)
                {
                    if (string.IsNullOrEmpty(pin.PlaceId) || pin.PlaceId != signal.PlaceId) continue;
                    point = GuPoint((float)pin.X, (float)pin.Y);
                    return true;
                }
            }
            return false;
        }

        // 표식을 화면이 처음 본 뒤 흐른 시간. 지도를 처음 열 때 이미 있던 표식은 떨어지지 않는다.
        private float PinAge(TerritoryPinState pin, float now)
        {
            if (!guPinSeen.TryGetValue(pin.Id, out var seen))
            {
                // r157 간단히: 새 표식도 떨어지지 않고 바로 꽂힌다. 본 것으로 적어 두어 효과를 켜도 나중에 재생하지 않는다.
                seen = guPrimed && mapEffects ? now : now - 10f;
                guPinSeen[pin.Id] = seen;
                if (guPrimed && mapEffects) guWaves.Add(new GuWave { X = (float)pin.X, Y = (float)pin.Y, Start = now + GuPinDrop, Radius = 650f, Seconds = 1.1f });
            }
            return now - seen;
        }

        private static float Bounce(float t)
        {
            const float n = 7.5625f, d = 2.75f;
            if (t < 1f / d) return n * t * t;
            if (t < 2f / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        private static Color GuPinColor(string kind) =>
            kind == TerritoryPinKinds.Branch ? Mint : kind == TerritoryPinKinds.Food ? Gold : kind == TerritoryPinKinds.Cafe ? Lilac : Sky;

        // 표식. 가까이 모인 것은 하나로 묶고 수를 적는다(지사가 있으면 지사가 대표다).
        private void DrawGuPins(TerritorySnapshot view, float now, bool repaint)
        {
            guClusters.Clear();
            foreach (var zone in view.Dongs)
            {
                foreach (var pin in zone.Pins)
                {
                    var point = GuPoint((float)pin.X, (float)pin.Y);
                    var seen = now - PinAge(pin, now);
                    GuCluster home = null;
                    foreach (var cluster in guClusters)
                    {
                        if ((cluster.Point - point).sqrMagnitude > GuClusterRadius * GuClusterRadius) continue;
                        home = cluster;
                        break;
                    }
                    if (home == null)
                    {
                        guClusters.Add(new GuCluster { Point = point, Lead = pin, Dong = zone, Count = 1, Seen = seen });
                        continue;
                    }
                    home.Count++;
                    home.Seen = Mathf.Min(home.Seen, seen);
                    if (pin.Kind == TerritoryPinKinds.Branch && home.Lead.Kind != TerritoryPinKinds.Branch)
                    {
                        home.Lead = pin;
                        home.Point = point;
                    }
                }
            }
            guClusters.Sort((a, b) => a.Point.y.CompareTo(b.Point.y));     // 위쪽 것부터 그려 아래쪽이 덮는다

            var pointer = Event.current.mousePosition;
            var free = !MapOverlayCovers(pointer + Field.position);
            GuCluster hovered = null;
            foreach (var cluster in guClusters)
            {
                var big = cluster.Lead.Kind == TerritoryPinKinds.Branch;
                var radius = big ? 13f : 9.5f;
                var age = now - cluster.Seen;
                if (age < 0f) continue;
                var fall = age < GuPinDrop ? -90f * (1f - Bounce(age / GuPinDrop)) : 0f;
                var tip = cluster.Point + new Vector2(0, fall);
                var area = new Rect(cluster.Point.x - radius - 2, cluster.Point.y - radius * 2 - 9, radius * 2 + 4, radius * 2 + 11);
                if (repaint)
                {
                    Paint(new Rect(cluster.Point.x - 5, cluster.Point.y - 1.5f, 10, 4), Fade(Color.black, 0.35f * Mathf.Clamp01(age / GuPinDrop)), 2);
                    DrawGuPin(tip, cluster.Lead.Kind, big, Mathf.Clamp01(age * 4f / GuPinDrop));
                    if (cluster.Count > 1)
                    {
                        var badge = new Vector2(tip.x + radius * 0.9f, tip.y - radius * 2 - 6);
                        Disc(badge, 8.5f, Canvas);
                        Disc(badge, 7.2f, Paper);
                        Text(Centered(badge, 18, 16), cluster.Count.ToString(), 10, Canvas, true, TextAnchor.MiddleCenter);
                    }
                    foreach (var signal in signals)
                    {
                        if (!TryGuSignalPoint(view, signal, out var at) || (at - cluster.Point).sqrMagnitude > GuClusterRadius * GuClusterRadius) continue;
                        DrawSignalBadge(new Vector2(tip.x - radius * 0.9f, tip.y - radius * 2 - 6), signal);
                        break;
                    }
                }
                if (repaint && guScale >= 0.2f && cluster.Count == 1 && age >= GuPinDrop)
                    GuPlate(cluster.Point + new Vector2(0, 11), cluster.Lead.Title, 10, GuPinColor(cluster.Lead.Kind), 0.72f);
                if (free && area.Contains(pointer)) hovered = cluster;
                if (!free || !Hit(area)) continue;
                // 홈 동의 표식은 동네 지도의 그 건물로 내려간다. 그 밖의 표식은 그 동을 고른다.
                if (cluster.Dong.IsHome)
                {
                    if (cluster.Count == 1 && !string.IsNullOrEmpty(cluster.Lead.PlaceId)) SelectPlace(cluster.Lead.PlaceId);
                    else ReturnToDong();
                    return;
                }
                guSelected = cluster.Dong.Code;
            }
            if (repaint && hovered != null)
            {
                var title = hovered.Count > 1 ? $"{hovered.Lead.Title} 외 {hovered.Count - 1}곳" : hovered.Lead.Title;
                GuPlate(hovered.Point + new Vector2(0, -44 - (hovered.Lead.Kind == TerritoryPinKinds.Branch ? 8 : 0)), title, 11, GuPinColor(hovered.Lead.Kind), 0.94f);
            }
        }

        private void DrawGuPin(Vector2 tip, string kind, bool big, float alpha)
        {
            var color = GuPinColor(kind);
            var radius = big ? 13f : 9.5f;
            var centre = new Vector2(tip.x, tip.y - radius - 7f);
            var body = Fade(big ? color : Canvas, alpha);
            var edge = Fade(big ? Canvas : color, alpha);
            var side = radius * 0.95f;
            var pivot = new Vector2(tip.x, tip.y - side * 0.7071f);
            // 테두리를 먼저 다 깔고(꼬리 + 머리) 그 위에 속을 채운다. 꼬리는 45도 돌린 네모다.
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(pivot, 45f);        // 창 비율이 달라도 꼬리가 제자리에 오게 그룹 위치를 더해 돈다
            Paint(new Rect(-side / 2 - 1.3f, -side / 2 - 1.3f, side + 2.6f, side + 2.6f), edge);
            GUI.matrix = original;
            Disc(centre, radius + 1.3f, edge);
            GUI.matrix = original * TurnAt(pivot, 45f);
            Paint(new Rect(-side / 2, -side / 2, side, side), body);
            GUI.matrix = original;
            Disc(centre, radius, body);

            var ink = Fade(big ? Canvas : color, alpha);
            var u = radius * 0.5f;
            if (kind == TerritoryPinKinds.Branch)
            {
                Paint(new Rect(centre.x - u * 0.75f, centre.y - u, 1.7f, u * 2f), ink);                       // 깃대
                Paint(new Rect(centre.x - u * 0.75f + 1.7f, centre.y - u, u * 1.45f, u * 0.95f), ink, 1.5f);   // 깃발
            }
            else if (kind == TerritoryPinKinds.Food)
            {
                Paint(new Rect(centre.x - u * 1.15f, centre.y - u * 0.6f, u * 2.3f, 1.6f), ink);               // 그릇 테
                Paint(new Rect(centre.x - u, centre.y - u * 0.25f, u * 2f, u * 1.1f), ink, u * 0.55f);          // 그릇
            }
            else if (kind == TerritoryPinKinds.Cafe)
            {
                Paint(new Rect(centre.x - u * 0.95f, centre.y - u * 0.75f, u * 1.3f, u * 1.5f), ink, 1.5f);    // 컵
                Hoop(new Vector2(centre.x + u * 0.55f, centre.y - u * 0.05f), u * 0.5f, ink, 1.4f);             // 손잡이
            }
            else
            {
                Paint(new Rect(centre.x - u * 1.1f, centre.y - u * 0.8f, u * 2.2f, u * 0.5f), ink, 1f);         // 지붕
                Paint(new Rect(centre.x - u * 0.85f, centre.y - u * 0.15f, u * 1.7f, u * 1.0f), ink, 1f);       // 창고 몸
            }
        }

        private Rect GuPlate(Vector2 centre, string text, int size, Color color, float back)
        {
            var rect = GuNamePlateRect(centre, text, size);
            Paint(rect, Fade(Canvas, back), 6);
            Text(rect, text, size, color, true, TextAnchor.MiddleCenter);
            return rect;
        }

        // 산·하천과 주변 구 이름도 도로·동 이름과 같은 실제 글자 점유를 따른다.
        private void DrawGuNames(float now)
        {
            foreach (var name in guNaturalNames) DrawPreparedGuName(name);
        }

        // 실제로 그릴 수 있는 이름표만 같은 사각형으로 선택한다. 숨긴 글자의 빈 선택 영역은 만들지 않는다.
        private void DrawGuLabels(TerritorySnapshot view)
        {
            var free = !MapOverlayCovers(Event.current.mousePosition + Field.position);
            foreach (var zone in view.Dongs)
            {
                if (!guDongNameRects.TryGetValue(zone.Code, out var rect)) continue;
                var unknown = zone.Level <= TerritoryLevels.Unknown && !zone.Candidate;
                var color = unknown ? Fade(Soft, 0.7f) : zone.Candidate ? Gold
                    : zone.Level == TerritoryLevels.Entering ? Fade(Paper, 0.78f) : Paper;
                var back = unknown ? 0.5f : zone.Candidate && guHover == zone.Code ? 0.95f : 0.8f;
                Paint(rect, Fade(Canvas, back), 6);
                Text(rect, zone.Candidate ? "+ " + zone.Name : zone.Name, unknown || zone.Candidate ? 11 : 12,
                    color, true, TextAnchor.MiddleCenter);
                if (free && Hit(rect)) guSelected = zone.Code;
            }
            foreach (var sig in guSpace.sigs)
            {
                if (!guSigNameRects.TryGetValue(sig.code, out var rect)) continue;
                Paint(rect, Fade(Canvas, 0.7f), 6);
                Text(rect, sig.name, 17, Paper, true, TextAnchor.MiddleCenter);
                if (free && Hit(rect)) OpenDongPanel(sig.name);
            }
        }

        // ── 영역 패널 ─────────────────────────────────────────────────────────────

        private void DrawTerritoryPanel()
        {
            var view = territory;
            var panel = ExpansionPanel;
            var x = panel.x + 17;
            var y = panel.y;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            Text(new Rect(x, y + 13, 238, 20), "넓어지는 영역 · 밝은 곳이 운영하는 곳", 11, Gold, true);
            Text(new Rect(x, y + 38, 238, 28), $"운영 {view.Operating}동" + (view.Entering > 0 ? $" · 진출 {view.Entering}동" : string.Empty), 21, Paper, true);
            Text(new Rect(x, y + 68, 238, 18), $"지사 {view.Branches} · 음식점 {view.Foods} · 카페 {view.Cafes} · 창고 {view.Stores}", 11, Soft);

            for (var index = 0; index < GuLegend.Length; index++)
            {
                var swatch = new Rect(x + index * 48, y + 96, 44, 12);
                if (index == 1) Paint(new Rect(swatch.x - 1, swatch.y - 1, swatch.width + 2, swatch.height + 2), Fade(Gold, 0.9f), 4);
                Paint(swatch, GuLegend[index], 3);
                Text(new Rect(swatch.x - 4, swatch.y + 13, 52, 14), GuLegendNames[index], 9, Soft, align: TextAnchor.MiddleCenter);
            }

            var box = new Rect(x, y + 132, 238, 190);
            Paint(box, Tile, 7);
            var chosen = guSelected == null ? null : view.Find(guSelected);
            var act = new Rect(x, y + 334, 238, 36);
            if (chosen == null)
            {
                Text(new Rect(box.x + 11, box.y + 8, 216, 18), "다음에 갈 수 있는 곳", 11, Gold, true);
                var row = 0;
                foreach (var zone in view.Dongs)
                {
                    if (!zone.Candidate || row >= 6) continue;
                    var line = new Rect(box.x + 6, box.y + 31 + row * 25, 226, 23);
                    if (guHover == zone.Code) Paint(line, Panel, 5);
                    Text(new Rect(line.x + 6, line.y, 110, line.height), "+ " + zone.Name, 12, Gold, true);
                    Text(new Rect(line.x + 110, line.y, 110, line.height), zone.SigName, 10, Soft, align: TextAnchor.MiddleRight);
                    if (Hit(line)) guSelected = zone.Code;
                    row++;
                }
                if (row == 0) Text(new Rect(box.x + 11, box.y + 34, 216, 40), "지금은 맞닿은 후보가 없습니다.", 11, Soft, wrap: true);
                ExpansionButton(act, "지도에서 동을 눌러 고르세요", false, Tile);
            }
            else
            {
                var danger = chosen.Signal != null && chosen.Signal.IsDanger;
                var tone = chosen.Signal != null ? SignalColor(chosen.Signal) : chosen.Candidate ? Gold : chosen.Level > TerritoryLevels.Unknown ? Mint : Soft;
                Text(new Rect(box.x + 11, box.y + 8, 150, 22), chosen.Name, 16, Paper, true);
                Text(new Rect(box.x + 120, box.y + 8, 107, 22), chosen.SigName, 10, Soft, align: TextAnchor.MiddleRight);
                var stage = chosen.Candidate ? "후보 · 맞닿은 동" : chosen.Level > TerritoryLevels.Unknown ? TerritoryLevels.Name(chosen.Level) : "미지 · 아직 닿지 않음";
                Text(new Rect(box.x + 11, box.y + 32, 216, 18), stage, 12, chosen.Candidate ? Gold : chosen.Level > TerritoryLevels.Unknown ? Mint : Soft, true);
                if (chosen.Level == TerritoryLevels.Entering || chosen.Level == TerritoryLevels.Operating)
                {
                    var bar = new Rect(box.x + 11, box.y + 56, 216, 7);
                    Paint(bar, Panel, 4);
                    Paint(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01((float)chosen.Progress), bar.height), Mint, 4);
                    Text(new Rect(box.x + 11, box.y + 65, 216, 16),
                        TerritoryLevels.Name(chosen.Level + 1) + "까지 " + Mathf.RoundToInt((float)chosen.Progress * 100f) + "%", 10, Soft);
                }
                var message = chosen.Signal != null ? (danger ? "위험 · " : "주의 · ") + chosen.Signal.Message
                    : chosen.Candidate ? (chosen.CanEnter ? "진출하면 첫 음식점이 들어서고, 시간이 지나면 운영 단계로 오릅니다." : chosen.EnterBlock)
                    : chosen.Level <= TerritoryLevels.Unknown ? chosen.EnterBlock
                    : chosen.IsHome ? "처음 시작한 동네입니다. 표식을 누르면 동네 지도의 그 건물로 내려갑니다."
                    : chosen.Level >= TerritoryLevels.Settled ? "자리를 잡았습니다. 신호를 놓치면 한 단계 어두워집니다."
                    : "위험 신호가 열려 있는 동안에는 깊어지지 않습니다. 놓치면 한 단계 어두워집니다.";
                Text(new Rect(box.x + 11, box.y + 86, 216, 52), message, 11, chosen.Signal != null ? tone : Paper, wrap: true);
                var pins = chosen.Pins.Count == 0 ? "표식 없음" : string.Join(" · ", chosen.Pins.Take(4).Select(pin => pin.Title)) + (chosen.Pins.Count > 4 ? " …" : string.Empty);
                Text(new Rect(box.x + 11, box.y + 142, 216, 40), pins, 10, Soft, wrap: true);

                if (chosen.Signal != null && !chosen.IsHome)
                {
                    if (ExpansionButton(act, string.IsNullOrEmpty(chosen.Signal.ActionLabel) ? "신호 처리" : chosen.Signal.ActionLabel, true, Mint))
                        SendTerritory("respond", chosen.Code);
                }
                else if (chosen.IsHome)
                {
                    if (!dongMapOpen) ExpansionButton(act, "운영하는 곳", false, Tile);      // 기본 판: 내려갈 동네 지도가 없다
                    else if (ExpansionButton(act, "동네 지도로 내려가기", true, Mint)) ReturnToDong();
                }
                else if (chosen.Level <= TerritoryLevels.Unknown)
                {
                    if (ExpansionButton(act, chosen.CanEnter ? chosen.Name + "에 진출하기" : "아직 진출할 수 없습니다", chosen.CanEnter, Mint))
                        SendTerritory("enter", chosen.Code);
                }
                else
                    ExpansionButton(act, TerritoryLevels.Name(chosen.Level) + " · 지켜보는 중", false, Tile);
            }

            var note = !string.IsNullOrEmpty(view.LastNote) && view.NowMs - view.LastNoteAtMs < 600000 ? view.LastNote
                : "임시 규칙입니다. 가상 매장에는 아직 주문과 정산이 없고, 시간이 흐른 만큼 깊어집니다.";
            Text(new Rect(x, y + 378, 238, 44), note, 10, Soft, align: TextAnchor.UpperLeft, wrap: true);
        }
    }
}
