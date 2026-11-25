using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 계절 · 변화 그림 구조 E2 (2026-11-23): 구 지도 바탕의 철.
    //  - 구 지도는 회색 바탕 위에 운영하는 동만 색이 깨어난다(회색 동네). 철은 그 위에 「숲 · 공원 · 하천 · 눈 · 젖음」의 옅은 기운으로 얹는다.
    //  - 어디가 숲 · 공원 · 하천인지는 세 구의 색 바탕 그림(hongdal-gu-base)의 색에서 읽는다(녹색이 짙은 곳 · 파란 곳 · 밝은 길).
    //    색 바탕은 불러올 때 한 번 읽고 버린다(LoadGu). 약 10 m 한 칸의 작은 그림으로 남긴다.
    //  - 철은 EnvState(SagajeongDispatchEnvironment.cs) 하나에서 온다. 동 디오라마와 같은 날짜 · 같은 규칙이다.
    //  - 날이 바뀌거나 눈 · 젖음이 달라질 때만 다시 굽는다(날짜 미리 보기로 끌 때는 0.2초에 한 번까지).
    public sealed partial class SagajeongDispatchView
    {
        private const int GuSeasonStep = 4;           // 색 바탕 픽셀 4개(약 10 m)가 한 칸
        private byte[] guSeasonVeg, guSeasonWater, guSeasonRoad, guSeasonNoise, guSeasonPatch, guSeasonSpeck, guSeasonPine;
        private int guSeasonWidth, guSeasonHeight;
        private Texture2D guSeasonTexture;
        private Color32[] guSeasonPixels;
        private string guSeasonKey;
        private float guSeasonBakedAt = -10f;

        private static int SeasonHash(int x, int y, int seed)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0xFFFF;
            }
        }

        private static float SeasonValueNoise(int x, int y, int scale, int seed)
        {
            var gx = x / scale;
            var gy = y / scale;
            var fx = (x - gx * scale) / (float)scale;
            var fy = (y - gy * scale) / (float)scale;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = SeasonHash(gx, gy, seed), b = SeasonHash(gx + 1, gy, seed), c = SeasonHash(gx, gy + 1, seed), d = SeasonHash(gx + 1, gy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) / 65535f;
        }

        // LoadGu: 색 바탕을 버리기 전에 부른다. 줄은 아래(남쪽)부터 — 색 바탕과 같은 방향이라 그대로 그림이 된다.
        private void ReadGuSeasonMasks(Texture2D color)
        {
            var width = color.width / GuSeasonStep;
            var height = color.height / GuSeasonStep;
            if (width <= 0 || height <= 0) return;
            var started = Time.realtimeSinceStartup;
            var raw = color.format == TextureFormat.RGB24 ? color.GetRawTextureData<byte>() : default;
            Color32[] pixels = raw.IsCreated ? null : color.GetPixels32();
            var stride = color.width;
            var count = width * height;
            guSeasonVeg = new byte[count];
            guSeasonWater = new byte[count];
            guSeasonRoad = new byte[count];
            guSeasonNoise = new byte[count];
            guSeasonPatch = new byte[count];
            guSeasonSpeck = new byte[count];
            guSeasonPine = new byte[count];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0;
                for (var sy = 1; sy < GuSeasonStep; sy += 2)
                for (var sx = 1; sx < GuSeasonStep; sx += 2)
                {
                    var index = (y * GuSeasonStep + sy) * stride + x * GuSeasonStep + sx;
                    if (pixels != null) { var p = pixels[index]; r += p.r; g += p.g; b += p.b; }
                    else { r += raw[index * 3]; g += raw[index * 3 + 1]; b += raw[index * 3 + 2]; }
                }
                r /= 4; g /= 4; b /= 4;
                var i = y * width + x;
                var green = g - Mathf.Max(r, b);
                var blue = b - Mathf.Max(r, g);
                var bright = (r + g + b) / 3;
                var spread = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
                guSeasonVeg[i] = (byte)Mathf.Clamp((green - 6) * 14, 0, 255);
                guSeasonWater[i] = (byte)Mathf.Clamp((blue - 30) * 6, 0, 255);
                guSeasonRoad[i] = (byte)Mathf.Clamp((bright - 120) * 4 - spread * 2, 0, 255);
                guSeasonNoise[i] = (byte)(255f * SeasonValueNoise(x, y, 8, 3));
                guSeasonPatch[i] = (byte)(255f * SeasonValueNoise(x, y, 12, 7));
                guSeasonPine[i] = (byte)(255f * SeasonValueNoise(x, y, 4, 19));
                guSeasonSpeck[i] = (byte)(SeasonHash(x, y, 11) >> 8);
            }
            guSeasonWidth = width;
            guSeasonHeight = height;
            Debug.Log($"Hongdal gu season: 칸 {width}×{height} · 준비 {(Time.realtimeSinceStartup - started) * 1000f:0}ms");
        }

        private void ReleaseGuSeason()
        {
            if (guSeasonTexture != null) Destroy(guSeasonTexture);
            guSeasonTexture = null;
            guSeasonKey = null;
        }

        private static Color32 SeasonColour(float r, float g, float b, float a) =>
            new((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        private void BakeGuSeason(EnvState env)
        {
            var count = guSeasonWidth * guSeasonHeight;
            guSeasonPixels ??= new Color32[count];
            if (guSeasonTexture == null)
                guSeasonTexture = new Texture2D(guSeasonWidth, guSeasonHeight, TextureFormat.RGBA32, false)
                    { name = "Hongdal gu season", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var bare = new Color(0.40f, 0.33f, 0.24f);
            var fresh = new Color(0.55f, 0.76f, 0.32f);
            var deep = new Color(0.22f, 0.50f, 0.29f);
            var ginkgo = new Color(0.90f, 0.74f, 0.20f);
            var orange = new Color(0.92f, 0.48f, 0.16f);
            var red = new Color(0.78f, 0.22f, 0.15f);
            var pink = new Color(1.00f, 0.76f, 0.86f);
            var snowWhite = new Color(0.90f, 0.93f, 0.96f);
            var leafBase = Color.Lerp(deep, fresh, env.Fresh);
            for (var i = 0; i < count; i++)
            {
                var v = guSeasonVeg[i] / 255f;
                var w = guSeasonWater[i] / 255f;
                var road = guSeasonRoad[i] / 255f;
                var n = guSeasonNoise[i] / 255f;
                float cr = 0f, cg = 0f, cb = 0f, a = 0f;
                if (v > 0.02f)
                {
                    var p = guSeasonPatch[i] / 255f;
                    var turnColour = p < 0.38f ? ginkgo : p < 0.72f ? orange : red;
                    var leaf = Color.Lerp(leafBase, turnColour, 0.85f * Mathf.Clamp01(env.Autumn * 1.5f - 0.5f * n));
                    var veg = Color.Lerp(bare, leaf, Mathf.Clamp01(env.Leaf * 1.35f - 0.35f * (1f - n)));
                    if (guSeasonPine[i] > 172) veg = Color.Lerp(veg, new Color(0.16f, 0.36f, 0.24f), 0.85f);      // 소나무 자리(표현): 철이 바뀌어도 짙은 녹색
                    var alpha = v * (0.34f + 0.10f * env.Autumn * env.Leaf);
                    if (env.Bloom > 0.02f && n > 0.6f && guSeasonSpeck[i] / 255f > 1f - 0.55f * env.Bloom)
                    {
                        veg = pink;      // 벚꽃: 몇 군데에 모여 핀다
                        alpha = Mathf.Max(alpha, 0.7f * Mathf.Clamp01(env.Bloom * 2f));
                    }
                    var spring = Mathf.Max(env.Azalea, env.Royal);
                    if (spring > 0.02f && n < 0.45f && guSeasonPine[i] <= 172 && guSeasonSpeck[i] / 255f > 1f - 0.40f * spring)
                    {
                        veg = env.Royal > env.Azalea ? new Color(0.90f, 0.30f, 0.56f) : new Color(0.86f, 0.48f, 0.74f);      // 진달래 · 철쭉(규칙 날짜)
                        alpha = Mathf.Max(alpha, 0.6f * Mathf.Clamp01(spring * 2f));
                    }
                    cr = veg.r; cg = veg.g; cb = veg.b; a = alpha;
                }
                if (w > 0.05f)
                {
                    // 비: 흐린 물 · 한겨울: 언 물
                    var murky = Mathf.Clamp01(env.Rain + 0.5f * env.Wet) * 0.35f * w;
                    var ice = env.Ice * 0.55f * w;
                    if (murky > 0f) { cr = Mathf.Lerp(cr, 0.45f, murky / Mathf.Max(0.01f, a + murky)); cg = Mathf.Lerp(cg, 0.40f, murky / Mathf.Max(0.01f, a + murky)); cb = Mathf.Lerp(cb, 0.28f, murky / Mathf.Max(0.01f, a + murky)); a += murky * (1f - a); }
                    if (ice > 0f) { cr = Mathf.Lerp(cr, 0.78f, ice); cg = Mathf.Lerp(cg, 0.86f, ice); cb = Mathf.Lerp(cb, 0.92f, ice); a += ice * (1f - a); }
                }
                if (env.Snow > 0.01f)
                {
                    var s = env.Snow * (0.14f + 0.06f * guSeasonPatch[i] / 255f) * (1f - w) * (1f - 0.75f * road) + v * env.Snow * 0.28f;
                    s = Mathf.Clamp01(s);
                    cr = Mathf.Lerp(cr, snowWhite.r, s / Mathf.Max(0.01f, a + s));
                    cg = Mathf.Lerp(cg, snowWhite.g, s / Mathf.Max(0.01f, a + s));
                    cb = Mathf.Lerp(cb, snowWhite.b, s / Mathf.Max(0.01f, a + s));
                    a += s * (1f - a);
                }
                if (env.Wet > 0.01f)
                {
                    var wet = env.Wet * 0.22f * (1f - w);
                    cr = Mathf.Lerp(cr, 0.05f, wet / Mathf.Max(0.01f, a + wet));
                    cg = Mathf.Lerp(cg, 0.11f, wet / Mathf.Max(0.01f, a + wet));
                    cb = Mathf.Lerp(cb, 0.18f, wet / Mathf.Max(0.01f, a + wet));
                    a += wet * (1f - a);
                }
                guSeasonPixels[i] = SeasonColour(cr, cg, cb, a);
            }
            guSeasonTexture.SetPixels32(guSeasonPixels);
            guSeasonTexture.Apply(false, false);
        }

        // 구 바탕 위, 동의 색 바탕(DrawGuGround의 밝기) 다음, 지사 · 매장 표식 앞에 그린다. Repaint에서만.
        private void DrawGuSeason()
        {
            if (!seasonOn || guDetail == null || guSeasonVeg == null || guSeasonWidth <= 0) return;
            var env = EnvNow();
            var key = $"{env.Date:yyyyMMdd}|{Mathf.RoundToInt(env.Snow * 20)}|{Mathf.RoundToInt(env.Wet * 10)}|{Mathf.RoundToInt(env.Rain * 4)}";
            if (key != guSeasonKey && (guSeasonTexture == null || previewDrag < 0 || Time.realtimeSinceStartup - guSeasonBakedAt > 0.2f))
            {
                guSeasonKey = key;
                guSeasonBakedAt = Time.realtimeSinceStartup;
                BakeGuSeason(env);
            }
            if (guSeasonTexture == null) return;
            // 밤에는 바탕(DrawGuSky)과 함께 가라앉는다.
            var tint = Color.Lerp(Color.white, new Color(0.32f, 0.36f, 0.46f, 0.8f), env.Night);
            GUI.DrawTexture(GuRect(guDetailRect), guSeasonTexture, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        }
    }
}
