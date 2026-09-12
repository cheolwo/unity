using System;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 가상배달관찰Tests
    {
        [Test]
        public void 기사표현변환은_같은주문과최신Session만받고_입력사본을바꾸지않는다()
        {
            var order = new Simulation음식배달Snapshot
            {
                FoodOrderStableId = "food-order:test:1",
                StateCode = "기사배정",
                Revision = 3,
            };
            var courier = new 가상배달기사Snapshot
            {
                ActorStableId = "actor:courier:1",
                OrderStableId = order.FoodOrderStableId,
                Stage = "ToRestaurant",
                X = 3,
                Z = 4,
                VehicleX = 2,
                VehicleZ = 4,
                Carrying = false,
            };
            var session = new 경영SimulationSessionSnapshot
            {
                SessionStableId = "simulation:test:food-delivery",
                Revision = 7,
                FoodDeliveries = new[] { order },
            };

            Assert.IsTrue(음식배달ActorPresentationProjector.TryProject(
                session, courier, session.SessionStableId, 6, courier.ActorStableId, out var model, out var failureCode));
            Assert.AreEqual(string.Empty, failureCode);
            Assert.AreEqual(session.SessionStableId, model.SessionStableId);
            Assert.AreEqual(session.Revision, model.SessionRevision);
            Assert.AreEqual(courier.ActorStableId, model.ActorStableId);
            Assert.AreEqual(order.FoodOrderStableId, model.OrderStableId);
            Assert.AreEqual(order.StateCode, model.OrderStateCode);
            Assert.AreEqual(3, model.ActorX);
            Assert.AreEqual("ToRestaurant", courier.Stage);
            Assert.AreEqual(7, session.Revision);

            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, session.SessionStableId, 8, courier.ActorStableId, out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.StaleRevision, failureCode);

            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, session.SessionStableId, 6, "actor:courier:other", out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.ActorIdentityChanged, failureCode);
        }

        [Test]
        public void 기사표현변환은_대기중빈주문을허용하고_활동중누락과중복은차단한다()
        {
            var session = new 경영SimulationSessionSnapshot
            {
                SessionStableId = "simulation:test:idle",
                Revision = 1,
            };
            var courier = new 가상배달기사Snapshot
            {
                ActorStableId = "actor:courier:idle",
                Stage = "Idle",
            };

            Assert.IsTrue(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty, out var idle, out _));
            Assert.AreEqual(string.Empty, idle.OrderStableId);
            Assert.AreEqual(-1, idle.OrderRevision);

            courier.Stage = "ToRestaurant";
            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty, out _, out var failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.ActiveOrderIdentityMissing, failureCode);

            courier.OrderStableId = "food-order:duplicate";
            session.FoodDeliveries = new[]
            {
                new Simulation음식배달Snapshot { FoodOrderStableId = courier.OrderStableId },
                new Simulation음식배달Snapshot { FoodOrderStableId = courier.OrderStableId },
            };
            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty, out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.OrderDuplicate, failureCode);
        }

        [TestCase("주문대기", "조리중", "Restaurant", "RI", "restaurant-workflow-meaning.r1")]
        [TestCase("조리중", "픽업대기", "Restaurant", "RI", "restaurant-workflow-meaning.r1")]
        [TestCase("픽업대기", "기사배정", "Dispatch", "GAN", "dispatch-workflow-meaning.r2")]
        [TestCase("기사배정", "픽업완료", "Delivery", "GAM", "delivery-workflow-meaning.r1")]
        [TestCase("픽업완료", "전달완료", "Delivery", "GAM", "delivery-workflow-meaning.r1")]
        [TestCase("전달완료", "수령확인", "Order", "JIN", "order-workflow-meaning.r1")]
        public void 기사표현변환은_최근확정전이를_오행업무의미로검증한다(
            string fromState, string toState, string moduleCode,
            string gwaeCode, string meaningRevision)
        {
            var order = new Simulation음식배달Snapshot
            {
                FoodOrderStableId = "food-order:test:workflow",
                StateCode = toState,
                Revision = 2,
                StateHistory = new[]
                {
                    new Simulation음식배달상태전이Snapshot
                    {
                        FromStateCode = fromState,
                        ToStateCode = toState,
                        WorldTick = 5,
                        RuleRevision = "food-delivery.v2",
                    },
                },
            };
            var courier = new 가상배달기사Snapshot
            {
                ActorStableId = "actor:courier:workflow",
                OrderStableId = order.FoodOrderStableId,
                Stage = "DriveRestaurant",
            };
            var session = new 경영SimulationSessionSnapshot
            {
                SessionStableId = "simulation:test:workflow",
                CurrentTick = 8,
                Revision = 9,
                FoodDeliveries = new[] { order },
            };

            Assert.IsTrue(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty,
                out var model, out var failureCode), failureCode);
            Assert.AreEqual(음식배달ActorPresentationProjector.WorkflowObservationVerified,
                model.WorkflowObservationStatusCode);
            Assert.AreEqual("BusinessWorkflow", model.WorkflowEngineCode);
            Assert.AreEqual(FiveElementClassificationCodes.토간,
                model.WorkflowEngineClassificationPrimaryCode);
            CollectionAssert.Contains(model.WorkflowEngineClassificationSupportCodes,
                FiveElementClassificationCodes.수감);
            Assert.IsFalse(model.WorkflowIsExecutionAuthority);
            Assert.AreEqual(moduleCode, model.WorkflowModuleCode);
            Assert.AreEqual(gwaeCode,
                model.WorkflowModuleClassificationPrimaryCode);
            Assert.AreEqual(meaningRevision, model.WorkflowMeaningRevision);
            Assert.AreEqual("food-delivery.v2", model.WorkflowRuleRevision);
            Assert.AreEqual(fromState, model.LatestTransitionFromStateCode);
            Assert.AreEqual(toState, model.LatestTransitionToStateCode);
            Assert.AreEqual(5, model.LatestTransitionWorldTick);
            Assert.AreEqual("음식점으로 운전", model.CourierActivityDisplayName);
            Assert.AreEqual(fromState, order.StateHistory[0].FromStateCode);
            Assert.AreEqual(8, session.CurrentTick);
        }

        [Test]
        public void 기사표현변환은_전이역사와규칙판본불일치를_낮은수준추정으로덮지않는다()
        {
            var order = new Simulation음식배달Snapshot
            {
                FoodOrderStableId = "food-order:test:mismatch",
                StateCode = 음식배달상태코드.기사배정,
                StateHistory = new[]
                {
                    new Simulation음식배달상태전이Snapshot
                    {
                        FromStateCode = 음식배달상태코드.픽업대기,
                        ToStateCode = 음식배달상태코드.기사배정,
                        WorldTick = 3,
                        RuleRevision = "old-rule",
                    },
                },
            };
            var courier = new 가상배달기사Snapshot
            {
                ActorStableId = "actor:courier:mismatch",
                OrderStableId = order.FoodOrderStableId,
                Stage = "WaitFood",
            };
            var session = new 경영SimulationSessionSnapshot
            {
                SessionStableId = "simulation:test:mismatch",
                CurrentTick = 4,
                FoodDeliveries = new[] { order },
            };

            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty,
                out _, out var failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.WorkflowRuleRevisionMismatch,
                failureCode);

            order.StateHistory[0].RuleRevision = "food-delivery.v2";
            order.StateCode = 음식배달상태코드.픽업완료;
            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty,
                out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.TransitionOrderStateMismatch,
                failureCode);

            order.StateCode = 음식배달상태코드.기사배정;
            order.StateHistory[0].WorldTick = 5;
            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty,
                out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.TransitionHistoryInvalid,
                failureCode);
        }

        [Test]
        public void 기사표현변환은_알수없는기사단계와_없는오행Engine을차단한다()
        {
            var session = new 경영SimulationSessionSnapshot
            {
                SessionStableId = "simulation:test:unknown-stage",
            };
            var courier = new 가상배달기사Snapshot
            {
                ActorStableId = "actor:courier:unknown-stage",
                Stage = "TeleportToCustomer",
            };

            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, string.Empty, -1, string.Empty,
                out _, out var failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.CourierStageUnknown,
                failureCode);

            courier.Stage = "Idle";
            Assert.IsFalse(음식배달ActorPresentationProjector.TryProject(
                session, courier, null, string.Empty, -1, string.Empty,
                out _, out failureCode));
            Assert.AreEqual(음식배달ActorPresentationProjector.WorkflowEngineMissing,
                failureCode);
        }

        [Test]
        public void Controller는_표현차단을_일반성공메시지로덮지않는다()
        {
            var root = new GameObject("배달기사 표현 차단 시험");
            try
            {
                var component = root.AddComponent<가상배달관찰Controller>();
                var type = component.GetType();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetMethod("Build", flags).Invoke(component, null);
                type.GetField("state", flags).SetValue(component,
                    new 경영SimulationSessionSnapshot
                    {
                        SessionStableId = "simulation:test:presentation-blocked",
                        SyntheticCourier = new 가상배달기사Snapshot
                        {
                            ActorStableId = "actor:courier:presentation-blocked",
                            Stage = "UnknownStage",
                        },
                    });
                type.GetField("playing", flags).SetValue(component, true);

                var rendered = (bool)type.GetMethod("Render", flags).Invoke(component, null);

                Assert.IsFalse(rendered);
                Assert.IsFalse((bool)type.GetField("playing", flags).GetValue(component));
                StringAssert.Contains(음식배달ActorPresentationProjector.CourierStageUnknown,
                    (string)type.GetField("message", flags).GetValue(component));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 한건관찰은_수령뒤기사복귀에서_다음Tick전에멈춘다()
        {
            var root = new GameObject("한 건 배달 관찰 시험");
            try
            {
                var component = root.AddComponent<가상배달관찰Controller>();
                var type = component.GetType();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var completedOrder = new Simulation음식배달Snapshot
                {
                    FoodOrderStableId = "food-order:test:completed",
                    StateCode = 음식배달상태코드.수령확인,
                    ReceivedTick = 20,
                };
                var waitingOrder = new Simulation음식배달Snapshot
                {
                    FoodOrderStableId = "food-order:test:next",
                    StateCode = 음식배달상태코드.픽업대기,
                };
                var snapshot = new 경영SimulationSessionSnapshot
                {
                    CurrentTick = 30,
                    SyntheticCourier = new 가상배달기사Snapshot
                    {
                        ActorStableId = "actor:courier:single",
                        Stage = "Idle",
                    },
                    FoodDeliveries = new[] { completedOrder, waitingOrder },
                };
                type.GetField("state", flags).SetValue(component, snapshot);
                type.GetField("singleCourierObservation", flags).SetValue(component, true);
                type.GetField("stopAfterOneOrder", flags).SetValue(component, true);
                type.GetField("observedOrderStableId", flags).SetValue(component,
                    completedOrder.FoodOrderStableId);
                type.GetField("playing", flags).SetValue(component, true);

                Assert.IsTrue((bool)type.GetMethod("TryStopAfterObservedOrder", flags)
                    .Invoke(component, null));
                Assert.IsFalse((bool)type.GetField("playing", flags).GetValue(component));
                StringAssert.Contains("한 건 관찰 완료",
                    (string)type.GetField("message", flags).GetValue(component));
                Assert.AreEqual(음식배달상태코드.픽업대기, waitingOrder.StateCode);
                Assert.IsNull(waitingOrder.DispatchCandidateTick);
                Assert.AreEqual(30, snapshot.CurrentTick);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void r4수신함표시는_원장참조를읽고_저장복구전진행을차단한다()
        {
            var previous=Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root=new GameObject("주문 원장 표현 시험");
            try {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY","4");
                Assert.AreEqual(Nature생존LaunchProfile.OrderFlow,SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                Assert.AreNotEqual(가상배달관찰표본.MartSlotId,가상배달관찰표본.OrderFlowSlotId);
                var state=new 경영SimulationSessionSnapshot {CurrentTick=1,DurationTicks=365,WaitingFleet=new 가상배달대기Snapshot {
                    Mart=new 가상마트Snapshot(), OrderFlow=new 가상주문흐름Snapshot {Entries=new[] {new 가상주문연결Snapshot {OrderId="order:test"}}} }};
                var c=root.AddComponent<가상배달관찰Controller>(); var f=BindingFlags.Instance|BindingFlags.NonPublic;
                c.GetType().GetMethod("Build",f).Invoke(c,null);
                c.GetType().GetField("state",f).SetValue(c,state);
                c.GetType().GetMethod("Render",f).Invoke(c,null);
                Assert.AreEqual("미확인 1",root.transform.Find("음식점/표시 수신함").GetComponent<TextMesh>().text);
                c.GetType().GetField("저장복구필요",f).SetValue(c,true);
                c.GetType().GetField("message",f).SetValue(c,"저장 실패");
                ((System.Threading.Tasks.Task)c.GetType().GetMethod("Tick",f).Invoke(c,null)).GetAwaiter().GetResult();
                Assert.AreEqual("저장 실패",c.GetType().GetField("message",f).GetValue(c));
                Assert.AreEqual(1,state.CurrentTick);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY",previous);}
        }
        [Test]
        public void 마트프로필은_역할색상과_작업자물품사본을_표현한다()
        {
            var previous = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root = new GameObject("마트 표현 시험");
            try {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "3");
                Assert.AreEqual(Nature생존LaunchProfile.MartCenter, SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                Assert.AreNotEqual(가상배달관찰표본.WaitingSlotId, 가상배달관찰표본.MartSlotId);
                var mart = new 가상마트Snapshot { X=-8, Z=-14, WorkerOrderId="mart:test",
                    Orders=new[] {new 가상마트주문Snapshot {OrderId="mart:test",PickedUnits=1}} };
                var state = new 경영SimulationSessionSnapshot { WaitingFleet = new 가상배달대기Snapshot {Mart=mart} };
                var c=root.AddComponent<가상배달관찰Controller>(); var f=BindingFlags.Instance|BindingFlags.NonPublic;
                c.GetType().GetMethod("Build",f).Invoke(c,null);
                c.GetType().GetField("state",f).SetValue(c,state);
                c.GetType().GetMethod("Render",f).Invoke(c,null);
                Assert.Less(Vector4.Distance(가상동네역할색상.음식점,root.transform.Find("음식점").GetComponent<Renderer>().sharedMaterial.color), .0001f);
                Assert.Less(Vector4.Distance(가상동네역할색상.주문자,root.transform.Find("주택 B").GetComponent<Renderer>().sharedMaterial.color), .0001f);
                Assert.Less(Vector4.Distance(가상동네역할색상.창고,root.transform.Find("살뜰마트 배송센터").GetComponent<Renderer>().sharedMaterial.color), .0001f);
                Assert.Less(Vector4.Distance(가상동네역할색상.배송기사,root.transform.Find("기사 3").GetComponent<Renderer>().sharedMaterial.color), .0001f);
                Assert.AreEqual(new Vector3(-8,.9f,-14),root.transform.Find("마트 작업자").position);
                Assert.IsTrue(root.transform.Find("작업자 상품").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("인계 상품").gameObject.activeSelf);
                Assert.NotNull(root.transform.Find("기사 3/표시 3"));
                mart.Orders[0].ReadyTick=40;
                c.GetType().GetMethod("Render",f).Invoke(c,null);
                Assert.IsFalse(root.transform.Find("작업자 상품").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("인계 상품").gameObject.activeSelf);
                Assert.AreEqual(-8,mart.X);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY",previous); }
        }
        [Test]
        public void 대기프로필은_세기사와마트도로변자리를표현한다()
        {
            var previous=Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root=new GameObject("대기 기사 시험");
            try {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY","2");
                Assert.AreEqual(Nature생존LaunchProfile.WaitingFleet,SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                Assert.AreNotEqual(가상배달관찰표본.SlotId,가상배달관찰표본.WaitingSlotId);
                var state=new 경영SimulationSessionSnapshot { WaitingFleet=new 가상배달대기Snapshot {
                    Drivers=new[] {
                        new 가상배달대기기사Snapshot {Courier=new 가상배달기사Snapshot {X=-6,Z=-4,VehicleX=-6,VehicleZ=-4}},
                        new 가상배달대기기사Snapshot {Courier=new 가상배달기사Snapshot {X=0,Z=-4,VehicleX=0,VehicleZ=-4}},
                        new 가상배달대기기사Snapshot {Courier=new 가상배달기사Snapshot {X=6,Z=-4,VehicleX=6,VehicleZ=-4}}
                    } } };
                var c=root.AddComponent<가상배달관찰Controller>(); var f=BindingFlags.Instance|BindingFlags.NonPublic;
                c.GetType().GetMethod("Build",f).Invoke(c,null);
                c.GetType().GetField("state",f).SetValue(c,state);
                c.GetType().GetMethod("Render",f).Invoke(c,null);
                Assert.NotNull(root.transform.Find("살뜰마트 대기 거점"));
                Assert.NotNull(root.transform.Find("도로변 대기 4"));
                Assert.AreEqual(new Vector3(6,.9f,-4),root.transform.Find("기사 3").position);
                Assert.AreEqual(3,state.WaitingFleet.Drivers.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY",previous); }
        }
        [Test]
        public void 기본도형표현은_상태사본좌표를읽고_사본은변경하지않는다()
        {
            var root = new GameObject("배달 표현 시험");
            try
            {
                var component = root.AddComponent<가상배달관찰Controller>();
                var type = component.GetType();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetMethod("Build", flags).Invoke(component, null);
                var snapshot = new 경영SimulationSessionSnapshot
                {
                    SessionStableId = "simulation:test:presentation",
                    SyntheticCourier = new 가상배달기사Snapshot { X = 30, Z = 24, VehicleX = 30, VehicleZ = 20, Carrying = true }
                };
                type.GetField("state", flags).SetValue(component, snapshot);
                type.GetMethod("Render", flags).Invoke(component, null);
                Assert.AreEqual(new Vector3(30,.9f,24), root.transform.Find("기사").position);
                Assert.AreEqual(new Vector3(30,.5f,20), root.transform.Find("배달 차량").position);
                Assert.IsTrue(root.transform.Find("음식 상자").gameObject.activeSelf);
                Assert.NotNull(root.transform.Find("주택 A 출입구"));
                Assert.AreEqual(30, snapshot.SyntheticCourier.X);
                Assert.AreEqual(24, snapshot.SyntheticCourier.Z);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Controller는_불일치상태에서_마지막정상기사위치를보존하고_재생을멈춘다()
        {
            var root = new GameObject("배달 표현 차단 시험");
            try
            {
                var component = root.AddComponent<가상배달관찰Controller>();
                var type = component.GetType();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetMethod("Build", flags).Invoke(component, null);
                var order = new Simulation음식배달Snapshot
                {
                    FoodOrderStableId = "food-order:test:valid",
                    StateCode = "기사배정",
                    Revision = 1,
                };
                var snapshot = new 경영SimulationSessionSnapshot
                {
                    SessionStableId = "simulation:test:blocked",
                    Revision = 1,
                    FoodDeliveries = new[] { order },
                    SyntheticCourier = new 가상배달기사Snapshot
                    {
                        ActorStableId = "actor:courier:blocked",
                        OrderStableId = order.FoodOrderStableId,
                        Stage = "ToRestaurant",
                        X = 9,
                        Z = 8,
                        VehicleX = 9,
                        VehicleZ = 7,
                    },
                };
                type.GetField("state", flags).SetValue(component, snapshot);
                type.GetMethod("Render", flags).Invoke(component, null);
                var lastPosition = root.transform.Find("기사").position;

                snapshot.Revision = 2;
                snapshot.SyntheticCourier.OrderStableId = "food-order:test:missing";
                snapshot.SyntheticCourier.X = 99;
                type.GetField("playing", flags).SetValue(component, true);
                type.GetMethod("Render", flags).Invoke(component, null);

                Assert.AreEqual(lastPosition, root.transform.Find("기사").position);
                Assert.IsFalse((bool)type.GetField("playing", flags).GetValue(component));
                StringAssert.Contains(음식배달ActorPresentationProjector.OrderMissing,
                    (string)type.GetField("message", flags).GetValue(component));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 합성프로필은_기존수동표본과_저장슬롯이별개다()
        {
            var previous = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            try
            {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "1");
                Assert.AreEqual(Nature생존LaunchProfile.SyntheticDelivery, SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                Assert.AreNotEqual(음식점관찰표본.SlotId, 가상배달관찰표본.SlotId);
                Assert.AreEqual(100, 음식점관찰표본.Create(Guid.NewGuid()).DurationTicks);
            }
            finally { Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", previous); }
        }
    }
}
