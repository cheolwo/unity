using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 타로객체강조Presenter : MonoBehaviour
    {
        private const string MarkerName = "TarotCurrentEffectMarker";
        private readonly Dictionary<string, 통합전시관SeedbedObjectRoot> roots =
            new Dictionary<string, 통합전시관SeedbedObjectRoot>(StringComparer.Ordinal);
        private readonly List<GameObject> markers = new List<GameObject>();
        private Material? markerMaterial;

        public IReadOnlyCollection<string> HighlightedObjectStableIds { get; private set; }
            = Array.Empty<string>();
        public int HighlightMarkerCount => markers.Count;

        public void RefreshRootsFromScene()
            => Configure(UnityEngine.Object.FindObjectsByType<통합전시관SeedbedObjectRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));

        public void Configure(IEnumerable<통합전시관SeedbedObjectRoot> objectRoots)
        {
            roots.Clear();
            foreach (var root in objectRoots ?? throw new ArgumentNullException(nameof(objectRoots)))
            {
                if (root == null || string.IsNullOrWhiteSpace(root.ObjectStableId))
                    throw new InvalidOperationException("TarotHighlightObjectIdentityMissing");
                if (!roots.TryAdd(root.ObjectStableId, root))
                    throw new InvalidOperationException("TarotHighlightObjectDuplicate:" + root.ObjectStableId);
            }
        }

        public void Apply(IEnumerable<string> objectStableIds)
        {
            Clear();
            if (roots.Count == 0) RefreshRootsFromScene();
            var requested = (objectStableIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var stableId in requested)
            {
                if (!roots.TryGetValue(stableId, out var root))
                    throw new InvalidOperationException("TarotHighlightObjectMissing:" + stableId);
                markers.Add(CreateMarker(root));
            }
            HighlightedObjectStableIds = requested;
        }

        public void Clear()
        {
            foreach (var marker in markers.Where(value => value != null))
            {
                if (UnityEngine.Application.isPlaying) Destroy(marker);
                else DestroyImmediate(marker);
            }
            markers.Clear();
            HighlightedObjectStableIds = Array.Empty<string>();
        }

        private GameObject CreateMarker(통합전시관SeedbedObjectRoot root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("TarotHighlightRendererMissing:" + root.ObjectStableId);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

            var marker = new GameObject(MarkerName);
            marker.transform.SetParent(root.transform, true);
            var line = marker.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 40;
            line.startWidth = .11f;
            line.endWidth = .11f;
            line.startColor = new Color(1f, .78f, .15f, 1f);
            line.endColor = new Color(1f, .45f, .08f, 1f);
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.material = ResolveMarkerMaterial();
            var radiusX = Math.Max(.65f, bounds.extents.x * 1.18f);
            var radiusZ = Math.Max(.65f, bounds.extents.z * 1.18f);
            var y = bounds.max.y + .16f;
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(index, new Vector3(
                    bounds.center.x + Mathf.Cos(angle) * radiusX,
                    y,
                    bounds.center.z + Mathf.Sin(angle) * radiusZ));
            }
            return marker;
        }

        private Material ResolveMarkerMaterial()
        {
            if (markerMaterial != null) return markerMaterial;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default")
                ?? throw new InvalidOperationException("TarotHighlightShaderMissing");
            markerMaterial = new Material(shader)
            {
                name = "TarotCurrentEffectMarker_Runtime",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return markerMaterial;
        }

        private void OnDestroy()
        {
            Clear();
            if (markerMaterial == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(markerMaterial);
            else DestroyImmediate(markerMaterial);
            markerMaterial = null;
        }
    }
}
