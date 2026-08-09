using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public sealed class PublicWorldMapPresenter : MonoBehaviour
    {
        [SerializeField] private Transform markerRoot;
        [SerializeField] private float mapWidth = 18f;
        [SerializeField] private float mapHeight = 9f;
        private readonly Dictionary<string, PublicWorldMarkerView> views = new Dictionary<string, PublicWorldMarkerView>(StringComparer.Ordinal);
        private Action<string> markerSelected;

        public int MarkerCount => views.Count;

        public void SetMarkerSelectedHandler(Action<string> handler) => markerSelected = handler;

        public void Apply(PublicWorldMapSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            EnsureRoot();
            var markers = snapshot.Markers ?? Array.Empty<PublicWorldMarker>();
            var byId = markers.ToDictionary(marker => marker.StableId, StringComparer.Ordinal);

            foreach (var removedId in views.Keys.Where(id => !byId.ContainsKey(id)).ToArray())
            {
                DestroyView(views[removedId]);
                views.Remove(removedId);
            }

            foreach (var marker in markers)
            {
                marker.Validate();
                if (views.TryGetValue(marker.StableId, out var existing)) UpdateMarker(existing, marker);
                else CreateMarker(marker);
            }
        }

        public void Clear()
        {
            if (markerRoot == null) return;
            views.Clear();
            for (var i = markerRoot.childCount - 1; i >= 0; i--)
                DestroyView(markerRoot.GetChild(i).GetComponent<PublicWorldMarkerView>());
        }

        public bool TrySelect(string stableId)
        {
            if (!views.TryGetValue(stableId ?? string.Empty, out var view)) return false;
            view.Select();
            return true;
        }

        public Vector3 Project(double latitude, double longitude) => new Vector3(
            (float)(longitude / 180d) * mapWidth * .5f,
            .15f,
            (float)(latitude / 90d) * mapHeight * .5f);

        private void EnsureRoot()
        {
            if (markerRoot != null) return;
            var root = new GameObject("PublicWorldMarkers");
            root.transform.SetParent(transform, false);
            markerRoot = root.transform;
        }

        private void CreateMarker(PublicWorldMarker marker)
        {
            marker.Validate();
            var view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            view.transform.SetParent(markerRoot, false);
            view.transform.localScale = Vector3.one * .28f;
            var markerView = view.AddComponent<PublicWorldMarkerView>();
            markerView.Initialize(marker, id => markerSelected?.Invoke(id));
            UpdateMarker(markerView, marker);
            views.Add(marker.StableId, markerView);
        }

        private void UpdateMarker(PublicWorldMarkerView view, PublicWorldMarker marker)
        {
            view.Initialize(marker, id => markerSelected?.Invoke(id));
            view.transform.localPosition = Project(marker.Latitude, marker.Longitude);
        }

        private void DestroyView(PublicWorldMarkerView view)
        {
            if (view == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
    }
}
