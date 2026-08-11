using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PotatoJourney;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoJourneyCityTests
    {
        [OneTimeSetUp]
        public void BuildScene() => PotatoJourneyCityBuilder.Build();

        [Test]
        public void City카드는_KAMIS관측가와판매가를분리한다()
        {
            var root = GameObject.Find("WorldBootstrap/" + PotatoJourneyCityBuilder.RootName);
            var model = root.GetComponent<PotatoJourneyCityPresenter>().CurrentModel;

            Assert.IsTrue(model.IsVisible);
            StringAssert.Contains("KRW/kg", model.ObservedPriceText);
            StringAssert.Contains("20kg box", model.SalePriceText);
            StringAssert.Contains("not this store's sale price", model.PriceSeparationText);
        }

        [Test]
        public void City공개수량은_ProjectedSaleAvailability로만표시한다()
        {
            var root = GameObject.Find("WorldBootstrap/" + PotatoJourneyCityBuilder.RootName);
            var model = root.GetComponent<PotatoJourneyCityPresenter>().CurrentModel;

            Assert.AreEqual(PotatoJourneyCityQuantityMeaningCodes.ProjectedSaleAvailability,
                model.QuantityMeaningCode);
            Assert.IsFalse(model.AvailabilityText.Contains("physical", System.StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void PVS7Scene은_Command를포함하지않고Simulation경계를표시한다()
        {
            EditorSceneManager.OpenScene(PotatoJourneyCityBuilder.ScenePath);
            PotatoJourneyCityBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + PotatoJourneyCityBuilder.RootName);
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

            Assert.IsFalse(behaviours.Any(value => value.GetType().Name.Contains("Command")));
            Assert.AreEqual("SIMULATION", root.GetComponent<PotatoJourneyCityPresenter>().CurrentModel.ModeLabel);
        }
    }
}
