using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ssalddel.Unity.UrbanMarket;

namespace Ssalddel.Unity.Samples.UrbanMarket.Editor
{
    public static class 도심마트PrimitiveSceneBuilder
    {
        private const string SceneDirectory = "Assets/Ssalddel/Scenes";
        private const string ScenePath = SceneDirectory + "/UrbanMarketPrimitive.unity";

        [MenuItem("Ssalddel/Samples/Create Urban Market Primitive Scene")]
        public static void CreateScene()
        {
            if (!CanReplaceCurrentScene())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("UrbanMarketZone");

            CreateGround(root.transform);
            var building = CreateBuilding(root.transform);
            var entrance = CreateEntrance(root.transform);
            var kiosk = CreateKiosk(root.transform);
            var shelves = new[]
            {
                CreateShelf(root.transform, "Shelf_Potato", new Vector3(-4f, 0.8f, 1f)),
                CreateShelf(root.transform, "Shelf_Rice", new Vector3(0f, 0.8f, 1f)),
                CreateShelf(root.transform, "Shelf_Onion", new Vector3(4f, 0.8f, 1f)),
            };
            var detailPanel = CreateDetailPanel(root.transform);

            var marketView = root.AddComponent<도심마트View>();
            marketView.Configure(
                building,
                shelves,
                kiosk,
                detailPanel.Root,
                detailPanel.Text,
                entrance);

            var controller = root.AddComponent<도심마트SceneController>();
            root.AddComponent<도심마트LifetimeScope>();

            CreateCamera();
            CreateLight();

            Directory.CreateDirectory(SceneDirectory);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Created urban market primitive scene: " + ScenePath);
        }

        public static void ValidateGeneratedScene()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("Urban market scene was not generated.", ScenePath);
            }

            if (!CanReplaceCurrentScene())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var marketView = Object.FindFirstObjectByType<도심마트View>();
            var controller = Object.FindFirstObjectByType<도심마트SceneController>();
            var lifetimeScope = Object.FindFirstObjectByType<도심마트LifetimeScope>();
            if (marketView == null || controller == null || lifetimeScope == null)
            {
                throw new MissingReferenceException("Urban market View, SceneController, or LifetimeScope is missing.");
            }

            if (!marketView.ValidateWiring())
            {
                throw new MissingReferenceException("Urban market View wiring is invalid after scene reload.");
            }

            var fixture = new Simulated도심마트조회UseCase().조회Async().GetAwaiter().GetResult();
            var errors = new 도심마트ScreenModelValidator().Validate(fixture);
            if (fixture.상품목록.Length != 3 || errors.Length > 0)
            {
                throw new System.InvalidOperationException(
                    "Urban market fixture contract is invalid: " + string.Join(", ", errors));
            }

