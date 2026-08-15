using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Survival;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 전술CharacterVisualCatalogEntry
    {
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private string sideCode = string.Empty;
        [SerializeField] private string animationPackCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private int weight = 1;
        [SerializeField] private bool presentationOnly = true;

        public string VisualKey => visualKey;
        public string SideCode => sideCode;
        public string AnimationPackCode => animationPackCode;
        public GameObject Prefab => prefab;
        public int Weight => weight;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string key, string side, string pack,
            GameObject sourcePrefab, int candidateWeight)
        {
            visualKey = key ?? string.Empty;
            sideCode = side ?? string.Empty;
            animationPackCode = pack ?? string.Empty;
            prefab = sourcePrefab;
            weight = candidateWeight;
            presentationOnly = true;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(visualKey)
                || (sideCode != FarmCombatPresentationCodes.Allied
                    && sideCode != FarmCombatPresentationCodes.Hostile)
                || !월드CompositionPackCodes.IsKnown(animationPackCode)
                || prefab == null || weight <= 0 || !presentationOnly)
                return false;
            var animator = prefab.GetComponentInChildren<Animator>(true);
            return animator != null && animator.avatar != null
                && animator.avatar.isHuman;
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/전술 Character Visual Catalog")]
    public sealed class 전술CharacterVisualCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 전술CharacterVisualCatalogEntry[] entries
            = Array.Empty<전술CharacterVisualCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<전술CharacterVisualCatalogEntry> Entries => entries;

        public void Configure(string revision,
            전술CharacterVisualCatalogEntry[] values)
        {
            catalogRevision = revision ?? string.Empty;
            entries = values ?? Array.Empty<전술CharacterVisualCatalogEntry>();
        }

        public 전술CharacterVisualCatalogEntry Resolve(
            string sideCode, string stableMemberId)
        {
            Validate();
            if (string.IsNullOrWhiteSpace(stableMemberId))
                throw new ArgumentException("TacticalVisualStableMemberMissing");
            var candidates = entries.Where(value => value.SideCode == sideCode)
                .OrderBy(value => value.VisualKey, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException(
                    "TacticalVisualSideMissing:" + sideCode);
            var hash = 결정적표현Seed.Hash(stableMemberId);
            var totalWeight = candidates.Sum(value => value.Weight);
            var selected = (int)(hash % (uint)totalWeight);
            foreach (var candidate in candidates)
            {
                if (selected < candidate.Weight) return candidate;
                selected -= candidate.Weight;
            }
            return candidates[^1];
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || entries.Length < 2
                || entries.Any(value => value == null || !value.Validate())
                || !entries.Any(value => value.SideCode ==
                    FarmCombatPresentationCodes.Allied)
                || !entries.Any(value => value.SideCode ==
                    FarmCombatPresentationCodes.Hostile)
                || entries.Select(value => value.VisualKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException(
                    "TacticalCharacterVisualCatalogInvalid");
        }
    }
}
