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
        public void 세팩기술대장은_보유Prefab1535건을_표현전용으로기록한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SyntyPackAssetInventoryCatalog>(
                SyntyPackAssetInventoryBuilder.CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.Entries.Count, Is.EqualTo(1535));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.Farm),
                Is.EqualTo(498));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.Town),
                Is.EqualTo(702));
            Assert.That(catalog.Entries.Count(value => value.PackCode == SyntyPackInventoryCodes.City),
                Is.EqualTo(335));
            Assert.That(catalog.Entries, Has.All.Matches<SyntyPackAssetInventoryEntry>(value =>
                value.PresentationOnly && value.SourceFingerprintSha256.Length == 64));
        }

        [Test]
        public void 공개요약은_유료원본파일목록을노출하지않는다()
        {
            var summary = File.ReadAllText(SyntyPackAssetInventoryBuilder.SummaryDocumentPath);

            Assert.That(summary, Does.Contain("전체 Prefab: 1535개"));
            Assert.That(summary, Does.Not.Contain("Assets/Synty"));
            Assert.That(summary, Does.Not.Contain(".prefab"));
            Assert.That(summary.Split('\n').Length, Is.LessThan(80));
        }
    }
}
