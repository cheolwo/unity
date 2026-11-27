using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 폰 지도 손 제스처 (2026-11-25 사용자 12:37: 「휴대폰에서는 확대하거나 끌어당기거나 손으로 눌러서 지도를 이동하잖아 … 그게 가능하도록」).
    // 프레임마다 Input.touches를 직접 읽는다(IMGUI의 흉내 마우스는 한 손가락만 알고, 두 손가락에서 튄다).
    //   한 손가락 끌기 = 옮기기(놓으면 미끄러지다 선다) · 두 손가락 = 벌려 확대 · 오므려 축소 · 함께 끌어 옮기기(가운데를 붙잡고)
    //   두 번 톡톡 = 그 자리를 두 배로 확대(칸이 떠 있으면 두 번 누르기는 칸 → 디오라마가 맡는다).
    // 누르기(칸 · 동 · 역 고르기)는 그대로 IMGUI가 받는다. 여기서 옮겼으면 guDragging을 켜 두어 누르기로 세지 않게 한다.
    // 판 · 칩 · 위 띠 · 확대 단추 · 칸 창 위에서 시작한 손은 지도를 움직이지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const float TouchPanStart = 9f;          // 이만큼(논리 점) 움직여야 옮기기로 본다
        private const float TouchFlingStop = 25f;        // 미끄러짐이 이 속도(점/초) 아래면 멈춘다
        private const float TouchFlingDecay = 5.5f;      // 미끄러짐이 줄어드는 빠르기(1/초)
        private const float TouchDoubleSeconds = 0.32f;
        private const float TouchDoubleReach = 32f;

        private int touchCountWas;
        private bool touchOnMap;                 // 지금 손이 지도 위에서 시작했는가
        private bool touchMoved;                 // 이번 손으로 지도를 옮겼는가
        private Vector2 touchStart, touchLast, touchMidLast;
        private float touchDistanceLast;
        private float touchDownAt;
        private Vector2 touchVelocity;
        private Vector2 touchFling;
        private float touchLastTapAt = -10f;
        private Vector2 touchLastTapPoint;
        private float touchZoomFrom, touchZoomTo, touchZoomAt = -10f;
        private Vector2 touchZoomPoint;

        private bool TouchMapActive => Input.touchCount > 0 && touchOnMap;

        private Vector2 TouchLogical(Vector2 screen) =>
            new(screen.x / Mathf.Max(0.01f, portraitScale), (Screen.height - screen.y) / Mathf.Max(0.01f, portraitScale));

        private bool TouchBlocked(Vector2 point) =>
            PortraitCovers(point) || CapitalHexCardCovers(point) || HexCardCovers(point) || point.y < PortraitMapTop;

        // LateUpdate(SagajeongDispatchDiorama.cs)가 부른다.
        private void UpdatePhoneMapTouch()
        {
            var ready = PortraitScreen && !PublicDongOpen && GuMapVisible && guCameraSet && guSpace != null && !PortraitPickerOpen;
            var count = ready ? Input.touchCount : 0;
            var dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            if (!ready)
            {
                touchCountWas = 0;
                touchOnMap = false;
                touchFling = Vector2.zero;
                return;
            }

            // 두 번 톡톡 확대: 0.22초 동안 부드럽게
            if (Time.unscaledTime - touchZoomAt < 0.22f && count == 0)
            {
                var t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - touchZoomAt) / 0.22f);
                ZoomGu(Mathf.Lerp(touchZoomFrom, touchZoomTo, t), touchZoomPoint);
            }

            if (count == 0)
            {
                if (touchCountWas > 0) touchOnMap = false;
                touchCountWas = 0;
                if (touchFling.sqrMagnitude > TouchFlingStop * TouchFlingStop)
                {
                    guOrigin += touchFling * dt;
                    guManual = true;
                    ClampGu();
                    touchFling *= Mathf.Exp(-TouchFlingDecay * dt);
                }
                else touchFling = Vector2.zero;
                return;
            }

            if (touchCountWas == 0)      // 새 손
            {
                var first = TouchLogical(Input.GetTouch(0).position);
                touchOnMap = !TouchBlocked(first);
                touchMoved = false;
                touchStart = touchLast = first;
                touchDownAt = Time.unscaledTime;
                touchVelocity = Vector2.zero;
                touchFling = Vector2.zero;
            }
            if (!touchOnMap)
            {
                touchCountWas = count;
                return;
            }

            if (count >= 2)
            {
                var a = TouchLogical(Input.GetTouch(0).position);
                var b = TouchLogical(Input.GetTouch(1).position);
                var mid = (a + b) * 0.5f;
                var distance = Vector2.Distance(a, b);
                if (touchCountWas < 2)
                {
                    touchMidLast = mid;
                    touchDistanceLast = distance;
                }
                else if (distance > 8f && touchDistanceLast > 8f)
                {
                    var world = GuWorld(touchMidLast);      // 앞 프레임 가운데에 있던 땅을 지금 가운데로
                    var full = GuFullScale;
                    guScale = Mathf.Clamp(guScale * distance / touchDistanceLast, full, Mathf.Max(full, GuMaxScale));
                    guOrigin = new Vector2(mid.x - world.x * guScale, mid.y - (guSpace.height - world.y) * guScale);
                    guManual = true;
                    ClampGu();
                    touchMidLast = mid;
                    touchDistanceLast = distance;
                }
                touchMoved = true;
                guPressed = false;          // IMGUI 누르기 · 끌기는 쉰다
                guDragging = true;
                touchVelocity = Vector2.zero;
                touchCountWas = count;
                return;
            }

            // 한 손가락
            var point = TouchLogical(Input.GetTouch(0).position);
            if (touchCountWas >= 2)          // 두 손가락에서 한 손가락이 떨어졌다: 남은 손가락으로 이어서 옮긴다
            {
                touchLast = point;
                touchCountWas = 1;
                return;
            }
            if (!touchMoved && (point - touchStart).sqrMagnitude >= TouchPanStart * TouchPanStart)
            {
                touchMoved = true;
                touchLast = point;
            }
            if (touchMoved)
            {
                var step = point - touchLast;
                guOrigin += step;
                guManual = true;
                ClampGu();
                guDragging = true;          // 놓을 때 누르기로 세지 않는다
                if (dt > 0f) touchVelocity = Vector2.Lerp(touchVelocity, step / dt, 0.35f);
                touchLast = point;
            }
            var phase = Input.GetTouch(0).phase;
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                if (touchMoved) touchFling = touchVelocity.magnitude > 300f ? touchVelocity : Vector2.zero;
                else if (Time.unscaledTime - touchDownAt < 0.3f) NoteMapTap(point);
            }
            touchCountWas = count;
        }

        // 톡 한 번: 바로 앞 톡과 가까우면 두 번 톡톡. 칸이 떠 있지 않을 때만 그 자리를 두 배로.
        private void NoteMapTap(Vector2 point)
        {
            var now = Time.unscaledTime;
            var twice = now - touchLastTapAt <= TouchDoubleSeconds && Vector2.Distance(point, touchLastTapPoint) <= TouchDoubleReach;
            touchLastTapAt = twice ? -10f : now;
            touchLastTapPoint = point;
            if (!twice) return;
            if (CapitalHexAlpha >= 0.5f || HexTilesActive) return;          // 칸 두 번 누르기 = 디오라마
            var full = GuFullScale;
            touchZoomFrom = guScale;
            touchZoomTo = Mathf.Clamp(guScale * 2f, full, Mathf.Max(full, GuMaxScale));
            touchZoomPoint = point;
            touchZoomAt = now;
        }
    }
}
