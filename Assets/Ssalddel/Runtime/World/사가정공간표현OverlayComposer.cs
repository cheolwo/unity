using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 기존 SagajeongReference r3를 바꾸지 않고 검증된 표현 자료의 조회 사본만 합성한다.
    /// 검토 자료는 업무, 통행, 충돌 또는 Simulation 권위가 아니다.
    /// </summary>
    public static class 사가정공간표현OverlayComposer
    {
        public const string SchemaVersion = "ssalddel.spatial-presentation-overlay.v1";
        public const string PrivateReviewRevision =
            "sagajeong-spatial-presentation.private-review.r2";
        public const string PublicRevisionPrefix = "sagajeong-spatial-presentation.public.";
        public const string LocalPrivateReviewStatus = "LocalPrivateReview";
        public const string PublicOpenDataStatus = "PublicOpenData";
        public const string BaseMapRevision = "sagajeong-reference.r3";
        public const string BaseMapHash =
            "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
        public const string CoordinateMethod = "WGS84-ECEF-ENU-at-zero-altitude";
        public const double OriginLatitude = 37.5806971d;
        public const double OriginLongitude = 127.0884106d;
        public const double OffsetX = 550d;
        public const double OffsetZ = 8d;
        public const double HalfExtent = 500d;
        public const string RequiredSpatialProvider = "국토교통부";
        public const string RequiredSpatialDatasetId = "data-go-kr-15083092";
        public const string RequiredSpatialRawHash =
            "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755";
        public const long RequiredSpatialRawLength = 135675376L;
        public const string PublicSpatialLicenseCode = "KOGL-Type1";
        public const string PublicSpatialSourceUrl =
            "https://www.data.go.kr/data/15083092/fileData.do";
        public const string PublicSpatialLicenseUrl =
            "https://www.kogl.or.kr/info/licenseType1.do";
        public const string OsmProvider = "OpenStreetMap contributors";
        public const string OsmDatasetId = "sagajeong-r2-map";
        public const string OsmRawHash =
            "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3";
        public const long OsmRawLength = 2993065L;
        public const string OsmLicenseCode = "ODbL-1.0";
        public const string OsmSourceUrl =
            "https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854";
        public const string OsmLicenseUrl = "https://www.openstreetmap.org/copyright";
        public const string OsmSurfaceSourceId = "openstreetmap:sagajeong-r2-map";

        private const int MaximumSourceReceipts = 64;
        private const int MaximumBuildings = 20000;
        private const int MaximumSurfaces = 20000;
        private const int MaximumVertices = 1000000;
        private const double LegacyMatchMinimumIntersectionOverUnion = .20d;
        private const double LegacyMatchMinimumAreaRatio = .25d;
        private const double LegacyMatchMinimumSmallerCoverage = .30d;
        private const double LegacyMatchCentroidScale = .75d;
        private static readonly HashSet<string> HeightKinds = new(StringComparer.Ordinal)
        {
            "ObservedSourceHeight", "SymbolicFallback4m"
        };
        private static readonly HashSet<string> SurfaceKinds = new(StringComparer.Ordinal)
        {
            "commercial", "garden", "park", "parking", "pedestrian", "pitch",
            "playground", "religious", "residential", "school", "water", "wood",
            "grass", "industrial", "cemetery"
        };

        public static 사가정공간표현Composition ComposeOrFallback(
            사가정공간표현OverlayData overlay,
            공간참고자료 baseMap,
            byte[] baseMapBytes,
            사가정공간표현OverlayBoundary boundary)
        {
            ValidateBaseMap(baseMap, baseMapBytes);
            try
            {
                return Compose(overlay, baseMap, boundary);
            }
            catch (ArgumentException exception)
            {
                return Fallback(baseMap, baseMapBytes, SafeCode(exception));
            }
        }

        internal static void ValidateBaseMap(공간참고자료 map, byte[] mapBytes)
        {
            Require(map != null && mapBytes != null && mapBytes.Length > 0,
                "SagajeongReferenceR3Missing");
            Require(Hash(mapBytes) == BaseMapHash, "SagajeongReferenceR3HashMismatch");
            Require(map.revision == BaseMapRevision
                    && map.coordinateMethod == CoordinateMethod
                    && Near(map.offsetX, OffsetX)
                    && Near(map.offsetZ, OffsetZ)
                    && Near(map.halfExtent, HalfExtent)
                    && map.buildings != null && map.buildings.Length == 602
                    && map.roads != null && map.roads.Length == 2397,
                "SagajeongReferenceR3ContractMismatch");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in map.buildings)
                Require(building != null && IsCanonicalOsmWayId(building.id)
                                         && ids.Add(building.id),
                    "SagajeongReferenceR3BuildingIdentityMismatch");
        }

        internal static 사가정공간표현Composition Fallback(
            공간참고자료 baseMap,
            byte[] baseMapBytes,
            string diagnosticCode)
        {
            ValidateBaseMap(baseMap, baseMapBytes);
            return new 사가정공간표현Composition(baseMap, false, BaseMapRevision, BaseMapHash,
                "SagajeongReferenceR3Fallback",
                string.IsNullOrWhiteSpace(diagnosticCode)
                    ? "SpatialOverlayUnavailable"
                    : diagnosticCode,
                Array.Empty<사가정공간표현SourceReceipt>(),
                Array.Empty<사가정공간표현Building>(),
                Array.Empty<사가정공간표현Surface>(),
                new Dictionary<string, 사가정공간표현Building>(StringComparer.Ordinal));
        }

        private static 사가정공간표현Composition Compose(
            사가정공간표현OverlayData overlay,
            공간참고자료 baseMap,
            사가정공간표현OverlayBoundary boundary)
        {
            ValidateHeader(overlay, baseMap, boundary);
            var receipts = ValidateReceipts(overlay, boundary);
            var publicReceiptSourceIds = BuildPublicReceiptSourceIds(overlay.sourceReceipts);
            var baseBuildings = baseMap.buildings.ToDictionary(x => x.id, StringComparer.Ordinal);
            var legacyIds = new HashSet<string>(StringComparer.Ordinal);
            var lookup = new Dictionary<string, 사가정공간표현Building>(StringComparer.Ordinal);
            var buildings = new List<사가정공간표현Building>(overlay.buildings.Length);
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var buildingSourceFeatureIds = new HashSet<string>(StringComparer.Ordinal);
            var vertexCount = 0;

            foreach (var source in overlay.buildings)
            {
                Require(source != null && Text(source.id) && buildingIds.Add(source.id)
                                       && Text(source.sourceFeatureId)
                                       && buildingSourceFeatureIds.Add(source.sourceFeatureId)
                                       && OptionalText(source.buildingKind)
                                       && OptionalText(source.purposeName)
                                       && HeightKinds.Contains(source.heightKind)
                                       && Text(source.heightPolicyRevision)
                                       && source.reviewStatus == overlay.status,
                    "SpatialOverlayBuildingEvidenceInvalid");
                Require(Finite(source.heightMeters) && source.heightMeters > 0d
                                                     && source.heightMeters <= 500d,
                    "SpatialOverlayBuildingHeightInvalid");
                if (source.observedHeightMeters.HasValue)
                    Require(Finite(source.observedHeightMeters.Value)
                            && source.observedHeightMeters.Value >= 0d
                            && source.observedHeightMeters.Value <= 500d,
                        "SpatialOverlayObservedHeightInvalid");
                if (source.observedAboveGroundFloors.HasValue)
                    Require(source.observedAboveGroundFloors.Value >= 0
                            && source.observedAboveGroundFloors.Value <= 200,
                        "SpatialOverlayObservedFloorInvalid");
                if (source.heightKind == "ObservedSourceHeight")
                    Require(source.observedHeightMeters > 0d
                            && Near(source.heightMeters, source.observedHeightMeters.Value),
                        "SpatialOverlayObservedHeightEvidenceMismatch");
                else
                    Require(Near(source.heightMeters, 4d)
                            && (!source.observedHeightMeters.HasValue
                                || Near(source.observedHeightMeters.Value, 0d)),
                        "SpatialOverlaySymbolicHeightEvidenceMismatch");

                var aliases = source.legacyOsmIds ?? Array.Empty<string>();
                var aliasCopies = new List<string>(aliases.Length);
                foreach (var alias in aliases)
                {
                    Require(IsCanonicalOsmWayId(alias) && baseBuildings.ContainsKey(alias)
                                                         && legacyIds.Add(alias),
                        "SpatialOverlayLegacyOsmAliasInvalid");
                    aliasCopies.Add(alias);
                }

                var parts = CopyAndValidateParts(source.parts, baseMap, ref vertexCount,
                    "SpatialOverlayBuildingGeometryInvalid");
                ValidateLegacyMatch(source.legacyMatch, aliasCopies, parts, baseBuildings);
                var building = new 사가정공간표현Building(source,
                    aliasCopies.AsReadOnly(), parts);
                buildings.Add(building);

                // 모호한 공간 결속은 근거로 보존하되 기존 r3 건물 대체 조회에는 사용하지 않는다.
                if (source.legacyMatch == null || source.legacyMatch.ambiguous
                                               || source.legacyMatch.method == "None")
                    continue;
                Require(aliasCopies.Count > 0, "SpatialOverlayLegacyMatchAliasMissing");
                foreach (var alias in aliasCopies) lookup.Add(alias, building);
            }

            var surfaces = new List<사가정공간표현Surface>(overlay.surfaces.Length);
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in overlay.surfaces)
            {
                Require(source != null && Text(source.id) && surfaceIds.Add(source.id)
                                       && SurfaceKinds.Contains(source.kind)
                                       && source.evidenceKind == PublicOpenDataStatus
                                       && source.presentationOnly && Text(source.sourceId)
                                       && publicReceiptSourceIds.Contains(source.sourceId),
                    "SpatialOverlaySurfaceEvidenceInvalid");
                var parts = CopyAndValidateParts(source.parts, baseMap, ref vertexCount,
                    "SpatialOverlaySurfaceGeometryInvalid");
                surfaces.Add(new 사가정공간표현Surface(source, parts));
            }

            Require(buildings.Count + surfaces.Count > 0, "SpatialOverlayGeometryMissing");
            return new 사가정공간표현Composition(baseMap, true, overlay.revision,
                overlay.contentHash, overlay.status, "Ready", receipts, buildings.AsReadOnly(),
                surfaces.AsReadOnly(), lookup);
        }

        private static void ValidateHeader(
            사가정공간표현OverlayData data,
            공간참고자료 baseMap,
            사가정공간표현OverlayBoundary boundary)
        {
            Require(data != null, "SpatialOverlayMissing");
            Require(data.schemaVersion == SchemaVersion && data.presentationOnly,
                "SpatialOverlayContractMismatch");

            switch (boundary)
            {
                case 사가정공간표현OverlayBoundary.LocalPrivateReview:
                    Require(data.status == LocalPrivateReviewStatus
                            && data.revision == PrivateReviewRevision
                            && !data.distributionApproved,
                        "SpatialOverlayPrivateBoundaryMismatch");
                    break;
                case 사가정공간표현OverlayBoundary.ApprovedResource:
                    Require(data.status == PublicOpenDataStatus
                            && IsPublicRevision(data.revision)
                            && data.distributionApproved,
                        "SpatialOverlayResourceBoundaryMismatch");
                    break;
                default:
                    throw new ArgumentException("SpatialOverlayBoundaryUnknown");
            }

            Require(data.baseMapRevision == BaseMapRevision
                    && data.baseMapRevision == baseMap.revision
                    && IsSha256(data.baseMapHash)
                    && string.Equals(data.baseMapHash, BaseMapHash,
                        StringComparison.OrdinalIgnoreCase),
                "SpatialOverlayBaseMapBindingMismatch");
            Require(data.coordinateMethod == baseMap.coordinateMethod
                    && data.coordinateMethod == CoordinateMethod
                    && Near(data.originLatitude, OriginLatitude)
                    && Near(data.originLongitude, OriginLongitude)
                    && Near(data.offsetX, baseMap.offsetX)
                    && Near(data.offsetZ, baseMap.offsetZ)
                    && Near(data.halfExtent, baseMap.halfExtent),
                "SpatialOverlayCoordinateContractMismatch");
            Require(string.IsNullOrWhiteSpace(data.contentHash) || IsSha256(data.contentHash),
                "SpatialOverlayContentHashInvalid");
            Require(data.sourceReceipts != null
                    && data.sourceReceipts.Length > 0
                    && data.sourceReceipts.Length <= MaximumSourceReceipts
                    && data.buildings != null && data.buildings.Length <= MaximumBuildings
                    && data.surfaces != null && data.surfaces.Length <= MaximumSurfaces,
                "SpatialOverlayCollectionBudgetInvalid");
        }

        private static IReadOnlyList<사가정공간표현SourceReceipt> ValidateReceipts(
            사가정공간표현OverlayData overlay,
            사가정공간표현OverlayBoundary boundary)
        {
            var result = new List<사가정공간표현SourceReceipt>(overlay.sourceReceipts.Length);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in overlay.sourceReceipts)
            {
                var statusAllowed = boundary == 사가정공간표현OverlayBoundary.ApprovedResource
                    ? source?.reviewStatus == PublicOpenDataStatus
                    : source?.reviewStatus == LocalPrivateReviewStatus
                      || source?.reviewStatus == PublicOpenDataStatus;
                Require(source != null && Text(source.provider) && Text(source.datasetId)
                                       && identities.Add(source.provider + "\n" + source.datasetId)
                                       && IsPublicHttpsUri(source.sourceUrl)
                                       && Text(source.licenseCode)
                                       && IsPublicHttpsUri(source.licenseUrl)
                                       && IsDatasetDate(source.datasetDate)
                                       && IsUtcTimestamp(source.fetchedAtUtc)
                                       && IsSha256(source.rawHash)
                                       && source.rawLength > 0 && source.rawLength <= 16L * 1024 * 1024 * 1024
                                       && Text(source.originalCrs)
                                       && statusAllowed,
                    "SpatialOverlaySourceReceiptInvalid");
                if (boundary == 사가정공간표현OverlayBoundary.ApprovedResource)
                    Require(IsExplicitlyAllowedPublicReceipt(source),
                        "SpatialOverlayPublicSourceReceiptNotApproved");
                result.Add(new 사가정공간표현SourceReceipt(source));
            }

            var required = overlay.sourceReceipts.Where(source =>
                    source.provider == RequiredSpatialProvider
                    && source.datasetId == RequiredSpatialDatasetId
                    && string.Equals(source.rawHash, RequiredSpatialRawHash,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Require(required.Length == 1, "SpatialOverlayRequiredSourceReceiptMissing");
            if (boundary == 사가정공간표현OverlayBoundary.ApprovedResource)
                Require(required[0].licenseCode == PublicSpatialLicenseCode
                        && required[0].reviewStatus == PublicOpenDataStatus,
                    "SpatialOverlayPublicRightsNotApproved");
            else
                Require(required[0].reviewStatus == LocalPrivateReviewStatus
                        && (required[0].licenseCode == "RightsConflictUnresolved"
                            || required[0].licenseCode == PublicSpatialLicenseCode),
                    "SpatialOverlayPrivateRightsBoundaryMismatch");
            return result.AsReadOnly();
        }

        private static HashSet<string> BuildPublicReceiptSourceIds(
            IEnumerable<사가정공간표현SourceReceiptData> receipts)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in receipts)
            {
                if (IsApprovedSpatialReceipt(receipt)) result.Add(RequiredSpatialDatasetId);
                if (IsApprovedOsmReceipt(receipt)) result.Add(OsmSurfaceSourceId);
            }
            return result;
        }

        private static bool IsExplicitlyAllowedPublicReceipt(
            사가정공간표현SourceReceiptData receipt)
            => IsApprovedSpatialReceipt(receipt) || IsApprovedOsmReceipt(receipt);

        private static bool IsApprovedSpatialReceipt(사가정공간표현SourceReceiptData receipt)
            => receipt != null
               && receipt.provider == RequiredSpatialProvider
               && receipt.datasetId == RequiredSpatialDatasetId
               && receipt.sourceUrl == PublicSpatialSourceUrl
               && receipt.licenseCode == PublicSpatialLicenseCode
               && receipt.licenseUrl == PublicSpatialLicenseUrl
               && string.Equals(receipt.rawHash, RequiredSpatialRawHash,
                   StringComparison.OrdinalIgnoreCase)
               && receipt.rawLength == RequiredSpatialRawLength
               && receipt.originalCrs == "EPSG:5186"
               && receipt.reviewStatus == PublicOpenDataStatus;

        private static bool IsApprovedOsmReceipt(사가정공간표현SourceReceiptData receipt)
            => receipt != null
               && receipt.provider == OsmProvider
               && receipt.datasetId == OsmDatasetId
               && receipt.sourceUrl == OsmSourceUrl
               && receipt.licenseCode == OsmLicenseCode
               && receipt.licenseUrl == OsmLicenseUrl
               && string.Equals(receipt.rawHash, OsmRawHash,
                   StringComparison.OrdinalIgnoreCase)
               && receipt.rawLength == OsmRawLength
               && receipt.originalCrs == "EPSG:4326"
               && receipt.reviewStatus == PublicOpenDataStatus;

        private static void ValidateLegacyMatch(사가정공간표현LegacyMatchData match,
            IReadOnlyList<string> aliases,
            IReadOnlyList<사가정공간표현Polygon> parts,
            IReadOnlyDictionary<string, 참고건물> baseBuildings)
        {
            var aliasCount = aliases.Count;
            Require(match != null, "SpatialOverlayLegacyMatchEvidenceInvalid");
            if (match.method == "None")
            {
                Require(aliasCount == 0 && !match.ambiguous
                                        && !match.centroidDistanceMeters.HasValue
                                        && !match.intersectionOverUnion.HasValue
                                        && !match.smallerFootprintCoverage.HasValue,
                    "SpatialOverlayLegacyMatchNoneInvalid");
                return;
            }

            Require((match.method == "FootprintOverlap" && aliasCount == 1 && !match.ambiguous)
                    || ((match.method == "AmbiguousGlobalAliasExcluded"
                         || match.method == "AmbiguousMultipleFootprintsExcluded"
                         || match.method == "WeakFootprintCandidateExcluded")
                        && aliasCount == 0 && match.ambiguous),
                "SpatialOverlayLegacyMatchMethodInvalid");
            Require(match.centroidDistanceMeters.HasValue
                    && Finite(match.centroidDistanceMeters.Value)
                    && match.centroidDistanceMeters.Value >= 0d
                    && match.intersectionOverUnion.HasValue
                    && Finite(match.intersectionOverUnion.Value)
                    && match.intersectionOverUnion.Value >= 0d
                    && match.intersectionOverUnion.Value <= 1d
                    && match.smallerFootprintCoverage.HasValue
                    && Finite(match.smallerFootprintCoverage.Value)
                    && match.smallerFootprintCoverage.Value >= 0d
                    && match.smallerFootprintCoverage.Value <= 1d,
                "SpatialOverlayLegacyMatchEvidenceInvalid");

            if (match.method != "FootprintOverlap") return;

            Require(match.intersectionOverUnion.Value >= LegacyMatchMinimumIntersectionOverUnion
                    && match.smallerFootprintCoverage.Value >= LegacyMatchMinimumSmallerCoverage,
                "SpatialOverlayLegacyMatchThresholdInvalid");
            Require(baseBuildings.TryGetValue(aliases[0], out var legacy),
                "SpatialOverlayLegacyMatchAliasMissing");
            Require(TryMeasure(parts, out var overlayArea, out var overlayCentroidX,
                        out var overlayCentroidZ),
                "SpatialOverlayLegacyMatchGeometryInvalid");
            Require(TryMeasure(legacy.points, out var legacyArea, out var legacyCentroidX,
                        out var legacyCentroidZ),
                "SpatialOverlayLegacyMatchGeometryInvalid");
            var smallerArea = Math.Min(overlayArea, legacyArea);
            var largerArea = Math.Max(overlayArea, legacyArea);
            Require(smallerArea / largerArea >= LegacyMatchMinimumAreaRatio,
                "SpatialOverlayLegacyMatchAreaRatioInvalid");
            var centroidLimit = Math.Max(2d, Math.Sqrt(smallerArea) * LegacyMatchCentroidScale);
            var actualDistance = Math.Sqrt(
                Math.Pow(overlayCentroidX - legacyCentroidX, 2d)
                + Math.Pow(overlayCentroidZ - legacyCentroidZ, 2d));
            Require(actualDistance <= centroidLimit
                    && match.centroidDistanceMeters.Value <= centroidLimit
                    && Math.Abs(actualDistance - match.centroidDistanceMeters.Value) <= .10d,
                "SpatialOverlayLegacyMatchCentroidInvalid");
        }

        private static bool TryMeasure(IReadOnlyList<사가정공간표현Polygon> parts,
            out double area, out double centroidX, out double centroidZ)
        {
            area = 0d;
            centroidX = 0d;
            centroidZ = 0d;
            foreach (var part in parts)
            {
                if (!TryMeasureRing(part.Outer, out var outerArea, out var outerX, out var outerZ))
                    return false;
                area += outerArea;
                centroidX += outerX * outerArea;
                centroidZ += outerZ * outerArea;
                foreach (var hole in part.Holes)
                {
                    if (!TryMeasureRing(hole, out var holeArea, out var holeX, out var holeZ))
                        return false;
                    area -= holeArea;
                    centroidX -= holeX * holeArea;
                    centroidZ -= holeZ * holeArea;
                }
            }
            if (area <= 0.000001d) return false;
            centroidX /= area;
            centroidZ /= area;
            return Finite(centroidX) && Finite(centroidZ);
        }

        private static bool TryMeasure(참고점[] points,
            out double area, out double centroidX, out double centroidZ)
        {
            area = 0d;
            centroidX = 0d;
            centroidZ = 0d;
            if (points == null || points.Length < 4) return false;
            var crossSum = 0d;
            var xSum = 0d;
            var zSum = 0d;
            for (var index = 0; index < points.Length; index++)
            {
                var next = points[(index + 1) % points.Length];
                var current = points[index];
                var cross = (double)current.x * next.z - (double)next.x * current.z;
                crossSum += cross;
                xSum += (current.x + next.x) * cross;
                zSum += (current.z + next.z) * cross;
            }
            if (Math.Abs(crossSum) <= 0.000002d) return false;
            area = Math.Abs(crossSum) * .5d;
            centroidX = xSum / (3d * crossSum);
            centroidZ = zSum / (3d * crossSum);
            return Finite(centroidX) && Finite(centroidZ);
        }

        private static bool TryMeasureRing(IReadOnlyList<사가정공간표현Point> points,
            out double area, out double centroidX, out double centroidZ)
        {
            area = 0d;
            centroidX = 0d;
            centroidZ = 0d;
            if (points == null || points.Count < 4) return false;
            var crossSum = 0d;
            var xSum = 0d;
            var zSum = 0d;
            for (var index = 0; index < points.Count; index++)
            {
                var next = points[(index + 1) % points.Count];
                var current = points[index];
                var cross = current.X * next.Z - next.X * current.Z;
                crossSum += cross;
                xSum += (current.X + next.X) * cross;
                zSum += (current.Z + next.Z) * cross;
            }
            if (Math.Abs(crossSum) <= 0.000002d) return false;
            area = Math.Abs(crossSum) * .5d;
            centroidX = xSum / (3d * crossSum);
            centroidZ = zSum / (3d * crossSum);
            return Finite(centroidX) && Finite(centroidZ);
        }

        private static IReadOnlyList<사가정공간표현Polygon> CopyAndValidateParts(
            사가정공간표현PolygonPartData[] source,
            공간참고자료 baseMap,
            ref int vertexCount,
            string code)
        {
            Require(source != null && source.Length > 0 && source.Length <= 256, code);
            var result = new List<사가정공간표현Polygon>(source.Length);
            foreach (var part in source)
            {
                Require(part?.outer != null && part.holes != null, code);
                var outer = CopyAndValidateRing(part.outer, baseMap, ref vertexCount, code);
                var holes = new List<IReadOnlyList<사가정공간표현Point>>(part.holes.Length);
                foreach (var hole in part.holes)
                    holes.Add(CopyAndValidateRing(hole, baseMap, ref vertexCount, code));
                result.Add(new 사가정공간표현Polygon(outer, holes.AsReadOnly()));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<사가정공간표현Point> CopyAndValidateRing(
            사가정공간표현RingData ring,
            공간참고자료 baseMap,
            ref int vertexCount,
            string code)
        {
            Require(ring?.points != null && ring.points.Length >= 4
                                              && ring.points.Length <= 100000, code);
            vertexCount = checked(vertexCount + ring.points.Length);
            Require(vertexCount <= MaximumVertices, "SpatialOverlayVertexBudgetExceeded");
            var points = new 사가정공간표현Point[ring.points.Length];
            var areaTwice = 0d;
            for (var index = 0; index < ring.points.Length; index++)
            {
                var point = ring.points[index];
                Require(point != null && Finite(point.x) && Finite(point.z)
                                      && point.x >= baseMap.offsetX - baseMap.halfExtent - .001d
                                      && point.x <= baseMap.offsetX + baseMap.halfExtent + .001d
                                      && point.z >= baseMap.offsetZ - baseMap.halfExtent - .001d
                                      && point.z <= baseMap.offsetZ + baseMap.halfExtent + .001d,
                    code);
                points[index] = new 사가정공간표현Point(point.x, point.z);
                if (index > 0)
                {
                    var previous = ring.points[index - 1];
                    areaTwice += previous.x * point.z - point.x * previous.z;
                }
            }

            var first = ring.points[0];
            var last = ring.points[ring.points.Length - 1];
            Require(Near(first.x, last.x) && Near(first.z, last.z)
                                               && Math.Abs(areaTwice) > .000001d, code);
            return Array.AsReadOnly(points);
        }

        private static bool IsPublicRevision(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !value.StartsWith(PublicRevisionPrefix, StringComparison.Ordinal)) return false;
            var suffix = value.Substring(PublicRevisionPrefix.Length);
            return suffix.Length >= 2 && suffix[0] == 'r'
                   && int.TryParse(suffix.Substring(1), NumberStyles.None,
                       CultureInfo.InvariantCulture, out var revision)
                   && revision > 0;
        }

        private static bool IsCanonicalOsmWayId(string value)
        {
            const string prefix = "osm:way:";
            if (string.IsNullOrEmpty(value)
                || !value.StartsWith(prefix, StringComparison.Ordinal)
                || value.Length == prefix.Length || value[prefix.Length] == '0') return false;
            for (var index = prefix.Length; index < value.Length; index++)
                if (value[index] < '0' || value[index] > '9') return false;
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }

        private static bool IsPublicHttpsUri(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

        private static bool IsDatasetDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                       out _)
                   || DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out _);
        }

        private static bool IsUtcTimestamp(string value)
            => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces, out var timestamp)
               && timestamp.Offset == TimeSpan.Zero;

        private static bool Text(string value) => !string.IsNullOrWhiteSpace(value)
                                                  && value.Length <= 2048;
        private static bool OptionalText(string value) => value != null && value.Length <= 2048;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Near(double left, double right) => Math.Abs(left - right) <= .000001d;

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static string SafeCode(ArgumentException exception)
        {
            var code = exception.Message;
            return string.IsNullOrWhiteSpace(code) || code.Length > 96
                ? "SpatialOverlayRejected"
                : code;
        }

        private static void Require(bool condition, string code)
        {
            if (!condition) throw new ArgumentException(code);
        }
    }

    /// <summary>
    /// r3 원본과 선택 표현 자료를 함께 읽는 메모리 전용 결과다.
    /// 로컬 파일 경로와 JSON 원문은 보존하지 않는다.
    /// </summary>
    public sealed class 사가정공간표현Composition
    {
        private readonly IReadOnlyDictionary<string, 사가정공간표현Building> legacyBuildings;

        internal 사가정공간표현Composition(
            공간참고자료 baseMap,
            bool overlayApplied,
            string revision,
            string contentHash,
            string sourceStatus,
            string diagnosticCode,
            IReadOnlyList<사가정공간표현SourceReceipt> sourceReceipts,
            IReadOnlyList<사가정공간표현Building> buildings,
            IReadOnlyList<사가정공간표현Surface> surfaces,
            IDictionary<string, 사가정공간표현Building> legacyBuildings)
        {
            BaseMap = baseMap;
            OverlayApplied = overlayApplied;
            Revision = revision;
            ContentHash = contentHash;
            SourceStatus = sourceStatus;
            DiagnosticCode = diagnosticCode;
            SourceReceipts = sourceReceipts;
            Buildings = buildings;
            Surfaces = surfaces;
            this.legacyBuildings = new ReadOnlyDictionary<string, 사가정공간표현Building>(
                new Dictionary<string, 사가정공간표현Building>(legacyBuildings,
                    StringComparer.Ordinal));
        }

        public 공간참고자료 BaseMap { get; }
        public bool OverlayApplied { get; }
        public string Revision { get; }
        public string ContentHash { get; }
        public string SourceStatus { get; }
        public string DiagnosticCode { get; }
        public IReadOnlyList<사가정공간표현SourceReceipt> SourceReceipts { get; }
        public IReadOnlyList<사가정공간표현Building> Buildings { get; }
        public IReadOnlyList<사가정공간표현Surface> Surfaces { get; }
        public int LegacyBuildingBindingCount => legacyBuildings.Count;

        public bool TryResolveLegacyBuilding(string canonicalOsmWayId,
            out 사가정공간표현Building building)
            => legacyBuildings.TryGetValue(canonicalOsmWayId, out building);

        public float ResolveHeight(참고건물 baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            return legacyBuildings.TryGetValue(baseline.id, out var overlay)
                ? (float)overlay.HeightMeters
                : baseline.height;
        }

        public string ResolveBuildingKind(참고건물 baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            return legacyBuildings.TryGetValue(baseline.id, out var overlay)
                ? overlay.BuildingKind
                : baseline.buildingKind;
        }
    }
}
