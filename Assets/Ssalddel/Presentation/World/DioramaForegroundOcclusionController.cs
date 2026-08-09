using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class DioramaOcclusionView : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers = System.Array.Empty<Renderer>();

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetOccluded(bool occluded)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var item in renderers)
            {
                if (item != null) item.enabled = !occluded;
            }
        }
    }

    /// <summary>
    /// Minimal WORLD-0 cutaway. Only explicitly marked Presentation renderers can be hidden.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DioramaForegroundOcclusionController : MonoBehaviour
    {
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;
        [SerializeField] private LayerMask occlusionLayers = ~0;
        [SerializeField, Min(.1f)] private float focusOcclusionRadius = 2.5f;

        private readonly HashSet<DioramaOcclusionView> hidden = new();
        private readonly HashSet<DioramaOcclusionView> nextHidden = new();

        public void Configure(DioramaTopDownCameraRig rig) => cameraRig = rig;

        private void LateUpdate() => ApplyNow();

        private void OnDisable()
        {
            foreach (var item in hidden)
            {
                if (item != null) item.SetOccluded(false);
            }
            hidden.Clear();
        }

        public void ApplyNow()
        {
            if (cameraRig == null) return;
            Physics.SyncTransforms();
            var origin = transform.position;
            var target = cameraRig.CurrentFocusPosition;
            var direction = target - origin;
            var distance = direction.magnitude;
            if (distance <= .01f) return;

            nextHidden.Clear();
            var hits = Physics.SphereCastAll(
                origin,
                focusOcclusionRadius,
                direction / distance,
                distance - .05f,
                occlusionLayers,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<DioramaOcclusionView>();
                if (view != null) nextHidden.Add(view);
            }

            foreach (var item in hidden)
            {
                if (item != null && !nextHidden.Contains(item)) item.SetOccluded(false);
            }
            foreach (var item in nextHidden)
            {
                if (item != null && !hidden.Contains(item)) item.SetOccluded(true);
            }

            hidden.Clear();
            foreach (var item in nextHidden) hidden.Add(item);
        }
    }
}
