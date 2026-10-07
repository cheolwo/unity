using System;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.BusinessWorkflow;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public enum Nature생존LaunchProfile
    {
        PlayableDefault = 0,
        E7Validation = 1,
        HansFenceE5 = 2,
        RestaurantObserver = 3,
        SyntheticDelivery = 4,
        WaitingFleet = 5,
        MartCenter = 6,
        OrderFlow = 7,
        NeighborhoodLife = 8,
        OfflineLife = 9,
        SagajeongFoodLife = 10,
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
            BusinessWorkflow = LocalBusinessWorkflowRuntimeFactory.Create(
                runtime, BusinessWorkflowRuleEngine.기본);
            SessionStableId = sessionStableId;
            LaunchProfile = launchProfile;
            SlotStableId = slotStableId;
            RestoredFromSlot = restoredFromSlot;
            RecoveredFromBackup = recoveredFromBackup;
        }

        public LocalSimulationRuntime Runtime { get; }
        /// <summary>
        /// 같은 LocalSimulationRuntime 원장을 다섯 업무 관점으로 노출하는 명시 조립이다.
        /// 별도 상태나 서버 fallback을 만들지 않는다.
        /// </summary>
        public IBusinessWorkflowRuntime BusinessWorkflow { get; }
        public string SessionStableId { get; }
        public Nature생존LaunchProfile LaunchProfile { get; }
        public string SlotStableId { get; }
        public bool RestoredFromSlot { get; }
        public bool RecoveredFromBackup { get; }
        public 로컬생활시작묶음? OfflineBundle { get; private set; }
        public static bool OfflineLifeRequested => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_BUNDLE"));
        public static bool IsInstalled => current != null;
        public static bool BusinessServerObserverConfigured => string.Equals(
            Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"),
            "6",
            StringComparison.Ordinal);
        public static bool BusinessServerObserverRequested
        {
            get
            {
                if (!BusinessServerObserverConfigured) return false;
                if (!CanUseBusinessServerObserver(
                        UnityEngine.Application.isEditor,
                        UnityEngine.Debug.isDebugBuild))
                    throw new InvalidOperationException(
                        "BusinessServerObserverDevelopmentBuildOnly");
                return true;
            }
        }
        public static bool CanUseBusinessServerObserver(bool isEditor, bool isDebugBuild)
            => isEditor || isDebugBuild;
        public static bool SagajeongFoodLifeRequested => current?.LaunchProfile == Nature생존LaunchProfile.SagajeongFoodLife
            || string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_SAGAJEONG_FOOD_LIFE"), "1", StringComparison.Ordinal);
        public static bool NeighborhoodLifeRequested => SagajeongFoodLifeRequested || OfflineLifeRequested || string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"), "5", StringComparison.Ordinal);
        public static bool OrderFlowRequested => NeighborhoodLifeRequested || string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"), "4", StringComparison.Ordinal);
        public static bool MartCenterRequested => OrderFlowRequested || string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"), "3", StringComparison.Ordinal);
        public static bool WaitingFleetRequested => MartCenterRequested || string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"), "2", StringComparison.Ordinal);
        public static bool SyntheticDeliveryRequested => BusinessServerObserverRequested || WaitingFleetRequested || string.Equals(
            Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY"), "1", StringComparison.Ordinal);
        public static bool RestaurantObserverRequested => SyntheticDeliveryRequested || string.Equals(
            Environment.GetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER"), "1", StringComparison.Ordinal);

        public static SimulationWorldLocalRuntimeScope Install()
            => Install(DetectLaunchProfile(), null);

        public static SimulationWorldLocalRuntimeScope Install(
            Nature생존LaunchProfile launchProfile, string? savesRootPath)
        {
            if (BusinessServerObserverRequested) throw new InvalidOperationException("BusinessServerObserverLocalRuntimeForbidden");
            lock (Gate)
            {
                if (current != null)
                {
                    if (current.LaunchProfile != launchProfile)
                        throw new InvalidOperationException(
                            "SimulationLocalRuntimeProfileAlreadyInstalled");
                    return current;
                }
                var offline = launchProfile == Nature생존LaunchProfile.OfflineLife
                    ? 로컬생활시작자료.Read(Environment.GetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_BUNDLE") ?? "",
                        Environment.GetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_SHA256") ?? "") : null;
                var runtime = string.IsNullOrWhiteSpace(savesRootPath)
                    ? LocalSimulationUnityRuntime.Create()
                    : LocalSimulationUnityRuntime.Create(savesRootPath!);
                try
                {
                    // 저장소의 64자 상한 안에서 프로필과 전체 묶음 해시를 함께 식별한다.
                    var slotStableId = offline == null ? SlotFor(launchProfile) : 로컬생활시작자료.Hash(
                        System.Text.Encoding.UTF8.GetBytes("offline-life-r1:" + offline.ContentSha256));
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
                        var request = offline == null ? CreateSessionRequest(launchProfile) : 로컬생활시작자료.Copy(offline.Payload.InitialRequest);
                        if(offline != null) request.ClientRequestId = Guid.NewGuid();
                        var created = runtime.Sessions.CreateAsync(request).AsTask()
                            .GetAwaiter().GetResult();
                        sessionStableId = created.SessionStableId;
                        if (launchProfile == Nature생존LaunchProfile.E7Validation)
                            AdvanceToValidationStart(runtime, sessionStableId,
                                created.Revision);
                    }
                    if(offline != null) runtime.GetLocalLifeReportAsync(sessionStableId,offline).AsTask().GetAwaiter().GetResult();
                    current = new SimulationWorldLocalRuntimeScope(runtime,
                        sessionStableId, launchProfile, slotStableId,
                        restoredFromSlot, recoveredFromBackup) {OfflineBundle=offline};
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
            => SagajeongFoodLifeRequested ? Nature생존LaunchProfile.SagajeongFoodLife
                : OfflineLifeRequested ? Nature생존LaunchProfile.OfflineLife
                : NeighborhoodLifeRequested ? Nature생존LaunchProfile.NeighborhoodLife
                : OrderFlowRequested ? Nature생존LaunchProfile.OrderFlow
                : MartCenterRequested ? Nature생존LaunchProfile.MartCenter
                : WaitingFleetRequested ? Nature생존LaunchProfile.WaitingFleet
                : SyntheticDeliveryRequested ? Nature생존LaunchProfile.SyntheticDelivery
                : RestaurantObserverRequested ? Nature생존LaunchProfile.RestaurantObserver
                : string.Equals(Environment.GetEnvironmentVariable(
                    "SSALDDEL_HANS_FENCE_E5"), "1", StringComparison.Ordinal)
                ? Nature생존LaunchProfile.HansFenceE5
                : string.Equals(Environment.GetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION"), "1",
                StringComparison.Ordinal)
                ? Nature생존LaunchProfile.E7Validation
                : Nature생존LaunchProfile.PlayableDefault;

        private static string SlotFor(Nature생존LaunchProfile launchProfile)
            => launchProfile == Nature생존LaunchProfile.SagajeongFoodLife
                ? Ssalddel.Unity.Observation.사가정음식생활관찰Profile.SlotId
                : launchProfile == Nature생존LaunchProfile.NeighborhoodLife
                ? Ssalddel.Unity.Warehouse.가상배달관찰표본.NeighborhoodLifeSlotId
                : launchProfile == Nature생존LaunchProfile.OrderFlow
                ? Ssalddel.Unity.Warehouse.가상배달관찰표본.OrderFlowSlotId
                : launchProfile == Nature생존LaunchProfile.MartCenter
                ? Ssalddel.Unity.Warehouse.가상배달관찰표본.MartSlotId
                : launchProfile == Nature생존LaunchProfile.WaitingFleet
                ? Ssalddel.Unity.Warehouse.가상배달관찰표본.WaitingSlotId
                : launchProfile == Nature생존LaunchProfile.SyntheticDelivery
                ? Ssalddel.Unity.Warehouse.가상배달관찰표본.SlotId
                : launchProfile == Nature생존LaunchProfile.RestaurantObserver
                ? Ssalddel.Unity.Warehouse.음식점관찰표본.SlotId
                : launchProfile == Nature생존LaunchProfile.HansFenceE5
                ? "hans-fence-e5-r6-primary"
                : launchProfile == Nature생존LaunchProfile.E7Validation
                ? "nature-e7-validation-r5-primary"
                : "solo-local-r5-primary";

        private static 경영SimulationSession생성Request CreateSessionRequest(
            Nature생존LaunchProfile launchProfile)
        {
            if (launchProfile == Nature생존LaunchProfile.SagajeongFoodLife)
                return Ssalddel.Unity.Observation.사가정음식생활관찰Profile.생성(Guid.NewGuid());
            if (launchProfile == Nature생존LaunchProfile.NeighborhoodLife)
                return Ssalddel.Unity.Warehouse.가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid(), dayEnabled: true);
            if (launchProfile == Nature생존LaunchProfile.OrderFlow)
                return Ssalddel.Unity.Warehouse.가상배달관찰표본.CreateOrderFlow(Guid.NewGuid());
            if (launchProfile == Nature생존LaunchProfile.MartCenter)
                return Ssalddel.Unity.Warehouse.가상배달관찰표본.CreateMart(Guid.NewGuid());
            if (launchProfile == Nature생존LaunchProfile.WaitingFleet)
                return Ssalddel.Unity.Warehouse.가상배달관찰표본.CreateWaiting(Guid.NewGuid());
            if (launchProfile == Nature생존LaunchProfile.SyntheticDelivery)
                return Ssalddel.Unity.Warehouse.가상배달관찰표본.Create(Guid.NewGuid());
            if (launchProfile == Nature생존LaunchProfile.RestaurantObserver)
                return Ssalddel.Unity.Warehouse.음식점관찰표본.Create(Guid.NewGuid());
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
                ? "simulation-data:nature-e7-validation:5"
                : "simulation-data:solo-local-world:5";
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
                ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR5,
                PlayerStableId = "player:session:owner",
                StartsWithAxe = false,
                BuildingProgressionCatalog =
                    Simulation영역건물발전Catalog.CreateDefault(),
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
            request.ActorEquipment = new SimulationActorEquipmentInitialStateRequest
            {
                ActorStableId = request.NatureSurvival.PlayerStableId,
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
            };
            request.Atmosphere = new SimulationAtmosphereInitialStateRequest
            {
                ProfileStableId =
                    WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1,
                RuleRevision = WorldAtmosphereRuleRevisions.R1,
                ClockSourceCode =
                    WorldAtmosphereClockSourceCodes.NatureCycleClock,
            };
            var combatActor = "actor:sim:player-survivor";
            request.TeamRoleCards = CreateNatureCombatCards(request.ClientRequestId,
                combatActor);
            var natureObservation = PyeongchangSimulation공간상호작용Fixture
                .CreateNatureTwilightActualE5Observation().Definitions;
            var droppedTimberPickup = PyeongchangSimulation공간상호작용Fixture
                .CreateNatureDroppedTimberActualE5().Definitions;
            var existingSpatial = request.SpatialWorld?.Definitions
                ?? Array.Empty<Simulation공간정의InitialRequest>();
            request.SpatialWorld = new Simulation공간세계InitialStateRequest
            {
                Definitions = existingSpatial.Where(value =>
                        value.SpatialStableId
                            != SimulationNatureSurvivalCodes.ActualE5SpatialStableId(
                                "WI-NATURE-01")
                        && value.SpatialStableId
                            != SimulationNatureSurvivalCodes.ActualE5SpatialStableId(
                                "WI-NATURE-18"))
                    .Concat(natureObservation)
                    .Concat(droppedTimberPickup).ToArray(),
            };
            if (launchProfile == Nature생존LaunchProfile.HansFenceE5)
            {
                request.ScenarioStableId = "scenario:unity.hans-fence-e5";
                request.ScenarioDataRevision = "simulation-data:hans-fence-e5:1";
                request.NatureSurvival.ProfileRevision = SimulationNatureSurvivalCodes.ProfileRevisionR6;
                request.NatureSurvival.HansFarmFenceRestorationEnabled = true;
                request.NatureSurvival.StartsWithAxe = true;
                // 기존 Solo 저장이나 E7 표본을 변경하지 않는 별도 시작 프로필이다.
                request.ActorEquipment = null;
            }
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
