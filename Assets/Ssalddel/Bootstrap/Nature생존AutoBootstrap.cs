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
    public static class Nature생존AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLocalScope()
            => SimulationWorldLocalRuntimeScope.Reset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
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

            var root = new GameObject("Nature생존_SoloLocal_r1").transform;
            root.SetParent(singleView.transform, false);
            root.position = homeAnchor != null ? homeAnchor.position : player.transform.position;
            var controller = root.gameObject.AddComponent<Nature생존Controller>();
            var localScope = SimulationWorldLocalRuntimeScope.Install();
            controller.Configure(player, root, root, authorityEngine:
                new Nature생존CoreRuntimeAdapter(localScope.Runtime,
                    localScope.SessionStableId));
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

            if (homeAnchor != null)
            {
                var character = player.GetComponent<CharacterController>();
                if (character != null) character.enabled = false;
                player.transform.position = homeAnchor.position + homeAnchor.forward * 2.5f
                    + Vector3.up * .2f;
                if (character != null) character.enabled = true;
            }
            Debug.Log("NatureSurvivalLocalSimulationRuntimeReady:"
                + "nature-survival.realtime.r1:network=0");
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
            battle.WorldLocalBattleResolved += OnBattleResolved;
            battle.WorldLocalBattleRequestFailed += OnBattleRequestFailed;
        }

        private void OnFightRequested(string encounterStableId)
        {
            if (!battle.AuthorityReady || !combat.TryRequestLocalEncounter(encounterStableId))
                controller.AllowFightRetry(battle.AuthorityReady
                    ? "전투 시점 전환 대기" : "전투 권위 준비 대기");
        }

        private void OnBattleResolved(string encounterStableId)
            => controller.ResolveFightVictory(encounterStableId);

        private void OnBattleRequestFailed(string encounterStableId, string reasonCode)
            => controller.AllowFightRetry(reasonCode);

        private void OnDestroy()
        {
            if (controller != null)
                controller.EncounterFightRequested -= OnFightRequested;
            if (battle != null)
            {
                battle.WorldLocalBattleResolved -= OnBattleResolved;
                battle.WorldLocalBattleRequestFailed -= OnBattleRequestFailed;
            }
        }
    }
}
