using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 행정동디오라마검토Tests
    {
        [Test]
        public void 수용된ApplyResult는_중심정규화된_읽기전용깊은사본이된다()
        {
            var source = Fixture();

            Assert.That(행정동디오라마검토Projection.TryCreate(source,
                out var snapshot, out var diagnostic), Is.True);

            Assert.That(diagnostic, Is.EqualTo(
                행정동디오라마검토Projection.AcceptedDiagnostic));
            Assert.That(snapshot.AdministrativeAreaStableId,
                Is.EqualTo("region:kr:hjd:1126057500"));
            Assert.That(snapshot.Bounds.MinX, Is.EqualTo(-50d));
            Assert.That(snapshot.Bounds.MaxX, Is.EqualTo(50d));
            Assert.That(snapshot.Boundary.Count, Is.EqualTo(4));
            Assert.That(snapshot.Buildings.Count, Is.EqualTo(1));
            Assert.That(snapshot.Roads.Count, Is.EqualTo(1));
            Assert.That(snapshot.ObservationPresentationOnly, Is.True);
            Assert.That(snapshot.DistributionApproved, Is.False);
            Assert.That(snapshot.TraversalReady, Is.False);
            Assert.That(snapshot.GameplayReady, Is.False);

            source.Manifest.DisplayName = "변조된 이름";
            source.Manifest.Boundary[0].X = 999d;
            source.Tiles[0].Buildings[0].Footprint[0].X = 999d;
            Assert.That(snapshot.DisplayName, Is.EqualTo("면목제3.8동"));
            Assert.That(snapshot.Boundary[0].X, Is.EqualTo(-50d));
            Assert.That(snapshot.Buildings[0].Footprint[0].X, Is.EqualTo(-40d));
        }

        [Test]
        public void 권위나배포준비가있는자료는_검토사본에서거절한다()
        {
            AssertRejected(value => value.Manifest.ObservationPresentationOnly = false,
                "AdministrativeDongReviewAuthorityRejected");
            AssertRejected(value => value.Manifest.DistributionApproved = true,
                "AdministrativeDongReviewDistributionApprovedRejected");
            AssertRejected(value => value.Manifest.TraversalReady = true,
                "AdministrativeDongReviewTraversalReadyRejected");
            AssertRejected(value => value.Manifest.GameplayReady = true,
                "AdministrativeDongReviewGameplayReadyRejected");
            AssertRejected(value => value.Manifest.ReadinessCode =
                    AdministrativeDongDioramaReadinessCodes.Ready,
                "AdministrativeDongReviewReadinessRejected");
        }

        [Test]
        public void geometryOnly검토는_표시오버레이가섞이면거절한다()
        {
            var source = Fixture();
            source.DisplayOverlays = new[]
            {
                new AdministrativeDongDisplayOverlay
                {
                    OverlayStableId = "overlay:test",
                    OverlayKindCode = AdministrativeDongDisplayOverlayKinds.PublicBusiness,
                    SemanticPlaceStableId = "place:test"
                }
            };

            Assert.That(행정동디오라마검토Projection.TryCreate(source,
                out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic,
                Is.EqualTo("AdministrativeDongReviewGeometryOnlyOverlayRejected"));
        }

        [Test]
        public void 타일hash나건수선언이다르면_표현전에거절한다()
        {
            var hashMismatch = Fixture();
            hashMismatch.Tiles[0].TileHashSha256 = new string('C', 64);
            Assert.That(행정동디오라마검토Projection.TryCreate(hashMismatch,
                out _, out var hashDiagnostic), Is.False);
            Assert.That(hashDiagnostic,
                Is.EqualTo("AdministrativeDongReviewTileHashOrCountInvalid"));

            var countMismatch = Fixture();
            countMismatch.Manifest.Tiles[0].BuildingCount = 2;
            Assert.That(행정동디오라마검토Projection.TryCreate(countMismatch,
                out _, out var countDiagnostic), Is.False);
            Assert.That(countDiagnostic,
                Is.EqualTo("AdministrativeDongReviewTileHashOrCountInvalid"));
        }

        [Test]
        public void 도형Builder는_최대네개의결합Mesh와_동적Bounds를만든다()
        {
            var snapshot = 행정동디오라마검토Projection.Create(Fixture());
            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();

            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);

            Assert.That(batches.Count, Is.EqualTo(4));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumRendererCount));
            Assert.That(report.BuildingCount, Is.EqualTo(1));
            Assert.That(report.RoadSegmentCount, Is.EqualTo(1));
            Assert.That(report.BoundarySegmentCount, Is.EqualTo(4));
            Assert.That(report.ModelBounds.size.x, Is.GreaterThan(100f));
            Assert.That(report.ModelBounds.size.z, Is.GreaterThan(100f));
            Assert.That(report.TotalVertexCount, Is.EqualTo(
                batches.Sum(value => value.Vertices.Count)));
            Assert.That(batches.All(value => value.Vertices.Count == value.Colors.Count),
                Is.True);
            Assert.That(batches.All(value => value.Vertices.Count
                <= 행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch), Is.True);
        }

        [Test]
        public void 알려진최대동규모의_4253개건물도_네Mesh예산안에묶는다()
        {
            const int buildingCount = 4_253;
            var source = Fixture(extent: 3000d);
            source.Tiles[0].Buildings = Enumerable.Range(0, buildingCount)
                .Select(index =>
                {
                    var x = 5d + index % 66 * 40d;
                    var z = 5d + index / 66 * 40d;
                    return new AdministrativeDongDioramaBuilding
                    {
                        BuildingStableId = $"building:dense:{index:D4}",
                        CategoryCode = index % 3 == 0 ? "commercial" : "residential",
                        EvidenceKindCode = "FixtureGeometry",
                        HeightMeters = 8d + index % 10,
                        Footprint = new[]
                        {
                            Point(x, z), Point(x + 10d, z), Point(x + 10d, z + 10d),
                            Point(x, z + 10d), Point(x, z)
                        }
                    };
                }).ToArray();
            source.Manifest.Tiles[0].BuildingCount = buildingCount;
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();

            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);

            Assert.That(report.BuildingCount, Is.EqualTo(buildingCount));
            Assert.That(report.RendererBatchCount, Is.EqualTo(4));
            Assert.That(report.MaximumBatchVertexCount,
                Is.LessThan(행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch));
        }

        [Test]
        public void View는_동하나만교체표시하고_선택과정리에Collider를만들지않는다()
        {
            var root = new GameObject("administrative-dong-review-test");
            try
            {
                root.transform.position = new Vector3(3000f, 0f, 0f);
                var view = root.AddComponent<행정동디오라마검토View>();

                Assert.That(view.TryApply(Fixture(), true, out var firstDiagnostic), Is.True);
                Assert.That(firstDiagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.AcceptedDiagnostic));
                var firstRoot = view.ModelRoot;
                var firstSize = view.ObservationCamera.orthographicSize;
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));

                Assert.That(view.TrySelectLocalPoint(new Vector2(-30f, -30f)), Is.True);
                Assert.That(view.SelectedBuilding.BuildingStableId,
                    Is.EqualTo("building:first"));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(5));

                Assert.That(view.TryApply(Fixture("region:kr:hjd:1126057600",
                    "building:second", 400d), true, out _), Is.True);
                Assert.That(firstRoot == null, Is.True);
                Assert.That(view.ActiveAdministrativeAreaStableId,
                    Is.EqualTo("region:kr:hjd:1126057600"));
                Assert.That(view.HasSelection, Is.False);
                Assert.That(view.ObservationCamera.orthographicSize, Is.GreaterThan(firstSize));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

                view.Clear();
                Assert.That(view.Snapshot, Is.Null);
                Assert.That(view.ModelRoot, Is.Null);
                Assert.That(view.ObservationCamera, Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 화면Ray선택은_변환된Root에서도_건물기둥만고른다()
        {
            var root = new GameObject("administrative-dong-review-ray-test");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(50f, 3f, -20f),
                    Quaternion.Euler(0f, 18f, 0f));
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), false, out _), Is.True);
                var localTop = new Vector3(-30f, 40f, -30f);
                var ray = new Ray(view.ModelRoot.TransformPoint(localTop),
                    view.ModelRoot.TransformDirection(Vector3.down));

                Assert.That(view.TrySelectWorldRay(ray), Is.True);
                Assert.That(view.SelectedBuilding.BuildingStableId,
                    Is.EqualTo("building:first"));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 거절된새자료는_이전동을남기지않는다()
        {
            var root = new GameObject("administrative-dong-review-reject-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), false, out _), Is.True);
                var rejected = Fixture("region:kr:hjd:1126057600",
                    "building:second", 100d);
                rejected.Manifest.DistributionApproved = true;

                Assert.That(view.TryApply(rejected, false, out var diagnostic), Is.False);
                Assert.That(diagnostic,
                    Is.EqualTo("AdministrativeDongReviewDistributionApprovedRejected"));
                Assert.That(view.Snapshot, Is.Null);
                Assert.That(view.ModelRoot, Is.Null);
                Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertRejected(Action<AdministrativeDongDioramaApplyResult> change,
            string expectedDiagnostic)
        {
            var source = Fixture();
            change(source);
            Assert.That(행정동디오라마검토Projection.TryCreate(source,
                out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expectedDiagnostic));
        }

        private static AdministrativeDongDioramaApplyResult Fixture(
            string administrativeAreaStableId = "region:kr:hjd:1126057500",
            string buildingStableId = "building:first", double extent = 100d)
        {
            const string projectionHash =
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string tileHash =
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            var tileStableId = $"tile:{administrativeAreaStableId}:0:0";
            var buildingMin = extent * .1d;
            var buildingMax = extent * .3d;
            return new AdministrativeDongDioramaApplyResult
            {
                Accepted = true,
                Manifest = new AdministrativeDongDioramaManifest
                {
                    SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                    AdministrativeAreaStableId = administrativeAreaStableId,
                    DisplayName = "면목제3.8동",
                    SourceVintage = "fixture.r1",
                    ProjectionHashSha256 = projectionHash,
                    ReadinessCode = AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                    DataPolicyCode = AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                    ObservationPresentationOnly = true,
                    TraversalReady = false,
                    GameplayReady = false,
                    DistributionApproved = false,
                    CoordinateFrame = new AdministrativeDongDioramaCoordinateFrame
                    {
                        Method = "WGS84-ECEF-ENU-at-zero-altitude",
                        WorldOffsetX = 0d,
                        WorldOffsetZ = 0d,
                        MetersPerUnit = 1d
                    },
                    Bounds = new AdministrativeDongDioramaBounds
                    {
                        MinX = 0d,
                        MinZ = 0d,
                        MaxX = extent,
                        MaxZ = extent
                    },
                    Boundary = new[]
                    {
                        Point(0d, 0d), Point(extent, 0d), Point(extent, extent),
                        Point(0d, extent), Point(0d, 0d)
                    },
                    Tiles = new[]
                    {
                        new AdministrativeDongDioramaTileSummary
                        {
                            TileStableId = tileStableId,
                            TileIndexX = 0,
                            TileIndexZ = 0,
                            TileHashSha256 = tileHash,
                            BuildingCount = 1,
                            RoadSegmentCount = 1
                        }
                    }
                },
                Tiles = new[]
                {
                    new AdministrativeDongDioramaTile
                    {
                        SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                        AdministrativeAreaStableId = administrativeAreaStableId,
                        ProjectionHashSha256 = projectionHash,
                        TileStableId = tileStableId,
                        TileHashSha256 = tileHash,
                        TileIndexX = 0,
                        TileIndexZ = 0,
                        Bounds = new AdministrativeDongDioramaBounds
                        {
                            MinX = 0d, MinZ = 0d, MaxX = extent, MaxZ = extent
                        },
                        Buildings = new[]
                        {
                            new AdministrativeDongDioramaBuilding
                            {
                                BuildingStableId = buildingStableId,
                                CategoryCode = "residential",
                                EvidenceKindCode = "FixtureGeometry",
                                HeightMeters = 18d,
                                AboveGroundFloorCount = 6,
                                Footprint = new[]
                                {
                                    Point(buildingMin, buildingMin),
                                    Point(buildingMax, buildingMin),
                                    Point(buildingMax, buildingMax),
                                    Point(buildingMin, buildingMax),
                                    Point(buildingMin, buildingMin)
                                }
                            }
                        },
                        Roads = new[]
                        {
                            new AdministrativeDongDioramaRoadSegment
                            {
                                RoadStableId = "road:centerline",
                                EvidenceKindCode = "NodeLinkCenterlineOnly",
                                From = Point(0d, extent * .5d),
                                To = Point(extent, extent * .5d)
                            }
                        }
                    }
                },
                DisplayOverlays = Array.Empty<AdministrativeDongDisplayOverlay>()
            };
        }

        private static AdministrativeDongDioramaPoint Point(double x, double z)
            => new() { X = x, Z = z };
    }
}
