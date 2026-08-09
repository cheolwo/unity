using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.LearningCards;
using Ssalddel.Unity.ResidentialPickup;
using Ssalddel.Unity.Samples.Farm;
using Ssalddel.Unity.Samples.ResidentialPickup;
using Ssalddel.Unity.Samples.UrbanLogisticsCenter;
using Ssalddel.Unity.Samples.UrbanMarket;
using Ssalddel.Unity.Transport;
using Ssalddel.Unity.UrbanMarket;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class CityFarmBusinessViewIntegrationBuilder
    {
        public const string SourceScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmSyntyWorldPrototype.unity";
        public const string ScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmBusinessViewIntegration.unity";
        public const string IntegrationRootName = "WORLD-3 Business Presentation";

        private const string MaterialDirectory =
            "Assets/Ssalddel/Experiments/CityFarmWorldIntegration/Materials";

        [MenuItem("Ssalddel/WORLD-3/Build Business View Integration")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("WORLD2SourceSceneMissing");

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("WORLD2WorldBootstrapMissing");
            var existing = world.transform.Find(IntegrationRootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var integration = new GameObject(IntegrationRootName);
            integration.transform.SetParent(world.transform, false);
            var selection = CreateSelectionEvidence(integration.transform);
            BuildFarm(integration.transform);
            BuildLogistics(integration.transform);
            BuildMarket(integration.transform, selection);
            BuildResidential(integration.transform);
            var coordinator = integration.AddComponent<WorldBusinessPresentationCoordinator>();
            coordinator.Configure(
                UnityEngine.Object.FindObjectsByType<도심마트ManagerShelfView>(FindObjectsSortMode.None)
                    .OrderBy(value => value.PresentationStableId, StringComparer.Ordinal).ToArray(),
                UnityEngine.Object.FindFirstObjectByType<ConceptCardDeckView>(FindObjectsInactive.Include),
                selection);
            coordinator.InitializePresentation();

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("WORLD3SceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = integration;
            SceneView.RepaintAll();
            Debug.Log("WORLD-3 business View integration created: " + ScenePath);
        }

        [MenuItem("Ssalddel/WORLD-3/Validate Business View Integration")]
        public static void ValidateOpenScene()
        {
            var grid = UnityEngine.Object.FindFirstObjectByType<FarmSoilTileGridView>();
            var logistics = UnityEngine.Object.FindFirstObjectByType<LogisticsFacilityOverviewView>();
            var shelves = UnityEngine.Object.FindObjectsByType<도심마트ManagerShelfView>(FindObjectsSortMode.None);
            var cards = UnityEngine.Object.FindFirstObjectByType<ConceptCardDeckView>(FindObjectsInactive.Include);
            var pickup = UnityEngine.Object.FindFirstObjectByType<ResidentialPickupView>();
            var pickupPoint = UnityEngine.Object.FindFirstObjectByType<ResidentialPickupPointView>();
            var selection = UnityEngine.Object.FindFirstObjectByType<WorldSelectionEvidenceView>();
            var coordinator = UnityEngine.Object.FindFirstObjectByType<WorldBusinessPresentationCoordinator>();
            var fallbacks = UnityEngine.Object.FindObjectsByType<WorldPresentationFallbackView>(FindObjectsSortMode.None);

            if (grid == null || grid.CellCount != 36 || !grid.ValidateWiring())
                throw new InvalidOperationException("WORLD3FarmGridWiringInvalid");
            if (logistics == null || logistics.AreaCount != 4 || !logistics.ValidateWiring())
                throw new InvalidOperationException("WORLD3LogisticsWiringInvalid");
            if (shelves.Length != 2 || shelves.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException("WORLD3MarketShelfWiringInvalid");
            if (cards == null || cards.SlotCount != 7 || !cards.ValidateWiring())
                throw new InvalidOperationException("WORLD3ConceptCardWiringInvalid");
            if (pickup == null || pickupPoint == null || !pickup.ValidateWiring()
                || pickupPoint.StableId != "pickup-point:residential:sample-1")
                throw new InvalidOperationException("WORLD3ResidentialPickupWiringInvalid");
            if (selection == null || !selection.ValidateWiring())
                throw new InvalidOperationException("WORLD3SelectionEvidenceWiringInvalid");
            if (coordinator == null || !coordinator.ValidateWiring())
                throw new InvalidOperationException("WORLD3PresentationCoordinatorWiringInvalid");
            if (fallbacks.Length < 40 || fallbacks.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException("WORLD3FallbackWiringInvalid");
            if (UnityEngine.Object.FindObjectsByType<FarmSoilTileSimulationController>(
                    FindObjectsSortMode.None).Length != 0)
                throw new InvalidOperationException("WORLD3PresentationMustNotOwnSimulationTick");
        }

        private static WorldSelectionEvidenceView CreateSelectionEvidence(Transform parent)
        {
            var root = new GameObject("SelectionEvidence");
            root.transform.SetParent(parent, false);
            var label = Text(root.transform, "SelectionLabel", "SELECT BUSINESS OBJECT",
                new Vector3(0f, .03f, -7f), .018f);
            var view = root.AddComponent<WorldSelectionEvidenceView>();
            view.Configure(label);
            return view;
        }

        private static void BuildFarm(Transform integration)
        {
            var zone = Require("WorldBootstrap/ZoneRoots/Zone_FarmProduction").transform;
            var root = new GameObject("FarmSoilTilePresentation");
            root.transform.SetParent(integration, false);
            var cells = new List<FarmSoilTileCellView>(36);
            for (var z = 0; z < 6; z++)
            for (var x = 0; x < 6; x++)
            {
                var wrapper = zone.Find("VisualRoot/FarmSoilRow_" + x + "_" + z)
                    ?? throw new InvalidOperationException("WORLD3FarmSoilVisualMissing:" + x + ":" + z);
                var overlay = Primitive(wrapper, "TileStateOverlay",
                    new Vector3(0f, .09f, 0f), new Vector3(1.05f, .035f, 1.05f),
                    Material("FarmTileUntilled", new Color(.42f, .24f, .12f)));
                var cell = overlay.AddComponent<FarmSoilTileCellView>();
                cell.Configure($"farm-soil-tile:sim.potato.{x}.{z}", overlay.GetComponent<Renderer>());
                cells.Add(cell);
                AttachFallback(wrapper.gameObject, Material("PrimitiveFarmTile", new Color(.52f, .31f, .16f)));
            }

            var mode = Text(zone, "FarmGridMode", string.Empty, new Vector3(-5f, .05f, -5.1f), .014f);
            var title = Text(zone, "FarmGridSelectionTitle", string.Empty, new Vector3(-5f, .05f, -4.45f), .016f);
            var detail = Text(zone, "FarmGridSelectionDetail", string.Empty, new Vector3(-5f, .05f, -3.75f), .012f);
            var grid = root.AddComponent<FarmSoilTileGridView>();
            grid.Configure(cells.ToArray(), new[]
            {
                FarmMaterial(FarmSoilTileColorTokens.Untilled, "FarmTileUntilled", new Color(.42f, .24f, .12f)),
                FarmMaterial(FarmSoilTileColorTokens.Tilled, "FarmTileTilled", new Color(.56f, .34f, .15f)),
                FarmMaterial(FarmSoilTileColorTokens.Sown, "FarmTileSown", new Color(.28f, .50f, .18f)),
                FarmMaterial(FarmSoilTileColorTokens.Selected, "FarmTileSelected", new Color(1f, .72f, .10f)),
            }, mode, title, detail);
            var snapshot = FarmPotatoSoilTileSimulationFixture.Create();
            grid.Apply(new FarmSoilTileMapProjector(new FarmSoilTileSimulationValidator()).Project(
                snapshot, "farm-soil-tile:sim.potato.2.2"));
        }

        private static FarmSoilTileMaterialBinding FarmMaterial(
            string token, string name, Color color)
            => new FarmSoilTileMaterialBinding { ColorToken = token, Material = Material(name, color) };

        private static void BuildLogistics(Transform integration)
        {
            var zone = Require("WorldBootstrap/ZoneRoots/Zone_UrbanLogistics").transform;
            var building = Require(zone, "VisualRoot/LogisticsBuildingVisual");
            var cargo = Require(zone, "VisualRoot/LogisticsPalletVisual_0");
            AttachFallback(building, Material("PrimitiveLogisticsBuilding", new Color(.38f, .43f, .48f)),
                new Vector3(4.5f, 1.4f, 3f));
            AttachFallback(cargo, Material("PrimitiveCargo", new Color(.68f, .48f, .22f)));

            var root = new GameObject("LogisticsFacilityPresentation");
            root.transform.SetParent(zone, false);
            var areaCodes = new[]
            {
                LogisticsFacilityAreaCodes.VehicleGate,
                LogisticsFacilityAreaCodes.InboundDock,
                LogisticsFacilityAreaCodes.Inspection,
                LogisticsFacilityAreaCodes.Storage,
            };
            var positions = new[]
            {
                new Vector3(-4.2f, .04f, -3.3f), new Vector3(-1.7f, .04f, -3.3f),
                new Vector3(.8f, .04f, -3.3f), new Vector3(3.3f, .04f, -3.3f),
            };
            var areas = new LogisticsFacilityAreaBinding[areaCodes.Length];
            for (var index = 0; index < areaCodes.Length; index++)
            {
                var marker = Primitive(root.transform, areaCodes[index] + "Area",
                    positions[index], new Vector3(1.75f, .08f, 1.35f),
                    Material("LogisticsIdle", new Color(.34f, .37f, .40f)));
                var anchor = new GameObject("CargoAnchor").transform;
                anchor.SetParent(marker.transform, false);
                anchor.localPosition = new Vector3(0f, .3f, 0f);
                var label = Text(marker.transform, "Status", areaCodes[index],
                    new Vector3(0f, .12f, 0f), .010f);
                areas[index] = new LogisticsFacilityAreaBinding
                {
                    AreaCode = areaCodes[index], VisualRoot = marker,
                    CargoAnchor = anchor, StatusRenderer = marker.GetComponent<Renderer>(),
                    StatusLabel = label,
                };
            }

            var summary = Text(root.transform, "Summary", string.Empty,
                new Vector3(0f, .04f, -5.1f), .013f);
            var boundary = Text(root.transform, "Boundary", string.Empty,
                new Vector3(0f, .04f, -4.55f), .010f);
            var view = root.AddComponent<LogisticsFacilityOverviewView>();
            view.Configure(building, cargo, summary, boundary, areas, new[]
            {
                LogisticsMaterial(LogisticsFacilityAreaStateCodes.Idle, "LogisticsIdle", new Color(.34f, .37f, .40f)),
                LogisticsMaterial(LogisticsFacilityAreaStateCodes.Next, "LogisticsNext", new Color(.90f, .70f, .18f)),
                LogisticsMaterial(LogisticsFacilityAreaStateCodes.Active, "LogisticsActive", new Color(.18f, .55f, .88f)),
                LogisticsMaterial(LogisticsFacilityAreaStateCodes.Completed, "LogisticsCompleted", new Color(.22f, .65f, .34f)),
            });
            var model = new LogisticsFacilityOverviewProjector().Project(new CargoWarehouseHandoffSnapshot
            {
                StableId = "cargo-handoff:transport-71.inbound-91",
                Revision = 3,
                HandoffStateCode = CargoHandoffStateCodes.ArrivedAtWarehouse,
                CargoStableId = "cargo:transport-71",
                TransportTaskStableId = "transport-task:71",
                InboundTaskStableId = "inbound-task:91",
                GeneratedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
            });
            view.Apply(model);
        }

        private static LogisticsFacilityStateMaterialBinding LogisticsMaterial(
            string state, string name, Color color)
            => new LogisticsFacilityStateMaterialBinding { StateCode = state, Material = Material(name, color) };

        private static void BuildMarket(Transform integration, WorldSelectionEvidenceView selection)
        {
            var zone = Require("WorldBootstrap/ZoneRoots/Zone_UrbanMarket").transform;
            var potato = MarketShelf(zone, 0, "market-shelf:potato", "Potato 4/12 kg",
                "Orange", 4, selection);
            var onion = MarketShelf(zone, 1, "market-shelf:onion", "Onion 10/12 kg",
                "Green", 10, selection);
            if (!potato.ValidateWiring() || !onion.ValidateWiring())
                throw new InvalidOperationException("WORLD3MarketShelfBuildInvalid");
            CreateConceptCardDeck(zone);
        }

        private static 도심마트ManagerShelfView MarketShelf(
            Transform zone, int index, string worldStableId, string quantity,
            string color, int count, WorldSelectionEvidenceView selection)
        {
            var wrapper = Require(zone, "VisualRoot/MarketShelfVisual_" + index);
            var overlay = Primitive(wrapper.transform, "ShelfStateOverlay",
                new Vector3(0f, 1.45f, 0f), new Vector3(1.15f, .18f, .65f),
                Material("MarketShelf" + color, ColorForMarket(color)));
            var socket = overlay.AddComponent<InteractionSocket>();
            socket.Configure(overlay.GetComponent<Collider>());
            var text = Text(wrapper.transform, "Quantity", quantity,
                new Vector3(0f, 1.8f, 0f), .012f);
            var presentationId = "urban-market-shelf:" + worldStableId;
            var view = wrapper.AddComponent<도심마트ManagerShelfView>();
            view.Configure(presentationId, overlay.GetComponent<Renderer>(), text,
                Array.Empty<GameObject>(), socket);
            view.Apply(new 도심마트ShelfSurfaceItem
            {
                StableId = new PresentationStableId(presentationId),
                ShelfWorldId = new WorldStableId(worldStableId),
                PresentationRevision = "world-3:market-shelf:1",
                DisplayBoxCount = count,
                QuantityText = quantity,
                VisualStateCode = color,
                ColorCode = color,
                IsHighlighted = index == 0,
            }, value => selection.Apply(value.Value));
            overlay.GetComponent<Renderer>().sharedMaterial =
                Material("MarketShelf" + color, ColorForMarket(color));
            AttachFallback(wrapper, Material("PrimitiveMarketShelf", new Color(.38f, .25f, .14f)));
            return view;
        }

        private static void CreateConceptCardDeck(Transform zone)
        {
            var root = new GameObject("ResidentialGroupConceptCardDeck");
            root.transform.SetParent(zone, false);
            root.transform.localPosition = new Vector3(0f, .2f, 4.5f);
            root.transform.localScale = Vector3.one * .38f;
            var status = Text(root.transform, "DeckStatus", string.Empty,
                new Vector3(0f, 4.3f, 0f), .045f, false);
            var skin = root.AddComponent<ConceptCardVisualSkin>();
            skin.Configure(new[]
            {
                CardMaterial(ConceptCardKindCodes.Concept, "CardConcept", new Color(.12f, .32f, .46f)),
                CardMaterial(ConceptCardKindCodes.Status, "CardStatus", new Color(.16f, .42f, .28f)),
                CardMaterial(ConceptCardKindCodes.Reason, "CardReason", new Color(.48f, .30f, .10f)),
                CardMaterial(ConceptCardKindCodes.Action, "CardAction", new Color(.35f, .20f, .48f)),
            }, Material("CardSelected", new Color(.78f, .58f, .12f)));
            var slots = Enumerable.Range(0, 7).Select(index => CardSlot(root.transform, index)).ToArray();
            var deck = root.AddComponent<ConceptCardDeckView>();
            deck.Configure(root, status, skin, slots);
            deck.Apply(도심마트ConceptCardSampleFixture.CreateDeck());
            deck.Hide();
        }

        private static ConceptCardKindMaterialBinding CardMaterial(string kind, string name, Color color)
            => new ConceptCardKindMaterialBinding { CardKindCode = kind, Material = Material(name, color) };

        private static ConceptCardView CardSlot(Transform parent, int index)
        {
            var row = index / 4;
            var column = index % 4;
            var root = Primitive(parent, "ConceptCardSlot_" + (index + 1),
                new Vector3(-4.8f + column * 3.2f + (row == 1 ? 1.6f : 0f), 2.2f - row * 3.7f, 0f),
                new Vector3(2.75f, 3.25f, .12f), Material("CardStatus", new Color(.16f, .42f, .28f)));
            var view = root.AddComponent<ConceptCardView>();
            view.Configure(root, root.GetComponent<Renderer>(), root.GetComponent<Collider>(),
                CardText(root.transform, "Kind", 1.25f, .030f),
                CardText(root.transform, "Title", .82f, .033f),
                CardText(root.transform, "Primary", .38f, .036f),
                CardText(root.transform, "Summary", -.08f, .024f),
                CardText(root.transform, "Evidence", -.55f, .021f),
                CardText(root.transform, "Caution", -.95f, .019f),
                CardText(root.transform, "Actions", -1.28f, .019f));
            return view;
        }

        private static TextMesh CardText(Transform parent, string name, float y, float size)
            => Text(parent, name, string.Empty, new Vector3(0f, y, -.7f), size, false);

        private static void BuildResidential(Transform integration)
        {
            var zone = Require("WorldBootstrap/ZoneRoots/Zone_ResidentialCommunity").transform;
            var wrapper = Require(zone, "VisualRoot/ResidentialPickupVisual");
            var overlay = Primitive(wrapper.transform, "PickupStateOverlay",
                new Vector3(0f, .75f, 0f), new Vector3(2.4f, .20f, 1.3f),
                Material("PickupArrived", new Color(.20f, .55f, .85f)));
            var badge = Primitive(wrapper.transform, "AuthorizedRoleBadge",
                new Vector3(0f, 1.05f, 0f), new Vector3(1.8f, .12f, .22f),
                Material("PickupOrdererRole", new Color(.25f, .58f, .92f)));
            var label = Text(wrapper.transform, "PickupLabel", string.Empty,
                new Vector3(0f, 1.45f, 0f), .012f);
            var point = wrapper.AddComponent<ResidentialPickupPointView>();
            point.Configure("pickup-point:residential:sample-1",
                overlay.GetComponent<Renderer>(), label, badge);
            var status = Text(zone, "PickupPerspectiveStatus", string.Empty,
                new Vector3(0f, .04f, -5.6f), .012f);
            var viewRoot = new GameObject("ResidentialPickupPresentation");
            viewRoot.transform.SetParent(integration, false);
            var view = viewRoot.AddComponent<ResidentialPickupView>();
            view.Configure(new[] { point }, status);
            var snapshot = new ResidentialPickupPerspectiveSnapshot
            {
                StableId = "residential-pickup-perspective:orderer:sample-1",
                Revision = 1,
                AuthorizedRoleCode = ResidentialPickupRoleCodes.Orderer,
                AuthorizationDecisionId = "authorization-decision:residential-pickup:sample-1",
                GeneratedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                PickupPoints = new[]
                {
                    new ResidentialPickupPointSnapshot
                    {
                        StableId = "pickup-point:residential:sample-1",
                        CanonicalTaskStableId = "pickup-task:residential:sample-1",
                        PickupPointLabel = "공동 수령지",
                        ProductLabel = "감자",
                        Quantity = 385,
                        StatusCode = ResidentialPickupStatusCodes.Arrived,
                        RoleLabel = "주문자 공개 범위",
                        CanInspect = true,
                        UpdatedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                    },
                },
            };
            if (view.Render(snapshot, new ResidentialPickupPerspectiveApplicator()).Length != 0)
                throw new InvalidOperationException("WORLD3ResidentialPickupTargetUnresolved");
            overlay.GetComponent<Renderer>().sharedMaterial =
                Material("PickupArrived", new Color(.20f, .55f, .85f));
            badge.GetComponent<Renderer>().sharedMaterial =
                Material("PickupOrdererRole", new Color(.25f, .58f, .92f));
            AttachFallback(wrapper, Material("PrimitivePickup", new Color(.63f, .50f, .28f)),
                new Vector3(2.4f, .5f, 1.4f));
        }

        private static WorldPresentationFallbackView AttachFallback(
            GameObject wrapper, Material material, Vector3? scale = null)
        {
            var visual = wrapper.GetComponent<WorldVisualInstanceView>()
                ?? throw new InvalidOperationException("WORLD3WorldVisualInstanceMissing:" + wrapper.name);
            var fallback = Primitive(wrapper.transform, "PrimitiveFallback",
                new Vector3(0f, .15f, 0f), scale ?? new Vector3(.9f, .25f, .9f), material);
            var collider = fallback.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var binding = wrapper.AddComponent<WorldPresentationFallbackView>();
            binding.Configure(visual.VisualRoot, fallback);
            return binding;
        }

        private static GameObject Require(string path)
            => GameObject.Find(path) ?? throw new InvalidOperationException("WORLD3ObjectMissing:" + path);

        private static GameObject Require(Transform parent, string path)
            => parent.Find(path)?.gameObject
               ?? throw new InvalidOperationException("WORLD3ObjectMissing:" + parent.name + "/" + path);

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
            Transform parent, string name, string value, Vector3 localPosition,
            float characterSize, bool horizontal = true)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            if (horizontal) target.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
                    ?? throw new InvalidOperationException("WORLD3CompatibleShaderMissing");
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

        private static Color ColorForMarket(string code)
            => code == "Orange" ? new Color(.95f, .45f, .12f)
                : code == "Green" ? new Color(.18f, .68f, .32f)
                : Color.gray;
    }
}
