using UnityEngine;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    public sealed class NpcWaypointView : MonoBehaviour
    {
        [SerializeField]
        private string waypointKey = string.Empty;

        public string WaypointKey => waypointKey;

        public void Configure(string key)
        {
            waypointKey = key?.Trim() ?? string.Empty;
        }

        public bool ValidateWiring()
        {
            return !string.IsNullOrWhiteSpace(waypointKey);
        }
    }
}
