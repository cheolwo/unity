using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 월드시간표면Kind
    {
        Generic = 0,
        GroundSoil = 1,
        CropLeaf = 2,
        Wood = 3,
        AsphaltConcrete = 4,
        Roof = 5,
        MetalVehicle = 6,
        GlassWindow = 7,
        SignageFixture = 8,
    }

    public readonly struct 농장Composition시간표면InventoryEntry
    {
        public 농장Composition시간표면InventoryEntry(
            string compositionKey,
            string setName,
            string variantCode,
            int rendererCount,
            int eligibleMaterialSlotCount,
            IReadOnlyList<월드시간표면Kind> surfaceKinds)
        {
            CompositionKey = compositionKey;
            SetName = setName;
            VariantCode = variantCode;
            RendererCount = rendererCount;
            EligibleMaterialSlotCount = eligibleMaterialSlotCount;
            SurfaceKinds = surfaceKinds;
        }

        public string CompositionKey { get; }
        public string SetName { get; }
        public string VariantCode { get; }
        public int RendererCount { get; }
        public int EligibleMaterialSlotCount { get; }
        public IReadOnlyList<월드시간표면Kind> SurfaceKinds { get; }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(CompositionKey)
                && 농장풍경SetNames.IsKnown(SetName)
                && 농장풍경VariantCodes.IsKnown(VariantCode)
                && RendererCount > 0
                && EligibleMaterialSlotCount > 0
                && SurfaceKinds != null
                && SurfaceKinds.Count > 0;
    }

    public static class 농장Composition시간표면Inventory
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static IReadOnlyList<농장Composition시간표면InventoryEntry> Measure(
            농장풍경CompositionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            return catalog.Entries.Select(Measure).ToArray();
        }

        private static 농장Composition시간표면InventoryEntry Measure(
            농장풍경CompositionCatalogEntry entry)
        {
            var view = entry.Prefab.GetComponent<농장풍경CompositionSetView>()
                ?? throw new InvalidOperationException(
                    "FarmTimeSurfaceViewMissing:" + entry.CompositionKey);
            var renderers = view.EnvironmentRoot.GetComponentsInChildren<Renderer>(true);
            var eligibleSlots = 0;
            var kinds = new HashSet<월드시간표면Kind>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null
                        && (material.HasProperty(BaseColorId) || material.HasProperty(ColorId)))
                        eligibleSlots++;
                }

                kinds.Add(Classify(renderer));
            }

            return new 농장Composition시간표면InventoryEntry(
                entry.CompositionKey,
                entry.SetName,
                entry.VariantCode,
                renderers.Length,
                eligibleSlots,
                kinds.OrderBy(value => value).ToArray());
        }

        public static 월드시간표면Kind Classify(Renderer renderer)
        {
            if (renderer == null) return 월드시간표면Kind.Generic;
            var text = BuildSearchText(renderer).ToLowerInvariant();
            if (ContainsAny(text, "crop", "plant", "tree", "grass", "flower", "bush", "vegetable"))
                return 월드시간표면Kind.CropLeaf;
            if (ContainsAny(text, "dirt", "soil", "ground", "road", "path"))
                return 월드시간표면Kind.GroundSoil;
            if (ContainsAny(text, "wood", "fence", "crate", "pallet", "barn"))
                return 월드시간표면Kind.Wood;
            if (ContainsAny(text, "tractor", "vehicle", "machine", "tool", "metal", "silo"))
                return 월드시간표면Kind.MetalVehicle;
            if (ContainsAny(text, "roof", "shingle"))
                return 월드시간표면Kind.Roof;
            if (ContainsAny(text, "glass", "window", "greenhouse"))
                return 월드시간표면Kind.GlassWindow;
            if (ContainsAny(text, "sign", "lamp", "light", "lantern"))
                return 월드시간표면Kind.SignageFixture;
            if (ContainsAny(text, "asphalt", "concrete", "sidewalk"))
                return 월드시간표면Kind.AsphaltConcrete;
            return 월드시간표면Kind.Generic;
        }

        private static string BuildSearchText(Renderer renderer)
        {
            var values = new List<string> { renderer.name };
            var current = renderer.transform.parent;
            for (var depth = 0; current != null && depth < 4; depth++, current = current.parent)
                values.Add(current.name);
            values.AddRange(renderer.sharedMaterials.Where(value => value != null)
                .Select(value => value.name));
            return string.Join(" ", values);
        }

        private static bool ContainsAny(string text, params string[] values)
            => values.Any(text.Contains);
    }
}
