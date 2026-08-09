using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class CityFarmVisualQualityGateTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                CityFarmVisualQualityGateBuilder.ScenePath,
                OpenSceneMode.Single);
        }

        [Test]
        public void ReloadedScene_PassesShaderPrefabAndQualityGateValidation()
        {
            CityFarmVisualQualityGateBuilder.ValidateOpenScene();

            var view = Object.FindFirstObjectByType<WorldVisualQualityGateView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.ValidateApplied(), Is.True);
        }

        [Test]
        public void Hud_ShowsOneCargoAndFourEvidenceBoundStages()
        {
            var view = Object.FindFirstObjectByType<WorldVisualQualityGateView>();

            Assert.That(view.CargoStableId, Is.EqualTo(CityFarmCargoJourneyBuilder.CargoStableId));
            Assert.That(view.CurrentZoneCode, Is.EqualTo(CargoJourneyZoneCodes.UrbanLogistics));
            Assert.That(view.MarketStateCode, Is.EqualTo(CargoJourneyAnchorStateCodes.Planned));
            Assert.That(view.StageCount, Is.EqualTo(4));
            Assert.That(view.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(view.Canvas.worldCamera, Is.Not.Null);
        }

        [Test]
        public void Camera_UsesSelectedZoneCompositionWithoutChangingFocusAuthority()
        {
            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>();

            rig.Focus("camera-focus:zone.urban-logistics");
            rig.ApplyNowForTests();

            Assert.That(rig.ConfiguredZoneDistance,
                Is.EqualTo(CityFarmVisualQualityGateBuilder.SelectedZoneDistance).Within(.01f));
            Assert.That(rig.Distance,
                Is.EqualTo(CityFarmVisualQualityGateBuilder.SelectedZoneDistance).Within(.01f));
            Assert.That(rig.CurrentFocusLevelCode, Is.EqualTo("Zone"));
        }

        [Test]
        public void UnreadableWorldText_IsSuppressedWhileHudRemainsActive()
        {
            Assert.That(Object.FindObjectsByType<TextMesh>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(value => value.gameObject.activeInHierarchy), Is.False);
            Assert.That(Object.FindFirstObjectByType<WorldVisualQualityGateView>()
                .Canvas.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void QualityGateAddsNoSeasonWeatherStreamingOrWorkAuthority()
        {
            var root = GameObject.Find("WorldBootstrap/"
                + CityFarmVisualQualityGateBuilder.IntegrationRootName);
            Assert.That(root, Is.Not.Null);
            var typeNames = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Select(value => value.GetType().Name).ToArray();

            Assert.That(typeNames.Any(value => value.Contains("Season", StringComparison.Ordinal)
                || value.Contains("Weather", StringComparison.Ordinal)
                || value.Contains("Streaming", StringComparison.Ordinal)
                || value.Contains("LifetimeScope", StringComparison.Ordinal)
                || value.Contains("SimulationController", StringComparison.Ordinal)), Is.False);
        }
    }
}
