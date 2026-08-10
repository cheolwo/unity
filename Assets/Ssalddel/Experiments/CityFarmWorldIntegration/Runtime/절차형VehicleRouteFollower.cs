using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class 절차형VehicleRouteFollower : MonoBehaviour
    {
        [SerializeField] private Transform routeStart = null!;
        [SerializeField] private Transform routeEnd = null!;
        [SerializeField] private float speed = 4f;
        [SerializeField] private bool loop = true;

        private bool _towardEnd = true;

        public Transform RouteStart => routeStart;
        public Transform RouteEnd => routeEnd;
        public float Speed => speed;

        public void Configure(Transform start, Transform end, float movementSpeed, bool shouldLoop)
        {
            routeStart = start;
            routeEnd = end;
            speed = movementSpeed;
            loop = shouldLoop;
            transform.position = start.position;
        }

        public bool ValidateWiring()
            => routeStart != null && routeEnd != null && routeStart != routeEnd && speed > 0f;

        public void TickPresentation(float deltaTime)
        {
            if (!ValidateWiring() || deltaTime <= 0f)
                return;
            var target = _towardEnd ? routeEnd.position : routeStart.position;
            var before = transform.position;
            transform.position = Vector3.MoveTowards(before, target, speed * deltaTime);
            var delta = transform.position - before;
            if (delta.sqrMagnitude > .000001f)
                transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            if (Vector3.Distance(transform.position, target) <= .001f && loop)
                _towardEnd = !_towardEnd;
        }

        private void Update() => TickPresentation(Time.deltaTime);
    }
}
