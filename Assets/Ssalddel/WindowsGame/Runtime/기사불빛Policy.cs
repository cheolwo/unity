using System;

namespace Ssalddel.WindowsGame
{
    // 기사와 가게의 빛 (2026-11-14). 순수 규칙이다. 화면과 자원을 모른다.
    // 사용자 방향: 기사는 모델이 아니라 반딧불이 같은 빛으로 보인다. 기사가 있는 자리가 밝아지고, 지나온 길이 한동안 밝다가
    //            멀어지면 어두워진다. 기사가 많을수록 어두운 지도가 더 밝아진다. 종류(기사 · 음식점 · 전달지)에 따라 빛이 다르다.
    // 빛의 양은 한 칸에 0~255로 적는다. 네 겹이다: 기사(지나간 자취), 음식점, 전달지, 온기(자주 다닌 길에 쌓이는 옅은 빛).
    // 모두 표현값이다. 배달 가능 범위·상권·매출·주문 수를 뜻하지 않는다.
    internal static class 기사불빛Policy
    {
        internal const int Rider = 0, Store = 1, Drop = 2, Warmth = 3;      // 한 칸의 네 겹 (그림의 R · G · B · A)
        internal const float CellMetres = 5f;
        internal const float Margin = 80f;            // 길의 범위 밖으로 두는 여백 (m)
        internal const float RiderRadius = 44f;       // 기사의 빛이 닿는 거리 (m). 길 하나와 그 양쪽 건물이 밝아질 만큼
        internal const float StoreRadius = 36f;
        internal const float DropRadius = 34f;
        internal const float RiderFade = 6f;          // 자취가 e분의 1로 어두워지는 시간 (표현의 초)
        internal const float StoreFade = 3f;
        internal const float DropFade = 4.5f;
        internal const float WarmthFade = 45f;
        internal const float WarmthEvery = 1.5f;      // 온기는 이만큼의 시간이 모이면 한 번 식힌다 (천천히 식는 값을 0~255로 다루려고)
        internal const float WarmthGain = 0.3f;       // 기사가 한 자리에 1초 머물 때 쌓이는 온기
        internal const float StoreRest = 0.42f;       // 음식점이 늘 띠는 빛 (기사가 픽업하러 닿으면 잠깐 가득해진다)
        internal const float Tail = 6f;               // 자취의 끝을 끊는 값 (1초에 255분의 몇)
        internal const float WarmthTail = 0.3f;

        // 가운데가 가장 밝고, 닿는 거리에서 0이 된다.
        internal static float Falloff(float distance, float radius)
        {
            if (radius <= 0f || distance >= radius) return 0f;
            var near = 1f - Math.Max(0f, distance) / radius;
            return near * near * (3f - 2f * near);
        }

        // seconds가 흐른 뒤의 밝기. 시간이 흐르면 반드시 줄고, 충분히 흐르면 0이 된다.
        internal static byte Faded(byte value, float seconds, float fade, float tail)
        {
            if (seconds <= 0f || value == 0) return value;
            var next = (int)Math.Floor(value * Math.Exp(-seconds / Math.Max(0.01f, fade)) - tail * seconds);
            if (next >= value) next = value - 1;
            return (byte)Math.Max(0, next);
        }

        internal static void FadeTable(byte[] table, float seconds, float fade, float tail)
        {
            for (var value = 0; value < 256; value++) table[value] = Faded((byte)value, seconds, fade, tail);
        }

        // 겹치는 빛은 더 밝은 쪽을 따른다 (기사 둘이 겹쳐도 넘치지 않는다).
        internal static byte Brighter(byte value, float light)
        {
            var next = (int)Math.Round(Math.Max(0f, Math.Min(1f, light)) * 255f);
            return next > value ? (byte)next : value;
        }

        // 온기는 쌓인다 (자주 다닌 길일수록 밝다). 255에서 멈춘다.
        internal static byte Warmer(byte value, float light, float seconds)
        {
            var add = (int)Math.Floor(Math.Max(0f, Math.Min(1f, light)) * WarmthGain * Math.Max(0f, Math.Min(10f, seconds)) * 255f);
            return (byte)Math.Min(255, value + add);
        }

        // ── 빛이 몰렸을 때 (2026-11-14 사용자: 매장이 많아 빛이 겹치면 구분이 어렵다) ──
        internal const float CrowdStep = 0.3f;        // 이웃한 빛 하나마다 연한 부분이 옅어지는 정도
        internal const float CrowdFloor = 0.45f;      // 아무리 몰려도 이만큼은 남는다

