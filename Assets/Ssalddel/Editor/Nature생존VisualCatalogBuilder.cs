using System.IO;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 설치된 Synty 자산을 빌드에서도 읽을 수 있는 Nature 시각 자산 묶음으로 고정합니다.
    /// </summary>
    public static class Nature생존VisualCatalogBuilder
    {
        private const string ResourceDirectory = "Assets/Ssalddel/Resources";
        private const string AssetPath = ResourceDirectory
            + "/Nature생존VisualCatalog.asset";

        [MenuItem("Ssalddel/검증/Nature 생존 시각 자산 묶음 갱신")]
        public static void Build()
        {
            Directory.CreateDirectory(ResourceDirectory);
            var catalog = AssetDatabase.LoadAssetAtPath<
                Nature생존VisualCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    Nature생존VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.Configure(
                Load("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab"),
                Load("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Stump_01.prefab"),
                Load("Assets/Synty/PolygonGeneric/Prefabs/Weapons/SM_Gen_Wep_Axe_01.prefab"),
                Load("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Log_02.prefab"),
                Load("Assets/Synty/PolygonTown/Prefabs/Buildings/SM_Bld_GardenShed_01.prefab"),
                Load("Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Table_Saw_01.prefab"),
                Load("Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab"),
                Load("Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Wood_Stack_01.prefab"),
                Load("Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Bed_01.prefab"),
                Load("Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_CampFire_01.prefab"),
                Load("Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Plans_01.prefab"));
            catalog.ConfigureInterior(
                Load("Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Rack_01.prefab"),
                Load("Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Table_01.prefab"),
                Load("Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_ToolBox_01.prefab"));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Nature 생존 시각 자산 묶음 갱신 완료: " + AssetPath);
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new FileNotFoundException("NatureVisualAssetMissing", path);
            return prefab;
        }
    }
}
