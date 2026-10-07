using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>기존 공식 Scene의 명시적 로컬 관찰 진입. Scene 저장과 자동 재생을 하지 않는다.</summary>
    [InitializeOnLoad]
    public static class 사가정음식생활관찰진입
    {
        public const string CanonicalScene = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string PreviousKey = "Ssalddel.SagajeongFoodLife.PreviousEnvironment";
        private static readonly string[] Names = { "SSALDDEL_SAGAJEONG_FOOD_LIFE", "SSALDDEL_SYNTHETIC_DELIVERY",
            "SSALDDEL_OFFLINE_LIFE_BUNDLE", "SSALDDEL_RESTAURANT_OBSERVER" };

        static 사가정음식생활관찰진입()
        { EditorApplication.playModeStateChanged += StateChanged; }

        [MenuItem("Ssalddel/관찰/사가정 샐러리아 음식 생활")]
        public static void 시작()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != CanonicalScene)
            {
                if (scene.isDirty) { Debug.LogWarning("사가정 관찰 시작 보류 · 현재 Scene 변경을 먼저 보존하세요."); return; }
                EditorSceneManager.OpenScene(CanonicalScene);
            }
            if (Resources.Load<TextAsset>("SagajeongReference") == null || Resources.Load<TextAsset>("SagajeongFoodLifeBinding") == null)
            { Debug.LogError("사가정 관찰 시작 실패 · 공간 자료가 없습니다."); return; }
            if (SessionState.GetString(PreviousKey, "").Length == 0)
                SessionState.SetString(PreviousKey, JsonConvert.SerializeObject(Array.ConvertAll(Names, Environment.GetEnvironmentVariable)));
            foreach (var name in Names) Environment.SetEnvironmentVariable(name, null);
            Environment.SetEnvironmentVariable(Names[0], "1");
            EditorApplication.isPlaying = true;
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var json = SessionState.GetString(PreviousKey, "");
            if (json.Length == 0) return;
            var values = JsonConvert.DeserializeObject<string[]>(json);
            if (values != null && values.Length == Names.Length)
                for (var i = 0; i < Names.Length; i++) Environment.SetEnvironmentVariable(Names[i], values[i]);
            SessionState.EraseString(PreviousKey);
            // 기존 Nature생존AutoBootstrap이 다음 Play의 SubsystemRegistration에서 공통 실행 범위를 해제한다.
        }
    }
}
