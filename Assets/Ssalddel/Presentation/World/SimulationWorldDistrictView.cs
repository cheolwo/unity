using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class SimulationWorldDistrictView : MonoBehaviour
    {
        [SerializeField] private string districtStableId = string.Empty;
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private bool presentationPlaceholder;

        public string DistrictStableId => districtStableId;
        public string VisualKey => visualKey;
        public bool PresentationPlaceholder => presentationPlaceholder;

        public void Configure(string stableId, string semanticVisualKey, bool isPlaceholder)
        {
            districtStableId = stableId;
            visualKey = semanticVisualKey;
            presentationPlaceholder = isPlaceholder;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(districtStableId))
                throw new InvalidOperationException("SimulationDistrictViewStableIdMissing");
            if (string.IsNullOrWhiteSpace(visualKey))
                throw new InvalidOperationException("SimulationDistrictViewVisualKeyMissing");
        }
    }
}
