using System;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버 공개 위치 상태 사본을 관전용 대리 캐릭터에만 투영한다.
    /// 실제 지형 높이와 플레이어 상태는 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 팀원관전PosePresenter : MonoBehaviour
    {
        [SerializeField] private 공간TileStreamingController tileStreaming = null!;
        [SerializeField] private Transform observedActorRoot = null!;
        [SerializeField] private Transform firstPersonAnchor = null!;
        [SerializeField] private bool presentationOnly = true;

        public Transform ObservedActorRoot => observedActorRoot;
        public Transform FirstPersonAnchor => firstPersonAnchor;
        public long LastPoseRevision { get; private set; } = -1;
        public double ObservedElevationMeters { get; private set; }
        public string MovementIntentCode { get; private set; } = string.Empty;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            공간TileStreamingController streaming,
            Transform actorRoot,
            Transform cameraAnchor)
        {
            tileStreaming = streaming;
            observedActorRoot = actorRoot;
            firstPersonAnchor = cameraAnchor;
            presentationOnly = true;
        }

        public void Apply(TeamObservationFramePresentationState state)
        {
            if (tileStreaming == null || observedActorRoot == null
                || firstPersonAnchor == null || state?.Camera == null
                || !state.Camera.IsActive || !state.PresentationOnly
                || state.ContainsPrivateUi || state.ContainsInventory
                || state.ContainsChat || state.PoseRevision < LastPoseRevision)
                throw new InvalidOperationException(
                    "TeamObservationPosePresentationBoundaryInvalid");

            var tileCenter = tileStreaming.WorldPositionForTile(
                state.Camera.TileFocusKey);
            var compressedMeter = tileStreaming.TileWorldSize / 500f;
            var position = tileCenter + new Vector3(
                (float)state.LocalOffsetXMeters * compressedMeter,
                0f,
                (float)state.LocalOffsetYMeters * compressedMeter);
            observedActorRoot.SetPositionAndRotation(position,
                Quaternion.Euler(0f, (float)state.YawDegrees, 0f));

            firstPersonAnchor.position = position
                + Vector3.up * (float)state.CameraHeightMeters;
            firstPersonAnchor.rotation = Quaternion.Euler(
                (float)state.PitchDegrees,
                (float)state.YawDegrees,
                0f);
            LastPoseRevision = state.PoseRevision;
            ObservedElevationMeters = state.ElevationMeters;
            MovementIntentCode = state.MovementIntentCode ?? string.Empty;
        }

        public void ResetPresentation()
        {
            LastPoseRevision = -1;
            ObservedElevationMeters = 0d;
            MovementIntentCode = string.Empty;
        }
    }
}
