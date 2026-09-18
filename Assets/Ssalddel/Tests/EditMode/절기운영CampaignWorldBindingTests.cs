using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Campaigns;
using Ssalddel.Unity.Cards;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 절기운영CampaignWorldBindingTests
    {
        [Test]
        public async Task ApiAdapter는_인증GET만RemoteHost에전달한다()
        {
            var api = new RecordingApiClient();
            var adapter = new 절기운영CampaignApiAdapter(api);

            var body = await adapter.GetAsync(
                "api/simulation/v1/sessions/session-1/seasonal-operations-campaign",
                allowNotFound: false,
                requiresAuthentication: true,
                CancellationToken.None);

            Assert.That(body, Is.EqualTo("{}"));
            Assert.That(api.Request.Method, Is.EqualTo("GET"));
            Assert.That(api.Request.RequiresAuthentication, Is.True);
            Assert.That(api.Request.JsonBody, Is.Empty);
        }

        [Test]
        public void WorldView는_읽기전용화면모델만보관하고_Clear한다()
        {
            var root = new GameObject("SeasonalCampaignWorldViewTest");
            try
            {
                var view = root.AddComponent<절기운영CampaignWorldView>();
                view.ApplySeasonalCampaign(new SeasonalCampaignScreenModel
                {
                    SessionStableId = "session:1",
                    AreaStableId = "region:1",
                    CampaignStableId = "campaign:1",
                    CurrentPhaseCode = "LunchPeak",
                    CampaignRevision = 4,
                    ActionsDisabled = true,
                    PresentationOnly = true,
                },
                new[]
                {
                    new SeasonalCampaignSourceDiagnostic
                    {
                        SourceCode =
                            SeasonalCampaignObservationSourceCodes.Operations,
                        FreshnessCode = SeasonalCampaignSourceFreshnessCodes
                            .RefreshDelayed,
                    },
                }, Array.Empty<SeasonalCampaignCompositionRevision>());

                Assert.That(view.SessionStableId, Is.EqualTo("session:1"));
                Assert.That(view.CurrentPhaseCode, Is.EqualTo("LunchPeak"));
                Assert.That(view.CampaignRevision, Is.EqualTo(4));
                Assert.That(view.ActionsDisabled, Is.True);
                Assert.That(view.SourceSummary,
                    Does.Contain("Operations:RefreshDelayed"));

                view.Clear();
                Assert.That(view.CampaignStableId, Is.Empty);
                Assert.That(view.CampaignRevision, Is.Zero);
                Assert.That(view.SourceSummary, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 카드서랍은_적용한원천판본사본을_관찰결속에알린다()
        {
            var root = new GameObject("SeasonalCampaignCardSourceTest");
            try
            {
                var presenter = root.AddComponent<카드서랍Presenter>();
                CardWorkspaceSnapshot observed = null;
                presenter.SnapshotApplied += snapshot => observed = snapshot;
                var source = new CardWorkspaceSnapshot
                {
                    PresentationOnly = true,
                    SourceRevisions = new[]
                    {
                        new CardWorkspaceFamilyRevision
                        {
                            FamilyCode = CardFamilyCodes.Tarot,
                            SourceRevision = 3,
                        },
                    },
                };

                presenter.Apply(source);

                Assert.That(observed, Is.SameAs(source));
                Assert.That(presenter.CurrentSnapshot, Is.SameAs(source));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class RecordingApiClient
            : ISimulationRehearsalUnityApiClient
        {
            public UnityApiRequest Request { get; private set; }

            public Task<UnityApiResponse> SendAsync(UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = "{}",
                });
            }
        }
    }
}
