using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "r46 생활 관찰의 OS별 표현 숨김·해제·최신 사본 재진입과 만료를 EditMode에서 검증한다.",
        Boundary = "순수 표현 수명 시험이며 실제 서버 연결, Play Mode, Game View 또는 E 승격 근거가 아니다.")]
    public sealed class 생활관찰표현Tests
    {
        private const string 음식Id = "living-observation:food";
        private const string 화물Id = "living-observation:cargo";
        private const string 음식Os = OperationalWorldOperatingSystemIds.FoodDelivery;
        private const string 화물Os = OperationalWorldOperatingSystemIds.DomesticCargoTransport;
        private GameObject root;
        private 운영관찰WorldView world;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Resources.Load<TextAsset>("SagajeongReference"), Is.Not.Null);
            Assert.That(Resources.Load<TextAsset>("SagajeongObservationPlacement"), Is.Not.Null);
            now = DateTime.UtcNow;
            root = new GameObject("living-observation-presentation-r46-test");
            world = root.AddComponent<운영관찰WorldView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void 음식표현을숨겨도_객체를보존하고_최신판본을수용하며_다른Os는계속보인다()
        {
            적용(표본Plan(1));
            var food = 객체(음식Id);
            var cargo = 객체(화물Id);

            world.SetOperatingSystemPresentation(음식Os, visible: false, retain: true);
            적용(표본Plan(7, 다중Os생명주기StageCodes.FoodPickup));

            Assert.That(world.ObjectCount, Is.EqualTo(2));
            Assert.That(객체(음식Id), Is.SameAs(food));
            Assert.That(food.gameObject.activeSelf, Is.False);
            Assert.That(food.Revision, Is.EqualTo(7));
            Assert.That(객체(화물Id), Is.SameAs(cargo));
            Assert.That(cargo.gameObject.activeSelf, Is.True);

            world.SetOperatingSystemPresentation(음식Os, visible: true, retain: true);

            Assert.That(객체(음식Id), Is.SameAs(food));
            Assert.That(food.gameObject.activeSelf, Is.True);
            Assert.That(food.LifecycleStageCode, Is.EqualTo(다중Os생명주기StageCodes.FoodPickup));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void 전체표시와Os표시는_둘다허용될때만_갱신전후객체를표시한다(
            bool globalVisible, bool foodVisible)
        {
            적용(표본Plan(1));
            world.SetPresentationVisible(globalVisible);
            world.SetOperatingSystemPresentation(음식Os, foodVisible, retain: true);
            Assert.That(객체(음식Id).gameObject.activeSelf,
                Is.EqualTo(globalVisible && foodVisible));

            적용(표본Plan(2));
            // 전체 토글이 개별 OS의 비표시를 다시 켜지 않는지도 확인합니다.
            world.SetPresentationVisible(!globalVisible);
            world.SetPresentationVisible(globalVisible);

            Assert.That(world.ObjectCount, Is.EqualTo(2));
            Assert.That(객체(음식Id).Revision, Is.EqualTo(2));
            Assert.That(객체(음식Id).gameObject.activeSelf,
                Is.EqualTo(globalVisible && foodVisible));
            Assert.That(객체(화물Id).gameObject.activeSelf, Is.EqualTo(globalVisible));
        }

        [Test]
        public void 음식표현해제뒤_전체Plan이반복되어도재생성하지않고_재진입때최신판본만조립한다()
        {
            적용(표본Plan(1));
            var food = 객체(음식Id);
            var ownedMaterial = food.GetComponent<Renderer>().sharedMaterial;
            var cargo = 객체(화물Id);

            world.SetOperatingSystemPresentation(음식Os, visible: false, retain: false);

            // UnityEngine.Object의 파기 여부는 관리 참조의 null 여부와 구별합니다.
            Assert.That(food == null, Is.True);
            Assert.That(ownedMaterial == null, Is.True);
            for (var revision = 2; revision <= 4; revision++)
            {
                적용(표본Plan(revision, 다중Os생명주기StageCodes.FoodPickup));
                Assert.That(world.ObjectCount, Is.EqualTo(1));
                Assert.That(world.Objects.Single(), Is.SameAs(cargo));
                Assert.That(root.GetComponentsInChildren<운영관찰ObjectView>(true).Length,
                    Is.EqualTo(1));
            }

            world.SetOperatingSystemPresentation(음식Os, visible: true, retain: true);
            Assert.That(world.ObjectCount, Is.EqualTo(1), "표시 요청만으로 과거 사본을 재생성하지 않습니다.");
            적용(표본Plan(9, 다중Os생명주기StageCodes.FoodDelivery));

            var returned = 객체(음식Id);
            Assert.That(ReferenceEquals(returned, food), Is.False);
            Assert.That(returned.Revision, Is.EqualTo(9));
            Assert.That(returned.LifecycleStageCode, Is.EqualTo(다중Os생명주기StageCodes.FoodDelivery));
            Assert.That(returned.gameObject.activeSelf, Is.True);
            Assert.That(객체(화물Id), Is.SameAs(cargo));
            Assert.That(world.ObjectCount, Is.EqualTo(2));
        }

        [Test]
        public void 생명주기표식을숨겨도_같은표식과상태사본을보존하고_최신판본을수용한다()
        {
            var layer = 생명주기Layer();
            적용(표본Plan(1), layer);
            var food = 표식(layer, 음식Id);
            var cargo = 표식(layer, 화물Id);

            layer.SetOperatingSystemPresentation(음식Os, visible: false, retain: true);
            적용(표본Plan(6, 다중Os생명주기StageCodes.FoodPickup), layer);

            Assert.That(layer.StateCount, Is.EqualTo(2));
            Assert.That(layer.MarkerCount, Is.EqualTo(2));
            Assert.That(표식(layer, 음식Id), Is.SameAs(food));
            Assert.That(food.gameObject.activeSelf, Is.False);
            Assert.That(food.Revision, Is.EqualTo(6));
            Assert.That(layer.CurrentStates.Single(value => value.ObjectStableId == 음식Id).Revision,
                Is.EqualTo(6));
            Assert.That(표식(layer, 화물Id), Is.SameAs(cargo));
            Assert.That(cargo.gameObject.activeSelf, Is.True);

            layer.SetOperatingSystemPresentation(음식Os, visible: true, retain: true);
            Assert.That(food.gameObject.activeSelf, Is.True);
            Assert.That(food.LifecycleStageCode, Is.EqualTo(다중Os생명주기StageCodes.FoodPickup));
        }

        [Test]
        public void 생명주기상태는_표현해제중에도남고_다른Os보존과최신판본재진입을지원한다()
        {
            var layer = 생명주기Layer();
            적용(표본Plan(1), layer);
            var food = 표식(layer, 음식Id);
            var cargo = 표식(layer, 화물Id);
            world.SetOperatingSystemPresentation(음식Os, visible: false, retain: false);
            layer.SetOperatingSystemPresentation(음식Os, visible: false, retain: false);

            Assert.That(food == null, Is.True);
            Assert.That(layer.StateCount, Is.EqualTo(2));
            for (var revision = 2; revision <= 4; revision++)
            {
                적용(표본Plan(revision, 다중Os생명주기StageCodes.FoodPickup), layer);
                Assert.That(layer.StateCount, Is.EqualTo(2));
                Assert.That(layer.MarkerCount, Is.EqualTo(1));
                Assert.That(layer.CurrentStates.Single(value => value.ObjectStableId == 음식Id).Revision,
                    Is.EqualTo(revision));
                Assert.That(표식(layer, 화물Id), Is.SameAs(cargo));
                Assert.That(layer.RuntimeRoot.GetComponentsInChildren<다중Os생명주기MarkerView>(true).Length,
                    Is.EqualTo(1));
                Assert.That(layer.Diagnostics, Is.Empty);
            }

            world.SetOperatingSystemPresentation(음식Os, visible: true, retain: true);
            layer.SetOperatingSystemPresentation(음식Os, visible: true, retain: true);
            적용(표본Plan(11, 다중Os생명주기StageCodes.FoodDelivery), layer);

            var returned = 표식(layer, 음식Id);
            Assert.That(ReferenceEquals(returned, food), Is.False);
            Assert.That(returned.Revision, Is.EqualTo(11));
            Assert.That(returned.LifecycleStageCode, Is.EqualTo(다중Os생명주기StageCodes.FoodDelivery));
            Assert.That(returned.gameObject.activeSelf, Is.True);
            Assert.That(layer.StateCount, Is.EqualTo(2));
            Assert.That(layer.MarkerCount, Is.EqualTo(2));
            Assert.That(표식(layer, 화물Id), Is.SameAs(cargo));
        }

        [Test]
        public void 만료와Clear는_숨기거나해제한상태사본도제거하고_남은표현자원을정리한다()
        {
            var layer = 생명주기Layer();
            적용(표본Plan(1), layer);
            var food = 표식(layer, 음식Id);
            var cargo = 표식(layer, 화물Id);
            layer.SetOperatingSystemPresentation(음식Os, visible: false, retain: true);

            layer.Expire(now.AddMinutes(10));
            world.Expire(now.AddMinutes(10));

            Assert.That(food == null, Is.True);
            Assert.That(layer.CurrentStates.Single().ObjectStableId, Is.EqualTo(화물Id));
            Assert.That(layer.MarkerCount, Is.EqualTo(1));
            Assert.That(표식(layer, 화물Id), Is.SameAs(cargo));
            Assert.That(world.Objects.Single().ObjectStableId, Is.EqualTo(화물Id));

            layer.SetOperatingSystemPresentation(화물Os, visible: false, retain: false);
            Assert.That(cargo == null, Is.True);
            Assert.That(layer.StateCount, Is.EqualTo(1));
            layer.Expire(now.AddMinutes(20));
            Assert.That(layer.CurrentStates, Is.Empty);
            Assert.That(layer.MarkerCount, Is.Zero);

            layer.SetOperatingSystemPresentation(화물Os, visible: true, retain: true);
            적용(표본Plan(2), layer);
            var runtimeRoot = layer.RuntimeRoot;
            var remainingMarkers = runtimeRoot.GetComponentsInChildren<다중Os생명주기MarkerView>(true);
            layer.Clear();
            world.Clear();

            Assert.That(layer.CurrentStates, Is.Empty);
            Assert.That(layer.MarkerCount, Is.Zero);
            Assert.That(layer.Diagnostics, Is.Empty);
            Assert.That(layer.RuntimeRoot == null, Is.True);
            Assert.That(runtimeRoot == null, Is.True);
            Assert.That(remainingMarkers.All(value => value == null), Is.True);
            Assert.That(world.ObjectCount, Is.Zero);
            Assert.That(root.GetComponentsInChildren<운영관찰ObjectView>(true), Is.Empty);
        }

        [Test]
        public void 조정기는_동과구에서상세만해제하고_입력복제와다른Os를유지하며_최신판본으로재진입한다()
        {
            var lifecycle = 생명주기Layer();
            var coordinator = 관찰조정Layer(lifecycle);
            Assert.That(coordinator.Apply(표본Plan(1),
                Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now), Is.True);
            var cargo = 객체(화물Id);
            var cargoMarker = 표식(lifecycle, 화물Id);
            Assert.That(coordinator.WorkCount, Is.EqualTo(1));
            Assert.That(world.ObjectCount, Is.EqualTo(1));

            coordinator.RequestStage(생활관찰단계.Street, 0, moveCamera: false);
            coordinator.Advance(.49, now.AddSeconds(.49));
            Assert.That(coordinator.DetailVisible, Is.False);
            coordinator.Advance(.5, now.AddSeconds(.5));
            var food = 객체(음식Id);
            Assert.That(coordinator.DetailVisible, Is.True);

            coordinator.RequestStage(생활관찰단계.Neighborhood, 1, moveCamera: false);
            coordinator.Advance(1.5, now.AddSeconds(1.5));
            Assert.That(coordinator.DetailVisible, Is.False);
            Assert.That(coordinator.DetailRetained, Is.True);
            Assert.That(food.gameObject.activeSelf, Is.False);
            coordinator.RequestStage(생활관찰단계.District, 2, moveCamera: false);
            coordinator.Advance(2.5, now.AddSeconds(2.5));
            coordinator.Advance(6.49, now.AddSeconds(6.49));
            Assert.That(coordinator.DetailRetained, Is.True);
            coordinator.Advance(6.5, now.AddSeconds(6.5));
            Assert.That(coordinator.CurrentStage, Is.EqualTo(생활관찰단계.District));
            Assert.That(coordinator.DetailRetained, Is.False);
            Assert.That(food == null, Is.True);

            var latest = 표본Plan(8, 다중Os생명주기StageCodes.FoodPickup);
            Assert.That(coordinator.Apply(latest,
                Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now.AddSeconds(7)), Is.True);
            latest.Instructions[0].Revision = 99;
            latest.Instructions[0].LifecycleStageCode = "mutated-input";
            latest.Instructions[1].Revision = 99;
            latest.Instructions[1].SemanticPlaceStableId = "mutated-input";
            coordinator.CurrentItems.Single().Revision = 88;

            Assert.That(coordinator.CurrentItems.Single().Revision, Is.EqualTo(8));
            Assert.That(lifecycle.CurrentStates.Single(value => value.ObjectStableId == 음식Id).Revision,
                Is.EqualTo(8));
            Assert.That(world.Objects.Single(), Is.SameAs(cargo));
            coordinator.RequestStage(생활관찰단계.Street, 7, moveCamera: false);
            coordinator.Advance(7.5, now.AddSeconds(7.5));

            Assert.That(coordinator.DetailVisible, Is.True);
            Assert.That(coordinator.DetailRetained, Is.True);
            Assert.That(coordinator.WorkCount, Is.EqualTo(1));
            Assert.That(객체(음식Id).Revision, Is.EqualTo(8));
            Assert.That(객체(음식Id).LifecycleStageCode, Is.EqualTo(다중Os생명주기StageCodes.FoodPickup));
            Assert.That(표식(lifecycle, 음식Id).Revision, Is.EqualTo(8));
            Assert.That(객체(화물Id), Is.SameAs(cargo));
            Assert.That(cargo.Revision, Is.EqualTo(1));
            Assert.That(표식(lifecycle, 화물Id), Is.SameAs(cargoMarker));
            Assert.That(world.UnplacedCount, Is.Zero);
        }

        [Test]
        public void 조정기의TTL은_업무사본과두표현을함께만료시키고_다른Os는자신의TTL까지유지한다()
        {
            var lifecycle = 생명주기Layer();
            var coordinator = 관찰조정Layer(lifecycle);
            coordinator.Apply(표본Plan(1), Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now);
            coordinator.RequestStage(생활관찰단계.Street, 0, moveCamera: false);
            coordinator.Advance(.5, now.AddSeconds(.5));
            var food = 객체(음식Id);
            var foodMarker = 표식(lifecycle, 음식Id);
            var cargo = 객체(화물Id);

            coordinator.Advance(600, now.AddMinutes(10));

            Assert.That(coordinator.WorkCount, Is.Zero);
            Assert.That(coordinator.CurrentItems, Is.Empty);
            Assert.That(food == null, Is.True);
            Assert.That(foodMarker == null, Is.True);
            Assert.That(world.Objects.Single(), Is.SameAs(cargo));
            Assert.That(lifecycle.CurrentStates.Single().ObjectStableId, Is.EqualTo(화물Id));
            Assert.That(lifecycle.MarkerCount, Is.EqualTo(1));

            coordinator.Advance(1200, now.AddMinutes(20));

            Assert.That(cargo == null, Is.True);
            Assert.That(world.ObjectCount, Is.Zero);
            Assert.That(lifecycle.CurrentStates, Is.Empty);
            Assert.That(lifecycle.MarkerCount, Is.Zero);
        }

        [Test]
        public void 조정기Clear는_음식사본과표현만제거하고_다른Os를지우지않는다()
        {
            var lifecycle = 생명주기Layer();
            var coordinator = 관찰조정Layer(lifecycle);
            coordinator.Apply(표본Plan(1), Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now);
            coordinator.RequestStage(생활관찰단계.Street, 0, moveCamera: false);
            coordinator.Advance(.5, now.AddSeconds(.5));
            var food = 객체(음식Id);
            var foodMarker = 표식(lifecycle, 음식Id);
            var cargo = 객체(화물Id);
            var cargoMarker = 표식(lifecycle, 화물Id);

            coordinator.Clear();
            coordinator.Clear();

            Assert.That(coordinator.CurrentItems, Is.Empty);
            Assert.That(coordinator.WorkCount, Is.Zero);
            Assert.That(coordinator.DetailVisible, Is.False);
            Assert.That(coordinator.DetailRetained, Is.False);
            Assert.That(food == null, Is.True);
            Assert.That(foodMarker == null, Is.True);
            Assert.That(world.Objects.Single(), Is.SameAs(cargo));
            Assert.That(lifecycle.CurrentStates.Single().ObjectStableId, Is.EqualTo(화물Id));
            Assert.That(lifecycle.MarkerCount, Is.EqualTo(1));
            Assert.That(표식(lifecycle, 화물Id), Is.SameAs(cargoMarker));
            Assert.That(cargo.gameObject.activeSelf, Is.True);
            Assert.That(cargoMarker.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void 조정기Clear뒤_새Plan없이거리로복귀해도_기존다른Os를빈사본으로삭제하지않는다()
        {
            var lifecycle = 생명주기Layer();
            var coordinator = 관찰조정Layer(lifecycle);
            coordinator.Apply(표본Plan(1), Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now);
            coordinator.RequestStage(생활관찰단계.Street, 0, moveCamera: false);
            coordinator.Advance(.5, now.AddSeconds(.5));
            var cargo = 객체(화물Id);
            var cargoMarker = 표식(lifecycle, 화물Id);

            coordinator.Clear();
            coordinator.RequestStage(생활관찰단계.Street, 1, moveCamera: false);
            coordinator.Advance(1.5, now.AddSeconds(1.5));

            Assert.That(coordinator.CurrentStage, Is.EqualTo(생활관찰단계.Street));
            Assert.That(coordinator.CurrentItems, Is.Empty);
            Assert.That(coordinator.WorkCount, Is.Zero);
            Assert.That(world.Objects.Single(), Is.SameAs(cargo));
            Assert.That(lifecycle.CurrentStates.Single().ObjectStableId, Is.EqualTo(화물Id));
            Assert.That(lifecycle.MarkerCount, Is.EqualTo(1));
            Assert.That(표식(lifecycle, 화물Id), Is.SameAs(cargoMarker));
            Assert.That(cargo.gameObject.activeSelf, Is.True);
            Assert.That(cargoMarker.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void 다른지역음식입력은_진단과함께거절하고_기존사본과표현을원래TTL까지보존한다()
        {
            var lifecycle = 생명주기Layer();
            var coordinator = 관찰조정Layer(lifecycle);
            Assert.That(coordinator.Apply(표본Plan(1),
                Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now), Is.True);
            coordinator.RequestStage(생활관찰단계.Street, 0, moveCamera: false);
            coordinator.Advance(.5, now.AddSeconds(.5));
            var food = 객체(음식Id);
            var foodMarker = 표식(lifecycle, 음식Id);
            var cargo = 객체(화물Id);
            var mismatched = 표본Plan(7, 다중Os생명주기StageCodes.FoodDelivery);
            mismatched.Instructions[0].AreaStableId = "region:kr:bjd:1126010200";
            mismatched.Instructions[0].ExpiresAtUtc = now.AddHours(1);

            Assert.That(coordinator.Apply(mismatched,
                Array.Empty<OperationalWorldSceneApplyDiagnostic>(), now.AddSeconds(1)), Is.False);

            Assert.That(coordinator.DiagnosticCode, Is.EqualTo("LivingObservationAreaMismatch"));
            Assert.That(coordinator.WorkCount, Is.EqualTo(1));
            Assert.That(coordinator.CurrentItems.Single().AreaStableId,
                Is.EqualTo("region:kr:bjd:1126010100"));
            Assert.That(coordinator.CurrentItems.Single().Revision, Is.EqualTo(1));
            Assert.That(coordinator.CurrentItems.Single().ExpiresAtUtc, Is.EqualTo(now.AddMinutes(10)));
            Assert.That(객체(음식Id), Is.SameAs(food));
            Assert.That(표식(lifecycle, 음식Id), Is.SameAs(foodMarker));
            Assert.That(food.Revision, Is.EqualTo(1));
            Assert.That(foodMarker.Revision, Is.EqualTo(1));
            coordinator.Advance(599.99, now.AddSeconds(599.99));
            Assert.That(coordinator.WorkCount, Is.EqualTo(1));
            Assert.That(객체(음식Id), Is.SameAs(food));

            coordinator.Advance(600, now.AddMinutes(10));

            Assert.That(coordinator.CurrentItems, Is.Empty);
            Assert.That(coordinator.WorkCount, Is.Zero);
            Assert.That(food == null, Is.True);
            Assert.That(foodMarker == null, Is.True);
            Assert.That(world.Objects.Single(), Is.SameAs(cargo));
            Assert.That(lifecycle.CurrentStates.Single().ObjectStableId, Is.EqualTo(화물Id));
        }

        private 생활관찰표현Layer 관찰조정Layer(다중Os생명주기검증Layer lifecycle)
        {
            var coordinator = root.AddComponent<생활관찰표현Layer>();
            coordinator.Configure(world, lifecycle, null, "region:kr:bjd:1126010100");
            return coordinator;
        }

        private 다중Os생명주기검증Layer 생명주기Layer()
        {
            var layer = root.AddComponent<다중Os생명주기검증Layer>();
            layer.Configure(world);
            return layer;
        }

        private void 적용(OperationalWorldPlacementPlan plan, 다중Os생명주기검증Layer layer = null)
        {
            world.Apply(plan, Array.Empty<OperationalWorldSceneApplyDiagnostic>());
            Assert.That(world.PlacementStatus, Is.EqualTo("Ready"));
            Assert.That(world.UnplacedCount, Is.Zero);
            layer?.Apply(plan);
        }

        private 운영관찰ObjectView 객체(string id)
            => world.Objects.Single(value => value.ObjectStableId == id);

        private static 다중Os생명주기MarkerView 표식(다중Os생명주기검증Layer layer, string id)
            => layer.RuntimeRoot.GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                .Single(value => value.ObjectStableId == id);

        private OperationalWorldPlacementPlan 표본Plan(long foodRevision,
            string foodStage = 다중Os생명주기StageCodes.FoodCooking)
            => new()
            {
                Accepted = true,
                Instructions = new[]
                {
                    표본Instruction(음식Id, 음식Os, "synthetic-place:food-route-a",
                        foodStage, foodRevision, now.AddMinutes(10)),
                    표본Instruction(화물Id, 화물Os, "synthetic-place:cargo-route-a",
                        다중Os생명주기StageCodes.CargoTransport, 1, now.AddMinutes(20))
                }
            };

        private static OperationalWorldPlacementInstruction 표본Instruction(string id, string os,
            string place, string stage, long revision, DateTime expiresAtUtc)
            => new()
            {
                ObjectStableId = id,
                AreaStableId = "region:kr:bjd:1126010100",
                OperatingSystemId = os,
                AnchorKey = os == 음식Os ? "world.area.food-delivery" : "world.area.cargo",
                VisualKey = os == 음식Os ? "operational.completed-lifecycle" : "cargo.handoff",
                ActivityCode = stage,
                WorkStableId = "work:" + id,
                LifecycleStageCode = stage,
                AttentionStateCode = OperationalWorldAttentionStateCodes.Active,
                ObjectKindCode = os == 음식Os ? "FoodDelivery" : "CargoTransport",
                SemanticPlaceStableId = place,
                SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                ScenarioRunStableId = "observable-operations-run:living-presentation-r46",
                Revision = revision,
                ExpiresAtUtc = expiresAtUtc
            };
    }
}
