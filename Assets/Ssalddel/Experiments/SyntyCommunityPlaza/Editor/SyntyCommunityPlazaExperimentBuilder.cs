using System;
using Ssalddel.Unity.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.Experiments
{
    public static class SyntyCommunityPlazaExperimentBuilder
    {
        public const string ScenePath = "Assets/Ssalddel/Experiments/SyntyCommunityPlaza/SyntyCommunityPlazaExperiment.unity";

        private const string PrefabRoot = "Assets/Synty/PolygonStarter/Prefabs/";
        private const string MaterialRoot = "Assets/Ssalddel/Experiments/SyntyCommunityPlaza/Materials/";

        [MenuItem("Ssalddel/Experiments/Build Synty Community Plaza")]
        public static void Build()
        {
            EnsureFolder("Assets/Ssalddel/Experiments/SyntyCommunityPlaza/Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SyntyCommunityPlazaExperiment";

            var root = CreateGroup("Synty Community Logistics Plaza", null);
            var environment = CreateGroup("01 Environment", root.transform);
            var operations = CreateGroup("02 Community Operations", root.transform);
            var beacons = CreateGroup("03 Ledger Beacons", root.transform);
            var people = CreateGroup("04 Community Members", root.transform);
            var presentation = CreateGroup("05 Presentation", root.transform);

            var asphalt = CreateMaterial("Plaza Asphalt", new Color(0.12f, 0.16f, 0.2f));
            var signDark = CreateMaterial("Sign Dark", new Color(0.025f, 0.07f, 0.11f));
            var publicData = CreateMaterial("Public Data Cyan", new Color(0.08f, 0.72f, 0.88f));
            var warehouse = CreateMaterial("Warehouse Amber", new Color(1f, 0.55f, 0.08f));
            var transport = CreateMaterial("Transport Green", new Color(0.14f, 0.76f, 0.38f));

            BuildPlaza(environment.transform, asphalt);
            BuildOperations(operations.transform);
            BuildLandscape(environment.transform);
            BuildPeople(people.transform);

            CreateBeacon(beacons.transform, "Public Data Beacon", "PUBLIC DATA", new Vector3(-7f, 0.65f, -3f), publicData);
            CreateBeacon(beacons.transform, "Warehouse Ledger Beacon", "WAREHOUSE LEDGER", new Vector3(0f, 0.65f, 2f), warehouse);
            CreateBeacon(beacons.transform, "Transport Ledger Beacon", "TRANSPORT LEDGER", new Vector3(7f, 0.65f, -3f), transport);

            CreateTitleBoard(presentation.transform, signDark);
            CreateCameraAndLighting(presentation.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Failed to save experiment scene: {ScenePath}");
            }

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Ssalddel] Built Synty community logistics plaza: {ScenePath}");
        }

        private static void BuildPlaza(Transform parent, Material asphalt)
        {
            var tilePath = PrefabRoot + "SM_PolygonPrototype_Buildings_Floor_5x5_01P.prefab";
            for (var x = -2; x <= 2; x++)
            {
                for (var z = -2; z <= 2; z++)
                {
                    var tile = Instantiate(tilePath, parent, $"Plaza Tile {x + 3}-{z + 3}", new Vector3(x * 5f, 0f, z * 5f));
                    ApplyMaterial(tile, asphalt);
                }
            }

            for (var i = -4; i <= 4; i++)
            {
                Instantiate(PrefabRoot + "SM_PolygonPrototype_Prop_Cone_01.prefab", parent, $"Safety Cone {i + 5}", new Vector3(i * 2.2f, 0.35f, -7f));
            }
        }

        private static void BuildOperations(Transform parent)
        {
            Instantiate(PrefabRoot + "SM_PolygonApocalypse_Bld_House_01.prefab", parent, "Community Warehouse", new Vector3(0f, 0f, 8f), new Vector3(0f, 180f, 0f));
            Instantiate(PrefabRoot + "SM_PolygonCity_Veh_Car_Small_01.prefab", parent, "Last Mile Delivery Vehicle", new Vector3(6f, 0.1f, 5f), new Vector3(0f, 215f, 0f));

            var cratePositions = new[]
            {
                new Vector3(-5.5f, 0.5f, 6f),
                new Vector3(-4.1f, 0.5f, 6f),
                new Vector3(-4.8f, 1.5f, 6f),
                new Vector3(4.4f, 0.5f, 7.2f),
            };

            for (var i = 0; i < cratePositions.Length; i++)
            {
                Instantiate(PrefabRoot + "SM_PolygonPrototype_Prop_Crate_03.prefab", parent, $"Community Cargo Crate {i + 1}", cratePositions[i], new Vector3(0f, i * 18f, 0f));
            }
        }

        private static void BuildLandscape(Transform parent)
        {
            var treePositions = new[]
            {
                new Vector3(-13f, 0f, -10f), new Vector3(-13f, 0f, 3f),
                new Vector3(13f, 0f, -10f), new Vector3(13f, 0f, 3f),
                new Vector3(-11f, 0f, 12f), new Vector3(11f, 0f, 12f),
            };

            for (var i = 0; i < treePositions.Length; i++)
            {
                var treeNumber = i % 4 + 1;
                Instantiate(PrefabRoot + $"SM_Generic_Tree_0{treeNumber}.prefab", parent, $"Plaza Tree {i + 1}", treePositions[i], new Vector3(0f, i * 47f, 0f));
            }

            Instantiate(PrefabRoot + "SM_Generic_Small_Rocks_02.prefab", parent, "Landscape Rocks West", new Vector3(-11f, 0f, 8f));
            Instantiate(PrefabRoot + "SM_Generic_Small_Rocks_04.prefab", parent, "Landscape Rocks East", new Vector3(11f, 0f, 8f), new Vector3(0f, 90f, 0f));
        }

        private static void BuildPeople(Transform parent)
        {
            Instantiate(PrefabRoot + "Characters/SM_Chr_Female_01.prefab", parent, "Community Coordinator", new Vector3(-3f, 0f, 1f), new Vector3(0f, 35f, 0f));
            Instantiate(PrefabRoot + "Characters/SM_Chr_Male_01.prefab", parent, "Warehouse Manager", new Vector3(2.5f, 0f, 5.5f), new Vector3(0f, 210f, 0f));
            Instantiate(PrefabRoot + "Characters/SM_Bean_Town_Female_01.prefab", parent, "Community Resident", new Vector3(-7f, 0f, -0.2f), new Vector3(0f, 150f, 0f));
            Instantiate(PrefabRoot + "Characters/SM_Bean_Cop_01.prefab", parent, "Safety Coordinator", new Vector3(7.5f, 0f, -0.5f), new Vector3(0f, 225f, 0f));
        }

        private static void CreateBeacon(Transform parent, string name, string label, Vector3 position, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<SyntyPlazaBeacon>();

            var column = Instantiate(PrefabRoot + "SM_PolygonPrototype_Primitive_Cylander_01P.prefab", root.transform, "Beacon Column", Vector3.zero);
            column.transform.localScale = new Vector3(0.8f, 1.3f, 0.8f);
            ApplyMaterial(column, material);

            var icon = Instantiate(PrefabRoot + "SM_PolygonPrototype_Primitive_Sphere_01P.prefab", root.transform, "Beacon Signal", new Vector3(0f, 2.15f, 0f));
            icon.transform.localScale = Vector3.one * 0.62f;
            ApplyMaterial(icon, material);

            CreateWorldText(root.transform, label, new Vector3(0f, 3.25f, 0f), 0.09f, Color.white);
        }

        private static void CreateTitleBoard(Transform parent, Material material)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Experiment Title Board";
            board.transform.SetParent(parent, false);
            board.transform.position = new Vector3(0f, 5.5f, 12.3f);
            board.transform.localScale = new Vector3(12f, 3.2f, 0.35f);
            ApplyMaterial(board, material);

            CreateWorldText(parent, "SSALDDEL\nCOMMUNITY LOGISTICS LAB", new Vector3(0f, 5.5f, 12.05f), 0.13f, new Color(0.75f, 0.95f, 1f));
        }

        private static void CreateCameraAndLighting(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.67f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.34f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.16f);

            var sun = new GameObject("Warm Directional Light");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.72f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = new Vector3(20f, 13f, -20f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.62f, 0.83f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SyntyPlazaOrbitCamera>();
            cameraObject.transform.LookAt(new Vector3(0f, 2.5f, 0f));
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            if (parent != null)
            {
                group.transform.SetParent(parent, false);
            }

            return group;
        }

        private static GameObject Instantiate(string path, Transform parent, string name, Vector3 position, Vector3? euler = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Required Synty prefab was not found: {path}");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate Synty prefab: {path}");
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(euler ?? Vector3.zero);
            return instance;
        }

        private static TextMesh CreateWorldText(Transform parent, string text, Vector3 localPosition, float size, Color color)
        {
            var textObject = new GameObject($"Label - {text.Replace('\n', ' ')}");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 72;
            textMesh.characterSize = size;
            textMesh.color = color;
            textObject.AddComponent<SyntyPlazaBillboard>();
            return textMesh;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = MaterialRoot + name.Replace(' ', '_') + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
