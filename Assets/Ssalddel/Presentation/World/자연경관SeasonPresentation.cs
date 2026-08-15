using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 자연경관SeasonPresentationRule
    {
        public string SeasonCode = string.Empty;
        public string BroadleafMaterialKey = string.Empty;
        public string ConiferMaterialKey = string.Empty;
        public string MountainMaterialKey = string.Empty;
        public string AmbientFxVisualKey = string.Empty;
        public Color FoliageTint = Color.white;

        public bool Validate()
            => 자연경관SeasonCodes.All.Contains(SeasonCode, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(BroadleafMaterialKey)
                && !string.IsNullOrWhiteSpace(ConiferMaterialKey)
                && !string.IsNullOrWhiteSpace(MountainMaterialKey)
                && !string.IsNullOrWhiteSpace(AmbientFxVisualKey);
    }

    public sealed class 자연경관SeasonPresentationProfile
    {
        private readonly 자연경관SeasonPresentationRule[] _rules;

        public 자연경관SeasonPresentationProfile(
            IEnumerable<자연경관SeasonPresentationRule> rules)
        {
            _rules = rules?.ToArray() ?? Array.Empty<자연경관SeasonPresentationRule>();
            Validate();
        }

        public IReadOnlyList<자연경관SeasonPresentationRule> Rules => _rules;

        public static 자연경관SeasonPresentationProfile CreateDefault() => new(new[]
        {
            Rule(자연경관SeasonCodes.Spring,
                "nature.material.leaves.spring",
                "nature.material.pine.spring",
                "nature.material.mountain.moss",
                "nature.fx.butterflies", new Color(.90f, 1f, .88f, 1f)),
            Rule(자연경관SeasonCodes.Summer,
                "nature.material.leaves.summer",
                "nature.material.pine.summer",
                "nature.material.mountain.moss",
                "nature.fx.sunbeams", Color.white),
            Rule(자연경관SeasonCodes.Autumn,
                "nature.material.leaves.autumn",
                "nature.material.pine.autumn",
                "nature.material.mountain.moss",
                "nature.fx.falling-leaves", new Color(1f, .72f, .42f, 1f)),
            Rule(자연경관SeasonCodes.Winter,
                "nature.material.leaves.winter",
                "nature.material.pine.snow",
                "nature.material.mountain.snow",
                "nature.fx.snow", new Color(.82f, .90f, 1f, 1f)),
        });

        public 자연경관SeasonPresentationRule Resolve(string seasonCode)
            => _rules.SingleOrDefault(value => value.SeasonCode == seasonCode)
                ?? throw new InvalidOperationException(
                    "NatureSeasonPresentationMissing:" + seasonCode);

        public void Validate()
        {
            if (_rules.Length != 자연경관SeasonCodes.All.Count
                || _rules.Any(value => value == null || !value.Validate())
                || _rules.Select(value => value.SeasonCode)
                    .Distinct(StringComparer.Ordinal).Count() != _rules.Length)
                throw new InvalidOperationException("NatureSeasonPresentationProfileInvalid");
        }

        private static 자연경관SeasonPresentationRule Rule(
            string seasonCode,
            string broadleaf,
            string conifer,
            string mountain,
            string fx,
            Color tint) => new()
        {
            SeasonCode = seasonCode,
            BroadleafMaterialKey = broadleaf,
            ConiferMaterialKey = conifer,
            MountainMaterialKey = mountain,
            AmbientFxVisualKey = fx,
            FoliageTint = tint,
        };
    }

    [Serializable]
    public sealed class 자연경관SeasonFxBinding
    {
        [SerializeField] private string seasonCode = string.Empty;
        [SerializeField] private GameObject visualRoot = null!;

        public string SeasonCode => seasonCode;
        public GameObject VisualRoot => visualRoot;

        public void Configure(string code, GameObject root)
        {
            seasonCode = code ?? string.Empty;
            visualRoot = root;
        }

        public bool Validate()
            => 자연경관SeasonCodes.All.Contains(seasonCode, StringComparer.Ordinal)
                && visualRoot != null;
    }

}
