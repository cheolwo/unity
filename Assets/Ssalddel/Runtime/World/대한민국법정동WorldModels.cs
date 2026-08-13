using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 법정동WorldRoleCodes
    {
        public const string Region = "Region";
        public const string Farm = "Farm";
        public const string Hub = "Hub";
        public const string Town = "Town";
    }

    public static class 법정동WorldEvidenceCodes
    {
        public const string OfficialBoundaryRepresentativePoint =
            "OfficialBoundaryRepresentativePoint";
        public const string SimulationScenario = "SimulationScenario";
        public const string RepresentativePointTopology =
            "RepresentativePointTopology";
        public const string OfficialBoundaryPolygon = "OfficialBoundaryPolygon";
        public const string AuthoritativeInternationalRaster =
            "AuthoritativeInternationalRaster";
        public const string OfficialDomesticComparisonRaster =
            "OfficialDomesticComparisonRaster";
        public const string StatisticallyAllocated = "StatisticallyAllocated";
    }

    public static class 법정동SpatialStatusCodes
    {
        public const string Complete = "Complete";
        public const string Incomplete = "Incomplete";
    }

    public static class 법정동LandCoverCodes
    {
        public const string Cropland = "Cropland";
        public const string Forest = "Forest";
        public const string Water = "Water";
        public const string BareGround = "BareGround";
        public const string Residential = "Residential";
        public const string Logistics = "Logistics";
        public const string Corridor = "Corridor";
    }

    public static class 법정동경관VisualKeys
    {
        public const string MountainSoft = "legal.terrain.mountain.soft";
        public const string TreePatch = "legal.vegetation.tree.patch";
        public const string Tree = "legal.vegetation.tree";
        public const string SoilRows = "legal.agriculture.soil.rows";
        public const string Potato = "legal.agriculture.crop.potato";
        public const string Barn = "legal.agriculture.building.barn";
        public const string Silo = "legal.agriculture.building.silo";
        public const string Farmhouse = "legal.rural.building.farmhouse";
        public const string Tractor = "legal.agriculture.vehicle.tractor";
        public const string ProduceStand = "legal.rural.building.produce-stand";
        public const string RuralRoad = "legal.road.rural";
        public const string Fence = "legal.rural.prop.fence";
        public const string Windmill = "legal.rural.prop.windmill";
        public const string WaterTower = "legal.transition.building.water-tower";
        public const string TownHouse = "legal.town.building.house";
        public const string LogisticsBuilding = "legal.logistics.building.station";
        public const string Pallet = "legal.logistics.cargo.pallet";
        public const string CargoBox = "legal.logistics.cargo.box";
        public const string Van = "legal.logistics.vehicle.van";
        public const string Greenhouse = "legal.agriculture.building.greenhouse";
        public const string ConiferTree = "legal.vegetation.tree.conifer";
        public const string Reeds = "legal.water-edge.reeds";
        public const string SmallRocks = "legal.bare-ground.rocks";

        public static readonly string[] All =
        {
            MountainSoft, TreePatch, Tree, SoilRows, Potato, Barn, Silo,
            Farmhouse, Tractor, ProduceStand, RuralRoad, Fence, Windmill,
            WaterTower, TownHouse, LogisticsBuilding, Pallet, CargoBox, Van,
            Greenhouse, ConiferTree, Reeds, SmallRocks,
        };
    }

    [Serializable]
    public sealed class 법정동WorldPointData
    {
        public float X;
        public float Z;

        public 법정동WorldPointData(float x, float z)
        {
            X = x;
            Z = z;
        }
    }

    [Serializable]
    public sealed class 법정동WorldNodeData
    {
        public string RegionStableId = string.Empty;
        public string LegalDongCode = string.Empty;
        public string KoreanName = string.Empty;
        public double Latitude;
        public double Longitude;
        public float LocalX;
        public float LocalZ;
        public string RoleCode = 법정동WorldRoleCodes.Region;
        public string RoleEvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario;
        public string BoundaryEvidenceCode = 법정동WorldEvidenceCodes.OfficialBoundaryPolygon;
        public string ElevationStatusCode = 법정동SpatialStatusCodes.Incomplete;
        public string ElevationEvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario;
        public string LandCoverStatusCode = 법정동SpatialStatusCodes.Incomplete;
        public string LandCoverEvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario;
        public 법정동WorldPointData[] BoundaryPoints = Array.Empty<법정동WorldPointData>();
    }

    [Serializable]
    public sealed class 법정동WorldRouteData
    {
        public string RouteStableId = string.Empty;
        public string FromRegionStableId = string.Empty;
        public string ToRegionStableId = string.Empty;
        public decimal RepresentativeDistanceKm;
        public string RouteEvidenceCode =
            법정동WorldEvidenceCodes.RepresentativePointTopology;
        public bool IsActualRoad;
    }

    [Serializable]
    public sealed class 법정동WorldProjectionData
    {
        public string ProjectionStableId = string.Empty;
        public string AdministrativeAreaCode = string.Empty;
        public string KoreanName = string.Empty;
        public string SourceName = string.Empty;
        public string SourceUrl = string.Empty;
        public string SourceSha256 = string.Empty;
        public string SourceVintage = string.Empty;
        public string GeometryCrs = string.Empty;
        public 법정동WorldNodeData[] Nodes = Array.Empty<법정동WorldNodeData>();
        public 법정동WorldRouteData[] Routes = Array.Empty<법정동WorldRouteData>();
    }

    [Serializable]
    public sealed class 법정동경관PlacementData
    {
        public string PlacementStableId = string.Empty;
        public string RegionStableId = string.Empty;
        public string VisualKey = string.Empty;
        public string LandCoverCode = string.Empty;
        public string RegionRoleCode = string.Empty;
        public string EvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario;
        public float LocalX;
        public float LocalZ;
        public float RotationY;
        public float Scale = 1f;
        public int DensityTier;
        public int LodGroup;
        public bool PresentationOnly = true;
    }

    [Serializable]
    public sealed class 법정동경관PlanData
    {
        public string PlanStableId = string.Empty;
        public int DeterministicSeed;
        public string RuleRevision = string.Empty;
        public string ElevationStatusCode = 법정동SpatialStatusCodes.Incomplete;
        public string LandCoverStatusCode = 법정동SpatialStatusCodes.Incomplete;
        public 법정동경관PlacementData[] Placements =
            Array.Empty<법정동경관PlacementData>();
    }

    public static class 법정동경관PlanValidator
    {
        public static void Validate(법정동경관PlanData value)
        {
            if (value == null || value.DeterministicSeed == 0
                || value.Placements.Length == 0
                || value.Placements.Select(item => item.PlacementStableId)
                    .Distinct(StringComparer.Ordinal).Count() != value.Placements.Length
                || value.Placements.Any(item => !item.PresentationOnly
                    || item.EvidenceCode != 법정동WorldEvidenceCodes.SimulationScenario
                    || !법정동경관VisualKeys.All.Contains(item.VisualKey, StringComparer.Ordinal)
                    || item.Scale <= 0f || item.DensityTier is < 0 or > 2
                    || item.LodGroup is < 0 or > 2))
                throw new InvalidOperationException("LegalDongScenicPlanInvalid");
        }
    }

    public static class 법정동WorldProjectionValidator
    {
        public static void Validate(법정동WorldProjectionData value)
        {
            if (value == null
                || string.IsNullOrWhiteSpace(value.ProjectionStableId)
                || value.SourceSha256.Length != 64
                || value.Nodes.Length == 0
                || value.Nodes.Any(node => node.LegalDongCode.Length != 10
                    || node.Latitude is < 33d or > 39.5d
                    || node.Longitude is < 124d or > 132d
                    || node.BoundaryEvidenceCode !=
                        법정동WorldEvidenceCodes.OfficialBoundaryPolygon
                    || node.BoundaryPoints.Length < 3)
                || value.Nodes.Select(node => node.RegionStableId)
                    .Distinct(StringComparer.Ordinal).Count() != value.Nodes.Length)
                throw new InvalidOperationException("LegalDongWorldProjectionInvalid");

            var nodeIds = new HashSet<string>(
                value.Nodes.Select(node => node.RegionStableId), StringComparer.Ordinal);
            if (value.Routes.Any(route => route.IsActualRoad
                    || route.RouteEvidenceCode !=
                        법정동WorldEvidenceCodes.RepresentativePointTopology
                    || route.RepresentativeDistanceKm <= 0m
                    || !nodeIds.Contains(route.FromRegionStableId)
                    || !nodeIds.Contains(route.ToRegionStableId)))
                throw new InvalidOperationException("LegalDongSimplifiedRouteInvalid");
        }
    }

    public static class 평창군법정동WorldFixture
    {
        public const string FarmRegionStableId = "region:kr:bjd:5176038000";
        public const string HubRegionStableId = "region:kr:bjd:5176036000";
        public const string TownRegionStableId = "region:kr:bjd:5176025000";

        public static 법정동WorldProjectionData Create()
        {
            var value = new 법정동WorldProjectionData
            {
                ProjectionStableId = "world-projection:kr:bjd:51760:20260701",
                AdministrativeAreaCode = "51760",
                KoreanName = "강원특별자치도 평창군",
                SourceName = "국토교통부 일별법정구역정보 · VWorld",
                SourceUrl = "https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=NA&dsId=21",
                SourceSha256 = "70B2FBD70FB1CD9BB31CD02EF3279778389D0F080C516F57EA682CD4F0D3F327",
                SourceVintage = "2026-07-01",
                GeometryCrs = "EPSG:5186 → EPSG:4326",
                Nodes = new[]
                {
                    Node("5176036000", "진부면", 37.6387363, 128.5653901, 10.514f, 14.488f,
                        법정동WorldRoleCodes.Hub, Points((-1.4f,3.66f),(1.39f,8.11f),(-2.17f,14.94f),(2.42f,14.94f),(4.09f,20.29f),(8.53f,22f),(12.58f,11.62f),(9.8f,9.06f),(12.63f,8.85f),(12.57f,6.2f),(14.97f,4.99f),(20.73f,4.14f),(20.58f,2.49f),(15.51f,-.25f),(14.03f,2.37f),(9.96f,1.3f),(9.55f,-4.49f),(7.27f,-3.31f),(5.88f,-6.97f),(-.97f,-4.93f),(1.45f,-.41f))),
                    Node("5176038000", "대관령면", 37.6851249, 128.6845495, 24f, 12f,
                        법정동WorldRoleCodes.Farm, Points((10.5f,18.31f),(22.03f,18.18f),(27.35f,15.84f),(29.99f,10.43f),(26.39f,8.96f),(26.86f,5.39f),(24.1f,3.66f),(15.22f,4.89f),(12.57f,6.2f),(12.63f,8.85f),(9.8f,9.06f),(12.58f,11.62f))),
                    Node("5176034000", "봉평면", 37.6011717, 128.3425146, -22f, 5f,
                        법정동WorldRoleCodes.Region, Points((-18.66f,-3.75f),(-23.74f,-2.85f),(-25.38f,4.3f),(-28.99f,6.63f),(-22.46f,12.37f),(-21.16f,11.33f),(-8.23f,11.62f),(-9.69f,6.13f),(-15.21f,2.58f),(-12.1f,1.27f),(-11.93f,-1.5f),(-17.69f,-2.24f))),
                    Node("5176032000", "방림면", 37.4560135, 128.3166057, -22f, -7f,
                        법정동WorldRoleCodes.Region, Points((-6.87f,-7.75f),(-7.31f,-10.55f),(-18.67f,-9.7f),(-23.95f,-11.62f),(-28.03f,-10.37f),(-30f,-5.41f),(-22.63f,-2.84f),(-17.17f,-4.49f),(-15f,-7.1f))),
                    Node("5176025000", "평창읍", 37.3590082, 128.3899721, -14f, -15f,
                        법정동WorldRoleCodes.Town, Points((-5.39f,-16.65f),(-9.34f,-16.08f),(-10.41f,-19.95f),(-14.49f,-20.32f),(-20.24f,-17.95f),(-20.07f,-13.56f),(-23.98f,-11.66f),(-15.18f,-9.38f),(-11.73f,-10.49f),(-7.31f,-10.55f),(-6.15f,-9.33f),(.44f,-9.93f),(-3.97f,-12.65f))),
                    Node("5176031000", "미탄면", 37.3389075, 128.5116027, 3f, -16f,
                        법정동WorldRoleCodes.Region, Points((-4.2f,-18.8f),(-3.05f,-11.86f),(.61f,-10.68f),(4.47f,-11.49f),(11.43f,-20.92f),(9.19f,-21.98f),(6.77f,-20.29f),(.31f,-20.29f))),
                    Node("5176035000", "용평면", 37.6267012, 128.4608995, -6f, 8f,
                        법정동WorldRoleCodes.Region, Points((-3.31f,14.98f),(-.98f,14.2f),(1.33f,7.89f),(-1.4f,3.66f),(-5.2f,2.87f),(-6.17f,.88f),(-10.56f,.67f),(-10.16f,-2.1f),(-15.21f,2.58f),(-9.69f,6.13f),(-7.18f,13.62f))),
                    Node("5176033000", "대화면", 37.5054806, 128.4236085, -8f, -4f,
                        법정동WorldRoleCodes.Region, Points((-1.31f,3.67f),(1.45f,-.41f),(-.97f,-4.93f),(2.57f,-6.48f),(.63f,-10.06f),(-6.15f,-9.33f),(-8.48f,-7.25f),(-15f,-7.1f),(-18.66f,-3.75f),(-17.51f,-2.19f),(-9.43f,-2.07f),(-10.56f,.67f),(-6.17f,.88f),(-5.2f,2.87f))),
                },
                Routes = new[]
                {
                    Route("jinbu-yongpyeong", "5176036000", "5176035000", 9.32m),
                    Route("yongpyeong-bongpyeong", "5176035000", "5176034000", 10.83m),
                    Route("daegwallyeong-jinbu", "5176038000", "5176036000", 11.71m),
                    Route("bongpyeong-daehwa", "5176034000", "5176033000", 12.81m),
                    Route("daehwa-bangnim", "5176033000", "5176032000", 10.94m),
                    Route("bangnim-pyeongchang", "5176032000", "5176025000", 12.58m),
                    Route("pyeongchang-mitan", "5176025000", "5176031000", 11.01m),
                },
            };
            법정동WorldProjectionValidator.Validate(value);
            return value;
        }

        private static 법정동WorldNodeData Node(
            string code, string name, double lat, double lon, float x, float z,
            string role, 법정동WorldPointData[] boundary)
            => new()
            {
                RegionStableId = "region:kr:bjd:" + code,
                LegalDongCode = code,
                KoreanName = name,
                Latitude = lat,
                Longitude = lon,
                LocalX = x,
                LocalZ = z,
                RoleCode = role,
                ElevationStatusCode = 법정동SpatialStatusCodes.Complete,
                ElevationEvidenceCode =
                    법정동WorldEvidenceCodes.AuthoritativeInternationalRaster,
                LandCoverStatusCode = 법정동SpatialStatusCodes.Complete,
                LandCoverEvidenceCode =
                    법정동WorldEvidenceCodes.AuthoritativeInternationalRaster,
                BoundaryPoints = boundary,
            };

        private static 법정동WorldPointData[] Points(params (float X, float Z)[] values)
            => values.Select(value => new 법정동WorldPointData(value.X, value.Z)).ToArray();

        private static 법정동WorldRouteData Route(
            string suffix, string fromCode, string toCode, decimal distanceKm)
            => new()
            {
                RouteStableId = "route:sim:bjd:51760:" + suffix,
                FromRegionStableId = "region:kr:bjd:" + fromCode,
                ToRegionStableId = "region:kr:bjd:" + toCode,
                RepresentativeDistanceKm = distanceKm,
            };
    }

    public static class 평창군경관Fixture
    {
        public static 법정동경관PlanData Create()
        {
            var items = new List<법정동경관PlacementData>();
            void Add(string id, string region, string key, string cover, string role,
                float x, float z, float rotation, float scale, int density, int lod)
                => items.Add(new 법정동경관PlacementData
                {
                    PlacementStableId = "scenic:sim:pyeongchang:" + id,
                    RegionStableId = region,
                    VisualKey = key,
                    LandCoverCode = cover,
                    RegionRoleCode = role,
                    LocalX = x,
                    LocalZ = z,
                    RotationY = rotation,
                    Scale = scale,
                    DensityTier = density,
                    LodGroup = lod,
                });

            var farm = 평창군법정동WorldFixture.FarmRegionStableId;
            Add("farm-mountain-a", farm, 법정동경관VisualKeys.MountainSoft, 법정동LandCoverCodes.Forest, 법정동WorldRoleCodes.Farm, 27f, 16f, 20f, .45f, 0, 0);
            Add("farm-tree-patch-a", farm, 법정동경관VisualKeys.TreePatch, 법정동LandCoverCodes.Forest, 법정동WorldRoleCodes.Farm, 26f, 8f, 35f, .55f, 0, 0);
            Add("farm-tree-patch-b", farm, 법정동경관VisualKeys.TreePatch, 법정동LandCoverCodes.Forest, 법정동WorldRoleCodes.Farm, 14f, 15f, -30f, .48f, 0, 0);
            for (var index = 0; index < 6; index++)
                Add("farm-field-" + index, farm, 법정동경관VisualKeys.SoilRows, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm,
                    18f + (index % 3) * 2.2f, 8f + (index / 3) * 2.3f, 8f, .32f, 1, 1);
            for (var index = 0; index < 12; index++)
                Add("farm-potato-" + index, farm, 법정동경관VisualKeys.Potato, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm,
                    17.2f + (index % 4) * 1.25f, 7.4f + (index / 4) * 1.35f, index * 17f, .42f, 2, 2);
            Add("farm-barn", farm, 법정동경관VisualKeys.Barn, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm, 23.5f, 12.5f, -55f, .32f, 0, 1);
            Add("farm-silo", farm, 법정동경관VisualKeys.Silo, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm, 25.3f, 11.5f, 0f, .27f, 1, 1);
            Add("farm-house", farm, 법정동경관VisualKeys.Farmhouse, 법정동LandCoverCodes.Residential, 법정동WorldRoleCodes.Farm, 21.8f, 15f, 145f, .35f, 0, 1);
            Add("farm-tractor", farm, 법정동경관VisualKeys.Tractor, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm, 21.5f, 10.7f, -70f, .58f, 1, 2);
            Add("farm-stand", farm, 법정동경관VisualKeys.ProduceStand, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm, 19f, 13.5f, 120f, .5f, 1, 1);
            Add("farm-windmill", farm, 법정동경관VisualKeys.Windmill, 법정동LandCoverCodes.Cropland, 법정동WorldRoleCodes.Farm, 15.5f, 13f, 15f, .6f, 1, 1);

            var hub = 평창군법정동WorldFixture.HubRegionStableId;
            Add("hub-mountain", hub, 법정동경관VisualKeys.MountainSoft, 법정동LandCoverCodes.Forest, 법정동WorldRoleCodes.Hub, 5f, 18f, -20f, .38f, 0, 0);
            Add("hub-trees", hub, 법정동경관VisualKeys.TreePatch, 법정동LandCoverCodes.Forest, 법정동WorldRoleCodes.Hub, 3.5f, 10f, 15f, .42f, 0, 0);
            Add("hub-station", hub, 법정동경관VisualKeys.LogisticsBuilding, 법정동LandCoverCodes.Logistics, 법정동WorldRoleCodes.Hub, 10f, 6f, 35f, .42f, 0, 1);
            Add("hub-water-tower", hub, 법정동경관VisualKeys.WaterTower, 법정동LandCoverCodes.Logistics, 법정동WorldRoleCodes.Hub, 7f, 7.5f, 0f, .5f, 1, 1);
            for (var index = 0; index < 3; index++)
                Add("hub-pallet-" + index, hub, 법정동경관VisualKeys.Pallet, 법정동LandCoverCodes.Logistics, 법정동WorldRoleCodes.Hub, 11f + index * 1.1f, 4.8f, 35f, .7f, 2, 2);
            for (var index = 0; index < 6; index++)
                Add("hub-box-" + index, hub, 법정동경관VisualKeys.CargoBox, 법정동LandCoverCodes.Logistics, 법정동WorldRoleCodes.Hub, 10.8f + (index % 3) * 1.1f, 4.8f + (index / 3) * .8f, index * 13f, .48f, 2, 2);
            Add("hub-van", hub, 법정동경관VisualKeys.Van, 법정동LandCoverCodes.Logistics, 법정동WorldRoleCodes.Hub, 8f, 4.7f, 35f, .6f, 1, 2);

            var town = 평창군법정동WorldFixture.TownRegionStableId;
            Add("town-house-a", town, 법정동경관VisualKeys.TownHouse, 법정동LandCoverCodes.Residential, 법정동WorldRoleCodes.Town, -16f, -15f, 20f, .34f, 0, 1);
            Add("town-house-b", town, 법정동경관VisualKeys.TownHouse, 법정동LandCoverCodes.Residential, 법정동WorldRoleCodes.Town, -12f, -14f, -15f, .3f, 1, 1);

            var value = new 법정동경관PlanData
            {
                PlanStableId = "scenic-plan:sim:pyeongchang:daegwallyeong-jinbu.v1",
                DeterministicSeed = 51760,
                RuleRevision = "legal-dong-scenic-placement.v1",
                ElevationStatusCode = 법정동SpatialStatusCodes.Complete,
                LandCoverStatusCode = 법정동SpatialStatusCodes.Complete,
                Placements = items.ToArray(),
            };
            법정동경관PlanValidator.Validate(value);
            return value;
        }
    }
}
