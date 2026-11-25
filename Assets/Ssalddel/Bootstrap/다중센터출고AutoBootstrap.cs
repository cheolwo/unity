using System;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>명시적으로 고른 모의 출고 프로필만 공식 Scene에 연결한다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "명시 출고 프로필의 같은 세션과 실제 View·모형 입력을 연결한다.",
        Boundary = "모형·카메라·강조는 Simulation 명령 완료나 실재 창고의 증거가 아니다.")]
    public static class 다중센터출고AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }
        private static void Loaded(Scene scene, LoadSceneMode mode) => InstallForCurrentScene();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static async void InstallForCurrentScene()
        {
            if (!SimulationWorldLocalRuntimeScope.FulfillmentObserverRequested
                || SceneManager.GetActiveScene().name != "SimulationWorldShell"
                || UnityEngine.Object.FindAnyObjectByType<다중센터출고SceneController>(FindObjectsInactive.Include) != null)
                return;

            var root = new GameObject("다중 센터 출고 · 모의 운영 r8");
            root.AddComponent<출고관찰화면격리>().구성(root.transform);
            var controller = root.AddComponent<다중센터출고SceneController>();
            var binding = root.AddComponent<다중센터출고선반Binding>();
            var viewObject = new GameObject("출고 화면", typeof(RectTransform));
            viewObject.transform.SetParent(root.transform, false);
            var view = viewObject.AddComponent<다중센터출고View>();
            try
            {
                var scope = SimulationWorldLocalRuntimeScope.Install();
                view.입력환경구성();
                await controller.InitializeAsync(scope.Runtime, scope.SessionStableId, view, binding, scope.SlotStableId);
                if (controller == null) return;
                binding.카메라연결(FrameCamera(binding.관찰Bounds, root.transform));
                Debug.Log("FulfillmentUnityReady:synthetic=1:network=0:scene=SimulationWorldShell");
            }
            catch (Exception error)
            {
                if (view != null) view.시작실패(error.Message);
                Debug.LogException(error);
            }
        }

        private static Camera FrameCamera(Bounds bounds, Transform parent)
        {
            // 이 모드가 소유한 모형만 표시해 기존 Scene 배치와 의미를 혼합하지 않는다.
            const int previewLayer = 관찰표현LayerContract.다중센터출고Layer;
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
                renderer.gameObject.layer = previewLayer;
            // 기존 Diorama 카메라 Rig가 매 프레임 pose를 되돌리므로 별도 카메라를 소유한다.
            // 기존 카메라·Scene 자산은 지우지 않고 이 실행이 끝나면 enabled 값을 복원한다.
            var previousCameras = new System.Collections.Generic.List<Camera>();
            foreach (var existing in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing.enabled) { previousCameras.Add(existing); existing.enabled = false; }
            var cameraObject = new GameObject("출고 관찰 카메라", typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            var center = bounds.center;
            camera.transform.position = center + new Vector3(0f, 19f, -19f);
            camera.transform.LookAt(center);
            camera.rect = new Rect(0f, 0.11f, 0.6f, 0.812f);
            camera.cullingMask = 1 << previewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.085f, 0.12f);
            var framing = parent.gameObject.AddComponent<출고관찰CameraFraming>();
            framing.구성(camera, bounds, previousCameras.ToArray());
            if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("출고 표본 조명", typeof(Light));
                light.transform.SetParent(parent, false);
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
            }
            return camera;
        }
    }

    /// <summary>전용 모드에서 기존 관찰 UI만 잠시 쉬게 한다. Core·Scene 자산은 변경하지 않는다.</summary>
    internal sealed class 출고관찰화면격리 : MonoBehaviour
    {
        private readonly System.Collections.Generic.Dictionary<Behaviour, bool> previous = new();
        private static readonly System.Collections.Generic.HashSet<string> ViewTypes = new(StringComparer.Ordinal)
        {
            "Ssalddel.Unity.Presentation.WorldMap.세계지구본View",
            "Ssalddel.Unity.Presentation.World.사가정운영디오라마View",
            "Ssalddel.Unity.Presentation.World.역방어준비View",
            "Ssalddel.Unity.Presentation.World.일인칭창고상호작용Controller",
            "Ssalddel.Unity.Presentation.World.플레이어경관Controller",
            "Ssalddel.Unity.Presentation.World.네이처조우Presenter",
            "Ssalddel.Unity.Presentation.World.플레이어전술조작Controller",
            "Ssalddel.Unity.Presentation.World.일인칭경관CameraController",
            "Ssalddel.Unity.Presentation.World.전투시점Controller",
            "Ssalddel.Unity.Presentation.World.오늘작업계획Presenter",
            "Ssalddel.Unity.Presentation.World.농장경영시점Controller",
        };

        public void 구성(Transform ownedRoot)
        {
            foreach (var view in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view != null && ViewTypes.Contains(view.GetType().FullName)) 쉬기(view);
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!canvas.transform.IsChildOf(ownedRoot)) 쉬기(canvas);
        }

        private void 쉬기(Behaviour view)
        {
            if (!previous.ContainsKey(view)) previous.Add(view, view.enabled);
            view.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (var pair in previous)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
        }
    }

    /// <summary>Game View 크기가 달라져도 좌측 관찰 영역에 두 센터 전체를 담는다.</summary>
    internal sealed class 출고관찰CameraFraming : MonoBehaviour
    {
        private Camera target;
        private Bounds bounds;
        private float previousAspect;
        private Camera[] previousCameras = Array.Empty<Camera>();

        public void 구성(Camera camera, Bounds worldBounds, Camera[] previous)
        {
            previousCameras = previous;
            target = camera; bounds = worldBounds; 맞춤();
        }

        private void LateUpdate()
        {
            if (target != null && !Mathf.Approximately(previousAspect, target.aspect)) 맞춤();
        }

        private void 맞춤()
        {
            var extent = bounds.extents;
            var halfWidth = 0f; var halfHeight = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = bounds.center + Vector3.Scale(extent, new Vector3(x, y, z));
                var local = target.transform.InverseTransformPoint(corner);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
            }
            previousAspect = target.aspect;
            target.orthographicSize = Mathf.Max(7f, halfHeight, halfWidth / Mathf.Max(.1f, previousAspect)) * 1.12f;
        }

        private void OnDestroy()
        {
            foreach (var previous in previousCameras)
                if (previous != null) previous.enabled = true;
        }
    }
}
