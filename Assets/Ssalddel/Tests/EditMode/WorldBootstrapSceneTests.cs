using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
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
            Assert.That(settings.OperationalApiBaseUrl, Is.EqualTo("https://localhost:7117/"));
            Assert.That(settings.SimulationRehearsalApiBaseUrl, Is.EqualTo("http://localhost:5204/"));
            Assert.That(settings.DetailBaseUrl, Is.EqualTo("http://localhost:5238"));
            Assert.That(settings.AllowFixtureData, Is.False);
        }
    }
}
