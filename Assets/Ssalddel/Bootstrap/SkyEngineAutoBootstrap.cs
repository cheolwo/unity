using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>
    /// canonical Scene의 저장된 Synty 하늘 표현을 Nature 권위 상태 사본에 연결합니다.
    /// LH가 있는 공식 실행에서는 지면 준비 뒤 World공간표현조립Coordinator가 호출합니다.
    /// </summary>
    public static class SkyEngineAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InstallForCurrentScene()
        {
            if (SceneManager.GetActiveScene().name != "SimulationWorldShell")
                return;
            World공간표현조립Coordinator.SurfacePrepared -=
                InstallAfterLhSurfacePrepared;
            World공간표현조립Coordinator.SurfacePrepared +=
                InstallAfterLhSurfacePrepared;
            if (Object.FindFirstObjectByType<공간LHStreamingEngine>(
                    FindObjectsInactive.Include) != null)
                return;
            if (TryInstall()) return;
            if (Object.FindFirstObjectByType<SkyEngine지연Installer>(
                    FindObjectsInactive.Include) != null)
                return;
            new GameObject("SkyEngine_지연설치")
                .AddComponent<SkyEngine지연Installer>();
        }

        private static void InstallAfterLhSurfacePrepared()
            => TryInstall();

        public static bool TryInstall()
        {
            if (Object.FindFirstObjectByType<SkyEnginePresenter>(
                    FindObjectsInactive.Include) != null)
                return true;
            var nature = Object.FindFirstObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            var time = Object.FindFirstObjectByType<월드시간대Presenter>(
                FindObjectsInactive.Include);
            var camera = Object.FindFirstObjectByType<Camera>(
                FindObjectsInactive.Include);
            var visualRoot = FindTransform("SkyEngineVisualRoot");
            if (nature == null || time == null || camera == null
                || visualRoot == null || !nature.ValidateWiring())
                return false;
            var clouds = visualRoot.Find("Clouds");
            var rain = visualRoot.Find("Rain");
            var flash = visualRoot.Find("LightningFlash")?.GetComponent<Light>();
            var rainAudio = visualRoot.Find("RainAudio")
                ?.GetComponent<AudioSource>();
            var thunderAudio = visualRoot.Find("ThunderAudio")
                ?.GetComponent<AudioSource>();
            if (clouds == null || rain == null || flash == null
                || rainAudio == null || thunderAudio == null)
                return false;
            var presenter = visualRoot.gameObject.AddComponent<SkyEnginePresenter>();
            presenter.Configure(time, nature, camera, clouds, rain, flash,
                rainAudio, thunderAudio);
            if (!presenter.ValidateWiring())
            {
                Object.Destroy(presenter);
                Debug.LogWarning("SkyEngineBootstrapDeferred:WiringInvalid");
                return false;
            }
            Debug.Log("SkyEngineReady:world-atmosphere.r1:authority=Simulation");
            return true;
        }

        private static Transform? FindTransform(string objectName)
        {
            foreach (var value in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (value.name == objectName) return value;
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SkyEngine지연Installer : MonoBehaviour
    {
        private float remainingSeconds = 20f;

        private void Update()
        {
            if (SkyEngineAutoBootstrap.TryInstall())
            {
                Destroy(gameObject);
                return;
            }
            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds > 0f) return;
            Debug.LogWarning("SkyEngineDeferred:ControllerOrVisualRootMissing");
            Destroy(gameObject);
        }
    }
}
