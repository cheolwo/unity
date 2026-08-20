using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 위치 독립 H2 조합물의 검토 계보만 보존한다.
    /// AreaSet, WI E단계, 공공데이터 또는 Simulation 상태의 권위가 아니다.
    /// </summary>
    public sealed class H2공간조합검토Root : MonoBehaviour
    {
        [SerializeField] private string h2StableId = string.Empty;
        [SerializeField] private string recipeId = string.Empty;
        [SerializeField] private string recipeRevision = string.Empty;
        [SerializeField] private string sourceRecipeSha256 = string.Empty;
        [SerializeField] private string[] childH1StableIds = Array.Empty<string>();
        [SerializeField] private bool presentationOnly = true;

        public string H2StableId => h2StableId;
        public string RecipeId => recipeId;
        public string RecipeRevision => recipeRevision;
        public string SourceRecipeSha256 => sourceRecipeSha256;
        public IReadOnlyList<string> ChildH1StableIds => childH1StableIds;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string targetStableId,
            string sourceRecipeId,
            string sourceRecipeRevision,
            string recipeSha256,
            string[] childStableIds)
        {
            h2StableId = targetStableId ?? string.Empty;
            recipeId = sourceRecipeId ?? string.Empty;
            recipeRevision = sourceRecipeRevision ?? string.Empty;
            sourceRecipeSha256 = recipeSha256 ?? string.Empty;
            childH1StableIds = childStableIds ?? Array.Empty<string>();
            presentationOnly = true;
        }

        public bool Validate()
        {
            return h2StableId.StartsWith("h2-candidate:", StringComparison.Ordinal)
                   && recipeId.StartsWith("h2-composition:", StringComparison.Ordinal)
                   && !string.IsNullOrWhiteSpace(recipeRevision)
                   && sourceRecipeSha256.Length == 64
                   && sourceRecipeSha256.All(Uri.IsHexDigit)
                   && childH1StableIds.Length >= 2
                   && childH1StableIds.All(value =>
                       value.StartsWith("h1-stock:", StringComparison.Ordinal))
                   && childH1StableIds.Distinct(StringComparer.Ordinal).Count()
                   == childH1StableIds.Length
                   && presentationOnly;
        }
    }
}
