using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 공간TileLodLoaderPlayModeTests
    {
        [UnityTest]
        public IEnumerator 카메라거리별_L0L1L2전환은_PresentationRoot만변경한다()
        {
            var root = new GameObject("RuntimeTileRoot");
            var overview = new GameObject("L0").transform;
            var region = new GameObject("L1").transform;
            var task = new GameObject("L2").transform;
            var cameraRoot = new GameObject("RuntimeTileCamera");
            overview.SetParent(root.transform);
            region.SetParent(root.transform);
            task.SetParent(root.transform);
            var camera = cameraRoot.AddComponent<Camera>();
            var loader = root.AddComponent<공간TileLodLoader>();
            try
            {
                camera.transform.position = new Vector3(0f, 0f, 60f);
                loader.Configure(camera, overview, region, task, 48f, 25f);
                yield return null;
                Assert.That(loader.ActiveLevel, Is.EqualTo(0));

                camera.transform.position = new Vector3(0f, 0f, 35f);
                yield return null;
                Assert.That(loader.ActiveLevel, Is.EqualTo(1));

                camera.transform.position = new Vector3(0f, 0f, 10f);
                yield return null;
                Assert.That(loader.ActiveLevel, Is.EqualTo(2));
                Assert.That(task.gameObject.activeSelf, Is.True);
                Assert.That(overview.gameObject.activeSelf, Is.False);
                Assert.That(region.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(cameraRoot);
            }
        }
    }
}
