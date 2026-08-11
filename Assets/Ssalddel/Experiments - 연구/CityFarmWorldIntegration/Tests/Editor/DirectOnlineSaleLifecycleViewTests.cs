using NUnit.Framework;
using Ssalddel.Unity.Farm;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class DirectOnlineSaleLifecycleViewTests
    {
        private DirectOnlineSaleLifecyclePresenter presenter = null!;

        [SetUp]
        public void Open()
        {
            EditorSceneManager.OpenScene(DirectOnlineSaleLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            presenter = GameObject.Find("WorldBootstrap/" + DirectOnlineSaleLifecycleBuilder.RootName)
                .GetComponent<DirectOnlineSaleLifecyclePresenter>();
            presenter.ResetLifecycle();
        }

        [Test]
        public void DIRECT1_View는직판선택과5개Action을보존한다()
        {
            DirectOnlineSaleLifecycleBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + DirectOnlineSaleLifecycleBuilder.RootName);
            Assert.That(root.GetComponentsInChildren<DirectOnlineSaleActionButton>(true), Has.Length.EqualTo(5));
            Assert.That(presenter.CurrentSnapshot.DispositionDecision.ChoiceCode,
                Is.EqualTo(HarvestDispositionChoiceCodes.DirectOnlineSale));
        }

        [Test]
        public void ReviewConfirm은Tick전소포장Lot을만들지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            presenter.ReviewPacking(); presenter.ConfirmPacking();
            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.PackingLot, Is.Null);
        }

        [Test]
        public void Tick은5kg60개와등록후보를표시한다()
        {
            presenter.ReviewPacking(); presenter.ConfirmPacking(); presenter.ApplyTick();
            Assert.That(presenter.CurrentSnapshot.PackingLot!.ParcelCount, Is.EqualTo(60));
            Assert.That(presenter.CurrentSnapshot.ListingCandidate, Is.Not.Null);
            var root = GameObject.Find("WorldBootstrap/" + DirectOnlineSaleLifecycleBuilder.RootName).transform;
            Assert.That(root.Find("ProducerPackingLotMarker_5kgx60").gameObject.activeSelf, Is.True);
            Assert.That(root.Find("OnlineListingDraftMarker").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void 등록초안은비공개이고가격과주문이없다()
        {
            presenter.RunGoldenPath();
            Assert.That(presenter.CurrentListingDraft, Is.Not.Null);
            Assert.That(presenter.CurrentListingDraft!.IsPublished, Is.False);
            Assert.That(presenter.CurrentListingDraft.UnitPrice, Is.Null);
            Assert.That(presenter.CurrentListingDraft.OrderCount, Is.Zero);
            var root = GameObject.Find("WorldBootstrap/" + DirectOnlineSaleLifecycleBuilder.RootName).transform;
            Assert.That(root.Find("OnlineListingDraftMarker").gameObject.activeSelf, Is.True);
        }
    }
}
