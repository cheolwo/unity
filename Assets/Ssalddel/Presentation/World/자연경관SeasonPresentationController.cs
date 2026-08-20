using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 자연경관SeasonPresentationController : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField] private Transform compositionRoot = null!;
        [SerializeField] private 자연경관SeasonFxBinding[] fxBindings =
            Array.Empty<자연경관SeasonFxBinding>();
        [SerializeField] private string activeSeasonCode = 자연경관SeasonCodes.Spring;
        [SerializeField] private bool presentationOnly = true;

        private readonly Dictionary<Renderer, Material[]> originalSharedMaterials = new();
        private readonly Dictionary<string, Material> sharedSeasonVariants =
            new(StringComparer.Ordinal);

        public string ActiveSeasonCode => activeSeasonCode;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            Transform natureCompositionRoot,
            자연경관SeasonFxBinding[] bindings,
            string initialSeasonCode)
        {
            compositionRoot = natureCompositionRoot;
            fxBindings = bindings ?? Array.Empty<자연경관SeasonFxBinding>();
            activeSeasonCode = initialSeasonCode ?? string.Empty;
            presentationOnly = true;
            ApplySeason(activeSeasonCode);
        }

        public void ApplySeason(string seasonCode)
        {
            var profile = 자연경관SeasonPresentationProfile.CreateDefault();
            var rule = profile.Resolve(seasonCode);
            activeSeasonCode = rule.SeasonCode;
            foreach (var binding in fxBindings)
                if (binding != null && binding.VisualRoot != null)
                    binding.VisualRoot.SetActive(binding.SeasonCode == activeSeasonCode);
            ApplyFoliageTint(rule.FoliageTint);
        }

        public bool ValidateWiring()
            => compositionRoot != null && presentationOnly
                && fxBindings.Length == 자연경관SeasonCodes.All.Count
                && fxBindings.All(value => value != null && value.Validate())
                && fxBindings.Select(value => value.SeasonCode)
                    .Distinct(StringComparer.Ordinal).Count() == fxBindings.Length
                && 자연경관SeasonCodes.All.Contains(
                    activeSeasonCode, StringComparer.Ordinal);

        private void OnEnable()
        {
            if (compositionRoot != null && fxBindings.Length > 0)
                ApplySeason(activeSeasonCode);
        }

        private void ApplyFoliageTint(Color tint)
        {
            if (compositionRoot == null) return;
            foreach (var view in compositionRoot
                         .GetComponentsInChildren<자연경관CompositionSetView>(true))
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                if (!originalSharedMaterials.TryGetValue(renderer, out var originals))
                {
                    originals = renderer.sharedMaterials.ToArray();
                    originalSharedMaterials.Add(renderer, originals);
                }
                var materials = originals.ToArray();
                for (var index = 0; index < materials.Length; index++)
                {
                    var source = originals[index];
                    if (source == null || !IsFoliage(source.name)) continue;
                    materials[index] = ResolveSharedSeasonVariant(source, tint);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private Material ResolveSharedSeasonVariant(Material source, Color tint)
        {
            var key = source.GetEntityId() + ":" + activeSeasonCode;
            if (sharedSeasonVariants.TryGetValue(key, out var existing)) return existing;
            var variant = new Material(source)
            {
                name = source.name + " [Season " + activeSeasonCode + "]",
            };
            if (variant.HasProperty(BaseColor))
                variant.SetColor(BaseColor, Multiply(source.GetColor(BaseColor), tint));
            else if (variant.HasProperty(ColorProperty))
                variant.SetColor(ColorProperty, Multiply(source.GetColor(ColorProperty), tint));
            sharedSeasonVariants.Add(key, variant);
            return variant;
        }

        private void OnDestroy()
        {
            foreach (var pair in originalSharedMaterials)
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            foreach (var material in sharedSeasonVariants.Values)
            {
                if (material == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            originalSharedMaterials.Clear();
            sharedSeasonVariants.Clear();
        }

        private static bool IsFoliage(string materialName)
            => materialName.IndexOf("Leaves", StringComparison.OrdinalIgnoreCase) >= 0
                || materialName.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0
                || materialName.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0
                || materialName.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0
                || materialName.IndexOf("Undergrowth", StringComparison.OrdinalIgnoreCase) >= 0;

        private static Color Multiply(Color value, Color tint)
            => new(value.r * tint.r, value.g * tint.g,
                value.b * tint.b, value.a * tint.a);
    }
}
