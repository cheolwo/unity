using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "canonical Scene에서 도끼·벌목·오두막·취소·저장·재진입을 실제 입력으로 검증한다.",
        Boundary = "자동 시험 증거를 사람의 Game View 확인 또는 Hosted 완료로 확대하지 않는다.")]
    public sealed class Nature생존E7폐루프PlayModeTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_Nature생활거점저장복원을완주한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-e7-playmode-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureE7ValidationKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>(
                "NatureE7ValidationMouse");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();
                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                var axe = Marker(Nature생존상호작용Kind.Axe);
                yield return ClickMarker(controller, player, axe, mouse);
                Assert.That(controller.State.HasAxe, Is.True,
                    $"status={controller.Status}; screen={Screen.width}x{Screen.height}; "
                    + $"mode={player.CurrentMode}; mouse={Mouse.current?.name}; "
                    + $"active={controller.isActiveAndEnabled}");

                var trees = UnityEngine.Object.FindObjectsByType<
                             Nature생존상호작용표식>(FindObjectsInactive.Include)
                         .Where(value => value.Kind ==
                             Nature생존상호작용Kind.HarvestTree)
                         .OrderBy(value => Vector3.Distance(
                             player.transform.position, value.transform.position))
                         .ThenBy(value => value.StableId,
                             StringComparer.Ordinal)
                         .Take(3).ToArray();
                var revisionBeforeApproach = controller.State.Revision;
                var cycleBeforeApproach = controller.State.CycleIndex;
                var elapsedBeforeApproach = controller.State.ElapsedSecondsInCycle;
                var timberBeforeApproach = controller.State.TimberQuantity;
                var resourcesBeforeApproach = string.Join("|",
                    controller.State.ResourceNodes
                        .OrderBy(value => value.StableId, StringComparer.Ordinal)
                        .Select(value => value.StableId + ":" + value.StateCode));
                yield return Click(controller, mouse,
                    new Vector2(364f, Screen.height - 167f));
                Assert.That(controller.AssistedApproachActive, Is.True);
                var approachDeadline = Time.unscaledTime + 8f;
                while (controller.AssistedApproachActive
                       && Time.unscaledTime < approachDeadline)
                    yield return null;
                Assert.That(controller.AssistedApproachActive, Is.False,
                    "다음 목표 접근이 제한 시간 안에 끝나야 한다.");
                Assert.That(controller.State.CycleIndex,
                    Is.EqualTo(cycleBeforeApproach));
                Assert.That(controller.State.Revision - revisionBeforeApproach,
                    Is.EqualTo(controller.State.ElapsedSecondsInCycle
                               - elapsedBeforeApproach),
                    "목표 접근 중 revision 변화는 기존 실시간 시계 진행분뿐이어야 한다.");
                Assert.That(controller.State.TimberQuantity,
                    Is.EqualTo(timberBeforeApproach),
                    "목표 접근은 자원 상태를 직접 변경하면 안 된다.");
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(string.Join("|", controller.State.ResourceNodes
                        .OrderBy(value => value.StableId, StringComparer.Ordinal)
                        .Select(value => value.StableId + ":" + value.StateCode)),
                    Is.EqualTo(resourcesBeforeApproach));
                Assert.That(Vector3.Distance(player.transform.position,
                        trees[0].transform.position), Is.LessThan(3.3f));
                yield return ClickMarker(controller, player, trees[0], mouse);
                Assert.That(controller.State.ActiveWork?.TargetStableId,
                    Is.EqualTo(trees[0].StableId));
                yield return PressKey(controller, keyboard, Key.C);
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(controller.State.TimberQuantity, Is.Zero);
                Assert.That(controller.State.ResourceNodes.Single(value =>
                    value.StableId == trees[0].StableId).StateCode,
                    Is.EqualTo(Nature생존Codes.Standing));

                yield return HoldPrimaryOnMarker(controller, player, trees[0], mouse,
                    NatureSurvivalRules.HarvestWorkSeconds + .25f);
                yield return ClickMarker(controller, player, trees[1], mouse);
                yield return Click(controller, mouse,
                    new Vector2(226f, Screen.height - 197f));
                Assert.That(controller.WorkHoldRequested, Is.True);
                var workHoldDeadline = Time.unscaledTime
                                       + NatureSurvivalRules.HarvestWorkSeconds + 3f;
                while (controller.State.ActiveWork != null
                       && Time.unscaledTime < workHoldDeadline)
                    yield return null;
                Assert.That(controller.State.ActiveWork, Is.Null,
                    "작업 유지 입력으로 벌목이 완료돼야 한다.");
                Assert.That(controller.WorkHoldRequested, Is.False);
                foreach (var tree in trees.Skip(2))
                {
                    yield return HoldPrimaryOnMarker(controller, player, tree, mouse,
                        NatureSurvivalRules.HarvestWorkSeconds + .25f);
                    Assert.That(controller.State.ActiveWork, Is.Null,
                        $"벌목 입력 완료 뒤 작업이 남았습니다: {tree.StableId}; "
                        + $"progress={controller.State.ActiveWork?.CompletedSeconds}/"
                        + controller.State.ActiveWork?.RequiredSeconds);
                }
                Assert.That(controller.State.TimberQuantity, Is.EqualTo(6));

                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                var placementGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                placementGround.name = "Nature_E7_ValidationPlacementGround";
                placementGround.transform.position = controller.transform.position;
                placementGround.transform.localScale = Vector3.one * .6f;
                player.transform.position = placementGround.transform.position
                    + Vector3.back * 2f;
                var camera = player.FirstPersonCamera;
                camera.transform.position = placementGround.transform.position
                    + Vector3.up * 7f;
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                yield return Click(controller, mouse);
                UnityEngine.Object.Destroy(placementGround);
                Assert.That(controller.State.Cabin.StateCode,
                    Is.Not.EqualTo(Nature생존Codes.Planned));

                var cabin = Marker(Nature생존상호작용Kind.Cabin);
                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                Assert.That(controller.AssistedApproachActive
                            || controller.Status.Contains("오두막 접근"), Is.True,
                    "도면 배치 뒤 같은 HUD 버튼은 남은 나무보다 오두막 접근을 우선해야 한다."
                    + $" status={controller.Status}");
                var cabinApproachDeadline = Time.unscaledTime + 8f;
                while (controller.AssistedApproachActive
                       && Time.unscaledTime < cabinApproachDeadline)
                    yield return null;
                Assert.That(controller.AssistedApproachActive, Is.False);
                Assert.That(Vector3.Distance(player.transform.position,
                    cabin.transform.position), Is.LessThan(3.3f));
                Assert.That(Vector3.Distance(player.transform.position,
                    cabin.transform.position), Is.GreaterThan(2.7f),
                    "오두막 접근은 카메라가 외벽 안으로 파고들지 않는 거리에서 끝나야 한다.");
                yield return HoldPrimaryOnMarker(controller, player, cabin, mouse,
                    NatureSurvivalRules.CabinWorkSeconds + 1.25f);
                Assert.That(controller.State.Cabin.StateCode,
                    Is.EqualTo(Nature생존Codes.Completed),
                    $"progress={controller.State.ActiveWork?.CompletedSeconds}/"
                    + controller.State.ActiveWork?.RequiredSeconds);
                yield return ClickMarker(controller, player, cabin, mouse);
                Assert.That(controller.State.PlayerInsideCabin, Is.True);
                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                Assert.That(controller.State.PlayerInsideCabin, Is.False);

                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 167f));
                Assert.That(controller.Status, Does.StartWith("저장 완료"));
                var savedSession = controller.State.SessionStableId;
                var savedRevision = controller.State.Revision;

                controller.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                Time.timeScale = 1f;
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();
                var restored = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                Assert.That(restored.State.SessionStableId,
                    Is.EqualTo(savedSession));
                Assert.That(restored.State.Revision, Is.EqualTo(savedRevision));
                Assert.That(restored.State.Cabin.StateCode,
                    Is.EqualTo(Nature생존Codes.Completed));
                Assert.That(restored.State.HasAxe, Is.True);
                Assert.That(restored.State.ResourceNodes.Count(value =>
                    value.StateCode == Nature생존Codes.Stump), Is.EqualTo(3));
                Assert.That(restored.State.PlayerInsideCabin, Is.False);
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        private static Nature생존상호작용표식 Marker(
            Nature생존상호작용Kind kind)
            => UnityEngine.Object.FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Include)
                .First(value => value.Kind == kind);

        private static IEnumerator ClickMarker(Nature생존Controller controller,
            플레이어경관Controller player,
            Nature생존상호작용표식 marker, Mouse mouse)
        {
            var screenPoint = PointAt(player, marker);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = screenPoint,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            PointAt(player, marker);
            Assert.That(플레이어경관Controller.IsPointerInPersistentModeBand(
                screenPoint, Screen.height), Is.False,
                $"배치 객체 조준점이 하단 모드 영역과 겹칩니다: {screenPoint}");
            Assert.That(플레이어경관Controller.IsPointerInPersistentHudControls(
                screenPoint, Screen.width, Screen.height), Is.False,
                $"배치 객체 조준점이 HUD 버튼 영역과 겹칩니다: {screenPoint}");
            Assert.That(플레이어경관Controller.IsPointerOverUnityUi(), Is.False,
                $"배치 객체 조준점 위에 Unity UI가 입력을 가립니다: {screenPoint}");
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(mouse,
                new MouseState { position = screenPoint });
            InputSystem.Update();
            mouse.MakeCurrent();
            yield return null;
        }

        private static IEnumerator Click(Nature생존Controller controller,
            Mouse mouse, Vector2? requestedPosition = null)
        {
            var position = requestedPosition
                ?? new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            Assert.That(controller.CurrentPrimaryInputPressedForTest, Is.True,
                "Nature controller did not observe the pressed mouse device.");
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            mouse.MakeCurrent();
            yield return null;
        }

        private static IEnumerator HoldPrimaryOnMarker(
            Nature생존Controller controller, 플레이어경관Controller player,
            Nature생존상호작용표식 marker, Mouse mouse, float seconds)
        {
            var position = PointAt(player, marker);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            PointAt(player, marker);
            controller.ProcessCurrentInputForTest();
            Assert.That(controller.State.ActiveWork, Is.Not.Null,
                $"지속 작업 시작 입력이 수락되지 않았습니다: {marker.Kind}/"
                + $"{marker.StableId}; status={controller.Status}; "
                + $"screen={position}; mode={player.CurrentMode}; "
                + $"distance={Vector3.Distance(player.transform.position, marker.transform.position)}; "
                + $"angle={Vector3.Angle(player.FirstPersonCamera.transform.forward, MarkerAimPoint(marker) - player.FirstPersonCamera.transform.position)}; "
                + $"active={marker.gameObject.activeInHierarchy}");

            var deadline = Time.unscaledTime + seconds * 2f + 2f;
            while (controller.State.ActiveWork != null
                   && Time.unscaledTime < deadline)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = position,
                }.WithButton(MouseButton.Left));
                InputSystem.Update();
                mouse.MakeCurrent();
                controller.ContinueCurrentInputForTest();
                yield return null;
            }

            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            mouse.MakeCurrent();
            controller.ContinueCurrentInputForTest();
            yield return null;
        }

        private static IEnumerator PressKey(Nature생존Controller controller,
            Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            keyboard.MakeCurrent();
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            keyboard.MakeCurrent();
            yield return null;
        }

        private static Vector2 PointAt(플레이어경관Controller player,
            Nature생존상호작용표식 marker)
        {
            var target = MarkerAimPoint(marker);
            player.transform.position = marker.transform.position
                + Vector3.back * 2.4f;
            player.transform.LookAt(new Vector3(target.x,
                player.transform.position.y, target.z));
            var camera = player.FirstPersonCamera;
            camera.transform.position = player.transform.position
                + Vector3.up * 1.35f;
            camera.transform.LookAt(target);
            Physics.SyncTransforms();
            return camera.WorldToScreenPoint(target);
        }

        private static Vector3 MarkerAimPoint(Nature생존상호작용표식 marker)
        {
            var collider = marker.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : marker.transform.position;
        }

        private static IEnumerator WaitForNatureController()
        {
            var timeout = 10f;
            while (UnityEngine.Object.FindAnyObjectByType<Nature생존Controller>(
                       FindObjectsInactive.Include) == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(UnityEngine.Object.FindAnyObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include), Is.Not.Null);
        }
    }
}
