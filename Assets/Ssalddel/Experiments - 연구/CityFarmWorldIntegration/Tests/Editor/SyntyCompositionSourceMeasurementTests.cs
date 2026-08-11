using System;
using System.Linq;
using NUnit.Framework;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class SyntyCompositionSourceMeasurementTests
    {
        [Test]
        public void 측정Catalog는_세Pack과_다섯Source역할을_모두포함한다()
        {
            var definitions = SyntyCompositionSourceMeasurementCatalog.Definitions;

            Assert.That(definitions.Count, Is.EqualTo(42));
            Assert.That(definitions.Select(value => value.PackCode).Distinct(),
                Is.EquivalentTo(new[] { "farm", "town", "city" }));
            Assert.That(definitions.Select(value => value.SourceRoleCode).Distinct(),
                Is.EquivalentTo(SyntyCompositionSourceRoleCodes.All));
            Assert.That(definitions.Count(value =>
                    value.PackCode == "town"
                    && value.SourceRoleCode ==
                    SyntyCompositionSourceRoleCodes.CompleteBuilding),
                Is.EqualTo(12));
            Assert.That(definitions.Select(value => value.AssetPath)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(definitions.Count));
        }

        [Test]
        public void 실측은_모든Source의Bounds와Pivot과Renderer를_기록한다()
        {
            var report = SyntyCompositionSourceMeasurementInspector.Inspect();

            Assert.That(report.entries.Length, Is.EqualTo(42));
            Assert.That(report.entries, Has.All.Matches<SyntyCompositionSourceMeasurementEntry>(
                value => value.rendererCount > 0
                         && value.localBoundsSize.x > 0f
                         && value.localBoundsSize.y >= 0f
                         && value.localBoundsSize.z > 0f
                         && value.rootScaleIsUnit
                         && value.shaderNames.Length > 0));
            Assert.That(report.totalRendererCount, Is.GreaterThan(42));
            Assert.That(report.sharedShaderNames, Is.Not.Empty);
        }

        [Test]
        public void Town과City의_5mGrid와_FarmAdapter오차를_명시한다()
        {
            var report = SyntyCompositionSourceMeasurementInspector.Inspect();

            Assert.That(report.townCityGridCellSize, Is.EqualTo(5f));
            Assert.That(report.townGridConfirmed, Is.True);
            Assert.That(report.cityGridConfirmed, Is.True);
            Assert.That(report.farmRoadModuleLength, Is.GreaterThan(0f));
            Assert.That(report.farmRoadGridError, Is.GreaterThanOrEqualTo(0f));
            Assert.That(float.IsNaN(report.farmToTownAdapterOffset.x), Is.False);
            Assert.That(float.IsNaN(report.farmToTownAdapterOffset.z), Is.False);
        }

        [Test]
        public void 문을검사하는Source는_방향미확정여부를_숨기지않는다()
        {
            var report = SyntyCompositionSourceMeasurementInspector.Inspect();
            var entranceEntries = SyntyCompositionSourceMeasurementCatalog.Definitions
                .Where(value => value.InspectEntrance)
                .Select(value => report.entries.Single(entry =>
                    entry.assetPath == value.AssetPath))
                .ToArray();

            Assert.That(entranceEntries.Length, Is.EqualTo(15));
            Assert.That(entranceEntries, Has.All.Matches<SyntyCompositionSourceMeasurementEntry>(
                value => !string.IsNullOrWhiteSpace(value.entranceDirectionCode)
                         && value.entranceDirectionCode !=
                         SyntyCompositionEntranceDirectionCodes.None));
        }
    }
}
