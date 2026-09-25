using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 다중Os생명주기검증Tests
    {
        private static readonly DateTime Epoch =
            new(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);

        [TestCase("주문대기", "주문 접수 대기", "Waiting")]
        [TestCase("조리중", "조리 중", "Working")]
        [TestCase("픽업대기", "조리 완료·픽업 대기", "Handoff")]
        [TestCase("기사배정", "기사 배정", "Waiting")]
        [TestCase("픽업완료", "픽업 완료·배송 중", "Moving")]
        [TestCase("전달완료", "전달 완료·수령 확인 대기", "Handoff")]
        public void 운영음식단계는_원문판본을보존하고_수령완료를예측하지않는다(string code, string label, string cue)
        {
            var instruction = new OperationalWorldPlacementInstruction
            {
                ObjectStableId = "food:active", WorkStableId = "work:active", Revision = 3,
                OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                SourceKindCode = OperationalWorldSceneSourceKinds.OperationalProjection,
                LifecycleStageCode = code, ActivityCode = code,
                SemanticPlaceStableId = "semantic-place:area:food-restaurant",
                AttentionStateCode = OperationalWorldAttentionStateCodes.Active
            };
            var plan = new OperationalWorldPlacementPlan { Accepted = true, Instructions = new[] { instruction } };
            var result = new 다중Os생명주기표현Projector().Create(plan);
            Assert.That(result.Diagnostics, Is.Empty);
            var state = result.States.Single();
            Assert.That(state.LifecycleStageCode, Is.EqualTo(code));
            Assert.That(state.LifecycleStageLabel, Is.EqualTo(label));
            Assert.That(state.CueCode, Is.EqualTo(cue));
            Assert.That(state.Revision, Is.EqualTo(3));
            Assert.That(state.Progress01, Is.LessThan(1f));
            Assert.That(state.AttentionStateCode, Is.EqualTo(OperationalWorldAttentionStateCodes.Active));
            instruction.OperatingSystemId = OperationalWorldOperatingSystemIds.DomesticCargoTransport;
            Assert.That(new 다중Os생명주기표현Projector().Create(plan).States, Is.Empty);
        }

        [Test]
        public void 동결표현타임라인은_서버R4의_여덟사례전단계를그대로사용한다()
        {
            AssertCase("food-normal",
                "5|food.order|OrderRequested|food-customer-a|A",
                "15|food.restaurant-response|RestaurantAccepted|food-restaurant-a|A",
                "25|food.cooking|Cooking|food-restaurant-a|A",
                "35|food.cooking|ReadyForPickup|food-restaurant-a|A",
                "45|food.dispatch|DriverAssigned|food-driver-wait-a|A",
                "55|food.pickup|PickedUp|food-restaurant-a|A",
                "65|food.delivery|Delivered|food-customer-a|A",
                "75|food.delivery|ReceiptConfirmed|food-customer-a|A",
                "85|food.delivery|DriverReturned|food-driver-wait-a|C");
            AssertCase("food-recovery",
                "20|food.order|OrderRequested|food-customer-b|A",
                "30|food.restaurant-response|RestaurantAccepted|food-restaurant-b|A",
                "40|food.cooking|Cooking|food-restaurant-b|A",
                "50|food.cooking|ReadyForPickup|food-restaurant-b|A",
                "60|food.dispatch|DriverAssigned|food-driver-wait-b|A",
                "70|food.pickup|PickedUp|food-restaurant-b|A",
                "80|food.interruption-recovery|DriverInterrupted|food-route-b|P",
                "95|food.cooking|Recooking|food-restaurant-b|R",
                "110|food.dispatch|Redispatched|food-driver-wait-b|R",
                "125|food.pickup|RepickedUp|food-restaurant-b|R",
                "140|food.delivery|Delivered|food-customer-b|R",
                "150|food.delivery|ReceiptConfirmed|food-customer-b|R",
                "160|food.interruption-recovery|DriverReturned|food-driver-wait-b|R");
            AssertCase("cargo-normal",
                "110|cargo.request|TransportRequested|cargo-origin-a|A",
                "125|cargo.terms-agreement|TermsAgreed|cargo-origin-a|A",
                "140|cargo.dispatch|DriverAssigned|cargo-driver-wait-a|A",
                "155|cargo.pickup|Loaded|cargo-origin-a|A",
                "170|cargo.transport|InTransit|cargo-route-a|A",
                "185|cargo.dropoff|Unloaded|cargo-destination-a|A",
                "200|cargo.dropoff|ConsigneeAccepted|cargo-destination-a|A",
                "215|cargo.evidence-settlement|ProofReturned|cargo-driver-wait-a|C");
            AssertCase("cargo-recovery",
                "150|cargo.request|TransportRequested|cargo-origin-b|A",
                "165|cargo.terms-agreement|TermsAgreed|cargo-origin-b|A",
                "180|cargo.dispatch|DriverAssigned|cargo-driver-wait-b|A",
                "195|cargo.pickup|Loaded|cargo-origin-b|A",
                "210|cargo.transport|InTransit|cargo-route-b|A",
                "225|cargo.interruption-recovery|TransportInterrupted|cargo-route-b|P",
                "240|cargo.interruption-recovery|CargoProtected|cargo-safe-stop-b|P",
                "255|cargo.dispatch|Redispatched|cargo-safe-stop-b|R",
                "270|cargo.transport|TransportResumed|cargo-route-b|R",
                "285|cargo.dropoff|ConsigneeAccepted|cargo-destination-b|R",
                "300|cargo.evidence-settlement|ProofReturned|cargo-driver-wait-b|R");
            AssertCase("warehouse-normal",
                "250|warehouse.inbound-plan|InboundExpected|warehouse-inbound-a|A",
                "265|warehouse.receiving|Receiving|warehouse-inbound-a|A",
                "280|warehouse.inspection|Inspecting|warehouse-inspection-a|A",
                "295|warehouse.put-away-inventory|PutAwayCompleted|warehouse-storage-a|A",
                "310|warehouse.outbound-allocation|OutboundAllocated|warehouse-storage-a|A",
                "325|warehouse.picking|Picking|warehouse-picking-a|A",
                "340|warehouse.packing|Packed|warehouse-packing-a|A",
                "355|warehouse.outbound-handoff|OutboundReady|warehouse-outbound-a|C");
            AssertCase("warehouse-quantity-mismatch-recovery",
                "290|warehouse.inbound-plan|InboundExpected|warehouse-inbound-b|A",
                "305|warehouse.receiving|Receiving|warehouse-inbound-b|A",
                "320|warehouse.inspection|QuantityMismatchDetected|warehouse-inspection-b|P",
                "335|warehouse.exception-recovery|UnitProtected|warehouse-hold-b|P",
                "350|warehouse.inspection|Recounted|warehouse-inspection-b|R",
                "365|warehouse.put-away-inventory|PutAwayCompleted|warehouse-storage-b|R",
                "380|warehouse.outbound-allocation|OutboundAllocated|warehouse-storage-b|R",
                "395|warehouse.picking|Picking|warehouse-picking-b|R",
                "410|warehouse.packing|Packed|warehouse-packing-b|R",
                "425|warehouse.outbound-handoff|OutboundReady|warehouse-outbound-b|R");
            AssertCase("mart-normal",
                "390|mart.supply-agreement|SupplyAgreementSelected|mart-a|A",
                "405|mart.replenishment-order|InboundOrdered|mart-a|A",
                "420|mart.inbound-receiving|ReceivedAndInspected|mart-inbound-a|A",
                "435|mart.putaway-inventory|Stocked|mart-storage-a|A",
                "450|mart.customer-order-allocation|OrderAllocated|mart-storage-a|A",
                "465|mart.picking-packing|PickedAndPacked|mart-packing-a|A",
                "480|mart.last-mile-handoff|FoodDeliveryChildHandedOff|mart-handoff-a|A",
                "495|mart.completion-recovery|Completed|mart-a|C");
            AssertCase("mart-recovery",
                "430|mart.supply-agreement|SupplyAgreementSelected|mart-b|A",
                "445|mart.replenishment-order|InboundOrdered|mart-b|A",
                "460|mart.inbound-receiving|ReceivedAndInspected|mart-inbound-b|A",
                "475|mart.putaway-inventory|Stocked|mart-storage-b|A",
                "490|mart.customer-order-allocation|StockoutDetected|mart-storage-b|P",
                "505|mart.completion-recovery|ReplacementOrHold|mart-hold-b|P",
                "520|mart.customer-order-allocation|StockAllocatedAfterRetry|mart-storage-b|R",
                "535|mart.picking-packing|PickedAndPacked|mart-packing-b|R",
                "550|mart.last-mile-handoff|FoodDeliveryChildHandedOff|mart-handoff-b|R",
                "565|mart.completion-recovery|CompletedAfterRecovery|mart-b|R");

            var final = 다중Os생명주기검증Fixture.CreateResponse("r4-test", 599, Epoch);
            Assert.That(final.Items.Length, Is.EqualTo(8));
            Assert.That(final.Items.Select(value => CaseCode(value.SnapshotStableId)),
                Is.EquivalentTo(new[]
                {
                    "food-normal", "food-recovery", "cargo-normal", "cargo-recovery",
                    "warehouse-normal", "warehouse-quantity-mismatch-recovery",
                    "mart-normal", "mart-recovery"
                }));
            Assert.That(final.Items.Any(value => value.LifecycleStageCode is
                "food.receipt-confirmed" or "food.courier-return"
                or "cargo.consignee-accepted"), Is.False);
        }

        [Test]
        public void 서버R4의_마흔개정확의미위치는_서로다른검증기준점으로결속한다()
        {
            var all = Enumerable.Range(0, 다중Os생명주기검증Policy.DurationSeconds)
                .SelectMany(ItemsAt)
                .GroupBy(value => value.SemanticPlaceStableId, StringComparer.Ordinal)
                .Select(value => value.First())
                .OrderBy(value => value.SemanticPlaceStableId, StringComparer.Ordinal)
                .ToArray();
            Assert.That(all.Length, Is.EqualTo(40));
            Assert.That(다중Os생명주기검증Policy.VerificationSemanticPlaceStableIds,
                Is.EquivalentTo(all.Select(value => value.SemanticPlaceStableId)));

            var resolved = all.Select(value =>
            {
                Assert.That(다중Os생명주기검증Policy.TryResolveVerificationAnchor(
                    value.OperatingSystemId, value.SemanticPlaceStableId, out var anchor),
                    Is.True, value.SemanticPlaceStableId);
                Assert.That(anchor.OperatingSystemId, Is.EqualTo(value.OperatingSystemId));
                Assert.That(anchor.SemanticPlaceStableId,
                    Is.EqualTo(value.SemanticPlaceStableId));
                return anchor;
            }).ToArray();

            Assert.That(resolved.Select(value => value.BaseAnchorStableId + "|"
                                                 + value.OffsetX + "|" + value.OffsetZ)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(40));
            AssertAnchor(OperationalWorldOperatingSystemIds.FoodDelivery,
                "synthetic-place:food-customer-a", "synthetic-place:food-route-a", 42f, 34f);
            AssertAnchor(OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                "synthetic-place:cargo-safe-stop-b", "synthetic-place:cargo-route-b", 12f, 45f);
            AssertAnchor(OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                "synthetic-place:warehouse-hold-b", "synthetic-place:warehouse-a", -5f, 92f);
            AssertAnchor(OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
                "synthetic-place:mart-handoff-b", "synthetic-place:mart-a", 75f, 35f);
            Assert.That(다중Os생명주기검증Policy.TryResolveVerificationAnchor(
                OperationalWorldOperatingSystemIds.FoodDelivery,
                "synthetic-place:unlisted", out _), Is.False);
            Assert.That(다중Os생명주기검증Policy.TryResolveVerificationAnchor(
                OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                "synthetic-place:food-customer-a", out _), Is.False);
        }

        [Test]
        public void 단계기준점변경은_부드럽게보간하고_낮은Revision은위치를바꾸지않는다()
        {
            var root = new GameObject("multi-os-marker-interpolation-test");
            try
            {
                var marker = root.AddComponent<다중Os생명주기MarkerView>();
                var from = MarkerState(1, 다중Os생명주기StageCodes.FoodPickup,
                    "PickedUp", "synthetic-place:food-restaurant-a");
                var to = MarkerState(2, 다중Os생명주기StageCodes.FoodDelivery,
                    "Delivered", "synthetic-place:food-customer-a");
                Assert.That(marker.ApplyAt(from, false,
                    new Vector3(10f, 8f, 20f), null, 10f), Is.True);
                Assert.That(marker.IsInterpolating, Is.False);

                Assert.That(marker.ApplyAt(to, false,
                    new Vector3(90f, 8f, 60f), null, 20f), Is.True);
                Assert.That(marker.IsInterpolating, Is.True);
                Assert.That(marker.TargetLocalPosition,
                    Is.EqualTo(new Vector3(90f, 8f, 60f)));

                marker.ApplyPresentation(20f
                    + 다중Os생명주기MarkerView.StageAnchorInterpolationSeconds * .5f);
                Assert.That(marker.DisplayedBaseLocalPosition.x, Is.EqualTo(50f).Within(.01f));
                Assert.That(marker.DisplayedBaseLocalPosition.z, Is.EqualTo(40f).Within(.01f));
                Assert.That(marker.IsInterpolating, Is.True);

                marker.ApplyPresentation(20f
                    + 다중Os생명주기MarkerView.StageAnchorInterpolationSeconds);
                Assert.That(marker.DisplayedBaseLocalPosition,
                    Is.EqualTo(new Vector3(90f, 8f, 60f)));
                Assert.That(marker.IsInterpolating, Is.False);

                Assert.That(marker.ApplyAt(from, false,
                    new Vector3(-100f, 8f, -100f), null, 22f), Is.False);
                Assert.That(marker.Revision, Is.EqualTo(2));
                Assert.That(marker.TargetLocalPosition,
                    Is.EqualTo(new Vector3(90f, 8f, 60f)));
                Assert.That(marker.Frozen, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 네Os의_허용ItemKind만_배치계약을통과한다()
        {
            var response = 다중Os생명주기검증Fixture.CreateResponse("allowlist", 599, Epoch);
            var result = new OperationalWorldSceneInterpreter().Apply(
                response, Epoch.AddSeconds(599));
            var plan = new OperationalWorldScenePlacementPlanner().Create(result);

            Assert.That(result.Accepted, Is.True);
            Assert.That(plan.Accepted, Is.True);
            Assert.That(plan.Diagnostics, Is.Empty);
            Assert.That(plan.Instructions.Length, Is.EqualTo(8));
            Assert.That(plan.Instructions.Where(value => value.OperatingSystemId ==
                    OperationalWorldOperatingSystemIds.FoodDelivery)
                .All(value => value.VisualKey == "operational.completed-lifecycle"), Is.True);
            Assert.That(plan.Instructions.Where(value => value.OperatingSystemId ==
                    OperationalWorldOperatingSystemIds.DomesticCargoTransport)
                .All(value => value.VisualKey == "cargo.handoff"), Is.True);
            Assert.That(plan.Instructions.Where(value => value.OperatingSystemId ==
                    OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment)
                .All(value => value.VisualKey == "warehouse.task"), Is.True);
            Assert.That(plan.Instructions.Where(value => value.OperatingSystemId ==
                    OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics)
                .All(value => value.VisualKey == "warehouse.actor"), Is.True);
        }

        [Test]
        public void 미지원항목한건은_격리하고_다른Os상태는계속해석한다()
        {
            var valid = PlanAt(465);
            valid.Instructions = valid.Instructions.Concat(new[]
            {
                Copy(valid.Instructions[0], "unsupported:stage", "unknown.lifecycle")
            }).ToArray();

            var projected = new 다중Os생명주기표현Projector().Create(valid);

            Assert.That(projected.Accepted, Is.True);
            Assert.That(projected.States.Length, Is.EqualTo(8));
            Assert.That(projected.Diagnostics.Single().Code,
                Is.EqualTo(다중Os생명주기표현DiagnosticCodes.StageUnsupported));
            Assert.That(projected.States.Single(value =>
                    value.ObjectStableId.EndsWith(":food-normal", StringComparison.Ordinal))
                .ActivityLabel, Is.EqualTo("기사 대기지 복귀"));
        }

        [Test]
        public void 세부합성위치는_검증표본에만_기존배치지점으로일반화한다()
        {
            var root = new GameObject("multi-os-layer-test");
            try
            {
                var world = root.AddComponent<운영관찰WorldView>();
                var diorama = root.AddComponent<사가정운영디오라마View>();
                diorama.Initialize();
                var layer = root.AddComponent<다중Os생명주기검증Layer>();
                layer.Configure(world, diorama);

                var samplePlan = PlanAt(465);
                world.Apply(samplePlan, Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                layer.Apply(samplePlan);

                Assert.That(layer.StateCount, Is.EqualTo(8));
                Assert.That(layer.MarkerCount, Is.EqualTo(8));
                Assert.That(layer.GeneralizedPlacementCount, Is.EqualTo(8));
                var originalMarkers = layer.RuntimeRoot
                    .GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                    .ToDictionary(value => value.ObjectStableId, StringComparer.Ordinal);

                var next = PlanAt(475);
                world.Apply(next, Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                layer.Apply(next);
                Assert.That(layer.MarkerCount, Is.EqualTo(8));
                Assert.That(layer.RuntimeRoot
                    .GetComponentsInChildren<다중Os생명주기MarkerView>(true)
                    .All(value => ReferenceEquals(value, originalMarkers[value.ObjectStableId])),
                    Is.True);

                var operational = PlanAt(465);
                operational.Instructions = operational.Instructions.Select(value =>
                    Copy(value, value.ObjectStableId + ":operational",
                        value.LifecycleStageCode,
                        OperationalWorldSceneSourceKinds.OperationalProjection)).ToArray();
                layer.Apply(operational);
                Assert.That(layer.StateCount, Is.Zero);
                Assert.That(layer.GeneralizedPlacementCount, Is.Zero);
                Assert.That(layer.Diagnostics.Count(value =>
                    value.Code == "LifecyclePlacementObjectMissing"), Is.EqualTo(8));

                layer.ClearLifecycleStates();
                Assert.That(layer.RuntimeRoot, Is.Not.Null);
                Assert.That(layer.MarkerCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 기존Controller의_V2적용경로도_같은생명주기Layer를갱신한다()
        {
            var root = new GameObject("multi-os-controller-binding-test");
            try
            {
                var world = root.AddComponent<운영관찰WorldView>();
                var diorama = root.AddComponent<사가정운영디오라마View>();
                diorama.Initialize();
                var controller = root.AddComponent<운영관찰WorldController>();
                controller.Configure(world);
                Invoke(controller, "InitializeClient");
                var response = 다중Os생명주기검증Fixture.CreateResponse(
                    "controller-v2", 465, Epoch);
                var applied = new OperationalWorldSceneInterpreter().Apply(
                    response, Epoch.AddSeconds(465));

                Invoke(controller, "CompleteRefresh", Task.FromResult(applied));

                var layer = root.GetComponent<다중Os생명주기검증Layer>();
                Assert.That(layer, Is.Not.Null);
                Assert.That(layer.StateCount, Is.EqualTo(8));
                Assert.That(layer.CurrentStates.Any(value =>
                    value.ActivityCode == "PickedAndPacked"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 로컬음식점입력은_최대열두익명아이콘과_샘플메뉴만허용한다()
        {
            var mapAsset = Resources.Load<TextAsset>("SagajeongReference");
            var map = JsonUtility.FromJson<공간참고자료>(mapAsset.text);
            var data = new 사가정음식점아이콘FixtureData
            {
                schema = 사가정음식점아이콘FixtureLoader.Schema,
                fixtureRevision = "db-readback-sanitized.r1",
                sourceKindCode = 사가정음식점아이콘FixtureLoader.RequiredSourceKind,
                distributionApproved = false,
                operationalEffectsAllowed = false,
                disclosure = 사가정음식점아이콘FixtureLoader.RequiredDisclosure,
                entries = Enumerable.Range(1, 12).Select(index =>
                    new 사가정음식점아이콘FixtureEntry
                    {
                        stableId = "restaurant-icon:sample-" + index,
                        categoryCode = index % 2 == 0 ? "Korean" : "CafeBakery",
                        unityX = map.offsetX,
                        unityZ = map.offsetZ,
                        sampleMenuDisplayNames = new[]
                        {
                            "[샘플] 오늘의 한 끼", "[샘플] 계절 음료"
                        }
                    }).ToArray()
            };

            Assert.DoesNotThrow(() => 사가정음식점아이콘FixtureLoader.Validate(data, map));
            data.entries[0].sampleMenuDisplayNames[0] = "실제 메뉴로 오해할 이름";
            Assert.Throws<FormatException>(() =>
                사가정음식점아이콘FixtureLoader.Validate(data, map));
        }

        [Test]
        public void 로컬음식점Json의_실상호주소필드는_파싱전에거부한다()
        {
            var path = Path.Combine(Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, "..", "Temp")),
                "restaurant-private-field-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{\"businessName\":\"실상호\"}");
            try
            {
                Assert.Throws<FormatException>(() =>
                    사가정음식점아이콘FixtureLoader.TryLoad(
                        out _, out _, path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void GameView캡처소유권은_동시에하나만허용하고_해제된다()
        {
            Assert.That(사가정GameView검증CaptureCoordinator.TryAcquire(
                "multi-os-test", out var first), Is.True);
            try
            {
                Assert.That(사가정GameView검증CaptureCoordinator.TryAcquire(
                    "other-test", out var second), Is.False);
                Assert.That(second, Is.Null);
                Assert.That(사가정GameView검증CaptureCoordinator.ActiveOwner,
                    Is.EqualTo("multi-os-test"));
            }
            finally
            {
                first.Dispose();
            }
            Assert.That(사가정GameView검증CaptureCoordinator.ActiveOwner, Is.Empty);
        }

        private static void AssertCase(string caseCode, params string[] signatures)
        {
            for (var index = 0; index < signatures.Length; index++)
            {
                var parts = signatures[index].Split('|');
                var offset = int.Parse(parts[0]);
                var current = ItemAt(caseCode, offset);
                Assert.That(current.Revision, Is.EqualTo(index + 1), caseCode);
                Assert.That(current.OccurredAtUtc,
                    Is.EqualTo(Epoch.AddSeconds(offset)), caseCode);
                Assert.That(current.LifecycleStageCode, Is.EqualTo(parts[1]), caseCode);
                Assert.That(current.ActivityCode, Is.EqualTo(parts[2]), caseCode);
                Assert.That(current.SemanticPlaceStableId,
                    Is.EqualTo("synthetic-place:" + parts[3]), caseCode);
                Assert.That(current.AttentionStateCode,
                    Is.EqualTo(Attention(parts[4])), caseCode);
                if (index == 0)
                {
                    Assert.That(ItemsAt(offset - 1).Any(value =>
                        CaseCode(value.SnapshotStableId) == caseCode), Is.False, caseCode);
                }
                else
                {
                    Assert.That(ItemAt(caseCode, offset - 1).Revision,
                        Is.EqualTo(index), caseCode);
                }
            }
            Assert.That(ItemAt(caseCode, 599).Revision, Is.EqualTo(signatures.Length));
        }

        private static void AssertAnchor(string operatingSystemId,
            string semanticPlaceStableId, string baseAnchorStableId,
            float offsetX, float offsetZ)
        {
            Assert.That(다중Os생명주기검증Policy.TryResolveVerificationAnchor(
                operatingSystemId, semanticPlaceStableId, out var anchor), Is.True);
            Assert.That(anchor.BaseAnchorStableId, Is.EqualTo(baseAnchorStableId));
            Assert.That(anchor.OffsetX, Is.EqualTo(offsetX));
            Assert.That(anchor.OffsetY, Is.Zero);
            Assert.That(anchor.OffsetZ, Is.EqualTo(offsetZ));
        }

        private static 다중Os생명주기표현State MarkerState(long revision,
            string stageCode, string activityCode, string place)
            => new()
            {
                ObjectStableId = "sample-observation:marker-test:food-normal",
                WorkStableId = "sample-work:marker-test:food-normal",
                OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                OperatingSystemLabel = "음식배달",
                LifecycleStageCode = stageCode,
                LifecycleStageLabel = stageCode,
                ActivityCode = activityCode,
                ActivityLabel = activityCode,
                AttentionStateCode = OperationalWorldAttentionStateCodes.Active,
                AttentionLabel = "진행 중",
                SemanticPlaceStableId = place,
                SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                Revision = revision,
                Progress01 = .5f,
                ExpiresAtUtc = Epoch.AddHours(1)
            };

        private static OperationalWorldSceneItem ItemAt(string caseCode, int second)
            => ItemsAt(second).Single(value =>
                CaseCode(value.SnapshotStableId) == caseCode);

        private static OperationalWorldSceneItem[] ItemsAt(int second)
            => 다중Os생명주기검증Fixture.CreateResponse("r4-test", second, Epoch).Items;

        private static string CaseCode(string snapshotStableId)
            => snapshotStableId.Substring(snapshotStableId.LastIndexOf(':') + 1);

        private static string Attention(string token)
            => token == "P" ? OperationalWorldAttentionStateCodes.RecoveryPending
                : token == "R" ? OperationalWorldAttentionStateCodes.Recovered
                : token == "C" ? OperationalWorldAttentionStateCodes.Completed
                : OperationalWorldAttentionStateCodes.Active;

        private static OperationalWorldPlacementPlan PlanAt(int second)
        {
            var response = 다중Os생명주기검증Fixture.CreateResponse(
                "presentation-layer", second, Epoch);
            var applied = new OperationalWorldSceneInterpreter().Apply(
                response, Epoch.AddSeconds(second));
            Assert.That(applied.Accepted, Is.True);
            return new OperationalWorldScenePlacementPlanner().Create(applied);
        }

        private static OperationalWorldPlacementInstruction Copy(
            OperationalWorldPlacementInstruction source,
            string objectStableId,
            string stageCode,
            string sourceKind = null)
            => new()
            {
                ObjectStableId = objectStableId,
                AreaStableId = source.AreaStableId,
                OperatingSystemId = source.OperatingSystemId,
                AnchorKey = source.AnchorKey,
                VisualKey = source.VisualKey,
                ActivityCode = source.ActivityCode,
                WorkStableId = source.WorkStableId,
                LifecycleStageCode = stageCode,
                AttentionStateCode = source.AttentionStateCode,
                ObjectKindCode = source.ObjectKindCode,
                SemanticPlaceStableId = source.SemanticPlaceStableId,
                SourceKindCode = sourceKind ?? source.SourceKindCode,
                ScenarioRunStableId = source.ScenarioRunStableId,
                Revision = source.Revision,
                ExpiresAtUtc = source.ExpiresAtUtc
            };

        private static void Invoke(object target, string method, params object[] values)
        {
            var found = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(target.GetType().Name, method);
            found.Invoke(target, values);
        }
    }
}
