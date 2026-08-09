using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
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
