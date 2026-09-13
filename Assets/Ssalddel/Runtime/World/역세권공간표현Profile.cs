using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 역별 파일명과 좌표·판본·hash를 공통 loader에서 분리하는 불변 설명자다.
    /// 로컬 경로와 원자료는 담지 않으며 Unity Scene 직렬화 대상으로 선언하지 않는다.
    /// </summary>
    public sealed class StationDioramaDatasetDescriptor
    {
        public StationDioramaDatasetDescriptor(
            string transitStationStableId,
            string displayName,
            string schemaVersion,
            string privateReviewRevision,
            string localPrivateReviewPathKey,
            string coordinateMethod,
            double originLatitude,
            double originLongitude,
            double offsetX,
            double offsetZ,
            double halfExtentMeters,
            string expectedContentHashSha256,
            string expectedAssetHashSha256,
            string legacyBaseMapResourceName = null,
            string legacyBaseMapRevision = null,
            string legacyBaseMapHashSha256 = null,
            string expectedOpenStreetMapSourceReceiptId = null)
        {
            TransitStationStableId = Required(transitStationStableId, nameof(transitStationStableId));
            DisplayName = Required(displayName, nameof(displayName));
            SchemaVersion = Required(schemaVersion, nameof(schemaVersion));
            PrivateReviewRevision = Required(privateReviewRevision, nameof(privateReviewRevision));
            LocalPrivateReviewPathKey = Required(localPrivateReviewPathKey,
                nameof(localPrivateReviewPathKey));
            CoordinateMethod = Required(coordinateMethod, nameof(coordinateMethod));
            RequireFinite(originLatitude, nameof(originLatitude));
            RequireFinite(originLongitude, nameof(originLongitude));
            RequireFinite(offsetX, nameof(offsetX));
            RequireFinite(offsetZ, nameof(offsetZ));
            if (!Finite(halfExtentMeters) || halfExtentMeters <= 0d)
                throw new ArgumentOutOfRangeException(nameof(halfExtentMeters));
            OriginLatitude = originLatitude;
            OriginLongitude = originLongitude;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            HalfExtentMeters = halfExtentMeters;
            ExpectedContentHashSha256 = OptionalSha256(expectedContentHashSha256,
                nameof(expectedContentHashSha256));
            ExpectedAssetHashSha256 = OptionalSha256(expectedAssetHashSha256,
                nameof(expectedAssetHashSha256));
            LegacyBaseMapResourceName = Optional(legacyBaseMapResourceName);
            LegacyBaseMapRevision = Optional(legacyBaseMapRevision);
            LegacyBaseMapHashSha256 = string.IsNullOrEmpty(legacyBaseMapHashSha256)
                ? string.Empty
                : OptionalSha256(legacyBaseMapHashSha256, nameof(legacyBaseMapHashSha256));
            ExpectedOpenStreetMapSourceReceiptId = Optional(
                expectedOpenStreetMapSourceReceiptId);
            if (!string.IsNullOrEmpty(ExpectedOpenStreetMapSourceReceiptId)
                && !ExpectedOpenStreetMapSourceReceiptId.StartsWith(
                    "openstreetmap:", StringComparison.Ordinal))
                throw new ArgumentException("StationDioramaOpenStreetMapReceiptIdInvalid",
                    nameof(expectedOpenStreetMapSourceReceiptId));

            var legacyFields = new[]
            {
                LegacyBaseMapResourceName, LegacyBaseMapRevision, LegacyBaseMapHashSha256
            };
            var legacyCount = 0;
            foreach (var value in legacyFields)
                if (!string.IsNullOrEmpty(value)) legacyCount++;
            if (legacyCount != 0 && legacyCount != legacyFields.Length)
                throw new ArgumentException("StationDioramaLegacyBaseMapDescriptorIncomplete");
        }

        public string TransitStationStableId { get; }
        public string DisplayName { get; }
        public string SchemaVersion { get; }
        public string PrivateReviewRevision { get; }
        public string LocalPrivateReviewPathKey { get; }
        public string CoordinateMethod { get; }
        public double OriginLatitude { get; }
        public double OriginLongitude { get; }
        public double OffsetX { get; }
        public double OffsetZ { get; }
        public double HalfExtentMeters { get; }
        public string ExpectedContentHashSha256 { get; }
        public string ExpectedAssetHashSha256 { get; }
        public string LegacyBaseMapResourceName { get; }
        public string LegacyBaseMapRevision { get; }
        public string LegacyBaseMapHashSha256 { get; }
        public string ExpectedOpenStreetMapSourceReceiptId { get; }
        public bool UsesLegacyBaseMap => !string.IsNullOrEmpty(LegacyBaseMapResourceName);
        public bool HasPinnedSnapshotFingerprint => !string.IsNullOrEmpty(ExpectedContentHashSha256)
                                                    && !string.IsNullOrEmpty(ExpectedAssetHashSha256);

        private static string Required(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ArgumentException("StationDioramaDescriptorTextInvalid", parameter);
            return value;
        }

        private static string Optional(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ArgumentException("StationDioramaDescriptorTextInvalid");
            return value;
        }

        private static string OptionalSha256(string value, string parameter)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length != 64)
                throw new ArgumentException("StationDioramaDescriptorHashInvalid", parameter);
            foreach (var character in value)
                if (!Uri.IsHexDigit(character))
                    throw new ArgumentException("StationDioramaDescriptorHashInvalid", parameter);
            return value.ToUpperInvariant();
        }

        private static void RequireFinite(double value, string parameter)
        {
            if (!Finite(value)) throw new ArgumentOutOfRangeException(parameter);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>역마다 자료와 독립적으로 조정할 수 있는 표현·성능 예산이다.</summary>
    public sealed class StationDioramaPresentationProfile
    {
        public StationDioramaPresentationProfile(string revision, int chunkAxisCount,
            float stationDetailRadiusMeters, int maximumRendererCount,
            int maximumMaterialCount, float symbolicRoadWidthMeters,
            int observationLayer, string shaderName, float wholeViewPitch,
            float wholeViewYaw)
        {
            if (string.IsNullOrWhiteSpace(revision))
                throw new ArgumentException("StationDioramaPresentationRevisionMissing",
                    nameof(revision));
            if (chunkAxisCount < 1 || chunkAxisCount > 16)
                throw new ArgumentOutOfRangeException(nameof(chunkAxisCount));
            if (!float.IsFinite(stationDetailRadiusMeters) || stationDetailRadiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stationDetailRadiusMeters));
            if (maximumRendererCount < chunkAxisCount * chunkAxisCount
                || maximumMaterialCount < 1 || maximumMaterialCount > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumRendererCount));
            if (!float.IsFinite(symbolicRoadWidthMeters) || symbolicRoadWidthMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(symbolicRoadWidthMeters));
            if (observationLayer < 0 || observationLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(observationLayer));
            if (string.IsNullOrWhiteSpace(shaderName))
                throw new ArgumentException("StationDioramaShaderNameMissing", nameof(shaderName));
            if (!float.IsFinite(wholeViewPitch) || !float.IsFinite(wholeViewYaw))
                throw new ArgumentOutOfRangeException(nameof(wholeViewPitch));
            Revision = revision;
            ChunkAxisCount = chunkAxisCount;
            StationDetailRadiusMeters = stationDetailRadiusMeters;
            MaximumRendererCount = maximumRendererCount;
            MaximumMaterialCount = maximumMaterialCount;
            SymbolicRoadWidthMeters = symbolicRoadWidthMeters;
            ObservationLayer = observationLayer;
            ShaderName = shaderName;
            WholeViewPitch = wholeViewPitch;
            WholeViewYaw = wholeViewYaw;
        }

        public string Revision { get; }
        public int ChunkAxisCount { get; }
        public float StationDetailRadiusMeters { get; }
        public int MaximumRendererCount { get; }
        public int MaximumMaterialCount { get; }
        public float SymbolicRoadWidthMeters { get; }
        public int ObservationLayer { get; }
        public string ShaderName { get; }
        public float WholeViewPitch { get; }
        public float WholeViewYaw { get; }
    }

    /// <summary>사가정 참조 구현과 후속 역을 동일한 선택 계약으로 노출한다.</summary>
    public static class StationDioramaProfileCatalog
    {
        public const string MyeonmokStationStableId = "station:kr:kric:s1107:0721";
        public const string SagajeongStationStableId = "station:kr:kric:s1107:0722";
        public const string YongmasanStationStableId = "station:kr:kric:s1107:0723";
        public const string LocalPrivateReviewStatus = "LocalPrivateReview";
        public const string MissingCoverage = "MissingCoverage";
        public const string PartialCoverage = "PartialCoverage";
        public const string Ready = "Ready";

        public static readonly StationDioramaPresentationProfile StandardOneKilometer = new(
            "station-diorama-presentation.standard-1km.r1", 4, 135f, 128, 12, 2.4f,
            30, "Ssalddel/StationDiorama", 34f, 40f);

        public static readonly StationDioramaDatasetDescriptor Sagajeong = new(
            SagajeongStationStableId,
            "사가정역",
            사가정공간표현OverlayComposer.SchemaVersion,
            사가정공간표현OverlayComposer.PrivateReviewRevision,
            사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey,
            사가정공간표현OverlayComposer.CoordinateMethod,
            사가정공간표현OverlayComposer.OriginLatitude,
            사가정공간표현OverlayComposer.OriginLongitude,
            사가정공간표현OverlayComposer.OffsetX,
            사가정공간표현OverlayComposer.OffsetZ,
            사가정공간표현OverlayComposer.HalfExtent,
            "138D1A47B286CE350CF339C1F69C6FFAD7778EBA7B6C95F3CF9F172F8248EBB1",
            "3EC6B95DC083F13F99042989E0B58957B3EE031E06C501F94022BE455DEC0A44",
            "SagajeongReference",
            사가정공간표현OverlayComposer.BaseMapRevision,
            사가정공간표현OverlayComposer.BaseMapHash);

        // 면목은 자체 snapshot 하나가 1km 창의 좌표·건물·도로·coverage를 제공한다.
        // SagajeongReference는 이 descriptor에 포함하지 않으며 최종 감사 fingerprint만 고정한다.
        public static readonly StationDioramaDatasetDescriptor Myeonmok = new(
            MyeonmokStationStableId,
            "면목역",
            "ssalddel.station-spatial-snapshot.v1",
            "myeonmok-station-spatial-snapshot.private-review.r1",
            "SSALDDEL_MYEONMOK_STATION_SPATIAL_SNAPSHOT",
            "WGS84-ECEF-ENU-at-zero-altitude",
            37.588671d,
            127.087503d,
            0d,
            0d,
            500d,
            "48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A",
            "4B8D45AE3414218228FEB1436AE8E99AB3999C83A30607292AC594C0099530B1",
            expectedOpenStreetMapSourceReceiptId: "openstreetmap:myeonmok-station-0721-r1");

        // 용마산은 면목과 같은 자가 완결 1km snapshot 계약을 사용한다.
        // 산출물은 로컬 비공개 검토 사본이며 배포·통행·게임플레이 권위를 갖지 않는다.
        public static readonly StationDioramaDatasetDescriptor Yongmasan = new(
            YongmasanStationStableId,
            "용마산역",
            "ssalddel.station-spatial-snapshot.v1",
            "yongmasan-station-spatial-snapshot.private-review.r1",
            "SSALDDEL_YONGMASAN_STATION_SPATIAL_SNAPSHOT",
            "WGS84-ECEF-ENU-at-zero-altitude",
            37.573752d,
            127.086802d,
            0d,
            0d,
            500d,
            "43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D",
            "6AC2E8595B913B6AE80D6426476F334225491E89555CEB9A5C1A17BD568120B6",
            expectedOpenStreetMapSourceReceiptId: "openstreetmap:yongmasan-station-0723-r1");

        private static readonly IReadOnlyDictionary<string, StationDioramaDatasetDescriptor>
            Datasets = new ReadOnlyDictionary<string, StationDioramaDatasetDescriptor>(
                new Dictionary<string, StationDioramaDatasetDescriptor>(StringComparer.Ordinal)
                {
                    [Sagajeong.TransitStationStableId] = Sagajeong,
                    [Myeonmok.TransitStationStableId] = Myeonmok,
                    [Yongmasan.TransitStationStableId] = Yongmasan
                });

        public static bool TryResolve(string transitStationStableId,
            out StationDioramaDatasetDescriptor dataset,
            out StationDioramaPresentationProfile presentation)
        {
            presentation = StandardOneKilometer;
            return Datasets.TryGetValue(transitStationStableId ?? string.Empty, out dataset);
        }
    }
}
