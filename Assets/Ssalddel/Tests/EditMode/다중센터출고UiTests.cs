using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "가상 출고 UI의 동일 세션 명령·부분 피킹·선반 ID 결속을 EditMode에서 검증한다.",
        Boundary = "실제 Play Mode 클릭·물리 바코드 스캔·실물 출고·운영 증거가 아니다.")]
    public sealed class 다중센터출고UiTests
    {
        private GameObject root;
        private LocalSimulationRuntime runtime;
        private string sessionId;
        private string saves;
        private const string Slot = "fulfillment-ui-test-r1";

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            runtime?.Dispose();
        }

        private async Task<(다중센터출고SceneController controller, 다중센터출고View view, 다중센터출고선반Binding binding)> CreateUi()
        {
            saves = Path.Combine(Path.GetTempPath(), "fulfillment-unity-tests", Guid.NewGuid().ToString("N"));
            runtime = LocalSimulationUnityRuntime.Create(saves);
            var created = await runtime.Sessions.CreateAsync(다중센터출고표본.Create(Guid.NewGuid()));
            sessionId = created.SessionStableId;
            await runtime.ConfirmFulfillmentAsync(sessionId, new SimulationFulfillmentCommand
            {
                CommandId = "test:configure", ExpectedRevision = created.Revision,
                ActionCode = SimulationFulfillmentActionCodes.Configure,
                InitialState = 다중센터출고표본.InitialState(),
            });
            root = new GameObject("출고 UI 시험");
            var view = new GameObject("출고 카드", typeof(RectTransform)).AddComponent<다중센터출고View>();
            view.transform.SetParent(root.transform, false);
            var binding = new GameObject("출고 모형").AddComponent<다중센터출고선반Binding>();
            binding.transform.SetParent(root.transform, false);
            var controller = root.AddComponent<다중센터출고SceneController>();
            await controller.InitializeAsync(runtime, sessionId, view, binding, Slot);
            return (controller, view, binding);
        }

        [Test]
        public async Task 같은세션에서_센터예약_부분피킹_포장준비를_구분한다()
        {
            var (controller, view, binding) = await CreateUi();
            var initial = await runtime.Sessions.GetAsync(sessionId);
            Assert.That(view.예약버튼.interactable, Is.False);
            await controller.주문검토Async();
            Assert.That(view.표시후보수, Is.EqualTo(2));
            Assert.That(controller.카드.센터후보.Single(value => value.센터코드 == 다중센터출고표본.NearCenter).선택가능, Is.False);
            Assert.That(view.예약버튼.interactable, Is.True);
            Assert.That((await runtime.Sessions.GetAsync(sessionId)).Revision, Is.EqualTo(initial.Revision), "미리보기는 세션을 변경하지 않는다.");

            await controller.센터선택Async(다중센터출고표본.ReadyCenter);
            await controller.예약Async();
            var selected = controller.현재지시;
            Assert.That(selected, Is.Not.Null);
            Assert.That(binding.선택센터코드, Is.EqualTo(selected.센터코드));
            Assert.That(binding.선택위치코드, Is.EqualTo(selected.위치코드));
            Assert.That(view.주문검토버튼.interactable, Is.False);
            Assert.That(view.분할입력.interactable, Is.False);
            Assert.That(view.취소버튼.interactable, Is.True);
            Assert.That(view.포장준비버튼.interactable, Is.False);
            var reservedRevision = (await runtime.Sessions.GetAsync(sessionId)).Revision;
            await controller.센터선택Async(다중센터출고표본.ReadyCenter);
            Assert.That((await runtime.Sessions.GetAsync(sessionId)).Revision, Is.EqualTo(reservedRevision));
            Assert.That(controller.현재지시.배분코드, Is.EqualTo(selected.배분코드), "진행 중 센터 변경은 기존 예약 지시를 잃지 않는다.");
            Assert.That(view.표시안내, Does.Contain("현재 예약을 마무리"));

            view.수량입력.SetTextWithoutNotify("1.25");
            await controller.피킹Async();
            Assert.That(controller.현재지시.배분코드, Is.EqualTo(selected.배분코드));
            Assert.That(controller.현재지시.피킹수량, Is.EqualTo(1.25m));
            Assert.That(controller.현재지시.남은수량, Is.EqualTo(1.75m));
            Assert.That(view.취소버튼.interactable, Is.False);
            Assert.That(view.포장준비버튼.interactable, Is.False);
            var partial = await runtime.Sessions.GetAsync(sessionId);
            var stock = partial.Fulfillment.Stocks.Single(value => value.CenterStableId == 다중센터출고표본.ReadyCenter);
            Assert.That(stock.OnHandQuantity, Is.EqualTo(8.75m));
            Assert.That(stock.ReservedQuantity, Is.EqualTo(1.75m));

            view.수량입력.SetTextWithoutNotify("1.75");
            await controller.피킹Async();
            Assert.That(view.포장준비버튼.interactable, Is.True);
            await controller.포장준비Async();
            Assert.That(controller.카드.현재주문상태, Is.EqualTo(SimulationFulfillmentStateCodes.PackReady));
            Assert.That(view.피킹버튼.interactable, Is.False);
            Assert.That(view.주문검토버튼.interactable, Is.True);
            Assert.That(view.표시안내, Does.Contain("실제 포장 완료·기사 인계·배송 완료는 아닙니다"));
            var complete = await runtime.Sessions.GetAsync(sessionId);
            Assert.That(complete.Fulfillment.Orders, Has.Length.EqualTo(1));
            Assert.That(complete.Fulfillment.Stocks.Single(value => value.CenterStableId == 다중센터출고표본.ReadyCenter).ReservedQuantity, Is.Zero);
            await controller.저장Async();
            Assert.That(view.표시안내, Does.Contain("전용 출고 슬롯에 저장"));
            using var restored = LocalSimulationUnityRuntime.Create(saves);
            var loaded = await restored.Sessions.LoadSlotAsync(Slot);
            var saved = loaded.Restore.Session;
            Assert.That(saved.Fulfillment.Orders.Single().StateCode, Is.EqualTo(SimulationFulfillmentStateCodes.PackReady));
        }

        [Test]
        public async Task 잘못된위치는_예약을변경하지않고_입력과현재지시를_보존한다()
        {
            var (controller, view, _) = await CreateUi();
            await controller.주문검토Async(); await controller.예약Async();
            var before = await runtime.Sessions.GetAsync(sessionId);
            var allocation = controller.현재지시.배분코드;
            view.위치입력.SetTextWithoutNotify("WRONG-SHELF");
            view.수량입력.SetTextWithoutNotify("1");
            await controller.피킹Async();
            var after = await runtime.Sessions.GetAsync(sessionId);
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(controller.현재지시.배분코드, Is.EqualTo(allocation));
            Assert.That(controller.현재지시.피킹수량, Is.Zero);
            Assert.That(view.위치입력.text, Is.EqualTo("WRONG-SHELF"));
            Assert.That(view.표시안내, Does.Contain("자동으로 명령을 재전송하지 않았습니다"));
            Assert.That(controller.처리중, Is.False);

            UnityEngine.Object.DestroyImmediate(controller);
            view.피킹버튼.onClick.Invoke();
            Assert.That((await runtime.Sessions.GetAsync(sessionId)).Revision, Is.EqualTo(before.Revision), "Controller 제거 뒤 UI 이벤트가 세션 명령을 보내지 않는다.");
        }

        [Test]
        public async Task 목록과입력글자는_부모전체영역을쓰고_우상단사분면에갇히지않는다()
        {
            var (_, view, _) = await CreateUi();
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in view.GetComponentsInChildren<ScrollRect>())
            {
                Assert.That(scroll.viewport.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(scroll.viewport.anchorMax, Is.EqualTo(Vector2.one));
            }
            foreach (var input in new[] { view.위치입력, view.상품입력, view.수량입력 })
            {
                Assert.That(input.textComponent.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(input.textComponent.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            }
            var check = (RectTransform)view.분할입력.targetGraphic.transform;
            Assert.That(check.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(check.offsetMax.x, Is.EqualTo(24));
        }

        [Test]
        public void 모형은_센터와선반StableId를_그대로쓰고_다른센터선반을_거부한다()
        {
            Assert.That(관찰표현LayerContract.다중센터출고Layer,
                Is.Not.EqualTo(Ssalddel.Unity.Presentation.WorldMap.세계지구본View.GlobeLayer));
            Assert.That(관찰표현LayerContract.다중센터출고Layer,
                Is.Not.EqualTo(관찰표현LayerContract.사가정운영관찰Layer));
            root = new GameObject("분리된 가상 센터") { layer = 8 };
            var existing = new GameObject("기존 장면은 보존"); existing.transform.SetParent(root.transform, false);
            var binding = root.AddComponent<다중센터출고선반Binding>();
            var state = 다중센터출고표본.InitialState();
            binding.구성(state);
            Assert.That(binding.센터코드목록, Is.EquivalentTo(state.Centers.Select(value => value.CenterStableId)));
            Assert.That(binding.위치코드목록, Is.EquivalentTo(state.Locations.Select(value => value.LocationStableId)));
            var location = state.Locations.Single(value => value.CenterStableId == 다중센터출고표본.ReadyCenter);
            binding.선택강조(다중센터출고표본.ReadyCenter, location.LocationStableId);
            Assert.Throws<InvalidOperationException>(() => binding.선택강조(다중센터출고표본.NearCenter, location.LocationStableId));
            Assert.That(binding.선택센터코드, Is.EqualTo(다중센터출고표본.ReadyCenter));
            Assert.That(binding.선택위치코드, Is.EqualTo(location.LocationStableId));
            Assert.That(state.Stocks.All(value => value.ReservedQuantity == 0m), Is.True);
            Assert.That(root.GetComponentsInChildren<Collider>().All(value => !value.enabled), Is.True);
            Assert.That(root.GetComponentsInChildren<Renderer>().All(value => value.gameObject.layer == 8), Is.True);
            Assert.That(existing.transform.parent, Is.SameAs(root.transform));
            Assert.That(binding.관찰Bounds.size.x, Is.GreaterThan(10f));
        }
    }
}
