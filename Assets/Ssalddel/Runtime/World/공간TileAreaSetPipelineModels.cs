using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공간TileLevelCodes
    {
        public const int Overview = 0;
        public const int Region = 1;
        public const int Task = 2;

        public static int SizeMeters(int level) => level switch
        {
            Overview => 8000,
            Region => 2000,
            Task => 500,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }

    public static class 공간LayerCodes
    {
        public const string LegalBoundary = "legal-boundary";
        public const string Elevation = "elevation";
        public const string LandCover = "land-cover";
        public const string Road = "road";
        public const string Slope = "slope";
        public const string WaterMask = "water-mask";
        public const string Contour = "contour";
        public const string TerrainMesh = "terrain-mesh";
        public const string PlacementMask = "placement-mask";

        public static readonly string[] All =
        {
            LegalBoundary, Elevation, LandCover, Road, Slope, WaterMask,
            Contour, TerrainMesh, PlacementMask,
        };
    }

    public static class 공간CoverageStatusCodes
    {
        public const string Complete = "CompleteCoverage";
        public const string Partial = "PartialCoverage";
        public const string Missing = "Missing";
    }

    public static class 공간EvidenceKindCodes
    {
        public const string OfficialDomestic = "OfficialDomestic";
        public const string AuthoritativeInternational = "AuthoritativeInternational";
        public const string StatisticallyAllocated = "StatisticallyAllocated";
        public const string ScenarioDerived = "ScenarioDerived";
        public const string PresentationDerived = "PresentationDerived";
    }

    public static class 공간MeaningConfidenceCodes
    {
        public const string Observed = "Observed";
        public const string Derived = "Derived";
        public const string StatisticallyAllocated = "StatisticallyAllocated";
        public const string Scenario = "Scenario";
        public const string Decorative = "Decorative";
    }

    public static class WorldBuildValidationStageCodes
    {
        public const string SourceMetadata = "source-metadata";
        public const string PhysicalVisualElevation = "physical-visual-elevation";
        public const string MeaningConfidence = "meaning-confidence";
        public const string TileHalo = "tile-halo";
        public const string AllocationCompositionSeparation = "allocation-composition-separation";
        public const string VisualPlacementCapability = "visual-placement-capability";
        public const string ReferenceTile = "reference-tile";
        public const string RenderingCost = "rendering-cost";
        public const string FinalVisualAssetBinding = "final-visual-asset-binding";

        public static readonly string[] All =
        {
            SourceMetadata, PhysicalVisualElevation, MeaningConfidence, TileHalo,
            AllocationCompositionSeparation, VisualPlacementCapability,
            ReferenceTile, RenderingCost, FinalVisualAssetBinding,
        };
    }

    public static class WorldBuildValidationStatusCodes
    {
        public const string ContractReady = "ContractReady";
        public const string WaitingForSpatialArtifact = "WaitingForSpatialArtifact";
        public const string RequiresEditorEvidence = "RequiresEditorEvidence";
    }

    public static class World경관완결단계Codes
    {
        public const string SourceArtifacts = "source-artifacts";
        public const string PhysicalSpace = "physical-space";
        public const string SpatialMeaning = "spatial-meaning";
        public const string ScenarioRules = "scenario-rules";
        public const string LandscapePlan = "landscape-plan";
        public const string UiPlan = "ui-plan";
        public const string UnityRuntime = "unity-runtime";
        public const string CompletionValidation = "completion-validation";

        public static readonly string[] All =
        {
            SourceArtifacts, PhysicalSpace, SpatialMeaning, ScenarioRules,
            LandscapePlan, UiPlan, UnityRuntime, CompletionValidation,
        };
    }

    public static class WorldSpatialUnitKindCodes
    {
        public const string Tile = "Tile";
        public const string Area = "Area";
        public const string AreaSet = "AreaSet";
    }

    public static class WorldAreaKindCodes
    {
        public const string LegalRegion = "LegalRegion";
        public const string Farm = "Farm";
        public const string Hub = "Hub";
        public const string Town = "Town";
    }

    public static class WorldAreaLinkKindCodes
    {
        public const string ScenarioRoute = "ScenarioRoute";
        public const string OfficialRoad = "OfficialRoad";
    }

    public static class 토지피복CompositionTargetCodes
    {
        public const string RicePaddy = "rice-paddy";
        public const string DryField = "dry-field";
        public const string Greenhouse = "greenhouse";
        public const string Orchard = "orchard";
        public const string BroadleafForest = "broadleaf-forest";
        public const string ConiferForest = "conifer-forest";
        public const string MixedForest = "mixed-forest";
        public const string InlandWater = "inland-water";
        public const string BareGround = "bare-ground";
    }

    public static class 토지피복CandidateGroupCodes
    {
        public const string Agriculture = "agriculture";
        public const string Forest = "forest";
        public const string Water = "water";
        public const string BareGround = "bare-ground";
    }

    [Serializable]
    public sealed class 공간BoundsData
    {
        public double MinEasting;
        public double MinNorthing;
        public double MaxEasting;
        public double MaxNorthing;

        public double WidthMeters => MaxEasting - MinEasting;
        public double HeightMeters => MaxNorthing - MinNorthing;

        public bool Validate() => MinEasting < MaxEasting && MinNorthing < MaxNorthing;
    }

    [Serializable]
    public sealed class 공간TileKey
    {
        public string CrsCode = "EPSG:5186";
        public int Level;
        public int X;
        public int Y;

        public string StableId => string.Concat(
            "kr5186:l", Level.ToString(CultureInfo.InvariantCulture), ":",
            X.ToString(CultureInfo.InvariantCulture), ":",
            Y.ToString(CultureInfo.InvariantCulture));

        public int SizeMeters => 공간TileLevelCodes.SizeMeters(Level);

        public 공간BoundsData Bounds => new()
        {
            MinEasting = (double)X * SizeMeters,
            MinNorthing = (double)Y * SizeMeters,
            MaxEasting = (double)(X + 1) * SizeMeters,
            MaxNorthing = (double)(Y + 1) * SizeMeters,
        };

        public static 공간TileKey FromCoordinates(int level, double easting, double northing)
        {
            var size = 공간TileLevelCodes.SizeMeters(level);
            return new 공간TileKey
            {
                Level = level,
                X = (int)Math.Floor(easting / size),
                Y = (int)Math.Floor(northing / size),
            };
        }

        public bool Validate() => CrsCode == "EPSG:5186" && Level is >= 0 and <= 2;
    }

    [Serializable]
    public sealed class 공간SourceSnapshotData
    {
        public string SourceSnapshotId = string.Empty;
        public string SourceName = string.Empty;
        public string SourceVintage = string.Empty;
        public string SourceSha256 = string.Empty;
        public string CrsCode = string.Empty;
        public double HorizontalResolutionMeters;
        public string NoDataValue = string.Empty;
        public string HeightUnit = string.Empty;
        public string VerticalReference = string.Empty;
        public string EvidenceKind = string.Empty;
        public string SemanticResolutionCode = string.Empty;

        public bool Validate() => !string.IsNullOrWhiteSpace(SourceSnapshotId)
            && SourceSha256.Length == 64
            && CrsCode == "EPSG:5186"
            && HorizontalResolutionMeters >= 0d
            && !string.IsNullOrWhiteSpace(NoDataValue)
            && !string.IsNullOrWhiteSpace(EvidenceKind)
            && !string.IsNullOrWhiteSpace(SemanticResolutionCode);
    }

    [Serializable]
    public sealed class PhysicalElevationProfile
    {
        public string SourceSnapshotId = string.Empty;
        public string HeightUnit = "m";
        public string VerticalReference = string.Empty;
        public string NoDataValue = string.Empty;
        public bool UsedForSlope = true;
        public bool UsedForPlacementEligibility = true;
        public bool UsedForHydrology = true;
    }

    [Serializable]
    public sealed class VisualElevationProfile
    {
        public double HeightExaggeration = 1d;
        public double VisualBaseOffset;
        public bool PresentationOnly = true;

        public double Apply(double physicalElevationMeters)
            => VisualBaseOffset + physicalElevationMeters * HeightExaggeration;
    }

    [Serializable]
    public sealed class 공간TileGenerationProfile
    {
        public int OverviewHaloMeters = 300;
        public int RegionHaloMeters = 150;
        public int TaskHaloMeters = 60;
        public string SeedStrategy = "world-coordinate-hash";

        public int HaloMeters(int level) => level switch
        {
            공간TileLevelCodes.Overview => OverviewHaloMeters,
            공간TileLevelCodes.Region => RegionHaloMeters,
            공간TileLevelCodes.Task => TaskHaloMeters,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }

    [Serializable]
    public sealed class 공간LayerTileManifest
    {
        public 공간TileKey TileKey = new();
        public string LayerCode = string.Empty;
        public string SourceSnapshotId = string.Empty;
        public string SourceHash = string.Empty;
        public string RuleRevision = string.Empty;
        public string RecipeHash = string.Empty;
        public string CompositionProfileHash = string.Empty;
        public string Fingerprint = string.Empty;
        public string CoverageStatusCode = 공간CoverageStatusCodes.Missing;
        public string EvidenceKind = string.Empty;
        public string SemanticResolutionCode = string.Empty;
        public string ArtifactReference = string.Empty;

        public string CalculateFingerprint() => 공간PipelineHash.Sha256(string.Join("|",
            TileKey.StableId, LayerCode, SourceHash, RuleRevision,
            RecipeHash, CompositionProfileHash));

        public bool Validate() => TileKey != null && TileKey.Validate()
            && 공간LayerCodes.All.Contains(LayerCode, StringComparer.Ordinal)
            && SourceHash.Length == 64
            && Fingerprint == CalculateFingerprint()
            && !string.IsNullOrWhiteSpace(CoverageStatusCode)
            && !string.IsNullOrWhiteSpace(EvidenceKind);
    }

    [Serializable]
    public sealed class 토지피복CompositionTarget
    {
        public string TargetCode = string.Empty;
        public string CandidateGroupCode = string.Empty;
        public string[] SourceCategoryCodes = Array.Empty<string>();
        public double TargetAreaSquareKm;
        public double TargetAreaRatio;
        public string[] AllowedLandCoverCodes = Array.Empty<string>();
        public string[] AllowedVisualKeys = Array.Empty<string>();
        public string ClusterStrategy = string.Empty;
        public int OverviewBudget;
        public int RegionBudget;
        public int TaskBudget;
        public int MinimumVisibleCount;
        public double MaximumVisualShare = .4d;
        public string EvidenceKind = 공간EvidenceKindCodes.StatisticallyAllocated;
        public string MeaningConfidenceCode = 공간MeaningConfidenceCodes.StatisticallyAllocated;
        public double Tolerance = .05d;

        public bool Validate(double totalAreaSquareKm) => !string.IsNullOrWhiteSpace(TargetCode)
            && !string.IsNullOrWhiteSpace(CandidateGroupCode)
            && TargetAreaSquareKm > 0d
            && Math.Abs(TargetAreaRatio - TargetAreaSquareKm / totalAreaSquareKm) < .000001d
            && SourceCategoryCodes.Length > 0
            && AllowedLandCoverCodes.Length > 0
            && AllowedVisualKeys.Length > 0
            && OverviewBudget >= 0 && RegionBudget >= 1 && TaskBudget >= 3
            && MinimumVisibleCount >= 1
            && MaximumVisualShare is > 0d and <= .4d
            && MeaningConfidenceCode == 공간MeaningConfidenceCodes.StatisticallyAllocated
            && Tolerance is > 0d and <= .05d;
    }

    [Serializable]
    public sealed class 토지피복CompositionProfile
    {
        public string ProfileStableId = string.Empty;
        public string SourceSnapshotId = string.Empty;
        public string RuleRevision = string.Empty;
        public double TotalAreaSquareKm;
        public int OverviewTileBudget = 8;
        public int RegionTileBudget = 32;
        public int TaskTileBudget = 120;
        public 토지피복CompositionTarget[] Targets = Array.Empty<토지피복CompositionTarget>();

        public string CalculateHash() => 공간PipelineHash.Sha256(string.Join("|",
            ProfileStableId, SourceSnapshotId, RuleRevision,
            TotalAreaSquareKm.ToString("0.####", CultureInfo.InvariantCulture),
            string.Join(";", Targets.OrderBy(value => value.TargetCode, StringComparer.Ordinal)
                .Select(value => string.Join(",", value.TargetCode,
                    value.TargetAreaSquareKm.ToString("0.####", CultureInfo.InvariantCulture),
                    value.OverviewBudget, value.RegionBudget, value.TaskBudget)))));

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProfileStableId)
                || TotalAreaSquareKm <= 0d
                || Targets.Length == 0
                || Targets.Select(value => value.TargetCode).Distinct(StringComparer.Ordinal).Count()
                    != Targets.Length
                || Targets.Any(value => !value.Validate(TotalAreaSquareKm))
                || Targets.Sum(value => value.TargetAreaSquareKm) > TotalAreaSquareKm)
                throw new InvalidOperationException("LandCoverCompositionProfileInvalid");
        }
    }

    [Serializable]
    public sealed class 토지피복CandidateGroupData
    {
        public string CandidateGroupCode = string.Empty;
        public double CandidateAreaSquareKm;
    }

    [Serializable]
    public sealed class 토지피복AllocationResult
    {
        public string TargetCode = string.Empty;
        public double TargetAreaSquareKm;
        public double CandidateShareSquareKm;
        public double AllocatedAreaSquareKm;
        public double UnresolvedTargetAreaSquareKm;
        public string EvidenceKind = 공간EvidenceKindCodes.StatisticallyAllocated;
        public string MeaningConfidenceCode = 공간MeaningConfidenceCodes.StatisticallyAllocated;
    }

    [Serializable]
    public sealed class LandscapeCompositionItem
    {
        public string TargetCode = string.Empty;
        public double AllocatedAreaSquareKm;
        public int OverviewInstanceBudget;
        public int RegionInstanceBudget;
        public int TaskInstanceBudget;
        public string[] AllowedVisualKeys = Array.Empty<string>();
        public string MeaningConfidenceCode = 공간MeaningConfidenceCodes.StatisticallyAllocated;
    }

    [Serializable]
    public sealed class LandscapeCompositionPlan
    {
        public string PlanStableId = string.Empty;
        public string SourceAllocationHash = string.Empty;
        public string RuleRevision = string.Empty;
        public LandscapeCompositionItem[] Items = Array.Empty<LandscapeCompositionItem>();
    }

    public static class 토지피복CompositionAllocator
    {
        public static 토지피복AllocationResult[] Allocate(
            토지피복CompositionProfile profile,
            IReadOnlyList<토지피복CandidateGroupData> candidateGroups)
        {
            profile.Validate();
            var candidates = candidateGroups.ToDictionary(
                value => value.CandidateGroupCode,
                value => Math.Max(0d, value.CandidateAreaSquareKm),
                StringComparer.Ordinal);
            var results = new List<토지피복AllocationResult>();

            foreach (var group in profile.Targets.GroupBy(value => value.CandidateGroupCode))
            {
                candidates.TryGetValue(group.Key, out var capacity);
                var targetTotal = group.Sum(value => value.TargetAreaSquareKm);
                var allocationScale = targetTotal <= 0d
                    ? 0d
                    : Math.Min(1d, capacity / targetTotal);
                foreach (var target in group.OrderBy(value => value.TargetCode, StringComparer.Ordinal))
                {
                    var allocated = target.TargetAreaSquareKm * allocationScale;
                    results.Add(new 토지피복AllocationResult
                    {
                        TargetCode = target.TargetCode,
                        TargetAreaSquareKm = target.TargetAreaSquareKm,
                        CandidateShareSquareKm = targetTotal <= 0d
                            ? 0d : capacity * target.TargetAreaSquareKm / targetTotal,
                        AllocatedAreaSquareKm = allocated,
                        UnresolvedTargetAreaSquareKm =
                            Math.Max(0d, target.TargetAreaSquareKm - allocated),
                    });
                }
            }
            return results.ToArray();
        }

        public static void ApplyVisualBudgets(토지피복CompositionProfile profile)
        {
            Apply(profile.Targets, profile.OverviewTileBudget, 0,
                (target, value) => target.OverviewBudget = value);
            Apply(profile.Targets, profile.RegionTileBudget, 1,
                (target, value) => target.RegionBudget = value);
            Apply(profile.Targets, profile.TaskTileBudget, 3,
                (target, value) => target.TaskBudget = value);
        }

        private static void Apply(
            IReadOnlyList<토지피복CompositionTarget> targets,
            int totalBudget,
            int minimum,
            Action<토지피복CompositionTarget, int> assign)
        {
            var weights = targets.Select(value => Math.Sqrt(value.TargetAreaRatio)).ToArray();
            var weightTotal = weights.Sum();
            var values = weights.Select(value => Math.Max(minimum,
                (int)Math.Floor(totalBudget * value / weightTotal))).ToArray();
            var caps = targets.Select(value => Math.Max(minimum,
                (int)Math.Floor(totalBudget * value.MaximumVisualShare))).ToArray();
            for (var index = 0; index < values.Length; index++)
                values[index] = Math.Min(values[index], caps[index]);

            while (values.Sum() < totalBudget)
            {
                var candidate = Enumerable.Range(0, values.Length)
                    .Where(index => values[index] < caps[index])
                    .OrderByDescending(index => weights[index] / (values[index] + 1d))
                    .ThenBy(index => targets[index].TargetCode, StringComparer.Ordinal)
                    .DefaultIfEmpty(-1)
                    .First();
                if (candidate < 0)
                    break;
                values[candidate]++;
            }

            while (values.Sum() > totalBudget)
            {
                var candidate = Enumerable.Range(0, values.Length)
                    .Where(index => values[index] > minimum)
                    .OrderBy(index => weights[index] / values[index])
                    .ThenByDescending(index => targets[index].TargetCode, StringComparer.Ordinal)
                    .DefaultIfEmpty(-1)
                    .First();
                if (candidate < 0)
                    break;
                values[candidate]--;
            }

            for (var index = 0; index < targets.Count; index++)
                assign(targets[index], values[index]);
        }
    }

    [Serializable]
    public sealed class WorldAreaDefinition
    {
        public string AreaStableId = string.Empty;
        public string AreaKindCode = string.Empty;
        public string LegalRegionStableId = string.Empty;
        public string ScenarioRoleCode = string.Empty;
        public string EvidenceKind = string.Empty;
        public 공간TileKey[] TileReferences = Array.Empty<공간TileKey>();
    }

    [Serializable]
    public sealed class WorldAreaLinkDefinition
    {
        public string LinkStableId = string.Empty;
        public string FromAreaStableId = string.Empty;
        public string ToAreaStableId = string.Empty;
        public string LinkKindCode = WorldAreaLinkKindCodes.ScenarioRoute;
        public string EvidenceKind = 공간EvidenceKindCodes.ScenarioDerived;
    }

    [Serializable]
    public sealed class WorldAreaSetDefinition
    {
        public string AreaSetStableId = string.Empty;
        public string[] AreaReferences = Array.Empty<string>();
        public string[] LinkReferences = Array.Empty<string>();
        public 공간TileKey OriginTile = new();
    }

    [Serializable]
    public sealed class World경관완결단계
    {
        public string StageCode = string.Empty;
        public string StatusCode = WorldBuildValidationStatusCodes.ContractReady;
        public string EvidenceReference = string.Empty;
    }

    [Serializable]
    public sealed class World경관완결영역Definition
    {
        public string CompletionAreaStableId = string.Empty;
        public string AreaStableId = string.Empty;
        public string LegalRegionStableId = string.Empty;
        public 공간BoundsData Bounds = new();
        public 공간TileKey[] TaskTileReferences = Array.Empty<공간TileKey>();
        public string ReferenceTileStableId = string.Empty;
        public World경관완결단계[] VerticalStages = Array.Empty<World경관완결단계>();
        public string CompletionHash = string.Empty;

        public string CalculateHash() => 공간PipelineHash.Sha256(string.Join("|",
            CompletionAreaStableId, AreaStableId, LegalRegionStableId,
            Bounds.MinEasting.ToString("0.###", CultureInfo.InvariantCulture),
            Bounds.MinNorthing.ToString("0.###", CultureInfo.InvariantCulture),
            Bounds.MaxEasting.ToString("0.###", CultureInfo.InvariantCulture),
            Bounds.MaxNorthing.ToString("0.###", CultureInfo.InvariantCulture),
            ReferenceTileStableId,
            string.Join(";", TaskTileReferences.OrderBy(value => value.StableId, StringComparer.Ordinal)
                .Select(value => value.StableId)),
            string.Join(";", VerticalStages.Select(value =>
                value.StageCode + ":" + value.StatusCode + ":" + value.EvidenceReference))));

        public bool Validate()
        {
            var xValues = TaskTileReferences.Select(value => value.X)
                .Distinct().OrderBy(value => value).ToArray();
            var yValues = TaskTileReferences.Select(value => value.Y)
                .Distinct().OrderBy(value => value).ToArray();
            return !string.IsNullOrWhiteSpace(CompletionAreaStableId)
                && !string.IsNullOrWhiteSpace(AreaStableId)
                && !string.IsNullOrWhiteSpace(LegalRegionStableId)
                && Bounds.Validate()
                && Math.Abs(Bounds.WidthMeters - 1000d) < .001d
                && Math.Abs(Bounds.HeightMeters - 1000d) < .001d
                && TaskTileReferences.Length == 4
                && TaskTileReferences.All(value =>
                    value.Level == 공간TileLevelCodes.Task && value.Validate())
                && TaskTileReferences.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal).Count() == 4
                && xValues.Length == 2 && xValues[1] == xValues[0] + 1
                && yValues.Length == 2 && yValues[1] == yValues[0] + 1
                && TaskTileReferences.Min(value => value.Bounds.MinEasting) == Bounds.MinEasting
                && TaskTileReferences.Min(value => value.Bounds.MinNorthing) == Bounds.MinNorthing
                && TaskTileReferences.Max(value => value.Bounds.MaxEasting) == Bounds.MaxEasting
                && TaskTileReferences.Max(value => value.Bounds.MaxNorthing) == Bounds.MaxNorthing
                && VerticalStages.Select(value => value.StageCode)
                    .SequenceEqual(World경관완결단계Codes.All)
                && VerticalStages.All(value => !string.IsNullOrWhiteSpace(value.StatusCode)
                    && !string.IsNullOrWhiteSpace(value.EvidenceReference))
                && CompletionHash == CalculateHash();
        }
    }

    [Serializable]
    public sealed class WorldBuildRecipe
    {
        public string RecipeStableId = string.Empty;
        public string RuleRevision = string.Empty;
        public int DeterministicSeed;
        public string CrsCode = "EPSG:5186";
        public 공간BoundsData CoverageBounds = new();
        public 공간SourceSnapshotData[] Sources = Array.Empty<공간SourceSnapshotData>();
        public PhysicalElevationProfile PhysicalElevation = new();
        public VisualElevationProfile VisualElevation = new();
        public 공간TileGenerationProfile TileGeneration = new();
        public string CompositionProfileStableId = string.Empty;
        public string[] AreaSetReferences = Array.Empty<string>();

        public string CalculateHash() => 공간PipelineHash.Sha256(string.Join("|",
            RecipeStableId, RuleRevision, DeterministicSeed, CrsCode,
            CoverageBounds.MinEasting.ToString("0.###", CultureInfo.InvariantCulture),
            CoverageBounds.MinNorthing.ToString("0.###", CultureInfo.InvariantCulture),
            CoverageBounds.MaxEasting.ToString("0.###", CultureInfo.InvariantCulture),
            CoverageBounds.MaxNorthing.ToString("0.###", CultureInfo.InvariantCulture),
            CompositionProfileStableId,
            PhysicalElevation.VerticalReference,
            VisualElevation.HeightExaggeration.ToString("0.###", CultureInfo.InvariantCulture),
            TileGeneration.SeedStrategy,
            TileGeneration.TaskHaloMeters,
            string.Join(";", Sources.OrderBy(value => value.SourceSnapshotId, StringComparer.Ordinal)
                .Select(value => value.SourceSnapshotId + ":" + value.SourceSha256))));
    }

    [Serializable]
    public sealed class ReferenceTileDefinition
    {
        public string ReferenceTileStableId = string.Empty;
        public 공간TileKey TileKey = new();
        public string LegalRegionStableId = string.Empty;
        public string AuthoringKind = "HandAuthored";
        public string ComparisonRuleRevision = string.Empty;
        public string[] CompositionPrinciples = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RenderingCostBudget
    {
        public int LodLevel;
        public int MaximumInstances;
        public long MaximumTriangles;
        public int MaximumMaterialSlots;
        public int MaximumDrawCalls;
        public int MaximumShadowCasters;
        public int MaximumColliders;
        public int MaximumAnimators;
        public bool RequiresClusterOrHlod;
    }

    [Serializable]
    public sealed class WorldBuildValidationStage
    {
        public string StageCode = string.Empty;
        public string StatusCode = WorldBuildValidationStatusCodes.ContractReady;
        public string EvidenceReference = string.Empty;
    }

    [Serializable]
    public sealed class WorldBuildManifest
    {
        public string ManifestStableId = string.Empty;
        public string RecipeHash = string.Empty;
        public string CompositionProfileHash = string.Empty;
        public 공간LayerTileManifest[] LayerTiles = Array.Empty<공간LayerTileManifest>();
        public WorldAreaDefinition[] Areas = Array.Empty<WorldAreaDefinition>();
        public WorldAreaLinkDefinition[] Links = Array.Empty<WorldAreaLinkDefinition>();
        public WorldAreaSetDefinition[] AreaSets = Array.Empty<WorldAreaSetDefinition>();
        public World경관완결영역Definition[] CompletionAreas =
            Array.Empty<World경관완결영역Definition>();
        public 토지피복AllocationResult[] AllocationResults =
            Array.Empty<토지피복AllocationResult>();
        public LandscapeCompositionPlan CompositionPlan = new();
        public ReferenceTileDefinition ReferenceTile = new();
        public RenderingCostBudget[] RenderingBudgets = Array.Empty<RenderingCostBudget>();
        public WorldBuildValidationStage[] ValidationStages =
            Array.Empty<WorldBuildValidationStage>();
    }

    public static class WorldBuildManifestValidator
    {
        public static void Validate(WorldBuildManifest value)
        {
            if (value == null || value.RecipeHash.Length != 64
                || value.CompositionProfileHash.Length != 64
                || value.LayerTiles.Any(item => !item.Validate())
                || value.Areas.Length == 0 || value.AreaSets.Length == 0
                || value.CompletionAreas.Length == 0
                || value.CompletionAreas.Any(item => !item.Validate())
                || value.CompositionPlan.Items.Length != value.AllocationResults.Length
                || value.ReferenceTile.TileKey == null || !value.ReferenceTile.TileKey.Validate()
                || value.RenderingBudgets.Length != 3
                || value.ValidationStages.Select(item => item.StageCode)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(WorldBuildValidationStageCodes.All
                        .OrderBy(item => item, StringComparer.Ordinal)) == false
                || value.Areas.Select(item => item.AreaStableId).Distinct(StringComparer.Ordinal).Count()
                    != value.Areas.Length
                || value.Links.Select(item => item.LinkStableId).Distinct(StringComparer.Ordinal).Count()
                    != value.Links.Length)
                throw new InvalidOperationException("WorldBuildManifestInvalid");

            var areaIds = new HashSet<string>(value.Areas.Select(item => item.AreaStableId),
                StringComparer.Ordinal);
            var linkIds = new HashSet<string>(value.Links.Select(item => item.LinkStableId),
                StringComparer.Ordinal);
            var layerTileKeys = new HashSet<string>(value.LayerTiles.Select(item =>
                item.TileKey.StableId + "|" + item.LayerCode), StringComparer.Ordinal);
            if (value.Areas.Any(item => item.TileReferences.Length == 0
                    || item.TileReferences.Any(tile => !tile.Validate()))
                || value.Links.Any(item => !areaIds.Contains(item.FromAreaStableId)
                    || !areaIds.Contains(item.ToAreaStableId)
                    || item.LinkKindCode != WorldAreaLinkKindCodes.ScenarioRoute
                    || item.EvidenceKind != 공간EvidenceKindCodes.ScenarioDerived)
                || value.AreaSets.Any(item => item.OriginTile == null
                    || !item.OriginTile.Validate()
                    || item.AreaReferences.Any(area => !areaIds.Contains(area))
                    || item.LinkReferences.Any(link => !linkIds.Contains(link)))
                || value.CompletionAreas.Any(item => !areaIds.Contains(item.AreaStableId)
                    || item.TaskTileReferences.Any(tile => new[]
                        {
                            공간LayerCodes.Elevation,
                            공간LayerCodes.LandCover,
                            공간LayerCodes.PlacementMask,
                        }.Any(layer => !layerTileKeys.Contains(tile.StableId + "|" + layer)))))
                throw new InvalidOperationException("WorldBuildManifestReferenceInvalid");
        }
    }

    public static class 평창군공간PipelineFixture
    {
        public const double TotalAreaSquareKm = 1464.2839d;
        public const string AreaSetStableId = "area-set:sim:pyeongchang:farm-hub-town.v1";

        public static 토지피복CompositionProfile CreateCompositionProfile()
        {
            토지피복CompositionTarget Target(
                string code, string group, double area, string[] sourceCodes,
                string[] covers, string[] visualKeys, string strategy) => new()
            {
                TargetCode = code,
                CandidateGroupCode = group,
                SourceCategoryCodes = sourceCodes,
                TargetAreaSquareKm = area,
                TargetAreaRatio = area / TotalAreaSquareKm,
                AllowedLandCoverCodes = covers,
                AllowedVisualKeys = visualKeys,
                ClusterStrategy = strategy,
                MinimumVisibleCount = 1,
            };

            var crop = new[] { 법정동LandCoverCodes.Cropland };
            var forest = new[] { 법정동LandCoverCodes.Forest };
            var profile = new 토지피복CompositionProfile
            {
                ProfileStableId = "land-cover-composition:kr:51760:2024.v1",
                SourceSnapshotId = "public-data:moe:detailed-land-cover-area:51760:2024",
                RuleRevision = "statistical-landscape-allocation.v1",
                TotalAreaSquareKm = TotalAreaSquareKm,
                Targets = new[]
                {
                    Target(토지피복CompositionTargetCodes.RicePaddy,
                        토지피복CandidateGroupCodes.Agriculture, 5.0185d,
                        new[] { "경지정리가 된 논", "경지정리가 안 된 논" }, crop,
                        new[] { 법정동경관VisualKeys.SoilRows }, "terrace-water-edge"),
                    Target(토지피복CompositionTargetCodes.DryField,
                        토지피복CandidateGroupCodes.Agriculture, 102.5913d,
                        new[] { "경지정리가 된 밭", "경지정리가 안 된 밭" }, crop,
                        new[] { 법정동경관VisualKeys.SoilRows }, "field-block"),
                    Target(토지피복CompositionTargetCodes.Greenhouse,
                        토지피복CandidateGroupCodes.Agriculture, 3.815d,
                        new[] { "시설재배지" }, crop,
                        new[] { 법정동경관VisualKeys.Greenhouse }, "sparse-building"),
                    Target(토지피복CompositionTargetCodes.Orchard,
                        토지피복CandidateGroupCodes.Agriculture, 1.2454d,
                        new[] { "과수원" }, crop,
                        new[] { 법정동경관VisualKeys.Tree }, "regular-tree-grid"),
                    Target(토지피복CompositionTargetCodes.BroadleafForest,
                        토지피복CandidateGroupCodes.Forest, 666.2624d,
                        new[] { "활엽수림" }, forest,
                        new[] { 법정동경관VisualKeys.TreePatch }, "large-cluster"),
                    Target(토지피복CompositionTargetCodes.ConiferForest,
                        토지피복CandidateGroupCodes.Forest, 369.3999d,
                        new[] { "침엽수림" }, forest,
                        new[] { 법정동경관VisualKeys.ConiferTree }, "large-cluster"),
                    Target(토지피복CompositionTargetCodes.MixedForest,
                        토지피복CandidateGroupCodes.Forest, 116.1934d,
                        new[] { "혼효림" }, forest,
                        new[] { 법정동경관VisualKeys.TreePatch,
                            법정동경관VisualKeys.ConiferTree }, "mixed-cluster"),
                    Target(토지피복CompositionTargetCodes.InlandWater,
                        토지피복CandidateGroupCodes.Water, 9.2042d,
                        new[] { "하천", "호소" }, new[] { 법정동LandCoverCodes.Water },
                        new[] { 법정동경관VisualKeys.Reeds }, "water-edge"),
                    Target(토지피복CompositionTargetCodes.BareGround,
                        토지피복CandidateGroupCodes.BareGround, 23.6943d,
                        new[] { "기타 나지" }, new[] { 법정동LandCoverCodes.BareGround },
                        new[] { 법정동경관VisualKeys.SmallRocks }, "sparse-rock"),
                },
            };
            토지피복CompositionAllocator.ApplyVisualBudgets(profile);
            profile.Validate();
            return profile;
        }

        public static WorldBuildRecipe CreateRecipe()
        {
            var profile = CreateCompositionProfile();
            return new WorldBuildRecipe
            {
                RecipeStableId = "world-build:kr:51760:farm-hub-town.v1",
                RuleRevision = "spatial-tile-area-set.v1",
                DeterministicSeed = 51760,
                CoverageBounds = new 공간BoundsData
                {
                    MinEasting = 309961.186196729d,
                    MinNorthing = 519763.258937941d,
                    MaxEasting = 355741.186196729d,
                    MaxNorthing = 581353.258937941d,
                },
                CompositionProfileStableId = profile.ProfileStableId,
                AreaSetReferences = new[] { AreaSetStableId },
                Sources = new[]
                {
                    Source("copernicus-dem-glo30:51760:2026-08-12",
                        "Copernicus DEM GLO-30", "2026-08-12",
                        "D173A82973EBB3292F6E1DA45C250F8A68C131E8039AC93E0C1181CF303FADE8",
                        공간EvidenceKindCodes.AuthoritativeInternational, "30m-elevation",
                        30d, "-32767", "m", "EGM2008 geoid"),
                    Source("esa-worldcover-2021-v200:51760",
                        "ESA WorldCover 2021 v200", "2021",
                        "2769C2087D97C5DB121D0DA76C1BEB966B9A96002C757A470294FF12559417A5",
                        공간EvidenceKindCodes.AuthoritativeInternational, "10m-general-land-cover",
                        10d, "0", "n/a", "n/a"),
                    Source("vworld-ngii-dem-90m:51760:2026-08-12",
                        "VWorld · 국토지리정보원 수치표고모델 90M", "2026-08-12",
                        "2D8139587033C1AD4341C914D8FD249104E89C7EEFDB031A1EC8F7D327C1B52B",
                        공간EvidenceKindCodes.OfficialDomestic, "90m-elevation-comparison",
                        90d, "-9999", "m", "source-metadata-not-confirmed"),
                    Source("moe-detailed-land-cover-area:51760:2024",
                        "기후에너지환경부 지역별 세분류 토지피복 면적", "2024",
                        "C2B2C7739145F62147FAA6A52FCB26F92418D88EEA2DF5718F6E9D30850F1695",
                        공간EvidenceKindCodes.OfficialDomestic, "municipality-area-statistics",
                        0d, "n/a", "n/a", "n/a"),
                    Source("vworld-legal-boundary:51760:2026-07-01",
                        "VWorld · 국토교통부 법정구역경계", "2026-07-01",
                        "70B2FBD70FB1CD9BB31CD02EF3279778389D0F080C516F57EA682CD4F0D3F327",
                        공간EvidenceKindCodes.OfficialDomestic, "legal-boundary-polygon",
                        0d, "n/a", "n/a", "n/a"),
                },
                PhysicalElevation = new PhysicalElevationProfile
                {
                    SourceSnapshotId = "copernicus-dem-glo30:51760:2026-08-12",
                    VerticalReference = "EGM2008 geoid",
                    NoDataValue = "-32767",
                },
                VisualElevation = new VisualElevationProfile
                {
                    HeightExaggeration = 1.35d,
                    VisualBaseOffset = 0d,
                },
                TileGeneration = new 공간TileGenerationProfile(),
            };
        }

        public static WorldBuildManifest CreateManifest()
        {
            var profile = CreateCompositionProfile();
            var recipe = CreateRecipe();
            var recipeHash = recipe.CalculateHash();
            var profileHash = profile.CalculateHash();
            var areas = new[]
            {
                Area("area:sim:pyeongchang:daegwallyeong-farm", WorldAreaKindCodes.Farm,
                    평창군법정동WorldFixture.FarmRegionStableId, 법정동WorldRoleCodes.Farm, 43, 71),
                Area("area:sim:pyeongchang:jinbu-hub", WorldAreaKindCodes.Hub,
                    평창군법정동WorldFixture.HubRegionStableId, 법정동WorldRoleCodes.Hub, 42, 70),
                Area("area:sim:pyeongchang:pyeongchang-town", WorldAreaKindCodes.Town,
                    평창군법정동WorldFixture.TownRegionStableId, 법정동WorldRoleCodes.Town, 40, 66),
            };
            var links = new[]
            {
                Link("area-link:sim:pyeongchang:farm-hub", areas[0], areas[1]),
                Link("area-link:sim:pyeongchang:hub-town", areas[1], areas[2]),
            };
            var completionArea = CreateFarmCompletionArea(areas[0]);
            var allocation = 토지피복CompositionAllocator.Allocate(profile,
                new[]
                {
                    Candidate(토지피복CandidateGroupCodes.Agriculture, .06421d),
                    Candidate(토지피복CandidateGroupCodes.Forest, .85495d),
                    Candidate(토지피복CandidateGroupCodes.Water, .00218d),
                    Candidate(토지피복CandidateGroupCodes.BareGround, .00148d),
                });
            var layerTiles = new List<공간LayerTileManifest>();
            foreach (var tile in completionArea.TaskTileReferences)
            foreach (var layer in new[] { 공간LayerCodes.Elevation,
                         공간LayerCodes.LandCover, 공간LayerCodes.PlacementMask })
                layerTiles.Add(Layer(tile, layer, recipe, recipeHash, profileHash));

            var manifest = new WorldBuildManifest
            {
                ManifestStableId = "world-manifest:kr:51760:farm-hub-town.v1",
                RecipeHash = recipeHash,
                CompositionProfileHash = profileHash,
                LayerTiles = layerTiles.ToArray(),
                Areas = areas,
                Links = links,
                CompletionAreas = new[] { completionArea },
                AreaSets = new[]
                {
                    new WorldAreaSetDefinition
                    {
                        AreaSetStableId = AreaSetStableId,
                        AreaReferences = areas.Select(value => value.AreaStableId).ToArray(),
                        LinkReferences = links.Select(value => value.LinkStableId).ToArray(),
                        OriginTile = new 공간TileKey { Level = 0, X = 38, Y = 64 },
                    },
                },
                AllocationResults = allocation,
                CompositionPlan = CreateCompositionPlan(profile, allocation),
                ReferenceTile = new ReferenceTileDefinition
                {
                    ReferenceTileStableId = "reference-tile:kr:51760:daegwallyeong:l2:v1",
                    TileKey = new 공간TileKey { Level = 2, X = 700, Y = 1144 },
                    LegalRegionStableId = 평창군법정동WorldFixture.FarmRegionStableId,
                    ComparisonRuleRevision = "reference-landscape-comparison.v1",
                    CompositionPrinciples = new[]
                    {
                        "큰 덩어리", "중간 군집", "작은 디테일", "연결 지점 강조",
                    },
                },
                RenderingBudgets = new[]
                {
                    Budget(0, 8, 120000, 16, 24, 8, 0, 0, true),
                    Budget(1, 32, 500000, 48, 72, 24, 8, 2, true),
                    Budget(2, 120, 1500000, 120, 180, 64, 40, 12, false),
                },
                ValidationStages = WorldBuildValidationStageCodes.All.Select(code =>
                    new WorldBuildValidationStage
                    {
                        StageCode = code,
                        StatusCode = code == WorldBuildValidationStageCodes.ReferenceTile
                            || code == WorldBuildValidationStageCodes.RenderingCost
                            || code == WorldBuildValidationStageCodes.FinalVisualAssetBinding
                            ? WorldBuildValidationStatusCodes.RequiresEditorEvidence
                            : WorldBuildValidationStatusCodes.ContractReady,
                        EvidenceReference = code == WorldBuildValidationStageCodes.ReferenceTile
                            ? "reference-tile:kr:51760:daegwallyeong:l2:v1"
                            : "world-manifest:kr:51760:farm-hub-town.v1",
                    }).ToArray(),
            };
            WorldBuildManifestValidator.Validate(manifest);
            return manifest;
        }

        private static 공간SourceSnapshotData Source(
            string id, string name, string vintage, string hash, string evidence, string resolution,
            double horizontalResolutionMeters, string noDataValue,
            string heightUnit, string verticalReference)
            => new()
            {
                SourceSnapshotId = id,
                SourceName = name,
                SourceVintage = vintage,
                SourceSha256 = hash,
                CrsCode = "EPSG:5186",
                HorizontalResolutionMeters = horizontalResolutionMeters,
                NoDataValue = noDataValue,
                HeightUnit = heightUnit,
                VerticalReference = verticalReference,
                EvidenceKind = evidence,
                SemanticResolutionCode = resolution,
            };

        private static LandscapeCompositionPlan CreateCompositionPlan(
            토지피복CompositionProfile profile,
            IReadOnlyList<토지피복AllocationResult> allocation)
        {
            var allocationHash = 공간PipelineHash.Sha256(string.Join("|", allocation
                .OrderBy(value => value.TargetCode, StringComparer.Ordinal)
                .Select(value => value.TargetCode + ":" +
                    value.AllocatedAreaSquareKm.ToString("0.####", CultureInfo.InvariantCulture))));
            return new LandscapeCompositionPlan
            {
                PlanStableId = "landscape-composition:kr:51760:farm-hub-town.v1",
                SourceAllocationHash = allocationHash,
                RuleRevision = "landscape-composition.v1",
                Items = allocation.Select(result =>
                {
                    var target = profile.Targets.Single(item => item.TargetCode == result.TargetCode);
                    return new LandscapeCompositionItem
                    {
                        TargetCode = result.TargetCode,
                        AllocatedAreaSquareKm = result.AllocatedAreaSquareKm,
                        OverviewInstanceBudget = target.OverviewBudget,
                        RegionInstanceBudget = target.RegionBudget,
                        TaskInstanceBudget = target.TaskBudget,
                        AllowedVisualKeys = target.AllowedVisualKeys,
                    };
                }).ToArray(),
            };
        }

        private static RenderingCostBudget Budget(
            int lod, int instances, long triangles, int materials, int drawCalls,
            int shadows, int colliders, int animators, bool cluster)
            => new()
            {
                LodLevel = lod,
                MaximumInstances = instances,
                MaximumTriangles = triangles,
                MaximumMaterialSlots = materials,
                MaximumDrawCalls = drawCalls,
                MaximumShadowCasters = shadows,
                MaximumColliders = colliders,
                MaximumAnimators = animators,
                RequiresClusterOrHlod = cluster,
            };

        private static 토지피복CandidateGroupData Candidate(string code, double ratio)
            => new() { CandidateGroupCode = code, CandidateAreaSquareKm = TotalAreaSquareKm * ratio };

        private static WorldAreaDefinition Area(
            string id, string kind, string region, string role, int overviewX, int overviewY)
            => new()
            {
                AreaStableId = id,
                AreaKindCode = kind,
                LegalRegionStableId = region,
                ScenarioRoleCode = role,
                EvidenceKind = 공간EvidenceKindCodes.ScenarioDerived,
                TileReferences = new[]
                {
                    new 공간TileKey { Level = 0, X = overviewX, Y = overviewY },
                },
            };

        private static WorldAreaLinkDefinition Link(
            string id, WorldAreaDefinition from, WorldAreaDefinition to)
            => new()
            {
                LinkStableId = id,
                FromAreaStableId = from.AreaStableId,
                ToAreaStableId = to.AreaStableId,
            };

        private static World경관완결영역Definition CreateFarmCompletionArea(
            WorldAreaDefinition farmArea)
        {
            var value = new World경관완결영역Definition
            {
                CompletionAreaStableId =
                    "completion-area:sim:pyeongchang:daegwallyeong-farm.v1",
                AreaStableId = farmArea.AreaStableId,
                LegalRegionStableId = farmArea.LegalRegionStableId,
                Bounds = new 공간BoundsData
                {
                    MinEasting = 350000d,
                    MinNorthing = 572000d,
                    MaxEasting = 351000d,
                    MaxNorthing = 573000d,
                },
                TaskTileReferences = new[]
                {
                    new 공간TileKey { Level = 2, X = 700, Y = 1144 },
                    new 공간TileKey { Level = 2, X = 701, Y = 1144 },
                    new 공간TileKey { Level = 2, X = 700, Y = 1145 },
                    new 공간TileKey { Level = 2, X = 701, Y = 1145 },
                },
                ReferenceTileStableId = "reference-tile:kr:51760:daegwallyeong:l2:v1",
                VerticalStages = World경관완결단계Codes.All.Select(code =>
                    new World경관완결단계
                    {
                        StageCode = code,
                        StatusCode = code == World경관완결단계Codes.PhysicalSpace
                            || code == World경관완결단계Codes.SpatialMeaning
                            ? WorldBuildValidationStatusCodes.WaitingForSpatialArtifact
                            : code == World경관완결단계Codes.UiPlan
                                || code == World경관완결단계Codes.UnityRuntime
                                || code == World경관완결단계Codes.CompletionValidation
                                ? WorldBuildValidationStatusCodes.RequiresEditorEvidence
                                : WorldBuildValidationStatusCodes.ContractReady,
                        EvidenceReference = code == World경관완결단계Codes.SourceArtifacts
                            ? "world-recipe:kr:51760:spatial-landscape.v1"
                            : "completion-area:sim:pyeongchang:daegwallyeong-farm.v1/" + code,
                    }).ToArray(),
            };
            value.CompletionHash = value.CalculateHash();
            return value;
        }

        private static 공간LayerTileManifest Layer(
            공간TileKey tile, string layer, WorldBuildRecipe recipe,
            string recipeHash, string profileHash)
        {
            var source = layer == 공간LayerCodes.Elevation
                ? recipe.Sources[0] : recipe.Sources[1];
            var value = new 공간LayerTileManifest
            {
                TileKey = tile,
                LayerCode = layer,
                SourceSnapshotId = source.SourceSnapshotId,
                SourceHash = source.SourceSha256,
                RuleRevision = recipe.RuleRevision,
                RecipeHash = recipeHash,
                CompositionProfileHash = profileHash,
                CoverageStatusCode = layer == 공간LayerCodes.LandCover
                    ? 공간CoverageStatusCodes.Complete
                    : 공간CoverageStatusCodes.Missing,
                EvidenceKind = layer == 공간LayerCodes.PlacementMask
                    ? 공간EvidenceKindCodes.PresentationDerived : source.EvidenceKind,
                SemanticResolutionCode = source.SemanticResolutionCode,
                ArtifactReference = layer == 공간LayerCodes.LandCover
                    ? "source-manifest://" + tile.StableId + "/" + layer
                    : "pending://" + tile.StableId + "/" + layer,
            };
            value.Fingerprint = value.CalculateFingerprint();
            return value;
        }
    }

    public static class 공간PipelineHash
    {
        public static int WorldCoordinateSeed(
            int baseSeed, double easting, double northing, string semanticKey)
        {
            var hash = Sha256(string.Join("|", baseSeed,
                Math.Round(easting, 3).ToString("0.###", CultureInfo.InvariantCulture),
                Math.Round(northing, 3).ToString("0.###", CultureInfo.InvariantCulture),
                semanticKey));
            return int.Parse(hash.Substring(0, 7), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        public static string Sha256(string value)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(item => item.ToString("X2", CultureInfo.InvariantCulture)));
        }
    }
}
