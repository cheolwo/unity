using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 정보Panel대상SceneBuilder
    {
        [MenuItem("Ssalddel/UI/대상 Scene 정보 Panel 다시 만들기")]
        public static void BuildAll()
        {
            Build(PotatoJourneyFarmVerticalSliceBuilder.Build,
                PotatoJourneyFarmVerticalSliceBuilder.RootName, "감자 농장 출발");
            Build(PotatoJourneyCityBuilder.Build,
                PotatoJourneyCityBuilder.RootName, "감자 도시 도착");
            Build(PotatoHubReceivingLifecycleBuilder.Build,
                PotatoHubReceivingLifecycleBuilder.RootName, "물류 거점 입고 검수");
            Build(PotatoHubDispositionLifecycleBuilder.Build,
                PotatoHubDispositionLifecycleBuilder.RootName, "물류 거점 판로 분배");
            Build(PotatoHarvestCargoLifecycleBuilder.Build,
                PotatoHarvestCargoLifecycleBuilder.RootName, "수확 포장 상차");
            Build(PotatoCultivationLifecycleBuilder.Build,
                PotatoCultivationLifecycleBuilder.RootName, "재배 수확");
            Build(DirectOnlineSaleLifecycleBuilder.Build,
                DirectOnlineSaleLifecycleBuilder.RootName, "온라인 직판");
            Build(CooperativeIntakeLifecycleBuilder.Build,
                CooperativeIntakeLifecycleBuilder.RootName, "조합 인수");
            Build(HarvestDispositionChoiceBuilder.Build,
                HarvestDispositionChoiceBuilder.RootName, "수확물 판로 선택");

            PatchExistingAssetLab();
            Debug.Log("정보Panel대상SceneBuildComplete:10");
        }

        private static void Build(Action builder, string rootName, string label)
        {
            builder();
            Validate(GameObject.Find("WorldBootstrap/" + rootName), label);
        }

        private static void Validate(GameObject root, string label)
        {
            if (root == null)
                throw new InvalidOperationException(label + " Scene Root가 없습니다.");
            var controller = root.GetComponentInChildren<정보Panel상호작용Controller>(true);
            if (controller == null || !controller.ValidateWiring())
                throw new InvalidOperationException(label + " 정보 Panel 상호작용 연결이 올바르지 않습니다.");
        }

        [MenuItem("Ssalddel/UI/신티 에셋 연구소 정보 Panel 적용")]
        public static void PatchExistingAssetLab()
        {
            var scene = EditorSceneManager.OpenScene(신티에셋연구소Builder.ScenePath);
            var root = GameObject.Find(신티에셋연구소Builder.RootName)
                       ?? throw new InvalidOperationException("신티 에셋 연구소 Scene Root가 없습니다.");
            var canvas = root.transform.Find("에셋연구소Canvas")
                         ?? throw new InvalidOperationException("신티 에셋 연구소 Canvas가 없습니다.");
            var detail = canvas.Find("연구카드") as RectTransform
                         ?? throw new InvalidOperationException("신티 에셋 연구 카드가 없습니다.");
            var previous = canvas.Find("에셋 연구 카드_정보Panel상호작용");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            정보Panel상호작용Builder.Attach(canvas, detail, "에셋 연구 카드");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("신티 에셋 연구소 Scene 저장에 실패했습니다.");
            Validate(root, "신티 에셋 연구소");
        }
    }
}
