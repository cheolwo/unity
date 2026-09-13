using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 역 중심 1km 공간 사본의 lowerCamelCase JSON 계약. Unity Scene 직렬화 계약이 아니다.
    /// </summary>
    [DataContract]
    public sealed class StationSpatialSnapshotData
    {
        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)] public string schemaVersion;
        [DataMember(Name = "revision", Order = 1, IsRequired = true)] public string revision;
        [DataMember(Name = "contentHash", Order = 2, IsRequired = true)] public string contentHash;
        [DataMember(Name = "status", Order = 3, IsRequired = true)] public string status;
        [DataMember(Name = "transitStationStableId", Order = 4, IsRequired = true)] public string transitStationStableId;
        [DataMember(Name = "stationName", Order = 5, IsRequired = true)] public string stationName;
        [DataMember(Name = "service", Order = 6, IsRequired = true)] public StationSpatialServiceData service;
        [DataMember(Name = "presentationOnly", Order = 7, IsRequired = true)] public bool presentationOnly;
        [DataMember(Name = "distributionApproved", Order = 8, IsRequired = true)] public bool distributionApproved;
        [DataMember(Name = "gameplayReady", Order = 9, IsRequired = true)] public bool gameplayReady;
        [DataMember(Name = "traversalReady", Order = 10, IsRequired = true)] public bool traversalReady;
        [DataMember(Name = "coordinateMethod", Order = 11, IsRequired = true)] public string coordinateMethod;
        [DataMember(Name = "coordinateFrame", Order = 12, IsRequired = true)] public StationSpatialCoordinateFrameData coordinateFrame;
        [DataMember(Name = "window", Order = 13, IsRequired = true)] public StationSpatialWindowData window;
        [DataMember(Name = "sourceReceipts", Order = 14, IsRequired = true)] public StationSpatialSourceReceiptData[] sourceReceipts;
        [DataMember(Name = "buildings", Order = 15, IsRequired = true)] public StationSpatialBuildingData[] buildings;
        [DataMember(Name = "roads", Order = 16, IsRequired = true)] public StationSpatialRoadData[] roads;
        [DataMember(Name = "surfaces", Order = 17, IsRequired = true)] public StationSpatialSurfaceData[] surfaces;
        [DataMember(Name = "administrativeAreas", Order = 18, IsRequired = true)] public StationSpatialAdministrativeAreaData[] administrativeAreas;
        [DataMember(Name = "tiles", Order = 19, IsRequired = true)] public StationSpatialTileData[] tiles;
        [DataMember(Name = "coverage", Order = 20, IsRequired = true)] public StationSpatialCoverageData coverage;
        [DataMember(Name = "missingCoverage", Order = 21, IsRequired = true)] public StationSpatialMissingCoverageData[] missingCoverage;
    }

    [DataContract]
    public sealed class StationSpatialServiceData
    {
        [DataMember(Name = "lineCode", Order = 0, IsRequired = true)] public string lineCode;
        [DataMember(Name = "stationCode", Order = 1, IsRequired = true)] public string stationCode;
    }

    [DataContract]
    public sealed class StationSpatialCoordinateFrameData
    {
        [DataMember(Name = "originLatitude", Order = 0, IsRequired = true)] public double originLatitude;
        [DataMember(Name = "originLongitude", Order = 1, IsRequired = true)] public double originLongitude;
        [DataMember(Name = "offsetX", Order = 2, IsRequired = true)] public double offsetX;
        [DataMember(Name = "offsetZ", Order = 3, IsRequired = true)] public double offsetZ;
        [DataMember(Name = "metersPerUnit", Order = 4, IsRequired = true)] public double metersPerUnit;
        [DataMember(Name = "halfExtentMeters", Order = 5, IsRequired = true)] public double halfExtentMeters;
    }

    [DataContract]
    public sealed class StationSpatialWindowData
    {
        [DataMember(Name = "widthMeters", Order = 0, IsRequired = true)] public double widthMeters;
        [DataMember(Name = "depthMeters", Order = 1, IsRequired = true)] public double depthMeters;
        [DataMember(Name = "bounds", Order = 2, IsRequired = true)] public StationSpatialBoundsData bounds;
        [DataMember(Name = "wgs84Bounds", Order = 3, IsRequired = true)] public StationSpatialWgs84BoundsData wgs84Bounds;
    }

    [DataContract]
    public sealed class StationSpatialBoundsData
    {
        [DataMember(Name = "minX", Order = 0, IsRequired = true)] public double minX;
        [DataMember(Name = "minZ", Order = 1, IsRequired = true)] public double minZ;
        [DataMember(Name = "maxX", Order = 2, IsRequired = true)] public double maxX;
        [DataMember(Name = "maxZ", Order = 3, IsRequired = true)] public double maxZ;
    }

    [DataContract]
    public sealed class StationSpatialWgs84BoundsData
    {
        [DataMember(Name = "minLongitude", Order = 0, IsRequired = true)] public double minLongitude;
        [DataMember(Name = "minLatitude", Order = 1, IsRequired = true)] public double minLatitude;
        [DataMember(Name = "maxLongitude", Order = 2, IsRequired = true)] public double maxLongitude;
        [DataMember(Name = "maxLatitude", Order = 3, IsRequired = true)] public double maxLatitude;
        [DataMember(Name = "corners", Order = 4, IsRequired = true)] public StationSpatialWgs84CornerData[] corners;
    }

    [DataContract]
    public sealed class StationSpatialWgs84CornerData
    {
        [DataMember(Name = "name", Order = 0, IsRequired = true)] public string name;
        [DataMember(Name = "longitude", Order = 1, IsRequired = true)] public double longitude;
        [DataMember(Name = "latitude", Order = 2, IsRequired = true)] public double latitude;
    }

    [DataContract]
    public sealed class StationSpatialSourceReceiptData
    {
        [DataMember(Name = "sourceId", Order = 0, IsRequired = true)] public string sourceId;
        [DataMember(Name = "datasetId", Order = 1, IsRequired = true)] public string datasetId;
        [DataMember(Name = "sourceUrl", Order = 2, IsRequired = true)] public string sourceUrl;
        [DataMember(Name = "sourceRevision", Order = 3, IsRequired = true)] public string sourceRevision;
        [DataMember(Name = "rawHash", Order = 4, IsRequired = true)] public string rawHash;
        // 역 기준점처럼 레코드 단위 official receipt는 별도 raw 파일 길이가 없을 수 있다.
        [DataMember(Name = "rawLength", Order = 5)] public long rawLength;
        [DataMember(Name = "originalCrs", Order = 6, IsRequired = true)] public string originalCrs;
        [DataMember(Name = "licenseCode", Order = 7, IsRequired = true)] public string licenseCode;
        [DataMember(Name = "licenseUrl", Order = 8)] public string licenseUrl;
        [DataMember(Name = "reviewStatus", Order = 9, IsRequired = true)] public string reviewStatus;
        [DataMember(Name = "distributionApproved", Order = 10, IsRequired = true)] public bool distributionApproved;
        [DataMember(Name = "limitations", Order = 11, IsRequired = true)] public string[] limitations;
    }

    [DataContract]
    public sealed class StationSpatialBuildingData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "sourceFeatureId", Order = 1, IsRequired = true)] public string sourceFeatureId;
        [DataMember(Name = "legalDongCode", Order = 2, IsRequired = true)] public string legalDongCode;
        [DataMember(Name = "buildingKind", Order = 3, IsRequired = true)] public string buildingKind;
        [DataMember(Name = "purposeName", Order = 4, IsRequired = true)] public string purposeName;
        [DataMember(Name = "heightMeters", Order = 5, IsRequired = true)] public double heightMeters;
        [DataMember(Name = "heightKind", Order = 6, IsRequired = true)] public string heightKind;
        [DataMember(Name = "observedHeightMeters", Order = 7)] public double? observedHeightMeters;
        [DataMember(Name = "observedAboveGroundFloors", Order = 8)] public int? observedAboveGroundFloors;
        [DataMember(Name = "heightPolicyRevision", Order = 9, IsRequired = true)] public string heightPolicyRevision;
        [DataMember(Name = "reviewStatus", Order = 10, IsRequired = true)] public string reviewStatus;
        [DataMember(Name = "boundaryClipped", Order = 11, IsRequired = true)] public bool boundaryClipped;
        [DataMember(Name = "parts", Order = 12, IsRequired = true)] public StationSpatialPolygonPartData[] parts;
    }

    [DataContract]
    public sealed class StationSpatialRoadData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "sourceRecordIndex", Order = 1, IsRequired = true)] public long sourceRecordIndex;
        [DataMember(Name = "fromNodeId", Order = 2, IsRequired = true)] public string fromNodeId;
        [DataMember(Name = "toNodeId", Order = 3, IsRequired = true)] public string toNodeId;
        [DataMember(Name = "roadName", Order = 4, IsRequired = true)] public string roadName;
        [DataMember(Name = "laneCount", Order = 5, IsRequired = true)] public int laneCount;
        [DataMember(Name = "updatedDate", Order = 6, IsRequired = true)] public string updatedDate;
        [DataMember(Name = "reviewStatus", Order = 7, IsRequired = true)] public string reviewStatus;
        [DataMember(Name = "presentationOnly", Order = 8, IsRequired = true)] public bool presentationOnly;
        [DataMember(Name = "traversalReady", Order = 9, IsRequired = true)] public bool traversalReady;
        [DataMember(Name = "boundaryClipped", Order = 10, IsRequired = true)] public bool boundaryClipped;
        [DataMember(Name = "parts", Order = 11, IsRequired = true)] public StationSpatialLinePartData[] parts;
    }

    [DataContract]
    public sealed class StationSpatialSurfaceData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "kind", Order = 1, IsRequired = true)] public string kind;
        [DataMember(Name = "evidenceKind", Order = 2, IsRequired = true)] public string evidenceKind;
        [DataMember(Name = "presentationOnly", Order = 3, IsRequired = true)] public bool presentationOnly;
        [DataMember(Name = "sourceId", Order = 4, IsRequired = true)] public string sourceId;
        [DataMember(Name = "boundaryClipped", Order = 5, IsRequired = true)] public bool boundaryClipped;
        [DataMember(Name = "parts", Order = 6, IsRequired = true)] public StationSpatialPolygonPartData[] parts;
    }

    [DataContract]
    public sealed class StationSpatialAdministrativeAreaData
    {
        [DataMember(Name = "administrativeAreaStableId", Order = 0, IsRequired = true)] public string administrativeAreaStableId;
        [DataMember(Name = "displayName", Order = 1, IsRequired = true)] public string displayName;
        [DataMember(Name = "sourceVintage", Order = 2, IsRequired = true)] public string sourceVintage;
        [DataMember(Name = "containsStation", Order = 3, IsRequired = true)] public bool containsStation;
        [DataMember(Name = "coverageSquareMeters", Order = 4, IsRequired = true)] public double coverageSquareMeters;
        [DataMember(Name = "coverageRatio", Order = 5, IsRequired = true)] public double coverageRatio;
        [DataMember(Name = "boundaryClipped", Order = 6, IsRequired = true)] public bool boundaryClipped;
        [DataMember(Name = "parts", Order = 7, IsRequired = true)] public StationSpatialPolygonPartData[] parts;
    }

    [DataContract]
    public sealed class StationSpatialTileData
    {
        [DataMember(Name = "tileStableId", Order = 0, IsRequired = true)] public string tileStableId;
        [DataMember(Name = "tileIndexX", Order = 1, IsRequired = true)] public int tileIndexX;
        [DataMember(Name = "tileIndexZ", Order = 2, IsRequired = true)] public int tileIndexZ;
        [DataMember(Name = "bounds", Order = 3, IsRequired = true)] public StationSpatialBoundsData bounds;
        [DataMember(Name = "tileHash", Order = 4, IsRequired = true)] public string tileHash;
        [DataMember(Name = "buildingCount", Order = 5, IsRequired = true)] public int buildingCount;
        [DataMember(Name = "roadCount", Order = 6, IsRequired = true)] public int roadCount;
        [DataMember(Name = "surfaceCount", Order = 7, IsRequired = true)] public int surfaceCount;
        [DataMember(Name = "coverageCellIds", Order = 8, IsRequired = true)] public string[] coverageCellIds;
    }

    [DataContract]
    public sealed class StationSpatialCoverageData
    {
        [DataMember(Name = "cellSizeMeters", Order = 0, IsRequired = true)] public double cellSizeMeters;
        [DataMember(Name = "columns", Order = 1, IsRequired = true)] public int columns;
        [DataMember(Name = "rows", Order = 2, IsRequired = true)] public int rows;
        [DataMember(Name = "buildingCoverageThreshold", Order = 3, IsRequired = true)] public double buildingCoverageThreshold;
        [DataMember(Name = "qualifiedOpenCoverageThreshold", Order = 4, IsRequired = true)] public double qualifiedOpenCoverageThreshold;
        [DataMember(Name = "cells", Order = 5, IsRequired = true)] public StationSpatialCoverageCellData[] cells;
        [DataMember(Name = "summary", Order = 6, IsRequired = true)] public StationSpatialCoverageSummaryData summary;
    }

    [DataContract]
    public sealed class StationSpatialCoverageCellData
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)] public string id;
        [DataMember(Name = "xIndex", Order = 1, IsRequired = true)] public int xIndex;
        [DataMember(Name = "zIndex", Order = 2, IsRequired = true)] public int zIndex;
        [DataMember(Name = "bounds", Order = 3, IsRequired = true)] public StationSpatialBoundsData bounds;
        [DataMember(Name = "buildingCoverageRatio", Order = 4, IsRequired = true)] public double buildingCoverageRatio;
        [DataMember(Name = "openCoverageRatio", Order = 5, IsRequired = true)] public double openCoverageRatio;
        [DataMember(Name = "surfaceCoverageRatio", Order = 6, IsRequired = true)] public double surfaceCoverageRatio;
        [DataMember(Name = "unknownCoverageRatio", Order = 7, IsRequired = true)] public double unknownCoverageRatio;
        [DataMember(Name = "classification", Order = 8, IsRequired = true)] public string classification;
        [DataMember(Name = "evidenceMix", Order = 9, IsRequired = true)] public string[] evidenceMix;
        [DataMember(Name = "boundaryClipped", Order = 10, IsRequired = true)] public bool boundaryClipped;
        [DataMember(Name = "incompleteSourceFeatureIds", Order = 11, IsRequired = true)] public string[] incompleteSourceFeatureIds;
    }

    [DataContract]
    public sealed class StationSpatialCoverageSummaryData
    {
        [DataMember(Name = "classificationCounts", Order = 0, IsRequired = true)] public Dictionary<string, int> classificationCounts;
        [DataMember(Name = "buildingCoverageSquareMeters", Order = 1, IsRequired = true)] public double buildingCoverageSquareMeters;
        [DataMember(Name = "surfaceCoverageSquareMeters", Order = 2, IsRequired = true)] public double surfaceCoverageSquareMeters;
        [DataMember(Name = "qualifiedOpenCoverageSquareMeters", Order = 3, IsRequired = true)] public double qualifiedOpenCoverageSquareMeters;
        [DataMember(Name = "incompleteRelationCount", Order = 4, IsRequired = true)] public int incompleteRelationCount;
    }

    [DataContract]
    public sealed class StationSpatialMissingCoverageData
    {
        [DataMember(Name = "code", Order = 0, IsRequired = true)] public string code;
        [DataMember(Name = "layer", Order = 1, IsRequired = true)] public string layer;
        [DataMember(Name = "detail", Order = 2, IsRequired = true)] public string detail;
        [DataMember(Name = "affectedCellIds", Order = 3, IsRequired = true)] public string[] affectedCellIds;
        [DataMember(Name = "sourceFeatureId", Order = 4, EmitDefaultValue = false)] public string sourceFeatureId;
        [DataMember(Name = "missingWayIds", Order = 5, EmitDefaultValue = false)] public string[] missingWayIds;
        [DataMember(Name = "missingNodeIds", Order = 6, EmitDefaultValue = false)] public string[] missingNodeIds;
    }

    [DataContract]
    public sealed class StationSpatialPolygonPartData
    {
        [DataMember(Name = "outer", Order = 0, IsRequired = true)] public StationSpatialRingData outer;
        [DataMember(Name = "holes", Order = 1, IsRequired = true)] public StationSpatialRingData[] holes;
    }

    [DataContract]
    public sealed class StationSpatialRingData
    {
        [DataMember(Name = "points", Order = 0, IsRequired = true)] public StationSpatialPointData[] points;
    }

    [DataContract]
    public sealed class StationSpatialLinePartData
    {
        [DataMember(Name = "points", Order = 0, IsRequired = true)] public StationSpatialPointData[] points;
    }

    [DataContract]
    public sealed class StationSpatialPointData
    {
        [DataMember(Name = "x", Order = 0, IsRequired = true)] public double x;
        [DataMember(Name = "z", Order = 1, IsRequired = true)] public double z;
    }
}
