using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Survival;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 농장생존표현ProjectionTests
    {
        [Test]
        public void 서버의세개위협개체를_교체가능한VisualKey세개로만든다()
        {
            var source = new FarmSurvivalStateApiModel
            {
                SessionStableId = "simulation-session:survival",
                WorldRevision = 1,
                WorldTick = 4,
                TileKey = "kr5186:l2:438:419",
                FarmBuildingStableId = "building:sim.daegwallyeong-farmhouse",
                SimulationOnly = true,
                Encounters = new[]
                {
                    new FarmSurvivalEncounterApiModel
                    {
                        EncounterStableId = "encounter:sim:zombie:day-5",
                        ThreatTypeCode = "ZombiePressure",
                        ThreatUnitCount = 3,
                        StateCode = "Warning",
                        PresentationKey = "survival.zombie-warning",
                    },
                },
            };

            var intents = new FarmSurvivalVisualIntentMapper(
                FarmSurvivalVisualCatalog.CreateDefault()).Map(source);

            var threats = intents.Where(value =>
                value.VisualKey == FarmSurvivalVisualKeys.StylizedZombie).ToArray();
            Assert.That(threats, Has.Length.EqualTo(3));
            Assert.That(threats.All(value => value.PresentationOnly), Is.True);
            Assert.That(threats.All(value =>
                value.FallbackVisualKey ==
                FarmSurvivalVisualKeys.SkeletonThreatFallback), Is.True);
        }
    }
}
