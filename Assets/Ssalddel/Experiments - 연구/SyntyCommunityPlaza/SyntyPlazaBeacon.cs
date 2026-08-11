using UnityEngine;

namespace Ssalddel.Unity.Experiments
{
    public sealed class SyntyPlazaBeacon : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 32f;
        [SerializeField] private float bobHeight = 0.18f;
        [SerializeField] private float bobSpeed = 1.5f;

        private Vector3 _origin;
        private float _phase;

        private void Awake()
        {
            _origin = transform.localPosition;
            _phase = Mathf.Abs(transform.position.x + transform.position.z) * 0.17f;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            var offset = Mathf.Sin((Time.time + _phase) * bobSpeed) * bobHeight;
            transform.localPosition = _origin + Vector3.up * offset;
        }
    }
}
