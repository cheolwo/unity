using NUnit.Framework;
using Ssalddel.Unity.Farm;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class HarvestDispositionChoiceViewTests
    {
        private HarvestDispositionChoicePresenter presenter = null!;

        [SetUp]
        public void Open()
        {
            EditorSceneManager.OpenScene(HarvestDispositionChoiceBuilder.ScenePath, OpenSceneMode.Single);
            presenter = GameObject.Find("WorldBootstrap/" + HarvestDispositionChoiceBuilder.RootName)
                .GetComponent<HarvestDispositionChoicePresenter>();
            presenter.ResetChoice();
        }

        [Test]
        public void HARVEST_CHOICE1은300kg수확Lot과6개Action을보존한다()
        {
            HarvestDispositionChoiceBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + HarvestDispositionChoiceBuilder.RootName);
            Assert.That(root.GetComponentsInChildren<HarvestDispositionChoiceActionButton>(true), Has.Length.EqualTo(6));
            Assert.That(presenter.CurrentSnapshot.HarvestLot.Quantity, Is.EqualTo(300m));
            Assert.That(presenter.CurrentSnapshot.Options, Has.Length.EqualTo(4));
            Assert.That(presenter.CurrentSnapshot.Options,
                Has.Some.Property("ChoiceCode").EqualTo(HarvestDispositionChoiceCodes.ReserveStorage));
            Assert.That(presenter.IsCardOpen, Is.False);
        }

        [Test]
        public void 수확물상호작용은판로Card만열고Revision을바꾸지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            var lot = GameObject.Find("WorldBootstrap/" + PotatoCultivationLifecycleBuilder.RootName
                + "/HarvestLotMarker_300kg");
            lot.GetComponent<HarvestDispositionInteractable>().Interact();
            Assert.That(presenter.IsCardOpen, Is.True);
            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.Decision, Is.Null);
        }

        [Test]
        public void 온라인직판PreviewConfirm은Tick전판로를확정하지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            presenter.OpenCard();
            presenter.SelectChoice(HarvestDispositionChoiceCodes.DirectOnlineSale);
            presenter.ConfirmChoice();
            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.Decision, Is.Null);
            Assert.That(presenter.CurrentCommand, Is.Not.Null);
        }

        [Test]
        public void Tick은직판포장후보만생성하고나머지경로를활성화하지않는다()
        {
            presenter.RunDirectOnlinePath();
            Assert.That(presenter.CurrentSnapshot.StateCode, Is.EqualTo(HarvestDispositionStateCodes.Decided));
            Assert.That(presenter.CurrentSnapshot.Decision!.ChoiceCode,
                Is.EqualTo(HarvestDispositionChoiceCodes.DirectOnlineSale));
            Assert.That(presenter.CurrentSnapshot.Decision.NextWorkflowCode,
                Is.EqualTo("ProducerPackingCandidate"));
            var root = GameObject.Find("WorldBootstrap/" + HarvestDispositionChoiceBuilder.RootName).transform;
            Assert.That(root.Find("DirectOnlineChoiceMarker").gameObject.activeSelf, Is.True);
            Assert.That(root.Find("CooperativeChoiceMarker").gameObject.activeSelf, Is.False);
            Assert.That(root.Find("ExportAgentChoiceMarker").gameObject.activeSelf, Is.False);
        }
    }
}
