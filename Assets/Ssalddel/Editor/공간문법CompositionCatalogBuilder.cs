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
    public static class 공간문법CompositionCatalogBuilder
    {
        public const string CatalogRevision = "pyeongchang-landscape-grammar.v1";
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창공간문법CompositionCatalog.asset";

        private static readonly SetSource[] FarmSets =
        {
            new(농장풍경SetNames.감자밭두렁, 농장풍경SetNames.감자밭두렁),
            new(농장풍경SetNames.혼합작물밭, 농장풍경SetNames.혼합작물밭),
            new(농장풍경SetNames.시설하우스단동, 농장풍경SetNames.시설하우스단동),
            new(농장풍경SetNames.시설하우스병렬단지, 농장풍경SetNames.시설하우스병렬단지),
            new(농장풍경SetNames.과수원블록, 농장풍경SetNames.과수원블록),
            new(농장풍경SetNames.논필지농수로표현, 농장풍경SetNames.논필지농수로표현),
            new(농장풍경SetNames.헛간작업마당, 농장풍경SetNames.헛간작업마당),
            new("농산물 집하·직판장", 농장풍경SetNames.수확물집하장),
        };

        private static readonly SetSource[] TownSets =
        {
            new(타운경관SetNames.저층주택블록, 타운경관SetNames.저층주택블록),
            new(타운경관SetNames.읍내상점전면, 타운경관SetNames.읍내상점전면),
            new(타운경관SetNames.정원담장경계, 타운경관SetNames.정원담장경계),
            new(타운경관SetNames.버스정류장보행쉼터, 타운경관SetNames.버스정류장보행쉼터),
            new(타운경관SetNames.근린놀이터, 타운경관SetNames.근린놀이터),
            new(타운경관SetNames.생활공공광장, 타운경관SetNames.생활공공광장),
        };

        private static readonly SetSource[] CitySets =
        {
            new(도시물류경관SetNames.물류Station진입부, 도시물류경관SetNames.물류Station진입부),
            new(도시물류경관SetNames.상하차Dock, 도시물류경관SetNames.상하차Dock),
            new(도시물류경관SetNames.화물대기야드, 도시물류경관SetNames.화물대기야드),
            new(도시물류경관SetNames.도심마트앞마당, 도시물류경관SetNames.도심마트앞마당),
            new(도시물류경관SetNames.먹거리상점골목, 도시물류경관SetNames.먹거리상점골목),
            new(도시물류경관SetNames.공동주택생활마당, 도시물류경관SetNames.공동주택생활마당),
        };

        [MenuItem("Ssalddel/World Placement/공간 문법 Composition 156개 생성")]
        public static 공간문법CompositionCatalog Build()
        {
            var nature = Required<자연경관CompositionCatalog>(자연경관CompositionSetBuilder.CatalogPath);
            var packs = Required<팩경관CompositionCatalog>(팩경관CompositionSetBuilder.CatalogPath);
            var roads = Required<도로GateCompositionCatalog>(도로GateCompositionSetBuilder.CatalogPath);
            nature.Validate();
            packs.Validate();
            roads.Validate();

            var entries = new List<공간문법CompositionCatalogEntry>();
            AddNature(entries, nature);
            AddPack(entries, packs, FarmSets, 월드CompositionPackCodes.Farm);
            AddPack(entries, packs, TownSets, 월드CompositionPackCodes.Town);
            AddPack(entries, packs, CitySets, 월드CompositionPackCodes.City);
            AddNetworks(entries, roads);
            AddTransitions(entries, nature, packs, roads);

            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<공간문법CompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(CatalogRevision, entries
                .OrderBy(value => value.CompositionKey, StringComparer.Ordinal).ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"LandscapeGrammarCatalogBuilt:{entries.Count}:{CatalogPath}");
            return catalog;
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void AddNature(
            ICollection<공간문법CompositionCatalogEntry> target,
            자연경관CompositionCatalog catalog)
        {
            foreach (var source in catalog.Entries)
            {
                var topology = NatureTopology(source.SetName);
                var descriptor = Descriptor(
                    월드CompositionPackCodes.Nature,
                    source.SetName,
                    source.VariantCode,
                    source.Footprint,
                    Array.Empty<월드CompositionConnectorContract>(),
                    Array.Empty<월드CompositionSocketContract>());
                target.Add(Entry(
                    descriptor,
                    source.CompositionKey,
                    source.Prefab,
                    topology,
                    Edges(NatureEdge(source.SetName)),
                    source.AllowedLandCoverCodes.ToArray(),
                    new[] { 법정동WorldRoleCodes.Region, 법정동WorldRoleCodes.Farm,
                        법정동WorldRoleCodes.Town },
                    new Vector2(source.MinimumSlopeDegrees, source.MaximumSlopeDegrees),
                    source.RequiresWaterMask,
                    source.HlodEligible,
                    Array.Empty<string>()));
            }
        }

        private static void AddPack(
            ICollection<공간문법CompositionCatalogEntry> target,
            팩경관CompositionCatalog catalog,
            IEnumerable<SetSource> sets,
            string canonicalPack)
        {
            foreach (var set in sets)
            foreach (var variant in 월드CompositionVariantCodes.All)
            {
                var source = catalog.Entries.Single(value =>
                    value.Descriptor.PackCode == canonicalPack
                    && value.Descriptor.SetName == set.SourceName
                    && value.Descriptor.VariantCode == variant);
                var topology = PackTopology(canonicalPack, set.CanonicalName);
                var descriptor = CopyDescriptor(
                    source.Descriptor, canonicalPack, set.CanonicalName, variant);
                target.Add(Entry(
                    descriptor,
                    source.CompositionKey,
                    source.Prefab,
                    topology,
                    Edges(PackEdge(canonicalPack, set.CanonicalName)),
                    source.AllowedLandCoverCodes.ToArray(),
                    source.AllowedRegionRoleCodes.ToArray(),
                    source.SlopeRange,
                    source.RequiresWaterMask,
                    source.ClusterAllowed,
                    set.CanonicalName == set.SourceName
                        ? Array.Empty<string>()
                        : new[] { source.CompositionKey }));
            }
        }

        private static void AddNetworks(
            ICollection<공간문법CompositionCatalogEntry> target,
            도로GateCompositionCatalog catalog)
        {
            var mappings = new[]
            {
                Network(공간문법NetworkSetNames.농촌도로직선, 도로GateCompositionSetNames.농촌도로직선,
                    월드CompositionConnectorKindCodes.FarmRoad, 법정동WorldRoleCodes.Farm),
                Network(공간문법NetworkSetNames.농촌도로곡선, 도로GateCompositionSetNames.농촌도로모서리,
                    월드CompositionConnectorKindCodes.FarmRoad, 법정동WorldRoleCodes.Farm),
                Network(공간문법NetworkSetNames.농촌도로T자, 도로GateCompositionSetNames.농촌도로T자,
                    월드CompositionConnectorKindCodes.FarmRoad, 법정동WorldRoleCodes.Farm),
                Network(공간문법NetworkSetNames.농촌도로십자, 도로GateCompositionSetNames.농촌도로십자,
                    월드CompositionConnectorKindCodes.FarmRoad, 법정동WorldRoleCodes.Farm),
                Network(공간문법NetworkSetNames.타운도로직선, 도로GateCompositionSetNames.타운도로직선,
                    월드CompositionConnectorKindCodes.TownRoad, 법정동WorldRoleCodes.Town),
                Network(공간문법NetworkSetNames.타운도로곡선, 도로GateCompositionSetNames.타운도로모서리,
                    월드CompositionConnectorKindCodes.TownRoad, 법정동WorldRoleCodes.Town),
                Network(공간문법NetworkSetNames.타운도로T자, 도로GateCompositionSetNames.타운도로T자,
                    월드CompositionConnectorKindCodes.TownRoad, 법정동WorldRoleCodes.Town),
                Network(공간문법NetworkSetNames.타운도로십자, 도로GateCompositionSetNames.타운도로십자,
                    월드CompositionConnectorKindCodes.TownRoad, 법정동WorldRoleCodes.Town),
                Network(공간문법NetworkSetNames.도시도로직선, 도로GateCompositionSetNames.도시도로직선,
                    월드CompositionConnectorKindCodes.CityRoad, 법정동WorldRoleCodes.Hub),
                Network(공간문법NetworkSetNames.도시도로곡선, 도로GateCompositionSetNames.도시도로모서리,
                    월드CompositionConnectorKindCodes.CityRoad, 법정동WorldRoleCodes.Hub),
                Network(공간문법NetworkSetNames.도시도로T자, 도로GateCompositionSetNames.도시도로T자,
                    월드CompositionConnectorKindCodes.CityRoad, 법정동WorldRoleCodes.Hub),
                Network(공간문법NetworkSetNames.도시도로십자, 도로GateCompositionSetNames.도시도로십자,
                    월드CompositionConnectorKindCodes.CityRoad, 법정동WorldRoleCodes.Hub),
            };

            foreach (var mapping in mappings)
            {
                var source = catalog.Resolve(mapping.SourceSetName);
                foreach (var variant in 월드CompositionVariantCodes.All)
                {
                    var topology = mapping.CanonicalSetName.EndsWith("T자", StringComparison.Ordinal)
                        || mapping.CanonicalSetName.EndsWith("십자", StringComparison.Ordinal)
                        ? 공간문법CompositionTopologyCodes.Junction
                        : 공간문법CompositionTopologyCodes.Linear;
                    var descriptor = CopyDescriptor(source.Descriptor,
                        월드CompositionPackCodes.Network, mapping.CanonicalSetName, variant);
                    target.Add(Entry(
                        descriptor,
                        source.CompositionKey,
                        source.Prefab,
                        topology,
                        Edges(공간문법EdgeProfileCodes.RoadFront),
                        new[] { 법정동LandCoverCodes.Corridor,
                            법정동LandCoverCodes.Residential,
                            법정동LandCoverCodes.Logistics,
                            법정동LandCoverCodes.Cropland },
                        new[] { mapping.RegionRoleCode },
                        new Vector2(0f, 12f),
                        false,
                        false,
                        variant == 월드CompositionVariantCodes.A
                            ? new[] { source.CompositionKey }
                            : Array.Empty<string>()));
                }
            }
        }

        private static void AddTransitions(
            ICollection<공간문법CompositionCatalogEntry> target,
            자연경관CompositionCatalog nature,
            팩경관CompositionCatalog packs,
            도로GateCompositionCatalog roads)
        {
            var transitions = new[]
            {
                Transition(공간문법TransitionSetNames.NatureFarm,
                    공간문법EdgeProfileCodes.Forest, 공간문법EdgeProfileCodes.Field,
                    new[] { 법정동LandCoverCodes.Forest, 법정동LandCoverCodes.Cropland },
                    new[] { 법정동WorldRoleCodes.Farm }),
                Transition(공간문법TransitionSetNames.FarmTown,
                    공간문법EdgeProfileCodes.Field, 공간문법EdgeProfileCodes.Residential,
                    new[] { 법정동LandCoverCodes.Cropland, 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Farm, 법정동WorldRoleCodes.Town }),
                Transition(공간문법TransitionSetNames.TownCity,
                    공간문법EdgeProfileCodes.Residential, 공간문법EdgeProfileCodes.Logistics,
                    new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub }),
                Transition(공간문법TransitionSetNames.FarmHub,
                    공간문법EdgeProfileCodes.Field, 공간문법EdgeProfileCodes.Logistics,
                    new[] { 법정동LandCoverCodes.Cropland, 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Farm, 법정동WorldRoleCodes.Hub }),
                Transition(공간문법TransitionSetNames.TownHub,
                    공간문법EdgeProfileCodes.Residential, 공간문법EdgeProfileCodes.Logistics,
                    new[] { 법정동LandCoverCodes.Residential, 법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub }),
                Transition(공간문법TransitionSetNames.HubCity,
                    공간문법EdgeProfileCodes.Logistics, 공간문법EdgeProfileCodes.Residential,
                    new[] { 법정동LandCoverCodes.Logistics, 법정동LandCoverCodes.Residential },
                    new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }),
                Transition(공간문법TransitionSetNames.WaterLand,
                    공간문법EdgeProfileCodes.Water, 공간문법EdgeProfileCodes.Open,
                    new[] { 법정동LandCoverCodes.Water, 법정동LandCoverCodes.Forest,
                        법정동LandCoverCodes.Cropland },
                    new[] { 법정동WorldRoleCodes.Region, 법정동WorldRoleCodes.Farm }),
                Transition(공간문법TransitionSetNames.RoadBuildingFront,
                    공간문법EdgeProfileCodes.RoadFront, 공간문법EdgeProfileCodes.Residential,
                    new[] { 법정동LandCoverCodes.Corridor, 법정동LandCoverCodes.Residential,
                        법정동LandCoverCodes.Logistics },
                    new[] { 법정동WorldRoleCodes.Town, 법정동WorldRoleCodes.Hub }),
            };

            foreach (var transition in transitions)
            foreach (var variant in 월드CompositionVariantCodes.All)
            {
                var source = TransitionSource(transition.SetName, variant, nature, packs, roads);
                var descriptor = Descriptor(
                    월드CompositionPackCodes.Transition,
                    transition.SetName,
                    variant,
                    new Vector2(20f, 10f),
                    TransitionConnectors(transition.SetName),
                    Array.Empty<월드CompositionSocketContract>());
                target.Add(Entry(
                    descriptor,
                    source.Key,
                    source.Prefab,
                    공간문법CompositionTopologyCodes.Transition,
                    TransitionEdges(transition.FromEdge, transition.ToEdge),
                    transition.LandCovers,
                    transition.Roles,
                    new Vector2(0f, 20f),
                    transition.SetName == 공간문법TransitionSetNames.WaterLand,
                    false,
                    source.LegacyKey == null ? Array.Empty<string>() : new[] { source.LegacyKey }));
            }
        }

        private static 공간문법CompositionCatalogEntry Entry(
            월드CompositionDescriptor descriptor,
            string sourceKey,
            GameObject prefab,
            string topology,
            공간문법EdgeProfileContract[] edges,
            string[] landCovers,
            string[] roles,
            Vector2 slopeRange,
            bool requiresWater,
            bool hlod,
            string[] legacyKeys)
        {
            var repeat = new 공간문법RepeatRuleContract();
            var maxConsecutive = topology == 공간문법CompositionTopologyCodes.Linear ? 3
                : topology == 공간문법CompositionTopologyCodes.Junction
                    || topology == 공간문법CompositionTopologyCodes.Landmark ? 1 : 2;
            repeat.Configure(
                topology != 공간문법CompositionTopologyCodes.Landmark,
                maxConsecutive,
                4,
                topology == 공간문법CompositionTopologyCodes.Junction ? 2f : 1f,
                공간문법RotationCodes.All.ToArray(),
                topology == 공간문법CompositionTopologyCodes.Area
                    || topology == 공간문법CompositionTopologyCodes.Detail
                    || topology == 공간문법CompositionTopologyCodes.Transition);

            var adjacency = new 공간문법AdjacencyRuleContract();
            adjacency.Configure(
                Preferred(topology),
                공간문법CompositionTopologyCodes.All.ToArray(),
                Array.Empty<string>());
            var expansion = new 공간문법ExpansionRuleContract();
            expansion.Configure(
                topology == 공간문법CompositionTopologyCodes.Area,
                topology == 공간문법CompositionTopologyCodes.Linear,
                topology != 공간문법CompositionTopologyCodes.Area,
                Array.Empty<string>());
            var generation = new 공간문법InternalGenerationContract();
            generation.Configure("world-coordinate-sha256.v1", "wrapper-micro-detail.v1");

            var entry = new 공간문법CompositionCatalogEntry();
            entry.Configure(
                descriptor,
                sourceKey,
                prefab,
                topology,
                공간문법AssemblyScaleCodes.Meso,
                edges,
                repeat,
                adjacency,
                expansion,
                generation,
                landCovers,
                roles,
                slopeRange,
                1f,
                requiresWater,
                hlod,
                legacyKeys);
            return entry;
        }

        private static string[] Preferred(string topology)
        {
            if (topology == 공간문법CompositionTopologyCodes.Linear)
                return new[] { 공간문법CompositionTopologyCodes.Linear,
                    공간문법CompositionTopologyCodes.Junction };
            if (topology == 공간문법CompositionTopologyCodes.Junction)
                return new[] { 공간문법CompositionTopologyCodes.Linear };
            if (topology == 공간문법CompositionTopologyCodes.Transition)
                return new[] { 공간문법CompositionTopologyCodes.Area,
                    공간문법CompositionTopologyCodes.Linear };
            if (topology == 공간문법CompositionTopologyCodes.Landmark)
                return new[] { 공간문법CompositionTopologyCodes.Area,
                    공간문법CompositionTopologyCodes.Transition };
            return new[] { 공간문법CompositionTopologyCodes.Area,
                공간문법CompositionTopologyCodes.Detail,
                공간문법CompositionTopologyCodes.Transition };
        }

        private static 월드CompositionDescriptor Descriptor(
            string pack,
            string setName,
            string variant,
            Vector2 footprint,
            월드CompositionConnectorContract[] connectors,
            월드CompositionSocketContract[] sockets)
        {
            var value = new 월드CompositionDescriptor();
            value.Configure(
                월드CompositionDescriptor.BuildKey(pack, setName, variant),
                setName,
                variant,
                pack,
                pack == 월드CompositionPackCodes.Nature
                    ? 월드CompositionSourceKinds.SyntyNestedPrefab
                    : 월드CompositionSourceKinds.Mixed,
                footprint,
                Vector2.one,
                true,
                false,
                false,
                월드CompositionJourneyKindCodes.Ambient,
                new[] { 월드CompositionDetailTierCodes.World,
                    월드CompositionDetailTierCodes.Zone,
                    월드CompositionDetailTierCodes.Object },
                connectors,
                sockets);
            return value;
        }

        private static 월드CompositionDescriptor CopyDescriptor(
            월드CompositionDescriptor source,
            string pack,
            string setName,
            string variant)
        {
            var value = new 월드CompositionDescriptor();
            value.Configure(
                월드CompositionDescriptor.BuildKey(pack, setName, variant),
                setName,
                variant,
                pack,
                source.SourceKind,
                source.Footprint,
                source.CellSize,
                source.HasEnvironmentRoot,
                source.HasOcclusionRoot,
                source.HasInteriorRoot,
                source.JourneyKindCode,
                source.DetailTierCodes.ToArray(),
                source.Connectors.ToArray(),
                source.Sockets.ToArray());
            return value;
        }

        private static 공간문법EdgeProfileContract[] Edges(string profile)
            => 월드CompositionConnectorDirectionCodes.All
                .Select(direction => Edge(direction, profile)).ToArray();

        private static 공간문법EdgeProfileContract[] TransitionEdges(string from, string to)
            => new[]
            {
                Edge(월드CompositionConnectorDirectionCodes.North, 공간문법EdgeProfileCodes.Open, false),
                Edge(월드CompositionConnectorDirectionCodes.East, to),
                Edge(월드CompositionConnectorDirectionCodes.South, 공간문법EdgeProfileCodes.Open, false),
                Edge(월드CompositionConnectorDirectionCodes.West, from),
            };

        private static 공간문법EdgeProfileContract Edge(
            string direction,
            string profile,
            bool required = true)
        {
            var value = new 공간문법EdgeProfileContract();
            value.Configure(direction, profile, required);
            return value;
        }

        private static 월드CompositionConnectorContract[] TransitionConnectors(string setName)
            => new[]
            {
                Connector("from-west", 월드CompositionConnectorDirectionCodes.West,
                    setName + ".from", new Vector3(-10f, 0f, 0f), 270f),
                Connector("to-east", 월드CompositionConnectorDirectionCodes.East,
                    setName + ".to", new Vector3(10f, 0f, 0f), 90f),
            };

        private static 월드CompositionConnectorContract Connector(
            string code,
            string direction,
            string signature,
            Vector3 position,
            float yaw)
        {
            var value = new 월드CompositionConnectorContract();
            value.Configure(code, direction, 월드CompositionConnectorKindCodes.Pedestrian,
                signature, position, yaw, 2f, true);
            return value;
        }

        private static string NatureTopology(string setName)
        {
            if (setName == 자연경관SetNames.활엽수림군집
                || setName == 자연경관SetNames.침엽수림군집
                || setName == 자연경관SetNames.혼효림군집
                || setName == 자연경관SetNames.초지야생화)
                return 공간문법CompositionTopologyCodes.Area;
            if (setName == 자연경관SetNames.산능선
                || setName == 자연경관SetNames.개울회랑
                || setName == 자연경관SetNames.고지대노출지)
                return 공간문법CompositionTopologyCodes.Landmark;
            return 공간문법CompositionTopologyCodes.Detail;
        }

        private static string NatureEdge(string setName)
        {
            if (setName == 자연경관SetNames.수변완충지
                || setName == 자연경관SetNames.개울회랑)
                return 공간문법EdgeProfileCodes.Water;
            if (setName == 자연경관SetNames.숲가장자리)
                return 공간문법EdgeProfileCodes.ForestEdge;
            return 공간문법EdgeProfileCodes.Forest;
        }

        private static string PackTopology(string pack, string setName)
        {
            if (pack == 월드CompositionPackCodes.Farm
                && setName != 농장풍경SetNames.헛간작업마당
                && setName != "농산물 집하·직판장")
                return 공간문법CompositionTopologyCodes.Area;
            if (pack == 월드CompositionPackCodes.Town
                && setName == 타운경관SetNames.저층주택블록)
                return 공간문법CompositionTopologyCodes.Area;
            if (pack == 월드CompositionPackCodes.City
                && setName == 도시물류경관SetNames.공동주택생활마당)
                return 공간문법CompositionTopologyCodes.Area;
            return 공간문법CompositionTopologyCodes.Landmark;
        }

        private static string PackEdge(string pack, string setName)
        {
            if (pack == 월드CompositionPackCodes.Farm)
                return 공간문법EdgeProfileCodes.Field;
            if (pack == 월드CompositionPackCodes.City
                && setName != 도시물류경관SetNames.공동주택생활마당
                && setName != 도시물류경관SetNames.먹거리상점골목)
                return 공간문법EdgeProfileCodes.Logistics;
            return 공간문법EdgeProfileCodes.Residential;
        }

        private static NetworkMapping Network(
            string canonical,
            string source,
            string connectorKind,
            string role)
            => new(canonical, source, connectorKind, role);

        private static TransitionMapping Transition(
            string name,
            string from,
            string to,
            string[] landCovers,
            string[] roles)
            => new(name, from, to, landCovers, roles);

        private static TransitionSourceResult TransitionSource(
            string setName,
            string variant,
            자연경관CompositionCatalog nature,
            팩경관CompositionCatalog packs,
            도로GateCompositionCatalog roads)
        {
            if (setName == 공간문법TransitionSetNames.NatureFarm)
            {
                var source = packs.Entries.Single(value =>
                    value.Descriptor.PackCode == 월드CompositionPackCodes.Mixed
                    && value.Descriptor.SetName == 혼합전환경관SetNames.NatureFarm
                    && value.Descriptor.VariantCode == variant);
                return new TransitionSourceResult(source.CompositionKey, source.Prefab, null);
            }
            if (setName == 공간문법TransitionSetNames.WaterLand)
            {
                var source = nature.Resolve(자연경관SetNames.수변완충지, variant);
                return new TransitionSourceResult(source.CompositionKey, source.Prefab, null);
            }
            if (setName == 공간문법TransitionSetNames.RoadBuildingFront)
            {
                var source = packs.Entries.Single(value =>
                    value.Descriptor.PackCode == 월드CompositionPackCodes.Town
                    && value.Descriptor.SetName == 타운경관SetNames.읍내상점전면
                    && value.Descriptor.VariantCode == variant);
                return new TransitionSourceResult(source.CompositionKey, source.Prefab, null);
            }

            var pair = GatePair(setName);
            var sourceName = variant == 월드CompositionVariantCodes.B ? pair.ToSetName : pair.FromSetName;
            var road = roads.Resolve(sourceName);
            return new TransitionSourceResult(road.CompositionKey, road.Prefab, road.CompositionKey);
        }

        private static GatePairMapping GatePair(string setName)
        {
            if (setName == 공간문법TransitionSetNames.FarmTown)
                return new GatePairMapping(도로GateCompositionSetNames.농장타운농장출구,
                    도로GateCompositionSetNames.농장타운타운입구);
            if (setName == 공간문법TransitionSetNames.TownCity)
                return new GatePairMapping(도로GateCompositionSetNames.타운시티타운출구,
                    도로GateCompositionSetNames.타운시티시티입구);
            if (setName == 공간문법TransitionSetNames.FarmHub)
                return new GatePairMapping(도로GateCompositionSetNames.농장허브농장출구,
                    도로GateCompositionSetNames.농장허브허브입구);
            if (setName == 공간문법TransitionSetNames.TownHub)
                return new GatePairMapping(도로GateCompositionSetNames.타운허브타운출구,
                    도로GateCompositionSetNames.타운허브허브입구);
            if (setName == 공간문법TransitionSetNames.HubCity)
                return new GatePairMapping(도로GateCompositionSetNames.허브시티허브출구,
                    도로GateCompositionSetNames.허브시티시티입구);
            throw new InvalidOperationException("LandscapeGrammarTransitionSourceMissing:" + setName);
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path)
                ?? throw new InvalidOperationException("LandscapeGrammarSourceMissing:" + path);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private sealed class SetSource
        {
            public SetSource(string canonicalName, string sourceName)
            {
                CanonicalName = canonicalName;
                SourceName = sourceName;
            }

            public string CanonicalName { get; }
            public string SourceName { get; }
        }

        private sealed class NetworkMapping
        {
            public NetworkMapping(string canonical, string source, string connector, string role)
            {
                CanonicalSetName = canonical;
                SourceSetName = source;
                ConnectorKindCode = connector;
                RegionRoleCode = role;
            }

            public string CanonicalSetName { get; }
            public string SourceSetName { get; }
            public string ConnectorKindCode { get; }
            public string RegionRoleCode { get; }
        }

        private sealed class TransitionMapping
        {
            public TransitionMapping(string setName, string from, string to,
                string[] landCovers, string[] roles)
            {
                SetName = setName;
                FromEdge = from;
                ToEdge = to;
                LandCovers = landCovers;
                Roles = roles;
            }

            public string SetName { get; }
            public string FromEdge { get; }
            public string ToEdge { get; }
            public string[] LandCovers { get; }
            public string[] Roles { get; }
        }

        private sealed class GatePairMapping
        {
            public GatePairMapping(string from, string to)
            {
                FromSetName = from;
                ToSetName = to;
            }

            public string FromSetName { get; }
            public string ToSetName { get; }
        }

        private sealed class TransitionSourceResult
        {
            public TransitionSourceResult(string key, GameObject prefab, string? legacyKey)
            {
                Key = key;
                Prefab = prefab;
                LegacyKey = legacyKey;
            }

            public string Key { get; }
            public GameObject Prefab { get; }
            public string? LegacyKey { get; }
        }
    }
}
