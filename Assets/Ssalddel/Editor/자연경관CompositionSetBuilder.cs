using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 자연경관CompositionSetBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군자연경관CompositionCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/NatureCompositionSets";
        private const string Nature = "Assets/Synty/PolygonNature/Prefabs/";

        [MenuItem("Ssalddel/WORLD-NATURE-1 평창군 Nature 경관 Composition 생성")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            AssetDatabase.Refresh();

            var entries = new List<자연경관CompositionCatalogEntry>();
            foreach (var setName in 자연경관SetNames.All)
            foreach (var variant in 월드CompositionVariantCodes.All)
            {
                var prefab = BuildPrefab(setName, variant);
                var rule = Rule(setName);
                var entry = new 자연경관CompositionCatalogEntry();
                entry.Configure(setName, variant, prefab, Footprint(setName),
                    rule.HlodEligible, rule.RoleCode, rule.LandCoverCodes,
                    rule.MinimumSlopeDegrees, rule.MaximumSlopeDegrees,
                    rule.RequiresWaterMask, 자연경관SeasonCodes.All.ToArray(),
                    new[] { 자연경관MoodCodes.Peaceful }, rule.MotionPolicyCode,
                    rule.ShaderFeatureCodes, rule.MinimumViewDistance,
                    rule.MaximumViewDistance, rule.GpuBudgetTierCode,
                    rule.ShadowPolicyCode);
                entries.Add(entry);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<자연경관CompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(entries.ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static string PrefabPath(string setName, string variant)
            => PrefabRoot + "/" + setName.Replace(" ", string.Empty)
                + "_" + variant + ".prefab";

        private static GameObject BuildPrefab(string setName, string variant)
        {
            var root = new GameObject(setName.Replace(" ", string.Empty) + "_" + variant);
            try
            {
                var environment = Child(root.transform, "EnvironmentRoot");
                var occlusion = Child(root.transform, "OcclusionRoot");
                var detail = Child(root.transform, "DetailRoot");
                var fx = Child(root.transform, "FxRoot");
                var sources = Sources(setName, variant);
                for (var index = 0; index < sources.Length; index++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(sources[index].Path)
                        ?? throw new InvalidOperationException(
                            "NatureCompositionSourceMissing:" + sources[index].Path);
                    var parent = sources[index].RootCode == SourceRootCodes.Occlusion
                        ? occlusion
                        : sources[index].RootCode == SourceRootCodes.Detail
                            ? detail
                            : sources[index].RootCode == SourceRootCodes.Fx
                                ? fx : environment;
                    var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject
                        ?? throw new InvalidOperationException(
                            "NatureCompositionInstantiateFailed:" + sources[index].Path);
                    var offset = Offset(index, variant);
                    instance.transform.localPosition = offset;
                    instance.transform.localRotation = Quaternion.Euler(
                        0f, index * 67f + VariantOffset(variant), 0f);
                    var scale = 0.72f + index * .08f + VariantOffset(variant) * .002f;
                    instance.transform.localScale = Vector3.one * scale;
                }

                var view = root.AddComponent<자연경관CompositionSetView>();
                view.Configure(setName, variant, environment, occlusion, detail,
                    fx, Footprint(setName));
                var shadowPolicy = root.AddComponent<자연경관ShadowPolicyView>();
                shadowPolicy.Configure(
                    Rule(setName).ShadowPolicyCode,
                    environment, occlusion, detail, fx);
                if (!view.ValidateWiring())
                    throw new InvalidOperationException(
                        "NatureCompositionWiringInvalid:" + setName + ":" + variant);
                if (!shadowPolicy.ValidateWiring())
                    throw new InvalidOperationException(
                        "NatureShadowPolicyWiringInvalid:" + setName + ":" + variant);

                var path = PrefabPath(setName, variant);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path)
                    ?? throw new InvalidOperationException(
                        "NatureCompositionSaveFailed:" + path);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SourceDefinition[] Sources(string setName, string variant)
        {
            if (setName == 자연경관SetNames.활엽수림군집)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Trees/SM_Tree_Round_01"),
                        Occlusion("Trees/SM_Tree_Round_03"),
                        Occlusion("Trees/SM_Tree_Birch_02"),
                        Detail("Plants/SM_Plant_Undergrowth_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Trees/SM_Tree_01"),
                            Occlusion("Trees/SM_Tree_Round_05"),
                            Occlusion("Trees/SM_Tree_Birch_04"),
                            Detail("Plants/SM_Plant_Fern_01"))
                        : Sources(
                            Environment("Trees/SM_Tree_Large_01"),
                            Occlusion("Trees/SM_Tree_Round_02"),
                            Occlusion("Trees/SM_Tree_Birch_01"),
                            Detail("Plants/SM_Plant_Bush_Leaves_01"));
            if (setName == 자연경관SetNames.침엽수림군집)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Trees/SM_Tree_Pine_01"),
                        Occlusion("Trees/SM_Tree_Pine_Large_01"),
                        Occlusion("Trees/SM_Tree_PolyPine_01"),
                        Detail("Plants/SM_Plant_Grass_03"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Trees/SM_Tree_Pine_02"),
                            Occlusion("Trees/SM_Tree_Pine_Large_02"),
                            Occlusion("Trees/SM_Tree_PolyPine_02"),
                            Detail("Rocks/SM_Rock_02"))
                        : Sources(
                            Environment("Trees/SM_Tree_Pine_Small_01"),
                            Occlusion("Trees/SM_Tree_Pine_Large_01"),
                            Occlusion("Trees/SM_Tree_PolyPine_03"),
                            Detail("Plants/SM_Plant_Undergrowth_01"));
            if (setName == 자연경관SetNames.혼효림군집)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Trees/SM_Tree_Round_01"),
                        Occlusion("Trees/SM_Tree_Pine_01"),
                        Occlusion("Trees/SM_Tree_Birch_02"),
                        Detail("Plants/SM_Plant_Fern_02"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Trees/SM_Tree_03"),
                            Occlusion("Trees/SM_Tree_PolyPine_02"),
                            Occlusion("Trees/SM_Tree_Birch_04"),
                            Detail("Rocks/SM_Rock_Small_01"))
                        : Sources(
                            Environment("Trees/SM_Tree_Large_01"),
                            Occlusion("Trees/SM_Tree_Pine_02"),
                            Occlusion("Trees/SM_Tree_Round_04"),
                            Detail("Plants/SM_Plant_Bush_02"));
            if (setName == 자연경관SetNames.수변완충지)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Terrain/SM_Terrain_RiverSide_01"),
                        Occlusion("Plants/SM_Plant_Reeds_01"),
                        Detail("Plants/SM_Plant_Grass_02"),
                        Detail("Rocks/SM_Rock_Small_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Terrain/SM_Terrain_RiverSide_Corner_01"),
                            Occlusion("Plants/SM_Plant_Reeds_02"),
                            Detail("Plants/SM_Plant_Grass_04"),
                            Detail("Rocks/SM_Rock_03"))
                        : Sources(
                            Environment("Terrain/SM_Terrain_RiverSide_Corner_02"),
                            Occlusion("Plants/SM_Plant_Reeds_01"),
                            Detail("Plants/SM_Plant_FlowerPatch_01"),
                            Detail("Rocks/SM_Rock_Small_02"));
            if (setName == 자연경관SetNames.바위절개지)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Rocks/SM_Rock_Wall_01"),
                        Occlusion("Rocks/SM_Rock_Cluster_Large_01"),
                        Detail("Rocks/SM_Rock_Pile_01"),
                        Detail("Plants/SM_Plant_Grass_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Rocks/SM_Rock_Wall_02"),
                            Occlusion("Rocks/SM_Rock_Cluster_Large_03"),
                            Detail("Rocks/SM_Rock_Pile_03"),
                            Detail("Trees/SM_Tree_Pine_Small_01"))
                        : Sources(
                            Environment("Rocks/SM_Rock_Wall_01"),
                            Occlusion("Rocks/SM_Rock_Cluster_Large_05"),
                            Detail("Rocks/SM_Rock_Pile_Curved_01"),
                            Detail("Plants/SM_Plant_Undergrowth_01"));
            if (setName == 자연경관SetNames.산능선)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Terrain/SM_Terrain_Mountain_01"),
                        Environment("Terrain/SM_Terrain_Mountain_02"),
                        Environment("Terrain/SM_MountainSkybox_01"),
                        Detail("Rocks/SM_Rock_Cluster_Large_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Terrain/SM_Terrain_Mountain_02"),
                            Environment("Terrain/SM_Terrain_Mountain_03"),
                            Environment("Terrain/SM_MountainSkybox_01"),
                            Detail("Rocks/SM_Rock_Wall_02"))
                        : Sources(
                            Environment("Terrain/SM_Terrain_Mountain_03"),
                            Environment("Terrain/SM_Terrain_Mountain_01"),
                            Environment("Terrain/SM_MountainSkybox_01"),
                            Detail("Rocks/SM_Rock_Cluster_Large_05"));
            if (setName == 자연경관SetNames.숲가장자리)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Plants/SM_Plant_Hedge_Bush_01"),
                        Occlusion("Plants/SM_Plant_Bush_Leaves_01"),
                        Detail("Plants/SM_Plant_Undergrowth_01"),
                        Detail("Plants/SM_Plant_Grass_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Plants/SM_Plant_Hedge_Bush_02"),
                            Occlusion("Plants/SM_Plant_Bush_Leaves_02"),
                            Detail("Plants/SM_Plant_Fern_Leaves_01"),
                            Detail("Plants/SM_Plant_FlowerPatch_01"))
                        : Sources(
                            Environment("Plants/SM_Plant_Bush_01"),
                            Occlusion("Plants/SM_Plant_Bush_Leaves_03"),
                            Detail("Plants/SM_Plant_Undergrowth_01"),
                            Detail("Plants/SM_Plant_Mushrooms_01"));
            if (setName == 자연경관SetNames.개울회랑)
                return variant == 월드CompositionVariantCodes.A
                    ? Sources(
                        Environment("Terrain/SM_River_Plane_01"),
                        Occlusion("Terrain/SM_Terrain_RiverSide_01"),
                        Detail("Plants/SM_Plant_Reeds_01"),
                        Fx("FX/FX_Water_Ripple_01"))
                    : variant == 월드CompositionVariantCodes.B
                        ? Sources(
                            Environment("Terrain/SM_River_Plane_Dip_01"),
                            Occlusion("Terrain/SM_Terrain_RiverSide_Corner_01"),
                            Detail("Plants/SM_Plant_Reeds_02"),
                            Fx("FX/FX_StreamParticle_Small_01"))
                        : Sources(
                            Environment("Terrain/SM_River_Plane_WaterFall_01"),
                            Occlusion("Terrain/SM_Terrain_RiverSide_Corner_02"),
                            Detail("Plants/SM_Plant_Lillypad_Large_01"),
                            Fx("FX/FX_Waterfall_Foam_01"));
            throw new InvalidOperationException("NatureCompositionSetUnknown:" + setName);
        }

        private static SourceDefinition[] Sources(params SourceDefinition[] values)
            => values;

        private static SourceDefinition Environment(string name)
            => Source(name, SourceRootCodes.Environment);

        private static SourceDefinition Occlusion(string name)
            => Source(name, SourceRootCodes.Occlusion);

        private static SourceDefinition Detail(string name)
            => Source(name, SourceRootCodes.Detail);

        private static SourceDefinition Fx(string name)
            => Source(name, SourceRootCodes.Fx);

        private static SourceDefinition Source(string name, string rootCode)
            => new(Nature + name + ".prefab", rootCode);

        private static Vector2 Footprint(string setName)
            => setName == 자연경관SetNames.산능선
                ? new Vector2(36f, 18f)
                : setName == 자연경관SetNames.개울회랑
                    ? new Vector2(14f, 7f)
                    : setName == 자연경관SetNames.수변완충지
                ? new Vector2(10f, 6f)
                : setName == 자연경관SetNames.바위절개지
                    ? new Vector2(10f, 5f)
                    : setName == 자연경관SetNames.숲가장자리
                        ? new Vector2(12f, 6f)
                    : new Vector2(12f, 12f);

        private static SetRule Rule(string setName)
        {
            if (setName == 자연경관SetNames.활엽수림군집)
                return new SetRule(자연경관RoleCodes.Canopy,
                    new[] { 법정동LandCoverCodes.Forest }, 0f, 35f, false,
                    자연경관MotionPolicyCodes.VegetationWind,
                    new[] { 자연경관ShaderFeatureCodes.VegetationWind },
                    8f, 180f, 자연경관GpuBudgetTierCodes.Region, true,
                    자연경관ShadowPolicyCodes.CastReceive);
            if (setName == 자연경관SetNames.침엽수림군집)
                return new SetRule(자연경관RoleCodes.Canopy,
                    new[] { 법정동LandCoverCodes.Forest }, 0f, 45f, false,
                    자연경관MotionPolicyCodes.VegetationWind,
                    new[] { 자연경관ShaderFeatureCodes.VegetationWind,
                        자연경관ShaderFeatureCodes.MossSnow },
                    8f, 220f, 자연경관GpuBudgetTierCodes.Region, true,
                    자연경관ShadowPolicyCodes.CastReceive);
            if (setName == 자연경관SetNames.혼효림군집)
                return new SetRule(자연경관RoleCodes.Canopy,
                    new[] { 법정동LandCoverCodes.Forest }, 0f, 40f, false,
                    자연경관MotionPolicyCodes.VegetationWind,
                    new[] { 자연경관ShaderFeatureCodes.VegetationWind },
                    8f, 200f, 자연경관GpuBudgetTierCodes.Region, true,
                    자연경관ShadowPolicyCodes.CastReceive);
            if (setName == 자연경관SetNames.수변완충지)
                return new SetRule(자연경관RoleCodes.WaterEdge,
                    new[] { 법정동LandCoverCodes.Water }, 0f, 12f, true,
                    자연경관MotionPolicyCodes.VegetationWind,
                    new[] { 자연경관ShaderFeatureCodes.VegetationWind,
                        자연경관ShaderFeatureCodes.Water },
                    0f, 100f, 자연경관GpuBudgetTierCodes.Task, false,
                    자연경관ShadowPolicyCodes.ReceiveOnly);
            if (setName == 자연경관SetNames.바위절개지)
                return new SetRule(자연경관RoleCodes.TerrainTransition,
                    new[] { 법정동LandCoverCodes.BareGround }, 15f, 70f, false,
                    자연경관MotionPolicyCodes.Static,
                    new[] { 자연경관ShaderFeatureCodes.MossSnow,
                        자연경관ShaderFeatureCodes.TerrainNormal },
                    5f, 180f, 자연경관GpuBudgetTierCodes.Region, true,
                    자연경관ShadowPolicyCodes.CastReceive);
            if (setName == 자연경관SetNames.산능선)
                // 원경 실루엣 보강용이므로 배치 지점 경사로 DEM을 대체하지 않습니다.
                return new SetRule(자연경관RoleCodes.Backdrop,
                    new[] { 법정동LandCoverCodes.Forest,
                        법정동LandCoverCodes.BareGround }, 0f, 90f, false,
                    자연경관MotionPolicyCodes.Static,
                    new[] { 자연경관ShaderFeatureCodes.MossSnow,
                        자연경관ShaderFeatureCodes.TerrainNormal },
                    70f, 900f, 자연경관GpuBudgetTierCodes.Overview, true,
                    자연경관ShadowPolicyCodes.Disabled);
            if (setName == 자연경관SetNames.숲가장자리)
                return new SetRule(자연경관RoleCodes.Understory,
                    new[] { 법정동LandCoverCodes.Forest,
                        법정동LandCoverCodes.Cropland }, 0f, 35f, false,
                    자연경관MotionPolicyCodes.VegetationWind,
                    new[] { 자연경관ShaderFeatureCodes.VegetationWind },
                    0f, 80f, 자연경관GpuBudgetTierCodes.Task, false,
                    자연경관ShadowPolicyCodes.ReceiveOnly);
            if (setName == 자연경관SetNames.개울회랑)
                return new SetRule(자연경관RoleCodes.WaterEdge,
                    new[] { 법정동LandCoverCodes.Water }, 0f, 10f, true,
                    자연경관MotionPolicyCodes.WaterSurface,
                    new[] { 자연경관ShaderFeatureCodes.Water,
                        자연경관ShaderFeatureCodes.VegetationWind },
                    0f, 140f, 자연경관GpuBudgetTierCodes.Task, false,
                    자연경관ShadowPolicyCodes.ReceiveOnly);
            throw new InvalidOperationException("NatureCompositionRuleMissing:" + setName);
        }

        private static Vector3 Offset(int index, string variant)
        {
            var baseOffsets = new[]
            {
                new Vector3(-2.8f, 0f, -1.5f),
                new Vector3(2.4f, 0f, -1.1f),
                new Vector3(.3f, 0f, 2.2f),
                new Vector3(-.6f, 0f, -.1f),
            };
            var offset = baseOffsets[index];
            if (variant == 월드CompositionVariantCodes.B) offset += new Vector3(.7f, 0f, -.5f);
            if (variant == 월드CompositionVariantCodes.C) offset += new Vector3(-.5f, 0f, .8f);
            return offset;
        }

        private static float VariantOffset(string variant)
            => variant == 월드CompositionVariantCodes.B ? 19f
                : variant == 월드CompositionVariantCodes.C ? 37f : 0f;

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static class SourceRootCodes
        {
            public const string Environment = "environment";
            public const string Occlusion = "occlusion";
            public const string Detail = "detail";
            public const string Fx = "fx";
        }

        private sealed class SourceDefinition
        {
            public SourceDefinition(string path, string rootCode)
            {
                Path = path;
                RootCode = rootCode;
            }

            public string Path { get; }
            public string RootCode { get; }
        }

        private sealed class SetRule
        {
            public SetRule(
                string roleCode,
                string[] landCoverCodes,
                float minimumSlopeDegrees,
                float maximumSlopeDegrees,
                bool requiresWaterMask,
                string motionPolicyCode,
                string[] shaderFeatureCodes,
                float minimumViewDistance,
                float maximumViewDistance,
                string gpuBudgetTierCode,
                bool hlodEligible,
                string shadowPolicyCode)
            {
                RoleCode = roleCode;
                LandCoverCodes = landCoverCodes;
                MinimumSlopeDegrees = minimumSlopeDegrees;
                MaximumSlopeDegrees = maximumSlopeDegrees;
                RequiresWaterMask = requiresWaterMask;
                MotionPolicyCode = motionPolicyCode;
                ShaderFeatureCodes = shaderFeatureCodes;
                MinimumViewDistance = minimumViewDistance;
                MaximumViewDistance = maximumViewDistance;
                GpuBudgetTierCode = gpuBudgetTierCode;
                HlodEligible = hlodEligible;
                ShadowPolicyCode = shadowPolicyCode;
            }

            public string RoleCode { get; }
            public string[] LandCoverCodes { get; }
            public float MinimumSlopeDegrees { get; }
            public float MaximumSlopeDegrees { get; }
            public bool RequiresWaterMask { get; }
            public string MotionPolicyCode { get; }
            public string[] ShaderFeatureCodes { get; }
            public float MinimumViewDistance { get; }
            public float MaximumViewDistance { get; }
            public string GpuBudgetTierCode { get; }
            public bool HlodEligible { get; }
            public string ShadowPolicyCode { get; }
        }
    }
}
