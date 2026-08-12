using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합전시관ScenePlacementTests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string SceneStableId = "scene:simulation-world-shell";
        private const string HarvestBoxPlacementStableId =
            "scene-placement:simulation-world-shell.farm.potato-harvest-box.a";
        private const string HubGatePlacementStableId =
            "scene-placement:simulation-world-shell.logistics.hub-inbound-gate.a";
        private const string DeliveryTruckPlacementStableId =
            "scene-placement:simulation-world-shell.logistics.delivery-truck.a";
        private const string CargoPalletPlacementStableId =
            "scene-placement:simulation-world-shell.logistics.cargo-pallet.a";
        private const string FarmPalletCratePlacementStableId =
            "scene-placement:simulation-world-shell.farm.pallet-crate.a";
        private const string UrbanMarketShopPlacementStableId =
            "scene-placement:simulation-world-shell.market.urban-market-shop.a";
        private const string GroupingCartTablePlacementStableId =
            "scene-placement:simulation-world-shell.town.grouping-cart-table.a";

        [Test]
        public void 감자수확상자는_SimulationWorldShellFarm에_개별Placement로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == HarvestBoxPlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId,
                    Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:farm"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey, Is.EqualTo("farm.harvest-lot.potato-001"));
                Assert.That(placement.DataBindingKey, Is.EqualTo("HarvestLot:harvest-lot:potato-001"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:farm.potato-harvest-box.a"));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void Hub입고Gate는_SimulationWorldShell물류구역에_화물인수Binding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == HubGatePlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:logistics"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey, Is.EqualTo("logistics.hub.inbound-gate"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("HubReceiving:hub-receiving:sim.potato"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:town.hub-inbound-gate.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "Entry", "Exit", "Vehicle", "Cargo" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void 화물배송차량은_SimulationWorldShell물류구역에_CargoJourneyBinding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == DeliveryTruckPlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:logistics"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey,
                    Is.EqualTo("logistics.cargo-journey.delivery-truck"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("CargoJourney:cargo-journey:sim.potato.farm-hub"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:town.delivery-truck.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "Driver", "Cargo", "RouteEntry", "RouteExit" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void 공용화물Pallet은_물류구역의_WarehouseHandoffBinding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == CargoPalletPlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:logistics"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey,
                    Is.EqualTo("logistics.warehouse-handoff.cargo-pallet"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("WarehouseHandoff:cargo-handoff:sim.potato.20260407.r3.inbound-91"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:shared.cargo-pallet.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "Cargo", "Forklift" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void 농장출하PalletCrate는_FarmOutbound의_HarvestCargoBinding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == FarmPalletCratePlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:farm"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey,
                    Is.EqualTo("farm.outbound.pallet-crate"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("CanonicalProductHarvestCargo:cargo:sim.potato.20260407.r3"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:farm.pallet-crate.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "HarvestCargo", "Vehicle", "HubHandoff" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void 도심마트Shop은_MarketDistrict의_공개상품Binding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == UrbanMarketShopPlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:market"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey, Is.EqualTo("market.public-products.shop"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("MartPublicProduct:mart-product:sim.potato.public"));
                Assert.That(placement.DataBindingKey, Does.Not.Contain("MarketInventory"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:city.urban-market-building.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "Entry", "PublicProduct", "DemandSignal" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void 집단수요CartTable은_TownDistrict의_개인정보제거PreviewBinding으로연결된다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Additive);
            try
            {
                var placements = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<통합전시관ScenePlacementView>(true))
                    .ToArray();
                Assert.That(placements, Has.Length.EqualTo(7));
                var placement = placements.Single(value =>
                    value.PlacementStableId == GroupingCartTablePlacementStableId);
                Assert.That(placement.ValidateWiring(), Is.True);
                Assert.That(placement.SceneStableId, Is.EqualTo(SceneStableId));
                Assert.That(placement.ZoneStableId, Is.EqualTo("district:town"));
                Assert.That(placement.PlacementProfileRevision, Is.EqualTo("r1"));
                Assert.That(placement.SceneAnchorKey,
                    Is.EqualTo("town.orderer-group.grouping-cart-table"));
                Assert.That(placement.DataBindingKey,
                    Is.EqualTo("GroupingPreview:grouping-preview:sim.potato.town"));
                Assert.That(placement.DataBindingKey, Does.Not.Contain("IndividualIntent"));
                Assert.That(placement.DataBindingKey, Does.Not.Contain("DomainCommand"));
                Assert.That(placement.ObjectRoot.ObjectStableId,
                    Is.EqualTo("seedbed-object:town.grouping-cart-table.a"));
                Assert.That(placement.ObjectRoot.Sockets.Select(value => value.SocketCode),
                    Is.SupersetOf(new[] { "IntentInput", "AggregateOutput", "ConsentBoundary" }));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(
                    placement.ObjectRoot.gameObject), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
