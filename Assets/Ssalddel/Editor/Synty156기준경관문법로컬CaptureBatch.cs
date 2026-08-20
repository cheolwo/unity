using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class Synty156기준경관문법로컬CaptureBatch
    {
        private static readonly CaptureGroup[] Groups =
        {
            new("farm", "Farm", "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/Farm", 36),
            new("nature", "Nature", "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/NatureCompositionSets", 36),
            new("town", "Town", "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/PackCompositionSets/town", 36),
            new("city", "City", "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/PackCompositionSets/city", 36),
            new("transition", "혼합·전환", "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/PackCompositionSets/mixed", 12),
        };

        [MenuItem("Ssalddel/Synty Web 검토/156 기준 경관 문법 로컬 일괄 촬영")]
        public static void CaptureCanonicalGrammarInventory()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/WI공간모판검토실.unity",
                OpenSceneMode.Single);
            var capturedAtUtc = DateTime.UtcNow;
            var collectionRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "artifacts",
                "local",
                "synty-web-review",
                "canonical-grammar-inventory",
                capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(collectionRoot);

            var inventory = new GrammarCaptureInventory
            {
                SchemaVersion = "synty-canonical-grammar-local-capture-inventory.v1",
                CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                AuthorityBoundary = "LocalPresentationEvidenceOnly",
                KnowledgeKind = "GrammarExpressionCandidate",
                ExpectedGrammarCount = 156,
            };

            foreach (var group in Groups)
            {
                var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { group.AssetFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => string.Equals(
                        Path.GetDirectoryName(path)?.Replace('\\', '/'),
                        group.AssetFolder,
                        StringComparison.Ordinal))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (prefabPaths.Length != group.ExpectedCount)
                    throw new InvalidOperationException(
                        $"CanonicalGrammarPrefabCountMismatch:{group.Code}:{prefabPaths.Length}/{group.ExpectedCount}");

                for (var index = 0; index < prefabPaths.Length; index++)
                {
                    var prefabPath = prefabPaths[index];
                    var displayName = Path.GetFileNameWithoutExtension(prefabPath);
                    var safeFolder = (index + 1).ToString("D2", CultureInfo.InvariantCulture) + "-" + displayName;
                    var outputFolder = Path.Combine(collectionRoot, group.Code, safeFolder);
                    var token = Sha256(prefabPath)[..16];
                    var stableId = $"grammar-expression:{group.Code}:{displayName}";
                    var root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        var job = new SyntyH공간조립검토Job
                        {
                            BatchStableId = $"review-batch:canonical-grammar.{group.Code}.{token}.r1",
                            BatchTitle = $"156 기준 경관 문법 · {group.DisplayName} · {displayName}",
                            ReviewItemStableId = $"review-item:canonical-grammar.{group.Code}.{token}.r1",
                            CompositionStableId = $"composition:canonical-grammar.{group.Code}.{token}.r1",
                            DisplayName = displayName,
                            ReviewTargetLevelCode = "H1",
                            ReviewTargetStableId = stableId,
                            H1StableId = stableId,
                            VariantCode = VariantCode(displayName),
                            StateProfileCode = "GrammarExpressionCandidate",
                        };
                        job.PlanHash = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
                        var bundle = Synty공간조립Web검토CapturePipeline.CaptureHierarchySelection(
                            root,
                            job,
                            outputFolderOverride: outputFolder);
                        inventory.Items.Add(new GrammarCaptureItem
                        {
                            GroupCode = group.Code,
                            GroupDisplayName = group.DisplayName,
                            StableId = stableId,
                            DisplayName = displayName,
                            VariantCode = job.VariantCode,
                            SourcePrefabPath = prefabPath,
                            RelativeFolder = Path.GetRelativePath(collectionRoot, bundle.OutputFolder).Replace('\\', '/'),
                            CaptureCount = bundle.Captures.Count,
                            SourceCompositionHash = bundle.SourceCompositionHash,
                            CaptureBundleHash = bundle.CaptureBundleHash,
                        });
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            if (inventory.Items.Count != inventory.ExpectedGrammarCount)
                throw new InvalidOperationException(
                    $"CanonicalGrammarInventoryCountMismatch:{inventory.Items.Count}/{inventory.ExpectedGrammarCount}");
            File.WriteAllText(
                Path.Combine(collectionRoot, "canonical-grammar-capture-inventory.json"),
                JsonUtility.ToJson(inventory, true),
                new System.Text.UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(collectionRoot, "README.md"),
                BuildReadme(inventory),
                new System.Text.UTF8Encoding(false));
            Debug.Log($"156 기준 경관 문법 로컬 촬영 완료: 조합={inventory.Items.Count}, PNG={inventory.Items.Sum(value => value.CaptureCount)}, Folder={collectionRoot}");
            if (!UnityEngine.Application.isBatchMode)
                EditorUtility.RevealInFinder(collectionRoot);
        }

        private static string VariantCode(string displayName)
        {
            if (displayName.EndsWith("_A", StringComparison.Ordinal)) return "A";
            if (displayName.EndsWith("_B", StringComparison.Ordinal)) return "B";
            if (displayName.EndsWith("_C", StringComparison.Ordinal)) return "C";
            return "Unspecified";
        }

        private static string Sha256(string value)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string BuildReadme(GrammarCaptureInventory inventory)
        {
            var lines = new List<string>
            {
                "# 156 기준 경관 문법 로컬 촬영",
                string.Empty,
                "- 촬영 시각: `" + inventory.CapturedAtUtc + "`",
                "- 구성: Farm 36 + Nature 36 + Town 36 + City 36 + 혼합·전환 12 = 156",
                "- 분류: `GrammarExpressionCandidate`",
                "- 권위: `LocalPresentationEvidenceOnly`",
                "- 주의: 기준 경관 문법 표현 후보이며 공식 H1, WI 공간 능력, E단계 증거를 자동으로 만들지 않는다.",
                string.Empty,
                "| 묶음 | 조합물 | PNG | 원본 Prefab |",
                "| --- | --- | ---: | --- |",
            };
            lines.AddRange(inventory.Items.Select(value =>
                $"| {value.GroupDisplayName} | `{value.StableId}` | {value.CaptureCount} | `{value.SourcePrefabPath}` |"));
            lines.Add(string.Empty);
            lines.Add("각 조합물 폴더의 `capture-manifest.json`과 PNG는 향후 서버 업로드 입력으로 사용할 수 있다.");
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private readonly struct CaptureGroup
        {
            public CaptureGroup(string code, string displayName, string assetFolder, int expectedCount)
            {
                Code = code;
                DisplayName = displayName;
                AssetFolder = assetFolder;
                ExpectedCount = expectedCount;
            }

            public string Code { get; }
            public string DisplayName { get; }
            public string AssetFolder { get; }
            public int ExpectedCount { get; }
        }

        [Serializable]
        private sealed class GrammarCaptureInventory
        {
            public string SchemaVersion;
            public string CapturedAtUtc;
            public string AuthorityBoundary;
            public string KnowledgeKind;
            public int ExpectedGrammarCount;
            public List<GrammarCaptureItem> Items = new();
        }

        [Serializable]
        private sealed class GrammarCaptureItem
        {
            public string GroupCode;
            public string GroupDisplayName;
            public string StableId;
            public string DisplayName;
            public string VariantCode;
            public string SourcePrefabPath;
            public string RelativeFolder;
            public int CaptureCount;
            public string SourceCompositionHash;
            public string CaptureBundleHash;
        }
    }
}
