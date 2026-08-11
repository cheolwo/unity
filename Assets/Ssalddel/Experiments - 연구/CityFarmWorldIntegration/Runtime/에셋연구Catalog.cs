using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [CreateAssetMenu(menuName = "Ssalddel/에셋 연구/공공 관측 Source Catalog")]
    public sealed class 공공관측SourceCatalog : ScriptableObject
    {
        [SerializeField] private 공공관측SourceEntry[] entries = Array.Empty<공공관측SourceEntry>();
        public IReadOnlyList<공공관측SourceEntry> Entries => entries;

        public void Configure(공공관측SourceEntry[] values)
            => entries = values ?? Array.Empty<공공관측SourceEntry>();

        public 공공관측SourceEntry? Find(string sourceKey)
            => entries.FirstOrDefault(value => value.출처Key값 == sourceKey);

        public void Validate()
        {
            if (entries == null
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.출처Key값).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("공공 관측 Source Catalog가 올바르지 않습니다.");
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/에셋 연구/에셋 연구 Catalog")]
    public sealed class 에셋연구Catalog : ScriptableObject
    {
        [SerializeField] private 에셋연구Entry[] entries = Array.Empty<에셋연구Entry>();
        public IReadOnlyList<에셋연구Entry> Entries => entries;

        public void Configure(에셋연구Entry[] values)
            => entries = values ?? Array.Empty<에셋연구Entry>();

        public 에셋연구Entry? FindBySourceGuid(string sourceGuid)
            => entries.FirstOrDefault(value => value.원본Guid값 == sourceGuid);

        public void Validate()
        {
            if (entries == null
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.연구Id값).Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Select(value => value.원본Guid값).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("에셋 연구 Catalog가 올바르지 않습니다.");
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/에셋 연구/에셋 공공 관측 Catalog")]
    public sealed class 에셋공공관측Catalog : ScriptableObject
    {
        [SerializeField] private 에셋공공관측Entry[] entries = Array.Empty<에셋공공관측Entry>();
        public IReadOnlyList<에셋공공관측Entry> Entries => entries;

        public void Configure(에셋공공관측Entry[] values)
            => entries = values ?? Array.Empty<에셋공공관측Entry>();

        public 에셋공공관측Entry? FindPrimaryBySourceGuid(string sourceGuid, string collection = "")
            => (string.IsNullOrWhiteSpace(collection)
                    ? null
                    : entries.FirstOrDefault(value => value.원본Guid값 == sourceGuid
                                                      && value.전시모음값 == collection))
               ?? entries.FirstOrDefault(value => value.원본Guid값 == sourceGuid);

        public IReadOnlyList<에셋공공관측Entry> FindByCollection(string collection)
            => entries.Where(value => value.전시모음값 == collection).ToArray();

        public bool IsInCollection(string sourceGuid, string collection)
            => entries.Any(value => value.원본Guid값 == sourceGuid
                                    && value.전시모음값 == collection);

        public void Validate()
        {
            if (entries == null
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.관측연결Id값)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("에셋 공공 관측 Catalog가 올바르지 않습니다.");
        }
    }
}
