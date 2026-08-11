using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoJourneyHubRouteTests
    {
        [SetUp]
        public void SetUp()
        {
            PotatoJourneyHubRouteBuilder.Build();
            EditorSceneManager.OpenScene(PotatoJourneyHubRouteBuilder.ScenePath, OpenSceneMode.Single);
        }

        [Test]
        public void WORLD6는_CARGO1Identity와SimulationRoute경계를함께보인다()
        {
            var presenter = GameObject.Find("WorldBootstrap/" + PotatoJourneyHubRouteBuilder.RootName)
                .GetComponent<PotatoJourneyHubRoutePresenter>();
            presenter.ApplyProjection();

            Assert.IsTrue(presenter.CurrentModel.IsVisible);
            Assert.AreEqual("SIMULATION", presenter.CurrentModel.ModeLabel);
            Assert.AreEqual("Loaded", presenter.CurrentModel.HandoffStateCode);
            Assert.AreEqual(15, presenter.CurrentModel.PackageCount);
            Assert.AreEqual(300m, presenter.CurrentModel.Quantity);
            Assert.IsTrue(presenter.CurrentModel.LineageText.Contains(presenter.CurrentModel.CargoStableId));
            Assert.IsTrue(presenter.ValidateWiring());
        }

        [Test]
        public void Van이동은_Presentation경로만변경한다()
        {
            var presenter = Object.FindAnyObjectByType<PotatoJourneyHubRoutePresenter>();
            presenter.ApplyProjection();
            var before = presenter.RouteFollower.transform.position;

            presenter.RouteFollower.TickPresentation(1f);

            Assert.AreNotEqual(before, presenter.RouteFollower.transform.position);
            Assert.AreEqual("cargo:sim.potato.20260407.r3", presenter.CurrentModel.CargoStableId);
        }

        [Test]
        public void 저장Scene은_dirty가아니며_Command를포함하지않는다()
        {
            PotatoJourneyHubRouteBuilder.ValidateOpenScene();

            Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
        }
    }
}
