using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.Cargo;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class CityFarmCargoJourneyBuilder
    {
        public const string SourceScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시업무화면통합.unity";
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시화물이동.unity";
        public const string IntegrationRootName = "WORLD-4 Cargo Journey";
        public const string CargoStableId = "cargo:transport-71";

        private const string UrbanCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/UrbanVisualCatalog.asset";
        private const string MaterialDirectory =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Materials";

        [MenuItem("Ssalddel/WORLD-4/Build Cargo Journey")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("WORLD4SourceSceneMissing");

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            var world = Require("WorldBootstrap");
            var previous = world.transform.Find(IntegrationRootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(IntegrationRootName);
            root.transform.SetParent(world.transform, false);
            var model = CreateModel(CargoHandoffStateCodes.ArrivedAtWarehouse, 5);
            var anchors = BuildAnchors();
            var logisticsZone = Require(
                "WorldBootstrap/ZoneRoots/Zone_UrbanLogistics").transform;
            var summary = Text(logisticsZone, "CargoJourneySummary", string.Empty,
                new Vector3(0f, .045f, -6.05f), .011f);
            var lineage = Text(logisticsZone, "CargoJourneyLineage", string.Empty,
                new Vector3(0f, .045f, 5.65f), .008f);

            var view = root.AddComponent<CargoJourneyView>();
            view.Configure(anchors, new[]
            {
                StateMaterial(CargoJourneyAnchorStateCodes.Previous,
                    "CargoPrevious", new Color(.32f, .55f, .78f)),
                StateMaterial(CargoJourneyAnchorStateCodes.Current,
                    "CargoCurrent", new Color(1f, .62f, .10f)),
                StateMaterial(CargoJourneyAnchorStateCodes.Next,
                    "CargoNext", new Color(.25f, .72f, .38f)),
                StateMaterial(CargoJourneyAnchorStateCodes.Planned,
                    "CargoPlanned", new Color(.55f, .48f, .68f)),
            }, summary, lineage);
            view.Apply(model);

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("WORLD4SceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("WORLD-4 cargo journey created: " + ScenePath);
        }

        [MenuItem("Ssalddel/WORLD-4/Validate Cargo Journey")]
        public static void ValidateOpenScene()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<CargoJourneyView>();
            var anchors = UnityEngine.Object.FindObjectsByType<CargoJourneyAnchorView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (view == null || !view.ValidateApplied() || view.AnchorCount != 4)
                throw new InvalidOperationException("WORLD4CargoJourneyWiringInvalid");
            if (!string.Equals(view.CargoStableId, CargoStableId, StringComparison.Ordinal)
                || anchors.Length != 4
                || anchors.Any(value => !value.ValidateApplied()
                    || !string.Equals(value.CargoStableId, CargoStableId, StringComparison.Ordinal)))
                throw new InvalidOperationException("WORLD4CargoIdentityInvalid");
            if (view.SourceStableIds.Count != 6
                || !view.SourceStableIds.Contains("farm-handoff:sim.potato.1")
                || !view.SourceStableIds.Contains("product:potato")
                || !view.SourceStableIds.Contains("cargo-handoff:transport-71.inbound-91"))
                throw new InvalidOperationException("WORLD4CargoLineageInvalid");

            var logistics = anchors.Single(value =>
                value.ZoneCode == CargoJourneyZoneCodes.UrbanLogistics);
            var market = anchors.Single(value =>
                value.ZoneCode == CargoJourneyZoneCodes.UrbanMarket);
            if (logistics.StateCode != CargoJourneyAnchorStateCodes.Current
                || market.StateCode != CargoJourneyAnchorStateCodes.Planned)
                throw new InvalidOperationException("WORLD4AuthorityBoundaryInvalid");
            if (anchors.Any(value => value.VisualInstance == null
                || !value.VisualInstance.ValidateWiring()))
                throw new InvalidOperationException("WORLD4VisualKeyWiringInvalid");
            if (anchors.Select(value => value.GetComponent<WorldPresentationFallbackView>())
                .Any(value => value == null || !value.ValidateWiring()))
                throw new InvalidOperationException("WORLD4FallbackWiringInvalid");
        }

        public static CargoJourneyPresentationModel CreateModel(string stateCode, long revision)
            => new CargoJourneyProjector().Project(new CargoJourneyProjectionInput
            {
                Mode = DataRuntimeMode.Simulation,
                ProductStableId = "product:potato",
                OriginSourceStableId = "farm-handoff:sim.potato.1",
                Handoff = new CargoWarehouseHandoffSnapshot
                {
                    StableId = "cargo-handoff:transport-71.inbound-91",
                    Revision = revision,
                    HandoffStateCode = stateCode,
                    CargoStableId = CargoStableId,
                    TransportTaskStableId = "transport-task:71",
                    InboundTaskStableId = "inbound-task:91",
                    GeneratedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                },
            });

        private static CargoJourneyAnchorView[] BuildAnchors()
        {
            var farm = Require(
                "WorldBootstrap/ZoneRoots/Zone_FarmYard/VisualRoot/PotatoBoxVisual_0");
            var transport = Require(
                "WorldBootstrap/ZoneRoots/Zone_TransportCorridor/VisualRoot/TransportCargoVisual");
            var logistics = UnityEngine.Object.FindObjectsByType<WorldVisualInstanceView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.gameObject.name == "LogisticsPalletVisual_0").gameObject;
            var marketZone = Require(
                "WorldBootstrap/ZoneRoots/Zone_UrbanMarket").transform;
            var marketVisualRoot = Require(marketZone, "VisualRoot").transform;
            var urbanCatalog = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(UrbanCatalogPath)
                ?? throw new InvalidOperationException("WORLD4UrbanCatalogMissing");
            var existingMarket = marketVisualRoot.Find("MarketBackroomCargoVisual");
            if (existingMarket != null)
                UnityEngine.Object.DestroyImmediate(existingMarket.gameObject);
            var market = Visual(marketVisualRoot, urbanCatalog, UrbanVisualKeys.CargoBox,
                "MarketBackroomCargoVisual", new Vector3(-3.3f, 0f, -2.5f),
                new Vector3(0f, 18f, 0f)).gameObject;

            return new[]
            {
                Anchor(farm, CargoJourneyZoneCodes.FarmYard,
                    CargoJourneyVisualRoleCodes.FarmPackedBox, FarmVisualKeys.PotatoBox),
                Anchor(transport, CargoJourneyZoneCodes.TransportCorridor,
                    CargoJourneyVisualRoleCodes.VehicleLoad, UrbanVisualKeys.CargoBox),
                Anchor(logistics, CargoJourneyZoneCodes.UrbanLogistics,
                    CargoJourneyVisualRoleCodes.LogisticsPallet, UrbanVisualKeys.Pallet),
                Anchor(market, CargoJourneyZoneCodes.UrbanMarket,
                    CargoJourneyVisualRoleCodes.MarketBackroom, UrbanVisualKeys.CargoBox),
            };
        }

        private static CargoJourneyAnchorView Anchor(
            GameObject wrapper, string zoneCode, string roleCode, string expectedVisualKey)
        {
            var visual = wrapper.GetComponent<WorldVisualInstanceView>()
                ?? throw new InvalidOperationException("WORLD4VisualInstanceMissing:" + wrapper.name);
            if (!string.Equals(visual.VisualKey, expectedVisualKey, StringComparison.Ordinal))
                throw new InvalidOperationException("WORLD4VisualRoleMismatch:" + wrapper.name);

            var oldMarker = wrapper.transform.Find("CargoJourneyStateMarker");
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            var marker = Primitive(wrapper.transform, "CargoJourneyStateMarker",
                new Vector3(0f, .72f, 0f), new Vector3(.85f, .08f, .35f),
                Material("CargoPlanned", new Color(.55f, .48f, .68f)));
            var collider = marker.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var label = Text(wrapper.transform, "CargoJourneyStateLabel", string.Empty,
                new Vector3(0f, 1.05f, 0f), .008f);

            var fallback = wrapper.GetComponent<WorldPresentationFallbackView>();
            if (fallback == null)
            {
                var fallbackRoot = Primitive(wrapper.transform, "CargoJourneyPrimitiveFallback",
                    new Vector3(0f, .15f, 0f), new Vector3(.75f, .35f, .75f),
                    Material("CargoJourneyPrimitive", new Color(.68f, .48f, .22f)));
                var fallbackCollider = fallbackRoot.GetComponent<Collider>();
                if (fallbackCollider != null) UnityEngine.Object.DestroyImmediate(fallbackCollider);
                fallback = wrapper.AddComponent<WorldPresentationFallbackView>();
                fallback.Configure(visual.VisualRoot, fallbackRoot);
            }

            var anchor = wrapper.GetComponent<CargoJourneyAnchorView>()
                ?? wrapper.AddComponent<CargoJourneyAnchorView>();
            anchor.Configure(zoneCode, roleCode, visual, marker.GetComponent<Renderer>(), label);
            return anchor;
        }

        private static WorldVisualInstanceView Visual(
            Transform parent, WorldVisualCatalog catalog, string key, string name,
            Vector3 localPosition, Vector3 localEuler)
        {
            var entry = catalog.Resolve(key);
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = localPosition;
            wrapper.transform.localRotation = Quaternion.Euler(localEuler);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject
                ?? throw new InvalidOperationException("WORLD4PrefabInstantiationFailed:" + key);
            instance.name = "SyntyPrefabInstance";
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localPosition = entry.LocalPositionCorrection;
            instance.transform.localRotation = Quaternion.Euler(entry.LocalEulerCorrection);
            instance.transform.localScale = entry.LocalScale;
            var view = wrapper.AddComponent<WorldVisualInstanceView>();
            view.Configure(key, catalog, visualRoot, instance);
            return view;
        }

        private static CargoJourneyStateMaterialBinding StateMaterial(
            string stateCode, string name, Color color)
            => new CargoJourneyStateMaterialBinding
            {
                StateCode = stateCode,
                Material = Material(name, color),
            };

        private static GameObject Require(string path)
            => GameObject.Find(path)
               ?? throw new InvalidOperationException("WORLD4ObjectMissing:" + path);

        private static GameObject Require(Transform parent, string path)
            => parent.Find(path)?.gameObject
               ?? throw new InvalidOperationException("WORLD4ObjectMissing:" + parent.name + "/" + path);

        private static GameObject Primitive(
            Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = localScale;
            value.GetComponent<Renderer>().sharedMaterial = material;
            return value;
        }

        private static TextMesh Text(
            Transform parent, string name, string value, Vector3 localPosition, float characterSize)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = target.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = Color.white;
            return text;
        }

        private static Material Material(string name, Color color)
        {
            Directory.CreateDirectory(MaterialDirectory);
            var path = MaterialDirectory + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? throw new InvalidOperationException("WORLD4CompatibleShaderMissing");
                material = new Material(shader) { name = name, color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.color = color;
                EditorUtility.SetDirty(material);
            }

            return material;
        }
    }
}
