using System;
using System.Collections.Generic;
using System.IO;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 자연경관EventOverlayBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군자연경관EventOverlayCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/NatureEventOverlays";
        private const string Nature = "Assets/Synty/PolygonNature/Prefabs/";

        [MenuItem("Ssalddel/WORLD-NATURE-2 평창군 Nature 사건 Overlay 생성")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            AssetDatabase.Refresh();

            var entries = new List<자연경관EventOverlayCatalogEntry>();
            foreach (var definition in Definitions())
            {
                var prefab = BuildPrefab(definition);
                var entry = new 자연경관EventOverlayCatalogEntry();
                entry.Configure(definition.PresentationKey,
                    definition.OverlayName, prefab);
                if (!entry.Validate())
                    throw new InvalidOperationException(
                        "NatureEventOverlayEntryInvalid:"
                        + definition.PresentationKey);
                entries.Add(entry);
            }

            var catalog = AssetDatabase
                .LoadAssetAtPath<자연경관EventOverlayCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject
                    .CreateInstance<자연경관EventOverlayCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(entries.ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefab(EventDefinition definition)
        {
            var root = new GameObject(definition.OverlayName.Replace(" ", string.Empty));
            try
            {
                var visualRoot = Child(root.transform, "VisualRoot_EventOnly");
                for (var index = 0; index < definition.SourcePaths.Length; index++)
                {
                    var sourcePath = Nature + definition.SourcePaths[index] + ".prefab";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)
                        ?? throw new InvalidOperationException(
                            "NatureEventOverlaySourceMissing:" + sourcePath);
                    var instance = PrefabUtility
                        .InstantiatePrefab(source, visualRoot) as GameObject
                        ?? throw new InvalidOperationException(
                            "NatureEventOverlayInstantiateFailed:" + sourcePath);
                    instance.transform.localPosition = new Vector3(
                        (index % 2) * 2.4f - 1.2f, 0f,
                        (index / 2) * 2.2f - 1.1f);
                    instance.transform.localRotation = Quaternion.Euler(
                        0f, index * 61f, 0f);
                    instance.transform.localScale = Vector3.one * (.68f + index * .08f);
                }

                var view = root.AddComponent<자연경관EventOverlayView>();
                view.Configure(definition.PresentationKey,
                    definition.OverlayName, visualRoot);
                if (!view.ValidateWiring())
                    throw new InvalidOperationException(
                        "NatureEventOverlayWiringInvalid:"
                        + definition.PresentationKey);

                var prefabPath = PrefabRoot + "/"
                    + definition.OverlayName.Replace(" ", string.Empty)
                    + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath,
                    ImportAssetOptions.ForceSynchronousImport);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    ?? throw new InvalidOperationException(
                        "NatureEventOverlaySaveFailed:"
                        + definition.PresentationKey);
                if (!saved.TryGetComponent<자연경관EventOverlayView>(out var savedView)
                    || savedView == null || !savedView.ValidateWiring())
                    throw new InvalidOperationException(
                        "NatureEventOverlaySavedWiringInvalid:"
                        + definition.PresentationKey
                        + ":view=" + (savedView != null)
                        + ":root=" + (savedView != null && savedView.VisualRoot != null)
                        + ":children=" + (savedView?.VisualRoot == null
                            ? -1 : savedView.VisualRoot.childCount)
                        + ":childOf=" + (savedView?.VisualRoot != null
                            && savedView.VisualRoot.IsChildOf(saved.transform))
                        + ":eventOnly=" + (savedView != null && savedView.EventOnly)
                        + ":presentationOnly="
                        + (savedView != null && savedView.PresentationOnly));
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static EventDefinition[] Definitions() => new[]
        {
            Definition(자연경관EventPresentationKeys.SeasonalDefenseWarning,
                "계절 방어 전조",
                "Trees/SM_Tree_Dead_01", "Props/SM_Prop_TorchStick_01",
                "FX/FX_Dust_Blowing_01", "FX/FX_Leaves_Orange_01"),
            Definition(자연경관EventPresentationKeys.ZombieWarning,
                "늪 위협 전조",
                "Trees/SM_Tree_Swamp_01", "Terrain/SM_Swamp_Root_01",
                "Props/SM_Prop_Grave_03", "FX/FX_Flies_01"),
            Definition(자연경관EventPresentationKeys.RaiderApproach,
                "폐허 접근 흔적",
                "Props/SM_Prop_Pillar_Arch_Broken_Moss_01",
                "Props/SM_Prop_StoneWall_02", "Props/SM_Prop_CampFire_01",
                "FX/FX_Smoke_Light_01"),
            Definition(자연경관EventPresentationKeys.DamageAssessment,
                "피해 현장",
                "Terrain/SM_Terrain_Rubble_Pebbles_03",
                "Trees/SM_Tree_Dead_02", "Props/SM_Prop_Skeleton_Ground_01",
                "FX/FX_Smoke_01"),
            Definition(자연경관EventPresentationKeys.TacticalZombiePressure,
                "동굴 압력 전조",
                "Rocks/SM_Rock_CaveEntrance_01",
                "Rocks/SM_Rock_Cluster_Large_04", "Props/SM_Prop_TorchStick_01",
                "FX/FX_Glowing_Dust_01"),
        };

        private static EventDefinition Definition(
            string presentationKey,
            string overlayName,
            params string[] sourcePaths)
            => new(presentationKey, overlayName, sourcePaths);

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private sealed class EventDefinition
        {
            public EventDefinition(
                string presentationKey,
                string overlayName,
                string[] sourcePaths)
            {
                PresentationKey = presentationKey;
                OverlayName = overlayName;
                SourcePaths = sourcePaths;
            }

            public string PresentationKey { get; }
            public string OverlayName { get; }
            public string[] SourcePaths { get; }
        }
    }
}
