using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 구와 동 사이의 타일 지도 (2026-11-22 사용자: 「구단이랑 동단 사이에 계층을 하나 … 동 단위 지형을 타일화해서 특색을 살리고,
    // 타일을 더블 클릭하면 동 단위 모듈로」 → 시안을 본 뒤 「나쁘지 않네. 구현 작업 진행해줘봐.」).
    //  - 구 지도에서 휠로 다가가면 같은 자리에 정육각 타일(이웃 사이 300 m)이 떠오른다. 멀리서는 예전 구 지도 그대로다.
    //  - 자료: Resources/hongdal-tiles-gu.json (hex-tiles-20261122-r1). 굽는 도구와 기록은
    //    Hongdal artifacts/local/hongdal-unity-claude-20261112/hex-tiles-20261122-r1/.
    //    타일마다의 수와 비율뿐이다(건물 모양 · 주소 · 상호 · 원천 행 번호 없음). 내부 검토용 · 배포 아님.
    //  - 타일 번호 = UTM-K(EPSG:5179) 한 평면의 축 좌표 (q, r). 화면에는 팩에 적힌 가운데(구 지도 미터)로 그린다.
    //  - 한 번 누르면 왼쪽에 그 타일의 그릇 창. 두 번 누르면 집 동(타일 가운데가 든 행정동)의 디오라마로 내려간다.
    //    기본 판(1.0)은 동 지도가 닫혀 있어 안내만 한다(판Policy.cs). 동에서 휠로 올라오면 같은 타일로 돌아온다.
    //  - 역 영향권: 고른 역(없으면 사가정역)의 타일에서 1 · 2 · 3겹을 금색 테두리로 그린다. 겹 넓히기 규칙은 아직 없다 — 보기만.
    // 보기와 고르기만 더한다. 영역 · 주문 · 돈 규칙을 읽거나 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string HexTilesResource = "hongdal-tiles-gu";
        private const string HexHomeStation = "사가정역";
        private const string HexRegionPrefix = "region:kr:hjd:";
        private const float HexFadeStart = 0.17f;       // 이 배율부터 타일이 떠오른다(화면 1점 ≈ 5.9 m)
        private const float HexFadeFull = 0.22f;        // 이 배율에서 다 드러난다(타일 한 칸 ≈ 66점)
        private const float HexLabelScale = 0.27f;      // 이보다 가까우면 칸마다 성격 이름을 적는다
        private const float HexDoubleSeconds = 0.45f;   // 같은 칸을 이 안에 다시 누르면 두 번 누르기
        private const float HexReturnScale = 0.26f;     // 동에서 올라와 타일로 돌아올 때의 배율
        private const int HexSpriteRadius = 74;         // 육각 그림의 외접 반지름(텍셀)
        private const float HexGap = 1.2f;              // 칸 사이 틈(화면 점, 반지름에서 뺀다)

        [Serializable]
        private sealed class HexPack
        {
            public string schema;
            public string revision;
            public string rule;
            public string label;
            public string grid;
            public float spacing;
            public float radius;
            public float guRadius;       // 구 지도 미터로 잰 외접 반지름
            public float guTurn;         // 격자의 동쪽 이웃이 구 지도의 동쪽에서 돈 각(도)
            public string[] sources;
            public string[] kinds;
            public string[] uses;
            public string[] greens;
            public string[] foods;
            public int busyRestaurants;
            public HexDong[] dongs;
            public HexTile[] tiles;
        }

        [Serializable]
        private sealed class HexDong
        {
            public string code;
            public string name;
            public string gu;
            public long pop;
        }

        [Serializable]
        private sealed class HexTile
        {
            public int q;
            public int r;
            public float x;          // 가운데(구 지도 미터, 남서쪽 모서리 기준)
            public float y;
            public int k;            // 성격(kinds의 자리)
            public int d = -1;       // 집 동(dongs의 자리), 없으면 -1
            public int d2 = -1;      // 크게(4분의 1 넘게) 걸친 동
            public int w;            // 서울 자료가 덮는 몫(‰). 500 아래면 자료 준비 전
            public int b;            // 건물 수
            public int c;            // 덮인 몫(‰)
            public int f;            // 평균 층 × 10
            public int e;            // 연면적 / 땅(%)
            public int t;            // 15층 넘는 건물
            public int[] u;          // 쓰임별 연면적 몫(‰): 단독 · 공동 · 상업 · 공공 · 공업 · 교통 · 기타
            public int[] g;          // 땅의 몫(‰): 하천 · 산림 · 조경녹지 · 경작지
            public string s;         // 역 이름(여럿이면 ·로)
            public int n = -1;       // 음식점 수, 모르면 -1
            public int[] m;          // 갈래별 음식점 수(foods 차례)
            public int p = -1;       // 사는 사람(추정), 모르면 -1
            public int[] lp;         // 지나는 사람(서울 생활인구 250m → 타일): 평일 점심 · 평일 저녁 · 주말 점심 · 주말 저녁(시간 평균, 명). 없으면 null
        }

        // 성격 색. 차례는 팩의 kinds와 같다:
        // 역 · 물 · 산 · 들 · 공원 · 빈터 · 공장·창고 · 아파트 · 번화가 · 학교·공공 · 골목 · 섞인 동네 · 자료 준비 전
        // 시안의 색에서 아파트(보라)와 학교·공공(분홍)만 바꿨다 — 게임 화면에서 아파트가 물(파랑)과 같아 보였다.
        private static readonly Color[] HexKindColours =
        {
            Ink(0xFFD395), Ink(0x4FA3E0), Ink(0x469664), Ink(0xB5C46A), Ink(0x6FCF97), Ink(0x5A6976), Ink(0x96A5B1),
            Ink(0x9A8CFF), Ink(0xFF9664), Ink(0xE58FCB), Ink(0xE9C46A), Ink(0xBEAA8C), Ink(0x3C4854),
        };
        // 쓰임 색: 단독 · 공동 · 상업 · 공공 · 공업 · 교통 · 기타 (성격 색과 맞춘다)
        private static readonly Color[] HexUseColours =
            { Ink(0xE9C46A), Ink(0x9A8CFF), Ink(0xFF9664), Ink(0xE58FCB), Ink(0x96A5B1), Ink(0x6FA8FF), Ink(0x5A6976) };
        // 여섯 이웃(축 좌표). i번 이웃과 맞닿은 변은 꼭짓점 (i+5)%6 → i 이다(꼭짓점 k는 60k+30도).
        private static readonly (int Q, int R)[] HexDirections = { (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1) };

        private HexPack hexPack;
        private bool hexTried;
        private readonly Dictionary<long, int> hexIndex = new();
        private readonly List<int> hexStationIndex = new();
        private readonly Dictionary<string, int> hexStationTiles = new(StringComparer.Ordinal);
        private float hexRadiusGu = 173.2f;
        private float hexTurn;
        private Vector2 hexEast;
        private Vector2 hexNorthEast;
        private Texture2D hexFill;
        private Texture2D hexRing;
        private Texture2D hexHatch;
        private int hexSelected = -1;
        private int hexHover = -1;
        private int hexLastClickTile = -1;
        private float hexLastClickAt = -10f;
        private int hexReturnTile = -1;
        private string hexReturnRegion;
        private double hexReturnLatitude;            // 내려간 타일의 가운데(경위도) — 동 화면에서 그 자리에 다가가고 테두리를 그린다
        private double hexReturnLongitude;
        private int hexFocusPending;                 // 동 화면이 한두 번 그려진 뒤(2.5D 시점이 선 뒤) 타일 자리로 다가간다
        private string hexNoticeShown;               // 촬영이 읽는다: 두 번 누르기에 띄운 안내

        private HexPack EnsureHexTiles()
        {
            if (hexTried) return hexPack;
            hexTried = true;
            var asset = Resources.Load<TextAsset>(HexTilesResource);
            if (asset == null)
            {
                Debug.Log("Hongdal hex tiles: 타일 팩이 없습니다");
                return null;
            }
            var started = Time.realtimeSinceStartup;
            HexPack pack = null;
            try { pack = JsonUtility.FromJson<HexPack>(asset.text); }
            catch (Exception error) { Debug.LogWarning("Hongdal hex tiles: " + error.Message); }
            Resources.UnloadAsset(asset);
            if (pack?.tiles == null || pack.tiles.Length == 0 || pack.kinds == null || pack.guRadius <= 0f)
            {
                Debug.LogWarning("Hongdal hex tiles: 타일 팩을 읽지 못했습니다");
                return null;
            }
            hexIndex.Clear();
            hexStationIndex.Clear();
            for (var i = 0; i < pack.tiles.Length; i++)
            {
                var tile = pack.tiles[i];
                if (tile == null) continue;
                hexIndex[HexKey(tile.q, tile.r)] = i;
                if (tile.k == 0 && tile.w >= 500) hexStationIndex.Add(i);
            }
            hexRadiusGu = pack.guRadius;
            hexTurn = pack.guTurn;
            var spacing = pack.guRadius * Mathf.Sqrt(3f);
            hexEast = new Vector2(Mathf.Cos(hexTurn * Mathf.Deg2Rad), Mathf.Sin(hexTurn * Mathf.Deg2Rad)) * spacing;
            hexNorthEast = new Vector2(Mathf.Cos((hexTurn + 60f) * Mathf.Deg2Rad), Mathf.Sin((hexTurn + 60f) * Mathf.Deg2Rad)) * spacing;
            hexPack = pack;
            Debug.Log($"Hongdal hex tiles: {pack.revision} · 타일 {pack.tiles.Length} · 동 {pack.dongs?.Length ?? 0} · 역 칸 {hexStationIndex.Count} · " +
                $"준비 {(Time.realtimeSinceStartup - started) * 1000f:0}ms");
            return pack;
        }

        private static long HexKey(int q, int r) => ((long)q << 32) ^ (uint)r;

        private static int HexDistance(int dq, int dr) => (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;

        // 타일 겹이 드러난 정도(0~1). 구 지도에서 다가갈수록 떠오른다.
        private float HexTilesAlpha
        {
            get
            {
                if (!GuMapVisible || !guCameraSet || guScale < HexFadeStart || !hexLayerOn || EnsureHexTiles() == null) return 0f;
                if (BranchBoardOn) return 0f;      // 지사장 판은 공개판 칸만 쓴다 (SagajeongDispatchBranchBoard.cs)
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HexFadeStart, HexFadeFull, guScale));
            }
        }

        private bool HexTilesActive => HexTilesAlpha >= 0.5f;

        private HexDong HexDongOf(int index) =>
            hexPack?.dongs != null && index >= 0 && index < hexPack.dongs.Length ? hexPack.dongs[index] : null;

        private string HexKindName(int kind) =>
            hexPack?.kinds != null && kind >= 0 && kind < hexPack.kinds.Length ? hexPack.kinds[kind] : "?";

        private static Color HexKindColour(int kind) =>
            kind >= 0 && kind < HexKindColours.Length ? HexKindColours[kind] : HexKindColours[HexKindColours.Length - 1];

        // 구 지도 미터의 한 점이 든 타일. 축 좌표를 어림한 뒤 그 칸과 여섯 이웃 가운데 가운데가 가장 가까운 칸을 고른다.
        private int HexTileAt(Vector2 world)
        {
            var pack = EnsureHexTiles();
            if (pack == null) return -1;
            var origin = pack.tiles[0];
            var local = world - new Vector2(origin.x, origin.y);
            var det = hexEast.x * hexNorthEast.y - hexEast.y * hexNorthEast.x;
            if (Mathf.Abs(det) < 1f) return -1;
            var qf = (local.x * hexNorthEast.y - local.y * hexNorthEast.x) / det;
            var rf = (hexEast.x * local.y - hexEast.y * local.x) / det;
            var sf = -qf - rf;
            int q = Mathf.RoundToInt(qf), r = Mathf.RoundToInt(rf), s = Mathf.RoundToInt(sf);
            float dq = Mathf.Abs(q - qf), dr = Mathf.Abs(r - rf), ds = Mathf.Abs(s - sf);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            q += origin.q;
            r += origin.r;
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = -1; i < HexDirections.Length; i++)
            {
                var step = i < 0 ? (Q: 0, R: 0) : HexDirections[i];
                if (!hexIndex.TryGetValue(HexKey(q + step.Q, r + step.R), out var index)) continue;
                var tile = pack.tiles[index];
                var distance = (new Vector2(tile.x, tile.y) - world).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }
            return bestDistance <= hexRadiusGu * hexRadiusGu * 1.05f ? best : -1;
        }

        // 영향권을 그릴 역의 타일: 고른 역, 없으면 사가정역.
        private int HexInfluenceTile(out string name)
        {
            name = guStation ?? HexHomeStation;
            if (hexStationTiles.TryGetValue(name, out var cached)) return cached;
            if (guSpace == null || EnsureHexTiles() == null) return -1;      // 구 지도와 타일 팩이 선 뒤에만 적어 둔다
            var index = -1;
            var pack = EnsureSubwayGu();
            if (pack?.stations != null)
                foreach (var station in pack.stations)
                {
                    if (station?.name != name) continue;
                    index = HexTileAt(이어지는지도Policy.구미터(station.longitude, station.latitude, guSpace.west, guSpace.south));
                    break;
                }
            hexStationTiles[name] = index;
            return index;
        }

        // ── 그림 ─────────────────────────────────────────────────────────────

        private void EnsureHexSprites()
        {
            if (hexFill != null) return;
            hexFill = MakeHexSprite("Hongdal hex fill", 0f, false);
            hexRing = MakeHexSprite("Hongdal hex ring", 7f, false);
            hexHatch = MakeHexSprite("Hongdal hex hatch", 0f, true);
        }

        // 꼭짓점이 위인 정육각(외접 반지름 HexSpriteRadius 텍셀). ring > 0이면 그 두께의 테두리만, hatch면 빗금.
        private static Texture2D MakeHexSprite(string name, float ring, bool hatch)
        {
            var width = Mathf.CeilToInt(HexSpriteRadius * Mathf.Sqrt(3f)) + 2;
            var height = HexSpriteRadius * 2 + 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width * height];
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            var inner = HexSpriteRadius * Mathf.Sqrt(3f) * 0.5f;     // 가운데에서 변까지
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var px = x + 0.5f - cx;
                    var py = y + 0.5f - cy;
                    var edge = float.MinValue;
                    for (var k = 0; k < 6; k++)
                    {
                        var angle = k * Mathf.PI / 3f;      // 변의 법선: 0 · 60 · 120 … 도 (꼭짓점이 위)
                        edge = Mathf.Max(edge, px * Mathf.Cos(angle) + py * Mathf.Sin(angle));
                    }
                    var outside = edge - inner;
                    var alpha = Mathf.Clamp01(0.5f - outside);
                    if (ring > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(outside + ring + 0.5f));
                    if (hatch && alpha > 0f) alpha *= (x + y) % 12 < 4 ? 0.95f : 0.22f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // DrawGuMap이 영역 바탕(DrawGuGround) 뒤, 지하철 앞에 부른다(Repaint).
        private void DrawGuHexTiles(float now)
        {
            var alpha = HexTilesAlpha;
            if (alpha <= 0.003f)
            {
                hexHover = -1;
                return;
            }
            var pack = hexPack;
            EnsureHexSprites();
            var current = Event.current;
            var free = !guDragging && !MapOverlayCovers(current.mousePosition + Field.position) && !HexCardCovers(current.mousePosition) &&
                !(GuStationNow() != null && GuStationCardRect.Contains(current.mousePosition));
            hexHover = free && alpha >= 0.5f ? HexTileAt(GuWorld(current.mousePosition)) : -1;
            var radius = hexRadiusGu * guScale;
            var size = Mathf.Max(0.5f, radius - HexGap) / HexSpriteRadius;
            var width = hexFill.width * size;
            var height = hexFill.height * size;
            var view = new Rect(-width, -height, Field.width + width * 2f, Field.height + height * 2f);
            for (var i = 0; i < pack.tiles.Length; i++)
            {
                var tile = pack.tiles[i];
                var at = GuPoint(tile.x, tile.y);
                if (!view.Contains(at)) continue;
                var ready = tile.w >= 500;
                var strength = !ready ? 0.55f : tile.k == 0 ? 0.46f : tile.k <= 4 ? 0.36f : 0.32f;      // 물 · 산 · 들 · 공원은 조금 짙게
                if (i == hexHover) strength += 0.12f;
                if (i == hexSelected) strength += 0.2f;
                if (ready && HexYieldNow != null) strength = Mathf.Min(0.82f, strength + 0.30f);      // 타일 수치 입장: 합의 색(SagajeongDispatchHexYields.cs)
                GUI.DrawTexture(new Rect(at.x - width / 2f, at.y - height / 2f, width, height), ready ? hexFill : hexHatch,
                    ScaleMode.StretchToFill, true, 0, Fade(HexTileFill(i, tile), strength * alpha), 0, 0);
            }
            // 테두리: 역 칸 · 가리킨 칸 · 영향권 · 고른 칸
            foreach (var index in hexStationIndex) DrawHexRing(pack.tiles[index], Fade(Gold, 0.7f * alpha), 0f);
            if (hexHover >= 0 && hexHover != hexSelected) DrawHexRing(pack.tiles[hexHover], Fade(Paper, 0.75f * alpha), 0f);
            DrawHexInfluence(now, alpha);
            if (hexSelected >= 0 && hexSelected < pack.tiles.Length)
            {
                var pulse = 0.85f + 0.15f * Mathf.Sin(now * 3f);
                DrawHexRing(pack.tiles[hexSelected], Fade(Canvas, 0.85f * alpha), 2.5f);
                DrawHexRing(pack.tiles[hexSelected], Fade(Gold, pulse * alpha), 0f);
            }
            // 가까이에서는 칸마다 성격 이름(역 칸은 역 이름이 따로 선다)
            if (guScale < HexLabelScale) return;
            var labelAlpha = alpha * Mathf.InverseLerp(HexLabelScale, HexLabelScale + 0.03f, guScale);
            if (DrawHexYieldLabels(labelAlpha, radius)) return;      // 수치 입장이면 칸마다 수치 세 줄
            for (var i = 0; i < pack.tiles.Length; i++)
            {
                var tile = pack.tiles[i];
                if (tile.k == 0 && tile.w >= 500) continue;
                var at = GuPoint(tile.x, tile.y);
                if (at.x < -44f || at.y < -20f || at.x > Field.width + 44f || at.y > Field.height + 20f) continue;
                var name = tile.w < 500 ? "준비 전" : HexKindName(tile.k);
                Text(new Rect(at.x - 44f, at.y + radius * 0.36f, 88f, 14f), name, 10, Fade(Paper, 0.66f * labelAlpha), false, TextAnchor.MiddleCenter);
            }
        }

        private void DrawHexRing(HexTile tile, Color colour, float grow)
        {
            var at = GuPoint(tile.x, tile.y);
            var size = Mathf.Max(0.5f, hexRadiusGu * guScale - HexGap + grow) / HexSpriteRadius;
            var width = hexRing.width * size;
            var height = hexRing.height * size;
            GUI.DrawTexture(new Rect(at.x - width / 2f, at.y - height / 2f, width, height), hexRing, ScaleMode.StretchToFill, true, 0, colour, 0, 0);
        }

        // 역 영향권 1 · 2 · 3겹(보기만). 3겹은 굵은 금색, 1 · 2겹은 옅은 금색.
        private void DrawHexInfluence(float now, float alpha)
        {
            var centre = HexInfluenceTile(out _);
            if (centre < 0) return;
            var origin = hexPack.tiles[centre];
            var breath = 0.8f + 0.2f * Mathf.Sin(now * 2f);
            for (var ring = 1; ring <= 3; ring++)
            {
                var strong = ring == 3;
                for (var dq = -ring; dq <= ring; dq++)
                    for (var dr = Mathf.Max(-ring, -dq - ring); dr <= Mathf.Min(ring, -dq + ring); dr++)
                        for (var side = 0; side < HexDirections.Length; side++)
                        {
                            var step = HexDirections[side];
                            if (HexDistance(dq + step.Q, dr + step.R) <= ring) continue;
                            var a = HexCorner(origin, dq, dr, (side + 5) % 6);
                            var b = HexCorner(origin, dq, dr, side);
                            if (strong)
                            {
                                HexLine(a, b, Fade(Gold, 0.14f * breath * alpha), 10f);      // 번짐
                                HexLine(a, b, Fade(Canvas, 0.85f * alpha), 5.5f);
                                HexLine(a, b, Fade(Gold, (0.85f + 0.15f * breath) * alpha), 3f);
                            }
                            else HexLine(a, b, Fade(Gold, 0.5f * alpha), 1.6f);
                        }
            }
            // 3겹 아랫변 밑에 이름표(위쪽은 알림 띠에 가린다)
            var bottom = new Vector2(origin.x, origin.y) + hexEast * 1.5f + hexNorthEast * -3f - new Vector2(0f, hexRadiusGu + 70f);
            var at = GuPoint(bottom.x, bottom.y);
            if (at.x < -60f || at.x > Field.width + 60f || at.y < 150f - Field.y || at.y > 576f - Field.y) return;    // 아랫변이 화면 아래(알림 · 단추 줄)로 가면 이름표는 쉰다
            at.x = Mathf.Clamp(at.x, 420f - Field.x, 900f - Field.x);
            var title = $"{(guStation ?? HexHomeStation)} 영향권 · 3겹";
            var width = title.Length * 10.5f + 18f;
            var plate = new Rect(at.x - width / 2f, at.y - 11f, width, 20f);
            Paint(plate, Fade(Canvas, 0.88f * alpha), 6);
            Paint(new Rect(plate.x, plate.y, 3f, plate.height), Fade(Gold, alpha), 1);
            Text(plate, title, 10, Fade(Gold, alpha), true, TextAnchor.MiddleCenter);
        }

        // 육각 변 긋기. 지도의 Line(SagajeongDispatchMap.cs)은 돌린 좌표에서 선의 절반(위쪽)이 지도 묶음의 잘림에 걸려 폭이 반쯤으로 보인다.
        // 여기서는 선을 폭의 반만큼 법선 쪽으로 밀어 (0, 0)부터 그려, 정한 폭이 그대로 보이게 한다.
        private static void HexLine(Vector2 a, Vector2 b, Color colour, float width)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length <= 0.01f) return;
            var normal = new Vector2(-delta.y, delta.x) / length;
            var start = a - normal * (width * 0.5f);
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(start, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Paint(new Rect(0, 0, length, width), colour);
            GUI.matrix = original;
        }

        private Vector2 HexCorner(HexTile origin, int dq, int dr, int corner)
        {
            var centre = new Vector2(origin.x, origin.y) + hexEast * dq + hexNorthEast * dr;
            var angle = (60f * corner + 30f + hexTurn) * Mathf.Deg2Rad;
            return GuPoint(centre.x + hexRadiusGu * Mathf.Cos(angle), centre.y + hexRadiusGu * Mathf.Sin(angle));
        }

        // ── 누르기 ───────────────────────────────────────────────────────────

        // 누른 자리의 타일을 고른다. 같은 칸을 곧 다시 누르면 두 번 누르기(집 동의 디오라마로). 골랐으면 true.
        private bool PickHexTile(Vector2 pointer)
        {
            if (!HexTilesActive) return false;
            var index = HexTileAt(GuWorld(pointer));
            if (index < 0)
            {
                hexSelected = -1;
                return false;
            }
            var now = Time.unscaledTime;
            var twice = index == hexLastClickTile && now - hexLastClickAt <= HexDoubleSeconds;
            hexLastClickTile = twice ? -1 : index;
            hexLastClickAt = now;
            hexSelected = index;
            guStation = null;
            if (twice) OpenHexTileDong(index);
            return true;
        }

        // 두 번 누르기: 집 동의 디오라마로 내려간다. 기본 판(동 지도 닫힘)은 안내만 한다.
        private void OpenHexTileDong(int index)
        {
            if (hexPack == null || index < 0 || index >= hexPack.tiles.Length) return;
            var tile = hexPack.tiles[index];
            var home = HexDongOf(tile.d);
            if (home == null)
            {
                HexSay("이 칸은 서울 밖이라 들어갈 동이 아직 없습니다");
                return;
            }
            if (!dongMapOpen)      // 기본 판(폰 포함): 공개판 동 디오라마로 (2026-11-25, SagajeongDispatchPublicDong.cs)
            {
                hexNoticeShown = OpenPublicDong(home.code, home.name, home.gu) ? home.name + " · 공개판 디오라마" : guNotice;
                return;
            }
            var module = DongModulesActive ? DongModuleById(HexRegionPrefix + home.code) : null;
            if (module == null && DongModulesActive)
                foreach (var candidate in dongModules)
                    if (candidate?.Entry?.code == home.code) { module = candidate; break; }
            if (module?.Entry == null || !module.Loaded)
            {
                HexSay($"{home.name}은(는) 아직 디오라마를 굽지 않았습니다");
                return;
            }
            var regionId = module.Entry.regionId;
            hexReturnTile = index;
            hexReturnRegion = regionId;
            if (guSpace != null)
            {
                var (longitude, latitude) = 이어지는지도Policy.구경위도(new Vector2(tile.x, tile.y), guSpace.west, guSpace.south);
                hexReturnLongitude = longitude;
                hexReturnLatitude = latitude;
            }
            focusDongRegion = regionId;
            if (dongView != DongView.Solo) SetDongView(DongView.Solo);
            else FocusDongModule(regionId);
            if (mapLevel == MapLevel.Dong)
            {
                notice = $"{home.name} 디오라마 · Esc · 휠로 같은 타일로";
                hexNoticeShown = notice;
                hexFocusPending = 2;
                Debug.Log($"Hongdal hex tiles: ({tile.q}, {tile.r}) → {regionId}");
            }
            else
            {
                hexReturnTile = -1;
                HexSay($"{home.name}(으)로 내려가지 못했습니다 · 지도는 그대로예요");
            }
        }

        // Esc(설정 열기보다 먼저, SagajeongDispatchSettings.cs): 타일에서 내려온 동이면 같은 타일로 돌아가고,
        // 구 지도에서 타일 그릇 창이 열려 있으면 창을 닫는다. 처리했으면 true.
        private bool HexEscape()
        {
            if (mapLevel == MapLevel.Dong && hexReturnTile >= 0 && dongMapOpen)
            {
                DongToGu();          // 끝에서 ReturnToHexTile이 같은 칸을 고른다
                return true;
            }
            if (!HexCardVisible) return false;
            hexSelected = -1;
            return true;
        }

        // 2.0 동 화면(DrawDongExtent가 동 경계 다음에 부른다): 타일에서 내려왔으면 그 칸의 육각 테두리를 땅에 그리고,
        // 처음 한두 번 그린 뒤에는 휠과 같은 길로 그 자리에 다가간다.
        private void DrawHexTileInDong()
        {
            if (mapLevel != MapLevel.Dong || hexReturnTile < 0 || hexPack == null || Event.current.type != EventType.Repaint) return;
            var module = hexReturnRegion == null ? null : DongModuleById(hexReturnRegion);
            if (hexFocusPending > 0 && --hexFocusPending == 0) FocusDongTile(module);
            var corners = new Vector2[6];
            for (var k = 0; k < 6; k++)
            {
                var angle = (60.0 * k + 30.0 + hexTurn) * Math.PI / 180.0;
                corners[k] = PlotSceneIn(module, hexReturnLatitude + hexRadiusGu * Math.Sin(angle) / 이어지는지도Policy.구위도미터,
                    hexReturnLongitude + hexRadiusGu * Math.Cos(angle) / 이어지는지도Policy.구경도미터);
                if (corners[k].x < -50000f) return;      // 숨긴 동
            }
            for (var k = 0; k < 6; k++)
            {
                HexLine(corners[k], corners[(k + 1) % 6], Fade(Canvas, 0.8f), 5.5f);
                HexLine(corners[k], corners[(k + 1) % 6], Fade(Gold, 0.95f), 3f);
            }
            var tile = hexPack.tiles[hexReturnTile];
            var title = $"타일 ({tile.q}, {tile.r}) · Esc · 휠로 돌아감";
            var width = title.Length * 9.5f + 16f;
            var plate = new Rect(corners[4].x - width / 2f, corners[4].y + 8f, width, 18f);      // 아래 꼭짓점 밑(위쪽은 사업 띠에 가리기 쉽다)
            Paint(plate, Fade(Canvas, 0.86f), 6);
            Text(plate, title, 10, Gold, true, TextAnchor.MiddleCenter);
        }

        // 동을 맞춘 뒤 그 타일 자리에 다가가 화면 가운데로 옮긴다(한 칸 ≈ 170점). 확대는 휠과 같은 길(ApplyMapWheel)로 한다.
        // 동 화면은 그 동 밖으로 멀리 옮길 수 없어(ClampDioramaOffset), 동 가장자리의 타일은 가운데까지 오지 못하고 보이는 쪽에 선다.
        private void FocusDongTile(DongModuleState module)
        {
            if (mapLevel != MapLevel.Dong) return;
            var at = PlotSceneIn(module, hexReturnLatitude, hexReturnLongitude);
            var east = PlotSceneIn(module, hexReturnLatitude, hexReturnLongitude + 150.0 / 이어지는지도Policy.구경도미터);
            if (at.x < -50000f || east.x < -50000f) return;
            var tilePixels = (east - at).magnitude * 2f;
            if (tilePixels < 1f) return;
            var factor = Mathf.Clamp(170f / tilePixels, 1f, 6f);
            if (factor > 1.01f)
                ApplyMapWheel(-Mathf.Log(factor) / Mathf.Log(1.08f), at + Field.position, Time.realtimeSinceStartupAsDouble);
            at = PlotSceneIn(module, hexReturnLatitude, hexReturnLongitude);
            if (at.x < -50000f) return;
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset + FlatDeltaFor(dioramaOn, centre - at), mapZoom), mapZoom);
            localTextureDirty = true;
            Debug.Log($"Hongdal hex tiles: 동 화면을 타일 자리로 · 배율 {mapZoom:0.000} · 한 칸 {tilePixels * factor:0}점");
        }

        private void HexSay(string text)
        {
            hexNoticeShown = text;
            GuSay(text);
        }

        // 동 → 구(DongToGu)가 부른다: 타일에서 내려간 동이면 같은 타일로 돌아와 그 칸을 고른다.
        private void ReturnToHexTile()
        {
            var index = hexReturnTile;
            var region = hexReturnRegion;
            hexReturnTile = -1;
            hexReturnRegion = null;
            hexFocusPending = 0;
            if (index < 0 || hexPack == null || index >= hexPack.tiles.Length || !GuMapVisible) return;
            if (region != null && focusDongRegion != null && focusDongRegion != region) return;      // 그 사이 다른 동으로 옮겼으면 그대로 둔다
            PlaceGuOnHex(hexPack.tiles[index], HexReturnScale);
            hexSelected = index;
            var home = HexDongOf(hexPack.tiles[index].d);
            notice = home == null ? "같은 타일로 돌아왔어요" : $"{home.name}에서 같은 타일로 돌아왔어요";
            Debug.Log($"Hongdal hex tiles: 동 → 같은 타일 ({hexPack.tiles[index].q}, {hexPack.tiles[index].r})");
        }

        // 이어 보기 띠의 「타일」: 지금 보는 자리에서 타일이 다 드러나는 배율로 다가간다(SagajeongDispatchMapContinuity.cs).
        private void JumpToHexTiles()
        {
            var (lon, lat) = ContinuityCentre();
            if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
            if (!GuMapVisible || EnsureHexTiles() == null) return;
            var index = HexTileAt(이어지는지도Policy.구미터(lon, lat, guSpace.west, guSpace.south));
            if (index < 0) index = HexInfluenceTile(out _);
            if (index >= 0) PlaceGuOnHex(hexPack.tiles[index], HexReturnScale);
        }

        // 그 타일을 화면 가운데에 두고 scale로 본다.
        private void PlaceGuOnHex(HexTile tile, float scale)
        {
            if (guSpace == null || tile == null) return;
            guScale = Mathf.Clamp(scale, GuFullScale, Mathf.Max(GuFullScale, GuMaxScale));
            guOrigin = new Vector2(Field.width * 0.5f - tile.x * guScale, Field.height * 0.5f - (guSpace.height - tile.y) * guScale);
            guCameraSet = true;
            guManual = true;
            ClampGu();
        }

        // 촬영용: 역 칸에서 rings 겹 안의 그 성격 칸 가운데 가장 가까운 것(없으면 역 칸).
        private int HexNearestOfKind(int centre, int kind, int rings)
        {
            if (hexPack == null || centre < 0) return centre;
            var origin = hexPack.tiles[centre];
            var best = centre;
            var bestRing = int.MaxValue;
            for (var i = 0; i < hexPack.tiles.Length; i++)
            {
                var tile = hexPack.tiles[i];
                if (tile.k != kind || tile.w < 500) continue;
                var ring = HexDistance(tile.q - origin.q, tile.r - origin.r);
                if (ring > rings || ring >= bestRing) continue;
                bestRing = ring;
                best = i;
            }
            return best;
        }

        // ── 그릇 창 ─────────────────────────────────────────────────────────

        private Rect HexTileCardRect => PortraitScreen ? PortraitCardRect(318f, 364f) : new Rect(22f - Field.x, 216f - Field.y, 318f, 364f);      // 아래 지하철 보기 단추(580~)에 닿지 않게

        private bool HexCardVisible =>
            hexSelected >= 0 && hexPack != null && hexSelected < hexPack.tiles.Length && HexTilesActive && GuStationNow() == null && !dataPanelOpen;

        private bool HexCardCovers(Vector2 point) => HexCardVisible && (HexTileCardRect.Contains(point) || HexYieldPanelCovers(point));

        // 타일 그릇 창. DrawGuMap이 역 창 다음에 부른다. 창 위의 누름은 지도로 넘기지 않는다(true).
        private bool DrawHexTileCard()
        {
            if (!HexCardVisible) return false;
            var pack = hexPack;
            var tile = pack.tiles[hexSelected];
            var card = HexTileCardRect;
            var current = Event.current;
            var over = card.Contains(current.mousePosition);
            if (DrawHexYieldPanel(card)) return true;      // 수치 입장: 창 옆 「이 칸의 수치」 · 「좋은 칸」(SagajeongDispatchHexYields.cs)
            var tint = HexKindColour(tile.k);
            var ready = tile.w >= 500;
            Paint(new Rect(card.x - 1.5f, card.y - 1.5f, card.width + 3f, card.height + 3f), Fade(tint, 0.7f), 13);
            Paint(card, Panel, 12);
            var x = card.x + 16f;
            var y = card.y + 12f;
            var wide = card.width - 32f;
            Text(new Rect(x, y, wide, 14f), "타일 · 읽기만 하는 창", 10, Gold, true);
            Text(new Rect(x, y, wide - 36f, 14f), $"칸 ({tile.q}, {tile.r})", 10, Soft, false, TextAnchor.MiddleRight);
            Paint(new Rect(x, y + 21f, 6f, 25f), tint, 3);
            Text(new Rect(x + 14f, y + 16f, wide - 50f, 32f), ready ? HexKindName(tile.k) : "자료 준비 전", 22, Paper, true);
            if (ExpansionButton(new Rect(card.xMax - 40f, card.y + 10f, 28f, 24f), "×", true, Tile))
            {
                hexSelected = -1;
                return true;
            }

            var home = HexDongOf(tile.d);
            var cross = HexDongOf(tile.d2);
            var homeText = home == null ? "집 동 · 없음(서울 밖)"
                : $"집 동 · {home.name} · {home.gu}" + (cross == null ? string.Empty : $"  ·  걸친 동 {cross.name}");
            Text(new Rect(x, y + 50f, wide, 16f), homeText, 11, home == null ? Soft : Gold, home != null);

            if (!ready)
            {
                Text(new Rect(x, y + 76f, wide, 64f),
                    $"서울 자료가 이 칸의 {tile.w / 10}%만 덮습니다(서울 밖). 건물 · 녹지 · 음식점 · 사람을 아직 모으지 않았습니다 — 0이 아니라 준비 전입니다.",
                    11, Soft, false, TextAnchor.UpperLeft, true);
                DrawHexCardFooter(tile, x, y, wide);
                return HexCardSwallow(over, current);
            }

            // 땅의 꼴
            DrawCardHeaderIcon(x, y + 70f, "building");
            Text(new Rect(x, y + 70f, wide, 14f), "땅의 꼴", 10, Gold, true);
            var boxWide = (wide - 12f) / 3f;
            DrawStationFigure(new Rect(x, y + 84f, boxWide, 42f), "건물", $"{tile.b:N0}채", Paper);
            DrawStationFigure(new Rect(x + boxWide + 6f, y + 84f, boxWide, 42f), "덮인 몫", $"{tile.c / 10f:0}%", Paper);
            DrawStationFigure(new Rect(x + (boxWide + 6f) * 2f, y + 84f, boxWide, 42f), "평균 층", tile.f > 0 ? $"{tile.f / 10f:0.0}층" : "—", Paper);
            Text(new Rect(x, y + 129f, wide, 16f), $"연면적 / 땅 {tile.e}%  ·  15층 넘는 건물 {tile.t}채", 11, Soft);

            // 쓰임
            DrawCardHeaderIcon(x, y + 148f, "local-business");
            Text(new Rect(x, y + 148f, wide, 14f), "쓰임 · 연면적의 몫", 10, Gold, true);
            var bar = new Rect(x, y + 164f, wide, 8f);
            Paint(bar, Tile, 3);
            if (tile.u != null)
            {
                var left = bar.x;
                for (var i = 0; i < tile.u.Length && i < HexUseColours.Length; i++)
                {
                    var part = bar.width * tile.u[i] / 1000f;
                    if (part < 0.5f) continue;
                    Paint(new Rect(left, bar.y, Mathf.Min(part, bar.xMax - left), bar.height), HexUseColours[i], 0);
                    left += part;
                }
            }
            var uses = HexTopParts(tile.u, pack.uses, 3, true);
            Text(new Rect(x, y + 174f, wide, 16f), uses.Length > 0 ? uses : "건물 없음", 11, uses.Length > 0 ? Paper : Soft);

            // 녹지 · 물
            DrawCardHeaderIcon(x, y + 195f, "park");
            Text(new Rect(x, y + 194f, 72f, 16f), "녹지 · 물", 10, Gold, true);
            var greens = HexTopParts(tile.g, pack.greens, 4, true);
            Text(new Rect(x + 72f, y + 194f, wide - 72f, 16f), greens.Length > 0 ? greens : "없음", 11, greens.Length > 0 ? Mint : Soft);

            // 음식점
            DrawCardHeaderIcon(x, y + 214f, "restaurant");
            Text(new Rect(x, y + 214f, wide, 14f), "음식점 · 영업 중 인허가", 10, Gold, true);
            Text(new Rect(x, y + 229f, 80f, 18f), tile.n >= 0 ? $"{tile.n:N0}곳" : "준비 전", 13, tile.n >= 0 ? Paper : Soft, true);
            Text(new Rect(x + 76f, y + 230f, wide - 76f, 16f), tile.n > 0 ? HexTopParts(tile.m, pack.foods, 3, false) : string.Empty, 11, Soft,
                false, TextAnchor.MiddleRight);

            // 사람
            DrawCardHeaderIcon(x, y + 252f, "residents");
            Text(new Rect(x, y + 251f, 40f, 16f), "사람", 10, Gold, true);
            Text(new Rect(x + 40f, y + 251f, wide - 40f, 16f), tile.p >= 0 ? $"사는 사람 약 {tile.p:N0}명 · 추정" + (LifeIndex(home?.code) >= 0 ? $" · 지금 생활인구 {LifeIndex(home?.code)}" : "") : "사는 사람 · 자료 준비 전", 11, tile.p >= 0 ? Paper : Soft);

            // 생활 시설 참고점 (중랑 16동만 · SagajeongDispatchDataPacks.cs)
            var facilities = FacilitiesOfTile(hexSelected);
            var facilityCount = 0;
            if (facilities != null) for (var i = 0; i < facilities.Length; i++) if (FacilityKeyShown(i)) facilityCount += facilities[i];
            DrawCardHeaderIcon(x, y + 271f, "grid");
            Text(new Rect(x, y + 271f, wide, 14f), facilities == null ? "생활 시설 · 참고점 없음(중랑 16동만 모았다)" : $"생활 시설 · 참고점 {facilityCount}곳 · 중랑만 · 자료 창에서 고른 갈래", 10, Gold, true);
            if (facilities != null) DrawFacilityIconRow(new Rect(x, y + 285f, wide, 18f), facilities, 7, tile.q, tile.r);   // 갈래 아이콘 + 개수(SagajeongDispatchMapIcons.cs)

            DrawHexCardFooter(tile, x, y, wide);
            return HexCardSwallow(over, current);
        }

        private void DrawHexCardFooter(HexTile tile, float x, float y, float wide)
        {
            var centre = HexInfluenceTile(out var station);
            var where = string.Empty;
            if (!string.IsNullOrEmpty(tile.s)) where = "역 칸 · " + tile.s.Replace("·", " · ");
            else if (centre >= 0)
            {
                var origin = hexPack.tiles[centre];
                var rings = HexDistance(tile.q - origin.q, tile.r - origin.r);
                where = rings <= 3 ? $"{station} 영향권 {rings}겹" : $"{station}에서 {rings}칸 · 영향권 밖";
            }
            Text(new Rect(x, y + 305f, wide, 16f), where, 11, Mint, true);
            Text(new Rect(x, y + 322f, wide, 14f), dongMapOpen ? "두 번 누르면 집 동의 디오라마로 내려갑니다" : "두 번 누르면 이 동의 디오라마로", 10, Soft);
            Text(new Rect(x, y + 337f, wide, 12f), "건물 11-09 · 생태 2025 · 인허가 11-18 · 주민등록 10-31 · 시설 11-23", 9, Soft);
        }

        private static bool HexCardSwallow(bool over, Event current)
        {
            if (!over || current.type == EventType.Repaint || current.type == EventType.Layout) return false;
            if (current.type != EventType.MouseDown && current.type != EventType.MouseUp && current.type != EventType.MouseDrag &&
                current.type != EventType.ScrollWheel) return false;
            current.Use();
            return true;
        }

        // 방향 촬영(상위 판 길이 열린 검증): 타일에서 두 번 눌러 동 디오라마로, 동에서 휠로 올라와 같은 타일로. 관찰용.
        private IEnumerator ViewProofHexTiles(string directory, ViewProofReceipt receipt)
        {
            if (!dongMapOpen) yield break;
            if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
            for (var frame = 0; frame < 8; frame++) yield return null;
            if (!GuMapVisible || EnsureHexTiles() == null) yield break;
            guStation = null;
            var station = HexInfluenceTile(out _);
            if (station < 0) yield break;
            receipt.hexTiles = hexPack.tiles.Length;
            PlaceGuOnHex(hexPack.tiles[station], 0.25f);
            hexSelected = station;
            for (var frame = 0; frame < 10; frame++) yield return null;
            yield return SaveFrame(directory, "145-gu-tiles.png");
            OpenHexTileDong(station);
            for (var frame = 0; frame < 30; frame++) yield return null;      // 동 화면이 서고, 타일 자리로 다가간 뒤
            receipt.hexDongOpened = mapLevel == MapLevel.Dong;
            yield return SaveFrame(directory, "146-tile-to-dong.png");
            if (mapLevel == MapLevel.Dong) HexEscape();      // Esc와 같은 길(휠로 올라가는 길도 DongToGu로 같다)
            for (var frame = 0; frame < 10; frame++) yield return null;
            receipt.hexReturned = GuMapVisible && hexSelected == station;
            yield return SaveFrame(directory, "147-dong-back-to-tile.png");
            hexSelected = -1;
            Debug.Log($"Hongdal hex tiles proof: tiles {receipt.hexTiles}, dong {receipt.hexDongOpened}, back {receipt.hexReturned}");
        }

        // 큰 것부터 count개: 「단독 41% · 공동 26%」(permille) 또는 「한식 24 · 기타 17」.
        private static string HexTopParts(int[] values, string[] names, int count, bool permille)
        {
            if (values == null || names == null) return string.Empty;
            var order = new List<int>();
            for (var i = 0; i < values.Length && i < names.Length; i++)
                if (values[i] > (permille ? 9 : 0)) order.Add(i);
            order.Sort((a, b) => values[b].CompareTo(values[a]));
            var parts = new List<string>();
            for (var i = 0; i < order.Count && i < count; i++)
                parts.Add(permille ? $"{names[order[i]]} {Mathf.RoundToInt(values[order[i]] / 10f)}%" : $"{names[order[i]]} {values[order[i]]}");
            return string.Join(" · ", parts);
        }
    }
}
