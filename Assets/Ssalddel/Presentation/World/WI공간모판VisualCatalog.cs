using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class WI공간모판CapacityView
    {
        [SerializeField] private string capacityCode = string.Empty;
        [SerializeField] private int quantity;
        [SerializeField] private string unitCode = string.Empty;

        public string CapacityCode => capacityCode;
        public int Quantity => quantity;
        public string UnitCode => unitCode;

        public void Configure(string code, int value, string unit)
        {
            capacityCode = code ?? string.Empty;
            quantity = value;
            unitCode = unit ?? string.Empty;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(capacityCode)
            && quantity > 0 && !string.IsNullOrWhiteSpace(unitCode);
    }

    [Serializable]
    public sealed class WI공간모판CandidateView
    {
        [SerializeField] private string compositionKey = string.Empty;
        [SerializeField] private string sourceCompositionKey = string.Empty;
        [SerializeField] private string topologyCode = string.Empty;
        [SerializeField] private Vector2 nativeFootprintMeters;
        [SerializeField] private GameObject prefab = null!;

        public string CompositionKey => compositionKey;
        public string SourceCompositionKey => sourceCompositionKey;
        public string TopologyCode => topologyCode;
        public Vector2 NativeFootprintMeters => nativeFootprintMeters;
        public GameObject Prefab => prefab;

        public void Configure(
            string key,
            string sourceKey,
            string topology,
            Vector2 footprint,
            GameObject sourcePrefab)
        {
            compositionKey = key ?? string.Empty;
            sourceCompositionKey = sourceKey ?? string.Empty;
            topologyCode = topology ?? string.Empty;
            nativeFootprintMeters = footprint;
            prefab = sourcePrefab;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(compositionKey)
            && !string.IsNullOrWhiteSpace(sourceCompositionKey)
            && !string.IsNullOrWhiteSpace(topologyCode)
            && nativeFootprintMeters.x > 0f && nativeFootprintMeters.y > 0f
            && prefab != null;
    }

    [Serializable]
    public sealed class WI공간모판SpaceView
    {
        [SerializeField] private string spaceCode = string.Empty;
        [SerializeField] private string spatialRoleCode = string.Empty;
        [SerializeField] private string[] capabilityCodes = Array.Empty<string>();
        [SerializeField] private WI공간모판CapacityView[] capacities =
            Array.Empty<WI공간모판CapacityView>();
        [SerializeField] private WI공간모판CandidateView[] candidates =
            Array.Empty<WI공간모판CandidateView>();

        public string SpaceCode => spaceCode;
        public string SpatialRoleCode => spatialRoleCode;
        public IReadOnlyList<string> CapabilityCodes => capabilityCodes;
        public IReadOnlyList<WI공간모판CapacityView> Capacities => capacities;
        public IReadOnlyList<WI공간모판CandidateView> Candidates => candidates;

        public void Configure(
            string code,
            string roleCode,
            string[] capabilities,
            WI공간모판CapacityView[] capacityValues,
            WI공간모판CandidateView[] candidateValues)
        {
            spaceCode = code ?? string.Empty;
            spatialRoleCode = roleCode ?? string.Empty;
            capabilityCodes = capabilities ?? Array.Empty<string>();
            capacities = capacityValues ?? Array.Empty<WI공간모판CapacityView>();
            candidates = candidateValues ?? Array.Empty<WI공간모판CandidateView>();
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(spaceCode)
            && !string.IsNullOrWhiteSpace(spatialRoleCode)
            && capabilityCodes.Length > 0
            && capabilityCodes.All(value => !string.IsNullOrWhiteSpace(value))
            && capacities.All(value => value != null && value.Validate())
            && candidates.Length > 0 && candidates.All(value => value != null && value.Validate())
            && candidates.Select(value => value.CompositionKey)
                .Distinct(StringComparer.Ordinal).Count() == candidates.Length;
    }

    [Serializable]
    public sealed class WI공간모판RelationView
    {
        [SerializeField] private string relationCode = string.Empty;
        [SerializeField] private string fromSpaceCode = string.Empty;
        [SerializeField] private string toSpaceCode = string.Empty;
        [SerializeField] private string connectorTypeCode = string.Empty;

        public string RelationCode => relationCode;
        public string FromSpaceCode => fromSpaceCode;
        public string ToSpaceCode => toSpaceCode;
        public string ConnectorTypeCode => connectorTypeCode;

        public void Configure(string relation, string from, string to, string connector)
        {
            relationCode = relation ?? string.Empty;
            fromSpaceCode = from ?? string.Empty;
            toSpaceCode = to ?? string.Empty;
            connectorTypeCode = connector ?? string.Empty;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(relationCode)
            && !string.IsNullOrWhiteSpace(fromSpaceCode)
            && !string.IsNullOrWhiteSpace(toSpaceCode)
            && !string.IsNullOrWhiteSpace(connectorTypeCode);
    }

    [Serializable]
    public sealed class WI공간모판ConnectorView
    {
        [SerializeField] private string stubCode = string.Empty;
        [SerializeField] private string internalSpaceCode = string.Empty;
        [SerializeField] private string connectorTypeCode = string.Empty;
        [SerializeField] private string flowDirectionCode = string.Empty;
        [SerializeField] private string adjacentWorldInteractionId = string.Empty;

        public string StubCode => stubCode;
        public string InternalSpaceCode => internalSpaceCode;
        public string ConnectorTypeCode => connectorTypeCode;
        public string FlowDirectionCode => flowDirectionCode;
        public string AdjacentWorldInteractionId => adjacentWorldInteractionId;

        public void Configure(
            string stub,
            string space,
            string connector,
            string direction,
            string adjacentWi)
        {
            stubCode = stub ?? string.Empty;
            internalSpaceCode = space ?? string.Empty;
            connectorTypeCode = connector ?? string.Empty;
            flowDirectionCode = direction ?? string.Empty;
            adjacentWorldInteractionId = adjacentWi ?? string.Empty;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(stubCode)
            && !string.IsNullOrWhiteSpace(internalSpaceCode)
            && !string.IsNullOrWhiteSpace(connectorTypeCode)
            && (flowDirectionCode == "Input" || flowDirectionCode == "Output")
            && !string.IsNullOrWhiteSpace(adjacentWorldInteractionId);
    }

    [Serializable]
    public sealed class WI공간모판VisualEntry
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private int revision;
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string summary = string.Empty;
        [SerializeField] private string[] includedWiIds = Array.Empty<string>();
        [SerializeField] private WI공간모판SpaceView[] spaces = Array.Empty<WI공간모판SpaceView>();
        [SerializeField] private WI공간모판RelationView[] relations =
            Array.Empty<WI공간모판RelationView>();
        [SerializeField] private WI공간모판ConnectorView[] connectorStubs =
            Array.Empty<WI공간모판ConnectorView>();
        [SerializeField] private Vector2 minimumSizeMeters;
        [SerializeField] private Vector2 preferredSizeMeters;
        [SerializeField] private Vector2 maximumSizeMeters;
        [SerializeField] private string sourceDefinitionHashSha256 = string.Empty;

        public string StableId => stableId;
        public int Revision => revision;
        public string Title => title;
        public string Summary => summary;
        public IReadOnlyList<string> IncludedWiIds => includedWiIds;
        public IReadOnlyList<WI공간모판SpaceView> Spaces => spaces;
        public IReadOnlyList<WI공간모판RelationView> Relations => relations;
        public IReadOnlyList<WI공간모판ConnectorView> ConnectorStubs => connectorStubs;
        public Vector2 MinimumSizeMeters => minimumSizeMeters;
        public Vector2 PreferredSizeMeters => preferredSizeMeters;
        public Vector2 MaximumSizeMeters => maximumSizeMeters;
        public string SourceDefinitionHashSha256 => sourceDefinitionHashSha256;

        public IEnumerable<WI공간모판CandidateView> UniqueCandidates => spaces
            .SelectMany(value => value.Candidates)
            .GroupBy(value => value.CompositionKey, StringComparer.Ordinal)
            .Select(value => value.First());

        public void Configure(
            string id,
            int sourceRevision,
            string displayTitle,
            string description,
            string[] wiIds,
            WI공간모판SpaceView[] spaceValues,
            WI공간모판RelationView[] relationValues,
            WI공간모판ConnectorView[] connectorValues,
            Vector2 minimum,
            Vector2 preferred,
            Vector2 maximum,
            string sourceHash)
        {
            stableId = id ?? string.Empty;
            revision = sourceRevision;
            title = displayTitle ?? string.Empty;
            summary = description ?? string.Empty;
            includedWiIds = wiIds ?? Array.Empty<string>();
            spaces = spaceValues ?? Array.Empty<WI공간모판SpaceView>();
            relations = relationValues ?? Array.Empty<WI공간모판RelationView>();
            connectorStubs = connectorValues ?? Array.Empty<WI공간모판ConnectorView>();
            minimumSizeMeters = minimum;
            preferredSizeMeters = preferred;
            maximumSizeMeters = maximum;
            sourceDefinitionHashSha256 = sourceHash ?? string.Empty;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(stableId) || revision <= 0
                || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary)
                || includedWiIds.Length == 0 || includedWiIds.Any(string.IsNullOrWhiteSpace)
                || includedWiIds.Distinct(StringComparer.Ordinal).Count() != includedWiIds.Length
                || spaces.Length == 0 || spaces.Any(value => value == null || !value.Validate())
                || spaces.Select(value => value.SpaceCode)
                    .Distinct(StringComparer.Ordinal).Count() != spaces.Length
                || relations.Any(value => value == null || !value.Validate())
                || connectorStubs.Any(value => value == null || !value.Validate())
                || sourceDefinitionHashSha256.Length != 64)
                return false;

            if (minimumSizeMeters.x <= 0f || minimumSizeMeters.y <= 0f
                || preferredSizeMeters.x < minimumSizeMeters.x
                || preferredSizeMeters.y < minimumSizeMeters.y
                || maximumSizeMeters.x < preferredSizeMeters.x
                || maximumSizeMeters.y < preferredSizeMeters.y)
                return false;

            var spaceCodes = spaces.Select(value => value.SpaceCode)
                .ToHashSet(StringComparer.Ordinal);
            return relations.All(value => spaceCodes.Contains(value.FromSpaceCode)
                    && spaceCodes.Contains(value.ToSpaceCode))
                && connectorStubs.All(value => spaceCodes.Contains(value.InternalSpaceCode));
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/WI 공간 모판 Visual Catalog")]
    public sealed class WI공간모판VisualCatalog : ScriptableObject
    {
        public const string HierarchyLevelCode = "H1";
        public const int ExpectedEntryCount = 5;
        public const int ExpectedSpaceCount = 9;
        public const int ExpectedWiCount = 13;
        public const int ExpectedUniqueCandidateCount = 27;

        [SerializeField] private string sourceCatalogRevision = string.Empty;
        [SerializeField] private string sourceCatalogHashSha256 = string.Empty;
        [SerializeField] private string worldInteractionCatalogRevision = string.Empty;
        [SerializeField] private string landscapeGrammarRevision = string.Empty;
        [SerializeField] private string unityCompositionCatalogRevision = string.Empty;
        [SerializeField] private string unityCompositionCatalogHashSha256 = string.Empty;
        [SerializeField] private string syntyBindingRevision = string.Empty;
        [SerializeField] private string syntyBindingHashSha256 = string.Empty;
        [SerializeField] private WI공간모판VisualEntry[] entries =
            Array.Empty<WI공간모판VisualEntry>();
        [SerializeField] private bool presentationOnly = true;

        public string SourceCatalogRevision => sourceCatalogRevision;
        public string SourceCatalogHashSha256 => sourceCatalogHashSha256;
        public string WorldInteractionCatalogRevision => worldInteractionCatalogRevision;
        public string LandscapeGrammarRevision => landscapeGrammarRevision;
        public string UnityCompositionCatalogRevision => unityCompositionCatalogRevision;
        public string UnityCompositionCatalogHashSha256 => unityCompositionCatalogHashSha256;
        public string SyntyBindingRevision => syntyBindingRevision;
        public string SyntyBindingHashSha256 => syntyBindingHashSha256;
        public IReadOnlyList<WI공간모판VisualEntry> Entries => entries;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string catalogRevision,
            string catalogHash,
            string wiCatalogRevision,
            string grammarRevision,
            string compositionRevision,
            string compositionHash,
            string bindingRevision,
            string bindingHash,
            WI공간모판VisualEntry[] values)
        {
            sourceCatalogRevision = catalogRevision ?? string.Empty;
            sourceCatalogHashSha256 = catalogHash ?? string.Empty;
            worldInteractionCatalogRevision = wiCatalogRevision ?? string.Empty;
            landscapeGrammarRevision = grammarRevision ?? string.Empty;
            unityCompositionCatalogRevision = compositionRevision ?? string.Empty;
            unityCompositionCatalogHashSha256 = compositionHash ?? string.Empty;
            syntyBindingRevision = bindingRevision ?? string.Empty;
            syntyBindingHashSha256 = bindingHash ?? string.Empty;
            entries = values ?? Array.Empty<WI공간모판VisualEntry>();
            presentationOnly = true;
        }

        public WI공간모판VisualEntry Resolve(string stableId)
        {
            Validate();
            return entries.SingleOrDefault(value => value.StableId == stableId)
                ?? throw new InvalidOperationException("WiSpatialSeedbedVisualMissing:" + stableId);
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(sourceCatalogRevision)
                || sourceCatalogHashSha256.Length != 64
                || string.IsNullOrWhiteSpace(worldInteractionCatalogRevision)
                || string.IsNullOrWhiteSpace(landscapeGrammarRevision)
                || string.IsNullOrWhiteSpace(unityCompositionCatalogRevision)
                || unityCompositionCatalogHashSha256.Length != 64
                || string.IsNullOrWhiteSpace(syntyBindingRevision)
                || syntyBindingHashSha256.Length != 64
                || entries.Length != ExpectedEntryCount
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Sum(value => value.Spaces.Count) != ExpectedSpaceCount
                || entries.SelectMany(value => value.IncludedWiIds)
                    .Distinct(StringComparer.Ordinal).Count() != ExpectedWiCount
                || entries.SelectMany(value => value.UniqueCandidates)
                    .Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != ExpectedUniqueCandidateCount
                || !presentationOnly)
                throw new InvalidOperationException("WiSpatialSeedbedVisualCatalogInvalid");
        }
    }
}
