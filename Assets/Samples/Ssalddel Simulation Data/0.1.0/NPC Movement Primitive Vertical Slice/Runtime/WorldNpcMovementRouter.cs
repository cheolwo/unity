using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Npcs;
using UnityEngine;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    [Serializable]
    public sealed class ZoneNpcMovementBinding
    {
        public string WorldZoneCode = string.Empty;

        public ZoneNpcMovementController Controller = null!;
    }

    public sealed class WorldNpcMovementRouter : MonoBehaviour
    {
        [SerializeField]
        private ZoneNpcMovementBinding[] zones = Array.Empty<ZoneNpcMovementBinding>();

        public void Configure(ZoneNpcMovementBinding[] bindings)
        {
            zones = bindings ?? Array.Empty<ZoneNpcMovementBinding>();
        }

        public string[] Apply(NpcMovementSnapshot[] snapshots)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            var unresolved = new List<string>();
            foreach (var group in snapshots.GroupBy(item => item.WorldZoneCode, StringComparer.Ordinal))
            {
                var binding = zones.SingleOrDefault(item =>
                    item != null
                    && string.Equals(item.WorldZoneCode, group.Key, StringComparison.Ordinal));
                if (binding?.Controller == null)
                {
                    unresolved.AddRange(group.Select(item => item.NpcStableId));
                    continue;
                }

                unresolved.AddRange(binding.Controller.ApplySnapshots(group.ToArray()));
            }

            return unresolved.ToArray();
        }

        public bool ValidateWiring()
        {
            return zones != null
                && zones.Length > 0
                && zones.All(item => item != null
                    && !string.IsNullOrWhiteSpace(item.WorldZoneCode)
                    && item.Controller != null
                    && item.Controller.ValidateWiring())
                && zones.Select(item => item.WorldZoneCode)
                    .Distinct(StringComparer.Ordinal).Count() == zones.Length;
        }
    }
}
