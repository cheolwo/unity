using UnityEngine;

namespace Ssalddel.Unity.Experiments
{
    public sealed class SyntyPlazaBillboard : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position, Vector3.up);
            }
        }
    }
}