        // 이웃한 빛이 n개일 때 연한 부분에 곱하는 값. 이웃이 없으면 1, 많을수록 작아지되 바닥 아래로 내려가지 않는다.
        internal static float Crowd(int neighbours) => Math.Max(CrowdFloor, 1f / (1f + CrowdStep * Math.Max(0, neighbours)));

        // 빛마다의 숨의 엇갈림(0 이상 2π 미만). 같은 열쇠는 늘 같고, 이웃한 열쇠끼리는 고르게 흩어진다.
        internal static float Phase(uint key)
        {
            var h = key * 0x9E3779B1u;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            h *= 0xC2B2AE3Du;
            h ^= h >> 16;
            return (float)(h / 4294967296.0 * 2.0 * Math.PI);
        }

        internal static float Phase(string key)
        {
            var h = 2166136261u;
            if (key != null)
                foreach (var ch in key)
                {
                    h ^= (uint)ch;
                    h *= 16777619u;
                }
            return Phase(h);
        }

        // 두 박자의 어긋남(0 = 같은 때, 1 = 정반대).
        internal static float PhaseGap(float a, float b)
        {
            var gap = Math.Abs((double)a - b) % (2.0 * Math.PI);
            if (gap > Math.PI) gap = 2.0 * Math.PI - gap;
            return (float)(gap / Math.PI);
        }
    }

    // 빛을 적는 판. 길의 범위를 덮는 칸들이고, 한 칸에 네 겹(기사불빛Policy의 차례)을 적는다. 가장자리 한 줄은 늘 비워 둔다(판 밖은 어둡다).
    internal sealed class 기사불빛판
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly float X0;
        internal readonly float Z0;
        internal readonly float Cell;
        internal readonly byte[] Cells;
        private readonly byte[] riderTable = new byte[256];
        private readonly byte[] storeTable = new byte[256];
        private readonly byte[] dropTable = new byte[256];
        private readonly byte[] warmthTable = new byte[256];
        private float warmthDue;

        internal 기사불빛판(float minX, float minZ, float maxX, float maxZ, float cell, float margin)
        {
            var spanX = Math.Max(1f, maxX - minX) + 2f * Math.Max(0f, margin);
            var spanZ = Math.Max(1f, maxZ - minZ) + 2f * Math.Max(0f, margin);
            Cell = Math.Max(Math.Max(1f, cell), Math.Max(spanX, spanZ) / 1024f);      // 한 변이 1024칸을 넘지 않게
            X0 = minX - Math.Max(0f, margin);
            Z0 = minZ - Math.Max(0f, margin);
            Width = Math.Max(4, (int)Math.Ceiling(spanX / Cell));
            Height = Math.Max(4, (int)Math.Ceiling(spanZ / Cell));
            Cells = new byte[Width * Height * 4];
        }

        private bool Reach(float x, float z, float radius, out int ix0, out int ix1, out int iz0, out int iz1)
        {
            ix0 = Math.Max(1, (int)Math.Floor((x - radius - X0) / Cell));
            ix1 = Math.Min(Width - 2, (int)Math.Floor((x + radius - X0) / Cell));
            iz0 = Math.Max(1, (int)Math.Floor((z - radius - Z0) / Cell));
            iz1 = Math.Min(Height - 2, (int)Math.Floor((z + radius - Z0) / Cell));
            return ix0 <= ix1 && iz0 <= iz1;
        }

        // 한 자리에 한 겹의 빛을 놓는다 (음식점, 전달지).
        internal void Light(int layer, float x, float z, float radius, float strength)
        {
            if (layer < 0 || layer > 3 || !Reach(x, z, radius, out var ix0, out var ix1, out var iz0, out var iz1)) return;
            for (var iz = iz0; iz <= iz1; iz++)
            {
                var dz = Z0 + (iz + 0.5f) * Cell - z;
                for (var ix = ix0; ix <= ix1; ix++)
                {
                    var dx = X0 + (ix + 0.5f) * Cell - x;
                    var light = strength * 기사불빛Policy.Falloff((float)Math.Sqrt(dx * dx + dz * dz), radius);
                    if (light <= 0f) continue;
                    var at = (iz * Width + ix) * 4 + layer;
                    Cells[at] = 기사불빛Policy.Brighter(Cells[at], light);
                }
            }
        }

