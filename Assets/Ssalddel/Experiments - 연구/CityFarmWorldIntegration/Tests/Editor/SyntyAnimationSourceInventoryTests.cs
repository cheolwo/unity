using System;
using NUnit.Framework;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class SyntyAnimationSourceInventoryTests
    {
        [Test]
        public void 현재SyntyImport는_HumanoidRig와Fx를제공하지만ClipController는없다()
        {
            var report = SyntyAnimationSourceInventory.Inspect();

            Assert.That(report.StandaloneAnimationClipCount, Is.Zero);
            Assert.That(report.AnimatorControllerCount, Is.Zero);
            Assert.That(report.AnimatorOverrideControllerCount, Is.Zero);
            Assert.That(report.HumanoidRigPaths.Count, Is.EqualTo(5));
            Assert.That(report.ImportedCharacterClipPaths, Is.Empty);
            Assert.That(report.ParticleSystemPrefabCounts["farm"], Is.EqualTo(11));
            Assert.That(report.ParticleSystemPrefabCounts["city"], Is.EqualTo(2));
            Assert.That(report.ParticleSystemPrefabCounts["generic"], Is.EqualTo(17));
        }

        [Test]
        public void Town의해소되지않은Controller참조_여덟개를검출하고거부한다()
        {
            var report = SyntyAnimationSourceInventory.Inspect();

            Assert.That(report.MissingControllerPrefabPaths.Count, Is.EqualTo(8));
            Assert.That(report.MissingControllerPrefabPaths,
                Does.Not.Contain(
                    "Assets/Synty/PolygonTown/Prefabs/Characters/SM_Chr_Daughter_01.prefab"));
            var exception = Assert.Throws<InvalidOperationException>(
                report.EnsureNoMissingControllerReferences);
            Assert.That(exception.Message,
                Does.StartWith("SyntyAnimatorControllerReferenceMissing:"));
        }
    }
}
