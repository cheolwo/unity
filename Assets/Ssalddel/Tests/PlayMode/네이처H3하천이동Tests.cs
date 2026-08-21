using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 네이처H3하천이동Tests
    {
        [UnityTest]
        public IEnumerator 이동차단Collider는_일반구간을막고_열린횡단부를통과시킨다()
        {
            var root = new GameObject("NatureRiverTraversalTestRoot");
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(0f, -.5f, 3f);
            var groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(12f, 1f, 12f);

            var player = new GameObject("ReviewPlayer");
            player.transform.SetParent(root.transform);
            player.transform.position = Vector3.zero;
            var controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = .38f;

            var cameraObject = new GameObject("ReviewCamera");
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            var home = Anchor(root.transform, "Home", Vector3.zero);
            var encounter = Anchor(root.transform, "Encounter", Vector3.forward * 6f);
            var defense = Anchor(root.transform, "Defense", Vector3.right * 6f);
            var bridge = Anchor(root.transform, "Bridge", Vector3.forward * 2f);
            var ford = Anchor(root.transform, "Ford", Vector3.right * 2f);
            var director = root.AddComponent<네이처H3GameViewDirector>();
            director.Configure(camera, player.transform, controller, home,
                encounter, defense, bridge, ford, false);

            var blocker = new GameObject("RiverBlocker");
            blocker.transform.SetParent(root.transform);
            blocker.transform.position = new Vector3(0f, 1f, 2f);
            var blockerCollider = blocker.AddComponent<BoxCollider>();
            blockerCollider.size = new Vector3(8f, 2f, .5f);
            Physics.SyncTransforms();
            yield return null;

            for (var index = 0; index < 45; index++)
            {
                director.MovePlayerForReview(Vector3.forward, 1f / 60f);
                yield return null;
            }
            Assert.That(player.transform.position.z, Is.LessThan(1.55f));

            Object.Destroy(blocker);
            yield return null;
            controller.enabled = false;
            player.transform.position = Vector3.zero;
            controller.enabled = true;
            Physics.SyncTransforms();
            for (var index = 0; index < 45; index++)
            {
                director.MovePlayerForReview(Vector3.forward, 1f / 60f);
                yield return null;
            }
            Assert.That(player.transform.position.z, Is.GreaterThan(3f));
            Object.Destroy(root);
        }

        private static Transform Anchor(Transform parent, string name,
            Vector3 position)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent);
            value.position = position;
            return value;
        }
    }
}
