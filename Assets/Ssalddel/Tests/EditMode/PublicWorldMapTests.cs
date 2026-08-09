using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.WorldMap;
using Ssalddel.Unity.Infrastructure.WorldMap;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;
using Ssalddel.Unity.Runtime.Transport;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class PublicWorldMapTests
    {
        private const string FixtureJson = "{\"DatasetCode\":\"public-world\",\"Revision\":\"world-map:2026-08-06\",\"GeneratedAtUtc\":\"2026-08-06T00:00:00Z\",\"Observations\":[{\"StableId\":\"publisher:kr-seoul-001\",\"DatasetCode\":\"public-world\",\"LayerCode\":\"news-publisher\",\"CountryCode\":\"KR\",\"CountryName\":\"대한민국\",\"Latitude\":37.5665,\"Longitude\":126.978,\"Title\":\"서울 공개 언론사\",\"Summary\":\"공개 RSS 출처\",\"SourceName\":\"공식 공개 출처\",\"EvidenceAsOfUtc\":\"2026-08-05T00:00:00Z\",\"EvidenceStatusCode\":\"Verified\",\"DetailHref\":\"/community/map/publisher:kr-seoul-001\",\"SourceHref\":\"https://example.test/source\",\"LocationPrecisionCode\":\"City\",\"FreshnessCode\":\"Current\",\"BoundaryNotice\":\"공개 정보이며 재고·계약·개인 위치를 의미하지 않습니다.\",\"Metrics\":[{\"Code\":\"article-count\",\"DisplayName\":\"기사 수\",\"Value\":12,\"Unit\":\"count\"}]}]}";

        [Test]
        public async Task Repository는_공개경계와_원본Revision을_보존한다()
        {
            var client = new StubApiClient(FixtureJson);
            var snapshot = await new LoadPublicWorldMapUseCase(new CommunityWorldMapRepository(client))
                .ExecuteAsync("public world", CancellationToken.None);

            Assert.That(client.LastRequest.RequiresAuthentication, Is.False);
            Assert.That(client.LastRequest.RelativePath, Is.EqualTo("api/v1/community/world-map/observations?dataset=public%20world"));
            Assert.That(snapshot.Revision, Is.EqualTo("world-map:2026-08-06"));
            Assert.That(snapshot.Markers[0].BoundaryNotice, Does.Contain("재고·계약"));
            Assert.That(snapshot.Markers[0].LocationPrecisionCode, Is.EqualTo("City"));
            Assert.That(snapshot.Markers[0].Metrics[0].Value, Is.EqualTo(12m));
        }

        [Test]
        public void Repository는_API실패를_sample로_숨기지_않는다()
        {
            var repository = new CommunityWorldMapRepository(new StubApiClient("{}", 503));
            Assert.ThrowsAsync<InvalidOperationException>(() => repository.LoadAsync(string.Empty, CancellationToken.None));
        }

        [Test]
        public async Task Presenter는_경위도를_표시좌표로_투영해_marker를_생성한다()
        {
            var snapshot = await new CommunityWorldMapRepository(new StubApiClient(FixtureJson))
                .LoadAsync(string.Empty, CancellationToken.None);
            var root = new GameObject("PresenterTest");
            try
            {
                var presenter = root.AddComponent<PublicWorldMapPresenter>();
                presenter.Apply(snapshot);
                Assert.That(presenter.MarkerCount, Is.EqualTo(1));
                Assert.That(presenter.Project(0, 0), Is.EqualTo(new Vector3(0, .15f, 0)));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public async Task SceneController는_중복초기화를_한번만_실행한다()
        {
            var repository = new CountingRepository(await new CommunityWorldMapRepository(new StubApiClient(FixtureJson))
                .LoadAsync(string.Empty, CancellationToken.None));
            var renderCount = 0;
            var controller = new PublicWorldMapSceneController(
                new LoadPublicWorldMapUseCase(repository),
                _ => renderCount++);

            await Task.WhenAll(
                controller.InitializeAsync(string.Empty, CancellationToken.None),
                controller.InitializeAsync(string.Empty, CancellationToken.None));

            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(renderCount, Is.EqualTo(1));
            Assert.That(controller.InitializationStarted, Is.True);
        }

        [Test]
        public async Task 최초조회실패는_marker를_비우고_InitialLoadError가_된다()
        {
            var clearCount = 0;
            var states = new System.Collections.Generic.List<PublicWorldMapSceneStatus>();
            var controller = new PublicWorldMapSceneController(
                new LoadPublicWorldMapUseCase(new ThrowingRepository()),
                _ => Assert.Fail("실패 응답은 render하면 안 됩니다."),
                () => clearCount++,
                state => states.Add(state.Status));

            await controller.InitializeAsync(string.Empty, CancellationToken.None);

            Assert.That(clearCount, Is.EqualTo(1));
            Assert.That(states, Is.EqualTo(new[] { PublicWorldMapSceneStatus.Loading, PublicWorldMapSceneStatus.InitialLoadError }));
            Assert.That(controller.CurrentSnapshot, Is.Null);
        }

        [Test]
        public async Task 성공후_갱신실패는_기존_marker와_snapshot을_유지한다()
        {
            var snapshot = await new CommunityWorldMapRepository(new StubApiClient(FixtureJson))
                .LoadAsync(string.Empty, CancellationToken.None);
            var repository = new SuccessThenFailureRepository(snapshot);
            var renderCount = 0;
            var clearCount = 0;
            var controller = new PublicWorldMapSceneController(
                new LoadPublicWorldMapUseCase(repository),
                _ => renderCount++,
                () => clearCount++);

            await controller.InitializeAsync(string.Empty, CancellationToken.None);
            await controller.RefreshAsync(string.Empty, CancellationToken.None);

            Assert.That(renderCount, Is.EqualTo(1));
            Assert.That(clearCount, Is.Zero);
            Assert.That(controller.CurrentSnapshot, Is.SameAs(snapshot));
            Assert.That(controller.CurrentState.Status, Is.EqualTo(PublicWorldMapSceneStatus.RefreshError));
            Assert.That(controller.CurrentState.KeepsExistingMarkers, Is.True);
        }

        private sealed class StubApiClient : IUnityApiClient
        {
            private readonly UnityApiResponse response;
            public UnityApiRequest LastRequest { get; private set; }
            public StubApiClient(string body, long statusCode = 200) => response = new UnityApiResponse { StatusCode = statusCode, Body = body };
            public Task<UnityApiResponse> SendAsync(UnityApiRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(response);
            }
        }

        private sealed class CountingRepository : ICommunityWorldMapRepository
        {
            private readonly PublicWorldMapSnapshot snapshot;
            public int LoadCount { get; private set; }
            public CountingRepository(PublicWorldMapSnapshot snapshot) => this.snapshot = snapshot;
            public Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken)
            {
                LoadCount++;
                return Task.FromResult(snapshot);
            }
        }

        private sealed class ThrowingRepository : ICommunityWorldMapRepository
        {
            public Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken) =>
                Task.FromException<PublicWorldMapSnapshot>(new InvalidOperationException("server unavailable"));
        }

        private sealed class SuccessThenFailureRepository : ICommunityWorldMapRepository
        {
            private readonly PublicWorldMapSnapshot snapshot;
            private int callCount;
            public SuccessThenFailureRepository(PublicWorldMapSnapshot snapshot) => this.snapshot = snapshot;
            public Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken)
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult(snapshot)
                    : Task.FromException<PublicWorldMapSnapshot>(new InvalidOperationException("refresh failed"));
            }
        }
    }
}
