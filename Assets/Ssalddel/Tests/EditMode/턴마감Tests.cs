using System;
using System.Collections.Generic;
using System.Linq;
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
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 턴마감Tests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string RootName = "SimulationWorldShell";
        [Test]
        public async System.Threading.Tasks.Task TURN_CARD_UI1B_Fixture는카드선택을다음경영일효과로넘긴다()
        {
            var authority = new 턴마감FixtureAuthorityClient();
            var context = await authority.GetContextAsync(
                SimulationWorldShellFixture.SessionStableId, CancellationToken.None);
            var preview = await authority.PreviewAsync(
                context.SessionStableId, context.Revision, 턴마감CardStableIds.Fool,
                CancellationToken.None);
            var result = await authority.ConfirmAsync(
                context.SessionStableId, "command:test.turn-close", context.Revision,
                턴마감CardStableIds.Fool, CancellationToken.None);

            Assert.That(context.AvailableCards, Has.Length.EqualTo(3));
            Assert.That(preview.ClosingTurnNumber, Is.EqualTo(13));
            Assert.That(preview.NextTurnNumber, Is.EqualTo(14));
            Assert.That(result.WorldTick, Is.EqualTo(13));
            Assert.That(result.Revision, Is.EqualTo(13));
            Assert.That(result.ActiveTurnNumber, Is.EqualTo(14));
            Assert.That(result.ActiveCardStableId, Is.EqualTo(턴마감CardStableIds.Fool));
            Assert.That(result.ActiveEffectCode, Is.EqualTo("BeginnerMind"));
        }

        [Test]
        public void CULTURE_CARD0_Scene은문화카드를포함한여섯행동을보존한다()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(value =>
                    value.name == RootName);
                var panel = root.transform.Find(
                    "PersistentUI/SimulationWorldHud/TurnClosingPanel");

                Assert.That(panel, Is.Not.Null);
                Assert.That(panel!.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(6));
                Assert.That(root.GetComponentInChildren<턴마감Presenter>(true), Is.Not.Null);
                var composition = root.GetComponentInChildren<턴마감SceneCompositionRoot>(true);
                Assert.That(composition, Is.Not.Null);
                Assert.That(composition!.서버기준사용중, Is.True);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public async Task TURN_CARD_HTTP1_ServerRepository는최종확인뒤서버기준Session을다시조회한다()
        {
            var api = new 턴마감ServerStubApiClient();
            var repository = new 턴마감ServerAuthorityRepository(api);
            var created = await repository.서버기준Session확보Async(CancellationToken.None);
            var context = await repository.GetContextAsync(
                created.SessionStableId, CancellationToken.None);
            var preview = await repository.PreviewAsync(
                context.SessionStableId, context.Revision,
                턴마감CardStableIds.SeoulCulture, CancellationToken.None);
            var confirmed = await repository.ConfirmAsync(
                context.SessionStableId, "command:test.server-turn-close",
                context.Revision, 턴마감CardStableIds.SeoulCulture,
                CancellationToken.None);

            Assert.That(api.Requests, Has.Count.EqualTo(5));
            Assert.That(api.Requests[0].Method, Is.EqualTo("POST"));
            Assert.That(api.Requests[0].RelativePath,
                Is.EqualTo("api/simulation/v1/sessions"));
            Assert.That(api.Requests[3].RelativePath,
                Does.EndWith("/turn-closings/confirm"));
            Assert.That(api.Requests[3].JsonBody,
                Does.Contain(턴마감CardStableIds.SeoulCulture));
            Assert.That(api.Requests[4].Method, Is.EqualTo("GET"));
            Assert.That(api.Requests[4].RelativePath,
                Is.EqualTo("api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(context.SessionStableId)));
            Assert.That(preview.BaseRevision, Is.EqualTo(0));
            Assert.That(confirmed.Revision, Is.EqualTo(1));
            Assert.That(confirmed.WorldSnapshot.SourceModeCode,
                Is.EqualTo("SimulationServer"));
            Assert.That(confirmed.ActiveEffectCode,
                Is.EqualTo("LocalContextAwareness"));
            Assert.That(confirmed.WorldSnapshot.RegionalCausality.RecoveryScore,
                Is.EqualTo(1));
            Assert.That(confirmed.WorldSnapshot.RegionalCausality.OutcomeCode,
                Is.EqualTo("Recovery"));
        }

        [Test]
        public async System.Threading.Tasks.Task CULTURE_CARD0_문화카드는근거를보존하고다음턴에만활성화된다()
        {
            var authority = new 턴마감FixtureAuthorityClient();
            var context = await authority.GetContextAsync(
                SimulationWorldShellFixture.SessionStableId, CancellationToken.None);
            var card = context.AvailableCards.Single(value =>
                value.CardStableId == 턴마감CardStableIds.SeoulCulture);

            턴마감FixtureAuthorityClient.ValidateCard(card);
            var result = await authority.ConfirmAsync(
                context.SessionStableId, "command:test.culture-card", context.Revision,
                card.CardStableId, CancellationToken.None);

            Assert.That(card.RegionKey, Is.EqualTo("kr-seoul"));
            Assert.That(card.CalendarRevision,
                Is.EqualTo("simulation-culture-calendar:kr-seoul:2026.r1"));
            Assert.That(card.SourceUrl, Does.StartWith("https://www.mcst.go.kr/"));
            Assert.That(result.ActiveTurnNumber, Is.EqualTo(14));
            Assert.That(result.ActiveEffectCode, Is.EqualTo("LocalContextAwareness"));
        }

        [Test]
        public async System.Threading.Tasks.Task TURN_CARD_UI1B_Presenter는Preview뒤Confirm에서만Shell날짜를바꾼다()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(value =>
                    value.name == RootName);
                var shell = root.GetComponentInChildren<SimulationWorldShellPresenter>(true);
                var presenter = root.GetComponentInChildren<턴마감Presenter>(true);
                Assert.That(shell, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);
                shell!.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                presenter!.SetAuthorityForTests(new 턴마감FixtureAuthorityClient());
                await presenter.LoadAsync();
                presenter.SelectCard(턴마감CardStableIds.Chariot);

                await presenter.PreviewAsync();
                Assert.That(shell.WorldTick, Is.EqualTo(12));
                Assert.That(presenter.HasPreview, Is.True);

                await presenter.ConfirmAsync();
                Assert.That(shell.WorldTick, Is.EqualTo(13));
                Assert.That(shell.WorldRevision, Is.EqualTo(13));
                Assert.That(presenter.Status, Does.Contain("IntegratedProgress"));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private sealed class 턴마감ServerStubApiClient : ISimulationRehearsalUnityApiClient
        {
            private const string SessionId =
                "simulation-session:706a236b17e544e2a070a0785ae42d19";
            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var path = request.RelativePath;
                if (request.Method == "POST" && path == "api/simulation/v1/sessions")
                    return Response(SessionJson(0, false), 201);
                if (request.Method == "GET" && path.EndsWith("/turn-closing-context"))
                    return Response(ContextJson(), 200);
                if (request.Method == "POST" && path.EndsWith("/turn-closing-previews"))
                    return Response(PreviewJson(), 200);
                if (request.Method == "POST" && path.EndsWith("/turn-closings/confirm"))
                    return Response(SessionJson(1, true), 200);
                if (request.Method == "GET" && path == "api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(SessionId))
                    return Response(SessionJson(1, true), 200);
                return Response("{}", 404);
            }

            private static Task<UnityApiResponse> Response(string body, long status)
                => Task.FromResult(new UnityApiResponse
                {
                    StatusCode = status,
                    Body = body,
                    ErrorCode = status < 300 ? string.Empty : "NotFound",
                });

            private static string ContextJson()
                => "{\"SessionStableId\":\"" + SessionId
                    + "\",\"TurnNumber\":1,\"GameDate\":\"2026-04-12T00:00:00Z\""
                    + ",\"Revision\":0,\"PendingTaskCount\":0,\"CanCloseTurn\":true"
                    + ",\"AvailableCards\":[" + CultureCardJson() + "]}";

            private static string PreviewJson()
                => "{\"PreviewStableId\":\"turn-closing:" + SessionId
                    + ":1\",\"BaseRevision\":0,\"ClosingTurnNumber\":1"
                    + ",\"NextTurnNumber\":2,\"NextGameDate\":\"2026-04-13T00:00:00Z\""
                    + ",\"PendingTaskCount\":0,\"SelectedCards\":["
                    + CultureCardJson() + "]}";

            private static string CultureCardJson()
                => "{\"CardStableId\":\"" + 턴마감CardStableIds.SeoulCulture
                    + "\",\"CardRevision\":\"culture-card.fixture-r1\""
                    + ",\"CardKindCode\":\"Culture\",\"Title\":\"서울 생활문화 질문\""
                    + ",\"Summary\":\"현재 경험과 공식 원천을 함께 확인한다.\""
                    + ",\"EffectCode\":\"LocalContextAwareness\""
                    + ",\"TargetStatCode\":\"CommunityInsight\",\"StatDelta\":1"
                    + ",\"SourceStableId\":\"source:kr-regional-culture-promotion-agency\""
                    + ",\"RegionKey\":\"kr-seoul\""
                    + ",\"AvailableFromGameDate\":\"2026-01-01T00:00:00Z\""
                    + ",\"AvailableThroughGameDate\":\"2026-12-31T00:00:00Z\""
                    + ",\"CalendarRevision\":\"simulation-culture-calendar:kr-seoul:2026.r1\""
                    + ",\"EffectRuleRevision\":\"culture-local-context-awareness:r1\""
                    + ",\"SourceUrl\":\"https://www.mcst.go.kr/source\""
                    + ",\"EvidenceCheckedAtUtc\":\"2026-07-26T00:00:00Z\"}";

            private static string SessionJson(long revision, bool active)
                => "{\"SessionStableId\":\"" + SessionId + "\",\"Revision\":" + revision
                    + ",\"WorldContext\":{\"WorldTick\":" + revision
                    + ",\"GameDate\":\"2026-04-" + (12 + revision).ToString("00")
                    + "T00:00:00Z\"},\"Settlement\":{"
                    + "\"SettlementStableId\":\"settlement:sim.border-town-1\""
                    + ",\"TreasuryBalance\":1000000,\"LaborAvailable\":75"
                    + ",\"LaborReserved\":25,\"FoodReserveEquivalent\":1200"
                    + ",\"FoodSecurityDays\":10,\"ActiveTaskStableIds\":[]"
                    + ",\"MarketSupplyByProduct\":[{\"ProductStableId\":\"product:potato\",\"Quantity\":300}]"
                    + ",\"Districts\":[{\"DistrictStableId\":\"district:farm\"},{\"DistrictStableId\":\"district:town\"},{\"DistrictStableId\":\"district:market\"},{\"DistrictStableId\":\"district:storage\"},{\"DistrictStableId\":\"district:logistics\"},{\"DistrictStableId\":\"district:residential\"},{\"DistrictStableId\":\"district:garrison\"},{\"DistrictStableId\":\"district:gate\"}]}"
                    + ",\"RegionalCausality\":{"
                    + "\"Revision\":" + revision + ",\"ThreatScore\":0"
                    + ",\"RecoveryScore\":" + (active ? 1 : 0)
                    + ",\"NetPressureModifier\":" + (active ? -1 : 0)
                    + ",\"OutcomeCode\":\"" + (active ? "Recovery" : "Normal") + "\"}"
                    + ",\"ActiveTurnCardEffects\":" + (active
                        ? "[{\"CardStableId\":\"" + 턴마감CardStableIds.SeoulCulture
                            + "\",\"EffectCode\":\"LocalContextAwareness\",\"ActiveTurnNumber\":2}]"
                        : "[]") + "}";
        }
    }
}
