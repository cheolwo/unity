using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    public static class SyntyAnimationRoleCodes
    {
        public const string Locomotion = "locomotion";
        public const string SocialEmote = "social-emote";
        public const string MeditationFeedback = "meditation-feedback";
        public const string SwordCombat = "sword-combat";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Locomotion, SocialEmote, MeditationFeedback, SwordCombat,
        };
    }

    /// <summary>
    /// 구매 팩을 Area나 권위 상태로 취급하지 않고, WI가 H 공간에서 발현될 때
    /// 검토할 수 있는 표현 원천으로 연결한다. 실제 Prefab·Clip 채택은 E4 후보 동결과
    /// E5 배치 검증을 별도로 통과해야 한다.
    /// </summary>
    public sealed class Synty구매자산활용Profile
    {
        public Synty구매자산활용Profile(
            string sourcePackCode,
            string sourceKindCode,
            IEnumerable<string> moduleCodes,
            IEnumerable<string> animationRoleCodes,
            IEnumerable<string> requiredHCapabilities,
            IEnumerable<string> playableLoopCandidateCodes)
        {
            SourcePackCode = sourcePackCode ?? string.Empty;
            SourceKindCode = sourceKindCode ?? string.Empty;
            ModuleCodes = Normalize(moduleCodes);
            AnimationRoleCodes = Normalize(animationRoleCodes);
            RequiredHCapabilities = Normalize(requiredHCapabilities);
            PlayableLoopCandidateCodes = Normalize(playableLoopCandidateCodes);
        }

        public string SourcePackCode { get; }
        public string SourceKindCode { get; }
        public IReadOnlyList<string> ModuleCodes { get; }
        public IReadOnlyList<string> AnimationRoleCodes { get; }
        public IReadOnlyList<string> RequiredHCapabilities { get; }
        public IReadOnlyList<string> PlayableLoopCandidateCodes { get; }
        public string LifecycleCode => Synty자산LifecycleCodes.NeedsReview;
        public bool PresentationOnly => true;

        public void Validate()
        {
            if (!Synty전체자산SourcePackCodes.IsKnown(SourcePackCode)
                || (SourceKindCode != "Prefab" && SourceKindCode != "AnimationClip")
                || ModuleCodes.Any(value => !Synty자산ModuleCodes.IsKnown(value))
                || AnimationRoleCodes.Any(value => !SyntyAnimationRoleCodes.All.Contains(value))
                || (ModuleCodes.Count == 0 && AnimationRoleCodes.Count == 0)
                || RequiredHCapabilities.Count == 0
                || PlayableLoopCandidateCodes.Count == 0
                || !PresentationOnly)
            {
                throw new InvalidOperationException("SyntyPurchasedAssetUsageProfileInvalid:" + SourcePackCode);
            }
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> values) =>
            (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
    }

    public static class Synty구매자산활용Profiles
    {
        public static IReadOnlyList<Synty구매자산활용Profile> All { get; } = new[]
        {
            Animation(
                Synty전체자산SourcePackCodes.AnimationBaseLocomotion,
                new[] { SyntyAnimationRoleCodes.Locomotion },
                new[] { "PlayerMovementArea", "NpcMovementArea", "TacticalCommandArea" },
                new[] { "nature-navigation", "farm-work-movement", "town-hub-actor-movement" }),
            Animation(
                Synty전체자산SourcePackCodes.AnimationEmotesAndTaunts,
                new[] { SyntyAnimationRoleCodes.SocialEmote, SyntyAnimationRoleCodes.MeditationFeedback },
                new[] { "MeditationWorkArea", "CommunityInteractionArea", "NpcRelationshipArea" },
                new[] { "player-mind-meditation", "community-resonance", "npc-cooperation" }),
            Animation(
                Synty전체자산SourcePackCodes.AnimationSwordCombat,
                new[] { SyntyAnimationRoleCodes.SwordCombat },
                new[] { "ThreatEncounterArea", "DirectCombatArea", "OutpostDefenseArea" },
                new[] { "nature-twilight-combat", "corridor-defense", "squad-combat" }),
            Prefab(
                Synty전체자산SourcePackCodes.AlpineMountain,
                new[]
                {
                    Synty자산ModuleCodes.WorldSurface, Synty자산ModuleCodes.NatureVegetation,
                    Synty자산ModuleCodes.ExteriorStructure, Synty자산ModuleCodes.ExteriorFunctionalProp,
                    Synty자산ModuleCodes.WorldTransition, Synty자산ModuleCodes.WorldFeedbackFx,
                },
                new[] { "NatureTravelArea", "ShelterSiteChoiceArea", "ClimateExposureArea" },
                new[] { "nature-shelter-foundation", "nature-night-day2", "nature-exploration" }),
            Prefab(
                Synty전체자산SourcePackCodes.DungeonRealms,
                new[]
                {
                    Synty자산ModuleCodes.ExteriorStructure, Synty자산ModuleCodes.InteriorStructure,
                    Synty자산ModuleCodes.InteriorFixture, Synty자산ModuleCodes.InteriorLooseItem,
                    Synty자산ModuleCodes.ActorVehicleTool, Synty자산ModuleCodes.WorldTransition,
                    Synty자산ModuleCodes.WorldFeedbackFx,
                },
                new[] { "ThreatEncounterArea", "RuinsExplorationArea", "OutpostDefenseArea", "BarracksWorkArea" },
                new[] { "nature-twilight-combat", "corridor-defense", "ruins-exploration" }),
            Prefab(
                Synty전체자산SourcePackCodes.DwarvenDungeonMap,
                new[]
                {
                    Synty자산ModuleCodes.WorldSurface, Synty자산ModuleCodes.ExteriorStructure,
                    Synty자산ModuleCodes.InteriorStructure, Synty자산ModuleCodes.InteriorFixture,
                    Synty자산ModuleCodes.WorldNetwork, Synty자산ModuleCodes.WorldTransition,
                    Synty자산ModuleCodes.ActorVehicleTool, Synty자산ModuleCodes.WorldFeedbackFx,
                },
                new[] { "MineExplorationArea", "UndergroundCorridorArea", "ResourceExtractionArea" },
                new[] { "mine-exploration", "resource-extraction", "corridor-defense" }),
        };

        private static Synty구매자산활용Profile Animation(
            string sourcePackCode,
            string[] animationRoles,
            string[] hCapabilities,
            string[] loopCandidates) =>
            Create(sourcePackCode, "AnimationClip",
                new[] { Synty자산ModuleCodes.ActorVehicleTool }, animationRoles,
                hCapabilities, loopCandidates);

        private static Synty구매자산활용Profile Prefab(
            string sourcePackCode,
            string[] modules,
            string[] hCapabilities,
            string[] loopCandidates) =>
            Create(sourcePackCode, "Prefab", modules, Array.Empty<string>(),
                hCapabilities, loopCandidates);

        private static Synty구매자산활용Profile Create(
            string sourcePackCode,
            string sourceKindCode,
            string[] modules,
            string[] animationRoles,
            string[] hCapabilities,
            string[] loopCandidates)
        {
            var profile = new Synty구매자산활용Profile(
                sourcePackCode, sourceKindCode, modules, animationRoles,
                hCapabilities, loopCandidates);
            profile.Validate();
            return profile;
        }
    }
}
