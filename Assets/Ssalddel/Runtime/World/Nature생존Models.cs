using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.ImmersiveWorld;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Nature생존Codes
    {
        public const string ProfileRevision = "nature-survival.realtime.r5";
        public const string SoloLocal = "SoloLocal";
        public const string HostedServer = "HostedServer";
        public const string Standing = "Standing";
        public const string Stump = "Stump";
        public const string DroppedTimberAvailable = "Available";
        public const string DroppedTimberCollected = "Collected";
        public const string Planned = "Planned";
        public const string Building = "Building";
        public const string Completed = "Completed";
        public const string Harvest = "Harvest";
        public const string CabinBuild = "CabinBuild";
        public const string AreaBuildingConstruction = "AreaBuildingConstruction";
        public const string Pending = "Pending";
        public const string CombatActive = "CombatActive";
        public const string Resolved = "Resolved";
        public const string Fight = "Fight";
        public const string Retreat = "Retreat";
        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string Night = NatureSurvivalClockPhaseCodes.Night;
        public const string Dawn = NatureSurvivalClockPhaseCodes.Dawn;
        public const string Workbench = "Workbench";
        public const string WorkbenchBlueprint = "blueprint:nature-workbench.v1";
        public const string WorkbenchFacility = "facility:nature-workbench";
        public const string WorkbenchH1 = "h1:nature:workbench";
        public const string Available = "Available";
        public const string Operational = "Operational";
        public const int WorkbenchTimberCost = 4;
        public const int WorkbenchRebuildPartCost = 1;
        public const int WorkbenchConstructionSeconds = 20;
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
    public sealed class Nature지면통나무State
    {
        public string StableId = string.Empty;
        public string SourceResourceNodeStableId = string.Empty;
        public string H2StableId = "h2-candidate:nature-encounter-route";
        public string H1StableId = "h1-stock:nature-exploration-buffer";
        public float LocalX;
        public float LocalZ;
        public int Quantity;
        public string StateCode = Nature생존Codes.DroppedTimberAvailable;
        public long CreatedWorldRevision;
        public long CollectedWorldRevision;

        public Nature지면통나무State Clone()
            => (Nature지면통나무State)MemberwiseClone();
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
        // Core 조회에서만 제공하는 작업 계보. 구형 Unity 저장 형식에는 추가하지 않는다.
        [NonSerialized] public string ActorStableId = string.Empty;
        [NonSerialized] public string OriginCommandId = string.Empty;
        public int RequiredSeconds;
        public int CompletedSeconds;

        public Nature생존WorkState Clone() => (Nature생존WorkState)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature작업대State
    {
        public string BlueprintStableId = Nature생존Codes.WorkbenchBlueprint;
        public string FacilityStableId = Nature생존Codes.WorkbenchFacility;
        public string H1StableId = Nature생존Codes.WorkbenchH1;
        public string StateCode = Nature생존Codes.Available;
        public float LocalX;
        public float LocalZ;
        public float YawDegrees;
        public int ReservedTimber;
        public int ReservedRebuildParts;
        public int CompletedWorkSeconds;

        public Nature작업대State Clone() => (Nature작업대State)MemberwiseClone();
    }

    public sealed class Nature작업대배치Preview
    {
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public int RequiredTimber = Nature생존Codes.WorkbenchTimberCost;
        public int RequiredRebuildParts = Nature생존Codes.WorkbenchRebuildPartCost;
        public int RequiredWorkSeconds = Nature생존Codes.WorkbenchConstructionSeconds;
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
    public sealed class WorldAtmosphereState
    {
        public bool IsEnabled = true;
        public string ProfileStableId =
            WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1;
        public string RuleRevision = WorldAtmosphereRuleRevisions.R1;
        public string ClockSourceCode =
            WorldAtmosphereClockSourceCodes.NatureCycleClock;
        public string ScopeCode = WorldAtmosphereScopeCodes.World;
        public string WeatherCode = WorldWeatherCodes.Clear;
        public string NextWeatherCode = WorldWeatherCodes.Clear;
        public int TransitionProgressPermille;
        public int CloudCoverPermille;
        public int PrecipitationPermille;
        public int WindIntensityPermille;
        public int LightningSequenceIndex;
        public int CycleIndex;
        public int ElapsedSecondsInCycle;

        public WorldAtmosphereState Clone()
            => (WorldAtmosphereState)MemberwiseClone();
    }

    /// <summary>Core가 이미 확정한 벌목 완료 기록의 표현용 사본. 새 결과나 저장 상태가 아니다.</summary>
    public sealed class Nature벌목CompletionEvidence
    {
        public string SessionStableId = string.Empty;
        public string ActorStableId = string.Empty;
        public string OriginCommandId = string.Empty;
        public string TargetStableId = string.Empty;
        public string EvidenceStableId = string.Empty;
        public string RecordHashSha256 = string.Empty;
        public long ResultRevision;

        public Nature벌목CompletionEvidence Clone()
            => (Nature벌목CompletionEvidence)MemberwiseClone();
    }

    [Serializable]
    public sealed class Nature생존State
    {
        public string ProfileRevision = Nature생존Codes.ProfileRevision;
        public string AuthorityModeCode = Nature생존Codes.SoloLocal;
        public string SessionStableId = "local:nature-survival";
        // 빈 값은 미제공이다. Scene 이름이나 임의 기본 Actor로 대체하지 않는다.
        [NonSerialized] public string PlayerStableId = string.Empty;
        [NonSerialized] public Nature벌목CompletionEvidence[] HarvestCompletionRecords =
            Array.Empty<Nature벌목CompletionEvidence>();
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
        public Nature지면통나무State[] DroppedTimber =
            Array.Empty<Nature지면통나무State>();
        public Nature생존WorkState? ActiveWork;
        public Nature생존CabinState Cabin = new();
        public Nature작업대State Workbench = new();
        public Nature생존EncounterState? Encounter;
        public WorldAtmosphereState Atmosphere = new();

        public Nature생존State Clone()
            => new()
            {
                ProfileRevision = ProfileRevision,
                AuthorityModeCode = AuthorityModeCode,
                SessionStableId = SessionStableId,
                PlayerStableId = PlayerStableId,
                HarvestCompletionRecords = HarvestCompletionRecords.Select(value => value.Clone()).ToArray(),
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
                DroppedTimber = DroppedTimber.Select(value => value.Clone()).ToArray(),
                ActiveWork = ActiveWork?.Clone(),
                Cabin = Cabin.Clone(),
                Workbench = Workbench.Clone(),
                Encounter = Encounter?.Clone(),
                Atmosphere = Atmosphere?.Clone() ?? new WorldAtmosphereState(),
            };
    }

    // 이 사건을 제공하지 않는 과거 LocalEngine은 기존 동작을 유지한다.
    public interface IHans농장울타리Engine
    {
        SimulationNatureSurvivalStateSnapshot? LastAuthoritySnapshot { get; }
        void AcquireHansBrokenAxe();
        void RepairHansFarmFence();
    }

    public interface INature생존Engine
    {
        Nature생존State Snapshot();
        void AcquireAxe();
        void BeginHarvest(string resourceStableId);
        void CollectDroppedTimber(string droppedTimberStableId);
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
        Nature작업대배치Preview PreviewWorkbench(float localX, float localZ,
            float yawDegrees);
        void BeginWorkbenchConstruction(float localX, float localZ,
            float yawDegrees);
    }

    /// <summary>
    /// 보편 Actor 물품 획득·장착 권위를 Unity 표현에 노출하는 좁은 경계입니다.
    /// 상태창은 이 경계를 호출한 뒤 반환된 상태 사본만 표시합니다.
    /// </summary>
    public interface IActor장착Engine
    {
        SimulationActorEquipmentStateSnapshot ActorEquipmentSnapshot();
        SimulationActorEquipmentStateSnapshot Equip(
            string itemInstanceStableId, string slotCode);
        SimulationActorEquipmentStateSnapshot Unequip(
            string itemInstanceStableId);
    }

    /// <summary>
    /// 권위 실행이 남긴 비권위 엔진 추적을 Unity 표현 궤적에 인계한다.
    /// 로컬 전용 구현이 없어도 기존 INature생존Engine은 그대로 동작한다.
    /// </summary>
    public interface INature생존EngineTraceSource
    {
        SimulationPlayableLoopEngineTraceEntry[] LastEngineInteractionTrace
        {
            get;
        }
    }

    /// <summary>
    /// WI-NATURE-01의 관찰 Preview/Confirm 경계입니다. 생존 조우 권위와
    /// 분리해 기존 직접 실행 엔진은 그대로 호환합니다.
    /// </summary>
    public interface INature위협관찰Engine
    {
        bool ThreatObservationConfirmed { get; }
        Nature위협관찰PreviewApiModel PreviewThreatObservation();
        void ConfirmThreatObservation();
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

        public Nature생존State Snapshot()
        {
            RefreshAtmosphere();
            return state.Clone();
        }

        private void RefreshAtmosphere()
        {
            state.Atmosphere ??= new WorldAtmosphereState();
            var projection = WorldAtmosphereRules.Evaluate(
                state.Atmosphere.ProfileStableId,
                state.ScenarioSeed,
                state.CycleIndex,
                state.ElapsedSecondsInCycle);
            state.Atmosphere.IsEnabled = true;
            state.Atmosphere.WeatherCode = projection.WeatherCode;
            state.Atmosphere.NextWeatherCode = projection.NextWeatherCode;
            state.Atmosphere.TransitionProgressPermille =
                projection.TransitionProgressPermille;
            state.Atmosphere.CloudCoverPermille = projection.CloudCoverPermille;
            state.Atmosphere.PrecipitationPermille =
                projection.PrecipitationPermille;
            state.Atmosphere.WindIntensityPermille =
                projection.WindIntensityPermille;
            state.Atmosphere.LightningSequenceIndex =
                projection.LightningSequenceIndex;
            state.Atmosphere.CycleIndex = state.CycleIndex;
            state.Atmosphere.ElapsedSecondsInCycle =
                state.ElapsedSecondsInCycle;
        }

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

        public void CollectDroppedTimber(string droppedTimberStableId)
        {
            var dropped = state.DroppedTimber.SingleOrDefault(value =>
                value.StableId == droppedTimberStableId)
                ?? throw new InvalidOperationException(
                    "SimulationNatureDroppedTimberNotFound");
            if (dropped.StateCode != Nature생존Codes.DroppedTimberAvailable)
                throw new InvalidOperationException(
                    "SimulationNatureDroppedTimberUnavailable");
            dropped.StateCode = Nature생존Codes.DroppedTimberCollected;
            state.TimberQuantity += dropped.Quantity;
            state.Revision++;
            dropped.CollectedWorldRevision = state.Revision;
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
            else if (state.ActiveWork.KindCode ==
                     Nature생존Codes.AreaBuildingConstruction)
            {
                state.TimberQuantity += state.Workbench.ReservedTimber;
                state.RebuildPartQuantity += state.Workbench.ReservedRebuildParts;
                state.Workbench.ReservedTimber = 0;
                state.Workbench.ReservedRebuildParts = 0;
                state.Workbench.CompletedWorkSeconds = 0;
                state.Workbench.StateCode = Nature생존Codes.Available;
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

        public Nature작업대배치Preview PreviewWorkbench(float localX,
            float localZ, float yawDegrees)
        {
            var reasons = new List<string>();
            if (!state.Day2Ready || state.SelectedExpansionPlanCode !=
                Nature생존Codes.Workbench)
                reasons.Add("SimulationAreaBuildingDay2Required");
            if (!state.PlayerInsideCabin)
                reasons.Add("SimulationAreaBuildingCabinAccessRequired");
            if (state.ActiveWork != null)
                reasons.Add("SimulationAreaBuildingConstructionActive");
            if (state.Workbench.StateCode == Nature생존Codes.Operational)
                reasons.Add("SimulationAreaBuildingAlreadyOperational");
            if (state.TimberQuantity + state.StoredTimberQuantity <
                Nature생존Codes.WorkbenchTimberCost)
                reasons.Add("SimulationAreaBuildingTimberInsufficient");
            if (state.RebuildPartQuantity <
                Nature생존Codes.WorkbenchRebuildPartCost)
                reasons.Add("SimulationAreaBuildingRebuildPartInsufficient");
            if (Math.Abs(localX) > 12f || Math.Abs(localZ) > 12f)
                reasons.Add("SimulationAreaBuildingPlacementOutsideHome");
            var cabinDistance = Math.Sqrt(Math.Pow(localX - state.Cabin.LocalX, 2)
                + Math.Pow(localZ - state.Cabin.LocalZ, 2));
            if (cabinDistance < 3.5d)
                reasons.Add("SimulationAreaBuildingPlacementOverlap");
            return new Nature작업대배치Preview
            {
                CanConfirm = reasons.Count == 0,
                BlockingReasonCodes = reasons.ToArray(),
            };
        }

        public void BeginWorkbenchConstruction(float localX, float localZ,
            float yawDegrees)
        {
            var preview = PreviewWorkbench(localX, localZ, yawDegrees);
            if (!preview.CanConfirm)
                throw new InvalidOperationException(string.Join(",",
                    preview.BlockingReasonCodes));
            var carried = Math.Min(state.TimberQuantity,
                Nature생존Codes.WorkbenchTimberCost);
            state.TimberQuantity -= carried;
            state.StoredTimberQuantity -=
                Nature생존Codes.WorkbenchTimberCost - carried;
            state.RebuildPartQuantity -= Nature생존Codes.WorkbenchRebuildPartCost;
            state.Workbench.StateCode = Nature생존Codes.Building;
            state.Workbench.LocalX = localX;
            state.Workbench.LocalZ = localZ;
            state.Workbench.YawDegrees = NormalizeYaw(yawDegrees);
            state.Workbench.ReservedTimber = Nature생존Codes.WorkbenchTimberCost;
            state.Workbench.ReservedRebuildParts =
                Nature생존Codes.WorkbenchRebuildPartCost;
            state.ActiveWork = new Nature생존WorkState
            {
                KindCode = Nature생존Codes.AreaBuildingConstruction,
                TargetStableId = Nature생존Codes.WorkbenchBlueprint,
                RequiredSeconds = Nature생존Codes.WorkbenchConstructionSeconds,
            };
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
                var droppedStableId = "drop:nature:timber:"
                    + node.StableId + ":cycle:" + state.CycleIndex;
                if (state.DroppedTimber.All(value =>
                        value.StableId != droppedStableId))
                {
                    state.DroppedTimber = state.DroppedTimber.Concat(new[]
                    {
                        new Nature지면통나무State
                        {
                            StableId = droppedStableId,
                            SourceResourceNodeStableId = node.StableId,
                            H2StableId = node.H2StableId,
                            H1StableId = node.H1StableId,
                            LocalX = node.LocalX,
                            LocalZ = node.LocalZ,
                            Quantity = NatureSurvivalRules
                                .HarvestTimberQuantity,
                            CreatedWorldRevision = state.Revision + 1,
                        },
                    }).ToArray();
                }
                state.NoiseEventCount++;
            }
            else if (state.ActiveWork.KindCode == Nature생존Codes.CabinBuild)
            {
                state.Cabin.CompletedWorkSeconds = state.ActiveWork.CompletedSeconds;
                state.Cabin.StateCode = Nature생존Codes.Completed;
                state.Cabin.ReservedTimber = 0;
                state.Cabin.RecoveryAvailable = true;
                state.Cabin.DefenseAvailable = true;
                state.NoiseEventCount++;
            }
            else if (state.ActiveWork.KindCode ==
                     Nature생존Codes.AreaBuildingConstruction)
            {
                state.Workbench.CompletedWorkSeconds =
                    state.ActiveWork.CompletedSeconds;
                state.Workbench.StateCode = Nature생존Codes.Operational;
                state.Workbench.ReservedTimber = 0;
                state.Workbench.ReservedRebuildParts = 0;
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
                || value.DroppedTimber == null
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
