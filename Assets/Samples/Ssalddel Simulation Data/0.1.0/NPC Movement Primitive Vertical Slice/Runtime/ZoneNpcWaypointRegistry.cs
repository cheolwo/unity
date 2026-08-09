using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    public sealed class ZoneNpcWaypointRegistry : MonoBehaviour
    {
        [SerializeField]
        private NpcWaypointView[] waypoints = Array.Empty<NpcWaypointView>();

        private Dictionary<string, Transform>? waypointMap;

        public void Configure(NpcWaypointView[] values)
        {
            waypoints = values ?? Array.Empty<NpcWaypointView>();
            waypointMap = null;
        }

        public bool TryResolve(string waypointKey, out Transform destination)
        {
            EnsureMap();
            return waypointMap!.TryGetValue(waypointKey, out destination!);
        }

        public bool ValidateWiring()
        {
            try
            {
                EnsureMap();
                return waypointMap!.Count > 0;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void EnsureMap()
        {
            if (waypointMap != null)
            {
                return;
            }

            waypointMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var waypoint in waypoints)
            {
                if (waypoint == null || !waypoint.ValidateWiring())
                {
                    throw new InvalidOperationException("NPC waypoint wiring이 유효하지 않습니다.");
                }

                if (!waypointMap.TryAdd(waypoint.WaypointKey, waypoint.transform))
                {
                    throw new InvalidOperationException(
                        "중복 NPC waypoint입니다: " + waypoint.WaypointKey);
                }
            }
        }
    }
}
