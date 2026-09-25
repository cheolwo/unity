using System;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 세계지구본Builder
    {
        public const string GlobeRootName = "GlobeRoot";

        [MenuItem("Ssalddel/통합 월드/세계 지구본/Build Read Only Globe")]
        public static void BuildExistingScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != 통합WorldScenePolicy.CanonicalScenePath)
                scene = EditorSceneManager.OpenScene(
                    통합WorldScenePolicy.CanonicalScenePath,
                    OpenSceneMode.Single);
            var shell = GameObject.Find(SimulationWorldShellBuilder.RootName)
                ?? throw new InvalidOperationException("SimulationWorldShellMissing");
            var worldMapRoot = shell.transform.Find("WorldMapRoot")
                ?? throw new InvalidOperationException("WorldMapRootMissing");
            EnsureGlobe(worldMapRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, 통합WorldScenePolicy.CanonicalScenePath))
                throw new InvalidOperationException("WorldGlobeSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateOpenScene();
            Selection.activeGameObject = worldMapRoot.Find(GlobeRootName)?.gameObject;
            SceneView.RepaintAll();
            Debug.Log("WORLD-GLOBE-0 built: " + 통합WorldScenePolicy.CanonicalScenePath);
        }

        [MenuItem("Ssalddel/통합 월드/세계 지구본/Validate Open Scene")]
        public static void ValidateOpenScene()
        {
            var shell = GameObject.Find(SimulationWorldShellBuilder.RootName)
                ?? throw new InvalidOperationException("SimulationWorldShellMissing");
            var globeRoot = shell.transform.Find("WorldMapRoot/" + GlobeRootName)
                ?? throw new InvalidOperationException("WorldGlobeRootMissing");
            var views = globeRoot.GetComponents<세계지구본View>();
            if (views.Length != 1)
                throw new InvalidOperationException("WorldGlobeViewCountInvalid:" + views.Length);
            var asset = Resources.Load<TextAsset>(세계지구본View.CatalogResourceName)
                ?? throw new InvalidOperationException("WorldGlobeCountryCatalogResourceMissing");
            var catalog = JsonUtility.FromJson<세계지구본Catalog>(asset.text)
                ?? throw new InvalidOperationException("WorldGlobeCountryCatalogJsonInvalid");
            catalog.Validate();
            Debug.Log("WORLD-GLOBE-0 validation passed: " + catalog.revision
                + " / countries=" + catalog.countries.Length);
        }

        public static 세계지구본View EnsureGlobe(Transform worldMapRoot)
        {
            if (worldMapRoot == null) throw new ArgumentNullException(nameof(worldMapRoot));
            var globeRoot = worldMapRoot.Find(GlobeRootName);
            if (globeRoot == null)
            {
                var item = new GameObject(GlobeRootName);
                item.transform.SetParent(worldMapRoot, false);
                globeRoot = item.transform;
            }
            var view = globeRoot.GetComponent<세계지구본View>();
            if (view == null) view = globeRoot.gameObject.AddComponent<세계지구본View>();
            return view;
        }
    }
}
