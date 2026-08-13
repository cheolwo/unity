using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 법정동Synty후단연결Tests
    {
        [Test]
        public void 어울리는SyntyPrefab은_공간계획뒤에만_연결한다()
        {
            var prefab = new GameObject("VendorPrefabNameDoesNotBecomeStableId");
            var catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
            var entry = Entry(prefab, new[] { 법정동LandCoverCodes.Cropland },
                new[] { 법정동WorldRoleCodes.Farm }, new Vector2(0f, 12f));
            catalog.Configure("test-catalog.v1", new[] { entry });
            var placement = Placement();

            try
            {
                var result = new 법정동Synty후단연결기().연결(placement, catalog, 4f);

                Assert.That(result.연결가능여부, Is.True);
                Assert.That(result.PlacementStableId, Is.EqualTo(placement.PlacementStableId));
                Assert.That(result.VisualKey, Is.EqualTo(placement.VisualKey));
                Assert.That(result.Prefab, Is.SameAs(prefab));
                Assert.That(result.PresentationOnly, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void 토지피복이나경사가맞지않으면_Prefab을연결하지않는다()
        {
            var prefab = new GameObject("SyntyPrefab");
            var catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
            catalog.Configure("test-catalog.v1", new[]
            {
                Entry(prefab, new[] { 법정동LandCoverCodes.Forest },
                    new[] { 법정동WorldRoleCodes.Farm }, new Vector2(0f, 8f)),
            });
            var placement = Placement();

            try
            {
                var landCover = new 법정동Synty후단연결기().연결(placement, catalog, 4f);
                placement.LandCoverCode = 법정동LandCoverCodes.Forest;
                var slope = new 법정동Synty후단연결기().연결(placement, catalog, 18f);

                Assert.That(landCover.StatusCode,
                    Is.EqualTo(법정동시각연결상태Codes.토지피복불일치));
                Assert.That(slope.StatusCode,
                    Is.EqualTo(법정동시각연결상태Codes.경사불일치));
                Assert.That(landCover.Prefab, Is.Null);
                Assert.That(slope.Prefab, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void 경관계획전체를_파이프라인마지막단계에서_한번에연결한다()
        {
            var prefab = new GameObject("SyntyPrefab");
            var catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
            catalog.Configure("test-catalog.v1", new[]
            {
                Entry(prefab, new[] { 법정동LandCoverCodes.Cropland },
                    new[] { 법정동WorldRoleCodes.Farm }, new Vector2(0f, 12f)),
            });
            var placement = Placement();
            var plan = new 법정동경관PlanData
            {
                PlanStableId = "landscape-plan:test",
                DeterministicSeed = 51760,
                RuleRevision = "landscape.v1",
                Placements = new[] { placement },
            };

            try
            {
                var results = new 법정동Synty후단연결기().연결계획(
                    plan, catalog, _ => 4f);

                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0].연결가능여부, Is.True);
                Assert.That(results[0].PlacementStableId,
                    Is.EqualTo(placement.PlacementStableId));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefab);
            }
        }

        private static 법정동경관VisualCatalogEntry Entry(
            GameObject prefab, string[] landCovers, string[] roles, Vector2 slopes)
        {
            var entry = new 법정동경관VisualCatalogEntry();
            entry.Configure(
                법정동경관VisualKeys.Barn, "PolygonFarm", prefab,
                landCovers, roles, new Vector2(8f, 6f), slopes,
                1, 1, new[] { "All" }, 1f,
                법정동경관CollisionPolicyCodes.FootprintOnly,
                1000, 1, 1, 1, 0, 0, false, true);
            return entry;
        }

        private static 법정동경관PlacementData Placement() => new()
        {
            PlacementStableId = "placement:sim:pyeongchang:farm:barn-1",
            RegionStableId = "region:kr:legal:5176038000",
            VisualKey = 법정동경관VisualKeys.Barn,
            LandCoverCode = 법정동LandCoverCodes.Cropland,
            RegionRoleCode = 법정동WorldRoleCodes.Farm,
            EvidenceCode = 법정동WorldEvidenceCodes.SimulationScenario,
            Scale = 1f,
            DensityTier = 1,
            LodGroup = 1,
            PresentationOnly = true,
        };
    }
}
