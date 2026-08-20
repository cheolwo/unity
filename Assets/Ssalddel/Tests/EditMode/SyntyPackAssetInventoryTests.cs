using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class SyntyPackAssetInventoryTests
    {
        [Test]
        public void 다섯팩기술대장은_보유Prefab2346건을_표현전용으로기록한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.Entries.Count, Is.EqualTo(2346));
            Assert.That(catalog.Entries.Count(value =>
                value.PackCode == SyntyPackInventoryCodes.Nature), Is.EqualTo(227));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.Farm),
                Is.EqualTo(498));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.Town),
                Is.EqualTo(702));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.City),
                Is.EqualTo(335));
            Assert.That(catalog.Entries.Count(value =>
                value.PackCode == SyntyPackInventoryCodes.Construction), Is.EqualTo(584));
            Assert.That(catalog.Entries, Has.All.Matches<SyntyPackAssetInventoryEntry>(value =>
                value.PresentationOnly
                && value.SourceFingerprintSha256.Length == 64
                && !string.IsNullOrWhiteSpace(value.AssetFamilyId)
                && SyntyAssetUsageTrackCodes.IsKnown(value.PrimaryUsageTrackCode)
                && value.PlannedAreaCodes.Count > 0));
        }

        [Test]
        public void 정규화분류는_원본경로차이를흡수하고_기존세팩Id재료를보존한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath)!;

            Assert.That(catalog.Entries.Count(value =>
                value.PackCode == SyntyPackInventoryCodes.Farm
                && value.CategoryCode == "Other"
                && value.NormalizedCategoryCode == "Environments"), Is.EqualTo(67));
            Assert.That(catalog.Entries.Count(value =>
                value.PackCode == SyntyPackInventoryCodes.City
                && value.CategoryCode == "Other"
                && value.NormalizedCategoryCode == "Environments"), Is.EqualTo(65));
            Assert.That(catalog.Entries.Where(value =>
                    value.PackCode == SyntyPackInventoryCodes.Farm
                    || value.PackCode == SyntyPackInventoryCodes.Town
                    || value.PackCode == SyntyPackInventoryCodes.City),
                Has.All.Matches<SyntyPackAssetInventoryEntry>(value =>
                    value.InventoryId == "synty-inventory:" + value.PackCode + ":"
                    + value.CategoryCode.ToLowerInvariant() + ":"
                    + value.SourceFingerprintSha256.Substring(0, 16)));
        }

        [Test]
        public void 모든자산은_자산군과활용트랙과계획영역을가진다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath)!;

            Assert.That(catalog.Entries.Select(value => value.Prefab).Distinct().Count(),
                Is.EqualTo(2346));
            Assert.That(catalog.Entries, Has.All.Matches<SyntyPackAssetInventoryEntry>(value =>
                SyntyPackNormalizedCategoryCodes.IsKnown(value.NormalizedCategoryCode)
                && SyntyAssetClassificationStateCodes.IsKnown(value.ClassificationStateCode)
                && value.PlannedAreaCodes.All(SyntyAssetPlannedAreaCodes.IsKnown)));
            Assert.That(catalog.Entries.Where(value =>
                    value.PackCode == SyntyPackInventoryCodes.Construction),
                Has.All.Matches<SyntyPackAssetInventoryEntry>(value =>
                    value.PlannedAreaCodes.Count == 4));
            Assert.That(catalog.Entries.Count(value =>
                value.NormalizedCategoryCode == SyntyPackNormalizedCategoryCodes.Vehicles
                && value.PrimaryUsageTrackCode == SyntyAssetUsageTrackCodes.Vehicle),
                Is.EqualTo(51));
            Assert.That(catalog.Entries.Count(value =>
                value.ClassificationStateCode ==
                SyntyAssetClassificationStateCodes.NeedsHumanReview), Is.EqualTo(1));
        }

        [Test]
        public void 공개요약은_유료원본파일목록을노출하지않는다()
        {
            var summary = File.ReadAllText(SyntyPackAssetInventoryBuilder.SummaryDocumentPath);

            Assert.That(summary, Does.Contain("전체 Prefab: 2346개"));
            Assert.That(summary, Does.Contain("의미 자산군:"));
            Assert.That(summary, Does.Contain("construction"));
            Assert.That(summary, Does.Contain("nature"));
            Assert.That(summary, Does.Not.Contain("Assets/Synty"));
            Assert.That(summary, Does.Not.Contain(".prefab"));
            Assert.That(summary.Split('\n').Length, Is.LessThan(140));
        }
    }
}
