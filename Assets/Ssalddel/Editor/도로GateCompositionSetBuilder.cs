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
    public static class 도로GateCompositionSetBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/도로GateCompositionCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/RoadGate";
        public const string PreviewScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/도로출입구조합모음미리보기.unity";
        public const string PreviewGroundMaterialPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/RoadGatePreviewGround.mat";
        public const string PreviewInternalConnectorMaterialPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/RoadGatePreviewInternalConnector.mat";
        public const string PreviewExternalConnectorMaterialPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/RoadGatePreviewExternalConnector.mat";

        public const float TownCityCellSize = 5f;
        public const float FarmCellSize = 11.910614f;
        public const float FarmToTownAdapterOffset = -.955307f;

        private const string TownRoadPath =
            "Assets/Synty/PolygonTown/Prefabs/Environment/SM_Env_Road_01.prefab";
        private const string CityRoadPath =
            "Assets/Synty/PolygonCity/Prefabs/Environments/SM_Env_Road_01.prefab";
        private const string FarmRoadRoot =
            "Assets/Synty/PolygonFarm/Prefabs/Environments/";

        [MenuItem("Ssalddel/World Composition/Build Road and Gate A Sets")]
        public static void Build()
        {
            var definitions = CreateDefinitions();
            ValidateDefinitions(definitions);
            EnsureFolder(PrefabRoot);
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));

            var entries = definitions.Select(definition =>
            {
                var prefab = BuildPrefab(definition);
                var entry = new 도로GateCompositionCatalogEntry();
                entry.Configure(definition.Descriptor, prefab);
                return entry;
            }).ToArray();

            var catalog = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<도로GateCompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            catalog.Validate();
            BuildPreviewScene(catalog);
            Debug.Log($"RoadGateCompositionSetsBuilt:{entries.Length}:{CatalogPath}");
        }

        [MenuItem("Ssalddel/World Composition/Open Road and Gate Preview")]
        public static void OpenLibraryPreview()
            => EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);

        public static IReadOnlyList<월드CompositionDescriptor> CreateDescriptorsForValidation()
            => CreateDefinitions().Select(value => value.Descriptor).ToArray();

        private static Definition[] CreateDefinitions()
        {
            var values = new List<Definition>
            {
                CreateTownCityRoad(
                    도로GateCompositionSetNames.타운도로직선,
                    월드CompositionPackCodes.Town,
                    TownRoadPath,
                    Directions.North | Directions.South),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.타운도로모서리,
                    월드CompositionPackCodes.Town,
                    TownRoadPath,
                    Directions.North | Directions.East),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.타운도로T자,
                    월드CompositionPackCodes.Town,
                    TownRoadPath,
                    Directions.North | Directions.East | Directions.West),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.타운도로십자,
                    월드CompositionPackCodes.Town,
                    TownRoadPath,
                    Directions.North | Directions.East | Directions.South | Directions.West),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.도시도로직선,
                    월드CompositionPackCodes.City,
                    CityRoadPath,
                    Directions.North | Directions.South),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.도시도로모서리,
                    월드CompositionPackCodes.City,
                    CityRoadPath,
                    Directions.North | Directions.East),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.도시도로T자,
                    월드CompositionPackCodes.City,
                    CityRoadPath,
                    Directions.North | Directions.East | Directions.West),
                CreateTownCityRoad(
                    도로GateCompositionSetNames.도시도로십자,
                    월드CompositionPackCodes.City,
                    CityRoadPath,
                    Directions.North | Directions.East | Directions.South | Directions.West),
                CreateFarmRoad(
                    도로GateCompositionSetNames.농촌도로직선,
                    "SM_Env_Road_Dirt_Straight_01.prefab",
                    Directions.North | Directions.South),
                CreateFarmRoad(
                    도로GateCompositionSetNames.농촌도로모서리,
                    "SM_Env_Road_Dirt_Corner_01.prefab",
                    Directions.North | Directions.East),
                CreateFarmRoad(
                    도로GateCompositionSetNames.농촌도로T자,
                    "SM_Env_Road_Dirt_T_Section_01.prefab",
                    Directions.North | Directions.East | Directions.West),
                CreateFarmRoad(
                    도로GateCompositionSetNames.농촌도로십자,
                    "SM_Env_Road_Dirt_Intersection_01.prefab",
                    Directions.North | Directions.East | Directions.South | Directions.West),
            };

            values.AddRange(CreatePassengerGatePair(
                도로GateCompositionSetNames.농장타운농장출구,
                월드CompositionPackCodes.Farm,
                "farm",
                도로GateCompositionSetNames.농장타운타운입구,
                월드CompositionPackCodes.Town,
                "town",
                "boundary.farm-town"));
            values.AddRange(CreatePassengerGatePair(
                도로GateCompositionSetNames.타운시티타운출구,
                월드CompositionPackCodes.Town,
                "town",
                도로GateCompositionSetNames.타운시티시티입구,
                월드CompositionPackCodes.City,
                "city",
                "boundary.town-city"));
            values.AddRange(CreateFreightGatePair(
                도로GateCompositionSetNames.농장허브농장출구,
                월드CompositionPackCodes.Farm,
                "farm",
                도로GateCompositionSetNames.농장허브허브입구,
                월드CompositionPackCodes.RegionalLogisticsHub,
                "hub",
                "freight.farm-hub"));
            values.AddRange(CreateFreightGatePair(
                도로GateCompositionSetNames.타운허브타운출구,
                월드CompositionPackCodes.Town,
                "town",
                도로GateCompositionSetNames.타운허브허브입구,
                월드CompositionPackCodes.RegionalLogisticsHub,
                "hub",
                "freight.town-hub"));
            values.AddRange(CreateFreightGatePair(
                도로GateCompositionSetNames.허브시티허브출구,
                월드CompositionPackCodes.RegionalLogisticsHub,
                "hub",
                도로GateCompositionSetNames.허브시티시티입구,
                월드CompositionPackCodes.City,
                "city",
                "freight.hub-city"));
            return values.ToArray();
        }

        private static Definition CreateTownCityRoad(
            string setName,
            string packCode,
            string roadPath,
            Directions directions)
        {
            var placements = new List<Placement>
            {
                CreateCenteredTile(roadPath, Vector3.zero),
            };
            foreach (var direction in EnumerateDirections(directions))
            {
                var offset = DirectionVector(direction) * TownCityCellSize;
                placements.Add(CreateCenteredTile(roadPath, offset));
            }

            var connectors = CreateRoadConnectors(
                packCode,
                directions,
                7.5f,
                includeFarmMachine: false);
            var descriptor = CreateDescriptor(
                setName,
                packCode,
                new Vector2(15f, 15f),
                Vector2.one * TownCityCellSize,
                월드CompositionJourneyKindCodes.None,
                connectors,
                Array.Empty<월드CompositionSocketContract>());
            return new Definition(descriptor, placements.ToArray());
        }

        private static Definition CreateFarmRoad(
            string setName,
            string prefabName,
            Directions directions)
        {
            var connectors = CreateRoadConnectors(
                월드CompositionPackCodes.Farm,
                directions,
                FarmCellSize * .5f,
                includeFarmMachine: true);
            var descriptor = CreateDescriptor(
                setName,
                월드CompositionPackCodes.Farm,
                Vector2.one * FarmCellSize,
                Vector2.one * FarmCellSize,
                월드CompositionJourneyKindCodes.None,
                connectors,
                Array.Empty<월드CompositionSocketContract>());
            return new Definition(
                descriptor,
                new[] { new Placement(FarmRoadRoot + prefabName, Vector3.zero, 0f) });
        }

        private static IEnumerable<Definition> CreatePassengerGatePair(
            string sourceSetName,
            string sourcePackCode,
            string sourceVisualCode,
            string targetSetName,
            string targetPackCode,
            string targetVisualCode,
            string boundarySignature)
        {
            yield return CreateGate(
                sourceSetName,
                sourcePackCode,
                sourceVisualCode,
                true,
                new[]
                {
                    GateRoute("vehicle", 월드CompositionConnectorKindCodes.Vehicle,
                        RoadSignature(sourcePackCode, 월드CompositionConnectorKindCodes.Vehicle),
                        boundarySignature + ".vehicle.v1", 3.4f),
                    GateRoute("pedestrian", 월드CompositionConnectorKindCodes.Pedestrian,
                        RoadSignature(sourcePackCode, 월드CompositionConnectorKindCodes.Pedestrian),
                        boundarySignature + ".pedestrian.v1", 1.2f),
                });
            yield return CreateGate(
                targetSetName,
                targetPackCode,
                targetVisualCode,
                false,
                new[]
                {
                    GateRoute("vehicle", 월드CompositionConnectorKindCodes.Vehicle,
                        RoadSignature(targetPackCode, 월드CompositionConnectorKindCodes.Vehicle),
                        boundarySignature + ".vehicle.v1", 3.4f),
                    GateRoute("pedestrian", 월드CompositionConnectorKindCodes.Pedestrian,
                        RoadSignature(targetPackCode, 월드CompositionConnectorKindCodes.Pedestrian),
                        boundarySignature + ".pedestrian.v1", 1.2f),
                });
        }

        private static IEnumerable<Definition> CreateFreightGatePair(
            string sourceSetName,
            string sourcePackCode,
            string sourceVisualCode,
            string targetSetName,
            string targetPackCode,
            string targetVisualCode,
            string freightSignature)
        {
            yield return CreateGate(
                sourceSetName,
                sourcePackCode,
                sourceVisualCode,
                true,
                new[]
                {
                    GateRoute("vehicle", 월드CompositionConnectorKindCodes.Vehicle,
                        RoadSignature(sourcePackCode, 월드CompositionConnectorKindCodes.Vehicle),
                        freightSignature + ".vehicle.v1", 3.8f),
                });
            yield return CreateGate(
                targetSetName,
                targetPackCode,
                targetVisualCode,
                false,
                new[]
                {
                    GateRoute("vehicle", 월드CompositionConnectorKindCodes.Vehicle,
                        RoadSignature(targetPackCode, 월드CompositionConnectorKindCodes.Vehicle),
                        freightSignature + ".vehicle.v1", 3.8f),
                });
        }

        private static Definition CreateGate(
            string setName,
            string packCode,
            string visualCode,
            bool sourceEndpoint,
            IReadOnlyList<GateRouteDefinition> routes)
        {
            var internalDirection = sourceEndpoint
                ? 월드CompositionConnectorDirectionCodes.South
                : 월드CompositionConnectorDirectionCodes.North;
            var externalDirection = sourceEndpoint
                ? 월드CompositionConnectorDirectionCodes.North
                : 월드CompositionConnectorDirectionCodes.South;
            var connectors = routes.SelectMany(route => new[]
            {
                Connector(
                    "internal-" + route.Code,
                    internalDirection,
                    route.Kind,
                    route.InternalSignature,
                    DirectionPosition(internalDirection, 5f),
                    DirectionYaw(internalDirection),
                    route.Width,
                    false),
                Connector(
                    "external-" + route.Code,
                    externalDirection,
                    route.Kind,
                    route.ExternalSignature,
                    DirectionPosition(externalDirection, 5f),
                    DirectionYaw(externalDirection),
                    route.Width,
                    true),
            }).ToArray();
            var sockets = routes.Count > 1
                ? new[] { Socket("traveller", 월드CompositionSocketCategoryCodes.Actor) }
                : new[]
                {
                    Socket("freight-vehicle", 월드CompositionSocketCategoryCodes.Vehicle,
                        new Vector3(-1.5f, 0f, 0f)),
                    Socket("freight-cargo", 월드CompositionSocketCategoryCodes.Cargo,
                        new Vector3(1.5f, 0f, 0f)),
                };
            var descriptor = CreateDescriptor(
                setName,
                packCode,
                new Vector2(10f, 10f),
                Vector2.one * TownCityCellSize,
                월드CompositionJourneyKindCodes.Stateful,
                connectors,
                sockets);
            return new Definition(descriptor, CreateGateVisuals(visualCode));
        }

        private static Placement[] CreateGateVisuals(string visualCode)
        {
            if (visualCode == "farm")
            {
                return new[]
                {
                    new Placement(
                        FarmRoadRoot + "SM_Env_Road_Dirt_Straight_01.prefab",
                        new Vector3(0f, 0f, FarmToTownAdapterOffset),
                        0f),
                    new Placement(
                        "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Ranch_Sign_01.prefab",
                        new Vector3(3f, 0f, 0f),
                        0f),
                };
            }

            if (visualCode == "town")
            {
                return new[]
                {
                    CreateCenteredTile(TownRoadPath, Vector3.zero),
                    new Placement(
                        "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_StreetSign_Pole_01.prefab",
                        new Vector3(3f, 0f, 0f),
                        0f),
                };
            }

            if (visualCode == "city")
            {
                return new[]
                {
                    CreateCenteredTile(CityRoadPath, Vector3.zero),
                    new Placement(
                        "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Sign_Entrance_01.prefab",
                        new Vector3(3f, 0f, 0f),
                        0f),
                };
            }

            return new[]
            {
                CreateCenteredTile(CityRoadPath, Vector3.zero),
                new Placement(
                    "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Barrier_01.prefab",
                    new Vector3(2.5f, 0f, 0f),
                    90f),
                new Placement(
                    "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Cone_01.prefab",
                    new Vector3(-2.5f, 0f, 0f),
                    0f),
            };
        }

        private static 월드CompositionDescriptor CreateDescriptor(
            string setName,
            string packCode,
            Vector2 footprint,
            Vector2 cellSize,
            string journeyKind,
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
                false,
                false,
                journeyKind,
                new[]
                {
                    월드CompositionDetailTierCodes.World,
                    월드CompositionDetailTierCodes.Zone,
                },
                connectors,
                sockets);
            return descriptor;
        }

        private static 월드CompositionConnectorContract[] CreateRoadConnectors(
            string packCode,
            Directions directions,
            float distance,
            bool includeFarmMachine)
        {
            var kinds = new List<string>
            {
                월드CompositionConnectorKindCodes.Vehicle,
                월드CompositionConnectorKindCodes.Pedestrian,
            };
            if (includeFarmMachine)
                kinds.Add(월드CompositionConnectorKindCodes.FarmMachine);
            return EnumerateDirections(directions).SelectMany(direction =>
                kinds.Select(kind => Connector(
                    kind + "-" + direction,
                    direction,
                    kind,
                    RoadSignature(packCode, kind),
                    DirectionPosition(direction, distance),
                    DirectionYaw(direction),
                    kind == 월드CompositionConnectorKindCodes.Pedestrian ? 1.2f : 3.4f,
                    true))).ToArray();
        }

        private static string RoadSignature(string packCode, string kind)
            => "road." + packCode + "." + kind + ".v1";

        private static 월드CompositionConnectorContract Connector(
            string code,
            string direction,
            string kind,
            string signature,
            Vector3 position,
            float yaw,
            float width,
            bool expansion)
        {
            var connector = new 월드CompositionConnectorContract();
            connector.Configure(
                code,
                direction,
                kind,
                signature,
                position,
                yaw,
                width,
                expansion);
            return connector;
        }

        private static 월드CompositionSocketContract Socket(
            string code,
            string category,
            Vector3 position = default)
        {
            var socket = new 월드CompositionSocketContract();
            socket.Configure(code, category, position, Vector3.zero);
            return socket;
        }

        private static Placement CreateCenteredTile(string assetPath, Vector3 tileCenter)
            => new Placement(
                assetPath,
                tileCenter + new Vector3(2.5f, 0f, 2.5f),
                0f);

        private static GameObject BuildPrefab(Definition definition)
        {
            var root = new GameObject(
                definition.Descriptor.SetName.Replace(" ", string.Empty) + "_A");
            try
            {
                var environmentRoot = new GameObject("EnvironmentRoot").transform;
                environmentRoot.SetParent(root.transform, false);
                for (var index = 0; index < definition.Placements.Length; index++)
                {
                    var placement = definition.Placements[index];
                    var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(placement.AssetPath)
                        ?? throw new InvalidOperationException(
                            "RoadGateSourcePrefabMissing:" + placement.AssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(
                        sourcePrefab,
                        environmentRoot) as GameObject
                        ?? throw new InvalidOperationException(
                            "RoadGateSourceInstantiateFailed:" + placement.AssetPath);
                    instance.name = $"Environment_{index + 1:D2}_{sourcePrefab.name}";
                    instance.transform.localPosition = placement.LocalPosition;
                    instance.transform.localEulerAngles = new Vector3(0f, placement.Yaw, 0f);
                }

                var connectorRoot = new GameObject("RouteConnectors").transform;
                connectorRoot.SetParent(root.transform, false);
                var anchors = definition.Descriptor.Connectors.Select(contract =>
                {
                    var anchor = new GameObject("Connector_" + contract.ConnectorCode).transform;
                    anchor.SetParent(connectorRoot, false);
                    anchor.localPosition = contract.LocalPosition;
                    anchor.localEulerAngles = new Vector3(0f, contract.LocalYaw, 0f);
                    return anchor;
                }).ToArray();

                var view = root.AddComponent<도로GateCompositionSetView>();
                view.Configure(definition.Descriptor, environmentRoot, anchors);
                if (!view.ValidateWiring())
                    throw new InvalidOperationException(
                        "RoadGateCompositionWiringInvalid:"
                        + definition.Descriptor.CompositionKey);

                var path = PrefabRoot + "/"
                           + definition.Descriptor.SetName.Replace(" ", string.Empty)
                           + "_A.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path)
                       ?? throw new InvalidOperationException(
                           "RoadGateCompositionPrefabSaveFailed:" + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPreviewScene(도로GateCompositionCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var previewRoot = new GameObject("도로GateCompositionLibrary");
            var groundMaterial = GetOrCreatePreviewMaterial(
                PreviewGroundMaterialPath,
                new Color(.28f, .42f, .25f, 1f));
            var internalConnectorMaterial = GetOrCreatePreviewMaterial(
                PreviewInternalConnectorMaterialPath,
                new Color(.18f, .46f, .82f, 1f));
            var externalConnectorMaterial = GetOrCreatePreviewMaterial(
                PreviewExternalConnectorMaterialPath,
                new Color(.95f, .48f, .12f, 1f));
            var entries = 도로GateCompositionSetNames.All.Select(catalog.Resolve).ToArray();
            for (var index = 0; index < entries.Length; index++)
            {
                var column = index % 6;
                var row = index / 6;
                var position = new Vector3(column * 18f, 0f, row * 18f);
                var entry = entries[index];
                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject
                               ?? throw new InvalidOperationException(
                                   "RoadGatePreviewInstantiateFailed:" + entry.CompositionKey);
                instance.name = entry.Descriptor.SetName.Replace(" ", string.Empty) + "_A";
                instance.transform.SetParent(previewRoot.transform, true);
                instance.transform.position = position;

                var view = instance.GetComponent<도로GateCompositionSetView>();
                for (var connectorIndex = 0;
                     connectorIndex < view.ConnectorAnchors.Count;
                     connectorIndex++)
                {
                    var anchor = view.ConnectorAnchors[connectorIndex];
                    var connector = view.Descriptor.Connectors[connectorIndex];
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.name = "ConnectorMarker_" + anchor.name;
                    marker.transform.SetParent(previewRoot.transform, false);
                    marker.transform.position = anchor.position + Vector3.up * .45f;
                    marker.transform.localScale = new Vector3(.28f, .45f, .28f);
                    marker.GetComponent<Renderer>().sharedMaterial = connector.ExpansionSocket
                        ? externalConnectorMaterial
                        : internalConnectorMaterial;
                    Object.DestroyImmediate(marker.GetComponent<Collider>());
                }

                var labelObject = new GameObject("Label_" + entry.Descriptor.SetName);
                labelObject.transform.SetParent(previewRoot.transform, false);
                labelObject.transform.position = position + new Vector3(0f, .25f, -9f);
                labelObject.transform.eulerAngles = new Vector3(90f, 0f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = entry.Descriptor.SetName;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .22f;
                label.fontSize = 28;
                label.color = new Color(.12f, .1f, .08f, 1f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "PreviewGround";
            ground.transform.SetParent(previewRoot.transform, false);
            ground.transform.position = new Vector3(45f, -.8f, 27f);
            ground.transform.localScale = new Vector3(110f, .5f, 74f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.66f, .64f, .58f);
            RenderSettings.fog = false;

            var lightObject = new GameObject("PreviewDirectionalLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .9f, .76f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.eulerAngles = new Vector3(55f, -30f, 0f);

            var cameraObject = new GameObject("RoadGatePreviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 38f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f, .79f, .82f);
            var focus = new Vector3(45f, 0f, 27f);
            cameraObject.transform.position = focus + new Vector3(0f, 82f, -76f);
            cameraObject.transform.LookAt(focus);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new InvalidOperationException(
                    "RoadGatePreviewSceneSaveFailed:" + PreviewScenePath);
        }

        private static Material GetOrCreatePreviewMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? throw new InvalidOperationException(
                                 "RoadGatePreviewShaderMissing");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateDefinitions(IReadOnlyList<Definition> definitions)
        {
            if (definitions.Count != 도로GateCompositionSetNames.All.Count
                || definitions.Any(value => !value.Descriptor.Validate()
                    || value.Descriptor.VariantCode != 월드CompositionVariantCodes.A
                    || value.Placements.Length == 0)
                || definitions.Select(value => value.Descriptor.SetName)
                    .Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            {
                throw new InvalidOperationException("RoadGateCompositionDefinitionsInvalid");
            }

            월드CompositionContractValidator.Validate(
                definitions.Select(value => value.Descriptor).ToArray(),
                false);
        }

        private static IEnumerable<string> EnumerateDirections(Directions value)
        {
            if ((value & Directions.North) != 0)
                yield return 월드CompositionConnectorDirectionCodes.North;
            if ((value & Directions.East) != 0)
                yield return 월드CompositionConnectorDirectionCodes.East;
            if ((value & Directions.South) != 0)
                yield return 월드CompositionConnectorDirectionCodes.South;
            if ((value & Directions.West) != 0)
                yield return 월드CompositionConnectorDirectionCodes.West;
        }

        private static Vector3 DirectionVector(string direction)
            => direction switch
            {
                월드CompositionConnectorDirectionCodes.North => Vector3.forward,
                월드CompositionConnectorDirectionCodes.East => Vector3.right,
                월드CompositionConnectorDirectionCodes.South => Vector3.back,
                월드CompositionConnectorDirectionCodes.West => Vector3.left,
                _ => throw new InvalidOperationException("RoadDirectionUnknown:" + direction),
            };

        private static Vector3 DirectionPosition(string direction, float distance)
            => DirectionVector(direction) * distance;

        private static float DirectionYaw(string direction)
            => direction switch
            {
                월드CompositionConnectorDirectionCodes.North => 0f,
                월드CompositionConnectorDirectionCodes.East => 90f,
                월드CompositionConnectorDirectionCodes.South => 180f,
                월드CompositionConnectorDirectionCodes.West => 270f,
                _ => throw new InvalidOperationException("RoadDirectionUnknown:" + direction),
            };

        private static GateRouteDefinition GateRoute(
            string code,
            string kind,
            string internalSignature,
            string externalSignature,
            float width)
            => new GateRouteDefinition(code, kind, internalSignature, externalSignature, width);

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

        [Flags]
        private enum Directions
        {
            North = 1,
            East = 2,
            South = 4,
            West = 8,
        }

        private sealed class Definition
        {
            public Definition(월드CompositionDescriptor descriptor, Placement[] placements)
            {
                Descriptor = descriptor;
                Placements = placements;
            }

            public 월드CompositionDescriptor Descriptor { get; }
            public Placement[] Placements { get; }
        }

        private sealed class Placement
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

        private sealed class GateRouteDefinition
        {
            public GateRouteDefinition(
                string code,
                string kind,
                string internalSignature,
                string externalSignature,
                float width)
            {
                Code = code;
                Kind = kind;
                InternalSignature = internalSignature;
                ExternalSignature = externalSignature;
                Width = width;
            }

            public string Code { get; }
            public string Kind { get; }
            public string InternalSignature { get; }
            public string ExternalSignature { get; }
            public float Width { get; }
        }
    }
}
