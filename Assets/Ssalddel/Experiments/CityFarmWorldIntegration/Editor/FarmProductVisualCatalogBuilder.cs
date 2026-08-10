using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class FarmProductVisualCatalogBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Catalogs/FarmProductVisualCatalog.asset";
        public const string Revision = "farm-product-visual.v1.2026-59";

        private const string PlantsRoot = "Assets/Synty/PolygonFarm/Prefabs/Plants/";
        private const string DirectNote =
            "POLYGON Farm inventory에서 같은 품목 의미의 전용 prefab을 확인했습니다.";
        private const string RepresentativeNote =
            "POLYGON Farm inventory의 유사 품목군 prefab을 대표 시각으로만 사용하며 동일 품종이나 규격을 뜻하지 않습니다.";
        private const string UnmappedNote =
            "현재 POLYGON Farm inventory에서 오해 없이 사용할 전용 품목 prefab을 확인하지 못했습니다.";

        private sealed class Spec
        {
            public Spec(string stableId, string name, string status, string visualKey = "", string prefabPath = "")
            {
                StableId = stableId;
                Name = name;
                Status = status;
                VisualKey = visualKey;
                PrefabPath = prefabPath;
            }

            public string StableId { get; }
            public string Name { get; }
            public string Status { get; }
            public string VisualKey { get; }
            public string PrefabPath { get; }
        }

        public static IReadOnlyList<string> CanonicalProductStableIds { get; } =
            BuildSpecs().Select(value => value.StableId).ToArray();

        [MenuItem("Ssalddel/World/Rebuild Farm Product Visual Catalog")]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FarmProductVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FarmProductVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = BuildSpecs().Select(CreateEntry).ToArray();
            catalog.Configure(Revision, entries);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"FarmProductVisualCatalogBuilt:total={entries.Length},mapped={entries.Count(value => value.IsMapped)}");
        }

        private static FarmProductVisualCatalogEntry CreateEntry(Spec spec)
        {
            var prefab = string.IsNullOrEmpty(spec.PrefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (!string.IsNullOrEmpty(spec.PrefabPath) && prefab == null)
                throw new InvalidOperationException("FarmProductPrefabMissing:" + spec.PrefabPath);
            var entry = new FarmProductVisualCatalogEntry();
            entry.Configure(
                spec.StableId,
                spec.Name,
                spec.Status,
                spec.VisualKey,
                prefab!,
                spec.Status == FarmProductVisualMappingStatusCodes.Direct
                    ? DirectNote
                    : spec.Status == FarmProductVisualMappingStatusCodes.Representative
                        ? RepresentativeNote
                        : UnmappedNote);
            return entry;
        }

        private static IReadOnlyList<Spec> BuildSpecs()
            => new[]
            {
                Direct("product:potato", "감자", "potato", "SM_Prop_Potato_01_Group.prefab"),
                None("100", "111", "쌀"),
                Direct("100", "141", "콩", "bean", "SM_Prop_Bean_01_Group.prefab"),
                None("100", "151", "고구마"),
                Representative("200", "211", "배추", "cabbage", "SM_Prop_Cabbage_01_L.prefab"),
                Direct("200", "212", "양배추", "cabbage", "SM_Prop_Cabbage_01_L.prefab"),
                None("200", "213", "시금치"),
                Direct("200", "214", "상추", "lettuce", "SM_Prop_Lettuce_01_L.prefab"),
                Representative("200", "215", "얼갈이배추", "cabbage", "SM_Prop_Cabbage_01_L.prefab"),
                None("200", "216", "갓"),
                Direct("200", "221", "수박", "watermelon", "SM_Prop_Watermelon_01_L.prefab"),
                None("200", "222", "참외"),
                Direct("200", "223", "오이", "cucumber", "SM_Prop_Cucumber_01_Group.prefab"),
                Representative("200", "224", "호박", "pumpkin", "SM_Prop_Pumpkin_01_L.prefab"),
                Direct("200", "225", "토마토", "tomato", "SM_Prop_Tomato_01_Group.prefab"),
                Direct("200", "226", "딸기", "strawberry", "SM_Prop_Strawberry_01_Group.prefab"),
                None("200", "231", "무"),
                Direct("200", "232", "당근", "carrot", "SM_Prop_Carrot_01_Group.prefab"),
                None("200", "233", "열무"),
                Representative("200", "242", "풋고추", "chilli", "SM_Prop_Chilli_01_Group.prefab"),
                Representative("200", "243", "붉은고추", "chilli", "SM_Prop_Chilli_01_Group.prefab"),
                None("200", "244", "피마늘"),
                Direct("200", "245", "양파", "onion", "SM_Prop_Onion_01_Group.prefab"),
                None("200", "246", "파"),
                None("200", "247", "생강"),
                Representative("200", "255", "피망", "pepper", "SM_Prop_Pepper_01_Group.prefab"),
                Representative("200", "256", "파프리카", "pepper", "SM_Prop_Pepper_01_Group.prefab"),
                None("200", "257", "멜론"),
                None("200", "258", "깐마늘(국산)"),
                Representative("200", "279", "알배기배추", "cabbage", "SM_Prop_Cabbage_01_L.prefab"),
                Direct("200", "280", "브로콜리", "broccoli", "SM_Prop_Broccoli_01_L.prefab"),
                Representative("200", "422", "방울토마토", "tomato", "SM_Prop_Tomato_01_Group.prefab"),
                None("300", "312", "참깨"),
                None("300", "314", "땅콩"),
                None("300", "315", "느타리버섯"),
                None("300", "316", "팽이버섯"),
                None("300", "317", "새송이버섯"),
                None("300", "318", "호두"),
                None("300", "319", "아몬드"),
                Direct("400", "411", "사과", "apple", "SM_Prop_Apple_01_Group.prefab"),
                Direct("400", "412", "배", "pear", "SM_Prop_Pear_01_Group.prefab"),
                Direct("400", "413", "복숭아", "peach", "SM_Prop_Peach_01_Group.prefab"),
                None("400", "414", "포도"),
                Representative("400", "415", "감귤", "orange", "SM_Prop_Orange_01_Group.prefab"),
                None("400", "416", "단감"),
                Direct("400", "418", "바나나", "banana", "SM_Prop_Banana_01_Group.prefab"),
                None("400", "419", "참다래"),
                None("400", "420", "파인애플"),
                Direct("400", "421", "오렌지", "orange", "SM_Prop_Orange_01_Group.prefab"),
                Direct("400", "424", "레몬", "lemon", "SM_Prop_Lemon_01_Group.prefab"),
                Direct("400", "425", "체리", "cherry", "SM_Prop_Cherry_01_Group.prefab"),
                None("400", "428", "망고"),
                None("400", "430", "아보카도"),
                None("600", "611", "고등어"),
                None("600", "619", "물오징어"),
                None("600", "644", "굴"),
                None("600", "653", "전복"),
                None("600", "656", "꽃게"),
                None("600", "658", "홍합"),
                None("600", "659", "가리비"),
            };

        private static Spec Direct(string stableId, string name, string visualName, string prefabName)
            => new(stableId, name, FarmProductVisualMappingStatusCodes.Direct,
                "farm.product." + visualName, PlantsRoot + prefabName);

        private static Spec Direct(string category, string item, string name, string visualName, string prefabName)
            => Direct(StableId(category, item), name, visualName, prefabName);

        private static Spec Representative(string category, string item, string name, string visualName, string prefabName)
            => new(StableId(category, item), name,
                FarmProductVisualMappingStatusCodes.Representative,
                "farm.product." + visualName, PlantsRoot + prefabName);

        private static Spec None(string category, string item, string name)
            => new(StableId(category, item), name,
                FarmProductVisualMappingStatusCodes.Unmapped);

        private static string StableId(string category, string item)
            => $"product:food:{category}:{item}";
    }
}
