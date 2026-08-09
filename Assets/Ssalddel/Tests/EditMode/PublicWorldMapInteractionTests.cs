using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.WorldMap;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class PublicWorldMapInteractionTests
    {
        [Test]
        public void Presenter는_StableId로_marker를_증분갱신하고_선택을_전달한다()
        {
            var root = new GameObject("PresenterDiffTest");
            try
            {
                var presenter = root.AddComponent<PublicWorldMapPresenter>();
                string selectedId = null;
                presenter.SetMarkerSelectedHandler(id => selectedId = id);
                presenter.Apply(Snapshot(Marker("observation:a", 37, 127)));
                presenter.Apply(Snapshot(Marker("observation:a", 38, 128), Marker("observation:b", 35, 129)));

                Assert.That(presenter.MarkerCount, Is.EqualTo(2));
                Assert.That(presenter.TrySelect("observation:a"), Is.True);
                Assert.That(selectedId, Is.EqualTo("observation:a"));

                presenter.Apply(Snapshot(Marker("observation:b", 36, 130)));
                Assert.That(presenter.MarkerCount, Is.EqualTo(1));
                Assert.That(presenter.TrySelect("observation:a"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public async Task Controller는_marker_Id를_현재_snapshot에서_다시_조회한다()
        {
            var marker = Marker("observation:selected", 37, 127);
            PublicWorldMarker selected = null;
            var controller = new PublicWorldMapSceneController(
                new LoadPublicWorldMapUseCase(new SnapshotRepository(Snapshot(marker))),
                _ => { },
                showDetail: item => selected = item);

            await controller.InitializeAsync(string.Empty, CancellationToken.None);

            Assert.That(controller.SelectMarker(marker.StableId), Is.True);
            Assert.That(selected, Is.SameAs(marker));
            Assert.That(controller.SelectMarker("observation:missing"), Is.False);
        }

        [Test]
        public void DetailNavigator는_검증된_상대경로만_연다()
        {
            string opened = null;
            var navigator = new ObservationDetailNavigator("http://localhost:5104", url => opened = url);
            var marker = Marker("observation:navigate", 37, 127);
            marker.DetailHref = "/community/world-map/observations/observation:navigate";

            Assert.That(navigator.Navigate(marker), Is.True);
            Assert.That(opened, Is.EqualTo("http://localhost:5104/community/world-map/observations/observation:navigate"));
            Assert.That(navigator.TryResolve("https://evil.example/path", out _), Is.False);
            Assert.That(navigator.TryResolve("//evil.example/path", out _), Is.False);
            Assert.That(navigator.TryResolve("/community/../private", out _), Is.False);
        }

        [Test]
        public void StateView는_최초실패와_갱신실패의_버튼정책을_구분한다()
        {
            var root = new GameObject("StateViewTest");
            try
            {
                var status = Child<Text>(root, "Status");
                var metadata = Child<Text>(root, "Metadata");
                var retry = Child<Button>(root, "Retry");
                var refresh = Child<Button>(root, "Refresh");
                var view = root.AddComponent<PublicWorldMapSceneView>();
                view.Configure(status, metadata, retry, refresh);

                view.Apply(new PublicWorldMapSceneState { Status = PublicWorldMapSceneStatus.InitialLoadError, ErrorMessage = "offline" });
                Assert.That(retry.gameObject.activeSelf, Is.True);
                Assert.That(refresh.gameObject.activeSelf, Is.False);

                view.Apply(new PublicWorldMapSceneState { Status = PublicWorldMapSceneStatus.RefreshError, MarkerCount = 3, Revision = "rev:3" });
                Assert.That(retry.gameObject.activeSelf, Is.False);
                Assert.That(refresh.gameObject.activeSelf, Is.True);
                Assert.That(view.VisibleMessage, Does.Contain("3건"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static T Child<T>(GameObject parent, string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent.transform, false);
            return child.GetComponent<T>();
        }

        private static PublicWorldMapSnapshot Snapshot(params PublicWorldMarker[] markers) => new PublicWorldMapSnapshot
        {
            DatasetCode = "public-world",
            Revision = "revision:test",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Markers = markers
        };

        private static PublicWorldMarker Marker(string stableId, double latitude, double longitude) => new PublicWorldMarker
        {
            StableId = stableId,
            DatasetCode = "public-world",
            LayerCode = "news-publisher",
            CountryCode = "KR",
            CountryName = "대한민국",
            Latitude = latitude,
            Longitude = longitude,
            Title = stableId,
            Summary = "공개 관측 정보",
            SourceName = "공식 공개 출처",
            EvidenceAsOfUtc = DateTimeOffset.UtcNow,
            DetailHref = "/community/world-map/observations/" + stableId,
            LocationPrecisionCode = "City",
            FreshnessCode = "Current",
            BoundaryNotice = "공개 정보이며 재고, 계약 또는 개인 위치를 의미하지 않습니다."
        };

        private sealed class SnapshotRepository : ICommunityWorldMapRepository
        {
            private readonly PublicWorldMapSnapshot snapshot;
            public SnapshotRepository(PublicWorldMapSnapshot snapshot) => this.snapshot = snapshot;
            public Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken) => Task.FromResult(snapshot);
        }
    }
}
