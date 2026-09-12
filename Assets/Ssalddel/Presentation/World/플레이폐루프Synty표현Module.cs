using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 플레이폐루프표현MomentCodes
    {
        public const string Entry = "Entry";
        public const string Choice = "Choice";
        public const string Progress = "Progress";
        public const string Success = "Success";
        public const string FailureRecovery = "FailureRecovery";
        public const string Return = "Return";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Entry, Choice, Progress, Success, FailureRecovery, Return,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    public static class Synty표현PlacementRoleCodes
    {
        public const string SpatialBase = "spatial-base";
        public const string FunctionalAnchor = "functional-anchor";
        public const string StateOverlay = "state-overlay";
        public const string AmbientDetail = "ambient-detail";
        public const string InteriorFixture = "interior-fixture";
        public const string InteriorLooseItem = "interior-loose-item";
        public const string FeedbackFx = "feedback-fx";
        public const string Actor = "actor";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            SpatialBase, FunctionalAnchor, StateOverlay, AmbientDetail,
            InteriorFixture, InteriorLooseItem, FeedbackFx, Actor,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    public sealed class 플레이폐루프Synty표현Slot
    {
        public 플레이폐루프Synty표현Slot(
            string slotStableId,
            string worldInteractionId,
            string loopMomentCode,
            string placementRoleCode,
            string[] authorityStateCodes,
            string[] assetFamilyIds,
            string[]? assetModuleCodes = null)
        {
            SlotStableId = slotStableId ?? string.Empty;
            WorldInteractionId = worldInteractionId ?? string.Empty;
            LoopMomentCode = loopMomentCode ?? string.Empty;
            PlacementRoleCode = placementRoleCode ?? string.Empty;
            AuthorityStateCodes = authorityStateCodes ?? Array.Empty<string>();
            AssetFamilyIds = assetFamilyIds ?? Array.Empty<string>();
            AssetModuleCodes = assetModuleCodes
                ?? Synty자산ModuleCodes.FromPlacementRole(PlacementRoleCode).ToArray();
        }

        public string SlotStableId { get; }
        public string WorldInteractionId { get; }
        public string LoopMomentCode { get; }
        public string PlacementRoleCode { get; }
        public IReadOnlyList<string> AuthorityStateCodes { get; }
        public IReadOnlyList<string> AssetFamilyIds { get; }
        public IReadOnlyList<string> AssetModuleCodes { get; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SlotStableId)
                || string.IsNullOrWhiteSpace(WorldInteractionId)
                || !플레이폐루프표현MomentCodes.IsKnown(LoopMomentCode)
                || !Synty표현PlacementRoleCodes.IsKnown(PlacementRoleCode)
                || AuthorityStateCodes.Count == 0
                || AuthorityStateCodes.Any(string.IsNullOrWhiteSpace)
                || AuthorityStateCodes.Distinct(StringComparer.Ordinal).Count()
                    != AuthorityStateCodes.Count
                || AssetFamilyIds.Count == 0
                || AssetFamilyIds.Any(value => string.IsNullOrWhiteSpace(value)
                    || !value.StartsWith("synty-family:", StringComparison.Ordinal))
                || AssetFamilyIds.Distinct(StringComparer.Ordinal).Count()
                    != AssetFamilyIds.Count
                || AssetModuleCodes.Count == 0
                || AssetModuleCodes.Any(value => !Synty자산ModuleCodes.IsKnown(value))
                || AssetModuleCodes.Distinct(StringComparer.Ordinal).Count()
                    != AssetModuleCodes.Count)
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyExpressionSlotInvalid:" + SlotStableId);
            }
        }

        public bool Accepts(string authorityStateCode) =>
            AuthorityStateCodes.Contains(authorityStateCode, StringComparer.Ordinal);
    }

    public sealed class 플레이폐루프Synty표현Module
    {
        public 플레이폐루프Synty표현Module(
            string moduleStableId,
            string loopStableId,
            string moduleRevision,
            string[] sharedModuleRefs,
            string[] worldInteractionIds,
            string[] requiredHCapabilities,
            플레이폐루프Synty표현Slot[] slots)
        {
            ModuleStableId = moduleStableId ?? string.Empty;
            LoopStableId = loopStableId ?? string.Empty;
            ModuleRevision = moduleRevision ?? string.Empty;
            SharedModuleRefs = sharedModuleRefs ?? Array.Empty<string>();
            WorldInteractionIds = worldInteractionIds ?? Array.Empty<string>();
            RequiredHCapabilities = requiredHCapabilities ?? Array.Empty<string>();
            Slots = slots ?? Array.Empty<플레이폐루프Synty표현Slot>();
        }

        public string ModuleStableId { get; }
        public string LoopStableId { get; }
        public string ModuleRevision { get; }
        public IReadOnlyList<string> SharedModuleRefs { get; }
        public IReadOnlyList<string> WorldInteractionIds { get; }
        public IReadOnlyList<string> RequiredHCapabilities { get; }
        public IReadOnlyList<플레이폐루프Synty표현Slot> Slots { get; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ModuleStableId)
                || string.IsNullOrWhiteSpace(LoopStableId)
                || string.IsNullOrWhiteSpace(ModuleRevision)
                || WorldInteractionIds.Count == 0
                || WorldInteractionIds.Any(string.IsNullOrWhiteSpace)
                || WorldInteractionIds.Distinct(StringComparer.Ordinal).Count()
                    != WorldInteractionIds.Count
                || RequiredHCapabilities.Count == 0
                || RequiredHCapabilities.Any(string.IsNullOrWhiteSpace)
                || Slots.Count == 0
                || Slots.Any(value => value == null)
                || Slots.Select(value => value.SlotStableId)
                    .Distinct(StringComparer.Ordinal).Count() != Slots.Count)
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyExpressionModuleInvalid:" + ModuleStableId);
            }

            foreach (var slot in Slots)
            {
                slot.Validate();
                if (!WorldInteractionIds.Contains(
                    slot.WorldInteractionId, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        "PlayableLoopSyntyExpressionSlotOutsideModule:"
                        + slot.SlotStableId);
                }
            }

            if (WorldInteractionIds.Any(worldInteractionId =>
                Slots.All(slot => slot.WorldInteractionId != worldInteractionId)))
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyExpressionWiCoverageMissing:" + ModuleStableId);
            }
        }
    }

    public sealed class 플레이폐루프Synty표현SelectionRequest
    {
        public 플레이폐루프Synty표현SelectionRequest(
            long worldSeed,
            string loopStableId,
            string worldInteractionId,
            string slotStableId,
            string placementStableId,
            string authorityStateCode,
            string assetModuleRevision = "")
        {
            WorldSeed = worldSeed;
            LoopStableId = loopStableId ?? string.Empty;
            WorldInteractionId = worldInteractionId ?? string.Empty;
            SlotStableId = slotStableId ?? string.Empty;
            PlacementStableId = placementStableId ?? string.Empty;
            AuthorityStateCode = authorityStateCode ?? string.Empty;
            AssetModuleRevision = assetModuleRevision ?? string.Empty;
        }

        public long WorldSeed { get; }
        public string LoopStableId { get; }
        public string WorldInteractionId { get; }
        public string SlotStableId { get; }
        public string PlacementStableId { get; }
        public string AuthorityStateCode { get; }
        public string AssetModuleRevision { get; }
    }

    public sealed class 플레이폐루프Synty표현Selection
    {
        public 플레이폐루프Synty표현Selection(
            string moduleStableId,
            string moduleRevision,
            string slotStableId,
            string placementRoleCode,
            IReadOnlyList<string> assetModuleCodes,
            string assetFamilyId,
            ulong selectionHash)
        {
            ModuleStableId = moduleStableId;
            ModuleRevision = moduleRevision;
            SlotStableId = slotStableId;
            PlacementRoleCode = placementRoleCode;
            AssetModuleCodes = assetModuleCodes ?? Array.Empty<string>();
            AssetFamilyId = assetFamilyId;
            SelectionHash = selectionHash;
        }

        public string ModuleStableId { get; }
        public string ModuleRevision { get; }
        public string SlotStableId { get; }
        public string PlacementRoleCode { get; }
        public IReadOnlyList<string> AssetModuleCodes { get; }
        public string AssetFamilyId { get; }
        public ulong SelectionHash { get; }
    }

    public static class 플레이폐루프Synty표현Resolver
    {
        public static 플레이폐루프Synty표현Selection Resolve(
            플레이폐루프Synty표현Module module,
            플레이폐루프Synty표현SelectionRequest request)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            module.Validate();

            if (module.LoopStableId != request.LoopStableId
                || string.IsNullOrWhiteSpace(request.PlacementStableId))
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyExpressionRequestInvalid");
            }

            var slot = module.Slots.SingleOrDefault(value =>
                value.SlotStableId == request.SlotStableId
                && value.WorldInteractionId == request.WorldInteractionId
                && value.Accepts(request.AuthorityStateCode));
            if (slot == null)
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyExpressionSlotNotFound:"
                    + request.SlotStableId + ":" + request.AuthorityStateCode);
            }

            var candidates = slot.AssetFamilyIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var hash = StableHash(
                request.WorldSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.PlacementStableId,
                module.ModuleRevision,
                request.AssetModuleRevision,
                slot.SlotStableId,
                request.AuthorityStateCode);
            var index = (int)(hash % (ulong)candidates.Length);
            return new 플레이폐루프Synty표현Selection(
                module.ModuleStableId,
                module.ModuleRevision,
                slot.SlotStableId,
                slot.PlacementRoleCode,
                slot.AssetModuleCodes,
                candidates[index],
                hash);
        }

        public static Synty표현SelectionPlan Resolve(
            플레이폐루프Synty표현Module module,
            플레이폐루프Synty표현Context context,
            Synty전체자산ModuleCatalog assetModules)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (assetModules == null) throw new ArgumentNullException(nameof(assetModules));
            assetModules.Validate();
            var selection = Resolve(module, new 플레이폐루프Synty표현SelectionRequest(
                context.WorldSeed,
                context.LoopStableId,
                context.WorldInteractionId,
                context.SlotStableId,
                context.PlacementStableId,
                context.AuthorityStateCode,
                assetModules.CatalogRevision));
            var candidates = assetModules.FindCandidates(
                selection.AssetModuleCodes,
                selection.AssetFamilyId);
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyAssetModuleCandidateMissing:"
                    + selection.AssetFamilyId);
            }

            var selectedAsset = candidates[(int)(selection.SelectionHash % (ulong)candidates.Count)];
            return new Synty표현SelectionPlan(
                selection,
                assetModules.CatalogRevision,
                selectedAsset.AssetStableId,
                selectedAsset.Prefab);
        }

        public static SyntyPackAssetInventoryEntry ResolvePrefabEntry(
            SyntyPackAssetInventoryCatalog inventory,
            플레이폐루프Synty표현Selection selection,
            string placementStableId)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (string.IsNullOrWhiteSpace(placementStableId))
                throw new ArgumentException("PlacementStableIdMissing", nameof(placementStableId));

            inventory.Validate();
            var candidates = inventory.Entries
                .Where(value => value.AssetFamilyId == selection.AssetFamilyId
                    && value.ClassificationStateCode !=
                        SyntyAssetClassificationStateCodes.Excluded
                    && value.ClassificationStateCode !=
                        SyntyAssetClassificationStateCodes.NeedsHumanReview)
                .OrderBy(value => value.InventoryId, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    "PlayableLoopSyntyAssetFamilyUnresolved:"
                    + selection.AssetFamilyId);
            }

            var hash = StableHash(
                selection.SelectionHash.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                placementStableId,
                selection.AssetFamilyId);
            return candidates[(int)(hash % (ulong)candidates.Length)];
        }

        private static ulong StableHash(params string[] values)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                foreach (var value in values)
                {
                    foreach (var character in value ?? string.Empty)
                    {
                        hash ^= character;
                        hash *= prime;
                    }
                    hash ^= 0xff;
                    hash *= prime;
                }
                return hash;
            }
        }
    }

    public sealed class 플레이폐루프Synty표현Context
    {
        public 플레이폐루프Synty표현Context(
            long worldSeed,
            string loopStableId,
            string worldInteractionId,
            string slotStableId,
            string placementStableId,
            string authorityStateCode)
        {
            WorldSeed = worldSeed;
            LoopStableId = loopStableId ?? string.Empty;
            WorldInteractionId = worldInteractionId ?? string.Empty;
            SlotStableId = slotStableId ?? string.Empty;
            PlacementStableId = placementStableId ?? string.Empty;
            AuthorityStateCode = authorityStateCode ?? string.Empty;
        }

        public long WorldSeed { get; }
        public string LoopStableId { get; }
        public string WorldInteractionId { get; }
        public string SlotStableId { get; }
        public string PlacementStableId { get; }
        public string AuthorityStateCode { get; }
    }

    public sealed class Synty표현SelectionPlan
    {
        public Synty표현SelectionPlan(
            플레이폐루프Synty표현Selection selection,
            string assetModuleRevision,
            string assetStableId,
            UnityEngine.GameObject prefab)
        {
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            AssetModuleRevision = assetModuleRevision ?? string.Empty;
            AssetStableId = assetStableId ?? string.Empty;
            Prefab = prefab;
        }

        public 플레이폐루프Synty표현Selection Selection { get; }
        public string AssetModuleRevision { get; }
        public string AssetStableId { get; }
        public UnityEngine.GameObject Prefab { get; }
    }
}
