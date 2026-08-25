using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "WI-NATURE-05 도끼 확보를 canonical Scene의 실제 입력·화면·저장 복원으로 검증한다.",
        Boundary = "이 시험은 도끼 확보 WI 하나의 E7 증거이며 후속 벌목 또는 Nature 전체 폐루프를 승격하지 않는다.")]
    public sealed class Nature도끼확보E7PlayModeTests
    {
        private const string AxeVisualSourceStableId =
            "asset:synty:polygon-generic:sm-gen-wep-axe-01";

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제마우스입력으로_도끼를확보하고_저장복원한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-wi-nature-05-e7-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable("SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var mouse = InputSystem.AddDevice<Mouse>("WiNature05E7Mouse");
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
                player.EnterFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                var axe = UnityEngine.Object.FindObjectsByType<
                        Nature생존상호작용표식>(FindObjectsInactive.Include)
                    .Single(value => value.Kind == Nature생존상호작용Kind.Axe);
                Assert.That(controller.State.HasAxe, Is.False);
                Assert.That(axe.StableId,
                    Is.EqualTo("pickup:nature-safe-clearing:basic-axe"));
                Assert.That(axe.UsesSyntyAsset, Is.True,
                    "E7 검증은 설치된 Synty 도끼를 사용해야 한다.");
                Assert.That(axe.VisualSourceStableId,
                    Is.EqualTo(AxeVisualSourceStableId));
                var collider = axe.GetComponentInChildren<Collider>();
                Assert.That(collider, Is.Not.Null,
                    "도끼 H1에는 실제 마우스 선택을 받을 충돌체가 필요하다.");

                var beforeRevision = controller.State.Revision;
                var screenPoint = PointAt(player, axe, collider!);
                yield return Click(controller, mouse, screenPoint);

                Assert.That(controller.State.HasAxe, Is.True);
                Assert.That(controller.State.Revision, Is.GreaterThan(beforeRevision));
                Assert.That(controller.State.ActiveWork, Is.Null,
                    "도끼 확보는 별도 지속 작업을 만들지 않는 단일 권위 행동이다.");
                Assert.That(controller.Status, Does.Contain("기본 도끼 획득"));
                Assert.That(axe.gameObject.activeSelf, Is.False,
                    "권위 상태가 바뀌면 도끼 표현도 같은 프레임 흐름에서 사라져야 한다.");

                controller.SaveWorldForTest();
                Assert.That(controller.Status, Does.StartWith("저장 완료"));
                var savedSession = controller.State.SessionStableId;
                var savedRevision = controller.State.Revision;

                controller.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();
                var restored = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                Assert.That(restored.State.SessionStableId, Is.EqualTo(savedSession));
                Assert.That(restored.State.Revision, Is.EqualTo(savedRevision));
                Assert.That(restored.State.HasAxe, Is.True);
                Assert.That(UnityEngine.Object.FindObjectsByType<
                        Nature생존상호작용표식>(FindObjectsInactive.Include)
                    .Single(value => value.Kind == Nature생존상호작용Kind.Axe)
                    .gameObject.activeSelf, Is.False);
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Environment.SetEnvironmentVariable("SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable("SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        private static Vector2 PointAt(플레이어경관Controller player,
            Nature생존상호작용표식 marker, Collider collider)
        {
            var target = collider.bounds.center;
            player.transform.position = marker.transform.position + Vector3.back * 2.4f;
            player.transform.LookAt(new Vector3(target.x,
                player.transform.position.y, target.z));
            var camera = player.FirstPersonCamera;
            camera.transform.position = player.transform.position + Vector3.up * 1.35f;
            camera.transform.LookAt(target);
            Physics.SyncTransforms();
            var screenPoint = camera.WorldToScreenPoint(target);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));
            Assert.That(screenPoint.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(screenPoint.y, Is.InRange(0f, (float)Screen.height));
            Assert.That(Vector3.Distance(player.transform.position,
                marker.transform.position), Is.LessThanOrEqualTo(3.2f));
            return screenPoint;
        }

        private static IEnumerator Click(Nature생존Controller controller,
            Mouse mouse, Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            Assert.That(controller.CurrentPrimaryInputPressedForTest, Is.True);
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            mouse.MakeCurrent();
            yield return null;
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
