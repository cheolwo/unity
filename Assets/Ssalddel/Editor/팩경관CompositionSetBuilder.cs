using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static class 팩경관CompositionSetBuilder
    {
        public const string CatalogRevision = "pyeongchang-four-pack-composition.v3";
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창FarmTownCity경관CompositionCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/PackCompositionSets";

        private const string TownRoot = "Assets/Synty/PolygonTown/Prefabs/";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs/";
        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string NatureRoot = "Assets/Synty/PolygonNature/Prefabs/";

        [MenuItem("Ssalddel/World Placement/Farm·Town·City 경관 조합 대장 생성")]
        public static 팩경관CompositionCatalog Build()
        {
            var farmCatalog = AssetDatabase.LoadAssetAtPath<농장풍경CompositionCatalog>(
                    농장풍경CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException("FarmCompositionCatalogMissing");
            farmCatalog.Validate();
            EnsureFolder(PrefabRoot);
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));

            var entries = new List<팩경관CompositionCatalogEntry>();
            var farmDescriptors = 농장풍경CompositionAdapter.Adapt(farmCatalog)
                .ToDictionary(value => value.CompositionKey, StringComparer.Ordinal);
            foreach (var farmEntry in farmCatalog.Entries)
            {
                var descriptorKey = 월드CompositionDescriptor.BuildKey(
                    월드CompositionPackCodes.Farm,
                    farmEntry.SetName,
                    farmEntry.VariantCode);
                var rules = FarmRules(farmEntry.SetName);
                entries.Add(Entry(
                    farmDescriptors[descriptorKey], farmEntry.Prefab,
                    rules.LandCovers, new[] { 법정동WorldRoleCodes.Farm },
                    rules.SlopeRange, false));
            }

            var definitions = CreateDefinitions();
            월드CompositionContractValidator.Validate(
                definitions.Select(value => value.Descriptor).ToArray());
            entries.AddRange(definitions.Select(definition => Entry(
                definition.Descriptor,
                BuildPrefab(definition),
                definition.AllowedLandCoverCodes,
                definition.AllowedRegionRoleCodes,
                definition.SlopeRange,
                false)));

            var catalog = AssetDatabase.LoadAssetAtPath<팩경관CompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<팩경관CompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(CatalogRevision, entries
                .OrderBy(value => value.CompositionKey, StringComparer.Ordinal)
                .ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"PackLandscapeCompositionCatalogBuilt:{entries.Count}:{CatalogPath}");
            return catalog;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static 팩경관CompositionCatalogEntry Entry(
            월드CompositionDescriptor descriptor,
            GameObject prefab,
            string[] landCovers,
            string[] roles,
            Vector2 slopeRange,
            bool clusterAllowed)
        {
            var entry = new 팩경관CompositionCatalogEntry();
            entry.Configure(
                descriptor, prefab, landCovers, roles, slopeRange,
                false, clusterAllowed);
            return entry;
        }

        private static RuleSet FarmRules(string setName)
        {
            if (setName == 농장풍경SetNames.농로교차로)
                return new RuleSet(
                    new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Cropland },
                    new Vector2(0f, 12f));
            if (setName == 농장풍경SetNames.수목완충지)
                return new RuleSet(
                    new[] { 법정동LandCoverCodes.Forest, 법정동LandCoverCodes.Cropland },
                    new Vector2(0f, 30f));
            if (setName == 농장풍경SetNames.헛간작업마당
                || setName == 농장풍경SetNames.농기계대기장
                || setName == 농장풍경SetNames.농산물직판장
                || setName == 농장풍경SetNames.수확물집하장
                || setName == 농장풍경SetNames.시설하우스단동
                || setName == 농장풍경SetNames.시설하우스병렬단지)
                return new RuleSet(
                    new[] { 법정동LandCoverCodes.Cropland, 법정동LandCoverCodes.Residential },
                    new Vector2(0f, 12f));
            return new RuleSet(
                new[] { 법정동LandCoverCodes.Cropland }, new Vector2(0f, 18f));
        }

        private static Definition[] CreateDefinitions()
        {
            var values = new List<Definition>();
            foreach (var variant in 월드CompositionVariantCodes.All)
            {
                var number = variant == "A" ? "01" : variant == "B" ? "04" : "07";
                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.저층주택블록,
                    variant, 22f, 18f,
                    new[] { 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Buildings/Presets/SM_Bld_House_Preset_" + number + ".prefab", 0f, 2f),
                    P(TownRoot + "Environment/SM_Env_Fence_White_Straight_01.prefab", -6f, -5f),
                    P(TownRoot + "Environment/SM_Env_Bush_0" + (variant == "C" ? "4" : variant == "B" ? "3" : "2") + ".prefab", 6f, -4f)));

                var shopNumber = variant == "A" ? "01" : variant == "B" ? "02" : "03";
                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.읍내상점전면,
                    variant, 20f, 16f,
                    new[] { 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", 0f, 2f),
                    P(TownRoot + "Props/SM_Prop_ParkBench_01.prefab", -5f, -4f),
                    P(TownRoot + (variant == "A"
                        ? "Props/SM_Prop_Sign_01.prefab"
                        : "Props/SM_Prop_Sign_Stop_01.prefab"), 5f, -4f)));

                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.정원담장경계,
                    variant, 18f, 8f,
                    new[] { 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Environment/SM_Env_Fence_Wood_Straight_01.prefab", -5f, 0f),
                    P(TownRoot + "Environment/SM_Env_Fence_Wood_Gate_01.prefab", 0f, 0f),
                    P(TownRoot + "Environment/SM_Env_Gardenbox_" + (variant == "B" ? "Double" : "Single") + "_01.prefab", 5f, 0f)));

                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.버스정류장보행쉼터,
                    variant, 20f, 12f,
                    new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Corridor },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Environment/SM_Env_Road_Parking_01.prefab", 0f, 0f),
                    P(TownRoot + "Props/SM_Prop_Sign_BusStop_01.prefab", -5f, -3f),
                    P(TownRoot + "Props/SM_Prop_ParkBench_01.prefab", 3f, -3f, variant == "B" ? 180f : 0f)));

                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.생활서비스골목,
                    variant, 18f, 16f,
                    new[] { 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Buildings/SM_Bld_GardenShed_01.prefab", -3f, 2f),
                    P(TownRoot + "Props/SM_Prop_Cart_01.prefab", 4f, -2f, variant == "C" ? 90f : 0f),
                    P(TownRoot + "Props/SM_Prop_TrashBag_0" + (variant == "B" ? "2" : "1") + ".prefab", 0f, -3f)));

                values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.소형배달주차공간,
                    variant, 22f, 18f,
                    new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Corridor },
                    new[] { 법정동WorldRoleCodes.Town },
                    P(TownRoot + "Buildings/Presets/SM_Bld_House_Preset_Garage_01.prefab", 0f, 3f),
                    P(TownRoot + "Vehicles/SM_Veh_Truck_Delivery_01.prefab", -4f, -4f, variant == "B" ? 20f : -20f),
                    P(TownRoot + "Environment/SM_Env_Road_Parking_01.prefab", 4f, -4f)));

                var stationNumber = variant == "A" ? "01" : variant == "B" ? "02" : "03";
                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.물류Station진입부,
                    variant, 28f, 22f,
                    new[] { 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Buildings/SM_Bld_Station_" + stationNumber + ".prefab", 0f, 3f),
                    P(CityRoot + "Environments/SM_Env_Road_01.prefab", 0f, -7f),
                    P(CityRoot + "Props/SM_Prop_Cone_01.prefab", -5f, -5f),
                    P(CityRoot + "Props/SM_Prop_Cone_02.prefab", 5f, -5f)));

                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.상하차Dock,
                    variant, 24f, 20f,
                    new[] { 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Buildings/SM_Bld_Station_" + stationNumber + ".prefab", 0f, 4f),
                    P(CityRoot + "Props/SM_Prop_Pallet_01.prefab", -5f, -4f),
                    P(CityRoot + "Props/SM_Prop_CardboardBox_0" + (variant == "A" ? "1" : variant == "B" ? "2" : "3") + ".prefab", 0f, -4f),
                    P(CityRoot + "Vehicles/SM_Veh_Car_Van_01.prefab", 5f, -4f, 180f)));

                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.화물대기야드,
                    variant, 24f, 18f,
                    new[] { 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Props/SM_Prop_Pallet_01.prefab", -5f, 2f),
                    P(CityRoot + "Props/SM_Prop_CardboardBox_01.prefab", -1f, 2f),
                    P(CityRoot + "Props/SM_Prop_CardboardBox_04.prefab", 3f, 2f),
                    P(CityRoot + "Props/SM_Prop_Barrier_01.prefab", 0f, -5f, variant == "C" ? 90f : 0f)));

                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.포장도로회차공간,
                    variant, 26f, 26f,
                    new[] { 법정동LandCoverCodes.Logistics, 법정동LandCoverCodes.Corridor },
                    new[] { 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Environments/SM_Env_Road_0" + (variant == "A" ? "1" : variant == "B" ? "2" : "3") + ".prefab", 0f, 0f),
                    P(CityRoot + "Environments/SM_Env_Road_Crossing_01.prefab", 0f, -7f),
                    P(CityRoot + "Props/SM_Prop_Cone_01.prefab", -6f, 6f),
                    P(CityRoot + "Props/SM_Prop_Cone_02.prefab", 6f, 6f)));

                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.안전서비스설비,
                    variant, 18f, 14f,
                    new[] { 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Props/SM_Prop_PowerBox_01.prefab", -4f, 2f),
                    P(CityRoot + "Props/SM_Prop_Barrier_01.prefab", 0f, -2f, variant == "B" ? 90f : 0f),
                    P(CityRoot + "Props/SM_Prop_Sign_Warning_01.prefab", 4f, 2f),
                    P(CityRoot + "Props/SM_Prop_Cone_0" + (variant == "C" ? "2" : "1") + ".prefab", 4f, -3f)));

                var transitionShop = variant == "A" ? "01" : variant == "B" ? "03" : "05";
                values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.TownHub전환경관,
                    variant, 22f, 18f,
                    new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Logistics, 법정동LandCoverCodes.Corridor },
                    new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub },
                    P(CityRoot + "Buildings/SM_Bld_Shop_" + transitionShop + ".prefab", 0f, 3f),
                    P(CityRoot + "Environments/SM_Env_Road_01.prefab", 0f, -6f),
                    P(CityRoot + "Props/SM_Prop_Sign_Bustop_01.prefab", -5f, -4f),
                    P(CityRoot + "Props/SM_Prop_Sign_Parking_01.prefab", 5f, -4f)));

                AddTownExtensions(values, variant, number, shopNumber);
                AddCityExtensions(values, variant, stationNumber, transitionShop);
                AddMixedTransitions(values, variant, number, shopNumber);
            }

            return values.ToArray();
        }

        private static void AddTownExtensions(
            ICollection<Definition> values,
            string variant,
            string houseNumber,
            string shopNumber)
        {
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.소도시도로직선,
                variant, 24f, 12f,
                new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, 0f),
                P(TownRoot + "Environment/SM_Env_Road_Crossing_01.prefab", -6f, 0f),
                P(TownRoot + "Props/SM_Prop_Streetlamp_01.prefab", 6f, -3f)));
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.T자교차로,
                variant, 24f, 24f,
                new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, 0f),
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, 6f, 90f),
                P(TownRoot + "Props/SM_Prop_Sign_Stop_01.prefab", 6f, 6f)));
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.십자교차로,
                variant, 26f, 26f,
                new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, 0f),
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, 0f, 90f),
                P(TownRoot + "Environment/SM_Env_Road_Crossing_02.prefab", 0f, -6f)));
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.텃밭형단독주택,
                variant, 24f, 20f,
                new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Cropland },
                new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Farm },
                P(TownRoot + "Buildings/Presets/SM_Bld_House_Preset_" + houseNumber + ".prefab", 0f, 3f),
                P(TownRoot + "Environment/SM_Env_Gardenbox_Double_01.prefab", -6f, -5f),
                P(TownRoot + "Environment/SM_Env_Fence_Wood_Gate_01.prefab", 6f, -4f)));
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.근린놀이터,
                variant, 22f, 18f,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(TownRoot + "Props/SM_Prop_Playground_Slide_01.prefab", -5f, 1f),
                P(TownRoot + "Props/SM_Prop_Playground_Swing_01.prefab", 4f, 2f),
                P(TownRoot + "Props/SM_Prop_ParkBench_01.prefab", 0f, -5f)));
            values.Add(D(월드CompositionPackCodes.Town, 타운경관SetNames.생활공공광장,
                variant, 24f, 20f,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(TownRoot + "Props/SM_Prop_Fountain_01.prefab", 0f, 1f),
                P(TownRoot + "Props/SM_Prop_ParkBench_01.prefab", -6f, -4f),
                P(TownRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", 6f, 4f)));
        }

        private static void AddCityExtensions(
            ICollection<Definition> values,
            string variant,
            string stationNumber,
            string shopNumber)
        {
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.도시진입교차로,
                variant, 30f, 30f,
                new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Logistics },
                new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town },
                P(CityRoot + "Environments/SM_Env_Road_01.prefab", 0f, 0f),
                P(CityRoot + "Environments/SM_Env_Road_02.prefab", 0f, 0f, 90f),
                P(CityRoot + "Props/SM_Prop_Sign_Street_01.prefab", 7f, -7f)));
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.도심마트앞마당,
                variant, 28f, 22f,
                new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Logistics },
                new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub },
                P(CityRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", 0f, 4f),
                P(CityRoot + "Environments/SM_Env_Road_ParkingLines_01.prefab", 0f, -6f),
                P(CityRoot + "Props/SM_Prop_CardboardBox_01.prefab", 6f, -4f)));
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.먹거리상점골목,
                variant, 28f, 20f,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(CityRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", -7f, 4f),
                P(CityRoot + "Buildings/SM_Bld_Shop_0" + (variant == "C" ? "6" : "2") + ".prefab", 7f, 4f),
                P(CityRoot + "Props/SM_Prop_ParkBench_01.prefab", 0f, -5f)));
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.공동주택생활마당,
                variant, 30f, 24f,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(CityRoot + "Buildings/SM_Bld_Apartment_0" + (variant == "A" ? "1" : variant == "B" ? "2" : "3") + ".prefab", 0f, 5f),
                P(CityRoot + "Props/SM_Prop_PicnicTable_01.prefab", -5f, -5f),
                P(CityRoot + "Props/SM_Prop_ParkBench_01.prefab", 5f, -5f)));
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.사무공공정보관앞,
                variant, 30f, 24f,
                new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Logistics },
                new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub },
                P(CityRoot + "Buildings/SM_Bld_OfficeSquare_0" + (variant == "A" ? "1" : variant == "B" ? "2" : "3") + ".prefab", 0f, 5f),
                P(CityRoot + "Props/SM_Prop_Sign_Street_02.prefab", -6f, -5f),
                P(CityRoot + "Props/SM_Prop_ParkBench_01.prefab", 5f, -5f)));
            values.Add(D(월드CompositionPackCodes.City, 도시물류경관SetNames.도시공원쉼터,
                variant, 26f, 22f,
                new[] { 법정동LandCoverCodes.Residential },
                new[] { 법정동WorldRoleCodes.Town },
                P(CityRoot + "Environments/SM_Env_Grass_01.prefab", 0f, 0f),
                P(CityRoot + "Props/SM_Prop_PicnicTable_01.prefab", -5f, 0f),
                P(CityRoot + "Props/SM_Prop_ParkBench_01.prefab", 5f, 0f),
                P(CityRoot + "Buildings/SM_Bld_Station_" + stationNumber + ".prefab", 0f, 8f)));
        }

        private static void AddMixedTransitions(
            ICollection<Definition> values,
            string variant,
            string houseNumber,
            string shopNumber)
        {
            values.Add(D(월드CompositionPackCodes.Mixed, 혼합전환경관SetNames.NatureFarm,
                variant, 26f, 18f,
                new[] { 법정동LandCoverCodes.Forest, 법정동LandCoverCodes.Cropland },
                new[] { 법정동WorldRoleCodes.Farm },
                P(NatureRoot + "Trees/SM_Tree_Round_01.prefab", -7f, 2f),
                P(FarmRoot + "Environments/SM_Env_Road_Dirt_Straight_01.prefab", 0f, -4f),
                P(FarmRoot + "Props/SM_Prop_Fence_Wood_01.prefab", 7f, 2f)));
            values.Add(D(월드CompositionPackCodes.Mixed, 혼합전환경관SetNames.FarmTown,
                variant, 28f, 20f,
                new[] { 법정동LandCoverCodes.Cropland, 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Corridor },
                new[] { 법정동WorldRoleCodes.Farm, 법정동WorldRoleCodes.Town },
                P(FarmRoot + "Buildings/SM_Bld_ProduceStand_01.prefab", -7f, 3f),
                P(TownRoot + "Environment/SM_Env_Road_01.prefab", 0f, -5f),
                P(TownRoot + "Buildings/Presets/SM_Bld_House_Preset_" + houseNumber + ".prefab", 7f, 3f)));
            values.Add(D(월드CompositionPackCodes.Mixed, 혼합전환경관SetNames.FarmHub,
                variant, 30f, 22f,
                new[] { 법정동LandCoverCodes.Cropland, 법정동LandCoverCodes.Logistics, 법정동LandCoverCodes.Corridor },
                new[] { 법정동WorldRoleCodes.Farm, 법정동WorldRoleCodes.Hub },
                P(FarmRoot + "Buildings/SM_Bld_WaterTower_01.prefab", -8f, 4f),
                P(CityRoot + "Environments/SM_Env_Road_01.prefab", 0f, -6f),
                P(CityRoot + "Props/SM_Prop_Pallet_01.prefab", 7f, 1f),
                P(CityRoot + "Props/SM_Prop_Cone_01.prefab", 8f, -4f)));
            values.Add(D(월드CompositionPackCodes.Mixed, 혼합전환경관SetNames.TownCity,
                variant, 30f, 22f,
                new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Corridor },
                new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub },
                P(TownRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", -7f, 4f),
                P(CityRoot + "Environments/SM_Env_Road_01.prefab", 0f, -6f),
                P(CityRoot + "Buildings/SM_Bld_Shop_" + shopNumber + ".prefab", 7f, 4f)));
        }

        private static Definition D(
            string packCode,
            string setName,
            string variantCode,
            float width,
            float depth,
            string[] landCovers,
            string[] regionRoles,
            params Placement[] placements)
        {
            var footprint = new Vector2(width, depth);
            var connectors = CreateConnectors(packCode, setName, footprint);
            var sockets = CreateSockets(setName, footprint);
            var descriptor = new 월드CompositionDescriptor();
            descriptor.Configure(
                월드CompositionDescriptor.BuildKey(packCode, setName, variantCode),
                setName,
                variantCode,
                packCode,
                packCode == 월드CompositionPackCodes.Mixed
                    ? 월드CompositionSourceKinds.Mixed
                    : 월드CompositionSourceKinds.SyntyNestedPrefab,
                footprint,
                Vector2.one,
                true,
                false,
                false,
                월드CompositionJourneyKindCodes.Stateful,
                new[]
                {
                    월드CompositionDetailTierCodes.World,
                    월드CompositionDetailTierCodes.Zone,
                    월드CompositionDetailTierCodes.Object,
                },
                connectors,
                sockets);
            return new Definition(
                descriptor, placements, landCovers, regionRoles,
                packCode == 월드CompositionPackCodes.City
                    || packCode == 월드CompositionPackCodes.Mixed
                    ? new Vector2(0f, 10f)
                    : new Vector2(0f, 15f));
        }

        private static 월드CompositionConnectorContract[] CreateConnectors(
            string packCode,
            string setName,
            Vector2 footprint)
        {
            var vehicle = packCode == 월드CompositionPackCodes.City
                || packCode == 월드CompositionPackCodes.Mixed
                || setName == 타운경관SetNames.버스정류장보행쉼터
                || setName == 타운경관SetNames.소형배달주차공간;
            if (vehicle)
                return new[]
                {
                    Connector("vehicle-south", 월드CompositionConnectorDirectionCodes.South,
                        월드CompositionConnectorKindCodes.Vehicle,
                        packCode + ".vehicle.local", new Vector3(0f, 0f, -footprint.y * .5f), 180f, 4f),
                    Connector("pedestrian-north", 월드CompositionConnectorDirectionCodes.North,
                        월드CompositionConnectorKindCodes.Pedestrian,
                        packCode + ".pedestrian.local", new Vector3(0f, 0f, footprint.y * .5f), 0f, 2f),
                };
            return new[]
            {
                Connector("pedestrian-west", 월드CompositionConnectorDirectionCodes.West,
                    월드CompositionConnectorKindCodes.Pedestrian,
                    packCode + ".pedestrian.local", new Vector3(-footprint.x * .5f, 0f, 0f), 270f, 2f),
                Connector("pedestrian-east", 월드CompositionConnectorDirectionCodes.East,
                    월드CompositionConnectorKindCodes.Pedestrian,
                    packCode + ".pedestrian.local", new Vector3(footprint.x * .5f, 0f, 0f), 90f, 2f),
            };
        }

        private static 월드CompositionSocketContract[] CreateSockets(
            string setName,
            Vector2 footprint)
        {
            var sockets = new List<월드CompositionSocketContract>
            {
                Socket("interaction", 월드CompositionSocketCategoryCodes.Interaction,
                    new Vector3(0f, 0f, -footprint.y * .5f + 2f)),
            };
            if (setName.Contains("화물", StringComparison.Ordinal)
                || setName.Contains("상하차", StringComparison.Ordinal)
                || setName.Contains("배달", StringComparison.Ordinal)
                || setName.Contains("Station", StringComparison.Ordinal))
                sockets.Add(Socket("cargo", 월드CompositionSocketCategoryCodes.Cargo,
                    new Vector3(3f, 0f, -2f)));
            if (setName.Contains("차", StringComparison.Ordinal)
                || setName.Contains("주차", StringComparison.Ordinal)
                || setName.Contains("Station", StringComparison.Ordinal)
                || setName.Contains("야드", StringComparison.Ordinal))
                sockets.Add(Socket("vehicle", 월드CompositionSocketCategoryCodes.Vehicle,
                    new Vector3(-3f, 0f, -2f)));
            sockets.Add(Socket("actor", 월드CompositionSocketCategoryCodes.Actor,
                new Vector3(-2f, 0f, -footprint.y * .5f + 2f)));
            return sockets.ToArray();
        }

        private static 월드CompositionConnectorContract Connector(
            string code, string direction, string kind, string signature,
            Vector3 position, float yaw, float width)
        {
            var value = new 월드CompositionConnectorContract();
            value.Configure(code, direction, kind, signature, position, yaw, width, true);
            return value;
        }

        private static 월드CompositionSocketContract Socket(
            string code, string category, Vector3 position)
        {
            var value = new 월드CompositionSocketContract();
            value.Configure(code, category, position, Vector3.zero);
            return value;
        }

        private static Placement P(string assetPath, float x, float z, float yaw = 0f) =>
            new(assetPath, new Vector3(x, 0f, z), yaw);

        private static GameObject BuildPrefab(Definition definition)
        {
            var root = new GameObject(
                definition.Descriptor.PackCode + "_"
                + definition.Descriptor.SetName.Replace(" ", string.Empty)
                + "_" + definition.Descriptor.VariantCode);
            try
            {
                var environment = new GameObject("EnvironmentRoot").transform;
                environment.SetParent(root.transform, false);
                for (var index = 0; index < definition.Placements.Length; index++)
                {
                    var placement = definition.Placements[index];
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(placement.AssetPath)
                        ?? throw new InvalidOperationException(
                            "PackCompositionSourcePrefabMissing:" + placement.AssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(source, environment) as GameObject
                        ?? throw new InvalidOperationException(
                            "PackCompositionSourceInstantiateFailed:" + placement.AssetPath);
                    instance.name = "VisualPart_" + (index + 1).ToString("D2");
                    instance.transform.localPosition = placement.LocalPosition;
                    instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                }

                var connectorRoot = new GameObject("RouteConnectors").transform;
                connectorRoot.SetParent(root.transform, false);
                var connectors = definition.Descriptor.Connectors.Select(contract =>
                {
                    var anchor = new GameObject("Connector_" + contract.ConnectorCode).transform;
                    anchor.SetParent(connectorRoot, false);
                    anchor.localPosition = contract.LocalPosition;
                    anchor.localEulerAngles = new Vector3(0f, contract.LocalYaw, 0f);
                    return anchor;
                }).ToArray();

                var socketRoot = new GameObject("StateSockets").transform;
                socketRoot.SetParent(root.transform, false);
                var sockets = definition.Descriptor.Sockets.Select(contract =>
                {
                    var anchor = new GameObject("Socket_" + contract.SocketCode).transform;
                    anchor.SetParent(socketRoot, false);
                    anchor.localPosition = contract.LocalPosition;
                    anchor.localEulerAngles = contract.LocalEuler;
                    return anchor;
                }).ToArray();

                var view = root.AddComponent<팩경관CompositionSetView>();
                view.Configure(definition.Descriptor, environment, connectors, sockets);
                if (!view.ValidateWiring())
                    throw new InvalidOperationException(
                        "PackCompositionWiringInvalid:" + definition.Descriptor.CompositionKey);

                var packFolder = PrefabRoot + "/" + definition.Descriptor.PackCode;
                EnsureFolder(packFolder);
                var path = packFolder + "/"
                    + definition.Descriptor.SetName.Replace(" ", string.Empty)
                        .Replace("·", string.Empty).Replace("–", string.Empty)
                    + "_" + definition.Descriptor.VariantCode + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path)
                    ?? throw new InvalidOperationException(
                        "PackCompositionPrefabSaveFailed:" + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private readonly struct RuleSet
        {
            public RuleSet(string[] landCovers, Vector2 slopeRange)
            {
                LandCovers = landCovers;
                SlopeRange = slopeRange;
            }

            public string[] LandCovers { get; }
            public Vector2 SlopeRange { get; }
        }

        private sealed class Definition
        {
            public Definition(
                월드CompositionDescriptor descriptor,
                Placement[] placements,
                string[] landCovers,
                string[] roles,
                Vector2 slopeRange)
            {
                Descriptor = descriptor;
                Placements = placements;
                AllowedLandCoverCodes = landCovers;
                AllowedRegionRoleCodes = roles;
                SlopeRange = slopeRange;
            }

            public 월드CompositionDescriptor Descriptor { get; }
            public Placement[] Placements { get; }
            public string[] AllowedLandCoverCodes { get; }
            public string[] AllowedRegionRoleCodes { get; }
            public Vector2 SlopeRange { get; }
        }

        private readonly struct Placement
        {
            public Placement(string assetPath, Vector3 localPosition, float yaw)
            {
                AssetPath = assetPath;
                LocalPosition = localPosition;
                Yaw = yaw;
            }

            public string AssetPath { get; }
            public Vector3 LocalPosition { get; }
            public float Yaw { get; }
        }
    }
}
