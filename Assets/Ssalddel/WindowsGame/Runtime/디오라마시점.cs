using System;

namespace Ssalddel.WindowsGame
{
    // 3D 기반 A1 (2026-11-14): 2.5D 지도를 보는 방향과, 그 방향에서 땅 ↔ 화면을 바꾸는 계산을 한 곳에 모은다.
    //  - 방향: 북쪽 기준 회전(yaw)과 내려다보는 각(pitch). Unity 카메라의 Quaternion.Euler(pitch, yaw, 0)과 같은 뜻이다.
    //  - 원근이 없는 화면(정사영)의 계산이다. 길이 단위는 모두 m이고, 화면 쪽은 (오른쪽, 아래쪽)이다.
    //  - 원근 보기(A6)는 같은 방향에서 카메라만 바꾼다. 화면 가운데의 크기를 평행 보기와 맞추는 거리 계산이 아래에 있다.
    // 기본값은 (30°, 55°)이고 그때 화면은 전과 같다. 화면은 이 값을 바꿔 다른 방향에서 볼 수 있다(A3).
    // 화면 표현 규칙이며 게임 시계·주문·업무 상태를 읽거나 바꾸지 않는다. Unity 없이 검사한다(행정동모듈Checks).
    internal readonly struct 디오라마시점
    {
        public const double DefaultYawDegrees = 30;      // 북쪽 기준 회전 (표현값)
        public const double DefaultPitchDegrees = 55;    // 내려다보는 각 (표현값)
        public const double MinPitchDegrees = 25;        // 이보다 눕히면 건물이 서로를 너무 많이 가린다 (표현값)
        public static readonly 디오라마시점 Default = new 디오라마시점(DefaultYawDegrees, DefaultPitchDegrees);

        // 바깥에서 받은 값을 쓸 수 있는 범위로 다듬는다: 회전은 0° 이상 360° 미만으로, 내려다보는 각은 25°~90°로.
        public static 디오라마시점 Of(double yawDegrees, double pitchDegrees)
        {
            var yaw = yawDegrees % 360;
            if (yaw < 0) yaw += 360;
            var pitch = Math.Max(MinPitchDegrees, Math.Min(90, pitchDegrees));
            return new 디오라마시점(yaw, pitch);
        }

        public readonly double YawDegrees;
        public readonly double PitchDegrees;
        public readonly double YawCos;
        public readonly double YawSin;
        public readonly double PitchSin;

        public 디오라마시점(double yawDegrees, double pitchDegrees)
        {
            if (!(pitchDegrees > 0 && pitchDegrees <= 90)) throw new ArgumentOutOfRangeException(nameof(pitchDegrees));
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            YawCos = Math.Cos(yawDegrees * Math.PI / 180);
            YawSin = Math.Sin(yawDegrees * Math.PI / 180);
            PitchSin = Math.Sin(pitchDegrees * Math.PI / 180);
        }

        // 땅 위 (동쪽, 북쪽) → 화면 (오른쪽, 아래쪽)
        public (double U, double V) Screen(double east, double north) =>
            행정동모듈Policy.Screen(east, north, true, YawCos, YawSin, PitchSin);

        // 화면 (오른쪽, 아래쪽) → 땅 위 (동쪽, 북쪽)
        public (double East, double North) Ground(double u, double v) =>
            행정동모듈Policy.Ground(u, v, true, YawCos, YawSin, PitchSin);

        // 화면에서 옮긴 거리 → 평면 지도 이동량. 평면 지도는 오른쪽이 동쪽, 아래쪽이 남쪽이다.
        public (double X, double Y) FlatDelta(double u, double v)
        {
            var (east, north) = Ground(u, v);
            return (east, -north);
        }

        // 동서 width, 남북 depth인 네모난 땅이 화면에서 차지하는 가로·세로.
        public (double X, double Y) ScreenSpan(double width, double depth) =>
            (width * Math.Abs(YawCos) + depth * Math.Abs(YawSin), (width * Math.Abs(YawSin) + depth * Math.Abs(YawCos)) * PitchSin);

        // 화면의 가로 width, 세로 height에 담기는 땅의 동서·남북 길이.
        public (double East, double North) GroundSpan(double width, double height)
        {
            var depth = height / PitchSin;
            return (width * Math.Abs(YawCos) + depth * Math.Abs(YawSin), width * Math.Abs(YawSin) + depth * Math.Abs(YawCos));
        }

        // ── 원근 보기 (3D 기반 A6, 2026-11-14) ──
        // 같은 방향에서 화각이 있는 카메라로 본다. 화면 가운데에 놓인 땅이 평행 보기와 같은 크기로 보이도록 카메라 거리를 잡는다.
        // 그래서 가운데를 기준으로 한 이동·확대 계산은 그대로 쓴다. 화면 아래쪽(가까운 쪽)은 더 크게, 위쪽(먼 쪽)은 더 작게 보인다.
        public const double PerspectiveFovDegrees = 28;      // 세로 화각 (표현값)
        private static readonly double PerspectiveHalfTan = Math.Tan(PerspectiveFovDegrees * 0.5 * Math.PI / 180);

        // halfHeight: 평행 보기에서 화면 세로 절반에 담기는 길이(m). 돌려주는 값은 카메라에서 화면 가운데의 땅까지의 거리(m).
        public static double PerspectiveDistance(double halfHeight) => halfHeight / PerspectiveHalfTan;

        // 가까운 면과 먼 면(m). 가까운 면을 멀리 둘수록 깊이 값이 촘촘해져 겹친 면이 깜박이지 않는다.
        public static (double Near, double Far) PerspectiveClip(double halfHeight)
        {
            var distance = PerspectiveDistance(halfHeight);
            return (Math.Max(1, distance - 2.4 * halfHeight), distance * 4);
        }

        // 화면 세로 위치 v(아래 끝 -1, 가운데 0, 위 끝 +1)의 시선이 땅과 만나는 곳의 깊이(카메라 앞쪽 거리, m). 땅과 만나지 않으면 무한대.
        public double PerspectiveGroundDepth(double halfHeight, double v)
        {
            var pitch = PitchDegrees * Math.PI / 180;
            var down = pitch - Math.Atan(v * PerspectiveHalfTan);       // 그 시선이 내려다보는 각
            if (down <= 1e-6) return double.PositiveInfinity;
            // 카메라 높이 = 거리 × sin(pitch). 시선 길이 = 높이 ÷ sin(down). 앞쪽 성분 = 시선 길이 × cos(pitch − down).
            return PerspectiveDistance(halfHeight) * Math.Sin(pitch) / Math.Sin(down) * Math.Cos(pitch - down);
        }

        // 그 자리의 땅이 평행 보기의 몇 배로 보이는가(화면 가운데가 1).
        public double PerspectiveScale(double halfHeight, double v) => PerspectiveDistance(halfHeight) / PerspectiveGroundDepth(halfHeight, v);
    }
}
