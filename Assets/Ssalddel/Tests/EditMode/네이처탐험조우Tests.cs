using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 네이처탐험조우Tests
    {
        [Test]
        public async Task 세션_상태사본에서_활성_네이처조우만_읽는다()
        {
            var client = new FakeClient
            {
                Response = new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = "{\"SessionStableId\":\"session:nature\","
                        + "\"Revision\":7,\"NatureThreat\":{"
                        + "\"SimulationOnly\":true,\"IsOperationalState\":false,"
                        + "\"Encounters\":[{\"EncounterStableId\":\"encounter:b\","
                        + "\"EncounterRevision\":2,\"NatureRouteCode\":\"NatureToFarm\","
                        + "\"StateCode\":\"Active\",\"ThreatUnitCount\":3},{"
                        + "\"EncounterStableId\":\"encounter:a\",\"EncounterRevision\":1,"
                        + "\"StateCode\":\"Resolved\",\"ThreatUnitCount\":1}]}}",
                },
            };
            var repository = new 네이처탐험조우ServerRepository(client);

            var state = await repository.LoadAsync("session:nature",
                CancellationToken.None);

            Assert.That(client.LastRequest!.Method, Is.EqualTo("GET"));
            Assert.That(client.LastRequest.RelativePath,
                Is.EqualTo("api/simulation/v1/sessions/session%3Anature"));
            Assert.That(state.WorldRevision, Is.EqualTo(7));
            Assert.That(state.ActiveEncounters(), Has.Length.EqualTo(1));
            Assert.That(state.ActiveEncounters()[0].EncounterStableId,
                Is.EqualTo("encounter:b"));
        }

        [Test]
        public void 조우형상은_플레이어에게_접근한뒤_전투요청만_발행한다()
        {
            var playerObject = new GameObject("Player", typeof(CharacterController));
            var player = playerObject.AddComponent<플레이어경관Controller>();
            typeof(플레이어경관Controller)
                .GetField("_currentMode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(player, 플레이어시점Mode.ThirdPerson);
            var root = new GameObject("NatureEncounterRoot");
            var presenter = root.AddComponent<네이처조우Presenter>();
            presenter.Configure(player, root.transform, null);
            string? requested = null;
            presenter.EncounterResponseRequested += value => requested = value;
            presenter.Apply(State("encounter:nature:one", 2));

            Assert.That(presenter.ActiveEncounterCount, Is.EqualTo(1));
            Assert.That(presenter.UsesPlaceholderVisual, Is.True);
            for (var index = 0; index < 100 && requested == null; index++)
                presenter.EvaluateApproach(.2f);

            Assert.That(requested, Is.EqualTo("encounter:nature:one"));
            Assert.That(presenter.LastStatus, Does.Contain("대응 준비"));
            presenter.MarkResolved("encounter:nature:one");
            Assert.That(presenter.ActiveEncounterCount, Is.Zero);
            Assert.That(presenter.LastStatus, Does.Contain("탐험으로 복귀"));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void 공식Scene은_네이처탐험조우를_서버권위전투에_연결한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity");
            var presenter = Object.FindFirstObjectByType<네이처조우Presenter>(
                FindObjectsInactive.Include);
            var composition = Object.FindFirstObjectByType<
                Ssalddel.Unity.Bootstrap.네이처탐험조우CompositionRoot>(
                FindObjectsInactive.Include);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter!.ValidateWiring(), Is.True);
            Assert.That(presenter.PresentationOnly, Is.True);
            Assert.That(presenter.UsesPlaceholderVisual, Is.True);
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition!.ValidateWiring(), Is.True);
            Assert.That(composition.ServerAuthorityEnabled, Is.True);
        }

        private static 네이처탐험조우StateApiModel State(string id, int count)
            => new()
            {
                SessionStableId = "session:nature",
                WorldRevision = 3,
                SimulationOnly = true,
                Encounters = new[]
                {
                    new 네이처탐험조우ApiModel
                    {
                        EncounterStableId = id,
                        EncounterRevision = 1,
                        NatureRouteCode = 네이처탐험조우Codes.NatureToFarm,
                        StateCode = 네이처탐험조우Codes.Active,
                        ThreatUnitCount = count,
                    },
                },
            };

        private sealed class FakeClient : ISimulationRehearsalUnityApiClient
        {
            public UnityApiResponse Response { get; set; } = new();
            public UnityApiRequest? LastRequest { get; private set; }

            public Task<UnityApiResponse> SendAsync(UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(Response);
            }
        }
    }
}
