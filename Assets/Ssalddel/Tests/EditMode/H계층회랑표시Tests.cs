using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 소유 Preview의 표시 정책 시험. 실제 canonical 참조·Play·통행은 공간 검증으로 분리한다.
    public sealed class H계층회랑표시Tests
    {
        private Scene scene;
        private GameObject owner;
        private H계층배치증거Root evidence;
        private Transform corridors;
        private GameObject support;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("D376OwnedOverlayFixture");
            SceneManager.MoveGameObjectToScene(owner, scene);
            evidence = owner.AddComponent<H계층배치증거Root>();
            corridors = new GameObject("TransitionCorridors_H5_3").transform;
            corridors.SetParent(owner.transform, false);
            for (int index = 0; index < 3; index++)
            {
                var corridor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                corridor.transform.SetParent(corridors, false);
                corridor.transform.localPosition = new Vector3(index * 2, .75f, index);
                corridor.transform.localScale = new Vector3(1.2f, .12f, 150f);
                Object.DestroyImmediate(corridor.GetComponent<Collider>());
            }
            support = GameObject.CreatePrimitive(PrimitiveType.Cube);
            support.name = "H3TerrainUnderlay_ActualSupport";
            support.transform.SetParent(owner.transform, false);
            typeof(H계층배치증거Root).GetField("corridorRoot", Private).SetValue(evidence, corridors);
        }

        [TearDown] public void TearDown()
        {
            try { if (owner != null) Object.DestroyImmediate(owner); }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        private void AssertVisible(bool visible)
        {
            foreach (var renderer in corridors.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.enabled, Is.EqualTo(visible));
        }

        [Test] public void 기본Awake재적용은_회랑Renderer만숨긴다()
        {
            typeof(H계층배치증거Root).GetMethod("Awake", Private).Invoke(evidence, null);
            AssertVisible(false);
            Assert.That(support.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(support.GetComponent<Collider>().enabled, Is.True);
        }

        [Test] public void 표시왕복은_세회랑구조와배치및지지면을보존한다()
        {
            var matrices = new Matrix4x4[3];
            for (int i = 0; i < 3; i++) matrices[i] = corridors.GetChild(i).localToWorldMatrix;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                evidence.SetHierarchyOverlay(false); AssertVisible(false);
                evidence.SetHierarchyOverlay(true); AssertVisible(true);
                evidence.SetHierarchyOverlay(false); AssertVisible(false);
            }
            Assert.That(corridors.childCount, Is.EqualTo(3));
            Assert.That(corridors.GetComponentsInChildren<Collider>(true), Is.Empty);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(corridors.GetChild(i).localToWorldMatrix, Is.EqualTo(matrices[i]));
                Assert.That(corridors.GetChild(i).gameObject.activeSelf, Is.True);
            }
            Assert.That(support.activeSelf, Is.True);
            Assert.That(support.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(support.GetComponent<Collider>().enabled, Is.True);
        }

        [TestCase(false)] [TestCase(true)] public void 비활성자식도_재활성전표시정책을받는다(bool visible)
        {
            var child = corridors.GetChild(0).gameObject;
            child.SetActive(false); evidence.SetHierarchyOverlay(visible);
            AssertVisible(visible); Assert.That(child.activeSelf, Is.False);
            child.SetActive(true); AssertVisible(visible);
        }

        [Test] public void 회랑참조없음은_다른지지면을검색하거나변경하지않는다()
        {
            typeof(H계층배치증거Root).GetField("corridorRoot", Private).SetValue(evidence, null);
            Assert.DoesNotThrow(() => evidence.SetHierarchyOverlay(false));
            AssertVisible(true);
            Assert.That(support.GetComponent<Renderer>().enabled, Is.True);
        }

        [Test] public void 표시에Collider가있더라도_충돌과활성은건드리지않는다()
        {
            var collider = corridors.GetChild(0).gameObject.AddComponent<BoxCollider>();
            evidence.SetHierarchyOverlay(false); AssertVisible(false);
            Assert.That(collider.enabled, Is.True);
            Assert.That(collider.gameObject.activeSelf, Is.True);
        }
    }
}
