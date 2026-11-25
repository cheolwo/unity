using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Ssalddel.WindowsGame
{
    // 행정동 모듈 디오라마(r54 첫 조각, 2026-11-13)의 순수 규칙. Unity 없이 검사한다(행정동모듈Checks).
    // 화면 표현 규칙이며 게임 시계·주문·업무 상태를 읽거나 바꾸지 않는다.
    internal static class 행정동모듈Policy
    {
        public const string RegionPrefix = "region:kr:hjd:";
        public const float FocusDim = 1f;       // 지금 보는 동
        public const float OtherDim = 0.62f;    // 같이 읽은 이웃 동 (어둡게)
        public const float SeparatedSpread = 0.09f;   // 띄워 보기: 동 가운데끼리 거리를 9% 벌린다 (표현값)

        // 2.5D 화면의 한 점: 땅 위 (동쪽 m, 북쪽 m) → 화면 (오른쪽 m, 아래쪽 m). 기울이지 않으면 위에서 내려다본다.
        // SagajeongDispatchView.FlatDelta의 역이다(지도 회전 yaw, 내려다보는 각 pitch).
        public static (double U, double V) Screen(double east, double north, bool tilted, double yawCos, double yawSin, double pitchSin)
        {
            if (!tilted) return (east, -north);
            return (yawCos * east - yawSin * north, pitchSin * (-yawSin * east - yawCos * north));
        }

        // Screen의 역. 화면 (오른쪽 m, 아래쪽 m) → 땅 위 (동쪽 m, 북쪽 m)
        public static (double East, double North) Ground(double u, double v, bool tilted, double yawCos, double yawSin, double pitchSin)
        {
            if (!tilted) return (u, -v);
            var w = v / pitchSin;
            return (yawCos * u - yawSin * w, -yawSin * u - yawCos * w);
        }

        // 한 동의 경계가 화면의 한 영역(논리 픽셀 width×height)에 다 들어오게 하는 배율과 가운데.
        // 돌려준 MetresPerPixel은 논리 픽셀 하나의 땅 길이(m)이고, (East, North)는 영역 가운데에 놓을 땅의 점이다.
        public static (double MetresPerPixel, double East, double North) Fit(IReadOnlyList<(double East, double North)> ring,
            double width, double height, double margin, bool tilted, double yawCos, double yawSin, double pitchSin)
        {
            if (ring == null || ring.Count == 0 || width <= 0 || height <= 0) throw new ArgumentException("ring or area");
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            foreach (var (east, north) in ring)
            {
                var (u, v) = Screen(east, north, tilted, yawCos, yawSin, pitchSin);
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }
            var usable = Math.Max(0.05, 1 - 2 * margin);
            var metres = Math.Max((maxU - minU) / (width * usable), (maxV - minV) / (height * usable));
            metres = Math.Max(metres, 0.05);      // 점 하나뿐이어도 0으로 나누지 않게
            var (centreEast, centreNorth) = Ground((minU + maxU) * 0.5, (minV + maxV) * 0.5, tilted, yawCos, yawSin, pitchSin);
            return (metres, centreEast, centreNorth);
        }

        // 이웃 동을 어둡게 할 정도. 고른 동이 없으면 모두 밝게.
        public static float DimFor(string focusRegion, string region) =>
            string.IsNullOrEmpty(focusRegion) || focusRegion == region ? FocusDim : OtherDim;

        // ── 행정동 깃발과 밝기 (2026-11-14, 표현값) ──
        // 어둠(닿지 않음) < 흐림(맞닿아 진출할 수 있음) < 깃발 < 운영 < 자리 잡음. 깃발·운영은 운영의 깊이만큼 더 밝아진다.
        // 단계는 영역 모듈의 값이다: 0 미지, 1 진출 중(깃발), 2 운영, 3 자리 잡음. 밝기는 점수가 아니다.
        // 셰이더는 색에 이 값을 곱한다. 너무 낮추면 동 판이 바탕보다 어두워져 구멍처럼 보이므로 어둠도 0.34에서 멈춘다.
        public const float DarkLight = 0.34f;
        public const float CandidateLight = 0.5f;
        public const float FlagLight = 0.62f;
        public const float OperatingLight = 0.82f;
        public const float OtherFlagDim = 0.85f;     // 다른 동을 보고 있을 때 곱한다

        public static float Brightness(int level, bool candidate, double depth)
        {
            var deep = (float)Math.Max(0, Math.Min(1, depth));
            if (level >= 3) return 1f;
            if (level == 2) return OperatingLight + (1f - OperatingLight) * 0.7f * deep;
            if (level == 1) return FlagLight + (OperatingLight - FlagLight) * 0.8f * deep;
            return candidate ? CandidateLight : DarkLight;
        }

        public static float FocusFactor(string focusRegion, string region) =>
            string.IsNullOrEmpty(focusRegion) || focusRegion == region ? 1f : OtherFlagDim;

        // 행정동 지역 ID 모양: region:kr:hjd:<10자리 숫자>
        public static bool IsRegionId(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(RegionPrefix, StringComparison.Ordinal)) return false;
            var code = id.Substring(RegionPrefix.Length);
            if (code.Length != 10) return false;
            foreach (var c in code) if (c < '0' || c > '9') return false;
            return true;
        }

        // 목록 한 줄이 읽을 만한가. 판본 결속(바이트 수·해시)과 범위가 비어 있지 않은지만 본다.
        public static string ValidateEntry(string region, string resource, long bytes, string sha256, double west, double south, double east, double north)
        {
            if (!IsRegionId(region)) return "지역 ID 모양이 아님";
            if (string.IsNullOrEmpty(resource)) return "자원 이름 없음";
            if (bytes <= 0) return "바이트 수 없음";
            if (!IsSha256(sha256)) return "해시 모양이 아님";
            if (!(west < east) || !(south < north)) return "범위가 비었음";
            return null;
        }

        // 동 메시 자원 풀기(지도 자료 적용 S2 첫 조각, 2026-11-16). 굽은 메시(HDIO)를 굽기 뒤 손실 없이 싼 것(HDIZ)이면 풀어서 돌려준다.
        // 싼 꼴(작은 끝): 'HDIZ' · int32 판(1) · int32 원래 길이 · 원래 SHA-256 32바이트 · raw deflate 본문(Tools~/pack_dong_resources.py).
        // 안 싼 HDIO는 그대로 돌려준다. 원래 길이 · 해시가 머리나 목록(rawBytes · rawSha256, 있으면)과 다르면 InvalidDataException.
        public const int PackHeaderBytes = 44;
        public static byte[] UnpackModule(byte[] stored, long expectedRawBytes, string expectedRawSha256)
        {
            if (stored == null || stored.Length < 4) throw new InvalidDataException("empty module");
            if (!(stored[0] == 'H' && stored[1] == 'D' && stored[2] == 'I' && stored[3] == 'Z')) return stored;
            if (stored.Length < PackHeaderBytes) throw new InvalidDataException("short pack header");
            static int Int32At(byte[] b, int at) => b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24;
            if (Int32At(stored, 4) != 1) throw new InvalidDataException("unknown pack version");
            var rawLength = Int32At(stored, 8);
            if (rawLength <= 0 || expectedRawBytes > 0 && rawLength != expectedRawBytes) throw new InvalidDataException("pack length");
            var raw = new byte[rawLength];
            using (var input = new MemoryStream(stored, PackHeaderBytes, stored.Length - PackHeaderBytes, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                var read = 0;
                while (read < rawLength)
                {
                    var n = deflate.Read(raw, read, rawLength - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read != rawLength || deflate.ReadByte() != -1) throw new InvalidDataException("pack body");
            }
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(raw);
            for (var i = 0; i < 32; i++)
                if (hash[i] != stored[12 + i]) throw new InvalidDataException("pack hash");
            if (!string.IsNullOrEmpty(expectedRawSha256) &&
                !string.Equals(BitConverter.ToString(hash).Replace("-", string.Empty), expectedRawSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("pack hash (manifest)");
            return raw;
        }

        public static bool IsSha256(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (var c in text)
                if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        // 메시 머리의 원점이 목록의 공통 원점과 같은가. 같은 원점이어야 동끼리 경계가 맞물린다.
        public static bool SameOrigin(double latitude, double longitude, double expectedLatitude, double expectedLongitude) =>
            Math.Abs(latitude - expectedLatitude) < 1e-9 && Math.Abs(longitude - expectedLongitude) < 1e-9;

        // 기존 역할표의 건물 ID → 모듈 건물 ID. 대응이 없으면 null(역할을 다른 건물에 억지로 얹지 않는다).
        public static string MapRoleBuilding(IReadOnlyList<(string From, string To)> map, string buildingId)
        {
            if (map == null || string.IsNullOrEmpty(buildingId)) return null;
            foreach (var (from, to) in map)
                if (from == buildingId) return string.IsNullOrEmpty(to) ? null : to;
            return null;
        }

        // AL_D010 건축물 주용도 코드 → 짧은 이름. 모르는 코드는 "용도 미상".
        public static string UseLabel(string kind)
        {
            var code = kind == null ? string.Empty : kind.Substring(kind.LastIndexOf(':') + 1);
            switch (code)
            {
                case "01000": return "단독주택";
                case "02000": return "공동주택";
                case "03000": return "제1종 근린생활";
                case "04000": return "제2종 근린생활";
                case "05000": return "문화·집회";
                case "06000": return "종교";
                case "07000": return "판매";
                case "08000": return "운수";
                case "09000": return "의료";
                case "10000": return "교육·연구";
                case "11000": return "노유자";
                case "12000": return "수련";
                case "13000": return "운동";
                case "14000": return "업무";
                case "15000": return "숙박";
                case "16000": return "위락";
                case "17000": return "공장";
                case "18000": return "창고";
                case "19000": return "위험물";
                case "20000": return "자동차 관련";
                case "21000": return "동·식물 관련";
                case "22000": return "자원순환";
                case "23000": return "교정";
                case "24000": return "국방·군사";
                case "25000": return "방송통신";
                case "26000": return "발전";
                case "27000": return "묘지 관련";
                case "28000": return "관광휴게";
                case "29000": return "장례";
                default: return "용도 미상";
            }
        }

        // ── 동 보기 방식 (2026-11-13 사용자 요청: 띄워서도, 붙여서도, 한 동만도) ──

        // 띄워 보기에서 한 동을 옮길 거리. 묶음 가운데에서 그 동 가운데 쪽으로 spread만큼 밀어낸다.
        // 이웃한 두 동의 가운데 거리는 (1 + spread)배가 되어 사이가 벌어진다. 동 안의 모양은 그대로다.
        public static (double East, double North) ExplodeOffset(double centreEast, double centreNorth,
            double unionEast, double unionNorth, double spread) =>
            ((centreEast - unionEast) * spread, (centreNorth - unionNorth) * spread);

        // 다각형의 넓이 중심(m). 경계 상자 가운데보다 ㄱ자 동에서 덜 치우친다.
        public static (double East, double North) Centroid(IReadOnlyList<(double East, double North)> ring)
        {
            double area = 0, cx = 0, cz = 0;
            for (var i = 0; i < ring.Count; i++)
            {
                var (x0, z0) = ring[i];
                var (x1, z1) = ring[(i + 1) % ring.Count];
                var cross = x0 * z1 - x1 * z0;
                area += cross;
                cx += (x0 + x1) * cross;
                cz += (z0 + z1) * cross;
            }
            if (Math.Abs(area) < 1e-9)
            {
                double sx = 0, sz = 0;
                foreach (var (x, z) in ring) { sx += x; sz += z; }
                return ring.Count == 0 ? (0, 0) : (sx / ring.Count, sz / ring.Count);
            }
            return (cx / (3 * area), cz / (3 * area));
        }

        // 이 동이 지금 보이는가. 이 동만 보기면 고른 한 동, 아니면 숨기지 않은 동.
        public static bool Visible(bool solo, string soloRegion, ICollection<string> hidden, string region) =>
            solo ? region == soloRegion : hidden == null || !hidden.Contains(region);

        // 숨길 수 있는가. 적어도 한 동은 보여야 한다.
        public static bool CanHide(int loaded, int hiddenAlready) => loaded - hiddenAlready > 1;

        // 높이를 어디서 얻었는가 → 건물 카드 한 줄
        public static string HeightNote(string heightSource) => heightSource switch
        {
            "height" => "높이: 원본 높이",
            "floors" => "높이: 지상 층수 × 3 m로 추정",
            "symbolic" => "높이: 자료 없음 · 상징 높이 4 m",
            _ => "높이: 출처 미상",
        };
    }

    // 땅 위 한 점이 어느 동에 있는지 빨리 찾는 격자. 경계를 칸 단위로 칠해 둔다(가로줄 채우기).
    // 표식(매장·기사·경로)을 띄워 보기에서 그 동과 함께 옮기고, 숨긴 동의 표식을 감추는 데 쓴다. 칸 경계 근처는 몇 m 틀릴 수 있다.
    internal sealed class 행정동격자
    {
        private readonly int[] cells;
        private readonly int columns;
        private readonly int rows;
        private readonly double west;
        private readonly double south;
        private readonly double cell;

        public 행정동격자(IReadOnlyList<IReadOnlyList<(double East, double North)>> rings, double cellMetres)
        {
            cell = cellMetres;
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            foreach (var ring in rings)
                foreach (var (x, z) in ring)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                }
            if (minX > maxX) { minX = maxX = minZ = maxZ = 0; }
            west = minX;
            south = minZ;
            columns = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cell) + 1);
            rows = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / cell) + 1);
            cells = new int[columns * rows];
            for (var i = 0; i < cells.Length; i++) cells[i] = -1;
            var crossings = new List<double>();
            for (var index = 0; index < rings.Count; index++)
            {
                var ring = rings[index];
                if (ring.Count < 3) continue;
                for (var row = 0; row < rows; row++)
                {
                    var z = south + (row + 0.5) * cell;
                    crossings.Clear();
                    for (var i = 0; i < ring.Count; i++)
                    {
                        var (x0, z0) = ring[i];
                        var (x1, z1) = ring[(i + 1) % ring.Count];
                        if ((z0 > z) == (z1 > z)) continue;
                        crossings.Add(x0 + (z - z0) * (x1 - x0) / (z1 - z0));
                    }
                    crossings.Sort();
                    for (var k = 0; k + 1 < crossings.Count; k += 2)
                    {
                        var first = Math.Max(0, (int)Math.Ceiling((crossings[k] - west) / cell - 0.5));
                        var last = Math.Min(columns - 1, (int)Math.Floor((crossings[k + 1] - west) / cell - 0.5));
                        for (var column = first; column <= last; column++) cells[row * columns + column] = index;
                    }
                }
            }
        }

        // 그 점이 들어 있는 동의 번호(만들 때 준 순서). 어느 동에도 없으면 -1.
        public int At(double east, double north)
        {
            var column = (int)Math.Floor((east - west) / cell);
            var row = (int)Math.Floor((north - south) / cell);
            if (column < 0 || row < 0 || column >= columns || row >= rows) return -1;
            return cells[row * columns + column];
        }
    }
}
