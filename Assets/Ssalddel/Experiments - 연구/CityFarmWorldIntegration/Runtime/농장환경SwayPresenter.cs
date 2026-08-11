using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class 농장환경SwayPresenter : MonoBehaviour
    {
        [SerializeField] private float amplitudeDegrees = 1.5f;
        [SerializeField] private float frequency = 1f;
        [SerializeField] private float phase;

        private Quaternion _restRotation;
        private bool _initialized;

        public float AmplitudeDegrees => amplitudeDegrees;
        public float Frequency => frequency;
        public float Phase => phase;

        public void Configure(float amplitude, float cyclesPerSecond, float phaseOffset)
        {
            amplitudeDegrees = Mathf.Max(0f, amplitude);
            frequency = Mathf.Max(.01f, cyclesPerSecond);
            phase = phaseOffset;
            _restRotation = transform.localRotation;
            _initialized = true;
        }

        public bool ValidateWiring()
            => amplitudeDegrees > 0f && amplitudeDegrees <= 3f
               && frequency >= .01f && frequency <= 2f;

        public void TickPresentation(float timeSeconds)
        {
            if (!_initialized)
            {
                _restRotation = transform.localRotation;
                _initialized = true;
            }
            if (!ValidateWiring())
                return;

            var angle = Mathf.Sin(timeSeconds * frequency * Mathf.PI * 2f + phase)
                        * amplitudeDegrees;
            transform.localRotation = _restRotation * Quaternion.Euler(angle, 0f, angle * .35f);
        }

        private void OnEnable()
        {
            _restRotation = transform.localRotation;
            _initialized = true;
        }

        private void Update() => TickPresentation(Time.time);
    }
}
