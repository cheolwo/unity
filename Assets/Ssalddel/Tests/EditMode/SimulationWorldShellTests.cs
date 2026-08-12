using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class SimulationWorldShellTests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public void WorldMap과Settlement전환은_같은Snapshot을유지한다()
        {
            var snapshot = SimulationWorldShellFixture.CreateSnapshot();
            var shell = new SimulationWorldShellStateMachine(snapshot);

            shell.ShowSettlement(SimulationWorldShellFixture.SettlementStableId);
            shell.ShowWorldMap();
            shell.ShowSettlement(SimulationWorldShellFixture.SettlementStableId);

            Assert.That(shell.Snapshot, Is.SameAs(snapshot));
            Assert.That(shell.Snapshot.WorldTick, Is.EqualTo(12));
            Assert.That(shell.Snapshot.WorldRevision, Is.EqualTo(12));
            Assert.That(shell.State.ObservationScaleCode,
                Is.EqualTo(SimulationObservationScaleCodes.Settlement));
            Assert.That(shell.State.SelectedSettlementStableId,
                Is.EqualTo(SimulationWorldShellFixture.SettlementStableId));
        }

        [Test]
        public void 새Snapshot은_존재하는선택만보존한다()
        {
            var shell = new SimulationWorldShellStateMachine(
                SimulationWorldShellFixture.CreateSnapshot());
            shell.ShowSettlement(SimulationWorldShellFixture.SettlementStableId);
            shell.ShowDistrict("district:farm");
            shell.ShowObject("harvest-lot:potato-001");

            shell.ApplySnapshot(SnapshotWithoutFarmObject(13));

            Assert.That(shell.State.ObservationScaleCode,
                Is.EqualTo(SimulationObservationScaleCodes.District));
            Assert.That(shell.State.SelectedSettlementStableId,
                Is.EqualTo(SimulationWorldShellFixture.SettlementStableId));
            Assert.That(shell.State.SelectedDistrictStableId, Is.EqualTo("district:farm"));
            Assert.That(shell.State.SelectedObjectStableId, Is.Empty);
        }

        [Test]
        public void 낮은Revision은_같은Session에서거부한다()
        {
            var shell = new SimulationWorldShellStateMachine(
                SimulationWorldShellFixture.CreateSnapshot());

            var error = Assert.Throws<InvalidOperationException>(() =>
                shell.ApplySnapshot(SnapshotWithoutFarmObject(11)));

            Assert.That(error!.Message, Is.EqualTo("SimulationWorldSnapshotRevisionRegressed"));
        }

        [Test]
        public void Back은_Object에서District와Settlement를거쳐WorldMap으로돌아간다()
        {
            var shell = new SimulationWorldShellStateMachine(
                SimulationWorldShellFixture.CreateSnapshot());
            shell.ShowSettlement(SimulationWorldShellFixture.SettlementStableId);
            shell.ShowDistrict("district:farm");
            shell.ShowObject("harvest-lot:potato-001");

            shell.Back();
            Assert.That(shell.State.ObservationScaleCode,
                Is.EqualTo(SimulationObservationScaleCodes.District));
            Assert.That(shell.State.SelectedDistrictStableId, Is.EqualTo("district:farm"));
            Assert.That(shell.State.SelectedObjectStableId, Is.Empty);

            shell.Back();
            Assert.That(shell.State.ObservationScaleCode,
                Is.EqualTo(SimulationObservationScaleCodes.Settlement));
            Assert.That(shell.State.SelectedSettlementStableId,
                Is.EqualTo(SimulationWorldShellFixture.SettlementStableId));
            Assert.That(shell.State.SelectedDistrictStableId, Is.Empty);

            shell.Back();
            Assert.That(shell.State.ObservationScaleCode,
                Is.EqualTo(SimulationObservationScaleCodes.WorldMap));
            Assert.That(shell.State.SelectedSettlementStableId,
                Is.EqualTo(SimulationWorldShellFixture.SettlementStableId));
        }

        [Test]
        public void Fixture는_정착지경제값을명시적으로제공한다()
        {
            var snapshot = SimulationWorldShellFixture.CreateSnapshot();

            Assert.That(snapshot.SourceModeCode, Is.EqualTo("SimulationFixture"));
            Assert.That(snapshot.SettlementCount, Is.EqualTo(1));
            Assert.That(snapshot.Treasury, Is.EqualTo(12500m));
            Assert.That(snapshot.LaborAvailable, Is.EqualTo(18m));
            Assert.That(snapshot.LaborReserved, Is.EqualTo(6m));
            Assert.That(snapshot.FoodSecurityDays, Is.EqualTo(12.94m));
        }

        [Test]
        public void 저장Scene은_두Surface와여덟DistrictSocket을가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var map = root.transform.Find("WorldMapRoot");
                var settlement = root.transform.Find("SettlementInteriorRoot");
                Assert.That(map, Is.Not.Null);
                Assert.That(settlement, Is.Not.Null);
                Assert.That(map!.Find("SettlementMarkers/Settlement_1"), Is.Not.Null);
                var districts = settlement!.Find("Districts");
                Assert.That(districts, Is.Not.Null);
                var views = districts!.GetComponentsInChildren<SimulationWorldDistrictView>(true);
                Assert.That(views, Has.Length.EqualTo(8));
                Assert.That(Array.FindAll(views, view => view.PresentationPlaceholder),
                    Has.Length.EqualTo(2));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 저장Scene은_PlayerCameraRig계층과_InputSystem전략카메라를가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var playerRig = root.transform.Find("CameraSystem/PlayerCameraRig");
                var pivot = playerRig?.Find("CameraPivot");
                var camera = pivot?.Find("Main Camera");
                var controller = playerRig?.GetComponent<전략카메라Controller>();

                Assert.That(playerRig, Is.Not.Null);
                Assert.That(pivot, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera!.CompareTag("MainCamera"), Is.True);
                Assert.That(controller, Is.Not.Null);
                Assert.DoesNotThrow(() => controller!.ValidateConfiguration());
                Assert.That(controller!.WorldMinimum, Is.EqualTo(new Vector2(-65f, -50f)));
                Assert.That(controller.WorldMaximum, Is.EqualTo(new Vector2(65f, 50f)));
                Assert.That(controller.MinimumZoomDistance, Is.EqualTo(12f));
                Assert.That(controller.MaximumZoomDistance, Is.EqualTo(110f));
                Assert.That(UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(),
                    Is.Not.Null);
                Assert.That(UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.StandaloneInputModule>(),
                    Is.Null);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 저장Scene은_WorldSettlementDistrictObjectNavigationTarget을가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var targets = root.GetComponentsInChildren<SimulationWorldNavigationTargetView>(true);
                Assert.That(targets, Has.Length.EqualTo(11));
                Assert.That(targets.Any(value =>
                    value.ObjectStableId == 물류이동Fixture.CargoStableId), Is.True);
                Assert.That(Array.FindAll(targets, target =>
                    target.ObservationScaleCode == SimulationObservationScaleCodes.Settlement),
                    Has.Length.EqualTo(1));
                Assert.That(Array.FindAll(targets, target =>
                    target.ObservationScaleCode == SimulationObservationScaleCodes.District),
                    Has.Length.EqualTo(8));
                var objectTarget = Array.Find(targets,
                    target => target.ObjectStableId == "harvest-lot:potato-001");
                Assert.That(objectTarget, Is.Not.Null);
                Assert.DoesNotThrow(() => objectTarget!.Validate());
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PresenterNavigation은_같은TickRevision에서Zone과ObjectFocus를전환한다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var presenter = root.GetComponentInChildren<SimulationWorldShellPresenter>(true);
                var targets = root.GetComponentsInChildren<SimulationWorldNavigationTargetView>(true);
                presenter.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                var farm = Array.Find(targets,
                    target => target.DistrictStableId == "district:farm"
                        && string.IsNullOrEmpty(target.ObjectStableId));
                var lot = Array.Find(targets,
                    target => target.ObjectStableId == "harvest-lot:potato-001");

                presenter.ShowSettlement();
                presenter.NavigateTo(farm!);
                Assert.That(presenter.ObservationScaleCode,
                    Is.EqualTo(SimulationObservationScaleCodes.District));
                Assert.That(presenter.CurrentFocusAnchorId,
                    Is.EqualTo(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:farm"));
                presenter.NavigateTo(lot!);
                Assert.That(presenter.ObservationScaleCode,
                    Is.EqualTo(SimulationObservationScaleCodes.Object));
                Assert.That(presenter.CurrentFocusAnchorId,
                    Is.EqualTo(SimulationWorldShellPresenter.ObjectFocusAnchorPrefix
                        + "harvest-lot:potato-001"));
                Assert.That(presenter.WorldTick, Is.EqualTo(12));
                Assert.That(presenter.WorldRevision, Is.EqualTo(12));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 저장Scene은_District역할별SyntyVisual과VendorPrefab연결을가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var settlement = root.transform.Find("SettlementInteriorRoot");
                var visuals = settlement!.GetComponentsInChildren<WorldVisualInstanceView>(true);
                Assert.That(visuals.Length, Is.GreaterThanOrEqualTo(45));
                Assert.That(visuals, Has.All.Matches<WorldVisualInstanceView>(value =>
                    value.ValidateWiring()
                    && PrefabUtility.GetCorrespondingObjectFromSource(value.PrefabInstanceRoot) != null));

                AssertDistrictVisual(settlement, "FarmDistrict", FarmVisualKeys.Barn, FarmVisualKeys.PotatoLarge);
                AssertDistrictVisual(settlement, "MarketDistrict", UrbanVisualKeys.MarketBuilding,
                    FarmVisualKeys.ProduceStand);
                AssertDistrictVisual(settlement, "StorageDistrict", UrbanVisualKeys.LogisticsBuilding,
                    UrbanVisualKeys.Pallet);
                AssertDistrictVisual(settlement, "ResidentialDistrict", UrbanVisualKeys.Apartment);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 저장Scene의시간대는_고정오후Presentation이며SimulationClock을변경하지않는다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Find(scene, "SimulationWorldShell");
                var time = root.GetComponent<월드시간대Presenter>();
                Assert.That(time, Is.Not.Null);
                Assert.That(time!.ValidateWiring(), Is.True);
                Assert.That(time.SourceMode, Is.EqualTo(월드시간대SourceMode.FixedReference));
                Assert.That(time.AutoCycleInPlayMode, Is.False);
                Assert.That(time.NormalizedTime, Is.EqualTo(15f / 24f).Within(.001f));
                Assert.That(time.SurfaceBindingCount, Is.GreaterThanOrEqualTo(45));

                var presenter = root.GetComponentInChildren<SimulationWorldShellPresenter>(true);
                presenter.Initialize(SimulationWorldShellFixture.CreateSnapshot());
                time.ApplyNowForTests(18.5f / 24f);
                Assert.That(presenter.WorldTick, Is.EqualTo(12));
                Assert.That(presenter.WorldRevision, Is.EqualTo(12));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static SimulationWorldShellSnapshot SnapshotWithoutFarmObject(long revision)
            => new(
                SimulationWorldShellFixture.SessionStableId,
                revision,
                13,
                "Year 1 · 04-13",
                12500m,
                18m,
                6m,
                420m,
                980m,
                12.94m,
                2,
                "SimulationFixture",
                new[]
                {
                    new SimulationWorldSettlementNode(
                        SimulationWorldShellFixture.SettlementStableId,
                        new[]
                        {
                            new SimulationWorldDistrictNode("district:farm", Array.Empty<string>()),
                        }),
                });

        private static GameObject Find(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == rootName) return root;
            throw new InvalidOperationException("SceneRootMissing:" + rootName);
        }

        private static void AssertDistrictVisual(
            Transform settlement,
            string districtName,
            params string[] visualKeys)
        {
            var district = settlement.Find("Districts/" + districtName);
            Assert.That(district, Is.Not.Null);
            var keys = district!.GetComponentsInChildren<WorldVisualInstanceView>(true)
                .Select(value => value.VisualKey)
                .ToArray();
            foreach (var visualKey in visualKeys)
                Assert.That(keys, Does.Contain(visualKey), districtName + " missing " + visualKey);
        }
    }
}
