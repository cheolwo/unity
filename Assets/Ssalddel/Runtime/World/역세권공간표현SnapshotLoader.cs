using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class StationSpatialSnapshotParser
    {
        public static StationSpatialSnapshotData Parse(byte[] utf8Json)
        {
            if (utf8Json == null || utf8Json.Length == 0)
                throw new ArgumentException("StationDioramaSnapshotJsonMissing");
            if (utf8Json.Length > StationDioramaLocalArtifactReader.MaximumPayloadBytes)
                throw new ArgumentException("StationDioramaSnapshotJsonBudgetExceeded");
            try
            {
                using var stream = new MemoryStream(utf8Json, false);
                var serializer = new DataContractJsonSerializer(
                    typeof(StationSpatialSnapshotData),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                return serializer.ReadObject(stream) as StationSpatialSnapshotData
                       ?? throw new SerializationException("StationDioramaSnapshotJsonNull");
            }
            catch (Exception exception) when (exception is SerializationException
                                               || exception is FormatException
                                               || exception is ArgumentException)
            {
                throw new ArgumentException("StationDioramaSnapshotJsonInvalid", exception);
            }
        }
    }

    public static class StationDioramaSpatialComposer
    {
        private const int MaximumReceipts = 32;
        private const int MaximumBuildings = 20000;
        private const int MaximumRoads = 10000;
        private const int MaximumSurfaces = 20000;
        private const int MaximumAdministrativeAreas = 64;
        private const int MaximumVertices = 2000000;
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
        private static readonly HashSet<string> CoverageKinds = new(StringComparer.Ordinal)
        {
            "ConfirmedBuilt", "ConfirmedOpen", "IncompleteSurfaceEvidence", "MissingCoverage"
        };
        private static readonly HashSet<string> MissingCodes = new(StringComparer.Ordinal)
        {
            "BuildingRightsConflictUnresolved", "LegalDongBoundaryCoverageUnverified",
            "MissingBuildingCoverage", "OsmRelationMembersMissing", "RoadWidthUnavailable",
            "SurfaceCoverageIncomplete", "MissingSurfaceCoverage",
            "TraversalAuthorityUnavailable"
        };

        public static StationDioramaSpatialComposition ComposeOrMissing(
            StationSpatialSnapshotData source,
            StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (presentationProfile == null)
                throw new ArgumentNullException(nameof(presentationProfile));
            try { return Compose(source, descriptor, presentationProfile); }
            catch (ArgumentException exception)
            {
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    SafeCode(exception));
            }
            catch (OverflowException)
            {
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    "StationDioramaGeometryBudgetExceeded");
            }
        }

        private static StationDioramaSpatialComposition Compose(
            StationSpatialSnapshotData source,
            StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile)
        {
            ValidateHeader(source, descriptor);
            var receipts = ValidateReceipts(source.sourceReceipts, descriptor);
            var receiptIds = new HashSet<string>(
                source.sourceReceipts.Select(value => value.sourceId), StringComparer.Ordinal);
            var vertexCount = 0;
            var buildings = ValidateBuildings(source.buildings, descriptor, ref vertexCount);
            var roads = ValidateRoads(source.roads, descriptor, ref vertexCount);
            var surfaces = ValidateSurfaces(source.surfaces, receiptIds, descriptor,
                ref vertexCount);
            var administrativeAreas = ValidateAdministrativeAreas(
                source.administrativeAreas, descriptor, ref vertexCount);
            var coverage = ValidateCoverage(source.coverage, descriptor);
            ValidateTiles(source.tiles, descriptor, coverage);
            var missing = ValidateMissingCoverage(source.missingCoverage, coverage);

            var incomplete = coverage.Any(value => value.Classification == "MissingCoverage"
                                                   || value.Classification ==
                                                   "IncompleteSurfaceEvidence")
                             || missing.Count > 0;
            return new StationDioramaSpatialComposition(descriptor, presentationProfile, true,
                source.revision, source.contentHash, source.status,
                incomplete ? StationDioramaProfileCatalog.PartialCoverage
                    : StationDioramaProfileCatalog.Ready,
                "Ready", receipts, buildings, roads, surfaces, administrativeAreas,
                coverage, missing);
        }

        private static void ValidateHeader(StationSpatialSnapshotData source,
            StationDioramaDatasetDescriptor descriptor)
        {
            Require(source != null, "StationDioramaSnapshotMissing");
            Require(!descriptor.UsesLegacyBaseMap,
                "StationDioramaSelfContainedSnapshotRequired");
            Require(descriptor.HasPinnedSnapshotFingerprint,
                "StationDioramaSnapshotFingerprintMissing");
            Require(source.schemaVersion == descriptor.SchemaVersion
                    && source.revision == descriptor.PrivateReviewRevision
                    && source.status == StationDioramaProfileCatalog.LocalPrivateReviewStatus
                    && source.transitStationStableId == descriptor.TransitStationStableId
                    && source.stationName == descriptor.DisplayName
                    && source.presentationOnly && !source.distributionApproved
                    && !source.gameplayReady && !source.traversalReady,
                "StationDioramaSnapshotContractMismatch");
            Require(IsSha256(source.contentHash)
                    && string.Equals(source.contentHash, descriptor.ExpectedContentHashSha256,
                        StringComparison.OrdinalIgnoreCase),
                "StationDioramaSnapshotContentHashMismatch");
            Require(source.service != null && Text(source.service.lineCode)
                                           && Text(source.service.stationCode),
                "StationDioramaServiceIdentityInvalid");
            var stationParts = descriptor.TransitStationStableId.Split(':');
            Require(stationParts.Length == 5
                    && string.Equals(source.service.lineCode, stationParts[3],
                        StringComparison.OrdinalIgnoreCase)
                    && source.service.stationCode == stationParts[4],
                "StationDioramaServiceIdentityMismatch");
            Require(source.coordinateMethod == descriptor.CoordinateMethod,
                "StationDioramaCoordinateMethodMismatch");
            var frame = source.coordinateFrame;
            Require(frame != null && Near(frame.originLatitude, descriptor.OriginLatitude)
                                  && Near(frame.originLongitude, descriptor.OriginLongitude)
                                  && Near(frame.offsetX, descriptor.OffsetX)
                                  && Near(frame.offsetZ, descriptor.OffsetZ)
                                  && Near(frame.metersPerUnit, 1d)
                                  && Near(frame.halfExtentMeters, descriptor.HalfExtentMeters),
                "StationDioramaCoordinateFrameMismatch");
            ValidateWindow(source.window, descriptor);
            Require(source.sourceReceipts != null && source.sourceReceipts.Length > 0
                    && source.sourceReceipts.Length <= MaximumReceipts
                    && source.buildings != null && source.buildings.Length <= MaximumBuildings
                    && source.roads != null && source.roads.Length <= MaximumRoads
                    && source.surfaces != null && source.surfaces.Length <= MaximumSurfaces
                    && source.administrativeAreas != null
                    && source.administrativeAreas.Length > 0
                    && source.administrativeAreas.Length <= MaximumAdministrativeAreas
                    && source.tiles != null && source.tiles.Length == 4
                    && source.coverage != null && source.missingCoverage != null,
                "StationDioramaSnapshotCollectionBudgetInvalid");
        }

        private static void ValidateWindow(StationSpatialWindowData window,
            StationDioramaDatasetDescriptor descriptor)
        {
            var extent = descriptor.HalfExtentMeters;
            Require(window?.bounds != null && window.wgs84Bounds != null
                    && Near(window.widthMeters, extent * 2d)
                    && Near(window.depthMeters, extent * 2d)
                    && Near(window.bounds.minX, descriptor.OffsetX - extent)
                    && Near(window.bounds.maxX, descriptor.OffsetX + extent)
                    && Near(window.bounds.minZ, descriptor.OffsetZ - extent)
                    && Near(window.bounds.maxZ, descriptor.OffsetZ + extent),
                "StationDioramaWindowMismatch");
            var wgs = window.wgs84Bounds;
            Require(FiniteLatitude(wgs.minLatitude) && FiniteLatitude(wgs.maxLatitude)
                    && FiniteLongitude(wgs.minLongitude) && FiniteLongitude(wgs.maxLongitude)
                    && wgs.minLatitude < descriptor.OriginLatitude
                    && descriptor.OriginLatitude < wgs.maxLatitude
                    && wgs.minLongitude < descriptor.OriginLongitude
                    && descriptor.OriginLongitude < wgs.maxLongitude
                    && wgs.corners != null && wgs.corners.Length == 4,
                "StationDioramaWgs84BoundsInvalid");
            var cornerNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var corner in wgs.corners)
                Require(corner != null && Text(corner.name) && cornerNames.Add(corner.name)
                                       && FiniteLatitude(corner.latitude)
                                       && FiniteLongitude(corner.longitude)
                                       && corner.latitude >= wgs.minLatitude - .0000001d
                                       && corner.latitude <= wgs.maxLatitude + .0000001d
                                       && corner.longitude >= wgs.minLongitude - .0000001d
                                       && corner.longitude <= wgs.maxLongitude + .0000001d,
                    "StationDioramaWgs84CornerInvalid");
        }

        private static IReadOnlyList<StationSpatialSourceReceipt> ValidateReceipts(
            IReadOnlyList<StationSpatialSourceReceiptData> sources,
            StationDioramaDatasetDescriptor descriptor)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<StationSpatialSourceReceipt>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && Text(source.sourceId) && ids.Add(source.sourceId)
                                       && Text(source.datasetId) && IsHttps(source.sourceUrl)
                                       && Text(source.sourceRevision) && IsSha256(source.rawHash)
                                       && source.rawLength >= 0 && Text(source.originalCrs)
                                       && Text(source.licenseCode) && OptionalHttps(source.licenseUrl)
                                       && source.reviewStatus ==
                                       StationDioramaProfileCatalog.LocalPrivateReviewStatus
                                       && !source.distributionApproved
                                       && source.limitations != null
                                       && source.limitations.Length > 0
                                       && source.limitations.Length <= 64
                                       && source.limitations.All(Text),
                    "StationDioramaSourceReceiptInvalid");
                result.Add(new StationSpatialSourceReceipt(source.sourceId, source.datasetId,
                    source.sourceUrl, source.sourceRevision, source.licenseCode,
                    source.licenseUrl ?? string.Empty, source.rawHash, source.rawLength,
                    source.originalCrs,
                    source.reviewStatus, source.distributionApproved,
                    Array.AsReadOnly((string[])source.limitations.Clone())));
            }
            var openStreetMapIds = ids.Where(value => value.StartsWith(
                "openstreetmap:", StringComparison.Ordinal)).ToArray();
            Require(Text(descriptor.ExpectedOpenStreetMapSourceReceiptId)
                    && ids.Contains("vworld:gis-building-integrated-information")
                    && ids.Contains("data-go-kr:standard-nodelink")
                    && ids.Contains("seoul-open-data:administrative-dong-boundary")
                    && openStreetMapIds.Length == 1
                    && openStreetMapIds[0] ==
                    descriptor.ExpectedOpenStreetMapSourceReceiptId,
                "StationDioramaRequiredSourceReceiptMissing");
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialBuilding> ValidateBuildings(
            IReadOnlyList<StationSpatialBuildingData> sources,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<StationSpatialBuilding>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && Text(source.id) && ids.Add(source.id)
                                       && Text(source.sourceFeatureId)
                                       && sourceIds.Add(source.sourceFeatureId)
                                       && IsDigits(source.legalDongCode, 10)
                                       && OptionalText(source.buildingKind)
                                       && OptionalText(source.purposeName)
                                       && HeightKinds.Contains(source.heightKind)
                                       && Text(source.heightPolicyRevision)
                                       && source.reviewStatus ==
                                       StationDioramaProfileCatalog.LocalPrivateReviewStatus,
                    "StationDioramaBuildingEvidenceInvalid");
                Require(Finite(source.heightMeters) && source.heightMeters > 0d
                                                     && source.heightMeters <= 500d,
                    "StationDioramaBuildingHeightInvalid");
                if (source.heightKind == "ObservedSourceHeight")
                    Require(source.observedHeightMeters > 0d
                            && Near(source.heightMeters, source.observedHeightMeters.Value),
                        "StationDioramaObservedHeightMismatch");
                else
                    Require(Near(source.heightMeters, 4d)
                            && (!source.observedHeightMeters.HasValue
                                || Near(source.observedHeightMeters.Value, 0d)),
                        "StationDioramaSymbolicHeightMismatch");
                if (source.observedAboveGroundFloors.HasValue)
                    Require(source.observedAboveGroundFloors.Value >= 0
                            && source.observedAboveGroundFloors.Value <= 200,
                        "StationDioramaObservedFloorInvalid");
                var parts = CopyPolygons(source.parts, descriptor, ref vertexCount,
                    "StationDioramaBuildingGeometryInvalid");
                result.Add(new StationSpatialBuilding(source.id, source.sourceFeatureId,
                    source.legalDongCode, source.buildingKind, source.purposeName,
                    source.heightMeters, source.heightKind, source.observedHeightMeters,
                    source.observedAboveGroundFloors, source.heightPolicyRevision,
                    source.reviewStatus, source.boundaryClipped, parts));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialRoad> ValidateRoads(
            IReadOnlyList<StationSpatialRoadData> sources,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var recordIndices = new HashSet<long>();
            var result = new List<StationSpatialRoad>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && Text(source.id) && ids.Add(source.id)
                                       && source.sourceRecordIndex >= 0
                                       && recordIndices.Add(source.sourceRecordIndex)
                                       && Text(source.fromNodeId) && Text(source.toNodeId)
                                       && OptionalText(source.roadName)
                                       && source.laneCount >= 1 && source.laneCount <= 20
                                       && Text(source.updatedDate)
                                       && source.reviewStatus ==
                                       StationDioramaProfileCatalog.LocalPrivateReviewStatus
                                       && source.presentationOnly && !source.traversalReady
                                       && source.parts != null && source.parts.Length > 0
                                       && source.parts.Length <= 256,
                    "StationDioramaRoadEvidenceInvalid");
                var parts = new List<IReadOnlyList<StationSpatialPoint>>(source.parts.Length);
                foreach (var part in source.parts)
                    parts.Add(CopyLine(part, descriptor, ref vertexCount,
                        "StationDioramaRoadGeometryInvalid"));
                result.Add(new StationSpatialRoad(source.id, source.sourceRecordIndex,
                    source.fromNodeId, source.toNodeId, source.roadName, source.laneCount,
                    source.updatedDate, source.reviewStatus, source.boundaryClipped,
                    parts.AsReadOnly()));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialSurface> ValidateSurfaces(
            IReadOnlyList<StationSpatialSurfaceData> sources,
            ISet<string> receiptIds,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<StationSpatialSurface>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && Text(source.id) && ids.Add(source.id)
                                       && SurfaceKinds.Contains(source.kind)
                                       && source.evidenceKind == "PublicOpenData"
                                       && source.presentationOnly && Text(source.sourceId)
                                       && receiptIds.Contains(source.sourceId),
                    "StationDioramaSurfaceEvidenceInvalid");
                var parts = CopyPolygons(source.parts, descriptor, ref vertexCount,
                    "StationDioramaSurfaceGeometryInvalid");
                result.Add(new StationSpatialSurface(source.id, source.kind,
                    source.evidenceKind, source.sourceId, source.boundaryClipped, parts));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialAdministrativeArea>
            ValidateAdministrativeAreas(
                IReadOnlyList<StationSpatialAdministrativeAreaData> sources,
                StationDioramaDatasetDescriptor descriptor, ref int vertexCount)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var containsStation = 0;
            var ratio = 0d;
            var result = new List<StationSpatialAdministrativeArea>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && Text(source.administrativeAreaStableId)
                                       && ids.Add(source.administrativeAreaStableId)
                                       && source.administrativeAreaStableId.StartsWith(
                                           "region:kr:hjd:", StringComparison.Ordinal)
                                       && Text(source.displayName) && Text(source.sourceVintage)
                                       && Finite(source.coverageSquareMeters)
                                       && source.coverageSquareMeters > 0d
                                       && source.coverageSquareMeters <= 1000000.1d
                                       && Ratio(source.coverageRatio),
                    "StationDioramaAdministrativeAreaEvidenceInvalid");
                if (source.containsStation) containsStation++;
                ratio += source.coverageRatio;
                var parts = CopyPolygons(source.parts, descriptor, ref vertexCount,
                    "StationDioramaAdministrativeAreaGeometryInvalid");
                result.Add(new StationSpatialAdministrativeArea(
                    source.administrativeAreaStableId, source.displayName,
                    source.coverageRatio, source.boundaryClipped, parts));
            }
            Require(containsStation == 1 && Math.Abs(ratio - 1d) <= .00001d,
                "StationDioramaAdministrativeAreaCoverageInvalid");
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialCoverageCell> ValidateCoverage(
            StationSpatialCoverageData source, StationDioramaDatasetDescriptor descriptor)
        {
            Require(source != null && source.columns == 10 && source.rows == 10
                                   && Near(source.cellSizeMeters, 100d)
                                   && Ratio(source.buildingCoverageThreshold)
                                   && Ratio(source.qualifiedOpenCoverageThreshold)
                                   && source.cells != null
                                   && source.cells.Length == source.columns * source.rows
                                   && source.summary?.classificationCounts != null,
                "StationDioramaCoverageContractInvalid");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var indexes = new HashSet<string>(StringComparer.Ordinal);
            var derivedCounts = CoverageKinds.ToDictionary(value => value, _ => 0,
                StringComparer.Ordinal);
            var result = new List<StationSpatialCoverageCell>(source.cells.Length);
            var minX = descriptor.OffsetX - descriptor.HalfExtentMeters;
            var minZ = descriptor.OffsetZ - descriptor.HalfExtentMeters;
            foreach (var cell in source.cells)
            {
                var indexKey = cell?.xIndex + ":" + cell?.zIndex;
                Require(cell != null && Text(cell.id) && ids.Add(cell.id)
                                     && cell.xIndex >= 0 && cell.xIndex < source.columns
                                     && cell.zIndex >= 0 && cell.zIndex < source.rows
                                     && indexes.Add(indexKey) && cell.bounds != null
                                     && Near(cell.bounds.minX,
                                         minX + cell.xIndex * source.cellSizeMeters)
                                     && Near(cell.bounds.maxX,
                                         minX + (cell.xIndex + 1) * source.cellSizeMeters)
                                     && Near(cell.bounds.minZ,
                                         minZ + cell.zIndex * source.cellSizeMeters)
                                     && Near(cell.bounds.maxZ,
                                         minZ + (cell.zIndex + 1) * source.cellSizeMeters)
                                     && Ratio(cell.buildingCoverageRatio)
                                     && Ratio(cell.openCoverageRatio)
                                     && Ratio(cell.surfaceCoverageRatio)
                                     && Ratio(cell.unknownCoverageRatio)
                                     && CoverageKinds.Contains(cell.classification)
                                     && ValidCoverageEvidence(cell),
                    "StationDioramaCoverageCellInvalid");
                derivedCounts[cell.classification]++;
                result.Add(new StationSpatialCoverageCell(cell.id, cell.xIndex, cell.zIndex,
                    CopyBounds(cell.bounds), cell.classification, cell.buildingCoverageRatio,
                    cell.openCoverageRatio, cell.surfaceCoverageRatio,
                    cell.unknownCoverageRatio,
                    Array.AsReadOnly((string[])cell.evidenceMix.Clone()),
                    cell.boundaryClipped));
            }
            Require(source.summary.classificationCounts.Count == CoverageKinds.Count
                    && CoverageKinds.All(value => source.summary.classificationCounts
                        .TryGetValue(value, out var count) && count == derivedCounts[value])
                    && Finite(source.summary.buildingCoverageSquareMeters)
                    && source.summary.buildingCoverageSquareMeters >= 0d
                    && Finite(source.summary.surfaceCoverageSquareMeters)
                    && source.summary.surfaceCoverageSquareMeters >= 0d
                    && Finite(source.summary.qualifiedOpenCoverageSquareMeters)
                    && source.summary.qualifiedOpenCoverageSquareMeters >= 0d
                    && source.summary.incompleteRelationCount >= 0,
                "StationDioramaCoverageSummaryMismatch");
            return result.AsReadOnly();
        }

        private static bool ValidCoverageEvidence(StationSpatialCoverageCellData cell)
        {
            if (cell.evidenceMix == null || !cell.evidenceMix.All(Text)
                                         || cell.incompleteSourceFeatureIds == null
                                         || !cell.incompleteSourceFeatureIds.All(Text))
                return false;
            if (cell.evidenceMix.Length > 0)
                return true;

            return cell.classification == "MissingCoverage"
                   && Near(cell.buildingCoverageRatio, 0d)
                   && Near(cell.openCoverageRatio, 0d)
                   && Near(cell.surfaceCoverageRatio, 0d)
                   && Near(cell.unknownCoverageRatio, 1d)
                   && cell.incompleteSourceFeatureIds.Length == 0;
        }

        private static void ValidateTiles(IReadOnlyList<StationSpatialTileData> sources,
            StationDioramaDatasetDescriptor descriptor,
            IReadOnlyList<StationSpatialCoverageCell> coverage)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var indexes = new HashSet<string>(StringComparer.Ordinal);
            var cellIds = new HashSet<string>(StringComparer.Ordinal);
            var expectedCells = new HashSet<string>(coverage.Select(value => value.Id),
                StringComparer.Ordinal);
            foreach (var tile in sources)
            {
                var key = tile?.tileIndexX + ":" + tile?.tileIndexZ;
                Require(tile != null && Text(tile.tileStableId) && ids.Add(tile.tileStableId)
                                     && (tile.tileIndexX == -1 || tile.tileIndexX == 0)
                                     && (tile.tileIndexZ == -1 || tile.tileIndexZ == 0)
                                     && indexes.Add(key) && tile.bounds != null
                                     && IsSha256(tile.tileHash)
                                     && tile.buildingCount >= 0 && tile.roadCount >= 0
                                     && tile.surfaceCount >= 0
                                     && tile.coverageCellIds != null
                                     && tile.coverageCellIds.Length == 25
                                     && tile.coverageCellIds.All(value => Text(value)
                                         && expectedCells.Contains(value) && cellIds.Add(value)),
                    "StationDioramaTileContractInvalid");
                var expectedMinX = descriptor.OffsetX + tile.tileIndexX * 500d;
                var expectedMinZ = descriptor.OffsetZ + tile.tileIndexZ * 500d;
                Require(Near(tile.bounds.minX, expectedMinX)
                        && Near(tile.bounds.maxX, expectedMinX + 500d)
                        && Near(tile.bounds.minZ, expectedMinZ)
                        && Near(tile.bounds.maxZ, expectedMinZ + 500d),
                    "StationDioramaTileBoundsMismatch");
            }
            Require(cellIds.SetEquals(expectedCells), "StationDioramaTileCoverageMismatch");
        }

        private static IReadOnlyList<StationSpatialMissingCoverage> ValidateMissingCoverage(
            IReadOnlyList<StationSpatialMissingCoverageData> sources,
            IReadOnlyList<StationSpatialCoverageCell> coverage)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var cellIds = new HashSet<string>(coverage.Select(value => value.Id),
                StringComparer.Ordinal);
            var missingCells = new HashSet<string>(coverage
                .Where(value => value.Classification == "MissingCoverage")
                .Select(value => value.Id), StringComparer.Ordinal);
            var describedMissingCells = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<StationSpatialMissingCoverage>(sources.Count);
            foreach (var source in sources)
            {
                Require(source != null && MissingCodes.Contains(source.code)
                                       && codes.Add(source.code) && Text(source.layer)
                                       && Text(source.detail) && source.affectedCellIds != null
                                       && source.affectedCellIds.Distinct(StringComparer.Ordinal)
                                           .Count() == source.affectedCellIds.Length
                                       && source.affectedCellIds.All(value => Text(value)
                                           && cellIds.Contains(value)),
                    "StationDioramaMissingCoverageInvalid");
                foreach (var id in source.affectedCellIds)
                    if (missingCells.Contains(id)) describedMissingCells.Add(id);
                if (source.code == "OsmRelationMembersMissing")
                    Require(Text(source.sourceFeatureId) && source.missingWayIds != null
                            && source.missingNodeIds != null
                            && (source.missingWayIds.Length > 0
                                || source.missingNodeIds.Length > 0),
                        "StationDioramaMissingRelationEvidenceInvalid");
                result.Add(new StationSpatialMissingCoverage(source.code, source.layer,
                    source.detail, source.sourceFeatureId ?? string.Empty,
                    Array.AsReadOnly((string[])source.affectedCellIds.Clone())));
            }
            Require(codes.Contains("BuildingRightsConflictUnresolved")
                    && codes.Contains("LegalDongBoundaryCoverageUnverified")
                    && codes.Contains("RoadWidthUnavailable")
                    && codes.Contains("TraversalAuthorityUnavailable")
                    && describedMissingCells.SetEquals(missingCells),
                "StationDioramaRequiredMissingCoverageAbsent");
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialPolygon> CopyPolygons(
            IReadOnlyList<StationSpatialPolygonPartData> sources,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount, string code)
        {
            Require(sources != null && sources.Count > 0 && sources.Count <= 256, code);
            var result = new List<StationSpatialPolygon>(sources.Count);
            foreach (var source in sources)
            {
                Require(source?.outer != null && source.holes != null
                                             && source.holes.Length <= 256, code);
                var outer = CopyRing(source.outer, descriptor, ref vertexCount, code);
                var holes = new List<IReadOnlyList<StationSpatialPoint>>(source.holes.Length);
                foreach (var hole in source.holes)
                    holes.Add(CopyRing(hole, descriptor, ref vertexCount, code));
                result.Add(new StationSpatialPolygon(outer, holes.AsReadOnly()));
            }
            return result.AsReadOnly();
        }

        private static IReadOnlyList<StationSpatialPoint> CopyRing(StationSpatialRingData source,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount, string code)
        {
            Require(source?.points != null && source.points.Length >= 4
                                           && source.points.Length <= 100000, code);
            var points = CopyPoints(source.points, descriptor, ref vertexCount, code);
            var first = points[0];
            var last = points[points.Count - 1];
            Require(Near(first.X, last.X) && Near(first.Z, last.Z)
                                             && Math.Abs(SignedAreaTwice(points)) > .000001d,
                code);
            return points;
        }

        private static IReadOnlyList<StationSpatialPoint> CopyLine(
            StationSpatialLinePartData source, StationDioramaDatasetDescriptor descriptor,
            ref int vertexCount, string code)
        {
            Require(source?.points != null && source.points.Length >= 2
                                           && source.points.Length <= 100000, code);
            var points = CopyPoints(source.points, descriptor, ref vertexCount, code);
            var hasLength = false;
            for (var index = 1; index < points.Count; index++)
                if (!Near(points[index - 1].X, points[index].X)
                    || !Near(points[index - 1].Z, points[index].Z))
                {
                    hasLength = true;
                    break;
                }
            Require(hasLength, code);
            return points;
        }

        private static IReadOnlyList<StationSpatialPoint> CopyPoints(
            IReadOnlyList<StationSpatialPointData> source,
            StationDioramaDatasetDescriptor descriptor, ref int vertexCount, string code)
        {
            vertexCount = checked(vertexCount + source.Count);
            Require(vertexCount <= MaximumVertices, "StationDioramaGeometryBudgetExceeded");
            var minX = descriptor.OffsetX - descriptor.HalfExtentMeters - .001d;
            var maxX = descriptor.OffsetX + descriptor.HalfExtentMeters + .001d;
            var minZ = descriptor.OffsetZ - descriptor.HalfExtentMeters - .001d;
            var maxZ = descriptor.OffsetZ + descriptor.HalfExtentMeters + .001d;
            var result = new StationSpatialPoint[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                var point = source[index];
                Require(point != null && Finite(point.x) && Finite(point.z)
                                      && point.x >= minX && point.x <= maxX
                                      && point.z >= minZ && point.z <= maxZ, code);
                result[index] = new StationSpatialPoint(point.x, point.z);
            }
            return Array.AsReadOnly(result);
        }

        private static StationSpatialBounds CopyBounds(StationSpatialBoundsData source)
            => new(source.minX, source.minZ, source.maxX, source.maxZ);

        private static double SignedAreaTwice(IReadOnlyList<StationSpatialPoint> points)
        {
            var area = 0d;
            for (var index = 1; index < points.Count; index++)
                area += points[index - 1].X * points[index].Z
                        - points[index].X * points[index - 1].Z;
            return area;
        }

        private static bool Text(string value) => !string.IsNullOrWhiteSpace(value)
                                                  && value.Length <= 4096;
        private static bool OptionalText(string value) => value != null && value.Length <= 4096;
        private static bool Ratio(double value) => Finite(value) && value >= 0d && value <= 1d;
        private static bool IsDigits(string value, int length)
            => value != null && value.Length == length && value.All(x => x >= '0' && x <= '9');
        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        private static bool IsHttps(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
        private static bool OptionalHttps(string value)
            => string.IsNullOrEmpty(value) || IsHttps(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool FiniteLatitude(double value) => Finite(value) && value >= -90d && value <= 90d;
        private static bool FiniteLongitude(double value) => Finite(value) && value >= -180d && value <= 180d;
        private static bool Near(double left, double right) => Math.Abs(left - right) <= .000001d;
        private static string SafeCode(ArgumentException exception)
        {
            var value = exception.Message;
            return string.IsNullOrWhiteSpace(value) || value.Length > 96
                   || value.IndexOfAny(new[] { '/', '\\' }) >= 0
                ? "StationDioramaSnapshotRejected"
                : value;
        }
        private static void Require(bool condition, string code)
        {
            if (!condition) throw new ArgumentException(code);
        }
    }

    /// <summary>자가 완결 역세권 snapshot의 Editor 비공개 읽기 진입점.</summary>
    public static class StationDioramaSpatialPipeline
    {
        public static StationDioramaSpatialComposition LoadLocalPrivateReviewForEditor(
            StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            presentationProfile ??= StationDioramaProfileCatalog.StandardOneKilometer;
            if (descriptor.UsesLegacyBaseMap)
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    "StationDioramaSelfContainedSnapshotRequired");
#if UNITY_EDITOR
            try
            {
                var bytes = StationDioramaLocalArtifactReader.Read(descriptor);
                return ParseAndCompose(bytes, descriptor, presentationProfile);
            }
            catch (ArgumentException exception)
            {
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    SafeCode(exception));
            }
#else
            return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                "StationDioramaPrivateEditorOnly");
