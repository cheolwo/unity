using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Samples.Farm.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm.Tests.Editor
{
    public sealed class FarmSoilTileGridViewTests
    {
        [SetUp]
        public void SetUp()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void Builder는_6x6토양타일과선택패널을만든다()
        {
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();

            Assert.That(view.ValidateWiring(), Is.True);
            Assert.That(view.CellCount, Is.EqualTo(36));
            Assert.That(view.GetComponentsInChildren<FarmSoilTileCellView>().Length, Is.EqualTo(36));
            Assert.That(view.GetComponentsInChildren<FarmSoilTileActionButtonView>().Length, Is.EqualTo(3));
        }

        [Test]
        public void 초기화는_Simulation표시를적용하고가상선택을만들지않는다()
        {
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();

            view.GetComponent<FarmSoilTileSimulationController>().Initialize();

            Assert.That(view.SelectedTitleText, Is.EqualTo("토양 타일을 선택하세요"));
            Assert.That(view.SelectedDetailText, Does.Contain("작업을 검토"));
        }

        [Test]
        public void 타일선택은_Projector상세와선택Material을적용한다()
        {
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();
            view.GetComponent<FarmSoilTileSimulationController>().Initialize();

            view.SelectTileForTests("farm-soil-tile:sim.potato.0.0");

            Assert.That(view.SelectedTitleText, Is.EqualTo("타일 0,0"));
            Assert.That(view.SelectedDetailText, Does.Contain("밭갈이 Preview 가능"));
            var selected = view.GetComponentsInChildren<FarmSoilTileCellView>()
                .Single(value => value.StableId == "farm-soil-tile:sim.potato.0.0");
            Assert.That(selected.SoilRenderer.sharedMaterial.name, Does.Contain("Selected"));
        }

        [Test]
        public void 선택만으로는_PreviewConfirmTick이발생하지않는다()
        {
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();
            var controller = view.GetComponent<FarmSoilTileSimulationController>();
            controller.Initialize();

            view.SelectTileForTests("farm-soil-tile:sim.potato.0.0");

            Assert.That(controller.CurrentPreview, Is.Null);
            Assert.That(controller.ConfirmedCommand, Is.Null);
            Assert.That(controller.CurrentSnapshot.DataRevision, Is.EqualTo(1));
        }

        [Test]
        public void PreviewConfirmTick은_각각명시적으로호출되어_새Snapshot을Reconcile한다()
        {
            const string tileId = "farm-soil-tile:sim.potato.0.0";
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();
            var controller = view.GetComponent<FarmSoilTileSimulationController>();
            controller.Initialize();
            view.SelectTileForTests(tileId);

            view.RequestTillingPreview();
            Assert.That(controller.CurrentSnapshot.DataRevision, Is.EqualTo(1));
            Assert.That(controller.CurrentPreview, Is.Not.Null);
            Assert.That(view.SelectedDetailText, Does.Contain("명시적 Confirm 필요"));

            view.RequestTillingConfirm();
            Assert.That(controller.CurrentSnapshot.DataRevision, Is.EqualTo(1));
            Assert.That(controller.ConfirmedCommand, Is.Not.Null);
            Assert.That(view.SelectedDetailText, Does.Contain("Tick 대기"));

            view.RequestSimulationTick();
            Assert.That(controller.CurrentSnapshot.DataRevision, Is.EqualTo(2));
            Assert.That(controller.CurrentPreview, Is.Null);
            Assert.That(controller.ConfirmedCommand, Is.Null);
            Assert.That(view.SelectedDetailText, Does.Contain("Tick 반영 완료"));
            var changed = view.GetComponentsInChildren<FarmSoilTileCellView>()
                .Single(value => value.StableId == tileId);
            Assert.That(changed.CultivationStateCode,
                Is.EqualTo(Ssalddel.Unity.Farm.FarmSoilTileCultivationStateCodes.Tilled));
            Assert.That(changed.SoilRenderer.transform.localScale.y, Is.EqualTo(.34f));
            Assert.That(changed.SoilRenderer.transform.localScale.z, Is.EqualTo(.76f));
        }

        [Test]
        public void Confirm과Tick은_선행단계없이는실행되지않는다()
        {
            var view = FarmPrimitiveSceneBuilder.CreateSoilTileGridForTests();
            var controller = view.GetComponent<FarmSoilTileSimulationController>();
            controller.Initialize();
            view.SelectTileForTests("farm-soil-tile:sim.potato.0.0");

            Assert.That(() => view.RequestTillingConfirm(),
                Throws.InvalidOperationException.With.Message.EqualTo("FarmSoilTileTillingPreviewRequired"));
            Assert.That(() => view.RequestSimulationTick(),
                Throws.InvalidOperationException.With.Message.EqualTo("FarmSoilTileTillingConfirmationRequired"));
        }
    }
}
