using UnityEngine;

namespace Ssalddel.Unity.Experiments
{
    public sealed class SyntyPlazaOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 focusPoint = new(0f, 2.5f, 0f);
        [SerializeField, Min(1f)] private float radius = 23f;
        [SerializeField, Min(1f)] private float height = 13f;
        [SerializeField] private float degreesPerSecond = 4f;

        private float _angle;

        private void Awake()
        {
            var offset = transform.position - focusPoint;
            _angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            ApplyPose();
        }

        private void LateUpdate()
        {
            _angle += degreesPerSecond * Time.deltaTime;
            ApplyPose();
        }

        private void ApplyPose()
        {
            var radians = _angle * Mathf.Deg2Rad;
            transform.position = focusPoint + new Vector3(
                Mathf.Sin(radians) * radius,
                height,
                Mathf.Cos(radians) * radius);
            transform.LookAt(focusPoint);
        }
    }
}
