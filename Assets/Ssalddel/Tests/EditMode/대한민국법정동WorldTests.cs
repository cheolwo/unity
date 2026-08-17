using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 대한민국법정동WorldTests
    {
        [Test]
        public void 평창군Fixture는_공식대표점8개와_개략연결로7개를_분리한다()
        {
            var value = 평창군법정동WorldFixture.Create();

            Assert.That(value.Nodes, Has.Length.EqualTo(8));
            Assert.That(value.Routes, Has.Length.EqualTo(7));
            Assert.That(value.Nodes.Select(node => node.LegalDongCode), Is.Unique);
            Assert.That(value.Nodes, Has.All.Matches<법정동WorldNodeData>(node =>
                node.BoundaryPoints.Length >= 3
                && node.BoundaryEvidenceCode ==
                    법정동WorldEvidenceCodes.OfficialBoundaryPolygon));
            Assert.That(value.Routes, Has.All.Matches<법정동WorldRouteData>(route =>
                route.RouteEvidenceCode == 법정동WorldEvidenceCodes.RepresentativePointTopology
                && !route.IsActualRoad));
        }

        [Test]
        public void DEM과WorldCover는_완전범위와국제공신력있는Raster근거를_분리한다()
        {
            var value = 평창군법정동WorldFixture.Create();

            Assert.That(value.Nodes, Has.All.Matches<법정동WorldNodeData>(node =>
                node.ElevationStatusCode == 법정동SpatialStatusCodes.Complete
                && node.LandCoverStatusCode == 법정동SpatialStatusCodes.Complete
                && node.ElevationEvidenceCode ==
                    법정동WorldEvidenceCodes.AuthoritativeInternationalRaster
                && node.LandCoverEvidenceCode ==
                    법정동WorldEvidenceCodes.AuthoritativeInternationalRaster));
        }

        [Test]
        public void 경관Plan은_결정적Seed와PresentationOnly배치를보존한다()
        {
            var first = 평창군경관Fixture.Create();
            var second = 평창군경관Fixture.Create();

            Assert.That(first.DeterministicSeed, Is.EqualTo(51760));
            Assert.That(first.Placements.Select(item => item.PlacementStableId),
                Is.EqualTo(second.Placements.Select(item => item.PlacementStableId)));
            Assert.That(first.Placements, Has.All.Matches<법정동경관PlacementData>(item =>
                item.PresentationOnly
                && item.EvidenceCode == 법정동WorldEvidenceCodes.SimulationScenario));
            Assert.That(first.Placements.Any(item => item.VisualKey == 법정동경관VisualKeys.Barn), Is.True);
            Assert.That(first.Placements.Any(item => item.VisualKey == 법정동경관VisualKeys.LogisticsBuilding), Is.True);
            Assert.That(first.Placements.Any(item => item.VisualKey == 법정동경관VisualKeys.TownHouse), Is.True);
        }

        [Test]
        public void Synty경관이PresentationOnly가아니면_검증에서차단한다()
        {
            var value = 평창군경관Fixture.Create();
            value.Placements[0].PresentationOnly = false;

            Assert.That(() => 법정동경관PlanValidator.Validate(value),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo("LegalDongScenicPlanInvalid"));
        }

        [Test]
        public void FarmHubTown역할은_공식경계가아닌_SimulationScenario다()
        {
            var value = 평창군법정동WorldFixture.Create();
            var roles = value.Nodes.Where(node => node.RoleCode != 법정동WorldRoleCodes.Region);

            Assert.That(roles.Select(node => node.RoleCode),
                Is.EquivalentTo(new[] { 법정동WorldRoleCodes.Farm,
                    법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }));
            Assert.That(roles, Has.All.Matches<법정동WorldNodeData>(node =>
                node.RoleEvidenceCode == 법정동WorldEvidenceCodes.SimulationScenario));
        }

        [Test]
        public void 대표점연결을_실제도로로표시하면_검증에서차단한다()
        {
            var value = 평창군법정동WorldFixture.Create();
            value.Routes[0].IsActualRoad = true;

            Assert.That(() => 법정동WorldProjectionValidator.Validate(value),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo("LegalDongSimplifiedRouteInvalid"));
        }

        [Test]
        public void 경관RenderingProfile은_그래픽품질과_1인칭시야를_표현전용으로보존한다()
        {
            var value = 평창군경관RenderingFixture.Create();

            Assert.That(value.Validate(), Is.True);
            Assert.That(value.ProfileStableId,
                Is.EqualTo(
                    "rendering-profile:sim:pyeongchang:rural-clear-late-morning.v2"));
            Assert.That(value.RuleRevision, Is.EqualTo("landscape-quality-pass.v2"));
            Assert.That(value.SunIntensity, Is.EqualTo(1.30f));
            Assert.That(value.SunPitch, Is.EqualTo(42f));
            Assert.That(value.SunYaw, Is.EqualTo(-32f));
            Assert.That(value.ShadowDistance, Is.EqualTo(80f));
            Assert.That(value.ShadowCascadeCount, Is.EqualTo(4));
            Assert.That(value.ShadowCascade1, Is.LessThan(value.ShadowCascade2));
            Assert.That(value.ShadowCascade2, Is.LessThan(value.ShadowCascade3));
            Assert.That(value.AmbientOcclusionIntensity, Is.EqualTo(.35f));
            Assert.That(value.FirstPersonEyeHeight, Is.EqualTo(1.68f));
            Assert.That(value.FirstPersonFieldOfView, Is.EqualTo(62f));
            Assert.That(value.FogEndDistance, Is.GreaterThan(value.FogStartDistance));
            Assert.That(value.PresentationOnly, Is.True);
            Assert.That(경관RenderingProfileHash.Compute(value), Has.Length.EqualTo(64));
        }

        [Test]
        public void 플레이어경관Profile은_보행과추적카메라를_표현전용으로제한한다()
        {
            var value = 평창군플레이어경관Fixture.Create();

            Assert.That(value.Validate(), Is.True);
            Assert.That(value.ProfileStableId,
                Is.EqualTo("player-profile:sim:pyeongchang:world-explorer.v2"));
            Assert.That(value.WalkSpeed, Is.EqualTo(3.6f));
            Assert.That(value.RunMultiplier, Is.EqualTo(1.7f));
            Assert.That(value.CameraDistance, Is.EqualTo(15.5f));
            Assert.That(value.CameraFieldOfView, Is.EqualTo(52f));
            Assert.That(value.TacticalPitch, Is.EqualTo(52f));
            Assert.That(value.TacticalYaw, Is.EqualTo(36f));
            Assert.That(value.TacticalMaximumDistance,
                Is.GreaterThan(value.TacticalMinimumDistance));
            Assert.That(value.TacticalPanSpeed, Is.EqualTo(10f));
            Assert.That(value.FirstPersonEyeHeight, Is.EqualTo(1.68f));
            Assert.That(value.FirstPersonFieldOfView, Is.EqualTo(64f));
            Assert.That(value.ClickMoveStopDistance, Is.EqualTo(.18f));
            var projection = 평창군법정동WorldFixture.Create();
            var boundaryPoints = projection.Nodes
                .SelectMany(node => node.BoundaryPoints)
                .ToArray();
            Assert.That(boundaryPoints.All(point =>
                point.X >= value.MinimumX && point.X <= value.MaximumX), Is.True);
            Assert.That(boundaryPoints.All(point =>
                point.Z >= value.MinimumZ && point.Z <= value.MaximumZ), Is.True);
            Assert.That(value.MinimumX, Is.LessThan(10.5f));
            Assert.That(value.MinimumZ, Is.LessThan(2.5f));
            Assert.That(value.PresentationOnly, Is.True);
        }
    }
}
