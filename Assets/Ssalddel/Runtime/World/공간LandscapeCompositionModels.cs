using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공간LandscapeCompositionCodes
    {
        public const string SchemaVersion = "simulation-world-landscape-composition.v1";
        public const string GrammarRevision = "pyeongchang-landscape-grammar.v1";
        public const string Available = "Available";
        public const string WaitingForSpatialArtifact = "WaitingForSpatialArtifact";
        public const string WaitingForGrammarManifest = "WaitingForGrammarManifest";
        public const string PartialUnresolved = "PartialUnresolved";
        public const string CatalogMismatch = "CatalogMismatch";
    }

    [Serializable]
    public sealed class 공간LandscapeCompositionTileData
    {
        public string SchemaVersion = string.Empty;
        public string TileKey = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string GraphBuildStableId = string.Empty;
        public string GraphHashSha256 = string.Empty;
        public string GrammarRevision = string.Empty;
        public string GrammarHashSha256 = string.Empty;
        public string StatusCode = string.Empty;
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
            if (SchemaVersion != 공간LandscapeCompositionCodes.SchemaVersion
                || !공간TileWindowPlanner.TryParse(TileKey, out _, out _)
                || string.IsNullOrWhiteSpace(AreaSetStableId)
                || string.IsNullOrWhiteSpace(GraphBuildStableId)
                || GrammarRevision != 공간LandscapeCompositionCodes.GrammarRevision
                || string.IsNullOrWhiteSpace(StatusCode)
                || Nodes == null || Edges == null || Placements == null
                || ExternalConnectorStubs == null || Unresolved == null
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("WorldLandscapeCompositionTileInvalid");

            if (CanAssemble && (GraphHashSha256 == null || GraphHashSha256.Length != 64
                    || GrammarHashSha256 == null || GrammarHashSha256.Length != 64
                    || Placements.Length == 0))
                throw new InvalidOperationException("WorldLandscapeCompositionAvailableInvalid");
            if (!CanAssemble && Placements.Length > 0)
                throw new InvalidOperationException("WaitingLandscapeCompositionMustNotPlace");

            foreach (var node in Nodes) node.Validate();
            foreach (var edge in Edges) edge.Validate();
            foreach (var placement in Placements) placement.Validate(TileKey);
            foreach (var stub in ExternalConnectorStubs) stub.Validate();
            foreach (var unresolved in Unresolved) unresolved.Validate();
            if (Nodes.Select(value => value.NodeStableId).Distinct(StringComparer.Ordinal).Count()
                    != Nodes.Length
                || Placements.Select(value => value.PlacementStableId)
                    .Distinct(StringComparer.Ordinal).Count() != Placements.Length)
                throw new InvalidOperationException("WorldLandscapeCompositionStableIdDuplicate");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeNodeData
    {
        public string NodeStableId = string.Empty;
        public string ParentNodeStableId = string.Empty;
        public string NodeKindCode = string.Empty;
        public string SemanticCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public double CenterEastingMeters;
        public double CenterNorthingMeters;
        public double WidthMeters;
        public double DepthMeters;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(NodeStableId)
                || string.IsNullOrWhiteSpace(NodeKindCode)
                || string.IsNullOrWhiteSpace(SemanticCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode)
                || WidthMeters <= 0d || DepthMeters <= 0d)
                throw new InvalidOperationException("WorldLandscapeNodeInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeEdgeData
    {
        public string EdgeStableId = string.Empty;
        public string FromNodeStableId = string.Empty;
        public string RelationCode = string.Empty;
        public string ToNodeStableId = string.Empty;
        public string ConnectorTypeCode = string.Empty;
        public string EvidenceKindCode = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(EdgeStableId)
                || string.IsNullOrWhiteSpace(FromNodeStableId)
                || string.IsNullOrWhiteSpace(RelationCode)
                || string.IsNullOrWhiteSpace(ToNodeStableId)
                || string.IsNullOrWhiteSpace(EvidenceKindCode))
                throw new InvalidOperationException("WorldLandscapeEdgeInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapePlacementData
    {
        public string PlacementStableId = string.Empty;
        public string NodeStableId = string.Empty;
        public string OwnerTileKey = string.Empty;
        public string CompositionKey = string.Empty;
        public string TopologyCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public double EastingMeters;
        public double NorthingMeters;
        public double PhysicalElevationMeters;
        public double RotationDegrees;
        public bool Mirrored;
        public int DeterministicSeed;
        public double FootprintWidthMeters;
        public double FootprintDepthMeters;
        public bool PresentationOnly;

        public void Validate(string tileKey)
        {
            if (string.IsNullOrWhiteSpace(PlacementStableId)
                || string.IsNullOrWhiteSpace(NodeStableId)
                || OwnerTileKey != tileKey
                || string.IsNullOrWhiteSpace(CompositionKey)
                || CompositionKey.Contains("/") || CompositionKey.Contains("\\")
                || string.IsNullOrWhiteSpace(TopologyCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode)
                || FootprintWidthMeters <= 0d || FootprintDepthMeters <= 0d
                || !PresentationOnly)
                throw new InvalidOperationException("WorldLandscapePlacementInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeExternalConnectorData
    {
        public string StubStableId = string.Empty;
        public string PlacementStableId = string.Empty;
        public string NeighborTileKey = string.Empty;
        public string ConnectorTypeCode = string.Empty;
        public string RouteSignature = string.Empty;
        public string DirectionCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public double WorldEastingMeters;
        public double WorldNorthingMeters;
        public double WidthMeters;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(StubStableId)
                || string.IsNullOrWhiteSpace(PlacementStableId)
                || !공간TileWindowPlanner.TryParse(NeighborTileKey, out _, out _)
                || string.IsNullOrWhiteSpace(ConnectorTypeCode)
                || string.IsNullOrWhiteSpace(RouteSignature)
                || string.IsNullOrWhiteSpace(DirectionCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode))
                throw new InvalidOperationException("WorldLandscapeExternalConnectorInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LandscapeUnresolvedData
    {
        public string UnresolvedStableId = string.Empty;
        public string NodeStableId = string.Empty;
        public string ReasonCode = string.Empty;
        public string RequiredSemanticCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public string Detail = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UnresolvedStableId)
                || string.IsNullOrWhiteSpace(ReasonCode)
                || string.IsNullOrWhiteSpace(RequiredSemanticCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode))
                throw new InvalidOperationException("WorldLandscapeUnresolvedInvalid");
        }
    }

    public interface I공간TileLandscapeCompositionRepository
    {
        Task<공간LandscapeCompositionTileData> LoadLandscapeCompositionsAsync(
            string tileKey,
            CancellationToken cancellationToken);
    }
}
