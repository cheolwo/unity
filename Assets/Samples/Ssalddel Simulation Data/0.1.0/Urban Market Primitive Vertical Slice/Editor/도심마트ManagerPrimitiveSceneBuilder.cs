using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Samples.UrbanMarket;
using Ssalddel.Unity.PresentationContracts.LearningCards;
using Ssalddel.Unity.UrbanMarket;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Samples.UrbanMarket.Editor
{
    public static class 도심마트ManagerPrimitiveSceneBuilder
    {
        private const string SceneDirectory = "Assets/Ssalddel/Scenes";
        private const string ScenePath = SceneDirectory + "/UrbanMarketManagerPrimitive.unity";
        private const string GeneratedDirectory = "Assets/Ssalddel/Generated";
        private const string AnimatorPath = GeneratedDirectory + "/UrbanMarketRepresentativePrimitive.controller";

        [MenuItem("Ssalddel/Samples/Create Urban Market Manager Primitive Scene")]
        public static void CreateScene()
        {
            if (!CanReplaceCurrentScene()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("UrbanMarketManagerZone");
            Primitive("Ground", root.transform, new Vector3(0f, -0.25f, 1f), new Vector3(18f, 0.5f, 13f), new Color(0.72f, 0.75f, 0.68f));

            var status = Panel(root.transform, "RuntimeStatus", "Loading...", new Vector3(0f, 5.4f, 4.8f), new Vector3(9f, 0.9f, 0.25f));
            var tasks = Panel(root.transform, "TaskMarkers", string.Empty, new Vector3(-5.5f, 2.2f, 3.8f), new Vector3(5.5f, 4.2f, 0.25f));
            var sourcePlans = Panel(root.transform, "SourcePlans", string.Empty, new Vector3(5.5f, 2.2f, 3.8f), new Vector3(5.5f, 4.2f, 0.25f));
            var details = Panel(root.transform, "Details", string.Empty, new Vector3(0f, 0.8f, -4.3f), new Vector3(11f, 2.3f, 0.25f));
            var shelves = new[]
            {
                Shelf(root.transform, "PotatoManagerShelf", "urban-market-shelf:market-shelf:potato", new Vector3(-2.5f, 1f, -0.5f)),
                Shelf(root.transform, "OnionManagerShelf", "urban-market-shelf:market-shelf:onion", new Vector3(2.5f, 1f, -0.5f)),
            };

            CreateAssetReadinessObjects(root, true);

            var view = root.AddComponent<도심마트ManagerSurfaceView>();
            view.Configure(status, tasks, sourcePlans, details, shelves);
            root.AddComponent<도심마트ManagerSceneController>();
            var lifetimeScope = root.AddComponent<도심마트LifetimeScope>();
            lifetimeScope.ConfigureManagerSimulation();

            CreateCamera();
            CreateLight();
            Directory.CreateDirectory(SceneDirectory);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Created urban market manager primitive scene: " + ScenePath);
        }

        public static void ValidateGeneratedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Urban market manager scene was not generated.", ScenePath);
            if (!CanReplaceCurrentScene()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = UnityEngine.Object.FindFirstObjectByType<도심마트ManagerSurfaceView>();
            var controller = UnityEngine.Object.FindFirstObjectByType<도심마트ManagerSceneController>();
            var scope = UnityEngine.Object.FindFirstObjectByType<도심마트LifetimeScope>();
            var cards = UnityEngine.Object.FindFirstObjectByType<ConceptCardDeckView>();
            var representative = UnityEngine.Object.FindFirstObjectByType<공동주택대표NpcView>();
            var conceptController = UnityEngine.Object.FindFirstObjectByType<도심마트ConceptCardSceneController>();
            var navMeshSurface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (view == null || controller == null || scope == null || !view.ValidateWiring())
                throw new MissingReferenceException("Urban market manager View, Controller, or LifetimeScope wiring is invalid.");
            if (cards == null || representative == null || conceptController == null
                || navMeshSurface == null || navMeshSurface.navMeshData == null
                || !cards.ValidateWiring() || !representative.ValidateWiring()
                || !conceptController.ValidateWiring())
                throw new MissingReferenceException("Urban market Concept Card or representative wiring is invalid.");
            if (UnityEngine.Object.FindObjectsByType<도심마트ManagerShelfView>(FindObjectsSortMode.None).Length != 2)
                throw new InvalidOperationException("Urban market manager fixture requires two shelf surfaces.");
            if (cards.SlotCount != 7)
                throw new InvalidOperationException("Urban market representative fixture requires seven card slots.");
            Debug.Log("Validated urban market manager scene reload and surface wiring.");
        }

        public static GameObject CreateAssetReadinessObjectsForTests()
        {
            var root = new GameObject("UrbanMarketAssetReadinessTestRoot");
            Primitive("Ground", root.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(18f, 0.5f, 13f), new Color(0.72f, 0.75f, 0.68f));
            CreateAssetReadinessObjects(root, false);
            return root;
        }

        private static void CreateAssetReadinessObjects(GameObject root, bool persistAnimator)
        {
            var entrance = Waypoint(root.transform, "MarketEntrance", "market.entrance",
                new Vector3(-7f, 0f, -3.5f));
            var managerDesk = Waypoint(root.transform, "ManagerDesk", "market.manager-desk",
                new Vector3(5.5f, 0f, -1.5f));
            var exit = Waypoint(root.transform, "MarketExit", "market.exit",
                new Vector3(-7f, 0f, 3.5f));
            Primitive("ManagerDeskVisualRoot", root.transform, new Vector3(5.5f, 0.75f, -1.5f),
                new Vector3(3.2f, 1.5f, 1.4f), new Color(0.25f, 0.18f, 0.12f));

            var representativeRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            representativeRoot.name = "ResidentialGroupRepresentative";
            representativeRoot.transform.SetParent(root.transform, false);
            representativeRoot.transform.position = new Vector3(-7f, 1f, -3.5f);
            var agent = representativeRoot.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 2.2f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.25f;
            var animator = representativeRoot.AddComponent<Animator>();
            animator.runtimeAnimatorController = PrimitiveAnimatorController(persistAnimator);
            var dialogue = Text("RepresentativeDialogue", representativeRoot.transform, string.Empty,
                new Vector3(0f, 2.2f, -0.6f), 0.025f);
            var representative = representativeRoot.AddComponent<공동주택대표NpcView>();
            representative.Configure(
                "npc:sim:residential-group-representative:1",
                agent,
                animator,
                dialogue,
                representativeRoot,
                representativeRoot.GetComponent<Collider>(),
                new[]
                {
                    Binding("market.entrance", entrance),
                    Binding("market.manager-desk", managerDesk),
                    Binding("market.exit", exit),
                },
                new[]
                {
                    new 공동주택대표ActionBinding
                    {
                        ActionCode = ResidentialGroupRepresentativeArrivalActionCodes.WaitForManagerReview,
                        AnimatorTrigger = "WaitForManagerReview",
                    },
                });

            var deckRoot = new GameObject("ResidentialGroupConceptCardDeck");
            deckRoot.transform.SetParent(root.transform, false);
            deckRoot.transform.position = new Vector3(0f, 0f, 2.5f);
            var status = Text("DeckStatus", deckRoot.transform, string.Empty,
                new Vector3(0f, 5.2f, -0.4f), 0.018f);
            var skin = CreateCardSkin(deckRoot);
            var slots = Enumerable.Range(0, 7)
                .Select(index => CardSlot(deckRoot.transform, index))
                .ToArray();
            var deck = deckRoot.AddComponent<ConceptCardDeckView>();
            deck.Configure(deckRoot, status, skin, slots);
            deck.Apply(도심마트ConceptCardSampleFixture.CreateDeck());

            var conceptController = root.AddComponent<도심마트ConceptCardSceneController>();
            conceptController.Configure(representative, deck);

            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private static Transform Waypoint(
            Transform parent, string name, string key, Vector3 position)
        {
            var waypoint = new GameObject(name);
            waypoint.transform.SetParent(parent, false);
            waypoint.transform.position = position;
            waypoint.name = name + " [" + key + "]";
            return waypoint.transform;
        }

        private static 공동주택대표WaypointBinding Binding(string key, Transform target)
            => new 공동주택대표WaypointBinding { WaypointKey = key, Target = target };

        private static ConceptCardVisualSkin CreateCardSkin(GameObject parent)
        {
            var skin = parent.AddComponent<ConceptCardVisualSkin>();
            skin.Configure(
                new[]
                {
                    CardMaterial(ConceptCardKindCodes.Concept, new Color(0.12f, 0.32f, 0.46f)),
                    CardMaterial(ConceptCardKindCodes.Status, new Color(0.16f, 0.42f, 0.28f)),
                    CardMaterial(ConceptCardKindCodes.Reason, new Color(0.48f, 0.30f, 0.10f)),
                    CardMaterial(ConceptCardKindCodes.Action, new Color(0.35f, 0.20f, 0.48f)),
                },
                Material("ConceptCardSelected", new Color(0.78f, 0.58f, 0.12f)));
            return skin;
        }

        private static ConceptCardKindMaterialBinding CardMaterial(string kind, Color color)
            => new ConceptCardKindMaterialBinding
            {
                CardKindCode = kind,
                Material = Material("ConceptCard" + kind, color),
            };

        private static ConceptCardView CardSlot(Transform parent, int index)
        {
            var row = index / 4;
            var column = index % 4;
            var x = -5.25f + column * 3.5f + (row == 1 ? 1.75f : 0f);
            var y = 3.6f - row * 4.1f;
            var root = Primitive("ConceptCardSlot_" + (index + 1), parent,
                new Vector3(x, y, 0f), new Vector3(3.15f, 3.7f, 0.18f), Color.gray, true);
            var view = root.AddComponent<ConceptCardView>();
            view.Configure(
                root,
                root.GetComponent<Renderer>(),
                root.GetComponent<Collider>(),
                Text("Kind", root.transform, string.Empty, new Vector3(0f, 1.45f, -0.65f), 0.012f),
                Text("Title", root.transform, string.Empty, new Vector3(0f, 1.0f, -0.65f), 0.015f),
                Text("Primary", root.transform, string.Empty, new Vector3(0f, 0.55f, -0.65f), 0.016f),
                Text("Summary", root.transform, string.Empty, new Vector3(0f, 0.05f, -0.65f), 0.009f),
                Text("Evidence", root.transform, string.Empty, new Vector3(0f, -0.55f, -0.65f), 0.008f),
                Text("Caution", root.transform, string.Empty, new Vector3(0f, -1.05f, -0.65f), 0.0075f),
                Text("Actions", root.transform, string.Empty, new Vector3(0f, -1.45f, -0.65f), 0.008f));
            return view;
        }

        private static RuntimeAnimatorController PrimitiveAnimatorController(bool persist)
        {
            if (persist)
            {
                Directory.CreateDirectory(GeneratedDirectory);
                var stored = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
                if (stored != null) return stored;
                var created = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
                ConfigureAnimator(created);
                return created;
            }

            var transient = new AnimatorController
            {
                name = "UrbanMarketRepresentativePrimitiveController",
            };
            transient.AddLayer("Base Layer");
            ConfigureAnimator(transient);
            return transient;
        }

        private static void ConfigureAnimator(AnimatorController controller)
        {
            if (!controller.parameters.Any(value => value.name == "Speed"))
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!controller.parameters.Any(value => value.name == "WaitForManagerReview"))
                controller.AddParameter("WaitForManagerReview", AnimatorControllerParameterType.Trigger);
            var layer = controller.layers.First();
            if (layer.stateMachine.states.Length == 0)
            {
                var idle = layer.stateMachine.AddState("Idle");
                idle.motion = new AnimationClip { name = "PrimitiveIdle" };
                layer.stateMachine.defaultState = idle;
            }
        }

        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { name = name, color = color };
        }

        private static 도심마트ManagerShelfView Shelf(Transform parent, string name, string stableId, Vector3 position)
        {
            var root = Primitive(name, parent, position, new Vector3(3.8f, 1.5f, 2.2f), new Color(0.35f, 0.24f, 0.16f));
            var socket = root.AddComponent<InteractionSocket>();
            socket.Configure(root.GetComponent<Collider>());
            var quantity = Text("Quantity", root.transform, string.Empty, new Vector3(0f, 1.2f, -1.2f), 0.026f);
            var boxes = Enumerable.Range(0, 12)
                .Select(index => Primitive(
                    "DisplayBox_" + (index + 1),
                    root.transform,
                    new Vector3(-1.2f + (index % 4) * 0.8f, 0.9f + (index / 4) * 0.55f, 0f),
                    new Vector3(0.55f, 0.45f, 0.55f),
                    new Color(0.72f, 0.58f, 0.32f),
                    true))
                .ToArray();
            var view = root.AddComponent<도심마트ManagerShelfView>();
            view.Configure(stableId, root.GetComponent<Renderer>(), quantity, boxes, socket);
            return view;
        }

        private static TextMesh Panel(Transform parent, string name, string value, Vector3 position, Vector3 scale)
        {
            var panel = Primitive(name, parent, position, scale, new Color(0.12f, 0.16f, 0.2f));
            return Text("Text", panel.transform, value, new Vector3(0f, 0f, -0.65f), 0.018f);
        }

        private static GameObject Primitive(
            string name, Transform parent, Vector3 position, Vector3 scale, Color color, bool local = false)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            if (local) value.transform.localPosition = position; else value.transform.position = position;
            value.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = name + "Material", color = color };
            return value;
        }

        private static TextMesh Text(
            string name, Transform parent, string value, Vector3 position, float characterSize)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            var text = target.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = Color.white;
            return text;
        }

        private static void CreateCamera()
        {
            var target = new GameObject("Main Camera");
            target.tag = "MainCamera";
            var camera = target.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            target.transform.position = new Vector3(0f, 12f, -16f);
            target.transform.LookAt(new Vector3(0f, 1.8f, 1f));
        }

        private static void CreateLight()
        {
            var target = new GameObject("Directional Light");
            var light = target.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            target.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static bool CanReplaceCurrentScene()
        {
            if (!UnityEngine.Application.isBatchMode) return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty) throw new InvalidOperationException("Batch mode refuses to replace a modified scene: " + scene.name);
            }
            return true;
        }
    }
}
