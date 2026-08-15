using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 통합SimulationWorldPlayModeTests
    {
        [UnityTest]
        public IEnumerator 하나의Scene버튼으로_월드와Farm두시점과Hub정보판을전환한다()
        {
            void RemoveLiveServerCompositions(Scene loadedScene, LoadSceneMode mode)
            {
                foreach (var value in Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
            }

            SceneManager.sceneLoaded += RemoveLiveServerCompositions;
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= RemoveLiveServerCompositions;

            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var inbound = Object.FindAnyObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include);
            var mode = Object.FindAnyObjectByType<통합월드ModePresenter>(
                FindObjectsInactive.Include);
            var farmManagement = Object.FindAnyObjectByType<농장경영시점Controller>(
                FindObjectsInactive.Include);
            Assert.That(shell, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(inbound, Is.Not.Null);
            Assert.That(mode, Is.Not.Null);
            Assert.That(farmManagement, Is.Not.Null);
            var tick = shell.WorldTick;
            var revision = shell.WorldRevision;

            var bar = GameObject.Find(
                "SimulationWorldShell/PersistentUI/UnifiedWorldModeCanvas/UnifiedWorldModeBar");
            Assert.That(bar, Is.Not.Null);
            RequiredButton(bar, "FarmFirstPersonButton").onClick.Invoke();
            Assert.That(player.IsCameraTransitioning, Is.True);
            yield return WaitForCameraTransition(player);
            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.FirstPerson));
            Assert.That(player.CurrentActivityCode,
                Is.EqualTo("FarmManagement"));
            Assert.That(farmManagement.IsActive, Is.False);

            RequiredButton(bar, "FarmTacticalButton").onClick.Invoke();
            Assert.That(player.IsCameraTransitioning, Is.True);
            Assert.That(farmManagement.IsActive, Is.False,
                "전환 중에는 두 시점의 입력 규칙을 모두 잠근다.");
            yield return WaitForCameraTransition(player);
            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.ThirdPerson));
            Assert.That(player.CurrentActivityCode,
                Is.EqualTo("FarmManagement"));
            Assert.That(farmManagement.IsActive, Is.True);

            RequiredButton(bar, "JinbuInboundButton").onClick.Invoke();
            yield return null;
            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.Strategy));
            Assert.That(inbound.ContextVisible, Is.True);
            Assert.That(shell.CurrentFocusAnchorId,
                Is.EqualTo(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix
                           + "district:logistics"));

            RequiredButton(bar, "WorldOverviewButton").onClick.Invoke();
            yield return null;
            Assert.That(shell.IsWorldMapVisible, Is.True);
            Assert.That(inbound.ContextVisible, Is.False);
            Assert.That(shell.WorldTick, Is.EqualTo(tick));
            Assert.That(shell.WorldRevision, Is.EqualTo(revision));
        }

        private static Button RequiredButton(GameObject bar, string name)
        {
            var child = bar.transform.Find(name);
            Assert.That(child, Is.Not.Null, name + " 배선 누락");
            return child.GetComponent<Button>();
        }

        private static IEnumerator WaitForCameraTransition(
            플레이어경관Controller player)
        {
            var timeout = 3f;
            while (player.IsCameraTransitioning && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(player.IsCameraTransitioning, Is.False,
                "시점 전환이 제한 시간 안에 끝나야 한다.");
        }
    }
}
