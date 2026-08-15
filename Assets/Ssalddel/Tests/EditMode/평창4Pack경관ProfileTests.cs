using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 평창4Pack경관ProfileTests
    {
        private const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군법정동경관VisualCatalog.asset";

        [Test]
        public void 다섯영역의_네Pack비중은_각각백이다()
        {
            var profile = 평창4Pack경관Profile.CreateDefault();

            Assert.DoesNotThrow(profile.Validate);
            Assert.That(profile.Weights.Count, Is.EqualTo(5));
            Assert.That(profile.Weights, Has.All.Matches<평창4Pack경관Weight>(
                value => value.Total == 100));
            Assert.That(profile.Resolve(평창4Pack경관AreaCodes.DaegwallyeongFarm).Nature,
                Is.EqualTo(48));
            Assert.That(profile.Resolve(평창4Pack경관AreaCodes.JinbuHub).City,
                Is.EqualTo(45));
        }

        [Test]
        public void 같은세계좌표Slot은_같은Pack을선택한다()
        {
            var profile = 평창4Pack경관Profile.CreateDefault();
            var first = profile.ResolvePack(
                평창4Pack경관AreaCodes.DaegwallyeongFarm,
                2,
                "kr5186:l2:700:1145:forest-edge-01",
                51760);
            var second = profile.ResolvePack(
                평창4Pack경관AreaCodes.DaegwallyeongFarm,
                2,
                "kr5186:l2:700:1145:forest-edge-01",
                51760);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(new[]
            {
                월드CompositionPackCodes.Nature,
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
            }, Does.Contain(first));
        }

        [Test]
        public void 평창경관대장v2는_Nature와기존VisualKey를함께보존한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.CatalogRevision, Is.EqualTo("legal-dong-scenic-catalog.v2"));
            Assert.That(catalog.Entries.Select(value => value.VisualKey),
                Is.EquivalentTo(Ssalddel.Unity.Runtime.World.법정동경관VisualKeys.All));
            Assert.That(catalog.Entries.Count(value => value.SourcePack == "PolygonNature"),
                Is.GreaterThanOrEqualTo(12));
            Assert.That(catalog.Resolve(
                Ssalddel.Unity.Runtime.World.법정동경관VisualKeys.ConiferTree).SourcePack,
                Is.EqualTo("PolygonNature"));
            Assert.That(catalog.Entries, Has.All.Matches<법정동경관VisualCatalogEntry>(
                value => value.ReviewStatusCode == 법정동경관ReviewStatusCodes.Active));
        }
    }
}
