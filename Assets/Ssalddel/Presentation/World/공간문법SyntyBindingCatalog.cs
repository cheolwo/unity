using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 공간문법SyntyBindingCodes
    {
        public const string BindingRevision = "pyeongchang-synty-landscape-binding.v1";
        public const string Primary = "Primary";
        public const string Fallback = "Fallback";
    }

    [Serializable]
    public sealed class 공간문법SyntyInventoryReceipt
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private string catalogHashSha256 = string.Empty;

        public string CatalogRevision => catalogRevision;
        public string CatalogHashSha256 => catalogHashSha256;

        public void Configure(string revision, string hash)
        {
            catalogRevision = revision ?? string.Empty;
            catalogHashSha256 = hash ?? string.Empty;
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(catalogRevision)
            && catalogHashSha256?.Length == 64;
    }

    [Serializable]
    public sealed class 공간문법SyntyBindingCandidate
    {
        [SerializeField] private string candidateRoleCode = string.Empty;
        [SerializeField] private int priority;
        [SerializeField] private string sourceCompositionKey = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string detailGeneratorRevision = string.Empty;

        public string CandidateRoleCode => candidateRoleCode;
        public int Priority => priority;
        public string SourceCompositionKey => sourceCompositionKey;
        public GameObject Prefab => prefab;
        public string DetailGeneratorRevision => detailGeneratorRevision;
        public bool IsPrimary => candidateRoleCode == 공간문법SyntyBindingCodes.Primary;

        public void Configure(
            string roleCode,
            int candidatePriority,
            string sourceKey,
            GameObject sourcePrefab,
            string generatorRevision)
        {
            candidateRoleCode = roleCode ?? string.Empty;
            priority = candidatePriority;
            sourceCompositionKey = sourceKey ?? string.Empty;
            prefab = sourcePrefab;
            detailGeneratorRevision = generatorRevision ?? string.Empty;
        }

        public bool Validate() => (candidateRoleCode == 공간문법SyntyBindingCodes.Primary
                || candidateRoleCode == 공간문법SyntyBindingCodes.Fallback)
            && priority >= 0
            && !string.IsNullOrWhiteSpace(sourceCompositionKey)
            && !sourceCompositionKey.Contains("/") && !sourceCompositionKey.Contains("\\")
            && prefab != null
            && !string.IsNullOrWhiteSpace(detailGeneratorRevision);
    }

    [Serializable]
    public sealed class 공간문법SyntyBindingEntry
    {
        [SerializeField] private string compositionKey = string.Empty;
        [SerializeField] private 공간문법SyntyBindingCandidate[] candidates =
            Array.Empty<공간문법SyntyBindingCandidate>();

        public string CompositionKey => compositionKey;
        public IReadOnlyList<공간문법SyntyBindingCandidate> Candidates => candidates;

        public void Configure(string key, 공간문법SyntyBindingCandidate[] values)
        {
            compositionKey = key ?? string.Empty;
            candidates = values ?? Array.Empty<공간문법SyntyBindingCandidate>();
        }

        public bool Validate() => !string.IsNullOrWhiteSpace(compositionKey)
            && !compositionKey.Contains("/") && !compositionKey.Contains("\\")
            && candidates.Length > 0
            && candidates.All(value => value != null && value.Validate())
            && candidates.Count(value => value.IsPrimary) == 1
            && candidates.Select(value => value.SourceCompositionKey)
                .Distinct(StringComparer.Ordinal).Count() == candidates.Length
            && candidates.Where(value => !value.IsPrimary).Select(value => value.Priority)
                .Distinct().Count() == candidates.Count(value => !value.IsPrimary);
    }

    public sealed class 공간문법SyntyBindingResolution
    {
        public 공간문법SyntyBindingCandidate Candidate { get; set; } = null!;
        public bool FallbackUsed { get; set; }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/공간 문법 Synty Binding Catalog")]
    public sealed class 공간문법SyntyBindingCatalog : ScriptableObject
    {
        [SerializeField] private string bindingRevision = string.Empty;
        [SerializeField] private string targetGrammarRevision = string.Empty;
        [SerializeField] private string targetGrammarHashSha256 = string.Empty;
        [SerializeField] private 공간문법SyntyInventoryReceipt[] sourceCatalogs =
            Array.Empty<공간문법SyntyInventoryReceipt>();
        [SerializeField] private 공간문법SyntyBindingEntry[] entries =
            Array.Empty<공간문법SyntyBindingEntry>();

        public string BindingRevision => bindingRevision;
        public string TargetGrammarRevision => targetGrammarRevision;
        public string TargetGrammarHashSha256 => targetGrammarHashSha256;
        public IReadOnlyList<공간문법SyntyInventoryReceipt> SourceCatalogs => sourceCatalogs;
        public IReadOnlyList<공간문법SyntyBindingEntry> Entries => entries;

        public void Configure(
            string revision,
            string grammarRevision,
            string grammarHash,
            공간문법SyntyInventoryReceipt[] receipts,
            공간문법SyntyBindingEntry[] values)
        {
            bindingRevision = revision ?? string.Empty;
            targetGrammarRevision = grammarRevision ?? string.Empty;
            targetGrammarHashSha256 = grammarHash ?? string.Empty;
            sourceCatalogs = receipts ?? Array.Empty<공간문법SyntyInventoryReceipt>();
            entries = values ?? Array.Empty<공간문법SyntyBindingEntry>();
        }

        public 공간문법SyntyBindingResolution Resolve(
            string compositionKey,
            Func<공간문법SyntyBindingCandidate, bool>? isAvailable = null)
        {
            Validate();
            var entry = entries.SingleOrDefault(value => value.CompositionKey == compositionKey)
                ?? throw new InvalidOperationException("LandscapeSyntyBindingMissing:" + compositionKey);
            var available = isAvailable ?? (value => value.Prefab != null);
            var primary = entry.Candidates.Single(value => value.IsPrimary);
            if (available(primary))
                return new 공간문법SyntyBindingResolution
                {
                    Candidate = primary,
                    FallbackUsed = false,
                };
            var fallback = entry.Candidates
                .Where(value => !value.IsPrimary && available(value))
                .OrderBy(value => value.Priority)
                .ThenBy(value => value.SourceCompositionKey, StringComparer.Ordinal)
                .FirstOrDefault();
            if (fallback == null)
                throw new InvalidOperationException(
                    "LandscapeSyntyBindingCandidateUnavailable:" + compositionKey);
            return new 공간문법SyntyBindingResolution
            {
                Candidate = fallback,
                FallbackUsed = true,
            };
        }

        public string BuildBindingHashSha256()
        {
            Validate();
            var builder = new StringBuilder()
                .Append(1).Append('|').Append(bindingRevision).Append('|')
                .Append(targetGrammarRevision).Append('|')
                .Append(targetGrammarHashSha256).AppendLine();
            foreach (var receipt in sourceCatalogs.OrderBy(
                         value => value.CatalogRevision, StringComparer.Ordinal))
                builder.Append(receipt.CatalogRevision).Append('|')
                    .Append(receipt.CatalogHashSha256).AppendLine();
            foreach (var entry in entries.OrderBy(
                         value => value.CompositionKey, StringComparer.Ordinal))
            foreach (var candidate in entry.Candidates
                         .OrderBy(value => value.IsPrimary ? 0 : 1)
                         .ThenBy(value => value.Priority)
                         .ThenBy(value => value.SourceCompositionKey, StringComparer.Ordinal))
                builder.Append(entry.CompositionKey).Append('|')
                    .Append(candidate.CandidateRoleCode).Append('|')
                    .Append(candidate.Priority.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(candidate.SourceCompositionKey).Append('|')
                    .Append(candidate.DetailGeneratorRevision).AppendLine();
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                .Select(value => value.ToString("x2")));
        }

        public void Validate()
        {
            if (bindingRevision != 공간문법SyntyBindingCodes.BindingRevision
                || targetGrammarRevision != 공간문법CompositionCatalog.NeutralGrammarRevision
                || targetGrammarHashSha256?.Length != 64
                || sourceCatalogs.Length == 0
                || sourceCatalogs.Any(value => value == null || !value.Validate())
                || entries.Length != 공간문법CompositionCatalog.ExpectedEntryCount
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("LandscapeSyntyBindingCatalogInvalid");
        }
    }
}
