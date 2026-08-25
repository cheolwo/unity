using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// Nature 권위 Bootstrap과 실행 순서를 결합하지 않고 표현 Adapter를 뒤에서
    /// 부착합니다. 표현 부착 실패는 Simulation 세션 생성이나 상태 전이를 막지 않습니다.
    /// </summary>
    public static class Nature감각표현AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InstallForCurrentScene()
        {
            if (SceneManager.GetActiveScene().name != "SimulationWorldShell")
                return;
            if (TryInstall()) return;
            if (Object.FindFirstObjectByType<Nature감각표현지연Installer>(
                    FindObjectsInactive.Include) != null)
                return;
            new GameObject("Nature감각표현_지연설치")
                .AddComponent<Nature감각표현지연Installer>();
        }

        public static bool TryInstall()
        {
            var controller = Object.FindFirstObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            if (controller == null || player == null
                || !controller.ValidateWiring())
                return false;
            var presenter = controller.GetComponent<Nature감각표현Presenter>()
                            ?? controller.gameObject
                                .AddComponent<Nature감각표현Presenter>();
            if (!presenter.IsConfigured) presenter.Configure(controller, player);
            return presenter.IsConfigured;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Nature감각표현지연Installer : MonoBehaviour
    {
        private float remainingSeconds = 20f;

        private void Update()
        {
            if (Nature감각표현AutoBootstrap.TryInstall())
            {
                Destroy(gameObject);
                return;
            }
            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds > 0f) return;
            Debug.LogWarning(
                "NatureSensoryPresentationDeferred:ControllerOrPlayerMissing");
            Destroy(gameObject);
        }
    }
}
