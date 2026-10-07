using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
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
        public void RoadDirectionAudit경로는_v4와v5에서각각고정되고_hash는정확히비교한다()
        {
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v4", "audit.json"), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v4", "road-direction-audit.json"), Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v5", "road-direction-audit.json"), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v5", "audit.json"), Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v5", "../audit.json"), Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v6",
                "road-direction-audit.json"), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v6", "audit.json"), Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v7",
                "road-direction-audit.json"), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.IsRoadDirectionAuditPathAllowed(
                "administrative-dong-diorama-unity-review-index.v7", "audit.json"), Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditFileHashMatches(new string('A',64),new string('a',64)),Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditFileHashMatches(new string('A',64),new string('B',64)),Is.False);
        }

        [Test]
        public void CompletionAuditDescriptor는_v5의두audit를각각정확히한번포함해야한다()
        {
            var expected=new[]{"audit.json","road-direction-audit.json"};
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(expected,expected),Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                new[]{"audit.json","road-direction-audit.json","road-direction-audit.json"},expected),Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                new[]{"audit.json"},expected),Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                new[]{"audit.json","wrong-audit.json"},expected),Is.False);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                new[]{"audit.json"},new[]{"audit.json"}),Is.True,"v4 audit-only descriptor remains valid");
            var laneExpected = new[]
                { "biotope-audit.json", "road-direction-audit.json", "lane-marking-audit.json" };
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                laneExpected, laneExpected), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                new[] { "biotope-audit.json", "lane-marking-audit.json",
                    "lane-marking-audit.json" }, laneExpected), Is.False);
            var contourExpected = new[]
            {
                "biotope-audit.json", "road-direction-audit.json",
                "lane-marking-audit.json", "horizontal-contour-trace-audit.json"
            };
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                contourExpected, contourExpected), Is.True);
            Assert.That(AdministrativeDongDioramaReviewCliCapture.AuditDescriptorPathsMatch(
                contourExpected.Take(3), contourExpected), Is.False);
        }

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
            Assert.That(snapshot.PrivateObservationOverlay, Is.Null);
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
        public void 비공개후보Overlay는_공통EnuMillimeter를로컬좌표로깊은복사한다()
        {
            var source = OverlayFixture();

            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), source);
            var overlay = snapshot.PrivateObservationOverlay;

            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.SchemaVersion,
                Is.EqualTo(행정동디오라마비공개관찰Policy.SchemaVersion));
            Assert.That(overlay.AdministrativeAreaStableId,
                Is.EqualTo(snapshot.AdministrativeAreaStableId));
            Assert.That(overlay.CrosswalkPoints, Has.Count.EqualTo(1));
            Assert.That(overlay.CrosswalkPoints[0].X, Is.EqualTo(-25d));
            Assert.That(overlay.CrosswalkPoints[0].Z, Is.EqualTo(25d));
            Assert.That(overlay.CrosswalkPoints[0].PedestrianSignalPresent, Is.True);
            Assert.That(overlay.IntersectionPoints, Has.Count.EqualTo(1));
            Assert.That(overlay.IntersectionPoints[0].X, Is.EqualTo(30d));
            Assert.That(overlay.IntersectionPoints[0].Z, Is.EqualTo(-30d));
            Assert.That(overlay.IntersectionPoints[0].LinkedCrosswalkCount, Is.EqualTo(3));
            Assert.That(overlay.PublicDisplayAllowed, Is.False);
            Assert.That(overlay.RuntimeAuthorized, Is.False);
            Assert.That(overlay.TraversalReady, Is.False);
            Assert.That(snapshot.DepthPlan.Layers.Single(value =>
                    value.LayerKindCode == "Mobility").StateCode,
                Is.EqualTo(행정동디오라마깊이StateCodes.Blocked));

            var crosswalkStableId = overlay.CrosswalkPoints[0].CandidateStableId;
            source.overlaySetHashSha256 = new string('F', 64);
            source.crosswalkPoints[0].candidateStableId = "modified:" + new string('1', 64);
            source.crosswalkPoints[0].commonEnuMillimeters.x = 99_000d;
            source.intersectionPoints[0].linkedCrosswalkCount = 99;
            source.crosswalkPoints = Array.Empty<행정동디오라마횡단보도점후보Data>();
            Assert.That(overlay.OverlaySetHashSha256, Is.EqualTo(new string('E', 64)));
            Assert.That(overlay.CrosswalkPoints[0].CandidateStableId,
                Is.EqualTo(crosswalkStableId));
            Assert.That(overlay.CrosswalkPoints[0].X, Is.EqualTo(-25d));
            Assert.That(overlay.IntersectionPoints[0].LinkedCrosswalkCount, Is.EqualTo(3));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토CrosswalkPoint>)overlay.CrosswalkPoints)[0] = null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토IntersectionPoint>)overlay.IntersectionPoints)[0] = null);
        }

        [Test]
        public void 비공개후보Overlay는_schema_area_hash와모든권위Flag를검증한다()
        {
            AssertOverlayRejected(value => value.schemaVersion = "overlay.v2",
                "AdministrativeDongPrivateObservationOverlaySchemaVersionInvalid");
            AssertOverlayRejected(value => value.administrativeAreaStableId =
                    "region:kr:hjd:1126057600",
                "AdministrativeDongPrivateObservationOverlayAdministrativeAreaMismatch");
            AssertOverlayRejected(value => value.overlaySetHashSha256 = "not-a-hash",
                "AdministrativeDongPrivateObservationOverlaySetHashInvalid");

            var authorityChanges = new Action<행정동디오라마비공개관찰OverlaySetData>[]
            {
                value => value.historicalBoundaryBootstrapOnly = false,
                value => value.privateReviewVisualizationAllowed = false,
                value => value.reviewVisualizationOnly = false,
                value => value.sourceCandidateUnityApplyAllowed = true,
                value => value.publicDisplayAllowed = true,
                value => value.runtimeAuthorized = true,
                value => value.traversalReady = true,
                value => value.gameplayReady = true,
                value => value.currentPointerUsed = true,
                value => value.currentPointerUpdated = true,
                value => value.exactAddressIncluded = true,
                value => value.businessIdentityIncluded = true,
                value => value.personalDataIncluded = true
            };
            foreach (var change in authorityChanges)
                AssertOverlayRejected(change,
                    "AdministrativeDongPrivateObservationOverlayAuthorityRejected");
        }

        [Test]
        public void 비공개후보Overlay는_중복과비유한좌표와범위밖점과음수연결수를거절한다()
        {
            AssertOverlayRejected(value => value.intersectionPoints[0].candidateStableId =
                    value.crosswalkPoints[0].candidateStableId.ToUpperInvariant(),
                "AdministrativeDongPrivateObservationOverlayPointDuplicate");
            AssertOverlayRejected(value => value.crosswalkPoints[0]
                    .commonEnuMillimeters.x = double.NaN,
                "AdministrativeDongPrivateObservationOverlayPointInvalid");
            AssertOverlayRejected(value => value.intersectionPoints[0]
                    .commonEnuMillimeters.z = double.PositiveInfinity,
                "AdministrativeDongPrivateObservationOverlayPointInvalid");
            AssertOverlayRejected(value => value.crosswalkPoints[0]
                    .commonEnuMillimeters.x = 101_000d,
                "AdministrativeDongPrivateObservationOverlayPointOutOfBounds");
            AssertOverlayRejected(value => value.intersectionPoints[0].linkedCrosswalkCount = -1,
                "AdministrativeDongPrivateObservationOverlayLinkedCountInvalid");
            AssertOverlayRejected(value => value.crosswalkPoints[0].candidateStableId =
                    "raw-management-number",
                "AdministrativeDongPrivateObservationOverlayPointIdentityInvalid");
            AssertOverlayRejected(value => value.intersectionPoints[0].qualityCode = " ",
                "AdministrativeDongPrivateObservationOverlayPointQualityInvalid");
        }

        [Test]
        public void 비공개보행망Overlay는_전체Path를로컬좌표로깊은복사하고이동권위를만들지않는다()
        {
            var source = WalkOverlayFixture();

            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                source);
            var overlay = snapshot.PrivateWalkNetworkOverlay;

            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.SchemaVersion,
                Is.EqualTo(행정동디오라마비공개보행망Policy.SchemaVersion));
            Assert.That(overlay.NodePoints, Has.Count.EqualTo(1));
            Assert.That(overlay.NodePoints[0].X, Is.EqualTo(-25d));
            Assert.That(overlay.NodePoints[0].Z, Is.EqualTo(25d));
            Assert.That(overlay.LinkFragments, Has.Count.EqualTo(1));
            Assert.That(overlay.LinkFragments[0].Path.Select(value => value.X),
                Is.EqualTo(new[] { -40d, 0d, 40d }));
            Assert.That(overlay.LinkFragments[0].Path.Select(value => value.Z),
                Is.EqualTo(new[] { -40d, 0d, -30d }));
            Assert.That(overlay.LinkPathVertexCount, Is.EqualTo(3));
            Assert.That(overlay.LinkFragments[0].ActorSummary.Pedestrian, Is.True);
            Assert.That(overlay.LinkFragments[0].ActorSummary.Vehicle, Is.False);
            Assert.That(overlay.PublicDisplayAllowed, Is.False);
            Assert.That(overlay.RuntimeAuthorized, Is.False);
            Assert.That(overlay.TraversalReady, Is.False);
            Assert.That(snapshot.TraversalReady, Is.False);
            Assert.That(snapshot.DepthPlan.Layers.Single(value =>
                    value.LayerKindCode == "Mobility").StateCode,
                Is.EqualTo(행정동디오라마깊이StateCodes.Blocked));

            var stableId = overlay.LinkFragments[0].CandidateStableId;
            source.overlaySetHashSha256 = new string('0', 64);
            source.linkFragments[0].candidateStableId = "modified:" + new string('1', 64);
            source.linkFragments[0].commonEnuMillimeterPath[0].x = 99_000d;
            source.linkFragments = Array.Empty<행정동디오라마보행망LinkFragment후보Data>();
            Assert.That(overlay.OverlaySetHashSha256, Is.EqualTo(new string('A', 64)));
            Assert.That(overlay.LinkFragments[0].CandidateStableId, Is.EqualTo(stableId));
            Assert.That(overlay.LinkFragments[0].Path[0].X, Is.EqualTo(-40d));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토WalkLinkFragment>)overlay.LinkFragments)[0] = null);
        }

        [Test]
        public void 비공개보행망Overlay는_schema_hash_stableId_권위와전체Path를검증한다()
        {
            AssertWalkOverlayRejected(value => value.schemaVersion = "walk.v2",
                "AdministrativeDongPrivateWalkNetworkOverlaySchemaVersionInvalid");
            AssertWalkOverlayRejected(value => value.overlaySetHashSha256 = "not-a-hash",
                "AdministrativeDongPrivateWalkNetworkOverlaySetHashInvalid");
            AssertWalkOverlayRejected(value => value.overlaySetStableId = "other",
                "AdministrativeDongPrivateWalkNetworkOverlayIdentityInvalid");
            AssertWalkOverlayRejected(value => value.nodePoints[0].candidateStableId =
                    value.linkFragments[0].candidateStableId.ToUpperInvariant(),
                "AdministrativeDongPrivateWalkNetworkOverlayCandidateDuplicate");
            AssertWalkOverlayRejected(value => value.nodePoints[0].candidateStableId = "raw-id",
                "AdministrativeDongPrivateWalkNetworkOverlayCandidateIdentityInvalid");
            AssertWalkOverlayRejected(value => value.nodePoints[0].kindCode = "LinkFragment",
                "AdministrativeDongPrivateWalkNetworkOverlayKindInvalid");
            AssertWalkOverlayRejected(value => value.nodePoints[0].actorSummaryFlags.pedestrian = true,
                "AdministrativeDongPrivateWalkNetworkOverlayNodeActorFlagsInvalid");
            AssertWalkOverlayRejected(value => value.linkFragments[0]
                    .commonEnuMillimeterPath = new[]
                    {
                        new 행정동디오라마공통EnuMillimetersData { x = 50_000d, z = 50_000d }
                    },
                "AdministrativeDongPrivateWalkNetworkOverlayLinkPathInvalid");
            AssertWalkOverlayRejected(value => value.linkFragments[0]
                    .commonEnuMillimeterPath[1].x = double.NaN,
                "AdministrativeDongPrivateWalkNetworkOverlayPointInvalid");
            AssertWalkOverlayRejected(value => value.linkFragments[0]
                    .commonEnuMillimeterPath[1].z = 101_000d,
                "AdministrativeDongPrivateWalkNetworkOverlayPointOutOfBounds");

            var authorityChanges = new Action<행정동디오라마비공개보행망OverlaySetData>[]
            {
                value => value.historicalBoundaryBootstrapOnly = false,
                value => value.privateReviewOnly = false,
                value => value.privateReviewVisualizationAllowed = false,
                value => value.reviewVisualizationOnly = false,
                value => value.observationCandidateOnly = false,
                value => value.sourceCandidateUnityApplyAllowed = true,
                value => value.publicDisplayAllowed = true,
                value => value.runtimeAuthorized = true,
                value => value.traversalReady = true,
                value => value.gameplayReady = true,
                value => value.currentPointerUsed = true,
                value => value.currentPointerUpdated = true,
                value => value.sourceIdentifiersIncluded = true,
                value => value.sourceBoroughIdentityIncluded = true,
                value => value.legalAreaIdentityIncluded = true,
                value => value.sourceTimestampsIncluded = true,
                value => value.sourceEpsg5186CoordinatesIncluded = true,
                value => value.exactAddressIncluded = true,
                value => value.personalDataIncluded = true
            };
            foreach (var change in authorityChanges)
                AssertWalkOverlayRejected(change,
                    "AdministrativeDongPrivateWalkNetworkOverlayAuthorityRejected");
        }

        [Test]
        public void 비공개도로방향후보는_ENU좌표만로컬복사하고_방향을해석하지않는다()
        {
            var source = RoadDirectionOverlayFixture();
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), source);
            var overlay = snapshot.PrivateRoadDirectionOverlay;

            Assert.That(overlay.CandidatePoints, Has.Count.EqualTo(1));
            Assert.That(overlay.CandidatePoints[0].X, Is.EqualTo(-25d));
            Assert.That(overlay.CandidatePoints[0].Z, Is.EqualTo(25d));
            Assert.That(overlay.CandidatePoints[0].OfficialKindCode, Is.EqualTo("007"));
            Assert.That(overlay.CandidatePoints[0].SourceDirectionNumericValue, Is.EqualTo(153d));
            Assert.That(overlay.AngleSemanticVerified, Is.False);
            Assert.That(overlay.DirectionResolved, Is.False);
            Assert.That(overlay.RenderedArrowAuthorized, Is.False);
            Assert.That(overlay.SourceLengthUnitVerified, Is.False);
            Assert.That(snapshot.DistributionApproved, Is.False);
            Assert.That(snapshot.TraversalReady, Is.False);
            Assert.That(snapshot.GameplayReady, Is.False);

            source.candidatePoints[0].commonEnuMillimeters.x = 90_000d;
            source.candidatePoints[0].officialKindCode = "changed";
            source.candidatePoints = Array.Empty<행정동디오라마도로방향점후보Data>();
            source.overlaySetHashSha256 = new string('F', 64);
            Assert.That(overlay.CandidatePoints[0].X, Is.EqualTo(-25d));
            Assert.That(overlay.CandidatePoints[0].OfficialKindCode, Is.EqualTo("007"));
            Assert.That(overlay.OverlaySetHashSha256, Is.EqualTo(new string('B', 64)));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토RoadDirectionPoint>)overlay.CandidatePoints)[0] = null);
        }

        [Test]
        public void 비공개도로방향후보는_식별자_좌표_hash와권위변조를거절한다()
        {
            AssertDirectionOverlayRejected(value => value.schemaVersion = "direction.v0",
                "AdministrativeDongPrivateRoadDirectionOverlaySchemaInvalid");
            AssertDirectionOverlayRejected(value => value.administrativeAreaStableId =
                    "region:kr:hjd:1126057600",
                "AdministrativeDongPrivateRoadDirectionOverlayAreaMismatch");
            AssertDirectionOverlayRejected(value => value.overlaySetHashSha256 = "invalid",
                "AdministrativeDongPrivateRoadDirectionOverlaySetHashInvalid");
            AssertDirectionOverlayRejected(value => value.candidatePoints[0].candidateStableId =
                    "raw-id",
                "AdministrativeDongPrivateRoadDirectionCandidateIdInvalid");
            AssertDirectionOverlayRejected(value => value.candidatePoints =
                    new[] { value.candidatePoints[0], value.candidatePoints[0] },
                "AdministrativeDongPrivateRoadDirectionCandidateDuplicate");
            AssertDirectionOverlayRejected(value => value.candidatePoints[0]
                    .commonEnuMillimeters.x = double.NaN,
                "AdministrativeDongPrivateRoadDirectionCoordinateInvalid");
            AssertDirectionOverlayRejected(value => value.candidatePoints[0]
                    .commonEnuMillimeters.z = 101_000d,
                "AdministrativeDongPrivateRoadDirectionCoordinateOutOfBounds");
            AssertDirectionOverlayRejected(value => value.candidatePoints[0]
                    .officialKindCode = " ",
                "AdministrativeDongPrivateRoadDirectionCandidateValueInvalid");
            AssertDirectionOverlayRejected(value => value.angleSemanticVerified = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            AssertDirectionOverlayRejected(value => value.directionResolved = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            AssertDirectionOverlayRejected(value => value.renderedArrowAuthorized = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            AssertDirectionOverlayRejected(value => value.sourceLengthUnitVerified = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            AssertDirectionOverlayRejected(value => value.traversalReady = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
            AssertDirectionOverlayRejected(value => value.publicDisplayAllowed = true,
                "AdministrativeDongPrivateRoadDirectionOverlayAuthorityRejected");
        }

        [Test]
        public void 방향후보8415개는_고정무방향마름모로기존네Mesh에포함된다()
        {
            var overlay = RoadDirectionOverlayFixture();
            overlay.candidatePoints = Enumerable.Range(0, 8415).Select(index =>
                new 행정동디오라마도로방향점후보Data
                {
                    candidateStableId = "road-direction-symbol:sha256:"
                                       + index.ToString("x64"),
                    commonEnuMillimeters = new 행정동디오라마공통EnuMillimetersData
                        { x = 25_000d, z = 75_000d },
                    officialKindCode = "007",
                    sourceDirectionNumericValue = 153d
                }).ToArray();
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), overlay);
            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();

            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);

            Assert.That(report.RoadDirectionPointCandidateCount, Is.EqualTo(8415));
            Assert.That(report.RoadDirectionUnresolvedCount, Is.EqualTo(8415));
            Assert.That(report.RoadDirectionRenderedArrowCount, Is.Zero);
            Assert.That(report.PrivateRoadDirectionOverlayVertexCount, Is.EqualTo(33_660));
            Assert.That(batches, Has.Count.EqualTo(4));
            Assert.That(report.RendererBatchCount, Is.LessThanOrEqualTo(4));
            Assert.That(report.MaximumBatchVertexCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch));
            Assert.That(report.TotalVertexCount,
                Is.EqualTo(batches.Sum(value => value.Vertices.Count)));
        }

        [Test]
        public void View는_도로방향후보를동일한네Renderer에표시하고Collider를만들지않는다()
        {
            var root = new GameObject("administrative-dong-private-direction-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), false, out var diagnostic), Is.True);
                Assert.That(diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.PrivateRoadDirectionOverlayAcceptedDiagnostic));
                Assert.That(view.PrivateRoadDirectionOverlay.CandidatePoints, Has.Count.EqualTo(1));
                Assert.That(view.BuildReport.RoadDirectionUnresolvedCount, Is.EqualTo(1));
                Assert.That(view.BuildReport.RoadDirectionRenderedArrowCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(view.ModelRoot.GetComponentsInChildren<Transform>(true).Length,
                    Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 비오톱은_원본double을깊은복사하고_128개짧은선분을정확히생략한다()
        {
            var source=BiotopeOverlayFixture();
            var ring=new List<double[]> { new[]{10_000d,10_000d} };
            for(var i=1;i<=128;i++) ring.Add(new[]{10_000d+i*.0005d,10_000d});
            ring.Add(new[]{20_000d,20_000d}); ring.Add(new[]{10_000d,10_000d});
            source.candidatePolygons[0].polygons[0].exteriorRingMm=ring.ToArray();
            var snapshot=행정동디오라마검토Projection.Create(Fixture(),OverlayFixture(),WalkOverlayFixture(),RoadDirectionOverlayFixture(),source);
            var model=snapshot.PrivateBiotopeOverlay;
            var expectedX=((10_000d+.0005d)/1000d-snapshot.SourceCenterX)/snapshot.MetersPerUnit;
            Assert.That(model.Candidates[0].Polygons[0].Exterior.Points[1].X,
                Is.EqualTo(expectedX).Within(1e-12));
            source.candidatePolygons[0].polygons[0].exteriorRingMm[1][0]=99_000d;
            Assert.That(model.Candidates[0].Polygons[0].Exterior.Points[1].X,
                Is.EqualTo(expectedX).Within(1e-12));
            var batches=new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var report=행정동디오라마검토MeshBuilder.Build(snapshot,batches.Add);
            Assert.That(report.BiotopeSourceSegmentCount,Is.EqualTo(130));
            Assert.That(report.BiotopeSkippedAtOrBelowOneMillimeterSegmentCount,Is.EqualTo(128));
            Assert.That(report.BiotopeRenderedSegmentCount,Is.EqualTo(2));
            Assert.That(report.PrivateBiotopeOverlayVertexCount,Is.EqualTo(8));
            Assert.That(report.PrivateBiotopeOverlayIndexCount,Is.EqualTo(12));
            Assert.That(report.TotalIndexCount,Is.EqualTo(batches.Sum(x=>x.Triangles.Count)));
        }

        [Test]
        public void 비오톱은_잘못된권한_hash와닫히지않은ring을거절한다()
        {
            AssertBiotopeRejected(v=>v.colliderAuthorized=true,"AdministrativeDongPrivateBiotopeAuthorityRejected");
            AssertBiotopeRejected(v=>v.overlaySetHashSha256="bad","AdministrativeDongPrivateBiotopeIdentityInvalid");
            AssertBiotopeRejected(v=>v.candidatePolygons[0].polygons[0].exteriorRingMm[3]=new[]{30_000d,30_000d},
                "AdministrativeDongPrivateBiotopeRingNotClosed");
            AssertBiotopeRejected(v=>v.coordinateFrame.method="unknown","AdministrativeDongPrivateBiotopeCoordinateFrameInvalid");
        }

        [Test]
        public void 비오톱bounds는_원본경계에서반올림오차0점5밀리미터까지만허용한다()
        {
            var allowed=BiotopeOverlayFixture();
            allowed.candidatePolygons[0].polygons[0].exteriorRingMm[0]=new[]{-.5d,10_000d};
            allowed.candidatePolygons[0].polygons[0].exteriorRingMm[3]=new[]{-.5d,10_000d};
            Assert.DoesNotThrow(()=>행정동디오라마검토Projection.Create(Fixture(),OverlayFixture(),WalkOverlayFixture(),RoadDirectionOverlayFixture(),allowed));
            var rejected=BiotopeOverlayFixture();
            rejected.candidatePolygons[0].polygons[0].exteriorRingMm[0]=new[]{-.5001d,10_000d};
            rejected.candidatePolygons[0].polygons[0].exteriorRingMm[3]=new[]{-.5001d,10_000d};
            AssertBiotopeRejected(v=>{v.candidatePolygons[0].polygons[0].exteriorRingMm[0]=new[]{-.5001d,10_000d};v.candidatePolygons[0].polygons[0].exteriorRingMm[3]=new[]{-.5001d,10_000d};},"AdministrativeDongPrivateBiotopeRingOutOfBounds");
        }

        [Test]
        public void 이전v1부터v4까지의projection경로는비오톱없이유지된다()
        {
            Assert.That(행정동디오라마검토Projection.Create(Fixture()).PrivateBiotopeOverlay,Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(),OverlayFixture()).PrivateBiotopeOverlay,Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(),OverlayFixture(),WalkOverlayFixture()).PrivateBiotopeOverlay,Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(),OverlayFixture(),WalkOverlayFixture(),RoadDirectionOverlayFixture()).PrivateBiotopeOverlay,Is.Null);
        }

        [Test]
        public void 차선표시선형은_double경로를깊은복사하고_기존네Mesh에만quad로합친다()
        {
            var source = LaneMarkingOverlayFixture();
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                source);
            var overlay = snapshot.PrivateLaneMarkingOverlay;
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.Candidates, Has.Count.EqualTo(1));
            Assert.That(overlay.FragmentCount, Is.EqualTo(2));
            Assert.That(overlay.PathVertexCount, Is.EqualTo(4));
            Assert.That(overlay.SegmentCount, Is.EqualTo(2));
            Assert.That(overlay.Candidates[0].Fragments[0].Path[0].X, Is.EqualTo(-40d));

            source.candidateMarkings[0].fragments[0].commonEnuMillimeterPath[0].x = 90_000d;
            source.candidateMarkings = Array.Empty<행정동디오라마차선표시후보Data>();
            Assert.That(overlay.Candidates, Has.Count.EqualTo(1));
            Assert.That(overlay.Candidates[0].Fragments[0].Path[0].X, Is.EqualTo(-40d));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토LaneMarkingCandidate>)overlay.Candidates)[0] = null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토LaneMarkingPoint>)overlay.Candidates[0]
                    .Fragments[0].Path)[0] = null);

            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);
            Assert.That(report.LaneMarkingCandidateCount, Is.EqualTo(1));
            Assert.That(report.LaneMarkingFragmentCount, Is.EqualTo(2));
            Assert.That(report.LaneMarkingRenderedSegmentCount, Is.EqualTo(2));
            Assert.That(report.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount,
                Is.Zero);
            Assert.That(report.PrivateLaneMarkingOverlayVertexCount, Is.EqualTo(8));
            Assert.That(report.PrivateLaneMarkingOverlayIndexCount, Is.EqualTo(12));
            Assert.That(report.RendererBatchCount, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void View는_차선표시선형을동일한네Renderer에표시하고Collider를만들지않는다()
        {
            var root = new GameObject("administrative-dong-private-lane-marking-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                    LaneMarkingOverlayFixture(), false, out var diagnostic), Is.True);
                Assert.That(diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.PrivateLaneMarkingOverlayAcceptedDiagnostic));
                Assert.That(view.PrivateLaneMarkingOverlay.Candidates, Has.Count.EqualTo(1));
                Assert.That(view.BuildReport.LaneMarkingRenderedSegmentCount,
                    Is.EqualTo(2));
                Assert.That(view.DepthPlan.Layers, Has.Count.EqualTo(8));
                Assert.That(view.DepthPlan.Layers.Single(value =>
                        value.LayerKindCode == "Mobility").StateCode,
                    Is.EqualTo(행정동디오라마깊이StateCodes.Blocked));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(view.ModelRoot.GetComponentsInChildren<Transform>(true).Length,
                    Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View는_차선표시적용뒤_v5또는기초도형으로교체하면차선상태를정리한다()
        {
            var root = new GameObject("administrative-dong-lane-marking-reset-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                    LaneMarkingOverlayFixture(), false, out _), Is.True);
                Assert.That(view.PrivateLaneMarkingOverlay, Is.Not.Null);
                Assert.That(view.BuildReport.LaneMarkingRenderedSegmentCount, Is.EqualTo(2));

                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(), false,
                    out var v5Diagnostic), Is.True);
                Assert.That(v5Diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.PrivateBiotopeOverlayAcceptedDiagnostic));
                Assert.That(view.PrivateLaneMarkingOverlay, Is.Null);
                Assert.That(view.BuildReport.LaneMarkingCandidateCount, Is.Zero);
                Assert.That(view.BuildReport.LaneMarkingFragmentCount, Is.Zero);
                Assert.That(view.BuildReport.LaneMarkingRenderedSegmentCount, Is.Zero);
                Assert.That(view.BuildReport.PrivateLaneMarkingOverlayVertexCount, Is.Zero);
                Assert.That(view.BuildReport.PrivateLaneMarkingOverlayIndexCount, Is.Zero);

                Assert.That(view.TryApply(Fixture(), false, out var geometryDiagnostic), Is.True);
                Assert.That(geometryDiagnostic,
                    Is.EqualTo(행정동디오라마검토Projection.AcceptedDiagnostic));
                Assert.That(view.PrivateLaneMarkingOverlay, Is.Null);
                Assert.That(view.BuildReport.LaneMarkingRenderedSegmentCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 차선표시선형은_권위변조와잘못된fragment를거절한다()
        {
            AssertLaneMarkingRejected(value => value.schemaVersion = "lane.v0",
                "AdministrativeDongPrivateLaneMarkingCoordinateFrameInvalid");
            AssertLaneMarkingRejected(value => value.sourceVintage = "2021",
                "AdministrativeDongPrivateLaneMarkingCoordinateFrameInvalid");
            AssertLaneMarkingRejected(value => value.coordinateUnit = "meter",
                "AdministrativeDongPrivateLaneMarkingCoordinateFrameInvalid");
            AssertLaneMarkingRejected(value => value.overlaySetHashSha256 = "bad",
                "AdministrativeDongPrivateLaneMarkingIdentityInvalid");
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].candidateStableId =
                    "lane-marking:raw",
                "AdministrativeDongPrivateLaneMarkingCandidateInvalid");
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].fragments[0]
                    .commonEnuMillimeterPath[0].x = double.NaN,
                "AdministrativeDongPrivateLaneMarkingCoordinateInvalid");
            var authorityChanges = new Action<행정동디오라마비공개차선표시OverlaySetData>[]
            {
                value => value.historicalBoundaryBootstrapOnly = false,
                value => value.privateReviewOnly = false,
                value => value.privateReviewVisualizationAllowed = false,
                value => value.reviewVisualizationOnly = false,
                value => value.laneOutlineAuthorized = false,
                value => value.allSourceSegmentsRendered = true,
                value => value.laneMeaningAuthorized = true,
                value => value.directionAuthorized = true,
                value => value.trafficAuthorized = true,
                value => value.stopLineAuthorized = true,
                value => value.signalAuthorized = true,
                value => value.colliderAuthorized = true,
                value => value.publicDisplayAllowed = true,
                value => value.runtimeAuthorized = true,
                value => value.traversalReady = true,
                value => value.gameplayReady = true,
                value => value.currentPointerUsed = true,
                value => value.currentPointerUpdated = true,
                value => value.databaseWriteAllowed = true,
                value => value.mongoWriteAllowed = true,
                value => value.sourceCandidateUnityApplyAllowed = true
            };
            foreach (var change in authorityChanges)
                AssertLaneMarkingRejected(change,
                    "AdministrativeDongPrivateLaneMarkingAuthorityRejected");
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].fragments[0]
                    .fragmentOrdinal = 1,
                "AdministrativeDongPrivateLaneMarkingFragmentInvalid");
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].fragments[0]
                    .commonEnuMillimeterPath = new[]
                    {
                        new 행정동디오라마공통EnuMillimetersData { x = 10_000d, z = 30_000d }
                    },
                "AdministrativeDongPrivateLaneMarkingFragmentInvalid");
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].fragments[0]
                    .commonEnuMillimeterPath[0].x = -.5001d,
                "AdministrativeDongPrivateLaneMarkingCoordinateOutOfBounds");
        }

        [Test]
        public void 차선표시선형은_1mm이하와물리중복선분을거절한다()
        {
            AssertLaneMarkingRejected(value => value.candidateMarkings[0].fragments[0]
                    .commonEnuMillimeterPath[1] =
                    new 행정동디오라마공통EnuMillimetersData
                        { x = 10_001d, z = 30_000d },
                "AdministrativeDongPrivateLaneMarkingSegmentTooShort");
            AssertLaneMarkingRejected(value =>
                {
                    var first = value.candidateMarkings[0];
                    first.fragments = new[]
                    {
                        first.fragments[0],
                        new 행정동디오라마차선표시조각Data
                        {
                            fragmentOrdinal = 1,
                            commonEnuMillimeterPath = new[]
                            {
                                new 행정동디오라마공통EnuMillimetersData
                                    { x = 40_000d, z = 30_000d },
                                new 행정동디오라마공통EnuMillimetersData
                                    { x = 10_000d, z = 30_000d }
                            }
                        }
                    };
                }, "AdministrativeDongPrivateLaneMarkingSegmentDuplicate");
        }

        [Test]
        public void 이전v1부터v5까지의projection경로는차선표시없이유지된다()
        {
            Assert.That(행정동디오라마검토Projection.Create(Fixture())
                .PrivateLaneMarkingOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture())
                .PrivateLaneMarkingOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture()).PrivateLaneMarkingOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture())
                .PrivateLaneMarkingOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture())
                .PrivateLaneMarkingOverlay, Is.Null);
        }

        [Test]
        public void 수평등고선추적은_높이값없이깊은복사하고_기존네Mesh에평면quad로합친다()
        {
            var source = HorizontalContourTraceOverlayFixture();
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                LaneMarkingOverlayFixture(), source);
            var overlay = snapshot.PrivateHorizontalContourTraceOverlay;
            Assert.That(overlay.Candidates, Has.Count.EqualTo(1));
            Assert.That(overlay.FragmentCount, Is.EqualTo(2));
            Assert.That(overlay.PathVertexCount, Is.EqualTo(4));
            Assert.That(overlay.SegmentCount, Is.EqualTo(2));
            Assert.That(overlay.Candidates[0].Fragments[0].Path[0].X, Is.EqualTo(-40d));
            Assert.That(typeof(행정동디오라마수평등고선추적후보Data).GetFields()
                    .Select(value => value.Name),
                Is.EquivalentTo(new[] { "candidateStableId", "fragments" }));

            source.candidateTraces[0].fragments[0].commonEnuMillimeterPath[0].x = 90_000d;
            source.candidateTraces = Array.Empty<행정동디오라마수평등고선추적후보Data>();
            Assert.That(overlay.Candidates, Has.Count.EqualTo(1));
            Assert.That(overlay.Candidates[0].Fragments[0].Path[0].X, Is.EqualTo(-40d));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마검토HorizontalContourTraceCandidate>)
                    overlay.Candidates)[0] = null);

            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);
            Assert.That(report.HorizontalContourTraceCandidateCount, Is.EqualTo(1));
            Assert.That(report.HorizontalContourTraceFragmentCount, Is.EqualTo(2));
            Assert.That(report.HorizontalContourTraceRenderedSegmentCount, Is.EqualTo(2));
            Assert.That(report.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount,
                Is.Zero);
            Assert.That(report.PrivateHorizontalContourTraceOverlayVertexCount, Is.EqualTo(8));
            Assert.That(report.PrivateHorizontalContourTraceOverlayIndexCount, Is.EqualTo(12));
            Assert.That(batches.SelectMany(value => value.Vertices)
                    .Count(value => Mathf.Abs(value.y - .4f) < .00001f),
                Is.EqualTo(8));
            Assert.That(batches, Has.Count.EqualTo(4));
        }

        [Test]
        public void View는_수평등고선을네Renderer에표시하고_환경과이동권위를올리지않는다()
        {
            var root = new GameObject("administrative-dong-horizontal-contour-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                    LaneMarkingOverlayFixture(), HorizontalContourTraceOverlayFixture(), false,
                    out var diagnostic), Is.True);
                Assert.That(diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection
                        .PrivateHorizontalContourTraceOverlayAcceptedDiagnostic));
                Assert.That(view.PrivateHorizontalContourTraceOverlay, Is.Not.Null);
                Assert.That(view.DepthPlan.Layers, Has.Count.EqualTo(8));
                Assert.That(view.DepthPlan.Layers.Single(value =>
                        value.LayerKindCode == "Mobility").StateCode,
                    Is.EqualTo(행정동디오라마깊이StateCodes.Blocked));
                Assert.That(view.DepthPlan.Layers.Single(value =>
                        value.LayerKindCode == "EnvironmentPresentation").StateCode,
                    Is.EqualTo(행정동디오라마깊이StateCodes.NotProvided));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(view.ModelRoot.GetComponentsInChildren<Transform>(true).Length,
                    Is.EqualTo(5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 수평등고선추적은_권위변조와높이관련읽기를모두거절한다()
        {
            AssertHorizontalContourTraceRejected(value => value.schemaVersion = "contour.v0",
                "AdministrativeDongPrivateHorizontalContourTraceCoordinateFrameInvalid");
            AssertHorizontalContourTraceRejected(value => value.sourceVintage = "2025",
                "AdministrativeDongPrivateHorizontalContourTraceCoordinateFrameInvalid");
            AssertHorizontalContourTraceRejected(value => value.overlaySetHashSha256 = "bad",
                "AdministrativeDongPrivateHorizontalContourTraceIdentityInvalid");
            AssertHorizontalContourTraceRejected(value =>
                    value.candidateTraces[0].candidateStableId = "contour-trace:raw",
                "AdministrativeDongPrivateHorizontalContourTraceCandidateInvalid");
            var authorityChanges = new Action<행정동디오라마비공개수평등고선추적OverlaySetData>[]
            {
                value => value.privateReviewOnly = false,
                value => value.privateReviewVisualizationAllowed = false,
                value => value.reviewVisualizationOnly = false,
                value => value.historicalBoundaryBootstrapOnly = false,
                value => value.sourceHorizontalGeometryObserved = false,
                value => value.allSourceSegmentsRendered = true,
                value => value.sourceAttributeDatabaseRead = true,
                value => value.sourceElevationValuesRead = true,
                value => value.sourceElevationValuesSerialized = true,
                value => value.currentBoundaryEstablished = true,
                value => value.currentTerrainEstablished = true,
                value => value.contourMeaningAuthorized = true,
                value => value.elevationMeaningAuthorized = true,
                value => value.heightUnitVerified = true,
                value => value.verticalDatumVerified = true,
                value => value.terrainSurfaceAuthorized = true,
                value => value.terrainMeshAuthorized = true,
                value => value.colliderAuthorized = true,
                value => value.publicDisplayAllowed = true,
                value => value.databaseWriteAllowed = true,
                value => value.mongoWriteAllowed = true,
                value => value.sourceCandidateUnityApplyAllowed = true,
                value => value.runtimeAuthorized = true,
                value => value.traversalReady = true,
                value => value.gameplayReady = true,
                value => value.currentPointerUsed = true,
                value => value.currentPointerUpdated = true
            };
            foreach (var change in authorityChanges)
                AssertHorizontalContourTraceRejected(change,
                    "AdministrativeDongPrivateHorizontalContourTraceAuthorityRejected");
        }

        [Test]
        public void 수평등고선추적은_잘못된fragment와1mm이하및물리중복을거절한다()
        {
            AssertHorizontalContourTraceRejected(value => value.candidateTraces[0]
                    .fragments[0].fragmentOrdinal = 1,
                "AdministrativeDongPrivateHorizontalContourTraceFragmentInvalid");
            AssertHorizontalContourTraceRejected(value => value.candidateTraces[0]
                    .fragments[0].commonEnuMillimeterPath = new[]
                    {
                        new 행정동디오라마공통EnuMillimetersData { x = 10_000d, z = 30_000d }
                    },
                "AdministrativeDongPrivateHorizontalContourTraceFragmentInvalid");
            AssertHorizontalContourTraceRejected(value => value.candidateTraces[0]
                    .fragments[0].commonEnuMillimeterPath[1] =
                    new 행정동디오라마공통EnuMillimetersData { x = 10_001d, z = 30_000d },
                "AdministrativeDongPrivateHorizontalContourTraceSegmentTooShort");
            AssertHorizontalContourTraceRejected(value =>
                {
                    var candidate = value.candidateTraces[0];
                    candidate.fragments = new[]
                    {
                        candidate.fragments[0],
                        new 행정동디오라마수평등고선추적조각Data
                        {
                            fragmentOrdinal = 1,
                            commonEnuMillimeterPath = new[]
                            {
                                new 행정동디오라마공통EnuMillimetersData
                                    { x = 40_000d, z = 30_000d },
                                new 행정동디오라마공통EnuMillimetersData
                                    { x = 10_000d, z = 30_000d }
                            }
                        }
                    };
                }, "AdministrativeDongPrivateHorizontalContourTraceSegmentDuplicate");
        }

        [Test]
        public void 이전v1부터v6까지의projection경로는수평등고선없이유지된다()
        {
            Assert.That(행정동디오라마검토Projection.Create(Fixture())
                .PrivateHorizontalContourTraceOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture())
                .PrivateHorizontalContourTraceOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture()).PrivateHorizontalContourTraceOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture())
                .PrivateHorizontalContourTraceOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture())
                .PrivateHorizontalContourTraceOverlay, Is.Null);
            Assert.That(행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                LaneMarkingOverlayFixture()).PrivateHorizontalContourTraceOverlay, Is.Null);
        }

        [Test]
        public void View는_수평등고선적용뒤_v6또는기초도형으로교체하면등고선상태를정리한다()
        {
            var root = new GameObject("administrative-dong-horizontal-contour-reset-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                    LaneMarkingOverlayFixture(), HorizontalContourTraceOverlayFixture(), false,
                    out _), Is.True);
                Assert.That(view.PrivateHorizontalContourTraceOverlay, Is.Not.Null);
                Assert.That(view.BuildReport.HorizontalContourTraceRenderedSegmentCount,
                    Is.EqualTo(2));

                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                    LaneMarkingOverlayFixture(), false, out _), Is.True);
                Assert.That(view.PrivateHorizontalContourTraceOverlay, Is.Null);
                Assert.That(view.BuildReport.HorizontalContourTraceCandidateCount, Is.Zero);
                Assert.That(view.BuildReport.HorizontalContourTraceRenderedSegmentCount, Is.Zero);
                Assert.That(view.BuildReport.PrivateHorizontalContourTraceOverlayVertexCount,
                    Is.Zero);
                Assert.That(view.BuildReport.PrivateHorizontalContourTraceOverlayIndexCount,
                    Is.Zero);

                Assert.That(view.TryApply(Fixture(), false, out _), Is.True);
                Assert.That(view.PrivateHorizontalContourTraceOverlay, Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertHorizontalContourTraceRejected(
            Action<행정동디오라마비공개수평등고선추적OverlaySetData> change,
            string expected)
        {
            var data = HorizontalContourTraceOverlayFixture();
            change(data);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                LaneMarkingOverlayFixture(), data, out var snapshot, out var diagnostic),
                Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expected));
        }

        private static void AssertLaneMarkingRejected(
            Action<행정동디오라마비공개차선표시OverlaySetData> change, string expected)
        {
            var data = LaneMarkingOverlayFixture();
            change(data);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), RoadDirectionOverlayFixture(), BiotopeOverlayFixture(),
                data, out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expected));
        }

        private static 행정동디오라마비공개차선표시OverlaySetData
            LaneMarkingOverlayFixture()
            => new()
            {
                schemaVersion = 행정동디오라마비공개차선표시Policy.SchemaVersion,
                revision = "northeast-seoul-private-lane-marking-overlay.fixture.r1",
                overlaySetStableId = "private-lane-marking-overlay:sha256:"
                                     + new string('3', 64),
                administrativeAreaStableId = "region:kr:hjd:1126057500",
                sourceVintage = "file-2021-08-09",
                coordinateFrame = new BiotopeCoordinateFrameData
                {
                    metersPerUnit = 1d,
                    method = 행정동디오라마비공개차선표시Policy.CoordinateFrameMethod,
                    originLatitude = 37.580912d,
                    originLongitude = 127.088502d,
                    worldOffsetX = 0d,
                    worldOffsetZ = 0d
                },
                coordinateUnit = "millimeter",
                candidateMarkings = new[]
                {
                    new 행정동디오라마차선표시후보Data
                    {
                        candidateStableId = "lane-marking:sha256:" + new string('4', 64),
                        sourceKindCode = "001",
                        sourceSecondaryKindCode = "UNSPECIFIED",
                        sourceFormCode = "UNSPECIFIED",
                        fragments = new[]
                        {
                            new 행정동디오라마차선표시조각Data
                            {
                                fragmentOrdinal = 0,
                                commonEnuMillimeterPath = new[]
                                {
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 10_000d, z = 30_000d },
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 30_000d }
                                }
                            },
                            new 행정동디오라마차선표시조각Data
                            {
                                fragmentOrdinal = 1,
                                commonEnuMillimeterPath = new[]
                                {
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 30_000d },
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 70_000d }
                                }
                            }
                        }
                    }
                },
                historicalBoundaryBootstrapOnly = true,
                privateReviewOnly = true,
                privateReviewVisualizationAllowed = true,
                reviewVisualizationOnly = true,
                laneOutlineAuthorized = true,
                allSourceSegmentsRendered = false,
                laneMeaningAuthorized = false,
                directionAuthorized = false,
                trafficAuthorized = false,
                stopLineAuthorized = false,
                signalAuthorized = false,
                colliderAuthorized = false,
                publicDisplayAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                currentPointerUsed = false,
                currentPointerUpdated = false,
                databaseWriteAllowed = false,
                mongoWriteAllowed = false,
                sourceCandidateUnityApplyAllowed = false,
                overlaySetHashSha256 = new string('5', 64)
            };

        private static 행정동디오라마비공개수평등고선추적OverlaySetData
            HorizontalContourTraceOverlayFixture()
            => new()
            {
                schemaVersion = 행정동디오라마비공개수평등고선추적Policy.SchemaVersion,
                revision = "northeast-seoul-private-horizontal-contour-trace.fixture.r1",
                overlaySetStableId = "private-horizontal-contour-trace-overlay:sha256:"
                                     + new string('6', 64),
                administrativeAreaStableId = "region:kr:hjd:1126057500",
                sourceVintage = "file-2025-03-20",
                coordinateFrame = new BiotopeCoordinateFrameData
                {
                    metersPerUnit = 1d,
                    method = 행정동디오라마비공개수평등고선추적Policy.CoordinateFrameMethod,
                    originLatitude = 37.580912d,
                    originLongitude = 127.088502d,
                    worldOffsetX = 0d,
                    worldOffsetZ = 0d
                },
                coordinateUnit = "millimeter",
                candidateTraces = new[]
                {
                    new 행정동디오라마수평등고선추적후보Data
                    {
                        candidateStableId = "contour-trace:sha256:" + new string('7', 64),
                        fragments = new[]
                        {
                            new 행정동디오라마수평등고선추적조각Data
                            {
                                fragmentOrdinal = 0,
                                commonEnuMillimeterPath = new[]
                                {
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 10_000d, z = 30_000d },
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 30_000d }
                                }
                            },
                            new 행정동디오라마수평등고선추적조각Data
                            {
                                fragmentOrdinal = 1,
                                commonEnuMillimeterPath = new[]
                                {
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 30_000d },
                                    new 행정동디오라마공통EnuMillimetersData
                                        { x = 40_000d, z = 70_000d }
                                }
                            }
                        }
                    }
                },
                privateReviewOnly = true,
                privateReviewVisualizationAllowed = true,
                reviewVisualizationOnly = true,
                historicalBoundaryBootstrapOnly = true,
                sourceHorizontalGeometryObserved = true,
                allSourceSegmentsRendered = false,
                sourceAttributeDatabaseRead = false,
                sourceElevationValuesRead = false,
                sourceElevationValuesSerialized = false,
                currentBoundaryEstablished = false,
                currentTerrainEstablished = false,
                contourMeaningAuthorized = false,
                elevationMeaningAuthorized = false,
                heightUnitVerified = false,
                verticalDatumVerified = false,
                terrainSurfaceAuthorized = false,
                terrainMeshAuthorized = false,
                colliderAuthorized = false,
                publicDisplayAllowed = false,
                databaseWriteAllowed = false,
                mongoWriteAllowed = false,
                sourceCandidateUnityApplyAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                currentPointerUsed = false,
                currentPointerUpdated = false,
                overlaySetHashSha256 = new string('8', 64)
            };

        private static void AssertBiotopeRejected(Action<행정동디오라마비공개비오톱OverlaySetData> change,string expected)
        {
            var data=BiotopeOverlayFixture(); change(data);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(),OverlayFixture(),WalkOverlayFixture(),RoadDirectionOverlayFixture(),data,out var snapshot,out var diagnostic),Is.False);
            Assert.That(snapshot,Is.Null); Assert.That(diagnostic,Is.EqualTo(expected));
        }

        private static 행정동디오라마비공개비오톱OverlaySetData BiotopeOverlayFixture()
            => new()
            {
                schemaVersion=행정동디오라마비공개비오톱Policy.SchemaVersion,
                revision="biotope.fixture.r1", overlaySetStableId="private-biotope-outline-overlay:sha256:"+new string('1',64),
                administrativeAreaStableId="region:kr:hjd:1126057500", sourceVintage="fixture",
                coordinateFrame=new BiotopeCoordinateFrameData{metersPerUnit=1,method=행정동디오라마비공개비오톱Policy.CoordinateFrameMethod,originLatitude=37.580912,originLongitude=127.088502,worldOffsetX=0,worldOffsetZ=0},
                coordinateUnit="millimeter", candidatePolygons=new[]{new BiotopeCandidateData{stableId="biotope:"+new string('2',32),classification=new BiotopeClassificationData{evaluationGrade="1등급",legendLabel="산림지",typeCode="H7"},polygons=new[]{new BiotopePolygonData{exteriorRingMm=new[]{new[]{10_000d,10_000d},new[]{20_000d,10_000d},new[]{20_000d,20_000d},new[]{10_000d,10_000d}},interiorRingsMm=Array.Empty<double[][]>()}}}},
                privateReviewVisualizationAllowed=true,biotopeOutlineAuthorized=true,allSourceSegmentsRendered=false,filledPolygonAuthorized=false,colliderAuthorized=false,legalEffectAuthorized=false,speciesObservationAuthorized=false,safetyAuthorized=false,passageAuthorized=false,publicDisplayAllowed=false,runtimeAuthorized=false,traversalReady=false,gameplayReady=false,currentPointerUsed=false,currentPointerUpdated=false,databaseWriteAllowed=false,mongoWriteAllowed=false,sourceCandidateUnityApplyAllowed=false,overlaySetHashSha256=new string('3',64)
            };

        [Test]
        public void 면목제3점8동과면목본동은_동일한8개깊이슬롯과의존성을갖는다()
        {
            var first = 행정동디오라마검토Projection.Create(Fixture()).DepthPlan;
            var second = 행정동디오라마검토Projection.Create(
                Fixture("region:kr:hjd:1126056500")).DepthPlan;
            var expectedKinds = new[]
            {
                "DataEvidence", "BaseSpatialPresentation", "SpatialMeaning", "Mobility",
                "Interaction", "LifeSimulation", "BusinessOverlay", "EnvironmentPresentation"
            };

            Assert.That(first.Layers.Select(value => value.LayerKindCode), Is.EqualTo(expectedKinds));
            Assert.That(second.Layers.Select(value => value.LayerKindCode), Is.EqualTo(expectedKinds));
            Assert.That(first.Layers.Select(value => value.AssemblyOrder),
                Is.EqualTo(Enumerable.Range(1, 8).Select(value => value * 10)));
            Assert.That(second.Layers.Select(value => value.LayerStableId),
                Is.EqualTo(first.Layers.Select(value => value.LayerStableId)));
            Assert.That(first.Layers.Select(value => value.LayerStableId).Distinct().Count(), Is.EqualTo(8));
            Assert.That(first.Layers.All(value => value.LayerStableId.StartsWith(
                "administrative-dong-diorama-layer:", StringComparison.Ordinal)), Is.True);

            foreach (var layer in first.Layers)
            {
                var counterpart = second.Layers.Single(value => value.LayerStableId == layer.LayerStableId);
                Assert.That(counterpart.DependencyLayerStableIds, Is.EqualTo(layer.DependencyLayerStableIds));
                Assert.That(counterpart.StateCode, Is.EqualTo(layer.StateCode));
                Assert.That(counterpart.DiagnosticCode, Is.EqualTo(layer.DiagnosticCode));
                Assert.That(layer.DiagnosticCode, Is.Not.Empty);
                foreach (var dependency in layer.DependencyLayerStableIds)
                    Assert.That(first.Layers.Single(value => value.LayerStableId == dependency).AssemblyOrder,
                        Is.LessThan(layer.AssemblyOrder));
            }
            Assert.That(first.PlanStableId, Is.Not.EqualTo(second.PlanStableId));
            Assert.That(first.PlanHashSha256, Is.Not.EqualTo(second.PlanHashSha256));
        }

        [Test]
        public void 도형깊이계획은_자료와기초표현과선택만검토가능하며_실행권위를올리지않는다()
        {
            var source = Fixture();
            source.Manifest.ReadinessCode = AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary;
            var plan = 행정동디오라마검토Projection.Create(source).DepthPlan;

            Assert.That(plan.ReadinessCode, Is.EqualTo(source.Manifest.ReadinessCode));
            Assert.That(plan.Layers.Where(value => value.AvailableForPrivateReview)
                .Select(value => value.LayerKindCode),
                Is.EqualTo(new[] { "DataEvidence", "BaseSpatialPresentation", "Interaction" }));
            Assert.That(plan.Layers.Where(value => value.StateCode == 행정동디오라마깊이StateCodes.Blocked)
                .Select(value => value.LayerKindCode), Is.EqualTo(new[] { "SpatialMeaning", "Mobility" }));
            Assert.That(plan.Layers.Where(value => value.StateCode == 행정동디오라마깊이StateCodes.NotProvided)
                .Select(value => value.LayerKindCode),
                Is.EqualTo(new[] { "LifeSimulation", "BusinessOverlay", "EnvironmentPresentation" }));
            foreach (var layer in plan.Layers.Where(value => value.AvailableForPrivateReview))
            foreach (var dependency in layer.DependencyLayerStableIds)
                Assert.That(plan.Layers.Single(value => value.LayerStableId == dependency)
                    .AvailableForPrivateReview, Is.True);
            Assert.That(plan.Layers.Single(value => value.LayerKindCode == "Interaction").DiagnosticCode,
                Does.Contain("RuntimeInputNotVerified"));
            Assert.That(plan.ObservationPresentationOnly, Is.True);
            Assert.That(plan.ChangesOperationalState, Is.False);
            Assert.That(plan.DistributionApproved, Is.False);
            Assert.That(plan.TraversalReady, Is.False);
            Assert.That(plan.GameplayReady, Is.False);
            Assert.That(plan.RuntimeVerified, Is.False);
            Assert.That(source.Manifest.DistributionApproved, Is.False);
            Assert.That(source.Manifest.TraversalReady, Is.False);
            Assert.That(source.Manifest.GameplayReady, Is.False);
        }

        [Test]
        public void 깊이계획Hash는_반복입력에안정적이고_입력hash와판본과검토상태에결속된다()
        {
            var baseline = 행정동디오라마검토Projection.Create(Fixture()).DepthPlan;
            var repeated = 행정동디오라마검토Projection.Create(Fixture()).DepthPlan;
            Assert.That(baseline.PlanHashSha256, Does.Match("^[0-9A-F]{64}$"));
            Assert.That(repeated.PlanHashSha256, Is.EqualTo(baseline.PlanHashSha256));

            var changedHash = Fixture();
            changedHash.Manifest.ProjectionHashSha256 = new string('C', 64);
            changedHash.Tiles[0].ProjectionHashSha256 = changedHash.Manifest.ProjectionHashSha256;
            var changedPlan = 행정동디오라마검토Projection.Create(changedHash).DepthPlan;
            Assert.That(changedPlan.PlanStableId, Is.EqualTo(baseline.PlanStableId));
            Assert.That(changedPlan.PlanHashSha256, Is.Not.EqualTo(baseline.PlanHashSha256));
            Assert.That(changedPlan.ProjectionHashSha256, Is.EqualTo(new string('C', 64)));

            var changedVintage = Fixture();
            changedVintage.Manifest.SourceVintage = "fixture.r2";
            Assert.That(행정동디오라마검토Projection.Create(changedVintage).DepthPlan.PlanHashSha256,
                Is.Not.EqualTo(baseline.PlanHashSha256));
            var changedReadiness = Fixture();
            changedReadiness.Manifest.ReadinessCode =
                AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary;
            Assert.That(행정동디오라마검토Projection.Create(changedReadiness).DepthPlan.PlanHashSha256,
                Is.Not.EqualTo(baseline.PlanHashSha256));
        }

        [Test]
        public void 깊이계획은_원본DTO변조와분리되고_슬롯과의존성을수정할수없다()
        {
            var source = Fixture();
            var plan = 행정동디오라마검토Projection.Create(source).DepthPlan;
            var hash = plan.PlanHashSha256;
            source.Manifest.AdministrativeAreaStableId = "region:kr:hjd:1126056500";
            source.Manifest.ProjectionHashSha256 = new string('C', 64);
            source.Manifest.SourceVintage = "modified";

            Assert.That(plan.AdministrativeAreaStableId, Is.EqualTo("region:kr:hjd:1126057500"));
            Assert.That(plan.ProjectionHashSha256, Is.EqualTo(new string('A', 64)));
            Assert.That(plan.SourceVintage, Is.EqualTo("fixture.r1"));
            Assert.That(plan.PlanHashSha256, Is.EqualTo(hash));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<행정동디오라마깊이Layer>)plan.Layers)[0] = plan.Layers[1]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)plan.Layers[1].DependencyLayerStableIds)[0] = "changed");
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
            Assert.That(행정동디오라마검토Projection.TryCreate(source, OverlayFixture(),
                out var overlaySnapshot, out var overlayDiagnostic), Is.False);
            Assert.That(overlaySnapshot, Is.Null);
            Assert.That(overlayDiagnostic,
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
            Assert.That(report.CrosswalkPointCount, Is.Zero);
            Assert.That(report.IntersectionPointCount, Is.Zero);
            Assert.That(report.PrivateObservationOverlayVertexCount, Is.Zero);
            Assert.That(report.WalkNetworkNodePointCount, Is.Zero);
            Assert.That(report.WalkNetworkLinkFragmentCount, Is.Zero);
            Assert.That(report.WalkNetworkLinkPathVertexCount, Is.Zero);
            Assert.That(report.WalkNetworkOutputPathVertexCount, Is.Zero);
            Assert.That(report.PrivateWalkNetworkOverlayVertexCount, Is.Zero);
        }

        [Test]
        public void 비공개후보Glyph는_후보별객체없이기존네Batch와Vertex예산에합쳐진다()
        {
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture());
            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();

            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);

            Assert.That(batches, Has.Count.EqualTo(4));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumRendererCount));
            Assert.That(report.CrosswalkPointCount, Is.EqualTo(1));
            Assert.That(report.IntersectionPointCount, Is.EqualTo(1));
            Assert.That(report.PrivateObservationOverlayVertexCount, Is.EqualTo(24));
            Assert.That(report.PrivateObservationOverlaySetHashSha256,
                Is.EqualTo(new string('E', 64)));
            Assert.That(report.MaximumBatchVertexCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch));
            Assert.That(report.TotalVertexCount,
                Is.EqualTo(batches.Sum(value => value.Vertices.Count)));
        }

        [Test]
        public void 보행망전체Path는_후보별객체없이기존네Batch에모든연속Edge를합친다()
        {
            var snapshot = 행정동디오라마검토Projection.Create(Fixture(), OverlayFixture(),
                WalkOverlayFixture());
            var batches = new List<행정동디오라마검토MeshBuilder.MeshBatch>();

            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, batches.Add);

            Assert.That(batches, Has.Count.EqualTo(4));
            Assert.That(report.RendererBatchCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumRendererCount));
            Assert.That(report.WalkNetworkNodePointCount, Is.EqualTo(1));
            Assert.That(report.WalkNetworkLinkFragmentCount, Is.EqualTo(1));
            Assert.That(report.WalkNetworkLinkPathVertexCount, Is.EqualTo(3));
            Assert.That(report.WalkNetworkOutputPathVertexCount, Is.EqualTo(4));
            Assert.That(report.PrivateWalkNetworkOverlayVertexCount, Is.EqualTo(12));
            Assert.That(report.PrivateWalkNetworkOverlaySetHashSha256,
                Is.EqualTo(new string('A', 64)));
            Assert.That(report.MaximumBatchVertexCount,
                Is.LessThanOrEqualTo(행정동디오라마검토MeshBuilder.MaximumVerticesPerBatch));
            Assert.That(report.TotalVertexCount,
                Is.EqualTo(batches.Sum(value => value.Vertices.Count)));
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
                var firstDepthPlan = view.DepthPlan;
                var firstSize = view.ObservationCamera.orthographicSize;
                Assert.That(firstDepthPlan, Is.Not.Null);
                Assert.That(firstDepthPlan.AdministrativeAreaStableId,
                    Is.EqualTo("region:kr:hjd:1126057500"));
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
                Assert.That(view.DepthPlan, Is.Not.SameAs(firstDepthPlan));
                Assert.That(view.DepthPlan.AdministrativeAreaStableId,
                    Is.EqualTo("region:kr:hjd:1126057600"));
                Assert.That(view.HasSelection, Is.False);
                Assert.That(view.ObservationCamera.orthographicSize, Is.GreaterThan(firstSize));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

                view.Clear();
                Assert.That(view.Snapshot, Is.Null);
                Assert.That(view.DepthPlan, Is.Null);
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
        public void View는_비공개후보를표시하고_geometryOnly교체시이전Overlay를정리한다()
        {
            var root = new GameObject("administrative-dong-private-overlay-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), false,
                    out var diagnostic), Is.True);
                Assert.That(diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.PrivateObservationOverlayAcceptedDiagnostic));
                var overlayRoot = view.ModelRoot;
                Assert.That(view.PrivateObservationOverlay, Is.Not.Null);
                Assert.That(view.BuildReport.CrosswalkPointCount, Is.EqualTo(1));
                Assert.That(view.BuildReport.IntersectionPointCount, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

                Assert.That(view.TryApply(Fixture("region:kr:hjd:1126057600",
                    "building:second"), false, out _), Is.True);
                Assert.That(overlayRoot == null, Is.True);
                Assert.That(view.PrivateObservationOverlay, Is.Null);
                Assert.That(view.BuildReport.CrosswalkPointCount, Is.Zero);
                Assert.That(view.BuildReport.IntersectionPointCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View는_관찰과보행망후보를같은네Mesh에표시하고Collider를만들지않는다()
        {
            var root = new GameObject("administrative-dong-private-walk-overlay-test");
            try
            {
                var view = root.AddComponent<행정동디오라마검토View>();
                Assert.That(view.TryApply(Fixture(), OverlayFixture(), WalkOverlayFixture(),
                    false, out var diagnostic), Is.True);
                Assert.That(diagnostic, Is.EqualTo(
                    행정동디오라마검토Projection.PrivateWalkNetworkOverlayAcceptedDiagnostic));
                Assert.That(view.PrivateObservationOverlay, Is.Not.Null);
                Assert.That(view.PrivateWalkNetworkOverlay, Is.Not.Null);
                Assert.That(view.BuildReport.WalkNetworkNodePointCount, Is.EqualTo(1));
                Assert.That(view.BuildReport.WalkNetworkLinkFragmentCount, Is.EqualTo(1));
                Assert.That(view.BuildReport.WalkNetworkLinkPathVertexCount, Is.EqualTo(3));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(view.Snapshot.DepthPlan.Layers.Single(value =>
                        value.LayerKindCode == "Mobility").StateCode,
                    Is.EqualTo(행정동디오라마깊이StateCodes.Blocked));
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
                Assert.That(view.DepthPlan, Is.Null);
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

        private static void AssertOverlayRejected(
            Action<행정동디오라마비공개관찰OverlaySetData> change,
            string expectedDiagnostic)
        {
            var overlay = OverlayFixture();
            change(overlay);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(), overlay,
                out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expectedDiagnostic));
        }

        private static void AssertWalkOverlayRejected(
            Action<행정동디오라마비공개보행망OverlaySetData> change,
            string expectedDiagnostic)
        {
            var overlay = WalkOverlayFixture();
            change(overlay);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(), OverlayFixture(),
                overlay, out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expectedDiagnostic));
        }

        private static void AssertDirectionOverlayRejected(
            Action<행정동디오라마비공개도로방향OverlaySetData> change,
            string expectedDiagnostic)
        {
            var overlay = RoadDirectionOverlayFixture();
            change(overlay);
            Assert.That(행정동디오라마검토Projection.TryCreate(Fixture(), OverlayFixture(),
                WalkOverlayFixture(), overlay, out var snapshot, out var diagnostic), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(expectedDiagnostic));
        }

        private static 행정동디오라마비공개도로방향OverlaySetData
            RoadDirectionOverlayFixture()
            => new()
            {
                schemaVersion = 행정동디오라마비공개도로방향Policy.SchemaVersion,
                revision = "northeast-seoul-private-road-direction-overlay.fixture.r1",
                overlaySetStableId = "private-road-direction-overlay:sha256:"
                                     + new string('1', 64),
                administrativeAreaStableId = "region:kr:hjd:1126057500",
                sourceVintage = "oa15536-file-20260910+fixture",
                candidatePoints = new[]
                {
                    new 행정동디오라마도로방향점후보Data
                    {
                        candidateStableId = "road-direction-symbol:sha256:"
                                            + new string('2', 64),
                        commonEnuMillimeters = new 행정동디오라마공통EnuMillimetersData
                            { x = 25_000d, z = 75_000d },
                        officialKindCode = "007",
                        sourceDirectionNumericValue = 153d
                    }
                },
                privateReviewVisualizationAllowed = true,
                reviewVisualizationOnly = true,
                historicalBoundaryBootstrapOnly = true,
                angleSemanticVerified = false,
                directionResolved = false,
                renderedArrowAuthorized = false,
                sourceLengthUnitVerified = false,
                publicDisplayAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                currentPointerUsed = false,
                currentPointerUpdated = false,
                sourceCandidateUnityApplyAllowed = false,
                overlaySetHashSha256 = new string('B', 64)
            };

        private static 행정동디오라마비공개보행망OverlaySetData WalkOverlayFixture()
            => new()
            {
                schemaVersion = 행정동디오라마비공개보행망Policy.SchemaVersion,
                revision = "northeast-seoul-private-walk-network-overlay.fixture.r1",
                overlaySetStableId =
                    "administrative-dong-private-walk-network:region:kr:hjd:1126057500.v1",
                administrativeAreaStableId = "region:kr:hjd:1126057500",
                sourceVintage = "oa21208-reference-2020+fixture",
                historicalBoundaryBootstrapOnly = true,
                privateReviewOnly = true,
                privateReviewVisualizationAllowed = true,
                reviewVisualizationOnly = true,
                observationCandidateOnly = true,
                sourceCandidateUnityApplyAllowed = false,
                publicDisplayAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                currentPointerUsed = false,
                currentPointerUpdated = false,
                sourceIdentifiersIncluded = false,
                sourceBoroughIdentityIncluded = false,
                legalAreaIdentityIncluded = false,
                sourceTimestampsIncluded = false,
                sourceEpsg5186CoordinatesIncluded = false,
                exactAddressIncluded = false,
                personalDataIncluded = false,
                sourceReceipts = new[]
                {
                    new 행정동디오라마비공개보행망SourceReceiptData
                    {
                        datasetId = "OA-21208",
                        officialPageUrl =
                            "https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do",
                        sourceReferenceVintage = "2020",
                        candidateSetHashSha256 = new string('8', 64),
                        acquisitionReceiptSha256 = new string('9', 64),
                        licenseCode = "KOGL-Type1-Attribution",
                        sourceCoordinateReference = "WGS84",
                        conversionCoordinateReference = "EPSG:5186",
                        coordinateReferenceStatus = "FixtureRoundTripToCommonEnu"
                    }
                },
                nodePoints = new[]
                {
                    new 행정동디오라마보행망NodePoint후보Data
                    {
                        candidateStableId = "walk-node-candidate:sha256:"
                                            + new string('6', 64),
                        commonEnuMillimeters = new 행정동디오라마공통EnuMillimetersData
                            { x = 25_000d, z = 75_000d },
                        kindCode = "Node",
                        qualityCode = "PendingHumanReview",
                        actorSummaryFlags = new 행정동디오라마보행망ActorSummaryFlagsData()
                    }
                },
                linkFragments = new[]
                {
                    new 행정동디오라마보행망LinkFragment후보Data
                    {
                        candidateStableId = "walk-link-fragment-candidate:sha256:"
                                            + new string('7', 64),
                        commonEnuMillimeterPath = new[]
                        {
                            new 행정동디오라마공통EnuMillimetersData
                                { x = 10_000d, z = 10_000d },
                            new 행정동디오라마공통EnuMillimetersData
                                { x = 50_000d, z = 50_000d },
                            new 행정동디오라마공통EnuMillimetersData
                                { x = 90_000d, z = 20_000d }
                        },
                        kindCode = "LinkFragment",
                        qualityCode = "PendingHumanReview",
                        actorSummaryFlags = new 행정동디오라마보행망ActorSummaryFlagsData
                        {
                            sourceReported = true,
                            pedestrian = true,
                            bicycle = true,
                            personalMobility = false,
                            vehicle = false
                        }
                    }
                },
                overlaySetHashSha256 = new string('A', 64)
            };

        private static 행정동디오라마비공개관찰OverlaySetData OverlayFixture()
            => new()
            {
                schemaVersion = 행정동디오라마비공개관찰Policy.SchemaVersion,
                revision = "administrative-dong-private-observation-overlays:fixture.r1",
                overlaySetStableId =
                    "administrative-dong-private-observation-overlays:region:kr:hjd:1126057500:fixture.r1",
                overlaySetHashSha256 = new string('E', 64),
                administrativeAreaStableId = "region:kr:hjd:1126057500",
                sourceVintage = "fixture.r1",
                historicalBoundaryBootstrapOnly = true,
                privateReviewVisualizationAllowed = true,
                reviewVisualizationOnly = true,
                sourceCandidateUnityApplyAllowed = false,
                publicDisplayAllowed = false,
                runtimeAuthorized = false,
                traversalReady = false,
                gameplayReady = false,
                currentPointerUsed = false,
                currentPointerUpdated = false,
                exactAddressIncluded = false,
                businessIdentityIncluded = false,
                personalDataIncluded = false,
                sourceReceipts = Array.Empty<행정동디오라마비공개관찰SourceReceiptData>(),
                crosswalkPoints = new[]
                {
                    new 행정동디오라마횡단보도점후보Data
                    {
                        candidateStableId =
                            "administrative-dong-crosswalk-candidate:" + new string('C', 64),
                        commonEnuMillimeters = new 행정동디오라마공통EnuMillimetersData
                            { x = 25_000d, z = 75_000d },
                        pedestrianSignalPresent = true,
                        sourceDistrictSpatialAssignmentConflict = false,
                        qualityCode = "OfficialPointCandidate"
                    }
                },
                intersectionPoints = new[]
                {
                    new 행정동디오라마교차로점후보Data
                    {
                        candidateStableId =
                            "candidate:kr:seoul:oa15534:intersection:" + new string('D', 64),
                        commonEnuMillimeters = new 행정동디오라마공통EnuMillimetersData
                            { x = 80_000d, z = 20_000d },
                        linkedCrosswalkCount = 3,
                        otherHistoricalAdministrativeAreaLinkCount = 1,
                        sourceDistrictSpatialAssignmentConflict = false,
                        qualityCode = "DerivedCrosswalkCluster"
                    }
                }
            };

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
