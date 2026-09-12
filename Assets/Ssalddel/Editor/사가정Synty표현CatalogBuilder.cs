using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 사가정Synty표현CatalogBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Resources/SagajeongSyntyVisualCatalog.asset";

        [MenuItem("Ssalddel/사가정/Synty 표현 대장 갱신")]
        public static 법정동경관VisualCatalog Build()
        {
            var inventory = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath);
            if (inventory == null) inventory = SyntyPackAssetInventoryBuilder.Build();
            inventory.Validate();

            var definitions = new[]
            {
                new Definition(법정동경관VisualKeys.CityApartment,
                    "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_OfficeSquare_01.prefab"),
                new Definition(법정동경관VisualKeys.CityShop,
                    "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_Shop_03.prefab"),
                new Definition(법정동경관VisualKeys.CityOffice,
                    "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_OfficeOld_Small_01.prefab"),
                new Definition(법정동경관VisualKeys.CityCivic,
                    "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_Shop_05.prefab"),
            };
            var entries = definitions.Select(value => CreateEntry(inventory, value)).ToArray();

            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(사가정Synty표현Planner.Revision, entries);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceUpdate);
            return catalog;
        }

        private static 법정동경관VisualCatalogEntry CreateEntry(
            SyntyPackAssetInventoryCatalog inventory,
            Definition definition)
        {
            var source = inventory.Entries.SingleOrDefault(value =>
                value.Prefab != null
                && AssetDatabase.GetAssetPath(value.Prefab) == definition.PrefabPath);
            if (source == null)
                throw new InvalidOperationException("SagajeongSyntyInventoryEntryMissing:" + definition.PrefabPath);

            var entry = new 법정동경관VisualCatalogEntry();
            entry.Configure(
                definition.VisualKey,
                "PolygonCity",
                source.Prefab,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                new Vector2(source.LocalBounds.size.x, source.LocalBounds.size.z),
                new Vector2(0f, 5f),
                1,
                1,
                new[] { "All" },
                .5f,
                법정동경관CollisionPolicyCodes.FootprintOnly,
                source.TriangleCount,
                source.MaterialSlotCount,
                source.MaterialSlotCount,
                source.ShadowCasterCount,
                source.ColliderCount,
                source.AnimatorCount,
                false,
                true,
                법정동경관CompositionRoleCodes.Anchor,
                "sagajeong-" + definition.VisualKey,
                법정동경관ReviewStatusCodes.Active,
                .75f,
                true);
            return entry;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private readonly struct Definition
        {
            public Definition(string visualKey, string prefabPath)
            {
                VisualKey = visualKey;
                PrefabPath = prefabPath;
            }

            public string VisualKey { get; }
            public string PrefabPath { get; }
        }
    }
}
