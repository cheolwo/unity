using System;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class H5세계배치Codes
    {
        public const string DefinitionSchema = "simulation-world-layout-definition.v1";
        public const string BindingSchema = "simulation-world-grounding-binding.v1";
        public const string ReadinessSchema = "simulation-world-grounding-readiness.v1";
        public const string ParentLocalMeters = "ParentLocalMeters";
        public const string ScenarioLocalMeters = "ScenarioLocalMeters";
        public const string ScenarioRelative = "ScenarioRelative";
        public const string NotApplied = "NotApplied";
        public const string Optional = "Optional";
        public const string Partial = "Partial";
        public const string PhysicalCorridor = "PhysicalCorridor";
        public const string AbstractTravel = "AbstractTravel";
    }

    [Serializable]
    public sealed class H5배치TransformData
    {
        public string CoordinateSpaceCode = string.Empty;
        public double LocalXMeters;
        public double LocalZMeters;
        public double RotationDegrees;
        public string SizeVariantCode = string.Empty;
        public string MirrorCode = string.Empty;
    }

    [Serializable]
    public sealed class H5연결지점PoseData
    {
        public string ConnectorStableId = string.Empty;
        public string CoordinateSpaceCode = string.Empty;
        public double LocalXMeters;
        public double LocalZMeters;
        public double RotationDegrees;
        public double WidthMeters;
        public string DirectionCode = string.Empty;
        public string[] TravelTypeCodes = Array.Empty<string>();
        public string ConnectorPoseHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class H5경관GraphInstanceData
    {
        public string GraphInstanceStableId = string.Empty;
        public string LandscapeGraphStableId = string.Empty;
        public string H3Ref = string.Empty;
        public H5배치TransformData PlacementTransform = new H5배치TransformData();
        public H5연결지점PoseData[] ExternalConnectors = Array.Empty<H5연결지점PoseData>();
        public string SourcePatternHashSha256 = string.Empty;
        public string PlacementHashSha256 = string.Empty;
        public string InstanceHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class H5지역InstanceData
    {
        public string AreaSetInstanceStableId = string.Empty;
        public string BlueprintStableId = string.Empty;
        public string AreaRoleCode = string.Empty;
        public string LoadPolicyCode = string.Empty;
        public H5배치TransformData PlacementTransform = new H5배치TransformData();
        public H5경관GraphInstanceData[] GraphInstances = Array.Empty<H5경관GraphInstanceData>();
        public H5연결지점PoseData[] ExternalConnectors = Array.Empty<H5연결지점PoseData>();
        public string PlacementHashSha256 = string.Empty;
        public string InstanceHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class H5회랑InstanceData
    {
        public string CorridorInstanceStableId = string.Empty;
        public string LandscapeGraphStableId = string.Empty;
        public H5배치TransformData PlacementTransform = new H5배치TransformData();
        public string FromAreaSetInstanceStableId = string.Empty;
        public string FromConnectorStableId = string.Empty;
        public string ToAreaSetInstanceStableId = string.Empty;
        public string ToConnectorStableId = string.Empty;
        public string RelationStableId = string.Empty;
        public H5연결지점PoseData[] ExternalConnectors = Array.Empty<H5연결지점PoseData>();
        public string PlacementHashSha256 = string.Empty;
        public string InstanceHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class H5공간관계Data
    {
        public string RelationStableId = string.Empty;
        public string FromAreaSetInstanceStableId = string.Empty;
        public string ToAreaSetInstanceStableId = string.Empty;
        public string RelationKindCode = string.Empty;
        public string SpatialRealizationCode = string.Empty;
        public string CorridorInstanceStableId = string.Empty;
    }

    [Serializable]
    public sealed class H5세계배치DefinitionData
    {
        public string SchemaVersion = string.Empty;
        public string WorldLayoutStableId = string.Empty;
        public int WorldLayoutRevision;
        public string WorldIntentStableId = string.Empty;
        public string AreaSetNetworkStableId = string.Empty;
        public string CoordinateSpaceCode = string.Empty;
        public string WorldGroundingPolicyCode = string.Empty;
        public H5지역InstanceData[] AreaSetInstances = Array.Empty<H5지역InstanceData>();
        public H5회랑InstanceData[] CorridorInstances = Array.Empty<H5회랑InstanceData>();
        public H5공간관계Data[] Relations = Array.Empty<H5공간관계Data>();
        public string WorldLayoutHashSha256 = string.Empty;
        public bool PresentationOnly;
        public bool IsOperationalState;

        public void Validate()
        {
            if (SchemaVersion != H5세계배치Codes.DefinitionSchema
                || WorldLayoutRevision <= 0
                || CoordinateSpaceCode != H5세계배치Codes.ScenarioLocalMeters
                || WorldGroundingPolicyCode != H5세계배치Codes.Optional
                || AreaSetInstances == null || AreaSetInstances.Length != 4
                || CorridorInstances == null || CorridorInstances.Length != 3
                || Relations == null || Relations.Length != 8
                || WorldLayoutHashSha256 == null || WorldLayoutHashSha256.Length != 64
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("H5WorldLayoutInvalid");
            if (AreaSetInstances.Any(area => area.PlacementTransform == null
                    || area.PlacementTransform.CoordinateSpaceCode != H5세계배치Codes.ScenarioLocalMeters
                    || area.GraphInstances == null
                    || area.GraphInstances.Any(graph => graph.PlacementTransform == null
                        || graph.PlacementTransform.CoordinateSpaceCode != H5세계배치Codes.ParentLocalMeters
                        || graph.ExternalConnectors == null
                        || graph.ExternalConnectors.Any(connector => connector.CoordinateSpaceCode != H5세계배치Codes.ParentLocalMeters))))
                throw new InvalidOperationException("H5WorldLayoutParentCoordinateInvalid");
            if (CorridorInstances.Any(corridor => corridor.PlacementTransform == null
                    || corridor.PlacementTransform.CoordinateSpaceCode != H5세계배치Codes.ScenarioLocalMeters)
                || Relations.Count(relation => relation.SpatialRealizationCode == H5세계배치Codes.PhysicalCorridor) != 3
                || Relations.Where(relation => relation.SpatialRealizationCode == H5세계배치Codes.AbstractTravel)
                    .Any(relation => !string.IsNullOrEmpty(relation.CorridorInstanceStableId)))
                throw new InvalidOperationException("H5WorldLayoutRelationInvalid");
        }
    }

    [Serializable]
    public sealed class H5현실결속BindingData
    {
        public string SchemaVersion = string.Empty;
        public string WorldLayoutStableId = string.Empty;
        public int WorldLayoutRevision;
        public string WorldLayoutHashSha256 = string.Empty;
        public string PlacementAuthorityCode = string.Empty;
        public string WorldGroundingStateCode = string.Empty;
        public string E6AnchorStableId = string.Empty;
        public string GroundingEvidenceHashSha256 = string.Empty;

        public void Validate(H5세계배치DefinitionData definition)
        {
            if (SchemaVersion != H5세계배치Codes.BindingSchema
                || WorldLayoutStableId != definition.WorldLayoutStableId
                || WorldLayoutRevision != definition.WorldLayoutRevision
                || WorldLayoutHashSha256 != definition.WorldLayoutHashSha256
                || PlacementAuthorityCode != H5세계배치Codes.ScenarioRelative
                || WorldGroundingStateCode != H5세계배치Codes.NotApplied
                || !string.IsNullOrEmpty(E6AnchorStableId)
                || !string.IsNullOrEmpty(GroundingEvidenceHashSha256))
                throw new InvalidOperationException("H5WorldGroundingBindingInvalid");
        }
    }

    [Serializable]
    public sealed class H5현실결속준비도Data
    {
        public string SchemaVersion = string.Empty;
        public string WorldLayoutStableId = string.Empty;
        public string GroundingReadinessStateCode = string.Empty;
        public bool AppliesAuthority;

        public void Validate(H5세계배치DefinitionData definition)
        {
            if (SchemaVersion != H5세계배치Codes.ReadinessSchema
                || WorldLayoutStableId != definition.WorldLayoutStableId
                || GroundingReadinessStateCode != H5세계배치Codes.Partial
                || AppliesAuthority)
                throw new InvalidOperationException("H5WorldGroundingReadinessInvalid");
        }
    }

    public readonly struct H5합성Pose
    {
        public H5합성Pose(double x, double z, double rotationDegrees)
        {
            X = x;
            Z = z;
            RotationDegrees = rotationDegrees;
        }
        public double X { get; }
        public double Z { get; }
        public double RotationDegrees { get; }
    }

    public static class H5좌표합성
    {
        public static H5합성Pose Compose(H5합성Pose parent, H5배치TransformData child)
        {
            var radians = parent.RotationDegrees * Math.PI / 180d;
            var x = Math.Cos(radians) * child.LocalXMeters + Math.Sin(radians) * child.LocalZMeters;
            var z = -Math.Sin(radians) * child.LocalXMeters + Math.Cos(radians) * child.LocalZMeters;
            return new H5합성Pose(parent.X + x, parent.Z + z,
                Normalize(parent.RotationDegrees + child.RotationDegrees));
        }

        public static H5합성Pose ProjectToRuntime(H5합성Pose authorityPose,
            double floatingOriginOffsetX, double floatingOriginOffsetZ)
            => new H5합성Pose(authorityPose.X - floatingOriginOffsetX,
                authorityPose.Z - floatingOriginOffsetZ, authorityPose.RotationDegrees);

        private static double Normalize(double value)
        {
            var result = value % 360d;
            return result < 0d ? result + 360d : result;
        }
    }
}
