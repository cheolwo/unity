#if UNITY_EDITOR
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        // Editor만 호출한다. Runtime 설치·조회·저장 없이 같은 공간 조립만 사용한다.
        public void 편집공간조립()
        {
            if (UnityEngine.Application.isPlaying || enabled || gameObject.activeInHierarchy)
                throw new System.InvalidOperationException("PreviewRequiresInactiveDisabledController");
            if (SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested)
            {
                업무배달동네배경조립();
                observer = new GameObject("업무 배달 편집 카메라").AddComponent<Camera>();
                observer.transform.SetParent(transform);
            }
            else
            {
                Build();
                // 시작 표본/저장을 읽지 않았으므로 동적 주체·재고를 현재 사실처럼 표시하지 않는다.
                foreach (var item in new[] { courier, vehicle, parcel, martWorker, workerParcel, handoffParcel,
                    화물차, 화물상자, 창고입고물품, 창고인계물품, 마트입고물품, 창고작업물품 })
                    if (item != null) item.gameObject.SetActive(false);
                foreach (var group in new[] { fleetActors, fleetVehicles, fleetParcels, shelfGoods, 창고재고표현 })
                    foreach (var item in group) if (item != null) item.gameObject.SetActive(false);
                foreach (var item in 생활주체.Values) if (item != null) item.gameObject.SetActive(false);
            }
            observer.depth = 1000;
            observer.orthographic = true;
            observer.clearFlags = CameraClearFlags.SolidColor;
            observer.backgroundColor = new Color(.5f, .65f, .8f);
            observer.cullingMask = 1 << 31;
            observer.nearClipPlane = .1f; observer.farClipPlane = 300;
            편집카메라맞춤();
        }

        public void 편집카메라맞춤()
        {
            if (observer == null || UnityEngine.Application.isPlaying) return;
            var bounds = SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested ? 업무배달동네배경Layout.관찰Bounds
                : SimulationWorldLocalRuntimeScope.NeighborhoodLifeRequested ? new Bounds(new Vector3(0, 3, 8), new Vector3(90, 8, 58))
                : new Bounds(new Vector3(12, 3, 8), new Vector3(58, 8, 58));
            var rotation = 동네전체관찰CameraMath.기본회전;
            observer.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 120, rotation);
            observer.orthographicSize = 동네전체관찰CameraMath.맞춤크기(bounds, rotation,
                Mathf.Max(.1f, observer.pixelRect.width / Mathf.Max(1, observer.pixelRect.height)));
        }
    }
}
#endif
