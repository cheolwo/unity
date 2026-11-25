using System;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>원본 자산을 바꾸지 않고 기존 대장의 차양·간판 여섯 부품을 비공개 검토 후보로 준비한다.</summary>
    public static class 사가정외관비공개검토준비
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/사가정비공개외관ModuleCatalog.asset";
        public const string CatalogRevision = "sagajeong-private-appearance-modules.r1";

        [MenuItem("Ssalddel/사가정/비공개 외관 부품 후보 준비")]
        public static void BuildCandidateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<사가정외관ModuleCatalog>(CatalogPath);
            if (existing != null) { existing.Validate(); return; }
            var catalog = CreateTransientCandidates();
            catalog.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("PrivateAppearanceCandidatesPrepared:6:review-not-performed");
        }

        public static 사가정외관ModuleCatalog CreateTransientCandidates()
        {
            var source = AssetDatabase.LoadAssetAtPath<Synty전체자산ModuleCatalog>(
                Synty전체자산ModuleCatalogBuilder.CatalogPath);
            if (source == null) throw new InvalidOperationException("AppearanceSourceCatalogMissing");
            var covers = source.Entries.Where(e => e.AssetFamilyId == "synty-family:city:buildings:shop-cover")
                .OrderBy(e => e.Prefab.name, StringComparer.Ordinal).ToArray();
            var signs = source.Entries.Where(e => e.AssetFamilyId == "synty-family:city:props:sign-cafe").ToArray();
            if (covers.Length != 5 || signs.Length != 1)
                throw new InvalidOperationException("AppearanceCandidateModuleSetChanged");
            var entries = covers.Select((e, i) => Candidate(e, "Awning",
                "appearance.awning.city-cover." + (i + 1).ToString("00")))
                .Concat(signs.Select(e => Candidate(e, "Sign", "appearance.sign.city-cafe.01"))).ToArray();
            var catalog = ScriptableObject.CreateInstance<사가정외관ModuleCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.Configure(CatalogRevision, source, entries);
            catalog.Validate();
            return catalog;
        }

        private static 사가정외관ModuleEntry Candidate(Synty자산ModuleEntry source,
            string kind, string key)
            => new()
            {
                visualKey = key, moduleKind = kind, sourceAssetStableId = source.AssetStableId,
                sourceFingerprintSha256 = source.SourceFingerprintSha256, reviewState = "Candidate",
            };
    }
}
