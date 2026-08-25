using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Nature생존Codes
    {
        public const string ProfileRevision = NatureSurvivalProfileCodes.RealtimeR2;
        public const string SoloLocal = "SoloLocal";
        public const string HostedServer = "HostedServer";
        public const string Standing = "Standing";
        public const string Stump = "Stump";
        public const string Planned = "Planned";
        public const string Building = "Building";
        public const string Completed = "Completed";
        public const string Harvest = "Harvest";
        public const string CabinBuild = "CabinBuild";
        public const string Pending = "Pending";
        public const string Resolved = "Resolved";
        public const string Fight = "Fight";
        public const string Retreat = "Retreat";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string Workbench = "Workbench";
        public const string StorageRack = "StorageRack";
        public const string Palisade = "Palisade";
        public const string SkeletonPlaceholder = "placeholder:synty-generic-skeleton";
    }

    [Serializable]
    public sealed class Nature생존ResourceNodeState
    {
        public string StableId = string.Empty;
        public string H2StableId = "h2-candidate:nature-encounter-route";
        public string H1StableId = string.Empty;
        public float LocalX;
        public float LocalZ;
        public string StateCode = Nature생존Codes.Standing;
        public int RegrowsAtCycleIndex = -1;

        public Nature생존ResourceNodeState Clone() => (Nature생존ResourceNodeState)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature생존CabinState
    {
        public string StableId = "facility:nature-cabin";
        public string H2StableId = "h2-candidate:nature-home-core";
        public string H1StableId = "h1-stock:nature-shelter";
        public string StateCode = Nature생존Codes.Planned;
        public float LocalX;
        public float LocalZ;
        public float YawDegrees;
        public int ReservedTimber;
        public int CompletedWorkSeconds;
        public bool RecoveryAvailable;
        public bool DefenseAvailable;

        public Nature생존CabinState Clone() => (Nature생존CabinState)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature생존WorkState
    {
        public string KindCode = string.Empty;
        public string TargetStableId = string.Empty;
        public int RequiredSeconds;
        public int CompletedSeconds;

        public Nature생존WorkState Clone() => (Nature생존WorkState)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature생존EncounterState
    {
        public string StableId = string.Empty;
        public string StateCode = Nature생존Codes.Pending;
        public string PresentationCode = Nature생존Codes.SkeletonPlaceholder;
        public int TriggeredCycleIndex;
        public string ResolutionCode = string.Empty;
        public bool CabinDefenseApplied;
        public int RawThreatTier;
        public int EffectiveThreatTier;
        public int HostileCount;
        public string LinkedCombatStableId = string.Empty;

        public Nature생존EncounterState Clone() => (Nature생존EncounterState)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature생존State
    {
        public string ProfileRevision = Nature생존Codes.ProfileRevision;
        public string AuthorityModeCode = Nature생존Codes.SoloLocal;
        public string SessionStableId = "local:nature-survival";
        public int ScenarioSeed = 1701;
        public long Revision;
        public int WorldTick;
        public int CycleIndex;
        public int ElapsedSecondsInCycle;
        public string ClockPhaseCode = NatureSurvivalClockPhaseCodes.Daylight;
        public bool ClockPaused;
        public bool HasAxe;
        public int TimberQuantity;
        public int StoredTimberQuantity;
        public int NoiseEventCount;
        public int RawThreatTier;
        public int EffectiveThreatTier;
        public int RebuildPartQuantity;
        public string LinkedCombatStableId = string.Empty;
        public string LastCombatResultCode = string.Empty;
        public bool Sleeping;
        public string SelectedExpansionPlanCode = string.Empty;
        public bool Day2Ready;
        public bool PlayerInsideCabin;
        public Nature생존ResourceNodeState[] ResourceNodes =
            Array.Empty<Nature생존ResourceNodeState>();
        public Nature생존WorkState? ActiveWork;
        public Nature생존CabinState Cabin = new();
        public Nature생존EncounterState? Encounter;

        public Nature생존State Clone()
            => new()
            {
                ProfileRevision = ProfileRevision,
                AuthorityModeCode = AuthorityModeCode,
                SessionStableId = SessionStableId,
                ScenarioSeed = ScenarioSeed,
                Revision = Revision,
                WorldTick = WorldTick,
                CycleIndex = CycleIndex,
                ElapsedSecondsInCycle = ElapsedSecondsInCycle,
                ClockPhaseCode = ClockPhaseCode,
                ClockPaused = ClockPaused,
                HasAxe = HasAxe,
                TimberQuantity = TimberQuantity,
                StoredTimberQuantity = StoredTimberQuantity,
                NoiseEventCount = NoiseEventCount,
                RawThreatTier = RawThreatTier,
                EffectiveThreatTier = EffectiveThreatTier,
                RebuildPartQuantity = RebuildPartQuantity,
                LinkedCombatStableId = LinkedCombatStableId,
                LastCombatResultCode = LastCombatResultCode,
                Sleeping = Sleeping,
                SelectedExpansionPlanCode = SelectedExpansionPlanCode,
                Day2Ready = Day2Ready,
                PlayerInsideCabin = PlayerInsideCabin,
                ResourceNodes = ResourceNodes.Select(value => value.Clone()).ToArray(),
                ActiveWork = ActiveWork?.Clone(),
                Cabin = Cabin.Clone(),
                Encounter = Encounter?.Clone(),
            };
    }

    public interface INature생존Engine
    {
        Nature생존State Snapshot();
        void AcquireAxe();
        void BeginHarvest(string resourceStableId);
        void PlaceCabinBlueprint(float localX, float localZ, float yawDegrees);
        void BeginCabinBuild();
        void EnterCabin();
        void LeaveCabin();
        void CancelActiveWork();
        void AdvanceRealtime(int elapsedSeconds, bool workInputHeld,
            bool menuOrApplicationPaused);
        void ResolveEncounter(string choiceCode);
        void StoreAtCabin();
        void SleepInCabin();
        void SelectExpansionPlan(string planCode);
    }

    /// <summary>
    /// Solo에서 외부 서버 없이 동작하는 결정적 Nature 생존 권위입니다.
    /// 종료 시각을 저장하지 않아 offline catch-up을 수행하지 않습니다.
    /// </summary>
    public sealed class Nature생존LocalEngine : INature생존Engine
    {
        private readonly HashSet<int> evaluatedDuskCycles = new();
        private readonly Nature생존State state;

        public Nature생존LocalEngine(Nature생존State initialState)
        {
            state = initialState?.Clone()
                ?? throw new ArgumentNullException(nameof(initialState));
            Validate(state);
        }

        public Nature생존State Snapshot() => state.Clone();

        public void AcquireAxe()
        {
            if (state.HasAxe) throw new InvalidOperationException("NatureAxeAlreadyAcquired");
            state.HasAxe = true;
            state.Revision++;
        }

        public void BeginHarvest(string resourceStableId)
        {
            if (!state.HasAxe) throw new InvalidOperationException("NatureAxeRequired");
            EnsureNoActiveWork();
            var node = state.ResourceNodes.SingleOrDefault(value =>
                value.StableId == resourceStableId)
                ?? throw new InvalidOperationException("NatureResourceUnknown");
            if (node.StateCode != Nature생존Codes.Standing)
                throw new InvalidOperationException("NatureResourceUnavailable");
            state.ActiveWork = new Nature생존WorkState
            {
                KindCode = Nature생존Codes.Harvest,
                TargetStableId = node.StableId,
                RequiredSeconds = NatureSurvivalRules.HarvestWorkSeconds,
            };
            state.Revision++;
        }

        public void PlaceCabinBlueprint(float localX, float localZ, float yawDegrees)
        {
            if (state.Cabin.StateCode != Nature생존Codes.Planned
                || Math.Abs(localX) > 12f || Math.Abs(localZ) > 12f)
                throw new InvalidOperationException("NatureCabinPlacementBlocked");
            state.Cabin.StateCode = Nature생존Codes.Building;
            state.Cabin.LocalX = localX;
            state.Cabin.LocalZ = localZ;
            state.Cabin.YawDegrees = NormalizeYaw(yawDegrees);
            state.Revision++;
        }

        public void BeginCabinBuild()
        {
            EnsureNoActiveWork();
            if (state.Cabin.StateCode != Nature생존Codes.Building
                || state.Cabin.ReservedTimber > 0)
                throw new InvalidOperationException("NatureCabinBlueprintRequired");
            if (state.TimberQuantity < NatureSurvivalRules.CabinTimberCost)
                throw new InvalidOperationException("NatureTimberInsufficient");
            state.TimberQuantity -= NatureSurvivalRules.CabinTimberCost;
            state.Cabin.ReservedTimber = NatureSurvivalRules.CabinTimberCost;
            state.ActiveWork = new Nature생존WorkState
            {
                KindCode = Nature생존Codes.CabinBuild,
                TargetStableId = state.Cabin.StableId,
                RequiredSeconds = NatureSurvivalRules.CabinWorkSeconds,
                CompletedSeconds = state.Cabin.CompletedWorkSeconds,
            };
            state.Revision++;
        }

        public void EnterCabin()
        {
            if (state.Cabin.StateCode != Nature생존Codes.Completed
                || state.PlayerInsideCabin)
                throw new InvalidOperationException("NatureCabinEntryBlocked");
            state.PlayerInsideCabin = true;
            state.Revision++;
        }

        public void LeaveCabin()
        {
            if (state.Cabin.StateCode != Nature생존Codes.Completed
                || !state.PlayerInsideCabin)
                throw new InvalidOperationException("NatureCabinExitBlocked");
            state.PlayerInsideCabin = false;
            state.Revision++;
        }

        public void CancelActiveWork()
        {
            if (state.ActiveWork == null)
                throw new InvalidOperationException("NatureActiveWorkRequired");
            if (state.ActiveWork.KindCode == Nature생존Codes.CabinBuild)
            {
                state.TimberQuantity += state.Cabin.ReservedTimber;
                state.Cabin.ReservedTimber = 0;
                state.Cabin.CompletedWorkSeconds = 0;
            }
            state.ActiveWork = null;
            state.Revision++;
        }

        public void AdvanceRealtime(int elapsedSeconds, bool workInputHeld,
            bool menuOrApplicationPaused)
        {
            if (elapsedSeconds < 0 || elapsedSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            var isSolo = state.AuthorityModeCode == Nature생존Codes.SoloLocal;
            state.ClockPaused = isSolo && menuOrApplicationPaused;
            if (state.ClockPaused)
            {
                state.Revision++;
                return;
            }

            var previousSecond = state.ElapsedSecondsInCycle;
            var previousCycle = state.CycleIndex;
            if (workInputHeld) AdvanceWork(elapsedSeconds);
            var appliedSeconds = state.Sleeping ? elapsedSeconds * 6 : elapsedSeconds;
            var projected = NatureSurvivalRules.AdvanceClock(state.CycleIndex,
                state.ElapsedSecondsInCycle, appliedSeconds);
            state.CycleIndex = projected.CycleIndex;
            state.ElapsedSecondsInCycle = projected.ElapsedSecondsInCycle;
            state.ClockPhaseCode = projected.PhaseCode;
            if (state.Sleeping && state.ClockPhaseCode ==
                NatureSurvivalClockPhaseCodes.Dawn)
                state.Sleeping = false;
            state.WorldTick += projected.CompletedCycleCount;
            RegrowTrees();
            TryTriggerEncounter(previousCycle, previousSecond, appliedSeconds);
            state.Revision++;
        }

        public void StoreAtCabin()
        {
            if (state.Cabin.StateCode != Nature생존Codes.Completed
                || !state.PlayerInsideCabin)
                throw new InvalidOperationException("NatureCabinAccessRequired");
            var quantity = Math.Min(state.TimberQuantity,
                Math.Max(0, 20 - state.StoredTimberQuantity));
            if (quantity <= 0)
                throw new InvalidOperationException("NatureTimberNotCarriedOrStorageFull");
            state.TimberQuantity -= quantity;
            state.StoredTimberQuantity += quantity;
            state.Revision++;
        }

        public void SleepInCabin()
        {
            if (state.Cabin.StateCode != Nature생존Codes.Completed
                || !state.PlayerInsideCabin
                || state.ClockPhaseCode != NatureSurvivalClockPhaseCodes.Night)
                throw new InvalidOperationException("NatureCabinSleepBlocked");
            state.Sleeping = true;
            state.Revision++;
        }

        public void SelectExpansionPlan(string planCode)
        {
            if (state.ClockPhaseCode != NatureSurvivalClockPhaseCodes.Dawn
                || state.Day2Ready
                || (planCode != Nature생존Codes.Workbench
                    && planCode != Nature생존Codes.StorageRack
                    && planCode != Nature생존Codes.Palisade))
                throw new InvalidOperationException("NatureExpansionPlanBlocked");
            state.SelectedExpansionPlanCode = planCode;
            state.Day2Ready = true;
            state.Revision++;
        }

        public void ResolveEncounter(string choiceCode)
        {
            if (state.Encounter == null || state.Encounter.StateCode != Nature생존Codes.Pending)
                throw new InvalidOperationException("NatureEncounterNotPending");
            if (choiceCode != Nature생존Codes.Fight
                && choiceCode != Nature생존Codes.Retreat)
                throw new InvalidOperationException("NatureEncounterChoiceInvalid");
            state.Encounter.StateCode = Nature생존Codes.Resolved;
            state.Encounter.ResolutionCode = choiceCode;
            if (choiceCode == Nature생존Codes.Retreat
                && state.Cabin.StateCode == Nature생존Codes.Completed)
                state.PlayerInsideCabin = true;
            state.Revision++;
        }

        private void AdvanceWork(int seconds)
        {
            if (state.ActiveWork == null) return;
            state.ActiveWork.CompletedSeconds = Math.Min(
                state.ActiveWork.RequiredSeconds,
                state.ActiveWork.CompletedSeconds + seconds);
            if (state.ActiveWork.CompletedSeconds < state.ActiveWork.RequiredSeconds) return;
            if (state.ActiveWork.KindCode == Nature생존Codes.Harvest)
            {
                var node = state.ResourceNodes.Single(value =>
                    value.StableId == state.ActiveWork.TargetStableId);
                node.StateCode = Nature생존Codes.Stump;
                node.RegrowsAtCycleIndex = state.CycleIndex
                    + NatureSurvivalRules.TreeRegrowthCycleCount;
                state.TimberQuantity += NatureSurvivalRules.HarvestTimberQuantity;
                state.NoiseEventCount++;
            }
            else
            {
                state.Cabin.CompletedWorkSeconds = state.ActiveWork.CompletedSeconds;
                state.Cabin.StateCode = Nature생존Codes.Completed;
                state.Cabin.ReservedTimber = 0;
                state.Cabin.RecoveryAvailable = true;
                state.Cabin.DefenseAvailable = true;
                state.NoiseEventCount++;
            }
            state.ActiveWork = null;
        }

        private void RegrowTrees()
        {
            foreach (var node in state.ResourceNodes)
            {
                if (node.StateCode == Nature생존Codes.Stump
                    && node.RegrowsAtCycleIndex >= 0
                    && state.CycleIndex >= node.RegrowsAtCycleIndex)
                {
                    node.StateCode = Nature생존Codes.Standing;
                    node.RegrowsAtCycleIndex = -1;
                }
            }
        }

        private void TryTriggerEncounter(int previousCycle, int previousSecond,
            int elapsedSeconds)
        {
            if ((state.Encounter != null && state.Encounter.StateCode !=
                    Nature생존Codes.Resolved) || state.NoiseEventCount <= 0
                || !NatureSurvivalRules.CrossedIntoDusk(previousSecond, elapsedSeconds)) return;
            var cycle = previousSecond < NatureSurvivalRules.DaylightEndsAtSecond
                ? previousCycle : previousCycle + 1;
            if (!evaluatedDuskCycles.Add(cycle)
                || !NatureSurvivalRules.ShouldTriggerDuskEncounter(state.ScenarioSeed,
                    state.SessionStableId, cycle, state.NoiseEventCount)) return;
            state.Encounter = new Nature생존EncounterState
            {
                StableId = "nature-encounter:" + cycle + ":skeleton-placeholder",
                TriggeredCycleIndex = cycle,
                CabinDefenseApplied = state.Cabin.StateCode == Nature생존Codes.Completed,
            };
        }

        private void EnsureNoActiveWork()
        {
            if (state.ActiveWork != null)
                throw new InvalidOperationException("NatureWorkAlreadyActive");
        }

        private static float NormalizeYaw(float value)
        {
            var result = value % 360f;
            return result < 0f ? result + 360f : result;
        }

        private static void Validate(Nature생존State value)
        {
            if (value.ProfileRevision != Nature생존Codes.ProfileRevision
                || string.IsNullOrWhiteSpace(value.SessionStableId)
                || value.ResourceNodes == null || value.ResourceNodes.Length == 0
                || value.ResourceNodes.Any(node => string.IsNullOrWhiteSpace(node.StableId))
                || value.ResourceNodes.Select(node => node.StableId).Distinct().Count()
                    != value.ResourceNodes.Length)
                throw new InvalidOperationException("NatureSurvivalInitialStateInvalid");
        }

        public static Nature생존State CreateDefaultFixture(int scenarioSeed = 1701)
            => new()
            {
                ScenarioSeed = scenarioSeed,
                HasAxe = false,
                ResourceNodes = Enumerable.Range(1, 6).Select(index =>
                    new Nature생존ResourceNodeState
                    {
                        StableId = $"resource:nature-tree:{index:00}",
                        H1StableId = "h1-stock:nature-exploration-buffer",
                        LocalX = -9f + index * 3f,
                        LocalZ = 8f + index % 2 * 2.5f,
                    }).ToArray(),
            };
    }
}
