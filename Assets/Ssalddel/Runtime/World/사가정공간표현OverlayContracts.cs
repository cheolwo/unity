using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 사가정 r3 위에 선택적으로 합성하는 표현 전용 공간 자료 계약이다.
    /// Unity Scene이나 Player 저장 자료가 되지 않도록 Unity 직렬화 대상 형식으로 선언하지 않는다.
    /// </summary>
    [DataContract]
    public sealed class 사가정공간표현OverlayData
    {
        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)]
        public string schemaVersion;

        [DataMember(Name = "revision", Order = 1, IsRequired = true)]
        public string revision;

        [DataMember(Name = "status", Order = 2, IsRequired = true)]
        public string status;

        [DataMember(Name = "presentationOnly", Order = 3, IsRequired = true)]
        public bool presentationOnly;

        [DataMember(Name = "distributionApproved", Order = 4, IsRequired = true)]
        public bool distributionApproved;

        [DataMember(Name = "baseMapRevision", Order = 5, IsRequired = true)]
        public string baseMapRevision;

        [DataMember(Name = "baseMapHash", Order = 6, IsRequired = true)]
        public string baseMapHash;

        [DataMember(Name = "coordinateMethod", Order = 7, IsRequired = true)]
        public string coordinateMethod;

        [DataMember(Name = "originLatitude", Order = 8, IsRequired = true)]
        public double originLatitude;

        [DataMember(Name = "originLongitude", Order = 9, IsRequired = true)]
        public double originLongitude;

        [DataMember(Name = "offsetX", Order = 10, IsRequired = true)]
        public double offsetX;

        [DataMember(Name = "offsetZ", Order = 11, IsRequired = true)]
        public double offsetZ;

        [DataMember(Name = "halfExtent", Order = 12, IsRequired = true)]
        public double halfExtent;

        [DataMember(Name = "sourceReceipts", Order = 13, IsRequired = true)]
        public 사가정공간표현SourceReceiptData[] sourceReceipts;

        [DataMember(Name = "buildings", Order = 14, IsRequired = true)]
        public 사가정공간표현BuildingData[] buildings;

        [DataMember(Name = "surfaces", Order = 15, IsRequired = true)]
        public 사가정공간표현SurfaceData[] surfaces;

        [DataMember(Name = "contentHash", Order = 16, EmitDefaultValue = false)]
        public string contentHash;
    }

    [DataContract]
    public sealed class 사가정공간표현SourceReceiptData
    {
        [DataMember(Name = "provider", Order = 0, IsRequired = true)] public string provider;
        [DataMember(Name = "datasetId", Order = 1, IsRequired = true)] public string datasetId;
        [DataMember(Name = "sourceUrl", Order = 2, IsRequired = true)] public string sourceUrl;
        [DataMember(Name = "licenseCode", Order = 3, IsRequired = true)] public string licenseCode;
        [DataMember(Name = "licenseUrl", Order = 4, IsRequired = true)] public string licenseUrl;
        [DataMember(Name = "datasetDate", Order = 5, IsRequired = true)] public string datasetDate;
        [DataMember(Name = "fetchedAtUtc", Order = 6, IsRequired = true)] public string fetchedAtUtc;
        [DataMember(Name = "rawHash", Order = 7, IsRequired = true)] public string rawHash;
        [DataMember(Name = "rawLength", Order = 8, IsRequired = true)] public long rawLength;
        [DataMember(Name = "originalCrs", Order = 9, IsRequired = true)] public string originalCrs;
        [DataMember(Name = "reviewStatus", Order = 10, IsRequired = true)] public string reviewStatus;
    }

    [DataContract]
    public sealed class 사가정공간표현BuildingData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "sourceFeatureId", Order = 1, IsRequired = true)] public string sourceFeatureId;
        [DataMember(Name = "buildingKind", Order = 2, IsRequired = true)] public string buildingKind;
        [DataMember(Name = "purposeName", Order = 3, IsRequired = true)] public string purposeName;
        [DataMember(Name = "heightMeters", Order = 4, IsRequired = true)] public double heightMeters;
        [DataMember(Name = "heightKind", Order = 5, IsRequired = true)] public string heightKind;
        [DataMember(Name = "observedHeightMeters", Order = 6)] public double? observedHeightMeters;
        [DataMember(Name = "observedAboveGroundFloors", Order = 7)] public int? observedAboveGroundFloors;
        [DataMember(Name = "heightPolicyRevision", Order = 8, IsRequired = true)] public string heightPolicyRevision;
        [DataMember(Name = "reviewStatus", Order = 9, IsRequired = true)] public string reviewStatus;
        [DataMember(Name = "legacyOsmIds", Order = 10, IsRequired = true)] public string[] legacyOsmIds;
        [DataMember(Name = "legacyMatch", Order = 11)] public 사가정공간표현LegacyMatchData legacyMatch;
        [DataMember(Name = "parts", Order = 12, IsRequired = true)] public 사가정공간표현PolygonPartData[] parts;
    }

    [DataContract]
    public sealed class 사가정공간표현LegacyMatchData
    {
        [DataMember(Name = "method", Order = 0, IsRequired = true)] public string method;
        [DataMember(Name = "centroidDistanceMeters", Order = 1)] public double? centroidDistanceMeters;
        [DataMember(Name = "intersectionOverUnion", Order = 2)] public double? intersectionOverUnion;
        [DataMember(Name = "smallerFootprintCoverage", Order = 3)] public double? smallerFootprintCoverage;
        [DataMember(Name = "ambiguous", Order = 4, IsRequired = true)] public bool ambiguous;
    }

    [DataContract]
    public sealed class 사가정공간표현SurfaceData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "kind", Order = 1, IsRequired = true)] public string kind;
        [DataMember(Name = "evidenceKind", Order = 2, IsRequired = true)] public string evidenceKind;
        [DataMember(Name = "presentationOnly", Order = 3, IsRequired = true)] public bool presentationOnly;
        [DataMember(Name = "sourceId", Order = 4, IsRequired = true)] public string sourceId;
        [DataMember(Name = "parts", Order = 5, IsRequired = true)] public 사가정공간표현PolygonPartData[] parts;
    }

    [DataContract]
    public sealed class 사가정공간표현PolygonPartData
    {
        [DataMember(Name = "outer", Order = 0, IsRequired = true)] public 사가정공간표현RingData outer;
        [DataMember(Name = "holes", Order = 1, IsRequired = true)] public 사가정공간표현RingData[] holes;
    }

    [DataContract]
    public sealed class 사가정공간표현RingData
    {
        [DataMember(Name = "points", Order = 0, IsRequired = true)] public 사가정공간표현PointData[] points;
    }

    [DataContract]
    public sealed class 사가정공간표현PointData
    {
        [DataMember(Name = "x", Order = 0, IsRequired = true)] public double x;
        [DataMember(Name = "z", Order = 1, IsRequired = true)] public double z;
    }

    public enum 사가정공간표현OverlayBoundary
    {
        LocalPrivateReview,
        ApprovedResource
    }

    /// <summary>원천 자료의 판본과 권리 경계를 잃지 않는 읽기 전용 영수증 사본.</summary>
    public sealed class 사가정공간표현SourceReceipt
    {
        internal 사가정공간표현SourceReceipt(사가정공간표현SourceReceiptData source)
        {
            Provider = source.provider;
            DatasetId = source.datasetId;
            SourceUrl = source.sourceUrl;
            LicenseCode = source.licenseCode;
            LicenseUrl = source.licenseUrl;
            DatasetDate = source.datasetDate;
            FetchedAtUtc = source.fetchedAtUtc;
            RawHash = source.rawHash;
            RawLength = source.rawLength;
            OriginalCrs = source.originalCrs;
            ReviewStatus = source.reviewStatus;
        }

        public string Provider { get; }
        public string DatasetId { get; }
        public string SourceUrl { get; }
        public string LicenseCode { get; }
        public string LicenseUrl { get; }
        public string DatasetDate { get; }
        public string FetchedAtUtc { get; }
        public string RawHash { get; }
        public long RawLength { get; }
        public string OriginalCrs { get; }
        public string ReviewStatus { get; }
    }

    public readonly struct 사가정공간표현Point
    {
        internal 사가정공간표현Point(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
    }

    public sealed class 사가정공간표현Polygon
    {
        internal 사가정공간표현Polygon(
            IReadOnlyList<사가정공간표현Point> outer,
            IReadOnlyList<IReadOnlyList<사가정공간표현Point>> holes)
        {
            Outer = outer;
            Holes = holes;
        }

        public IReadOnlyList<사가정공간표현Point> Outer { get; }
        public IReadOnlyList<IReadOnlyList<사가정공간표현Point>> Holes { get; }
    }

    /// <summary>건물 외곽, 용도, 높이 근거를 함께 보존하는 표현용 사본.</summary>
    public sealed class 사가정공간표현Building
    {
        internal 사가정공간표현Building(
            사가정공간표현BuildingData source,
            IReadOnlyList<string> legacyOsmIds,
            IReadOnlyList<사가정공간표현Polygon> parts)
        {
            Id = source.id;
            SourceFeatureId = source.sourceFeatureId;
            BuildingKind = source.buildingKind;
            PurposeName = source.purposeName;
            HeightMeters = source.heightMeters;
            HeightKind = source.heightKind;
            ObservedHeightMeters = source.observedHeightMeters;
            ObservedAboveGroundFloors = source.observedAboveGroundFloors;
            HeightPolicyRevision = source.heightPolicyRevision;
            ReviewStatus = source.reviewStatus;
            LegacyOsmIds = legacyOsmIds;
            LegacyMatchMethod = source.legacyMatch?.method ?? string.Empty;
            LegacyMatchAmbiguous = source.legacyMatch?.ambiguous ?? true;
            Parts = parts;
        }

        public string Id { get; }
        public string SourceFeatureId { get; }
        public string BuildingKind { get; }
        public string PurposeName { get; }
        public double HeightMeters { get; }
        public string HeightKind { get; }
        public double? ObservedHeightMeters { get; }
        public int? ObservedAboveGroundFloors { get; }
        public string HeightPolicyRevision { get; }
        public string ReviewStatus { get; }
        public IReadOnlyList<string> LegacyOsmIds { get; }
        public string LegacyMatchMethod { get; }
        public bool LegacyMatchAmbiguous { get; }
        public IReadOnlyList<사가정공간표현Polygon> Parts { get; }
    }

    /// <summary>지표면 분류와 그 근거를 함께 보존하는 표현 전용 사본.</summary>
    public sealed class 사가정공간표현Surface
    {
        internal 사가정공간표현Surface(
            사가정공간표현SurfaceData source,
            IReadOnlyList<사가정공간표현Polygon> parts)
        {
            Id = source.id;
            Kind = source.kind;
            EvidenceKind = source.evidenceKind;
            SourceId = source.sourceId;
            Parts = parts;
        }

        public string Id { get; }
        public string Kind { get; }
        public string EvidenceKind { get; }
        public string SourceId { get; }
        public IReadOnlyList<사가정공간표현Polygon> Parts { get; }
    }
}
