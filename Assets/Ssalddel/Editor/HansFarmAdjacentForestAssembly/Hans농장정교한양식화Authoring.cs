using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.HansFarmAdjacentForestAssembly
{
    public static class Hans농장정교한양식화Authoring
    {
        public const string CandidateRootName = "HansFarmAdjacentForest_r5_NaturalWorldCandidate";
        public const string RefinementRootName = "HansFarmRefinedStylized_r1";
        public const string GeneratedRoot = "Assets/Ssalddel/Presentation/World/Generated/HansFarmRefinedStylized";
        public const string MaterialRoot = GeneratedRoot + "/Materials";
        public const string HouseAtlasMaterialPath = MaterialRoot + "/HansHouseAtlasWeathered_r1.mat";
        public const string RoofMaterialPath = MaterialRoot + "/HansHouseRoofWeathered_r1.mat";
        public const string ExposedWoodMaterialPath = MaterialRoot + "/HansHouseExposedWood_r1.mat";
        public const string FencePostMaterialPath = MaterialRoot + "/HansFencePostWeathered_r1.mat";
        public const string RouteMaterialPath = MaterialRoot + "/HansRouteDirt_r1.mat";
        public const string RouteEdgeMaterialPath = MaterialRoot + "/HansRouteEdgeBlend_r1.mat";
        public const string SourceFarmMaterialPath = "Assets/Synty/PolygonFarm/Materials/PolygonFarm_01_A.mat";
        public const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [MenuItem("Ssalddel/한스 농장/정교한 양식화 r1 적용")]
        public static void ApplyToCanonicalScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("HansRefinedStylizedRequiresStoppedEditor");

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("HansRefinedStylizedRequiresCanonicalScene");
            if (scene.isDirty)
                throw new InvalidOperationException("HansRefinedStylizedRequiresCleanScene");

            EnsureMaterials();
            var candidate = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(x => x.name == CandidateRootName)
                ?? throw new InvalidOperationException("HansRefinedStylizedCandidateMissing");

            var old = candidate.Find(RefinementRootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            RestoreAtlasAndDamageMaterials(candidate);
            RefineGrassPresentation(candidate);
            RefineRouteMaterials(candidate);

            var overlay = new GameObject(RefinementRootName).transform;
            overlay.SetParent(candidate, false);
            BuildHouseCraftDetails(candidate, overlay);
            BuildFencePosts(candidate, overlay);
            BuildRouteEdgeBlends(candidate, overlay);
            BuildFarmhousePracticalLight(candidate, overlay);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("HansFarmRefinedStylizedApplied:save-not-yet-called");
        }

        public static bool IsFarmBoundaryName(string value) =>
            value != null && value.StartsWith("boundary:farm:", StringComparison.Ordinal);

        public static bool IsFarmEdgeGrassName(string value) =>
            value != null && value.StartsWith("vegetation:farm-edge-grass:", StringComparison.Ordinal);

        public static Vector3 RefinedGrassRendererScale => new(0.62f, 0.62f, 0.62f);

        private static void EnsureMaterials()
        {
            Directory.CreateDirectory(Path.Combine(UnityEngine.Application.dataPath, "Ssalddel/Presentation/World/Generated/HansFarmRefinedStylized/Materials"));
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceFarmMaterialPath)
                ?? throw new InvalidOperationException("HansRefinedStylizedFarmAtlasMaterialMissing");

            SaveOrReplaceClone(HouseAtlasMaterialPath, source, "HansHouseAtlasWeathered_r1", new Color(1.35f, 1.15f, 0.95f, 1f), 0.34f);
            SaveOrReplaceClone(RoofMaterialPath, source, "HansHouseRoofWeathered_r1", new Color(0.40f, 0.33f, 0.25f, 1f), 0.50f);
            SaveOrReplaceClone(ExposedWoodMaterialPath, source, "HansHouseExposedWood_r1", new Color(0.70f, 0.48f, 0.27f, 1f), 0.43f);
            SaveOrReplaceClone(FencePostMaterialPath, source, "HansFencePostWeathered_r1", new Color(0.74f, 0.56f, 0.34f, 1f), 0.48f);

            var lit = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("HansRefinedStylizedUrpLitMissing");
            SaveOrReplaceLit(RouteMaterialPath, lit, "HansRouteDirt_r1", new Color(0.28f, 0.16f, 0.075f, 1f), 0.92f);
            SaveOrReplaceLit(RouteEdgeMaterialPath, lit, "HansRouteEdgeBlend_r1", new Color(0.18f, 0.26f, 0.105f, 1f), 0.96f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SaveOrReplaceClone(string path, Material source, string name, Color tint, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(source);
            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        private static void SaveOrReplaceLit(string path, Shader shader, string name, Color color, float roughness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 1f - roughness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
        }

        private static void RestoreAtlasAndDamageMaterials(Transform candidate)
        {
            var house = candidate.Find("placement-instance:forest-edge-farm:hans-residential-home")
                ?? throw new InvalidOperationException("HansRefinedStylizedHouseMissing");
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(HouseAtlasMaterialPath);
            var roof = AssetDatabase.LoadAssetAtPath<Material>(RoofMaterialPath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(ExposedWoodMaterialPath);
            foreach (var renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "VARIANT_SM_Bld_Farmhouse_02") renderer.sharedMaterial = atlas;
                else if (renderer.name.StartsWith("Damage_RoofCavity", StringComparison.Ordinal)) renderer.sharedMaterial = roof;
                else if (renderer.name.StartsWith("Damage_LooseRoofBoard", StringComparison.Ordinal)) renderer.enabled = false;
            }
        }

        private static void RefineGrassPresentation(Transform candidate)
        {
            foreach (Transform child in candidate)
            {
                if (!IsFarmEdgeGrassName(child.name)) continue;
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                    renderer.transform.localScale = RefinedGrassRendererScale;
            }
        }

        private static void RefineRouteMaterials(Transform candidate)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RouteMaterialPath);
            foreach (Transform child in candidate)
                if (child.name.StartsWith("RouteSurface_", StringComparison.Ordinal))
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }

        private static void BuildHouseCraftDetails(Transform candidate, Transform overlay)
        {
            var house = candidate.Find("placement-instance:forest-edge-farm:hans-residential-home");
            var group = new GameObject("HouseCraftDetails_r1").transform;
            group.SetParent(house, false);
            var roof = AssetDatabase.LoadAssetAtPath<Material>(RoofMaterialPath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(ExposedWoodMaterialPath);

            AddBox(group, "RoofFascia_Front", new Vector3(0.95f, 4.05f, 4.48f), new Vector3(6.10f, 0.16f, 0.16f), roof);
            AddBox(group, "RoofFascia_Back", new Vector3(0.95f, 4.05f, -4.48f), new Vector3(6.10f, 0.16f, 0.16f), roof);
            group.SetParent(overlay, true);
        }

        private static void BuildFencePosts(Transform candidate, Transform overlay)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FencePostMaterialPath);
            var group = new GameObject("FencePostDetails_r1").transform;
            group.SetParent(overlay, false);
            foreach (Transform child in candidate)
            {
                if (!IsFarmBoundaryName(child.name)) continue;
                var post = AddBox(group, "Post_" + child.name.Replace(':', '_'), child.localPosition + new Vector3(0f, 0.56f, 0f), new Vector3(0.18f, 1.46f, 0.18f), material);
                post.localRotation = Quaternion.Euler(0f, child.localEulerAngles.y, StableTilt(child.name));
            }
        }

        private static void BuildRouteEdgeBlends(Transform candidate, Transform overlay)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RouteEdgeMaterialPath);
            var group = new GameObject("RouteEdgeBlends_r1").transform;
            group.SetParent(overlay, false);
            foreach (Transform route in candidate)
            {
                if (!route.name.StartsWith("RouteSurface_", StringComparison.Ordinal)) continue;
                var source = route.GetComponent<MeshFilter>();
                if (source == null || source.sharedMesh == null) continue;
                var edge = new GameObject("EdgeBlend_" + route.name);
                edge.transform.SetParent(group, false);
                edge.transform.localPosition = route.localPosition + new Vector3(0f, -0.018f, 0f);
                edge.transform.localRotation = route.localRotation;
                edge.transform.localScale = Vector3.Scale(route.localScale, new Vector3(1.08f, 1f, 1.08f));
                edge.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                edge.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static void BuildFarmhousePracticalLight(Transform candidate, Transform overlay)
        {
            var house = candidate.Find("placement-instance:forest-edge-farm:hans-residential-home");
            var lamp = new GameObject("FarmhousePorchPracticalLight_r1");
            lamp.transform.SetParent(overlay, false);
            lamp.transform.position = house.TransformPoint(new Vector3(0.9f, 2.55f, 4.72f));
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.69f, 0.38f, 1f);
            light.intensity = 60f;
            light.range = 8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.62f;
        }

        private static Transform AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Vector3? localEuler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static float StableTilt(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var c in value) hash = hash * 31 + c;
                return (hash % 7) - 3f;
            }
        }
    }
}
