using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 공급망WorldZoneView : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string presentationZoneCode = string.Empty;
        [SerializeField] private string canonicalWorldZoneCode = string.Empty;
        [SerializeField] private int flowOrder;
        [SerializeField] private Transform focusAnchor = null!;
        [SerializeField] private Transform visualRoot = null!;

        public string StableId => stableId;
        public string PresentationZoneCode => presentationZoneCode;
        public string CanonicalWorldZoneCode => canonicalWorldZoneCode;
        public int FlowOrder => flowOrder;
        public Transform FocusAnchor => focusAnchor;
        public Transform VisualRoot => visualRoot;

        public void Configure(
            공급망WorldZoneDefinition definition,
            Transform anchor,
            Transform visuals)
        {
            stableId = definition.StableId;
            presentationZoneCode = definition.PresentationZoneCode;
            canonicalWorldZoneCode = definition.CanonicalWorldZoneCode;
            flowOrder = definition.FlowOrder;
            focusAnchor = anchor;
            visualRoot = visuals;
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId)
                && 공급망PresentationZoneCodes.IsKnown(presentationZoneCode)
                && !string.IsNullOrWhiteSpace(canonicalWorldZoneCode)
                && flowOrder >= 0
                && focusAnchor != null
                && visualRoot != null;
    }
}
