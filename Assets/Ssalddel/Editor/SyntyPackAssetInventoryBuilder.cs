using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Editor
{
    public static class SyntyPackAssetInventoryBuilder
    {
        public const string ScanRuleRevision = "synty-pack-inventory.v2";
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/SyntyPackAssetInventoryCatalog.asset";
        public const string SummaryDocumentPath =
            "Assets/Documentation/WorldPlacementPlans/공통/Synty3Pack자산대장요약.md";

        private static readonly IReadOnlyDictionary<string, string> PackRoots =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SyntyPackInventoryCodes.Nature] = "Assets/Synty/PolygonNature/Prefabs",
                [SyntyPackInventoryCodes.Farm] = "Assets/Synty/PolygonFarm/Prefabs",
                [SyntyPackInventoryCodes.Town] = "Assets/Synty/PolygonTown/Prefabs",
                [SyntyPackInventoryCodes.City] = "Assets/Synty/PolygonCity/Prefabs",
                [SyntyPackInventoryCodes.Construction] =
                    "Assets/Synty/PolygonConstruction/Prefabs",
            };

        [MenuItem("Ssalddel/World Placement/신티 5팩 전수 기술 대장 생성")]
        public static SyntyPackAssetInventoryCatalog Build()
        {
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(SummaryDocumentPath)!.Replace('\\', '/'));

            var sources = PackRoots
                .SelectMany(pair => AssetDatabase.FindAssets("t:Prefab", new[] { pair.Value })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new SourceAsset(pair.Key, path)))
                .OrderBy(value => value.PackCode, StringComparer.Ordinal)
                .ThenBy(value => value.Path, StringComparer.Ordinal)
                .ToArray();

            var entries = sources.Select(CreateEntry).ToArray();
            var sourceHash = Sha256(string.Join("\n", sources.Select(value =>
                value.PackCode + "|" + AssetDatabase.AssetPathToGUID(value.Path) + "|"
                + AssetDatabase.GetAssetDependencyHash(value.Path))));

            var catalog = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SyntyPackAssetInventoryCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(ScanRuleRevision, sourceHash, entries);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            File.WriteAllText(SummaryDocumentPath, BuildSummary(catalog), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"SyntyPackAssetInventoryBuilt:{entries.Length}:{sourceHash}");
            return catalog;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static SyntyPackAssetInventoryEntry CreateEntry(SourceAsset source)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path)
                ?? throw new InvalidOperationException("SyntyInventoryPrefabMissing:" + source.Path);
            // category는 v1 inventoryId 호환을 위해 기존 판정을 유지한다.
            var category = CategoryFromPath(source.Path);
            var normalizedCategory = NormalizedCategoryFromPath(source.Path);
            var usageTrack = ResolveUsageTrack(normalizedCategory);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var bounds = CalculateBounds(renderers);
            var triangles = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(value => value.sharedMesh != null)
                .Sum(value => (long)value.sharedMesh.triangles.Length / 3L)
                + prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(value => value.sharedMesh != null)
                    .Sum(value => (long)value.sharedMesh.triangles.Length / 3L);
            var sourceGuid = AssetDatabase.AssetPathToGUID(source.Path);
            var dependencyHash = AssetDatabase.GetAssetDependencyHash(source.Path).ToString();
            var fingerprint = Sha256(sourceGuid + "|" + dependencyHash);
            var inventoryId = "synty-inventory:" + source.PackCode + ":"
                + category.ToLowerInvariant() + ":" + fingerprint.Substring(0, 16);

            var entry = new SyntyPackAssetInventoryEntry();
            entry.Configure(
                inventoryId,
                source.PackCode,
                category,
                ResolveUseKind(category),
                normalizedCategory,
                BuildAssetFamilyId(source, normalizedCategory, fingerprint),
                usageTrack,
                usageTrack == SyntyAssetUsageTrackCodes.ManualReview
                    ? SyntyAssetClassificationStateCodes.NeedsHumanReview
                    : SyntyAssetClassificationStateCodes.AutoClassified,
                ResolvePlannedAreas(source.PackCode),
                prefab,
                bounds,
                triangles,
                renderers.Sum(value => value.sharedMaterials.Length),
                renderers.Count(value => value.shadowCastingMode != ShadowCastingMode.Off),
                prefab.GetComponentsInChildren<Collider>(true).Length,
                prefab.GetComponentsInChildren<Animator>(true).Length,
                prefab.GetComponentsInChildren<ParticleSystem>(true).Length,
                prefab.GetComponentsInChildren<LODGroup>(true).Length,
                fingerprint);
            return entry;
        }

        private static Bounds CalculateBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static string CategoryFromPath(string path)
        {
            var marker = "/Prefabs/";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "Other";
            var remainder = path.Substring(index + marker.Length);
            var slash = remainder.IndexOf('/');
            var raw = slash < 0 ? "Other" : remainder.Substring(0, slash);
            return raw switch
            {
                "Environment" => "Environments",
                "Generic" => "Generic",
                "Items" => "Items",
                "Plants" => "Plants",
                "Props" => "Props",
                "Buildings" => "Buildings",
                "Characters" => "Characters",
                "Vehicles" => "Vehicles",
                "FX" => "FX",
                _ => "Other",
            };
        }

        private static string NormalizedCategoryFromPath(string path)
        {
            var marker = "/Prefabs/";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return SyntyPackNormalizedCategoryCodes.ManualReview;
            var remainder = path.Substring(index + marker.Length);
            var slash = remainder.IndexOf('/');
            var raw = slash < 0 ? string.Empty : remainder.Substring(0, slash);
            return raw switch
            {
                "Buildings" => SyntyPackNormalizedCategoryCodes.Buildings,
                "Characters" => SyntyPackNormalizedCategoryCodes.Characters,
                "Environment" or "Environments" =>
                    SyntyPackNormalizedCategoryCodes.Environments,
                "FX" => SyntyPackNormalizedCategoryCodes.Fx,
                "Generic" => SyntyPackNormalizedCategoryCodes.Generic,
                "Items" => SyntyPackNormalizedCategoryCodes.Items,
                "Plants" => SyntyPackNormalizedCategoryCodes.Plants,
                "Props" => SyntyPackNormalizedCategoryCodes.Props,
                "Rocks" => SyntyPackNormalizedCategoryCodes.Rocks,
                "Terrain" => SyntyPackNormalizedCategoryCodes.Terrain,
                "Trees" => SyntyPackNormalizedCategoryCodes.Trees,
                "Tools" => SyntyPackNormalizedCategoryCodes.Tools,
                "Vehicles" => SyntyPackNormalizedCategoryCodes.Vehicles,
                _ => SyntyPackNormalizedCategoryCodes.ManualReview,
            };
        }

        private static string ResolveUseKind(string category) => category switch
        {
            "Buildings" => SyntyPackAssetUseKindCodes.StandaloneCandidate,
            "Characters" => SyntyPackAssetUseKindCodes.Actor,
            "Items" => SyntyPackAssetUseKindCodes.Item,
            "FX" => SyntyPackAssetUseKindCodes.Fx,
            "Other" => SyntyPackAssetUseKindCodes.ManualReview,
            _ => SyntyPackAssetUseKindCodes.CompositionPart,
        };

        private static string ResolveUsageTrack(string normalizedCategory) =>
            normalizedCategory switch
            {
                "Buildings" or "Environments" or "Plants" or "Rocks"
                    or "Terrain" or "Trees" => SyntyAssetUsageTrackCodes.SpatialBase,
                "Props" or "Generic" => SyntyAssetUsageTrackCodes.FunctionalProp,
                "Characters" => SyntyAssetUsageTrackCodes.Actor,
                "Vehicles" => SyntyAssetUsageTrackCodes.Vehicle,
                "Items" or "Tools" => SyntyAssetUsageTrackCodes.ToolOrItem,
                "FX" => SyntyAssetUsageTrackCodes.StateFx,
                _ => SyntyAssetUsageTrackCodes.ManualReview,
            };

        private static string[] ResolvePlannedAreas(string packCode) => packCode switch
        {
            SyntyPackInventoryCodes.Nature =>
                new[] { SyntyAssetPlannedAreaCodes.NatureHome },
            SyntyPackInventoryCodes.Farm =>
                new[] { SyntyAssetPlannedAreaCodes.Farm },
            SyntyPackInventoryCodes.Town =>
                new[] { SyntyAssetPlannedAreaCodes.Town },
            SyntyPackInventoryCodes.City =>
                new[] { SyntyAssetPlannedAreaCodes.CityHub },
            SyntyPackInventoryCodes.Construction => new[]
            {
                SyntyAssetPlannedAreaCodes.NatureHome,
                SyntyAssetPlannedAreaCodes.Farm,
                SyntyAssetPlannedAreaCodes.Town,
                SyntyAssetPlannedAreaCodes.CityHub,
            },
            _ => throw new InvalidOperationException("SyntyInventoryPackUnknown:" + packCode),
        };

        private static string BuildAssetFamilyId(
            SourceAsset source,
            string normalizedCategory,
            string fingerprint)
        {
            var fileName = Path.GetFileNameWithoutExtension(source.Path);
            var tokens = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !string.Equals(token, "SM", StringComparison.OrdinalIgnoreCase))
                .Where(token => !IsTechnicalPrefix(token))
                .Where(token => !token.All(char.IsDigit))
                .Select(ToFamilyToken)
                .Where(token => token.Length > 0)
                .ToArray();
            var familySlug = tokens.Length == 0
                ? fingerprint.Substring(0, 16)
                : string.Join("-", tokens);
            return "synty-family:" + source.PackCode + ":"
                + normalizedCategory.ToLowerInvariant() + ":" + familySlug;
        }

        private static bool IsTechnicalPrefix(string token) => token switch
        {
            "Bld" or "Chr" or "Env" or "FX" or "Generic" or "Item" or
                "Plant" or "Prop" or "Rock" or "Terrain" or "Tree" or
                "Veh" or "Tool" => true,
            _ => false,
        };

        private static string ToFamilyToken(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static string BuildSummary(SyntyPackAssetInventoryCatalog catalog)
        {
            var lines = new List<string>
            {
                "# Synty Nature·Farm·Town·City·Construction 전수 기술 대장 요약",
                string.Empty,
                $"- 스캔 규칙: `{catalog.ScanRuleRevision}`",
                $"- 원본 묶음 hash: `{catalog.CatalogSourceHashSha256}`",
                $"- 전체 Prefab: {catalog.Entries.Count.ToString(CultureInfo.InvariantCulture)}개",
                $"- 의미 자산군: {catalog.Entries.Select(value => value.AssetFamilyId).Distinct(StringComparer.Ordinal).Count().ToString(CultureInfo.InvariantCulture)}개",
                $"- 자동 분류: {catalog.Entries.Count(value => value.ClassificationStateCode == SyntyAssetClassificationStateCodes.AutoClassified).ToString(CultureInfo.InvariantCulture)}개",
                $"- 사람 검토 대기: {catalog.Entries.Count(value => value.ClassificationStateCode == SyntyAssetClassificationStateCodes.NeedsHumanReview).ToString(CultureInfo.InvariantCulture)}개",
                "- 이 문서는 수량과 배치 원칙만 공개하며 유료 원본 파일명·경로·GUID는 기록하지 않는다.",
                "- 파일 경로의 `Synty3Pack` 이름은 기존 Unity 문서 GUID를 보존하기 위한 호환 경로다.",
                string.Empty,
                "## 팩·정규화 분류별 수량",
                string.Empty,
                "| 팩 | 분류 | 수량 |",
                "| --- | --- | ---: |",
            };
            lines.AddRange(catalog.Entries
                .GroupBy(value => (value.PackCode, value.NormalizedCategoryCode))
                .OrderBy(value => value.Key.PackCode, StringComparer.Ordinal)
                .ThenBy(value => value.Key.NormalizedCategoryCode, StringComparer.Ordinal)
                .Select(group => $"| {group.Key.PackCode} | {group.Key.NormalizedCategoryCode} | {group.Count()} |"));
            lines.AddRange(new[]
            {
                string.Empty,
                "## 팩·주 활용 트랙별 수량",
                string.Empty,
                "| 팩 | 활용 트랙 | Prefab | 자산군 |",
                "| --- | --- | ---: | ---: |",
            });
            lines.AddRange(catalog.Entries
                .GroupBy(value => (value.PackCode, value.PrimaryUsageTrackCode))
                .OrderBy(value => value.Key.PackCode, StringComparer.Ordinal)
                .ThenBy(value => value.Key.PrimaryUsageTrackCode, StringComparer.Ordinal)
                .Select(group => $"| {group.Key.PackCode} | {group.Key.PrimaryUsageTrackCode} | {group.Count()} | {group.Select(value => value.AssetFamilyId).Distinct(StringComparer.Ordinal).Count()} |"));
            lines.AddRange(new[]
            {
                string.Empty,
                "## 승격 경계",
                string.Empty,
                "- 전수 기술 대장 등록은 월드 배치 승인을 뜻하지 않는다.",
                "- 사람이 의미와 토지피복·경사·동선을 검토한 항목만 `VisualKey` 또는 `CompositionKey`로 승격한다.",
                "- Character·Vehicle·Item·Tool·FX는 기술 대장에는 포함하지만 정적 경관 자동 배치에서 제외한다.",
                "- 모든 항목은 `PresentationOnly`이며 공간 사실이나 Simulation 상태를 만들지 않는다.",
                string.Empty,
            });
            return string.Join("\n", lines);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(valueByte => valueByte.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private readonly struct SourceAsset
        {
            public SourceAsset(string packCode, string path)
            {
                PackCode = packCode;
                Path = path;
            }

            public string PackCode { get; }
            public string Path { get; }
        }
    }
}
