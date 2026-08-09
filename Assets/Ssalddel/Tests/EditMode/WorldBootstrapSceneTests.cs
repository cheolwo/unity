using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class WorldBootstrapSceneTests
    {
        [Test]
        public void Scene은_CompositionRoot_UI_설정을_직렬화한다()
        {
            EditorSceneManager.OpenScene("Assets/Ssalddel/Scenes/WorldBootstrapScene.unity", OpenSceneMode.Single);
            var world = GameObject.Find("SsalddelWorld");
            var compositionRoot = world.GetComponent<WorldBootstrapSceneCompositionRoot>();
            var serialized = new SerializedObject(compositionRoot);

            Assert.That(serialized.FindProperty("runtimeSettings").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("markerPresenter").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("sceneView").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("detailPanel").objectReferenceValue, Is.Not.Null);
            Assert.That(GameObject.Find("WorldMapStatusPanel"), Is.Not.Null);
            Assert.That(GameObject.Find("ObservationDetailPanel"), Is.Not.Null);

            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset").ToOptions();
            Assert.That(settings.ApiBaseUrl, Is.EqualTo("http://localhost:5104"));
            Assert.That(settings.DetailBaseUrl, Is.EqualTo("http://localhost:5238"));
            Assert.That(settings.AllowFixtureData, Is.False);
        }
    }
}
