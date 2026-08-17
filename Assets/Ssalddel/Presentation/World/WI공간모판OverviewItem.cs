using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class WI공간모판OverviewItem : MonoBehaviour
    {
        [SerializeField] private string seedbedStableId = string.Empty;
        [SerializeField] private string spaceCode = string.Empty;
        [SerializeField] private string compositionKey = string.Empty;

        public string SeedbedStableId => seedbedStableId;
        public string SpaceCode => spaceCode;
        public string CompositionKey => compositionKey;

        public void Configure(string seedbedId, string space, string candidateKey)
        {
            seedbedStableId = seedbedId ?? string.Empty;
            spaceCode = space ?? string.Empty;
            compositionKey = candidateKey ?? string.Empty;
        }
    }
}
