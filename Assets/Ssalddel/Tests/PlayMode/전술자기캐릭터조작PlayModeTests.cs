using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "canonical Scene의 전술 자기 캐릭터 선택·이동·카메라 재집중 실제 입력을 검증한다.",
        Boundary = "표현 이동은 WI 권위 결과나 온라인 위치 동기화를 증명하지 않는다.")]
    public sealed class 전술자기캐릭터조작PlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 전술자기캐릭터조작은_World권위개정을바꾸지않는다()
        {
            공간TileStreamRemoteRequestDiagnostics.ResetForTests();
            Screen.SetResolution(1280, 720, false);
            yield return SceneManager.LoadSceneAsync(
                "SimulationWorldShell", LoadSceneMode.Single);
            yield return null;
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            var spatialComposition = Object.FindAnyObjectByType<공간TileStreamingCompositionRoot>(
                FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            Assert.That(shell, Is.Not.Null);
            Assert.That(spatialComposition, Is.Not.Null);
            for (var frame = 0; frame < 60
                 && !spatialComposition!.Solo로컬공간파이프라인준비됨; frame++)
                yield return null;
            Assert.That(spatialComposition!.Solo로컬공간파이프라인준비됨, Is.True);
            Assert.That(spatialComposition.활성Tile자료원Code,
                Is.EqualTo(공간TileStreamingCodes.Fixture));
            Assert.That(spatialComposition.활성LH자료원Code,
                Is.Not.EqualTo(공간TileStreamingCodes.SimulationServer));
            Assert.That(공간TileStreamRemoteRequestDiagnostics.RequestCount, Is.Zero);
            player!.EnterThirdPersonMode();
            player.TickCameraTransition(player.ViewTransitionDuration);
            player.enabled = true;
            Physics.SyncTransforms();
            yield return null;

            var keyboard = InputSystem.AddDevice<Keyboard>(
                "TacticalSelfNavigationKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>(
                "TacticalSelfNavigationMouse");
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            try
            {
                var initialTick = shell!.WorldTick;
                var initialRevision = shell.WorldRevision;
                var playerPoint = player.PlayerCamera.WorldToScreenPoint(
                    player.GetComponent<CharacterController>().bounds.center);
                Assert.That(playerPoint.z, Is.GreaterThan(0f));

                yield return Click(mouse, new Vector2(playerPoint.x, playerPoint.y),
                    MouseButton.Left);
                Assert.That(player.IsSelected, Is.True);

                player.SetThirdPersonSelection(false);
                var dragStart = new Vector2(playerPoint.x - 90f,
                    playerPoint.y - 90f);
                var dragEnd = new Vector2(playerPoint.x + 90f,
                    playerPoint.y + 90f);
                yield return Drag(mouse, dragStart, dragEnd);
                Assert.That(player.IsSelected, Is.True);

                var beforeClickMove = player.transform.position;
                var cameraForward = Vector3.ProjectOnPlane(
                    player.PlayerCamera.transform.forward, Vector3.up).normalized;
                var destination = GroundPoint(beforeClickMove + cameraForward * 4f);
                var destinationPoint = player.PlayerCamera.WorldToScreenPoint(destination);
                Assert.That(destinationPoint.z, Is.GreaterThan(0f));
                yield return Click(mouse,
                    new Vector2(destinationPoint.x, destinationPoint.y),
                    MouseButton.Right);
                Assert.That(player.HasDestination, Is.True);
                for (var frame = 0; frame < 30; frame++) yield return null;
                Assert.That(Vector3.Distance(beforeClickMove,
                    player.transform.position), Is.GreaterThan(.15f));

                var beforeArrowMove = player.transform.position;
                InputSystem.QueueStateEvent(keyboard,
                    new KeyboardState(Key.UpArrow));
                for (var frame = 0; frame < 24; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(player.HasDestination, Is.False,
                    "방향키 직접 이동은 기존 우클릭 목적지를 취소해야 한다.");
                Assert.That(Vector3.Distance(beforeArrowMove,
                    player.transform.position), Is.GreaterThan(.1f));

                var beforeCameraPanPlayer = player.transform.position;
                var beforeCameraPanFocus = player.TacticalFocus;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (var frame = 0; frame < 18; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(Vector3.Distance(beforeCameraPanFocus,
                    player.TacticalFocus), Is.GreaterThan(.1f));
                Assert.That(Vector3.Distance(beforeCameraPanPlayer,
                    player.transform.position), Is.LessThan(.05f),
                    "WASD는 전술 카메라만 이동해야 한다.");

                yield return TapKey(keyboard, Key.Backspace);
                yield return TapKey(keyboard, Key.Backspace);
                Assert.That(Vector3.Distance(player.TacticalFocus,
                    player.transform.position), Is.LessThan(.05f));

                Assert.That(shell.WorldTick, Is.EqualTo(initialTick));
                Assert.That(shell.WorldRevision, Is.EqualTo(initialRevision));
                Assert.That(공간TileStreamRemoteRequestDiagnostics.RequestCount,
                    Is.Zero, "Solo 전술 이동 중 원격 공간 요청이 발생하면 안 된다.");
            }
            finally
            {
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator Click(Mouse mouse, Vector2 position,
            MouseButton button)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        private static IEnumerator Drag(Mouse mouse, Vector2 start, Vector2 end)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = start,
            }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = end,
            }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(Object.FindAnyObjectByType<플레이어전술조작Controller>()!
                .SelectionDragging, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
            yield return null;
        }

        private static IEnumerator TapKey(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static Vector3 GroundPoint(Vector3 near)
        {
            if (Physics.Raycast(near + Vector3.up * 20f, Vector3.down,
                    out var hit, 60f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(near.x, 0f, near.z);
        }
    }
}
