using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 사가정외관ModuleEntry
    {
        public string visualKey = string.Empty;
        public string moduleKind = string.Empty;
        public string sourceAssetStableId = string.Empty;
        public string sourceFingerprintSha256 = string.Empty;
        public string reviewState = "Candidate";
        public string reviewerKind = string.Empty;
        public string reviewReference = string.Empty;
    }

    /// <summary>
    /// 기존 Synty 자산 대장의 일부를 참조하는 비공개 외관 부품 허용 목록이다.
    /// 자산·건물·지도 정본을 복제하지 않으며 후보 등록은 외관 사용 승인이나 배포 승인이 아니다.
    /// </summary>
    public sealed class 사가정외관ModuleCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private Synty전체자산ModuleCatalog sourceCatalog;
        [SerializeField] private string sourceCatalogRevision = string.Empty;
        [SerializeField] private string sourceCatalogHashSha256 = string.Empty;
        [SerializeField] private 사가정외관ModuleEntry[] entries = Array.Empty<사가정외관ModuleEntry>();
        public string CatalogRevision => catalogRevision;
        public string SourceCatalogRevision => sourceCatalogRevision;
        public string SourceCatalogHashSha256 => sourceCatalogHashSha256;
        public IReadOnlyList<사가정외관ModuleEntry> Entries => entries;
        public bool PrivateReviewOnly => true;
        public bool DistributionApproved => false;

        public void Configure(string revision, Synty전체자산ModuleCatalog source,
            사가정외관ModuleEntry[] values)
        {
            catalogRevision = revision;
            sourceCatalog = source;
            sourceCatalogRevision = source?.CatalogRevision ?? string.Empty;
            sourceCatalogHashSha256 = source?.SourceHashSha256 ?? string.Empty;
            entries = values ?? Array.Empty<사가정외관ModuleEntry>();
        }

        public void Validate()
        {
            if (!사가정외관검토Gate.ReferenceId(catalogRevision) || sourceCatalog == null
                || sourceCatalog.CatalogRevision != sourceCatalogRevision
                || !사가정외관검토Gate.Sha256(sourceCatalogHashSha256)
                || sourceCatalog.SourceHashSha256 != sourceCatalogHashSha256
                || entries.Length == 0 || entries.Length > 32
                || entries.Any(e => e == null || !사가정외관검토Gate.ReferenceId(e.visualKey)
                    || !e.visualKey.StartsWith("appearance.", StringComparison.Ordinal)
                    || !사가정외관검토Gate.ModuleKind(e.moduleKind))
                || entries.Select(e => e.visualKey).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("AppearanceModuleCatalogRejected");
            foreach (var entry in entries) ResolveSource(entry);
        }

        public 사가정외관ModuleEntry Resolve(string visualKey)
            => entries.SingleOrDefault(e => e.visualKey == visualKey)
                ?? throw new InvalidOperationException("AppearanceModuleMissing");

        public GameObject ResolvePrefab(string visualKey) => ResolveSource(Resolve(visualKey)).Prefab;

        private Synty자산ModuleEntry ResolveSource(사가정외관ModuleEntry entry)
        {
            if (sourceCatalog == null) throw new InvalidOperationException("AppearanceSourceCatalogMissing");
            var matches = sourceCatalog.Entries.Where(e => e != null && e.AssetStableId == entry.sourceAssetStableId).ToArray();
            if (matches.Length != 1 || !matches[0].Validate()
                || matches[0].SourceFingerprintSha256 != entry.sourceFingerprintSha256
                || matches[0].LifecycleCode is Synty자산LifecycleCodes.Excluded or Synty자산LifecycleCodes.PrototypeFallback)
                throw new InvalidOperationException("AppearanceModuleSourceChanged");
            // 처음 허용하는 두 부품 계열만 정확히 선택한다. 건물 본체를 크기만 줄여 사용하지 않는다.
            var expectedKind = matches[0].AssetFamilyId switch
            {
                "synty-family:city:buildings:shop-cover" => "Awning",
                "synty-family:city:props:sign-cafe" => "Sign",
                _ => string.Empty,
            };
            if (expectedKind != entry.moduleKind)
                throw new InvalidOperationException("AppearanceWholeBuildingOrUnreviewedFamilyRejected");
            return matches[0];
        }
    }
}
