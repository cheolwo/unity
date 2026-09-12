using System;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 산수몽지도Builder
    {
        private const string ScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [MenuItem("Ssalddel/통합 월드/산수몽/호송 지도 구성")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var shell = UnityEngine.Object.FindFirstObjectByType<
                SimulationWorldShellPresenter>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("MengMapShellMissing");
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("MengMapPlayerMissing");
            var runtimeRoot = shell.transform;
            var presenter = runtimeRoot.GetComponent<산수몽지도Presenter>()
                ?? runtimeRoot.gameObject.AddComponent<산수몽지도Presenter>();
            presenter.Configure(player, "actor:sim:player-survivor");
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                    "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset")
                ?? throw new InvalidOperationException(
                    "UnityClientRuntimeSettingsMissing");
            var composition = runtimeRoot.GetComponent<산수몽지도CompositionRoot>()
                ?? runtimeRoot.gameObject.AddComponent<산수몽지도CompositionRoot>();
            composition.Configure(settings, shell, presenter,
                "actor:sim:player-survivor");
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(composition);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("MengMapSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Debug.Log("산수몽 호송 지도 구성을 저장했습니다. 실제 서버 조회와 Game View는 별도 검증 대상입니다.");
        }
    }
}
