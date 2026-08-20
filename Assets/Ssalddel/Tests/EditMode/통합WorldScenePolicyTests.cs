using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합WorldScenePolicyTests
    {
        [Test]
        public void BuildSettings는_SimulationWorldShell하나만_포함한다()
        {
            var scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[0].path,
                Is.EqualTo(통합WorldScenePolicy.CanonicalScenePath));
        }

        [Test]
        public void Ssalddel의모든Scene은_공식플레이또는_참고자산으로분류된다()
        {
            var unclassified = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Ssalddel" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => 통합WorldScenePolicy.Classify(path)
                    == 통합WorldScenePolicy.SceneRole.분류되지않음)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(unclassified, Is.Empty,
                "새 기능은 별도 정식 Scene이 아니라 SimulationWorldShell 조립 모듈로 추가해야 합니다. "
                + "검토용 Scene이 꼭 필요하면 Experiments - 연구 아래에 둡니다.");
        }

        [Test]
        public void 기존BootstrapScene은_공식플레이Scene이아니다()
        {
            Assert.That(통합WorldScenePolicy.Classify(
                    "Assets/Ssalddel/Scenes/WorldBootstrapScene.unity"),
                Is.EqualTo(통합WorldScenePolicy.SceneRole.기존검토참고));
        }
    }
}
