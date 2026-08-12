using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.Exhibition;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Exhibition;
using Ssalddel.Unity.Runtime.ExhibitionFixtures;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합전시관SimulationLoadingTests
    {
        private const string SessionId = "simulation-session:integrated-exhibition-test";

        [Test]
        public async Task 저장소는_기존SimulationSession조회경로를_읽는다()
        {
            var api = new StubApiClient(Response(5));
            var repository = new 통합전시관SimulationSessionRepository(api);

            var result = await repository.LoadAsync(SessionId, CancellationToken.None);

            Assert.That(api.Requests, Has.Count.EqualTo(1));
            Assert.That(api.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(api.Requests[0].RelativePath,
                Is.EqualTo("api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(SessionId)));
            Assert.That(api.Requests[0].RequiresAuthentication, Is.False);
            Assert.That(result.Revision, Is.EqualTo(5));
            Assert.That(result.WorldTick, Is.EqualTo(3));
        }

        [Test]
        public void 저장소는_실운영상태를_Simulation으로받아들이지않는다()
        {
            var repository = new 통합전시관SimulationSessionRepository(
                new StubApiClient(Response(5, true)));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.LoadAsync(SessionId, CancellationToken.None));

            Assert.That(exception!.Message, Is.EqualTo("IntegratedExhibitionOperationalStateForbidden"));
        }

        [Test]
        public void 조회UseCase는_이미표시한개정번호보다_낮은응답을거부한다()
        {
            var useCase = UseCase(new QueueRepository(Session(4)));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                useCase.ExecuteAsync(SessionId, 5, CancellationToken.None));

            Assert.That(exception!.Message, Is.EqualTo("IntegratedExhibitionSimulationRevisionRegressed"));
        }

        [Test]
        public async Task 새로고침실패는_기존상태사본과개정번호를보존한다()
        {
            var rendered = new List<통합전시관ServerBoundSnapshot>();
            var controller = new 통합전시관SimulationController(
                UseCase(new QueueRepository(Session(5), Session(4))),
                rendered.Add);

            await controller.InitializeAsync(SessionId, CancellationToken.None);
            await controller.RefreshAsync(SessionId, CancellationToken.None);

            Assert.That(rendered, Has.Count.EqualTo(1));
            Assert.That(controller.Current!.Session.Revision, Is.EqualTo(5));
            Assert.That(controller.State.Status,
                Is.EqualTo(통합전시관SimulationLoadStatus.RefreshError));
            Assert.That(controller.State.AcceptedRevision, Is.EqualTo(5));
        }

        [Test]
        public void 취소된조회는_화면상태사본을교체하지않는다()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var renderCount = 0;
            var controller = new 통합전시관SimulationController(
                UseCase(new CancelledRepository()),
                _ => renderCount++);

            Assert.CatchAsync<OperationCanceledException>(() =>
                controller.InitializeAsync(SessionId, cancellation.Token));
            Assert.That(renderCount, Is.Zero);
            Assert.That(controller.Current, Is.Null);
        }

        private static 통합전시관SimulationLoadUseCase UseCase(
            I통합전시관SimulationSessionRepository repository)
            => new 통합전시관SimulationLoadUseCase(repository, () =>
                new 통합전시관Mapper().Map(
                    통합전시관FixtureApiModelFactory.CreateFixtureApiModel()));

        private static 통합전시관SimulationSessionState Session(long revision)
            => new 통합전시관SimulationSessionState
            {
                SessionStableId = SessionId,
                ScenarioStableId = "scenario:integrated-exhibition-test",
                Revision = revision,
                WorldRevision = revision,
                WorldTick = 3,
                GameDate = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                ModeCode = "Simulation",
                IsOperationalState = false,
                FetchedAtUtc = DateTimeOffset.UtcNow,
            };

        private static UnityApiResponse Response(long revision, bool operational = false)
            => new UnityApiResponse
            {
                StatusCode = 200,
                Body = "{\"sessionStableId\":\"" + SessionId
                    + "\",\"scenarioStableId\":\"scenario:integrated-exhibition-test\""
                    + ",\"revision\":" + revision
                    + ",\"modeCode\":\"Simulation\",\"isOperationalState\":"
                    + (operational ? "true" : "false")
                    + ",\"worldContext\":{\"worldTick\":3,\"worldRevision\":"
                    + revision + ",\"gameDate\":\"2026-04-15T00:00:00Z\"}}",
            };

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly UnityApiResponse response;
            public StubApiClient(UnityApiResponse value) => response = value;
            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(response);
            }
        }

        private sealed class QueueRepository : I통합전시관SimulationSessionRepository
        {
            private readonly Queue<통합전시관SimulationSessionState> values;
            public QueueRepository(params 통합전시관SimulationSessionState[] states)
                => values = new Queue<통합전시관SimulationSessionState>(states);

            public Task<통합전시관SimulationSessionState> LoadAsync(
                string sessionStableId, CancellationToken cancellationToken)
                => Task.FromResult(values.Dequeue());
        }

        private sealed class CancelledRepository : I통합전시관SimulationSessionRepository
        {
            public Task<통합전시관SimulationSessionState> LoadAsync(
                string sessionStableId, CancellationToken cancellationToken)
                => Task.FromCanceled<통합전시관SimulationSessionState>(cancellationToken);
        }
    }
}
