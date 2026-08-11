using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [CreateAssetMenu(menuName = "Ssalddel/에셋 연구/에셋 원본 Index")]
    public sealed class 에셋원본Index : ScriptableObject
    {
        [SerializeField] private string 생성기준 = string.Empty;
        [SerializeField] private 에셋원본IndexEntry[] entries = Array.Empty<에셋원본IndexEntry>();

        public string 생성기준값 => 생성기준;
        public IReadOnlyList<에셋원본IndexEntry> Entries => entries;

        public void Configure(string basis, 에셋원본IndexEntry[] values)
        {
            생성기준 = basis;
            entries = values ?? Array.Empty<에셋원본IndexEntry>();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(생성기준)
                || entries == null || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.원본Guid값).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("에셋 원본 Index가 올바르지 않습니다.");
        }
    }
}
