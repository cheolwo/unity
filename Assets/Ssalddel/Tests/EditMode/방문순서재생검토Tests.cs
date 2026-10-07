using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    /// <summary>기본 시험은 합성 입력이다. 실제 파일 검사는 환경변수로 선택하며 위치 승인이나 화면 검증이 아니다.</summary>
    public sealed class 방문순서재생검토Tests
    {
        private const string Area = "region:kr:hjd:1126057500";
        private const string RecordId = "synthetic-visit-review-record";
        private const string Revision = "synthetic-r1";
        private readonly List<GameObject> roots = new();

        [TearDown]
        public void 소유시험객체정리()
        {
            for (var i = roots.Count - 1; i >= 0; i--)
                if (roots[i] != null) UnityEngine.Object.DestroyImmediate(roots[i]);
            roots.Clear();
        }

        [Test]
        public void 작은참조범위는_완전지도를보존하고_공통좌표중심을한번만뺀다()
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var input = FocusInput(snapshot, source.Manifest.CoordinateFrame);
            var space = 방문재생공간검토.Create(snapshot, input);
            var local = space.ToLocal(1010, 2020);

            Assert.That(local.X, Is.EqualTo(-40));
            Assert.That(local.Z, Is.EqualTo(-30));
            Assert.That(space.Snapshot, Is.SameAs(snapshot));
            Assert.That(snapshot.TileCount, Is.EqualTo(1));
            Assert.That(snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(snapshot.Roads, Has.Count.EqualTo(1));
            Assert.That(space.SelectedBuildings[0], Is.SameAs(snapshot.Buildings[0]));

            input.SelectedBuildingIds[0] = "changed-building";
            input.SelectedRoadIds[0] = "changed-road";
            input.MinX = -999;
            source.Tiles[0].Buildings[0].Footprint[0].X = -999;
            Assert.That(space.MinX, Is.EqualTo(1000));
            Assert.That(space.SelectedBuildings[0].BuildingStableId, Is.EqualTo("synthetic-building"));
            Assert.That(space.SelectedRoads[0].RoadStableId, Is.EqualTo("synthetic-road"));
            Assert.That(space.SelectedBuildings[0].Footprint[0].X, Is.EqualTo(-40));
        }

        [Test]
        public void 출처와완전목록과권한과선택ID가맞지않으면_공간결속을거절한다()
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var changes = new (Action<방문검토범위입력> Change, string Error)[]
            {
                (v => v.AdministrativeAreaStableId = "other-area", "VisitReviewSourceMismatch"),
                (v => v.ProjectionHashSha256 = new string('F', 64), "VisitReviewSourceMismatch"),
                (v => v.FocusContentHashSha256 = "invalid-hash", "VisitReviewSourceMismatch"),
                (v => v.SourceBundleHashSha256 = "invalid-hash", "VisitReviewSourceMismatch"),
                (v => v.SourceTileCount = 0, "VisitReviewCompleteSnapshotRequired"),
                (v => v.SourceBuildingCount = 0, "VisitReviewCompleteSnapshotRequired"),
                (v => v.SourceRoadCount = 0, "VisitReviewCompleteSnapshotRequired"),
                (v => v.DistributionApproved = true, "VisitReviewAuthorityInvalid"),
                (v => v.PrivateReviewOnly = false, "VisitReviewAuthorityInvalid"),
                (v => v.TraversalReady = true, "VisitReviewAuthorityInvalid"),
                (v => v.GameplayReady = true, "VisitReviewAuthorityInvalid"),
                (v => v.SourceCenterX += 1, "VisitReviewCoordinateFrameMismatch"),
                (v => v.MetersPerUnit = 2, "VisitReviewCoordinateFrameMismatch"),
                (v => v.MinX = double.NaN, "VisitReviewBoundsInvalid"),
                (v => v.SelectedBuildingIds = new[] { "missing-building" }, "VisitReviewSelectedIdMissing"),
                (v => v.SelectedRoadIds = new[] { "synthetic-road", "synthetic-road" }, "VisitReviewSelectionIdsInvalid")
            };
            foreach (var change in changes)
            {
                var input = FocusInput(snapshot, source.Manifest.CoordinateFrame);
                change.Change(input);
                Assert.That(Assert.Throws<InvalidOperationException>(() =>
                    방문재생공간검토.Create(snapshot, input)).Message, Is.EqualTo(change.Error));
            }
        }

        [Test]
        public void 좌표계식별자는_문화권에독립적이며_원점과offset변경을구별한다()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                var frame = MapFixture().Manifest.CoordinateFrame;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
                var key = 방문재생공간검토.FrameKey(frame);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(방문재생공간검토.FrameKey(frame), Is.EqualTo(key));
                frame.WorldOffsetX += 1;
                Assert.That(방문재생공간검토.FrameKey(frame), Is.Not.EqualTo(key));
                frame.WorldOffsetX -= 1;
                frame.OriginLongitude += .001;
                Assert.That(방문재생공간검토.FrameKey(frame), Is.Not.EqualTo(key));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void 방문은_같은좌표계와검토범위와역사경계안에서만수용하고_경계점은포함한다()
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var input = FocusInput(snapshot, source.Manifest.CoordinateFrame);
            var space = 방문재생공간검토.Create(snapshot, input);
            Assert.That(space.ValidatePrepared(Prepared(space, firstX: 1000, firstZ: 2000)), Is.Empty);
            Assert.That(space.ValidatePrepared(Prepared(space, frame: "other-frame")),
                Is.EqualTo("VisitReviewVisitFrameMismatch"));
            Assert.That(space.ValidatePrepared(Prepared(space, firstX: 999)),
                Is.EqualTo("VisitReviewVisitOutsideFocus"));
            input.MinX = 950;
            var largerFocus = 방문재생공간검토.Create(snapshot, input);
            Assert.That(largerFocus.ValidatePrepared(Prepared(largerFocus, firstX: 999)),
                Is.EqualTo("VisitReviewVisitOutsideHistoricalMap"));
        }

        [Test]
        public void 위치후보와미결속입력은_진단을남기며_초기marker를만들지않는다()
        {
            var presenter = Presenter(out var view, out var space);
            var prepared = UnresolvedPrepared();
            Assert.That(prepared.CanReplay, Is.False);
            Assert.That(presenter.TryLoad(prepared, 0), Is.False);
            Assert.That(presenter.MarkerCount, Is.Zero);
            Assert.That(presenter.MarkerTransform == null, Is.True);
            Assert.That(presenter.DiagnosticCode, Is.EqualTo("VisitPlaybackPreparationRequired"));
            Assert.That(presenter.BoundView, Is.SameAs(view));
            Assert.That(view.Snapshot, Is.SameAs(space.Snapshot));
        }

        [Test]
        public void 명시재생과정지와번호선택을_지도아래로컬marker에반영한다()
        {
            var presenter = Presenter(out var view, out var space);
            Assert.That(presenter.TryLoad(Prepared(space), 0), Is.True, presenter.DiagnosticCode);
            Assert.That(presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Ready));
            Assert.That(presenter.Advance(10), Is.True);
            Assert.That(presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Ready));
            AssertMarker(presenter, -40, -40);
            Assert.That(presenter.MarkerTransform.IsChildOf(view.ModelRoot), Is.True);
            Assert.That(presenter.Play(10), Is.True);
            Assert.That(presenter.Advance(14), Is.True);
            AssertMarker(presenter, 0, 0);
            Assert.That(presenter.Pause(14), Is.True);
            Assert.That(presenter.Advance(100), Is.True);
            AssertMarker(presenter, 0, 0);
            Assert.That(presenter.SeekVisit("synthetic-visit:2", 100), Is.True);
            Assert.That(presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Paused));
            Assert.That(presenter.CurrentFrame.Sequence, Is.EqualTo(2));
            AssertMarker(presenter, 40, 40);
            Assert.That(view.ModelRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void 잘못된갱신은_진단만바꾸고_기존marker와진행을보존한다()
        {
            var presenter = Presenter(out _, out var space);
            Assert.That(presenter.TryLoad(Prepared(space), 0), Is.True);
            Assert.That(presenter.Play(0), Is.True);
            Assert.That(presenter.Advance(4), Is.True);
            var marker = presenter.MarkerTransform;
            var focus = presenter.FocusRoot;
            var position = marker.localPosition;
            var seconds = presenter.CurrentFrame.PresentationSeconds;

            Assert.That(presenter.TryLoad(UnresolvedPrepared(), 5), Is.False);
            Assert.That(presenter.MarkerTransform, Is.SameAs(marker));
            Assert.That(presenter.FocusRoot, Is.SameAs(focus));
            Assert.That(marker.localPosition, Is.EqualTo(position));
            Assert.That(presenter.CurrentFrame.PresentationSeconds, Is.EqualTo(seconds));
            Assert.That(presenter.TryLoad(Prepared(space, menu: "같은판본의변조"), 5), Is.False);
            Assert.That(presenter.DiagnosticCode, Is.EqualTo("VisitPlaybackRevisionConflict"));
            Assert.That(presenter.MarkerTransform, Is.SameAs(marker));
            Assert.That(marker.localPosition, Is.EqualTo(position));
        }

        [Test]
        public void 동일입력재로드는_재생을초기화하거나_marker를중복생성하지않는다()
        {
            var presenter = Presenter(out _, out var space);
            var prepared = Prepared(space);
            Assert.That(presenter.TryLoad(prepared, 0), Is.True);
            Assert.That(presenter.Play(0), Is.True);
            Assert.That(presenter.Advance(4), Is.True);
            var marker = presenter.MarkerTransform;
            var count = presenter.MarkerCount;
            Assert.That(presenter.TryLoad(prepared, 5), Is.True);
            Assert.That(presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Playing));
            Assert.That(presenter.CurrentFrame.PresentationSeconds, Is.EqualTo(5));
            Assert.That(presenter.MarkerCount, Is.EqualTo(count));
            Assert.That(presenter.MarkerTransform, Is.SameAs(marker));
            AssertMarker(presenter, 20, 20);
        }

        [Test]
        public void Clear는_소유표현과재생만정리하고_기존지도를남긴다()
        {
            var presenter = Presenter(out var view, out var space);
            Assert.That(presenter.TryLoad(Prepared(space), 0), Is.True);
            var marker = presenter.MarkerTransform;
            var focus = presenter.FocusRoot;
            var modelRoot = view.ModelRoot;
            var markerMesh = marker.GetComponent<MeshFilter>().sharedMesh;
            var markerMaterial = marker.GetComponent<MeshRenderer>().sharedMaterial;
            var focusMesh = focus.GetComponent<MeshFilter>().sharedMesh;
            var focusMaterial = focus.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(marker.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            Assert.That(focus.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            presenter.Clear();
            Assert.That(marker == null, Is.True);
            Assert.That(focus == null, Is.True);
            Assert.That(markerMesh == null, Is.True);
            Assert.That(markerMaterial == null, Is.True);
            Assert.That(focusMesh == null, Is.True);
            Assert.That(focusMaterial == null, Is.True);
            Assert.That(presenter.MarkerCount, Is.Zero);
            Assert.That(presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Empty));
            Assert.That(view.ModelRoot, Is.SameAs(modelRoot));
            Assert.That(view.Snapshot, Is.SameAs(space.Snapshot));
            Assert.That(modelRoot != null, Is.True);
            Assert.DoesNotThrow(presenter.Clear);
        }

        [Test]
        public void 다른지도사본으로focus를바꾸려하면_기존정상표현을보존한다()
        {
            var presenter = Presenter(out var view, out var space);
            Assert.That(presenter.TryLoad(Prepared(space), 0), Is.True);
            var marker = presenter.MarkerTransform;
            var focus = presenter.FocusRoot;
            var otherView = NewRoot("synthetic-empty-view").AddComponent<행정동디오라마검토View>();
            Assert.That(presenter.TryShowFocus(otherView, space), Is.False);
            var source = MapFixture();
            var otherSnapshot = 행정동디오라마검토Projection.Create(source);
            var otherSpace = 방문재생공간검토.Create(otherSnapshot,
                FocusInput(otherSnapshot, source.Manifest.CoordinateFrame));
            Assert.That(presenter.TryShowFocus(view, otherSpace), Is.False);
            Assert.That(presenter.BoundView, Is.SameAs(view));
            Assert.That(presenter.FocusRoot, Is.SameAs(focus));
            Assert.That(presenter.MarkerTransform, Is.SameAs(marker));
            Assert.That(presenter.DiagnosticCode, Is.Not.Empty);
        }

        [Test]
        public void 명시된로컬동결자료는_완전목록과작은선택목록을구별한다()
        {
            var repositoryRoot = Environment.GetEnvironmentVariable("SSALDDEL_R53_REPO_ROOT");
            if (string.IsNullOrWhiteSpace(repositoryRoot))
                Assert.Ignore("SSALDDEL_R53_REPO_ROOT가 없어 실제 동결 자료 검사를 건너뜁니다.");

            var input = 방문순서재생검토진입.ValidateFrozenSources(repositoryRoot);
            Assert.That(input.SourceTileCount, Is.EqualTo(12));
            Assert.That(input.SourceBuildingCount, Is.EqualTo(2700));
            Assert.That(input.SourceRoadCount, Is.EqualTo(291));
            Assert.That(input.SelectedBuildingIds, Has.Length.EqualTo(86));
            Assert.That(input.SelectedRoadIds, Has.Length.EqualTo(9));
            Assert.That(input.PrivateReviewOnly, Is.True);
            Assert.That(input.DistributionApproved, Is.False);
        }

        [Test]
        public void 명시된실제기록도_위치결속근거없이는_방문재생을보류한다()
        {
            var recordPath = Environment.GetEnvironmentVariable("SSALDDEL_R53_RECORD_PATH");
            if (string.IsNullOrWhiteSpace(recordPath))
                Assert.Ignore("SSALDDEL_R53_RECORD_PATH가 없어 실제 미결속 기록 검사를 건너뜁니다.");

            var prepared = 방문순서재생검토진입.PrepareRecordFile(recordPath, string.Empty, false);
            Assert.That(prepared.CanReplay, Is.False);
            Assert.That(prepared.Visits, Is.Empty);
            Assert.That(prepared.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void 검토카메라를그대로두고지우면_원래시점으로복원한다()
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var space = 방문재생공간검토.Create(snapshot, FocusInput(snapshot, source.Manifest.CoordinateFrame));
            var view = NewRoot("synthetic-camera-review").AddComponent<행정동디오라마검토View>();
            view.BuildFromSnapshot(snapshot, true);
            var camera = view.ObservationCamera;
            var originalPosition = camera.transform.position;
            var originalSize = camera.orthographicSize;
            var presenter = NewRoot("synthetic-camera-presenter").AddComponent<방문순서재생검토Presenter>();
            Assert.That(presenter.TryShowFocus(view, space), Is.True);
            presenter.Clear();
            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(originalSize));
        }

        [Test]
        public void 사용자가검토뒤바꾼카메라는_Clear가덮어쓰지않는다()
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            var space = 방문재생공간검토.Create(snapshot, FocusInput(snapshot, source.Manifest.CoordinateFrame));
            var view = NewRoot("synthetic-camera-review").AddComponent<행정동디오라마검토View>();
            view.BuildFromSnapshot(snapshot, true);
            var presenter = NewRoot("synthetic-camera-presenter").AddComponent<방문순서재생검토Presenter>();
            Assert.That(presenter.TryShowFocus(view, space), Is.True);
            var camera = view.ObservationCamera;
            camera.transform.position += new Vector3(10, 20, 30);
            camera.orthographicSize += 7;
            var changedPosition = camera.transform.position;
            var changedSize = camera.orthographicSize;
            presenter.Clear();
            Assert.That(camera.transform.position, Is.EqualTo(changedPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(changedSize));
        }

        private GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            return root;
        }

        private 방문순서재생검토Presenter Presenter(out 행정동디오라마검토View view,
            out 방문재생공간검토 space)
        {
            var source = MapFixture();
            var snapshot = 행정동디오라마검토Projection.Create(source);
            space = 방문재생공간검토.Create(snapshot, FocusInput(snapshot, source.Manifest.CoordinateFrame));
            view = NewRoot("synthetic-map-review").AddComponent<행정동디오라마검토View>();
            view.BuildFromSnapshot(snapshot, false);
            var presenter = NewRoot("synthetic-visit-review").AddComponent<방문순서재생검토Presenter>();
            Assert.That(presenter.TryShowFocus(view, space), Is.True, presenter.DiagnosticCode);
            return presenter;
        }

        private static void AssertMarker(방문순서재생검토Presenter presenter, double x, double z)
        {
            Assert.That(presenter.MarkerTransform, Is.Not.Null);
            Assert.That(presenter.MarkerTransform.localPosition.x, Is.EqualTo(x).Within(.0001));
            Assert.That(presenter.MarkerTransform.localPosition.z, Is.EqualTo(z).Within(.0001));
        }

        private static 방문재생준비Result Prepared(방문재생공간검토 space,
            double firstX = 1010, double firstZ = 2010, string frame = null, string menu = "합성 메뉴")
        {
            var visits = new[] { Visit(2, menu), Visit(1, menu) };
            var record = Record(visits);
            var bindings = visits.Select(v => new 방문위치결속(RecordId, Revision, v.VisitId,
                v.StoreId, v.Address, v.Latitude, v.Longitude, Area, frame ?? space.CoordinateFrameKey,
                v.Sequence == 1 ? firstX : 1090, v.Sequence == 1 ? firstZ : 2090,
                "synthetic-coordinate-r1", "synthetic-boundary-r1", "synthetic-location-evidence")).ToArray();
            var prepared = 방문재생준비.Prepare(record, Revision, bindings);
            Assert.That(prepared.CanReplay, Is.True, string.Join(",", prepared.Diagnostics));
            return prepared;
        }

        private static 방문재생준비Result UnresolvedPrepared()
        {
            var visit = Visit(1, "합성 미확인 메뉴");
            visit.CoordinateStatus = "AddressGeocodeCandidate_NotEntrance";
            visit.AreaBindingStatus = "Unresolved";
            return 방문재생준비.Prepare(Record(visit), Revision, Array.Empty<방문위치결속>());
        }

        private static RegionalPickupRecord Record(params RegionalPickupVisit[] visits) => new()
        {
            SchemaVersion = "delivery-visit-record.v2", RecordId = RecordId,
            LocalReviewOnly = true, IsOperationalState = false, Visits = visits
        };

        private static RegionalPickupVisit Visit(int sequence, string menu) => new()
        {
            VisitId = "synthetic-visit:" + sequence, StoreId = "synthetic-store:" + sequence,
            Sequence = sequence, Name = "합성 가게 " + sequence, Menu = menu,
            Address = "합성 주소 " + sequence, Latitude = 37.5 + sequence * .001, Longitude = 127.1,
            AddressStatus = "Verified", CoordinateStatus = "Verified", AreaBindingStatus = "Verified",
            AdministrativeAreaStableId = Area, AreaBindingEvidence = "synthetic-boundary-evidence",
            EventKind = "RecordedVisit", PickupCompletionConfirmed = false,
            SourceUrls = new[] { "https://example.invalid/synthetic-visit" }
        };

        private static 방문검토범위입력 FocusInput(행정동디오라마검토Snapshot snapshot,
            AdministrativeDongDioramaCoordinateFrame frame) => new()
        {
            AdministrativeAreaStableId = Area, ProjectionHashSha256 = snapshot.ProjectionHashSha256,
            FocusContentHashSha256 = new string('C', 64), SourceBundleHashSha256 = new string('D', 64),
            CoordinateFrameKey = 방문재생공간검토.FrameKey(frame),
            SourceCenterX = snapshot.SourceCenterX, SourceCenterZ = snapshot.SourceCenterZ,
            MetersPerUnit = 1, MinX = 1000, MinZ = 2000, MaxX = 1100, MaxZ = 2100,
            SourceTileCount = 1, SourceBuildingCount = 1, SourceRoadCount = 1,
            SelectedBuildingIds = new[] { "synthetic-building" }, SelectedRoadIds = new[] { "synthetic-road" },
            PrivateReviewOnly = true
        };

        private static AdministrativeDongDioramaApplyResult MapFixture()
        {
            var projectionHash = new string('A', 64);
            var tileHash = new string('B', 64);
            var tileId = "tile:" + Area + ":2:4";
            return new AdministrativeDongDioramaApplyResult
            {
                Accepted = true,
                Manifest = new AdministrativeDongDioramaManifest
                {
                    SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                    AdministrativeAreaStableId = Area, DisplayName = "합성 행정동", SourceVintage = "synthetic-r1",
                    ProjectionHashSha256 = projectionHash,
                    ReadinessCode = AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                    DataPolicyCode = AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                    ObservationPresentationOnly = true,
                    CoordinateFrame = new AdministrativeDongDioramaCoordinateFrame
                    {
                        Method = "WGS84-ECEF-ENU-at-zero-altitude", OriginLatitude = 37.5805,
                        OriginLongitude = 127.0885, WorldOffsetX = 300, WorldOffsetZ = 600, MetersPerUnit = 1
                    },
                    Bounds = Bounds(),
                    Boundary = new[] { Point(1000, 2000), Point(1100, 2000), Point(1100, 2100),
                        Point(1000, 2100), Point(1000, 2000) },
                    Tiles = new[] { new AdministrativeDongDioramaTileSummary
                    {
                        TileStableId = tileId, TileIndexX = 2, TileIndexZ = 4, TileHashSha256 = tileHash,
                        BuildingCount = 1, RoadSegmentCount = 1
                    } }
                },
                Tiles = new[] { new AdministrativeDongDioramaTile
                {
                    SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                    AdministrativeAreaStableId = Area, ProjectionHashSha256 = projectionHash,
                    TileStableId = tileId, TileHashSha256 = tileHash, TileIndexX = 2, TileIndexZ = 4,
                    Bounds = Bounds(),
                    Buildings = new[] { new AdministrativeDongDioramaBuilding
                    {
                        BuildingStableId = "synthetic-building", CategoryCode = "residential",
                        EvidenceKindCode = "FixtureGeometry", HeightMeters = 18, AboveGroundFloorCount = 6,
                        Footprint = new[] { Point(1010, 2010), Point(1030, 2010), Point(1030, 2030),
                            Point(1010, 2030), Point(1010, 2010) }
                    } },
                    Roads = new[] { new AdministrativeDongDioramaRoadSegment
                    {
                        RoadStableId = "synthetic-road", EvidenceKindCode = "NodeLinkCenterlineOnly",
                        From = Point(1000, 2050), To = Point(1100, 2050)
                    } }
                } },
                DisplayOverlays = Array.Empty<AdministrativeDongDisplayOverlay>()
            };
        }

        private static AdministrativeDongDioramaBounds Bounds() => new()
            { MinX = 1000, MinZ = 2000, MaxX = 1100, MaxZ = 2100 };
        private static AdministrativeDongDioramaPoint Point(double x, double z) => new() { X = x, Z = z };
    }
}
