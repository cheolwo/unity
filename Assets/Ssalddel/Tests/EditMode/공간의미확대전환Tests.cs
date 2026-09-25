using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간의미확대전환Tests
    {
        [Test]
        public void 거점지형부터_권역_행정동_역세권순서로내려간다()
        {
            var flow = new 공간의미확대전환흐름();

            flow.SetTerrainHubLevel(true);
            Assert.That(flow.CurrentStage, Is.EqualTo(공간의미확대전환단계.Region));
            Assert.That(flow.CurrentStableId,
                Is.EqualTo(사가정저밀도교통Fixture.RegionStableId));
            Assert.That(flow.TryAdvance(), Is.True);
            Assert.That(flow.CurrentStage,
                Is.EqualTo(공간의미확대전환단계.AdministrativeArea));
            Assert.That(flow.CurrentStableId,
                Is.EqualTo(사가정저밀도교통Fixture.AdministrativeAreaStableId));
            Assert.That(flow.TryAdvance(), Is.True);
            Assert.That(flow.CurrentStage,
                Is.EqualTo(공간의미확대전환단계.StationArea));
            Assert.That(flow.CurrentStableId,
                Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
            Assert.That(flow.CanOpenPreparedDiorama, Is.True);
        }

        [Test]
        public void 행정동단계는_현재공개경계가아닌과거후보임을드러낸다()
        {
            var flow = new 공간의미확대전환흐름();
            flow.SetTerrainHubLevel(true);
            flow.TryAdvance();

            Assert.That(flow.DiagnosticCode,
                Is.EqualTo("HistoricalBoundaryCandidate_CurrentPublicationBlocked"));
            Assert.That(flow.TruthBoundary, Does.Contain("과거 경계 후보"));
            Assert.That(flow.TruthBoundary, Does.Contain("보류"));
        }

        [Test]
        public void 준비된역세권에서만_디오라마를열고_상위지형이되면초기화한다()
        {
            var flow = new 공간의미확대전환흐름();
            flow.SetTerrainHubLevel(true);
            Assert.That(flow.MarkLocalDioramaOpened(), Is.False);

            flow.TryAdvance();
            flow.TryAdvance();
            Assert.That(flow.MarkLocalDioramaOpened(), Is.True);
            Assert.That(flow.CurrentStage,
                Is.EqualTo(공간의미확대전환단계.LocalDiorama));

            flow.RestoreStationArea();
            Assert.That(flow.CurrentStage,
                Is.EqualTo(공간의미확대전환단계.StationArea));
            flow.SetTerrainHubLevel(false);
            Assert.That(flow.CurrentStage, Is.EqualTo(공간의미확대전환단계.Hidden));
            Assert.That(flow.CurrentStableId, Is.Empty);
        }
    }
}
