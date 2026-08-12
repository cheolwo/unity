using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공급망MacroWorldSceneTests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시공간배치초안.unity";

        [Test]
        public void 저장Scene은_6개Zone과5개Route및CameraFocus를직렬화한다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var view = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<공급망MacroWorldView>(true))
                    .Single();

                var wiringDiagnostics = "zones=" + string.Join(",", view.Zones.Select(value =>
                        value == null ? "null" : value.StableId + ":" + value.ValidateWiring()))
                    + ";routes=" + string.Join(",", view.Routes.Select(value =>
                        value == null ? "null" : value.StableId + ":" + value.ValidateWiring()))
                    + ";camera=" + (view.CameraRig != null);
                Assert.That(view.ValidateWiring(), Is.True, wiringDiagnostics);
                Assert.That(view.Zones.Count, Is.EqualTo(6));
                Assert.That(view.Routes.Count, Is.EqualTo(5));
                Assert.That(view.Zones.OrderBy(value => value.FlowOrder)
                    .Select(value => value.PresentationZoneCode), Is.EqualTo(new[]
                    {
                        "farm-production",
                        "farm-yard",
                        "transport-corridor",
                        "urban-logistics",
                        "urban-market",
                        "residential-community",
                    }));
                view.CameraRig.Initialize();
                Assert.That(view.CameraRig.CurrentFocusAnchorId,
                    Is.EqualTo("camera-focus:world.city-farm-supply-chain"));
                Assert.That(view.CameraRig.GetComponent<Camera>().orthographic, Is.False);
            }
            finally
            {
                if (openedByTest)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }
    }
}
