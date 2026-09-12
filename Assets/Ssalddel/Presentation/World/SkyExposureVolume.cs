using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class SkyExposureVolume : MonoBehaviour
    {
        [SerializeField] private BoxCollider volume = null!;
        [SerializeField] private bool blocksPrecipitation = true;

        public bool BlocksPrecipitation => blocksPrecipitation;

        public void Configure(BoxCollider box, bool blocksRain = true)
        {
            volume = box;
            blocksPrecipitation = blocksRain;
            volume.isTrigger = true;
        }

        public bool Contains(Vector3 worldPosition)
            => blocksPrecipitation && volume != null
                && volume.bounds.Contains(worldPosition);

        public static bool IsCovered(Vector3 worldPosition)
        {
            foreach (var item in FindObjectsByType<SkyExposureVolume>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (item.Contains(worldPosition)) return true;
            return false;
        }
    }
}
