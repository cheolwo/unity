using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정비공개현실감StyleTests
    {
        [Test]
        public void 실제카메라벡터를_보고서에_저장할때_계산속성_순환참조를_따라가지않는다()
        {
            var report = new 사가정비공개현실감Style.Report
            {
                cameras = new[] { new 사가정비공개현실감Style.CameraReview
                {
                    cameraName = "comparison",
                    originalPosition = new Vector3(550, 1600, 8),
                    styledPosition = new Vector3(550, 65, 8)
                } }
            };
            var json = 사가정비공개현실감Style.SerializeReport(report);
            Assert.That(json, Does.Not.Contain("normalized"));
            var restored = JsonUtility.FromJson<사가정비공개현실감Style.Report>(json);
            Assert.That(restored.cameras.Length, Is.EqualTo(1));
            Assert.That(restored.cameras[0].originalPosition, Is.EqualTo(new Vector3(550, 1600, 8)));
            Assert.That(restored.cameras[0].styledPosition, Is.EqualTo(new Vector3(550, 65, 8)));
        }

        [Test]
        public void 코드질감은_반복실행동일하고_비사진미세normal을_만든다()
        {
            var first = 사가정비공개현실감Style.CreateNoisePixels(16, 20111, false);
            Assert.That(first, Is.EqualTo(사가정비공개현실감Style.CreateNoisePixels(16, 20111, false)));
            Assert.That(first.Distinct().Count(), Is.GreaterThan(2));
            var normal = 사가정비공개현실감Style.CreateNoisePixels(16, 20111, true);
            Assert.That(normal.Any(p => p.r != 128 || p.g != 128), Is.True);
            Assert.That(normal.All(p => p.b >= 240), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => 사가정비공개현실감Style.CreateNoisePixels(2048, 1, false));
        }

        [Test]
        public void 가까운정사영이_앞쪽건물을자르면_원거리카메라를_유지한다()
        {
            var cameraObject = new GameObject("fixture-camera"); var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 10; camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.transform.rotation = Quaternion.identity;
                cube.transform.position = new Vector3(0, 0, -100);
                var review = 사가정비공개현실감Style.ReviewCamera(camera, Vector3.zero, new[] { cube.GetComponent<MeshRenderer>() });
                Assert.That(review.cameraDistanceChanged, Is.False);
                Assert.That(review.styledPosition, Is.EqualTo(camera.transform.position));
                Assert.That(review.clippedVisibleTriangleCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(cube); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void 같은시선축의_안전한65m정사영은_구도를_유지한다()
        {
            var cameraObject = new GameObject("fixture-camera"); var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 10; camera.aspect = 1; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.transform.rotation = Quaternion.identity;
                var point = new Vector3(2, 1, 0); var before = camera.WorldToViewportPoint(point);
                var review = 사가정비공개현실감Style.ReviewCamera(camera, Vector3.zero, new[] { cube.GetComponent<MeshRenderer>() });
                Assert.That(review.cameraDistanceChanged, Is.True);
                camera.transform.position = review.styledPosition; var after = camera.WorldToViewportPoint(point);
                Assert.That(after.x, Is.EqualTo(before.x).Within(.00001f));
                Assert.That(after.y, Is.EqualTo(before.y).Within(.00001f));
                Assert.That(review.styledMinimumVisibleDepth, Is.GreaterThan(camera.nearClipPlane));
            }
            finally { Object.DestroyImmediate(cube); Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void 재질조명카메라사본은_Dispose후_정확한원참조로_돌아온다()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("fixture-preview");
            SceneManager.MoveGameObjectToScene(root, scene);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform, false);
            var cameraObject = new GameObject("fixture-camera"); cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.scene = scene; camera.orthographicSize = 10; camera.farClipPlane = 2000;
            camera.transform.position = new Vector3(0, 0, -1000);
            var lightObject = new GameObject("fixture-light"); lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .8f; light.color = Color.white;
            var renderer = cube.GetComponent<MeshRenderer>(); var originalMaterials = renderer.sharedMaterials;
            var mesh = cube.GetComponent<MeshFilter>().sharedMesh; var originalPosition = camera.transform.position;
            var ambient = RenderSettings.ambientLight;
            사가정비공개현실감Style.Scope scope = null;
            try
            {
                scope = 사가정비공개현실감Style.Apply(root.transform, light, new[] { camera }, new[] { Vector3.zero });
                Assert.That(renderer.sharedMaterials, Is.Not.EqualTo(originalMaterials));
                Assert.That(cube.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(scope.Result.hdriUsed, Is.False);
                Assert.That(scope.Result.originalWindowPatternPreserved, Is.True);
                scope.Dispose(); scope.Dispose();
                Assert.That(renderer.sharedMaterials, Is.EqualTo(originalMaterials));
                Assert.That(light.intensity, Is.EqualTo(.8f)); Assert.That(light.color, Is.EqualTo(Color.white));
                Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
                Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
                Assert.That(scope.Result.rendererReferencesRestored && scope.Result.lightRestored && scope.Result.cameraPosesRestored, Is.True);
            }
            finally { scope?.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
