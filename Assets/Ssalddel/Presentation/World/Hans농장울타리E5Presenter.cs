using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 한스 농장 첫 사건의 권위 상태를 최소 판독 가능한 World 객체로 투영한다.
    /// 애니메이션은 E6 책임이며 이 Component는 Simulation 상태를 변경하지 않는다.
    /// </summary>
    public sealed class Hans농장울타리E5Presenter : MonoBehaviour
    {
        public const string PresentationRevision =
            "hans-farm-fence-restoration.presentation-e5.r1";
        public const string AnimationRole = "NotApplicable";
        public const string BrokenAxeVisualKey =
            "visual:nature:hans-broken-axe:e5-fallback";
        public const string DamagedFenceVisualKey =
            "visual:nature:hans-fence:damaged:e5-fallback";
        public const string RepairedFenceVisualKey =
            "visual:nature:hans-fence:repaired:e5-fallback";

        [SerializeField] private Transform worldPlacementRoot = null!;
        [SerializeField] private GameObject? fenceSegmentPrefab;
        [SerializeField] private GameObject? brokenAxePrefab;

        private readonly List<GameObject> segmentObjects = new();
        private GameObject? brokenAxeObject;

        public IReadOnlyList<GameObject> SegmentObjects => segmentObjects;
        public GameObject? BrokenAxeObject => brokenAxeObject;
        public string RenderedFenceStateCode { get; private set; } = string.Empty;

        public void Configure(Transform placementRoot,
            GameObject? segmentPrefab = null, GameObject? axePrefab = null)
        {
            worldPlacementRoot = placementRoot != null
                ? placementRoot
                : throw new ArgumentNullException(nameof(placementRoot));
            fenceSegmentPrefab = segmentPrefab;
            brokenAxePrefab = axePrefab;
        }

        public void Render(SimulationNatureSurvivalStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var source = state.HansFarmFenceRestoration
                ?? throw new InvalidOperationException(
                    "HansFarmFenceRestorationStateMissing");
            ValidateSource(state, source);
            if (worldPlacementRoot == null)
                worldPlacementRoot = transform;

            ClearChildren();
            RenderedFenceStateCode = source.FenceStateCode;
            foreach (var segment in source.Segments.OrderBy(value =>
                         value.SegmentStableId, StringComparer.Ordinal))
                segmentObjects.Add(CreateFenceSegment(segment));

            if (source.BrokenAxeStateCode ==
                SimulationNatureSurvivalCodes.Available)
                brokenAxeObject = CreateBrokenAxe(source);
        }

        private static void ValidateSource(
            SimulationNatureSurvivalStateSnapshot state,
            SimulationHansFarmFenceRestorationSnapshot source)
        {
            RequireEqual(state.ProfileRevision,
                SimulationNatureSurvivalCodes.ProfileRevisionR6,
                "HansFarmFenceProfileRevisionMismatch");
            RequireEqual(source.FenceAggregateStableId,
                SimulationNatureSurvivalCodes.HansFarmFenceAggregateStableId,
                "HansFarmFenceAggregateStableIdMismatch");
            RequireEqual(source.H2StableId,
                SimulationNatureSurvivalCodes.HansFarmFenceH2StableId,
                "HansFarmFenceH2StableIdMismatch");
            RequireEqual(source.H1StableId,
                SimulationNatureSurvivalCodes.HansFarmFenceH1StableId,
                "HansFarmFenceH1StableIdMismatch");
            RequireEqual(source.BrokenAxePickupStableId,
                SimulationNatureSurvivalCodes.HansBrokenAxePickupStableId,
                "HansBrokenAxePickupStableIdMismatch");
            RequireState(source.FenceStateCode,
                SimulationNatureSurvivalCodes.Damaged,
                SimulationNatureSurvivalCodes.Repaired,
                "HansFarmFenceStateInvalid");
            RequireState(source.BrokenAxeStateCode,
                SimulationNatureSurvivalCodes.Available,
                SimulationNatureSurvivalCodes.Carried,
                "HansBrokenAxeStateInvalid");
            if (source.RequiredTimberQuantity != 2)
                throw new InvalidOperationException(
                    "HansFarmFenceTimberRequirementMismatch");
            if (source.Segments == null || source.Segments.Length != 3)
                throw new InvalidOperationException(
                    "HansFarmFenceSegmentCountMismatch");

            var expectedIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "fence-segment:hans-farm:first-incident:01",
                "fence-segment:hans-farm:first-incident:02",
                "fence-segment:hans-farm:first-incident:03",
            };
            foreach (var segment in source.Segments)
            {
                if (segment == null || !expectedIds.Remove(
                        segment.SegmentStableId))
                    throw new InvalidOperationException(
                        "HansFarmFenceSegmentStableIdMismatch");
                RequireEqual(segment.StateCode, source.FenceStateCode,
                    "HansFarmFenceSegmentStateMismatch");
                if (!IsFinite(segment.LocalX) || !IsFinite(segment.LocalZ)
                    || !IsFinite(segment.YawDegrees))
                    throw new InvalidOperationException(
                        "HansFarmFenceSegmentTransformInvalid");
            }
            if (expectedIds.Count != 0)
                throw new InvalidOperationException(
                    "HansFarmFenceSegmentStableIdMismatch");
        }

        private static void RequireEqual(string actual, string expected,
            string errorCode)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidOperationException(errorCode);
        }

        private static void RequireState(string actual, string first,
            string second, string errorCode)
        {
            if (!string.Equals(actual, first, StringComparison.Ordinal)
                && !string.Equals(actual, second, StringComparison.Ordinal))
                throw new InvalidOperationException(errorCode);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private GameObject CreateFenceSegment(
            SimulationHansFarmFenceSegmentSnapshot source)
        {
            var instance = fenceSegmentPrefab != null
                ? Instantiate(fenceSegmentPrefab, worldPlacementRoot)
                : CreateFenceFallback(worldPlacementRoot);
            instance.name = source.SegmentStableId;
            instance.transform.localPosition = new Vector3(
                (float)source.LocalX, 0.65f, (float)source.LocalZ);
            instance.transform.localRotation = Quaternion.Euler(0f,
                (float)source.YawDegrees, 0f);
            EnsureCollider(instance);
            var marker = instance.GetComponent<Hans농장울타리E5표식>()
                ?? instance.AddComponent<Hans농장울타리E5표식>();
            marker.Bind(source.SegmentStableId,
                source.StateCode == SimulationNatureSurvivalCodes.Repaired
                    ? RepairedFenceVisualKey : DamagedFenceVisualKey,
                SimulationNatureSurvivalCodes
                    .RepairHansFarmFenceWorldInteractionId,
                "FenceSegment");
            return instance;
        }

        private GameObject CreateBrokenAxe(
            SimulationHansFarmFenceRestorationSnapshot source)
        {
            var instance = brokenAxePrefab != null
                ? Instantiate(brokenAxePrefab, worldPlacementRoot)
                : CreateBrokenAxeFallback(worldPlacementRoot);
            instance.name = source.BrokenAxePickupStableId;
            instance.transform.localPosition = new Vector3(-3.1f, 0.14f, 0.9f);
            instance.transform.localRotation = Quaternion.Euler(8f, 24f, 82f);
            EnsureCollider(instance);
            var marker = instance.GetComponent<Hans농장울타리E5표식>()
                ?? instance.AddComponent<Hans농장울타리E5표식>();
            marker.Bind(source.BrokenAxePickupStableId, BrokenAxeVisualKey,
                SimulationNatureSurvivalCodes
                    .AcquireHansBrokenAxeWorldInteractionId,
                "BrokenAxePickupAnchor");
            return instance;
        }

        private static GameObject CreateFenceFallback(Transform parent)
        {
            var root = new GameObject("FenceSegmentFallback");
            root.transform.SetParent(parent, false);
            var upper = CreateCube("UpperRail", root.transform,
                new Vector3(0f, 0.32f, 0f), new Vector3(2.2f, 0.16f, 0.18f));
            var lower = CreateCube("LowerRail", root.transform,
                new Vector3(0f, -0.25f, 0f), new Vector3(2.2f, 0.16f, 0.18f));
            CreateCube("LeftPost", root.transform,
                new Vector3(-0.95f, 0f, 0f), new Vector3(0.18f, 1.3f, 0.18f));
            CreateCube("RightPost", root.transform,
                new Vector3(0.95f, 0f, 0f), new Vector3(0.18f, 1.3f, 0.18f));
            upper.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            lower.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            return root;
        }

        private static GameObject CreateBrokenAxeFallback(Transform parent)
        {
            var root = new GameObject("BrokenAxeFallback");
            root.transform.SetParent(parent, false);
            CreateCube("BrokenHandle", root.transform,
                new Vector3(-0.18f, 0f, 0f), new Vector3(0.55f, 0.09f, 0.09f));
            CreateCube("AxeHead", root.transform,
                new Vector3(0.18f, 0.02f, 0f), new Vector3(0.22f, 0.32f, 0.1f));
            return root;
        }

        private static GameObject CreateCube(string name, Transform parent,
            Vector3 localPosition, Vector3 localScale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            return cube;
        }

        private static void EnsureCollider(GameObject value)
        {
            if (value.GetComponentsInChildren<Collider>(true).Length > 0)
                return;
            var renderers = value.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("VisibleRendererMissing");
            var localBounds = LocalBounds(value.transform, renderers[0].bounds);
            for (var index = 1; index < renderers.Length; index++)
                localBounds.Encapsulate(LocalBounds(value.transform,
                    renderers[index].bounds));
            var collider = value.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }

        private static Bounds LocalBounds(Transform root, Bounds worldBounds)
        {
            var min = worldBounds.min;
            var max = worldBounds.max;
            var local = new Bounds(root.InverseTransformPoint(min),
                Vector3.zero);
            for (var mask = 1; mask < 8; mask++)
            {
                var corner = new Vector3(
                    (mask & 1) == 0 ? min.x : max.x,
                    (mask & 2) == 0 ? min.y : max.y,
                    (mask & 4) == 0 ? min.z : max.z);
                local.Encapsulate(root.InverseTransformPoint(corner));
            }
            return local;
        }

        private void ClearChildren()
        {
            foreach (var value in segmentObjects)
                DestroyObject(value);
            segmentObjects.Clear();
            if (brokenAxeObject != null) DestroyObject(brokenAxeObject);
            brokenAxeObject = null;
        }

        private static void DestroyObject(GameObject value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }

    public sealed class Hans농장울타리E5표식 : MonoBehaviour
    {
        public string StableId { get; private set; } = string.Empty;
        public string VisualKey { get; private set; } = string.Empty;
        public string WorldInteractionId { get; private set; } = string.Empty;
        public string PlacementRole { get; private set; } = string.Empty;

        public void Bind(string stableId, string visualKey,
            string worldInteractionId, string placementRole)
        {
            StableId = stableId;
            VisualKey = visualKey;
            WorldInteractionId = worldInteractionId;
            PlacementRole = placementRole;
        }
    }
}
