using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 월드CompositionContractTests
    {
        private const string FarmCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/농장풍경CompositionCatalog.asset";

        [Test]
        public void 농장서른여섯Set는_공통Contract로손실없이적응된다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<농장풍경CompositionCatalog>(FarmCatalogPath)
                ?? throw new AssertionException("Farm composition catalog missing");

            var descriptors = 농장풍경CompositionAdapter.Adapt(catalog);

            var expectedCount = 농장풍경SetNames.All.Count * 월드CompositionVariantCodes.All.Count;
            Assert.That(descriptors.Count, Is.EqualTo(expectedCount));
            Assert.That(descriptors.All(value =>
                value.PackCode == 월드CompositionPackCodes.Farm
                && value.SourceKind == 월드CompositionSourceKinds.SyntyNestedPrefab
                && value.HasEnvironmentRoot
                && value.Validate()), Is.True);
            Assert.That(descriptors.Select(value => value.CompositionKey).Distinct().Count(),
                Is.EqualTo(expectedCount));
        }

        [Test]
        public void 기존농장A_B_C는_같은Footprint와SocketSignature를유지한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<농장풍경CompositionCatalog>(FarmCatalogPath)
                ?? throw new AssertionException("Farm composition catalog missing");
            var descriptors = 농장풍경CompositionAdapter.Adapt(catalog);

            foreach (var group in descriptors.GroupBy(value => value.SetName))
            {
                Assert.That(group.Select(value => value.VariantCode),
                    Is.EquivalentTo(월드CompositionVariantCodes.All));
                Assert.That(group.Select(value => value.BuildStructuralSignature()).Distinct().Count(),
                    Is.EqualTo(1), group.Key);
            }
        }

        [Test]
        public void 중복Key는_공통Validator에서거부된다()
        {
            var descriptor = CreateDescriptor("교차로", 월드CompositionVariantCodes.A);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                월드CompositionContractValidator.Validate(
                    new[] { descriptor, descriptor },
                    false));

            Assert.That(exception.Message, Is.EqualTo("CompositionKeyDuplicate"));
        }

        [Test]
        public void 잘못된Connector방향은_공통Validator에서거부된다()
        {
            var connector = new 월드CompositionConnectorContract();
            connector.Configure(
                "road.connector.north",
                "diagonal",
                월드CompositionConnectorKindCodes.Vehicle,
                "road.vehicle.5m",
                Vector3.zero,
                0f,
                5f,
                false);
            var descriptor = CreateDescriptor(
                "교차로",
                월드CompositionVariantCodes.A,
                new[] { connector });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                월드CompositionContractValidator.Validate(new[] { descriptor }, false));

            Assert.That(exception.Message, Is.EqualTo("CompositionDescriptorInvalid"));
        }

        [Test]
        public void A_B_C의구조Signature가다르면_확장을거부한다()
        {
            var descriptors = new[]
            {
                CreateDescriptor("기본주택", 월드CompositionVariantCodes.A),
                CreateDescriptor("기본주택", 월드CompositionVariantCodes.B),
                CreateDescriptor("기본주택", 월드CompositionVariantCodes.C, footprint: new Vector2(12f, 10f)),
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                월드CompositionContractValidator.Validate(descriptors));

            Assert.That(exception.Message,
                Is.EqualTo("CompositionVariantSignatureMismatch:town:기본주택"));
        }

        [Test]
        public void StatefulJourney와AmbientTraffic은_서로다른Code로유지된다()
        {
            Assert.That(월드CompositionJourneyKindCodes.Stateful,
                Is.Not.EqualTo(월드CompositionJourneyKindCodes.Ambient));
            Assert.That(월드CompositionJourneyKindCodes.All,
                Does.Contain(월드CompositionJourneyKindCodes.Stateful));
            Assert.That(월드CompositionJourneyKindCodes.All,
                Does.Contain(월드CompositionJourneyKindCodes.Ambient));
        }

        private static 월드CompositionDescriptor CreateDescriptor(
            string setName,
            string variant,
            월드CompositionConnectorContract[] connectors = null,
            Vector2? footprint = null)
        {
            var descriptor = new 월드CompositionDescriptor();
            descriptor.Configure(
                월드CompositionDescriptor.BuildKey(
                    월드CompositionPackCodes.Town,
                    setName,
                    variant),
                setName,
                variant,
                월드CompositionPackCodes.Town,
                월드CompositionSourceKinds.SyntyNestedPrefab,
                footprint ?? new Vector2(10f, 10f),
                new Vector2(5f, 5f),
                true,
                false,
                false,
                월드CompositionJourneyKindCodes.None,
                new[]
                {
                    월드CompositionDetailTierCodes.World,
                    월드CompositionDetailTierCodes.Zone,
                },
                connectors ?? Array.Empty<월드CompositionConnectorContract>(),
                Array.Empty<월드CompositionSocketContract>());
            return descriptor;
        }
    }
}
