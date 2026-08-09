using System;
using NUnit.Framework;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Samples.UrbanLogisticsCenter.Editor;
using Ssalddel.Unity.Transport;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter.Tests.Editor
{
    public sealed class LogisticsFacilityOverviewViewTests
    {
        [SetUp]
        public void SetUp()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void 물류센터View는_건물과4개업무영역VisualRoot를제공한다()
        {
            var view = 도심물류센터PrimitiveSceneBuilder.CreateFacilityOverviewForTests();

            Assert.That(view.ValidateWiring(), Is.True);
            Assert.That(view.AreaCount, Is.EqualTo(4));
            Assert.That(view.BuildingVisualRoot.name, Is.EqualTo("WarehouseBuildingVisualRoot"));
            Assert.That(view.CargoVisualRoot.name, Is.EqualTo("FacilityCargoVisualRoot"));
        }

        [Test]
        public void 운송중Handoff는_차량접근영역에화물을표시한다()
        {
            var view = 도심물류센터PrimitiveSceneBuilder.CreateFacilityOverviewForTests();
            var model = new LogisticsFacilityOverviewProjector().Project(Handoff())!;

            view.Apply(model);

            Assert.That(view.CargoVisualRoot.activeSelf, Is.True);
            Assert.That(view.CargoVisualRoot.transform.position.x, Is.EqualTo(-6.6f).Within(.01f));
        }

        [Test]
        public void 활성Handoff가없으면_화물을숨긴다()
        {
            var view = 도심물류센터PrimitiveSceneBuilder.CreateFacilityOverviewForTests();

            view.Apply(null);

            Assert.That(view.CargoVisualRoot.activeSelf, Is.False);
            Assert.That(view.BuildingVisualRoot.activeSelf, Is.True);
        }

        private static CargoWarehouseHandoffSnapshot Handoff()
            => new CargoWarehouseHandoffSnapshot
            {
                StableId = "cargo-handoff:transport-71.inbound-91",
                Revision = 1,
                HandoffStateCode = CargoHandoffStateCodes.InTransit,
                CargoStableId = "cargo:transport-71",
                TransportTaskStableId = "transport-task:71",
                InboundTaskStableId = "inbound-task:91",
                GeneratedAt = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
            };
    }
}
