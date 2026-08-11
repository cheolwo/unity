using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public sealed class SyntyAnimationSourceInventoryReport
    {
        public SyntyAnimationSourceInventoryReport(
            int standaloneAnimationClipCount,
            int animatorControllerCount,
            int animatorOverrideControllerCount,
            IReadOnlyList<string> humanoidRigPaths,
            IReadOnlyList<string> importedCharacterClipPaths,
            IReadOnlyList<string> missingControllerPrefabPaths,
            IReadOnlyDictionary<string, int> particleSystemPrefabCounts)
        {
            StandaloneAnimationClipCount = standaloneAnimationClipCount;
            AnimatorControllerCount = animatorControllerCount;
            AnimatorOverrideControllerCount = animatorOverrideControllerCount;
            HumanoidRigPaths = humanoidRigPaths;
            ImportedCharacterClipPaths = importedCharacterClipPaths;
            MissingControllerPrefabPaths = missingControllerPrefabPaths;
            ParticleSystemPrefabCounts = particleSystemPrefabCounts;
        }

        public int StandaloneAnimationClipCount { get; }
        public int AnimatorControllerCount { get; }
        public int AnimatorOverrideControllerCount { get; }
        public IReadOnlyList<string> HumanoidRigPaths { get; }
        public IReadOnlyList<string> ImportedCharacterClipPaths { get; }
        public IReadOnlyList<string> MissingControllerPrefabPaths { get; }
        public IReadOnlyDictionary<string, int> ParticleSystemPrefabCounts { get; }

        public void EnsureNoMissingControllerReferences()
        {
            if (MissingControllerPrefabPaths.Count > 0)
                throw new InvalidOperationException(
                    "SyntyAnimatorControllerReferenceMissing:"
                    + string.Join(",", MissingControllerPrefabPaths));
        }
    }

    public static class SyntyAnimationSourceInventory
    {
        private const string SyntyRoot = "Assets/Synty";
        private const string TownCharacterPrefabRoot =
            "Assets/Synty/PolygonTown/Prefabs/Characters";

        private static readonly string[] CharacterModelPaths =
        {
            "Assets/Synty/PolygonFarm/Models/Characters.fbx",
            "Assets/Synty/PolygonCity/Models/Characters.fbx",
            "Assets/Synty/PolygonGeneric/Models/Generic_Characters.fbx",
            "Assets/Synty/PolygonStarter/Models/Characters.fbx",
            "Assets/Synty/PolygonTown/Models/Characters/Characters.fbx",
        };

        private static readonly IReadOnlyDictionary<string, string> FxRoots =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["farm"] = "Assets/Synty/PolygonFarm/Prefabs/FX",
                ["city"] = "Assets/Synty/PolygonCity/Prefabs/FX",
                ["generic"] = "Assets/Synty/PolygonGeneric/Prefabs/FX",
            };

        [MenuItem("Ssalddel/Validation/Inspect Synty Animation Sources")]
        public static void InspectAndLog()
        {
            var report = Inspect();
            Debug.Log(
                "SyntyAnimationInventory:"
                + $"clips={report.StandaloneAnimationClipCount};"
                + $"controllers={report.AnimatorControllerCount};"
                + $"overrideControllers={report.AnimatorOverrideControllerCount};"
                + $"humanoidRigs={report.HumanoidRigPaths.Count};"
                + $"importedCharacterClips={report.ImportedCharacterClipPaths.Count};"
                + $"missingControllers={report.MissingControllerPrefabPaths.Count};"
                + string.Join(";", report.ParticleSystemPrefabCounts
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => $"fx.{value.Key}={value.Value}")));
        }

        public static SyntyAnimationSourceInventoryReport Inspect()
        {
            if (!AssetDatabase.IsValidFolder(SyntyRoot))
                throw new InvalidOperationException("SyntyRootMissing:" + SyntyRoot);

            var standaloneClips = AssetDatabase.FindAssets("t:AnimationClip", new[] { SyntyRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Count(path => string.Equals(
                    Path.GetExtension(path), ".anim", StringComparison.OrdinalIgnoreCase));
            var controllers = AssetDatabase.FindAssets("t:AnimatorController", new[] { SyntyRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Count(path => string.Equals(
                    Path.GetExtension(path), ".controller", StringComparison.OrdinalIgnoreCase));
            var overrideControllers = AssetDatabase.FindAssets(
                    "t:AnimatorOverrideController",
                    new[] { SyntyRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Count(path => string.Equals(
                    Path.GetExtension(path), ".overrideController",
                    StringComparison.OrdinalIgnoreCase));

            var humanoidRigs = CharacterModelPaths.Where(path =>
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                return importer != null && importer.animationType == ModelImporterAnimationType.Human;
            }).ToArray();
            var importedCharacterClips = CharacterModelPaths.SelectMany(path =>
                    AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<AnimationClip>()
                        .Where(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal))
                        .Select(value => path + "#" + value.name))
                .ToArray();
            var missingControllers = FindMissingTownControllerReferences();
            var fxCounts = FxRoots.ToDictionary(
                value => value.Key,
                value => CountParticleSystemPrefabs(value.Value),
                StringComparer.Ordinal);

            return new SyntyAnimationSourceInventoryReport(
                standaloneClips,
                controllers,
                overrideControllers,
                humanoidRigs,
                importedCharacterClips,
                missingControllers,
                fxCounts);
        }

        private static IReadOnlyList<string> FindMissingTownControllerReferences()
        {
            var controllerPattern = new Regex(
                @"m_Controller:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32})",
                RegexOptions.Compiled);
            return AssetDatabase.FindAssets("t:Prefab", new[] { TownCharacterPrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    var absolutePath = Path.GetFullPath(path);
                    var match = controllerPattern.Match(File.ReadAllText(absolutePath));
                    return match.Success
                        && string.IsNullOrWhiteSpace(
                            AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
                })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static int CountParticleSystemPrefabs(string root)
            => AssetDatabase.FindAssets("t:Prefab", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Count(value => value != null
                    && value.GetComponentInChildren<ParticleSystem>(true) != null);
    }
}
