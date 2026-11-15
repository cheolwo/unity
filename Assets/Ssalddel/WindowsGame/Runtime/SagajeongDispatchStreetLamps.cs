using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 길가 불빛 (품6, 2026-11-14. 제안서 9절 4번: 사용자가 넣는 쪽으로 정했다).
    //  - 표현이다. 가로등의 자리 자료는 없다. 길 그래프(hongdal-dong-routes, 그것도 표현값이다)의 큰길과 골목을 따라 걸으며
    //    이미 놓은 불빛에서 일정한 거리보다 멀어지면 하나씩 놓는다. 실제 가로등의 자리 · 수와는 관계가 없다.
    //    그래서 설정의 이름도 「길가 불빛(표현)」이다. 나무 · 횡단보도 · 간판은 그리지 않는다.
    //  - 가까이 볼 때만 보인다. 세기는 가까이 보기의 정도(CloseNow)의 제곱이다. 멀리서는 0이라 화면이 전과 똑같다.
    //  - 그리는 방식은 기사 · 가게의 빛과 같다: 메시 좌표로 읽는 밝기 그림 한 장(_HongdalLampMap)을 셰이더가 읽어
    //    땅 · 길과 벽의 아래쪽을 밝힌다. 색은 거의 넣지 않는다(음식점의 주황빛 · 기사의 연둣빛과 헷갈리지 않게).
    //  - 그림은 처음 가까이 볼 때 한 번 만든다. 시계 · 주문 · 업무 상태는 읽지도 바꾸지도 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string LampPreference = "hongdal.windows.streetLamps";
        private const float LampRoadGap = 34f;          // 큰길: 불빛 사이 (m, 표현값)
        private const float LampAlleyGap = 27f;         // 골목: 불빛 사이 (m, 표현값)
        private const float LampRoadReach = 4.6f;        // 큰길 불빛이 번지는 정도 (m, 표현값)
        private const float LampAlleyReach = 3.3f;      // 골목 불빛이 번지는 정도 (m, 표현값)
        private const float LampAlleyLight = 0.75f;     // 골목 불빛의 밝기 (큰길 = 1)
        private const float LampSide = 4f;              // 큰길: 가운데 선에서 번갈아 비켜 놓는다 (m, 표현값)
        private const float LampWalk = 3f;              // 길을 따라 걷는 걸음 (m)
        private const float LampCellMetres = 1.5f;      // 빛 그림의 한 칸 (m)
        private const int LampMapLimit = 2048;          // 빛 그림의 한 변 (칸). 넘으면 칸을 키운다
        private const float LampMargin = 24f;           // 길의 범위 밖으로 두는 여백 (m)

        private static readonly int LampMapId = Shader.PropertyToID("_HongdalLampMap");
        private static readonly int LampRectId = Shader.PropertyToID("_HongdalLampRect");
        private static readonly int LampId = Shader.PropertyToID("_HongdalLamp");

        private bool lampsOn = true;
        private bool lampsRead;
        private bool lampMapTried;
        private Texture2D lampMap;
        private int lampCount, lampRoadCount;
        private float lampCell;

        // 취향. 검증 촬영(--sagajeong-*)에서는 쓰지 않는다(지도 효과와 같다).
        private bool LampsOn
        {
            get
            {
                if (lampsRead) return lampsOn;
                lampsRead = true;
                lampsOn = !mapEffectsPersistent || PlayerPrefs.GetInt(LampPreference, 1) == 1;
                return lampsOn;
            }
        }

        private void SetLamps(bool on)
        {
            if (LampsOn == on) return;
            lampsOn = on;
            if (mapEffectsPersistent)
            {
                PlayerPrefs.SetInt(LampPreference, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            Debug.Log("Hongdal street lamps: " + (on ? "켬" : "끔"));
        }

        // 프레임마다(SyncCloseShader). close = 가까이 보기의 정도(0~1).
        private void SyncLamps(float close)
        {
            var strength = 0f;
            if (close > 0.02f && LampsOn)
            {
                EnsureLampMap();
                if (lampMap != null) strength = close * close;
            }
            Shader.SetGlobalFloat(LampId, strength);
        }

        private void EnsureLampMap()
        {
            if (lampMapTried) return;
            var graph = EnsureRouteGraph();
            if (graph == null)
            {
                if (routeGraphTried) lampMapTried = true;      // 길 그래프가 없으면 불빛도 없다
                return;
            }
            lampMapTried = true;
            if (graph.Nodes == 0 || graph.Edges == 0) return;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (var node = 0; node < graph.Nodes; node++)
            {
                minX = Mathf.Min(minX, graph.X[node]);
                maxX = Mathf.Max(maxX, graph.X[node]);
                minZ = Mathf.Min(minZ, graph.Z[node]);
                maxZ = Mathf.Max(maxZ, graph.Z[node]);
            }
            var x0 = minX - LampMargin;
            var z0 = minZ - LampMargin;
            var spanX = maxX - minX + 2f * LampMargin;
            var spanZ = maxZ - minZ + 2f * LampMargin;
            lampCell = Mathf.Max(LampCellMetres, Mathf.Max(spanX, spanZ) / LampMapLimit);
            var width = Mathf.Max(1, Mathf.CeilToInt(spanX / lampCell));
            var height = Mathf.Max(1, Mathf.CeilToInt(spanZ / lampCell));
            var cells = new byte[width * height];

            // 자리 고르기: 길을 따라 걸으며, 이미 놓은 불빛에서 정한 거리보다 멀어지면 하나 놓는다. 큰길 먼저, 골목은 그다음.
            // 길의 순서가 같으면 자리도 늘 같다(난수를 쓰지 않는다).
            var bucket = Mathf.Max(LampRoadGap, LampAlleyGap);
            var gridWidth = Mathf.CeilToInt(spanX / bucket) + 1;
            var gridHeight = Mathf.CeilToInt(spanZ / bucket) + 1;
            var head = new int[gridWidth * gridHeight];
            for (var at = 0; at < head.Length; at++) head[at] = -1;
            var placedX = new List<float>();
            var placedZ = new List<float>();
            var next = new List<int>();
            lampCount = 0;
            lampRoadCount = 0;
            for (var pass = 0; pass < 2; pass++)
            {
                var kind = pass == 0 ? 길그래프.Road : 길그래프.Alley;
                var gap = pass == 0 ? LampRoadGap : LampAlleyGap;
                var reach = (pass == 0 ? LampRoadReach : LampAlleyReach) / lampCell;
                var light = pass == 0 ? 1f : LampAlleyLight;
                for (var edge = 0; edge < graph.Edges; edge++)
                {
                    if (graph.Kind[edge] != kind) continue;
                    float ax = graph.X[graph.A[edge]], az = graph.Z[graph.A[edge]];
                    float bx = graph.X[graph.B[edge]], bz = graph.Z[graph.B[edge]];
                    var length = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (length < 0.01f) continue;
                    var steps = Mathf.Max(1, Mathf.CeilToInt(length / LampWalk));
                    for (var step = 0; step < steps; step++)
                    {
                        var t = (step + 0.5f) / steps;
                        var x = ax + (bx - ax) * t;
                        var z = az + (bz - az) * t;
                        var gridX = Mathf.Clamp((int)((x - x0) / bucket), 0, gridWidth - 1);
                        var gridZ = Mathf.Clamp((int)((z - z0) / bucket), 0, gridHeight - 1);
                        var free = true;
                        for (var dz = -1; dz <= 1 && free; dz++)
                        {
                            for (var dx = -1; dx <= 1 && free; dx++)
                            {
                                int cellX = gridX + dx, cellZ = gridZ + dz;
                                if (cellX < 0 || cellZ < 0 || cellX >= gridWidth || cellZ >= gridHeight) continue;
                                for (var at = head[cellZ * gridWidth + cellX]; at >= 0; at = next[at])
                                {
                                    var ex = placedX[at] - x;
                                    var ez = placedZ[at] - z;
                                    if (ex * ex + ez * ez >= gap * gap) continue;
                                    free = false;
                                    break;
                                }
                            }
                        }
                        if (!free) continue;
                        placedX.Add(x);
                        placedZ.Add(z);
                        next.Add(head[gridZ * gridWidth + gridX]);
                        head[gridZ * gridWidth + gridX] = placedX.Count - 1;
                        lampCount++;
                        var lampX = x;
                        var lampZ = z;
                        if (pass == 0)
                        {
                            var side = (lampRoadCount & 1) == 0 ? LampSide : -LampSide;
                            lampX += -(bz - az) / length * side;
                            lampZ += (bx - ax) / length * side;
                            lampRoadCount++;
                        }
                        StampLamp(cells, width, height, (lampX - x0) / lampCell, (lampZ - z0) / lampCell, reach, light);
                    }
                }
            }
            if (lampCount == 0) return;

            var single = SystemInfo.SupportsTextureFormat(TextureFormat.R8);
            lampMap = new Texture2D(width, height, single ? TextureFormat.R8 : TextureFormat.RGBA32, false, true)
            {
                name = "Hongdal street lamps", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            if (single)
                lampMap.LoadRawTextureData(cells);
            else
            {
                var wide = new byte[cells.Length * 4];
                for (var at = 0; at < cells.Length; at++)
                {
                    wide[at * 4] = cells[at];
                    wide[at * 4 + 3] = 255;
                }
                lampMap.LoadRawTextureData(wide);
            }
            lampMap.Apply(false, true);
            Shader.SetGlobalTexture(LampMapId, lampMap);
            Shader.SetGlobalVector(LampRectId, new Vector4(x0, z0, 1f / (width * lampCell), 1f / (height * lampCell)));
            Debug.Log($"Hongdal street lamps: {lampCount} (큰길 {lampRoadCount}), {width} x {height} cells, {lampCell:0.0} m each · 표현");
        }

        // 한 불빛: 가운데가 가장 밝고 둥글게 옅어진다. 겹치는 곳은 더하지 않고 밝은 쪽을 쓴다.
        private static void StampLamp(byte[] cells, int width, int height, float centreX, float centreZ, float reach, float light)
        {
            var radius = Mathf.CeilToInt(reach * 2.4f);
            var x1 = Mathf.Max(0, Mathf.FloorToInt(centreX) - radius);
            var x2 = Mathf.Min(width - 1, Mathf.FloorToInt(centreX) + radius);
            var z1 = Mathf.Max(0, Mathf.FloorToInt(centreZ) - radius);
            var z2 = Mathf.Min(height - 1, Mathf.FloorToInt(centreZ) + radius);
            for (var z = z1; z <= z2; z++)
            {
                for (var x = x1; x <= x2; x++)
                {
                    var dx = x + 0.5f - centreX;
                    var dz = z + 0.5f - centreZ;
                    var value = (byte)Mathf.RoundToInt(255f * light * Mathf.Exp(-(dx * dx + dz * dz) / (reach * reach)));
                    var at = z * width + x;
                    if (value > cells[at]) cells[at] = value;
                }
            }
        }

        // 방향 촬영: 가장 가까이(매장 창을 닫은 지도)에서 불빛을 켠 그림과 끈 그림의 밝기를 견준다. 끈 그림을 남긴다(켠 그림은 64).
        private IEnumerator ViewProofLamps(string directory, ViewProofReceipt receipt)
        {
            var were = LampsOn;
            lampsOn = true;
            yield return null;
            yield return null;
            receipt.lampCount = lampCount;
            receipt.lampRoadCount = lampRoadCount;
            receipt.lampMapWidth = lampMap != null ? lampMap.width : 0;
            receipt.lampMapHeight = lampMap != null ? lampMap.height : 0;
            receipt.lampCellMetres = lampCell;
            receipt.lampNear = Shader.GetGlobalFloat(LampId);
            yield return new WaitForEndOfFrame();
            var lit = ScreenCapture.CaptureScreenshotAsTexture();
            receipt.lampMeanOn = MeanLight(lit);

            lampsOn = false;
            yield return null;
            yield return null;
            receipt.lampOff = Shader.GetGlobalFloat(LampId);
            yield return new WaitForEndOfFrame();
            var dark = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                receipt.lampMeanOff = MeanLight(dark);
                receipt.lampLitShare = LitShare(lit, dark);
                File.WriteAllBytes(Path.Combine(directory, "69-close-lamps-off.png"), dark.EncodeToPNG());
            }
            finally
            {
                Destroy(dark);
                Destroy(lit);
            }
            lampsOn = were;
            yield return null;
        }

        // 켠 그림이 끈 그림보다 뚜렷이(0.04 넘게) 밝은 칸의 몫(0~1). 빛의 숨처럼 조금씩 달라지는 것은 세지 않는다.
        private static float LitShare(Texture2D on, Texture2D off)
        {
            var bright = on.GetPixels32();
            var plain = off.GetPixels32();
            if (bright.Length != plain.Length || bright.Length == 0) return 0f;
            var lit = 0;
            var count = 0;
            for (var at = 0; at < bright.Length; at += 4)
            {
                var more = bright[at].r + bright[at].g + bright[at].b - plain[at].r - plain[at].g - plain[at].b;
                if (more > 30) lit++;       // 765 가운데 30 = 약 0.04
                count++;
            }
            return (float)lit / count;
        }

        // 그림의 평균 밝기(0~1). 네 칸에 하나씩 본다.
        private static float MeanLight(Texture2D frame)
        {
            var pixels = frame.GetPixels32();
            long sum = 0;
            var count = 0;
            for (var at = 0; at < pixels.Length; at += 4)
            {
                sum += pixels[at].r + pixels[at].g + pixels[at].b;
                count++;
            }
            return count == 0 ? 0f : sum / (count * 765f);
        }
    }
}
