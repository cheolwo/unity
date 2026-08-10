using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static class 거점CompositionSetBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Catalogs/거점CompositionCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/Anchors";
        public const string PreviewScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/AnchorCompositionLibraryPreview.unity";

        private const string PreviewGroundMaterialPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/AnchorPreviewGround.mat";
        private const string PreviewConnectorMaterialPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/AnchorPreviewConnector.mat";
        private const string PreviewSocketMaterialPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/AnchorPreviewSocket.mat";
        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string TownRoot = "Assets/Synty/PolygonTown/Prefabs/";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs/";

        [MenuItem("Ssalddel/World Composition/Build Minimum Anchor A Sets")]
        public static void Build()
        {
            var definitions = CreateDefinitions();
            ValidateDefinitions(definitions);
            EnsureFolder(PrefabRoot);
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));

            var entries = definitions.Select(definition =>
            {
                var prefab = BuildPrefab(definition);
                var entry = new 거점CompositionCatalogEntry();
                entry.Configure(definition.Descriptor, prefab);
                return entry;
            }).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<거점CompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<거점CompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            catalog.Validate();
            BuildPreviewScene(catalog);
            Debug.Log($"MinimumAnchorCompositionSetsBuilt:{entries.Length}:{CatalogPath}");
        }

        [MenuItem("Ssalddel/World Composition/Open Minimum Anchor Preview")]
        public static void OpenLibraryPreview()
            => EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);

        public static IReadOnlyList<월드CompositionDescriptor> CreateDescriptorsForValidation()
            => CreateDefinitions().Select(value => value.Descriptor).ToArray();

        private static Definition[] CreateDefinitions()
            => new[]
            {
                CreateFarmPotatoPlot(),
                CreateTownHouse(),
                CreateCityApartment(),
                CreateRegionalHubDock(),
            };

        private static Definition CreateFarmPotatoPlot()
        {
            var placements = new List<Placement>();
            for (var z = 0; z < 6; z++)
            for (var x = 0; x < 6; x++)
            {
                placements.Add(P(
                    FarmRoot + "Environments/SM_Env_Dirt_Rows_01.prefab",
                    new Vector3((x - 2.5f) * 5f, 0f, (z - 2.5f) * 5f)));
            }

            placements.Add(P(
                FarmRoot + "Props/SM_Prop_Sign_Potatoes_01.prefab",
                new Vector3(0f, 0f, -16f)));
            placements.Add(P(
                FarmRoot + "Props/SM_Prop_Fence_Wood_Gate_01.prefab",
                new Vector3(-4f, 0f, -16f)));
            placements.Add(P(
                FarmRoot + "Props/SM_Prop_Fence_Wood_01.prefab",
                new Vector3(5f, 0f, -16f)));
            var descriptor = Descriptor(
                거점CompositionSetNames.실제감자6x6필지,
                월드CompositionPackCodes.Farm,
                new Vector2(35f, 35f),
                Vector2.one * 5f,
                false,
                new[]
                {
                    C("vehicle-south", "south", "vehicle", "road.farm.vehicle.v1",
                        new Vector3(0f, 0f, -17.5f), 180f, 3.4f),
                    C("pedestrian-south", "south", "pedestrian", "road.farm.pedestrian.v1",
                        new Vector3(-2f, 0f, -17.5f), 180f, 1.2f),
                    C("farm-machine-south", "south", "farm-machine", "road.farm.farm-machine.v1",
                        new Vector3(2f, 0f, -17.5f), 180f, 3.4f),
                },
                new[]
                {
                    S("farm.socket.potato-field", "simulation-target", Vector3.zero),
                    S("farm.actor.worker", "actor", new Vector3(-4f, 0f, -14f)),
                    S("farm.machine.work-head", "implement", new Vector3(4f, 0f, -14f)),
                });
            return new Definition(
                descriptor,
                placements.ToArray(),
                거점CompositionEntranceCodes.None,
                거점CompositionEntranceCodes.South,
                6f);
        }

        private static Definition CreateTownHouse()
        {
            var descriptor = Descriptor(
                거점CompositionSetNames.타운기본주택,
                월드CompositionPackCodes.Town,
                new Vector2(20f, 20f),
                Vector2.one * 5f,
                true,
                new[]
                {
                    C("vehicle-south", "south", "vehicle", "road.town.vehicle.v1",
                        new Vector3(0f, 0f, -10f), 180f, 3.4f),
                    C("pedestrian-south", "south", "pedestrian", "road.town.pedestrian.v1",
                        new Vector3(-2f, 0f, -10f), 180f, 1.2f),
                },
                new[]
                {
                    S("town.actor.resident", "actor", new Vector3(-2f, 0f, -3f)),
                    S("town.actor.visitor", "actor", new Vector3(2f, 0f, -3f)),
                    S("town.vehicle.delivery", "vehicle", new Vector3(5f, 0f, -5f)),
                    S("town.interaction.entrance", "interaction", new Vector3(0f, 0f, -4.5f)),
                });
            return new Definition(
                descriptor,
                new[]
                {
                    P(TownRoot + "Buildings/Presets/SM_Bld_House_Preset_01.prefab",
                        new Vector3(0f, 0f, 2.5f), occlusion: true),
                    P(TownRoot + "Environment/SM_Env_Driveway_01.prefab",
                        new Vector3(4.5f, 0f, -4f)),
                    P(TownRoot + "Environment/SM_Env_Fence_White_Gate_01.prefab",
                        new Vector3(-3f, 0f, -7f)),
                    P(TownRoot + "Environment/SM_Env_Tree_01.prefab",
                        new Vector3(-6f, 0f, 4f)),
                    P(TownRoot + "Props/SM_Prop_LetterBox_01.prefab",
                        new Vector3(3f, 0f, -7f)),
                },
                거점CompositionEntranceCodes.Unknown,
                거점CompositionEntranceCodes.South,
                5f);
        }

        private static Definition CreateCityApartment()
        {
            var descriptor = Descriptor(
                거점CompositionSetNames.시티공동주택가로형,
                월드CompositionPackCodes.City,
                new Vector2(25f, 20f),
                Vector2.one * 5f,
                true,
                new[]
                {
                    C("vehicle-south", "south", "vehicle", "road.city.vehicle.v1",
                        new Vector3(0f, 0f, -10f), 180f, 3.4f),
                    C("pedestrian-south", "south", "pedestrian", "road.city.pedestrian.v1",
                        new Vector3(-2f, 0f, -10f), 180f, 1.2f),
                },
                new[]
                {
                    S("city.actor.resident", "actor", new Vector3(-4f, 0f, -4f)),
                    S("city.actor.representative", "actor", new Vector3(0f, 0f, -4f)),
                    S("city.vehicle.delivery", "vehicle", new Vector3(7f, 0f, -5f)),
                    S("city.cargo.pickup", "cargo", new Vector3(5f, 0f, -4f)),
                    S("city.interaction.entrance", "interaction", new Vector3(-4f, 0f, -2.5f)),
                });
            return new Definition(
                descriptor,
                new[]
                {
                    P(CityRoot + "Buildings/SM_Bld_Apartment_Door_01.prefab",
                        new Vector3(-4f, 0f, 2f), yaw: 90f, occlusion: true),
                    P(CityRoot + "Buildings/SM_Bld_Apartment_Roof_01.prefab",
                        new Vector3(-4f, 3f, 2f), yaw: 90f, occlusion: true),
                    P(CityRoot + "Buildings/SM_Bld_Apartment_Stack_01.prefab",
                        new Vector3(4f, 0f, 2f), yaw: 90f, occlusion: true),
                    P(CityRoot + "Buildings/SM_Bld_Apartment_Roof_01.prefab",
                        new Vector3(4f, 9f, 2f), yaw: 90f, occlusion: true),
                    P(CityRoot + "Environments/SM_Env_Sidewalk_Panel_01.prefab",
                        new Vector3(0f, 0f, -4f)),
                    P(CityRoot + "Props/SM_Prop_ParkBench_01.prefab",
                        new Vector3(0f, 0f, -5f), yaw: 180f),
                },
                거점CompositionEntranceCodes.East,
                거점CompositionEntranceCodes.South,
                6f);
        }

        private static Definition CreateRegionalHubDock()
        {
            var descriptor = Descriptor(
                거점CompositionSetNames.지역물류허브Dock,
                월드CompositionPackCodes.RegionalLogisticsHub,
                new Vector2(35f, 30f),
                Vector2.one * 5f,
                true,
                new[]
                {
                    C("vehicle-south", "south", "vehicle",
                        "road.regional-logistics-hub.vehicle.v1",
                        new Vector3(0f, 0f, -15f), 180f, 3.8f),
                    C("vehicle-north", "north", "vehicle",
                        "road.regional-logistics-hub.vehicle.v1",
                        new Vector3(0f, 0f, 15f), 0f, 3.8f),
                },
                new[]
                {
                    S("hub.vehicle.gate", "vehicle", new Vector3(0f, 0f, -11f)),
                    S("hub.area.inbound-dock", "interaction", new Vector3(-7f, 0f, -3f)),
                    S("hub.area.inspection", "interaction", new Vector3(-2f, 0f, -3f)),
                    S("hub.area.storage", "interaction", new Vector3(4f, 0f, 5f)),
                    S("hub.area.outbound-staging", "interaction", new Vector3(7f, 0f, -3f)),
                    S("hub.cargo.handoff", "cargo", new Vector3(-7f, 0f, -1f)),
                });
            return new Definition(
                descriptor,
                new[]
                {
                    P(CityRoot + "Buildings/SM_Bld_Station_03.prefab",
                        new Vector3(0f, 0f, 6f), occlusion: true),
                    P(CityRoot + "Buildings/SM_Bld_Cover_01.prefab",
                        new Vector3(-7f, 0f, -2f), occlusion: true),
                    P(CityRoot + "Props/SM_Prop_Pallet_01.prefab",
                        new Vector3(-7f, 0f, -1f)),
                    P(CityRoot + "Props/SM_Prop_Pallet_01.prefab",
                        new Vector3(4f, 0f, 3f), yaw: 90f),
                    P(CityRoot + "Vehicles/SM_Veh_Car_Van_01.prefab",
                        new Vector3(7f, 0f, -5f), yaw: 180f),
                    P(CityRoot + "Props/SM_Prop_Barrier_01.prefab",
                        new Vector3(0f, 0f, -11f), yaw: 90f),
                    P(CityRoot + "Props/SM_Prop_Cone_01.prefab",
                        new Vector3(-2f, 0f, -9f)),
                    P(CityRoot + "Props/SM_Prop_Cone_01.prefab",
                        new Vector3(2f, 0f, -9f)),
                },
                거점CompositionEntranceCodes.Unknown,
                거점CompositionEntranceCodes.South,
                8f);
        }

        private static 월드CompositionDescriptor Descriptor(
            string setName,
            string packCode,
            Vector2 footprint,
            Vector2 cellSize,
            bool hasOcclusionRoot,
            월드CompositionConnectorContract[] connectors,
            월드CompositionSocketContract[] sockets)
        {
            var descriptor = new 월드CompositionDescriptor();
            descriptor.Configure(
                월드CompositionDescriptor.BuildKey(
                    packCode,
                    setName,
                    월드CompositionVariantCodes.A),
                setName,
                월드CompositionVariantCodes.A,
                packCode,
                월드CompositionSourceKinds.SyntyNestedPrefab,
                footprint,
                cellSize,
                true,
                hasOcclusionRoot,
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
            return descriptor;
        }

        private static 월드CompositionConnectorContract C(
            string code,
            string direction,
            string kind,
            string signature,
            Vector3 position,
            float yaw,
            float width)
        {
            var connector = new 월드CompositionConnectorContract();
            connector.Configure(code, direction, kind, signature, position, yaw, width, true);
            return connector;
        }

        private static 월드CompositionSocketContract S(
            string code,
            string category,
            Vector3 position)
        {
            var socket = new 월드CompositionSocketContract();
            socket.Configure(code, category, position, Vector3.zero);
            return socket;
        }

        private static Placement P(
            string assetPath,
            Vector3 center,
            float yaw = 0f,
            bool occlusion = false)
            => new Placement(assetPath, center, yaw, occlusion);

        private static GameObject BuildPrefab(Definition definition)
        {
            var root = new GameObject(
                definition.Descriptor.SetName.Replace(" ", string.Empty) + "_A");
            try
            {
                var environmentRoot = new GameObject("EnvironmentRoot").transform;
                environmentRoot.SetParent(root.transform, false);
                Transform? occlusionRoot = null;
                if (definition.Descriptor.HasOcclusionRoot)
                {
                    occlusionRoot = new GameObject("OcclusionRoot").transform;
                    occlusionRoot.SetParent(root.transform, false);
                }

                for (var index = 0; index < definition.Placements.Length; index++)
                {
                    var placement = definition.Placements[index];
                    var parent = placement.Occlusion
                        ? occlusionRoot ?? throw new InvalidOperationException(
                            "AnchorOcclusionRootMissing:" + definition.Descriptor.SetName)
                        : environmentRoot;
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(placement.AssetPath)
                                 ?? throw new InvalidOperationException(
                                     "AnchorSourcePrefabMissing:" + placement.AssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject
                                   ?? throw new InvalidOperationException(
                                       "AnchorSourceInstantiateFailed:" + placement.AssetPath);
                    instance.name = $"Environment_{index + 1:D2}_{source.name}";
                    CenterOnGround(instance, parent, placement.Center, placement.Yaw);
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

                var view = root.AddComponent<거점CompositionSetView>();
                view.Configure(
                    definition.Descriptor,
                    environmentRoot,
                    occlusionRoot,
                    connectors,
                    sockets,
                    definition.SourceEntranceDirection,
                    definition.DesignedAccessDirection,
                    definition.VehicleTurnRadius);
                if (!view.ValidateWiring())
                    throw new InvalidOperationException(
                        "AnchorCompositionWiringInvalid:"
                        + definition.Descriptor.CompositionKey);

                var path = PrefabRoot + "/"
                           + definition.Descriptor.SetName.Replace(" ", string.Empty)
                           + "_A.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path)
                       ?? throw new InvalidOperationException(
                           "AnchorCompositionPrefabSaveFailed:" + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CenterOnGround(
            GameObject instance,
            Transform parent,
            Vector3 desiredCenter,
            float yaw)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("AnchorSourceRendererMissing:" + instance.name);
            var bounds = LocalBounds(parent, renderers);
            instance.transform.localPosition += new Vector3(
                desiredCenter.x - bounds.center.x,
                desiredCenter.y - bounds.min.y,
                desiredCenter.z - bounds.center.z);
        }

        private static Bounds LocalBounds(Transform root, IReadOnlyList<Renderer> renderers)
        {
            var initialized = false;
            var bounds = default(Bounds);
            foreach (var renderer in renderers)
            {
                var worldBounds = renderer.bounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = worldBounds.center + Vector3.Scale(
                        worldBounds.extents,
                        new Vector3(x, y, z));
                    var local = root.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return bounds;
        }

        private static void BuildPreviewScene(거점CompositionCatalog catalog)
        {
            var roadCatalog = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(
                                  도로GateCompositionSetBuilder.CatalogPath)
                              ?? throw new InvalidOperationException(
                                  "RoadGateCatalogMissingForAnchorPreview");
            roadCatalog.Validate();
            var groundMaterial = PreviewMaterial(
                PreviewGroundMaterialPath,
                new Color(.29f, .43f, .27f, 1f));
            var connectorMaterial = PreviewMaterial(
                PreviewConnectorMaterialPath,
                new Color(.96f, .48f, .12f, 1f));
            var socketMaterial = PreviewMaterial(
                PreviewSocketMaterialPath,
                new Color(.18f, .52f, .92f, 1f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MinimumAnchorCompositionLibrary");
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(48f, 0f, 0f),
                new Vector3(0f, 0f, 50f),
                new Vector3(48f, 0f, 50f),
            };
            var partnerNames = new[]
            {
                도로GateCompositionSetNames.농촌도로직선,
                도로GateCompositionSetNames.타운도로직선,
                도로GateCompositionSetNames.도시도로직선,
                도로GateCompositionSetNames.농장허브허브입구,
            };
            var partnerNorthDistances = new[] { 5.955307f, 7.5f, 7.5f, 5f };
            var entries = 거점CompositionSetNames.All.Select(catalog.Resolve).ToArray();
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var position = positions[index];
                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject
                               ?? throw new InvalidOperationException(
                                   "AnchorPreviewInstantiateFailed:" + entry.CompositionKey);
                instance.transform.SetParent(root.transform, true);
                instance.transform.position = position;
                instance.name = entry.Descriptor.SetName.Replace(" ", string.Empty) + "_A";
                var view = instance.GetComponent<거점CompositionSetView>();
                CreateMarkers(root.transform, view.ConnectorAnchors, connectorMaterial, .34f);
                CreateMarkers(root.transform, view.StateSocketAnchors, socketMaterial, .24f);

                var southConnector = entry.Descriptor.Connectors.First(value =>
                    value.DirectionCode == 월드CompositionConnectorDirectionCodes.South);
                var partnerEntry = roadCatalog.Resolve(partnerNames[index]);
                var partner = PrefabUtility.InstantiatePrefab(
                    partnerEntry.Prefab,
                    scene) as GameObject
                              ?? throw new InvalidOperationException(
                                  "AnchorRoadPartnerInstantiateFailed:" + partnerNames[index]);
                partner.transform.SetParent(root.transform, true);
                partner.transform.position = position + new Vector3(
                    southConnector.LocalPosition.x,
                    0f,
                    southConnector.LocalPosition.z - partnerNorthDistances[index]);
                partner.name = "ConnectorPartner_" + partnerNames[index].Replace(" ", string.Empty);

                var labelObject = new GameObject("Label_" + entry.Descriptor.SetName);
                labelObject.transform.SetParent(root.transform, false);
                labelObject.transform.position = position + new Vector3(
                    0f,
                    .3f,
                    entry.Descriptor.Footprint.y * .5f + 3f);
                labelObject.transform.eulerAngles = new Vector3(90f, 0f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = entry.Descriptor.SetName;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .25f;
                label.fontSize = 30;
                label.color = new Color(.08f, .07f, .05f, 1f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "PreviewGround";
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = new Vector3(24f, -1f, 18f);
            ground.transform.localScale = new Vector3(105f, .5f, 125f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.68f, .66f, .6f);
            RenderSettings.fog = false;
            var lightObject = new GameObject("PreviewDirectionalLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .9f, .76f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.eulerAngles = new Vector3(52f, -32f, 0f);

            var cameraObject = new GameObject("MinimumAnchorPreviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 39f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 350f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f, .79f, .82f);
            var focus = new Vector3(24f, 0f, 18f);
            cameraObject.transform.position = focus + new Vector3(0f, 82f, -88f);
            cameraObject.transform.LookAt(focus);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new InvalidOperationException(
                    "AnchorPreviewSceneSaveFailed:" + PreviewScenePath);
        }

        private static void CreateMarkers(
            Transform parent,
            IReadOnlyList<Transform> anchors,
            Material material,
            float radius)
        {
            foreach (var anchor in anchors)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Marker_" + anchor.name;
                marker.transform.SetParent(parent, false);
                marker.transform.position = anchor.position + Vector3.up * .5f;
                marker.transform.localScale = new Vector3(radius, .5f, radius);
                marker.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
        }

        private static Material PreviewMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("AnchorPreviewShaderMissing"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateDefinitions(IReadOnlyList<Definition> definitions)
        {
            if (definitions.Count != 4
                || definitions.Any(value => !value.Descriptor.Validate()
                    || value.Placements.Length == 0
                    || !거점CompositionEntranceCodes.IsKnown(
                        value.SourceEntranceDirection)
                    || !거점CompositionEntranceCodes.IsKnown(
                        value.DesignedAccessDirection)
                    || value.VehicleTurnRadius <= 0f)
                || definitions.Select(value => value.Descriptor.SetName)
                    .Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            {
                throw new InvalidOperationException("AnchorCompositionDefinitionsInvalid");
            }

            월드CompositionContractValidator.Validate(
                definitions.Select(value => value.Descriptor).ToArray(),
                false);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private sealed class Definition
        {
            public Definition(
                월드CompositionDescriptor descriptor,
                Placement[] placements,
                string sourceEntranceDirection,
                string designedAccessDirection,
                float vehicleTurnRadius)
            {
                Descriptor = descriptor;
                Placements = placements;
                SourceEntranceDirection = sourceEntranceDirection;
                DesignedAccessDirection = designedAccessDirection;
                VehicleTurnRadius = vehicleTurnRadius;
            }

            public 월드CompositionDescriptor Descriptor { get; }
            public Placement[] Placements { get; }
            public string SourceEntranceDirection { get; }
            public string DesignedAccessDirection { get; }
            public float VehicleTurnRadius { get; }
        }

        private sealed class Placement
        {
            public Placement(string assetPath, Vector3 center, float yaw, bool occlusion)
            {
                AssetPath = assetPath;
                Center = center;
                Yaw = yaw;
                Occlusion = occlusion;
            }

            public string AssetPath { get; }
            public Vector3 Center { get; }
            public float Yaw { get; }
            public bool Occlusion { get; }
        }
    }
}
