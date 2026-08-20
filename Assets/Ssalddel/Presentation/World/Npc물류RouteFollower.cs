using System;
using System.Linq;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public class Npc물류RouteFollower : MonoBehaviour
    {
        [SerializeField] private 물류이동Presenter movementPresenter = null!;
        [SerializeField] private Transform vehicleRoot = null!;
        [SerializeField] private Transform farmPoint = null!;
        [SerializeField] private Transform hubPoint = null!;
        [SerializeField] private Transform[] routePoints = Array.Empty<Transform>();
        [SerializeField] private 공간LHStreamingEngine streamingEngine = null!;
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private float vehicleHeight = .45f;
        [SerializeField] private string transportKindCode = Npc물류운송Codes.Freight;

        private Npc물류RoutePlanData? routePlan;
        private bool checkpointPending;
        private string lastPresentationState = string.Empty;

        public Npc물류RoutePlanData? RoutePlan => routePlan;
        public bool CheckpointPending => checkpointPending;
        public string TransportKindCode => transportKindCode;

        public void Configure(
            물류이동Presenter presenter,
            Transform vehicle,
            Transform farm,
            Transform hub)
            => Configure(presenter, vehicle, new[] { farm, hub });

        public void Configure(
            물류이동Presenter presenter,
            Transform vehicle,
            Transform[] points)
        {
            if (presenter == null || vehicle == null || points == null || points.Length < 2
                || points.Any(value => value == null))
                throw new InvalidOperationException("NpcLogisticsRoutePointsMissing");
            movementPresenter = presenter;
            vehicleRoot = vehicle;
            routePoints = points;
            farmPoint = points[0];
            hubPoint = points[^1];
            routePlan = null;
            ResetRoutePosition();
        }

        public void ConfigureTransportKind(string kindCode)
        {
            if (kindCode != Npc물류운송Codes.Freight
                && kindCode != Npc물류운송Codes.FoodDelivery)
                throw new ArgumentException("NpcLogisticsTransportKindInvalid", nameof(kindCode));
            transportKindCode = kindCode;
            routePlan = null;
        }

        public void ConfigureStreaming(공간LHStreamingEngine engine)
            => streamingEngine = engine;

        private void Awake()
        {
            if (streamingEngine == null)
                streamingEngine = FindFirstObjectByType<공간LHStreamingEngine>(FindObjectsInactive.Include);
        }

        private void OnDisable()
        {
            var npcStableId = routePlan?.NpcStableId;
            if (streamingEngine != null && !string.IsNullOrWhiteSpace(npcStableId))
                streamingEngine.UnregisterNpcRouteInterest(npcStableId);
        }

        private void Update()
        {
            if (!checkpointPending)
                _ = TickPresentationAsync(Time.deltaTime);
        }

        public async Task TickPresentationAsync(float deltaTime)
        {
            if (movementPresenter == null || vehicleRoot == null || routePoints.Length < 2
                || deltaTime <= 0f)
                return;
            var snapshot = movementPresenter.CurrentAuthoritySnapshot;
            if (snapshot == null) return;
            var phase = movementPresenter.CurrentPhaseCode;
            if (phase == 물류이동PhaseCodes.CargoSelected
                || phase == 물류이동PhaseCodes.PreviewReady)
            {
                routePlan = null;
                ResetRoutePosition();
                SetPresentationState(Npc물류운송Codes.Planned);
                return;
            }
            if (phase == 물류이동PhaseCodes.Arrived)
            {
                vehicleRoot.position = routePoints[^1].position + Vector3.up * vehicleHeight;
                SetPresentationState(Npc물류운송Codes.Arrived);
                return;
            }
            if (phase != 물류이동PhaseCodes.Reserved
                && phase != 물류이동PhaseCodes.InTransit)
                return;

            EnsureRoutePlan(snapshot);
            var nextIndex = snapshot.CompletedRouteTicks + 1;
            if (nextIndex >= routePoints.Length) return;
            var target = routePoints[nextIndex].position + Vector3.up * vehicleHeight;
            if (streamingEngine != null && streamingEngine.IsInitialized)
            {
                streamingEngine.RegisterNpcRouteInterest(routePlan!.NpcStableId, target);
                if (!streamingEngine.IsNpcNavigationReady(target))
                {
                    SetPresentationState(Npc물류운송Codes.PausedByStreaming);
                    return;
                }
            }

            SetPresentationState(Npc물류운송Codes.Moving);
            var before = vehicleRoot.position;
            vehicleRoot.position = Vector3.MoveTowards(before, target, movementSpeed * deltaTime);
            var direction = vehicleRoot.position - before;
            if (direction.sqrMagnitude > .000001f)
                vehicleRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (Vector3.Distance(vehicleRoot.position, target) > .001f) return;

            checkpointPending = true;
            try
            {
                await movementPresenter.ApplyNpcRouteCheckpointAsync(
                    CreateCheckpoint(snapshot, nextIndex));
            }
            finally
            {
                checkpointPending = false;
            }
        }

        private void EnsureRoutePlan(물류이동AuthoritySnapshot snapshot)
        {
            if (routePlan != null && routePlan.RouteStableId == snapshot.RouteStableId
                && routePlan.NpcStableId == snapshot.CarrierCandidateStableId
                && routePlan.VehicleStableId == snapshot.VehicleStableId)
                return;
            if (snapshot.RequiredRouteTicks != routePoints.Length - 1)
                throw new InvalidOperationException("NpcLogisticsRouteTickWaypointMismatch");
            routePlan = new Npc물류RoutePlanData
            {
                RouteStableId = snapshot.RouteStableId,
                RouteVersion = "npc-route.local.r1",
                TransportKindCode = transportKindCode,
                CargoOrOrderStableId = snapshot.CargoStableId,
                NpcStableId = snapshot.CarrierCandidateStableId,
                VehicleStableId = snapshot.VehicleStableId,
                Waypoints = routePoints.Select((point, index) => new Npc물류WaypointData
                {
                    WaypointStableId = snapshot.RouteStableId + ":waypoint:" + index,
                    Sequence = index,
                    L3CellKey = streamingEngine == null
                        ? 공간LHCellKey.FromWorldPosition(point.position.x, point.position.z, 0d, 0d)
                        : streamingEngine.CellKeyAtPosition(point.position),
                    Position = new Npc물류PositionData
                    {
                        X = point.position.x, Y = point.position.y, Z = point.position.z,
                    },
                }).ToArray(),
            };
            routePlan.Validate();
        }

        private Npc물류RouteCheckpointData CreateCheckpoint(
            물류이동AuthoritySnapshot snapshot, int sequence)
            => new()
            {
                CheckpointStableId = snapshot.RouteStableId + ":checkpoint:" + sequence,
                RouteStableId = snapshot.RouteStableId,
                CargoOrOrderStableId = snapshot.CargoStableId,
                NpcStableId = snapshot.CarrierCandidateStableId,
                VehicleStableId = snapshot.VehicleStableId,
                Sequence = sequence,
                ExpectedRevision = snapshot.Revision,
            };

        private void ResetRoutePosition()
        {
            if (vehicleRoot == null || routePoints == null || routePoints.Length == 0
                || routePoints[0] == null)
                return;
            vehicleRoot.position = routePoints[0].position + Vector3.up * vehicleHeight;
            if (routePoints.Length > 1)
            {
                var direction = routePoints[1].position - routePoints[0].position;
                if (direction.sqrMagnitude > .01f)
                    vehicleRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private void SetPresentationState(string stateCode)
        {
            if (lastPresentationState == stateCode) return;
            lastPresentationState = stateCode;
            movementPresenter.SetNpcRoutePresentationState(stateCode);
        }
    }
}
