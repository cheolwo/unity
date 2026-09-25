using System;
using System.Collections.Generic;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    [Serializable]
    public sealed class 한반도교류축SourceReceipt
    {
        public string sourceReceiptStableId = string.Empty;
        public string providerName = string.Empty;
        public string documentTitle = string.Empty;
        public string sourceUrl = string.Empty;
        public string evidenceStateCode = string.Empty;
    }

    [Serializable]
    public sealed class 한반도교류축Hub
    {
        public string researchSubjectId = string.Empty;
        public string displayName = string.Empty;
        public int displayOrder;
        public string evidenceStateCode = string.Empty;
        public string operationStateCode = string.Empty;
        public string maximumObservationLevel = string.Empty;
        public string limitationText = string.Empty;
        public string[] sourceReceiptStableIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 한반도교류축Segment
    {
        public string segmentStableId = string.Empty;
        public string fromResearchSubjectId = string.Empty;
        public string toResearchSubjectId = string.Empty;
        public string segmentStateCode = string.Empty;
        public string displayStyleCode = string.Empty;
        public string[] sourceReceiptStableIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 한반도교류축Catalog
    {
        private static readonly HashSet<string> AllowedSegmentStates = new(StringComparer.Ordinal)
        {
            "ResearchPending",
            "OperationalDomestic",
            "HistoricalConnection",
            "Disconnected",
            "AwaitingIdentityReview",
            "InternationalCorridorDescription",
        };

        public string schemaVersion = string.Empty;
        public string revision = string.Empty;
        public string generatedAtUtc = string.Empty;
        public bool observationPresentationOnly;
        public bool operationalAuthority;
        public bool detailedGeometryAllowed;
        public string boundaryNotice = string.Empty;
        public 한반도교류축SourceReceipt[] sources = Array.Empty<한반도교류축SourceReceipt>();
        public 한반도교류축Hub[] hubs = Array.Empty<한반도교류축Hub>();
        public 한반도교류축Segment[] segments = Array.Empty<한반도교류축Segment>();

        public void Validate()
        {
            if (schemaVersion != "korean-peninsula-corridor-catalog.v1")
                throw new InvalidOperationException("KoreanPeninsulaCatalogSchemaVersionUnsupported");
            if (string.IsNullOrWhiteSpace(revision)
                || !DateTimeOffset.TryParse(generatedAtUtc, out _))
                throw new InvalidOperationException("KoreanPeninsulaCatalogRevisionInvalid");
            if (!observationPresentationOnly || operationalAuthority || detailedGeometryAllowed)
                throw new InvalidOperationException("KoreanPeninsulaCatalogAuthorityBoundaryInvalid");
            if (string.IsNullOrWhiteSpace(boundaryNotice))
                throw new InvalidOperationException("KoreanPeninsulaCatalogBoundaryNoticeMissing");
            if (sources == null || sources.Length != 5)
                throw new InvalidOperationException("KoreanPeninsulaCatalogSourcesInvalid");

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                if (source == null
                    || string.IsNullOrWhiteSpace(source.sourceReceiptStableId)
                    || !sourceIds.Add(source.sourceReceiptStableId)
                    || string.IsNullOrWhiteSpace(source.providerName)
                    || string.IsNullOrWhiteSpace(source.documentTitle)
                    || !Uri.TryCreate(source.sourceUrl, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("KoreanPeninsulaCatalogSourceInvalid");
            }

            if (hubs == null || hubs.Length != 8)
                throw new InvalidOperationException("KoreanPeninsulaCatalogHubsInvalid");
            var hubIds = new HashSet<string>(StringComparer.Ordinal);
            var displayOrders = new HashSet<int>();
            foreach (var hub in hubs)
            {
                if (hub == null
                    || string.IsNullOrWhiteSpace(hub.researchSubjectId)
                    || !hubIds.Add(hub.researchSubjectId)
                    || !displayOrders.Add(hub.displayOrder)
                    || string.IsNullOrWhiteSpace(hub.displayName)
                    || string.IsNullOrWhiteSpace(hub.evidenceStateCode)
                    || string.IsNullOrWhiteSpace(hub.operationStateCode)
                    || string.IsNullOrWhiteSpace(hub.maximumObservationLevel))
                    throw new InvalidOperationException("KoreanPeninsulaCatalogHubInvalid");
                ValidateSourceReferences(hub.sourceReceiptStableIds, sourceIds);
            }

            if (segments == null || segments.Length != 7)
                throw new InvalidOperationException("KoreanPeninsulaCatalogSegmentsInvalid");
            var segmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var segment in segments)
            {
                if (segment == null
                    || string.IsNullOrWhiteSpace(segment.segmentStableId)
                    || !segmentIds.Add(segment.segmentStableId)
                    || !hubIds.Contains(segment.fromResearchSubjectId)
                    || !hubIds.Contains(segment.toResearchSubjectId)
                    || !AllowedSegmentStates.Contains(segment.segmentStateCode)
                    || string.IsNullOrWhiteSpace(segment.displayStyleCode))
                    throw new InvalidOperationException("KoreanPeninsulaCatalogSegmentInvalid");
                ValidateSourceReferences(segment.sourceReceiptStableIds, sourceIds, true);
            }
        }

        public 한반도교류축Hub FindHub(string researchSubjectId)
        {
            if (string.IsNullOrWhiteSpace(researchSubjectId)) return null;
            foreach (var hub in hubs)
                if (string.Equals(hub.researchSubjectId, researchSubjectId,
                        StringComparison.Ordinal))
                    return hub;
            return null;
        }

        private static void ValidateSourceReferences(
            string[] references,
            HashSet<string> sourceIds,
            bool allowEmpty = false)
        {
            if (references == null || (!allowEmpty && references.Length == 0))
                throw new InvalidOperationException("KoreanPeninsulaCatalogSourceReferenceMissing");
            foreach (var reference in references)
                if (!sourceIds.Contains(reference))
                    throw new InvalidOperationException("KoreanPeninsulaCatalogSourceReferenceInvalid");
        }
    }
}
