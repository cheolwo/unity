using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 음식배달JourneyVisualState
    {
        public bool OverviewMarkerVisible { get; internal set; }
        public bool ActorVisible { get; internal set; }
        public bool VehicleVisible { get; internal set; }
        public Vector3 ActorPosition { get; internal set; }
        public Vector3 VehiclePosition { get; internal set; }
    }

    /// <summary>
    /// 서버가 확정한 음식 배달 경로를 사가정 지역 Root의 로컬 좌표로 재생한다.
    /// 경로 node마다 GameObject를 만들지 않고, 도착·픽업·전달 Command도 호출하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 음식배달JourneyPlaybackLayer : MonoBehaviour
    {
        public const float DefaultOverviewThreshold = 220f;
        public const float DefaultPerspectiveOverviewDistance = 70f;

        [SerializeField] private Camera observationCamera = null!;
        [SerializeField] private Transform mobilityRoot = null!;
        [SerializeField] private GameObject overviewMarkerPrefab = null!;
        [SerializeField] private GameObject actorPrefab = null!;
        [SerializeField] private GameObject vehiclePrefab = null!;
        [SerializeField] private Material selectedRouteMaterial = null!;
        [SerializeField] private float overviewThreshold = DefaultOverviewThreshold;
        [SerializeField] private float perspectiveOverviewDistance =
            DefaultPerspectiveOverviewDistance;
        [SerializeField] private float routeWidth = 1.4f;
        [SerializeField] private float routeElevation = .34f;
        [SerializeField, Min(0)] private int maxPoolSize = 32;

        private readonly Dictionary<string, JourneyVisual> active =
            new(StringComparer.Ordinal);
        private readonly Stack<JourneyVisual> pooled = new();
        private string selectedRouteStableId = string.Empty;
        private LineRenderer selectedRouteLine;
        private Material ownedRouteMaterial;
        private int createdVisualCount;
        private bool configured;

        public int ActiveVisualCount => active.Count;
        public int PooledVisualCount => pooled.Count;
        public int CreatedVisualCount => createdVisualCount;
        public int RouteLinePositionCount => selectedRouteLine == null
            ? 0 : selectedRouteLine.positionCount;
        public int RoutePointGameObjectCount => 0;
        public string SelectedRouteStableId => selectedRouteStableId;
        public bool SelectedRouteVisible => selectedRouteLine != null
                                            && selectedRouteLine.gameObject.activeInHierarchy;

        public void Configure(
            Camera camera,
            Transform root,
            GameObject marker = null,
            GameObject actor = null,
            GameObject vehicle = null,
            Material routeMaterial = null,
            float overviewSize = DefaultOverviewThreshold,
            float perspectiveDistance = DefaultPerspectiveOverviewDistance,
            int poolCapacity = 32)
        {
            if (configured || active.Count > 0 || pooled.Count > 0
                || selectedRouteLine != null)
                throw new InvalidOperationException(
                    "FoodDeliveryJourneyPlaybackAlreadyConfigured");
            if (root == null || !float.IsFinite(overviewSize) || overviewSize <= 0f
                || !float.IsFinite(perspectiveDistance) || perspectiveDistance <= 0f
                || poolCapacity < 0)
                throw new ArgumentException("FoodDeliveryJourneyPlaybackConfigurationInvalid");
            observationCamera = camera;
            mobilityRoot = root;
            overviewMarkerPrefab = marker;
            actorPrefab = actor;
            vehiclePrefab = vehicle;
            selectedRouteMaterial = routeMaterial;
            overviewThreshold = overviewSize;
            perspectiveOverviewDistance = perspectiveDistance;
            maxPoolSize = poolCapacity;
            configured = true;
        }

        public void Apply(음식배달JourneyPresentationModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RouteStableId)
                || model.RoutePoints == null || model.RoutePoints.Count < 2)
            {
                throw new ArgumentException("FoodDeliveryJourneyPresentationModelInvalid",
                    nameof(model));
            }
            EnsureMobilityRoot();
            if (!active.TryGetValue(model.RouteStableId, out var visual))
            {
                visual = Acquire();
                active.Add(model.RouteStableId, visual);
            }
            visual.Root.name = "음식 배달 이동 · " + model.RouteStableId;
            visual.Root.SetActive(isActiveAndEnabled);
            visual.Model = model;
            ApplyPositions(visual);
            RefreshVisibilityFromCamera(visual);
            if (selectedRouteStableId == model.RouteStableId)
                ApplySelectedRoute(model);
        }

        public bool Remove(string routeStableId)
        {
            if (string.IsNullOrWhiteSpace(routeStableId)
                || !active.Remove(routeStableId, out var visual))
                return false;
            visual.Model = null;
            visual.Root.SetActive(false);
            if (pooled.Count < maxPoolSize)
                pooled.Push(visual);
            else
                ReleaseObject(visual.Root);
            if (selectedRouteStableId == routeStableId)
                SetSelectedRoute(string.Empty);
            return true;
        }

        public void SetSelectedRoute(string routeStableId)
        {
            selectedRouteStableId = routeStableId?.Trim() ?? string.Empty;
            if (selectedRouteStableId.Length == 0
                || !active.TryGetValue(selectedRouteStableId, out var visual)
                || visual.Model == null)
            {
                if (selectedRouteLine != null)
                {
                    selectedRouteLine.positionCount = 0;
                    selectedRouteLine.gameObject.SetActive(false);
                }
                return;
            }
            ApplySelectedRoute(visual.Model);
        }

        public void ClearAll()
        {
            foreach (var routeStableId in active.Keys.ToArray())
                Remove(routeStableId);
            selectedRouteStableId = string.Empty;
            if (selectedRouteLine != null)
            {
                selectedRouteLine.positionCount = 0;
                selectedRouteLine.gameObject.SetActive(false);
            }
        }

        public void ClearPool()
        {
            while (pooled.Count > 0)
                ReleaseObject(pooled.Pop().Root);
        }

        public void RefreshVisibilityForTests(float orthographicSize)
        {
            if (!float.IsFinite(orthographicSize) || orthographicSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(orthographicSize));
            foreach (var visual in active.Values)
                RefreshVisibility(visual, orthographicSize);
        }

        public void RefreshVisibilityFromCameraForTests()
        {
            foreach (var visual in active.Values)
                RefreshVisibilityFromCamera(visual);
        }

        public bool TryGetVisualState(string routeStableId,
            out 음식배달JourneyVisualState state)
        {
            state = null;
            if (!active.TryGetValue(routeStableId ?? string.Empty, out var visual))
                return false;
            state = new 음식배달JourneyVisualState
            {
                OverviewMarkerVisible = visual.Marker.activeInHierarchy,
                ActorVisible = visual.Actor.activeInHierarchy,
                VehicleVisible = visual.Vehicle.activeInHierarchy,
                ActorPosition = visual.Actor.transform.localPosition,
                VehiclePosition = visual.Vehicle.transform.localPosition,
            };
            return true;
        }

        private void Update()
        {
            foreach (var visual in active.Values)
                RefreshVisibilityFromCamera(visual);
        }

        private void ApplyPositions(JourneyVisual visual)
        {
            var model = visual.Model;
            var current = Local(model.CurrentPosition, 0f);
            var direction = new Vector3((float)model.CurrentDirection.X, 0f,
                (float)model.CurrentDirection.Z);
            var rotation = direction.sqrMagnitude > .000001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;

            visual.Marker.transform.SetLocalPositionAndRotation(
                current + Vector3.up * 3.2f, rotation);
            visual.Actor.transform.SetLocalPositionAndRotation(
                current + Vector3.up * .18f, rotation);
            var vehiclePosition = model.CurrentModeCode
                                  == 음식배달JourneyWireCodes.Pedestrian
                                  && model.HasVehiclePosition
                ? Local(model.VehiclePosition, .28f)
                : current + Vector3.up * .28f;
            visual.Vehicle.transform.SetLocalPositionAndRotation(vehiclePosition, rotation);
        }

        private void RefreshVisibility(JourneyVisual visual, float orthographicSize)
        {
            if (visual.Model == null) return;
            ApplyVisibility(visual, orthographicSize >= overviewThreshold);
        }

        private void RefreshVisibilityFromCamera(JourneyVisual visual)
        {
            if (visual.Model == null) return;
            var overview = observationCamera == null
                || (observationCamera.orthographic
                    ? observationCamera.orthographicSize >= overviewThreshold
                    : Vector3.Distance(observationCamera.transform.position,
                        mobilityRoot.TransformPoint(Local(visual.Model.CurrentPosition, 0f)))
                      >= perspectiveOverviewDistance);
            ApplyVisibility(visual, overview);
        }

        private static void ApplyVisibility(JourneyVisual visual, bool overview)
        {
            visual.Marker.SetActive(overview);
            visual.Actor.SetActive(!overview);
            visual.Vehicle.SetActive(!overview
                && (visual.Model.CurrentModeCode != 음식배달JourneyWireCodes.Pedestrian
                    || visual.Model.HasVehiclePosition));
        }

        private JourneyVisual Acquire()
        {
            if (pooled.Count > 0)
            {
                var reused = pooled.Pop();
                reused.Root.transform.SetParent(mobilityRoot, false);
                return reused;
            }

            var root = new GameObject("음식 배달 이동 · pooled");
            root.transform.SetParent(mobilityRoot, false);
            var marker = CreateVisual(overviewMarkerPrefab, "전체뷰 배달 기호",
                PrimitiveType.Cylinder, new Vector3(7f, .5f, 7f), root.transform);
            var actor = CreateVisual(actorPrefab, "확대 배달 기사",
                PrimitiveType.Capsule, new Vector3(1f, 1f, 1f), root.transform);
            var vehicle = CreateVisual(vehiclePrefab, "확대 배달 스쿠터",
                PrimitiveType.Cube, new Vector3(2.4f, .7f, 1.1f), root.transform);
            createdVisualCount++;
            return new JourneyVisual(root, marker, actor, vehicle);
        }

        private GameObject CreateVisual(GameObject prefab, string name,
            PrimitiveType fallback, Vector3 fallbackScale, Transform parent)
        {
            var value = prefab == null
                ? GameObject.CreatePrimitive(fallback)
                : Instantiate(prefab);
            value.name = name;
            value.transform.SetParent(parent, false);
            if (prefab == null) value.transform.localScale = fallbackScale;
            ApplyLayer(value.transform, mobilityRoot.gameObject.layer);
            foreach (var collider in value.GetComponentsInChildren<Collider>(true))
                ReleaseObject(collider);
            return value;
        }

        private void ApplySelectedRoute(음식배달JourneyPresentationModel model)
        {
            EnsureRouteLine();
            var points = new Vector3[model.RoutePoints.Count];
            for (var index = 0; index < points.Length; index++)
                points[index] = Local(model.RoutePoints[index], routeElevation);
            selectedRouteLine.positionCount = points.Length;
            selectedRouteLine.SetPositions(points);
            selectedRouteLine.gameObject.SetActive(isActiveAndEnabled);
        }

        private void EnsureRouteLine()
        {
            EnsureMobilityRoot();
            if (selectedRouteLine != null) return;
            var root = new GameObject("선택한 배달 경로")
            {
                layer = mobilityRoot.gameObject.layer,
            };
            root.transform.SetParent(mobilityRoot, false);
            selectedRouteLine = root.AddComponent<LineRenderer>();
            selectedRouteLine.useWorldSpace = false;
            selectedRouteLine.loop = false;
            selectedRouteLine.widthMultiplier = routeWidth;
            selectedRouteLine.numCornerVertices = 2;
            selectedRouteLine.numCapVertices = 2;
            selectedRouteLine.startColor = new Color(.98f, .68f, .18f, .95f);
            selectedRouteLine.endColor = new Color(.98f, .42f, .16f, .95f);
            if (selectedRouteMaterial != null)
            {
                selectedRouteLine.sharedMaterial = selectedRouteMaterial;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    ownedRouteMaterial = new Material(shader)
                    {
                        name = "선택한 배달 경로 · runtime",
                        color = Color.white,
                    };
                    selectedRouteLine.sharedMaterial = ownedRouteMaterial;
                }
            }
        }

        private void EnsureMobilityRoot()
        {
            if (mobilityRoot != null) return;
            mobilityRoot = new GameObject("Mobility · 음식 배달 이동").transform;
            mobilityRoot.SetParent(transform, false);
        }

        private static Vector3 Local(음식배달JourneyPoint value, float y)
            => new((float)value.X, y, (float)value.Z);

        private static void ApplyLayer(Transform root, int layer)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true))
                value.gameObject.layer = layer;
        }

        private static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDisable()
        {
            foreach (var visual in active.Values)
                visual.Root.SetActive(false);
            if (selectedRouteLine != null)
                selectedRouteLine.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            foreach (var visual in active.Values)
            {
                visual.Root.SetActive(true);
                ApplyPositions(visual);
                RefreshVisibilityFromCamera(visual);
            }
            if (selectedRouteStableId.Length > 0
                && active.TryGetValue(selectedRouteStableId, out var selected)
                && selected.Model != null)
            {
                ApplySelectedRoute(selected.Model);
            }
        }

        private void OnDestroy()
        {
            ClearAll();
            ClearPool();
            if (selectedRouteLine != null)
                ReleaseObject(selectedRouteLine.gameObject);
            if (ownedRouteMaterial != null)
                ReleaseObject(ownedRouteMaterial);
            selectedRouteLine = null;
            ownedRouteMaterial = null;
        }

        private sealed class JourneyVisual
        {
            public JourneyVisual(GameObject root, GameObject marker,
                GameObject actor, GameObject vehicle)
            {
                Root = root;
                Marker = marker;
                Actor = actor;
                Vehicle = vehicle;
            }

            public GameObject Root { get; }
            public GameObject Marker { get; }
            public GameObject Actor { get; }
            public GameObject Vehicle { get; }
            public 음식배달JourneyPresentationModel Model { get; set; }
        }
    }
}
