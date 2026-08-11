using NUnit.Framework;
using Ssalddel.Unity.Farm;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class CooperativeIntakeLifecycleViewTests
    {
        private CooperativeIntakeLifecyclePresenter presenter = null!;

        [SetUp]
        public void Open()
        {
            EditorSceneManager.OpenScene(CooperativeIntakeLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            presenter = GameObject.Find("WorldBootstrap/" + CooperativeIntakeLifecycleBuilder.RootName)
                .GetComponent<CooperativeIntakeLifecyclePresenter>();
            presenter.ResetLifecycle();
        }

        [Test]
        public void COOP1_View는조합선택과5개Action을보존한다()
        {
            CooperativeIntakeLifecycleBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + CooperativeIntakeLifecycleBuilder.RootName);
            Assert.That(root.GetComponentsInChildren<CooperativeIntakeActionButton>(true), Has.Length.EqualTo(5));
            Assert.That(presenter.CurrentSnapshot.DispositionDecision.ChoiceCode,
                Is.EqualTo(HarvestDispositionChoiceCodes.CooperativeShipment));
        }

        [Test]
        public void ReviewConfirm은Tick전인수Lot을만들지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            presenter.ReviewIntake();
            presenter.ConfirmIntake();
            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.IntakeLot, Is.Null);
        }

        [Test]
        public void Tick은300kg인수Lot과Cargo준비후보를표시한다()
        {
            presenter.ReviewIntake(); presenter.ConfirmIntake(); presenter.ApplyTick();
            Assert.That(presenter.CurrentSnapshot.IntakeLot!.Quantity, Is.EqualTo(300m));
            Assert.That(presenter.CurrentSnapshot.CargoPreparationCandidate, Is.Not.Null);
            var root = GameObject.Find("WorldBootstrap/" + CooperativeIntakeLifecycleBuilder.RootName).transform;
            Assert.That(root.Find("CooperativeIntakeLotMarker_300kg").gameObject.activeSelf, Is.True);
            Assert.That(root.Find("CargoPreparationCandidateMarker").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Cargo연결은포장검토만열고Package와Cargo를만들지않는다()
        {
            presenter.RunGoldenPath();
            Assert.That(presenter.CurrentCargoPreparation, Is.Not.Null);
            Assert.That(presenter.CurrentCargoPreparation!.PackageLot, Is.Null);
            Assert.That(presenter.CurrentCargoPreparation.Cargo, Is.Null);
            Assert.That(presenter.CurrentModel.CandidateText, Does.Contain("CANDIDATE ONLY"));
        }
    }
}
