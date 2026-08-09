using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Samples.Farm;
using Ssalddel.Unity.Samples.ResidentialPickup;
using Ssalddel.Unity.Samples.UrbanLogisticsCenter;
using Ssalddel.Unity.Samples.UrbanMarket;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class CityFarmBusinessViewIntegrationTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                CityFarmBusinessViewIntegrationBuilder.ScenePath,
                OpenSceneMode.Single);
        }

        [Test]
        public void ReloadedScene_ReusesAllFourExistingBusinessViews()
        {
            CityFarmBusinessViewIntegrationBuilder.ValidateOpenScene();

            Assert.That(Object.FindFirstObjectByType<FarmSoilTileGridView>().CellCount, Is.EqualTo(36));
            Assert.That(Object.FindFirstObjectByType<LogisticsFacilityOverviewView>().AreaCount, Is.EqualTo(4));
            Assert.That(Object.FindObjectsByType<도심마트ManagerShelfView>(
                FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(Object.FindFirstObjectByType<ResidentialPickupPointView>().StableId,
                Is.EqualTo("pickup-point:residential:sample-1"));
        }

        [Test]
        public void FarmSelection_SurvivesSceneSerializationAndUsesStableId()
        {
            var grid = Object.FindFirstObjectByType<FarmSoilTileGridView>();
            var selected = string.Empty;
            grid.TileSelected += value => selected = value;

            grid.RebindSelection();
            grid.SelectTileForTests("farm-soil-tile:sim.potato.4.3");

            Assert.That(selected, Is.EqualTo("farm-soil-tile:sim.potato.4.3"));
        }

        [Test]
        public void MarketShelfAndConceptCardSelection_ArePresentationOnly()
        {
            var coordinator = Object.FindFirstObjectByType<WorldBusinessPresentationCoordinator>();
            var selection = Object.FindFirstObjectByType<WorldSelectionEvidenceView>();
            coordinator.InitializePresentation();
            var potato = Object.FindObjectsByType<도심마트ManagerShelfView>(FindObjectsSortMode.None)
                .Single(value => value.PresentationStableId.EndsWith("potato", StringComparison.Ordinal));
            potato.GetComponentInChildren<InteractionSocket>(true).SelectForTests();

            Assert.That(selection.SelectedStableId, Is.EqualTo("market-shelf:potato"));

            var deck = Object.FindFirstObjectByType<ConceptCardDeckView>(FindObjectsInactive.Include);
            deck.Show();
            var target = deck.GetComponentsInChildren<ConceptCardView>(true)
                .Select(value => value.PresentationStableId)
                .Last(value => !string.IsNullOrWhiteSpace(value));
            Assert.That(coordinator.SelectCardForTests(target), Is.True);
            Assert.That(deck.SelectedCardStableId, Is.EqualTo(target));
        }

        [Test]
        public void PrimitiveFallback_DoesNotReplaceBusinessViewOrStableId()
        {
            var cell = Object.FindObjectsByType<FarmSoilTileCellView>(FindObjectsSortMode.None)
                .Single(value => value.StableId == "farm-soil-tile:sim.potato.2.2");
            var stableId = cell.StableId;
            var fallback = cell.transform.parent.GetComponent<WorldPresentationFallbackView>();

            fallback.UsePrimitiveFallback(true);

            Assert.That(fallback.IsUsingPrimitiveFallback, Is.True);
            Assert.That(fallback.SyntyVisualRoot.gameObject.activeSelf, Is.False);
            Assert.That(cell.StableId, Is.EqualTo(stableId));
            Assert.That(cell.ValidateWiring(), Is.True);

            fallback.UsePrimitiveFallback(false);
            Assert.That(fallback.SyntyVisualRoot.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void SceneContainsNoSimulationTickOrLifetimeScopeAuthority()
        {
            Assert.That(Object.FindObjectsByType<FarmSoilTileSimulationController>(
                FindObjectsSortMode.None), Is.Empty);
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(behaviours.Any(value => value.GetType().Name.Contains(
                "LifetimeScope", StringComparison.Ordinal)), Is.False);
        }
    }
}
