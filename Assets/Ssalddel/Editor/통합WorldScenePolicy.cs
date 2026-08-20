using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 정식 플레이는 하나의 영속 Scene에서 실행하고, 기능은 그 Scene에 조립되는 모듈로 관리한다.
    /// 기존 검토·실험 Scene은 참고 자산으로 보존하지만 Build 진입점으로 승격하지 않는다.
    /// </summary>
    public static class 통합WorldScenePolicy
    {
        public const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        private static readonly HashSet<string> ExistingReviewScenePaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Assets/Ssalddel/Scenes/WorldBootstrapScene.unity",
                "Assets/Ssalddel/Scenes/UrbanLogisticsCenterPrimitive.unity",
                "Assets/Ssalddel/Scenes/UrbanMarketManagerPrimitive.unity",
                "Assets/Ssalddel/Scenes/턴카드모판.unity",
                "Assets/Ssalddel/Scenes/통합모판전시관.unity",
                "Assets/Ssalddel/Scenes/통합Object모판.unity",
                "Assets/Ssalddel/Scenes/WI공간모판검토실.unity"
            };

        public enum SceneRole
        {
            공식플레이,
            기존검토참고,
            실험연구,
            자동생성참고,
            외부팩예제,
            Unity복구임시,
            분류되지않음
        }

        [MenuItem("Ssalddel/통합 월드/SimulationWorldShell 열기", false, 1)]
        public static void OpenCanonicalScene()
        {
            if (!File.Exists(CanonicalScenePath))
                throw new FileNotFoundException("CanonicalSimulationWorldSceneMissing", CanonicalScenePath);

            EditorSceneManager.OpenScene(CanonicalScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Ssalddel/통합 월드/Build Settings를 단일 Scene으로 정리", false, 2)]
        public static void ApplyCanonicalBuildSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CanonicalScenePath) == null)
                throw new InvalidOperationException("CanonicalSimulationWorldSceneMissing");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(CanonicalScenePath, true)
            };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Ssalddel/통합 월드/단일 Scene 정책 검증", false, 3)]
        public static void ValidateCanonicalPolicy()
        {
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length != 1
                || !buildScenes[0].enabled
                || !string.Equals(buildScenes[0].path, CanonicalScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("CanonicalSimulationWorldBuildEntryInvalid");

            var unclassified = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Ssalddel" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Classify(path) == SceneRole.분류되지않음)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (unclassified.Length > 0)
                throw new InvalidOperationException(
                    "UnclassifiedSsalddelScene:" + string.Join(",", unclassified));
        }

        public static SceneRole Classify(string assetPath)
        {
            if (string.Equals(assetPath, CanonicalScenePath, StringComparison.Ordinal))
                return SceneRole.공식플레이;
            if (ExistingReviewScenePaths.Contains(assetPath))
                return SceneRole.기존검토참고;
            if (assetPath.StartsWith("Assets/Ssalddel/Experiments - 연구/", StringComparison.Ordinal))
                return SceneRole.실험연구;
            if (assetPath.StartsWith("Assets/SsalddelGenerated/", StringComparison.Ordinal))
                return SceneRole.자동생성참고;
            if (assetPath.StartsWith("Assets/Synty/", StringComparison.Ordinal))
                return SceneRole.외부팩예제;
            if (assetPath.StartsWith("Assets/_Recovery/", StringComparison.Ordinal))
                return SceneRole.Unity복구임시;

            return SceneRole.분류되지않음;
        }
    }
}
