using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 수도권 육각 타일(공개판, 2026-11-25 사용자 11:22: 「타일 두 번 누르면 디오라마로 … 수도권부터 전국으로」).
    // Resources/HongdalCapital/hongdal-capital-hex.bytes(make_capital_hex.py): 구 지도 타일 r1과 같은 격자(UTM-K 300 m)로
    // 수도권 151,000여 칸. 칸마다 가운데가 든 행정동 · 건물 수 · 덮인 몫 · 평균 층 · 녹지 · 물 · 성격.
    // 자료: GIS건물통합정보 AL_D010 · vuski/admdongkor · ESA WorldCover — 모두 재배포 가능(출처 표시). 주소 자료는 쓰지 않는다.
    // 한 번 누르면 칸 창, 두 번 누르면(또는 창의 「디오라마」) 그 동의 공개판 디오라마(SagajeongDispatchPublicDong.cs).
    // 세 구의 기존 타일(hongdal-tiles-gu.json)이 떠 있을 때는 그 칸과 겹치는 수도권 칸을 그리지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string CapitalHexResource = "HongdalCapital/hongdal-capital-hex";
        private const float CapitalHexFadeStart = 0.045f;     // 칸 반지름 ≈ 7.8점부터 떠오른다
        private const float CapitalHexFadeFull = 0.07f;
        private const float CapitalHexBucket = 2000f;          // 보이는 칸을 찾는 바구니(구 지도 미터)

        [Serializable]
        private sealed class CapitalHexDong
        {
            public string code;
            public string name;
            public string sgg;
            public string sido;
        }

        [Serializable]
        private sealed class CapitalHexTable
        {
            public string schema;
            public string revision;
            public string distribution;
            public string grid;
            public string[] kinds;
            public string[] uses;
            public string kindRule;
            public string[] sources;
            public CapitalHexDong[] dongs;
        }

        private bool capitalHexTried;
        private int capitalHexCount;
        private float[] chX, chY;
        private ushort[] chDong, chBuildings;
        private byte[] chCover, chFloors, chUse, chGreen, chWater, chKind;
        private bool[] chUnderOld;                 // 세 구 기존 타일과 겹치는 칸
        private bool chUnderOldReady;
        private float chRadius = 173.3292f;
        private CapitalHexTable chTable;
        private readonly Dictionary<long, List<int>> chBuckets = new();
        private int chHover = -1, chSelected = -1, chLastTap = -1;
        private float chLastTapAt;

        private bool CapitalHexReady => EnsureCapitalHex();

        private bool EnsureCapitalHex()
        {
            if (chX != null) return true;
            if (capitalHexTried || !CapitalReady) return false;
            capitalHexTried = true;
            var asset = Resources.Load<TextAsset>("HongdalNation/hongdal-nation-hex");      // 전국 칸(2026-11-25 make_nation_hex.py)이 있으면 그것을
            if (asset == null) asset = Resources.Load<TextAsset>(CapitalHexResource);
            if (asset == null) return false;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(asset.bytes));
                var magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'H' || magic[1] != 'H' || magic[2] != 'E' || magic[3] != 'X' || reader.ReadInt32() != 1)
                    throw new InvalidDataException("unknown header");
                var count = reader.ReadInt32();
                if (count <= 0 || count > 2000000) throw new InvalidDataException("tile count");
                reader.ReadSingle();
                chRadius = reader.ReadSingle();
                chX = new float[count]; chY = new float[count];
                chDong = new ushort[count]; chBuildings = new ushort[count];
                chCover = new byte[count]; chFloors = new byte[count]; chUse = new byte[count]; chGreen = new byte[count]; chWater = new byte[count]; chKind = new byte[count];
                for (var i = 0; i < count; i++)
                {
                    chX[i] = reader.ReadSingle(); chY[i] = reader.ReadSingle();
                    chDong[i] = reader.ReadUInt16(); chBuildings[i] = reader.ReadUInt16();
                    chCover[i] = reader.ReadByte(); chFloors[i] = reader.ReadByte(); chUse[i] = reader.ReadByte();
                    chGreen[i] = reader.ReadByte(); chWater[i] = reader.ReadByte(); chKind[i] = reader.ReadByte();
                    reader.ReadInt16();
                    var key = CapitalHexKey(chX[i], chY[i]);
                    if (!chBuckets.TryGetValue(key, out var list)) chBuckets[key] = list = new List<int>();
                    list.Add(i);
                }
                var length = reader.ReadInt32();
                chTable = JsonUtility.FromJson<CapitalHexTable>(Encoding.UTF8.GetString(reader.ReadBytes(length)));
                capitalHexCount = count;
                chUnderOld = new bool[count];
                Debug.Log($"Hongdal capital hex: {count} tiles, {chTable?.dongs?.Length ?? 0} dongs, {chTable?.revision}");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                Debug.LogWarning("Hongdal capital hex: could not be read. " + exception.Message);
                chX = null;
                return false;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private static long CapitalHexKey(float x, float y) =>
            ((long)Mathf.FloorToInt(x / CapitalHexBucket) << 32) ^ (uint)Mathf.FloorToInt(y / CapitalHexBucket);

        private CapitalHexDong CapitalHexDongOf(int tile) =>
            chTable?.dongs != null && tile >= 0 && tile < capitalHexCount && chDong[tile] < chTable.dongs.Length ? chTable.dongs[chDong[tile]] : null;

        private float CapitalHexAlpha
        {
            get
            {
                if (!CapitalReady || !GuMapVisible || !guCameraSet || !hexLayerOn || guScale < CapitalHexFadeStart || !EnsureCapitalHex()) return 0f;
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CapitalHexFadeStart, CapitalHexFadeFull, guScale));
            }
        }

        // 세 구의 기존 타일과 겹치는 칸 표시(한 번만 센다)
        private void MarkCapitalHexUnderOld()
        {
            if (chUnderOldReady || hexPack == null) return;
            chUnderOldReady = true;
            for (var i = 0; i < capitalHexCount; i++)
                chUnderOld[i] = HexTileAt(new Vector2(chX[i], chY[i])) >= 0;
        }

        // 구 지도 미터의 한 점이 든 수도권 칸(가운데가 가장 가까운 칸, 반지름 안)
        private int CapitalHexAt(Vector2 world)
        {
            if (!EnsureCapitalHex()) return -1;
            var best = -1;
            var bestDistance = chRadius * chRadius;
            var bx = Mathf.FloorToInt(world.x / CapitalHexBucket);
            var by = Mathf.FloorToInt(world.y / CapitalHexBucket);
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!chBuckets.TryGetValue(((long)(bx + dx) << 32) ^ (uint)(by + dy), out var list)) continue;
                    foreach (var i in list)
                    {
                        var ex = chX[i] - world.x;
                        var ey = chY[i] - world.y;
                        var distance = ex * ex + ey * ey;
                        if (distance < bestDistance) { bestDistance = distance; best = i; }
                    }
                }
            return best;
        }

        private void DrawCapitalHex(float now)
        {
            var alpha = CapitalHexAlpha;
            if (alpha <= 0.003f)
            {
                chHover = -1;
                return;
            }
            EnsureHexSprites();
            var branchOn = BranchBoardOn;      // 지사장 판: 동마다 내 몫으로 칠한다 (SagajeongDispatchBranchBoard.cs)
            var old = HexTilesAlpha;
            if (old > 0.003f) MarkCapitalHexUnderOld();
            var current = Event.current;
            var free = !guDragging && !MapOverlayCovers(current.mousePosition + Field.position) && !CapitalHexCardCovers(current.mousePosition);
            chHover = free && alpha >= 0.5f && !PortraitScreen ? CapitalHexAt(GuWorld(current.mousePosition)) : -1;
            var radius = chRadius * guScale;
            var size = Mathf.Max(0.5f, radius - HexGap) / HexSpriteRadius;
            var width = hexFill.width * size;
            var height = hexFill.height * size;
            var topLeft = GuWorld(new Vector2(-width, -height));
            var bottomRight = GuWorld(new Vector2(Field.width + width, Field.height + height));
            var bx0 = Mathf.FloorToInt(topLeft.x / CapitalHexBucket); var bx1 = Mathf.FloorToInt(bottomRight.x / CapitalHexBucket);
            var by0 = Mathf.FloorToInt(bottomRight.y / CapitalHexBucket); var by1 = Mathf.FloorToInt(topLeft.y / CapitalHexBucket);
            if ((bx1 - bx0 + 1) * (by1 - by0 + 1) > 4000) return;      // 너무 멀다(가장자리 보호)
            var view = new Rect(-width, -height, Field.width + width * 2f, Field.height + height * 2f);
            for (var bx = bx0; bx <= bx1; bx++)
                for (var by = by0; by <= by1; by++)
                {
                    if (!chBuckets.TryGetValue(((long)bx << 32) ^ (uint)by, out var list)) continue;
                    foreach (var i in list)
                    {
                        var at = GuPoint(chX[i], chY[i]);
                        if (!view.Contains(at)) continue;
                        var a = alpha;
                        if (old > 0.003f && chUnderOld[i]) a *= 1f - old;          // 기존 타일이 뜨면 그 칸은 비킨다
                        if (a <= 0.01f) continue;
                        var kind = chKind[i];
                        var cover = chCover[i] / 255f;
                        var strength = kind <= 4 ? 0.26f : kind == 5 ? 0.14f : 0.14f + 0.26f * Mathf.Sqrt(cover);      // 밑 지도가 비치게
                        var tint = HexKindColour(kind);
                        if (branchOn) BranchHexColour(i, ref tint, ref strength);
                        if (i == chHover) strength += 0.12f;
                        if (i == chSelected) strength += 0.2f;
                        GUI.DrawTexture(new Rect(at.x - width / 2f, at.y - height / 2f, width, height), hexFill,
                            ScaleMode.StretchToFill, true, 0, Fade(tint, strength * a), 0, 0);
                    }
                }
            if (chHover >= 0 && chHover != chSelected) DrawCapitalHexRing(chHover, Fade(Paper, 0.75f * alpha), 0f);
            if (chSelected >= 0 && chSelected < capitalHexCount)
            {
                var pulse = 0.85f + 0.15f * Mathf.Sin(now * 3f);
                DrawCapitalHexRing(chSelected, Fade(Canvas, 0.85f * alpha), 2.5f);
                DrawCapitalHexRing(chSelected, Fade(Gold, pulse * alpha), 0f);
            }
        }

        private void DrawCapitalHexRing(int tile, Color colour, float grow)
        {
            var at = GuPoint(chX[tile], chY[tile]);
            var size = Mathf.Max(0.5f, chRadius * guScale - HexGap + grow) / HexSpriteRadius;
            var width = hexRing.width * size;
            var height = hexRing.height * size;
            GUI.DrawTexture(new Rect(at.x - width / 2f, at.y - height / 2f, width, height), hexRing, ScaleMode.StretchToFill, true, 0, colour, 0, 0);
        }

        // 누르기: 칸을 고른다. 같은 칸을 곧 다시 누르면 그 동의 공개판 디오라마로. 골랐으면 true.
        private bool PickCapitalHex(Vector2 pointer)
        {
            if (CapitalHexAlpha < 0.5f) return false;
            var index = CapitalHexAt(GuWorld(pointer));
            if (index >= 0 && HexTilesAlpha > 0.5f && chUnderOld[index]) index = -1;      // 기존 타일이 맡는다
            if (index < 0)
            {
                chSelected = -1;
                return false;
            }
            var now = Time.unscaledTime;
            var twice = index == chLastTap && now - chLastTapAt <= HexDoubleSeconds;
            chLastTap = twice ? -1 : index;
            chLastTapAt = now;
            chSelected = index;
            guStation = null;
            if (twice) OpenCapitalHexDong(index);
            return true;
        }

        private void OpenCapitalHexDong(int index)
        {
            var dong = CapitalHexDongOf(index);
            if (dong == null) return;
            OpenPublicDong(dong.code, dong.name, dong.sgg);
        }

        private Rect CapitalHexCardRect => BranchBoardOn ? BranchDongCardRect : PortraitScreen
            ? new Rect(10f, portraitSheetRect.y - 124f - (CapitalReady ? 34f : 22f), PortraitWidth - 20f, 118f)
            : new Rect((Field.width - 344f) * 0.5f, Field.height - 268f, 344f, 120f);      // 가운데 아래: 「자료 창」 · 「영역 전체 보기」 위

        private bool CapitalHexCardVisible => chSelected >= 0 && chSelected < capitalHexCount && CapitalHexAlpha >= 0.5f && !PublicDongOpen;

        private bool CapitalHexCardCovers(Vector2 point) => CapitalHexCardVisible && CapitalHexCardRect.Contains(point);

        // 칸 창: 동 · 성격 · 건물 · 녹지와 「디오라마 ▸」. 눌렀으면 true(지도 입력을 건너뛴다).
        private bool DrawCapitalHexCard()
        {
            if (!CapitalHexCardVisible) return false;
            if (BranchBoardOn && BranchSlotOfCell(chSelected) >= 0)      // 배달 판: 기사는 칸을 고르기만 · 지사장은 동 카드
                return branchSeat != 0 && DrawBranchDongCard(BranchSlotOfCell(chSelected));      // 지사장 판의 동 카드
            var card = CapitalHexCardRect;
            var i = chSelected;
            var dong = CapitalHexDongOf(i);
            Paint(card, Fade(Canvas, 0.93f), 10);
            var x = card.x + 12f;
            var wide = card.width - 24f;
            var kind = chKind[i];
            var kindName = chTable?.kinds != null && kind < chTable.kinds.Length ? chTable.kinds[kind] : "?";
            Paint(new Rect(x, card.y + 13f, 10f, 10f), HexKindColour(kind), 5);
            Text(new Rect(x + 16f, card.y + 6f, wide - 110f, 24f), dong == null ? "수도권 칸" : dong.name, 16, Paper, true);
            Text(new Rect(x + 16f, card.y + 28f, wide - 110f, 16f), dong == null ? "" : $"{dong.sgg} · {kindName}", 11, Soft);
            var buildings = chBuildings[i];
            var floors = chFloors[i] / 10f;
            Text(new Rect(x, card.y + 50f, wide, 17f),
                buildings == 0 ? "건물 없음" : floors > 0 ? $"건물 {buildings}채 · 평균 {floors:0.#}층 · 덮인 몫 {chCover[i] * 100 / 255}%" : $"건물 {buildings}채 · 덮인 몫 {chCover[i] * 100 / 255}%", 12, Paper);
            Text(new Rect(x, card.y + 68f, wide, 15f), $"녹지 {chGreen[i] * 100 / 255}% · 물 {chWater[i] * 100 / 255}% · 300 m 칸", 10, Soft);
            Text(new Rect(x, card.y + 88f, wide - 120f, 24f), "두 번 누르면 이 동의 디오라마로", 10, Fade(Soft, 0.9f));
            var go = new Rect(card.xMax - 120f, card.y + 82f, 108f, 28f);
            Paint(go, Mint, 7);
            Text(go, "디오라마 →", 12, Canvas, true, TextAnchor.MiddleCenter);
            var close = new Rect(card.xMax - 34f, card.y + 6f, 26f, 22f);
            Text(close, "×", 14, Soft, false, TextAnchor.MiddleCenter);
            var current = Event.current;
            if (current.type == EventType.MouseUp && current.button == 0)
            {
                if (go.Contains(current.mousePosition))
                {
                    current.Use();
                    OpenCapitalHexDong(i);
                    return true;
                }
                if (close.Contains(current.mousePosition))
                {
                    current.Use();
                    chSelected = -1;
                    return true;
                }
            }
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag) && card.Contains(current.mousePosition))
            {
                current.Use();
                return true;
            }
            return false;
        }
    }
}
