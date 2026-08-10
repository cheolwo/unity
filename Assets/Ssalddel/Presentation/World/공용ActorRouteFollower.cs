using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공용ActorRouteFollower : MonoBehaviour
    {
        [SerializeField] private Transform routeStart = null!;
        [SerializeField] private Transform routeEnd = null!;
        [SerializeField] private 공용AnimationAdapter animationAdapter = null!;
        [SerializeField] private float speed = 1.6f;
        [SerializeField] private float idleDuration = .8f;

        private bool _towardEnd = true;
        private float _idleRemaining;

        public Transform RouteStart => routeStart;
        public Transform RouteEnd => routeEnd;
        public 공용AnimationAdapter AnimationAdapter => animationAdapter;
        public float Speed => speed;

        public void Configure(
            Transform start,
            Transform end,
            공용AnimationAdapter adapter,
            float movementSpeed,
            float waitDuration)
        {
            routeStart = start;
            routeEnd = end;
            animationAdapter = adapter;
            speed = movementSpeed;
            idleDuration = waitDuration;
            transform.position = routeStart.position;
        }

        public bool ValidateWiring()
            => routeStart != null
               && routeEnd != null
               && routeStart != routeEnd
               && animationAdapter != null
               && animationAdapter.transform == transform
               && speed > 0f
               && idleDuration >= 0f;

        public void TickRoute(float deltaTime)
        {
            if (!ValidateWiring() || deltaTime <= 0f)
                return;
            if (_idleRemaining > 0f)
            {
                _idleRemaining -= deltaTime;
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Idle);
                return;
            }

            var target = _towardEnd ? routeEnd.position : routeStart.position;
            var before = transform.position;
            transform.position = Vector3.MoveTowards(before, target, speed * deltaTime);
            var delta = transform.position - before;
            if (delta.sqrMagnitude > .000001f)
            {
                transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Walk);
            }
            if (Vector3.Distance(transform.position, target) <= .001f)
            {
                _towardEnd = !_towardEnd;
                _idleRemaining = idleDuration;
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Idle);
            }
        }

        private void Update() => TickRoute(Time.deltaTime);
    }
}
