using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    public static class Nature플레이폐루프Synty표현Modules
    {
        public const string ShelterFoundation =
            "playable-loop:nature-shelter-foundation.v1";
        public const string TwilightReturn =
            "playable-loop:nature-twilight-return.v1";
        public const string NightDay2 =
            "playable-loop:nature-night-day2.v1";
        public const string WorkbenchFoundation =
            "playable-loop:nature-workbench-foundation.v1";

        public static IReadOnlyList<플레이폐루프Synty표현Module> All { get; } =
            new[]
            {
                CreateShelterFoundation(),
                CreateTwilightReturn(),
                CreateNightDay2(),
                CreateWorkbenchFoundation(),
            };

        public static 플레이폐루프Synty표현Module Find(string loopStableId)
        {
            var module = All.SingleOrDefault(value =>
                value.LoopStableId == loopStableId);
            return module ?? throw new InvalidOperationException(
                "NaturePlayableLoopSyntyModuleMissing:" + loopStableId);
        }

        private static 플레이폐루프Synty표현Module CreateShelterFoundation() =>
            Module(
                "synty-loop:nature-shelter-foundation.v1",
                ShelterFoundation,
                "nature-shelter-synty.r1",
                new[]
                {
                    "synty-shared:nature-ground.v1",
                    "synty-shared:construction-progress.v1",
                },
                new[]
                {
                    "WI-NATURE-05", "WI-NATURE-06", "WI-NATURE-18",
                    "WI-NATURE-07", "WI-NATURE-08", "WI-NATURE-09",
                    "WI-NATURE-10", "WI-NATURE-12",
                },
                new[]
                {
                    "ToolAccessible", "HarvestWorkArea",
                    "ShelterBuildingSite", "RecoveryWorkArea",
                },
                Slot("nature-shelter.tool-choice", "WI-NATURE-05",
                    플레이폐루프표현MomentCodes.Choice,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "ToolChoiceAvailable" },
                    "synty-family:farm:props:axe"),
                Slot("nature-shelter.harvest-progress", "WI-NATURE-06",
                    플레이폐루프표현MomentCodes.Progress,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "TreeHarvestTaskActive" },
                    "synty-family:nature:trees:birch",
                    "synty-family:nature:trees:pine",
                    "synty-family:nature:trees:large"),
                Slot("nature-shelter.timber-result", "WI-NATURE-18",
                    플레이폐루프표현MomentCodes.Success,
                    Synty표현PlacementRoleCodes.StateOverlay,
                    new[] { "TimberPickupAvailable" },
                    "synty-family:nature:trees:log"),
                Slot("nature-shelter.site-choice", "WI-NATURE-07",
                    플레이폐루프표현MomentCodes.Choice,
                    Synty표현PlacementRoleCodes.SpatialBase,
                    new[] { "ShelterSiteChoiceAvailable" },
                    "synty-family:nature:plants:grass",
                    "synty-family:nature:plants:undergrowth",
                    "synty-family:nature:rocks:small"),
                Slot("nature-shelter.build-progress", "WI-NATURE-08",
                    플레이폐루프표현MomentCodes.Progress,
                    Synty표현PlacementRoleCodes.StateOverlay,
                    new[] { "CabinConstructionActive" },
                    "synty-family:construction:props:wood-frame",
                    "synty-family:construction:props:plywood-stack",
                    "synty-family:construction:props:scaffold-preset"),
                Slot("nature-shelter.enter", "WI-NATURE-09",
                    플레이폐루프표현MomentCodes.Success,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "CabinOperational" },
                    "synty-family:farm:buildings:shelter"),
                Slot("nature-shelter.exit-return", "WI-NATURE-10",
                    플레이폐루프표현MomentCodes.Return,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "NatureSafeChoiceAvailable" },
                    "synty-family:nature:props:campfire",
                    "synty-family:nature:props:torchstick"),
                Slot("nature-shelter.cancel-recovery", "WI-NATURE-12",
                    플레이폐루프표현MomentCodes.FailureRecovery,
                    Synty표현PlacementRoleCodes.StateOverlay,
                    new[] { "NatureWorkCancelled" },
                    "synty-family:construction:props:crate",
                    "synty-family:construction:props:plywood-stack"));

        private static 플레이폐루프Synty표현Module CreateTwilightReturn() =>
            Module(
                "synty-loop:nature-twilight-return.v1",
                TwilightReturn,
                "nature-twilight-synty.r1",
                new[]
                {
                    "synty-shared:nature-ground.v1",
                    "synty-shared:nature-atmosphere.v1",
                },
                new[] { "WI-NATURE-01", "WI-NATURE-11" },
                new[] { "ThreatEncounterArea", "ThreatRetreatRoute", "ShelterOccupancy" },
                Slot("nature-twilight.warning", "WI-NATURE-01",
                    플레이폐루프표현MomentCodes.Entry,
                    Synty표현PlacementRoleCodes.FeedbackFx,
                    new[] { "TwilightEncounterReady" },
                    "synty-family:nature:fx:fireflies",
                    "synty-family:nature:fx:glowing-dust",
                    "synty-family:nature:fx:smoke-light"),
                Slot("nature-twilight.encounter", "WI-NATURE-11",
                    플레이폐루프표현MomentCodes.Progress,
                    Synty표현PlacementRoleCodes.Actor,
                    new[] { "ThreatEncounterActive" },
                    "synty-family:nature:props:skeleton-ground"),
                Slot("nature-twilight.recovery-return", "WI-NATURE-11",
                    플레이폐루프표현MomentCodes.Return,
                    Synty표현PlacementRoleCodes.FeedbackFx,
                    new[]
                    {
                        "TwilightThreatResolvedOrRetreated",
                        "NatureSafeChoiceAvailable",
                    },
                    "synty-family:nature:fx:fire-small",
                    "synty-family:nature:props:campfire"));

        private static 플레이폐루프Synty표현Module CreateNightDay2() =>
            Module(
                "synty-loop:nature-night-day2.v1",
                NightDay2,
                "nature-night-day2-synty.r1",
                new[]
                {
                    "synty-shared:shelter-interior.v1",
                    "synty-shared:nature-atmosphere.v1",
                    "synty-shared:construction-progress.v1",
                },
                new[] { "WI-NATURE-13", "WI-NATURE-14", "WI-NATURE-15" },
                new[] { "ShelterStorage", "ShelterSleep", "DawnPlanChoice" },
                Slot("nature-night.storage-empty", "WI-NATURE-13",
                    플레이폐루프표현MomentCodes.Success,
                    Synty표현PlacementRoleCodes.InteriorFixture,
                    new[] { "ShelterStorageAvailable" },
                    "synty-family:farm:props:rack"),
                Slot("nature-night.storage-partial", "WI-NATURE-13",
                    플레이폐루프표현MomentCodes.Success,
                    Synty표현PlacementRoleCodes.InteriorLooseItem,
                    new[] { "ShelterStoragePartiallyFilled" },
                    "synty-family:farm:props:crate",
                    "synty-family:construction:props:crate"),
                Slot("nature-night.storage-full", "WI-NATURE-13",
                    플레이폐루프표현MomentCodes.Success,
                    Synty표현PlacementRoleCodes.InteriorLooseItem,
                    new[] { "ShelterStorageFull" },
                    "synty-family:construction:props:crate-stack"),
                Slot("nature-night.sleep", "WI-NATURE-14",
                    플레이폐루프표현MomentCodes.Progress,
                    Synty표현PlacementRoleCodes.InteriorFixture,
                    new[] { "ShelterSleepActive" },
                    "synty-family:farm:props:bed",
                    "synty-family:town:props:bed-single"),
                Slot("nature-night.dawn-return", "WI-NATURE-14",
                    플레이폐루프표현MomentCodes.Return,
                    Synty표현PlacementRoleCodes.FeedbackFx,
                    new[] { "DawnReached" },
                    "synty-family:nature:fx:sunbeams-particle",
                    "synty-family:nature:fx:fire-small"),
                Slot("nature-night.day2-workbench-plan", "WI-NATURE-15",
                    플레이폐루프표현MomentCodes.Choice,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "WorkbenchExpansionPlanSelected" },
                    "synty-family:farm:props:table",
                    "synty-family:construction:props:toolbox"),
                Slot("nature-night.day2-storage-plan", "WI-NATURE-15",
                    플레이폐루프표현MomentCodes.Choice,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "StorageExpansionPlanSelected" },
                    "synty-family:farm:props:rack",
                    "synty-family:construction:props:crate-stack"),
                Slot("nature-night.day2-barricade-plan", "WI-NATURE-15",
                    플레이폐루프표현MomentCodes.Choice,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[] { "BarricadeExpansionPlanSelected" },
                    "synty-family:farm:props:fence-wood",
                    "synty-family:construction:props:barrier"));

        private static 플레이폐루프Synty표현Module CreateWorkbenchFoundation() =>
            Module(
                "synty-loop:nature-workbench-foundation.v1",
                WorkbenchFoundation,
                "nature-workbench-synty.r1",
                new[] { "synty-shared:construction-progress.v1" },
                new[] { "WI-CON-01", "WI-NATURE-12" },
                new[] { "AreaBuildingPlacementAllowed", "CraftingWorkArea" },
                Slot("nature-workbench.build", "WI-CON-01",
                    플레이폐루프표현MomentCodes.Progress,
                    Synty표현PlacementRoleCodes.FunctionalAnchor,
                    new[]
                    {
                        "NatureWorkbenchConstructionActive",
                        "NatureWorkbenchOperational",
                    },
                    "synty-family:farm:props:table",
                    "synty-family:construction:props:wooden-table-top",
                    "synty-family:construction:props:toolbox"),
                Slot("nature-workbench.cancel-recovery", "WI-NATURE-12",
                    플레이폐루프표현MomentCodes.FailureRecovery,
                    Synty표현PlacementRoleCodes.StateOverlay,
                    new[] { "NatureWorkCancelled" },
                    "synty-family:construction:props:crate",
                    "synty-family:construction:props:plywood-stack"));

        private static 플레이폐루프Synty표현Module Module(
            string moduleStableId,
            string loopStableId,
            string revision,
            string[] sharedModuleRefs,
            string[] worldInteractionIds,
            string[] requiredHCapabilities,
            params 플레이폐루프Synty표현Slot[] slots)
        {
            var module = new 플레이폐루프Synty표현Module(
                moduleStableId,
                loopStableId,
                revision,
                sharedModuleRefs,
                worldInteractionIds,
                requiredHCapabilities,
                slots);
            module.Validate();
            return module;
        }

        private static 플레이폐루프Synty표현Slot Slot(
            string slotStableId,
            string worldInteractionId,
            string momentCode,
            string placementRoleCode,
            string[] authorityStateCodes,
            params string[] assetFamilyIds) =>
            new 플레이폐루프Synty표현Slot(
                slotStableId,
                worldInteractionId,
                momentCode,
                placementRoleCode,
                authorityStateCodes,
                assetFamilyIds);
    }
}
