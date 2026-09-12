using System;
using System.Linq;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.ImmersiveWorld;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// Unity 표현 계약을 공통 Session Aggregate 명령으로 번역합니다.
    /// 상태 계산은 하지 않고 Core Snapshot만 Unity 모델로 옮깁니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class Nature생존CoreRuntimeAdapter : INature생존Engine,
        INature위협관찰Engine, INature생존EngineTraceSource,
        IActor장착Engine, IHans농장울타리Engine, IDisposable
    {
        private readonly LocalSimulationRuntime runtime;
        private readonly string sessionStableId;
        private readonly bool ownsRuntime;
        private readonly string playerStableId;
        private long revision;
        private int commandSequence;
        public SimulationNatureSurvivalStateSnapshot? LastAuthoritySnapshot
            { get; private set; }

        public SimulationPlayableLoopEngineTraceEntry[] LastEngineInteractionTrace
            { get; private set; } =
                Array.Empty<SimulationPlayableLoopEngineTraceEntry>();

        public Nature생존CoreRuntimeAdapter(LocalSimulationRuntime localRuntime)
            : this(localRuntime, null, null, true)
        {
        }

        public Nature생존CoreRuntimeAdapter(LocalSimulationRuntime localRuntime,
            string existingSessionStableId)
            : this(localRuntime, existingSessionStableId, null, false)
        {
        }

        private Nature생존CoreRuntimeAdapter(LocalSimulationRuntime localRuntime,
            string? existingSessionStableId, string? loadSlotStableId,
            bool disposeRuntime)
        {
            runtime = localRuntime ?? throw new ArgumentNullException(nameof(localRuntime));
            ownsRuntime = disposeRuntime;
            if (!string.IsNullOrWhiteSpace(existingSessionStableId))
            {
                var existing = Await(runtime.Sessions.GetAsync(
                    existingSessionStableId!));
                sessionStableId = existing.SessionStableId;
                revision = existing.Revision;
                playerStableId = Await(runtime.Nature.GetAsync(sessionStableId))
                    .PlayerStableId;
            }
            else if (string.IsNullOrWhiteSpace(loadSlotStableId))
            {
                var fixture = Nature생존LocalEngine.CreateDefaultFixture();
                var created = Await(runtime.Sessions.CreateAsync(CreateRequest(fixture)));
                sessionStableId = created.SessionStableId;
                revision = created.Revision;
                playerStableId = Await(runtime.Nature.GetAsync(sessionStableId))
                    .PlayerStableId;
            }
            else
            {
                var loaded = Await(runtime.Sessions.LoadSlotAsync(loadSlotStableId!));
                sessionStableId = loaded.Restore.Session.SessionStableId;
                revision = loaded.Restore.Session.Revision;
                playerStableId = Await(runtime.Nature.GetAsync(sessionStableId))
                    .PlayerStableId;
            }
        }

        public static Nature생존CoreRuntimeAdapter Load(
            LocalSimulationRuntime localRuntime, string slotStableId)
            => new(localRuntime, null, slotStableId, true);

        public SimulationLocalSaveSlotResult SaveSlot(string slotStableId)
            => Await(runtime.Sessions.SaveSlotAsync(sessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = slotStableId,
                    ExpectedRevision = revision,
                }));

        public Nature생존State Snapshot()
        {
            var observation = Await(runtime.GetNature표현관측Async(sessionStableId));
            var session = observation.Session;
            var state = observation.Nature;
            LastAuthoritySnapshot = state;
            revision = session.Revision;
            var mapped = Map(session, state, observation.BuildingProgression);
            const string suffix = ":completed";
            mapped.HarvestCompletionRecords = (observation.ActionLedger?.TailRecords
                    ?? Array.Empty<Simulation행위발현Record>())
                .Where(record => record.SessionStableId == session.SessionStableId
                    && record.ActorStableId == state.PlayerStableId
                    && record.WorldInteractionId == SimulationNatureSurvivalCodes.BeginHarvestWorldInteractionId
                    && record.PrimaryOutcomeCode == "HarvestCompleted"
                    && record.결과분류Code == Simulation행위결과분류Codes.성공
                    && record.CommandId != null && record.CommandId.Length > suffix.Length
                    && record.CommandId.EndsWith(suffix, StringComparison.Ordinal)
                    && record.OutcomeStableId == "outcome:" + record.CommandId
                    && record.TargetStableIds != null && record.TargetStableIds.Length == 1
                    && !string.IsNullOrWhiteSpace(record.TargetStableIds[0])
                    && !string.IsNullOrWhiteSpace(record.행위기록StableId)
                    && !string.IsNullOrWhiteSpace(record.기록HashSha256)
                    && record.AfterWorldRevision > record.BeforeWorldRevision
                    && record.AfterWorldRevision <= session.Revision)
                .Select(record => new Nature벌목CompletionEvidence
                {
                    SessionStableId = record.SessionStableId,
                    ActorStableId = record.ActorStableId,
                    OriginCommandId = record.CommandId.Substring(0, record.CommandId.Length - suffix.Length),
                    TargetStableId = record.TargetStableIds[0],
                    EvidenceStableId = record.행위기록StableId,
                    RecordHashSha256 = record.기록HashSha256,
                    ResultRevision = record.AfterWorldRevision,
                }).ToArray();
            return mapped;
        }

        public bool ThreatObservationConfirmed
        {
            get
            {
                var taskStableId = ThreatObservationTaskStableId();
                var session = Await(runtime.Sessions.GetAsync(sessionStableId));
                revision = session.Revision;
                return session.Tasks.Any(value => string.Equals(
                    value.TaskStableId, taskStableId, StringComparison.Ordinal));
            }
        }

        public Nature위협관찰PreviewApiModel PreviewThreatObservation()
        {
            var preview = Await(runtime.PreviewNatureThreatObservationAsync(
                sessionStableId, BuildThreatObservationPreview()));
            return new Nature위협관찰PreviewApiModel
            {
                NatureRouteCode = preview.NatureRouteCode,
                EffectivePressure = preview.EffectivePressure,
                PressureLevelCode = preview.PressureLevelCode,
                NextWorldInteractionIds = preview.NextWorldInteractionIds.ToArray(),
                CanConfirm = preview.CanConfirm,
                BlockingReasonCodes = preview.BlockingReasonCodes.ToArray(),
                SimulationOnly = preview.SimulationOnly,
                IsOperationalState = preview.IsOperationalState,
            };
        }

        public void ConfirmThreatObservation()
        {
            if (ThreatObservationConfirmed) return;
            Execute(() =>
            {
                var preview = BuildThreatObservationPreview();
                var result = Await(runtime.ConfirmNatureThreatObservationAsync(
                    sessionStableId,
                    new SimulationNatureThreatObservationConfirmRequest
                    {
                        CommandId = NextCommand("threat-observation"),
                        ExpectedRevision = revision,
                        Preview = preview,
                    }));
                revision = result.Revision;
            });
        }

        public void AcquireHansBrokenAxe()
            => Confirm(SimulationNatureSurvivalCodes.AcquireHansBrokenAxe,
                SimulationNatureSurvivalCodes.HansBrokenAxePickupStableId);

        public void RepairHansFarmFence()
            => Confirm(SimulationNatureSurvivalCodes.RepairHansFarmFence,
                SimulationNatureSurvivalCodes.HansFarmFenceAggregateStableId);

        public void AcquireAxe() => Confirm(SimulationNatureSurvivalCodes.AcquireAxe,
            SimulationNatureSurvivalCodes.AxePickupStableId);

        public SimulationActorEquipmentStateSnapshot ActorEquipmentSnapshot()
            => Await(runtime.ActorEquipment.GetActorEquipmentAsync(
                sessionStableId));

        public SimulationActorEquipmentStateSnapshot Equip(
            string itemInstanceStableId, string slotCode)
            => ChangeEquipment(SimulationActorEquipmentCodes.Equip,
                itemInstanceStableId, slotCode);

        public SimulationActorEquipmentStateSnapshot Unequip(
            string itemInstanceStableId)
            => ChangeEquipment(SimulationActorEquipmentCodes.Unequip,
                itemInstanceStableId, string.Empty);

        public void BeginHarvest(string resourceStableId)
            => Confirm(SimulationNatureSurvivalCodes.BeginHarvest, resourceStableId);

        public void CollectDroppedTimber(string droppedTimberStableId)
            => Confirm(SimulationNatureSurvivalCodes.CollectDroppedTimber,
                droppedTimberStableId);

        public void PlaceCabinBlueprint(float localX, float localZ, float yawDegrees)
            => Confirm(SimulationNatureSurvivalCodes.PlaceCabinBlueprint,
                "facility:nature-cabin", string.Empty, localX, localZ, yawDegrees);

        public void BeginCabinBuild()
            => Confirm(SimulationNatureSurvivalCodes.BeginCabinBuild,
                "facility:nature-cabin");

        public void EnterCabin()
            => Confirm(SimulationNatureSurvivalCodes.EnterCabin,
                "facility:nature-cabin");

        public void LeaveCabin()
            => Confirm(SimulationNatureSurvivalCodes.LeaveCabin,
                "facility:nature-cabin");

        public void CancelActiveWork()
            => Confirm(SimulationNatureSurvivalCodes.CancelActiveWork,
                Snapshot().ActiveWork?.TargetStableId ?? string.Empty);

        public void AdvanceRealtime(int elapsedSeconds, bool workInputHeld,
            bool menuOrApplicationPaused)
        {
            Execute(() =>
            {
                var result = Await(runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = NextCommand("clock"),
                        ExpectedRevision = revision,
                        ElapsedRealtimeSeconds = elapsedSeconds,
                        WorkInputHeld = workInputHeld,
                        PauseReasonCode = menuOrApplicationPaused
                            ? SimulationNatureSurvivalCodes.ApplicationInactive
                            : string.Empty,
                    }));
                revision = result.Revision;
            });
        }

        public void ResolveEncounter(string choiceCode)
        {
            var encounter = Snapshot().Encounter
                ?? throw new InvalidOperationException(
                    SimulationNatureSurvivalCodes.EncounterNotPending);
            Confirm(SimulationNatureSurvivalCodes.ResolveEncounter,
                encounter.StableId, choiceCode);
        }

        public void StoreAtCabin()
            => Confirm(SimulationNatureSurvivalCodes.StoreAtCabin,
                SimulationNatureSurvivalCodes.CabinStorageContainerStableId);

        public void SleepInCabin()
            => Confirm(SimulationNatureSurvivalCodes.SleepInCabin,
                "facility:nature-cabin");

        public void SelectExpansionPlan(string planCode)
            => Confirm(SimulationNatureSurvivalCodes.SelectExpansionPlan,
                "plan:nature:day2", planCode);

        public Nature작업대배치Preview PreviewWorkbench(float localX,
            float localZ, float yawDegrees)
        {
            var preview = Await(runtime.Nature.PreviewAsync(sessionStableId,
                new SimulationNatureSurvivalActionPreviewRequest
                {
                    ObservedWorldRevision = revision,
                    PlayerStableId = playerStableId,
                    ActionCode = Simulation영역건물발전Codes
                        .BeginBuildingConstruction,
                    TargetStableId = Simulation영역건물발전Codes
                        .NatureWorkbenchBlueprint,
                    LocalX = localX,
                    LocalZ = localZ,
                    YawDegrees = yawDegrees,
                }));
            return new Nature작업대배치Preview
            {
                CanConfirm = preview.CanConfirm,
                BlockingReasonCodes = preview.BlockReasonCodes.ToArray(),
                RequiredTimber = preview.RequiredTimberQuantity,
                RequiredRebuildParts = preview.RequiredRebuildPartQuantity,
                RequiredWorkSeconds = preview.RequiredWorkSeconds,
            };
        }

        public void BeginWorkbenchConstruction(float localX, float localZ,
            float yawDegrees)
            => Confirm(Simulation영역건물발전Codes.BeginBuildingConstruction,
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint,
                string.Empty, localX, localZ, yawDegrees);

        private void Confirm(string actionCode, string targetStableId,
            string choiceCode = "", double localX = 0, double localZ = 0,
            double yawDegrees = 0)
        {
            Execute(() =>
            {
                var commandId = NextCommand(actionCode);
                var result = Await(runtime.Nature.ConfirmAsync(sessionStableId,
                    new SimulationNatureSurvivalCommandRequest
                    {
                        CommandId = commandId,
                        ExpectedRevision = revision,
                        PlayerStableId = playerStableId,
                        ActionCode = actionCode,
                        TargetStableId = targetStableId,
                        ChoiceCode = choiceCode,
                        LocalX = localX,
                        LocalZ = localZ,
                        YawDegrees = yawDegrees,
                    }));
                revision = result.Revision;
                var worldInteractionId = SimulationNatureSurvivalCodes
                    .WorldInteractionIdForAction(actionCode);
                LastEngineInteractionTrace = runtime.GetPlayableLoopEngineTrace(
                    SimulationNatureSurvivalCodes
                        .PlayableLoopStableIdForAction(actionCode),
                    worldInteractionId, commandId);
            });
        }

        private string NextCommand(string kind)
            => $"unity-local:{kind}:revision-{revision}:{++commandSequence}";

        private SimulationActorEquipmentStateSnapshot ChangeEquipment(
            string operationCode, string itemInstanceStableId,
            string slotCode)
        {
            var state = ActorEquipmentSnapshot();
            var preview = Await(runtime.ActorEquipment
                .PreviewActorEquipmentChangeAsync(sessionStableId,
                    new SimulationActorEquipmentChangePreviewRequest
                    {
                        ObservedEquipmentRevision = state.EquipmentRevision,
                        ActorStableId = playerStableId,
                        OperationCode = operationCode,
                        ItemInstanceStableId = itemInstanceStableId,
                        SlotCode = slotCode,
                    }));
            if (!preview.CanConfirm)
                throw new InvalidOperationException(string.Join(", ",
                    preview.BlockReasonCodes));
            return Await(runtime.ActorEquipment
                .ConfirmActorEquipmentChangeAsync(sessionStableId,
                    new SimulationActorEquipmentChangeConfirmRequest
                    {
                        CommandId = NextCommand("actor-equipment-" + operationCode),
                        ExpectedEquipmentRevision = state.EquipmentRevision,
                        ActorStableId = playerStableId,
                        OperationCode = operationCode,
                        ItemInstanceStableId = itemInstanceStableId,
                        SlotCode = slotCode,
                    }));
        }

        private SimulationNatureThreatObservationPreviewRequest
            BuildThreatObservationPreview()
        {
            var state = Snapshot();
            var encounter = state.Encounter
                ?? throw new InvalidOperationException(
                    SimulationNatureSurvivalCodes.EncounterNotPending);
            if (encounter.StateCode != SimulationNatureSurvivalCodes.Pending)
                throw new InvalidOperationException(
                    SimulationNatureSurvivalCodes.EncounterNotPending);
            return new SimulationNatureThreatObservationPreviewRequest
            {
                ExpectedRevision = revision,
                DecisionStableId = "decision:nature-threat-observation:"
                    + encounter.StableId,
                TaskStableId = ThreatObservationTaskStableId(),
                ActorStableId = playerStableId,
                NatureRouteCode = SimulationNatureInteractionCodes
                    .NatureHomeTwilightRoute,
                PreferredSpatialStableId = SimulationNatureSurvivalCodes
                    .ActualE5SpatialStableId("WI-NATURE-01"),
            };
        }

        private string ThreatObservationTaskStableId()
        {
            var encounter = Snapshot().Encounter;
            return encounter == null ? string.Empty
                : "task:nature-threat-observation:" + encounter.StableId;
        }

        private static void Execute(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { throw; }
            catch (Exception error)
            { throw new InvalidOperationException(error.Message, error); }
        }

        private static T Await<T>(System.Threading.Tasks.ValueTask<T> value)
            => value.AsTask().GetAwaiter().GetResult();

        private static 경영SimulationSession생성Request CreateRequest(
            Nature생존State fixture) => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:nature-solo-local",
            ScenarioDataRevision = Nature생존Codes.ProfileRevision,
            ScenarioSeed = fixture.ScenarioSeed,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 365,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:solo",
                TerritoryStableId = "territory:nature",
                SettlementStableId = "settlement:nature-home",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 23, 0, 0, 0,
                    TimeSpan.Zero),
            },
            NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR5,
                PlayerStableId = "player:solo",
                StartsWithAxe = false,
                BuildingProgressionCatalog =
                    Simulation영역건물발전Catalog.CreateDefault(),
                ResourceNodes = fixture.ResourceNodes.Select(node =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = node.StableId,
                        H2StableId = node.H2StableId,
                        H1StableId = node.H1StableId,
                        LocalX = node.LocalX,
                        LocalZ = node.LocalZ,
                    }).ToArray(),
            },
            ActorEquipment = new SimulationActorEquipmentInitialStateRequest
            {
                ActorStableId = "player:solo",
                ItemInstances = new[]
                {
                    new SimulationOwnedItemInstanceInitialState
                    {
                        ItemInstanceStableId =
                            SimulationNatureSurvivalCodes.AxePickupStableId,
                        ItemDefinitionStableId =
                            SimulationActorEquipmentCodes.AxeDefinitionStableId,
                        LocationCode = SimulationActorEquipmentCodes.WorldPickup,
                        SourceSpatialStableId =
                            SimulationNatureSurvivalCodes.AxePickupStableId,
                    },
                },
            },
            Atmosphere = new SimulationAtmosphereInitialStateRequest
            {
                ProfileStableId =
                    WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1,
                RuleRevision = WorldAtmosphereRuleRevisions.R1,
                ClockSourceCode =
                    WorldAtmosphereClockSourceCodes.NatureCycleClock,
            },
            SpatialWorld = new Simulation공간세계InitialStateRequest
            {
                Definitions = PyeongchangSimulation공간상호작용Fixture
                    .CreateNatureTwilightActualE5Observation().Definitions
                    .Concat(PyeongchangSimulation공간상호작용Fixture
                        .CreateNatureDroppedTimberActualE5().Definitions)
                    .ToArray(),
            },
        };

        private static Nature생존State Map(경영SimulationSessionSnapshot session,
            SimulationNatureSurvivalStateSnapshot state,
            Simulation영역건물발전Snapshot buildings) => new()
        {
            ProfileRevision = state.ProfileRevision,
            AuthorityModeCode = Nature생존Codes.SoloLocal,
            SessionStableId = session.SessionStableId,
            PlayerStableId = state.PlayerStableId,
            ScenarioSeed = session.ScenarioSeed,
            Revision = session.Revision,
            WorldTick = session.CurrentTick,
            CycleIndex = state.CycleIndex,
            ElapsedSecondsInCycle = state.ElapsedSecondsInCycle,
            ClockPhaseCode = state.ClockPhaseCode,
            ClockPaused = state.ClockPaused,
            HasAxe = state.HasAxe,
            TimberQuantity = state.TimberQuantity,
            StoredTimberQuantity = state.StoredTimberQuantity,
            NoiseEventCount = state.NoiseEventCount,
            RawThreatTier = state.RawThreatTier,
            EffectiveThreatTier = state.EffectiveThreatTier,
            RebuildPartQuantity = state.RebuildPartQuantity,
            LinkedCombatStableId = state.LinkedCombatStableId,
            LastCombatResultCode = state.LastCombatResultCode,
            Sleeping = state.Sleeping,
            SelectedExpansionPlanCode = state.SelectedExpansionPlanCode,
            Day2Ready = state.Day2Ready,
            PlayerInsideCabin = state.PlayerInsideCabin,
            Atmosphere = MapAtmosphere(session.Atmosphere),
            ResourceNodes = state.ResourceNodes.Select(node =>
                new Nature생존ResourceNodeState
                {
                    StableId = node.ResourceNodeStableId,
                    H2StableId = node.H2StableId,
                    H1StableId = node.H1StableId,
                    LocalX = (float)node.LocalX,
                    LocalZ = (float)node.LocalZ,
                    StateCode = node.StateCode,
                    RegrowsAtCycleIndex = node.RegrowsAtCycleIndex,
                }).ToArray(),
            DroppedTimber = state.DroppedTimber.Select(value =>
                new Nature지면통나무State
                {
                    StableId = value.DroppedTimberStableId,
                    SourceResourceNodeStableId =
                        value.SourceResourceNodeStableId,
                    H2StableId = value.H2StableId,
                    H1StableId = value.H1StableId,
                    LocalX = (float)value.LocalX,
                    LocalZ = (float)value.LocalZ,
                    Quantity = value.Quantity,
                    StateCode = value.StateCode,
                    CreatedWorldRevision = value.CreatedWorldRevision,
                    CollectedWorldRevision = value.CollectedWorldRevision,
                }).ToArray(),
            ActiveWork = state.ActiveWork == null ? null : new Nature생존WorkState
            {
                KindCode = state.ActiveWork.WorkKindCode,
                TargetStableId = state.ActiveWork.TargetStableId,
                ActorStableId = state.ActiveWork.ActorStableId,
                OriginCommandId = state.ActiveWork.OriginCommandId,
                RequiredSeconds = state.ActiveWork.RequiredWorkSeconds,
                CompletedSeconds = state.ActiveWork.CompletedWorkSeconds,
            },
            Cabin = new Nature생존CabinState
            {
                StableId = state.Cabin.CabinStableId,
                H2StableId = state.Cabin.H2StableId,
                H1StableId = state.Cabin.H1StableId,
                StateCode = state.Cabin.StateCode,
                LocalX = (float)state.Cabin.LocalX,
                LocalZ = (float)state.Cabin.LocalZ,
                YawDegrees = (float)state.Cabin.YawDegrees,
                ReservedTimber = state.Cabin.ReservedTimberQuantity,
                CompletedWorkSeconds = state.Cabin.CompletedWorkSeconds,
                RecoveryAvailable = state.Cabin.RecoveryAvailable,
                DefenseAvailable = state.Cabin.DefenseAvailable,
            },
            Workbench = MapWorkbench(buildings),
            Encounter = state.Encounter == null ? null : new Nature생존EncounterState
            {
                StableId = state.Encounter.EncounterStableId,
                StateCode = state.Encounter.StateCode,
                PresentationCode = state.Encounter.ThreatPresentationCode,
                TriggeredCycleIndex = state.Encounter.TriggeredCycleIndex,
                ResolutionCode = state.Encounter.ResolutionCode,
                CabinDefenseApplied = state.Encounter.CabinDefenseApplied,
                RawThreatTier = state.Encounter.RawThreatTier,
                EffectiveThreatTier = state.Encounter.EffectiveThreatTier,
                HostileCount = state.Encounter.HostileCount,
                LinkedCombatStableId = state.Encounter.LinkedCombatStableId,
            },
        };

        private static WorldAtmosphereState MapAtmosphere(
            SimulationAtmosphereStateSnapshot source)
            => new()
            {
                IsEnabled = source.IsEnabled,
                ProfileStableId = source.ProfileStableId,
                RuleRevision = source.RuleRevision,
                ClockSourceCode = source.ClockSourceCode,
                ScopeCode = source.ScopeCode,
                WeatherCode = source.WeatherCode,
                NextWeatherCode = source.NextWeatherCode,
                TransitionProgressPermille = source.TransitionProgressPermille,
                CloudCoverPermille = source.CloudCoverPermille,
                PrecipitationPermille = source.PrecipitationPermille,
                WindIntensityPermille = source.WindIntensityPermille,
                LightningSequenceIndex = source.LightningSequenceIndex,
                CycleIndex = source.CycleIndex,
                ElapsedSecondsInCycle = source.ElapsedSecondsInCycle,
            };

        private static Nature작업대State MapWorkbench(
            Simulation영역건물발전Snapshot buildings)
        {
            var node = buildings.Nodes.Single(value => value.BlueprintStableId ==
                Simulation영역건물발전Codes.NatureWorkbenchBlueprint);
            return new Nature작업대State
            {
                BlueprintStableId = node.BlueprintStableId,
                FacilityStableId = node.FacilityStableId,
                H1StableId = node.H1StableId,
                StateCode = node.StateCode,
                LocalX = (float)node.LocalX,
                LocalZ = (float)node.LocalZ,
                YawDegrees = (float)node.YawDegrees,
                CompletedWorkSeconds = node.CompletedWorkSeconds,
            };
        }

        public void Dispose()
        {
            if (ownsRuntime) runtime.Dispose();
        }
    }

}
