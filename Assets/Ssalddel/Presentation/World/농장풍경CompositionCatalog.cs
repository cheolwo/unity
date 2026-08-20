using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 농장풍경SetNames
    {
        public const string 감자밭두렁 = "감자밭 두렁";
        public const string 혼합작물밭 = "혼합 작물밭";
        public const string 헛간작업마당 = "헛간 작업마당";
        public const string 농기계대기장 = "농기계 대기장";
        public const string 농산물직판장 = "농산물 직판장";
        public const string 수확물집하장 = "수확물 집하장";
        public const string 농로교차로 = "농로 교차로";
        public const string 수목완충지 = "수목 완충지";
        public const string 시설하우스단동 = "시설하우스 단동";
        public const string 시설하우스병렬단지 = "시설하우스 병렬단지";
        public const string 과수원블록 = "과수원 블록";
        public const string 논필지농수로표현 = "논 필지·농수로 표현";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            감자밭두렁,
            혼합작물밭,
            헛간작업마당,
            농기계대기장,
            농산물직판장,
            수확물집하장,
            농로교차로,
            수목완충지,
            시설하우스단동,
            시설하우스병렬단지,
            과수원블록,
            논필지농수로표현,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 농장풍경VariantCodes
    {
        public const string A = "A";
        public const string B = "B";
        public const string C = "C";

        public static IReadOnlyList<string> All { get; } = new[] { A, B, C };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class 농장풍경CompositionCatalogEntry
    {
        [SerializeField] private string setName = string.Empty;
        [SerializeField] private string variantCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private Vector2 footprint;

        public string SetName => setName;
        public string VariantCode => variantCode;
        public GameObject Prefab => prefab;
        public Vector2 Footprint => footprint;
        public string CompositionKey => BuildKey(setName, variantCode);

        public void Configure(
            string name,
            string variant,
            GameObject sourcePrefab,
            Vector2 size)
        {
            setName = name;
            variantCode = variant;
            prefab = sourcePrefab;
            footprint = size;
        }

        public bool Validate()
        {
            if (!농장풍경SetNames.IsKnown(setName)
                || !농장풍경VariantCodes.IsKnown(variantCode)
                || prefab == null
                || footprint.x <= 0f
                || footprint.y <= 0f)
            {
                return false;
            }

            var view = prefab.GetComponent<농장풍경CompositionSetView>();
            return view != null
                && view.SetName == setName
                && view.VariantCode == variantCode
                && view.ValidateWiring();
        }

        public static string BuildKey(string name, string variant)
            => name + ":" + variant;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/농장 풍경 Composition Catalog")]
    public sealed class 농장풍경CompositionCatalog : ScriptableObject
    {
        [SerializeField] private 농장풍경CompositionCatalogEntry[] entries =
            Array.Empty<농장풍경CompositionCatalogEntry>();

        public IReadOnlyList<농장풍경CompositionCatalogEntry> Entries => entries;

        public void Configure(농장풍경CompositionCatalogEntry[] values)
            => entries = values ?? Array.Empty<농장풍경CompositionCatalogEntry>();

        public 농장풍경CompositionCatalogEntry Resolve(string setName, string variantCode)
        {
            Validate();
            var key = 농장풍경CompositionCatalogEntry.BuildKey(setName, variantCode);
            return entries.SingleOrDefault(value => value.CompositionKey == key)
                ?? throw new InvalidOperationException("FarmCompositionMissing:" + key);
        }

        public void Validate()
        {
            var expectedCount = 농장풍경SetNames.All.Count * 농장풍경VariantCodes.All.Count;
            if (entries == null
                || entries.Length != expectedCount
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || 농장풍경SetNames.All.Any(name =>
                    entries.Count(value => value.SetName == name) != 농장풍경VariantCodes.All.Count))
            {
                throw new InvalidOperationException("FarmCompositionCatalogInvalid");
            }
        }
    }
}
