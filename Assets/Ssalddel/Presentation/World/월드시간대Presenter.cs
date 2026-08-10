using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 월드시간대SourceMode
    {
        FixedReference = 0,
        PreviewScrub = 1,
        SimulationClock = 2,
        OperationalObservation = 3,
    }

    public enum 월드시간대AnchorCode
    {
        Dawn = 0,
        Morning = 1,
        Midday = 2,
        Afternoon = 3,
        GoldenDusk = 4,
        Night = 5,
    }

    public readonly struct 월드시간대PresentationModel
    {
        public 월드시간대PresentationModel(
            float normalizedTime,
            월드시간대AnchorCode previousAnchor,
            월드시간대AnchorCode nextAnchor,
            float blendWeight,
            float sunPitch,
            float sunYaw,
            Color sunColor,
            float sunIntensity,
            float shadowStrength,
            Color ambientSky,
            Color ambientEquator,
            Color ambientGround,
            Color fogColor,
            float fogDensity,
            Color cameraBackground,
            Color surfaceTint,
            float surfaceBrightness)
        {
            NormalizedTime = normalizedTime;
            PreviousAnchor = previousAnchor;
            NextAnchor = nextAnchor;
            BlendWeight = blendWeight;
            SunPitch = sunPitch;
            SunYaw = sunYaw;
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            ShadowStrength = shadowStrength;
            AmbientSky = ambientSky;
            AmbientEquator = ambientEquator;
            AmbientGround = ambientGround;
            FogColor = fogColor;
            FogDensity = fogDensity;
            CameraBackground = cameraBackground;
            SurfaceTint = surfaceTint;
            SurfaceBrightness = surfaceBrightness;
        }

        public float NormalizedTime { get; }
        public 월드시간대AnchorCode PreviousAnchor { get; }
        public 월드시간대AnchorCode NextAnchor { get; }
        public float BlendWeight { get; }
        public float SunPitch { get; }
        public float SunYaw { get; }
        public Color SunColor { get; }
        public float SunIntensity { get; }
        public float ShadowStrength { get; }
        public Color AmbientSky { get; }
        public Color AmbientEquator { get; }
        public Color AmbientGround { get; }
        public Color FogColor { get; }
        public float FogDensity { get; }
        public Color CameraBackground { get; }
        public Color SurfaceTint { get; }
        public float SurfaceBrightness { get; }
    }

    public static class 월드시간대Interpreter
    {
        private readonly struct Anchor
        {
            public Anchor(
                월드시간대AnchorCode code, float time, float pitch, float yaw,
                Color sun, float intensity, float shadow,
                Color sky, Color equator, Color ground,
                Color fog, float fogDensity, Color background,
                Color surfaceTint, float surfaceBrightness)
            {
                Code = code;
                Time = time;
                Pitch = pitch;
                Yaw = yaw;
                Sun = sun;
                Intensity = intensity;
                Shadow = shadow;
                Sky = sky;
                Equator = equator;
                Ground = ground;
                Fog = fog;
                FogDensity = fogDensity;
                Background = background;
                SurfaceTint = surfaceTint;
                SurfaceBrightness = surfaceBrightness;
            }

            public 월드시간대AnchorCode Code { get; }
            public float Time { get; }
            public float Pitch { get; }
            public float Yaw { get; }
            public Color Sun { get; }
            public float Intensity { get; }
            public float Shadow { get; }
            public Color Sky { get; }
            public Color Equator { get; }
            public Color Ground { get; }
            public Color Fog { get; }
            public float FogDensity { get; }
            public Color Background { get; }
            public Color SurfaceTint { get; }
            public float SurfaceBrightness { get; }
        }

        private static readonly Anchor[] Anchors =
        {
            new(월드시간대AnchorCode.Dawn, 5.5f / 24f, 12f, -104f,
                new Color(1f, .58f, .43f), .48f, .55f,
                new Color(.27f, .35f, .51f), new Color(.47f, .38f, .42f),
                new Color(.12f, .15f, .2f), new Color(.48f, .43f, .49f), .003f,
                new Color(.31f, .38f, .5f), new Color(.75f, .78f, .9f), .78f),
            new(월드시간대AnchorCode.Morning, 8.5f / 24f, 25f, -66f,
                new Color(1f, .77f, .57f), .91f, .68f,
                new Color(.52f, .66f, .81f), new Color(.63f, .55f, .48f),
                new Color(.19f, .22f, .18f), new Color(.62f, .65f, .61f), .0025f,
                new Color(.55f, .66f, .76f), new Color(.91f, .88f, .82f), .92f),
            new(월드시간대AnchorCode.Midday, 12.5f / 24f, 38f, -32f,
                new Color(1f, .89f, .72f), 1.18f, .78f,
                new Color(.65f, .74f, .86f), new Color(.69f, .61f, .48f),
                new Color(.22f, .25f, .17f), new Color(.7f, .73f, .64f), .0022f,
                new Color(.64f, .72f, .76f), Color.white, 1f),
            new(월드시간대AnchorCode.Afternoon, 16f / 24f, 29f, 21f,
                new Color(1f, .78f, .56f), 1.02f, .72f,
                new Color(.57f, .65f, .76f), new Color(.7f, .53f, .39f),
                new Color(.23f, .22f, .15f), new Color(.7f, .65f, .55f), .00235f,
                new Color(.57f, .63f, .68f), new Color(1f, .91f, .76f), .96f),
            new(월드시간대AnchorCode.GoldenDusk, 18.5f / 24f, 12f, 73f,
                new Color(1f, .48f, .27f), .67f, .6f,
                new Color(.39f, .4f, .58f), new Color(.62f, .39f, .31f),
                new Color(.17f, .17f, .2f), new Color(.55f, .43f, .42f), .0028f,
                new Color(.4f, .4f, .53f), new Color(1f, .71f, .52f), .82f),
            new(월드시간대AnchorCode.Night, 21f / 24f, 20f, 142f,
                new Color(.48f, .61f, .86f), .3f, .42f,
                new Color(.12f, .18f, .34f), new Color(.17f, .21f, .32f),
                new Color(.06f, .09f, .14f), new Color(.12f, .17f, .28f), .0031f,
                new Color(.09f, .14f, .26f), new Color(.57f, .68f, .9f), .66f),
        };

        public static 월드시간대PresentationModel Evaluate(float normalizedTime)
        {
            var time = Mathf.Repeat(normalizedTime, 1f);
            var previousIndex = Anchors.Length - 1;
            var nextIndex = 0;
            for (var index = 0; index < Anchors.Length; index++)
            {
                if (time < Anchors[index].Time)
                {
                    nextIndex = index;
                    previousIndex = index == 0 ? Anchors.Length - 1 : index - 1;
                    break;
                }

                if (index == Anchors.Length - 1)
                {
                    previousIndex = index;
                    nextIndex = 0;
                }
            }

            var previous = Anchors[previousIndex];
            var next = Anchors[nextIndex];
            var previousTime = previous.Time;
            var nextTime = next.Time;
            var sampleTime = time;
            if (nextTime <= previousTime) nextTime += 1f;
            if (sampleTime < previousTime) sampleTime += 1f;
            var linear = Mathf.InverseLerp(previousTime, nextTime, sampleTime);
            var blend = linear * linear * (3f - 2f * linear);

            return new 월드시간대PresentationModel(
                time, previous.Code, next.Code, blend,
                Mathf.Lerp(previous.Pitch, next.Pitch, blend),
                Mathf.LerpAngle(previous.Yaw, next.Yaw, blend),
                Color.Lerp(previous.Sun, next.Sun, blend),
                Mathf.Lerp(previous.Intensity, next.Intensity, blend),
                Mathf.Lerp(previous.Shadow, next.Shadow, blend),
                Color.Lerp(previous.Sky, next.Sky, blend),
                Color.Lerp(previous.Equator, next.Equator, blend),
                Color.Lerp(previous.Ground, next.Ground, blend),
                Color.Lerp(previous.Fog, next.Fog, blend),
                Mathf.Lerp(previous.FogDensity, next.FogDensity, blend),
                Color.Lerp(previous.Background, next.Background, blend),
                Color.Lerp(previous.SurfaceTint, next.SurfaceTint, blend),
                Mathf.Lerp(previous.SurfaceBrightness, next.SurfaceBrightness, blend));
        }
    }

    [DisallowMultipleComponent]
    public sealed class 월드시간대Presenter : MonoBehaviour
    {
        private sealed class SurfaceBinding
        {
            public Renderer Renderer = null!;
            public int MaterialIndex;
            public int ColorPropertyId;
            public Color OriginalColor;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private 월드시간대SourceMode sourceMode =
            월드시간대SourceMode.FixedReference;
        [SerializeField, Range(0f, 1f)] private float normalizedTime = 12.5f / 24f;
        [SerializeField] private bool autoCycleInPlayMode;
        [SerializeField, Min(10f)] private float cycleDurationSeconds = 180f;
        [SerializeField] private Light directionalLight = null!;
        [SerializeField] private Camera targetCamera = null!;
        [SerializeField] private Transform surfaceRoot = null!;

        private readonly List<SurfaceBinding> surfaceBindings = new();
        private MaterialPropertyBlock propertyBlock = null!;

        public 월드시간대SourceMode SourceMode => sourceMode;
        public float NormalizedTime => normalizedTime;
        public bool AutoCycleInPlayMode => autoCycleInPlayMode;
        public int SurfaceBindingCount => surfaceBindings.Count;
        public 월드시간대PresentationModel CurrentModel { get; private set; }

        private void OnEnable()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            RebuildSurfaceBindings();
            if (directionalLight != null && targetCamera != null && surfaceRoot != null)
                ApplyNowForTests(normalizedTime);
        }

        public void Configure(
            Light worldDirectionalLight,
            Camera worldCamera,
            Transform worldSurfaceRoot,
            float initialNormalizedTime,
            bool playAutomatically,
            float dayDurationSeconds = 180f)
        {
            directionalLight = worldDirectionalLight;
            targetCamera = worldCamera;
            surfaceRoot = worldSurfaceRoot;
            normalizedTime = Mathf.Repeat(initialNormalizedTime, 1f);
            autoCycleInPlayMode = playAutomatically;
            cycleDurationSeconds = Mathf.Max(10f, dayDurationSeconds);
            sourceMode = playAutomatically
                ? 월드시간대SourceMode.PreviewScrub
                : 월드시간대SourceMode.FixedReference;
            propertyBlock ??= new MaterialPropertyBlock();
            RebuildSurfaceBindings();
            ApplyNowForTests(normalizedTime);
        }

        public bool ValidateWiring()
        {
            if (surfaceBindings.Count == 0 && surfaceRoot != null)
                RebuildSurfaceBindings();
            return directionalLight != null
                && directionalLight.type == LightType.Directional
                && targetCamera != null
                && surfaceRoot != null
                && (surfaceRoot == transform || surfaceRoot.IsChildOf(transform))
                && cycleDurationSeconds >= 10f
                && surfaceBindings.Count > 0;
        }

        public void ApplyNowForTests(float value)
        {
            normalizedTime = Mathf.Repeat(value, 1f);
            CurrentModel = 월드시간대Interpreter.Evaluate(normalizedTime);
            Apply(CurrentModel);
        }

        public void RebuildSurfaceBindings()
        {
            surfaceBindings.Clear();
            if (surfaceRoot == null) return;

            foreach (var renderer in surfaceRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    if (material == null) continue;
                    var propertyId = material.HasProperty(BaseColorId)
                        ? BaseColorId
                        : material.HasProperty(ColorId) ? ColorId : 0;
                    if (propertyId == 0) continue;
                    surfaceBindings.Add(new SurfaceBinding
                    {
                        Renderer = renderer,
                        MaterialIndex = index,
                        ColorPropertyId = propertyId,
                        OriginalColor = material.GetColor(propertyId),
                    });
                }
            }
        }

        private void Update()
        {
            if (!autoCycleInPlayMode || sourceMode != 월드시간대SourceMode.PreviewScrub)
                return;
            ApplyNowForTests(normalizedTime + Time.deltaTime / cycleDurationSeconds);
        }

        private void Apply(월드시간대PresentationModel model)
        {
            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(
                    model.SunPitch, model.SunYaw, 0f);
                directionalLight.color = model.SunColor;
                directionalLight.intensity = model.SunIntensity;
                directionalLight.shadows = LightShadows.Soft;
                directionalLight.shadowStrength = model.ShadowStrength;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = model.AmbientSky;
            RenderSettings.ambientEquatorColor = model.AmbientEquator;
            RenderSettings.ambientGroundColor = model.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = model.FogColor;
            RenderSettings.fogDensity = model.FogDensity;
            if (targetCamera != null) targetCamera.backgroundColor = model.CameraBackground;

            var tint = model.SurfaceTint * model.SurfaceBrightness;
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var binding in surfaceBindings)
            {
                if (binding.Renderer == null) continue;
                binding.Renderer.GetPropertyBlock(propertyBlock, binding.MaterialIndex);
                var color = binding.OriginalColor * tint;
                color.a = binding.OriginalColor.a;
                propertyBlock.SetColor(binding.ColorPropertyId, color);
                binding.Renderer.SetPropertyBlock(propertyBlock, binding.MaterialIndex);
                propertyBlock.Clear();
            }
        }
    }
}
