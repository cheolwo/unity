using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 팀원관전CameraTests
    {
        [Test]
        public void 같은팀관전은_대상시점만따르고_대상조작권을만들지않는다()
        {
            var root = new GameObject("TeamObservationTestRoot");
            try
            {
                var local = new GameObject("LocalPlayer");
                local.transform.SetParent(root.transform);
                local.AddComponent<CharacterController>();
                var localController = local.AddComponent<플레이어경관Controller>();
                var previousCamera = new GameObject("PreviousCamera")
                    .AddComponent<Camera>();
                previousCamera.transform.SetParent(root.transform);
                previousCamera.enabled = true;
                var observedCamera = new GameObject("ObservedCamera")
                    .AddComponent<Camera>();
                observedCamera.transform.SetParent(root.transform);
                var controller = root.AddComponent<팀원관전CameraController>();
                controller.Configure(observedCamera, localController, previousCamera);

                var firstPerson = new GameObject("RemoteFirstPerson").transform;
                firstPerson.SetParent(root.transform);
                firstPerson.SetPositionAndRotation(new Vector3(11f, 2f, 7f),
                    Quaternion.Euler(8f, 42f, 0f));
                var follow = new GameObject("RemoteFollow").transform;
                follow.SetParent(root.transform);
                follow.SetPositionAndRotation(new Vector3(10f, 0f, 6f),
                    Quaternion.Euler(0f, 30f, 0f));

                controller.BeginObservation(State(), firstPerson, follow);

                Assert.That(controller.IsObserving, Is.True);
                Assert.That(controller.CanControlObservedTarget, Is.False);
                Assert.That(localController.enabled, Is.False);
                Assert.That(observedCamera.enabled, Is.True);
                Assert.That(previousCamera.enabled, Is.False);
                Assert.That(observedCamera.transform.position,
                    Is.EqualTo(firstPerson.position));
                Assert.That(observedCamera.transform.rotation,
                    Is.EqualTo(firstPerson.rotation));

                controller.SetViewMode(TeamObservationViewModeCodes.Follow);
                Assert.That(controller.ViewModeCode,
                    Is.EqualTo(TeamObservationViewModeCodes.Follow));
                Assert.That(follow.position, Is.EqualTo(new Vector3(10f, 0f, 6f)),
                    "관전 카메라가 대상 Transform을 변경하면 안 됩니다.");

                controller.SignalLocalDanger();
                Assert.That(controller.IsObserving, Is.False);
                Assert.That(localController.enabled, Is.True);
                Assert.That(previousCamera.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TeamObservationPresentationState State()
            => new TeamObservationPresentationState
            {
                SessionStableId = "session:sim:team-1",
                TeamStableId = "team:sim:survivors",
                LocalControlActorStableId = "actor:sim:farmer-1",
                CameraTargetActorStableId = "actor:sim:explorer-1",
                ViewModeCode = TeamObservationViewModeCodes.FirstPerson,
                TileFocusKey = "kr5186:l2:700:1145",
                TeamRevision = 3,
                IsActive = true,
                AcceptsTargetCommands = false,
                MovesLocalActor = false,
                ShowObservedIndicator = true,
                ExitOnLocalDanger = true,
                PresentationOnly = true,
            };
    }
}
