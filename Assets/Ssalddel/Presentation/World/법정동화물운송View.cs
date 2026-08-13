using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 법정동화물운송View : MonoBehaviour
    {
        [SerializeField] private 물류이동Presenter movementPresenter = null!;
        [SerializeField] private Transform vehicleRoot = null!;
        [SerializeField] private Transform farmPoint = null!;
        [SerializeField] private Transform hubPoint = null!;
        [SerializeField] private Transform[] routePoints = System.Array.Empty<Transform>();

        private string lastPhase = string.Empty;
        private int lastCompletedTicks = -1;

        public void Configure(
            물류이동Presenter presenter,
            Transform vehicle,
            Transform farm,
            Transform hub)
        {
            movementPresenter = presenter;
            vehicleRoot = vehicle;
            farmPoint = farm;
            hubPoint = hub;
            routePoints = new[] { farm, hub };
            Apply();
        }

        public void Configure(
            물류이동Presenter presenter,
            Transform vehicle,
            Transform[] points)
        {
            if (points == null || points.Length < 2)
                throw new System.InvalidOperationException("LegalDongFreightRoutePointsMissing");
            movementPresenter = presenter;
            vehicleRoot = vehicle;
            routePoints = points;
            farmPoint = points[0];
            hubPoint = points[^1];
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (movementPresenter == null || vehicleRoot == null
                || farmPoint == null || hubPoint == null)
                return;
            var snapshot = movementPresenter.CurrentAuthoritySnapshot;
            if (snapshot == null) return;
            var phase = movementPresenter.CurrentPhaseCode;
            if (phase == lastPhase && snapshot.CompletedRouteTicks == lastCompletedTicks) return;

            var progress = phase == 물류이동PhaseCodes.Arrived ? 1f
                : snapshot.RequiredRouteTicks <= 0 ? 0f
                : Mathf.Clamp01((float)snapshot.CompletedRouteTicks / snapshot.RequiredRouteTicks);
            var scaled = progress * (routePoints.Length - 1);
            var segment = Mathf.Min(Mathf.FloorToInt(scaled), routePoints.Length - 2);
            var segmentProgress = scaled - segment;
            var from = routePoints[segment].position;
            var to = routePoints[segment + 1].position;
            vehicleRoot.position = Vector3.Lerp(from, to, segmentProgress) + Vector3.up * .45f;
            var direction = to - from;
            if (direction.sqrMagnitude > .01f)
                vehicleRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
            lastPhase = phase;
            lastCompletedTicks = snapshot.CompletedRouteTicks;
        }
    }
}
