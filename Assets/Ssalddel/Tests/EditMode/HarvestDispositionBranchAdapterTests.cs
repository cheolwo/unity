using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Farm;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class HarvestDispositionBranchAdapterTests
    {
        [TestCase(HarvestDispositionChoiceCodes.CooperativeShipment,
            HarvestDispositionWorkflowCodes.CooperativeIntakeCandidate)]
        [TestCase(HarvestDispositionChoiceCodes.DirectOnlineSale,
            HarvestDispositionWorkflowCodes.ProducerPackingCandidate)]
        [TestCase(HarvestDispositionChoiceCodes.ExportAgent,
            HarvestDispositionWorkflowCodes.ExportReadinessCandidate)]
        [TestCase(HarvestDispositionChoiceCodes.ReserveStorage,
            HarvestDispositionWorkflowCodes.ReserveStockLotCandidate)]
        public void AllBranchesPreserveDecisionLotAndTaskLineage(string choiceCode, string workflowCode)
        {
            var decided = Decide(choiceCode);

            var envelope = Adapter().CreatePreviewEnvelope(decided, "actor:producer.fixture");

            Assert.That(envelope.PreviewRequest.DispositionDecisionStableId,
                Is.EqualTo(decided.Decision!.StableId));
            Assert.That(envelope.PreviewRequest.HarvestLotStableId,
                Is.EqualTo(decided.HarvestLot.StableId));
            Assert.That(envelope.PreviewRequest.ChoiceCode, Is.EqualTo(choiceCode));
            Assert.That(envelope.PreviewRequest.NextWorkflowCode, Is.EqualTo(workflowCode));
            Assert.That(envelope.TaskCandidate.CandidateTaskStableId,
                Is.EqualTo("task:harvest-impact:" + decided.Decision.StableId));
            Assert.That(envelope.TaskCandidate.TaskTypeCode, Is.EqualTo(choiceCode + "Work"));
            Assert.That(envelope.TaskCandidate.InputLotStableIds,
                Is.EqualTo(new[] { decided.HarvestLot.StableId }));
            Assert.That(envelope.TaskCandidate.OutputCandidateCodes,
                Is.EqualTo(new[] { workflowCode }));
        }

        [Test]
        public void EnvelopeRequiresServerPreviewAndDoesNotClaimWorldEffects()
        {
            var decided = Decide(HarvestDispositionChoiceCodes.ReserveStorage);

            var envelope = Adapter().CreatePreviewEnvelope(decided, "actor:producer.fixture");

            Assert.That(envelope.RequiresServerPreview, Is.True);
            Assert.That(envelope.RequiresExplicitConfirmation, Is.True);
            Assert.That(envelope.ServerMustRecalculatePolicy, Is.True);
            Assert.That(envelope.DoesNotApplySettlementState, Is.True);
            Assert.That(envelope.DoesNotCreateCargoOrSale, Is.True);
            Assert.That(envelope.PreviewRequest.SourceStableIds,
                Does.Contain(decided.HarvestLot.StableId));
            Assert.That(envelope.PreviewRequest.SourceStableIds,
                Does.Contain(decided.Decision!.StableId));
        }

        [Test]
        public void UndecidedInvalidActorAndCanonicalWorkflowMismatchAreRejected()
        {
            Assert.That(() => Adapter().CreatePreviewEnvelope(Snapshot(), "actor:producer.fixture"),
                Throws.InvalidOperationException.With.Message.EqualTo("HarvestDispositionDecisionRequired"));

            var decided = Decide(HarvestDispositionChoiceCodes.CooperativeShipment);
            Assert.That(() => Adapter().CreatePreviewEnvelope(decided, "producer fixture"),
                Throws.InvalidOperationException.With.Message.EqualTo("HarvestDispositionActorStableIdInvalid"));

            decided.Decision!.NextWorkflowCode = HarvestDispositionWorkflowCodes.ProducerPackingCandidate;
            decided.Options.Single(value =>
                value.ChoiceCode == HarvestDispositionChoiceCodes.CooperativeShipment).NextWorkflowCode
                = HarvestDispositionWorkflowCodes.ProducerPackingCandidate;
            Assert.That(() => Adapter().CreatePreviewEnvelope(decided, "actor:producer.fixture"),
                Throws.InvalidOperationException.With.Message.EqualTo("HarvestDispositionWorkflowMismatch"));

            decided = Decide(HarvestDispositionChoiceCodes.CooperativeShipment);
            decided.Decision!.SourceStableIds = decided.Decision.SourceStableIds
                .Concat(new[] { "not a stable id" }).ToArray();
            Assert.That(() => Adapter().CreatePreviewEnvelope(decided, "actor:producer.fixture"),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "HarvestDispositionSourceStableIdsInvalid"));
        }

        private static HarvestDispositionBranchAdapter Adapter()
            => new HarvestDispositionBranchAdapter(new HarvestDispositionSimulationValidator());

        private static HarvestDispositionSimulationSnapshot Decide(string choiceCode)
        {
            var snapshot = Snapshot();
            var engine = new HarvestDispositionSimulationEngine(new HarvestDispositionSimulationValidator());
            return engine.Tick(snapshot, engine.Confirm(snapshot, engine.Preview(snapshot, choiceCode)));
        }

        private static HarvestDispositionSimulationSnapshot Snapshot()
            => HarvestDispositionSimulationFixture.Create(Harvested());

        private static 감자재배LifecycleSimulationSnapshot Harvested()
        {
            var validator = new 감자재배LifecycleSimulationValidator(
                new FarmSoilTileSimulationValidator(), new 재배달력ProfileValidator());
            var engine = new 감자재배LifecycleSimulationEngine(validator);
            var source = 감자재배LifecycleSimulationFixture.Create();
            var tile = source.Soil.Tiles.First(value =>
                value.CultivationStateCode == FarmSoilTileCultivationStateCodes.Tilled);
            source = engine.Tick(source,
                engine.Confirm(source, engine.PreviewSowing(source, tile.StableId)));
            source = engine.Tick(source, engine.CreateAdvanceDaysCommand(source, 6));
            return engine.Tick(source, engine.Confirm(source, engine.PreviewHarvest(source)));
        }
    }
}
