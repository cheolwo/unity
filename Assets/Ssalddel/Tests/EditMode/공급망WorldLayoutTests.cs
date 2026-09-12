using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 공급망WorldLayoutTests
    {
        [Test]
        public void Layout은_생산부터공동수령까지6개PresentationZone을순서대로연결한다()
        {
            var layout = 공급망WorldLayoutFixture.Create();

            layout.Validate();

            Assert.That(layout.Zones, Has.Length.EqualTo(6));
            Assert.That(layout.RouteLegs, Has.Length.EqualTo(5));
            Assert.That(layout.Zones.OrderBy(value => value.FlowOrder)
                .Select(value => value.PresentationZoneCode), Is.EqualTo(new[]
                {
                    공급망PresentationZoneCodes.FarmProduction,
                    공급망PresentationZoneCodes.FarmYard,
                    공급망PresentationZoneCodes.TransportCorridor,
                    공급망PresentationZoneCodes.UrbanLogistics,
                    공급망PresentationZoneCodes.UrbanMarket,
                    공급망PresentationZoneCodes.ResidentialCommunity,
                }));
        }

        [Test]
        public void FarmProduction과FarmYard는_같은CanonicalFarm을다른Presentation공간으로사용한다()
        {
            var layout = 공급망WorldLayoutFixture.Create();
            var farmZones = layout.Zones.Where(value =>
                value.CanonicalWorldZoneCode == WorldZoneCodes.Farm).ToArray();

            Assert.That(farmZones, Has.Length.EqualTo(2));
            Assert.That(farmZones.Select(value => value.PresentationZoneCode).Distinct().Count(), Is.EqualTo(2));
            Assert.That(farmZones.Select(value => value.StableId).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void 중복Zone과건너뛴Route를거부한다()
        {
            var duplicate = 공급망WorldLayoutFixture.Create();
            duplicate.Zones[1].StableId = duplicate.Zones[0].StableId;
            Assert.That(() => duplicate.Validate(), Throws.TypeOf<InvalidOperationException>()
                .With.Message.EqualTo("SupplyChainWorldZoneStableIdDuplicate"));

            var disconnected = 공급망WorldLayoutFixture.Create();
            disconnected.RouteLegs[1].ToZoneStableId = disconnected.Zones[4].StableId;
            Assert.That(() => disconnected.Validate(), Throws.TypeOf<InvalidOperationException>()
                .With.Message.EqualTo("SupplyChainWorldRouteDisconnected"));
        }
    }
}
