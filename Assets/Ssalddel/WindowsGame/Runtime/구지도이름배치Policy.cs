using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame
{
    /// <summary>화면 좌표에서 이름이 차지하는 실제 회전 사각형이다.</summary>
    internal readonly struct 구지도이름영역
    {
        internal 구지도이름영역(double centerX, double centerY, double width, double height, double angleDegrees = 0)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
            AngleDegrees = angleDegrees;
        }

        internal double CenterX { get; }
        internal double CenterY { get; }
        internal double Width { get; }
        internal double Height { get; }
        internal double AngleDegrees { get; }
    }

    /// <summary>
    /// 호출 순서대로 화면 이름의 점유 영역을 등록한다. 우선순위·지도 상태는 호출자가 소유한다.
    /// </summary>
    internal sealed class 구지도이름배치Policy
    {
        // 삼각함수의 직각 반올림 오차만 흡수하는 화면 좌표 허용값이다.
        private const double Tolerance = 0.00000001;
        private readonly List<회전영역> occupied = new List<회전영역>();
        private double viewportWidth;
        private double viewportHeight;

        internal int Count => occupied.Count;

        internal void Reset(double viewportWidth, double viewportHeight)
        {
            if (!PositiveFinite(viewportWidth)) throw new ArgumentOutOfRangeException(nameof(viewportWidth));
            if (!PositiveFinite(viewportHeight)) throw new ArgumentOutOfRangeException(nameof(viewportHeight));
            this.viewportWidth = viewportWidth;
            this.viewportHeight = viewportHeight;
            occupied.Clear();
        }

        /// <summary>
        /// 고정 UI는 화면 일부가 밖에 있거나 다른 고정 UI와 겹쳐도 등록한다.
        /// 잘못된 값 또는 Reset 전 호출은 등록하지 않는다.
        /// </summary>
        internal bool Reserve(구지도이름영역 area)
        {
            if (!Ready || !회전영역.TryCreate(area, out var rectangle)) return false;
            occupied.Add(rectangle);
            return true;
        }

        /// <summary>
        /// 실제 회전 영역 전체가 화면 안에 있고 기존 영역과 분리된 이름만 등록한다.
        /// margin은 이번 후보와 기존 실제 영역 사이의 분리축별 여유이며 누적하지 않는다.
        /// margin 0에서는 경계끼리 닿는 배치를 허용한다.
        /// </summary>
        internal bool TryReserve(구지도이름영역 area, double margin = 2)
        {
            if (!Ready || !Finite(margin) || margin < 0 || !회전영역.TryCreate(area, out var rectangle)) return false;
            if (!rectangle.Inside(viewportWidth, viewportHeight)) return false;
            foreach (var previous in occupied)
            {
                if (!rectangle.SeparatedFrom(previous, margin)) return false;
            }
            occupied.Add(rectangle);
            return true;
        }

        private bool Ready => viewportWidth > 0 && viewportHeight > 0;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool PositiveFinite(double value) => Finite(value) && value > 0;

        private readonly struct 회전영역
        {
            private readonly double x, y, halfWidth, halfHeight, cos, sin;
            private readonly double extentX, extentY;

            private 회전영역(구지도이름영역 area, double cos, double sin, double extentX, double extentY)
            {
                x = area.CenterX;
                y = area.CenterY;
                halfWidth = area.Width / 2;
                halfHeight = area.Height / 2;
                this.cos = cos;
                this.sin = sin;
                this.extentX = extentX;
                this.extentY = extentY;
            }

            internal static bool TryCreate(구지도이름영역 area, out 회전영역 rectangle)
            {
                rectangle = default;
                if (!Finite(area.CenterX) || !Finite(area.CenterY) || !PositiveFinite(area.Width) ||
                    !PositiveFinite(area.Height) || !Finite(area.AngleDegrees)) return false;

                var radians = (area.AngleDegrees % 360) * (Math.PI / 180);
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);
                var extentX = Math.Abs(cos) * (area.Width / 2) + Math.Abs(sin) * (area.Height / 2);
                var extentY = Math.Abs(sin) * (area.Width / 2) + Math.Abs(cos) * (area.Height / 2);
                if (!Finite(extentX) || !Finite(extentY) ||
                    !Finite(area.CenterX - extentX) || !Finite(area.CenterX + extentX) ||
                    !Finite(area.CenterY - extentY) || !Finite(area.CenterY + extentY)) return false;

                rectangle = new 회전영역(area, cos, sin, extentX, extentY);
                return true;
            }

            internal bool Inside(double width, double height)
            {
                // 회전 사각형의 모든 꼭짓점을 포함하는 범위가 직사각형 화면 안에 있어야 한다.
                return x - extentX >= -Tolerance && x + extentX <= width + Tolerance &&
                       y - extentY >= -Tolerance && y + extentY <= height + Tolerance;
            }

            internal bool SeparatedFrom(회전영역 other, double margin)
            {
                // 두 사각형의 각 변에 수직인 네 축 중 하나라도 분리되면 겹치지 않는다.
                // 화면 축 AABB만 비교하면 긴 대각선 이름 사이의 빈 곳을 잃게 된다.
                return SeparatedOnAxis(other, cos, sin, margin) ||
                       SeparatedOnAxis(other, -sin, cos, margin) ||
                       SeparatedOnAxis(other, other.cos, other.sin, margin) ||
                       SeparatedOnAxis(other, -other.sin, other.cos, margin);
            }

            private bool SeparatedOnAxis(회전영역 other, double axisX, double axisY, double margin)
            {
                var distance = Math.Abs((other.x - x) * axisX + (other.y - y) * axisY);
                var ownRadius = ProjectedRadius(axisX, axisY);
                var otherRadius = other.ProjectedRadius(axisX, axisY);
                var required = ownRadius + otherRadius + margin;
                // 매우 큰 유한 입력의 중간 계산이 넘쳐도 배치를 통과시키지 않는다.
                if (!Finite(distance) || !Finite(required)) return false;
                // 아주 작은 양수 영역을 0 크기로 취급하지 않도록 분리축 오차는 투영 크기에 비례한다.
                var projectionTolerance = Math.Max(distance, required) * 0.000000000001;
                return distance >= required - projectionTolerance;
            }

            private double ProjectedRadius(double axisX, double axisY)
            {
                return halfWidth * Math.Abs(cos * axisX + sin * axisY) +
                       halfHeight * Math.Abs(-sin * axisX + cos * axisY);
            }
        }
    }
}
