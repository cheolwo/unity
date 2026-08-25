using System;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public enum Nature생존LaunchProfile
    {
        PlayableDefault = 0,
        E7Validation = 1,
    }

    /// <summary>
    /// canonical World의 Solo 영역들이 같은 Runtime과 Session을 사용하게 하는 실행 범위입니다.
    /// GameObject 상태가 아니라 공통 Session Aggregate가 권위를 가집니다.
    /// </summary>
    public sealed class SimulationWorldLocalRuntimeScope : IDisposable
    {
        private static readonly object Gate = new();
        private static SimulationWorldLocalRuntimeScope? current;

        private SimulationWorldLocalRuntimeScope(LocalSimulationRuntime runtime,
            string sessionStableId, Nature생존LaunchProfile launchProfile,
            string slotStableId, bool restoredFromSlot,
            bool recoveredFromBackup)
        {
            Runtime = runtime;
            SessionStableId = sessionStableId;
            LaunchProfile = launchProfile;
            SlotStableId = slotStableId;
            RestoredFromSlot = restoredFromSlot;
            RecoveredFromBackup = recoveredFromBackup;
        }

        public LocalSimulationRuntime Runtime { get; }
        public string SessionStableId { get; }
        public Nature생존LaunchProfile LaunchProfile { get; }
        public string SlotStableId { get; }
        public bool RestoredFromSlot { get; }
        public bool RecoveredFromBackup { get; }
        public static bool IsInstalled => current != null;

        public static SimulationWorldLocalRuntimeScope Install()
            => Install(DetectLaunchProfile(), null);

        public static SimulationWorldLocalRuntimeScope Install(
            Nature생존LaunchProfile launchProfile, string? savesRootPath)
        {
            lock (Gate)
            {
                if (current != null)
                {
                    if (current.LaunchProfile != launchProfile)
                        throw new InvalidOperationException(
                            "SimulationLocalRuntimeProfileAlreadyInstalled");
                    return current;
                }
                var runtime = string.IsNullOrWhiteSpace(savesRootPath)
                    ? LocalSimulationUnityRuntime.Create()
                    : LocalSimulationUnityRuntime.Create(savesRootPath!);
                try
                {
                    var slotStableId = SlotFor(launchProfile);
                    string sessionStableId;
                    var restoredFromSlot = false;
                    var recoveredFromBackup = false;
                    try
                    {
                        var loaded = runtime.Sessions.LoadSlotAsync(slotStableId)
                            .AsTask().GetAwaiter().GetResult();
                        sessionStableId = loaded.Restore.Session.SessionStableId;
                        restoredFromSlot = true;
                        recoveredFromBackup = loaded.RecoveredFromBackup;
                    }
                    catch (SimulationNotFoundException error) when (
                        error.Message == "SimulationLocalSaveSlotNotFound")
                    {
                        var request = CreateSessionRequest(launchProfile);
                        var created = runtime.Sessions.CreateAsync(request).AsTask()
                            .GetAwaiter().GetResult();
                        sessionStableId = created.SessionStableId;
                        if (launchProfile == Nature생존LaunchProfile.E7Validation)
                            AdvanceToValidationStart(runtime, sessionStableId,
                                created.Revision);
                    }
                    current = new SimulationWorldLocalRuntimeScope(runtime,
                        sessionStableId, launchProfile, slotStableId,
                        restoredFromSlot, recoveredFromBackup);
                    return current;
                }
                catch
                {
                    runtime.Dispose();
                    throw;
                }
            }
        }

        public SimulationLocalSaveSlotResult SaveCurrent()
        {
            var session = Runtime.Sessions.GetAsync(SessionStableId).AsTask()
                .GetAwaiter().GetResult();
            return Runtime.Sessions.SaveSlotAsync(SessionStableId,
                new SimulationLocalSaveSlotRequest
                {
                    SlotStableId = SlotStableId,
                    ExpectedRevision = session.Revision,
                }).AsTask().GetAwaiter().GetResult();
        }

        public static bool TryGet(out SimulationWorldLocalRuntimeScope scope)
        {
            lock (Gate)
            {
                scope = current!;
                return scope != null;
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                current?.Dispose();
                current = null;
            }
        }

        public static Nature생존LaunchProfile DetectLaunchProfile()
            => string.Equals(Environment.GetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION"), "1",
                StringComparison.Ordinal)
                ? Nature생존LaunchProfile.E7Validation
                : Nature생존LaunchProfile.PlayableDefault;

        private static string SlotFor(Nature생존LaunchProfile launchProfile)
            => launchProfile == Nature생존LaunchProfile.E7Validation
                ? "nature-e7-validation-r3-primary"
                : "solo-local-r3-primary";

        private static 경영SimulationSession생성Request CreateSessionRequest(
            Nature생존LaunchProfile launchProfile)
        {
            var request = JsonConvert.DeserializeObject<경영SimulationSession생성Request>(
                JsonConvert.SerializeObject(
                    턴마감ServerAuthorityRepository.CreateSessionRequest()))
                ?? throw new InvalidOperationException(
                    "SimulationLocalSessionFixtureInvalid");
            var nature = Nature생존LocalEngine.CreateDefaultFixture();
            request.ScenarioStableId = launchProfile ==
                Nature생존LaunchProfile.E7Validation
                ? "scenario:unity.nature-e7-validation"
                : "scenario:unity.solo-local-world";
            request.ScenarioDataRevision = launchProfile ==
                Nature생존LaunchProfile.E7Validation
                ? "simulation-data:nature-e7-validation:2"
                : "simulation-data:solo-local-world:2";
            if (launchProfile == Nature생존LaunchProfile.E7Validation)
            {
                request.ClientRequestId = Guid.Parse(
                    "70e70e70-e700-4700-8700-70e70e70e700");
                request.ScenarioSeed = FindValidationEncounterSeed(
                    request.ClientRequestId, 4);
            }
            // Farm 공간·수확 상태는 Solo 물류가 같은 Session의 포장 Lot 계보를
            // 사용하도록 보존한다. 통합 시설 투영만 아직 별도 이관 대상이다.
            request.IntegratedWorld = null;
            request.NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR2,
                PlayerStableId = "player:session:owner",
                StartsWithAxe = false,
                ResourceNodes = nature.ResourceNodes.Select(node =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = node.StableId,
                        H2StableId = node.H2StableId,
                        H1StableId = node.H1StableId,
                        LocalX = node.LocalX,
                        LocalZ = node.LocalZ,
                    }).ToArray(),
            };
            var combatActor = "actor:sim:player-survivor";
            request.TeamRoleCards = CreateNatureCombatCards(request.ClientRequestId,
                combatActor);
            return request;
        }

        private static SimulationTeamRoleCardInitialState CreateNatureCombatCards(
            Guid clientRequestId, string actorStableId)
        {
            SimulationTeamRoleCardInitialCard Card(string copyId, string definitionId,
                string title) => new()
                {
                    CardCopyStableId = copyId,
                    CardDefinitionStableId = definitionId,
                    Title = title,
                    ActivityRoleCodes = new[]
                    {
                        SimulationTeamRoleCardCodes.Exploration,
                    },
                };
            var sessionStableId = "simulation-session:"
                + clientRequestId.ToString("N");
            return new SimulationTeamRoleCardInitialState
            {
                SessionStableId = sessionStableId,
                TeamStableId = "team:nature:solo-player",
                TeamPolicyRevision = 1,
                MemberActorStableIds = new[] { actorStableId },
                Cards = new[]
                {
                    Card("card:nature:observer:focused-assault",
                        SimulationLocalCombatCodes.FocusedAssaultCardDefinition,
                        "집중 공세"),
                    Card("card:nature:observer:cautious-defense",
                        SimulationLocalCombatCodes.CautiousDefenseCardDefinition,
                        "신중 방어"),
                    Card("card:nature:observer:weakness-observation",
                        SimulationLocalCombatCodes.WeaknessObservationCardDefinition,
                        "약점 관찰"),
                    Card("card:nature:observer:cabin-cover",
                        SimulationLocalCombatCodes.CabinCoverCardDefinition,
                        "거점 엄폐"),
                    Card("card:nature:observer:field-recovery",
                        SimulationLocalCombatCodes.FieldRecoveryCardDefinition,
                        "재정비"),
                    Card("card:nature:observer:safe-retreat",
                        SimulationLocalCombatCodes.SafeRetreatCardDefinition,
                        "안전 후퇴"),
                },
                CombatLoadouts = new[]
                {
                    new SimulationCombatCardLoadoutInitialState
                    {
                        ActorStableId = actorStableId,
                        CombatControlModeCode =
                            SimulationTeamRoleCardCodes.ObserverOperation,
                        Slots = new[]
                        {
                            new SimulationCombatCardLoadoutSlotSnapshot
                            {
                                SlotCode = SimulationTeamRoleCardCodes.ObserverTactic,
                                CardCopyStableId =
                                    "card:nature:observer:focused-assault",
                            },
                            new SimulationCombatCardLoadoutSlotSnapshot
                            {
                                SlotCode = SimulationTeamRoleCardCodes.ObserverSupport,
                                CardCopyStableId = "card:nature:observer:cabin-cover",
                            },
                            new SimulationCombatCardLoadoutSlotSnapshot
                            {
                                SlotCode = SimulationTeamRoleCardCodes.ObserverEmergency,
                                CardCopyStableId =
                                    "card:nature:observer:field-recovery",
                            },
                        },
                    },
                },
            };
        }

        private static void AdvanceToValidationStart(LocalSimulationRuntime runtime,
            string sessionStableId, long revision)
        {
            var remaining = Math.Max(0,
                NatureSurvivalRules.DaylightEndsAtSecond - 90);
            var sequence = 0;
            while (remaining > 0)
            {
                var elapsed = Math.Min(60, remaining);
                var advanced = runtime.Nature.AdvanceRealtimeAsync(sessionStableId,
                    new SimulationNatureSurvivalClockAdvanceRequest
                    {
                        CommandId = "unity-e7-validation-clock:" + ++sequence,
                        ExpectedRevision = revision,
                        ElapsedRealtimeSeconds = elapsed,
                        WorkInputHeld = false,
                    }).AsTask().GetAwaiter().GetResult();
                revision = advanced.Revision;
                remaining -= elapsed;
            }
        }

        private static int FindValidationEncounterSeed(Guid clientRequestId,
            int expectedNoiseEventCount)
        {
            var sessionStableId = "simulation-session:"
                + clientRequestId.ToString("N");
            for (var seed = 1; seed < 10_000; seed++)
            {
                if (NatureSurvivalRules.RollFirstDuskEncounter(seed,
                        sessionStableId, 0, expectedNoiseEventCount))
                    return seed;
            }
            throw new InvalidOperationException(
                "NatureE7ValidationEncounterSeedUnavailable");
        }

        public void Dispose() => Runtime.Dispose();
    }
}
