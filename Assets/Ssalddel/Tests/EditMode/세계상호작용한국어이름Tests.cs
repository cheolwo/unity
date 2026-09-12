using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 세계상호작용한국어이름Tests
    {
        [Test]
        public void 번호형고유식별자는_절차단계가아닌_한국어기능명으로표시된다()
        {
            Assert.That(Simulation세계상호작용이름Catalog.All.Count,
                Is.EqualTo(60));
            Assert.That(Simulation세계상호작용이름Catalog
                    .한국어표시명("WI-NATURE-07"),
                Is.EqualTo("자연 탐사·생활 거점 · 오두막을 지을 터 선정 (WI-NATURE-07)"));
            Assert.That(Simulation세계상호작용이름Catalog
                    .한국어표시명("WI-001"),
                Is.EqualTo("물류 거점 창고 · 입고 화물 검수 (WI-001)"));
            Assert.That(Simulation세계상호작용이름Catalog
                    .한국어표시명("WI-UNKNOWN-01"),
                Is.EqualTo("알 수 없는 세계 상호작용 (WI-UNKNOWN-01)"));
            Assert.That(Simulation세계상호작용이름Catalog.Find("WI-NATURE-11")!
                    .단일책임판정코드,
                Is.EqualTo("LegacyCompositeMigrationRequired"));
            Assert.That(Simulation세계상호작용이름Catalog.Find("WI-LOG-03")!
                    .단일책임판정코드,
                Is.EqualTo("ProceduralStepMigrationRequired"));
            Assert.That(오늘작업계획Presenter.한국어세계상호작용목록(new[]
                {
                    "WI-001",
                    "WI-002",
                }), Is.EqualTo(
                    "물류 거점 창고 · 입고 화물 검수 (WI-001)\n"
                    + "물류 거점 창고 · 검수 완료 화물 창고 적재 (WI-002)"));
        }

        [Test]
        public void 권위음양상태사본은_Unity에서읽기전용한국어로표시된다()
        {
            var player = SimulationWI음양주체사분면Rules.Resolve(
                "WI-FARM-04", SimulationWI수행주체Codes.PlayerActor,
                SimulationWorldInteractionTriggerSourceCodes.PlayerDriven);
            var npc = SimulationWI음양주체사분면Rules.Resolve(
                "WI-FARM-04", SimulationWI수행주체Codes.NpcActor,
                SimulationWorldInteractionTriggerSourceCodes.NpcDriven);
            var automatic = SimulationWI음양주체사분면Rules.Resolve(
                "WI-CITY-02", SimulationWI수행주체Codes.NotApplicable,
                SimulationWorldInteractionTriggerSourceCodes.WorldDerived);

            Assert.That(오늘작업계획Presenter.한국어음양수행주체분류(player),
                Is.EqualTo("++ · 양·플레이어"));
            Assert.That(오늘작업계획Presenter.한국어음양수행주체분류(npc),
                Is.EqualTo("+- · 양·NPC"));
            Assert.That(오늘작업계획Presenter.한국어음양수행주체분류(automatic),
                Is.EqualTo("사분면 적용 제외"));
        }
    }
}
