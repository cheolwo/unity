using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 수도권 바탕 — 구 지도를 서울 · 인천 · 경기 끝까지 이어 본다 (2026-11-25 10:44 · 10:50 · 10:59 사용자:
    //  「지금 구지도 같은 형태에서 전국을 모두 관찰할 수 있도록 … 타일로도 쪼개져 있고 … 수도권에서부터 … Play Store에 올리더라도 문제 없을 정도로」).
    //  - 자료: Resources/HongdalCapital/ (만든 곳: artifacts/local/public-data/capital-base-20261125/make_capital_base.py).
    //    땅 덮개 ESA WorldCover(CC BY 4.0) · 지형 Copernicus DEM · 길 국토교통부 표준노드링크 · 경계 통계청 SGIS(vuski/admdongkor) — 모두 재배포 가능(출처 표시).
    //  - 좌표는 구 지도와 같은 미터 판(남서 126.94, 37.523). 조각(512점 JPEG)은 32 m/점 · 8 m/점 두 단계. 보는 곳만 읽고, 오래 안 본 조각은 버린다.
    //  - 구 지도 바탕(세 구)은 그 위에 그대로 깐다. 그 밖은 이 바탕 + 시군구 · 행정동 이름.
    //  - 끌기 · 확대 한계가 수도권 끝까지 넓어진다(ClampGu · GuFullScale). 가장 넓게 보면 전국 지도로 넘어가고, 전국에서 수도권 위로 확대하면 여기로 온다.
    public sealed partial class SagajeongDispatchView
    {
        [Serializable] private sealed class CapitalLevelRaw { public int level; public float metresPerPixel; public int tile, columns, rows; }
        [Serializable] private sealed class CapitalName { public string code, name, sgg, sido; public float x, y, km2; public int dongs; }
        [Serializable] private sealed class CapitalSpace
        {
            public string schema, revision, attributionShort, scope, tilePrefix;
            public float west, east, south, north, x0, y1, width, height;
            public int[] background;
            public CapitalLevelRaw[] levels;
            public CapitalName[] sggs, dongs, sidos;
        }

        private const string CapitalFolder = "HongdalCapital/";
        private const int CapitalCacheLimit = 96;            // 512점 RGB 조각 ≈ 0.75 MB → 약 72 MB
        private const int CapitalLoadsPerFrame = 4;
        private static readonly HashSet<string> CapitalCoreSgg = new() { "11260", "11230", "11215" };      // 세 구는 구 지도 자체 이름표가 있다

        private CapitalSpace capital;
        private bool capitalTried;
        // 전국 바탕(2026-11-25, make_nation_base.py)이 있으면 그것을, 없으면 수도권 바탕을 쓴다. 조각 이름 = 폴더 + 앞말-단계-열-줄.
        private string capitalFolder = CapitalFolder;
        private string capitalPrefix = "hongdal-cap";
        private string capitalTilesDirectory;
        private bool CapitalIsNation => capital?.scope == "nation";
        private HashSet<long>[] capitalPresent;
        private readonly Dictionary<long, Texture2D> capitalTiles = new();
        private readonly LinkedList<long> capitalOrder = new();
        private readonly Dictionary<long, LinkedListNode<long>> capitalNodes = new();
        private readonly List<Rect> capitalNameRects = new();
        private Color capitalBackground = new(30f / 255f, 30f / 255f, 31f / 255f, 1f);

        private bool CapitalReady => LoadCapital();

        private bool LoadCapital()
        {
            if (capitalTried) return capital != null;
            capitalTried = true;
            var text = Resources.Load<TextAsset>("HongdalNation/hongdal-nation-base-space");
            if (text != null) capitalFolder = "HongdalNation/";
            else text = Resources.Load<TextAsset>(CapitalFolder + "hongdal-capital-space");
            // PC: 앱에 싣지 않은 8 m 조각(수도권 밖)은 이 폴더에서 읽는다(실행 인자 --hongdal-tiles-dir= 또는 HONGDAL_TILES_DIR)
            var argument = Environment.GetCommandLineArgs().FirstOrDefault(item => item.StartsWith("--hongdal-tiles-dir=", StringComparison.Ordinal));
            capitalTilesDirectory = argument != null ? argument.Substring("--hongdal-tiles-dir=".Length).Trim('"') : Environment.GetEnvironmentVariable("HONGDAL_TILES_DIR");
            if (text == null)
            {
                Debug.Log("Hongdal capital: 수도권 바탕 자료가 없어 구 지도 범위만 씁니다.");
                return false;
            }
            try
            {
                var space = JsonUtility.FromJson<CapitalSpace>(text.text);
                if (space?.levels == null || space.levels.Length == 0) return false;
                // JsonUtility는 int[][]을 못 읽는다 → 조각 목록은 손으로 읽는다
                capitalPresent = new HashSet<long>[space.levels.Length];
                for (var l = 0; l < space.levels.Length; l++) capitalPresent[l] = ReadCapitalTiles(text.text, l);
                if (space.background is { Length: 3 })
                    capitalBackground = new Color(space.background[0] / 255f, space.background[1] / 255f, space.background[2] / 255f, 1f);
                capital = space;
                if (!string.IsNullOrEmpty(space.tilePrefix)) capitalPrefix = space.tilePrefix;
                Debug.Log($"Hongdal capital: {space.revision} · 시군구 {space.sggs?.Length ?? 0} · 행정동 {space.dongs?.Length ?? 0} · 조각 {capitalPresent[0].Count}+{(capitalPresent.Length > 1 ? capitalPresent[1].Count : 0)}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hongdal capital: 읽지 못함 · " + exception.Message);
                capital = null;
            }
            Resources.UnloadAsset(text);
            return capital != null;
        }

        // "levels":[{"level":0,…,"tiles":[[i,j],…]},…]에서 level번째의 tiles만 읽는다.
        private static HashSet<long> ReadCapitalTiles(string json, int level)
        {
            var set = new HashSet<long>();
            var at = json.IndexOf("\"levels\"", StringComparison.Ordinal);
            for (var l = 0; l <= level && at >= 0; l++) at = json.IndexOf("\"tiles\"", at + 1, StringComparison.Ordinal);
            if (at < 0) return set;
            var start = json.IndexOf('[', at);
            var depth = 0;
            int number = 0, first = -1;
            var reading = false;
            for (var k = start; k < json.Length; k++)
            {
                var c = json[k];
                if (c >= '0' && c <= '9') { number = number * 10 + (c - '0'); reading = true; continue; }
                if (reading)
                {
                    if (first < 0) first = number;
                    else { set.Add(CapitalKey(level, first, number)); first = -1; }
                    number = 0;
                    reading = false;
                }
                if (c == '[') depth++;
                else if (c == ']') { depth--; if (depth == 0) break; }
            }
            return set;
        }

        private static long CapitalKey(int level, int i, int j) => ((long)level << 40) | ((long)i << 20) | (uint)j;

        // 구 미터 판에서 수도권 범위(남쪽이 작은 y)
        private float CapitalX0 => capital.x0;
        private float CapitalX1 => capital.x0 + capital.width;
        private float CapitalY0 => capital.y1 - capital.height;
        private float CapitalY1 => capital.y1;

        // 지도 영역에 수도권 전체가 들어오는 배율(가장 넓은 보기). 자료가 없으면 구 지도 범위.
        private float CapitalFullScale => Mathf.Max(Field.width / capital.width, Field.height / capital.height);

        // 전국 좌표의 한 점이 수도권 바탕 안인가
        private bool InCapital(double longitude, double latitude) =>
            CapitalReady && longitude >= capital.west && longitude <= capital.east && latitude >= capital.south && latitude <= capital.north;

        // 구 지도의 바탕 밑: 바다 빛으로 깔고, 보이는 조각을 그린다. DrawGuMap이 구 바탕보다 먼저 부른다(Repaint).
        private void DrawCapitalBase()
        {
            if (!CapitalReady) return;
            Paint(new Rect(0, 0, Field.width, Field.height), capitalBackground);
            var budget = CapitalLoadsPerFrame;
            DrawCapitalLevel(0, ref budget);
            if (capital.levels.Length > 1 && guScale * capital.levels[1].metresPerPixel >= 0.35f) DrawCapitalLevel(1, ref budget);
        }

        private void DrawCapitalLevel(int level, ref int budget)
        {
            var info = capital.levels[level];
            var span = info.tile * info.metresPerPixel;             // 조각 하나의 미터
            var size = span * guScale;
            // 보이는 미터 범위
            var left = (0f - guOrigin.x) / guScale;
            var right = (Field.width - guOrigin.x) / guScale;
            var top = guSpace.height - (0f - guOrigin.y) / guScale;
            var bottom = guSpace.height - (Field.height - guOrigin.y) / guScale;
            var i0 = Mathf.Max(0, Mathf.FloorToInt((left - capital.x0) / span));
            var i1 = Mathf.Min(info.columns - 1, Mathf.FloorToInt((right - capital.x0) / span));
            var j0 = Mathf.Max(0, Mathf.FloorToInt((capital.y1 - top) / span));
            var j1 = Mathf.Min(info.rows - 1, Mathf.FloorToInt((capital.y1 - bottom) / span));
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    var key = CapitalKey(level, i, j);
                    if (!capitalPresent[level].Contains(key)) continue;            // 바다 · 한 빛깔
                    var texture = CapitalTexture(key, level, i, j, ref budget);
                    if (texture == null) continue;
                    var x = capital.x0 + i * span;
                    var yTop = capital.y1 - j * span;
                    // 이음매가 보이지 않게 반 점씩 겹친다
                    GUI.DrawTexture(new Rect(guOrigin.x + x * guScale - 0.5f, guOrigin.y + (guSpace.height - yTop) * guScale - 0.5f, size + 1f, size + 1f), texture);
                }
        }

        private Texture2D CapitalTexture(long key, int level, int i, int j, ref int budget)
        {
            if (capitalTiles.TryGetValue(key, out var texture))
            {
                var node = capitalNodes[key];
                capitalOrder.Remove(node);
                capitalOrder.AddLast(node);
                return texture;
            }
            if (budget <= 0) return null;
            budget--;
            var name = $"{capitalPrefix}-{level}-{i}-{j}";
            var asset = Resources.Load<TextAsset>(capitalFolder + name);
            byte[] bytes = null;
            if (asset != null) bytes = asset.bytes;
            else if (!string.IsNullOrEmpty(capitalTilesDirectory))
            {
                try
                {
                    var file = System.IO.Path.Combine(capitalTilesDirectory, name + ".bytes");
                    if (System.IO.File.Exists(file)) bytes = System.IO.File.ReadAllBytes(file);
                }
                catch (Exception) { bytes = null; }
            }
            if (bytes == null) { capitalPresent[level].Remove(key); return null; }      // 앱에 없는 조각: 아래 단계(32 m)가 대신 보인다
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            texture.LoadImage(bytes, true);       // 읽은 뒤 CPU 쪽 사본은 버린다
            if (asset != null) Resources.UnloadAsset(asset);
            capitalTiles[key] = texture;
            capitalNodes[key] = capitalOrder.AddLast(key);
            while (capitalTiles.Count > CapitalCacheLimit)
            {
                var old = capitalOrder.First.Value;
                capitalOrder.RemoveFirst();
                capitalNodes.Remove(old);
                if (capitalTiles.Remove(old, out var gone)) Destroy(gone);
            }
            return texture;
        }

        // 시군구 · 행정동 이름. 구 지도 이름표 뒤에 부른다(Repaint). 멀리서는 시군구, 가까이서는 행정동(세 구는 제 이름표가 있어 뺀다).
        private void DrawCapitalNames()
        {
            if (!CapitalReady) return;
            capitalNameRects.Clear();
            var near = guScale >= 0.05f;
            if (CapitalIsNation && guScale < 0.0045f && capital.sidos != null)      // 전국을 멀리서: 시도 이름만
            {
                foreach (var name in capital.sidos) DrawCapitalName(name.x, name.y, name.name, 12, Paper);
                return;
            }
            if (!near && capital.sggs != null)
                foreach (var name in capital.sggs)
                    DrawCapitalName(name.x, name.y, name.name, guScale < 0.016f ? 11 : 13, name.sido == "11" ? Paper : Soft);
            if (near && capital.dongs != null)
                foreach (var name in capital.dongs)
                {
                    if (CapitalCoreSgg.Contains(name.sgg)) continue;
                    DrawCapitalName(name.x, name.y, name.name, 11, Fade(Soft, 0.9f));
                }
        }

        private void DrawCapitalName(float x, float y, string text, int size, Color color)
        {
            var at = GuPoint(x, y);
            if (at.x < -60f || at.y < -20f || at.x > Field.width + 60f || at.y > Field.height + 20f) return;
            var width = PortraitTextWidth(text, size) + 8f;
            var rect = new Rect(at.x - width * 0.5f, at.y - size * 0.75f, width, size * 1.5f);
            foreach (var other in capitalNameRects) if (other.Overlaps(rect)) return;
            capitalNameRects.Add(rect);
            Text(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, size, new Color(0f, 0f, 0f, 0.85f), true, TextAnchor.MiddleCenter);
            Text(rect, text, size, color, true, TextAnchor.MiddleCenter);
        }

        private string CapitalAttribution => CapitalReady ? capital.attributionShort : null;

        private void ReleaseCapital()
        {
            foreach (var texture in capitalTiles.Values) if (texture != null) Destroy(texture);
            capitalTiles.Clear();
            capitalOrder.Clear();
            capitalNodes.Clear();
        }
    }
}
