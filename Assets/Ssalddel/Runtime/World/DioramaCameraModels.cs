using System;

namespace Ssalddel.Unity.Runtime.World
{
    public static class DioramaCameraFocusLevelCodes
    {
        public const string World = "World";
        public const string Zone = "Zone";
        public const string Object = "Object";

        public static bool IsKnown(string value)
            => value == World || value == Zone || value == Object;
    }

    public readonly struct DioramaPoint
    {
        public DioramaPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public sealed class DioramaCameraFocus
    {
        public string AnchorId { get; set; } = string.Empty;
        public string LevelCode { get; set; } = string.Empty;
        public DioramaPoint Point { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(AnchorId))
                throw new InvalidOperationException("DioramaCameraFocusAnchorIdMissing");
            if (!DioramaCameraFocusLevelCodes.IsKnown(LevelCode))
                throw new InvalidOperationException("DioramaCameraFocusLevelInvalid:" + LevelCode);
        }
    }

    public sealed class DioramaCameraSettings
    {
        public float MinPitch { get; set; } = 45f;
        public float MaxPitch { get; set; } = 55f;
        public float MinDistance { get; set; } = 12f;
        public float MaxDistance { get; set; } = 110f;
        public float WorldDistance { get; set; } = 96f;
        public float ZoneDistance { get; set; } = 28f;
        public float ObjectDistance { get; set; } = 20f;
        public float MinFieldOfView { get; set; } = 25f;
        public float MaxFieldOfView { get; set; } = 35f;
        public float WorldFieldOfView { get; set; } = 35f;
        public float ZoneFieldOfView { get; set; } = 30f;
        public float ObjectFieldOfView { get; set; } = 28f;

        public void Validate()
        {
            if (MinPitch <= 0f || MaxPitch < MinPitch || MaxPitch >= 90f
                || MinDistance <= 0f || MaxDistance < MinDistance
                || MinFieldOfView <= 0f || MaxFieldOfView < MinFieldOfView
                || MaxFieldOfView >= 90f
                || !InRange(WorldDistance, MinDistance, MaxDistance)
                || !InRange(ZoneDistance, MinDistance, MaxDistance)
                || !InRange(ObjectDistance, MinDistance, MaxDistance)
                || !InRange(WorldFieldOfView, MinFieldOfView, MaxFieldOfView)
                || !InRange(ZoneFieldOfView, MinFieldOfView, MaxFieldOfView)
                || !InRange(ObjectFieldOfView, MinFieldOfView, MaxFieldOfView))
            {
                throw new InvalidOperationException("DioramaCameraSettingsInvalid");
            }
        }

        internal float DistanceFor(string levelCode)
            => levelCode switch
            {
                DioramaCameraFocusLevelCodes.World => WorldDistance,
                DioramaCameraFocusLevelCodes.Zone => ZoneDistance,
                DioramaCameraFocusLevelCodes.Object => ObjectDistance,
                _ => throw new InvalidOperationException("DioramaCameraFocusLevelInvalid:" + levelCode),
            };

        internal float FieldOfViewFor(string levelCode)
            => levelCode switch
            {
                DioramaCameraFocusLevelCodes.World => WorldFieldOfView,
                DioramaCameraFocusLevelCodes.Zone => ZoneFieldOfView,
                DioramaCameraFocusLevelCodes.Object => ObjectFieldOfView,
                _ => throw new InvalidOperationException("DioramaCameraFocusLevelInvalid:" + levelCode),
            };

        private static bool InRange(float value, float minimum, float maximum)
            => value >= minimum && value <= maximum;
    }

    public sealed class DioramaCameraState
    {
        public string FocusAnchorId { get; internal set; } = string.Empty;
        public string FocusLevelCode { get; internal set; } = string.Empty;
        public DioramaPoint FocusPoint { get; internal set; }
        public int YawQuarterTurns { get; internal set; }
        public float Pitch { get; internal set; }
        public float Distance { get; internal set; }
        public float FieldOfView { get; internal set; }
    }

    /// <summary>
    /// Presentation camera state only. It does not advance Simulation or authorize Operational work.
    /// </summary>
    public sealed class DioramaCameraStateMachine
    {
        private readonly DioramaCameraSettings settings;

        public DioramaCameraStateMachine(
            DioramaCameraSettings cameraSettings,
            DioramaCameraFocus initialFocus)
        {
            settings = cameraSettings ?? throw new ArgumentNullException(nameof(cameraSettings));
            settings.Validate();
            State = new DioramaCameraState
            {
                Pitch = Clamp(50f, settings.MinPitch, settings.MaxPitch),
            };
            Focus(initialFocus);
        }

        public DioramaCameraState State { get; }

        public void Focus(DioramaCameraFocus focus)
        {
            if (focus == null) throw new ArgumentNullException(nameof(focus));
            focus.Validate();
            State.FocusAnchorId = focus.AnchorId;
            State.FocusLevelCode = focus.LevelCode;
            State.FocusPoint = focus.Point;
            State.Distance = settings.DistanceFor(focus.LevelCode);
            State.FieldOfView = settings.FieldOfViewFor(focus.LevelCode);
        }

        public void PanWorld(float deltaX, float deltaZ)
        {
            State.FocusPoint = new DioramaPoint(
                State.FocusPoint.X + deltaX,
                State.FocusPoint.Y,
                State.FocusPoint.Z + deltaZ);
        }

        public void Zoom(float distanceDelta)
        {
            State.Distance = Clamp(
                State.Distance + distanceDelta,
                settings.MinDistance,
                settings.MaxDistance);
        }

        public void RotateQuarterTurns(int delta)
        {
            var normalized = (State.YawQuarterTurns + delta) % 4;
            State.YawQuarterTurns = normalized < 0 ? normalized + 4 : normalized;
        }

        public void SetPitch(float pitch)
        {
            State.Pitch = Clamp(pitch, settings.MinPitch, settings.MaxPitch);
        }

        public void SetFieldOfView(float fieldOfView)
        {
            State.FieldOfView = Clamp(
                fieldOfView,
                settings.MinFieldOfView,
                settings.MaxFieldOfView);
        }

        private static float Clamp(float value, float minimum, float maximum)
            => value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
