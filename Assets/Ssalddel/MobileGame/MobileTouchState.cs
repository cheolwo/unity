using System;

namespace Ssalddel.MobileGame
{
    /// <summary>좌표 단위와 관계없는 탭/끌기 분리. 손가락 두 개를 쓰면 모든 손을 뗄 때까지 탭을 취소한다.</summary>
    public sealed class MobileTouchState
    {
        private double startX, startY;
        private bool pressed, moved, suppressed;
        public bool IsDragging { get { return pressed && moved && !suppressed; } }
        public bool IsPressed { get { return pressed; } }

        public void Begin(double x, double y, bool allowed)
        {
            if (suppressed) return;
            startX = x; startY = y; pressed = allowed; moved = false;
        }
        public void Move(double x, double y, double dragThreshold)
        {
            if (!pressed || suppressed) return;
            if ((x - startX) * (x - startX) + (y - startY) * (y - startY) >= dragThreshold * dragThreshold)
                moved = true;
        }
        public bool End(bool stillOnMap)
        {
            var tap = pressed && !moved && !suppressed && stillOnMap;
            pressed = false;
            return tap;
        }
        public void CancelForPinch() { suppressed = true; pressed = false; moved = false; }
        public void AllPointersReleased() { suppressed = false; pressed = false; moved = false; }
        public void Cancel() { pressed = false; moved = false; }
        public static float Zoom(float current, float ratio) { return Math.Max(.65f, Math.Min(5f, current * ratio)); }
    }
}
