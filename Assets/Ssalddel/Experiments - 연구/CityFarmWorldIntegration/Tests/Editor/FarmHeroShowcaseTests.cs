using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class FarmHeroShowcaseTests
    {
        [SetUp]
        public void OpenScene()
            => EditorSceneManager.OpenScene(FarmHeroShowcaseBuilder.ScenePath, OpenSceneMode.Single);

        [Test]
        public void HeroSlice는_원본Showcase와분리된Farm전용구도를제공한다()
        {
            FarmHeroShowcaseBuilder.ValidateOpenScene();
            var root = GameObject.Find("WorldBootstrap/" + FarmHeroShowcaseBuilder.RootName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.childCount, Is.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<Transform>(true)
                .Count(value => value.name.StartsWith("Environment_")), Is.GreaterThanOrEqualTo(90));

            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>();
            Assert.That(rig.ConfiguredZoneDistance, Is.EqualTo(33f).Within(.01f));
            Assert.That(rig.CurrentFocusAnchorId,
                Is.EqualTo("camera-focus:zone.farm-production"));
        }

        [Test]
        public void Hero추가물은_VisualRoot아래VendorPrefab이며업무권위를갖지않는다()
        {
            var root = GameObject.Find("WorldBootstrap/" + FarmHeroShowcaseBuilder.RootName);
            var wrappers = root.transform.Cast<Transform>()
                .SelectMany(group => group.Cast<Transform>())
                .Where(value => value.name.StartsWith("Environment_")).ToArray();
            Assert.That(wrappers, Has.Length.GreaterThanOrEqualTo(90));
            Assert.That(wrappers.All(value => value.Find("VisualRoot/SyntyPrefabInstance") != null), Is.True);
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                value.GetType().Name.Contains("Command")
                || value.GetType().Name.Contains("Simulation")
                || value.GetType().Name.Contains("Operational")), Is.False);
        }

        [Test]
        public void HeroART4는_작물군집과트랙터에_Presentation움직임을제공한다()
        {
            var root = GameObject.Find("WorldBootstrap/" + FarmHeroShowcaseBuilder.RootName);
            var sway = root.GetComponentsInChildren<농장환경SwayPresenter>(true);
            Assert.That(sway, Has.Length.GreaterThanOrEqualTo(59));
            Assert.That(sway.All(value => value.ValidateWiring()), Is.True);

            var tractor = root.GetComponentInChildren<절차형VehicleRouteFollower>(true);
            Assert.That(tractor, Is.Not.Null);
            Assert.That(tractor.ValidateWiring(), Is.True);
            Assert.That(tractor.Speed, Is.EqualTo(1.35f).Within(.01f));
        }

        [Test]
        public void Hero조명은_따뜻한SoftShadow와절제된Fog를사용한다()
        {
            var light = GameObject.Find("WorldBootstrap/GlobalLighting").GetComponent<Light>();
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(light.shadowStrength, Is.InRange(.75f, .8f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogDensity, Is.LessThan(.003f));
        }

        [Test]
        public void HeroTOD0는_Midday회귀기준과교체가능한시간Presentation을제공한다()
        {
            var root = GameObject.Find("WorldBootstrap/" + FarmHeroShowcaseBuilder.RootName);
            var presenter = root.GetComponent<월드시간대Presenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.ValidateWiring(), Is.True);
            Assert.That(presenter.SourceMode, Is.EqualTo(월드시간대SourceMode.FixedReference));
            Assert.That(presenter.AutoCycleInPlayMode, Is.False);
            Assert.That(presenter.SurfaceBindingCount, Is.GreaterThanOrEqualTo(70));

            presenter.ApplyNowForTests(12.5f / 24f);
            Assert.That(presenter.CurrentModel.PreviousAnchor,
                Is.EqualTo(월드시간대AnchorCode.Midday));
            Assert.That(presenter.CurrentModel.BlendWeight, Is.EqualTo(0f).Within(.001f));
            Assert.That(GameObject.Find("WorldBootstrap/GlobalLighting")
                .GetComponent<Light>().intensity, Is.EqualTo(1.18f).Within(.01f));
        }

        [Test]
        public void HeroTOD1은_시간순서에따라그림자밝기와표면색을연속변화시킨다()
        {
            var root = GameObject.Find("WorldBootstrap/" + FarmHeroShowcaseBuilder.RootName);
            var presenter = root.GetComponent<월드시간대Presenter>();
            var light = GameObject.Find("WorldBootstrap/GlobalLighting").GetComponent<Light>();

            presenter.ApplyNowForTests(5.5f / 24f);
            var dawnIntensity = light.intensity;
            var dawnRotation = light.transform.rotation;
            var dawnSky = RenderSettings.ambientSkyColor;

            presenter.ApplyNowForTests(21f / 24f);
            Assert.That(light.intensity, Is.LessThan(dawnIntensity));
            Assert.That(Quaternion.Angle(light.transform.rotation, dawnRotation),
                Is.GreaterThan(20f));
            Assert.That(RenderSettings.ambientSkyColor.maxColorComponent,
                Is.LessThan(dawnSky.maxColorComponent));
            Assert.That(presenter.CurrentModel.SurfaceBrightness, Is.LessThan(.7f));

            var beforeMidnight = 월드시간대Interpreter.Evaluate(.999f);
            var afterMidnight = 월드시간대Interpreter.Evaluate(.001f);
            Assert.That(Mathf.Abs(beforeMidnight.SunIntensity - afterMidnight.SunIntensity),
                Is.LessThan(.02f));
        }
    }
}
