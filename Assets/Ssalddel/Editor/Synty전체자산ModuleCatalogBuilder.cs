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

namespace Ssalddel.Unity.Editor
{
    public static class Synty전체자산ModuleCatalogBuilder
    {
        public const string CatalogRevision = "synty-all-asset-modules.r2";
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/Synty전체자산ModuleCatalog.asset";
        public const string SummaryPath =
            "Documentation/Generated/Synty전체자산ModuleCatalog.md";

        public static IReadOnlyDictionary<string, string> PackRoots { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Synty전체자산SourcePackCodes.Nature] = "Assets/Synty/PolygonNature/Prefabs",
                [Synty전체자산SourcePackCodes.Farm] = "Assets/Synty/PolygonFarm/Prefabs",
                [Synty전체자산SourcePackCodes.Town] = "Assets/Synty/PolygonTown/Prefabs",
                [Synty전체자산SourcePackCodes.City] = "Assets/Synty/PolygonCity/Prefabs",
                [Synty전체자산SourcePackCodes.Construction] = "Assets/Synty/PolygonConstruction/Prefabs",
                [Synty전체자산SourcePackCodes.Generic] = "Assets/Synty/PolygonGeneric/Prefabs",
                [Synty전체자산SourcePackCodes.Starter] = "Assets/Synty/PolygonStarter/Prefabs",
                [Synty전체자산SourcePackCodes.AnimationBaseLocomotion] = "Assets/Synty/AnimationBaseLocomotion",
                [Synty전체자산SourcePackCodes.AnimationEmotesAndTaunts] = "Assets/Synty/AnimationEmotesAndTaunts",
                [Synty전체자산SourcePackCodes.AnimationSwordCombat] = "Assets/Synty/AnimationSwordCombat",
                [Synty전체자산SourcePackCodes.AlpineMountain] = "Assets/Synty/PolygonNatureBiomes",
                [Synty전체자산SourcePackCodes.DungeonRealms] = "Assets/Synty/PolygonDungeonRealms",
                [Synty전체자산SourcePackCodes.DwarvenDungeonMap] = "Assets/Synty/PolygonMapsDwarfDungeon",
            };

