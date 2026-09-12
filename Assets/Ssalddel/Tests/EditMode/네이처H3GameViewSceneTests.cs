using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class 네이처H3GameViewSceneTests
    {
        [Test]
        public void 검토Scene은_카메라와플레이어와_완전한H3를가진다()
        {
            네이처H3GameViewSceneBuilder.BuildReviewScene();
            var root = GameObject.Find(네이처H3GameViewSceneBuilder.RootName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root!.GetComponent<네이처H3GameViewDirector>().Validate(),
                Is.True);
            Assert.That(root.GetComponentInChildren<
                네이처생활핵조우방어공간Root>(true).Validate(), Is.True);
            Assert.That(root.GetComponentsInChildren<Camera>(true).Length,
                Is.EqualTo(1));
            Assert.That(root.transform.Find("ReviewPlayer"), Is.Not.Null);
            Assert.That(root.transform.Find("ReviewPlayer")!
                .GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(EditorSceneManager.GetActiveScene().path,
                Is.EqualTo(네이처H3GameViewSceneBuilder.ScenePath));
        }

        [Test]
        public void 하천은_표현과이동계층을분리하고_다리와여울을연다()
        {
            네이처H3GameViewSceneBuilder.BuildReviewScene();
            var root = GameObject.Find(네이처H3GameViewSceneBuilder.RootName)!;
            var presentation = root.transform.Find("NatureRiverRoot/"
                + 네이처H3GameViewSceneBuilder.RiverPresentationRootName)!;
            var traversal = root.transform.Find("NatureRiverRoot/"
                + 네이처H3GameViewSceneBuilder.RiverTraversalRootName)!;
            Assert.That(presentation, Is.Not.Null);
            Assert.That(traversal, Is.Not.Null);
            Assert.That(presentation.GetComponentsInChildren<Collider>(true)
                .Where(value => value.enabled), Is.Empty);
            Assert.That(traversal.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(traversal.Find(
                네이처H3GameViewSceneBuilder.BridgeOpeningName), Is.Not.Null);
            Assert.That(traversal.Find(
                네이처H3GameViewSceneBuilder.FordOpeningName), Is.Not.Null);
            Assert.That(traversal.Find("RiverBlockers")!
                .GetComponentsInChildren<BoxCollider>(true).Length,
                Is.GreaterThanOrEqualTo(8));

            var water = presentation.Find("WaterSegments")!;
            Assert.That(water.Cast<Transform>().Count(value =>
                    value.name.StartsWith("RiverPlane_", StringComparison.Ordinal)),
                Is.EqualTo(네이처H3GameViewSceneBuilder.RiverControlPoints.Count - 1));
            Assert.That(water.Cast<Transform>().Count(value =>
                    value.name.StartsWith("RiverSurface_", StringComparison.Ordinal)),
                Is.EqualTo(네이처H3GameViewSceneBuilder.RiverControlPoints.Count - 1));
            Assert.That(water.Cast<Transform>().Count(value =>
                    value.name.StartsWith("RiverSurfaceCorner_", StringComparison.Ordinal)),
                Is.EqualTo(네이처H3GameViewSceneBuilder.RiverControlPoints.Count - 2));
            foreach (var renderer in water.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null);
                Assert.That(material.shader.name,
                    Is.Not.EqualTo("Hidden/InternalErrorShader"));
            }

            var director = root.GetComponent<네이처H3GameViewDirector>();
            Assert.That(director.CaptureEvidenceOnPlay, Is.False);
            Assert.That(director.Bridge, Is.Not.Null);
            Assert.That(director.Ford, Is.Not.Null);
        }

        [Test]
        public void 숲배치는_영역별수량과고정Seed를보존한다()
        {
            네이처H3GameViewSceneBuilder.BuildReviewScene();
            var firstRoot = GameObject.Find(
                네이처H3GameViewSceneBuilder.RootName)!.transform;
            var firstForest = firstRoot.Find("NatureForestZones")!;
            var firstSignature = PlacementSignature(firstForest);
            Assert.That(firstForest.Find("OuterForestZone")!.childCount,
                Is.EqualTo(24));
            Assert.That(firstForest.Find("RiverBankZone")!.childCount,
                Is.EqualTo(8));
            Assert.That(firstForest.Find("ForestUnderstoryZone")!.childCount,
                Is.EqualTo(20));
            Assert.That(firstForest.Find("ForestRockZone")!.childCount,
                Is.EqualTo(16));

            네이처H3GameViewSceneBuilder.BuildReviewScene();
            var secondForest = GameObject.Find(
                네이처H3GameViewSceneBuilder.RootName)!.transform
                .Find("NatureForestZones")!;
            Assert.That(PlacementSignature(secondForest), Is.EqualTo(firstSignature));
        }

        private static string PlacementSignature(Transform root)
            => string.Join("|", root.GetComponentsInChildren<Transform>(true)
                .Where(value => value != root)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .Select(value => $"{value.name}:{value.localPosition.x:F3},"
                                 + $"{value.localPosition.y:F3},"
                                 + $"{value.localPosition.z:F3}:"
                                 + $"{value.localEulerAngles.y:F3}:"
                                 + $"{value.localScale.x:F3}"));
    }
}
