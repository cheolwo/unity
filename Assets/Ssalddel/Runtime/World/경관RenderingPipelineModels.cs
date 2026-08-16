using System;
using System.Globalization;
using System.Text;

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
        public float SunColorR = 1f;
        public float SunColorG = 1f;
        public float SunColorB = 1f;
        public float ShadowStrength;
        public float ShadowBias;
        public float ShadowNormalBias;
        public float ShadowDistance;
        public int ShadowCascadeCount;
        public float ShadowCascade1;
        public float ShadowCascade2;
        public float ShadowCascade3;
        public float AmbientSkyR;
        public float AmbientSkyG;
        public float AmbientSkyB;
        public float AmbientEquatorR;
        public float AmbientEquatorG;
        public float AmbientEquatorB;
        public float AmbientGroundR;
        public float AmbientGroundG;
        public float AmbientGroundB;
        public float AmbientIntensity;
        public float FogColorR;
        public float FogColorG;
        public float FogColorB;
        public float FogStartDistance;
        public float FogEndDistance;
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public float ColorFilterR = 1f;
        public float ColorFilterG = 1f;
        public float ColorFilterB = 1f;
        public float WhiteBalanceTemperature;
        public float WhiteBalanceTint;
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomScatter;
        public float VignetteIntensity;
        public float VignetteSmoothness;
        public float AmbientOcclusionIntensity;
        public float AmbientOcclusionRadius;
        public float AmbientOcclusionDirectLightingStrength;
        public float FirstPersonEyeHeight;
        public float FirstPersonFieldOfView;
        public bool PresentationOnly = true;

        public bool Validate()
            => !string.IsNullOrWhiteSpace(ProfileStableId)
                && !string.IsNullOrWhiteSpace(RuleRevision)
                && SunIntensity > 0f
                && SunPitch is > 0f and < 90f
                && IsColor(SunColorR, SunColorG, SunColorB)
                && ShadowStrength is >= 0f and <= 1f
                && ShadowBias >= 0f
                && ShadowNormalBias >= 0f
                && ShadowDistance > 0f
                && ShadowCascadeCount == 4
                && ShadowCascade1 is > 0f and < 1f
                && ShadowCascade2 > ShadowCascade1 && ShadowCascade2 < 1f
                && ShadowCascade3 > ShadowCascade2 && ShadowCascade3 < 1f
                && IsColor(AmbientSkyR, AmbientSkyG, AmbientSkyB)
                && IsColor(AmbientEquatorR, AmbientEquatorG, AmbientEquatorB)
                && IsColor(AmbientGroundR, AmbientGroundG, AmbientGroundB)
                && AmbientIntensity > 0f
                && IsColor(FogColorR, FogColorG, FogColorB)
                && FogEndDistance > FogStartDistance
                && IsColor(ColorFilterR, ColorFilterG, ColorFilterB)
                && WhiteBalanceTemperature is >= -100f and <= 100f
                && WhiteBalanceTint is >= -100f and <= 100f
                && BloomThreshold >= 0f
                && BloomIntensity >= 0f
                && BloomScatter is >= 0f and <= 1f
                && VignetteIntensity is >= 0f and <= 1f
                && VignetteSmoothness is >= 0f and <= 1f
                && AmbientOcclusionIntensity is >= 0f and <= 4f
                && AmbientOcclusionRadius is > 0f and <= 1f
                && AmbientOcclusionDirectLightingStrength is >= 0f and <= 1f
                && FirstPersonEyeHeight is >= 1f and <= 2.2f
                && FirstPersonFieldOfView is >= 45f and <= 85f
                && PresentationOnly;

        private static bool IsColor(float red, float green, float blue) =>
            red is >= 0f and <= 2f
            && green is >= 0f and <= 2f
            && blue is >= 0f and <= 2f;
    }

    public static class 평창군경관RenderingFixture
    {
        public static 경관RenderingProfile Create()
        {
            var value = new 경관RenderingProfile
            {
                ProfileStableId =
                    "rendering-profile:sim:pyeongchang:rural-clear-late-morning.v2",
                RuleRevision = "landscape-quality-pass.v2",
                SunIntensity = 1.30f,
                SunPitch = 42f,
                SunYaw = -32f,
                SunColorR = 1f,
                SunColorG = .93f,
                SunColorB = .82f,
                ShadowStrength = .82f,
                ShadowBias = .04f,
                ShadowNormalBias = .25f,
                ShadowDistance = 80f,
                ShadowCascadeCount = 4,
                ShadowCascade1 = .10f,
                ShadowCascade2 = .28f,
                ShadowCascade3 = .58f,
                AmbientSkyR = .55f,
                AmbientSkyG = .66f,
                AmbientSkyB = .78f,
                AmbientEquatorR = .42f,
                AmbientEquatorG = .45f,
                AmbientEquatorB = .38f,
                AmbientGroundR = .17f,
                AmbientGroundG = .18f,
                AmbientGroundB = .14f,
                AmbientIntensity = .82f,
                FogColorR = .58f,
                FogColorG = .67f,
                FogColorB = .72f,
                FogStartDistance = 75f,
                FogEndDistance = 230f,
                PostExposure = .08f,
                Contrast = 12f,
                Saturation = 5f,
                ColorFilterR = 1f,
                ColorFilterG = .98f,
                ColorFilterB = .94f,
                WhiteBalanceTemperature = 2f,
                WhiteBalanceTint = -2f,
                BloomThreshold = 1.05f,
                BloomIntensity = .08f,
                BloomScatter = .55f,
                VignetteIntensity = .09f,
                VignetteSmoothness = .32f,
                AmbientOcclusionIntensity = .35f,
                AmbientOcclusionRadius = .25f,
                AmbientOcclusionDirectLightingStrength = .20f,
                FirstPersonEyeHeight = 1.68f,
                FirstPersonFieldOfView = 62f,
            };
            if (!value.Validate())
                throw new InvalidOperationException("LandscapeRenderingProfileInvalid");
            return value;
        }
    }

    public static class 경관RenderingProfileHash
    {
        public static string Compute(경관RenderingProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Validate())
                throw new InvalidOperationException("LandscapeRenderingProfileInvalid");
            var builder = new StringBuilder();
            Append(builder,
                profile.ProfileStableId, profile.RuleRevision,
                profile.SunIntensity, profile.SunPitch, profile.SunYaw,
                profile.SunColorR, profile.SunColorG, profile.SunColorB,
                profile.ShadowStrength, profile.ShadowBias, profile.ShadowNormalBias,
                profile.ShadowDistance, profile.ShadowCascadeCount,
                profile.ShadowCascade1, profile.ShadowCascade2, profile.ShadowCascade3,
                profile.AmbientSkyR, profile.AmbientSkyG, profile.AmbientSkyB,
                profile.AmbientEquatorR, profile.AmbientEquatorG,
                profile.AmbientEquatorB, profile.AmbientGroundR,
                profile.AmbientGroundG, profile.AmbientGroundB,
                profile.AmbientIntensity, profile.FogColorR,
                profile.FogColorG, profile.FogColorB,
                profile.FogStartDistance, profile.FogEndDistance,
                profile.PostExposure, profile.Contrast, profile.Saturation,
                profile.ColorFilterR, profile.ColorFilterG, profile.ColorFilterB,
                profile.WhiteBalanceTemperature, profile.WhiteBalanceTint,
                profile.BloomThreshold, profile.BloomIntensity, profile.BloomScatter,
                profile.VignetteIntensity, profile.VignetteSmoothness,
                profile.AmbientOcclusionIntensity, profile.AmbientOcclusionRadius,
                profile.AmbientOcclusionDirectLightingStrength,
                profile.FirstPersonEyeHeight, profile.FirstPersonFieldOfView,
                profile.PresentationOnly);
            return 정적경관배치PlanHash.Sha256(builder.ToString());
        }

        private static void Append(StringBuilder builder, params object[] values)
        {
            foreach (var value in values)
            {
                var text = value is float number
                    ? number.ToString("R", CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                builder.Append(text).Append('|');
            }
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
