using System;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical Scene에 네이처 탐험·조우 표현 연결부만 좁게 추가합니다.
    /// 별도 공식 Scene이나 전투 권위를 만들지 않습니다.
    /// </summary>
    public static class 네이처탐험조우SceneBuilder
    {
        public const string RootName = "NatureExplorationEncounterRoot_PresentationOnly";
        private const string SettingsPath =
            "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset";

        [MenuItem("Ssalddel/NATURE-PLAY-1/Apply Exploration Encounter Loop")]
        public static void ApplyToCanonicalScene()
        {
            var scene = EditorSceneManager.OpenScene(
                통합WorldScenePolicy.CanonicalScenePath, OpenSceneMode.Single);
            var shell = UnityEngine.Object.FindFirstObjectByType<
                SimulationWorldShellPresenter>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("SimulationWorldShellPresenterMissing");
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureExplorationPlayerMissing");
            var combat = UnityEngine.Object.FindFirstObjectByType<
                전투시점Controller>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureExplorationCombatMissing");
            var battle = UnityEngine.Object.FindFirstObjectByType<
                현장전투CompositionRoot>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureExplorationBattleRootMissing");
            var runtimeRoot = battle.transform;
            var worldMapRoot = GameObject.Find("SimulationWorldShell/WorldMapRoot")
                ?.transform ?? throw new InvalidOperationException("WorldMapRootMissing");
            var previous = worldMapRoot.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var existing in runtimeRoot
                         .GetComponents<네이처탐험조우CompositionRoot>())
                UnityEngine.Object.DestroyImmediate(existing);

            var root = new GameObject(RootName).transform;
            root.SetParent(worldMapRoot, true);
            root.position = player.transform.position;
            var presenter = root.gameObject.AddComponent<네이처조우Presenter>();
            // 현재 설치된 Nature 팩에는 캐릭터·몬스터 Prefab이 없다.
            // null 슬롯은 명시적인 대체 위협 형상을 사용하며 추후 검토한 자산으로 교체한다.
            presenter.Configure(player, root, null);

            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                SettingsPath) ?? throw new InvalidOperationException(
                "UnityClientRuntimeSettingsMissing");
            var composition = runtimeRoot.gameObject
                .AddComponent<네이처탐험조우CompositionRoot>();
            composition.Configure(settings, shell, presenter, combat, battle, true);

            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(composition);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene,
                    통합WorldScenePolicy.CanonicalScenePath))
                throw new InvalidOperationException("NatureExplorationSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateOpenScene();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("NATURE-PLAY-1 applied: "
                + 통합WorldScenePolicy.CanonicalScenePath);
        }

        [MenuItem("Ssalddel/NATURE-PLAY-1/Validate Open Scene")]
        public static void ValidateOpenScene()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<
                네이처조우Presenter>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureEncounterPresenterMissing");
            var composition = UnityEngine.Object.FindFirstObjectByType<
                네이처탐험조우CompositionRoot>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureEncounterCompositionMissing");
            if (!presenter.ValidateWiring() || !presenter.PresentationOnly
                || !presenter.UsesPlaceholderVisual || !composition.ValidateWiring()
                || !composition.ServerAuthorityEnabled)
                throw new InvalidOperationException("NatureExplorationSceneWiringInvalid");
        }
    }
}
