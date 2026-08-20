using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 통합SimulationWorldPlayModeTests
    {
        [UnityTest]
        public IEnumerator 화물Npc는_확정된네경로지점을따라_Hub까지자동이동한다()
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
            var presenter = Object.FindAnyObjectByType<물류이동Presenter>(
                FindObjectsInactive.Include);
            var routeView = Object.FindAnyObjectByType<법정동화물운송View>(
                FindObjectsInactive.Include);
            var streaming = Object.FindAnyObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(routeView, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);

            var timeout = 5f;
            while (!streaming!.IsInitialized && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(streaming.IsInitialized, Is.True);
            yield return Await(presenter!.PreviewAsync());
            yield return Await(presenter.ConfirmAsync());

            timeout = 12f;
            while (presenter.CurrentPhaseCode != 물류이동PhaseCodes.Arrived && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(presenter.CurrentPhaseCode, Is.EqualTo(물류이동PhaseCodes.Arrived));
            Assert.That(presenter.CurrentAuthoritySnapshot!.CompletedRouteTicks, Is.EqualTo(3));
            Assert.That(routeView!.RoutePlan, Is.Not.Null);
            Assert.That(routeView.RoutePlan!.Waypoints, Has.Length.EqualTo(4));
            Assert.That(routeView.RoutePlan.EvidenceKindCode,
                Is.EqualTo(Npc물류운송Codes.ScenarioProcedural));
            Assert.That(presenter.NpcRouteStateCode, Is.EqualTo(Npc물류운송Codes.Arrived));
        }

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

        [UnityTest]
        public IEnumerator 저장Scene은_F2_W_F3입력으로_시점과플레이어위치를바꾼다()
        {
            void RemoveLiveServerCompositions(Scene loadedScene, LoadSceneMode mode)
            {
                foreach (var value in Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<농장전투CompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
            }

            SceneManager.sceneLoaded += RemoveLiveServerCompositions;
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= RemoveLiveServerCompositions;

            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitForCameraTransition(player);
            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.FirstPerson));

            var before = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (var index = 0; index < 8; index++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Distance(before, player.transform.position),
                Is.GreaterThan(.01f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return WaitForCameraTransition(player);
            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.ThirdPerson));
        }

        [UnityTest]
        public IEnumerator 저장Scene의_일인칭과삼인칭은_같은곡선을_왕복한다()
        {
            void RemoveLiveServerCompositions(Scene loadedScene, LoadSceneMode mode)
            {
                foreach (var value in Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<농장전투CompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
            }

            SceneManager.sceneLoaded += RemoveLiveServerCompositions;
            yield return SceneManager.LoadSceneAsync(
                "SimulationWorldShell", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= RemoveLiveServerCompositions;

            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            Assert.That(shell, Is.Not.Null);
            var worldTick = shell!.WorldTick;
            var worldRevision = shell.WorldRevision;

            player!.EnterFirstPersonMode();
            player.TickCameraTransition(player.ViewTransitionDuration);

            const int sampleCount = 10;
            var forwardPositions = new List<Vector3>(sampleCount + 1);
            var forwardRotations = new List<Quaternion>(sampleCount + 1);
            var forwardFieldOfViews = new List<float>(sampleCount + 1);

            player.EnterThirdPersonMode();
            var transitionCamera = player.transform.Find("시점전환Camera")
                ?.GetComponent<Camera>();
            Assert.That(transitionCamera, Is.Not.Null);
            Capture(transitionCamera!, forwardPositions,
                forwardRotations, forwardFieldOfViews);
            for (var index = 0; index < sampleCount; index++)
            {
                player.TickCameraTransition(
                    player.ViewTransitionDuration / sampleCount);
                Capture(transitionCamera!, forwardPositions,
                    forwardRotations, forwardFieldOfViews);
            }

            var reversePositions = new List<Vector3>(sampleCount + 1);
            var reverseRotations = new List<Quaternion>(sampleCount + 1);
            var reverseFieldOfViews = new List<float>(sampleCount + 1);
            player.EnterFirstPersonMode();
            Capture(transitionCamera!, reversePositions,
                reverseRotations, reverseFieldOfViews);
            for (var index = 0; index < sampleCount; index++)
            {
                player.TickCameraTransition(
                    player.ViewTransitionDuration / sampleCount);
                Capture(transitionCamera!, reversePositions,
                    reverseRotations, reverseFieldOfViews);
            }

            for (var index = 0; index <= sampleCount; index++)
            {
                var reverseIndex = sampleCount - index;
                Assert.That(Vector3.Distance(
                        forwardPositions[index], reversePositions[reverseIndex]),
                    Is.LessThan(.001f), $"왕복 위치 표본 {index} 불일치");
                Assert.That(Quaternion.Angle(
                        forwardRotations[index], reverseRotations[reverseIndex]),
                    Is.LessThan(.01f), $"왕복 회전 표본 {index} 불일치");
                Assert.That(forwardFieldOfViews[index],
                    Is.EqualTo(reverseFieldOfViews[reverseIndex]).Within(.001f),
                    $"왕복 시야각 표본 {index} 불일치");
            }

            Assert.That(player.CurrentMode, Is.EqualTo(플레이어시점Mode.FirstPerson));
            Assert.That(shell.WorldTick, Is.EqualTo(worldTick));
            Assert.That(shell.WorldRevision, Is.EqualTo(worldRevision));
        }

        private static void Capture(
            Camera camera,
            ICollection<Vector3> positions,
            ICollection<Quaternion> rotations,
            ICollection<float> fieldOfViews)
        {
            positions.Add(camera.transform.position);
            rotations.Add(camera.transform.rotation);
            fieldOfViews.Add(camera.fieldOfView);
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
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
