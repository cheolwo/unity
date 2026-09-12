namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>진단 표시 입력만 분배하며 권위 상태나 캡처 명령을 실행하지 않는다.</summary>
    public static class 월드진단단축키Policy
    {
        public static bool ShouldToggleNature(bool pressed, bool shift, bool control, bool alt, bool meta)
            => pressed && !shift && !control && !alt && !meta;

        public static bool ShouldToggleStreaming(bool pressed, bool shift, bool control, bool alt, bool meta)
            => pressed && shift && !control && !alt && !meta;

        // 시험 입력의 직접 Update와 자동 Update가 같은 native edge를 두 번 소비하지 않는다.
        public static bool TryConsume(bool requested, int frame, ref int consumedFrame)
        {
            if (!requested || frame == consumedFrame) return false;
            consumedFrame = frame;
            return true;
        }
    }
}
