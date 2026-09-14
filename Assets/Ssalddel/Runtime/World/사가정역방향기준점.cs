using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable]
    public sealed class 역세권방향기준점자료
    {
        public string schemaVersion;
        public string revision;
        public string stationStableId;
        public string mapRevision;
        public string mapSha256;
        public string sourceUrl;
        public string sourceRawSha256;
        public string attribution;
        public string licenseUrl;
        public string coordinateMethod;
        public double originLatitude;
        public double originLongitude;
        public float offsetX;
        public float offsetZ;
        public string authorityBoundary;
        public 역세권방향기준점[] anchors;
    }

    [Serializable]
    public sealed class 역세권방향기준점
    {
        public string stableId;
        public string kindCode;
        public string displayNameKo;
        public int exitNumber;
        public string sourceNodeId;
        public int sourceNodeVersion;
        public double latitude;
        public double longitude;
        public float x;
        public float z;
        public string directionBasisCode;
        public string placementQualityCode;
        public bool traversalReady;
        public bool interactionReady;
        public bool operationalAuthority;
    }

    /// <summary>
    /// 사가정역 출구의 읽기 전용 위치 근거를 검증한다. 통행·상호작용·운영 권위를 만들지 않는다.
    /// </summary>
    public sealed class 사가정역방향기준점조회
    {
        public const string ExpectedSchemaVersion = "station-diorama-orientation-anchors.v1";
        public const string ExpectedRevision = "sagajeong-station-exits.r1";
        public const string ExpectedStationStableId = "station:kr:kric:s1107:0722";
        public const string ExpectedSourceRawSha256 =
            "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3";
        public const string RequiredAuthorityBoundary =
            "ReferenceOnly;NoNavigation;NoTraversal;NoInteraction;NoOperationalAuthority;GeneralizedProceduralAppearance";

        private readonly IReadOnlyList<역세권방향기준점> anchors;

        private 사가정역방향기준점조회(IReadOnlyList<역세권방향기준점> values,
            string attribution, string revision)
        {
            anchors = values;
            Attribution = attribution;
            Revision = revision;
        }

        public IReadOnlyList<역세권방향기준점> Anchors => anchors;
        public string Attribution { get; }
        public string Revision { get; }

        public 역세권방향기준점 Exit(int exitNumber)
            => anchors.SingleOrDefault(value => value.exitNumber == exitNumber);

        public static 사가정역방향기준점조회 Validate(역세권방향기준점자료 data,
            공간참고자료 map, byte[] mapBytes)
        {
            if (data == null || map == null || mapBytes == null || mapBytes.Length == 0)
                throw new ArgumentException("StationOrientationAnchorInputMissing");
            if (!string.Equals(data.schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal)
                || !string.Equals(data.revision, ExpectedRevision, StringComparison.Ordinal)
                || !string.Equals(data.stationStableId, ExpectedStationStableId, StringComparison.Ordinal))
                throw new ArgumentException("StationOrientationAnchorIdentityMismatch");
            if (!string.Equals(data.mapRevision, map.revision, StringComparison.Ordinal)
                || !string.Equals(data.mapSha256, Hash(mapBytes), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("StationOrientationAnchorMapMismatch");
            if (!string.Equals(data.sourceRawSha256, ExpectedSourceRawSha256,
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(data.sourceUrl)
                || string.IsNullOrWhiteSpace(data.attribution)
                || string.IsNullOrWhiteSpace(data.licenseUrl))
                throw new ArgumentException("StationOrientationAnchorSourceMissing");
            if (!string.Equals(data.coordinateMethod, map.coordinateMethod, StringComparison.Ordinal)
                || !Approximately(data.offsetX, map.offsetX)
                || !Approximately(data.offsetZ, map.offsetZ))
                throw new ArgumentException("StationOrientationAnchorCoordinateMismatch");
            if (!string.Equals(data.authorityBoundary, RequiredAuthorityBoundary,
                    StringComparison.Ordinal))
                throw new ArgumentException("StationOrientationAnchorAuthorityBoundaryMismatch");
            if (data.anchors == null || data.anchors.Length != 4)
                throw new ArgumentException("StationOrientationAnchorCountMismatch");

            var exitNumbers = new HashSet<int>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceNodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var anchor in data.anchors)
            {
                if (anchor == null || anchor.exitNumber < 1 || anchor.exitNumber > 4
                    || !exitNumbers.Add(anchor.exitNumber))
                    throw new ArgumentException("StationOrientationAnchorExitNumberInvalid");
                var expectedStableId =
                    $"orientation-anchor:station:kr:kric:s1107:0722:exit:{anchor.exitNumber}";
                if (!string.Equals(anchor.stableId, expectedStableId, StringComparison.Ordinal)
                    || !stableIds.Add(anchor.stableId)
                    || string.IsNullOrWhiteSpace(anchor.sourceNodeId)
                    || !anchor.sourceNodeId.StartsWith("osm:node:", StringComparison.Ordinal)
                    || !sourceNodes.Add(anchor.sourceNodeId)
                    || anchor.sourceNodeVersion <= 0)
                    throw new ArgumentException("StationOrientationAnchorStableIdentityInvalid");
                if (!string.Equals(anchor.kindCode, "SubwayExit", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(anchor.displayNameKo)
                    || !string.Equals(anchor.directionBasisCode, "TowardStationReferenceOnly",
                        StringComparison.Ordinal)
                    || !string.Equals(anchor.placementQualityCode,
                        "OsmSourceGeometryNotSurveyed", StringComparison.Ordinal))
                    throw new ArgumentException("StationOrientationAnchorMeaningInvalid");
                if (!Finite(anchor.latitude) || !Finite(anchor.longitude)
                    || !Finite(anchor.x) || !Finite(anchor.z)
                    || Math.Abs(anchor.x - map.offsetX) > map.halfExtent
                    || Math.Abs(anchor.z - map.offsetZ) > map.halfExtent)
                    throw new ArgumentException("StationOrientationAnchorPositionInvalid");
                if (anchor.traversalReady || anchor.interactionReady || anchor.operationalAuthority)
                    throw new ArgumentException("StationOrientationAnchorAuthorityEscalation");
            }

            if (!exitNumbers.SetEquals(new[] { 1, 2, 3, 4 }))
                throw new ArgumentException("StationOrientationAnchorCoverageInvalid");
            return new 사가정역방향기준점조회(
                Array.AsReadOnly(data.anchors.OrderBy(value => value.exitNumber).ToArray()),
                data.attribution, data.revision);
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("X2")));
        }

        private static bool Approximately(float left, float right)
            => Math.Abs(left - right) <= .001f;

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
