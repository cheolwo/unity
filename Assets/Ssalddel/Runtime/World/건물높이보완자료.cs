using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 건물높이보완자료
    {
        public string schema, revision, mapRevision, mapHash, sourceUrl;
        public bool presentationOnly;
        public 건물높이보완항목[] buildings;
    }

    [Serializable] public sealed class 건물높이보완항목
    {
        public string id, street, houseNumber, sourceHash, recordCreationDate, matchingStatus;
        public float heightMeters;
    }

    /// <summary>주소·필지 후보의 높이만 표현에 연결한다. 공식 건물 동일성이나 업무 상태를 확정하지 않는다.</summary>
    public sealed class 건물높이보완조회
    {
        private readonly Dictionary<string, float> heights;
        public int Count => heights.Count;
        private 건물높이보완조회(Dictionary<string, float> values) { heights = values; }
        public float Resolve(참고건물 building) => heights.TryGetValue(building.id, out var height) ? height : building.height;

        public static 건물높이보완조회 Validate(건물높이보완자료 overlay, 공간참고자료 map, byte[] mapBytes)
        {
            if (overlay == null || map?.buildings == null || mapBytes == null || overlay.buildings == null)
                throw new ArgumentException("HeightOverlayMissing");
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(mapBytes)).Replace("-", "");
            if (overlay.schema != "sagajeong-building-height-overlay.v1" || string.IsNullOrWhiteSpace(overlay.revision)
                || overlay.mapRevision != map.revision || !string.Equals(overlay.mapHash, hash, StringComparison.OrdinalIgnoreCase)
                || !overlay.presentationOnly || overlay.sourceUrl != "https://www.data.go.kr/data/15134735/openapi.do")
                throw new ArgumentException("HeightOverlayBoundaryMismatch");
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var item in overlay.buildings)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.street)
                    || string.IsNullOrWhiteSpace(item.houseNumber) || item.matchingStatus != "RoadAndParcelAgreeCandidate"
                    || !float.IsFinite(item.heightMeters) || item.heightMeters <= 0 || item.heightMeters > 200
                    || item.sourceHash == null || item.sourceHash.Length != 64 || !item.sourceHash.All(Uri.IsHexDigit)
                    || !DateTime.TryParseExact(item.recordCreationDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                    throw new ArgumentException("HeightOverlayEvidenceInvalid");
                var candidates = map.buildings.Where(b => b != null && b.id == item.id).ToArray();
                if (candidates.Length != 1 || candidates[0].heightKind != "SymbolicHeight"
                    || candidates[0].street != item.street || candidates[0].houseNumber != item.houseNumber
                    || map.buildings.Count(b => b != null && b.street == item.street && b.houseNumber == item.houseNumber) != 1
                    || !values.TryAdd(item.id, item.heightMeters))
                    throw new ArgumentException("HeightOverlayBuildingMismatch");
            }
            return new 건물높이보완조회(values);
        }
    }
}
