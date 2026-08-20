using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 공간문법SyntyBindingCatalogBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창Synty경관BindingCatalog.asset";
        public const string NatureInventoryRevision =
            "pyeongchang-nature-composition-inventory.v1";
        public const string RoadGateInventoryRevision =
            "pyeongchang-road-gate-composition-inventory.v1";

        [MenuItem("Ssalddel/World Placement/공간 문법 Synty Binding 생성")]
        public static 공간문법SyntyBindingCatalog Build()
        {
            var grammar = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                    공간문법CompositionCatalogBuilder.CatalogPath)
                ?? throw new InvalidOperationException("LandscapeGrammarCatalogMissing");
            grammar.Validate();
            var packs = AssetDatabase.LoadAssetAtPath<팩경관CompositionCatalog>(
                    팩경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException("PackLandscapeCompositionCatalogMissing");
            var nature = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                    자연경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException("NatureCompositionCatalogMissing");
            var roads = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(
                    도로GateCompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException("RoadGateCompositionCatalogMissing");
            packs.Validate();
            nature.Validate();
            roads.Validate();

            var receipts = new[]
            {
                Receipt(packs.CatalogRevision,
                    packs.Entries.Select(value => (value.CompositionKey, value.Prefab))),
                Receipt(NatureInventoryRevision,
                    nature.Entries.Select(value => (value.CompositionKey, value.Prefab))),
                Receipt(RoadGateInventoryRevision,
                    roads.Entries.Select(value => (value.CompositionKey, value.Prefab))),
            };
            var entries = grammar.Entries
                .OrderBy(value => value.CompositionKey, StringComparer.Ordinal)
                .Select(value =>
                {
                    var primary = new 공간문법SyntyBindingCandidate();
                    primary.Configure(
                        공간문법SyntyBindingCodes.Primary,
                        0,
                        value.SourceCompositionKey,
                        value.Prefab,
                        value.InternalGeneration.DetailGeneratorRevision);
                    var entry = new 공간문법SyntyBindingEntry();
                    entry.Configure(value.CompositionKey, new[] { primary });
                    return entry;
                }).ToArray();

            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            var binding = AssetDatabase.LoadAssetAtPath<공간문법SyntyBindingCatalog>(CatalogPath);
            if (binding == null)
            {
                binding = ScriptableObject.CreateInstance<공간문법SyntyBindingCatalog>();
                AssetDatabase.CreateAsset(binding, CatalogPath);
            }
            binding.Configure(
                공간문법SyntyBindingCodes.BindingRevision,
                공간문법CompositionCatalog.NeutralGrammarRevision,
                grammar.BuildNeutralGrammarHashSha256(),
                receipts,
                entries);
            binding.Validate();
            EditorUtility.SetDirty(binding);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"LandscapeSyntyBindingCatalogBuilt:{entries.Length}:"
                + binding.BuildBindingHashSha256());
            return binding;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static 공간문법SyntyInventoryReceipt Receipt(
            string revision,
            System.Collections.Generic.IEnumerable<(string Key, GameObject Prefab)> values)
        {
            var receipt = new 공간문법SyntyInventoryReceipt();
            receipt.Configure(revision, BuildInventoryHash(revision, values));
            return receipt;
        }

        private static string BuildInventoryHash(
            string revision,
            System.Collections.Generic.IEnumerable<(string Key, GameObject Prefab)> values)
        {
            var builder = new StringBuilder().Append(revision).AppendLine();
            foreach (var entry in values.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GetAssetPath(entry.Prefab);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid))
                    throw new InvalidOperationException(
                        "LandscapeSyntyInventoryPrefabGuidMissing:" + entry.Key);
                builder.Append(entry.Key).Append('|').Append(guid).AppendLine();
            }
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                .Select(value => value.ToString("x2")));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
