using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 캐릭터지면정렬Utility
    {
        public const float DefaultFootClearance = .03f;

        public static float AlignFeetToGround(
            Transform visualRoot,
            Transform groundAnchor,
            float footClearance = DefaultFootClearance)
        {
            if (visualRoot == null || groundAnchor == null
                || footClearance < 0f)
                throw new ArgumentException("CharacterGroundingInputInvalid");
            if (!TryGetVisibleBounds(visualRoot, out var bounds))
                throw new InvalidOperationException(
                    "CharacterGroundingVisibleRendererMissing");

            var lift = groundAnchor.position.y + footClearance - bounds.min.y;
            visualRoot.position += Vector3.up * lift;
            return lift;
        }

        public static bool TryGetVisibleBounds(
            Transform visualRoot,
            out Bounds bounds)
        {
            bounds = default;
            if (visualRoot == null) return false;

            var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            var found = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled
                    || !IsLocallyVisible(renderer.transform, visualRoot))
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                    continue;
                }
                bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static bool IsLocallyVisible(
            Transform candidate,
            Transform visualRoot)
        {
            var current = candidate;
            while (current != null)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == visualRoot) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
