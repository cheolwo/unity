using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 통합월드자유이동PlayModeTests
    {
        [UnityTest]
        public IEnumerator 실제W입력은_기존Farm경계를넘어_평창전체지도에서이동한다()
        {
            SceneManager.LoadScene("SimulationWorldShell", LoadSceneMode.Single);
            yield return null;
            Physics.SyncTransforms();

            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>();
            Assert.That(player, Is.Not.Null);
            Assert.That(shell, Is.Not.Null);
            Assert.That(player!.Profile.ProfileStableId,
                Is.EqualTo("player-profile:sim:pyeongchang:world-explorer.v2"));
            Assert.That(player.Profile.MinimumX,
                Is.EqualTo(평창군플레이어경관Fixture.WorldMinimumX));
            Assert.That(player.HasMovementSafetyGate, Is.True);

            var start = GroundedPosition(10.8f, 7f);
            var initialTick = shell!.WorldTick;
            var initialRevision = shell.WorldRevision;
            player.EnterFirstPersonMode();
            while (player.IsCameraTransitioning) yield return null;
            player.SetPresentationStartPose(start, -90f);
            Physics.SyncTransforms();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                for (var frame = 0; frame < 45; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                Assert.That(player.transform.position.x, Is.LessThan(10.2f));
                Assert.That(player.transform.position.x,
                    Is.GreaterThanOrEqualTo(player.Profile.MinimumX));
                Assert.That(player.MovementBlockedByStreaming, Is.False);
                Assert.That(shell.WorldTick, Is.EqualTo(initialTick));
                Assert.That(shell.WorldRevision, Is.EqualTo(initialRevision));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static Vector3 GroundedPosition(float x, float z)
        {
            var origin = new Vector3(x, 30f, z);
            Assert.That(Physics.Raycast(
                origin, Vector3.down, out var hit, 80f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            return hit.point + Vector3.up * .06f;
        }
    }
}
