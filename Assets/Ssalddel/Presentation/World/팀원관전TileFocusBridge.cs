using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>관전 중 타일 준비 중심만 원격 대리 캐릭터로 전환한다.</summary>
    [DisallowMultipleComponent]
    public sealed class 팀원관전TileFocusBridge : MonoBehaviour
    {
        [SerializeField] private 공간TileStreamingController tileStreaming = null!;
        [SerializeField] private Transform observedActorRoot = null!;
        [SerializeField] private bool presentationOnly = true;

        private Transform? previousFocusTarget;

        public bool IsFocusingObservedActor { get; private set; }
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            공간TileStreamingController streaming,
            Transform actorRoot)
        {
            tileStreaming = streaming;
            observedActorRoot = actorRoot;
            presentationOnly = true;
        }

        public void Begin()
        {
            if (tileStreaming == null || observedActorRoot == null
                || !presentationOnly)
                throw new InvalidOperationException(
                    "TeamObservationTileFocusBoundaryInvalid");
            if (!IsFocusingObservedActor)
                previousFocusTarget = tileStreaming.FocusTarget;
            tileStreaming.SetFocusTarget(observedActorRoot);
            IsFocusingObservedActor = true;
        }

        public void End()
        {
            if (!IsFocusingObservedActor) return;
            if (previousFocusTarget != null)
                tileStreaming.SetFocusTarget(previousFocusTarget);
            previousFocusTarget = null;
            IsFocusingObservedActor = false;
        }

        private void OnDisable() => End();
    }
}
