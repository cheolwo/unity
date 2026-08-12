using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.ExhibitionFixtures;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합전시관Tests
    {
        [Test]
        public void Presenter는_예행연습상태생성과_외부상태주입을_분리한다()
        {
            Assert.That(
                typeof(통합전시관Presenter).GetMethod(
                    nameof(통합전시관Presenter.Initialize),
                    new[] { typeof(통합전시관Snapshot) }),
                Is.Not.Null);
            Assert.That(
                typeof(통합전시관Presenter).GetMethod("CreateFixtureApiModel"),
                Is.Null);
        }

        [Test]
        public void Fixture는_모판_현실관측_Simulation을_분리한다()
        {
            var source = 통합전시관FixtureApiModelFactory.CreateFixtureApiModel();
            var snapshot = new 통합전시관Mapper().Map(source);

            Assert.That(snapshot.Exhibits, Has.Length.EqualTo(6));
            Assert.That(snapshot.Exhibits.Count(value =>
                value.ExperienceModeCode == 통합전시관ExperienceModeCodes.Research), Is.EqualTo(1));
            Assert.That(snapshot.Exhibits.Count(value =>
                value.ExperienceModeCode == 통합전시관ExperienceModeCodes.ReadOnly), Is.EqualTo(1));
            Assert.That(snapshot.Exhibits.Count(value =>
                value.ExperienceModeCode == 통합전시관ExperienceModeCodes.Simulation), Is.EqualTo(4));
            Assert.That(snapshot.Exhibits.Any(value =>
                value.ExperienceModeCode == 통합전시관ExperienceModeCodes.OperationalHandoff), Is.False);
        }

        [Test]
        public void EXH5는_음식주문부터_기사인계와_주문자수령을_별도권한과확정으로분리한다()
        {
            var snapshot = new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel());
            var exhibit = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == "exhibit:city:food-delivery");

            Assert.That(exhibit.WorkflowCheckpoints, Has.Length.EqualTo(8));
            Assert.That(exhibit.CanonicalRecordRelations, Has.Length.EqualTo(7));
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "DriverOffer"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.DriverCandidateApproximate
                && !value.RequiresSeparateConfirmation), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "DriverAssignment"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized
                && value.RequiresSeparateConfirmation), Is.True);
            var delivery = exhibit.WorkflowCheckpoints.Single(value => value.StateCode == "전달완료");
            var receipt = exhibit.WorkflowCheckpoints.Single(value => value.StateCode == "수령확인");
            Assert.That(delivery.CanonicalRecordStableId, Is.Not.EqualTo(receipt.CanonicalRecordStableId));
            Assert.That(delivery.RequiresSeparateConfirmation, Is.True);
            Assert.That(receipt.RequiresSeparateConfirmation, Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "CargoJourney" || value.StateMachineCode == "WarehouseHandoff"), Is.False);
            Assert.That(exhibit.AllowedInteractionIntentCodes,
                Has.None.EqualTo(통합전시관InteractionIntentCodes.DomainCommand));
        }

        [Test]
        public void EXH4는_개인의향_집단Preview_마트공개_운영재고를_공개범위별로분리한다()
        {
            var snapshot = new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel());
            var exhibit = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == "exhibit:town-city:orderer-group-urban-market");

            Assert.That(exhibit.WorkflowCheckpoints, Has.Length.EqualTo(6));
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "IndividualIntent"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.OwnerPrivate
                && value.RequiresSeparateConfirmation), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "GroupingPreview"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.PrivacySafeAggregate), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "MartPublicProduct"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.OrdererPublic), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "MarketInventory"
                && value.DisclosureScopeCode == 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized), Is.True);
            Assert.That(exhibit.CanonicalRecordRelations.Any(value =>
                value.RelationCode == "ComparedWithNotUsedAsSalePrice"), Is.True);
            Assert.That(exhibit.AllowedInteractionIntentCodes,
                Has.None.EqualTo(통합전시관InteractionIntentCodes.DomainCommand));
        }

        [Test]
        public void EXH3는_같은Cargo계보와_도착입고검수보관경계를_분리한다()
        {
            var snapshot = new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel());
            var exhibit = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == "exhibit:logistics:cargo-hub-warehouse");

            Assert.That(exhibit.CanonicalRecordRelations, Has.Length.EqualTo(5));
            Assert.That(exhibit.WorkflowCheckpoints, Has.Length.EqualTo(7));
            Assert.That(exhibit.WorkflowCheckpoints.Select(value => value.LineageStableId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "CargoJourney" && value.StateCode == "ArrivedAtHub"), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "WarehouseHandoff"
                && value.StateCode == "ArrivedAtWarehouse"
                && value.RequiresSeparateConfirmation), Is.True);
            Assert.That(exhibit.WorkflowCheckpoints.Any(value =>
                value.StateMachineCode == "WarehouseHandoff" && value.StateCode == "ReceivingCompleted"), Is.True);
            Assert.That(exhibit.AllowedInteractionIntentCodes, Has.None.EqualTo(통합전시관InteractionIntentCodes.SimulationConfirm));
        }

        [Test]
        public void 현실관측은_미수집과운영미확인을_숨기지않는다()
        {
            var snapshot = new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel());
            var observation = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == "exhibit:public-data:potato-observation");

            Assert.That(observation.DataStateCode, Is.EqualTo(통합전시관DataStateCodes.Uncollected));
            Assert.That(observation.BlockedReasonCodes, Contains.Item("ActualObservationNotCollected"));
            Assert.That(observation.Evidence.Single(value =>
                value.EvidenceKindCode == 통합전시관EvidenceKindCodes.Operational).StatusCode,
                Is.EqualTo(통합전시관EvidenceStatusCodes.Unverified));
        }

        [Test]
        public void 연구와읽기전시는_GenericConfirm을_제공하지않는다()
        {
            var snapshot = new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel());

            Assert.That(snapshot.Exhibits
                .Where(value => value.ExperienceModeCode != 통합전시관ExperienceModeCodes.Simulation)
                .SelectMany(value => value.AllowedInteractionIntentCodes),
                Has.None.EqualTo("ConfirmExhibit"));
        }
    }
}
