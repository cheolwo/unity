using System;
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
    public static class 공용AnimationPreviewBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Catalogs/공용AnimationCatalog.asset";
        public const string PreviewScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/CommonAnimationPreview.unity";

        private const string MaterialRoot =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/CommonAnimationMaterials";
        private const string FarmCharacter =
            "Assets/Synty/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Male_01.prefab";
        private const string TownCharacter =
            "Assets/Synty/PolygonTown/Prefabs/Characters/SM_Chr_Father_01.prefab";
        private const string CityCharacter =
            "Assets/Synty/PolygonCity/Prefabs/Characters/Character_BusinessMan_Shirt.prefab";

        [MenuItem("Ssalddel/World Composition/Build Common Animation Fallback Preview")]
        public static void Build()
        {
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(PreviewScenePath)!.Replace('\\', '/'));
            EnsureFolder(MaterialRoot);

            var entries = new[]
            {
                Entry(월드CompositionPackCodes.Farm, "farm.actor.worker", FarmCharacter),
                Entry(월드CompositionPackCodes.Town, "town.actor.resident", TownCharacter),
                Entry(월드CompositionPackCodes.City, "city.actor.resident", CityCharacter),
            };
            var catalog = AssetDatabase.LoadAssetAtPath<공용AnimationCatalog>(CatalogPath);
            if (catalog == null)
            {
                AssetDatabase.DeleteAsset(CatalogPath);
                catalog = ScriptableObject.CreateInstance<공용AnimationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.Validate();
            BuildScene(catalog);
            Debug.Log($"CommonAnimationFallbackPreviewBuilt:{entries.Length}:"
                      + "source=procedural-fallback");
        }

        public static 공용AnimationCatalogEntry[] CreateEntriesForValidation()
            => new[]
            {
                Entry(월드CompositionPackCodes.Farm, "farm.actor.worker", FarmCharacter),
                Entry(월드CompositionPackCodes.Town, "town.actor.resident", TownCharacter),
                Entry(월드CompositionPackCodes.City, "city.actor.resident", CityCharacter),
            };

        private static 공용AnimationCatalogEntry Entry(
            string pack,
            string actorRole,
            string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                         ?? throw new InvalidOperationException(
                             "CommonAnimationCharacterMissing:" + prefabPath);
            var entry = new 공용AnimationCatalogEntry();
            entry.Configure(
                pack,
                actorRole,
                "locomotion.idle.v1",
                "locomotion.walk.v1",
                공용AnimationSourceKindCodes.ProceduralFallback,
                "humanoid.procedural-locomotion.v1",
                prefab,
                null,
                null);
            if (!entry.Validate())
                throw new InvalidOperationException("CommonAnimationEntryInvalid:" + pack);
            return entry;
        }

        private static void BuildScene(공용AnimationCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("CommonAnimationFallbackPreview");
            var laneMaterial = Material(
                MaterialRoot + "/Lane.mat",
                new Color(.36f, .39f, .4f, 1f));
            var groundMaterial = Material(
                MaterialRoot + "/Ground.mat",
                new Color(.34f, .47f, .29f, 1f));
            var colors = new[]
            {
                new Color(.83f, .62f, .28f, 1f),
                new Color(.32f, .63f, .84f, 1f),
                new Color(.72f, .43f, .75f, 1f),
            };
            var entries = catalog.Entries.ToArray();
            for (var index = 0; index < entries.Length; index++)
            {
                var x = (index - 1) * 9f;
                var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lane.name = "RouteLane_" + entries[index].PackCode;
                lane.transform.SetParent(root.transform, false);
                lane.transform.position = new Vector3(x, -.05f, 0f);
                lane.transform.localScale = new Vector3(4.5f, .1f, 15f);
                lane.GetComponent<Renderer>().sharedMaterial = laneMaterial;
                Object.DestroyImmediate(lane.GetComponent<Collider>());

                var start = Anchor(root.transform, "RouteStart_" + entries[index].PackCode,
                    new Vector3(x, 0f, -5.5f));
                var end = Anchor(root.transform, "RouteEnd_" + entries[index].PackCode,
                    new Vector3(x, 0f, 5.5f));
                CreateRouteMarker(root.transform, start.position, colors[index]);
                CreateRouteMarker(root.transform, end.position, colors[index]);

                var actor = new GameObject("Actor_" + entries[index].PackCode);
                actor.transform.SetParent(root.transform, false);
                actor.transform.position = start.position;
                var visual = PrefabUtility.InstantiatePrefab(
                    entries[index].CharacterPrefab,
                    actor.transform) as GameObject
                             ?? throw new InvalidOperationException(
                                 "CommonAnimationCharacterInstantiateFailed:" + entries[index].PackCode);
                visual.name = "VisualRoot_" + entries[index].PackCode;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                var animator = visual.GetComponentInChildren<Animator>(true)
                               ?? throw new InvalidOperationException(
                                   "CommonAnimationAnimatorMissing:" + entries[index].PackCode);
                var adapter = actor.AddComponent<공용AnimationAdapter>();
                adapter.Configure(entries[index], animator);
                var follower = actor.AddComponent<공용ActorRouteFollower>();
                follower.Configure(start, end, adapter, 1.65f + index * .15f, .8f);
                if (!adapter.ValidateWiring() || !follower.ValidateWiring())
                    throw new InvalidOperationException(
                        "CommonAnimationPreviewWiringInvalid:" + entries[index].PackCode);

                CreateLabel(root.transform, entries[index].PackCode.ToUpperInvariant()
                    + "  IDLE / WALK FALLBACK", new Vector3(x, .05f, -7.5f));
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "PreviewGround";
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = new Vector3(0f, -.35f, 0f);
            ground.transform.localScale = new Vector3(36f, .5f, 24f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .7f, .65f);
            var lightObject = new GameObject("PreviewDirectionalLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, .9f, .78f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.eulerAngles = new Vector3(48f, -30f, 0f);

            var cameraObject = new GameObject("CommonAnimationPreviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 37f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 120f;
            camera.backgroundColor = new Color(.73f, .8f, .84f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 17f, -24f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.2f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new InvalidOperationException("CommonAnimationPreviewSaveFailed");
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static void CreateRouteMarker(Transform parent, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "RouteMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.position = position + Vector3.up * .08f;
            marker.transform.localScale = new Vector3(.45f, .08f, .45f);
            marker.GetComponent<Renderer>().sharedMaterial = Material(
                MaterialRoot + "/Marker_" + ColorUtility.ToHtmlStringRGB(color) + ".mat",
                color);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static void CreateLabel(Transform parent, string text, Vector3 position)
        {
            var labelObject = new GameObject("Label_" + text);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.position = position;
            labelObject.transform.eulerAngles = new Vector3(72f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
                label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .11f;
            label.fontSize = 42;
            label.color = new Color(.07f, .06f, .05f, 1f);
        }

        private static Material Material(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("CommonAnimationPreviewShaderMissing"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
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
    }
}
