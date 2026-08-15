using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 진부Hub입고UiTests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public async Task Preview는상태를바꾸지않고_Confirm과WorldTick뒤재조회한다()
        {
            var authority = new 진부Hub입고UiFixtureAuthorityClient();
            var coordinator = new 진부Hub입고UiCoordinator(authority);
            await coordinator.LoadAsync(SimulationWorldShellFixture.SessionStableId,
                CancellationToken.None);
            var initialRevision = coordinator.CurrentProjection.StateRevision;

            await coordinator.PreviewAsync(CancellationToken.None);

            Assert.That(coordinator.PhaseCode, Is.EqualTo(진부Hub입고UiCodes.PreviewReady));
            Assert.That(coordinator.CurrentProjection.StateRevision, Is.EqualTo(initialRevision));
            Assert.That(coordinator.CurrentPreview.TargetStableId,
                Is.EqualTo("freight-transport:sim:jinbu-potato.fixture"));

            await coordinator.ConfirmAsync(CancellationToken.None);
            Assert.That(coordinator.CurrentProjection.StateCode,
                Is.EqualTo(진부Hub입고UiCodes.InProgress));
            for (var index = 0; index < 3; index++)
                await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(coordinator.CurrentProjection.WorkflowStageCode,
                Is.EqualTo("PutAwayPending"));

            await coordinator.PreviewAsync(CancellationToken.None);
            await coordinator.ConfirmAsync(CancellationToken.None);
            for (var index = 0; index < 3; index++)
                await coordinator.AdvanceAsync(CancellationToken.None);

            Assert.That(coordinator.CurrentProjection.StateCode,
                Is.EqualTo(진부Hub입고UiCodes.Completed));
            Assert.That(coordinator.CurrentProjection.WorkflowStageCode,
                Is.EqualTo("PutAwayCompleted"));
        }

        [Test]
        public async Task ServerRepository는허용된Simulation경로만사용하고_확정뒤정보판을재조회한다()
        {
            var fixture = new 진부Hub입고UiFixtureAuthorityClient();
            var ready = await fixture.LoadAsync("unused", CancellationToken.None);
            await fixture.PreviewAsync(ready, ready.Action("Preview"), CancellationToken.None);
            var working = await fixture.ConfirmAsync(
                ready, ready.Action("Confirm"), CancellationToken.None);
            var api = new StubApiClient(new[]
            {
                Ok(JsonUtility.ToJson(ready)),
                Ok("{\"Decision\":{\"BlockReasonCodes\":[]},\"TaskPlan\":{\"TaskStableId\":\"task:inspection\",\"DurationTicks\":2}}"),
                Ok("{}"),
                Ok(JsonUtility.ToJson(working)),
            });
            var repository = new 진부Hub입고UiServerRepository(api);

            var loaded = await repository.LoadAsync(ready.SessionStableId, CancellationToken.None);
            var preview = await repository.PreviewAsync(
                loaded, loaded.Action("Preview"), CancellationToken.None);
            var confirmed = await repository.ConfirmAsync(
                loaded, loaded.Action("Confirm"), CancellationToken.None);

            Assert.That(preview.CanConfirm, Is.True);
            Assert.That(confirmed.StateCode, Is.EqualTo(진부Hub입고UiCodes.InProgress));
            Assert.That(api.Requests, Has.Count.EqualTo(4));
            Assert.That(api.Requests[0].RelativePath,
                Does.EndWith("/world-ui/surfaces/" + Uri.EscapeDataString(진부Hub입고UiCodes.SurfaceStableId)));
            Assert.That(api.Requests[1].RelativePath, Does.EndWith("/freight-receipt-previews"));
            Assert.That(api.Requests[2].RelativePath, Does.EndWith("/freight-receipts/confirm"));
            Assert.That(api.Requests[2].JsonBody, Does.Contain("\"ExpectedRevision\":11"));
            Assert.That(api.Requests[3].Method, Is.EqualTo("GET"));
        }

        [Test]
        public void 저장Scene은_FigmaMaui테마와서버권위입고정보판을가진다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                Assert.That(root, Is.Not.Null);
                var panel = root.transform.Find(
                    "PersistentUI/SimulationWorldHud/JinbuInboundPanel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(4));
                var presenter = root.GetComponentInChildren<진부Hub입고UiPresenter>(true);
                Assert.That(presenter, Is.Not.Null);
                Assert.DoesNotThrow(() => presenter.ValidateWiring());
                var composition = root.GetComponentInChildren<진부Hub입고UiSceneCompositionRoot>(true);
                Assert.That(composition, Is.Not.Null);
                Assert.That(composition.서버기준사용중, Is.True);
                var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<FigmaMauiWarehouseUiThemeCatalog>(
                    "Assets/Ssalddel/Presentation/World/Catalogs/"
                    + "FigmaMauiWarehouseUiThemeCatalog.asset");
                Assert.That(theme, Is.Not.Null);
                Assert.That(theme.Supports(진부Hub입고UiCodes.SupportedDesignProfileRevision),
                    Is.True);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static UnityApiResponse Ok(string body)
            => new UnityApiResponse { StatusCode = 200, Body = body };

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly Queue<UnityApiResponse> responses;
            public readonly List<UnityApiRequest> Requests = new List<UnityApiRequest>();

            public StubApiClient(IEnumerable<UnityApiResponse> values)
                => responses = new Queue<UnityApiResponse>(values);

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                if (responses.Count == 0)
                    throw new InvalidOperationException("JinbuInboundUiStubResponseMissing");
                return Task.FromResult(responses.Dequeue());
            }
        }
    }
}
