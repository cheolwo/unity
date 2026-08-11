using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PotatoJourney;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoJourneyFarmVerticalSliceTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                PotatoJourneyFarmVerticalSliceBuilder.ScenePath,
                OpenSceneMode.Single);
            GameObject.Find("WorldBootstrap/" + PotatoJourneyFarmVerticalSliceBuilder.RootName)
                .GetComponent<PotatoJourneyFarmSlicePresenter>()
                .ApplyFarmSelection();
        }

        [Test]
        public void PVS5는_Synty감자밭과상자에_서버형Card를연결한다()
        {
            PotatoJourneyFarmVerticalSliceBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + PotatoJourneyFarmVerticalSliceBuilder.RootName);
            var presenter = root.GetComponent<PotatoJourneyFarmSlicePresenter>();

            Assert.That(root.GetComponentsInChildren<Transform>(true)
                .Count(value => value.name == "SyntyPotatoVisual"), Is.GreaterThanOrEqualTo(29));
            Assert.That(presenter.CurrentModel, Is.Not.Null);
            Assert.That(presenter.CurrentModel.CardDeck.Cards, Has.Length.EqualTo(3));
            Assert.That(presenter.CurrentModel.ModeLabel, Is.EqualTo("SIMULATION"));
        }

        [Test]
        public void 밭과상자선택은_Linkage와공간강조를다르게표현한다()
        {
            var presenter = GameObject.Find("WorldBootstrap/" + PotatoJourneyFarmVerticalSliceBuilder.RootName)
                .GetComponent<PotatoJourneyFarmSlicePresenter>();

            presenter.ApplyFarmSelection();
            Assert.That(presenter.CurrentAnchorKind, Is.EqualTo(PotatoJourneyAnchorKindCodes.FarmPlot));
            Assert.That(presenter.CurrentModel.LinkageStatusCode,
                Is.EqualTo(PotatoJourneyLinkageStatusCodes.SimulationLinked));
            Assert.That(presenter.CurrentModel.ShowFarmConditionMarker, Is.True);

            presenter.ApplyCargoSelection();
            Assert.That(presenter.CurrentAnchorKind, Is.EqualTo(PotatoJourneyAnchorKindCodes.FarmYardCargo));
            Assert.That(presenter.CurrentModel.LinkageStatusCode,
                Is.EqualTo(PotatoJourneyLinkageStatusCodes.ProductOnly));
            Assert.That(presenter.CurrentModel.ShowCargoRoute, Is.False);
        }

        [Test]
        public void Slice는_읽기전용이며_Command나운영완료를소유하지않는다()
        {
            var root = GameObject.Find("WorldBootstrap/" + PotatoJourneyFarmVerticalSliceBuilder.RootName);
            var names = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(value => value != null)
                .Select(value => value.GetType().Name).ToArray();

            Assert.That(names.Any(value => value.Contains("Command")), Is.False);
            Assert.That(names.Any(value => value.Contains("Confirm")), Is.False);
            Assert.That(root.GetComponent<PotatoJourneyFarmSlicePresenter>().CurrentModel.ShowCargoRoute,
                Is.False);
        }
    }
}
