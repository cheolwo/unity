using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    public enum 한반도지형미리보기단계
    {
        Hidden = 0,
        Coastline = 1,
        Relief = 2,
        Rivers = 3,
        Hubs = 4,
    }

    [Serializable]
    public sealed class 한반도지형미리보기Bounds
    {
        public double minLongitude;
        public double maxLongitude;
        public double minLatitude;
        public double maxLatitude;
    }

    [Serializable]
    public sealed class 한반도지형미리보기Source
    {
        public string sourceId = string.Empty;
        public string datasetId = string.Empty;
        public string providerName = string.Empty;
        public string sourceLandingPageUrl = string.Empty;
        public string sourceDownloadUrl = string.Empty;
        public string sourceRevision = string.Empty;
        public string archiveRevision = string.Empty;
        public string licenseCode = string.Empty;
        public string attribution = string.Empty;
        public string sourceArchiveHashSha256 = string.Empty;
        public string derivedAssetHashSha256 = string.Empty;
        public string resourceName = string.Empty;
        public long derivedAssetByteLength;
        public int derivedAssetWidth;
        public int derivedAssetHeight;
        public string versionNotice = string.Empty;
    }

    [Serializable]
    public sealed class 한반도지형검증하천
    {
        public string displayName = string.Empty;
        public string englishName = string.Empty;
        public int[] sourceFeatureIndices = Array.Empty<int>();
        public string[] naturalEarthIds = Array.Empty<string>();
        public string wikidataId = string.Empty;
        public string evidenceStateCode = string.Empty;
    }

    [Serializable]
    public sealed class 한반도지형누락하천
    {
        public string displayName = string.Empty;
        public string englishName = string.Empty;
        public string evidenceStateCode = string.Empty;
        public string limitationText = string.Empty;
    }

    [Serializable]
    public sealed class 한반도지형미리보기Catalog
    {
        public const string ResourceName = "KoreanPeninsulaTerrainPreviewManifest";

        public string schemaVersion = string.Empty;
        public string revision = string.Empty;
        public string generatedAtUtc = string.Empty;
        public bool distributionApproved;
        public bool observationPresentationOnly;
        public bool finalTerrainAuthority;
        public bool traversalReady;
        public bool gameplayReady;
        public string maximumPreviewStageCode = string.Empty;
        public string coordinateReferenceSystem = string.Empty;
        public string boundaryNotice = string.Empty;
        public 한반도지형미리보기Bounds bounds = new();
        public string textureResourceName = string.Empty;
        public string riverTextureResourceName = string.Empty;
        public 한반도지형미리보기Source[] sources = Array.Empty<한반도지형미리보기Source>();
        public string[] verifiedRiverNames = Array.Empty<string>();
        public string[] missingRiverNames = Array.Empty<string>();
        public 한반도지형검증하천[] verifiedRivers = Array.Empty<한반도지형검증하천>();
        public 한반도지형누락하천[] missingRivers = Array.Empty<한반도지형누락하천>();

        public void Validate()
        {
            if (schemaVersion != "korean-peninsula-terrain-preview.v1")
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewSchemaVersionUnsupported");
            if (string.IsNullOrWhiteSpace(revision)
                || !DateTimeOffset.TryParse(generatedAtUtc, out _))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewRevisionInvalid");
            if (!distributionApproved
                || !observationPresentationOnly
                || finalTerrainAuthority
                || traversalReady
                || gameplayReady)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewAuthorityBoundaryInvalid");
            if (maximumPreviewStageCode != nameof(한반도지형미리보기단계.Hubs)
                || coordinateReferenceSystem != "EPSG:4326"
                || string.IsNullOrWhiteSpace(boundaryNotice))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewMetadataInvalid");
            if (bounds == null
                || bounds.minLongitude >= bounds.maxLongitude
                || bounds.minLatitude >= bounds.maxLatitude
                || bounds.minLongitude is < -180d or > 180d
                || bounds.maxLongitude is < -180d or > 180d
                || bounds.minLatitude is < -90d or > 90d
                || bounds.maxLatitude is < -90d or > 90d)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewBoundsInvalid");
            if (string.IsNullOrWhiteSpace(textureResourceName)
                || string.IsNullOrWhiteSpace(riverTextureResourceName)
                || string.Equals(textureResourceName, riverTextureResourceName,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewResourcesInvalid");

            ValidateSources();
            ValidateRivers();
        }

        public 한반도지형미리보기Source FindSource(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) return null;
            foreach (var source in sources)
                if (string.Equals(source.sourceId, sourceId, StringComparison.Ordinal))
                    return source;
            return null;
        }

        private void ValidateSources()
        {
            if (sources == null || sources.Length != 2)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewSourcesInvalid");

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            var resourceNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                if (source == null
                    || string.IsNullOrWhiteSpace(source.sourceId)
                    || !sourceIds.Add(source.sourceId)
                    || string.IsNullOrWhiteSpace(source.datasetId)
                    || string.IsNullOrWhiteSpace(source.providerName)
                    || !IsHttps(source.sourceLandingPageUrl)
                    || !IsHttps(source.sourceDownloadUrl)
                    || string.IsNullOrWhiteSpace(source.sourceRevision)
                    || string.IsNullOrWhiteSpace(source.archiveRevision)
                    || source.licenseCode != "PublicDomain"
                    || string.IsNullOrWhiteSpace(source.attribution)
                    || !IsSha256(source.sourceArchiveHashSha256)
                    || !IsSha256(source.derivedAssetHashSha256)
                    || string.IsNullOrWhiteSpace(source.resourceName)
                    || !resourceNames.Add(source.resourceName)
                    || source.derivedAssetByteLength <= 0
                    || source.derivedAssetWidth <= 0
                    || source.derivedAssetHeight <= 0)
                    throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewSourceInvalid");
            }

            if (!resourceNames.Contains(textureResourceName)
                || !resourceNames.Contains(riverTextureResourceName))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewSourceResourceMismatch");
        }

        private void ValidateRivers()
        {
            if (verifiedRivers == null || verifiedRivers.Length != 3)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewVerifiedRiversInvalid");
            if (verifiedRiverNames == null || verifiedRiverNames.Length != 3)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewVerifiedRiverNamesInvalid");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var river in verifiedRivers)
            {
                if (river == null
                    || string.IsNullOrWhiteSpace(river.displayName)
                    || !names.Add(river.displayName)
                    || string.IsNullOrWhiteSpace(river.englishName)
                    || river.sourceFeatureIndices == null
                    || river.sourceFeatureIndices.Length == 0
                    || river.naturalEarthIds == null
                    || river.sourceFeatureIndices.Length != river.naturalEarthIds.Length
                    || string.IsNullOrWhiteSpace(river.wikidataId)
                    || river.evidenceStateCode != "VerifiedSourceFeature")
                    throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewVerifiedRiverInvalid");
                for (var index = 0; index < river.sourceFeatureIndices.Length; index++)
                    if (river.sourceFeatureIndices[index] < 0
                        || string.IsNullOrWhiteSpace(river.naturalEarthIds[index]))
                        throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewVerifiedRiverFeatureInvalid");
            }

            if (!names.Contains("한강") || !names.Contains("압록강") || !names.Contains("두만강"))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewRequiredRiverMissing");
            foreach (var name in verifiedRiverNames)
                if (!names.Contains(name))
                    throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewVerifiedRiverNameMismatch");
            if (missingRivers == null || missingRivers.Length != 1)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewMissingRiversInvalid");
            if (missingRiverNames == null
                || missingRiverNames.Length != 1
                || missingRiverNames[0] != "대동강")
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewMissingRiverNamesInvalid");

            var missing = missingRivers[0];
            if (missing == null
                || missing.displayName != "대동강"
                || string.IsNullOrWhiteSpace(missing.englishName)
                || missing.evidenceStateCode != "NotFoundInNaturalEarth10mSource"
                || string.IsNullOrWhiteSpace(missing.limitationText))
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewMissingRiverInvalid");
        }

        private static bool IsHttps(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
