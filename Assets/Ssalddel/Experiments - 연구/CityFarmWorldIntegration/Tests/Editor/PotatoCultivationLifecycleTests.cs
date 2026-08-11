using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Farm;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoCultivationLifecycleTests
    {
        private PotatoCultivationLifecyclePresenter presenter = null!;

        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                PotatoCultivationLifecycleBuilder.ScenePath,
                OpenSceneMode.Single);
            presenter = GameObject.Find("WorldBootstrap/" + PotatoCultivationLifecycleBuilder.RootName)
                .GetComponent<PotatoCultivationLifecyclePresenter>();
            presenter.ResetLifecycle();
        }

        [Test]
        public void FARM3_View는_GameCalendar_Source_Limitation과8개명시적Action을보존한다()
        {
            PotatoCultivationLifecycleBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + PotatoCultivationLifecycleBuilder.RootName);

            Assert.That(presenter.ValidateWiring(), Is.True);
            Assert.That(presenter.CurrentModel.SimulationDateText, Is.EqualTo("2026-04-01"));
            Assert.That(presenter.CurrentModel.SourceModeCode, Is.EqualTo("Simulation/Fixture"));
            Assert.That(presenter.CurrentModel.LimitationText, Does.Contain("실제 파종·수확 권고"));
            Assert.That(root.GetComponentsInChildren<PotatoCultivationLifecycleActionButton>(true),
                Has.Length.EqualTo(8));
            var baseRoot = GameObject.Find("WorldBootstrap/"
                + PotatoJourneyFarmVerticalSliceBuilder.RootName);
            var texts = baseRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            Assert.That(texts.Single(value => value.name == "SelectionTitle").text,
                Is.EqualTo("POTATO IDENTITY · PRICE EVIDENCE"));
            Assert.That(texts.Single(value => value.name == "ModeBadge").text,
                Is.EqualTo("SERVER PRODUCT DATA · READ ONLY"));
        }

        [Test]
        public void 파종Preview와Confirm은_Tick전Snapshot과작물크기를바꾸지않는다()
        {
            var revision = presenter.CurrentSnapshot.DataRevision;
            var scale = PotatoPlants().First().localScale;

            presenter.ReviewSowing();
            presenter.ConfirmPreview();

            Assert.That(presenter.CurrentSnapshot.DataRevision, Is.EqualTo(revision));
            Assert.That(presenter.CurrentSnapshot.Cultivation, Is.Null);
            Assert.That(PotatoPlants().First().localScale, Is.EqualTo(scale));
            Assert.That(presenter.CurrentCommand, Is.Not.Null);
        }

        [Test]
        public void 파종Tick과날짜진행은_Sown에서HarvestReady까지작물을성장시킨다()
        {
            presenter.ReviewSowing();
            presenter.ConfirmPreview();
            presenter.ApplyConfirmedTick();
            var sownScale = PotatoPlants().First().localScale.x;

            presenter.AdvanceToHarvestReady();
            var readyScale = PotatoPlants().First().localScale.x;

            Assert.That(presenter.CurrentSnapshot.Cultivation, Is.Not.Null);
            Assert.That(presenter.CurrentModel.GrowthStageCode,
                Is.EqualTo(재배생육단계Codes.HarvestReady));
            Assert.That(readyScale, Is.GreaterThan(sownScale));
            Assert.That(presenter.CurrentModel.CanPreviewHarvest, Is.True);
        }

        [Test]
        public void GoldenPath는_감자를숨기고300kgHarvestLot과상자를표시한다()
        {
            presenter.RunGoldenPathToHarvest();
            var cargo = GameObject.Find("WorldBootstrap/"
                + PotatoJourneyFarmVerticalSliceBuilder.RootName
                + "/FarmYardCargoAnchor_Potato");
            var marker = GameObject.Find("WorldBootstrap/"
                + PotatoCultivationLifecycleBuilder.RootName
                + "/HarvestLotMarker_300kg");

            Assert.That(presenter.CurrentModel.GrowthStageCode,
                Is.EqualTo(재배생육단계Codes.Harvested));
            Assert.That(presenter.CurrentSnapshot.HarvestLot, Is.Not.Null);
            Assert.That(presenter.CurrentSnapshot.HarvestLot.Quantity, Is.EqualTo(300m));
            Assert.That(PotatoPlants().All(value => !value.gameObject.activeSelf), Is.True);
            Assert.That(cargo.activeSelf, Is.True);
            Assert.That(marker.activeSelf, Is.True);
        }

        private static Transform[] PotatoPlants()
            => GameObject.Find("WorldBootstrap/"
                    + PotatoJourneyFarmVerticalSliceBuilder.RootName
                    + "/FarmPlotAnchor_Potato")
                .GetComponentsInChildren<Transform>(true)
                .Where(value => value.name == "SyntyPotatoVisual")
                .ToArray();
    }
}
