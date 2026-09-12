using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public readonly struct 배치객체표면검증결과
    {
        public 배치객체표면검증결과(
            bool passed,
            string resultCode,
            float surfaceWorldY,
            float visibleBottomWorldY,
            float requiredClearance,
            float error)
        {
            Passed = passed;
            ResultCode = resultCode;
            SurfaceWorldY = surfaceWorldY;
            VisibleBottomWorldY = visibleBottomWorldY;
            RequiredClearance = requiredClearance;
            Error = error;
        }

        public bool Passed { get; }
        public string ResultCode { get; }
        public float SurfaceWorldY { get; }
        public float VisibleBottomWorldY { get; }
        public float RequiredClearance { get; }
        public float Error { get; }
    }

    public static class 배치객체표면정렬Utility
    {
        public const float DefaultSurfaceClearance = .03f;
        public const float DefaultValidationTolerance = .015f;

        public static float AlignVisibleBottomToWorldHeight(
            Transform visualRoot,
            float surfaceWorldY,
            float surfaceClearance = DefaultSurfaceClearance)
        {
            if (visualRoot == null || !IsFinite(surfaceWorldY)
                || !IsFinite(surfaceClearance) || surfaceClearance < 0f)
                throw new ArgumentException("PlacementSurfaceAlignmentInputInvalid");
            if (!TryGetVisibleBounds(visualRoot, out var bounds))
                throw new InvalidOperationException(
                    "PlacementSurfaceVisibleRendererMissing");

            var lift = surfaceWorldY + surfaceClearance - bounds.min.y;
            visualRoot.position += Vector3.up * lift;
            return lift;
        }

        public static 배치객체표면검증결과 ValidateVisibleBottomClearance(
            Transform visualRoot,
            float surfaceWorldY,
            float surfaceClearance = DefaultSurfaceClearance,
            float tolerance = DefaultValidationTolerance)
        {
            if (visualRoot == null || !IsFinite(surfaceWorldY)
                || !IsFinite(surfaceClearance) || surfaceClearance < 0f
                || !IsFinite(tolerance) || tolerance < 0f)
                return new 배치객체표면검증결과(false,
                    "SurfaceValidationInputInvalid", surfaceWorldY, float.NaN,
                    surfaceClearance, float.NaN);
            if (!TryGetVisibleBounds(visualRoot, out var bounds))
                return new 배치객체표면검증결과(false,
                    "VisibleRendererMissing", surfaceWorldY, float.NaN,
                    surfaceClearance, float.NaN);

            var expectedBottom = surfaceWorldY + surfaceClearance;
            var error = bounds.min.y - expectedBottom;
            var resultCode = error < -tolerance
                ? "VisibleBottomBelowSurface"
                : error > tolerance
                    ? "ObjectFloatingAboveSurface"
                    : "Passed";
            return new 배치객체표면검증결과(resultCode == "Passed",
                resultCode, surfaceWorldY, bounds.min.y, surfaceClearance, error);
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

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

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
            if (!TryGetVisibleBounds(visualRoot, out _))
                throw new InvalidOperationException(
                    "CharacterGroundingVisibleRendererMissing");
            return 배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                visualRoot, groundAnchor.position.y, footClearance);
        }

        public static bool TryGetVisibleBounds(
            Transform visualRoot,
            out Bounds bounds)
        {
            return 배치객체표면정렬Utility.TryGetVisibleBounds(
                visualRoot, out bounds);
        }
    }
}
