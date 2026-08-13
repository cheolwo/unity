using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공간TileLodLoader : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera = null!;
        [SerializeField] private Transform overviewRoot = null!;
        [SerializeField] private Transform regionRoot = null!;
        [SerializeField] private Transform taskRoot = null!;
        [SerializeField] private float regionDistance = 48f;
        [SerializeField] private float taskDistance = 25f;
        [SerializeField] private int activeLevel = -1;

        public int ActiveLevel => activeLevel;

        public void Configure(
            Camera camera, Transform overview, Transform region, Transform task,
            float regionThreshold = 48f, float taskThreshold = 25f)
        {
            targetCamera = camera;
            overviewRoot = overview;
            regionRoot = region;
            taskRoot = task;
            regionDistance = regionThreshold;
            taskDistance = taskThreshold;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (!ValidateWiring())
                return;
            var distance = Vector3.Distance(targetCamera.transform.position, transform.position);
            var next = distance <= taskDistance ? 2 : distance <= regionDistance ? 1 : 0;
            if (next == activeLevel)
                return;
            overviewRoot.gameObject.SetActive(next == 0);
            regionRoot.gameObject.SetActive(next == 1);
            taskRoot.gameObject.SetActive(next == 2);
            activeLevel = next;
        }

        public bool ValidateWiring()
            => targetCamera != null && overviewRoot != null && regionRoot != null
                && taskRoot != null && taskDistance > 0f && regionDistance > taskDistance
                && overviewRoot.IsChildOf(transform)
                && regionRoot.IsChildOf(transform)
                && taskRoot.IsChildOf(transform);
    }
}
