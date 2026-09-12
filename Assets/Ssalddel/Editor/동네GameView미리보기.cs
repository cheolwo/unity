using System;
using Ssalddel.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    [InitializeOnLoad]
    public static class 동네GameView미리보기
    {
        private static GameObject root;
        private static 가상배달관찰Controller controller;
        private static string signature;
        private static double nextUpdate;
        static 동네GameView미리보기()
        {
            EditorApplication.update += 갱신;
            EditorApplication.playModeStateChanged += phase => {
                if (phase == PlayModeStateChange.ExitingEditMode || phase == PlayModeStateChange.ExitingPlayMode) 정리();
            };
            AssemblyReloadEvents.beforeAssemblyReload += 정리;
            EditorApplication.quitting += 정리;
        }
        public static void 정리()
        {
            if (root != null) Object.DestroyImmediate(root);
            root = null; controller = null; signature = null;
        }
        public static void 갱신()
        {
            if (EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + .25;
            var scene = SceneManager.GetActiveScene();
            var profile = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || scene.name != "SimulationWorldShell" || PrefabStageUtility.GetCurrentPrefabStage() != null
                || (profile != "5" && profile != "6"))
            { 정리(); return; }
            var current = scene.handle + ":" + profile;
            if (root == null || signature != current)
            {
                정리(); signature = current;
                root = new GameObject("동네 편집 미리보기 · 공간만 표시 · NPC/업무는 Play") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                try
                {
                    controller = root.AddComponent<가상배달관찰Controller>(); controller.enabled = false;
                    controller.편집공간조립();
                    foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    root.SetActive(true);
                }
                catch (Exception error) { 정리(); Debug.LogException(error); return; }
            }
            controller.편집카메라맞춤();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
