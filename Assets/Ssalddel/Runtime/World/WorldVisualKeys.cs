using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class WorldVisualCatalogCodes
    {
        public const string Farm = "farm";
        public const string Urban = "urban";
        public const string Transition = "transition";
        public const string Environment = "environment";

        public static bool IsKnown(string value)
            => value == Farm || value == Urban || value == Transition
                || value == Environment;
    }

    public static class FarmVisualKeys
    {
        public const string SoilDirt = "farm.soil.dirt";
        public const string SoilRows = "farm.soil.rows";
        public const string PotatoSmall = "farm.crop.potato.small";
        public const string PotatoMedium = "farm.crop.potato.medium";
        public const string PotatoLarge = "farm.crop.potato.large";
        public const string PotatoBox = "farm.cargo.potato-box";
        public const string Farmer = "farm.npc.farmer";
        public const string Barn = "farm.building.barn";
        public const string Silo = "farm.building.silo";
        public const string ProduceStand = "farm.building.produce-stand";
        public const string Tractor = "farm.vehicle.tractor";

        public static readonly string[] All =
        {
            SoilDirt, SoilRows, PotatoSmall, PotatoMedium, PotatoLarge,
            PotatoBox, Farmer, Barn, Silo, ProduceStand, Tractor,
        };
    }

    public static class UrbanVisualKeys
    {
        public const string LogisticsBuilding = "urban.building.logistics";
        public const string MarketBuilding = "urban.building.market";
        public const string Apartment = "urban.building.apartment";
        public const string Van = "urban.vehicle.van";
        public const string Pallet = "urban.cargo.pallet";
        public const string CargoBox = "urban.cargo.cardboard-box";
        public const string Shelf = "urban.market.shelf";
        public const string Desk = "urban.market.desk";

        public static readonly string[] All =
        {
            LogisticsBuilding, MarketBuilding, Apartment, Van,
            Pallet, CargoBox, Shelf, Desk,
        };
    }

    public static class TransitionVisualKeys
    {
        public const string RuralRoad = "transition.road.rural";
        public const string UrbanRoad = "transition.road.urban";

        public static readonly string[] All = { RuralRoad, UrbanRoad };
    }

    /// <summary>업무 상태를 담지 않는 Farm·City 배경 전용 Presentation key입니다.</summary>
    public static class EnvironmentVisualKeys
    {
        public const string FarmGroundFlat = "environment.farm.ground.flat";
        public const string FarmHillA = "environment.farm.hill.a";
        public const string FarmHillB = "environment.farm.hill.b";
        public const string FarmHillC = "environment.farm.hill.c";
        public const string FarmMountainA = "environment.farm.mountain.a";
        public const string FarmMountainB = "environment.farm.mountain.b";
        public const string FarmTreeClusterA = "environment.farm.tree-cluster.a";
        public const string FarmTreeClusterB = "environment.farm.tree-cluster.b";
        public const string FarmTreeA = "environment.farm.tree.a";
        public const string FarmTreeB = "environment.farm.tree.b";
        public const string FarmTreeC = "environment.farm.tree.c";
        public const string FarmTreeD = "environment.farm.tree.d";
        public const string FarmTreeLarge = "environment.farm.tree.large";
        public const string FarmTreeApple = "environment.farm.tree.fruit.apple";
        public const string FarmTreeCherry = "environment.farm.tree.fruit.cherry";
        public const string FarmTreeOrange = "environment.farm.tree.fruit.orange";
        public const string FarmGrassA = "environment.farm.grass.a";
        public const string FarmGrassB = "environment.farm.grass.b";
        public const string FarmGrassC = "environment.farm.grass.c";
        public const string FarmFlowersA = "environment.farm.flowers.a";
        public const string FarmFlowersB = "environment.farm.flowers.b";
        public const string FarmFlowersC = "environment.farm.flowers.c";
        public const string FarmPond = "environment.farm.pond";
        public const string FarmReedsA = "environment.farm.reeds.a";
        public const string FarmReedsB = "environment.farm.reeds.b";
        public const string FarmReedsC = "environment.farm.reeds.c";
        public const string FarmRocksA = "environment.farm.rocks.a";
        public const string FarmRocksB = "environment.farm.rocks.b";
        public const string FarmRocksC = "environment.farm.rocks.c";
        public const string FarmRocksD = "environment.farm.rocks.d";
        public const string FarmRocksE = "environment.farm.rocks.e";
        public const string FarmhouseA = "environment.farm.building.farmhouse.a";
        public const string FarmhouseB = "environment.farm.building.farmhouse.b";
        public const string FarmWindmill = "environment.farm.prop.windmill";
        public const string FarmWaterTower = "environment.farm.building.water-tower";
        public const string FarmBench = "environment.farm.prop.bench";
        public const string FarmWell = "environment.farm.prop.well";
        public const string FarmFence = "environment.farm.prop.fence";
        public const string FarmHayA = "environment.farm.prop.hay.a";
        public const string FarmHayB = "environment.farm.prop.hay.b";
        public const string FarmRoadStraight = "environment.farm.road.straight";
        public const string FarmRoadCurveA = "environment.farm.road.curve.a";
        public const string FarmRoadCurveB = "environment.farm.road.curve.b";
        public const string FarmWheat = "environment.farm.crop.wheat";
        public const string FarmCorn = "environment.farm.crop.corn";
        public const string CityTreeA = "environment.city.tree.a";
        public const string CityTreeB = "environment.city.tree.b";
        public const string CityTreeC = "environment.city.tree.c";
        public const string CityGrass = "environment.city.grass";
        public const string CityFlower = "environment.city.flower";
        public const string CityGrassPathStraight = "environment.city.path.grass.straight";
        public const string CityGrassPathCorner = "environment.city.path.grass.corner";
        public const string CityShopA = "environment.city.building.shop.a";
        public const string CityShopB = "environment.city.building.shop.b";
        public const string CityShopC = "environment.city.building.shop.c";
        public const string CityShopD = "environment.city.building.shop.d";
        public const string CityOffice = "environment.city.building.office";
        public const string CityStation = "environment.city.building.station";
        public const string CityParkBench = "environment.city.prop.park-bench";
        public const string CityBusStop = "environment.city.prop.bus-stop";
        public const string CityPlanter = "environment.city.prop.planter";
        public const string CityPicnicTable = "environment.city.prop.picnic-table";
        public const string CityUmbrella = "environment.city.prop.umbrella";
        public const string CityLightPole = "environment.city.prop.light-pole";
        public const string CityTrashCan = "environment.city.prop.trash-can";
        public const string CityRoad = "environment.city.road";

        public static readonly string[] All =
        {
            FarmGroundFlat, FarmHillA, FarmHillB, FarmHillC, FarmMountainA, FarmMountainB,
            FarmTreeClusterA, FarmTreeClusterB, FarmTreeA, FarmTreeB, FarmTreeC,
            FarmTreeD, FarmTreeLarge, FarmTreeApple, FarmTreeCherry,
            FarmTreeOrange, FarmGrassA, FarmGrassB, FarmGrassC,
            FarmFlowersA, FarmFlowersB, FarmFlowersC, FarmPond, FarmReedsA,
            FarmReedsB, FarmReedsC, FarmRocksA, FarmRocksB, FarmRocksC,
            FarmRocksD, FarmRocksE, FarmhouseA, FarmhouseB, FarmWindmill,
            FarmWaterTower, FarmBench, FarmWell, FarmFence, FarmHayA, FarmHayB,
            FarmRoadStraight, FarmRoadCurveA, FarmRoadCurveB, FarmWheat, FarmCorn,
            CityTreeA, CityTreeB, CityTreeC, CityGrass, CityFlower,
            CityGrassPathStraight, CityGrassPathCorner, CityShopA, CityShopB,
            CityShopC, CityShopD, CityOffice, CityStation, CityParkBench,
            CityBusStop, CityPlanter, CityPicnicTable, CityUmbrella,
            CityLightPole, CityTrashCan, CityRoad,
        };
    }

    public static class WorldVisualKeys
    {
        public static IReadOnlyList<string> All { get; } = FarmVisualKeys.All
            .Concat(UrbanVisualKeys.All)
            .Concat(TransitionVisualKeys.All)
            .Concat(EnvironmentVisualKeys.All)
            .ToArray();

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }
}
