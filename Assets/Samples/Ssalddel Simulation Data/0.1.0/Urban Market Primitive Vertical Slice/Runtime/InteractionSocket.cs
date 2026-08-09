using System;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class InteractionSocket : MonoBehaviour
    {
        [SerializeField]
        private Collider interactionCollider = null!;

        public event Action? Selected;

        public void Configure(Collider targetCollider)
        {
            interactionCollider = targetCollider;
        }

        public bool ValidateWiring()
        {
            return interactionCollider != null;
        }

        public void SelectForTests()
        {
            if (interactionCollider != null && interactionCollider.enabled)
                Selected?.Invoke();
        }

        private void OnMouseDown()
        {
            SelectForTests();
        }
    }
}
