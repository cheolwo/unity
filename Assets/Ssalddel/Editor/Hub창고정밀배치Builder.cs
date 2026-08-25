using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Interior.Contracts;
using Ssalddel.Interior.Domain;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical SimulationWorldShell의 Hub 안에 25x20m 창고와 다섯 업무 구역을
    /// 결정적 InteriorPlacementPlan으로 조립합니다. 원본 Synty Prefab Transform은
    /// 유지하고 미세 조정·Collider는 Wrapper에서만 수행합니다.
    /// </summary>
    public static class Hub창고정밀배치Builder
    {
        public const string RootName = "HubWarehousePrecisionPlacement";
        public static readonly Vector3 ExteriorBuildingSize =
            new(25f, 4.2f, 20f);

        private const string FloorPrefabPath =
            "Assets/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_Concrete_Floor_01.prefab";
        private const string WallPrefabPath =
            "Assets/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_ConcreteFrame_Wall_01.prefab";
        private const string GarageDoorPrefabPath =
            "Assets/Synty/PolygonTown/Prefabs/Buildings/SM_Bld_House_ExteriorWall_GarageDoor_01.prefab";

        private static readonly IReadOnlyDictionary<string, string>
            VisualPrefabPaths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [HubWarehouseInteriorGrammar.PalletVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Pallet_01.prefab",
                [HubWarehouseInteriorGrammar.BoxStackVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_PalletStack_01.prefab",
                [HubWarehouseInteriorGrammar.StorageShelfVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_StorageShelf_01.prefab",
                [HubWarehouseInteriorGrammar.WorkbenchVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Table_02.prefab",
                [HubWarehouseInteriorGrammar.RollerTrackVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Roller_Track_01.prefab",
                [HubWarehouseInteriorGrammar.HandTrolleyVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_HandTrolley_01.prefab",
                [HubWarehouseInteriorGrammar.SafetyBarrierVisualKey] =
                    "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Barrier_Long_01.prefab",
            };

        private static readonly (string StableId, string PrefabPath,
            string[] WiIds, int[] RouteIndexes)[] NpcDefinitions =
        {
            ("npc:hub:inspector-01",
                "Assets/Synty/PolygonConstruction/Prefabs/Characters/SM_Chr_Inspector_Male_01.prefab",
                new[] { "WI-001", "WI-002" }, new[] { 0, 1, 2 }),
            ("npc:hub:picker-01",
                "Assets/Synty/PolygonConstruction/Prefabs/Characters/SM_Chr_Builder_HighVis_01.prefab",
                new[] { "WI-HUB-03" }, new[] { 2, 3 }),
            ("npc:hub:packer-01",
                "Assets/Synty/PolygonConstruction/Prefabs/Characters/SM_Chr_Builder_Overalls_01.prefab",
                new[] { "WI-HUB-04", "WI-HUB-05" }, new[] { 3, 4 }),
        };

        [MenuItem("Ssalddel/통합 월드/Hub/Build Precision Warehouse")]
        public static void BuildCanonicalScene()
        {
            var scene = EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath);
            var shell = GameObject.Find(SimulationWorldShellBuilder.RootName)
                        ?? throw new InvalidOperationException(
                            "SimulationWorldShellMissing");
            var logistics = shell.transform.Find(
                "SettlementInteriorRoot/Districts/LogisticsDistrict")
                ?? throw new InvalidOperationException(
                    "SimulationWorldShellLogisticsDistrictMissing");
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "SimulationWorldShellPlayerReferenceMissing");

            var previous = logistics.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var placement = BuildUnder(logistics, player);
            EditorUtility.SetDirty(placement.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene,
                    SimulationWorldShellBuilder.ScenePath))
                throw new InvalidOperationException(
                    "HubWarehouseSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = placement.gameObject;
            SceneView.RepaintAll();
            Debug.Log("HUB-WAREHOUSE-PLACEMENT built: "
                      + placement.InteriorPlanHashSha256);
        }

        public static Hub창고정밀배치Root BuildUnder(Transform parent,
            플레이어경관Controller player)
        {
            if (parent == null || player == null || player.VisualRoot == null)
                throw new InvalidOperationException(
                    "HubWarehousePlacementInputInvalid");

            var request = HubWarehouseInteriorGrammar.CreateRequest();
            var plan = new DeterministicInteriorLayoutEngine()
                .Generate(request);
            var module = new GameObject(RootName);
            module.transform.SetParent(parent, false);
            module.transform.localPosition = Vector3.zero;

            var hierarchyRoot = Child(module.transform,
                "PlacementControlHierarchy");
            hierarchyRoot.gameObject.AddComponent<배치통제계층Root>()
                .ConfigureCompositionV3(module.transform, player,
                    MeasurePlayerHeight(player));

            BuildStructure(module.transform, out var roofRoot);
            BuildZoneAnchors(module.transform, plan);
            var routeAnchors = BuildRouteAnchors(module.transform, plan);
            var receipts = BuildFixtures(module.transform, plan);
            var npcRoutes = BuildNpcRoutes(module.transform, routeAnchors);

            var component = module.AddComponent<Hub창고정밀배치Root>();
            component.Configure(plan, request.Definition.Structure,
                ExteriorBuildingSize, receipts, routeAnchors, npcRoutes,
                roofRoot);
            return component;
        }

        public static string ResolveVisualPrefabPath(string visualKey)
            => VisualPrefabPaths.TryGetValue(visualKey, out var path)
                ? path
                : throw new InvalidOperationException(
                    "HubWarehouseVisualKeyUnmapped:" + visualKey);

        private static void BuildStructure(Transform parent,
            out Transform roofRoot)
        {
            var structure = Child(parent, "BuildingShell_25x20m");
            var floors = Child(structure, "FloorModules");
            var walls = Child(structure, "WallModules");
            roofRoot = Child(structure, "CutawayRoofModules");

            for (var x = -10f; x <= 10f; x += 5f)
            for (var z = -7.5f; z <= 7.5f; z += 5f)
            {
                PlaceScaledSource(floors, FloorPrefabPath,
                    $"Floor_{x:0.0}_{z:0.0}", new Vector3(x, 0f, z),
                    Quaternion.identity, new Vector3(5f, .2f, 5f), true);
                PlaceScaledSource(roofRoot, FloorPrefabPath,
                    $"Roof_{x:0.0}_{z:0.0}", new Vector3(x, 4.2f, z),
                    Quaternion.identity, new Vector3(5f, .2f, 5f), false);
            }

            for (var x = -10f; x <= 10f; x += 5f)
                PlaceScaledSource(walls, WallPrefabPath,
                    $"BackWall_{x:0.0}", new Vector3(x, 2.1f, 10f),
                    Quaternion.identity, new Vector3(5f, 4.2f, .3f), true);
            for (var z = -7.5f; z <= 7.5f; z += 5f)
            {
                PlaceScaledSource(walls, WallPrefabPath,
                    $"LeftWall_{z:0.0}", new Vector3(-12.5f, 2.1f, z),
                    Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(.3f, 4.2f, 5f), true);
                PlaceScaledSource(walls, WallPrefabPath,
                    $"RightWall_{z:0.0}", new Vector3(12.5f, 2.1f, z),
                    Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(.3f, 4.2f, 5f), true);
            }

            foreach (var x in new[] { -12f, -4f, 0f, 4f, 12f })
                PlaceScaledSource(walls, WallPrefabPath,
                    $"FrontWall_{x:0.0}", new Vector3(x, 2.1f, -10f),
                    Quaternion.identity, new Vector3(3.2f, 4.2f, .3f), true);
            foreach (var x in new[] { -8f, 8f })
                PlaceScaledSource(walls, GarageDoorPrefabPath,
                    $"DockDoor_{x:0.0}", new Vector3(x, 2.1f, -10f),
                    Quaternion.identity, new Vector3(4.2f, 4.2f, .3f), true);

            roofRoot.gameObject.SetActive(false);
        }

        private static void BuildZoneAnchors(Transform parent,
            InteriorPlacementPlan plan)
        {
            var root = Child(parent, "H1WorkZones");
            foreach (var zone in plan.Zones.OrderBy(value =>
                         value.ZoneStableId, StringComparer.Ordinal))
            {
                var zoneRoot = Child(root, SafeName(zone.ZoneStableId));
                zoneRoot.localPosition = ToVector3(zone.Bounds.Center);
                var trigger = zoneRoot.gameObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = Vector3.up * .05f;
                trigger.size = new Vector3((float)zone.Bounds.Size.X, .1f,
                    (float)zone.Bounds.Size.Z);
            }
        }

        private static Transform[] BuildRouteAnchors(Transform parent,
            InteriorPlacementPlan plan)
        {
            var root = Child(parent, "NpcWorkRouteAnchors");
            var zoneOrder = new[]
            {
                HubWarehouseInteriorGrammar.InboundZone,
                HubWarehouseInteriorGrammar.InspectionZone,
                HubWarehouseInteriorGrammar.StorageZone,
                HubWarehouseInteriorGrammar.PickingZone,
                HubWarehouseInteriorGrammar.OutboundZone,
            };
            return zoneOrder.Select((zoneId, index) =>
            {
                var zone = plan.Zones.Single(value =>
                    value.ZoneStableId == zoneId);
                var anchor = Child(root, $"Route_{index + 1}_{SafeName(zoneId)}");
                anchor.localPosition = ToVector3(zone.Bounds.Center)
                                       + Vector3.up * .05f;
                return anchor;
            }).ToArray();
        }

        private static Hub창고배치객체Record[] BuildFixtures(
            Transform parent, InteriorPlacementPlan plan)
        {
            var root = Child(parent, "FixtureWrappers");
            var records = new List<Hub창고배치객체Record>();
            foreach (var placement in plan.Placements.OrderBy(value =>
                         value.PlacementStableId, StringComparer.Ordinal))
            {
                var path = ResolveVisualPrefabPath(placement.VisualKey);
                var wrapper = Child(root, SafeName(placement.PlacementStableId));
                wrapper.localPosition = ToVector3(
                    placement.AppliedTransform.LocalPosition);
                wrapper.localRotation = Quaternion.Euler(0f,
                    (float)placement.AppliedTransform.LocalRotationDegrees, 0f);
                wrapper.localScale = Vector3.one
                    * (float)placement.AppliedTransform.UniformScale;
                var source = InstantiateSource(path, wrapper);
                var collider = wrapper.gameObject.AddComponent<BoxCollider>();
                var scale = Mathf.Max(.0001f,
                    (float)placement.AppliedTransform.UniformScale);
                collider.size = new Vector3((float)placement.Size.X / scale,
                    (float)placement.Size.Y / scale,
                    (float)placement.Size.Z / scale);
                collider.center = Vector3.up * (collider.size.y * .5f);
                var receipt = wrapper.gameObject
                    .AddComponent<Hub창고배치객체Record>();
                receipt.Configure(placement, path,
                    plan.InteriorPlacementPlanHashSha256, source.transform);
                records.Add(receipt);
            }
            return records.ToArray();
        }

        private static Hub창고Npc업무경로View[] BuildNpcRoutes(
            Transform parent, Transform[] routeAnchors)
        {
            var root = Child(parent, "NpcPresentation");
            return NpcDefinitions.Select(definition =>
            {
                var wrapper = Child(root, SafeName(definition.StableId));
                InstantiateSource(definition.PrefabPath, wrapper);
                var route = definition.RouteIndexes.Select(index =>
                    routeAnchors[index]).ToArray();
                var view = wrapper.gameObject
                    .AddComponent<Hub창고Npc업무경로View>();
                view.Configure(definition.StableId, definition.WiIds, route);
                return view;
            }).ToArray();
        }

        private static Transform PlaceScaledSource(Transform parent,
            string sourcePath, string name, Vector3 position,
            Quaternion rotation, Vector3 targetSize, bool addCollider)
        {
            var wrapper = Child(parent, name);
            wrapper.localPosition = position;
            wrapper.localRotation = rotation;
            var source = InstantiateSource(sourcePath, wrapper);
            var bounds = RendererBounds(source);
            wrapper.localScale = new Vector3(
                targetSize.x / Mathf.Max(.01f, bounds.size.x),
                targetSize.y / Mathf.Max(.01f, bounds.size.y),
                targetSize.z / Mathf.Max(.01f, bounds.size.z));
            if (addCollider)
            {
                var collider = wrapper.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(
                    targetSize.x / wrapper.localScale.x,
                    targetSize.y / wrapper.localScale.y,
                    targetSize.z / wrapper.localScale.z);
            }
            return wrapper;
        }

        private static GameObject InstantiateSource(string assetPath,
            Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)
                         ?? throw new InvalidOperationException(
                             "HubWarehouseSyntyPrefabMissing:" + assetPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent)
                           as GameObject
                           ?? throw new InvalidOperationException(
                               "HubWarehouseSyntyPrefabInstantiateFailed:"
                               + assetPath);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "HubWarehouseSyntyRendererMissing:" + root.name);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static float MeasurePlayerHeight(
            플레이어경관Controller player)
        {
            var renderers = player.VisualRoot.GetComponentsInChildren<Renderer>(
                true);
            if (renderers.Length == 0) return 1.8f;
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return Mathf.Clamp(bounds.size.y, .8f, 3f);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Vector3 ToVector3(InteriorVector3 value) =>
            new((float)value.X, (float)value.Y, (float)value.Z);

        private static string SafeName(string value) => value
            .Replace(':', '_').Replace('/', '_');
    }
}
