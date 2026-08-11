using NUnit.Framework;
using Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor;
using UnityEditor;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Tests.Editor
{
    public sealed class 연구Scene폴더정리Tests
    {
        [TestCase(PotatoCultivationLifecycleBuilder.ScenePath, "c3e0d68d8ac57c54e8d55fc675d7f652")]
        [TestCase(PotatoHarvestCargoLifecycleBuilder.ScenePath, "1047765d762afc240887a8dcc328ee72")]
        [TestCase(PotatoJourneyFarmVerticalSliceBuilder.ScenePath, "30f8ca6732a5b0743baa3156465c8804")]
        [TestCase(PotatoJourneyHubRouteBuilder.ScenePath, "3ac80ca49dadc884ea0b820c737d1c8b")]
        [TestCase(PotatoCargoJourneyLifecycleBuilder.ScenePath, "8d9f35616caa22b4d82ca722fc3984b3")]
        [TestCase(PotatoHubReceivingLifecycleBuilder.ScenePath, "2563f75dbf347484189b63183ead5207")]
        [TestCase(PotatoHubDispositionLifecycleBuilder.ScenePath, "fb20ee028770cb94385f9d5ca5777af4")]
        [TestCase(PotatoJourneyCityBuilder.ScenePath, "ee4a8b5c5abaae04e87ae1af04592a19")]
        [TestCase(HarvestDispositionChoiceBuilder.ScenePath, "b3298476727068f4f84365b1ed5aa510")]
        [TestCase(CooperativeIntakeLifecycleBuilder.ScenePath, "d6723cf96a2db0c458768362b42d45f5")]
        [TestCase(DirectOnlineSaleLifecycleBuilder.ScenePath, "93db34097e59f8a46b9f6fbd7cb4dff5")]
        [TestCase(신티에셋연구소Builder.ScenePath, "28f819f8fef99914f8aaee1e25b74d44")]
        public void 맥락별_경로에서_기존SceneGuid를_보존한다(string path, string expectedGuid)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(expectedGuid), path);
        }
    }
}