        // 기사가 seconds 동안 그 자리에 있었다. 자취는 더 밝은 쪽을 따르고, 온기는 쌓인다.
        internal void Ride(float x, float z, float seconds)
        {
            var radius = 기사불빛Policy.RiderRadius;
            if (!Reach(x, z, radius, out var ix0, out var ix1, out var iz0, out var iz1)) return;
            for (var iz = iz0; iz <= iz1; iz++)
            {
                var dz = Z0 + (iz + 0.5f) * Cell - z;
                for (var ix = ix0; ix <= ix1; ix++)
                {
                    var dx = X0 + (ix + 0.5f) * Cell - x;
                    var light = 기사불빛Policy.Falloff((float)Math.Sqrt(dx * dx + dz * dz), radius);
                    if (light <= 0f) continue;
                    var at = (iz * Width + ix) * 4;
                    Cells[at + 기사불빛Policy.Rider] = 기사불빛Policy.Brighter(Cells[at + 기사불빛Policy.Rider], light);
                    Cells[at + 기사불빛Policy.Warmth] = 기사불빛Policy.Warmer(Cells[at + 기사불빛Policy.Warmth], light, seconds);
                }
            }
        }

        // seconds가 흐른다. 모든 칸이 어두워진다. 0이면 아무것도 달라지지 않는다(정지한 동안).
        internal void Fade(float seconds)
        {
            if (seconds <= 0f) return;
            기사불빛Policy.FadeTable(riderTable, seconds, 기사불빛Policy.RiderFade, 기사불빛Policy.Tail);
            기사불빛Policy.FadeTable(storeTable, seconds, 기사불빛Policy.StoreFade, 기사불빛Policy.Tail);
            기사불빛Policy.FadeTable(dropTable, seconds, 기사불빛Policy.DropFade, 기사불빛Policy.Tail);
            warmthDue += seconds;
            var cool = warmthDue >= 기사불빛Policy.WarmthEvery;
            if (cool)
            {
                기사불빛Policy.FadeTable(warmthTable, warmthDue, 기사불빛Policy.WarmthFade, 기사불빛Policy.WarmthTail);
                warmthDue = 0f;
            }
            var cells = Cells;
            for (var at = 0; at < cells.Length; at += 4)
            {
                if ((cells[at] | cells[at + 1] | cells[at + 2] | cells[at + 3]) == 0) continue;
                cells[at] = riderTable[cells[at]];
                cells[at + 1] = storeTable[cells[at + 1]];
                cells[at + 2] = dropTable[cells[at + 2]];
                if (cool) cells[at + 3] = warmthTable[cells[at + 3]];
            }
        }

        // 그 자리의 한 겹의 빛. 판 밖은 0.
        internal byte At(int layer, float x, float z)
        {
            var ix = (int)Math.Floor((x - X0) / Cell);
            var iz = (int)Math.Floor((z - Z0) / Cell);
            if (layer < 0 || layer > 3 || ix < 0 || iz < 0 || ix >= Width || iz >= Height) return 0;
            return Cells[(iz * Width + ix) * 4 + layer];
        }

        // 한 겹에서 floor 이상으로 밝은 칸의 수.
        internal int Lit(int layer, int floor = 1)
        {
            var count = 0;
            if (layer < 0 || layer > 3) return 0;
            for (var at = layer; at < Cells.Length; at += 4)
                if (Cells[at] >= floor) count++;
            return count;
        }

        // 기사의 자취나 온기로 밝은 칸의 수 (지도가 얼마나 넓게 밝아졌는가).
        internal int Awake(int floor = 24)
        {
            var count = 0;
            for (var at = 0; at < Cells.Length; at += 4)
                if (Cells[at + 기사불빛Policy.Rider] >= floor || Cells[at + 기사불빛Policy.Warmth] >= floor) count++;
            return count;
        }

        // 가장자리 한 줄에 빛이 있는가 (없어야 한다).
        internal bool EdgeLit()
        {
            for (var ix = 0; ix < Width; ix++)
                for (var layer = 0; layer < 4; layer++)
                    if (Cells[ix * 4 + layer] != 0 || Cells[((Height - 1) * Width + ix) * 4 + layer] != 0) return true;
            for (var iz = 0; iz < Height; iz++)
                for (var layer = 0; layer < 4; layer++)
                    if (Cells[iz * Width * 4 + layer] != 0 || Cells[(iz * Width + Width - 1) * 4 + layer] != 0) return true;
            return false;
        }
    }
}
