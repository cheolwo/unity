using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "운영 관찰 배치 객체의 안정 식별자 조정·개별 동결·회복을 EditMode에서 검증한다.",
        Boundary = "실제 서버 연결, Play Mode, Game View 또는 업무 완료 증거가 아니다.")]
    public sealed class 운영관찰WorldTests
    {
        [Test]
        public void 같은안정식별자는_객체를재사용하고_누락객체만제거한다()
        {
            var root = new GameObject("OperationalOsWorldRoot");
            try
            {
                var view = root.AddComponent<운영관찰WorldView>();
                view.Apply(Plan(Instruction("object:a", 1), Instruction("object:b", 1)),
                    Array.Empty<OperationalWorldSceneApplyDiagnostic>());
                var original = view.Objects.Single(value => value.ObjectStableId == "object:a");

                view.Apply(Plan(Instruction("object:a", 2)),
                    Array.Empty<OperationalWorldSceneApplyDiagnostic>());

                Assert.That(view.ObjectCount, Is.EqualTo(1));
                Assert.That(view.Objects.Single(), Is.SameAs(original));
                Assert.That(original.Revision, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 잘못된한객체는_마지막정상표현에서동결되고_높은판본으로회복한다()
        {
            var root = new GameObject("OperationalOsWorldRoot");
            try
            {
                var view = root.AddComponent<운영관찰WorldView>();
                view.Apply(Plan(Instruction("object:a", 3)),
                    new[]
                    {
                        new OperationalWorldSceneApplyDiagnostic
                        {
                            SnapshotStableId = "object:a",
                            ErrorCode = OperationalWorldSceneApplyDiagnosticCodes.LowerRevision,
                            ExistingObjectFrozen = true
                        }
                    });
                Assert.That(view.Objects.Single().Frozen, Is.True);
                Assert.That(view.Objects.Single().Revision, Is.EqualTo(3));

                view.Apply(Plan(Instruction("object:a", 4)),
                    Array.Empty<OperationalWorldSceneApplyDiagnostic>());

                Assert.That(view.Objects.Single().Frozen, Is.False);
                Assert.That(view.Objects.Single().Revision, Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 연결은_전용Loopback주소만허용하고_갱신주기는30초다()
        {
            Assert.That(운영관찰검증ApiTransport.AllowedEndpoint("http://127.0.0.1:53216"), Is.True);
            Assert.That(운영관찰검증ApiTransport.AllowedEndpoint("https://127.0.0.1:53216"), Is.False);
            Assert.That(운영관찰검증ApiTransport.AllowedEndpoint("http://localhost:53216"), Is.False);
            Assert.That(OperationalWorldScenePolicy.RefreshAfterSeconds, Is.EqualTo(30));
        }

        private static OperationalWorldPlacementPlan Plan(
            params OperationalWorldPlacementInstruction[] instructions)
            => new()
            {
                Accepted = true,
                Instructions = instructions
            };

        [Test]
        public void 재활성화는_Awake없이도_조회연결을다시초기화한다()
        {
            var root=new GameObject("reload-test");
            try
            {
                var view=root.AddComponent<운영관찰WorldView>();
                var controller=root.AddComponent<Ssalddel.Unity.Bootstrap.운영관찰WorldController>();
                controller.Configure(view);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                controller.GetType().GetMethod("OnDisable",flags).Invoke(controller,null);
                controller.GetType().GetMethod("OnEnable",flags).Invoke(controller,null);
                var field=controller.GetType().GetField("client",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Assert.That(field.GetValue(controller),Is.Not.Null);
                Assert.That(controller.Cursor,Is.Zero);
                Assert.That(controller.StatusCode,Is.EqualTo("WaitingForConnection"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static OperationalWorldPlacementInstruction Instruction(string id, long revision)
            => new()
            {
                ObjectStableId = id,
                AreaStableId = "area:test",
                OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                AnchorKey = "world.area.food-delivery",
                VisualKey = "operational.completed-lifecycle",
                ActivityCode = "ReceiptConfirmed",
                WorkStableId = "work:" + id,
                LifecycleStageCode = "ReceiptConfirmed",
                AttentionStateCode = OperationalWorldAttentionStateCodes.Completed,
                ObjectKindCode = "FoodDelivery",
                SemanticPlaceStableId = "synthetic-place:food-route-a",
                SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                ScenarioRunStableId = "observable-operations-run:test",
                Revision = revision,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            };
    }
}