        [MenuItem("Ssalddel/공간·경관/배치 대장/Synty 13팩 기능 모듈 대장 생성")]
        public static Synty전체자산ModuleCatalog Build()
        {
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath)!);

            var legacyInventory = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath);
            var legacyFamilyByPrefab = legacyInventory == null
                ? new Dictionary<GameObject, string>()
                : legacyInventory.Entries.ToDictionary(
                    value => value.Prefab,
                    value => value.AssetFamilyId);
            var sources = DiscoverSources().ToArray();
            var entries = sources.Select(source =>
                CreateEntry(source, legacyFamilyByPrefab)).ToArray();
            var sourceHash = Sha256(string.Join("\n", sources.Select(value =>
                value.PackCode + "|" + AssetDatabase.AssetPathToGUID(value.Path) + "|"
                + AssetDatabase.GetAssetDependencyHash(value.Path))));

            var catalog = AssetDatabase.LoadAssetAtPath<Synty전체자산ModuleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Synty전체자산ModuleCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(CatalogRevision, sourceHash, entries);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            File.WriteAllText(SummaryPath, BuildSummary(catalog), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"SyntyAllAssetModuleCatalogBuilt:{entries.Length}:{sourceHash}");
            return catalog;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static IEnumerable<(string PackCode, string Path)> DiscoverSourcePaths() =>
            DiscoverSources().Select(value => (value.PackCode, value.Path));

        private static IEnumerable<SourceAsset> DiscoverSources() =>
            PackRoots
                .SelectMany(pair => AssetDatabase.FindAssets("t:Prefab", new[] { pair.Value })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new SourceAsset(pair.Key, path)))
                .OrderBy(value => value.PackCode, StringComparer.Ordinal)
                .ThenBy(value => value.Path, StringComparer.Ordinal);

        private static Synty자산ModuleEntry CreateEntry(
            SourceAsset source,
            IReadOnlyDictionary<GameObject, string> legacyFamilyByPrefab)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path)
                ?? throw new InvalidOperationException("SyntyAllAssetPrefabMissing:" + source.Path);
            var fingerprint = Sha256(
                AssetDatabase.AssetPathToGUID(source.Path) + "|"
                + AssetDatabase.GetAssetDependencyHash(source.Path));
            var familyId = legacyFamilyByPrefab.TryGetValue(prefab, out var legacyFamilyId)
                ? legacyFamilyId
                : BuildFamilyId(source, fingerprint);
            var classification = Synty자산ModuleClassifier.Classify(
                source.PackCode, source.Path);
            if (!classification.Validate())
                throw new InvalidOperationException("SyntyAllAssetClassificationInvalid:" + source.Path);

            var entry = new Synty자산ModuleEntry();
            entry.Configure(
                "synty-asset:" + source.PackCode + ":" + fingerprint.Substring(0, 20),
                source.PackCode,
                familyId,
                classification.ModuleCodes,
                classification.LifecycleCode,
                classification.HoldReasonCode,
                prefab,
                fingerprint);
            return entry;
        }

        private static string BuildFamilyId(SourceAsset source, string fingerprint)
        {
            var fileName = Path.GetFileNameWithoutExtension(source.Path);
            var slug = new string(fileName.ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray()).Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (string.IsNullOrWhiteSpace(slug)) slug = fingerprint.Substring(0, 16);
            return "synty-family:" + source.PackCode + ":auto:" + slug;
        }

        private static string BuildSummary(Synty전체자산ModuleCatalog catalog)
        {
            var lines = new List<string>
            {
                "# Synty 13팩 자산 기능 대장",
                string.Empty,
                "> Unity 대장에서 자동 생성된다. 원본 Prefab 경로는 공개 문서에 기록하지 않는다.",
                string.Empty,
                $"- 대장 revision: `{catalog.CatalogRevision}`",
                $"- 전체 Prefab: `{catalog.Entries.Count}`",
                $"- 기능군: `{Synty자산ModuleCodes.All.Count}`",
                "- 사람이 읽는 분류: `실외 표현 / 실내 표현 / 공통 표현`",
                "- 분류 순서: `범위 → 기능군 → 세부 기능군 → 자산 종류 → 자산 계열 → 실제 Prefab`",
                $"- 명시적 보류: `{catalog.Entries.Count(value => value.ModuleCodes.Count == 0)}`",
                "- Construction은 AreaSet이 아니라 건설·복구 상태를 공유하는 표현 계층이다.",
                "- Generic은 공유 기반, Starter는 prototype fallback으로만 사용한다.",
                "- 애니메이션 3팩은 이 대장에서는 sample Prefab만 집계하며, 실제 Clip은 별도 애니메이션 원천 프로필에서 관리한다.",
                "- Alpine·Dungeon·Dwarven 팩은 새 Area가 아니라 기존 WI/H가 선택할 수 있는 검토 후보이다.",
                string.Empty,
                "## 팩별 수량",
                string.Empty,
                "| 팩 | Prefab | 생산 후보 | 검토/보류 |",
                "| --- | ---: | ---: | ---: |",
            };
            lines.AddRange(catalog.Entries
                .GroupBy(value => value.SourcePackCode)
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(group => $"| {group.Key} | {group.Count()} | "
                    + $"{group.Count(value => value.LifecycleCode == Synty자산LifecycleCodes.ProductionReady)} | "
                    + $"{group.Count(value => value.LifecycleCode != Synty자산LifecycleCodes.ProductionReady)} |"));
            lines.AddRange(new[]
            {
                string.Empty,
                "## 기능군별 수량",
                string.Empty,
                "| 표현 범위 | 기능군 | Stable Code | Prefab |",
                "| --- | --- | --- | ---: |",
            });
            lines.AddRange(Synty자산기능분류기준.All.Select(기준 =>
                $"| {기준.범위이름} | {기준.기능군이름} | `{기준.기능군Code}` | "
                + $"{catalog.Entries.Count(value => value.ModuleCodes.Contains(기준.기능군Code))} |"));
            lines.AddRange(new[]
            {
                string.Empty,
                "대장 등록은 Scene 배치 승인이나 E 승격을 뜻하지 않는다. PlayableLoop 슬롯이 기능 모듈과 자산 계열을 함께 요구하고 실제 배치 검증을 통과해야 한다.",
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
