using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;
using NUnit.Framework;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 산수몽지도Tests
    {
        [Test]
        public void 전체지도는윤곽과확정을모두보이고_미니맵은인접확정과호송로만보인다()
        {
            var snapshot = new SimulationWorldExplorationStateSnapshot
            {
                Revision = 7,
                ActorTilePositions = new[]
                {
                    new SimulationActorTilePositionSnapshot
                    {
                        ActorStableId = "actor:player",
                        CurrentL2TileKey = "kr5186:l2:701:1145",
                    },
                },
                MapKnowledgeEntries = new[]
                {
                    Entry("area:nature", SimulationMapKnowledgeKindCodes.Place,
                        SimulationMapKnowledgeLevelCodes.Confirmed,
                        "kr5186:l2:701:1145"),
                    Entry("map-place:town-outskirts",
                        SimulationMapKnowledgeKindCodes.Place,
                        SimulationMapKnowledgeLevelCodes.KnownOutline,
                        "kr5186:l2:702:1145"),
                    Entry("route:meng:hans-farm-to-hub",
                        SimulationMapKnowledgeKindCodes.Route,
                        SimulationMapKnowledgeLevelCodes.KnownOutline,
                        "kr5186:l2:700:1145", "kr5186:l2:703:1145"),
                },
            };

            var view = 산수몽지도Projection.Create(snapshot, "actor:player");

            Assert.That(view.FullMapEntries, Has.Length.EqualTo(3));
            Assert.That(view.MiniMapEntries, Has.Length.EqualTo(2));
            Assert.That(view.MiniMapEntries, Has.Some.Matches<
                SimulationMapKnowledgeEntrySnapshot>(value =>
                    value.SpatialStableId == "area:nature"));
            Assert.That(view.MiniMapEntries, Has.Some.Matches<
                SimulationMapKnowledgeEntrySnapshot>(value =>
                    value.KindCode == SimulationMapKnowledgeKindCodes.Route));
        }

        private static SimulationMapKnowledgeEntrySnapshot Entry(
            string id, string kind, string knowledge, params string[] tiles)
            => new()
            {
                SpatialStableId = id,
                KindCode = kind,
                KoreanLabel = id,
                KnowledgeLevelCode = knowledge,
                RelatedL2TileKeys = tiles,
            };
    }
}
