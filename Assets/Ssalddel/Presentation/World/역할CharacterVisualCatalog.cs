using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 역할CharacterVisualCatalogEntry
    {
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private string sourcePack = string.Empty;
        [SerializeField] private string animationPackCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string[] allowedActorRoleCodes = Array.Empty<string>();
        [SerializeField] private string[] appearanceFamilyCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedAreaRoleCodes = Array.Empty<string>();
        [SerializeField] private int weight = 1;
        [SerializeField] private bool playerEligible = true;
        [SerializeField] private int estimatedTriangles;
        [SerializeField] private int materialSlotCount;
        [SerializeField] private int animatorCount;
        [SerializeField] private bool presentationOnly = true;

        public string VisualKey => visualKey;
        public string SourcePack => sourcePack;
        public string AnimationPackCode => animationPackCode;
        public GameObject Prefab => prefab;
        public IReadOnlyList<string> AllowedActorRoleCodes => allowedActorRoleCodes;
        public IReadOnlyList<string> AppearanceFamilyCodes => appearanceFamilyCodes;
        public IReadOnlyList<string> AllowedAreaRoleCodes => allowedAreaRoleCodes;
        public int Weight => weight;
        public bool PlayerEligible => playerEligible;
        public int EstimatedTriangles => estimatedTriangles;
        public int MaterialSlotCount => materialSlotCount;
        public int AnimatorCount => animatorCount;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string key,
            string pack,
            string animationPack,
            GameObject sourcePrefab,
            string[] roles,
            string[] families,
            string[] areas,
            int candidateWeight,
            bool canBePlayer,
            int triangles,
            int materialSlots,
            int animators)
        {
            visualKey = key ?? string.Empty;
            sourcePack = pack ?? string.Empty;
            animationPackCode = animationPack ?? string.Empty;
            prefab = sourcePrefab;
            allowedActorRoleCodes = roles ?? Array.Empty<string>();
            appearanceFamilyCodes = families ?? Array.Empty<string>();
            allowedAreaRoleCodes = areas ?? Array.Empty<string>();
            weight = candidateWeight;
            playerEligible = canBePlayer;
            estimatedTriangles = triangles;
            materialSlotCount = materialSlots;
            animatorCount = animators;
            presentationOnly = true;
        }

        public WorldCharacterAssignmentCandidate ToCandidate()
            => new()
            {
                VisualKey = visualKey,
                AllowedActorRoleCodes = allowedActorRoleCodes.ToArray(),
                AppearanceFamilyCodes = appearanceFamilyCodes.ToArray(),
                Weight = weight,
                PlayerEligible = playerEligible,
                PresentationOnly = presentationOnly,
            };

        public bool Validate()
        {
            if (!WorldCharacterVisualKeys.IsKnown(visualKey)
                || string.IsNullOrWhiteSpace(sourcePack)
                || !월드CompositionPackCodes.IsKnown(animationPackCode)
                || prefab == null
                || allowedActorRoleCodes.Length == 0
                || allowedActorRoleCodes.Any(value => !WorldActorRoleCodes.IsKnown(value))
                || appearanceFamilyCodes.Length == 0
                || appearanceFamilyCodes.Any(value =>
                    !WorldActorAppearanceFamilyCodes.IsKnown(value))
                || allowedAreaRoleCodes.Length == 0
                || allowedAreaRoleCodes.Any(value => value != 법정동WorldRoleCodes.Farm
                    && value != 법정동WorldRoleCodes.Hub
                    && value != 법정동WorldRoleCodes.Town)
                || weight <= 0 || estimatedTriangles < 0 || materialSlotCount < 0
                || animatorCount <= 0 || !presentationOnly)
                return false;
            var animator = prefab.GetComponentInChildren<Animator>(true);
            return animator != null && animator.avatar != null && animator.avatar.isHuman;
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/역할 Character Visual Catalog")]
    public sealed class 역할CharacterVisualCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 역할CharacterVisualCatalogEntry[] entries =
            Array.Empty<역할CharacterVisualCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<역할CharacterVisualCatalogEntry> Entries => entries;

        public void Configure(string revision, 역할CharacterVisualCatalogEntry[] values)
        {
            catalogRevision = revision ?? string.Empty;
            entries = values ?? Array.Empty<역할CharacterVisualCatalogEntry>();
        }

        public 역할CharacterVisualCatalogEntry Resolve(string visualKey)
            => entries.SingleOrDefault(value => value.VisualKey == visualKey)
                ?? throw new InvalidOperationException(
                    "RoleCharacterVisualMissing:" + visualKey);

        public IReadOnlyList<WorldCharacterAssignmentCandidate> AssignmentCandidates()
            => entries.Select(value => value.ToCandidate()).ToArray();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.VisualKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("RoleCharacterVisualCatalogInvalid");

            foreach (var role in WorldActorRoleCodes.Playable)
            {
                var playerCandidates = entries.Count(value => value.PlayerEligible
                    && value.AllowedActorRoleCodes.Contains(role));
                if (playerCandidates < 2)
                    throw new InvalidOperationException(
                        "RoleCharacterPlayerCandidatesInsufficient:" + role);
            }
            if (!entries.Any(value =>
                    value.AllowedActorRoleCodes.Contains(WorldActorRoleCodes.Unresolved)))
                throw new InvalidOperationException("RoleCharacterNeutralFallbackMissing");
        }
    }

}
