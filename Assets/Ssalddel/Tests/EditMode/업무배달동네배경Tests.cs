using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3, "동네 배경 범위·배치·관찰 화면비 검증", Boundary = "정적 배치 시험은 업무 실행이나 실제 Game View 증거가 아니다")]
    public sealed class 업무배달동네배경Tests
    {
        [Test] public void 기본구도는_높이와_양쪽옆면을_함께보는_대각선이다()
        {
            var forward = 동네전체관찰CameraMath.기본회전 * Vector3.forward;
            Assert.That(forward.x, Is.GreaterThan(.4f));
            Assert.That(forward.y, Is.LessThan(-.4f));
            Assert.That(forward.z, Is.GreaterThan(.4f));
            Assert.That(Vector3.Dot(동네전체관찰CameraMath.근접관찰오프셋.normalized, forward), Is.EqualTo(-1f).Within(.0001f));
        }
        [Test] public void 입체명암Shader는_Resources에서_결속가능하다()
        {
            var shader = Resources.Load<Shader>("관찰도형명암");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("Ssalddel/ObservationPrimitive"));
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False);
        }
        [TestCase(1920, 1080, false)]
        [TestCase(1280, 720, true)]
        [TestCase(800, 1200, true)]
        public void 로컬동네도_입체전체보기에서_기존바닥을_자르지않는다(int width, int height, bool phoneOpen)
        {
            var bounds = new Bounds(new Vector3(0, 3, 8), new Vector3(90, 8, 58));
            var aspect = width * (phoneOpen ? Mathf.Clamp(1 - (412f / 720f) * height / width, .25f, 1) : 1) / height;
            var rotation = 동네전체관찰CameraMath.기본회전;
            var size = 동네전체관찰CameraMath.맞춤크기(bounds, rotation, aspect);
            foreach (var corner in 동네전체관찰CameraMath.모서리(bounds))
            {
                var projected = Quaternion.Inverse(rotation) * (corner - bounds.center);
                Assert.That(Mathf.Abs(projected.x), Is.LessThan(size * aspect));
                Assert.That(Mathf.Abs(projected.y), Is.LessThan(size));
            }
        }
        [TestCase(0, 8, 90, 58)]
        [TestCase(-4, 4, 58, 28)]
        public void 확장바닥은_기존프로필의_바닥범위를_모두포함한다(float x, float z, float width, float depth)
        {
            var floor = 업무배달동네배경Layout.바닥Bounds;
            Assert.That(floor.size, Is.EqualTo(new Vector3(120, .2f, 96)));
            Assert.That(floor.min.x, Is.LessThanOrEqualTo(x - width / 2));
            Assert.That(floor.max.x, Is.GreaterThanOrEqualTo(x + width / 2));
            Assert.That(floor.min.z, Is.LessThanOrEqualTo(z - depth / 2));
            Assert.That(floor.max.z, Is.GreaterThanOrEqualTo(z + depth / 2));
        }
        [Test] public void 활성시설과_기존도로의_위치및크기는_유지한다()
        {
            var buildings = 업무배달동네배경Layout.건물.Where(x => x.서버연결).ToArray();
            Assert.That(buildings.Length, Is.EqualTo(2));
            Assert.That(buildings.Single(x => x.이름 == "음식점").중심, Is.EqualTo(new Vector2(0, 6)));
            Assert.That(buildings.Single(x => x.이름 == "주문자 집").중심, Is.EqualTo(new Vector2(16, 6)));
            Assert.That(buildings.All(x => x.크기 == new Vector2(6, 6)), Is.True);
            var road = 업무배달동네배경Layout.도로.Single(x => x.이름 == "도로 동서");
            Assert.That(road.영역, Is.EqualTo(new Rect(-28, -2, 48, 4)));
            var waiting = 업무배달동네배경Layout.도로.Single(x => x.이름 == "기사 대기 연결 도로");
            Assert.That(waiting.영역, Is.EqualTo(new Rect(-22, -2, 4, 12)));
            foreach (var point in new[] { new Vector2(-8, 0), Vector2.zero, new Vector2(16, 0), new Vector2(-20, 8) })
                Assert.That(업무배달동네배경Layout.도로.Any(x => x.영역.Contains(point)), Is.True);
        }
        [Test] public void 미연동배경은_주택14_음식점4_창고2이며_고유이름을갖는다()
        {
            var background = 업무배달동네배경Layout.건물.Where(x => !x.서버연결).ToArray();
            Assert.That(background.Count(x => x.종류 == "주택"), Is.EqualTo(14));
            Assert.That(background.Count(x => x.종류 == "음식점"), Is.EqualTo(4));
            Assert.That(background.Count(x => x.창고형), Is.EqualTo(2));
            Assert.That(background.All(x => x.표시명.Contains("배경") || x.표시명.Contains("미연동")), Is.True);
            Assert.That(업무배달동네배경Layout.건물.Select(x => x.이름).Distinct().Count(), Is.EqualTo(22));
        }
        [Test] public void 건물끼리_그리고_건물과도로는_겹치지않는다()
        {
            var buildings = 업무배달동네배경Layout.건물;
            for (var i = 0; i < buildings.Count; i++)
            {
                for (var j = i + 1; j < buildings.Count; j++)
                    Assert.That(면적겹침(buildings[i].영역, buildings[j].영역), Is.False, buildings[i].이름 + " / " + buildings[j].이름);
                foreach (var road in 업무배달동네배경Layout.도로)
                    Assert.That(면적겹침(buildings[i].영역, road.영역), Is.False, buildings[i].이름 + " / " + road.이름);
            }
        }
        [Test] public void 모든건물의_접근로는_출입구와도로에_연결되고_다른건물을_가로지르지않는다()
        {
            foreach (var building in 업무배달동네배경Layout.건물)
            {
                var access = building.접근로영역; var door = building.출입구;
                Assert.That(door.x, Is.InRange(access.xMin, access.xMax), building.이름);
                Assert.That(door.y, Is.InRange(access.yMin, access.yMax), building.이름);
                Assert.That(업무배달동네배경Layout.도로.Any(x => 면적겹침(access, x.영역)), Is.True, building.이름);
                foreach (var other in 업무배달동네배경Layout.건물.Where(x => x.이름 != building.이름))
                    Assert.That(면적겹침(access, other.영역), Is.False, building.이름 + " / " + other.이름);
            }
        }
        [Test] public void 모든배치와접근로는_확장바닥안에_있다()
        {
            var bounds = 업무배달동네배경Layout.바닥Bounds;
            var rects = 업무배달동네배경Layout.도로.Select(x => x.영역)
                .Concat(업무배달동네배경Layout.건물.Select(x => x.영역))
                .Concat(업무배달동네배경Layout.건물.Select(x => x.접근로영역));
            foreach (var rect in rects)
            {
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(bounds.min.x)); Assert.That(rect.xMax, Is.LessThanOrEqualTo(bounds.max.x));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(bounds.min.z)); Assert.That(rect.yMax, Is.LessThanOrEqualTo(bounds.max.z));
            }
        }
        [TestCase(1920, 1080, false)]
        [TestCase(1920, 1080, true)]
        [TestCase(1280, 720, false)]
        [TestCase(1280, 720, true)]
        [TestCase(800, 1200, true)]
        [TestCase(2560, 1080, false)]
        public void 실제관찰영역_화면비에서_높이를포함한_모든모서리가_여백안에있다(int width, int height, bool phoneOpen)
        {
            var root = new GameObject("전체 동네 카메라 시험");
            try
            {
                var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 300;
                var fraction = phoneOpen ? Mathf.Clamp(1 - (412f / 720f) * height / width, .25f, 1) : 1;
                camera.aspect = width * fraction / height;
                var bounds = 업무배달동네배경Layout.관찰Bounds; var rotation = 동네전체관찰CameraMath.기본회전;
                camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 120, rotation);
                camera.orthographicSize = 동네전체관찰CameraMath.맞춤크기(bounds, rotation, camera.aspect);
                foreach (var corner in 동네전체관찰CameraMath.모서리(bounds))
                {
                    var point = camera.WorldToViewportPoint(corner);
                    Assert.That(point.x, Is.InRange(.0369f, .9631f)); Assert.That(point.y, Is.InRange(.0369f, .9631f));
                    Assert.That(point.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void 유효하지않은화면비는_조용히_작은맵으로_대체하지않는다(float aspect)
            => Assert.Throws<ArgumentOutOfRangeException>(() => 동네전체관찰CameraMath.맞춤크기(업무배달동네배경Layout.관찰Bounds, Quaternion.identity, aspect));

        [Test] public void 정적배경조립은_서버주체나활성Collider를_추가하지않는다()
        {
            var previous = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root = new GameObject("동네 배경 조립 시험"); root.SetActive(false);
            try
            {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "6");
                var controller = root.AddComponent<가상배달관찰Controller>();
                controller.GetType().GetMethod("업무배달동네배경조립", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.That(root.GetComponentsInChildren<업무배달관찰표식View>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true).All(x => !x.enabled), Is.True);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(x => x.GetComponent<TextMesh>() == null)
                    .All(x => x.sharedMaterial.shader.name == "Ssalddel/ObservationPrimitive"), Is.True);
                var floor = root.transform.Find("업무 서버 관찰 바닥");
                Assert.That(floor.localScale, Is.EqualTo(업무배달동네배경Layout.바닥Bounds.size));
                foreach (var building in 업무배달동네배경Layout.건물)
                {
                    var view = root.transform.Find(building.이름); Assert.That(view, Is.Not.Null, building.이름);
                    Assert.That(view.position.x, Is.EqualTo(building.중심.x)); Assert.That(view.position.z, Is.EqualTo(building.중심.y));
                    var label = view.GetComponentInChildren<TextMesh>();
                    Assert.That(label.characterSize, Is.EqualTo(.36f));
                    Assert.That(label.GetComponent<Renderer>().bounds.min.y, Is.GreaterThan(building.창고형 ? 1f : 4f));
                    Assert.That(label.GetComponent<Renderer>().bounds.max.y, Is.LessThanOrEqualTo(업무배달동네배경Layout.관찰Bounds.max.y));
                    var access = root.transform.Find(building.이름 + " 접근로");
                    Assert.That(access.position.x, Is.EqualTo(building.접근로영역.center.x));
                    Assert.That(access.position.z, Is.EqualTo(building.접근로영역.center.y));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", previous); }
        }
        private static bool 면적겹침(Rect a, Rect b)
            => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > .001f
                && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > .001f;
    }
}
