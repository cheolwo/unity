using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class CityFarmCargoJourneyTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                CityFarmCargoJourneyBuilder.ScenePath,
                OpenSceneMode.Single);
        }

        [Test]
        public void ReloadedScene_PreservesOneCargoAcrossFourZoneAnchors()
        {
            CityFarmCargoJourneyBuilder.ValidateOpenScene();
            var view = Object.FindFirstObjectByType<CargoJourneyView>();
            var anchors = Object.FindObjectsByType<CargoJourneyAnchorView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(view.CargoStableId, Is.EqualTo(CityFarmCargoJourneyBuilder.CargoStableId));
            Assert.That(anchors.Length, Is.EqualTo(4));
            Assert.That(anchors.Select(value => value.CargoStableId).Distinct().Single(),
                Is.EqualTo(CityFarmCargoJourneyBuilder.CargoStableId));
            Assert.That(anchors.Select(value => value.ZoneCode), Is.EquivalentTo(new[]
            {
                CargoJourneyZoneCodes.FarmYard,
                CargoJourneyZoneCodes.TransportCorridor,
                CargoJourneyZoneCodes.UrbanLogistics,
                CargoJourneyZoneCodes.UrbanMarket,
            }));
        }

        [Test]
        public void Lineage_PreservesOriginProductCargoHandoffAndTaskSources()
        {
            var sources = Object.FindFirstObjectByType<CargoJourneyView>().SourceStableIds;

            Assert.That(sources, Is.EquivalentTo(new[]
            {
                "farm-handoff:sim.potato.1",
                "product:potato",
                "cargo:transport-71",
                "cargo-handoff:transport-71.inbound-91",
                "transport-task:71",
                "inbound-task:91",
            }));
        }

        [Test]
        public void MarketRemainsPlanned_WhenHandoffOnlyProvesWarehouseArrival()
        {
            var anchors = Object.FindObjectsByType<CargoJourneyAnchorView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var logistics = anchors.Single(value =>
                value.ZoneCode == CargoJourneyZoneCodes.UrbanLogistics);
            var market = anchors.Single(value =>
                value.ZoneCode == CargoJourneyZoneCodes.UrbanMarket);

            Assert.That(logistics.StateCode, Is.EqualTo(CargoJourneyAnchorStateCodes.Current));
            Assert.That(market.StateCode, Is.EqualTo(CargoJourneyAnchorStateCodes.Planned));
            Assert.That(Object.FindFirstObjectByType<CargoJourneyView>().CurrentZoneCode,
                Is.EqualTo(CargoJourneyZoneCodes.UrbanLogistics));
        }

        [Test]
        public void LowerRevision_IsIgnored_AndIdentityCannotChange()
        {
            var view = Object.FindFirstObjectByType<CargoJourneyView>();
            var revision = view.SourceRevision;

            Assert.That(view.Apply(CityFarmCargoJourneyBuilder.CreateModel(
                "InTransit", revision - 1)), Is.False);
            Assert.That(view.SourceRevision, Is.EqualTo(revision));
            Assert.That(view.CurrentZoneCode, Is.EqualTo(CargoJourneyZoneCodes.UrbanLogistics));

            var changed = CityFarmCargoJourneyBuilder.CreateModel("InTransit", revision + 1);
            changed.CargoStableId = "cargo:other-72";
            Assert.Throws<InvalidOperationException>(() => view.Apply(changed));
        }

        [Test]
        public void PrimitiveFallback_DoesNotChangeCargoIdentityOrLineage()
        {
            var view = Object.FindFirstObjectByType<CargoJourneyView>();
            var anchor = Object.FindObjectsByType<CargoJourneyAnchorView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.ZoneCode == CargoJourneyZoneCodes.FarmYard);
            var fallback = anchor.GetComponent<WorldPresentationFallbackView>();
            var sources = view.SourceStableIds.ToArray();

            fallback.UsePrimitiveFallback(true);

            Assert.That(anchor.CargoStableId, Is.EqualTo(CityFarmCargoJourneyBuilder.CargoStableId));
            Assert.That(view.SourceStableIds, Is.EqualTo(sources));
            Assert.That(view.ValidateApplied(), Is.True);

            fallback.UsePrimitiveFallback(false);
        }

        [Test]
        public void SceneAddsNoSimulationTickOrLifetimeScopeAuthority()
        {
            var root = GameObject.Find("WorldBootstrap/" + CityFarmCargoJourneyBuilder.IntegrationRootName);
            Assert.That(root, Is.Not.Null);
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            Assert.That(behaviours.Any(value => value.GetType().Name.Contains(
                "LifetimeScope", StringComparison.Ordinal)), Is.False);
            Assert.That(behaviours.Any(value => value.GetType().Name.Contains(
                "SimulationController", StringComparison.Ordinal)), Is.False);
        }
    }
}