            Debug.Log("Validated urban market scene reload, wiring, and three-product fixture.");
        }

        private static bool CanReplaceCurrentScene()
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new System.InvalidOperationException(
                        "Batch mode refuses to replace a modified scene: " + scene.name);
                }
            }

            return true;
        }

        private static void CreateGround(Transform parent)
        {
            var ground = Primitive(
                "Ground",
                parent,
                new Vector3(0f, -0.25f, 1f),
                new Vector3(18f, 0.5f, 13f),
                new Color(0.72f, 0.75f, 0.68f));
            ground.isStatic = true;
        }

        private static GameObject CreateBuilding(Transform parent)
        {
            var building = new GameObject("BuildingVisualRoot");
            building.transform.SetParent(parent, false);
            Primitive(
                "BackWall",
                building.transform,
                new Vector3(0f, 2.5f, 5.5f),
                new Vector3(16f, 5f, 0.5f),
                new Color(0.85f, 0.82f, 0.72f));
            Primitive(
                "RoofSign",
                building.transform,
                new Vector3(0f, 5.1f, 4.9f),
                new Vector3(8f, 1f, 0.4f),
                new Color(0.16f, 0.48f, 0.31f));
            CreateText(
                "MarketSignText",
                building.transform,
                "SSALDDEL URBAN MARKET",
                new Vector3(0f, 5.1f, 4.65f),
                0.07f,
                Color.white);
            return building;
        }

        private static InteractionSocket CreateEntrance(Transform parent)
        {
            var entrance = Primitive(
                "EntranceInteractionSocket",
                parent,
                new Vector3(0f, 1.25f, 5.15f),
                new Vector3(3f, 2.5f, 0.35f),
                new Color(0.24f, 0.35f, 0.42f));
            var socket = entrance.AddComponent<InteractionSocket>();
            socket.Configure(entrance.GetComponent<Collider>());
            return socket;
        }

        private static 정보키오스크View CreateKiosk(Transform parent)
        {
            var root = Primitive(
                "InformationKiosk",
                parent,
                new Vector3(-6.6f, 1.25f, -2.5f),
                new Vector3(2.4f, 2.5f, 0.6f),
                new Color(0.15f, 0.34f, 0.44f));
            var title = CreateText(
                "Title",
                root.transform,
                "살뜰 도심 마트",
                new Vector3(0f, 0.45f, -0.36f),
                0.04f,
                Color.white,
                true);
            var status = CreateText(
                "Status",
                root.transform,
                "Loading...",
                new Vector3(0f, -0.15f, -0.36f),
                0.025f,
                Color.white,
                true);
            var view = root.AddComponent<정보키오스크View>();
            view.Configure(title, status);
            return view;
        }

        private static 상품진열대View CreateShelf(
            Transform parent,
            string name,
            Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            var baseObject = Primitive(
                "ShelfBase",
                root.transform,
                Vector3.zero,
                new Vector3(3.2f, 1.4f, 2f),
                new Color(0.35f, 0.24f, 0.16f),
                true);
            var socket = baseObject.AddComponent<InteractionSocket>();
            socket.Configure(baseObject.GetComponent<Collider>());

            var boxes = new 상품상자View[3];
            for (var index = 0; index < boxes.Length; index++)
            {
                var box = Primitive(
                    "ProductBox_" + (index + 1),
                    root.transform,
                    new Vector3(-0.85f + index * 0.85f, 1f, 0f),
                    new Vector3(0.7f, 0.7f, 0.7f),
                    Color.white,
                    true);
                var label = CreateText(
                    "Label",
                    box.transform,
                    "상품",
                    new Vector3(0f, 0f, -0.52f),
                    0.025f,
                    Color.black,
                    true);
                boxes[index] = box.AddComponent<상품상자View>();
                boxes[index].Configure(box.GetComponent<Renderer>(), label);
            }

            var priceBoard = Primitive(
                "PriceBoard",
                root.transform,
                new Vector3(0f, 2.1f, -0.2f),
                new Vector3(3.1f, 1.2f, 0.2f),
                new Color(0.94f, 0.93f, 0.82f),
                true);
            var productText = CreateText(
                "ProductText",
                priceBoard.transform,
                "상품",
                new Vector3(0f, 0.28f, -0.18f),
                0.035f,
                Color.black,
                true);
            var priceText = CreateText(
                "PriceText",
                priceBoard.transform,
                "0 KRW",
                new Vector3(0f, 0f, -0.18f),
                0.03f,
                Color.black,
                true);
            var sourceText = CreateText(
                "SourceText",
                priceBoard.transform,
                "source",
                new Vector3(0f, -0.34f, -0.18f),
                0.012f,
                Color.gray,
                true);
            var priceTag = priceBoard.AddComponent<가격표View>();
            priceTag.Configure(productText, priceText, sourceText);

            var indicator = Primitive(
                "StockIndicator",
                root.transform,
                new Vector3(1.15f, 1.05f, -1.05f),
                new Vector3(0.25f, 0.25f, 0.25f),
                Color.gray,
                true);
            var stockText = CreateText(
                "StockText",
                root.transform,
                "재고",
                new Vector3(0f, 1.05f, -1.1f),
                0.03f,
                Color.black,
                true);
            var stockStatus = indicator.AddComponent<재고상태View>();
            stockStatus.Configure(indicator.GetComponent<Renderer>(), stockText);

            var shelfView = root.AddComponent<상품진열대View>();
            shelfView.Configure(priceTag, stockStatus, boxes, socket);
            return shelfView;
        }

        private static DetailPanel CreateDetailPanel(Transform parent)
        {
            var root = Primitive(
                "ProductDetailPanel",
                parent,
                new Vector3(6.6f, 2f, -2.5f),
                new Vector3(3.5f, 3.6f, 0.4f),
                new Color(0.12f, 0.16f, 0.2f));
            var text = CreateText(
                "DetailText",
                root.transform,
                string.Empty,
                new Vector3(0f, 0f, -0.3f),
                0.03f,
                Color.white,
                true);
            root.SetActive(false);
            return new DetailPanel(root, text);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            cameraObject.transform.position = new Vector3(0f, 13f, -15f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 1.5f));
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static GameObject Primitive(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool local = false)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            if (local)
            {
                value.transform.localPosition = position;
            }
            else
            {
                value.transform.position = position;
            }

            value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name + "Material", color);
            return value;
        }

        private static TextMesh CreateText(
            string name,
            Transform parent,
            string value,
            Vector3 position,
            float characterSize,
            Color color,
            bool local = false)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            if (local)
            {
                textObject.transform.localPosition = position;
            }
            else
            {
                textObject.transform.position = position;
            }

            var text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
            return text;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                color = color,
            };
            return material;
        }

        private readonly struct DetailPanel
        {
            public DetailPanel(GameObject root, TextMesh text)
            {
                Root = root;
                Text = text;
            }

            public GameObject Root { get; }

            public TextMesh Text { get; }
        }
    }
}
