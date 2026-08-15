using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 자연경관EventPresentationKeys
    {
        public const string ScenicExploration = "survival.scenic-exploration";
        public const string SeasonalDefenseWarning =
            "survival.seasonal-defense.warning";
        public const string ZombieWarning = "survival.zombie-warning";
        public const string RaiderApproach = "survival.raider-approach";
        public const string DamageAssessment = "survival.damage-assessment";
        public const string TacticalZombiePressure =
            "survival.tactical.squad.zombie-pressure";

        public static IReadOnlyList<string> OverlayKeys { get; } = new[]
        {
            SeasonalDefenseWarning, ZombieWarning, RaiderApproach,
            DamageAssessment, TacticalZombiePressure,
        };
    }

    [Serializable]
    public sealed class 자연경관EventOverlayCatalogEntry
    {
        [SerializeField] private string presentationKey = string.Empty;
        [SerializeField] private string overlayName = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string moodCode = 자연경관MoodCodes.SurvivalEvent;
        [SerializeField] private string natureRoleCode = 자연경관RoleCodes.EventOnly;
        [SerializeField] private bool presentationOnly = true;

        public string PresentationKey => presentationKey;
        public string OverlayName => overlayName;
        public GameObject Prefab => prefab;
        public string MoodCode => moodCode;
        public string NatureRoleCode => natureRoleCode;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string key, string name, GameObject sourcePrefab)
        {
            presentationKey = key ?? string.Empty;
            overlayName = name ?? string.Empty;
            prefab = sourcePrefab;
            moodCode = 자연경관MoodCodes.SurvivalEvent;
            natureRoleCode = 자연경관RoleCodes.EventOnly;
            presentationOnly = true;
        }

        public bool Validate()
            => 자연경관EventPresentationKeys.OverlayKeys.Contains(
                    presentationKey, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(overlayName)
                && prefab != null
                && moodCode == 자연경관MoodCodes.SurvivalEvent
                && natureRoleCode == 자연경관RoleCodes.EventOnly
                && presentationOnly
                && prefab.TryGetComponent<자연경관EventOverlayView>(out var view)
                && view != null && view.ValidateWiring()
                && view.PresentationKey == presentationKey;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/자연 경관 Event Overlay Catalog")]
    public sealed class 자연경관EventOverlayCatalog : ScriptableObject
    {
        [SerializeField] private 자연경관EventOverlayCatalogEntry[] entries =
            Array.Empty<자연경관EventOverlayCatalogEntry>();

        public IReadOnlyList<자연경관EventOverlayCatalogEntry> Entries => entries;

        public void Configure(자연경관EventOverlayCatalogEntry[] values)
            => entries = values ?? Array.Empty<자연경관EventOverlayCatalogEntry>();

        public bool TryResolve(
            string presentationKey,
            out 자연경관EventOverlayCatalogEntry entry)
        {
            entry = entries.SingleOrDefault(value =>
                value.PresentationKey == presentationKey)!;
            return entry != null;
        }

        public void Validate()
        {
            if (entries.Length != 자연경관EventPresentationKeys.OverlayKeys.Count
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.PresentationKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Any(value => value.PresentationKey
                    == 자연경관EventPresentationKeys.ScenicExploration))
                throw new InvalidOperationException("NatureEventOverlayCatalogInvalid");
        }
    }

}
