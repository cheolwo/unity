using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 공급망MacroWorldView : MonoBehaviour
    {
        [SerializeField] private 공급망WorldZoneView[] zones =
            Array.Empty<공급망WorldZoneView>();
        [SerializeField] private 공급망WorldRouteView[] routes =
            Array.Empty<공급망WorldRouteView>();
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;

        public IReadOnlyList<공급망WorldZoneView> Zones => zones;
        public IReadOnlyList<공급망WorldRouteView> Routes => routes;
        public DioramaTopDownCameraRig CameraRig => cameraRig;

        public void Configure(
            공급망WorldZoneView[] zoneViews,
            공급망WorldRouteView[] routeViews,
            DioramaTopDownCameraRig rig)
        {
            zones = zoneViews ?? Array.Empty<공급망WorldZoneView>();
            routes = routeViews ?? Array.Empty<공급망WorldRouteView>();
            cameraRig = rig;
        }

        public bool ValidateWiring()
        {
            if (zones == null || zones.Length != 6 || routes == null
                || routes.Length != zones.Length - 1 || cameraRig == null
                || zones.Any(value => value == null || !value.ValidateWiring())
                || routes.Any(value => value == null || !value.ValidateWiring()))
            {
                return false;
            }

            var zoneIds = zones.Select(value => value.StableId)
                .ToHashSet(StringComparer.Ordinal);
            if (zoneIds.Count != zones.Length
                || zones.Select(value => value.FlowOrder).Distinct().Count() != zones.Length)
            {
                return false;
            }

            var orderedZones = zones.OrderBy(value => value.FlowOrder).ToArray();
            var orderedRoutes = routes.OrderBy(value => value.FlowOrder).ToArray();
            for (var index = 0; index < orderedRoutes.Length; index++)
            {
                if (orderedRoutes[index].FromZoneStableId != orderedZones[index].StableId
                    || orderedRoutes[index].ToZoneStableId != orderedZones[index + 1].StableId)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
