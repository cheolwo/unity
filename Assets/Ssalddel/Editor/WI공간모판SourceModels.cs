using System;
using Newtonsoft.Json;

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    internal sealed class WI공간모판SourceCatalog
    {
        [JsonProperty("revision")] public string Revision = string.Empty;
        [JsonProperty("worldInteractionCatalogRevision")] public string WorldInteractionCatalogRevision = string.Empty;
        [JsonProperty("landscapeGrammarRevision")] public string LandscapeGrammarRevision = string.Empty;
        [JsonProperty("definitionRefs")] public string[] DefinitionRefs = Array.Empty<string>();
        [JsonProperty("presentationOnly")] public bool PresentationOnly;
    }

    [Serializable]
    internal sealed class WI공간모판SourceDefinition
    {
        [JsonProperty("stableId")] public string StableId = string.Empty;
        [JsonProperty("revision")] public int Revision;
        [JsonProperty("title")] public string Title = string.Empty;
        [JsonProperty("summary")] public string Summary = string.Empty;
        [JsonProperty("includedWiIds")] public string[] IncludedWiIds = Array.Empty<string>();
        [JsonProperty("internalSpaces")] public WI공간모판SourceSpace[] InternalSpaces = Array.Empty<WI공간모판SourceSpace>();
        [JsonProperty("internalRelations")] public WI공간모판SourceRelation[] InternalRelations = Array.Empty<WI공간모판SourceRelation>();
        [JsonProperty("externalConnectorStubs")] public WI공간모판SourceConnector[] ExternalConnectorStubs = Array.Empty<WI공간모판SourceConnector>();
        [JsonProperty("transformConstraint")] public WI공간모판SourceTransform TransformConstraint = new();
        [JsonProperty("reviewStatusCode")] public string ReviewStatusCode = string.Empty;
        [JsonProperty("presentationOnly")] public bool PresentationOnly;
        [JsonProperty("isOperationalState")] public bool IsOperationalState;
    }

    [Serializable]
    internal sealed class WI공간모판SourceSpace
    {
        [JsonProperty("spaceCode")] public string SpaceCode = string.Empty;
        [JsonProperty("spatialRoleCode")] public string SpatialRoleCode = string.Empty;
        [JsonProperty("capabilityCodes")] public string[] CapabilityCodes = Array.Empty<string>();
        [JsonProperty("baseCapacities")] public WI공간모판SourceCapacity[] BaseCapacities = Array.Empty<WI공간모판SourceCapacity>();
        [JsonProperty("allowedLandscapeCompositionKeys")] public string[] AllowedLandscapeCompositionKeys = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class WI공간모판SourceCapacity
    {
        [JsonProperty("capacityCode")] public string CapacityCode = string.Empty;
        [JsonProperty("quantity")] public int Quantity;
        [JsonProperty("unitCode")] public string UnitCode = string.Empty;
    }

    [Serializable]
    internal sealed class WI공간모판SourceRelation
    {
        [JsonProperty("relationCode")] public string RelationCode = string.Empty;
        [JsonProperty("fromSpaceCode")] public string FromSpaceCode = string.Empty;
        [JsonProperty("toSpaceCode")] public string ToSpaceCode = string.Empty;
        [JsonProperty("connectorTypeCode")] public string ConnectorTypeCode = string.Empty;
    }

    [Serializable]
    internal sealed class WI공간모판SourceConnector
    {
        [JsonProperty("stubCode")] public string StubCode = string.Empty;
        [JsonProperty("internalSpaceCode")] public string InternalSpaceCode = string.Empty;
        [JsonProperty("connectorTypeCode")] public string ConnectorTypeCode = string.Empty;
        [JsonProperty("flowDirectionCode")] public string FlowDirectionCode = string.Empty;
        [JsonProperty("adjacentWorldInteractionId")] public string AdjacentWorldInteractionId = string.Empty;
    }

    [Serializable]
    internal sealed class WI공간모판SourceTransform
    {
        [JsonProperty("minimumWidthMeters")] public float MinimumWidthMeters;
        [JsonProperty("minimumDepthMeters")] public float MinimumDepthMeters;
        [JsonProperty("preferredWidthMeters")] public float PreferredWidthMeters;
        [JsonProperty("preferredDepthMeters")] public float PreferredDepthMeters;
        [JsonProperty("maximumWidthMeters")] public float MaximumWidthMeters;
        [JsonProperty("maximumDepthMeters")] public float MaximumDepthMeters;
    }

    [Serializable]
    internal sealed class WI공간모판SourceReceipt
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("sourceProject")] public string SourceProject = string.Empty;
        [JsonProperty("sourceRelativeRoot")] public string SourceRelativeRoot = string.Empty;
        [JsonProperty("catalogRevision")] public string CatalogRevision = string.Empty;
        [JsonProperty("files")] public WI공간모판SourceReceiptFile[] Files = Array.Empty<WI공간모판SourceReceiptFile>();
        [JsonProperty("presentationOnly")] public bool PresentationOnly;
    }

    [Serializable]
    internal sealed class WI공간모판SourceReceiptFile
    {
        [JsonProperty("relativePath")] public string RelativePath = string.Empty;
        [JsonProperty("sha256")] public string Sha256 = string.Empty;
    }
}
