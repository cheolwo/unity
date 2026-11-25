namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>서로 다른 건물 ID 체계의 자동 교체를 막고 기존 상세 공간을 우선 보존한다.</summary>
    public static class 사가정공간표현ModePolicy
    {
        public const string OverlayPreservedStatus = "공간 바탕 유지 · Synty 건물 대응 검토 필요";

        public static bool UseLegacySynty(bool requested, bool spatialOverlayApplied)
            => requested && !spatialOverlayApplied;
    }
}
