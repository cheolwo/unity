using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class FarmCityGraphicalShowcaseTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(
                FarmCityGraphicalShowcaseBuilder.ScenePath,
                OpenSceneMode.Single);
        }

        [Test]
        public void ReloadedScene_PreservesWorldWiringAndValidEnvironmentInstances()
        {
            FarmCityGraphicalShowcaseBuilder.ValidateOpenScene();

            var root = GameObject.Find("WorldBootstrap/"
                + FarmCityGraphicalShowcaseBuilder.RootName);
            var instances = root.GetComponentsInChildren<WorldVisualInstanceView>(true);

            Assert.That(instances.Length, Is.GreaterThanOrEqualTo(340));
            Assert.That(instances.Count(value => value.VisualKey.StartsWith(
                "environment.farm.", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(250));
            Assert.That(instances.Count(value => value.VisualKey.StartsWith(
                "environment.city.", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(80));
            Assert.That(instances.All(value => value.ValidateWiring()), Is.True);
        }

        [Test]
        public void EnvironmentCatalog_UsesOnlyVendorPrefabsBehindPresentationWrappers()
        {
            var root = GameObject.Find("WorldBootstrap/"
                + FarmCityGraphicalShowcaseBuilder.RootName);
            var instances = root.GetComponentsInChildren<WorldVisualInstanceView>(true);

            Assert.That(instances.All(value =>
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(
                    value.PrefabInstanceRoot);
                return source != null
                    && AssetDatabase.GetAssetPath(source).StartsWith(
                        "Assets/Synty/", StringComparison.Ordinal);
            }), Is.True);
            Assert.That(EnvironmentVisualKeys.All.All(WorldVisualKeys.IsKnown), Is.True);
        }

        [Test]
        public void ShowcaseHidesLegacyGroundAndHudWithoutAddingWorkAuthority()
        {
            var environment = GameObject.Find("WorldBootstrap/"
                + FarmCityGraphicalShowcaseBuilder.RootName);
            var legacyGround = GameObject.Find("WorldBootstrap/SharedWorldGround");
            var canvas = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(value => value.name == "WorldQualityPresentationCanvas");
            var typeNames = environment.GetComponentsInChildren<MonoBehaviour>(true)
                .Select(value => value.GetType().Name).ToArray();

            Assert.That(legacyGround, Is.Null);
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Assert.That(typeNames.Any(value => value.Contains("Simulation", StringComparison.Ordinal)
                || value.Contains("Operational", StringComparison.Ordinal)
                || value.Contains("LifetimeScope", StringComparison.Ordinal)
                || value.Contains("Command", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void CameraHotReloadGuard_KeepsPresentationAccessSafe()
        {
            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>();

            Assert.DoesNotThrow(() => _ = rig.CurrentFocusPosition);
            rig.Focus("camera-focus:zone.farm-production");
            rig.ApplyNowForTests();

            Assert.That(rig.CurrentFocusLevelCode, Is.EqualTo(DioramaCameraFocusLevelCodes.Zone));
            Assert.That(rig.Distance, Is.EqualTo(26f).Within(.01f));
        }
    }
}
