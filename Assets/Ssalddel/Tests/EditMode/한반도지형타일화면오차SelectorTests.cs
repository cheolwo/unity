using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 한반도지형타일화면오차SelectorTests
    {
        private const int ViewportHeight = 1080;
        private const double FieldOfView = 38d;
        private const double CameraDistance = 33d;
        private const double GlobeRadius = 18d;
        private const double RepresentativeLatitude = 38.5d;

        [TestCase(16d, 0, 1, 10.427d)]
        [TestCase(8d, 1, 4, 5.214d)]
        [TestCase(4d, 2, 16, 3.910d)]
        public void 같은1080p카메라에서_목표화면오차에따라상세도를선택한다(
            double targetSse, int expectedLevel, int expectedTiles, double expectedSse)
        {
            var result = 한반도지형타일화면오차Selector.Select(
                targetSse, ViewportHeight, FieldOfView, CameraDistance,
                GlobeRadius, RepresentativeLatitude);

            Assert.That(result.DetailLevel, Is.EqualTo(expectedLevel));
            Assert.That(result.ActiveTileCount, Is.EqualTo(expectedTiles));
            Assert.That(result.TargetSsePixels, Is.EqualTo(targetSse));
            Assert.That(result.EstimatedSsePixels, Is.EqualTo(expectedSse).Within(.001d));
            Assert.That(result.DetailCapReached, Is.False);
        }

        [Test]
        public void 최소카메라거리의4픽셀목표는_현재Mesh상한을명시한다()
        {
            var result = 한반도지형타일화면오차Selector.Select(
                4d, ViewportHeight, FieldOfView, 32d,
                GlobeRadius, RepresentativeLatitude);

            Assert.That(result.DetailLevel, Is.EqualTo(2));
            Assert.That(result.ActiveTileCount, Is.EqualTo(16));
            Assert.That(result.EstimatedSsePixels, Is.EqualTo(4.189d).Within(.001d));
            Assert.That(result.DetailCapReached, Is.True);
        }

        [Test]
        public void 잘못된화면오차입력은_비교전에거부한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                한반도지형타일화면오차Selector.Select(
                    0d, ViewportHeight, FieldOfView, CameraDistance,
                    GlobeRadius, RepresentativeLatitude));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                한반도지형타일화면오차Selector.Select(
                    8d, 0, FieldOfView, CameraDistance,
                    GlobeRadius, RepresentativeLatitude));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                한반도지형타일화면오차Selector.Select(
                    8d, ViewportHeight, 180d, CameraDistance,
                    GlobeRadius, RepresentativeLatitude));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                한반도지형타일화면오차Selector.Select(
                    8d, ViewportHeight, FieldOfView, GlobeRadius,
                    GlobeRadius, RepresentativeLatitude));
        }

        [Test]
        public void 비교상세도는_의미자료층과독립되고_해제하면기존단계로복귀한다()
        {
            var root = new GameObject("PeninsulaTerrainSseComparisonTestRoot");
            try
            {
                var globeView = root.AddComponent<세계지구본View>();
                globeView.InitializeForTests();
                globeView.SelectCountryAtForTests(37.5665d, 126.9780d);
                globeView.SetCameraDistanceForTests(33f);
                Assert.That(globeView.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(globeView.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Hubs));

                var terrainView = root.GetComponent<한반도지형미리보기View>();
                Assert.That(terrainView, Is.Not.Null);
                Assert.That(terrainView!.IsRiverVisible, Is.False);

                var loose = globeView.ApplyTerrainSseComparisonForTests(16f, ViewportHeight);
                Assert.That(loose.DetailLevel, Is.EqualTo(0));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);
                var looseVertices = terrainView.ActiveSurfaceVertexCount;
                var looseTriangles = terrainView.ActiveSurfaceTriangleCount;

                var balanced = globeView.ApplyTerrainSseComparisonForTests(8f, ViewportHeight);
                Assert.That(balanced.DetailLevel, Is.EqualTo(1));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.ActiveSurfaceVertexCount, Is.EqualTo(0));
                Assert.That(terrainView.ActiveSurfaceTriangleCount,
                    Is.EqualTo(0));
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);

                var strict = globeView.ApplyTerrainSseComparisonForTests(4f, ViewportHeight);
                Assert.That(strict.DetailLevel, Is.EqualTo(2));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.ActiveSurfaceVertexCount,
                    Is.EqualTo(0));
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);
                Assert.That(terrainView.IsRiverVisible, Is.False,
                    "SSE 비교가 제외된 사진형 하천 자료층을 다시 켜면 안 된다.");

                globeView.ClearTerrainSseComparisonForTests();
                Assert.That(terrainView.IsSurfaceDetailComparisonActive, Is.False);
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }
    }
}
