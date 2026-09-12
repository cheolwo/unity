using NUnit.Framework;
using Ssalddel.Unity.Editor.HansFarmAdjacentForestAssembly;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Hans농장정교한양식화Tests
    {
        [TestCase("boundary:farm:north:1", true)]
        [TestCase("boundary:forest:north:1", false)]
        [TestCase(null, false)]
        public void 농장경계_이름만_울타리지주_보강대상이다(string value, bool expected)
        {
            Assert.That(Hans농장정교한양식화Authoring.IsFarmBoundaryName(value), Is.EqualTo(expected));
        }

        [TestCase("vegetation:farm-edge-grass:01", true)]
        [TestCase("vegetation:forest-edge-understory:01", false)]
        [TestCase(null, false)]
        public void 큰풀_보정은_농장가장자리표현에만_적용한다(string value, bool expected)
        {
            Assert.That(Hans농장정교한양식화Authoring.IsFarmEdgeGrassName(value), Is.EqualTo(expected));
        }

        [Test]
        public void 풀_시각보정은_원래_배치루트가_아닌_Renderer에_적용할_고정값이다()
        {
            Assert.That(Hans농장정교한양식화Authoring.RefinedGrassRendererScale, Is.EqualTo(new Vector3(0.62f, 0.62f, 0.62f)));
        }
    }
}
