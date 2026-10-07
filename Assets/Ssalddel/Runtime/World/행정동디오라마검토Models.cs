using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 행정동디오라마비공개관찰Policy
    {
        public const string SchemaVersion =
            "administrative-dong-private-observation-overlays.v1";
    }

    public static class 행정동디오라마비공개보행망Policy
    {
        public const string SchemaVersion =
            "administrative-dong-private-walk-network-overlays.v1";
    }

    public static class 행정동디오라마비공개도로방향Policy
    {
        public const string SchemaVersion =
            "administrative-dong-private-road-direction-overlays.v1";
    }

    public static class 행정동디오라마비공개비오톱Policy
    {
        public const string SchemaVersion = "administrative-dong-private-biotope-overlays.v1";
        public const string CoordinateFrameMethod = "WGS84-ECEF-ENU-at-zero-altitude";
    }

    public static class 행정동디오라마비공개차선표시Policy
    {
        public const string SchemaVersion =
            "administrative-dong-private-lane-marking-overlays.v1";
        public const string CoordinateFrameMethod = "WGS84-ECEF-ENU-at-zero-altitude";
    }

    public static class 행정동디오라마비공개수평등고선추적Policy
    {
        public const string SchemaVersion =
            "administrative-dong-private-horizontal-contour-trace-overlays.v1";
        public const string CoordinateFrameMethod = "WGS84-ECEF-ENU-at-zero-altitude";
    }

    /// <summary>
    /// 서버가 만든 비공개 후보 관찰 묶음을 읽기 위한 전송 계약이다. 좌표 단위는 공통 ENU
    /// millimeter이며, 이 형식 자체는 공개 표시나 통행·게임 실행 권한을 부여하지 않는다.
    /// </summary>
    [DataContract]
    public sealed class 행정동디오라마비공개관찰OverlaySetData
    {
        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)]
        public string schemaVersion;

        [DataMember(Name = "revision", Order = 1, IsRequired = true)]
        public string revision;

        [DataMember(Name = "overlaySetStableId", Order = 2, IsRequired = true)]
        public string overlaySetStableId;

        [DataMember(Name = "overlaySetHashSha256", Order = 3, IsRequired = true)]
        public string overlaySetHashSha256;

        [DataMember(Name = "administrativeAreaStableId", Order = 4, IsRequired = true)]
        public string administrativeAreaStableId;

        [DataMember(Name = "sourceVintage", Order = 5, IsRequired = true)]
        public string sourceVintage;

        [DataMember(Name = "historicalBoundaryBootstrapOnly", Order = 6, IsRequired = true)]
        public bool historicalBoundaryBootstrapOnly;

        [DataMember(Name = "privateReviewVisualizationAllowed", Order = 7, IsRequired = true)]
        public bool privateReviewVisualizationAllowed;

        [DataMember(Name = "reviewVisualizationOnly", Order = 8, IsRequired = true)]
        public bool reviewVisualizationOnly;

        [DataMember(Name = "sourceCandidateUnityApplyAllowed", Order = 9, IsRequired = true)]
        public bool sourceCandidateUnityApplyAllowed;

        [DataMember(Name = "publicDisplayAllowed", Order = 10, IsRequired = true)]
        public bool publicDisplayAllowed;

        [DataMember(Name = "runtimeAuthorized", Order = 11, IsRequired = true)]
        public bool runtimeAuthorized;

        [DataMember(Name = "traversalReady", Order = 12, IsRequired = true)]
        public bool traversalReady;

        [DataMember(Name = "gameplayReady", Order = 13, IsRequired = true)]
        public bool gameplayReady;

        [DataMember(Name = "currentPointerUsed", Order = 14, IsRequired = true)]
        public bool currentPointerUsed;

        [DataMember(Name = "currentPointerUpdated", Order = 15, IsRequired = true)]
        public bool currentPointerUpdated;

        [DataMember(Name = "exactAddressIncluded", Order = 16, IsRequired = true)]
        public bool exactAddressIncluded;

        [DataMember(Name = "businessIdentityIncluded", Order = 17, IsRequired = true)]
        public bool businessIdentityIncluded;

        [DataMember(Name = "personalDataIncluded", Order = 18, IsRequired = true)]
        public bool personalDataIncluded;

        [DataMember(Name = "sourceReceipts", Order = 19, IsRequired = true)]
        public 행정동디오라마비공개관찰SourceReceiptData[] sourceReceipts;

        [DataMember(Name = "crosswalkPoints", Order = 20, IsRequired = true)]
        public 행정동디오라마횡단보도점후보Data[] crosswalkPoints;

        [DataMember(Name = "intersectionPoints", Order = 21, IsRequired = true)]
        public 행정동디오라마교차로점후보Data[] intersectionPoints;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개관찰SourceReceiptData
    {
        [DataMember(Name = "datasetId", Order = 0, IsRequired = true)]
        public string datasetId;

        [DataMember(Name = "officialPageUrl", Order = 1, IsRequired = true)]
        public string officialPageUrl;

        [DataMember(Name = "fileRevision", Order = 2, IsRequired = true)]
        public string fileRevision;

        [DataMember(Name = "fileSha256", Order = 3, IsRequired = true)]
        public string fileSha256;

        [DataMember(Name = "byteLength", Order = 4, EmitDefaultValue = false)]
        public long? byteLength;

        [DataMember(Name = "licenseCode", Order = 5, IsRequired = true)]
        public string licenseCode;

        [DataMember(Name = "coordinateReferenceStatus", Order = 6, IsRequired = true)]
        public string coordinateReferenceStatus;
    }

    [DataContract]
    public sealed class 행정동디오라마공통EnuMillimetersData
    {
        [DataMember(Name = "x", Order = 0, IsRequired = true)] public double x;
        [DataMember(Name = "z", Order = 1, IsRequired = true)] public double z;
    }

    [DataContract]
    public sealed class 행정동디오라마횡단보도점후보Data
    {
        [DataMember(Name = "candidateStableId", Order = 0, IsRequired = true)]
        public string candidateStableId;

        [DataMember(Name = "commonEnuMillimeters", Order = 1, IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData commonEnuMillimeters;

        [DataMember(Name = "pedestrianSignalPresent", Order = 2, IsRequired = true)]
        public bool pedestrianSignalPresent;

        [DataMember(Name = "sourceDistrictSpatialAssignmentConflict", Order = 3,
            IsRequired = true)]
        public bool sourceDistrictSpatialAssignmentConflict;

        [DataMember(Name = "qualityCode", Order = 4, IsRequired = true)]
        public string qualityCode;
    }

    [DataContract]
    public sealed class 행정동디오라마교차로점후보Data
    {
        [DataMember(Name = "candidateStableId", Order = 0, IsRequired = true)]
        public string candidateStableId;

        [DataMember(Name = "commonEnuMillimeters", Order = 1, IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData commonEnuMillimeters;

        [DataMember(Name = "linkedCrosswalkCount", Order = 2, IsRequired = true)]
        public int linkedCrosswalkCount;

        [DataMember(Name = "otherHistoricalAdministrativeAreaLinkCount", Order = 3,
            IsRequired = true)]
        public int otherHistoricalAdministrativeAreaLinkCount;

        [DataMember(Name = "sourceDistrictSpatialAssignmentConflict", Order = 4,
            IsRequired = true)]
        public bool sourceDistrictSpatialAssignmentConflict;

        [DataMember(Name = "qualityCode", Order = 5, IsRequired = true)]
        public string qualityCode;
    }

    [DataContract]
    public sealed class 행정동디오라마보행망ActorSummaryFlagsData
    {
        [DataMember(Name = "sourceReported", Order = 0, IsRequired = true)]
        public bool sourceReported;

        [DataMember(Name = "pedestrian", Order = 1, IsRequired = true)]
        public bool pedestrian;

        [DataMember(Name = "bicycle", Order = 2, IsRequired = true)]
        public bool bicycle;

        [DataMember(Name = "personalMobility", Order = 3, IsRequired = true)]
        public bool personalMobility;

        [DataMember(Name = "vehicle", Order = 4, IsRequired = true)]
        public bool vehicle;
    }

    [DataContract]
    public sealed class 행정동디오라마보행망NodePoint후보Data
    {
        [DataMember(Name = "candidateStableId", Order = 0, IsRequired = true)]
        public string candidateStableId;

        [DataMember(Name = "commonEnuMillimeters", Order = 1, IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData commonEnuMillimeters;

        [DataMember(Name = "kindCode", Order = 2, IsRequired = true)]
        public string kindCode;

        [DataMember(Name = "qualityCode", Order = 3, IsRequired = true)]
        public string qualityCode;

        [DataMember(Name = "actorSummaryFlags", Order = 4, IsRequired = true)]
        public 행정동디오라마보행망ActorSummaryFlagsData actorSummaryFlags;
    }

    [DataContract]
    public sealed class 행정동디오라마보행망LinkFragment후보Data
    {
        [DataMember(Name = "candidateStableId", Order = 0, IsRequired = true)]
        public string candidateStableId;

        [DataMember(Name = "commonEnuMillimeterPath", Order = 1, IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData[] commonEnuMillimeterPath;

        [DataMember(Name = "kindCode", Order = 3, IsRequired = true)]
        public string kindCode;

        [DataMember(Name = "qualityCode", Order = 4, IsRequired = true)]
        public string qualityCode;

        [DataMember(Name = "actorSummaryFlags", Order = 5, IsRequired = true)]
        public 행정동디오라마보행망ActorSummaryFlagsData actorSummaryFlags;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개보행망SourceReceiptData
    {
        [DataMember(Name = "datasetId", Order = 0, IsRequired = true)]
        public string datasetId;

        [DataMember(Name = "officialPageUrl", Order = 1, IsRequired = true)]
        public string officialPageUrl;

        [DataMember(Name = "sourceReferenceVintage", Order = 2, IsRequired = true)]
        public string sourceReferenceVintage;

        [DataMember(Name = "candidateSetHashSha256", Order = 3, IsRequired = true)]
        public string candidateSetHashSha256;

        [DataMember(Name = "acquisitionReceiptSha256", Order = 4, IsRequired = true)]
        public string acquisitionReceiptSha256;

        [DataMember(Name = "licenseCode", Order = 5, IsRequired = true)]
        public string licenseCode;

        [DataMember(Name = "sourceCoordinateReference", Order = 6, IsRequired = true)]
        public string sourceCoordinateReference;

        [DataMember(Name = "conversionCoordinateReference", Order = 7, IsRequired = true)]
        public string conversionCoordinateReference;

        [DataMember(Name = "coordinateReferenceStatus", Order = 8, IsRequired = true)]
        public string coordinateReferenceStatus;
    }

    /// <summary>
    /// 2020 보행자우선도로 후보를 비공개 검토용으로만 전달한다. 이 계약의 선분은
    /// 보행 가능 경로·내비게이션·NPC 이동 권위를 만들지 않는다.
    /// </summary>
    [DataContract]
    public sealed class 행정동디오라마비공개보행망OverlaySetData
    {
        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)]
        public string schemaVersion;

        [DataMember(Name = "revision", Order = 1, IsRequired = true)]
        public string revision;

        [DataMember(Name = "overlaySetStableId", Order = 2, IsRequired = true)]
        public string overlaySetStableId;

        [DataMember(Name = "administrativeAreaStableId", Order = 3, IsRequired = true)]
        public string administrativeAreaStableId;

        [DataMember(Name = "sourceVintage", Order = 4, IsRequired = true)]
        public string sourceVintage;

        [DataMember(Name = "historicalBoundaryBootstrapOnly", Order = 5, IsRequired = true)]
        public bool historicalBoundaryBootstrapOnly;

        [DataMember(Name = "privateReviewOnly", Order = 6, IsRequired = true)]
        public bool privateReviewOnly;

        [DataMember(Name = "privateReviewVisualizationAllowed", Order = 7, IsRequired = true)]
        public bool privateReviewVisualizationAllowed;

        [DataMember(Name = "reviewVisualizationOnly", Order = 8, IsRequired = true)]
        public bool reviewVisualizationOnly;

        [DataMember(Name = "observationCandidateOnly", Order = 9, IsRequired = true)]
        public bool observationCandidateOnly;

        [DataMember(Name = "sourceCandidateUnityApplyAllowed", Order = 10, IsRequired = true)]
        public bool sourceCandidateUnityApplyAllowed;

        [DataMember(Name = "publicDisplayAllowed", Order = 11, IsRequired = true)]
        public bool publicDisplayAllowed;

        [DataMember(Name = "runtimeAuthorized", Order = 12, IsRequired = true)]
        public bool runtimeAuthorized;

        [DataMember(Name = "traversalReady", Order = 13, IsRequired = true)]
        public bool traversalReady;

        [DataMember(Name = "gameplayReady", Order = 14, IsRequired = true)]
        public bool gameplayReady;

        [DataMember(Name = "currentPointerUsed", Order = 15, IsRequired = true)]
        public bool currentPointerUsed;

        [DataMember(Name = "currentPointerUpdated", Order = 16, IsRequired = true)]
        public bool currentPointerUpdated;

        [DataMember(Name = "sourceIdentifiersIncluded", Order = 17, IsRequired = true)]
        public bool sourceIdentifiersIncluded;

        [DataMember(Name = "sourceBoroughIdentityIncluded", Order = 18, IsRequired = true)]
        public bool sourceBoroughIdentityIncluded;

        [DataMember(Name = "legalAreaIdentityIncluded", Order = 19, IsRequired = true)]
        public bool legalAreaIdentityIncluded;

        [DataMember(Name = "sourceTimestampsIncluded", Order = 20, IsRequired = true)]
        public bool sourceTimestampsIncluded;

        [DataMember(Name = "sourceEpsg5186CoordinatesIncluded", Order = 21,
            IsRequired = true)]
        public bool sourceEpsg5186CoordinatesIncluded;

        [DataMember(Name = "exactAddressIncluded", Order = 22, IsRequired = true)]
        public bool exactAddressIncluded;

        [DataMember(Name = "personalDataIncluded", Order = 23, IsRequired = true)]
        public bool personalDataIncluded;

        [DataMember(Name = "sourceReceipts", Order = 24, IsRequired = true)]
        public 행정동디오라마비공개보행망SourceReceiptData[] sourceReceipts;

        [DataMember(Name = "nodePoints", Order = 25, IsRequired = true)]
        public 행정동디오라마보행망NodePoint후보Data[] nodePoints;

        [DataMember(Name = "linkFragments", Order = 26, IsRequired = true)]
        public 행정동디오라마보행망LinkFragment후보Data[] linkFragments;

        [DataMember(Name = "overlaySetHashSha256", Order = 27, IsRequired = true)]
        public string overlaySetHashSha256;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개도로방향OverlaySetData
    {
        [DataMember(Name = "schemaVersion", Order = 0, IsRequired = true)]
        public string schemaVersion;
        [DataMember(Name = "revision", Order = 1, IsRequired = true)]
        public string revision;
        [DataMember(Name = "overlaySetStableId", Order = 2, IsRequired = true)]
        public string overlaySetStableId;
        [DataMember(Name = "administrativeAreaStableId", Order = 3, IsRequired = true)]
        public string administrativeAreaStableId;
        [DataMember(Name = "sourceVintage", Order = 4, IsRequired = true)]
        public string sourceVintage;
        [DataMember(Name = "candidatePoints", Order = 5, IsRequired = true)]
        public 행정동디오라마도로방향점후보Data[] candidatePoints;
        [DataMember(Name = "privateReviewVisualizationAllowed", Order = 6, IsRequired = true)]
        public bool privateReviewVisualizationAllowed;
        [DataMember(Name = "reviewVisualizationOnly", Order = 7, IsRequired = true)]
        public bool reviewVisualizationOnly;
        [DataMember(Name = "historicalBoundaryBootstrapOnly", Order = 8, IsRequired = true)]
        public bool historicalBoundaryBootstrapOnly;
        [DataMember(Name = "angleSemanticVerified", Order = 9, IsRequired = true)]
        public bool angleSemanticVerified;
        [DataMember(Name = "directionResolved", Order = 10, IsRequired = true)]
        public bool directionResolved;
        [DataMember(Name = "renderedArrowAuthorized", Order = 11, IsRequired = true)]
        public bool renderedArrowAuthorized;
        [DataMember(Name = "sourceLengthUnitVerified", Order = 12, IsRequired = true)]
        public bool sourceLengthUnitVerified;
        [DataMember(Name = "publicDisplayAllowed", Order = 13, IsRequired = true)]
        public bool publicDisplayAllowed;
        [DataMember(Name = "runtimeAuthorized", Order = 14, IsRequired = true)]
        public bool runtimeAuthorized;
        [DataMember(Name = "traversalReady", Order = 15, IsRequired = true)]
        public bool traversalReady;
        [DataMember(Name = "gameplayReady", Order = 16, IsRequired = true)]
        public bool gameplayReady;
        [DataMember(Name = "currentPointerUsed", Order = 17, IsRequired = true)]
        public bool currentPointerUsed;
        [DataMember(Name = "currentPointerUpdated", Order = 18, IsRequired = true)]
        public bool currentPointerUpdated;
        [DataMember(Name = "sourceCandidateUnityApplyAllowed", Order = 19, IsRequired = true)]
        public bool sourceCandidateUnityApplyAllowed;
        [DataMember(Name = "overlaySetHashSha256", Order = 20, IsRequired = true)]
        public string overlaySetHashSha256;
    }

    [DataContract]
    public sealed class 행정동디오라마도로방향점후보Data
    {
        [DataMember(Name = "candidateStableId", Order = 0, IsRequired = true)]
        public string candidateStableId;
        [DataMember(Name = "commonEnuMillimeters", Order = 1, IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData commonEnuMillimeters;
        [DataMember(Name = "officialKindCode", Order = 2, IsRequired = true)]
        public string officialKindCode;
        [DataMember(Name = "sourceDirectionNumericValue", Order = 3, IsRequired = true)]
        public double sourceDirectionNumericValue;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개비오톱OverlaySetData
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] public string schemaVersion;
        [DataMember(Name = "revision", IsRequired = true)] public string revision;
        [DataMember(Name = "overlaySetStableId", IsRequired = true)] public string overlaySetStableId;
        [DataMember(Name = "administrativeAreaStableId", IsRequired = true)] public string administrativeAreaStableId;
        [DataMember(Name = "sourceVintage", IsRequired = true)] public string sourceVintage;
        [DataMember(Name = "coordinateFrame", IsRequired = true)] public BiotopeCoordinateFrameData coordinateFrame;
        [DataMember(Name = "coordinateUnit", IsRequired = true)] public string coordinateUnit;
        [DataMember(Name = "candidatePolygons", IsRequired = true)] public BiotopeCandidateData[] candidatePolygons;
        [DataMember(Name = "privateReviewVisualizationAllowed", IsRequired = true)] public bool privateReviewVisualizationAllowed;
        [DataMember(Name = "biotopeOutlineAuthorized", IsRequired = true)] public bool biotopeOutlineAuthorized;
        [DataMember(Name = "allSourceSegmentsRendered", IsRequired = true)] public bool allSourceSegmentsRendered;
        [DataMember(Name = "filledPolygonAuthorized", IsRequired = true)] public bool filledPolygonAuthorized;
        [DataMember(Name = "colliderAuthorized", IsRequired = true)] public bool colliderAuthorized;
        [DataMember(Name = "legalEffectAuthorized", IsRequired = true)] public bool legalEffectAuthorized;
        [DataMember(Name = "speciesObservationAuthorized", IsRequired = true)] public bool speciesObservationAuthorized;
        [DataMember(Name = "safetyAuthorized", IsRequired = true)] public bool safetyAuthorized;
        [DataMember(Name = "passageAuthorized", IsRequired = true)] public bool passageAuthorized;
        [DataMember(Name = "publicDisplayAllowed", IsRequired = true)] public bool publicDisplayAllowed;
        [DataMember(Name = "runtimeAuthorized", IsRequired = true)] public bool runtimeAuthorized;
        [DataMember(Name = "traversalReady", IsRequired = true)] public bool traversalReady;
        [DataMember(Name = "gameplayReady", IsRequired = true)] public bool gameplayReady;
        [DataMember(Name = "currentPointerUsed", IsRequired = true)] public bool currentPointerUsed;
        [DataMember(Name = "currentPointerUpdated", IsRequired = true)] public bool currentPointerUpdated;
        [DataMember(Name = "databaseWriteAllowed", IsRequired = true)] public bool databaseWriteAllowed;
        [DataMember(Name = "mongoWriteAllowed", IsRequired = true)] public bool mongoWriteAllowed;
        [DataMember(Name = "sourceCandidateUnityApplyAllowed", IsRequired = true)] public bool sourceCandidateUnityApplyAllowed;
        [DataMember(Name = "overlaySetHashSha256", IsRequired = true)] public string overlaySetHashSha256;
    }

    [DataContract] public sealed class BiotopeCoordinateFrameData
    {
        [DataMember(Name="metersPerUnit", IsRequired=true)] public double metersPerUnit;
        [DataMember(Name="method", IsRequired=true)] public string method;
        [DataMember(Name="originLatitude", IsRequired=true)] public double originLatitude;
        [DataMember(Name="originLongitude", IsRequired=true)] public double originLongitude;
        [DataMember(Name="worldOffsetX", IsRequired=true)] public double worldOffsetX;
        [DataMember(Name="worldOffsetZ", IsRequired=true)] public double worldOffsetZ;
    }
    [DataContract] public sealed class BiotopeCandidateData
    {
        [DataMember(Name="stableId", IsRequired=true)] public string stableId;
        [DataMember(Name="classification", IsRequired=true)] public BiotopeClassificationData classification;
        [DataMember(Name="polygons", IsRequired=true)] public BiotopePolygonData[] polygons;
    }
    [DataContract] public sealed class BiotopeClassificationData
    {
        [DataMember(Name="evaluationGrade", IsRequired=true)] public string evaluationGrade;
        [DataMember(Name="legendLabel", IsRequired=true)] public string legendLabel;
        [DataMember(Name="typeCode", IsRequired=true)] public string typeCode;
    }
    [DataContract] public sealed class BiotopePolygonData
    {
        [DataMember(Name="exteriorRingMm", IsRequired=true)] public double[][] exteriorRingMm;
        [DataMember(Name="interiorRingsMm", IsRequired=true)] public double[][][] interiorRingsMm;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개차선표시OverlaySetData
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] public string schemaVersion;
        [DataMember(Name = "revision", IsRequired = true)] public string revision;
        [DataMember(Name = "overlaySetStableId", IsRequired = true)] public string overlaySetStableId;
        [DataMember(Name = "administrativeAreaStableId", IsRequired = true)] public string administrativeAreaStableId;
        [DataMember(Name = "sourceVintage", IsRequired = true)] public string sourceVintage;
        [DataMember(Name = "coordinateFrame", IsRequired = true)] public BiotopeCoordinateFrameData coordinateFrame;
        [DataMember(Name = "coordinateUnit", IsRequired = true)] public string coordinateUnit;
        [DataMember(Name = "candidateMarkings", IsRequired = true)] public 행정동디오라마차선표시후보Data[] candidateMarkings;
        [DataMember(Name = "historicalBoundaryBootstrapOnly", IsRequired = true)] public bool historicalBoundaryBootstrapOnly;
        [DataMember(Name = "privateReviewOnly", IsRequired = true)] public bool privateReviewOnly;
        [DataMember(Name = "privateReviewVisualizationAllowed", IsRequired = true)] public bool privateReviewVisualizationAllowed;
        [DataMember(Name = "reviewVisualizationOnly", IsRequired = true)] public bool reviewVisualizationOnly;
        [DataMember(Name = "laneOutlineAuthorized", IsRequired = true)] public bool laneOutlineAuthorized;
        [DataMember(Name = "allSourceSegmentsRendered", IsRequired = true)] public bool allSourceSegmentsRendered;
        [DataMember(Name = "laneMeaningAuthorized", IsRequired = true)] public bool laneMeaningAuthorized;
        [DataMember(Name = "directionAuthorized", IsRequired = true)] public bool directionAuthorized;
        [DataMember(Name = "trafficAuthorized", IsRequired = true)] public bool trafficAuthorized;
        [DataMember(Name = "stopLineAuthorized", IsRequired = true)] public bool stopLineAuthorized;
        [DataMember(Name = "signalAuthorized", IsRequired = true)] public bool signalAuthorized;
        [DataMember(Name = "colliderAuthorized", IsRequired = true)] public bool colliderAuthorized;
        [DataMember(Name = "publicDisplayAllowed", IsRequired = true)] public bool publicDisplayAllowed;
        [DataMember(Name = "runtimeAuthorized", IsRequired = true)] public bool runtimeAuthorized;
        [DataMember(Name = "traversalReady", IsRequired = true)] public bool traversalReady;
        [DataMember(Name = "gameplayReady", IsRequired = true)] public bool gameplayReady;
        [DataMember(Name = "currentPointerUsed", IsRequired = true)] public bool currentPointerUsed;
        [DataMember(Name = "currentPointerUpdated", IsRequired = true)] public bool currentPointerUpdated;
        [DataMember(Name = "databaseWriteAllowed", IsRequired = true)] public bool databaseWriteAllowed;
        [DataMember(Name = "mongoWriteAllowed", IsRequired = true)] public bool mongoWriteAllowed;
        [DataMember(Name = "sourceCandidateUnityApplyAllowed", IsRequired = true)] public bool sourceCandidateUnityApplyAllowed;
        [DataMember(Name = "overlaySetHashSha256", IsRequired = true)] public string overlaySetHashSha256;
    }

    [DataContract]
    public sealed class 행정동디오라마차선표시후보Data
    {
        [DataMember(Name = "candidateStableId", IsRequired = true)] public string candidateStableId;
        [DataMember(Name = "sourceKindCode", IsRequired = true)] public string sourceKindCode;
        [DataMember(Name = "sourceSecondaryKindCode", IsRequired = true)] public string sourceSecondaryKindCode;
        [DataMember(Name = "sourceFormCode", IsRequired = true)] public string sourceFormCode;
        [DataMember(Name = "fragments", IsRequired = true)] public 행정동디오라마차선표시조각Data[] fragments;
    }

    [DataContract]
    public sealed class 행정동디오라마차선표시조각Data
    {
        [DataMember(Name = "fragmentOrdinal", IsRequired = true)] public int fragmentOrdinal;
        [DataMember(Name = "commonEnuMillimeterPath", IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData[] commonEnuMillimeterPath;
    }

    [DataContract]
    public sealed class 행정동디오라마비공개수평등고선추적OverlaySetData
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] public string schemaVersion;
        [DataMember(Name = "revision", IsRequired = true)] public string revision;
        [DataMember(Name = "overlaySetStableId", IsRequired = true)] public string overlaySetStableId;
        [DataMember(Name = "administrativeAreaStableId", IsRequired = true)] public string administrativeAreaStableId;
        [DataMember(Name = "sourceVintage", IsRequired = true)] public string sourceVintage;
        [DataMember(Name = "coordinateFrame", IsRequired = true)] public BiotopeCoordinateFrameData coordinateFrame;
        [DataMember(Name = "coordinateUnit", IsRequired = true)] public string coordinateUnit;
        [DataMember(Name = "candidateTraces", IsRequired = true)] public 행정동디오라마수평등고선추적후보Data[] candidateTraces;
        [DataMember(Name = "privateReviewOnly", IsRequired = true)] public bool privateReviewOnly;
        [DataMember(Name = "privateReviewVisualizationAllowed", IsRequired = true)] public bool privateReviewVisualizationAllowed;
        [DataMember(Name = "reviewVisualizationOnly", IsRequired = true)] public bool reviewVisualizationOnly;
        [DataMember(Name = "historicalBoundaryBootstrapOnly", IsRequired = true)] public bool historicalBoundaryBootstrapOnly;
        [DataMember(Name = "sourceHorizontalGeometryObserved", IsRequired = true)] public bool sourceHorizontalGeometryObserved;
        [DataMember(Name = "allSourceSegmentsRendered", IsRequired = true)] public bool allSourceSegmentsRendered;
        [DataMember(Name = "sourceAttributeDatabaseRead", IsRequired = true)] public bool sourceAttributeDatabaseRead;
        [DataMember(Name = "sourceElevationValuesRead", IsRequired = true)] public bool sourceElevationValuesRead;
        [DataMember(Name = "sourceElevationValuesSerialized", IsRequired = true)] public bool sourceElevationValuesSerialized;
        [DataMember(Name = "currentBoundaryEstablished", IsRequired = true)] public bool currentBoundaryEstablished;
        [DataMember(Name = "currentTerrainEstablished", IsRequired = true)] public bool currentTerrainEstablished;
        [DataMember(Name = "contourMeaningAuthorized", IsRequired = true)] public bool contourMeaningAuthorized;
        [DataMember(Name = "elevationMeaningAuthorized", IsRequired = true)] public bool elevationMeaningAuthorized;
        [DataMember(Name = "heightUnitVerified", IsRequired = true)] public bool heightUnitVerified;
        [DataMember(Name = "verticalDatumVerified", IsRequired = true)] public bool verticalDatumVerified;
        [DataMember(Name = "terrainSurfaceAuthorized", IsRequired = true)] public bool terrainSurfaceAuthorized;
        [DataMember(Name = "terrainMeshAuthorized", IsRequired = true)] public bool terrainMeshAuthorized;
        [DataMember(Name = "colliderAuthorized", IsRequired = true)] public bool colliderAuthorized;
        [DataMember(Name = "publicDisplayAllowed", IsRequired = true)] public bool publicDisplayAllowed;
        [DataMember(Name = "databaseWriteAllowed", IsRequired = true)] public bool databaseWriteAllowed;
        [DataMember(Name = "mongoWriteAllowed", IsRequired = true)] public bool mongoWriteAllowed;
        [DataMember(Name = "sourceCandidateUnityApplyAllowed", IsRequired = true)] public bool sourceCandidateUnityApplyAllowed;
        [DataMember(Name = "runtimeAuthorized", IsRequired = true)] public bool runtimeAuthorized;
        [DataMember(Name = "traversalReady", IsRequired = true)] public bool traversalReady;
        [DataMember(Name = "gameplayReady", IsRequired = true)] public bool gameplayReady;
        [DataMember(Name = "currentPointerUsed", IsRequired = true)] public bool currentPointerUsed;
        [DataMember(Name = "currentPointerUpdated", IsRequired = true)] public bool currentPointerUpdated;
        [DataMember(Name = "overlaySetHashSha256", IsRequired = true)] public string overlaySetHashSha256;
    }

    [DataContract]
    public sealed class 행정동디오라마수평등고선추적후보Data
    {
        [DataMember(Name = "candidateStableId", IsRequired = true)] public string candidateStableId;
        [DataMember(Name = "fragments", IsRequired = true)] public 행정동디오라마수평등고선추적조각Data[] fragments;
    }

    [DataContract]
    public sealed class 행정동디오라마수평등고선추적조각Data
    {
        [DataMember(Name = "fragmentOrdinal", IsRequired = true)] public int fragmentOrdinal;
        [DataMember(Name = "commonEnuMillimeterPath", IsRequired = true)]
        public 행정동디오라마공통EnuMillimetersData[] commonEnuMillimeterPath;
    }

    public sealed class 행정동디오라마검토RoadDirectionPoint
    {
        internal 행정동디오라마검토RoadDirectionPoint(string candidateStableId,
            double x, double z, string officialKindCode, double sourceDirectionNumericValue)
        {
            CandidateStableId = candidateStableId;
            X = x;
            Z = z;
            OfficialKindCode = officialKindCode;
            SourceDirectionNumericValue = sourceDirectionNumericValue;
        }
        public string CandidateStableId { get; }
        public double X { get; }
        public double Z { get; }
        public string OfficialKindCode { get; }
        public double SourceDirectionNumericValue { get; }
    }

    public sealed class 행정동디오라마검토BiotopePoint
    {
        internal 행정동디오라마검토BiotopePoint(double x, double z) { X=x; Z=z; }
        public double X { get; }
        public double Z { get; }
    }
    public sealed class 행정동디오라마검토BiotopeRing
    {
        internal 행정동디오라마검토BiotopeRing(IReadOnlyList<행정동디오라마검토BiotopePoint> points) { Points=points; }
        public IReadOnlyList<행정동디오라마검토BiotopePoint> Points { get; }
    }
    public sealed class 행정동디오라마검토BiotopePolygon
    {
        internal 행정동디오라마검토BiotopePolygon(행정동디오라마검토BiotopeRing exterior, IReadOnlyList<행정동디오라마검토BiotopeRing> holes) { Exterior=exterior; InteriorRings=holes; }
        public 행정동디오라마검토BiotopeRing Exterior { get; }
        public IReadOnlyList<행정동디오라마검토BiotopeRing> InteriorRings { get; }
    }
    public sealed class 행정동디오라마검토BiotopeCandidate
    {
        internal 행정동디오라마검토BiotopeCandidate(string stableId, string typeCode, string grade, string label, IReadOnlyList<행정동디오라마검토BiotopePolygon> polygons)
        { StableId=stableId; TypeCode=typeCode; EvaluationGrade=grade; LegendLabel=label; Polygons=polygons; }
        public string StableId { get; }
        public string TypeCode { get; }
        public string EvaluationGrade { get; }
        public string LegendLabel { get; }
        public IReadOnlyList<행정동디오라마검토BiotopePolygon> Polygons { get; }
    }
    public sealed class 행정동디오라마비공개비오톱OverlaySet
    {
        internal 행정동디오라마비공개비오톱OverlaySet(행정동디오라마비공개비오톱OverlaySetData source, IReadOnlyList<행정동디오라마검토BiotopeCandidate> candidates)
        {
            SchemaVersion=source.schemaVersion; Revision=source.revision; OverlaySetStableId=source.overlaySetStableId;
            AdministrativeAreaStableId=source.administrativeAreaStableId; SourceVintage=source.sourceVintage;
            OverlaySetHashSha256=source.overlaySetHashSha256; Candidates=candidates;
        }
        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public string OverlaySetHashSha256 { get; }
        public IReadOnlyList<행정동디오라마검토BiotopeCandidate> Candidates { get; }
        public int PolygonPartCount => Candidates.Sum(c => c.Polygons.Count);
        public int InteriorRingCount => Candidates.Sum(c => c.Polygons.Sum(p => p.InteriorRings.Count));
        public int RingVertexCount => Candidates.Sum(c => c.Polygons.Sum(p => p.Exterior.Points.Count + p.InteriorRings.Sum(r => r.Points.Count)));
    }

    public sealed class 행정동디오라마검토LaneMarkingPoint
    {
        internal 행정동디오라마검토LaneMarkingPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }
    }

    public sealed class 행정동디오라마검토LaneMarkingFragment
    {
        internal 행정동디오라마검토LaneMarkingFragment(int fragmentOrdinal,
            IReadOnlyList<행정동디오라마검토LaneMarkingPoint> path)
        {
            FragmentOrdinal = fragmentOrdinal;
            Path = path;
        }

        public int FragmentOrdinal { get; }
        public IReadOnlyList<행정동디오라마검토LaneMarkingPoint> Path { get; }
    }

    public sealed class 행정동디오라마검토LaneMarkingCandidate
    {
        internal 행정동디오라마검토LaneMarkingCandidate(string candidateStableId,
            string sourceKindCode, string sourceSecondaryKindCode, string sourceFormCode,
            IReadOnlyList<행정동디오라마검토LaneMarkingFragment> fragments)
        {
            CandidateStableId = candidateStableId;
            SourceKindCode = sourceKindCode;
            SourceSecondaryKindCode = sourceSecondaryKindCode;
            SourceFormCode = sourceFormCode;
            Fragments = fragments;
        }

        public string CandidateStableId { get; }
        public string SourceKindCode { get; }
        public string SourceSecondaryKindCode { get; }
        public string SourceFormCode { get; }
        public IReadOnlyList<행정동디오라마검토LaneMarkingFragment> Fragments { get; }
    }

    public sealed class 행정동디오라마비공개차선표시OverlaySet
    {
        internal 행정동디오라마비공개차선표시OverlaySet(
            행정동디오라마비공개차선표시OverlaySetData source,
            IReadOnlyList<행정동디오라마검토LaneMarkingCandidate> candidates)
        {
            SchemaVersion = source.schemaVersion;
            Revision = source.revision;
            OverlaySetStableId = source.overlaySetStableId;
            AdministrativeAreaStableId = source.administrativeAreaStableId;
            SourceVintage = source.sourceVintage;
            OverlaySetHashSha256 = source.overlaySetHashSha256;
            Candidates = candidates;
        }

        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public string OverlaySetHashSha256 { get; }
        public IReadOnlyList<행정동디오라마검토LaneMarkingCandidate> Candidates { get; }
        public int FragmentCount => Candidates.Sum(value => value.Fragments.Count);
        public int PathVertexCount => Candidates.Sum(value =>
            value.Fragments.Sum(fragment => fragment.Path.Count));
        public int SegmentCount => Candidates.Sum(value =>
            value.Fragments.Sum(fragment => fragment.Path.Count - 1));
    }

    public sealed class 행정동디오라마검토HorizontalContourTracePoint
    {
        internal 행정동디오라마검토HorizontalContourTracePoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }
    }

    public sealed class 행정동디오라마검토HorizontalContourTraceFragment
    {
        internal 행정동디오라마검토HorizontalContourTraceFragment(int fragmentOrdinal,
            IReadOnlyList<행정동디오라마검토HorizontalContourTracePoint> path)
        {
            FragmentOrdinal = fragmentOrdinal;
            Path = path;
        }

        public int FragmentOrdinal { get; }
        public IReadOnlyList<행정동디오라마검토HorizontalContourTracePoint> Path { get; }
    }

    public sealed class 행정동디오라마검토HorizontalContourTraceCandidate
    {
        internal 행정동디오라마검토HorizontalContourTraceCandidate(string candidateStableId,
            IReadOnlyList<행정동디오라마검토HorizontalContourTraceFragment> fragments)
        {
            CandidateStableId = candidateStableId;
            Fragments = fragments;
        }

        public string CandidateStableId { get; }
        public IReadOnlyList<행정동디오라마검토HorizontalContourTraceFragment> Fragments { get; }
    }

    public sealed class 행정동디오라마비공개수평등고선추적OverlaySet
    {
        internal 행정동디오라마비공개수평등고선추적OverlaySet(
            행정동디오라마비공개수평등고선추적OverlaySetData source,
            IReadOnlyList<행정동디오라마검토HorizontalContourTraceCandidate> candidates)
        {
            SchemaVersion = source.schemaVersion;
            Revision = source.revision;
            OverlaySetStableId = source.overlaySetStableId;
            AdministrativeAreaStableId = source.administrativeAreaStableId;
            SourceVintage = source.sourceVintage;
            OverlaySetHashSha256 = source.overlaySetHashSha256;
            Candidates = candidates;
        }

        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public string OverlaySetHashSha256 { get; }
        public IReadOnlyList<행정동디오라마검토HorizontalContourTraceCandidate> Candidates { get; }
        public int FragmentCount => Candidates.Sum(value => value.Fragments.Count);
        public int PathVertexCount => Candidates.Sum(value =>
            value.Fragments.Sum(fragment => fragment.Path.Count));
        public int SegmentCount => Candidates.Sum(value =>
            value.Fragments.Sum(fragment => fragment.Path.Count - 1));
    }

    public sealed class 행정동디오라마비공개도로방향OverlaySet
    {
        internal 행정동디오라마비공개도로방향OverlaySet(
            행정동디오라마비공개도로방향OverlaySetData source,
            IReadOnlyList<행정동디오라마검토RoadDirectionPoint> candidatePoints)
        {
            SchemaVersion = source.schemaVersion;
            Revision = source.revision;
            OverlaySetStableId = source.overlaySetStableId;
            AdministrativeAreaStableId = source.administrativeAreaStableId;
            SourceVintage = source.sourceVintage;
            OverlaySetHashSha256 = source.overlaySetHashSha256;
            CandidatePoints = candidatePoints;
            AngleSemanticVerified = source.angleSemanticVerified;
            DirectionResolved = source.directionResolved;
            RenderedArrowAuthorized = source.renderedArrowAuthorized;
            SourceLengthUnitVerified = source.sourceLengthUnitVerified;
        }
        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public string OverlaySetHashSha256 { get; }
        public IReadOnlyList<행정동디오라마검토RoadDirectionPoint> CandidatePoints { get; }
        public bool AngleSemanticVerified { get; }
        public bool DirectionResolved { get; }
        public bool RenderedArrowAuthorized { get; }
        public bool SourceLengthUnitVerified { get; }
        public int UnresolvedDirectionCount => CandidatePoints.Count;
    }

    public sealed class 행정동디오라마검토CrosswalkPoint
    {
        internal 행정동디오라마검토CrosswalkPoint(string candidateStableId,
            double x, double z, bool pedestrianSignalPresent,
            bool sourceDistrictSpatialAssignmentConflict, string qualityCode)
        {
            CandidateStableId = candidateStableId;
            X = x;
            Z = z;
            PedestrianSignalPresent = pedestrianSignalPresent;
            SourceDistrictSpatialAssignmentConflict = sourceDistrictSpatialAssignmentConflict;
            QualityCode = qualityCode;
        }

        public string CandidateStableId { get; }
        public double X { get; }
        public double Z { get; }
        public bool PedestrianSignalPresent { get; }
        public bool SourceDistrictSpatialAssignmentConflict { get; }
        public string QualityCode { get; }
    }

    public sealed class 행정동디오라마검토IntersectionPoint
    {
        internal 행정동디오라마검토IntersectionPoint(string candidateStableId,
            double x, double z, int linkedCrosswalkCount,
            int otherHistoricalAdministrativeAreaLinkCount,
            bool sourceDistrictSpatialAssignmentConflict, string qualityCode)
        {
            CandidateStableId = candidateStableId;
            X = x;
            Z = z;
            LinkedCrosswalkCount = linkedCrosswalkCount;
            OtherHistoricalAdministrativeAreaLinkCount =
                otherHistoricalAdministrativeAreaLinkCount;
            SourceDistrictSpatialAssignmentConflict = sourceDistrictSpatialAssignmentConflict;
            QualityCode = qualityCode;
        }

        public string CandidateStableId { get; }
        public double X { get; }
        public double Z { get; }
        public int LinkedCrosswalkCount { get; }
        public int OtherHistoricalAdministrativeAreaLinkCount { get; }
        public bool SourceDistrictSpatialAssignmentConflict { get; }
        public string QualityCode { get; }
    }

    /// <summary>검증된 비공개 후보를 로컬 좌표로 깊은 복사한 읽기 전용 묶음.</summary>
    public sealed class 행정동디오라마비공개관찰OverlaySet
    {
        internal 행정동디오라마비공개관찰OverlaySet(
            행정동디오라마비공개관찰OverlaySetData source,
            IReadOnlyList<행정동디오라마검토CrosswalkPoint> crosswalkPoints,
            IReadOnlyList<행정동디오라마검토IntersectionPoint> intersectionPoints)
        {
            SchemaVersion = source.schemaVersion;
            Revision = source.revision;
            OverlaySetStableId = source.overlaySetStableId;
            OverlaySetHashSha256 = source.overlaySetHashSha256;
            AdministrativeAreaStableId = source.administrativeAreaStableId;
            SourceVintage = source.sourceVintage;
            HistoricalBoundaryBootstrapOnly = source.historicalBoundaryBootstrapOnly;
            PrivateReviewVisualizationAllowed = source.privateReviewVisualizationAllowed;
            ReviewVisualizationOnly = source.reviewVisualizationOnly;
            SourceCandidateUnityApplyAllowed = source.sourceCandidateUnityApplyAllowed;
            PublicDisplayAllowed = source.publicDisplayAllowed;
            RuntimeAuthorized = source.runtimeAuthorized;
            TraversalReady = source.traversalReady;
            GameplayReady = source.gameplayReady;
            CurrentPointerUsed = source.currentPointerUsed;
            CurrentPointerUpdated = source.currentPointerUpdated;
            ExactAddressIncluded = source.exactAddressIncluded;
            BusinessIdentityIncluded = source.businessIdentityIncluded;
            PersonalDataIncluded = source.personalDataIncluded;
            CrosswalkPoints = crosswalkPoints;
            IntersectionPoints = intersectionPoints;
        }

        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string OverlaySetHashSha256 { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public bool HistoricalBoundaryBootstrapOnly { get; }
        public bool PrivateReviewVisualizationAllowed { get; }
        public bool ReviewVisualizationOnly { get; }
        public bool SourceCandidateUnityApplyAllowed { get; }
        public bool PublicDisplayAllowed { get; }
        public bool RuntimeAuthorized { get; }
        public bool TraversalReady { get; }
        public bool GameplayReady { get; }
        public bool CurrentPointerUsed { get; }
        public bool CurrentPointerUpdated { get; }
        public bool ExactAddressIncluded { get; }
        public bool BusinessIdentityIncluded { get; }
        public bool PersonalDataIncluded { get; }
        public IReadOnlyList<행정동디오라마검토CrosswalkPoint> CrosswalkPoints { get; }
        public IReadOnlyList<행정동디오라마검토IntersectionPoint> IntersectionPoints { get; }
    }

    public sealed class 행정동디오라마검토WalkActorSummary
    {
        internal 행정동디오라마검토WalkActorSummary(
            행정동디오라마보행망ActorSummaryFlagsData source)
        {
            SourceReported = source.sourceReported;
            Pedestrian = source.pedestrian;
            Bicycle = source.bicycle;
            PersonalMobility = source.personalMobility;
            Vehicle = source.vehicle;
        }

        public bool SourceReported { get; }
        public bool Pedestrian { get; }
        public bool Bicycle { get; }
        public bool PersonalMobility { get; }
        public bool Vehicle { get; }
    }

    public sealed class 행정동디오라마검토WalkNodePoint
    {
        internal 행정동디오라마검토WalkNodePoint(string candidateStableId,
            double x, double z, string kindCode, string qualityCode,
            행정동디오라마검토WalkActorSummary actorSummary)
        {
            CandidateStableId = candidateStableId;
            X = x;
            Z = z;
            KindCode = kindCode;
            QualityCode = qualityCode;
            ActorSummary = actorSummary;
        }

        public string CandidateStableId { get; }
        public double X { get; }
        public double Z { get; }
        public string KindCode { get; }
        public string QualityCode { get; }
        public 행정동디오라마검토WalkActorSummary ActorSummary { get; }
    }

    public sealed class 행정동디오라마검토WalkLinkFragment
    {
        internal 행정동디오라마검토WalkLinkFragment(string candidateStableId,
            IReadOnlyList<행정동디오라마검토Point> path,
            string kindCode, string qualityCode,
            행정동디오라마검토WalkActorSummary actorSummary)
        {
            CandidateStableId = candidateStableId;
            Path = path;
            KindCode = kindCode;
            QualityCode = qualityCode;
            ActorSummary = actorSummary;
        }

        public string CandidateStableId { get; }
        public IReadOnlyList<행정동디오라마검토Point> Path { get; }
        public string KindCode { get; }
        public string QualityCode { get; }
        public 행정동디오라마검토WalkActorSummary ActorSummary { get; }
    }

    /// <summary>검증된 보행망 후보를 로컬 좌표로 깊은 복사한 읽기 전용 묶음.</summary>
    public sealed class 행정동디오라마비공개보행망OverlaySet
    {
        internal 행정동디오라마비공개보행망OverlaySet(
            행정동디오라마비공개보행망OverlaySetData source,
            IReadOnlyList<행정동디오라마검토WalkNodePoint> nodePoints,
            IReadOnlyList<행정동디오라마검토WalkLinkFragment> linkFragments)
        {
            SchemaVersion = source.schemaVersion;
            Revision = source.revision;
            OverlaySetStableId = source.overlaySetStableId;
            OverlaySetHashSha256 = source.overlaySetHashSha256;
            AdministrativeAreaStableId = source.administrativeAreaStableId;
            SourceVintage = source.sourceVintage;
            HistoricalBoundaryBootstrapOnly = source.historicalBoundaryBootstrapOnly;
            PrivateReviewOnly = source.privateReviewOnly;
            PrivateReviewVisualizationAllowed = source.privateReviewVisualizationAllowed;
            ReviewVisualizationOnly = source.reviewVisualizationOnly;
            ObservationCandidateOnly = source.observationCandidateOnly;
            SourceCandidateUnityApplyAllowed = source.sourceCandidateUnityApplyAllowed;
            PublicDisplayAllowed = source.publicDisplayAllowed;
            RuntimeAuthorized = source.runtimeAuthorized;
            TraversalReady = source.traversalReady;
            GameplayReady = source.gameplayReady;
            CurrentPointerUsed = source.currentPointerUsed;
            CurrentPointerUpdated = source.currentPointerUpdated;
            NodePoints = nodePoints;
            LinkFragments = linkFragments;
        }

        public string SchemaVersion { get; }
        public string Revision { get; }
        public string OverlaySetStableId { get; }
        public string OverlaySetHashSha256 { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceVintage { get; }
        public bool HistoricalBoundaryBootstrapOnly { get; }
        public bool PrivateReviewOnly { get; }
        public bool PrivateReviewVisualizationAllowed { get; }
        public bool ReviewVisualizationOnly { get; }
        public bool ObservationCandidateOnly { get; }
        public bool SourceCandidateUnityApplyAllowed { get; }
        public bool PublicDisplayAllowed { get; }
        public bool RuntimeAuthorized { get; }
        public bool TraversalReady { get; }
        public bool GameplayReady { get; }
        public bool CurrentPointerUsed { get; }
        public bool CurrentPointerUpdated { get; }
        public IReadOnlyList<행정동디오라마검토WalkNodePoint> NodePoints { get; }
        public IReadOnlyList<행정동디오라마검토WalkLinkFragment> LinkFragments { get; }
        public int LinkPathVertexCount => LinkFragments.Sum(value => value.Path.Count);
    }

    /// <summary>
    /// 서버 해석기가 수용한 행정동 도형을 Unity 표현에서만 쓰도록 깊은 복사한 상태 사본이다.
    /// 원본 좌표의 중심을 로컬 원점으로 옮기지만 주소·통행·게임플레이 의미는 만들지 않는다.
    /// </summary>
    public sealed class 행정동디오라마검토Snapshot
    {
        private readonly IReadOnlyDictionary<string, 행정동디오라마검토Building> buildingsById;

        internal 행정동디오라마검토Snapshot(
            string administrativeAreaStableId,
            string displayName,
            string sourceVintage,
            string projectionHashSha256,
            string readinessCode,
            double sourceCenterX,
            double sourceCenterZ,
            double metersPerUnit,
            행정동디오라마검토Bounds bounds,
            IReadOnlyList<행정동디오라마검토Point> boundary,
            IReadOnlyList<행정동디오라마검토Building> buildings,
            IReadOnlyList<행정동디오라마검토RoadSegment> roads,
            int tileCount,
            행정동디오라마비공개관찰OverlaySet privateObservationOverlay = null,
            행정동디오라마비공개보행망OverlaySet privateWalkNetworkOverlay = null,
            행정동디오라마비공개도로방향OverlaySet privateRoadDirectionOverlay = null,
            행정동디오라마비공개비오톱OverlaySet privateBiotopeOverlay = null,
            행정동디오라마비공개차선표시OverlaySet privateLaneMarkingOverlay = null,
            행정동디오라마비공개수평등고선추적OverlaySet
                privateHorizontalContourTraceOverlay = null)
        {
            AdministrativeAreaStableId = administrativeAreaStableId;
            DisplayName = displayName;
            SourceVintage = sourceVintage;
            ProjectionHashSha256 = projectionHashSha256;
            ReadinessCode = readinessCode;
            SourceCenterX = sourceCenterX;
            SourceCenterZ = sourceCenterZ;
            MetersPerUnit = metersPerUnit;
            Bounds = bounds;
            Boundary = boundary;
            Buildings = buildings;
            Roads = roads;
            TileCount = tileCount;
            PrivateObservationOverlay = privateObservationOverlay;
            PrivateWalkNetworkOverlay = privateWalkNetworkOverlay;
            PrivateRoadDirectionOverlay = privateRoadDirectionOverlay;
            PrivateBiotopeOverlay = privateBiotopeOverlay;
            PrivateLaneMarkingOverlay = privateLaneMarkingOverlay;
            PrivateHorizontalContourTraceOverlay = privateHorizontalContourTraceOverlay;
            buildingsById = buildings.ToDictionary(value => value.BuildingStableId,
                StringComparer.Ordinal);
            DepthPlan = 행정동디오라마깊이Plan.Create(this);
        }

        public string AdministrativeAreaStableId { get; }
        public string DisplayName { get; }
        public string SourceVintage { get; }
        public string ProjectionHashSha256 { get; }
        public string ReadinessCode { get; }
        public double SourceCenterX { get; }
        public double SourceCenterZ { get; }
        public double MetersPerUnit { get; }
        public 행정동디오라마검토Bounds Bounds { get; }
        public IReadOnlyList<행정동디오라마검토Point> Boundary { get; }
        public IReadOnlyList<행정동디오라마검토Building> Buildings { get; }
        public IReadOnlyList<행정동디오라마검토RoadSegment> Roads { get; }
        public int TileCount { get; }
        public 행정동디오라마비공개관찰OverlaySet PrivateObservationOverlay { get; }
        public bool HasPrivateObservationOverlay => PrivateObservationOverlay != null;
        public 행정동디오라마비공개보행망OverlaySet PrivateWalkNetworkOverlay { get; }
        public bool HasPrivateWalkNetworkOverlay => PrivateWalkNetworkOverlay != null;
        public 행정동디오라마비공개도로방향OverlaySet PrivateRoadDirectionOverlay { get; }
        public bool HasPrivateRoadDirectionOverlay => PrivateRoadDirectionOverlay != null;
        public 행정동디오라마비공개비오톱OverlaySet PrivateBiotopeOverlay { get; }
        public bool HasPrivateBiotopeOverlay => PrivateBiotopeOverlay != null;
        public 행정동디오라마비공개차선표시OverlaySet PrivateLaneMarkingOverlay { get; }
        public bool HasPrivateLaneMarkingOverlay => PrivateLaneMarkingOverlay != null;
        public 행정동디오라마비공개수평등고선추적OverlaySet
            PrivateHorizontalContourTraceOverlay { get; }
        public bool HasPrivateHorizontalContourTraceOverlay =>
            PrivateHorizontalContourTraceOverlay != null;
        public 행정동디오라마깊이Plan DepthPlan { get; }
        public bool ObservationPresentationOnly => true;
        public bool DistributionApproved => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;

        public bool TryGetBuilding(string buildingStableId,
            out 행정동디오라마검토Building building)
            => buildingsById.TryGetValue(buildingStableId ?? string.Empty, out building);

        public bool TrySelectBuildingAt(double localX, double localZ,
            out 행정동디오라마검토Building building)
        {
            building = null;
            var bestArea = double.PositiveInfinity;
            foreach (var candidate in Buildings)
            {
                if (!candidate.Contains(localX, localZ)) continue;
                if (candidate.FootprintArea < bestArea - 행정동디오라마검토Projection.Epsilon
                    || (Math.Abs(candidate.FootprintArea - bestArea)
                        <= 행정동디오라마검토Projection.Epsilon
                        && (building == null || string.CompareOrdinal(
                            candidate.BuildingStableId, building.BuildingStableId) < 0)))
                {
                    building = candidate;
                    bestArea = candidate.FootprintArea;
                }
            }
            return building != null;
        }
    }

    public static class 행정동디오라마깊이StateCodes
    {
        public const string PrivateReview = "PrivateReview";
        public const string Blocked = "Blocked";
        public const string NotProvided = "NotProvided";
    }

    /// <summary>행정동별 관찰 기능의 자리와 결손을 나타내며 실제 실행 증거를 만들지 않는다.</summary>
    public sealed class 행정동디오라마깊이Layer
    {
        internal 행정동디오라마깊이Layer(string key, string kind, int order,
            bool required, string state, string diagnostic, string sourceRevision,
            params string[] dependencies)
        {
            LayerStableId = StableId(key);
            LayerKindCode = kind;
            AssemblyOrder = order;
            RequiredForObservation = required;
            StateCode = state;
            DiagnosticCode = diagnostic;
            SourceRevision = sourceRevision;
            DependencyLayerStableIds = Array.AsReadOnly(dependencies.Select(StableId).ToArray());
        }

        public string LayerStableId { get; }
        public string LayerKindCode { get; }
        public int AssemblyOrder { get; }
        public bool RequiredForObservation { get; }
        public string StateCode { get; }
        public string DiagnosticCode { get; }
        public string SourceRevision { get; }
        public IReadOnlyList<string> DependencyLayerStableIds { get; }
        public bool AvailableForPrivateReview =>
            StateCode == 행정동디오라마깊이StateCodes.PrivateReview;

        private static string StableId(string key)
            => "administrative-dong-diorama-layer:" + key + ".v1";
    }

    /// <summary>
    /// 수용된 geometry-only 사본에 같은 8개 기능 자리를 부여한다. PrivateReview는
    /// 사본과 도형 선택 자료의 준비 상태이며 원천 재검증·Mesh 실행·입력 검증·E 승격이 아니다.
    /// 역세권 Profile, 운영 상태와 자료 적용 권한을 변경하지 않는다.
    /// </summary>
    public sealed class 행정동디오라마깊이Plan
    {
        public const string Schema = "administrative-dong-diorama-depth-plan.v1";
        public const string GeometryOnlyRevision = "administrative-dong-diorama-depth:geometry-only.r1";

        private 행정동디오라마깊이Plan(행정동디오라마검토Snapshot snapshot)
        {
            AdministrativeAreaStableId = snapshot.AdministrativeAreaStableId;
            PlanStableId = "administrative-dong-diorama-depth:" + AdministrativeAreaStableId + ".v1";
            ProjectionHashSha256 = snapshot.ProjectionHashSha256;
            SourceVintage = snapshot.SourceVintage;
            ReadinessCode = snapshot.ReadinessCode;
            var available = 행정동디오라마깊이StateCodes.PrivateReview;
            var blocked = 행정동디오라마깊이StateCodes.Blocked;
            var missing = 행정동디오라마깊이StateCodes.NotProvided;
            Layers = Array.AsReadOnly(new[]
            {
                new 행정동디오라마깊이Layer("data-evidence", "DataEvidence", 10, true,
                    available, "GeometrySnapshotOnly;RawSourceRevalidationNotPerformed",
                    ProjectionHashSha256),
                new 행정동디오라마깊이Layer("base-spatial-presentation", "BaseSpatialPresentation", 20, true,
                    available, "GeometrySnapshotPrepared;RuntimePresentationNotVerified",
                    ProjectionHashSha256, "data-evidence"),
                new 행정동디오라마깊이Layer("spatial-meaning", "SpatialMeaning", 30, false,
                    blocked, "GeometryDoesNotEstablishAddressEntranceOrRole", string.Empty,
                    "data-evidence"),
                new 행정동디오라마깊이Layer("mobility", "Mobility", 40, false,
                    blocked, "CenterlinesDoNotEstablishTraversal", string.Empty,
                    "data-evidence"),
                // 도형 선택은 건물의 업무 역할·출입구가 미확인이어도 독립적으로 검토할 수 있다.
                new 행정동디오라마깊이Layer("interaction", "Interaction", 50, false,
                    available, "GeometrySelectionPrepared;RuntimeInputNotVerified",
                    ProjectionHashSha256, "base-spatial-presentation"),
                new 행정동디오라마깊이Layer("life-simulation", "LifeSimulation", 60, false,
                    missing, "LifeSimulationSourceNotProvided", string.Empty, "mobility"),
                new 행정동디오라마깊이Layer("business-overlay", "BusinessOverlay", 70, false,
                    missing, "BusinessOverlayExcludedByGeometryOnlyReview", string.Empty,
                    "data-evidence"),
                new 행정동디오라마깊이Layer("environment-presentation", "EnvironmentPresentation", 80, false,
                    missing, "EnvironmentSourceNotProvided", string.Empty,
                    "base-spatial-presentation")
            });
            PlanHashSha256 = ComputeHash(snapshot);
        }

        public string SchemaVersion => Schema;
        public string Revision => GeometryOnlyRevision;
        public string PlanStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string ProjectionHashSha256 { get; }
        public string SourceVintage { get; }
        public string ReadinessCode { get; }
        public IReadOnlyList<행정동디오라마깊이Layer> Layers { get; }
        public string PlanHashSha256 { get; }
        public bool ObservationPresentationOnly => true;
        public bool ChangesOperationalState => false;
        public bool DistributionApproved => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;
        public bool RuntimeVerified => false;

        internal static 행정동디오라마깊이Plan Create(행정동디오라마검토Snapshot snapshot)
            => new(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

        private string ComputeHash(행정동디오라마검토Snapshot snapshot)
        {
            // 길이 접두어와 고정 문화권을 사용해 구분 문자가 포함된 판본도 모호하지 않게 결속한다.
            var canonical = new StringBuilder();
            void Add(string value)
            {
                value ??= string.Empty;
                canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(value);
            }
            void AddNumber(int value) => Add(value.ToString(CultureInfo.InvariantCulture));
            Add(SchemaVersion);
            Add(Revision);
            Add(PlanStableId);
            Add(AdministrativeAreaStableId);
            Add(ProjectionHashSha256);
            Add(SourceVintage);
            Add(ReadinessCode);
            Add("ObservationPresentationOnly;NoOperationalState;NoDistribution;NoTraversal;NoGameplay;NoRuntimeVerification");
            AddNumber(snapshot.TileCount);
            AddNumber(snapshot.Buildings.Count);
            AddNumber(snapshot.Roads.Count);
            AddNumber(Layers.Count);
            foreach (var layer in Layers)
            {
                Add(layer.LayerStableId);
                Add(layer.LayerKindCode);
                AddNumber(layer.AssemblyOrder);
                Add(layer.RequiredForObservation ? "required" : "optional");
                Add(layer.StateCode);
                Add(layer.DiagnosticCode);
                Add(layer.SourceRevision);
                AddNumber(layer.DependencyLayerStableIds.Count);
                foreach (var dependency in layer.DependencyLayerStableIds) Add(dependency);
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                .Replace("-", string.Empty);
        }
    }

    public sealed class 행정동디오라마검토Point
    {
        internal 행정동디오라마검토Point(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }
    }

    public sealed class 행정동디오라마검토Bounds
    {
        internal 행정동디오라마검토Bounds(double minX, double minZ, double maxX, double maxZ)
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
        public double Width => MaxX - MinX;
        public double Depth => MaxZ - MinZ;
    }

    public sealed class 행정동디오라마검토Building
    {
        internal 행정동디오라마검토Building(string buildingStableId,
            string categoryCode, string evidenceKindCode, double displayedHeight,
            string displayedHeightKind, IReadOnlyList<행정동디오라마검토Point> footprint)
        {
            BuildingStableId = buildingStableId;
            CategoryCode = categoryCode;
            EvidenceKindCode = evidenceKindCode;
            DisplayedHeight = displayedHeight;
            DisplayedHeightKind = displayedHeightKind;
            Footprint = footprint;

            var minX = double.PositiveInfinity;
            var minZ = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxZ = double.NegativeInfinity;
            var signedArea = 0d;
            var centroidX = 0d;
            var centroidZ = 0d;
            for (var index = 0; index < footprint.Count; index++)
            {
                var point = footprint[index];
                var next = footprint[(index + 1) % footprint.Count];
                minX = Math.Min(minX, point.X);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxZ = Math.Max(maxZ, point.Z);
                var cross = point.X * next.Z - next.X * point.Z;
                signedArea += cross;
                centroidX += (point.X + next.X) * cross;
                centroidZ += (point.Z + next.Z) * cross;
            }

            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            FootprintArea = Math.Abs(signedArea) * .5d;
            if (Math.Abs(signedArea) > 행정동디오라마검토Projection.Epsilon)
            {
                CentroidX = centroidX / (3d * signedArea);
                CentroidZ = centroidZ / (3d * signedArea);
            }
            else
            {
                CentroidX = (minX + maxX) * .5d;
                CentroidZ = (minZ + maxZ) * .5d;
            }
        }

        public string BuildingStableId { get; }
        public string CategoryCode { get; }
        public string EvidenceKindCode { get; }
        public double DisplayedHeight { get; }
        public string DisplayedHeightKind { get; }
        public IReadOnlyList<행정동디오라마검토Point> Footprint { get; }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double CentroidX { get; }
        public double CentroidZ { get; }
        public double FootprintArea { get; }

        internal bool Contains(double x, double z)
        {
            if (x < MinX - 행정동디오라마검토Projection.Epsilon
                || x > MaxX + 행정동디오라마검토Projection.Epsilon
                || z < MinZ - 행정동디오라마검토Projection.Epsilon
                || z > MaxZ + 행정동디오라마검토Projection.Epsilon) return false;

            var inside = false;
            for (int current = 0, previous = Footprint.Count - 1;
                 current < Footprint.Count; previous = current++)
            {
                var a = Footprint[previous];
                var b = Footprint[current];
                if (OnSegment(a, b, x, z)) return true;
                if ((a.Z > z) == (b.Z > z)) continue;
                var intersectX = (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X;
                if (x < intersectX) inside = !inside;
            }
            return inside;
        }

        private static bool OnSegment(행정동디오라마검토Point a,
            행정동디오라마검토Point b, double x, double z)
        {
            var cross = (b.X - a.X) * (z - a.Z) - (b.Z - a.Z) * (x - a.X);
            if (Math.Abs(cross) > 행정동디오라마검토Projection.Epsilon) return false;
            return x >= Math.Min(a.X, b.X) - 행정동디오라마검토Projection.Epsilon
                   && x <= Math.Max(a.X, b.X) + 행정동디오라마검토Projection.Epsilon
                   && z >= Math.Min(a.Z, b.Z) - 행정동디오라마검토Projection.Epsilon
                   && z <= Math.Max(a.Z, b.Z) + 행정동디오라마검토Projection.Epsilon;
        }
    }

    public sealed class 행정동디오라마검토RoadSegment
    {
        internal 행정동디오라마검토RoadSegment(string roadStableId,
            string evidenceKindCode, 행정동디오라마검토Point from,
            행정동디오라마검토Point to)
        {
            RoadStableId = roadStableId;
            EvidenceKindCode = evidenceKindCode;
            From = from;
            To = to;
        }

        public string RoadStableId { get; }
        public string EvidenceKindCode { get; }
        public 행정동디오라마검토Point From { get; }
        public 행정동디오라마검토Point To { get; }
    }

    public static class 행정동디오라마검토Projection
    {
        internal const double Epsilon = .000001d;
        public const string AcceptedDiagnostic = "AdministrativeDongGeometryReviewAccepted";
        public const string PrivateObservationOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateObservationOverlayReviewAccepted";
        public const string PrivateWalkNetworkOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateWalkNetworkOverlayReviewAccepted";
        public const string PrivateRoadDirectionOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateRoadDirectionOverlayReviewAccepted";
        public const string PrivateBiotopeOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateBiotopeOverlayReviewAccepted";
        public const string PrivateLaneMarkingOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateLaneMarkingOverlayReviewAccepted";
        public const string PrivateHorizontalContourTraceOverlayAcceptedDiagnostic =
            "AdministrativeDongPrivateHorizontalContourTraceOverlayReviewAccepted";

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            if (source == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongReviewApplyResultMissing";
                return false;
            }
            try
            {
                snapshot = Create(source);
                diagnosticCode = AcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            if (source == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongReviewApplyResultMissing";
                return false;
            }
            if (privateObservationOverlay == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongPrivateObservationOverlayMissing";
                return false;
            }
            try
            {
                snapshot = Create(source, privateObservationOverlay);
                diagnosticCode = PrivateObservationOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay,
            행정동디오라마비공개보행망OverlaySetData privateWalkNetworkOverlay,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            if (source == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongReviewApplyResultMissing";
                return false;
            }
            if (privateObservationOverlay == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongPrivateObservationOverlayMissing";
                return false;
            }
            if (privateWalkNetworkOverlay == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongPrivateWalkNetworkOverlayMissing";
                return false;
            }
            try
            {
                snapshot = Create(source, privateObservationOverlay,
                    privateWalkNetworkOverlay);
                diagnosticCode = PrivateWalkNetworkOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay,
            행정동디오라마비공개보행망OverlaySetData privateWalkNetworkOverlay,
            행정동디오라마비공개도로방향OverlaySetData privateRoadDirectionOverlay,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            if (source == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongReviewApplyResultMissing";
                return false;
            }
            if (privateObservationOverlay == null || privateWalkNetworkOverlay == null
                || privateRoadDirectionOverlay == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongPrivateRoadDirectionOverlayMissing";
                return false;
            }
            try
            {
                snapshot = Create(source, privateObservationOverlay,
                    privateWalkNetworkOverlay, privateRoadDirectionOverlay);
                diagnosticCode = PrivateRoadDirectionOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay,
            행정동디오라마비공개보행망OverlaySetData privateWalkNetworkOverlay,
            행정동디오라마비공개도로방향OverlaySetData privateRoadDirectionOverlay)
        {
            if (privateObservationOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayMissing");
            if (privateWalkNetworkOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayMissing");
            if (privateRoadDirectionOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayMissing");
            var geometry = Create(source);
            var observation = CreatePrivateObservationOverlay(privateObservationOverlay,
                geometry);
            var walkNetwork = CreatePrivateWalkNetworkOverlay(privateWalkNetworkOverlay,
                geometry, observation);
            var roadDirection = CreatePrivateRoadDirectionOverlay(
                privateRoadDirectionOverlay, geometry);
            return new 행정동디오라마검토Snapshot(
                geometry.AdministrativeAreaStableId, geometry.DisplayName,
                geometry.SourceVintage, geometry.ProjectionHashSha256,
                geometry.ReadinessCode, geometry.SourceCenterX, geometry.SourceCenterZ,
                geometry.MetersPerUnit, geometry.Bounds, geometry.Boundary,
                geometry.Buildings, geometry.Roads, geometry.TileCount,
                observation, walkNetwork, roadDirection);
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            try
            {
                if (source == null || observation == null || walk == null || direction == null || biotope == null)
                    throw new InvalidOperationException("AdministrativeDongPrivateBiotopeOverlayMissing");
                snapshot = Create(source, observation, walk, direction, biotope);
                diagnosticCode = PrivateBiotopeOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null; diagnosticCode = exception.Message; return false;
            }
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope)
        {
            var geometry = Create(source);
            var observationSet = CreatePrivateObservationOverlay(observation, geometry);
            var walkSet = CreatePrivateWalkNetworkOverlay(walk, geometry, observationSet);
            var directionSet = CreatePrivateRoadDirectionOverlay(direction, geometry);
            var biotopeSet = CreatePrivateBiotopeOverlay(biotope, geometry);
            return new 행정동디오라마검토Snapshot(geometry.AdministrativeAreaStableId,
                geometry.DisplayName, geometry.SourceVintage, geometry.ProjectionHashSha256,
                geometry.ReadinessCode, geometry.SourceCenterX, geometry.SourceCenterZ,
                geometry.MetersPerUnit, geometry.Bounds, geometry.Boundary, geometry.Buildings,
                geometry.Roads, geometry.TileCount, observationSet, walkSet, directionSet, biotopeSet);
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope,
            행정동디오라마비공개차선표시OverlaySetData laneMarking,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            try
            {
                if (source == null || observation == null || walk == null || direction == null
                    || biotope == null || laneMarking == null)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateLaneMarkingOverlayMissing");
                snapshot = Create(source, observation, walk, direction, biotope, laneMarking);
                diagnosticCode = PrivateLaneMarkingOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope,
            행정동디오라마비공개차선표시OverlaySetData laneMarking)
        {
            var geometry = Create(source);
            var observationSet = CreatePrivateObservationOverlay(observation, geometry);
            var walkSet = CreatePrivateWalkNetworkOverlay(walk, geometry, observationSet);
            var directionSet = CreatePrivateRoadDirectionOverlay(direction, geometry);
            var biotopeSet = CreatePrivateBiotopeOverlay(biotope, geometry);
            var laneMarkingSet = CreatePrivateLaneMarkingOverlay(laneMarking, geometry);
            return new 행정동디오라마검토Snapshot(geometry.AdministrativeAreaStableId,
                geometry.DisplayName, geometry.SourceVintage, geometry.ProjectionHashSha256,
                geometry.ReadinessCode, geometry.SourceCenterX, geometry.SourceCenterZ,
                geometry.MetersPerUnit, geometry.Bounds, geometry.Boundary, geometry.Buildings,
                geometry.Roads, geometry.TileCount, observationSet, walkSet, directionSet,
                biotopeSet, laneMarkingSet);
        }

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope,
            행정동디오라마비공개차선표시OverlaySetData laneMarking,
            행정동디오라마비공개수평등고선추적OverlaySetData horizontalContourTrace,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            try
            {
                if (source == null || observation == null || walk == null || direction == null
                    || biotope == null || laneMarking == null || horizontalContourTrace == null)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateHorizontalContourTraceOverlayMissing");
                snapshot = Create(source, observation, walk, direction, biotope, laneMarking,
                    horizontalContourTrace);
                diagnosticCode = PrivateHorizontalContourTraceOverlayAcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData observation,
            행정동디오라마비공개보행망OverlaySetData walk,
            행정동디오라마비공개도로방향OverlaySetData direction,
            행정동디오라마비공개비오톱OverlaySetData biotope,
            행정동디오라마비공개차선표시OverlaySetData laneMarking,
            행정동디오라마비공개수평등고선추적OverlaySetData horizontalContourTrace)
        {
            var geometry = Create(source);
            var observationSet = CreatePrivateObservationOverlay(observation, geometry);
            var walkSet = CreatePrivateWalkNetworkOverlay(walk, geometry, observationSet);
            var directionSet = CreatePrivateRoadDirectionOverlay(direction, geometry);
            var biotopeSet = CreatePrivateBiotopeOverlay(biotope, geometry);
            var laneMarkingSet = CreatePrivateLaneMarkingOverlay(laneMarking, geometry);
            var contourTraceSet = CreatePrivateHorizontalContourTraceOverlay(
                horizontalContourTrace, geometry);
            return new 행정동디오라마검토Snapshot(geometry.AdministrativeAreaStableId,
                geometry.DisplayName, geometry.SourceVintage, geometry.ProjectionHashSha256,
                geometry.ReadinessCode, geometry.SourceCenterX, geometry.SourceCenterZ,
                geometry.MetersPerUnit, geometry.Bounds, geometry.Boundary, geometry.Buildings,
                geometry.Roads, geometry.TileCount, observationSet, walkSet, directionSet,
                biotopeSet, laneMarkingSet, contourTraceSet);
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay,
            행정동디오라마비공개보행망OverlaySetData privateWalkNetworkOverlay)
        {
            if (privateObservationOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayMissing");
            if (privateWalkNetworkOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayMissing");
            var geometry = Create(source);
            var observation = CreatePrivateObservationOverlay(privateObservationOverlay,
                geometry);
            var walkNetwork = CreatePrivateWalkNetworkOverlay(privateWalkNetworkOverlay,
                geometry, observation);
            return new 행정동디오라마검토Snapshot(
                geometry.AdministrativeAreaStableId,
                geometry.DisplayName,
                geometry.SourceVintage,
                geometry.ProjectionHashSha256,
                geometry.ReadinessCode,
                geometry.SourceCenterX,
                geometry.SourceCenterZ,
                geometry.MetersPerUnit,
                geometry.Bounds,
                geometry.Boundary,
                geometry.Buildings,
                geometry.Roads,
                geometry.TileCount,
                observation,
                walkNetwork);
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source,
            행정동디오라마비공개관찰OverlaySetData privateObservationOverlay)
        {
            if (privateObservationOverlay == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayMissing");
            var geometry = Create(source);
            var overlay = CreatePrivateObservationOverlay(privateObservationOverlay, geometry);
            return new 행정동디오라마검토Snapshot(
                geometry.AdministrativeAreaStableId,
                geometry.DisplayName,
                geometry.SourceVintage,
                geometry.ProjectionHashSha256,
                geometry.ReadinessCode,
                geometry.SourceCenterX,
                geometry.SourceCenterZ,
                geometry.MetersPerUnit,
                geometry.Bounds,
                geometry.Boundary,
                geometry.Buildings,
                geometry.Roads,
                geometry.TileCount,
                overlay);
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source)
        {
            if (source == null)
                throw new InvalidOperationException("AdministrativeDongReviewApplyResultMissing");
            if (!source.Accepted)
                throw new InvalidOperationException("AdministrativeDongReviewApplyResultRejected");

            var manifest = source.Manifest
                           ?? throw new InvalidOperationException("AdministrativeDongReviewManifestMissing");
            if (!manifest.ObservationPresentationOnly
                || !string.Equals(manifest.DataPolicyCode,
                    AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("AdministrativeDongReviewAuthorityRejected");
            if (manifest.DistributionApproved)
                throw new InvalidOperationException("AdministrativeDongReviewDistributionApprovedRejected");
            if (manifest.TraversalReady)
                throw new InvalidOperationException("AdministrativeDongReviewTraversalReadyRejected");
            if (manifest.GameplayReady)
                throw new InvalidOperationException("AdministrativeDongReviewGameplayReadyRejected");
            if (!ReviewReadiness(manifest.ReadinessCode))
                throw new InvalidOperationException("AdministrativeDongReviewReadinessRejected");
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    manifest.AdministrativeAreaStableId))
                throw new InvalidOperationException("AdministrativeDongReviewStableIdInvalid");
            if (!string.Equals(manifest.SchemaVersion,
                    AdministrativeDongDioramaPolicy.SchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("AdministrativeDongReviewSchemaVersionInvalid");
            if (!Sha256(manifest.ProjectionHashSha256))
                throw new InvalidOperationException("AdministrativeDongReviewProjectionHashMissing");
            if ((source.DisplayOverlays ?? Array.Empty<AdministrativeDongDisplayOverlay>()).Length != 0)
                throw new InvalidOperationException("AdministrativeDongReviewGeometryOnlyOverlayRejected");

            var frame = manifest.CoordinateFrame
                        ?? throw new InvalidOperationException("AdministrativeDongReviewCoordinateFrameMissing");
            if (string.IsNullOrWhiteSpace(frame.Method) || !Finite(frame.OriginLatitude)
                || !Finite(frame.OriginLongitude) || !Finite(frame.WorldOffsetX)
                || !Finite(frame.WorldOffsetZ) || !Finite(frame.MetersPerUnit)
                || Math.Abs(frame.MetersPerUnit - 1d) > Epsilon)
                throw new InvalidOperationException("AdministrativeDongReviewCoordinateFrameInvalid");
            var sourceBounds = manifest.Bounds
                               ?? throw new InvalidOperationException("AdministrativeDongReviewBoundsMissing");
            if (!Finite(sourceBounds.MinX) || !Finite(sourceBounds.MinZ)
                || !Finite(sourceBounds.MaxX) || !Finite(sourceBounds.MaxZ)
                || sourceBounds.MaxX - sourceBounds.MinX <= Epsilon
                || sourceBounds.MaxZ - sourceBounds.MinZ <= Epsilon)
                throw new InvalidOperationException("AdministrativeDongReviewBoundsInvalid");

            var centerX = (sourceBounds.MinX + sourceBounds.MaxX) * .5d;
            var centerZ = (sourceBounds.MinZ + sourceBounds.MaxZ) * .5d;
            var scale = frame.MetersPerUnit;
            행정동디오라마검토Point Point(AdministrativeDongDioramaPoint value)
            {
                if (value == null || !Finite(value.X) || !Finite(value.Z))
                    throw new InvalidOperationException("AdministrativeDongReviewPointInvalid");
                return new 행정동디오라마검토Point(
                    (value.X - centerX) / scale, (value.Z - centerZ) / scale);
            }

            var boundary = NormalizeFootprint(manifest.Boundary, Point,
                "AdministrativeDongReviewBoundaryInvalid");
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var buildings = new List<행정동디오라마검토Building>();
            var roads = new List<행정동디오라마검토RoadSegment>();
            var tiles = source.Tiles ?? Array.Empty<AdministrativeDongDioramaTile>();
            var summaries = manifest.Tiles ?? Array.Empty<AdministrativeDongDioramaTileSummary>();
            if (tiles.Length != summaries.Length
                || summaries.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.TileStableId))
                || summaries.GroupBy(value => value.TileStableId, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
                throw new InvalidOperationException("AdministrativeDongReviewTileSetInvalid");
            var summaryMap = summaries.ToDictionary(value => value.TileStableId,
                StringComparer.Ordinal);
            var tileIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tile in tiles.OrderBy(value => value.TileStableId, StringComparer.Ordinal))
            {
                if (tile == null
                    || string.IsNullOrWhiteSpace(tile.TileStableId)
                    || !tileIds.Add(tile.TileStableId)
                    || !string.Equals(tile.SchemaVersion, AdministrativeDongDioramaPolicy.SchemaVersion,
                        StringComparison.Ordinal)
                    || !string.Equals(tile.AdministrativeAreaStableId,
                        manifest.AdministrativeAreaStableId, StringComparison.Ordinal)
                    || !string.Equals(tile.ProjectionHashSha256,
                        manifest.ProjectionHashSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("AdministrativeDongReviewTileVersionInvalid");

                var tileBuildings = tile.Buildings ?? Array.Empty<AdministrativeDongDioramaBuilding>();
                var tileRoads = tile.Roads ?? Array.Empty<AdministrativeDongDioramaRoadSegment>();
                if (!summaryMap.TryGetValue(tile.TileStableId, out var summary)
                    || !Sha256(tile.TileHashSha256)
                    || !string.Equals(tile.TileHashSha256, summary.TileHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || summary.BuildingCount != tileBuildings.Length
                    || summary.RoadSegmentCount != tileRoads.Length)
                    throw new InvalidOperationException("AdministrativeDongReviewTileHashOrCountInvalid");

                foreach (var value in tileBuildings
                             .OrderBy(item => item.BuildingStableId, StringComparer.Ordinal))
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.BuildingStableId)
                        || !buildingIds.Add(value.BuildingStableId))
                        throw new InvalidOperationException("AdministrativeDongReviewBuildingIdentityInvalid");
                    var footprint = NormalizeFootprint(value.Footprint, Point,
                        "AdministrativeDongReviewBuildingGeometryInvalid");
                    var height = ResolveHeight(value, scale, out var heightKind);
                    buildings.Add(new 행정동디오라마검토Building(value.BuildingStableId,
                        value.CategoryCode ?? string.Empty, value.EvidenceKindCode ?? string.Empty,
                        height, heightKind, footprint));
                }

                foreach (var value in tileRoads
                             .OrderBy(item => item.RoadStableId, StringComparer.Ordinal)
                             .ThenBy(item => item.From?.X ?? double.MinValue)
                             .ThenBy(item => item.From?.Z ?? double.MinValue))
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.RoadStableId))
                        throw new InvalidOperationException("AdministrativeDongReviewRoadIdentityInvalid");
                    var from = Point(value.From);
                    var to = Point(value.To);
                    var dx = to.X - from.X;
                    var dz = to.Z - from.Z;
                    if (dx * dx + dz * dz <= Epsilon * Epsilon)
                        throw new InvalidOperationException("AdministrativeDongReviewRoadGeometryInvalid");
                    roads.Add(new 행정동디오라마검토RoadSegment(value.RoadStableId,
                        value.EvidenceKindCode ?? string.Empty, from, to));
                }
            }

            buildings.Sort((left, right) => string.CompareOrdinal(
                left.BuildingStableId, right.BuildingStableId));
            var localBounds = new 행정동디오라마검토Bounds(
                (sourceBounds.MinX - centerX) / scale,
                (sourceBounds.MinZ - centerZ) / scale,
                (sourceBounds.MaxX - centerX) / scale,
                (sourceBounds.MaxZ - centerZ) / scale);
            return new 행정동디오라마검토Snapshot(
                manifest.AdministrativeAreaStableId,
                manifest.DisplayName ?? string.Empty,
                manifest.SourceVintage ?? string.Empty,
                manifest.ProjectionHashSha256,
                manifest.ReadinessCode,
                centerX,
                centerZ,
                scale,
                localBounds,
                Array.AsReadOnly(boundary),
                Array.AsReadOnly(buildings.ToArray()),
                Array.AsReadOnly(roads.ToArray()),
                tiles.Length);
        }

        private static 행정동디오라마비공개관찰OverlaySet
            CreatePrivateObservationOverlay(
                행정동디오라마비공개관찰OverlaySetData source,
                행정동디오라마검토Snapshot geometry)
        {
            if (!string.Equals(source.schemaVersion,
                    행정동디오라마비공개관찰Policy.SchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlaySchemaVersionInvalid");
            if (string.IsNullOrWhiteSpace(source.revision)
                || string.IsNullOrWhiteSpace(source.overlaySetStableId)
                || string.IsNullOrWhiteSpace(source.sourceVintage))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayIdentityInvalid");
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    source.administrativeAreaStableId)
                || !string.Equals(source.administrativeAreaStableId,
                    geometry.AdministrativeAreaStableId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayAdministrativeAreaMismatch");
            if (!Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlaySetHashInvalid");
            if (!source.historicalBoundaryBootstrapOnly
                || !source.privateReviewVisualizationAllowed
                || !source.reviewVisualizationOnly
                || source.sourceCandidateUnityApplyAllowed
                || source.publicDisplayAllowed
                || source.runtimeAuthorized
                || source.traversalReady
                || source.gameplayReady
                || source.currentPointerUsed
                || source.currentPointerUpdated
                || source.exactAddressIncluded
                || source.businessIdentityIncluded
                || source.personalDataIncluded)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayAuthorityRejected");
            if (source.sourceReceipts == null || source.crosswalkPoints == null
                || source.intersectionPoints == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayCollectionsInvalid");

            var pointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var crosswalks = new List<행정동디오라마검토CrosswalkPoint>(
                source.crosswalkPoints.Length);
            foreach (var value in source.crosswalkPoints.OrderBy(
                         item => item?.candidateStableId, StringComparer.Ordinal))
            {
                ValidateCandidate(value?.candidateStableId, value?.qualityCode, pointIds);
                var point = LocalPoint(value.commonEnuMillimeters, geometry);
                crosswalks.Add(new 행정동디오라마검토CrosswalkPoint(
                    value.candidateStableId, point.X, point.Z,
                    value.pedestrianSignalPresent,
                    value.sourceDistrictSpatialAssignmentConflict,
                    value.qualityCode));
            }

            var intersections = new List<행정동디오라마검토IntersectionPoint>(
                source.intersectionPoints.Length);
            foreach (var value in source.intersectionPoints.OrderBy(
                         item => item?.candidateStableId, StringComparer.Ordinal))
            {
                ValidateCandidate(value?.candidateStableId, value?.qualityCode, pointIds);
                if (value.linkedCrosswalkCount < 0
                    || value.otherHistoricalAdministrativeAreaLinkCount < 0)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateObservationOverlayLinkedCountInvalid");
                var point = LocalPoint(value.commonEnuMillimeters, geometry);
                intersections.Add(new 행정동디오라마검토IntersectionPoint(
                    value.candidateStableId, point.X, point.Z,
                    value.linkedCrosswalkCount,
                    value.otherHistoricalAdministrativeAreaLinkCount,
                    value.sourceDistrictSpatialAssignmentConflict,
                    value.qualityCode));
            }

            return new 행정동디오라마비공개관찰OverlaySet(source,
                Array.AsReadOnly(crosswalks.ToArray()),
                Array.AsReadOnly(intersections.ToArray()));
        }

        private static 행정동디오라마비공개보행망OverlaySet
            CreatePrivateWalkNetworkOverlay(
                행정동디오라마비공개보행망OverlaySetData source,
                행정동디오라마검토Snapshot geometry,
                행정동디오라마비공개관찰OverlaySet observation)
        {
            if (!string.Equals(source.schemaVersion,
                    행정동디오라마비공개보행망Policy.SchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlaySchemaVersionInvalid");
            if (string.IsNullOrWhiteSpace(source.revision)
                || string.IsNullOrWhiteSpace(source.overlaySetStableId)
                || string.IsNullOrWhiteSpace(source.sourceVintage))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayIdentityInvalid");
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    source.administrativeAreaStableId)
                || !string.Equals(source.administrativeAreaStableId,
                    geometry.AdministrativeAreaStableId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayAdministrativeAreaMismatch");
            if (!string.Equals(source.overlaySetStableId,
                    "administrative-dong-private-walk-network:"
                    + source.administrativeAreaStableId + ".v1", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayIdentityInvalid");
            if (!Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlaySetHashInvalid");
            if (!source.historicalBoundaryBootstrapOnly
                || !source.privateReviewOnly
                || !source.privateReviewVisualizationAllowed
                || !source.reviewVisualizationOnly
                || !source.observationCandidateOnly
                || source.sourceCandidateUnityApplyAllowed
                || source.publicDisplayAllowed
                || source.runtimeAuthorized
                || source.traversalReady
                || source.gameplayReady
                || source.currentPointerUsed
                || source.currentPointerUpdated
                || source.sourceIdentifiersIncluded
                || source.sourceBoroughIdentityIncluded
                || source.legalAreaIdentityIncluded
                || source.sourceTimestampsIncluded
                || source.sourceEpsg5186CoordinatesIncluded
                || source.exactAddressIncluded
                || source.personalDataIncluded)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayAuthorityRejected");
            if (source.sourceReceipts == null || source.sourceReceipts.Length == 0
                || source.nodePoints == null || source.linkFragments == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayCollectionsInvalid");
            if (source.sourceReceipts.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.datasetId)
                || string.IsNullOrWhiteSpace(value.officialPageUrl)
                || string.IsNullOrWhiteSpace(value.sourceReferenceVintage)
                || !Sha256(value.candidateSetHashSha256)
                || !Sha256(value.acquisitionReceiptSha256)
                || string.IsNullOrWhiteSpace(value.licenseCode)
                || string.IsNullOrWhiteSpace(value.sourceCoordinateReference)
                || string.IsNullOrWhiteSpace(value.conversionCoordinateReference)
                || string.IsNullOrWhiteSpace(value.coordinateReferenceStatus)))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayReceiptInvalid");

            var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (observation != null)
            {
                foreach (var value in observation.CrosswalkPoints)
                    candidateIds.Add(value.CandidateStableId);
                foreach (var value in observation.IntersectionPoints)
                    candidateIds.Add(value.CandidateStableId);
            }
            var nodes = new List<행정동디오라마검토WalkNodePoint>(source.nodePoints.Length);
            foreach (var value in source.nodePoints.OrderBy(
                         item => item?.candidateStableId, StringComparer.Ordinal))
            {
                ValidateWalkCandidate(value?.candidateStableId, value?.kindCode,
                    "Node", value?.qualityCode, value?.actorSummaryFlags, candidateIds);
                if (value.actorSummaryFlags.sourceReported
                    || value.actorSummaryFlags.pedestrian
                    || value.actorSummaryFlags.bicycle
                    || value.actorSummaryFlags.personalMobility
                    || value.actorSummaryFlags.vehicle)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateWalkNetworkOverlayNodeActorFlagsInvalid");
                var point = LocalWalkPoint(value.commonEnuMillimeters, geometry);
                nodes.Add(new 행정동디오라마검토WalkNodePoint(value.candidateStableId,
                    point.X, point.Z, value.kindCode, value.qualityCode,
                    new 행정동디오라마검토WalkActorSummary(value.actorSummaryFlags)));
            }

            var links = new List<행정동디오라마검토WalkLinkFragment>(
                source.linkFragments.Length);
            foreach (var value in source.linkFragments.OrderBy(
                         item => item?.candidateStableId, StringComparer.Ordinal))
            {
                ValidateWalkCandidate(value?.candidateStableId, value?.kindCode,
                    "LinkFragment", value?.qualityCode, value?.actorSummaryFlags,
                    candidateIds);
                if (value.commonEnuMillimeterPath == null
                    || value.commonEnuMillimeterPath.Length < 2)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateWalkNetworkOverlayLinkPathInvalid");
                var path = value.commonEnuMillimeterPath
                    .Select(point => LocalWalkPoint(point, geometry)).ToArray();
                links.Add(new 행정동디오라마검토WalkLinkFragment(
                    value.candidateStableId, Array.AsReadOnly(path), value.kindCode,
                    value.qualityCode,
                    new 행정동디오라마검토WalkActorSummary(value.actorSummaryFlags)));
            }

            return new 행정동디오라마비공개보행망OverlaySet(source,
                Array.AsReadOnly(nodes.ToArray()), Array.AsReadOnly(links.ToArray()));
        }

        private static 행정동디오라마비공개도로방향OverlaySet
            CreatePrivateRoadDirectionOverlay(
                행정동디오라마비공개도로방향OverlaySetData source,
                행정동디오라마검토Snapshot geometry)
        {
            if (!string.Equals(source.schemaVersion,
                    행정동디오라마비공개도로방향Policy.SchemaVersion,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlaySchemaInvalid");
            if (string.IsNullOrWhiteSpace(source.revision)
                || string.IsNullOrWhiteSpace(source.overlaySetStableId)
                || string.IsNullOrWhiteSpace(source.sourceVintage))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayIdentityInvalid");
            const string setIdPrefix = "private-road-direction-overlay:sha256:";
            if (!source.overlaySetStableId.StartsWith(setIdPrefix,
                    StringComparison.Ordinal)
                || !Sha256(source.overlaySetStableId.Substring(setIdPrefix.Length)))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayIdentityInvalid");
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    source.administrativeAreaStableId)
                || !string.Equals(source.administrativeAreaStableId,
                    geometry.AdministrativeAreaStableId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayAreaMismatch");
            if (!Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlaySetHashInvalid");
            if (!source.privateReviewVisualizationAllowed
                || !source.reviewVisualizationOnly
                || !source.historicalBoundaryBootstrapOnly
                || source.angleSemanticVerified || source.directionResolved
                || source.renderedArrowAuthorized || source.sourceLengthUnitVerified
                || source.publicDisplayAllowed || source.runtimeAuthorized
                || source.traversalReady || source.gameplayReady
                || source.currentPointerUsed || source.currentPointerUpdated
                || source.sourceCandidateUnityApplyAllowed)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            if (source.candidatePoints == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateRoadDirectionOverlayCandidatesMissing");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var points = new List<행정동디오라마검토RoadDirectionPoint>(
                source.candidatePoints.Length);
            foreach (var value in source.candidatePoints.OrderBy(
                         item => item?.candidateStableId, StringComparer.Ordinal))
            {
                var prefix = "road-direction-symbol:sha256:";
                if (value == null || string.IsNullOrWhiteSpace(value.candidateStableId)
                    || !value.candidateStableId.StartsWith(prefix, StringComparison.Ordinal)
                    || !Sha256(value.candidateStableId.Substring(prefix.Length)))
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateRoadDirectionCandidateIdInvalid");
                if (!ids.Add(value.candidateStableId))
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateRoadDirectionCandidateDuplicate");
                if (string.IsNullOrWhiteSpace(value.officialKindCode)
                    || !Finite(value.sourceDirectionNumericValue))
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateRoadDirectionCandidateValueInvalid");
                행정동디오라마검토Point point;
                try
                {
                    point = LocalPoint(value.commonEnuMillimeters, geometry);
                }
                catch (InvalidOperationException exception) when (exception.Message ==
                    "AdministrativeDongPrivateObservationOverlayPointInvalid")
                {
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateRoadDirectionCoordinateInvalid");
                }
                catch (InvalidOperationException exception) when (exception.Message ==
                    "AdministrativeDongPrivateObservationOverlayPointOutOfBounds")
                {
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateRoadDirectionCoordinateOutOfBounds");
                }
                points.Add(new 행정동디오라마검토RoadDirectionPoint(
                    value.candidateStableId, point.X, point.Z, value.officialKindCode,
                    value.sourceDirectionNumericValue));
            }
            return new 행정동디오라마비공개도로방향OverlaySet(source,
                Array.AsReadOnly(points.ToArray()));
        }

        private static 행정동디오라마비공개비오톱OverlaySet CreatePrivateBiotopeOverlay(
            행정동디오라마비공개비오톱OverlaySetData source,
            행정동디오라마검토Snapshot geometry)
        {
            if (source.schemaVersion != 행정동디오라마비공개비오톱Policy.SchemaVersion
                || source.coordinateUnit != "millimeter" || source.coordinateFrame == null
                || source.coordinateFrame.method != 행정동디오라마비공개비오톱Policy.CoordinateFrameMethod
                || source.coordinateFrame.metersPerUnit != 1d
                || source.coordinateFrame.originLatitude != 37.580912d
                || source.coordinateFrame.originLongitude != 127.088502d
                || source.coordinateFrame.worldOffsetX != 0d || source.coordinateFrame.worldOffsetZ != 0d
                || !Finite(source.coordinateFrame.originLatitude) || !Finite(source.coordinateFrame.originLongitude)
                || source.coordinateFrame.originLatitude < -90d || source.coordinateFrame.originLatitude > 90d
                || source.coordinateFrame.originLongitude < -180d || source.coordinateFrame.originLongitude > 180d
                || !Finite(source.coordinateFrame.worldOffsetX) || !Finite(source.coordinateFrame.worldOffsetZ))
                throw new InvalidOperationException("AdministrativeDongPrivateBiotopeCoordinateFrameInvalid");
            if (string.IsNullOrWhiteSpace(source.revision) || !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(source.administrativeAreaStableId)
                || source.administrativeAreaStableId != geometry.AdministrativeAreaStableId
                || !source.overlaySetStableId.StartsWith("private-biotope-outline-overlay:sha256:", StringComparison.Ordinal)
                || !Sha256(source.overlaySetStableId.Substring("private-biotope-outline-overlay:sha256:".Length))
                || !Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException("AdministrativeDongPrivateBiotopeIdentityInvalid");
            if (!source.privateReviewVisualizationAllowed || !source.biotopeOutlineAuthorized
                || source.allSourceSegmentsRendered || source.filledPolygonAuthorized || source.colliderAuthorized
                || source.legalEffectAuthorized || source.speciesObservationAuthorized || source.safetyAuthorized
                || source.passageAuthorized || source.publicDisplayAllowed || source.runtimeAuthorized
                || source.traversalReady || source.gameplayReady || source.currentPointerUsed || source.currentPointerUpdated
                || source.databaseWriteAllowed || source.mongoWriteAllowed || source.sourceCandidateUnityApplyAllowed)
                throw new InvalidOperationException("AdministrativeDongPrivateBiotopeAuthorityRejected");
            if (source.candidatePolygons == null) throw new InvalidOperationException("AdministrativeDongPrivateBiotopeCandidatesMissing");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<행정동디오라마검토BiotopeCandidate>();
            foreach (var candidate in source.candidatePolygons)
            {
                if (candidate == null || candidate.stableId == null || !candidate.stableId.StartsWith("biotope:", StringComparison.Ordinal)
                    || !Hex(candidate.stableId.Substring("biotope:".Length), 32) || !ids.Add(candidate.stableId)
                    || candidate.classification == null || string.IsNullOrWhiteSpace(candidate.classification.evaluationGrade)
                    || string.IsNullOrWhiteSpace(candidate.classification.legendLabel) || string.IsNullOrWhiteSpace(candidate.classification.typeCode)
                    || !"ABCDEFGHI".Contains(candidate.classification.typeCode.Substring(0, 1))
                    || candidate.polygons == null || candidate.polygons.Length == 0)
                    throw new InvalidOperationException("AdministrativeDongPrivateBiotopeCandidateInvalid");
                var parts = new List<행정동디오라마검토BiotopePolygon>();
                foreach (var polygon in candidate.polygons)
                {
                    var exterior = CreateBiotopeRing(polygon?.exteriorRingMm, geometry);
                    if (polygon.interiorRingsMm == null) throw new InvalidOperationException("AdministrativeDongPrivateBiotopeHoleInvalid");
                    var holes = polygon.interiorRingsMm.Select(r => CreateBiotopeRing(r, geometry)).ToArray();
                    parts.Add(new 행정동디오라마검토BiotopePolygon(exterior, Array.AsReadOnly(holes)));
                }
                candidates.Add(new 행정동디오라마검토BiotopeCandidate(candidate.stableId,
                    candidate.classification.typeCode, candidate.classification.evaluationGrade,
                    candidate.classification.legendLabel, Array.AsReadOnly(parts.ToArray())));
            }
            return new 행정동디오라마비공개비오톱OverlaySet(source, Array.AsReadOnly(candidates.ToArray()));
        }

        private static 행정동디오라마검토BiotopeRing CreateBiotopeRing(double[][] points,
            행정동디오라마검토Snapshot geometry)
        {
            if (points == null || points.Length < 4) throw new InvalidOperationException("AdministrativeDongPrivateBiotopeRingInvalid");
            var local = new List<행정동디오라마검토BiotopePoint>(points.Length);
            foreach (var point in points)
            {
                if (point == null || point.Length != 2 || !Finite(point[0]) || !Finite(point[1]))
                    throw new InvalidOperationException("AdministrativeDongPrivateBiotopeRingCoordinateInvalid");
                var x = (point[0] / 1000d - geometry.SourceCenterX) / geometry.MetersPerUnit;
                var z = (point[1] / 1000d - geometry.SourceCenterZ) / geometry.MetersPerUnit;
                const double toleranceMeters = .0005d;
                if (x < geometry.Bounds.MinX - toleranceMeters || x > geometry.Bounds.MaxX + toleranceMeters
                    || z < geometry.Bounds.MinZ - toleranceMeters || z > geometry.Bounds.MaxZ + toleranceMeters)
                    throw new InvalidOperationException("AdministrativeDongPrivateBiotopeRingOutOfBounds");
                local.Add(new 행정동디오라마검토BiotopePoint(x, z));
            }
            var first = local[0]; var last = local[local.Count - 1];
            if (first.X != last.X || first.Z != last.Z)
                throw new InvalidOperationException("AdministrativeDongPrivateBiotopeRingNotClosed");
            return new 행정동디오라마검토BiotopeRing(Array.AsReadOnly(local.ToArray()));
        }

        private static 행정동디오라마비공개차선표시OverlaySet
            CreatePrivateLaneMarkingOverlay(
                행정동디오라마비공개차선표시OverlaySetData source,
                행정동디오라마검토Snapshot geometry)
        {
            if (source.schemaVersion != 행정동디오라마비공개차선표시Policy.SchemaVersion
                || source.sourceVintage != "file-2021-08-09"
                || source.coordinateUnit != "millimeter"
                || source.coordinateFrame == null
                || source.coordinateFrame.method
                != 행정동디오라마비공개차선표시Policy.CoordinateFrameMethod
                || source.coordinateFrame.metersPerUnit != 1d
                || source.coordinateFrame.originLatitude != 37.580912d
                || source.coordinateFrame.originLongitude != 127.088502d
                || source.coordinateFrame.worldOffsetX != 0d
                || source.coordinateFrame.worldOffsetZ != 0d)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateLaneMarkingCoordinateFrameInvalid");
            if (string.IsNullOrWhiteSpace(source.revision)
                || !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    source.administrativeAreaStableId)
                || source.administrativeAreaStableId != geometry.AdministrativeAreaStableId
                || source.overlaySetStableId == null
                || !source.overlaySetStableId.StartsWith(
                    "private-lane-marking-overlay:sha256:", StringComparison.Ordinal)
                || !Sha256(source.overlaySetStableId.Substring(
                    "private-lane-marking-overlay:sha256:".Length))
                || !Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateLaneMarkingIdentityInvalid");
            if (!source.historicalBoundaryBootstrapOnly || !source.privateReviewOnly
                || !source.privateReviewVisualizationAllowed
                || !source.reviewVisualizationOnly || !source.laneOutlineAuthorized
                || source.allSourceSegmentsRendered || source.laneMeaningAuthorized
                || source.directionAuthorized || source.trafficAuthorized
                || source.stopLineAuthorized || source.signalAuthorized
                || source.colliderAuthorized || source.publicDisplayAllowed
                || source.runtimeAuthorized || source.traversalReady || source.gameplayReady
                || source.currentPointerUsed || source.currentPointerUpdated
                || source.databaseWriteAllowed || source.mongoWriteAllowed
                || source.sourceCandidateUnityApplyAllowed)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateLaneMarkingAuthorityRejected");
            if (source.candidateMarkings == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateLaneMarkingCandidatesMissing");

            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            var physicalSegments = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<행정동디오라마검토LaneMarkingCandidate>(
                source.candidateMarkings.Length);
            foreach (var candidate in source.candidateMarkings.OrderBy(
                         value => value?.candidateStableId, StringComparer.Ordinal))
            {
                const string prefix = "lane-marking:sha256:";
                if (candidate == null || candidate.candidateStableId == null
                    || !candidate.candidateStableId.StartsWith(prefix, StringComparison.Ordinal)
                    || !Sha256(candidate.candidateStableId.Substring(prefix.Length))
                    || !candidateIds.Add(candidate.candidateStableId)
                    || string.IsNullOrWhiteSpace(candidate.sourceKindCode)
                    || string.IsNullOrWhiteSpace(candidate.sourceSecondaryKindCode)
                    || string.IsNullOrWhiteSpace(candidate.sourceFormCode)
                    || candidate.fragments == null || candidate.fragments.Length == 0)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateLaneMarkingCandidateInvalid");

                var fragments = new List<행정동디오라마검토LaneMarkingFragment>(
                    candidate.fragments.Length);
                foreach (var fragment in candidate.fragments.OrderBy(
                             value => value?.fragmentOrdinal ?? -1))
                {
                    if (fragment == null || fragment.fragmentOrdinal != fragments.Count
                        || fragment.commonEnuMillimeterPath == null
                        || fragment.commonEnuMillimeterPath.Length != 2)
                        throw new InvalidOperationException(
                            "AdministrativeDongPrivateLaneMarkingFragmentInvalid");
                    var points = new List<행정동디오라마검토LaneMarkingPoint>(
                        fragment.commonEnuMillimeterPath.Length);
                    for (var pointIndex = 0;
                         pointIndex < fragment.commonEnuMillimeterPath.Length; pointIndex++)
                    {
                        var point = fragment.commonEnuMillimeterPath[pointIndex];
                        if (point == null || !Finite(point.x) || !Finite(point.z))
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateLaneMarkingCoordinateInvalid");
                        var x = (point.x / 1000d - geometry.SourceCenterX)
                                / geometry.MetersPerUnit;
                        var z = (point.z / 1000d - geometry.SourceCenterZ)
                                / geometry.MetersPerUnit;
                        const double toleranceMeters = .0005d;
                        if (x < geometry.Bounds.MinX - toleranceMeters
                            || x > geometry.Bounds.MaxX + toleranceMeters
                            || z < geometry.Bounds.MinZ - toleranceMeters
                            || z > geometry.Bounds.MaxZ + toleranceMeters)
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateLaneMarkingCoordinateOutOfBounds");
                        points.Add(new 행정동디오라마검토LaneMarkingPoint(x, z));
                        if (pointIndex == 0) continue;
                        var previous = fragment.commonEnuMillimeterPath[pointIndex - 1];
                        var dx = point.x - previous.x;
                        var dz = point.z - previous.z;
                        if (Math.Sqrt(dx * dx + dz * dz) <= 1d)
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateLaneMarkingSegmentTooShort");
                        if (!physicalSegments.Add(LaneSegmentKey(previous, point)))
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateLaneMarkingSegmentDuplicate");
                    }
                    fragments.Add(new 행정동디오라마검토LaneMarkingFragment(
                        fragment.fragmentOrdinal, Array.AsReadOnly(points.ToArray())));
                }
                candidates.Add(new 행정동디오라마검토LaneMarkingCandidate(
                    candidate.candidateStableId, candidate.sourceKindCode,
                    candidate.sourceSecondaryKindCode, candidate.sourceFormCode,
                    Array.AsReadOnly(fragments.ToArray())));
            }
            return new 행정동디오라마비공개차선표시OverlaySet(source,
                Array.AsReadOnly(candidates.ToArray()));
        }

        private static 행정동디오라마비공개수평등고선추적OverlaySet
            CreatePrivateHorizontalContourTraceOverlay(
                행정동디오라마비공개수평등고선추적OverlaySetData source,
                행정동디오라마검토Snapshot geometry)
        {
            if (source.schemaVersion != 행정동디오라마비공개수평등고선추적Policy.SchemaVersion
                || source.sourceVintage != "file-2025-03-20"
                || source.coordinateUnit != "millimeter"
                || source.coordinateFrame == null
                || source.coordinateFrame.method
                != 행정동디오라마비공개수평등고선추적Policy.CoordinateFrameMethod
                || source.coordinateFrame.metersPerUnit != 1d
                || source.coordinateFrame.originLatitude != 37.580912d
                || source.coordinateFrame.originLongitude != 127.088502d
                || source.coordinateFrame.worldOffsetX != 0d
                || source.coordinateFrame.worldOffsetZ != 0d)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateHorizontalContourTraceCoordinateFrameInvalid");
            if (string.IsNullOrWhiteSpace(source.revision)
                || !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    source.administrativeAreaStableId)
                || source.administrativeAreaStableId != geometry.AdministrativeAreaStableId
                || source.overlaySetStableId == null
                || !source.overlaySetStableId.StartsWith(
                    "private-horizontal-contour-trace-overlay:sha256:",
                    StringComparison.Ordinal)
                || !Sha256(source.overlaySetStableId.Substring(
                    "private-horizontal-contour-trace-overlay:sha256:".Length))
                || !Sha256(source.overlaySetHashSha256))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateHorizontalContourTraceIdentityInvalid");
            if (!source.privateReviewOnly || !source.privateReviewVisualizationAllowed
                || !source.reviewVisualizationOnly
                || !source.historicalBoundaryBootstrapOnly
                || !source.sourceHorizontalGeometryObserved
                || source.allSourceSegmentsRendered || source.sourceAttributeDatabaseRead
                || source.sourceElevationValuesRead
                || source.sourceElevationValuesSerialized
                || source.currentBoundaryEstablished || source.currentTerrainEstablished
                || source.contourMeaningAuthorized || source.elevationMeaningAuthorized
                || source.heightUnitVerified || source.verticalDatumVerified
                || source.terrainSurfaceAuthorized || source.terrainMeshAuthorized
                || source.colliderAuthorized || source.publicDisplayAllowed
                || source.databaseWriteAllowed || source.mongoWriteAllowed
                || source.sourceCandidateUnityApplyAllowed || source.runtimeAuthorized
                || source.traversalReady || source.gameplayReady
                || source.currentPointerUsed || source.currentPointerUpdated)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateHorizontalContourTraceAuthorityRejected");
            if (source.candidateTraces == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateHorizontalContourTraceCandidatesMissing");

            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            var physicalSegments = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<행정동디오라마검토HorizontalContourTraceCandidate>(
                source.candidateTraces.Length);
            foreach (var candidate in source.candidateTraces.OrderBy(
                         value => value?.candidateStableId, StringComparer.Ordinal))
            {
                const string prefix = "contour-trace:sha256:";
                if (candidate == null || candidate.candidateStableId == null
                    || !candidate.candidateStableId.StartsWith(prefix,
                        StringComparison.Ordinal)
                    || !Sha256(candidate.candidateStableId.Substring(prefix.Length))
                    || !candidateIds.Add(candidate.candidateStableId)
                    || candidate.fragments == null || candidate.fragments.Length == 0)
                    throw new InvalidOperationException(
                        "AdministrativeDongPrivateHorizontalContourTraceCandidateInvalid");

                var fragments = new List<행정동디오라마검토HorizontalContourTraceFragment>(
                    candidate.fragments.Length);
                foreach (var fragment in candidate.fragments.OrderBy(
                             value => value?.fragmentOrdinal ?? -1))
                {
                    if (fragment == null || fragment.fragmentOrdinal != fragments.Count
                        || fragment.commonEnuMillimeterPath == null
                        || fragment.commonEnuMillimeterPath.Length != 2)
                        throw new InvalidOperationException(
                            "AdministrativeDongPrivateHorizontalContourTraceFragmentInvalid");
                    var points = new List<행정동디오라마검토HorizontalContourTracePoint>(2);
                    for (var pointIndex = 0; pointIndex < 2; pointIndex++)
                    {
                        var point = fragment.commonEnuMillimeterPath[pointIndex];
                        if (point == null || !Finite(point.x) || !Finite(point.z))
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateHorizontalContourTraceCoordinateInvalid");
                        var x = (point.x / 1000d - geometry.SourceCenterX)
                                / geometry.MetersPerUnit;
                        var z = (point.z / 1000d - geometry.SourceCenterZ)
                                / geometry.MetersPerUnit;
                        const double toleranceMeters = .0005d;
                        if (x < geometry.Bounds.MinX - toleranceMeters
                            || x > geometry.Bounds.MaxX + toleranceMeters
                            || z < geometry.Bounds.MinZ - toleranceMeters
                            || z > geometry.Bounds.MaxZ + toleranceMeters)
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateHorizontalContourTraceCoordinateOutOfBounds");
                        points.Add(new 행정동디오라마검토HorizontalContourTracePoint(x, z));
                    }
                    var first = fragment.commonEnuMillimeterPath[0];
                    var second = fragment.commonEnuMillimeterPath[1];
                    var dx = second.x - first.x;
                    var dz = second.z - first.z;
                    if (Math.Sqrt(dx * dx + dz * dz) <= 1d)
                        throw new InvalidOperationException(
                            "AdministrativeDongPrivateHorizontalContourTraceSegmentTooShort");
                    if (!physicalSegments.Add(LaneSegmentKey(first, second)))
                        throw new InvalidOperationException(
                            "AdministrativeDongPrivateHorizontalContourTraceSegmentDuplicate");
                    fragments.Add(new 행정동디오라마검토HorizontalContourTraceFragment(
                        fragment.fragmentOrdinal, Array.AsReadOnly(points.ToArray())));
                }
                candidates.Add(new 행정동디오라마검토HorizontalContourTraceCandidate(
                    candidate.candidateStableId, Array.AsReadOnly(fragments.ToArray())));
            }
            return new 행정동디오라마비공개수평등고선추적OverlaySet(source,
                Array.AsReadOnly(candidates.ToArray()));
        }

        private static string LaneSegmentKey(행정동디오라마공통EnuMillimetersData left,
            행정동디오라마공통EnuMillimetersData right)
        {
            var a = left.x.ToString("R", CultureInfo.InvariantCulture) + ","
                    + left.z.ToString("R", CultureInfo.InvariantCulture);
            var b = right.x.ToString("R", CultureInfo.InvariantCulture) + ","
                    + right.z.ToString("R", CultureInfo.InvariantCulture);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static void ValidateWalkCandidate(string candidateStableId,
            string kindCode, string expectedKindCode, string qualityCode,
            행정동디오라마보행망ActorSummaryFlagsData actorSummaryFlags,
            ISet<string> candidateIds)
        {
            var separator = candidateStableId?.LastIndexOf(':') ?? -1;
            if (separator < 0 || !Sha256(candidateStableId.Substring(separator + 1)))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayCandidateIdentityInvalid");
            if (!candidateIds.Add(candidateStableId))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayCandidateDuplicate");
            if (!string.Equals(kindCode, expectedKindCode, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayKindInvalid");
            if (string.IsNullOrWhiteSpace(qualityCode) || actorSummaryFlags == null)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayCandidateQualityInvalid");
        }

        private static void ValidateCandidate(string candidateStableId,
            string qualityCode, ISet<string> pointIds)
        {
            var separator = candidateStableId?.LastIndexOf(':') ?? -1;
            if (separator < 0 || !Sha256(candidateStableId.Substring(separator + 1)))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointIdentityInvalid");
            if (!pointIds.Add(candidateStableId))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointDuplicate");
            if (string.IsNullOrWhiteSpace(qualityCode))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointQualityInvalid");
        }

        private static 행정동디오라마검토Point LocalPoint(
            행정동디오라마공통EnuMillimetersData value,
            행정동디오라마검토Snapshot geometry)
        {
            if (value == null || !Finite(value.x) || !Finite(value.z))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointInvalid");
            var localX = (value.x / 1000d - geometry.SourceCenterX)
                         / geometry.MetersPerUnit;
            var localZ = (value.z / 1000d - geometry.SourceCenterZ)
                         / geometry.MetersPerUnit;
            if (!Finite(localX) || !Finite(localZ))
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointInvalid");
            if (localX < geometry.Bounds.MinX - Epsilon
                || localX > geometry.Bounds.MaxX + Epsilon
                || localZ < geometry.Bounds.MinZ - Epsilon
                || localZ > geometry.Bounds.MaxZ + Epsilon)
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateObservationOverlayPointOutOfBounds");
            return new 행정동디오라마검토Point(localX, localZ);
        }

        private static 행정동디오라마검토Point LocalWalkPoint(
            행정동디오라마공통EnuMillimetersData value,
            행정동디오라마검토Snapshot geometry)
        {
            try
            {
                return LocalPoint(value, geometry);
            }
            catch (InvalidOperationException exception) when (exception.Message ==
                "AdministrativeDongPrivateObservationOverlayPointInvalid")
            {
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayPointInvalid");
            }
            catch (InvalidOperationException exception) when (exception.Message ==
                "AdministrativeDongPrivateObservationOverlayPointOutOfBounds")
            {
                throw new InvalidOperationException(
                    "AdministrativeDongPrivateWalkNetworkOverlayPointOutOfBounds");
            }
        }

        private static 행정동디오라마검토Point[] NormalizeFootprint(
            IReadOnlyList<AdministrativeDongDioramaPoint> source,
            Func<AdministrativeDongDioramaPoint, 행정동디오라마검토Point> map,
            string errorCode)
        {
            if (source == null || source.Count < 3)
                throw new InvalidOperationException(errorCode);
            var points = source.Select(map).ToList();
            if (points.Count > 1 && Same(points[0], points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);
            for (var index = points.Count - 1; index > 0; index--)
                if (Same(points[index], points[index - 1])) points.RemoveAt(index);
            if (points.Count < 3 || Math.Abs(SignedArea(points)) <= Epsilon)
                throw new InvalidOperationException(errorCode);
            return points.ToArray();
        }

        private static double ResolveHeight(AdministrativeDongDioramaBuilding value,
            double metersPerUnit, out string heightKind)
        {
            double meters;
            if (value.EvidenceKindCode != null && value.EvidenceKindCode.EndsWith(
                    ":SymbolicHeightFallback", StringComparison.Ordinal))
            {
                // 생성기가 대입한 4m는 원천 양수 높이와 구분한다. 층수가 있어도 높이를 소급 추정하지 않는다.
                if (!value.HeightMeters.HasValue || value.HeightMeters.Value != 4d)
                    throw new InvalidOperationException("AdministrativeDongReviewSymbolicHeightProvenanceInvalid");
                meters = 4d;
                heightKind = "SymbolicFallback4m";
            }
            else if (value.HeightMeters.HasValue && Finite(value.HeightMeters.Value)
                                             && value.HeightMeters.Value > 0d)
            {
                meters = value.HeightMeters.Value;
                heightKind = "SourceHeight";
            }
            else if (value.AboveGroundFloorCount.HasValue
                     && value.AboveGroundFloorCount.Value > 0)
            {
                meters = value.AboveGroundFloorCount.Value * 3d;
                heightKind = "FloorCountPresentationEstimate";
            }
            else
            {
                meters = 4d;
                heightKind = "SymbolicFallback4m";
            }
            return Math.Max(2d / metersPerUnit, Math.Min(300d / metersPerUnit,
                meters / metersPerUnit));
        }

        private static bool ReviewReadiness(string value)
            => string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary,
                   StringComparison.Ordinal)
               || string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.WaitingForSpatialLinkage,
                   StringComparison.Ordinal)
               || string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                   StringComparison.Ordinal);

        private static double SignedArea(IReadOnlyList<행정동디오라마검토Point> points)
        {
            var result = 0d;
            for (var index = 0; index < points.Count; index++)
            {
                var next = (index + 1) % points.Count;
                result += points[index].X * points[next].Z - points[next].X * points[index].Z;
            }
            return result * .5d;
        }

        private static bool Same(행정동디오라마검토Point left,
            행정동디오라마검토Point right)
            => Math.Abs(left.X - right.X) <= Epsilon
               && Math.Abs(left.Z - right.Z) <= Epsilon;

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Sha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }

        private static bool Hex(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != length) return false;
            foreach (var character in value) if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
