using NUnit.Framework;
using Ssalddel.Unity.PotatoJourney;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoJourneyOperationalApiClientTests
    {
        [Test]
        public void WireJson은_서버계약을_ApiModel과검증Snapshot으로변환한다()
        {
            const string json = "{"
                + "\"stableId\":\"world-slice:potato-journey\","
                + "\"revision\":\"wire:1\","
                + "\"generatedAt\":\"2026-08-10T03:00:00Z\","
                + "\"authorizedRoleCode\":\"Producer\","
                + "\"viewerScopeCode\":\"AuthorizedParty\","
                + "\"authorizationDecisionId\":\"authorized:producer-a\","
                + "\"sourceModeCode\":\"OperationalProjection\","
                + "\"linkageStatusCode\":\"ProductOnly\","
                + "\"product\":{\"productStableId\":\"product:potato\",\"displayName\":\"Potato\",\"hsPrefix\":\"0701\",\"mappingQualityCode\":\"ExactCommodity\",\"mappingQualityLabel\":\"Exact\",\"mappingEvidence\":\"HS 0701\",\"informationOnly\":true},"
                + "\"domesticPrice\":{\"statusCode\":\"Ready\",\"hsCode\":\"0701\",\"unitCode\":\"KRW_PER_KG\",\"currencyCode\":\"KRW\",\"dataSource\":\"aT\",\"startDate\":\"20260801\",\"endDate\":\"20260810\",\"wholesale\":{\"marketStageCode\":\"Wholesale\",\"marketStageLabel\":\"Wholesale\",\"averageKrwPerKg\":2450,\"minimumKrwPerKg\":2200,\"maximumKrwPerKg\":2700,\"sampleCount\":8,\"latestSurveyDate\":\"20260810\"},\"notices\":[\"Information only\"],\"informationOnly\":true},"
                + "\"sourceLineage\":[{\"sourceKey\":\"public-data:kamis\",\"sourceStableId\":\"price-observation:potato.0701\",\"sourceRevision\":\"20260810\",\"observedAt\":\"2026-08-10T00:00:00Z\",\"sourceModeCode\":\"OperationalProjection\"}],"
                + "\"limitations\":[\"No canonical cargo relation\"],\"isReadOnly\":true}";

            var api = OperationalPotatoJourneyApiClient.ParseJson(json);
            var snapshot = new PotatoJourneyMapper().Map(api);

            Assert.AreEqual("wire:1", snapshot.Revision);
            Assert.AreEqual(2450m, snapshot.DomesticPrice.Wholesale.AverageKrwPerKg);
            Assert.AreEqual(PotatoJourneyLinkageStatusCodes.ProductOnly, snapshot.LinkageStatusCode);
            Assert.IsNull(snapshot.CargoJourney);
        }

        [Test]
        public void 빈응답은_fixture로대체하지않고_거부한다()
        {
            var error = Assert.Throws<PotatoJourneyOperationalApiException>(
                () => OperationalPotatoJourneyApiClient.ParseJson(" "));

            Assert.AreEqual("PotatoJourneyHttpResponseEmpty", error.Message);
        }

        [Test]
        public void NullableAuthorizedQuantity_RemainsNullAcrossWireMapping()
        {
            const string json = "{"
                + "\"generatedAt\":\"2026-08-10T03:00:00Z\","
                + "\"warehouse\":{\"warehouseStableId\":\"warehouse:hub-1\","
                + "\"inventoryStableId\":\"inventory:potato-1\","
                + "\"taskStableId\":\"task:inbound-1\","
                + "\"statusCode\":\"AwaitingAuthorization\","
                + "\"authorizedQuantity\":null}}";

            var api = OperationalPotatoJourneyApiClient.ParseJson(json);

            Assert.IsNotNull(api.Warehouse);
            Assert.IsNull(api.Warehouse.AuthorizedQuantity);
        }
    }
}
