using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공간안전이동Gate : MonoBehaviour
    {
        [SerializeField] private 공간TileStreamingController streaming = null!;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float probeHeight = 4f;
        [SerializeField] private float probeDistance = 12f;
        [SerializeField] private bool fixture는SceneCollider허용 = true;

        public string LastBlockedTileKey { get; private set; } = string.Empty;
        public string LastProbeTileKey { get; private set; } = string.Empty;
        public bool LastProbeHadGround { get; private set; }
        public bool LastMoveAllowed { get; private set; }

        public void Configure(
            공간TileStreamingController tileStreaming,
            LayerMask walkableGroundLayers,
            bool allowFixtureSceneCollider)
        {
            streaming = tileStreaming;
            groundLayers = walkableGroundLayers;
            fixture는SceneCollider허용 = allowFixtureSceneCollider;
        }

        public bool CanEnter(Vector3 nextPosition)
        {
            if (streaming == null || !streaming.IsInitialized)
                return true;
            var tileKey = streaming.TileKeyAtPosition(nextPosition);
            LastProbeTileKey = tileKey;
            if (!streaming.IsTracked(tileKey))
                return Block(tileKey, false);

            var origin = nextPosition + Vector3.up * probeHeight;
            LastProbeHadGround = Physics.Raycast(
                origin, Vector3.down, out _, probeDistance,
                groundLayers, QueryTriggerInteraction.Ignore);
            var fixture = string.Equals(
                streaming.SourceModeCode, 공간TileStreamingCodes.Fixture,
                StringComparison.Ordinal);
            var allowed = fixture && fixture는SceneCollider허용
                ? LastProbeHadGround
                : streaming.IsSafeBaseReady(tileKey) && LastProbeHadGround;
            if (!allowed) return Block(tileKey, LastProbeHadGround);
            LastBlockedTileKey = string.Empty;
            LastMoveAllowed = true;
            return true;
        }

        private bool Block(string tileKey, bool hadGround)
        {
            LastBlockedTileKey = tileKey;
            LastProbeHadGround = hadGround;
            LastMoveAllowed = false;
            return false;
        }
    }
}
