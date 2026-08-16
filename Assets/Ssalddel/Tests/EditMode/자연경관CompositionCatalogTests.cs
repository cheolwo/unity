using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 자연경관CompositionCatalogTests
    {
        [OneTimeSetUp]
        public void GenerateCatalog()
        {
            자연경관CompositionSetBuilder.Build();
            자연경관EventOverlayBuilder.Build();
        }

        [Test]
        public void 여덟경관Set은_A_B_C_세변형을가진다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                자연경관CompositionSetBuilder.CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.Entries.Count, Is.EqualTo(24));
            foreach (var setName in 자연경관SetNames.All)
            {
                var entries = catalog.Entries.Where(value => value.SetName == setName).ToArray();
                Assert.That(entries.Select(value => value.VariantCode),
                    Is.EquivalentTo(월드CompositionVariantCodes.All));
                Assert.That(entries.All(value => value.PresentationOnly), Is.True);
                Assert.That(entries.All(value => value.MaterialSlotCount > 0), Is.True);
            }
        }

        [Test]
        public void 자연역할과_토지피복_경사_수계_성능예산이구분된다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                자연경관CompositionSetBuilder.CatalogPath)!;

            var ridge = catalog.Resolve(
                자연경관SetNames.산능선, 월드CompositionVariantCodes.A);
            Assert.That(ridge.NatureRoleCode, Is.EqualTo(자연경관RoleCodes.Backdrop));
            Assert.That(ridge.GpuBudgetTierCode,
                Is.EqualTo(자연경관GpuBudgetTierCodes.Overview));
            Assert.That(ridge.MinimumViewDistance, Is.GreaterThanOrEqualTo(70f));
            Assert.That(ridge.HlodEligible, Is.True);
            Assert.That(ridge.ShadowPolicyCode,
                Is.EqualTo(자연경관ShadowPolicyCodes.Disabled));

            var edge = catalog.Resolve(
                자연경관SetNames.숲가장자리, 월드CompositionVariantCodes.A);
            Assert.That(edge.NatureRoleCode,
                Is.EqualTo(자연경관RoleCodes.Understory));
            Assert.That(edge.GpuBudgetTierCode,
                Is.EqualTo(자연경관GpuBudgetTierCodes.Task));
            Assert.That(edge.AllowedLandCoverCodes,
                Does.Contain(법정동LandCoverCodes.Cropland));
            Assert.That(edge.ShadowPolicyCode,
                Is.EqualTo(자연경관ShadowPolicyCodes.ReceiveOnly));

            var canopy = catalog.Resolve(
                자연경관SetNames.혼효림군집, 월드CompositionVariantCodes.A);
            Assert.That(canopy.ShadowPolicyCode,
                Is.EqualTo(자연경관ShadowPolicyCodes.CastReceive));

            var stream = catalog.Resolve(
                자연경관SetNames.개울회랑, 월드CompositionVariantCodes.A);
            Assert.That(stream.RequiresWaterMask, Is.True);
            Assert.That(stream.AllowedLandCoverCodes,
                Is.EquivalentTo(new[] { 법정동LandCoverCodes.Water }));
            Assert.That(stream.ShaderFeatureCodes,
                Does.Contain(자연경관ShaderFeatureCodes.Water));
            Assert.That(stream.ParticleSystemCount, Is.GreaterThan(0));
        }

        [Test]
        public void 같은세계Slot은_같은변형을선택하고_수계없는개울을거부한다()
        {
            var selector = new 자연경관CompositionSelector();
            var first = selector.ResolveVariant(자연경관SetNames.개울회랑,
                "kr5186:l2:700:1145:stream-01", 51760);
            var second = selector.ResolveVariant(자연경관SetNames.개울회랑,
                "kr5186:l2:700:1145:stream-01", 51760);
            Assert.That(second, Is.EqualTo(first));

            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                자연경관CompositionSetBuilder.CatalogPath)!;
            var stream = catalog.Resolve(자연경관SetNames.개울회랑, first);
            Assert.That(selector.CanPlace(stream, 법정동LandCoverCodes.Water,
                5f, true, 자연경관SeasonCodes.Spring,
                자연경관MoodCodes.Peaceful, 30f), Is.True);
            Assert.That(selector.CanPlace(stream, 법정동LandCoverCodes.Water,
                5f, false, 자연경관SeasonCodes.Spring,
                자연경관MoodCodes.Peaceful, 30f), Is.False);
            Assert.That(selector.CanPlace(stream, 법정동LandCoverCodes.Forest,
                5f, true, 자연경관SeasonCodes.Spring,
                자연경관MoodCodes.Peaceful, 30f), Is.False);
        }

        [Test]
        public void 생성Prefab은_Nature원본을감싸며_표현권위만가진다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                자연경관CompositionSetBuilder.CatalogPath)!;

            foreach (var entry in catalog.Entries)
            {
                var view = entry.Prefab.GetComponent<자연경관CompositionSetView>();
                Assert.That(view, Is.Not.Null, entry.CompositionKey);
                Assert.That(view!.ValidateWiring(), Is.True, entry.CompositionKey);
                Assert.That(view.PresentationOnly, Is.True, entry.CompositionKey);

                var nestedSources = entry.Prefab.GetComponentsInChildren<Transform>(true)
                    .Select(value => PrefabUtility.GetCorrespondingObjectFromSource(value.gameObject))
                    .Where(value => value != null)
                    .Select(AssetDatabase.GetAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToArray();
                Assert.That(nestedSources.Any(path =>
                    path.StartsWith("Assets/Synty/PolygonNature/Prefabs/")), Is.True,
                    entry.CompositionKey);
                Assert.That(entry.Prefab.GetComponentsInChildren<MonoBehaviour>(true)
                    .All(value => value is 자연경관CompositionSetView
                        or 자연경관ShadowPolicyView), Is.True,
                    entry.CompositionKey);
                var shadow = entry.Prefab.GetComponent<자연경관ShadowPolicyView>();
                Assert.That(shadow, Is.Not.Null, entry.CompositionKey);
                Assert.That(shadow!.ValidateWiring(), Is.True, entry.CompositionKey);
                Assert.That(shadow.ShadowPolicyCode,
                    Is.EqualTo(entry.ShadowPolicyCode), entry.CompositionKey);
            }
        }

        [Test]
        public void 수관은그림자를만들고_하층과원경은비용을줄인다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                자연경관CompositionSetBuilder.CatalogPath)!;
            var canopy = catalog.Resolve(
                자연경관SetNames.활엽수림군집, 월드CompositionVariantCodes.A)
                .Prefab.GetComponent<자연경관ShadowPolicyView>();
            var edge = catalog.Resolve(
                자연경관SetNames.숲가장자리, 월드CompositionVariantCodes.A)
                .Prefab.GetComponent<자연경관ShadowPolicyView>();
            var ridge = catalog.Resolve(
                자연경관SetNames.산능선, 월드CompositionVariantCodes.A)
                .Prefab.GetComponent<자연경관ShadowPolicyView>();

            Assert.That(canopy.CastingRendererCount, Is.GreaterThan(0));
            Assert.That(canopy.ReceiveOnlyRendererCount, Is.GreaterThan(0));
            Assert.That(edge.CastingRendererCount, Is.Zero);
            Assert.That(edge.ReceiveOnlyRendererCount, Is.GreaterThan(0));
            Assert.That(ridge.CastingRendererCount, Is.Zero);
        }

        [Test]
        public void 사계절Profile은_의미키와_원본비파괴색조를제공한다()
        {
            var profile = 자연경관SeasonPresentationProfile.CreateDefault();

            Assert.DoesNotThrow(profile.Validate);
            Assert.That(profile.Rules.Count, Is.EqualTo(4));
            Assert.That(profile.Resolve(자연경관SeasonCodes.Winter).ConiferMaterialKey,
                Is.EqualTo("nature.material.pine.snow"));
            Assert.That(profile.Resolve(자연경관SeasonCodes.Autumn).AmbientFxVisualKey,
                Is.EqualTo("nature.fx.falling-leaves"));
        }

        [Test]
        public void 평온탐색에는_사건Overlay가없고_기존서버키만등록된다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<자연경관EventOverlayCatalog>(
                자연경관EventOverlayBuilder.CatalogPath)!;

            Assert.DoesNotThrow(catalog.Validate);
            Assert.That(catalog.Entries.Count, Is.EqualTo(5));
            Assert.That(catalog.TryResolve(
                자연경관EventPresentationKeys.ScenicExploration, out _), Is.False);
            Assert.That(catalog.Entries, Has.All.Matches<자연경관EventOverlayCatalogEntry>(
                value => value.NatureRoleCode == 자연경관RoleCodes.EventOnly
                    && value.MoodCode == 자연경관MoodCodes.SurvivalEvent
                    && value.PresentationOnly));
        }

        [Test]
        public void 저장Scene은_봄계절과_비활성사건Overlay를가진다()
        {
            EditorSceneManager.OpenScene(대한민국법정동WorldBuilder.ScenePath,
                OpenSceneMode.Single);
            var season = Object.FindFirstObjectByType<자연경관SeasonPresentationController>(
                FindObjectsInactive.Include);
            var overlay = Object.FindFirstObjectByType<자연경관EventOverlayController>(
                FindObjectsInactive.Include);

            Assert.That(season, Is.Not.Null);
            Assert.That(season!.ValidateWiring(), Is.True);
            Assert.That(season.ActiveSeasonCode, Is.EqualTo(자연경관SeasonCodes.Spring));
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay!.ValidateWiring(), Is.True);
            Assert.That(overlay.ActiveOverlayCount, Is.EqualTo(0));
            Assert.That(overlay.ApplyPresentationKey(
                자연경관EventPresentationKeys.ZombieWarning), Is.True);
            Assert.That(overlay.ActiveOverlayCount, Is.EqualTo(1));
            Assert.That(overlay.ApplyPresentationKey(
                자연경관EventPresentationKeys.ScenicExploration), Is.False);
            Assert.That(overlay.ActiveOverlayCount, Is.EqualTo(0));

            var sets = Object.FindObjectsByType<자연경관CompositionSetView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(sets.Any(value => value.SetName == 자연경관SetNames.산능선),
                Is.True);
            Assert.That(sets.Any(value => value.SetName == 자연경관SetNames.숲가장자리),
                Is.True);
            Assert.That(sets.Any(value => value.SetName == 자연경관SetNames.개울회랑),
                Is.False);
        }
    }
}
