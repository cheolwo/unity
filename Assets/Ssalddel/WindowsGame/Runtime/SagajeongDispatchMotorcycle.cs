using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private const string MotorcycleRiderId = "management-driver-jungnang-b";
        private readonly Dictionary<string, float> motorcycleHeadings = new();

        private void UpdateMotorcycleHeading(bool reset)
        {
            if (reset) motorcycleHeadings.Clear();
            foreach (var rider in state.couriers)
                if (rider.phase is "ToPickup" or "ToDropoff" && TryRoadHeading(rider, out var heading))
                    motorcycleHeadings[rider.id] = heading;
        }

        private float MotorcycleHeading(string riderId) =>
            motorcycleHeadings.TryGetValue(riderId, out var heading) ? heading : 0f;

        private static bool TryRoadHeading(DispatchCourier rider, out float heading)
        {
            heading = 0;
            if (rider.route is not { Length: > 1 }) return false;

            // Match the map projection. The engine's sampled position lies on the active road.
            const double MetresPerLatitudeDegree = 111200;
            var longitudeMetres = Math.Cos(rider.latitude * Math.PI / 180) * MetresPerLatitudeDegree;
            var nearestSquared = double.PositiveInfinity;
            for (var index = 1; index < rider.route.Length; index++)
            {
                var start = rider.route[index - 1];
                var end = rider.route[index];
                var east = (end.longitude - start.longitude) * longitudeMetres;
                var north = (end.latitude - start.latitude) * MetresPerLatitudeDegree;
                var lengthSquared = east * east + north * north;
                if (lengthSquared == 0) continue;

                var riderEast = (rider.longitude - start.longitude) * longitudeMetres;
                var riderNorth = (rider.latitude - start.latitude) * MetresPerLatitudeDegree;
                var fraction = Math.Clamp((riderEast * east + riderNorth * north) / lengthSquared, 0, 1);
                var offsetEast = riderEast - fraction * east;
                var offsetNorth = riderNorth - fraction * north;
                var distanceSquared = offsetEast * offsetEast + offsetNorth * offsetNorth;

                // At a shared vertex the outgoing segment gives the direction of the next move.
                var atStart = rider.latitude == start.latitude && rider.longitude == start.longitude;
                if (distanceSquared > nearestSquared || (distanceSquared == nearestSquared && !atStart)) continue;
                nearestSquared = distanceSquared;
                heading = (float)(Math.Atan2(east, north) * 180 / Math.PI);
            }
            return nearestSquared < double.PositiveInfinity;
        }

        private static void RotateLocal(float angle, Vector2 pivot)
        {
            if (Mathf.Approximately(angle, 0f)) return;
            GUI.matrix = GUI.matrix * TurnAround(pivot, Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)));
        }
    }
}
