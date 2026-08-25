using System;
using System.Linq;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;

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
    public sealed class Nature생존CoreRuntimeAdapter : INature생존Engine, IDisposable
    {
        private readonly LocalSimulationRuntime runtime;
        private readonly string sessionStableId;
        private readonly bool ownsRuntime;
        private readonly string playerStableId;
        private long revision;
        private int commandSequence;

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
            var session = Await(runtime.Sessions.GetAsync(sessionStableId));
            var state = Await(runtime.Nature.GetAsync(sessionStableId));
            revision = session.Revision;
            return Map(session, state);
        }

        public void AcquireAxe() => Confirm(SimulationNatureSurvivalCodes.AcquireAxe,
            SimulationNatureSurvivalCodes.AxePickupStableId);

        public void BeginHarvest(string resourceStableId)
            => Confirm(SimulationNatureSurvivalCodes.BeginHarvest, resourceStableId);

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

        private void Confirm(string actionCode, string targetStableId,
            string choiceCode = "", double localX = 0, double localZ = 0,
            double yawDegrees = 0)
        {
            Execute(() =>
            {
                var result = Await(runtime.Nature.ConfirmAsync(sessionStableId,
                    new SimulationNatureSurvivalCommandRequest
                    {
                        CommandId = NextCommand(actionCode),
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
            });
        }

        private string NextCommand(string kind)
            => $"unity-local:{kind}:revision-{revision}:{++commandSequence}";

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
                ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR2,
                PlayerStableId = "player:solo",
                StartsWithAxe = false,
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
        };

        private static Nature생존State Map(경영SimulationSessionSnapshot session,
            SimulationNatureSurvivalStateSnapshot state) => new()
        {
            ProfileRevision = state.ProfileRevision,
            AuthorityModeCode = Nature생존Codes.SoloLocal,
            SessionStableId = session.SessionStableId,
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
            ActiveWork = state.ActiveWork == null ? null : new Nature생존WorkState
            {
                KindCode = state.ActiveWork.WorkKindCode,
                TargetStableId = state.ActiveWork.TargetStableId,
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

        public void Dispose()
        {
            if (ownsRuntime) runtime.Dispose();
        }
    }
}
