using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class CityFarmSyntyWorldSceneTests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmSyntyWorldPrototype.unity";
        [Test]
        public void 저장Scene은_WorldWiring과CatalogPrefab및GlobalVolume을유지한다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var world = roots.SelectMany(value =>
                    value.GetComponentsInChildren<공급망MacroWorldView>(true)).Single();
                var visuals = roots.SelectMany(value =>
                    value.GetComponentsInChildren<WorldVisualInstanceView>(true)).ToArray();
                var volume = roots.SelectMany(value =>
                    value.GetComponentsInChildren<Volume>(true)).Single();

                Assert.That(world.ValidateWiring(), Is.True);
                Assert.That(world.Zones.Count, Is.EqualTo(6));
                Assert.That(world.Routes.Count, Is.EqualTo(5));
                Assert.That(visuals.Length, Is.GreaterThanOrEqualTo(80));
                Assert.That(visuals.All(value => value.ValidateWiring()), Is.True);
                Assert.That(visuals.All(value =>
                    PrefabUtility.GetCorrespondingObjectFromSource(
                        value.PrefabInstanceRoot) != null), Is.True);
                Assert.That(volume.isGlobal, Is.True);
                Assert.That(volume.sharedProfile, Is.Not.Null);
                Assert.That(world.CameraRig.GetComponent<Camera>().orthographic, Is.False);
            }
            finally
            {
                if (openedByTest) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }
    }
}
