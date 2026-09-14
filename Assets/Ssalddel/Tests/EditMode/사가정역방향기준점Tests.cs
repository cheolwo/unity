using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정역방향기준점Tests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");
        private static TextAsset AnchorAsset =>
            Resources.Load<TextAsset>("SagajeongStationOrientationAnchors");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(MapAsset.text);
        private static 역세권방향기준점자료 Data =>
            JsonUtility.FromJson<역세권방향기준점자료>(AnchorAsset.text);

        [Test]
        public void 동결지도에결속된_사가정역_네출구만허용한다()
        {
            var result = 사가정역방향기준점조회.Validate(Data, Map, MapAsset.bytes);

            Assert.That(result.Revision, Is.EqualTo("sagajeong-station-exits.r1"));
            Assert.That(result.Anchors.Select(value => value.exitNumber),
                Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(result.Anchors.Select(value => value.sourceNodeId).Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(result.Exit(1).x, Is.EqualTo(557.649f).Within(.001f));
            Assert.That(result.Exit(1).z, Is.EqualTo(109.022f).Within(.001f));
            Assert.That(result.Exit(4).x, Is.EqualTo(578.266f).Within(.001f));
            Assert.That(result.Exit(4).z, Is.EqualTo(-14.697f).Within(.001f));
            Assert.That(result.Anchors.All(value => !value.traversalReady
                                                    && !value.interactionReady
                                                    && !value.operationalAuthority), Is.True);
        }

        [Test]
        public void 지도Hash와_권위경계를_바꾸면_출구표현을거부한다()
        {
            var changedBytes = MapAsset.bytes.ToArray();
            changedBytes[^1] ^= 1;
            Assert.Throws<ArgumentException>(() =>
                사가정역방향기준점조회.Validate(Data, Map, changedBytes));

            var changed = Data;
            changed.anchors[0].traversalReady = true;
            Assert.Throws<ArgumentException>(() =>
                사가정역방향기준점조회.Validate(changed, Map, MapAsset.bytes));
        }

        [Test]
        public void 일반화된_출구모형은_네기준점과_콜라이더없는_스무부품을만든다()
        {
            var parent = new GameObject("station-exit-builder-test").transform;
            try
            {
                var source = 사가정역방향기준점조회.Validate(Data, Map, MapAsset.bytes);
                var report = 사가정역방향기준점Builder.Build(source, Map, parent,
                    (container, name, position, scale, _) =>
                    {
                        var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        value.name = name;
                        value.layer = 사가정운영디오라마View.ObservationLayer;
                        value.transform.SetParent(container, false);
                        value.transform.localPosition = position;
                        value.transform.localScale = scale;
                        UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
                    });

                Assert.That(report.AnchorCount, Is.EqualTo(4));
                Assert.That(report.VisualPartCount, Is.EqualTo(20));
                Assert.That(report.Root.childCount, Is.EqualTo(4));
                Assert.That(report.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(report.Root.GetComponentsInChildren<Renderer>(true)
                    .All(value => value.gameObject.layer == 사가정운영디오라마View.ObservationLayer),
                    Is.True);
                foreach (var anchor in report.Anchors)
                {
                    var group = report.Root.GetChild(anchor.exitNumber - 1);
                    Assert.That(group.localPosition.x, Is.EqualTo(anchor.x).Within(.001f));
                    Assert.That(group.localPosition.z, Is.EqualTo(anchor.z).Within(.001f));
                    var towardStation = new Vector3(Map.offsetX - anchor.x, 0f,
                        Map.offsetZ - anchor.z).normalized;
                    Assert.That(Vector3.Dot(group.forward, towardStation), Is.GreaterThan(.999f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void 사가정운영View는_지도실패없이_네출구기준점을조립한다()
        {
            var root = new GameObject("station-exit-view-test");
            try
            {
                var view = root.AddComponent<사가정운영디오라마View>();
                view.Initialize();

                Assert.That(view.MapStatus, Is.EqualTo("Ready"));
                Assert.That(view.StationExitAnchorCount, Is.EqualTo(4));
                Assert.That(view.StationExitAnchorStatus,
                    Does.Contain("sagajeong-station-exits.r1"));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
