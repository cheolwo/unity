using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    [InitializeOnLoad]
    public static class 다중센터출고진입
    {
        public const string MenuPath = "Ssalddel/경영/다중 센터 출고 플레이";
        public const string CanonicalScene = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string PreviousKey = "Ssalddel.Fulfillment.PreviousEnvironment";
        private static readonly string[] Names =
        {
            "SSALDDEL_FULFILLMENT_OBSERVER", "SSALDDEL_SAGAJEONG_FOOD_LIFE",
            "SSALDDEL_SYNTHETIC_DELIVERY", "SSALDDEL_OFFLINE_LIFE_BUNDLE",
            "SSALDDEL_RESTAURANT_OBSERVER", "SSALDDEL_HANS_FENCE_E5",
            "SSALDDEL_NATURE_E7_VALIDATION",
        };

        static 다중센터출고진입() => EditorApplication.playModeStateChanged += StateChanged;

        [MenuItem(MenuPath)]
        public static void 시작()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("출고 플레이 시작 보류 · 스크립트 컴파일과 자산 가져오기가 끝난 뒤 다시 시작하세요.");
                return;
            }
            foreach (var scene in OpenScenes())
                if (scene.isDirty)
                {
                    Debug.LogWarning("출고 플레이 시작 보류 · 열린 Scene의 미저장 변경을 먼저 보존하세요.");
                    return;
                }
            if (SceneManager.GetActiveScene().path != CanonicalScene)
                EditorSceneManager.OpenScene(CanonicalScene);
            if (SessionState.GetString(PreviousKey, "").Length == 0)
                SessionState.SetString(PreviousKey,
                    JsonConvert.SerializeObject(Array.ConvertAll(Names, Environment.GetEnvironmentVariable)));
            foreach (var name in Names) Environment.SetEnvironmentVariable(name, null);
            Environment.SetEnvironmentVariable("SSALDDEL_FULFILLMENT_OBSERVER", "1");
            try { EditorApplication.EnterPlaymode(); }
            catch { RestoreEnvironment(); throw; }
        }

        private static System.Collections.Generic.IEnumerable<Scene> OpenScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
                yield return SceneManager.GetSceneAt(index);
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) RestoreEnvironment();
        }

        private static void RestoreEnvironment()
        {
            var previous = SessionState.GetString(PreviousKey, "");
            if (previous.Length == 0) return;
            var values = JsonConvert.DeserializeObject<string[]>(previous);
            if (values == null || values.Length != Names.Length)
            {
                Debug.LogError("출고 실행 환경 복원 실패 · 저장한 환경 값이 올바르지 않습니다.");
                return;
            }
            for (var index = 0; index < Names.Length; index++)
                Environment.SetEnvironmentVariable(Names[index], values[index]);
            SessionState.EraseString(PreviousKey);
        }
    }
}
