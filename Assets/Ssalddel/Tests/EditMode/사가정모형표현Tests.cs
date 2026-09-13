using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정모형표현Tests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(MapAsset.text);

        [TestCase(false)]
        [TestCase(true)]
        public void 어느윤곽방향에서도_외벽높이를보존하고_창문Geometry를늘리지않는다(bool reverse)
        {
            var points = new[]
            {
                new 참고점 { x = 0, z = 0 }, new 참고점 { x = 12, z = 0 },
                new 참고점 { x = 12, z = 8 }, new 참고점 { x = 0, z = 8 },
                new 참고점 { x = 0, z = 0 },
            };
            if (reverse) Array.Reverse(points);
            var data = new 공간참고자료
            {
                halfExtent = 500,
                roads = Array.Empty<참고도로>(),
                buildings = new[] { new 참고건물 { id = "test:footprint", height = 19.1f, points = points } },
            };
            var before = JsonUtility.ToJson(data);
            var batches = new List<사가정모형MeshBuilder.MeshBatch>();
            var report = 사가정모형MeshBuilder.BuildBatches(data, null, batches.Add, false);
            var wallVertices = batches.SelectMany(batch => batch.Vertices.Select((value, index) =>
                    new { value, surface = batch.SurfaceData[index].x }))
                .Where(value => Mathf.Approximately(value.surface, 사가정모형MeshBuilder.WallSurfaceCode))
                .Select(value => value.value).ToArray();

            Assert.That(wallVertices, Is.Not.Empty);
            Assert.That(wallVertices.Max(value => value.y) - wallVertices.Min(value => value.y),
                Is.EqualTo(19.1f).Within(.001f));
            Assert.That(report.WindowGeometryVertexCount, Is.Zero);
            Assert.That(batches.Any(batch => batch.Name.Contains("창문", StringComparison.Ordinal)), Is.False);
            Assert.That(batches.SelectMany(batch => batch.Triangles)
                .All(index => index >= 0), Is.True);
            foreach (var batch in batches)
            {
                Assert.That(batch.Triangles.All(index => index < batch.Vertices.Count), Is.True);
                Assert.That(batch.Vertices.All(value => float.IsFinite(value.x)
                    && float.IsFinite(value.y) && float.IsFinite(value.z)), Is.True);
            }
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [Test]
        public void 실제자료는_결정적4x4Chunk와_도로접합_그림자예산을지킨다()
        {
            var data = Map;
            var before = JsonUtility.ToJson(data);
            var first = new List<사가정모형MeshBuilder.MeshBatch>();
            var second = new List<사가정모형MeshBuilder.MeshBatch>();
            var report = 사가정모형MeshBuilder.BuildBatches(data, null, first.Add);
            사가정모형MeshBuilder.BuildBatches(data, null, second.Add);

            Assert.That(report.ChunkCount, Is.EqualTo(16));
            Assert.That(first.Where(batch => !batch.IsStationDetail)
                .Select(batch => (batch.ChunkX, batch.ChunkZ)).Distinct().Count(), Is.EqualTo(16));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(사가정모형MeshBuilder.MaximumRendererCount));
            Assert.That(report.MaterialSlotCount,
                Is.LessThanOrEqualTo(사가정모형MeshBuilder.MaximumMaterialCount));
            Assert.That(report.RoadJoinCount, Is.GreaterThan(report.IntersectionPadCount));
            Assert.That(report.IntersectionPadCount, Is.GreaterThan(0));
            Assert.That(report.ContinuousMarkingJoinCount, Is.GreaterThan(0));
            Assert.That(report.HeightMassingBandCount, Is.EqualTo(3));
            Assert.That(report.UsageMassingKindCount, Is.EqualTo(4));
            Assert.That(report.StationDetailBuildingCount, Is.GreaterThan(0));
            Assert.That(report.StationDetailBatchCount, Is.InRange(1, 4));
            Assert.That(report.DecorativeTreeCount, Is.GreaterThan(0));
            Assert.That(report.LongestAnalyticShadow,
                Is.EqualTo(사가정모형MeshBuilder.MaximumShadowLength).Within(.001f));
            Assert.That(report.WindowGeometryVertexCount, Is.Zero);

            var shadowVertices = first.Where(batch => batch.IsShadow)
                .SelectMany(batch => batch.Vertices).ToArray();
            Assert.That(shadowVertices, Is.Not.Empty);
            Assert.That(shadowVertices.All(value =>
                value.x >= data.offsetX - data.halfExtent - .001f
                && value.x <= data.offsetX + data.halfExtent + .001f
                && value.z >= data.offsetZ - data.halfExtent - .001f
                && value.z <= data.offsetZ + data.halfExtent + .001f), Is.True);
            var shadowAlphas = first.Where(batch => batch.IsShadow)
                .SelectMany(batch => batch.Colors).Select(value => value.a).Distinct().ToArray();
            Assert.That(shadowAlphas.Any(value => Mathf.Approximately(value,
                사가정모형MeshBuilder.UmbraAlpha)), Is.True);
            Assert.That(shadowAlphas.Any(value => Mathf.Approximately(value,
                사가정모형MeshBuilder.PenumbraAlpha)), Is.True);

            Assert.That(first.SelectMany(batch => batch.Vertices),
                Is.EqualTo(second.SelectMany(batch => batch.Vertices)));
            Assert.That(first.SelectMany(batch => batch.Colors),
                Is.EqualTo(second.SelectMany(batch => batch.Colors)));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
        }

        [Test]
        public void 기본View는_전용URP색보정과_구도_표현예산만소유한다()
        {
            var root = new GameObject("sagajeong-miniature-view-test");
            try
            {
                var view = root.AddComponent<사가정운영디오라마View>();
                view.Initialize();
                var data = Map;

                Assert.That(view.UsesSyntyPresentation, Is.False);
                Assert.That(view.SyntyPlacementCount, Is.Zero);
                Assert.That(view.VisualTargetRevision, Is.EqualTo(사가정모형MeshBuilder.Revision));
                Assert.That(view.MiniatureBuildReport, Is.Not.Null);
                Assert.That(view.SpatialPresentationRevision, Is.Not.EqualTo("NotLoaded"));
                Assert.That(view.SpatialPresentationStatus, Does.Contain("Ready")
                    .Or.Contain("Fallback"));
                Assert.That(view.SpatialPresentationAttribution, Does.Contain("OpenStreetMap"));
                Assert.That(view.MiniatureBuildReport.OverlayApplied,
                    Is.EqualTo(view.SpatialOverlayApplied));
                Assert.That(view.MiniatureBuildReport.SourceRevision,
                    Is.EqualTo(view.SpatialPresentationRevision));
                Assert.That(view.MiniatureBuildReport.SourceContentHash,
                    Is.EqualTo(view.SpatialPresentationContentHash));
                if (view.SpatialOverlayApplied)
                {
                    Assert.That(view.MiniatureBuildReport.OverlayBuildingCount, Is.GreaterThan(0));
                    Assert.That(view.MiniatureBuildReport.OverlaySurfaceCount, Is.GreaterThan(0));
                }
                Assert.That(view.MiniatureBuildReport.RendererBatchCount,
                    Is.LessThanOrEqualTo(사가정모형MeshBuilder.MaximumRendererCount));
                var chunks = root.GetComponentsInChildren<Transform>(true)
                    .Single(value => value.name == "사가정 4x4 표현 chunks");
                Assert.That(chunks.childCount, Is.EqualTo(16));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.LessThanOrEqualTo(사가정모형MeshBuilder.MaximumRendererCount));
                Assert.That(renderers.Select(value => value.sharedMaterial).Where(value => value != null)
                    .Distinct().Count(), Is.LessThanOrEqualTo(사가정모형MeshBuilder.MaximumMaterialCount));
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true)
                    .Any(value => value.name.Contains("창문", StringComparison.Ordinal)), Is.False);

                Assert.That(view.PresentationVolume, Is.Not.Null);
                Assert.That(view.PresentationVolume.gameObject.layer, Is.EqualTo(사가정운영디오라마View.ObservationLayer));
                Assert.That(view.PresentationVolume.isGlobal, Is.True);
                var effects = view.PresentationVolume.sharedProfile.components;
                Assert.That(effects.Select(value => value.GetType().Name),
                    Does.Contain("Tonemapping").And.Contain("ColorAdjustments").And.Contain("Vignette"));
                Assert.That(effects.Any(value => value.GetType().Name == "DepthOfField"), Is.False);
                Assert.That(ReadParameter(effects, "ColorAdjustments", "postExposure"),
                    Is.EqualTo(사가정운영디오라마View.PostExposure).Within(.001f));
                Assert.That(ReadParameter(effects, "ColorAdjustments", "contrast"),
                    Is.EqualTo(사가정운영디오라마View.PostContrast).Within(.001f));
                Assert.That(ReadParameter(effects, "ColorAdjustments", "saturation"),
                    Is.EqualTo(사가정운영디오라마View.PostSaturation).Within(.001f));
                Assert.That(ReadParameter(effects, "Vignette", "intensity"),
                    Is.EqualTo(사가정운영디오라마View.PostVignette).Within(.001f));
                Assert.That(view.ObservationCamera.GetComponent("UniversalAdditionalCameraData"), Is.Not.Null);

                view.Show(false);
                Assert.That(view.ObservationCamera.enabled, Is.False);
                Assert.That(view.PresentationVolume.gameObject.activeInHierarchy, Is.False);
                Assert.That(renderers.All(value => !value.gameObject.activeInHierarchy), Is.True);
                view.Show(true);
                Assert.That(view.ObservationCamera.enabled, Is.True);
                Assert.That(view.PresentationVolume.gameObject.activeInHierarchy, Is.True);

                view.ObservationCamera.aspect = 16f / 9f;
                view.ShowWhole();
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.ObservationCamera.transform.eulerAngles.x,
                    사가정운영디오라마View.WholeViewPitch)), Is.LessThan(.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.ObservationCamera.transform.eulerAngles.y,
                    사가정운영디오라마View.WholeViewYaw)), Is.LessThan(.01f));
                var presentationBounds = new Bounds(
                    root.transform.TransformPoint(new Vector3(data.offsetX, 35f, data.offsetZ)),
                    new Vector3(data.halfExtent * 2f, 130f, data.halfExtent * 2f));
                var projectedSize = ProjectedBoundsSize(view.ObservationCamera, presentationBounds);
                Assert.That(projectedSize.x, Is.LessThanOrEqualTo(.96f));
                Assert.That(projectedSize.y, Is.LessThanOrEqualTo(.96f));
                Assert.That(Mathf.Max(projectedSize.x, projectedSize.y), Is.InRange(.92f, .96f));
                Assert.That(view.StationDetailsVisible, Is.False);
                view.FocusPoint(root.transform.TransformPoint(new Vector3(data.offsetX, 0f, data.offsetZ)));
                Assert.That(view.StationDetailsVisible, Is.True);
                view.ShowWhole();
                Assert.That(view.StationDetailsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static float ReadParameter(IEnumerable<UnityEngine.Rendering.VolumeComponent> effects,
            string componentName, string fieldName)
        {
            var component = effects.Single(value => value.GetType().Name == componentName);
            var field = component.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var parameter = field.GetValue(component);
            var property = parameter.GetType().GetProperty("value",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return Convert.ToSingle(property.GetValue(parameter));
        }

        private static Vector2 ProjectedBoundsSize(Camera camera, Bounds bounds)
        {
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                {
                    var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    var point = camera.WorldToViewportPoint(world);
                    minimum.x = Mathf.Min(minimum.x, point.x);
                    minimum.y = Mathf.Min(minimum.y, point.y);
                    maximum.x = Mathf.Max(maximum.x, point.x);
                    maximum.y = Mathf.Max(maximum.y, point.y);
                }
            return maximum - minimum;
        }
    }
}
