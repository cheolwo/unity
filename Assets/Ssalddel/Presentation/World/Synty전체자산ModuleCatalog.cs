using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class Synty전체자산SourcePackCodes
    {
        public const string Nature = "nature";
        public const string Farm = "farm";
        public const string Town = "town";
        public const string City = "city";
        public const string Construction = "construction";
        public const string Generic = "generic";
        public const string Starter = "starter";
        public const string AnimationBaseLocomotion = "animation-base-locomotion";
        public const string AnimationEmotesAndTaunts = "animation-emotes-and-taunts";
        public const string AnimationSwordCombat = "animation-sword-combat";
        public const string AlpineMountain = "alpine-mountain";
        public const string DungeonRealms = "dungeon-realms";
        public const string DwarvenDungeonMap = "dwarven-dungeon-map";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Nature, Farm, Town, City, Construction, Generic, Starter,
            AnimationBaseLocomotion, AnimationEmotesAndTaunts,
            AnimationSwordCombat, AlpineMountain, DungeonRealms,
            DwarvenDungeonMap,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    public static class Synty표현범위Codes
    {
        public const string Outdoor = "Outdoor";
        public const string Interior = "Interior";
        public const string Shared = "Shared";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Outdoor, Interior, Shared,
        };

        public static string 사람이름(string 범위Code) => 범위Code switch
        {
            Outdoor => "실외 표현",
            Interior => "실내 표현",
            Shared => "공통 표현",
            _ => string.Empty,
        };
    }

    public sealed class Synty자산기능군기준
    {
        public Synty자산기능군기준(
            string 범위Code,
            string 기능군Code,
            string 기능군이름)
        {
            this.범위Code = 범위Code ?? string.Empty;
            this.기능군Code = 기능군Code ?? string.Empty;
            this.기능군이름 = 기능군이름 ?? string.Empty;
        }

        public string 범위Code { get; }
        public string 범위이름 => Synty표현범위Codes.사람이름(범위Code);
        public string 기능군Code { get; }
        public string 기능군이름 { get; }

        public string 사람이읽는경로 => $"{범위이름} → {기능군이름}";
    }

    public static class Synty자산ModuleCodes
    {
        public const string WorldSurface = "world-surface";
        public const string NatureVegetation = "nature-vegetation";
        public const string ExteriorStructure = "exterior-structure";
        public const string ExteriorFunctionalProp = "exterior-functional-prop";
        public const string WorldNetwork = "world-network";
        public const string WorldTransition = "world-transition";
        public const string ConstructionState = "construction-state";
        public const string InteriorStructure = "interior-structure";
        public const string InteriorFixture = "interior-fixture";
        public const string InteriorLooseItem = "interior-loose-item";
        public const string ActorVehicleTool = "actor-vehicle-tool";
        public const string WorldFeedbackFx = "world-feedback-fx";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            WorldSurface, NatureVegetation, ExteriorStructure,
            ExteriorFunctionalProp, WorldNetwork, WorldTransition,
            ConstructionState, InteriorStructure, InteriorFixture,
            InteriorLooseItem, ActorVehicleTool, WorldFeedbackFx,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);

        public static IReadOnlyList<string> FromPlacementRole(string roleCode) =>
            roleCode switch
            {
                "spatial-base" => new[] { WorldSurface, NatureVegetation, ExteriorStructure },
                "functional-anchor" => new[] { ExteriorFunctionalProp, ActorVehicleTool },
                "state-overlay" => new[] { ConstructionState, WorldFeedbackFx },
                "ambient-detail" => new[] { NatureVegetation, ExteriorFunctionalProp },
                "interior-fixture" => new[] { InteriorFixture, InteriorStructure },
                "interior-loose-item" => new[] { InteriorLooseItem },
                "feedback-fx" => new[] { WorldFeedbackFx },
                "actor" => new[] { ActorVehicleTool },
                _ => Array.Empty<string>(),
            };
    }

    /// <summary>
    /// 사람이 설계·검토할 때는 한국어 이름을 기준으로 읽고, 기존 영문 Module Code는
    /// 저장·폐루프 연결 호환을 위해 유지한다. 세부 기능군과 자산 종류는 기술 대장에서
    /// 자산 계열 Stable ID 및 실제 Prefab으로 이어진다.
    /// </summary>
    public static class Synty자산기능분류기준
    {
        public static IReadOnlyList<Synty자산기능군기준> All { get; } = new[]
        {
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.WorldSurface, "월드 지면"),
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.NatureVegetation, "자연 식생"),
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.ExteriorStructure, "실외 구조물"),
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.ExteriorFunctionalProp, "실외 기능 소품"),
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.WorldNetwork, "도로·통행망"),
            new Synty자산기능군기준(Synty표현범위Codes.Outdoor, Synty자산ModuleCodes.WorldTransition, "영역 전이"),
            new Synty자산기능군기준(Synty표현범위Codes.Interior, Synty자산ModuleCodes.InteriorStructure, "실내 구조"),
            new Synty자산기능군기준(Synty표현범위Codes.Interior, Synty자산ModuleCodes.InteriorFixture, "실내 설비"),
            new Synty자산기능군기준(Synty표현범위Codes.Interior, Synty자산ModuleCodes.InteriorLooseItem, "실내 소품"),
            new Synty자산기능군기준(Synty표현범위Codes.Shared, Synty자산ModuleCodes.ConstructionState, "건설·복구 표현"),
            new Synty자산기능군기준(Synty표현범위Codes.Shared, Synty자산ModuleCodes.ActorVehicleTool, "인물·차량·도구"),
            new Synty자산기능군기준(Synty표현범위Codes.Shared, Synty자산ModuleCodes.WorldFeedbackFx, "세계 피드백 효과"),
        };

        public static Synty자산기능군기준 찾기(string 기능군Code) =>
            All.FirstOrDefault(value => value.기능군Code == 기능군Code);
    }

    public static class Synty자산LifecycleCodes
    {
        public const string ProductionReady = "production-ready";
        public const string NeedsReview = "needs-review";
        public const string SharedBase = "shared-base";
        public const string PrototypeFallback = "prototype-fallback";
        public const string ReservedForFutureLoop = "reserved-for-future-loop";
        public const string Excluded = "excluded";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            ProductionReady, NeedsReview, SharedBase, PrototypeFallback,
            ReservedForFutureLoop, Excluded,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    public sealed class Synty자산ModuleClassification
    {
        public Synty자산ModuleClassification(
            string lifecycleCode,
            IEnumerable<string> moduleCodes,
            string holdReasonCode)
        {
            LifecycleCode = lifecycleCode ?? string.Empty;
            ModuleCodes = (moduleCodes ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            HoldReasonCode = holdReasonCode ?? string.Empty;
        }

        public string LifecycleCode { get; }
        public IReadOnlyList<string> ModuleCodes { get; }
        public string HoldReasonCode { get; }

        public bool Validate() =>
            Synty자산LifecycleCodes.IsKnown(LifecycleCode)
            && ModuleCodes.All(Synty자산ModuleCodes.IsKnown)
            && (ModuleCodes.Count > 0 || !string.IsNullOrWhiteSpace(HoldReasonCode));
    }

    public static class Synty자산ModuleClassifier
    {
        private static readonly string[] NetworkTokens =
            { "road", "street", "path", "bridge", "rail", "track", "traffic", "sign" };
        private static readonly string[] TransitionTokens =
            { "gate", "portal", "tunnel", "entrance", "exit", "doorway", "arch" };
        private static readonly string[] VegetationTokens =
            { "tree", "plant", "grass", "bush", "flower", "rock", "stump", "log" };
        private static readonly string[] SurfaceTokens =
            { "terrain", "ground", "landscape", "floor", "water", "river", "cliff" };
        private static readonly string[] StructureTokens =
            { "building", "house", "wall", "roof", "window", "door", "stairs", "fence", "shed" };
        private static readonly string[] InteriorTokens =
            { "interior", "bed", "sofa", "chair", "table", "desk", "shelf", "cabinet", "sink", "toilet", "bath" };
        private static readonly string[] LooseItemTokens =
            { "cup", "plate", "bowl", "book", "bottle", "food", "box", "crate", "bag", "pot", "pan" };
        private static readonly string[] ActorToolTokens =
            { "character", "vehicle", "car", "truck", "tractor", "tool", "weapon", "axe", "hammer" };
        private static readonly string[] FxTokens =
            { "fx", "particle", "smoke", "fire", "spark", "dust", "rain", "cloud" };

        public static Synty자산ModuleClassification Classify(
            string sourcePackCode,
            string assetPath)
        {
            if (!Synty전체자산SourcePackCodes.IsKnown(sourcePackCode))
                throw new ArgumentException("SyntySourcePackUnknown", nameof(sourcePackCode));
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("SyntyAssetPathMissing", nameof(assetPath));

            var normalized = assetPath.Replace('\\', '/').ToLowerInvariant();
            var modules = new HashSet<string>(StringComparer.Ordinal);

            if (ContainsAny(normalized, FxTokens)) modules.Add(Synty자산ModuleCodes.WorldFeedbackFx);
            if (ContainsAny(normalized, ActorToolTokens)) modules.Add(Synty자산ModuleCodes.ActorVehicleTool);
            if (ContainsAny(normalized, NetworkTokens)) modules.Add(Synty자산ModuleCodes.WorldNetwork);
            if (ContainsAny(normalized, TransitionTokens)) modules.Add(Synty자산ModuleCodes.WorldTransition);
            if (ContainsAny(normalized, VegetationTokens)) modules.Add(Synty자산ModuleCodes.NatureVegetation);
            if (ContainsAny(normalized, SurfaceTokens)) modules.Add(Synty자산ModuleCodes.WorldSurface);
            if (ContainsAny(normalized, StructureTokens)) modules.Add(Synty자산ModuleCodes.ExteriorStructure);
            if (ContainsAny(normalized, InteriorTokens))
            {
                modules.Add(Synty자산ModuleCodes.InteriorFixture);
                if (normalized.Contains("wall") || normalized.Contains("floor")
                    || normalized.Contains("door") || normalized.Contains("window")
                    || normalized.Contains("stairs"))
                {
                    modules.Add(Synty자산ModuleCodes.InteriorStructure);
                }
            }
            if (ContainsAny(normalized, LooseItemTokens)) modules.Add(Synty자산ModuleCodes.InteriorLooseItem);
            if (normalized.Contains("/props/") || normalized.Contains("/items/"))
                modules.Add(Synty자산ModuleCodes.ExteriorFunctionalProp);
            if (sourcePackCode == Synty전체자산SourcePackCodes.Construction)
                modules.Add(Synty자산ModuleCodes.ConstructionState);

            var lifecycle = sourcePackCode switch
            {
                Synty전체자산SourcePackCodes.Generic => Synty자산LifecycleCodes.SharedBase,
                Synty전체자산SourcePackCodes.Starter => Synty자산LifecycleCodes.PrototypeFallback,
                _ => Synty자산LifecycleCodes.NeedsReview,
            };
            var holdReason = modules.Count == 0
                ? (sourcePackCode == Synty전체자산SourcePackCodes.Starter
                    ? "PrototypeFallbackNotAssignedToPlayableLoop"
                    : "ModuleMeaningNeedsReview")
                : string.Empty;
            return new Synty자산ModuleClassification(lifecycle, modules, holdReason);
        }

        private static bool ContainsAny(string value, IEnumerable<string> tokens) =>
            tokens.Any(value.Contains);
    }

    [Serializable]
    public sealed class Synty자산ModuleEntry
    {
        [SerializeField] private string assetStableId = string.Empty;
        [SerializeField] private string sourcePackCode = string.Empty;
        [SerializeField] private string assetFamilyId = string.Empty;
        [SerializeField] private string[] moduleCodes = Array.Empty<string>();
        [SerializeField] private string lifecycleCode = string.Empty;
        [SerializeField] private string holdReasonCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string sourceFingerprintSha256 = string.Empty;
        [SerializeField] private bool presentationOnly = true;

        public string AssetStableId => assetStableId;
        public string SourcePackCode => sourcePackCode;
        public string AssetFamilyId => assetFamilyId;
        public IReadOnlyList<string> ModuleCodes => moduleCodes;
        public string LifecycleCode => lifecycleCode;
        public string HoldReasonCode => holdReasonCode;
        public GameObject Prefab => prefab;
        public string SourceFingerprintSha256 => sourceFingerprintSha256;
        public bool PresentationOnly => presentationOnly;

        // 사람이 읽는 새 분류 API. 기존 영문 속성은 Scene·대장 호환을 위해 유지한다.
        public string 자산계열StableId => assetFamilyId;
        public IReadOnlyList<string> 기능군Codes => moduleCodes;
        public GameObject 실제Prefab => prefab;

        public void Configure(
            string stableId,
            string packCode,
            string familyId,
            IEnumerable<string> modules,
            string lifecycle,
            string holdReason,
            GameObject sourcePrefab,
            string fingerprint)
        {
            assetStableId = stableId ?? string.Empty;
            sourcePackCode = packCode ?? string.Empty;
            assetFamilyId = familyId ?? string.Empty;
            moduleCodes = (modules ?? Array.Empty<string>()).ToArray();
            lifecycleCode = lifecycle ?? string.Empty;
            holdReasonCode = holdReason ?? string.Empty;
            prefab = sourcePrefab;
            sourceFingerprintSha256 = fingerprint ?? string.Empty;
            presentationOnly = true;
        }

        public bool Validate() =>
            !string.IsNullOrWhiteSpace(assetStableId)
            && Synty전체자산SourcePackCodes.IsKnown(sourcePackCode)
            && !string.IsNullOrWhiteSpace(assetFamilyId)
            && moduleCodes != null
            && moduleCodes.All(Synty자산ModuleCodes.IsKnown)
            && moduleCodes.Distinct(StringComparer.Ordinal).Count() == moduleCodes.Length
            && Synty자산LifecycleCodes.IsKnown(lifecycleCode)
            && (moduleCodes.Length > 0 || !string.IsNullOrWhiteSpace(holdReasonCode))
            && prefab != null
            && sourceFingerprintSha256.Length == 64
            && sourceFingerprintSha256.All(Uri.IsHexDigit)
            && presentationOnly;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/Synty 전체 자산 기능 모듈 대장")]
    public sealed class Synty전체자산ModuleCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private string sourceHashSha256 = string.Empty;
        [SerializeField] private Synty자산ModuleEntry[] entries = Array.Empty<Synty자산ModuleEntry>();

        public string CatalogRevision => catalogRevision;
        public string SourceHashSha256 => sourceHashSha256;
        public IReadOnlyList<Synty자산ModuleEntry> Entries => entries;

        public void Configure(string revision, string hash, Synty자산ModuleEntry[] values)
        {
            catalogRevision = revision ?? string.Empty;
            sourceHashSha256 = hash ?? string.Empty;
            entries = values ?? Array.Empty<Synty자산ModuleEntry>();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || sourceHashSha256.Length != 64
                || !sourceHashSha256.All(Uri.IsHexDigit)
                || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.AssetStableId).Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Select(value => value.Prefab).Distinct().Count() != entries.Length
                || Synty전체자산SourcePackCodes.All.Any(pack => entries.All(value => value.SourcePackCode != pack)))
            {
                throw new InvalidOperationException("SyntyAllAssetModuleCatalogInvalid");
            }
        }

        public IReadOnlyList<Synty자산ModuleEntry> FindCandidates(
            IEnumerable<string> moduleCodes,
            string assetFamilyId = "")
        {
            var required = (moduleCodes ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            return entries
                .Where(value => value.LifecycleCode != Synty자산LifecycleCodes.Excluded)
                .Where(value => value.LifecycleCode != Synty자산LifecycleCodes.PrototypeFallback)
                .Where(value => string.IsNullOrWhiteSpace(assetFamilyId)
                    || value.AssetFamilyId == assetFamilyId)
                .Where(value => required.Length == 0
                    || value.ModuleCodes.Any(required.Contains))
                .OrderBy(value => value.AssetStableId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public readonly struct Synty자산ModuleRef
    {
        public Synty자산ModuleRef(string moduleCode, bool required)
        {
            ModuleCode = moduleCode ?? string.Empty;
            Required = required;
        }

        public string ModuleCode { get; }
        public bool Required { get; }
    }
}
