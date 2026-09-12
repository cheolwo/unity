using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 면목동디오라마표현자료
    {
        public string schema, revision, mapRevision, mapSha256, sourceManifestSha256, authorityBoundary;
        public bool privateReviewOnly, distributionApproved, gameStateConnected;
        public 면목동표현Layer[] layers;
        public 면목동건물Overlay[] buildingOverlays;
        public 면목동상태Count[] unplacedSampleSummary;
        public 면목동디오라마Count counts;
    }

    [Serializable] public sealed class 면목동표현Layer
    {
        public string layerCode, title;
        public bool defaultVisible;
    }

    [Serializable] public sealed class 면목동건물Overlay
    {
        public string spatialId, buildingRef, buildingUseCode, buildingUseEvidenceCode, heightSemanticsCode;
        public float displayHeight;
        public string[] activityCategories;
        public 면목동분류Count[] activityCounts;
        public 면목동영업근거Count[] operationEvidenceCounts;
        public string connectionStatus, connectionMethod;
        public int observationCount;

    }

    [Serializable] public sealed class 면목동분류Count { public string categoryCode; public int count; }
    [Serializable] public sealed class 면목동영업근거Count { public string evidenceCode; public int count; }
    [Serializable] public sealed class 면목동상태Count { public string status; public int count; }
    [Serializable] public sealed class 면목동디오라마Count
    {
        public int baseBuildings, baseRoadSegments, overlayBuildings, activityObservations, landSamples;
        public int officialBuildingLinks, addressSingleCandidates, multipleCandidates, unlinkedSamples;
    }

    public static class 면목동디오라마표현검사
    {
        public const string ProjectionHash = "01C06DB91774612D4DAA988ADC03216835563B7490E880011E3AD52F544E6FB0";
        public const string MapHash = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
        private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
        { "음식", "소매", "의료", "주거", "제조", "전통시장", "기타관측" };
        private static readonly HashSet<string> OperationEvidence = new(StringComparer.Ordinal)
        { "LicenseOperatingObservationOnly", "LicenseInactiveObservationOnly", "CurrentOperationUnknown" };

        public static byte[] ReadFrozen(string path, byte[] mapBytes)
        {
            Require(Hash(mapBytes) == MapHash, "DioramaMapChanged");
            var full = Path.GetFullPath(path);
            for (var folder = new DirectoryInfo(Path.GetDirectoryName(full)); folder != null; folder = folder.Parent)
                if (folder.Exists) Require((folder.Attributes & FileAttributes.ReparsePoint) == 0, "DioramaReparseRejected");
            var file = new FileInfo(full);
            Require(file.Exists && file.Length > 0 && file.Length <= 4 * 1024 * 1024 && (file.Attributes & FileAttributes.ReparsePoint) == 0, "DioramaProjectionMissingOrInvalid");
            var bytes = File.ReadAllBytes(full);
            Require(Hash(bytes) == ProjectionHash, "DioramaProjectionChanged");
            return bytes;
        }

        public static void Validate(면목동디오라마표현자료 data, 공간참고자료 map)
        {
            Require(data != null && map != null && map.buildings != null, "DioramaDataMissing");
            Require(data.schema == "myeonmok-diorama-presentation.r1" && data.revision == "myeonmok-diorama-view.r1"
                && data.privateReviewOnly && !data.distributionApproved && !data.gameStateConnected
                && data.mapRevision == map.revision && data.mapSha256 == MapHash, "DioramaContractMismatch");
            Require(data.layers != null && data.layers.Length == 7 && data.layers.Select(x => x.layerCode).Distinct(StringComparer.Ordinal).Count() == 7, "DioramaLayerContract");
            Require(data.counts != null && data.counts.baseBuildings == 602 && data.counts.baseRoadSegments == 2397
                && data.counts.overlayBuildings == 204 && data.counts.activityObservations == 801 && data.counts.landSamples == 30
                && data.counts.officialBuildingLinks == 0 && data.counts.addressSingleCandidates == 14
                && data.counts.multipleCandidates == 8 && data.counts.unlinkedSamples == 8, "DioramaCountContract");
            Require(data.unplacedSampleSummary != null && data.unplacedSampleSummary.Sum(x => x.count) == 30, "DioramaSampleSummary");
            Require(data.buildingOverlays != null && data.buildingOverlays.Length == 204, "DioramaOverlayCount");
            var buildings = map.buildings.ToDictionary(x => x.id, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var refs = new HashSet<string>(StringComparer.Ordinal);
            var observations = 0;
            foreach (var overlay in data.buildingOverlays)
            {
                Require(overlay != null && !string.IsNullOrEmpty(overlay.spatialId) && ids.Add(overlay.spatialId)
                    && !string.IsNullOrEmpty(overlay.buildingRef) && refs.Add(overlay.buildingRef) && buildings.ContainsKey(overlay.buildingRef), "DioramaOverlayIdentity");
                Require(overlay.connectionStatus == "주소단일후보" && overlay.connectionMethod == "UniqueAddressCandidateNotVerifiedOccupancy", "DioramaConnectionStatus");
                Require(overlay.activityCategories != null && overlay.activityCategories.Length > 0
                    && overlay.activityCategories.Distinct(StringComparer.Ordinal).Count() == overlay.activityCategories.Length
                    && overlay.activityCategories.All(Categories.Contains), "DioramaActivityCategory");
                Require(overlay.activityCounts != null && overlay.activityCounts.Sum(x => x.count) == overlay.observationCount
                    && overlay.activityCounts.All(x => Categories.Contains(x.categoryCode) && x.count > 0), "DioramaActivityCounts");
                Require(overlay.operationEvidenceCounts != null && overlay.operationEvidenceCounts.Sum(x => x.count) == overlay.observationCount
                    && overlay.operationEvidenceCounts.All(x => OperationEvidence.Contains(x.evidenceCode) && x.count > 0), "DioramaOperationEvidence");
                Require(Finite(overlay.displayHeight) && overlay.displayHeight > 0
                    && overlay.heightSemanticsCode is "OsmHeightOrLevelsDerived" or "SymbolicDisplayOnly", "DioramaHeightEvidence");
                observations += overlay.observationCount;
            }
            Require(observations == 801, "DioramaObservationTotal");
        }

        public static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string code) { if (!condition) throw new InvalidDataException(code); }
    }
}
