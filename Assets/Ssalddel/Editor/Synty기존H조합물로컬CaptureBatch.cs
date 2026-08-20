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
    public static class Synty기존H조합물로컬CaptureBatch
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/WI공간모판검토실.unity";

        private static readonly H1CaptureDefinition[] H1Definitions =
        {
            new("01-farm-production", "모판01_", "농장 생산 공간 모판", "h1-stock:farm-production"),
            new("02-farm-work-yard", "모판02_", "농장 작업마당 공간 모판", "h1-stock:farm-work-yard"),
            new("03-farm-loading-gate", "모판03_", "농장 상차 Gate 공간 모판", "h1-stock:farm-loading-gate"),
            new("04-farm-hub-corridor", "모판04_", "Farm Hub 회랑 공간 모판", "h1-stock:farm-hub-corridor"),
            new("05-hub-receiving-storage", "모판05_", "Hub 입고 보관 공간 모판", "h1-stock:hub-receiving-storage"),
        };

        [MenuItem("Ssalddel/Synty Web 검토/기존 H1 5종 로컬 일괄 촬영")]
        public static void CaptureExistingH1Inventory()
        {
            if (!UnityEngine.Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reviewRoot = scene.GetRootGameObjects()
                .FirstOrDefault(value => value.name == "WI공간모판검토실")
                ?? throw new InvalidOperationException("WiSpatialSeedbedReviewRootMissing");
            var overview = reviewRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == "전체개요_5모판_9공간")
                ?? throw new InvalidOperationException("WiSpatialSeedbedOverviewRootMissing");

            var capturedAtUtc = DateTime.UtcNow;
            var collectionRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "artifacts",
                "local",
                "synty-web-review",
                "existing-h-inventory",
                capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(collectionRoot);

            var inventory = new ExistingHLocalCaptureInventory
            {
                SchemaVersion = "synty-existing-h-local-capture-inventory.v1",
                CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                SourceScenePath = ScenePath,
                H2CaptureStatus = "DefinitionOnlyNoUnityRoot",
                H3CaptureStatus = "DefinitionOnlyNoUnityRoot",
                AuthorityBoundary = "LocalPresentationEvidenceOnly",
            };

            foreach (var definition in H1Definitions)
            {
                var selectedRoot = overview.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name.StartsWith(
                        definition.HierarchyNamePrefix, StringComparison.Ordinal));
                if (selectedRoot == null)
                    throw new InvalidOperationException(
                        "ExistingH1HierarchyRootMissing:" + definition.StableId);

                var itemFolder = Path.Combine(collectionRoot, "H1", definition.FolderName);
                var job = new SyntyH공간조립검토Job
                {
                    BatchStableId = "review-batch:existing-h1." + definition.FolderName + ".r1",
                    BatchTitle = "기존 H1 로컬 촬영 · " + definition.DisplayName,
                    ReviewItemStableId = "review-item:existing-h1." + definition.FolderName + ".r1",
                    CompositionStableId = "composition:existing-h1." + definition.FolderName + ".r1",
                    DisplayName = definition.DisplayName,
                    ReviewTargetLevelCode = "H1",
                    ReviewTargetStableId = definition.StableId,
                    H1StableId = definition.StableId,
                    VariantCode = "A",
                    StateProfileCode = "ExistingSavedScene",
                };
                job.PlanHash = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
                var bundle = Synty공간조립Web검토CapturePipeline.CaptureHierarchySelection(
                    selectedRoot.gameObject,
                    job,
                    outputFolderOverride: itemFolder);
                inventory.Items.Add(new ExistingHLocalCaptureItem
                {
                    HierarchyLevelCode = "H1",
                    StableId = definition.StableId,
                    DisplayName = definition.DisplayName,
                    SourceHierarchyPath = "WI공간모판검토실/VisualRoot_PresentationOnly/전체개요_5모판_9공간/"
                                          + selectedRoot.name,
                    CaptureStatus = "Captured",
                    RelativeFolder = Path.GetRelativePath(collectionRoot, bundle.OutputFolder)
                        .Replace('\\', '/'),
                    CaptureCount = bundle.Captures.Count,
                    SourceCompositionHash = bundle.SourceCompositionHash,
                    CaptureBundleHash = bundle.CaptureBundleHash,
                });
            }

            File.WriteAllText(
                Path.Combine(collectionRoot, "existing-h-capture-inventory.json"),
                JsonUtility.ToJson(inventory, true),
                new System.Text.UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(collectionRoot, "README.md"),
                BuildReadme(inventory),
                new System.Text.UTF8Encoding(false));
            Debug.Log($"기존 H 로컬 촬영 완료: H1={inventory.Items.Count}, PNG={inventory.Items.Sum(value => value.CaptureCount)}, "
                      + $"H2/H3=Unity Root 없음, Folder={collectionRoot}");
            if (!UnityEngine.Application.isBatchMode)
                EditorUtility.RevealInFinder(collectionRoot);
        }

        private static string BuildReadme(ExistingHLocalCaptureInventory inventory)
        {
            var lines = new List<string>
            {
                "# 기존 H 조합물 로컬 촬영",
                string.Empty,
                "- 촬영 시각: `" + inventory.CapturedAtUtc + "`",
                "- 원본 Scene: `" + inventory.SourceScenePath + "`",
                "- 권위: `LocalPresentationEvidenceOnly`",
                "- H2: 설계 정의만 존재하며 촬영 가능한 Unity Root 없음",
                "- H3: 설계 정의만 존재하며 촬영 가능한 Unity Root 없음",
                string.Empty,
                "| 단계 | 조합물 | PNG | 폴더 |",
                "| --- | --- | ---: | --- |",
            };
            lines.AddRange(inventory.Items.Select(value =>
                $"| {value.HierarchyLevelCode} | `{value.StableId}` {value.DisplayName} | {value.CaptureCount} | `{value.RelativeFolder}` |"));
            lines.Add(string.Empty);
            lines.Add("각 폴더의 `capture-manifest.json`과 PNG는 향후 서버 업로드 영수증 발급 입력으로 사용할 수 있다.");
            lines.Add("이 촬영은 H 승인, E단계 승격, Scene 적용이나 Simulation 상태를 만들지 않는다.");
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private readonly struct H1CaptureDefinition
        {
            public H1CaptureDefinition(
                string folderName,
                string hierarchyNamePrefix,
                string displayName,
                string stableId)
            {
                FolderName = folderName;
                HierarchyNamePrefix = hierarchyNamePrefix;
                DisplayName = displayName;
                StableId = stableId;
            }

            public string FolderName { get; }
            public string HierarchyNamePrefix { get; }
            public string DisplayName { get; }
            public string StableId { get; }
        }

        [Serializable]
        private sealed class ExistingHLocalCaptureInventory
        {
            public string SchemaVersion;
            public string CapturedAtUtc;
            public string SourceScenePath;
            public string H2CaptureStatus;
            public string H3CaptureStatus;
            public string AuthorityBoundary;
            public List<ExistingHLocalCaptureItem> Items = new();
        }

        [Serializable]
        private sealed class ExistingHLocalCaptureItem
        {
            public string HierarchyLevelCode;
            public string StableId;
            public string DisplayName;
            public string SourceHierarchyPath;
            public string CaptureStatus;
            public string RelativeFolder;
            public int CaptureCount;
            public string SourceCompositionHash;
            public string CaptureBundleHash;
        }
    }
}
