using System;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public sealed class PublicWorldMarkerView : MonoBehaviour
    {
        private Action<string> selected;

        public string StableId { get; private set; } = string.Empty;
        public PublicWorldMarker Marker { get; private set; }

        public void Initialize(PublicWorldMarker marker, Action<string> onSelected)
        {
            Marker = marker ?? throw new ArgumentNullException(nameof(marker));
            marker.Validate();
            StableId = marker.StableId;
            selected = onSelected;
            name = $"Marker_{marker.StableId}_{marker.SourceName}";
        }

        public void Select() => selected?.Invoke(StableId);

        private void OnMouseDown() => Select();
    }
}
