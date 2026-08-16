using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Survival;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 농장전투입력Tests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public async Task 전투저장소는_운영인증없이_기존Simulation경로를사용한다()
        {
            var api = new StubApiClient(new[]
            {
                new UnityApiResponse { StatusCode = 200, Body = StateJson() },
            });
            var repository = new SimulationFarmCombatServerRepository(api);

            var state = await repository.LoadAsync(
                "session:farm:test", CancellationToken.None);

            Assert.That(api.Requests, Has.Count.EqualTo(1));
            Assert.That(api.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(api.Requests[0].RelativePath,
                Is.EqualTo(SimulationFarmCombatApiRoutes.State(
                    "session:farm:test")));
            Assert.That(api.Requests[0].RequiresAuthentication, Is.False);
            Assert.That(state.WorldRevision, Is.EqualTo(7));
            Assert.That(state.Engagements, Has.Length.EqualTo(1));
            Assert.That(state.Engagements[0].StateCode,
                Is.EqualTo(FarmCombatPresentationCodes.AwaitingCombat));
            Assert.That(state.SimulationOnly, Is.True);
            Assert.That(state.IsOperationalState, Is.False);
        }

        [Test]
        public void 개정충돌은_클라이언트수정없이_재동기화대상으로분류한다()
        {
            var api = new StubApiClient(new[]
            {
                new UnityApiResponse
                {
                    StatusCode = 409,
                    Body = "{\"ErrorCode\":\"SimulationExpectedRevisionMismatch\"}",
                },
            });
            var repository = new SimulationFarmCombatServerRepository(api);

            var exception = Assert.ThrowsAsync<SimulationFarmCombatRequestException>(
                async () => await repository.LoadAsync(
                    "session:farm:test", CancellationToken.None));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.IsRevisionConflict, Is.True);
            Assert.That(exception.ErrorCode,
                Is.EqualTo("SimulationExpectedRevisionMismatch"));
        }

        [Test]
        public void 저장Scene의전투Controller는_의미기반공격방어입력을준비한다()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var player = Required<플레이어경관Controller>();
                var combat = Required<전투시점Controller>();
                var input = Required<전투입력Adapter>();
                var composition = Required<농장전투CompositionRoot>();

                Assert.That(player.PresentationOnly, Is.True);
                Assert.That(input.ValidateWiring(), Is.True);
                Assert.That(combat.ValidateWiring(), Is.True);
                Assert.That(combat.PresentationOnly, Is.True);
                Assert.That(composition.ValidateWiring(), Is.True);
                Assert.That(composition.ServerAuthorityEnabled, Is.True);
                Assert.That(composition.ActorStableId,
                    Is.EqualTo("actor:sim:player-survivor"));
                Assert.That(combat.InputPhaseCode,
                    Is.EqualTo(FarmCombatPresentationCodes.Ready));
                Assert.That(combat.LocksPlayerMovement, Is.False);
            }
            finally
            {
                if (scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string StateJson()
            => "{"
               + "\"WorldRevision\":7,"
               + "\"Encounters\":[{"
               + "\"EncounterStableId\":\"encounter:farm:test\","
               + "\"StateCode\":\"AwaitingCombat\","
               + "\"PresentationKey\":\"combat.zombie.awaiting\"}],"
               + "\"Combat\":{"
               + "\"Perspectives\":[],\"Beats\":[],\"Reactions\":[],"
               + "\"SimulationOnly\":true,\"IsOperationalState\":false},"
               + "\"SimulationOnly\":true,\"IsOperationalState\":false}";

        private static T Required<T>() where T : Object
            => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)
                ?? throw new AssertionException(typeof(T).Name + " 배선 누락");

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly Queue<UnityApiResponse> responses;

            public StubApiClient(IEnumerable<UnityApiResponse> values)
                => responses = new Queue<UnityApiResponse>(values);

            public List<UnityApiRequest> Requests { get; } = new();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(responses.Dequeue());
            }
        }
    }
}
