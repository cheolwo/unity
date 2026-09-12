using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 운영관찰지역배치자료
    {
        public string schema, mapRevision, mapHash, coordinateMethod;
        public 운영관찰배치지점[] places;
    }

    [Serializable] public sealed class 운영관찰배치지점
    {
        public string semanticPlaceStableId, role, label;
        public float x, y, z, yaw;
    }

    /// <summary>개인 위치가 아닌 가상 표본의 명시적 지역 연결. 업무 상태를 확정하지 않습니다.</summary>
    public static class 운영관찰지역배치검사
    {
        public static Dictionary<string, 운영관찰배치지점> Validate(
            운영관찰지역배치자료 catalog, 공간참고자료 map, byte[] mapBytes)
        {
            if (catalog == null || map == null || mapBytes == null || catalog.places == null)
                throw new ArgumentException("PlacementDataMissing");
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(mapBytes)).Replace("-", "");
            if (catalog.schema != "sagajeong-observation-placement.v1"
                || catalog.mapRevision != map.revision || catalog.coordinateMethod != map.coordinateMethod
                || !string.Equals(catalog.mapHash, hash, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("PlacementMapMismatch");
            var result = new Dictionary<string, 운영관찰배치지점>(StringComparer.Ordinal);
            foreach (var place in catalog.places)
            {
                if (place == null || string.IsNullOrEmpty(place.semanticPlaceStableId)
                    || !place.semanticPlaceStableId.StartsWith("synthetic-place:", StringComparison.Ordinal)
                    || !result.TryAdd(place.semanticPlaceStableId, place))
                    throw new ArgumentException("PlacementIdInvalidOrDuplicate");
                if (!float.IsFinite(place.x) || !float.IsFinite(place.y) || !float.IsFinite(place.z)
                    || !float.IsFinite(place.yaw) || place.y < 0 || place.y > 150
                    || Math.Abs(place.x - map.offsetX) > map.halfExtent
                    || Math.Abs(place.z - map.offsetZ) > map.halfExtent)
                    throw new ArgumentException("PlacementOutOfBounds");
                if (place.role != "restaurant" && place.role != "destination" && place.role != "cargo"
                    && place.role != "warehouse" && place.role != "mart")
                    throw new ArgumentException("PlacementRoleUnsupported");
            }
            return result;
        }
    }
}
