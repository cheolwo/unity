using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 소유 Preview에서 생성/표시만 검증한다. 저장 Scene 반영·실제 기상 입력·GameView는 별도다.
    public sealed class Sky구름초기표시Tests
    {
        private Scene scene;
        private GameObject owner;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("D378OwnedCloudFixture");
            SceneManager.MoveGameObjectToScene(owner, scene);
        }

        [TearDown] public void TearDown()
        {
            try { if (owner != null) Object.DestroyImmediate(owner); }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        private Transform Build()
        {
            // 공개 메뉴는 저장을 수행하므로 호출하지 않는다.
            typeof(SimulationWorldShellBuilder).GetMethod("BuildSkyEngineVisualRoot",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { owner.transform });
            return owner.transform.Find("SkyEngineVisualRoot/Clouds");
        }

        [Test] public void 생성구름은_원본연결과배치를보존하며_권위준비전비활성이다()
        {
            var clouds = Build();
            Assert.That(clouds.childCount, Is.EqualTo(5));
            Assert.That(clouds.gameObject.activeSelf, Is.True);
            Assert.That(clouds.localPosition, Is.EqualTo(new Vector3(0, 38, 0)));
            var positions = new[] { new Vector3(-28, 0, -12), new Vector3(-12, 2, 17),
                new Vector3(6, -1, -20), new Vector3(22, 3, 9), new Vector3(34, 0, -5) };
            for (int index = 1; index <= 5; index++)
            {
                var child = clouds.GetChild(index - 1);
                Assert.That(child.name, Is.EqualTo("SyntyCloud_" + index.ToString("00")));
                Assert.That(child.gameObject.activeSelf, Is.False);
                Assert.That(child.localPosition, Is.EqualTo(positions[index - 1]));
                Assert.That(child.localScale, Is.EqualTo(Vector3.one * (7f + index * .7f)));
                Assert.That(Quaternion.Angle(child.localRotation, Quaternion.Euler(0, index * 37, 0)), Is.LessThan(.001f));
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject),
                    Is.EqualTo(SimulationWorldShellBuilder.SkyEngineCloudPrefabPrefix + index + ".prefab"));
                Assert.That(child.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(child.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            }
        }

        [Test] public void 재생성은_소유구름만교체하고_중복을남기지않는다()
        {
            var other = new GameObject("UnrelatedFixture");
            other.transform.SetParent(owner.transform, false);
            var old = Build();
            var current = Build();
            Assert.That(old == null, Is.True);
            Assert.That(owner.transform.childCount, Is.EqualTo(2));
            Assert.That(other != null && other.activeSelf, Is.True);
            Assert.That(current.childCount, Is.EqualTo(5));
            Assert.That(current.Cast<Transform>().All(value => !value.gameObject.activeSelf), Is.True);
        }

        [TestCase(0f, 0)] [TestCase(.15f, 0)] [TestCase(.16f, 1)]
        [TestCase(.5f, 3)] [TestCase(1f, 5)]
        public void 기존기상비율은_초기비활성구름을복원하되_Renderer보기정책은바꾸지않는다(float cover, int expected)
        {
            var clouds = Build();
            var presenter = clouds.parent.gameObject.AddComponent<SkyEnginePresenter>();
            typeof(SkyEnginePresenter).GetField("cloudVisualRoot", Private).SetValue(presenter, clouds);
            var renderers = clouds.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++) renderers[index].enabled = index % 2 == 0;
            var visibility = renderers.Select(value => value.enabled).ToArray();
            var positions = clouds.Cast<Transform>().Select(value => value.localPosition).ToArray();
            var apply = typeof(SkyEnginePresenter).GetMethod("ApplyClouds", Private);
            apply.Invoke(presenter, new object[] { cover });
            Assert.That(presenter.VisibleCloudCount, Is.EqualTo(expected));
            Assert.That(renderers.Select(value => value.enabled).ToArray(), Is.EqualTo(visibility));
            Assert.That(clouds.Cast<Transform>().Select(value => value.localPosition).ToArray(), Is.EqualTo(positions));
            apply.Invoke(presenter, new object[] { 0f });
            Assert.That(presenter.VisibleCloudCount, Is.Zero);
            Assert.That(renderers.Select(value => value.enabled).ToArray(), Is.EqualTo(visibility));
        }
    }
}
