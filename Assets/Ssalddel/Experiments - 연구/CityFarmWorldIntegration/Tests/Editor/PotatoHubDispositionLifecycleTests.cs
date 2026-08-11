using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoHubDispositionLifecycleTests
    {
        private PotatoHubDispositionLifecyclePresenter presenter = null!;

        [SetUp]
        public void Open()
        {
            EditorSceneManager.OpenScene(PotatoHubDispositionLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            presenter = GameObject.Find("WorldBootstrap/" + PotatoHubDispositionLifecycleBuilder.RootName)
                .GetComponent<PotatoHubDispositionLifecyclePresenter>();
            presenter.ResetLifecycle();
        }

        [Test]
        public void HUB2_View는7개Action과검수합격경계를보존한다()
        {
            PotatoHubDispositionLifecycleBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + PotatoHubDispositionLifecycleBuilder.RootName);
            Assert.That(root.GetComponentsInChildren<PotatoHubDispositionActionButton>(true),
                Has.Length.EqualTo(7));
            Assert.That(presenter.CurrentModel.StateCode, Is.EqualTo("AcceptedAtHub"));
        }

        [Test]
        public void SplitPreviewConfirm은Tick전Lot을만들지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            presenter.ReviewSeparation();
            presenter.ConfirmPreview();
            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.AcceptedLot, Is.Null);
            Assert.That(presenter.CurrentSnapshot.RejectedLossLot, Is.Null);
        }

        [Test]
        public void Finish는288kg합격Lot과12kg손실Lot을표시한다()
        {
            presenter.Finish();
            Assert.That(presenter.CurrentModel.StateCode, Is.EqualTo("OutboundCandidate"));
            Assert.That(presenter.CurrentModel.LotsText, Does.Contain("288kg ACCEPTED"));
            Assert.That(presenter.CurrentModel.LotsText, Does.Contain("12kg LOSS"));
            Assert.That(presenter.CurrentModel.CandidateText, Does.Contain("CANDIDATE ONLY"));
        }

        [Test]
        public void GoldenPath는합격Lot만후보경로에연결한다()
        {
            presenter.RunGoldenPath();
            var root = GameObject.Find("WorldBootstrap/" + PotatoHubDispositionLifecycleBuilder.RootName);
            Assert.That(root.transform.Find("HubAcceptedLotMarker_288kg").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("HubRejectedLossLotMarker_12kg").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("CityOutboundCandidateMarker_288kg").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("CandidateRoute_HubCity").gameObject.activeSelf, Is.True);
            Assert.That(presenter.CurrentSnapshot.OutboundCandidate!.SourceStableIds,
                Does.Contain(presenter.CurrentSnapshot.AcceptedLot!.StableId));
            Assert.That(presenter.CurrentSnapshot.OutboundCandidate.SourceStableIds,
                Does.Not.Contain(presenter.CurrentSnapshot.RejectedLossLot!.StableId));
        }
    }
}
