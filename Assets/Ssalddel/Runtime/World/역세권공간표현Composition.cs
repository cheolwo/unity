using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.World
{
    public readonly struct StationSpatialPoint
    {
        public StationSpatialPoint(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
    }

    public readonly struct StationSpatialBounds
    {
        public StationSpatialBounds(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
    }

    public sealed class StationSpatialPolygon
    {
        internal StationSpatialPolygon(IReadOnlyList<StationSpatialPoint> outer,
            IReadOnlyList<IReadOnlyList<StationSpatialPoint>> holes)
        {
            Outer = outer;
            Holes = holes;
        }
        public IReadOnlyList<StationSpatialPoint> Outer { get; }
        public IReadOnlyList<IReadOnlyList<StationSpatialPoint>> Holes { get; }
    }

    public sealed class StationSpatialSourceReceipt
    {
        internal StationSpatialSourceReceipt(string sourceId, string datasetId,
            string sourceUrl, string sourceRevision, string licenseCode, string licenseUrl,
            string rawHash, long rawLength, string originalCrs, string reviewStatus,
            bool distributionApproved, IReadOnlyList<string> limitations)
        {
            SourceId = sourceId;
            DatasetId = datasetId;
            SourceUrl = sourceUrl;
            SourceRevision = sourceRevision;
            LicenseCode = licenseCode;
            LicenseUrl = licenseUrl;
            RawHash = rawHash;
            RawLength = rawLength;
            OriginalCrs = originalCrs;
            ReviewStatus = reviewStatus;
            DistributionApproved = distributionApproved;
            Limitations = limitations;
        }
        public string SourceId { get; }
        public string DatasetId { get; }
        public string SourceUrl { get; }
        public string SourceRevision { get; }
        public string LicenseCode { get; }
        public string LicenseUrl { get; }
        public string RawHash { get; }
        public long RawLength { get; }
        public string OriginalCrs { get; }
        public string ReviewStatus { get; }
        public bool DistributionApproved { get; }
        public IReadOnlyList<string> Limitations { get; }
    }

    public sealed class StationSpatialBuilding
    {
        internal StationSpatialBuilding(string id, string sourceFeatureId,
            string legalDongCode, string buildingKind, string purposeName,
            double heightMeters, string heightKind, double? observedHeightMeters,
            int? observedAboveGroundFloors, string heightPolicyRevision,
            string reviewStatus, bool boundaryClipped,
            IReadOnlyList<StationSpatialPolygon> parts)
        {
            Id = id;
            SourceFeatureId = sourceFeatureId;
            LegalDongCode = legalDongCode;
            BuildingKind = buildingKind;
            PurposeName = purposeName;
            HeightMeters = heightMeters;
            HeightKind = heightKind;
            ObservedHeightMeters = observedHeightMeters;
            ObservedAboveGroundFloors = observedAboveGroundFloors;
            HeightPolicyRevision = heightPolicyRevision;
            ReviewStatus = reviewStatus;
            BoundaryClipped = boundaryClipped;
            Parts = parts;
        }
        public string Id { get; }
        public string SourceFeatureId { get; }
        public string LegalDongCode { get; }
        public string BuildingKind { get; }
        public string PurposeName { get; }
        public double HeightMeters { get; }
        public string HeightKind { get; }
        public double? ObservedHeightMeters { get; }
        public int? ObservedAboveGroundFloors { get; }
        public string HeightPolicyRevision { get; }
        public string ReviewStatus { get; }
        public bool BoundaryClipped { get; }
        public IReadOnlyList<StationSpatialPolygon> Parts { get; }
    }

    public sealed class StationSpatialRoad
    {
        internal StationSpatialRoad(string id, long sourceRecordIndex,
            string fromNodeId, string toNodeId, string roadName, int laneCount,
            string updatedDate, string reviewStatus, bool boundaryClipped,
            IReadOnlyList<IReadOnlyList<StationSpatialPoint>> parts)
        {
            Id = id;
            SourceRecordIndex = sourceRecordIndex;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            RoadName = roadName;
            LaneCount = laneCount;
            UpdatedDate = updatedDate;
            ReviewStatus = reviewStatus;
            BoundaryClipped = boundaryClipped;
            Parts = parts;
        }
        public string Id { get; }
        public long SourceRecordIndex { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public string RoadName { get; }
        public int LaneCount { get; }
        public string UpdatedDate { get; }
        public string ReviewStatus { get; }
        public bool BoundaryClipped { get; }
        public IReadOnlyList<IReadOnlyList<StationSpatialPoint>> Parts { get; }
    }

    public sealed class StationSpatialSurface
    {
        internal StationSpatialSurface(string id, string kind, string evidenceKind,
            string sourceId, bool boundaryClipped,
            IReadOnlyList<StationSpatialPolygon> parts)
        {
            Id = id;
            Kind = kind;
            EvidenceKind = evidenceKind;
            SourceId = sourceId;
            BoundaryClipped = boundaryClipped;
            Parts = parts;
        }
        public string Id { get; }
        public string Kind { get; }
        public string EvidenceKind { get; }
        public string SourceId { get; }
        public bool BoundaryClipped { get; }
        public IReadOnlyList<StationSpatialPolygon> Parts { get; }
    }

    public sealed class StationSpatialAdministrativeArea
    {
        internal StationSpatialAdministrativeArea(string id, string name,
            double intersectionRatio, bool boundaryClipped,
            IReadOnlyList<StationSpatialPolygon> parts)
        {
            Id = id;
            Name = name;
            IntersectionRatio = intersectionRatio;
            BoundaryClipped = boundaryClipped;
            Parts = parts;
        }
        public string Id { get; }
        public string Name { get; }
        public double IntersectionRatio { get; }
        public bool BoundaryClipped { get; }
        public IReadOnlyList<StationSpatialPolygon> Parts { get; }
    }

    public sealed class StationSpatialCoverageCell
    {
        internal StationSpatialCoverageCell(string id, int xIndex, int zIndex,
            StationSpatialBounds bounds, string classification,
            double buildingCoverageRatio, double openCoverageRatio,
            double surfaceCoverageRatio, double unknownCoverageRatio,
            IReadOnlyList<string> evidenceMix, bool boundaryClipped)
        {
            Id = id;
            XIndex = xIndex;
            ZIndex = zIndex;
            Bounds = bounds;
            Classification = classification;
            BuildingCoverageRatio = buildingCoverageRatio;
            OpenCoverageRatio = openCoverageRatio;
            SurfaceCoverageRatio = surfaceCoverageRatio;
            UnknownCoverageRatio = unknownCoverageRatio;
            EvidenceMix = evidenceMix;
            BoundaryClipped = boundaryClipped;
        }
        public string Id { get; }
        public int XIndex { get; }
        public int ZIndex { get; }
        public StationSpatialBounds Bounds { get; }
        public string Classification { get; }
        public double BuildingCoverageRatio { get; }
        public double OpenCoverageRatio { get; }
        public double SurfaceCoverageRatio { get; }
        public double UnknownCoverageRatio { get; }
        public IReadOnlyList<string> EvidenceMix { get; }
        public bool BoundaryClipped { get; }
    }

    public sealed class StationSpatialMissingCoverage
    {
        internal StationSpatialMissingCoverage(string code, string layer, string detail,
            string sourceFeatureId, IReadOnlyList<string> affectedCellIds)
        {
            Code = code;
            Layer = layer;
            Detail = detail;
            SourceFeatureId = sourceFeatureId;
            AffectedCellIds = affectedCellIds;
        }
        public string Code { get; }
        public string Layer { get; }
        public string Detail { get; }
        public string SourceFeatureId { get; }
        public IReadOnlyList<string> AffectedCellIds { get; }
    }

    /// <summary>
    /// 단일 역 snapshot의 검증된 읽기 전용 사본. 실패 결과에는 도형을 부분 적용하지 않는다.
    /// </summary>
    public sealed class StationDioramaSpatialComposition
    {
        internal StationDioramaSpatialComposition(StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile, bool snapshotApplied,
            string revision, string contentHash, string dataStatus, string coverageStatus,
            string diagnosticCode, IReadOnlyList<StationSpatialSourceReceipt> sourceReceipts,
            IReadOnlyList<StationSpatialBuilding> buildings,
            IReadOnlyList<StationSpatialRoad> roads,
            IReadOnlyList<StationSpatialSurface> surfaces,
            IReadOnlyList<StationSpatialAdministrativeArea> administrativeAreas,
            IReadOnlyList<StationSpatialCoverageCell> coverageCells,
            IReadOnlyList<StationSpatialMissingCoverage> missingCoverage)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            PresentationProfile = presentationProfile
                                  ?? throw new ArgumentNullException(nameof(presentationProfile));
            SnapshotApplied = snapshotApplied;
            Revision = revision ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            DataStatus = dataStatus ?? string.Empty;
            CoverageStatus = coverageStatus ?? StationDioramaProfileCatalog.MissingCoverage;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            SourceReceipts = sourceReceipts ?? Array.Empty<StationSpatialSourceReceipt>();
            Buildings = buildings ?? Array.Empty<StationSpatialBuilding>();
            Roads = roads ?? Array.Empty<StationSpatialRoad>();
            Surfaces = surfaces ?? Array.Empty<StationSpatialSurface>();
            AdministrativeAreas = administrativeAreas
                                  ?? Array.Empty<StationSpatialAdministrativeArea>();
            CoverageCells = coverageCells ?? Array.Empty<StationSpatialCoverageCell>();
            MissingCoverageItems = missingCoverage ?? Array.Empty<StationSpatialMissingCoverage>();
        }

        public StationDioramaDatasetDescriptor Descriptor { get; }
        public StationDioramaPresentationProfile PresentationProfile { get; }
        public bool SnapshotApplied { get; }
        public string Revision { get; }
        public string ContentHash { get; }
        public string DataStatus { get; }
        public string CoverageStatus { get; }
        public string DiagnosticCode { get; }
        public IReadOnlyList<StationSpatialSourceReceipt> SourceReceipts { get; }
        public IReadOnlyList<StationSpatialBuilding> Buildings { get; }
        public IReadOnlyList<StationSpatialRoad> Roads { get; }
        public IReadOnlyList<StationSpatialSurface> Surfaces { get; }
        public IReadOnlyList<StationSpatialAdministrativeArea> AdministrativeAreas { get; }
        public IReadOnlyList<StationSpatialCoverageCell> CoverageCells { get; }
        public IReadOnlyList<StationSpatialMissingCoverage> MissingCoverageItems { get; }

        internal static StationDioramaSpatialComposition Missing(
            StationDioramaDatasetDescriptor descriptor,
            StationDioramaPresentationProfile presentationProfile,
            string diagnosticCode)
            => new(descriptor, presentationProfile, false, string.Empty, string.Empty,
                "NoData", StationDioramaProfileCatalog.MissingCoverage, diagnosticCode,
                Array.Empty<StationSpatialSourceReceipt>(),
                Array.Empty<StationSpatialBuilding>(),
                Array.Empty<StationSpatialRoad>(),
                Array.Empty<StationSpatialSurface>(),
                Array.Empty<StationSpatialAdministrativeArea>(),
                Array.Empty<StationSpatialCoverageCell>(),
                Array.Empty<StationSpatialMissingCoverage>());
    }
}
