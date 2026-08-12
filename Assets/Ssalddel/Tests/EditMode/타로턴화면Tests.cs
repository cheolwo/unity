using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 타로턴화면Tests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public async Task ARC_F3_ServerRepository는_타로3장과_현재객체반응을_별도조회한다()
        {
            var api = new TarotApiStub();
            var repository = new 턴마감ServerAuthorityRepository(api);

            var context = await repository.GetContextAsync(
                TarotApiStub.SessionId, CancellationToken.None);
            var reactions = await repository.Preview타로객체반응Async(
                context.SessionStableId, context.Revision,
                context.TarotDraw.DrawStableId, CancellationToken.None);

            Assert.That(context.TarotDraw.Offers, Has.Length.EqualTo(3));
            Assert.That(context.TarotDraw.Offers.Select(value => value.OfferStableId),
                Is.Unique);
            Assert.That(reactions.IsCandidateOnly, Is.True);
            Assert.That(reactions.DoesNotMutateSession, Is.True);
            Assert.That(reactions.Find("tarot-offer:test.slot-1").HighlightObjectStableIds,
                Is.EqualTo(new[] { "seedbed-object:city.urban-market-building.a" }));
            Assert.That(api.Requests[1].RelativePath,
                Does.EndWith("/tarot-object-reaction-previews"));
        }

        [Test]
        public async Task ARC_F3_선택은_현재영향객체만강조하고_Confirm뒤제거한다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(value =>
                    value.name == "SimulationWorldShell");
                var shell = root.GetComponentInChildren<SimulationWorldShellPresenter>(true);
                var presenter = root.GetComponentInChildren<턴마감Presenter>(true);
                var highlighter = root.GetComponentInChildren<타로객체강조Presenter>(true);
                Assert.That(shell, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(highlighter, Is.Not.Null);

                shell!.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                await presenter!.InitializeAsync(new 턴마감FixtureAuthorityClient());
                await presenter.SelectTarotOfferAsync(1);

                Assert.That(presenter.SelectedTarotOfferStableId,
                    Is.EqualTo("tarot-offer:fixture.turn-13.slot-1"));
                Assert.That(presenter.HighlightedObjectStableIds,
                    Is.EqualTo(new[] { "seedbed-object:city.urban-market-building.a" }));
                Assert.That(highlighter!.HighlightMarkerCount, Is.EqualTo(1));
                Assert.That(presenter.Status, Does.Contain("마감 Preview"));

                await presenter.PreviewAsync();
                Assert.That(shell.WorldRevision, Is.EqualTo(12));
                await presenter.ConfirmAsync();
                Assert.That(shell.WorldRevision, Is.EqualTo(13));
                Assert.That(highlighter.HighlightMarkerCount, Is.EqualTo(0));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public async Task ARC_F3_화면은_서버제안의_정역방향과_기회부담을_한국어로표시한다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(value =>
                    value.name == "SimulationWorldShell");
                var shell = root.GetComponentInChildren<SimulationWorldShellPresenter>(true)!;
                var presenter = root.GetComponentInChildren<턴마감Presenter>(true)!;
                shell.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                await presenter.InitializeAsync(new 턴마감FixtureAuthorityClient());

                var panel = root.transform.Find(
                    "PersistentUI/SimulationWorldHud/TurnClosingPanel")!;
                var labels = panel.GetComponentsInChildren<Button>(true)
                    .Select(value => value.GetComponentInChildren<Text>(true)?.text ?? string.Empty)
                    .ToArray();
                Assert.That(labels, Does.Contain("여제 · 정방향"));
                Assert.That(labels, Does.Contain("절제 · 역방향"));

                await presenter.SelectTarotOfferAsync(3);
                var cardText = panel.Find("CardText").GetComponent<Text>().text;
                Assert.That(cardText, Does.Contain("기회"));
                Assert.That(cardText, Does.Contain("부담"));
                Assert.That(cardText, Does.Contain("역방향"));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private sealed class TarotApiStub : ISimulationRehearsalUnityApiClient
        {
            public const string SessionId = "simulation-session:tarot-ui-test";
            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = request.RelativePath.EndsWith("/turn-closing-context")
                        ? ContextJson()
                        : ReactionJson(),
                });
            }

            private static string ContextJson()
                => "{\"SessionStableId\":\"" + SessionId
                    + "\",\"TurnNumber\":1,\"GameDate\":\"2026-04-12T00:00:00Z\""
                    + ",\"Revision\":0,\"PendingTaskCount\":0,\"CanCloseTurn\":true"
                    + ",\"AvailableCards\":[],\"TarotDraw\":{"
                    + "\"DrawStableId\":\"tarot-draw:test.turn-1\""
                    + ",\"DeckStableId\":\"tarot-deck:starter-12\""
                    + ",\"DeckRevision\":\"tarot-deck:starter-12.r1\""
                    + ",\"DrawRuleRevision\":\"tarot-draw-rule:r1\""
                    + ",\"TurnNumber\":1,\"TurnHistoryHash\":\"history:test\""
                    + ",\"Offers\":[" + OfferJson(1, "empress", "여제", "Upright") + ","
                    + OfferJson(2, "chariot", "전차", "Reversed") + ","
                    + OfferJson(3, "temperance", "절제", "Upright") + "]}}";

            private static string OfferJson(int slot, string key, string title, string orientation)
                => "{\"OfferStableId\":\"tarot-offer:test.slot-" + slot
                    + "\",\"OfferSlotNumber\":" + slot
                    + ",\"CardCopyStableId\":\"tarot-copy:test.slot-" + slot
                    + "\",\"OrientationCode\":\"" + orientation + "\",\"Card\":{"
                    + "\"CardStableId\":\"tarot:major." + key
                    + "\",\"CardRevision\":\"tarot-card-gameplay:r1\""
                    + ",\"CardKindCode\":\"Tarot\",\"Title\":\"" + title
                    + "\",\"Summary\":\"게임용 일반 타로 해석\""
                    + ",\"EffectCode\":\"Effect\",\"TargetStatCode\":\"RuleModifier\""
                    + ",\"SourceStableId\":\"source:tarot-gameplay.r1\"}}";

            private static string ReactionJson()
                => "{\"PreviewStableId\":\"tarot-preview:test\",\"BaseRevision\":0"
                    + ",\"TurnNumber\":1,\"DrawStableId\":\"tarot-draw:test.turn-1\""
                    + ",\"ObjectCatalogRevision\":\"integrated-seedbed:o6.r1\""
                    + ",\"IsCandidateOnly\":true,\"DoesNotMutateSession\":true"
                    + ",\"CardReactions\":["
                    + ReactionCardJson(1, "empress", "Upright", true) + ","
                    + ReactionCardJson(2, "chariot", "Reversed", false) + ","
                    + ReactionCardJson(3, "temperance", "Upright", true) + "]}";

            private static string ReactionCardJson(
                int slot, string card, string orientation, bool highlight)
                => "{\"OfferStableId\":\"tarot-offer:test.slot-" + slot
                    + "\",\"CardStableId\":\"tarot:major." + card
                    + "\",\"OrientationCode\":\"" + orientation
                    + "\",\"ObjectReactions\":[],\"HighlightObjectStableIds\":"
                    + (highlight
                        ? "[\"seedbed-object:city.urban-market-building.a\"]}"
                        : "[]}");
        }
    }
}
