using System;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 실제E5AreaSetNetworkCodes
    {
        public const string SchemaVersion = "simulation-world-area-set-network.v1";
        public const string ReadinessSchemaVersion =
            "simulation-world-interaction-network-readiness.v1";
        public const string ActualE5 = "ActualE5";
        public const string Persistent = "Persistent";
        public const string OnDemand = "OnDemand";
        public const string PlayerTraversal = "PlayerTraversal";
        public const string CargoLogistics = "CargoLogistics";
        public const string Ready = "Ready";
        public const string ContextBound = "AreaSetContextBound";
        public const string NotSpatiallyApplicable = "NotSpatiallyApplicable";

        public const string NatureAreaSet =
            "area-set:sim:pyeongchang:nature-home.v1";
        public const string FarmAreaSet =
            "area-set:sim:pyeongchang:farm-production.v1";
        public const string HubAreaSet =
            "area-set:sim:pyeongchang:logistics-hub.v1";
        public const string TownAreaSet =
            "area-set:sim:pyeongchang:town-market.v1";
    }

    [Serializable]
    public sealed class 실제E5AreaSetNetworkData
    {
        public string SchemaVersion = string.Empty;
        public string NetworkStableId = string.Empty;
        public int Revision;
        public string Title = string.Empty;
        public string Summary = string.Empty;
        public string CoordinateSpaceCode = string.Empty;
        public string EvidenceStageCode = string.Empty;
        public string DefinitionHashSha256 = string.Empty;
        public string DocumentHashSha256 = string.Empty;
        public string DefinitionStatusCode = string.Empty;
        public 실제E5NetworkAreaData[] AreaSets = Array.Empty<실제E5NetworkAreaData>();
        public 공간LandscapeGraphDescriptorData[] RouteGraphs =
            Array.Empty<공간LandscapeGraphDescriptorData>();
        public 실제E5NetworkRelationData[] Relations =
            Array.Empty<실제E5NetworkRelationData>();
        public bool PresentationOnly;
        public bool IsOperationalState;

        public void Validate()
        {
            if (SchemaVersion != 실제E5AreaSetNetworkCodes.SchemaVersion
                || NetworkStableId != 공간AreaSetLandscapeGraphCodes.ActualE5Network
                || Revision <= 0
                || string.IsNullOrWhiteSpace(Title)
                || CoordinateSpaceCode != 공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters
                || EvidenceStageCode != 실제E5AreaSetNetworkCodes.ActualE5
                || DefinitionHashSha256?.Length != 64
                || DocumentHashSha256?.Length != 64
                || string.IsNullOrWhiteSpace(DefinitionStatusCode)
                || AreaSets == null || RouteGraphs == null || Relations == null
                || AreaSets.Length != 4 || RouteGraphs.Length != 3 || Relations.Length != 8
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("ActualE5AreaSetNetworkInvalid");
            if (AreaSets.Select(value => value.AreaSetStableId)
                    .Distinct(StringComparer.Ordinal).Count() != AreaSets.Length)
                throw new InvalidOperationException("ActualE5AreaSetNetworkAreaDuplicate");
            foreach (var area in AreaSets) area.Validate();
            foreach (var graph in RouteGraphs)
            {
                graph.Validate();
                if (graph.SpatialOwnerKindCode !=
                        공간AreaSetLandscapeGraphCodes.AreaSetNetworkOwner
                    || graph.SpatialOwnerStableId != NetworkStableId)
                    throw new InvalidOperationException("ActualE5RouteGraphOwnerInvalid");
            }
            var areaIds = AreaSets.Select(value => value.AreaSetStableId)
                .ToHashSet(StringComparer.Ordinal);
            var routeIds = RouteGraphs.Select(value => value.LandscapeGraphStableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var relation in Relations) relation.Validate(areaIds, routeIds);
        }
    }

    [Serializable]
    public sealed class 실제E5NetworkAreaData
    {
        public string AreaSetStableId = string.Empty;
        public string AreaRoleCode = string.Empty;
        public string LoadPolicyCode = string.Empty;
        public string DefaultEntryConnectorStableId = string.Empty;
        public int AreaSetRevision;
        public string DefinitionHashSha256 = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(AreaSetStableId)
                || string.IsNullOrWhiteSpace(AreaRoleCode)
                || (LoadPolicyCode != 실제E5AreaSetNetworkCodes.Persistent
                    && LoadPolicyCode != 실제E5AreaSetNetworkCodes.OnDemand)
                || string.IsNullOrWhiteSpace(DefaultEntryConnectorStableId)
                || AreaSetRevision <= 0 || DefinitionHashSha256?.Length != 64)
                throw new InvalidOperationException("ActualE5AreaSetNetworkAreaInvalid");
        }
    }

    [Serializable]
    public sealed class 실제E5NetworkRelationData
    {
        public string RelationStableId = string.Empty;
        public string FromAreaSetStableId = string.Empty;
        public string FromConnectorStableId = string.Empty;
        public string ToAreaSetStableId = string.Empty;
        public string ToConnectorStableId = string.Empty;
        public string RelationKindCode = string.Empty;
        public string DirectionCode = string.Empty;
        public string RouteGraphStableId = string.Empty;
        public string RouteSignature = string.Empty;
        public string[] SourceStableIds = Array.Empty<string>();

        public void Validate(
            System.Collections.Generic.ISet<string> areaIds,
            System.Collections.Generic.ISet<string> routeGraphIds)
        {
            if (string.IsNullOrWhiteSpace(RelationStableId)
                || !areaIds.Contains(FromAreaSetStableId)
                || !areaIds.Contains(ToAreaSetStableId)
                || FromAreaSetStableId == ToAreaSetStableId
                || string.IsNullOrWhiteSpace(FromConnectorStableId)
                || string.IsNullOrWhiteSpace(ToConnectorStableId)
                || (RelationKindCode != 실제E5AreaSetNetworkCodes.PlayerTraversal
                    && RelationKindCode != 실제E5AreaSetNetworkCodes.CargoLogistics)
                || string.IsNullOrWhiteSpace(DirectionCode)
                || (!string.IsNullOrWhiteSpace(RouteGraphStableId)
                    && !routeGraphIds.Contains(RouteGraphStableId))
                || string.IsNullOrWhiteSpace(RouteSignature)
                || SourceStableIds == null)
                throw new InvalidOperationException("ActualE5AreaSetNetworkRelationInvalid");
        }
    }

    [Serializable]
    public sealed class 실제E5InteractionReadinessData
    {
        public string SchemaVersion = string.Empty;
        public string NetworkStableId = string.Empty;
        public int NetworkRevision;
        public string NetworkDefinitionHashSha256 = string.Empty;
        public string BindingCatalogRevision = string.Empty;
        public string BindingCatalogHashSha256 = string.Empty;
        public string OverallStatusCode = string.Empty;
        public 실제E5DirectBindingData[] DirectBindings =
            Array.Empty<실제E5DirectBindingData>();
        public 실제E5ContextBindingData[] ContextualBindings =
            Array.Empty<실제E5ContextBindingData>();
        public 실제E5NonSpatialBindingData[] NonSpatialBindings =
            Array.Empty<실제E5NonSpatialBindingData>();
        public 실제E5TransitionReadinessData[] Transitions =
            Array.Empty<실제E5TransitionReadinessData>();
        public int TotalWorldInteractionCount;
        public bool PresentationOnly;
        public bool IsOperationalState;

        public void Validate()
        {
            if (SchemaVersion != 실제E5AreaSetNetworkCodes.ReadinessSchemaVersion
                || NetworkStableId != 공간AreaSetLandscapeGraphCodes.ActualE5Network
                || NetworkRevision <= 0
                || NetworkDefinitionHashSha256?.Length != 64
                || BindingCatalogHashSha256?.Length != 64
                || OverallStatusCode != 실제E5AreaSetNetworkCodes.Ready
                || DirectBindings == null || DirectBindings.Length != 30
                || ContextualBindings == null || ContextualBindings.Length != 5
                || NonSpatialBindings == null || NonSpatialBindings.Length != 6
                || Transitions == null || TotalWorldInteractionCount != 41
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("ActualE5InteractionReadinessInvalid");
            if (DirectBindings.Any(value => !value.SpatialClosedLoop
                    || value.StatusCode != 실제E5AreaSetNetworkCodes.Ready)
                || ContextualBindings.Any(value =>
                    value.StatusCode != 실제E5AreaSetNetworkCodes.ContextBound)
                || NonSpatialBindings.Any(value => value.StatusCode !=
                    실제E5AreaSetNetworkCodes.NotSpatiallyApplicable))
                throw new InvalidOperationException("ActualE5InteractionReadinessPartial");
        }
    }

    [Serializable]
    public sealed class 실제E5DirectBindingData
    {
        public string WorldInteractionId = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string LandscapeGraphStableId = string.Empty;
        public string H1Ref = string.Empty;
        public string H2Ref = string.Empty;
        public string H3Ref = string.Empty;
        public string StatusCode = string.Empty;
        public bool SpatialClosedLoop;
    }

    [Serializable]
    public sealed class 실제E5ContextBindingData
    {
        public string WorldInteractionId = string.Empty;
        public string ContextStableId = string.Empty;
        public string StatusCode = string.Empty;
    }

    [Serializable]
    public sealed class 실제E5NonSpatialBindingData
    {
        public string WorldInteractionId = string.Empty;
        public string StatusCode = string.Empty;
    }

    [Serializable]
    public sealed class 실제E5TransitionReadinessData
    {
        public string TransitionStableId = string.Empty;
        public string StatusCode = string.Empty;
        public string NetworkRelationStableId = string.Empty;
        public string RouteGraphStableId = string.Empty;
    }

    [Serializable]
    public sealed class 실제E5RegionalCausalityData
    {
        public long Revision;
        public int ThreatScore;
        public int RecoveryScore;
        public int NetPressureModifier;
        public string OutcomeCode = "Normal";

        public void Validate()
        {
            if (Revision < 0 || ThreatScore < 0 || ThreatScore > 12
                || RecoveryScore < 0 || RecoveryScore > 12
                || NetPressureModifier != ThreatScore - RecoveryScore
                || (OutcomeCode != "Normal" && OutcomeCode != "Opportunity"
                    && OutcomeCode != "Threat" && OutcomeCode != "Recovery"))
                throw new InvalidOperationException("ActualE5RegionalCausalityInvalid");
        }
    }
}
