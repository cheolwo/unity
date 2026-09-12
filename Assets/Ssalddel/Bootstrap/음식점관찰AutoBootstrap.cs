using System;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "명시적으로 선택한 음식점 프로필만 공식 Scene의 로컬 권위와 연결한다.",
        Boundary = "기존 자연 생존 저장·기본 실행·원격 서버를 변경하지 않는다.")]
    public static class 음식점관찰AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode mode) => InstallForCurrentScene();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static async void InstallForCurrentScene()
        {
            if (SimulationWorldLocalRuntimeScope.SyntheticDeliveryRequested
                || !SimulationWorldLocalRuntimeScope.RestaurantObserverRequested
                || SceneManager.GetActiveScene().name != "SimulationWorldShell"
                || UnityEngine.Object.FindAnyObjectByType<음식점관찰SceneController>(FindObjectsInactive.Include) != null) return;
            // 생성 먼저: sceneLoaded와 AfterSceneLoad가 중복 호출돼도 하나만 연결한다.
            var root = new GameObject("음식점 관찰 · 로컬 r1");
            var controller = root.AddComponent<음식점관찰SceneController>();
            var view = new GameObject("음식점 관찰 UI", typeof(RectTransform)).AddComponent<음식점관찰View>();
            view.transform.SetParent(root.transform, false);
            try
            {
                var scope = SimulationWorldLocalRuntimeScope.Install();
                view.입력환경구성();
                await controller.InitializeAsync(scope.Runtime.Sessions,
                    scope.Runtime, scope.BusinessWorkflow.Orders,
                    scope.SessionStableId, view);
                if (controller.카드.조회됨) Debug.Log("RestaurantObserverLocalReady:network=0:manual-tick=1");
            }
            catch (Exception error)
            {
                view.시작실패(error.Message);
                Debug.LogException(error);
            }
        }
    }
}
