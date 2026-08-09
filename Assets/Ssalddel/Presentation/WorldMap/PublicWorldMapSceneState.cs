using System;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public enum PublicWorldMapSceneStatus
    {
        Idle,
        Loading,
        Success,
        InitialLoadError,
        Refreshing,
        RefreshError
    }

    public sealed class PublicWorldMapSceneState
    {
        public PublicWorldMapSceneStatus Status { get; set; } = PublicWorldMapSceneStatus.Idle;
        public string ErrorMessage { get; set; } = string.Empty;
        public int MarkerCount { get; set; }
        public string Revision { get; set; } = string.Empty;
        public DateTimeOffset? GeneratedAtUtc { get; set; }

        public bool KeepsExistingMarkers => Status == PublicWorldMapSceneStatus.Refreshing
            || Status == PublicWorldMapSceneStatus.RefreshError;
    }
}
