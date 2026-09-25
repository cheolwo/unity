using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 지구위경도선Tests
    {
        [Test] public void 경선은양극을잇고반지름을유지한다()
        {
            var points = 지구위경도선View.Meridian(130, 18.16f);
            Assert.That(points.Length, Is.EqualTo(181));
            Assert.That(points[0].y, Is.EqualTo(-18.16f).Within(.0001f));
            Assert.That(points[180].y, Is.EqualTo(18.16f).Within(.0001f));
            foreach (var point in points) Assert.That(point.magnitude, Is.EqualTo(18.16f).Within(.0001f));
        }
        [Test] public void 위선은일정위도를유지하며이음이한도간격이다()
        {
            var points = 지구위경도선View.Parallel(30, 18.16f);
            foreach (var point in points) Assert.That(point.y, Is.EqualTo(9.08f).Within(.0001f));
            Assert.That(Vector3.Distance(points[0], points[359]), Is.EqualTo(Vector3.Distance(points[0], points[1])).Within(.0001f));
        }
        [Test] public void 날짜변경선양쪽좌표는같다()
            => Assert.That(Vector3.Distance(지구위경도선View.Coordinate(35, -180, 18), 지구위경도선View.Coordinate(35, 180, 18)), Is.LessThan(.0001f));
        [Test] public void 확대간격은반복생성없이경계여유를두고전환한다()
        {
            var root = new GameObject("test grid"); var ui = new GameObject("test canvas", typeof(Canvas));
            var camera = new GameObject("test camera", typeof(Camera));
            try
            {
                var view = root.AddComponent<지구위경도선View>();
                view.Initialize(root.transform, ui.GetComponent<Canvas>(), camera.GetComponent<Camera>(), 18);
                var count = view.LineCount;
                Assert.That(view.SpacingDegrees, Is.EqualTo(10));
                view.UpdateDensity(40); Assert.That(view.SpacingDegrees, Is.EqualTo(2));
                view.UpdateDensity(41); Assert.That(view.SpacingDegrees, Is.EqualTo(2));
                view.UpdateDensity(42); Assert.That(view.SpacingDegrees, Is.EqualTo(10));
                view.UpdateDensity(41); Assert.That(view.SpacingDegrees, Is.EqualTo(10));
                view.Initialize(root.transform, ui.GetComponent<Canvas>(), camera.GetComponent<Camera>(), 18);
                Assert.That(view.LineCount, Is.EqualTo(count));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(ui); Object.DestroyImmediate(camera); }
        }
    }
}
