using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 공간문법CompositionTopologyCodes
    {
        public const string Area = "area";
        public const string Linear = "linear";
        public const string Junction = "junction";
        public const string Transition = "transition";
        public const string Landmark = "landmark";
        public const string Detail = "detail";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Area, Linear, Junction, Transition, Landmark, Detail,
        };

        public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
    }

    public static class 공간문법AssemblyScaleCodes
    {
        public const string Macro = "macro";
        public const string Meso = "meso";
        public const string Micro = "micro";

        public static IReadOnlyList<string> All { get; } = new[] { Macro, Meso, Micro };
        public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
    }

    public static class 공간문법EdgeProfileCodes
    {
        public const string Open = "open";
        public const string Field = "field";
        public const string Forest = "forest";
        public const string ForestEdge = "forest-edge";
        public const string Residential = "residential";
        public const string Logistics = "logistics";
        public const string Water = "water";
        public const string RoadFront = "road-front";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Open, Field, Forest, ForestEdge, Residential, Logistics, Water, RoadFront,
        };

        public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
    }

    public static class 공간문법RotationCodes
    {
        public const string Deg0 = "0";
        public const string Deg90 = "90";
        public const string Deg180 = "180";
        public const string Deg270 = "270";

        public static IReadOnlyList<string> All { get; } = new[] { Deg0, Deg90, Deg180, Deg270 };
        public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
    }

    public static class 공간문법NetworkSetNames
    {
        public const string 농촌도로직선 = "농촌도로 직선";
        public const string 농촌도로곡선 = "농촌도로 곡선";
        public const string 농촌도로T자 = "농촌도로 T자";
        public const string 농촌도로십자 = "농촌도로 십자";
        public const string 타운도로직선 = "타운도로 직선";
        public const string 타운도로곡선 = "타운도로 곡선";
        public const string 타운도로T자 = "타운도로 T자";
        public const string 타운도로십자 = "타운도로 십자";
        public const string 도시도로직선 = "도시도로 직선";
        public const string 도시도로곡선 = "도시도로 곡선";
        public const string 도시도로T자 = "도시도로 T자";
        public const string 도시도로십자 = "도시도로 십자";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            농촌도로직선, 농촌도로곡선, 농촌도로T자, 농촌도로십자,
            타운도로직선, 타운도로곡선, 타운도로T자, 타운도로십자,
            도시도로직선, 도시도로곡선, 도시도로T자, 도시도로십자,
        };
    }

    public static class 공간문법TransitionSetNames
    {
        public const string NatureFarm = "Nature–Farm 전환";
        public const string FarmTown = "Farm–Town 전환";
        public const string TownCity = "Town–City 전환";
        public const string FarmHub = "Farm–Hub 전환";
        public const string TownHub = "Town–Hub 전환";
        public const string HubCity = "Hub–City 전환";
        public const string WaterLand = "Water–Land 전환";
        public const string RoadBuildingFront = "Road–BuildingFront 전환";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            NatureFarm, FarmTown, TownCity, FarmHub,
            TownHub, HubCity, WaterLand, RoadBuildingFront,
        };
    }

    [Serializable]
    public sealed class 공간문법EdgeProfileContract
    {
        [SerializeField] private string directionCode = string.Empty;
        [SerializeField] private string profileCode = string.Empty;
        [SerializeField] private bool required = true;

        public string DirectionCode => directionCode;
        public string ProfileCode => profileCode;
        public bool Required => required;

        public void Configure(string direction, string profile, bool isRequired = true)
        {
            directionCode = direction ?? string.Empty;
            profileCode = profile ?? string.Empty;
            required = isRequired;
        }

        public bool Validate() => 월드CompositionConnectorDirectionCodes.IsKnown(directionCode)
            && 공간문법EdgeProfileCodes.IsKnown(profileCode);
    }

    [Serializable]
    public sealed class 공간문법RepeatRuleContract
    {
        [SerializeField] private bool allowRepeat = true;
        [SerializeField] private int maxConsecutive = 2;
        [SerializeField] private int recentWindowSize = 4;
        [SerializeField] private float neighborDiversityWeight = 1f;
        [SerializeField] private string[] rotationCodes = Array.Empty<string>();
        [SerializeField] private bool mirrorAllowed;

        public bool AllowRepeat => allowRepeat;
        public int MaxConsecutive => maxConsecutive;
        public int RecentWindowSize => recentWindowSize;
        public float NeighborDiversityWeight => neighborDiversityWeight;
        public IReadOnlyList<string> RotationCodes => rotationCodes;
        public bool MirrorAllowed => mirrorAllowed;

        public void Configure(
            bool canRepeat,
            int consecutiveLimit,
            int recentWindow,
            float diversityWeight,
            string[] rotations,
            bool canMirror)
        {
            allowRepeat = canRepeat;
            maxConsecutive = consecutiveLimit;
            recentWindowSize = recentWindow;
            neighborDiversityWeight = diversityWeight;
            rotationCodes = rotations ?? Array.Empty<string>();
            mirrorAllowed = canMirror;
        }

        public bool Validate() => maxConsecutive >= 1
            && recentWindowSize >= maxConsecutive
            && neighborDiversityWeight >= 0f
            && rotationCodes.Length > 0
            && rotationCodes.All(공간문법RotationCodes.IsKnown)
            && rotationCodes.Distinct(StringComparer.Ordinal).Count() == rotationCodes.Length;
    }

    [Serializable]
    public sealed class 공간문법AdjacencyRuleContract
    {
        [SerializeField] private string[] preferredNeighborTopologyCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedNeighborTopologyCodes = Array.Empty<string>();
        [SerializeField] private string[] forbiddenNeighborTopologyCodes = Array.Empty<string>();

        public IReadOnlyList<string> PreferredNeighborTopologyCodes => preferredNeighborTopologyCodes;
        public IReadOnlyList<string> AllowedNeighborTopologyCodes => allowedNeighborTopologyCodes;
        public IReadOnlyList<string> ForbiddenNeighborTopologyCodes => forbiddenNeighborTopologyCodes;

        public void Configure(string[] preferred, string[] allowed, string[] forbidden)
        {
            preferredNeighborTopologyCodes = preferred ?? Array.Empty<string>();
            allowedNeighborTopologyCodes = allowed ?? Array.Empty<string>();
            forbiddenNeighborTopologyCodes = forbidden ?? Array.Empty<string>();
        }

        public bool Validate()
        {
            var all = preferredNeighborTopologyCodes
                .Concat(allowedNeighborTopologyCodes)
                .Concat(forbiddenNeighborTopologyCodes).ToArray();
            return allowedNeighborTopologyCodes.Length > 0
                && all.All(공간문법CompositionTopologyCodes.IsKnown)
                && preferredNeighborTopologyCodes.Intersect(
                    forbiddenNeighborTopologyCodes, StringComparer.Ordinal).Any() == false
                && allowedNeighborTopologyCodes.Intersect(
                    forbiddenNeighborTopologyCodes, StringComparer.Ordinal).Any() == false;
        }
    }

    [Serializable]
    public sealed class 공간문법ExpansionRuleContract
    {
        [SerializeField] private bool canTile;
        [SerializeField] private bool canChain;
        [SerializeField] private bool canTerminate;
        [SerializeField] private string[] terminationCompositionKeys = Array.Empty<string>();

        public bool CanTile => canTile;
        public bool CanChain => canChain;
        public bool CanTerminate => canTerminate;
        public IReadOnlyList<string> TerminationCompositionKeys => terminationCompositionKeys;

        public void Configure(bool tile, bool chain, bool terminate, string[] terminationKeys)
        {
            canTile = tile;
            canChain = chain;
            canTerminate = terminate;
            terminationCompositionKeys = terminationKeys ?? Array.Empty<string>();
        }

        public bool Validate() => terminationCompositionKeys.All(value =>
            !string.IsNullOrWhiteSpace(value) && !value.Contains("/") && !value.Contains("\\"));
    }

    [Serializable]
    public sealed class 공간문법InternalGenerationContract
    {
        [SerializeField] private string seedVersion = string.Empty;
        [SerializeField] private string detailGeneratorRevision = string.Empty;

        public string SeedVersion => seedVersion;
        public string DetailGeneratorRevision => detailGeneratorRevision;

        public void Configure(string seed, string generatorRevision)
        {
            seedVersion = seed ?? string.Empty;
            detailGeneratorRevision = generatorRevision ?? string.Empty;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(seedVersion)
            && !string.IsNullOrWhiteSpace(detailGeneratorRevision)
            && !seedVersion.Contains("/") && !seedVersion.Contains("\\")
            && !detailGeneratorRevision.Contains("/") && !detailGeneratorRevision.Contains("\\");
    }

    [Serializable]
    public sealed class 공간문법CompositionCatalogEntry
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private string sourceCompositionKey = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string topologyCode = string.Empty;
        [SerializeField] private string assemblyScaleCode = string.Empty;
        [SerializeField] private 공간문법EdgeProfileContract[] edgeProfiles = Array.Empty<공간문법EdgeProfileContract>();
        [SerializeField] private 공간문법RepeatRuleContract repeatRules = null!;
        [SerializeField] private 공간문법AdjacencyRuleContract adjacencyRules = null!;
        [SerializeField] private 공간문법ExpansionRuleContract expansionRules = null!;
        [SerializeField] private 공간문법InternalGenerationContract internalGeneration = null!;
        [SerializeField] private string[] allowedLandCoverCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedRegionRoleCodes = Array.Empty<string>();
        [SerializeField] private Vector2 slopeRange;
        [SerializeField] private float paddingMeters;
        [SerializeField] private bool requiresWaterMask;
        [SerializeField] private bool hlodEligible;
        [SerializeField] private int triangleCount;
        [SerializeField] private int materialSlotCount;
        [SerializeField] private int rendererCount;
        [SerializeField] private int shadowCasterCount;
        [SerializeField] private int colliderCount;
        [SerializeField] private int animatorCount;
        [SerializeField] private string[] legacyCompositionKeys = Array.Empty<string>();
        [SerializeField] private bool presentationOnly = true;

        public 월드CompositionDescriptor Descriptor => descriptor;
        public string CompositionKey => descriptor?.CompositionKey ?? string.Empty;
        public string SourceCompositionKey => sourceCompositionKey;
        public GameObject Prefab => prefab;
        public string TopologyCode => topologyCode;
        public string AssemblyScaleCode => assemblyScaleCode;
        public IReadOnlyList<공간문법EdgeProfileContract> EdgeProfiles => edgeProfiles;
        public 공간문법RepeatRuleContract RepeatRules => repeatRules;
        public 공간문법AdjacencyRuleContract AdjacencyRules => adjacencyRules;
        public 공간문법ExpansionRuleContract ExpansionRules => expansionRules;
        public 공간문법InternalGenerationContract InternalGeneration => internalGeneration;
        public IReadOnlyList<string> AllowedLandCoverCodes => allowedLandCoverCodes;
        public IReadOnlyList<string> AllowedRegionRoleCodes => allowedRegionRoleCodes;
        public Vector2 SlopeRange => slopeRange;
        public float PaddingMeters => paddingMeters;
        public bool RequiresWaterMask => requiresWaterMask;
        public bool HlodEligible => hlodEligible;
        public int TriangleCount => triangleCount;
        public int MaterialSlotCount => materialSlotCount;
        public int RendererCount => rendererCount;
        public int ShadowCasterCount => shadowCasterCount;
        public int ColliderCount => colliderCount;
        public int AnimatorCount => animatorCount;
        public IReadOnlyList<string> LegacyCompositionKeys => legacyCompositionKeys;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            월드CompositionDescriptor value,
            string sourceKey,
            GameObject sourcePrefab,
            string topology,
            string scale,
            공간문법EdgeProfileContract[] edges,
            공간문법RepeatRuleContract repeat,
            공간문법AdjacencyRuleContract adjacency,
            공간문법ExpansionRuleContract expansion,
            공간문법InternalGenerationContract generation,
            string[] landCovers,
            string[] regionRoles,
            Vector2 physicalSlopeRange,
            float padding,
            bool waterMask,
            bool canBuildHlod,
            string[] legacyKeys)
        {
            descriptor = value;
            sourceCompositionKey = sourceKey ?? string.Empty;
            prefab = sourcePrefab;
            topologyCode = topology ?? string.Empty;
            assemblyScaleCode = scale ?? string.Empty;
            edgeProfiles = edges ?? Array.Empty<공간문법EdgeProfileContract>();
            repeatRules = repeat;
            adjacencyRules = adjacency;
            expansionRules = expansion;
            internalGeneration = generation;
            allowedLandCoverCodes = landCovers ?? Array.Empty<string>();
            allowedRegionRoleCodes = regionRoles ?? Array.Empty<string>();
            slopeRange = physicalSlopeRange;
            paddingMeters = padding;
            requiresWaterMask = waterMask;
            hlodEligible = canBuildHlod;
            legacyCompositionKeys = legacyKeys ?? Array.Empty<string>();
            presentationOnly = true;

            var renderers = sourcePrefab.GetComponentsInChildren<Renderer>(true);
            triangleCount = sourcePrefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(value2 => value2.sharedMesh != null)
                .Sum(value2 => value2.sharedMesh.triangles.Length / 3);
            materialSlotCount = renderers.Sum(value2 => value2.sharedMaterials.Length);
            rendererCount = renderers.Length;
            shadowCasterCount = renderers.Count(value2 => value2.shadowCastingMode
                != UnityEngine.Rendering.ShadowCastingMode.Off);
            colliderCount = sourcePrefab.GetComponentsInChildren<Collider>(true).Length;
            animatorCount = sourcePrefab.GetComponentsInChildren<Animator>(true).Length;
        }

        public bool Validate()
        {
            return descriptor != null && descriptor.Validate()
                && !string.IsNullOrWhiteSpace(sourceCompositionKey)
                && !sourceCompositionKey.Contains("/") && !sourceCompositionKey.Contains("\\")
                && prefab != null
                && 공간문법CompositionTopologyCodes.IsKnown(topologyCode)
                && 공간문법AssemblyScaleCodes.IsKnown(assemblyScaleCode)
                && edgeProfiles.Length == 4
                && edgeProfiles.All(value => value != null && value.Validate())
                && edgeProfiles.Select(value => value.DirectionCode)
                    .Distinct(StringComparer.Ordinal).Count() == 4
                && repeatRules != null && repeatRules.Validate()
                && adjacencyRules != null && adjacencyRules.Validate()
                && expansionRules != null && expansionRules.Validate()
                && internalGeneration != null && internalGeneration.Validate()
                && allowedLandCoverCodes.Length > 0
                && allowedRegionRoleCodes.Length > 0
                && slopeRange.x >= 0f && slopeRange.y >= slopeRange.x && slopeRange.y <= 90f
                && paddingMeters >= 0f
                && triangleCount >= 0 && materialSlotCount >= 0 && rendererCount > 0
                && shadowCasterCount >= 0 && colliderCount >= 0 && animatorCount >= 0
                && legacyCompositionKeys.All(value => !string.IsNullOrWhiteSpace(value)
                    && !value.Contains("/") && !value.Contains("\\"))
                && presentationOnly;
        }

        public string BuildGrammarSignature()
        {
            var edges = string.Join(";", edgeProfiles
                .OrderBy(value => value.DirectionCode, StringComparer.Ordinal)
                .Select(value => value.DirectionCode + ":" + value.ProfileCode));
            return string.Join("|", topologyCode, assemblyScaleCode,
                descriptor.Footprint.x.ToString("0.###", CultureInfo.InvariantCulture),
                descriptor.Footprint.y.ToString("0.###", CultureInfo.InvariantCulture),
                edges, repeatRules.MaxConsecutive, repeatRules.RecentWindowSize,
                expansionRules.CanTile ? "1" : "0",
                expansionRules.CanChain ? "1" : "0",
                expansionRules.CanTerminate ? "1" : "0",
                internalGeneration.SeedVersion,
                internalGeneration.DetailGeneratorRevision);
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/공간 문법 Composition Catalog")]
    public sealed class 공간문법CompositionCatalog : ScriptableObject
    {
        public const int ExpectedEntryCount = 156;
        public const int ExpectedSetCount = 52;
        public const string NeutralGrammarRevision = "pyeongchang-landscape-grammar.v2";

        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 공간문법CompositionCatalogEntry[] entries =
            Array.Empty<공간문법CompositionCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<공간문법CompositionCatalogEntry> Entries => entries;

        public void Configure(string revision, 공간문법CompositionCatalogEntry[] values)
        {
            catalogRevision = revision ?? string.Empty;
            entries = values ?? Array.Empty<공간문법CompositionCatalogEntry>();
        }

        public 공간문법CompositionCatalogEntry Resolve(string compositionKey)
        {
            Validate();
            return entries.SingleOrDefault(value => value.CompositionKey == compositionKey)
                ?? throw new InvalidOperationException("LandscapeGrammarCompositionMissing:" + compositionKey);
        }

        public 공간문법CompositionCatalogEntry ResolveLegacy(string compositionKey)
        {
            Validate();
            return entries.SingleOrDefault(value => value.CompositionKey == compositionKey
                || value.LegacyCompositionKeys.Contains(compositionKey, StringComparer.Ordinal))
                ?? throw new InvalidOperationException("LandscapeGrammarLegacyCompositionMissing:" + compositionKey);
        }

        public string BuildSafeCatalogHashSha256()
        {
            Validate();
            var builder = new StringBuilder()
                .Append(1).Append('|')
                .Append(catalogRevision).Append('|')
                .Append('1').AppendLine();
            foreach (var entry in entries.OrderBy(
                         value => value.CompositionKey, StringComparer.Ordinal))
            {
                builder.Append(entry.CompositionKey).Append('|')
                    .Append(entry.SourceCompositionKey).Append('|')
                    .Append(entry.TopologyCode).Append('|')
                    .Append(entry.AssemblyScaleCode).Append('|')
                    .Append(entry.Descriptor.Footprint.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.Descriptor.Footprint.y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.SlopeRange.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.SlopeRange.y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(string.Join(",", entry.EdgeProfiles
                        .OrderBy(value => value.DirectionCode, StringComparer.Ordinal)
                        .Select(value => value.DirectionCode + ":" + value.ProfileCode + ":"
                            + (value.Required ? "1" : "0"))))
                    .Append('|').Append(entry.RepeatRules.MaxConsecutive).Append('|')
                    .Append(entry.RepeatRules.RecentWindowSize).Append('|')
                    .Append(string.Join(",", entry.RepeatRules.RotationCodes)).Append('|')
                    .Append(entry.InternalGeneration.SeedVersion).Append('|')
                    .Append(entry.InternalGeneration.DetailGeneratorRevision).Append('|')
                    .Append(entry.TriangleCount).Append('|')
                    .Append(entry.MaterialSlotCount).AppendLine();
            }
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                .Select(value => value.ToString("x2")));
        }

        public string BuildNeutralGrammarHashSha256()
        {
            Validate();
            var builder = new StringBuilder()
                .Append(2).Append('|')
                .Append(NeutralGrammarRevision).Append('|')
                .Append('1').AppendLine();
            foreach (var entry in entries.OrderBy(
                         value => value.CompositionKey, StringComparer.Ordinal))
            {
                var connectors = entry.Descriptor.Connectors
                    .OrderBy(value => value.ConnectorCode, StringComparer.Ordinal)
                    .Select(value => string.Join(":",
                        value.ConnectorCode,
                        value.ConnectorKindCode,
                        value.DirectionCode,
                        value.RouteSignature,
                        value.LocalPosition.x.ToString("R", CultureInfo.InvariantCulture),
                        value.LocalPosition.y.ToString("R", CultureInfo.InvariantCulture),
                        value.LocalPosition.z.ToString("R", CultureInfo.InvariantCulture),
                        value.LocalYaw.ToString("R", CultureInfo.InvariantCulture),
                        value.Width.ToString("R", CultureInfo.InvariantCulture),
                        value.ExpansionSocket ? "1" : "0"));
                builder.Append(entry.CompositionKey).Append('|')
                    .Append(entry.Descriptor.SetName).Append('|')
                    .Append(entry.Descriptor.VariantCode).Append('|')
                    .Append(entry.Descriptor.PackCode).Append('|')
                    .Append(entry.TopologyCode).Append('|')
                    .Append(entry.AssemblyScaleCode).Append('|')
                    .Append(entry.Descriptor.Footprint.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.Descriptor.Footprint.y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.PaddingMeters.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.SlopeRange.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.SlopeRange.y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(entry.RequiresWaterMask ? '1' : '0').Append('|')
                    .Append(string.Join(",", entry.EdgeProfiles
                        .OrderBy(value => value.DirectionCode, StringComparer.Ordinal)
                        .Select(value => value.DirectionCode + ":" + value.ProfileCode + ":"
                            + (value.Required ? "1" : "0")))).Append('|')
                    .Append(string.Join(",", connectors)).Append('|')
                    .Append(entry.RepeatRules.AllowRepeat ? '1' : '0').Append('|')
                    .Append(entry.RepeatRules.MaxConsecutive).Append('|')
                    .Append(entry.RepeatRules.RecentWindowSize).Append('|')
                    .Append(entry.RepeatRules.NeighborDiversityWeight.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(string.Join(",", entry.RepeatRules.RotationCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(entry.RepeatRules.MirrorAllowed ? '1' : '0').Append('|')
                    .Append(string.Join(",", entry.AdjacencyRules.PreferredNeighborTopologyCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(string.Join(",", entry.AdjacencyRules.AllowedNeighborTopologyCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(string.Join(",", entry.AdjacencyRules.ForbiddenNeighborTopologyCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(entry.ExpansionRules.CanTile ? '1' : '0').Append('|')
                    .Append(entry.ExpansionRules.CanChain ? '1' : '0').Append('|')
                    .Append(entry.ExpansionRules.CanTerminate ? '1' : '0').Append('|')
                    .Append(string.Join(",", entry.ExpansionRules.TerminationCompositionKeys
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(entry.InternalGeneration.SeedVersion).Append('|')
                    .Append(string.Join(",", entry.AllowedLandCoverCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(string.Join(",", entry.AllowedRegionRoleCodes
                        .OrderBy(value => value, StringComparer.Ordinal))).Append('|')
                    .Append(entry.PresentationOnly ? '1' : '0').AppendLine();
            }
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                .Select(value => value.ToString("x2")));
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || entries == null || entries.Length != ExpectedEntryCount
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != ExpectedEntryCount)
                throw new InvalidOperationException("LandscapeGrammarCatalogInvalid");

            var groups = entries.GroupBy(value =>
                value.Descriptor.PackCode + "|" + value.Descriptor.SetName,
                StringComparer.Ordinal).ToArray();
            if (groups.Length != ExpectedSetCount
                || groups.Any(group => !group.Select(value => value.Descriptor.VariantCode)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(월드CompositionVariantCodes.All)
                    || group.Select(value => value.BuildGrammarSignature())
                        .Distinct(StringComparer.Ordinal).Count() != 1))
                throw new InvalidOperationException("LandscapeGrammarVariantParityInvalid");

            월드CompositionContractValidator.Validate(
                entries.Select(value => value.Descriptor).ToArray());
        }
    }
}
