using System.Linq;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// 저장 Scene을 덮어쓰지 않고 canonical SimulationWorldShell의 Play 진입 때만
    /// Solo Nature 생존 세로 조각을 조립합니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public static class Nature생존AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLocalScope()
        {
            SimulationWorldLocalRuntimeScope.Reset();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => InstallForCurrentScene();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InstallForCurrentScene()
        {
            if (다중Os생명주기GameView검증Driver.CliCaptureRequested) return;
            if (SimulationWorldLocalRuntimeScope.RestaurantObserverRequested) return;
            if (SceneManager.GetActiveScene().name != "SimulationWorldShell"
                || Object.FindFirstObjectByType<Nature생존Controller>(
                    FindObjectsInactive.Include) != null)
                return;

            var singleView = Object.FindFirstObjectByType<단일PlayerViewController>(
                FindObjectsInactive.Include);
            var player = singleView?.Player
                ?? Object.FindFirstObjectByType<플레이어경관Controller>(
                    FindObjectsInactive.Include);
            if (singleView == null || player == null)
            {
                Debug.LogWarning("NatureSurvivalBootstrapDeferred:PlayerViewMissing");
                return;
            }

            var semanticRoot = Object.FindFirstObjectByType<네이처생활핵조우방어공간Root>(
                FindObjectsInactive.Include);
            var homeAnchor = semanticRoot?.Nodes.FirstOrDefault(value =>
                value.NodeStableId == "h2-nature-home-core")?.Anchor;
            var hierarchyEvidence = Object.FindFirstObjectByType<
                H계층배치증거Root>(FindObjectsInactive.Include);
            var natureAreaAnchor = hierarchyEvidence?.AreaSetRoots
                .FirstOrDefault(value => value.AreaRoleCode == "NatureHome")
                ?.transform;
            var resolvedNatureAnchor = homeAnchor ?? natureAreaAnchor;

            var root = new GameObject("Nature생존_SoloLocal_r5").transform;
            root.SetParent(singleView.transform, false);
            root.position = resolvedNatureAnchor != null
                ? resolvedNatureAnchor.position : player.transform.position;
            var controller = root.gameObject.AddComponent<Nature생존Controller>();
            var localScope = SimulationWorldLocalRuntimeScope.Install();
            controller.Configure(player, root, root, authorityEngine:
                new Nature생존CoreRuntimeAdapter(localScope.Runtime,
                    localScope.SessionStableId),
                saveWorldAction: () =>
                {
                    var saved = localScope.SaveCurrent();
                    return $"저장 완료 · Tick {saved.SavedWorldTick} · "
                        + saved.ReplayHash[..System.Math.Min(10,
                            saved.ReplayHash.Length)];
                },
                profileLabel: localScope.LaunchProfile ==
                    Nature생존LaunchProfile.E7Validation
                    ? "E7 검증 프로필" : "기본 Solo");
            if (localScope.RestoredFromSlot)
                controller.SetStartupStatus(localScope.RecoveredFromBackup
                    ? "백업 슬롯에서 세계를 복원했습니다."
                    : "저장된 세계를 복원했습니다.");
            else if (localScope.LaunchProfile ==
                     Nature생존LaunchProfile.E7Validation)
                controller.SetStartupStatus(
                    "E7 검증 프로필 · 황혼 90초 전부터 실제 입력을 검증합니다.");
            var combat = Object.FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            var battle = Object.FindFirstObjectByType<현장전투CompositionRoot>(
                FindObjectsInactive.Include);
            if (combat != null && battle != null)
                root.gameObject.AddComponent<Nature생존BattleBridge>()
                    .Configure(controller, combat, battle);

            var approved = singleView.ApprovedEnvironmentRoots
                .Where(value => value != null).Concat(new[] { root }).ToArray();
            singleView.SetApprovedEnvironmentRoots(approved);

            if (resolvedNatureAnchor != null)
            {
                var character = player.GetComponent<CharacterController>();
                if (character != null) character.enabled = false;
                player.transform.position = resolvedNatureAnchor.position
                    + resolvedNatureAnchor.forward * 2.5f + Vector3.up * .2f;
                if (character != null) character.enabled = true;
            }
            Debug.Log("NatureSurvivalLocalSimulationRuntimeReady:"
                + "nature-survival.realtime.r5:network=0");
        }

    }

    [DisallowMultipleComponent]
    public sealed class Nature생존BattleBridge : MonoBehaviour
    {
        private Nature생존Controller controller = null!;
        private 전투시점Controller combat = null!;
        private 현장전투CompositionRoot battle = null!;

        public void Configure(Nature생존Controller natureController,
            전투시점Controller combatController, 현장전투CompositionRoot battleRoot)
        {
            controller = natureController;
            combat = combatController;
            battle = battleRoot;
            controller.EncounterFightRequested += OnFightRequested;
            battle.WorldLocalBattleResultResolved += OnBattleResolved;
            battle.WorldLocalBattleRequestFailed += OnBattleRequestFailed;
        }

        private void OnFightRequested(string encounterStableId,
            long expectedWorldRevision)
        {
            if (!battle.AuthorityReady || !combat.TryRequestLocalEncounter(
                    encounterStableId, expectedWorldRevision))
                controller.AllowFightRetry(battle.AuthorityReady
                    ? "전투 시점 전환 대기" : "전투 권위 준비 대기");
        }

        private void OnBattleResolved(string encounterStableId, string resultCode)
            => controller.RefreshAfterBattle(encounterStableId, resultCode);

        private void OnBattleRequestFailed(string encounterStableId, string reasonCode)
            => controller.AllowFightRetry(reasonCode);

        private void OnDestroy()
        {
            if (controller != null)
                controller.EncounterFightRequested -= OnFightRequested;
            if (battle != null)
            {
                battle.WorldLocalBattleResultResolved -= OnBattleResolved;
                battle.WorldLocalBattleRequestFailed -= OnBattleRequestFailed;
            }
        }
    }
}
