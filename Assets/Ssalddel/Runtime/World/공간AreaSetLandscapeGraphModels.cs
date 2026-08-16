using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공간AreaSetLandscapeGraphCodes
    {
        public const string AreaSetSchemaVersion = "simulation-world-area-set.v1";
        public const string GraphSchemaVersion = "simulation-world-landscape-graph.v2";
        public const string CanonicalAreaSet = "area-set:sim:pyeongchang:farm-hub-town.v1";
        public const string GraphV2 = "GraphV2";
        public const string TileFacadeV1 = "TileFacadeV1";
        public const string Declared = "Declared";
        public const string Adjacent = "Adjacent";
        public const string Connected = "Connected";
        public const string Transition = "Transition";

        public static bool IsKnownRelation(string value)
            => value == Adjacent || value == Connected || value == Transition;
    }

    public enum 공간LandscapeGraphStreamingState
    {
        Unloaded,
        Declared,
        Prepared,
        Active,
        Cached,
    }

    [Serializable]
    public sealed class 공간AreaSetDefinitionData
    {
        public string SchemaVersion = string.Empty;
        public string AreaSetStableId = string.Empty;
        public int Revision;
        public string Title = string.Empty;
        public string Summary = string.Empty;
        public string DefinitionHashSha256 = string.Empty;
        public string DocumentHashSha256 = string.Empty;
        public string[] AreaRefs = Array.Empty<string>();
        public string[] ScenarioRouteRefs = Array.Empty<string>();
        public string[] CompletionAreaRefs = Array.Empty<string>();
        public 공간LandscapeGraphDescriptorData[] LandscapeGraphs =
            Array.Empty<공간LandscapeGraphDescriptorData>();
        public 공간LandscapeGraphRelationData[] GraphRelations =
            Array.Empty<공간LandscapeGraphRelationData>();
        public string DefinitionStatusCode = string.Empty;
        public bool PresentationOnly;
        public bool IsOperationalState;

        public void Validate()
        {
            if (SchemaVersion != 공간AreaSetLandscapeGraphCodes.AreaSetSchemaVersion
                || string.IsNullOrWhiteSpace(AreaSetStableId)
                || Revision <= 0
                || string.IsNullOrWhiteSpace(Title)
                || DefinitionHashSha256 == null || DefinitionHashSha256.Length != 64
                || DocumentHashSha256 == null || DocumentHashSha256.Length != 64
                || AreaRefs == null || ScenarioRouteRefs == null || CompletionAreaRefs == null
                || LandscapeGraphs == null || GraphRelations == null
                || string.IsNullOrWhiteSpace(DefinitionStatusCode)
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("WorldAreaSetDefinitionInvalid");
            if (AreaRefs.Distinct(StringComparer.Ordinal).Count() != AreaRefs.Length
                || ScenarioRouteRefs.Distinct(StringComparer.Ordinal).Count()
                    != ScenarioRouteRefs.Length
                || CompletionAreaRefs.Distinct(StringComparer.Ordinal).Count()
                    != CompletionAreaRefs.Length)
                throw new InvalidOperationException("WorldAreaSetReferenceDuplicate");
            foreach (var graph in LandscapeGraphs) graph.Validate();
            var graphIds = LandscapeGraphs.Select(value => value.LandscapeGraphStableId)
                .ToHashSet(StringComparer.Ordinal);
            if (graphIds.Count != LandscapeGraphs.Length)
                throw new InvalidOperationException("WorldAreaSetGraphDuplicate");
            foreach (var relation in GraphRelations) relation.Validate(graphIds);
        }
    }

    [Serializable]
    public sealed class 공간LandscapeGraphIndexData
    {
        public string SchemaVersion = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string CenterTileKey = string.Empty;
        public int RadiusTiles;
        public 공간LandscapeGraphDescriptorData[] Graphs =
            Array.Empty<공간LandscapeGraphDescriptorData>();
        public string[] CoveredTileKeys = Array.Empty<string>();
        public bool PresentationOnly;

        public void Validate()
        {
            if (SchemaVersion != 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion
                || string.IsNullOrWhiteSpace(AreaSetStableId)
                || !공간TileWindowPlanner.TryParse(CenterTileKey, out _, out _)
                || RadiusTiles < 0 || RadiusTiles > 12
                || Graphs == null || CoveredTileKeys == null || !PresentationOnly)
                throw new InvalidOperationException("WorldLandscapeGraphIndexInvalid");
            foreach (var graph in Graphs) graph.Validate();
        }
    }

    [Serializable]
    public sealed class 공간LandscapeGraphDescriptorData
    {
        public string LandscapeGraphStableId = string.Empty;
        public string GraphRoleCode = string.Empty;
        public int GraphRevision;
        public string DefinitionHashSha256 = string.Empty;
        public string BuildStatusCode = string.Empty;
        public string GraphHashSha256 = string.Empty;
        public 공간LandscapeBoundsData Bounds = new 공간LandscapeBoundsData();
        public string[] AreaRefs = Array.Empty<string>();
        public string[] TileRefs = Array.Empty<string>();
        public string[] ScenarioRouteRefs = Array.Empty<string>();

        public bool CanLoad => BuildStatusCode == 공간LandscapeCompositionCodes.Available
                               || BuildStatusCode == 공간LandscapeCompositionCodes.PartialUnresolved;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LandscapeGraphStableId)
                || string.IsNullOrWhiteSpace(GraphRoleCode) || GraphRevision <= 0
                || DefinitionHashSha256 == null || DefinitionHashSha256.Length != 64
                || string.IsNullOrWhiteSpace(BuildStatusCode)
                || Bounds == null || AreaRefs == null || TileRefs == null || ScenarioRouteRefs == null
                || TileRefs.Any(value => !공간TileWindowPlanner.TryParse(value, out _, out _)))
                throw new InvalidOperationException("WorldLandscapeGraphDescriptorInvalid");
            Bounds.Validate();
            if (TileRefs.Distinct(StringComparer.Ordinal).Count() != TileRefs.Length)
                throw new InvalidOperationException("WorldLandscapeGraphTileDuplicate");
            if (CanLoad && (GraphHashSha256 == null || GraphHashSha256.Length != 64))
                throw new InvalidOperationException("WorldLandscapeGraphDescriptorHashMissing");
        }

        public bool Intersects(ISet<string> tileKeys) => TileRefs.Any(tileKeys.Contains);
    }

    [Serializable]
    public sealed class 공간LandscapeBoundsData
    {
        public double MinEastingMeters;
        public double MinNorthingMeters;
        public double MaxEastingMeters;
        public double MaxNorthingMeters;

        public void Validate()
        {
            if (double.IsNaN(MinEastingMeters) || double.IsInfinity(MinEastingMeters)
                || double.IsNaN(MinNorthingMeters) || double.IsInfinity(MinNorthingMeters)
                || double.IsNaN(MaxEastingMeters) || double.IsInfinity(MaxEastingMeters)
                || double.IsNaN(MaxNorthingMeters) || double.IsInfinity(MaxNorthingMeters)
                || MaxEastingMeters < MinEastingMeters
                || MaxNorthingMeters < MinNorthingMeters)
                throw new InvalidOperationException("WorldLandscapeBoundsInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeGraphRelationData
    {
        public string RelationStableId = string.Empty;
        public string FromGraphStableId = string.Empty;
        public string ToGraphStableId = string.Empty;
        public string RelationCode = string.Empty;
        public 공간LandscapeConnectorPairData ConnectorPair = new 공간LandscapeConnectorPairData();

        public void Validate(ISet<string> graphIds)
        {
            if (string.IsNullOrWhiteSpace(RelationStableId)
                || !graphIds.Contains(FromGraphStableId) || !graphIds.Contains(ToGraphStableId)
                || FromGraphStableId == ToGraphStableId
                || !공간AreaSetLandscapeGraphCodes.IsKnownRelation(RelationCode)
                || ConnectorPair == null)
                throw new InvalidOperationException("WorldLandscapeGraphRelationInvalid");
            ConnectorPair.Validate();
        }
    }

    [Serializable]
    public sealed class 공간LandscapeConnectorPairData
    {
        public string FromConnectorStableId = string.Empty;
        public string ToConnectorStableId = string.Empty;
        public string ConnectorTypeCode = string.Empty;
        public string RouteSignature = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(FromConnectorStableId)
                || string.IsNullOrWhiteSpace(ToConnectorStableId)
                || string.IsNullOrWhiteSpace(ConnectorTypeCode)
                || string.IsNullOrWhiteSpace(RouteSignature))
                throw new InvalidOperationException("WorldLandscapeConnectorPairInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeGraphData
    {
        public string SchemaVersion = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string LandscapeGraphStableId = string.Empty;
        public string GraphBuildStableId = string.Empty;
        public string GraphRoleCode = string.Empty;
        public int GraphRevision;
        public string DefinitionHashSha256 = string.Empty;
        public string GraphHashSha256 = string.Empty;
        public string GrammarRevision = string.Empty;
        public string GrammarHashSha256 = string.Empty;
        public string StatusCode = string.Empty;
        public 공간LandscapeBoundsData Bounds = new 공간LandscapeBoundsData();
        public string[] AreaRefs = Array.Empty<string>();
        public string[] TileRefs = Array.Empty<string>();
        public string[] ScenarioRouteRefs = Array.Empty<string>();
        public 공간LandscapeNodeData[] Nodes = Array.Empty<공간LandscapeNodeData>();
        public 공간LandscapeEdgeData[] Edges = Array.Empty<공간LandscapeEdgeData>();
        public 공간LandscapePlacementData[] Placements = Array.Empty<공간LandscapePlacementData>();
        public 공간LandscapeExternalConnectorData[] ExternalConnectorStubs =
            Array.Empty<공간LandscapeExternalConnectorData>();
        public 공간LandscapeUnresolvedData[] Unresolved = Array.Empty<공간LandscapeUnresolvedData>();
        public bool PresentationOnly;
        public bool IsOperationalState;

        public bool CanAssemble => StatusCode == 공간LandscapeCompositionCodes.Available
                                   || StatusCode == 공간LandscapeCompositionCodes.PartialUnresolved;

        public void Validate()
        {
            if (SchemaVersion != 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion
                || string.IsNullOrWhiteSpace(AreaSetStableId)
                || string.IsNullOrWhiteSpace(LandscapeGraphStableId)
                || string.IsNullOrWhiteSpace(GraphBuildStableId)
                || string.IsNullOrWhiteSpace(GraphRoleCode) || GraphRevision <= 0
                || DefinitionHashSha256 == null || DefinitionHashSha256.Length != 64
                || GraphHashSha256 == null || GraphHashSha256.Length != 64
                || GrammarRevision != 공간LandscapeCompositionCodes.GrammarRevision
                || string.IsNullOrWhiteSpace(StatusCode)
                || Bounds == null || AreaRefs == null || TileRefs == null || ScenarioRouteRefs == null
                || Nodes == null || Edges == null || Placements == null
                || ExternalConnectorStubs == null || Unresolved == null
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("WorldLandscapeGraphInvalid");
            Bounds.Validate();
            var tileSet = TileRefs.ToHashSet(StringComparer.Ordinal);
            if (TileRefs.Any(value => !공간TileWindowPlanner.TryParse(value, out _, out _))
                || Placements.Any(value => !tileSet.Contains(value.OwnerTileKey)))
                throw new InvalidOperationException("WorldLandscapeGraphTileRefInvalid");
            foreach (var node in Nodes) node.Validate();
            foreach (var edge in Edges) edge.Validate();
            foreach (var placement in Placements) placement.Validate(placement.OwnerTileKey);
            foreach (var stub in ExternalConnectorStubs) stub.Validate();
            foreach (var unresolved in Unresolved) unresolved.Validate();
            var nodeIds = Nodes.Select(value => value.NodeStableId).ToHashSet(StringComparer.Ordinal);
            if (Edges.Any(value => !nodeIds.Contains(value.FromNodeStableId)
                                   || !nodeIds.Contains(value.ToNodeStableId)))
                throw new InvalidOperationException("WorldLandscapeGraphCrossGraphNodeRef");
        }

        public 공간LandscapeCompositionTileData ToTileData(string tileKey)
        {
            if (!TileRefs.Contains(tileKey, StringComparer.Ordinal))
                throw new InvalidOperationException("WorldLandscapeGraphTileNotReferenced");
            var placements = Placements.Where(value => value.OwnerTileKey == tileKey).ToArray();
            var nodeIds = placements.Select(value => value.NodeStableId).ToHashSet(StringComparer.Ordinal);
            var placementIds = placements.Select(value => value.PlacementStableId)
                .ToHashSet(StringComparer.Ordinal);
            return new 공간LandscapeCompositionTileData
            {
                SchemaVersion = 공간LandscapeCompositionCodes.SchemaVersion,
                TileKey = tileKey,
                AreaSetStableId = AreaSetStableId,
                GraphBuildStableId = GraphBuildStableId + ":tile:" + tileKey,
                GraphHashSha256 = GraphHashSha256,
                GrammarRevision = GrammarRevision,
                GrammarHashSha256 = GrammarHashSha256,
                StatusCode = placements.Length == 0 ? 공간AreaSetLandscapeGraphCodes.Declared : StatusCode,
                Nodes = Nodes.Where(value => nodeIds.Contains(value.NodeStableId)).ToArray(),
                Edges = Edges.Where(value => nodeIds.Contains(value.FromNodeStableId)
                                             && nodeIds.Contains(value.ToNodeStableId)).ToArray(),
                Placements = placements,
                ExternalConnectorStubs = ExternalConnectorStubs.Where(value =>
                    placementIds.Contains(value.PlacementStableId)).ToArray(),
                Unresolved = Unresolved,
                PresentationOnly = true,
                IsOperationalState = false,
            };
        }
    }

}
