using System;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable]
    public sealed class 경관RenderingProfile
    {
        public string ProfileStableId = string.Empty;
        public string RuleRevision = string.Empty;
        public float SunIntensity;
        public float SunPitch;
        public float SunYaw;
        public float ShadowDistance;
        public float FogStartDistance;
        public float FogEndDistance;
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public float BloomIntensity;
        public float VignetteIntensity;
        public float FirstPersonEyeHeight;
        public float FirstPersonFieldOfView;
        public bool PresentationOnly = true;

        public bool Validate()
            => !string.IsNullOrWhiteSpace(ProfileStableId)
                && !string.IsNullOrWhiteSpace(RuleRevision)
                && SunIntensity > 0f
                && SunPitch is > 0f and < 90f
                && ShadowDistance > 0f
                && FogEndDistance > FogStartDistance
                && BloomIntensity >= 0f
                && VignetteIntensity is >= 0f and <= 1f
                && FirstPersonEyeHeight is >= 1f and <= 2.2f
                && FirstPersonFieldOfView is >= 45f and <= 85f
                && PresentationOnly;
    }

    public static class 평창군경관RenderingFixture
    {
        public static 경관RenderingProfile Create()
        {
            var value = new 경관RenderingProfile
            {
                ProfileStableId = "rendering-profile:sim:pyeongchang:rural-clear-day.v1",
                RuleRevision = "landscape-quality-pass.v1",
                SunIntensity = 1.42f,
                SunPitch = 42f,
                SunYaw = -28f,
                ShadowDistance = 90f,
                FogStartDistance = 65f,
                FogEndDistance = 220f,
                PostExposure = .18f,
                Contrast = 16f,
                Saturation = 8f,
                BloomIntensity = .12f,
                VignetteIntensity = .14f,
                FirstPersonEyeHeight = 1.68f,
                FirstPersonFieldOfView = 62f,
            };
            if (!value.Validate())
                throw new InvalidOperationException("LandscapeRenderingProfileInvalid");
            return value;
        }
    }

    [Serializable]
    public sealed class 플레이어경관Profile
    {
        public string ProfileStableId = string.Empty;
        public float WalkSpeed;
        public float RunMultiplier;
        public float CapsuleHeight;
        public float CapsuleRadius;
        public float CameraDistance;
        public float CameraHeight;
        public float CameraFieldOfView;
        public float TacticalYaw;
        public float TacticalPitch;
        public float TacticalMinimumDistance;
        public float TacticalMaximumDistance;
        public float TacticalPanSpeed;
        public float TacticalZoomSpeed;
        public float FirstPersonEyeHeight;
        public float FirstPersonFieldOfView;
        public float ClickMoveStopDistance;
        public float InitialPitch;
        public float LookSensitivity;
        public float MinimumX;
        public float MaximumX;
        public float MinimumZ;
        public float MaximumZ;
        public bool PresentationOnly = true;

        public bool Validate()
            => !string.IsNullOrWhiteSpace(ProfileStableId)
                && WalkSpeed > 0f
                && RunMultiplier >= 1f
                && CapsuleHeight is >= 1.2f and <= 2.4f
                && CapsuleRadius is >= .2f and <= .7f
                && CameraDistance is >= 8f and <= 24f
                && CameraHeight is >= .5f and <= 3f
                && CameraFieldOfView is >= 45f and <= 85f
                && TacticalYaw is >= -180f and <= 180f
                && TacticalPitch is >= 35f and <= 70f
                && TacticalMinimumDistance is >= 6f and <= 16f
                && TacticalMaximumDistance > TacticalMinimumDistance
                && TacticalMaximumDistance <= 32f
                && CameraDistance >= TacticalMinimumDistance
                && CameraDistance <= TacticalMaximumDistance
                && TacticalPanSpeed > 0f
                && TacticalZoomSpeed > 0f
                && FirstPersonEyeHeight is >= 1.2f and <= 2.2f
                && FirstPersonFieldOfView is >= 45f and <= 90f
                && ClickMoveStopDistance is >= .05f and <= 1f
                && InitialPitch is >= -10f and <= 45f
                && LookSensitivity > 0f
                && MaximumX > MinimumX
                && MaximumZ > MinimumZ
                && PresentationOnly;
    }

    public static class 평창군플레이어경관Fixture
    {
        public static 플레이어경관Profile Create()
        {
            var value = new 플레이어경관Profile
            {
                ProfileStableId = "player-profile:sim:pyeongchang:farm-explorer.v1",
                WalkSpeed = 3.6f,
                RunMultiplier = 1.7f,
                CapsuleHeight = 1.78f,
                CapsuleRadius = .34f,
                CameraDistance = 15.5f,
                CameraHeight = 1.1f,
                CameraFieldOfView = 52f,
                TacticalYaw = 36f,
                TacticalPitch = 52f,
                TacticalMinimumDistance = 9f,
                TacticalMaximumDistance = 23f,
                TacticalPanSpeed = 10f,
                TacticalZoomSpeed = 1.2f,
                FirstPersonEyeHeight = 1.68f,
                FirstPersonFieldOfView = 64f,
                ClickMoveStopDistance = .18f,
                InitialPitch = 2f,
                LookSensitivity = .09f,
                MinimumX = 10.5f,
                MaximumX = 31.5f,
                MinimumZ = 2.5f,
                MaximumZ = 22f,
            };
            if (!value.Validate())
                throw new InvalidOperationException("PlayerLandscapeProfileInvalid");
            return value;
        }
    }
}