#endif
        }

        public static StationDioramaSpatialComposition ParseAndCompose(byte[] bytes,
            StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile = null)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            presentationProfile ??= StationDioramaProfileCatalog.StandardOneKilometer;
            if (descriptor.UsesLegacyBaseMap)
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    "StationDioramaSelfContainedSnapshotRequired");
            try
            {
                if (!descriptor.HasPinnedSnapshotFingerprint)
                    throw new ArgumentException("StationDioramaSnapshotFingerprintMissing");
                if (!string.Equals(StationDioramaLocalArtifactReader.Hash(bytes),
                        descriptor.ExpectedAssetHashSha256, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("StationDioramaSnapshotAssetHashMismatch");
                return StationDioramaSpatialComposer.ComposeOrMissing(
                    StationSpatialSnapshotParser.Parse(bytes), descriptor, presentationProfile);
            }
            catch (ArgumentException exception)
            {
                return StationDioramaSpatialComposition.Missing(descriptor, presentationProfile,
                    SafeCode(exception));
            }
        }

        private static string SafeCode(ArgumentException exception)
        {
            var value = exception.Message;
            return string.IsNullOrWhiteSpace(value) || value.Length > 96
                   || value.IndexOfAny(new[] { '/', '\\' }) >= 0
                ? "StationDioramaSnapshotRejected"
                : value;
        }
    }
}
