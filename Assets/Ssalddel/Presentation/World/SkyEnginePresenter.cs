using System;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public readonly struct SkyEnginePresentationModel
    {
        public SkyEnginePresentationModel(
            월드시간대PresentationModel composedTimeOfDay,
            float cloudCover,
            float precipitation,
            float windIntensity,
            bool precipitationVisible,
            bool lightningEnabled)
        {
            ComposedTimeOfDay = composedTimeOfDay;
            CloudCover = cloudCover;
            Precipitation = precipitation;
            WindIntensity = windIntensity;
            PrecipitationVisible = precipitationVisible;
            LightningEnabled = lightningEnabled;
        }

        public 월드시간대PresentationModel ComposedTimeOfDay { get; }
        public float CloudCover { get; }
        public float Precipitation { get; }
        public float WindIntensity { get; }
        public bool PrecipitationVisible { get; }
        public bool LightningEnabled { get; }
    }

    /// <summary>
    /// Simulation의 세계 대기 상태와 기존 시간대 표현을 합성합니다.
    /// GameObject, frame time 또는 카메라 상태를 권위 입력으로 사용하지 않습니다.
    /// </summary>
    public static class SkyEngineProjector
    {
        public static SkyEnginePresentationModel Compose(
            월드시간대PresentationModel timeOfDay,
            WorldAtmosphereState atmosphere,
            bool skyExposed)
        {
            if (atmosphere == null) throw new ArgumentNullException(
                nameof(atmosphere));
            var cloud = Mathf.Clamp01(atmosphere.CloudCoverPermille / 1000f);
            var precipitation = Mathf.Clamp01(
                atmosphere.PrecipitationPermille / 1000f);
            var wind = Mathf.Clamp01(atmosphere.WindIntensityPermille / 1000f);
            var storm = atmosphere.WeatherCode ==
                WorldWeatherCodes.Thunderstorm ? 1f : 0f;
            var overcast = Mathf.Clamp01(cloud * .72f + precipitation * .18f
                + storm * .1f);
            var skyGray = new Color(.32f, .38f, .45f);
            var groundGray = new Color(.16f, .18f, .18f);
            var fog = Color.Lerp(timeOfDay.FogColor,
                new Color(.34f, .4f, .44f), overcast);
            var composed = new 월드시간대PresentationModel(
                timeOfDay.NormalizedTime,
                timeOfDay.PreviousAnchor,
                timeOfDay.NextAnchor,
                timeOfDay.BlendWeight,
                timeOfDay.SunPitch,
                timeOfDay.SunYaw,
                Color.Lerp(timeOfDay.SunColor,
                    new Color(.72f, .77f, .82f), overcast * .72f),
                timeOfDay.SunIntensity * Mathf.Lerp(1f, .52f, overcast),
                timeOfDay.ShadowStrength * Mathf.Lerp(1f, .55f, overcast),
                Color.Lerp(timeOfDay.AmbientSky, skyGray, overcast),
                Color.Lerp(timeOfDay.AmbientEquator, skyGray, overcast * .82f),
                Color.Lerp(timeOfDay.AmbientGround, groundGray, overcast * .75f),
                fog,
                timeOfDay.FogDensity * Mathf.Lerp(1f, 2.25f,
                    Mathf.Max(cloud * .55f, precipitation)),
                Color.Lerp(timeOfDay.CameraBackground, fog, overcast * .78f),
                Color.Lerp(timeOfDay.SurfaceTint,
                    new Color(.68f, .73f, .76f), overcast * .52f),
                timeOfDay.SurfaceBrightness * Mathf.Lerp(1f, .66f, overcast));
            return new SkyEnginePresentationModel(
                composed,
                cloud,
                precipitation,
                wind,
                skyExposed && precipitation > .04f,
                atmosphere.LightningSequenceIndex > 0);
        }
    }

    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "권위 대기 상태를 카메라 전역 하늘·구름·강수·번개·음향으로 표현한다.",
        Boundary = "Sky Engine은 WorldTick이나 기상 전이를 직접 변경하지 않는다.")]
    public sealed class SkyEnginePresenter : MonoBehaviour,
        I월드시간대PresentationOutput
    {
        [SerializeField] private 월드시간대Presenter timeOfDay = null!;
        [SerializeField] private Nature생존Controller nature = null!;
        [SerializeField] private Camera targetCamera = null!;
        [SerializeField] private Transform cloudVisualRoot = null!;
        [SerializeField] private Transform rainVisualRoot = null!;
        [SerializeField] private Light lightningLight = null!;
        [SerializeField] private AudioSource rainAudio = null!;
        [SerializeField] private AudioSource thunderAudio = null!;

        private WorldAtmosphereState atmosphere = new();
        private SkyEnginePresentationModel currentModel;
        private float refreshRemaining;
        private float lightningRemaining;
        private int appliedLightningSequence;
        private AudioClip? generatedRainClip;
        private AudioClip? generatedThunderClip;
        private bool skyExposed = true;

        public string WeatherCode => atmosphere.WeatherCode;
        public string ProfileStableId => atmosphere.ProfileStableId;
        public bool RainVisible => rainVisualRoot != null
            && rainVisualRoot.gameObject.activeSelf;
        public int VisibleCloudCount => cloudVisualRoot == null ? 0
            : CountActiveChildren(cloudVisualRoot);
        public int AppliedLightningSequence => appliedLightningSequence;
        public bool SkyExposed => skyExposed;
        public SkyEnginePresentationModel CurrentModel => currentModel;

        public void Configure(
            월드시간대Presenter timeOfDayPresenter,
            Nature생존Controller natureController,
            Camera worldCamera,
            Transform clouds,
            Transform rain,
            Light flashLight,
            AudioSource rainSource,
            AudioSource thunderSource)
        {
            timeOfDay = timeOfDayPresenter;
            nature = natureController;
            targetCamera = worldCamera;
            cloudVisualRoot = clouds;
            rainVisualRoot = rain;
            lightningLight = flashLight;
            rainAudio = rainSource;
            thunderAudio = thunderSource;
            ConfigureRainVisual();
            EnsureAudio();
            lightningLight.enabled = false;
            timeOfDay.SetExternalOutput(this);
            RefreshFromAuthority(true);
        }

        public bool ValidateWiring()
            => timeOfDay != null
                && timeOfDay.ValidateWiring()
                && nature != null
                && nature.ValidateWiring()
                && targetCamera != null
                && cloudVisualRoot != null
                && cloudVisualRoot.childCount >= 3
                && rainVisualRoot != null
                && rainVisualRoot.GetComponentsInChildren<ParticleSystem>(true)
                    .Length > 0
                && lightningLight != null
                && rainAudio != null
                && thunderAudio != null;

        private void OnEnable()
        {
            if (timeOfDay != null) timeOfDay.SetExternalOutput(this);
        }

        private void OnDisable()
        {
            if (timeOfDay != null) timeOfDay.ClearExternalOutput(this);
            if (rainAudio != null) rainAudio.Stop();
            if (lightningLight != null) lightningLight.enabled = false;
        }

        private void Update()
        {
            refreshRemaining -= Time.unscaledDeltaTime;
            if (refreshRemaining <= 0f)
            {
                refreshRemaining = .2f;
                RefreshFromAuthority(false);
            }

            if (lightningRemaining > 0f)
            {
                lightningRemaining -= Time.unscaledDeltaTime;
                if (lightningRemaining <= 0f && lightningLight != null)
                    lightningLight.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            if (cloudVisualRoot != null)
                cloudVisualRoot.position = new Vector3(
                    targetCamera.transform.position.x,
                    cloudVisualRoot.position.y,
                    targetCamera.transform.position.z);
            if (rainVisualRoot != null && nature != null)
                rainVisualRoot.position = nature.PlayerWorldPosition + Vector3.up * 8f;
            if (lightningLight != null)
                lightningLight.transform.position = targetCamera.transform.position
                    + targetCamera.transform.forward * 4f;
        }

        public void RefreshFromAuthority(bool force)
        {
            // 저장 Scene이나 World 조립 경로가 Presenter를 먼저 활성화해도
            // Nature 권위 Runtime이 준비되기 전에는 상태 사본을 읽지 않는다.
            if (nature == null || timeOfDay == null || !nature.ValidateWiring())
                return;
            var state = nature.State;
            var incoming = state.Atmosphere ?? new WorldAtmosphereState();
            var nextSkyExposed = !state.PlayerInsideCabin
                && !SkyExposureVolume.IsCovered(nature.PlayerWorldPosition);
            var changed = force
                || atmosphere.WeatherCode != incoming.WeatherCode
                || atmosphere.NextWeatherCode != incoming.NextWeatherCode
                || atmosphere.TransitionProgressPermille !=
                    incoming.TransitionProgressPermille
                || atmosphere.LightningSequenceIndex !=
                    incoming.LightningSequenceIndex
                || skyExposed != nextSkyExposed;
            atmosphere = incoming.Clone();
            skyExposed = nextSkyExposed;
            if (!changed) return;
            timeOfDay.ApplyNowForTests(Mathf.Repeat(
                atmosphere.ElapsedSecondsInCycle
                / (float)WorldAtmosphereRules.CycleSeconds, 1f));
        }

        public void ApplyAtmosphereForTests(WorldAtmosphereState state,
            bool isSkyExposed)
        {
            atmosphere = state?.Clone() ?? throw new ArgumentNullException(
                nameof(state));
            skyExposed = isSkyExposed;
            if (timeOfDay != null) timeOfDay.ApplyNowForTests(Mathf.Repeat(
                atmosphere.ElapsedSecondsInCycle
                / (float)WorldAtmosphereRules.CycleSeconds, 1f));
        }

        public void ApplyTimeOfDay(월드시간대PresentationModel model)
        {
            currentModel = SkyEngineProjector.Compose(model, atmosphere,
                skyExposed);
            timeOfDay.ApplyComposedModel(currentModel.ComposedTimeOfDay);
            ApplyClouds(currentModel.CloudCover);
            ApplyRain(currentModel.Precipitation,
                currentModel.PrecipitationVisible);
            ApplyLightning(currentModel.LightningEnabled);
        }

        private void ApplyClouds(float cover)
        {
            if (cloudVisualRoot == null) return;
            var visibleCount = cover < .16f ? 0 : Mathf.Clamp(
                Mathf.CeilToInt(cover * cloudVisualRoot.childCount), 1,
                cloudVisualRoot.childCount);
            for (var index = 0; index < cloudVisualRoot.childCount; index++)
                cloudVisualRoot.GetChild(index).gameObject.SetActive(
                    index < visibleCount);
        }

        private void ApplyRain(float intensity, bool visible)
        {
            if (rainVisualRoot != null)
            {
                rainVisualRoot.gameObject.SetActive(visible);
                foreach (var renderer in rainVisualRoot
                             .GetComponentsInChildren<ParticleSystemRenderer>(true))
                    renderer.enabled = visible;
            }
            if (rainAudio == null) return;
            rainAudio.volume = visible ? Mathf.Lerp(.08f, .34f, intensity) : 0f;
            if (visible && !rainAudio.isPlaying) rainAudio.Play();
            else if (!visible && rainAudio.isPlaying) rainAudio.Stop();
        }

        private void ConfigureRainVisual()
        {
            if (rainVisualRoot == null) return;
            foreach (var renderer in rainVisualRoot
                         .GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                // Synty 원본의 Stretch 50은 근거리에서 굵은 기둥처럼 보인다.
                // 권위 강수량은 유지하고 표현 길이와 화면 점유만 제한한다.
                if (renderer.renderMode == ParticleSystemRenderMode.Stretch)
                    renderer.lengthScale = Mathf.Min(renderer.lengthScale, 6f);
                renderer.maxParticleSize = Mathf.Min(
                    renderer.maxParticleSize, .12f);
            }
        }

        private void ApplyLightning(bool enabled)
        {
            if (!enabled)
            {
                appliedLightningSequence = 0;
                return;
            }
            if (atmosphere.LightningSequenceIndex <= 0
                || atmosphere.LightningSequenceIndex == appliedLightningSequence)
                return;
            appliedLightningSequence = atmosphere.LightningSequenceIndex;
            if (lightningLight != null)
            {
                lightningLight.enabled = true;
                lightningRemaining = .16f;
            }
            if (thunderAudio != null && generatedThunderClip != null)
                thunderAudio.PlayOneShot(generatedThunderClip, .52f);
        }

        private void EnsureAudio()
        {
            if (rainAudio != null && rainAudio.clip == null)
            {
                generatedRainClip = CreateRainClip();
                rainAudio.clip = generatedRainClip;
                rainAudio.loop = true;
                rainAudio.playOnAwake = false;
                rainAudio.spatialBlend = 0f;
            }
            if (thunderAudio != null)
            {
                generatedThunderClip ??= CreateThunderClip();
                thunderAudio.playOnAwake = false;
                thunderAudio.spatialBlend = 0f;
            }
        }

        private static AudioClip CreateRainClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate * 2];
            var random = new System.Random(1701);
            var filtered = 0f;
            for (var index = 0; index < samples.Length; index++)
            {
                var noise = (float)(random.NextDouble() * 2d - 1d);
                filtered = filtered * .82f + noise * .18f;
                samples[index] = filtered * .18f;
            }
            var clip = AudioClip.Create("SkyEngine_Rain_r1", samples.Length,
                1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateThunderClip()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            var random = new System.Random(31415);
            for (var index = 0; index < samples.Length; index++)
            {
                var t = index / (float)sampleRate;
                var envelope = Mathf.Exp(-3.2f * t);
                var rumble = Mathf.Sin(t * Mathf.PI * 2f * 47f) * .34f;
                var crack = index < sampleRate / 18
                    ? (float)(random.NextDouble() * 2d - 1d) * .55f : 0f;
                samples[index] = (rumble + crack) * envelope;
            }
            var clip = AudioClip.Create("SkyEngine_Thunder_r1", samples.Length,
                1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static int CountActiveChildren(Transform root)
        {
            var count = 0;
            for (var index = 0; index < root.childCount; index++)
                if (root.GetChild(index).gameObject.activeSelf) count++;
            return count;
        }

        private void OnDestroy()
        {
            if (generatedRainClip != null) Destroy(generatedRainClip);
            if (generatedThunderClip != null) Destroy(generatedThunderClip);
        }
    }
}
