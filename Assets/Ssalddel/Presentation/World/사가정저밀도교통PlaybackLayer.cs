using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 사가정저밀도교통ActorVisualState
    {
        public string KindCode { get; internal set; } = string.Empty;
        public string StateCode { get; internal set; } = string.Empty;
        public string DisplayPolylineOrderCode { get; internal set; } = string.Empty;
        public Vector3 LocalPosition { get; internal set; }
        public bool OverviewMarkerVisible { get; internal set; }
        public bool DetailVisible { get; internal set; }
        public EntityId VisualInstanceId { get; internal set; }
    }

    public sealed class 사가정저밀도교통RouteLegVisualState
    {
        public string StableId { get; internal set; } = string.Empty;
        public string LaneStableId { get; internal set; } = string.Empty;
        public string StateCode { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// 검증된 합성 교통 상태 사본만 사가정 로컬 좌표에 표현한다.
    /// OSM 중심선은 관찰선이고 offset 차로·신호 주기·차량은 모두 합성 표시다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 사가정저밀도교통PlaybackLayer : MonoBehaviour
    {
        public const float DefaultOverviewThreshold = 220f;
        public const int DefaultPoolCapacity = 8;

        [SerializeField] private Camera observationCamera = null!;
        [SerializeField] private Transform trafficRoot = null!;
        [SerializeField] private float overviewThreshold = DefaultOverviewThreshold;
        [SerializeField, Min(0)] private int maxPoolSize = DefaultPoolCapacity;

        private readonly Dictionary<string, ActorVisual> activeActors =
            new(StringComparer.Ordinal);
        private readonly Stack<ActorVisual> pooledActors = new();
        private readonly List<SignalVisual> signals = new();
        private readonly Dictionary<string, RouteVisual> routeVisuals =
            new(StringComparer.Ordinal);
        private readonly List<Material> ownedMaterials = new();
        private readonly Dictionary<string, Material> materialByKey =
            new(StringComparer.Ordinal);
        private Transform staticRoot;
        private Transform dynamicRoot;
        private GameObject candidateEndpointMarker;
        private bool configured;
        private bool geometryBuilt;
        private string geometryLaneGraphHash = string.Empty;
        private long appliedRevision = -1;

        public int ActiveActorCount => activeActors.Count;
        public int PooledActorCount => pooledActors.Count;
        public int CreatedActorCount { get; private set; }
        public int ObservationLineCount { get; private set; }
        public int SyntheticLaneLineCount { get; private set; }
        public int CourierRouteLineCount => routeVisuals.Count;
        public int StopLineCount { get; private set; }
        public int CrosswalkStripeCount { get; private set; }
        public int SignalHeadCount => signals.Count;
        public bool StaticGeometryReady => geometryBuilt;
        public long AppliedRevision => appliedRevision;
        public string CurrentRouteLegStableId { get; private set; } = string.Empty;
        public double CourierRouteCumulativeProgressMeters { get; private set; }
        public double CourierRouteTotalDistanceMeters { get; private set; }
        public bool CandidateEndpointMarkerVisible =>
            candidateEndpointMarker != null && candidateEndpointMarker.activeInHierarchy;
        public bool CandidateEndpointReached { get; private set; }

        public void Configure(Camera camera, Transform root,
            float overviewSize = DefaultOverviewThreshold,
            int poolCapacity = DefaultPoolCapacity)
        {
            if (configured || activeActors.Count > 0 || pooledActors.Count > 0
                || staticRoot != null || dynamicRoot != null)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficPlaybackAlreadyConfigured");
            if (root == null || !float.IsFinite(overviewSize) || overviewSize <= 0f
                || poolCapacity < 0)
                throw new ArgumentException(
                    "SagajeongLowDensityTrafficPlaybackConfigurationInvalid");
            observationCamera = camera;
            trafficRoot = root;
            overviewThreshold = overviewSize;
            maxPoolSize = poolCapacity;
            configured = true;
        }

        public void Apply(사가정저밀도교통PresentationModel model)
        {
            if (!configured || model == null || model.Actors == null
                || model.Actors.Count < 1
                || model.Actors.Count > 사가정저밀도교통Fixture.MaximumActorCount
                || model.Signals == null || model.Signals.Count != 2
                || model.Lanes == null
                || model.Lanes.Count != 사가정저밀도교통Fixture.ExpectedLaneCount
                || model.CourierRouteLegs == null
                || model.CourierRouteLegs.Count !=
                사가정저밀도교통Fixture.ExpectedRouteLegCount
                || model.CourierRouteState == null)
                throw new ArgumentException(
                    "SagajeongLowDensityTrafficPresentationModelInvalid", nameof(model));
            EnsureRoots();
            EnsureStaticGeometry(model);
            if (geometryLaneGraphHash != model.LaneGraphHash)
                throw new ArgumentException(
                    "SagajeongLowDensityTrafficLaneGraphChanged", nameof(model));
            ApplySignals(model);
            ApplyCourierRoute(model);

            var incomingIds = new HashSet<string>(
                model.Actors.Select(value => value.StableId), StringComparer.Ordinal);
            foreach (var stale in activeActors.Keys.Where(id => !incomingIds.Contains(id))
                         .ToArray())
                RemoveActor(stale);
            foreach (var actor in model.Actors)
            {
                if (!activeActors.TryGetValue(actor.StableId, out var visual))
                {
                    visual = AcquireActor();
                    activeActors.Add(actor.StableId, visual);
                }
                visual.Model = actor;
                visual.Root.name = "합성 교통 주체 · " + actor.StableId;
                visual.Root.SetActive(isActiveAndEnabled);
                ApplyActor(visual);
                RefreshVisibility(visual);
            }
            appliedRevision = model.Revision;
            var presentationVisible = isActiveAndEnabled;
            staticRoot.gameObject.SetActive(presentationVisible);
            dynamicRoot.gameObject.SetActive(presentationVisible);
        }

        public void ClearDynamic()
        {
            foreach (var stableId in activeActors.Keys.ToArray())
                RemoveActor(stableId);
            appliedRevision = -1;
        }

        public void ClearPool()
        {
            while (pooledActors.Count > 0)
                ReleaseObject(pooledActors.Pop().Root);
        }

        public void ClearAll()
        {
            ClearDynamic();
            ClearPool();
            if (staticRoot != null) ReleaseObject(staticRoot.gameObject);
            if (dynamicRoot != null) ReleaseObject(dynamicRoot.gameObject);
            staticRoot = null;
            dynamicRoot = null;
            candidateEndpointMarker = null;
            geometryBuilt = false;
            geometryLaneGraphHash = string.Empty;
            signals.Clear();
            routeVisuals.Clear();
            ObservationLineCount = 0;
            SyntheticLaneLineCount = 0;
            StopLineCount = 0;
            CrosswalkStripeCount = 0;
            CurrentRouteLegStableId = string.Empty;
            CourierRouteCumulativeProgressMeters = 0d;
            CourierRouteTotalDistanceMeters = 0d;
            CandidateEndpointReached = false;
        }

        public void RefreshVisibilityForTests(float orthographicSize)
        {
            if (!float.IsFinite(orthographicSize) || orthographicSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(orthographicSize));
            var overview = orthographicSize >= overviewThreshold;
            foreach (var visual in activeActors.Values)
                ApplyVisibility(visual, overview);
        }

        public bool TryGetActorVisualState(string stableId,
            out 사가정저밀도교통ActorVisualState state)
        {
            state = null;
            if (!activeActors.TryGetValue(stableId ?? string.Empty, out var visual)
                || visual.Model == null)
                return false;
            state = new 사가정저밀도교통ActorVisualState
            {
                KindCode = visual.Model.KindCode,
                StateCode = visual.Model.StateCode,
                DisplayPolylineOrderCode = visual.Model.DisplayPolylineOrderCode,
                LocalPosition = visual.Root.transform.localPosition,
                OverviewMarkerVisible = visual.Marker.activeInHierarchy,
                DetailVisible = visual.Detail.activeInHierarchy,
                VisualInstanceId = visual.Root.GetEntityId(),
            };
            return true;
        }

        public bool TryGetRouteLegVisualState(string stableId,
            out 사가정저밀도교통RouteLegVisualState state)
        {
            state = null;
            if (!routeVisuals.TryGetValue(stableId ?? string.Empty, out var visual))
                return false;
            state = new 사가정저밀도교통RouteLegVisualState
            {
                StableId = visual.StableId,
                LaneStableId = visual.LaneStableId,
                StateCode = visual.StateCode,
            };
            return true;
        }

        public bool TryGetSignalAspect(string axisCode, out string aspectCode)
        {
            var found = signals.FirstOrDefault(value => value.AxisCode == axisCode);
            aspectCode = found?.AspectCode ?? string.Empty;
            return found != null;
        }

        private void Update()
        {
            foreach (var actor in activeActors.Values) RefreshVisibility(actor);
        }

        private void OnEnable()
        {
            if (staticRoot != null) staticRoot.gameObject.SetActive(true);
            if (dynamicRoot != null) dynamicRoot.gameObject.SetActive(true);
            foreach (var actor in activeActors.Values)
            {
                actor.Root.SetActive(true);
                RefreshVisibility(actor);
            }
        }

        private void OnDisable()
        {
            if (staticRoot != null) staticRoot.gameObject.SetActive(false);
            if (dynamicRoot != null) dynamicRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            activeActors.Clear();
            pooledActors.Clear();
            signals.Clear();
            routeVisuals.Clear();
            foreach (var material in ownedMaterials)
                if (material != null) ReleaseObject(material);
            ownedMaterials.Clear();
            materialByKey.Clear();
        }

        private void EnsureRoots()
        {
            if (trafficRoot == null)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficRootMissing");
            if (staticRoot == null)
            {
                staticRoot = NewObject("정적 교통 관찰과 합성 차로", trafficRoot).transform;
            }
            if (dynamicRoot == null)
            {
                dynamicRoot = NewObject("동적 저밀도 합성 교통", trafficRoot).transform;
            }
        }

        private void EnsureStaticGeometry(사가정저밀도교통PresentationModel model)
        {
            if (geometryBuilt) return;
            var lanes = model.Lanes.ToDictionary(value => value.StableId,
                StringComparer.Ordinal);
            if (!lanes.TryGetValue(사가정저밀도교통WireCodes.EastWestLane,
                    out var main)
                || !lanes.TryGetValue(사가정저밀도교통WireCodes.NorthSouthLane,
                    out var cross)
                || !lanes.TryGetValue(사가정저밀도교통WireCodes.NeighborhoodAccessLane,
                    out var neighborhood)
                || !lanes.TryGetValue(사가정저밀도교통WireCodes.AlleyCandidateLane,
                    out var alley))
                throw new ArgumentException("SagajeongLowDensityTrafficLaneMissing");

            CreateLine("수신 OSM 표시선 · 대로 · " + main.SourceWayStableId,
                main.Points, .24f, .20f,
                ColorFor("osm-main", new Color(.23f, .56f, .80f, .90f)));
            CreateLine("수신 OSM 표시선 · 교차로 · " + cross.SourceWayStableId,
                cross.Points, .24f, .20f,
                ColorFor("osm-cross", new Color(.23f, .56f, .80f, .90f)));
            CreateLine("수신 OSM 생활도로 후보 · PendingHumanReview · RuntimeAuthorized=false",
                neighborhood.Points, .44f, .26f,
                ColorFor("neighborhood", new Color(.98f, .65f, .16f, .88f)));
            CreateLine("수신 OSM 골목 후보 · PendingHumanReview · RuntimeAuthorized=false",
                alley.Points, .62f, .30f,
                ColorFor("alley", new Color(1f, .42f, .08f, .98f)));
            ObservationLineCount = model.Lanes.Count;

            CreateLine("합성 우측차로 · 대로 표시 순서",
                Offset(main.Points, 2.15d), .33f, .28f,
                ColorFor("lane", new Color(.98f, .88f, .36f, .92f)));
            CreateLine("합성 우측차로 · 대로 역표시 순서",
                Offset(Reverse(main.Points), 2.15d), .33f,
                .28f, ColorFor("lane", new Color(.98f, .88f, .36f, .92f)));
            CreateLine("합성 우측차로 · 남북 표시 순서",
                Offset(cross.Points, 2.15d), .33f,
                .28f, ColorFor("lane", new Color(.98f, .88f, .36f, .92f)));
            CreateLine("합성 우측차로 · 남북 역표시 순서",
                Offset(Reverse(cross.Points), 2.15d),
                .33f, .28f, ColorFor("lane", new Color(.98f, .88f, .36f, .92f)));
            SyntheticLaneLineCount = 4;

            foreach (var leg in model.CourierRouteLegs.OrderBy(value => value.Index))
            {
                var lane = lanes[leg.LaneStableId];
                var line = CreateLine("합성 배달 표시 경로 · " + leg.StageCode
                                      + " · 통행 승인 아님",
                    lane.Points, .88f, .48f,
                    ColorFor("route-pending", new Color(.40f, .44f, .50f, .72f)));
                routeVisuals.Add(leg.StableId,
                    new RouteVisual(leg.StableId, leg.LaneStableId, leg.Index, line));
            }

            var endpoint = alley.Points[alley.Points.Count - 1];
            candidateEndpointMarker = CreatePrimitive(staticRoot,
                "후보 종점 · 배송 완료 아님", PrimitiveType.Cylinder,
                new Vector3(3.6f, .18f, 3.6f),
                Point(endpoint, .62f),
                ColorFor("candidate-endpoint", new Color(.95f, .22f, .58f, 1f)));

            if (!TrySharedPoint(main.Points, cross.Points, out var intersection))
                throw new ArgumentException("SagajeongLowDensityTrafficIntersectionMissing");
            var center = Point(intersection, .32f);
            var mainDirection = Direction(main.Points[0],
                main.Points[main.Points.Count - 1]);
            var crossDirection = Direction(cross.Points[0],
                cross.Points[cross.Points.Count - 1]);
            CreateStopLine("합성 정지선 · 서측", center - mainDirection * 8f,
                mainDirection);
            CreateStopLine("합성 정지선 · 동측", center + mainDirection * 8f,
                mainDirection);
            CreateStopLine("합성 정지선 · 남측", center - crossDirection * 8f,
                crossDirection);
            CreateStopLine("합성 정지선 · 북측", center + crossDirection * 8f,
                crossDirection);
            StopLineCount = 4;

            CreateCrosswalk("합성 횡단보도 · 서측", center - mainDirection * 5f,
                mainDirection);
            CreateCrosswalk("합성 횡단보도 · 동측", center + mainDirection * 5f,
                mainDirection);
            CreateCrosswalk("합성 횡단보도 · 남측", center - crossDirection * 5f,
                crossDirection);
            CreateCrosswalk("합성 횡단보도 · 북측", center + crossDirection * 5f,
                crossDirection);

            CreateSignalHead("남북 신호 · 북서", 사가정저밀도교통WireCodes.NorthSouth,
                center + new Vector3(-7f, 0f, 7f), Quaternion.Euler(0f, 180f, 0f));
            CreateSignalHead("남북 신호 · 남동", 사가정저밀도교통WireCodes.NorthSouth,
                center + new Vector3(7f, 0f, -7f), Quaternion.identity);
            CreateSignalHead("동서 신호 · 북동", 사가정저밀도교통WireCodes.EastWest,
                center + new Vector3(7f, 0f, 7f), Quaternion.Euler(0f, -90f, 0f));
            CreateSignalHead("동서 신호 · 남서", 사가정저밀도교통WireCodes.EastWest,
                center + new Vector3(-7f, 0f, -7f), Quaternion.Euler(0f, 90f, 0f));
            geometryLaneGraphHash = model.LaneGraphHash;
            geometryBuilt = true;
        }

        private void ApplyCourierRoute(사가정저밀도교통PresentationModel model)
        {
            var state = model.CourierRouteState;
            CurrentRouteLegStableId = state.CurrentRouteLegStableId;
            CourierRouteCumulativeProgressMeters = state.CumulativeProgressMeters;
            CourierRouteTotalDistanceMeters = state.TotalDistanceMeters;
            CandidateEndpointReached = state.CandidateEndpointReached;
            foreach (var visual in routeVisuals.Values)
            {
                if (visual.Index < state.CurrentRouteLegIndex)
                {
                    visual.StateCode = "Completed";
                    visual.Line.sharedMaterial = ColorFor("route-completed",
                        new Color(.20f, .76f, .48f, .90f));
                }
                else if (visual.Index == state.CurrentRouteLegIndex)
                {
                    visual.StateCode = state.CandidateEndpointReached
                        ? "CandidateEndpointReached" : "Current";
                    visual.Line.sharedMaterial = ColorFor("route-current",
                        new Color(.10f, .90f, 1f, 1f));
                }
                else
                {
                    visual.StateCode = "Pending";
                    visual.Line.sharedMaterial = ColorFor("route-pending",
                        new Color(.40f, .44f, .50f, .72f));
                }
            }
            if (candidateEndpointMarker != null)
            {
                candidateEndpointMarker.SetActive(true);
                candidateEndpointMarker.transform.localScale = state.CandidateEndpointReached
                    ? new Vector3(4.4f, .24f, 4.4f)
                    : new Vector3(3.6f, .18f, 3.6f);
            }
        }

        private void ApplySignals(사가정저밀도교통PresentationModel model)
        {
            foreach (var signal in signals)
            {
                var state = model.Signals.FirstOrDefault(value =>
                    value.AxisCode == signal.AxisCode);
                if (state == null)
                    throw new ArgumentException("SagajeongLowDensityTrafficSignalMissing");
                signal.AspectCode = state.StateCode;
                signal.Red.SetActive(state.StateCode == 사가정저밀도교통WireCodes.Red);
                signal.Amber.SetActive(state.StateCode == 사가정저밀도교통WireCodes.Amber);
                signal.Green.SetActive(state.StateCode == 사가정저밀도교통WireCodes.Green);
            }
        }

        private ActorVisual AcquireActor()
        {
            if (pooledActors.Count > 0)
            {
                var reused = pooledActors.Pop();
                reused.Root.transform.SetParent(dynamicRoot, false);
                return reused;
            }
            var root = NewObject("합성 교통 주체 · pooled", dynamicRoot);
            var marker = CreatePrimitive(root.transform, "전체뷰 주체 기호",
                PrimitiveType.Cylinder, new Vector3(4.4f, .45f, 4.4f),
                new Vector3(0f, 3.5f, 0f), ColorFor("marker",
                    new Color(1f, .32f, .08f, 1f)));
            var detail = NewObject("확대 주체 표현", root.transform);
            var car = NewObject("주변 승용차", detail.transform);
            CreatePrimitive(car.transform, "차체", PrimitiveType.Cube,
                new Vector3(2.1f, .85f, 4.3f), new Vector3(0f, .72f, 0f),
                ColorFor("car", new Color(.18f, .52f, .84f, 1f)));
            CreatePrimitive(car.transform, "차창", PrimitiveType.Cube,
                new Vector3(1.75f, .55f, 1.9f), new Vector3(0f, 1.32f, -.15f),
                ColorFor("window", new Color(.13f, .20f, .28f, 1f)));
            var scooter = NewObject("배달 오토바이", detail.transform);
            CreatePrimitive(scooter.transform, "오토바이 차체", PrimitiveType.Cube,
                new Vector3(1.15f, .58f, 2.9f), new Vector3(0f, .78f, 0f),
                ColorFor("scooter", new Color(.12f, .60f, .94f, 1f)));
            CreatePrimitive(scooter.transform, "배달 상자", PrimitiveType.Cube,
                new Vector3(1.35f, 1.25f, 1.25f), new Vector3(0f, 1.45f, -1.0f),
                ColorFor("courier", new Color(.97f, .22f, .07f, 1f)));
            CreatePrimitive(scooter.transform, "기사", PrimitiveType.Capsule,
                new Vector3(.75f, 1.15f, .75f), new Vector3(0f, 1.65f, .25f),
                ColorFor("rider", new Color(1f, .79f, .08f, 1f)));
            CreatedActorCount++;
            return new ActorVisual(root, marker, detail, car, scooter);
        }

        private void ApplyActor(ActorVisual visual)
        {
            var model = visual.Model;
            var rotation = Quaternion.Euler(0f, (float)model.HeadingDegrees, 0f);
            var position = new Vector3((float)model.Position.X, .38f,
                (float)model.Position.Z);
            var courier = model.KindCode ==
                          사가정저밀도교통WireCodes.CourierMotorcycle;
            if (!courier && model.RouteKindCode ==
                사가정저밀도교통WireCodes.SyntheticLaneDisplayOnly)
                position += rotation * Vector3.right * 2.15f;
            visual.Root.transform.SetLocalPositionAndRotation(
                position, rotation);
            visual.Car.SetActive(!courier);
            visual.Scooter.SetActive(courier);
        }

        private void RemoveActor(string stableId)
        {
            if (!activeActors.Remove(stableId, out var visual)) return;
            visual.Model = null;
            visual.Root.SetActive(false);
            if (pooledActors.Count < maxPoolSize)
                pooledActors.Push(visual);
            else
                ReleaseObject(visual.Root);
        }

        private void RefreshVisibility(ActorVisual visual)
        {
            var overview = observationCamera == null
                           || (observationCamera.orthographic
                               && observationCamera.orthographicSize >= overviewThreshold);
            ApplyVisibility(visual, overview);
        }

        private static void ApplyVisibility(ActorVisual visual, bool overview)
        {
            visual.Marker.SetActive(overview);
            visual.Detail.SetActive(!overview);
        }

        private LineRenderer CreateLine(string name,
            IReadOnlyList<사가정저밀도교통Point> points, float width, float elevation,
            Material material)
        {
            var root = NewObject(name, staticRoot);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.positionCount = points.Count;
            for (var index = 0; index < points.Count; index++)
                line.SetPosition(index, Point(points[index], elevation));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void CreateStopLine(string name, Vector3 center, Vector3 roadDirection)
        {
            var across = Vector3.Cross(Vector3.up, roadDirection).normalized;
            CreateLine(name, new[]
            {
                Data(center - across * 3.6f),
                Data(center + across * 3.6f),
            }, .55f, .38f, ColorFor("stop", new Color(1f, .95f, .82f, 1f)));
        }

        private void CreateCrosswalk(string name, Vector3 center, Vector3 roadDirection)
        {
            var root = NewObject(name, staticRoot).transform;
            var rotation = Quaternion.LookRotation(roadDirection.normalized, Vector3.up);
            for (var index = 0; index < 6; index++)
            {
                var offset = (index - 2.5f) * .72f;
                var stripe = CreatePrimitive(root, "횡단보도 줄 " + (index + 1),
                    PrimitiveType.Cube, new Vector3(6.2f, .08f, .34f),
                    Vector3.zero, ColorFor("crosswalk", new Color(.96f, .96f, .91f, 1f)));
                stripe.transform.SetPositionAndRotation(
                    trafficRoot.TransformPoint(center + roadDirection.normalized * offset
                                                + Vector3.up * .38f),
                    trafficRoot.rotation * rotation);
                CrosswalkStripeCount++;
            }
        }

        private void CreateSignalHead(string name, string axisCode, Vector3 localPosition,
            Quaternion localRotation)
        {
            var root = NewObject(name, staticRoot);
            root.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            CreatePrimitive(root.transform, "신호 지주", PrimitiveType.Cylinder,
                new Vector3(.24f, 3.1f, .24f), new Vector3(0f, 3.1f, 0f),
                ColorFor("pole", new Color(.18f, .20f, .20f, 1f)));
            CreatePrimitive(root.transform, "신호함", PrimitiveType.Cube,
                new Vector3(.72f, 2.25f, .66f), new Vector3(0f, 6.2f, 0f),
                ColorFor("housing", new Color(.08f, .10f, .10f, 1f)));
            var red = CreatePrimitive(root.transform, "적색", PrimitiveType.Sphere,
                new Vector3(.42f, .42f, .25f), new Vector3(0f, 6.82f, .38f),
                ColorFor("red", new Color(1f, .08f, .04f, 1f)));
            var amber = CreatePrimitive(root.transform, "황색", PrimitiveType.Sphere,
                new Vector3(.42f, .42f, .25f), new Vector3(0f, 6.20f, .38f),
                ColorFor("amber", new Color(1f, .64f, .04f, 1f)));
            var green = CreatePrimitive(root.transform, "녹색", PrimitiveType.Sphere,
                new Vector3(.42f, .42f, .25f), new Vector3(0f, 5.58f, .38f),
                ColorFor("green", new Color(.12f, 1f, .25f, 1f)));
            signals.Add(new SignalVisual(axisCode, red, amber, green));
        }

        private GameObject CreatePrimitive(Transform parent, string name,
            PrimitiveType primitiveType, Vector3 scale, Vector3 localPosition,
            Material material)
        {
            var value = GameObject.CreatePrimitive(primitiveType);
            value.name = name;
            value.layer = trafficRoot.gameObject.layer;
            value.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = scale;
            var renderer = value.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = value.GetComponent<Collider>();
            if (collider != null) ReleaseObject(collider);
            return value;
        }

        private GameObject NewObject(string name, Transform parent)
        {
            var value = new GameObject(name)
            {
                layer = trafficRoot == null ? gameObject.layer : trafficRoot.gameObject.layer,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            value.transform.SetParent(parent, false);
            return value;
        }

        private Material ColorFor(string key, Color color)
        {
            if (materialByKey.TryGetValue(key, out var existing)) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException(
                    "SagajeongLowDensityTrafficShaderMissing");
            var material = new Material(shader)
            {
                name = "사가정 저밀도 교통 · " + key,
                color = color,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            ownedMaterials.Add(material);
            materialByKey.Add(key, material);
            return material;
        }

        private static IReadOnlyList<사가정저밀도교통Point> Reverse(
            IReadOnlyList<사가정저밀도교통Point> points)
            => points.Reverse().ToArray();

        private static IReadOnlyList<사가정저밀도교통Point> Offset(
            IReadOnlyList<사가정저밀도교통Point> points, double rightMeters)
        {
            var result = new 사가정저밀도교통Point[points.Count];
            for (var index = 0; index < points.Count; index++)
            {
                var before = points[Math.Max(0, index - 1)];
                var after = points[Math.Min(points.Count - 1, index + 1)];
                var dx = after.X - before.X;
                var dz = after.Z - before.Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                var rightX = dz / length;
                var rightZ = -dx / length;
                result[index] = new 사가정저밀도교통Point(
                    points[index].X + rightX * rightMeters,
                    points[index].Z + rightZ * rightMeters);
            }
            return result;
        }

        private static Vector3 Direction(사가정저밀도교통Point first,
            사가정저밀도교통Point second)
            => new Vector3((float)(second.X - first.X), 0f,
                (float)(second.Z - first.Z)).normalized;

        private static bool TrySharedPoint(
            IReadOnlyList<사가정저밀도교통Point> first,
            IReadOnlyList<사가정저밀도교통Point> second,
            out 사가정저밀도교통Point point)
        {
            foreach (var candidate in first)
            foreach (var other in second)
                if (Math.Abs(candidate.X - other.X) <= .000001d
                    && Math.Abs(candidate.Z - other.Z) <= .000001d)
                {
                    point = candidate;
                    return true;
                }
            point = default;
            return false;
        }

        private static Vector3 Point(사가정저밀도교통Point point, float y)
            => new((float)point.X, y, (float)point.Z);

        private static 사가정저밀도교통Point Data(Vector3 point)
            => new(point.x, point.z);

        private static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private sealed class ActorVisual
        {
            public ActorVisual(GameObject root, GameObject marker, GameObject detail,
                GameObject car, GameObject scooter)
            {
                Root = root;
                Marker = marker;
                Detail = detail;
                Car = car;
                Scooter = scooter;
            }

            public GameObject Root { get; }
            public GameObject Marker { get; }
            public GameObject Detail { get; }
            public GameObject Car { get; }
            public GameObject Scooter { get; }
            public 사가정저밀도교통ActorModel Model { get; set; }
        }

        private sealed class RouteVisual
        {
            public RouteVisual(string stableId, string laneStableId, int index,
                LineRenderer line)
            {
                StableId = stableId;
                LaneStableId = laneStableId;
                Index = index;
                Line = line;
                StateCode = "Pending";
            }

            public string StableId { get; }
            public string LaneStableId { get; }
            public int Index { get; }
            public LineRenderer Line { get; }
            public string StateCode { get; set; }
        }

        private sealed class SignalVisual
        {
            public SignalVisual(string axisCode, GameObject red, GameObject amber,
                GameObject green)
            {
                AxisCode = axisCode;
                Red = red;
                Amber = amber;
                Green = green;
            }

            public string AxisCode { get; }
            public GameObject Red { get; }
            public GameObject Amber { get; }
            public GameObject Green { get; }
            public string AspectCode { get; set; } = string.Empty;
        }
    }
}
