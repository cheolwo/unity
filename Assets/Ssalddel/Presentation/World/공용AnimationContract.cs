using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 공용AnimationIntentCodes
    {
        public const string Idle = "idle";
        public const string Walk = "walk";

        public static IReadOnlyList<string> All { get; } = new[] { Idle, Walk };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
               && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 공용AnimationSourceKindCodes
    {
        public const string SyntyProvided = "synty-provided";
        public const string Retargeted = "retargeted";
        public const string ProceduralFallback = "procedural-fallback";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            SyntyProvided,
            Retargeted,
            ProceduralFallback,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
               && All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class 공용AnimationKey
    {
        [SerializeField] private string value = string.Empty;

        public string Value => value;

        public void Configure(string key) => value = key ?? string.Empty;

        public bool Validate() => !string.IsNullOrWhiteSpace(value);
    }

    [Serializable]
    public sealed class 공용AnimationCatalogEntry
    {
        [SerializeField] private string packCode = string.Empty;
        [SerializeField] private string actorRoleCode = string.Empty;
        [SerializeField] private 공용AnimationKey idleKey = new();
        [SerializeField] private 공용AnimationKey walkKey = new();
        [SerializeField] private string sourceKindCode = string.Empty;
        [SerializeField] private string fallbackKey = string.Empty;
        [SerializeField] private GameObject characterPrefab = null!;
        [SerializeField] private AnimationClip? idleClip;
        [SerializeField] private AnimationClip? walkClip;

        public string PackCode => packCode;
        public string ActorRoleCode => actorRoleCode;
        public 공용AnimationKey IdleKey => idleKey;
        public 공용AnimationKey WalkKey => walkKey;
        public string SourceKindCode => sourceKindCode;
        public string FallbackKey => fallbackKey;
        public GameObject CharacterPrefab => characterPrefab;
        public AnimationClip? IdleClip => idleClip;
        public AnimationClip? WalkClip => walkClip;
        public bool UsesFallback => sourceKindCode == 공용AnimationSourceKindCodes.ProceduralFallback;

        public void Configure(
            string pack,
            string actorRole,
            string idle,
            string walk,
            string sourceKind,
            string fallback,
            GameObject prefab,
            AnimationClip? idleSource,
            AnimationClip? walkSource)
        {
            packCode = pack ?? string.Empty;
            actorRoleCode = actorRole ?? string.Empty;
            idleKey = new 공용AnimationKey();
            idleKey.Configure(idle);
            walkKey = new 공용AnimationKey();
            walkKey.Configure(walk);
            sourceKindCode = sourceKind ?? string.Empty;
            fallbackKey = fallback ?? string.Empty;
            characterPrefab = prefab;
            idleClip = idleSource;
            walkClip = walkSource;
        }

        public bool Validate()
        {
            if (!월드CompositionPackCodes.IsKnown(packCode)
                || string.IsNullOrWhiteSpace(actorRoleCode)
                || !idleKey.Validate()
                || !walkKey.Validate()
                || !공용AnimationSourceKindCodes.IsKnown(sourceKindCode)
                || string.IsNullOrWhiteSpace(fallbackKey)
                || characterPrefab == null)
                return false;

            var animator = characterPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return false;

            return UsesFallback
                ? idleClip == null && walkClip == null
                : idleClip != null && walkClip != null;
        }
    }

}
