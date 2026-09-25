using System;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>
    /// 현재 한반도 곡면 Mesh의 표본 간격을 화면 픽셀 오차로 환산하는 비교용 선택기다.
    /// 실제 DEM의 기하 오차나 정식 지리 타일 판본을 뜻하지 않는다.
    /// </summary>
    public static class 한반도지형타일화면오차Selector
    {
        private const double DegreesToRadians = Math.PI / 180d;
        private const double MinimumSurfaceDistance = .0001d;

        private static readonly LevelSpec[] Levels =
        {
            new(0, 1, 12d, 9d, 48, 36),
            new(1, 4, 6d, 4.5d, 48, 36),
            new(2, 16, 3d, 2.25d, 32, 24),
        };

        public static 한반도지형타일화면오차선택결과 Select(
            double targetSsePixels,
            int viewportHeightPixels,
            double verticalFovDegrees,
            double cameraCenterDistance,
            double globeRadius,
            double centerLatitude)
        {
            Validate(targetSsePixels, viewportHeightPixels, verticalFovDegrees,
                cameraCenterDistance, globeRadius, centerLatitude);

            for (var index = 0; index < Levels.Length; index++)
            {
                var level = Levels[index];
                var estimatedSse = Estimate(level, viewportHeightPixels,
                    verticalFovDegrees, cameraCenterDistance, globeRadius, centerLatitude);
                if (estimatedSse <= targetSsePixels || index == Levels.Length - 1)
                {
                    return new 한반도지형타일화면오차선택결과(
                        level.DetailLevel,
                        level.TileCount,
                        targetSsePixels,
                        estimatedSse,
                        index == Levels.Length - 1 && estimatedSse > targetSsePixels);
                }
            }

            throw new InvalidOperationException("KoreanPeninsulaTerrainSseSelectionUnavailable");
        }

        public static double EstimateLevelSse(
            int detailLevel,
            int viewportHeightPixels,
            double verticalFovDegrees,
            double cameraCenterDistance,
            double globeRadius,
            double centerLatitude)
        {
            if (detailLevel < 0 || detailLevel >= Levels.Length)
                throw new ArgumentOutOfRangeException(nameof(detailLevel));
            Validate(1d, viewportHeightPixels, verticalFovDegrees,
                cameraCenterDistance, globeRadius, centerLatitude);
            return Estimate(Levels[detailLevel], viewportHeightPixels,
                verticalFovDegrees, cameraCenterDistance, globeRadius, centerLatitude);
        }

        private static double Estimate(
            LevelSpec level,
            int viewportHeightPixels,
            double verticalFovDegrees,
            double cameraCenterDistance,
            double globeRadius,
            double centerLatitude)
        {
            var projectionScale = viewportHeightPixels
                / (2d * Math.Tan(verticalFovDegrees * DegreesToRadians * .5d));
            var surfaceDistance = Math.Max(cameraCenterDistance - globeRadius,
                MinimumSurfaceDistance);
            var latitudeStep = level.LatitudeSpanDegrees / level.LatitudeSegments;
            var longitudeGroundStep = level.LongitudeSpanDegrees / level.LongitudeSegments
                * Math.Cos(centerLatitude * DegreesToRadians);
            var sampleSpacingWorld = globeRadius * DegreesToRadians
                * Math.Sqrt(latitudeStep * latitudeStep
                    + longitudeGroundStep * longitudeGroundStep);
            return sampleSpacingWorld * projectionScale / surfaceDistance;
        }

        private static void Validate(
            double targetSsePixels,
            int viewportHeightPixels,
            double verticalFovDegrees,
            double cameraCenterDistance,
            double globeRadius,
            double centerLatitude)
        {
            if (!double.IsFinite(targetSsePixels) || targetSsePixels <= 0d)
                throw new ArgumentOutOfRangeException(nameof(targetSsePixels));
            if (viewportHeightPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewportHeightPixels));
            if (!double.IsFinite(verticalFovDegrees)
                || verticalFovDegrees <= 0d
                || verticalFovDegrees >= 180d)
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            if (!double.IsFinite(globeRadius) || globeRadius <= 0d)
                throw new ArgumentOutOfRangeException(nameof(globeRadius));
            if (!double.IsFinite(cameraCenterDistance)
                || cameraCenterDistance <= globeRadius)
                throw new ArgumentOutOfRangeException(nameof(cameraCenterDistance));
            if (!double.IsFinite(centerLatitude)
                || centerLatitude < -90d
                || centerLatitude > 90d)
                throw new ArgumentOutOfRangeException(nameof(centerLatitude));
        }

        private sealed class LevelSpec
        {
            public LevelSpec(int detailLevel, int tileCount,
                double latitudeSpanDegrees, double longitudeSpanDegrees,
                int latitudeSegments, int longitudeSegments)
            {
                DetailLevel = detailLevel;
                TileCount = tileCount;
                LatitudeSpanDegrees = latitudeSpanDegrees;
                LongitudeSpanDegrees = longitudeSpanDegrees;
                LatitudeSegments = latitudeSegments;
                LongitudeSegments = longitudeSegments;
            }

            public int DetailLevel { get; }
            public int TileCount { get; }
            public double LatitudeSpanDegrees { get; }
            public double LongitudeSpanDegrees { get; }
            public int LatitudeSegments { get; }
            public int LongitudeSegments { get; }
        }
    }

    public readonly struct 한반도지형타일화면오차선택결과
    {
        public 한반도지형타일화면오차선택결과(
            int detailLevel,
            int activeTileCount,
            double targetSsePixels,
            double estimatedSsePixels,
            bool detailCapReached)
        {
            DetailLevel = detailLevel;
            ActiveTileCount = activeTileCount;
            TargetSsePixels = targetSsePixels;
            EstimatedSsePixels = estimatedSsePixels;
            DetailCapReached = detailCapReached;
        }

        public int DetailLevel { get; }
        public int ActiveTileCount { get; }
        public double TargetSsePixels { get; }
        public double EstimatedSsePixels { get; }
        public bool DetailCapReached { get; }
    }
}
