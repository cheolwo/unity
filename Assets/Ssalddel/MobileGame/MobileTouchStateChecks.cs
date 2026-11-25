using System;

namespace Ssalddel.MobileGame
{
    /// <summary>외부 시험기에서 호출 가능한 순수 입력 회귀. Unity·씬·기기 상태를 변경하지 않는다.</summary>
    public static class MobileTouchStateChecks
    {
        public static string[] Run()
        {
            var state = new MobileTouchState();
            state.Begin(10,10,true);state.Move(12,11,12);
            Require(state.End(true),"짧은 이동은 탭");
            state.Begin(0,0,true);state.Move(13,0,12);
            Require(state.IsDragging && !state.End(true),"끌기는 배차 탭이 아님");
            state.Begin(0,0,false);
            Require(!state.End(true),"패널에서 시작한 터치는 지도 탭이 아님");
            state.Begin(0,0,true);
            Require(!state.End(false),"지도 밖 해제는 탭이 아님");
            state.Begin(0,0,true);state.CancelForPinch();state.Begin(0,0,true);
            Require(!state.End(true),"두 손가락 이후 남은 손으로 탭하지 않음");
            state.AllPointersReleased();state.Begin(0,0,true);
            Require(state.End(true),"모든 손을 뗀 다음 탭 복구");
            Require(MobileTouchState.Zoom(1,.1f)==.65f && MobileTouchState.Zoom(1,10)==5,"확대 범위 제한");
            return new[]{"tap","drag-not-tap","blocked-origin","release-outside","pinch-suppression","all-up-recovery","zoom-bounds"};
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
