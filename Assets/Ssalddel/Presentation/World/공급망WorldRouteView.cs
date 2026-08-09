using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 공급망WorldRouteView : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string fromZoneStableId = string.Empty;
        [SerializeField] private string toZoneStableId = string.Empty;
        [SerializeField] private int flowOrder;
        [SerializeField] private Transform visualRoot = null!;

        public string StableId => stableId;
        public string FromZoneStableId => fromZoneStableId;
        public string ToZoneStableId => toZoneStableId;
        public int FlowOrder => flowOrder;
        public Transform VisualRoot => visualRoot;

        public void Configure(공급망WorldRouteLegDefinition definition, Transform visuals)
        {
            stableId = definition.StableId;
            fromZoneStableId = definition.FromZoneStableId;
            toZoneStableId = definition.ToZoneStableId;
            flowOrder = definition.FlowOrder;
            visualRoot = visuals;
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId)
                && !string.IsNullOrWhiteSpace(fromZoneStableId)
                && !string.IsNullOrWhiteSpace(toZoneStableId)
                && flowOrder >= 0
                && visualRoot != null;
    }
}
