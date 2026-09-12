using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 업무배달관찰Tests
    {
        private static 업무배달관찰Dto Dto() => new() {
            schemaVersion = "food-delivery-observer.r1", runId = "verification-run", revision = 3,
            status = "Running", durationSeconds = 300, elapsedSeconds = 65, orderNo = "ORDER-ONE",
            orderStatus = "접수", dispatchStatus = "배차 대기", message = "격리 검증",
            actors = new[] {
                new 업무배달관찰ActorDto { id = "customer", name = "주문자", role = "orderer", x = 16, state = "주문 조회", nextAction = "전달 대기", waitReason = "조리 중", lastAction = "주문 등록" },
                new 업무배달관찰ActorDto { id = "restaurant", name = "음식점", role = "restaurant", state = "조리 중" },
                new 업무배달관찰ActorDto { id = "driver-near", name = "가까운 기사", role = "driver", x = -8 },
                new 업무배달관찰ActorDto { id = "driver-far", name = "먼 기사", role = "driver", x = -20, z = 8 } },
            events = new[] { new 업무배달관찰EventDto { sequence = 1, actorId = "customer", action = "주문", result = "등록됨", occurredAtUtc = "2026-09-07T00:00:00Z" } } };
        private static 업무배달관찰Model Model() => 업무배달관찰ApiMapper.Map(Dto());

        [Test] public void 메뉴선택은_이전계약을_유지하며_선택근거와_가격종류를_분리한다()
        {
            Assert.That(Model().MenuSelection, Is.Null);
            var dto = Dto(); dto.menuSelection = new 업무배달메뉴선택Dto { snapshotHash = new string('a',64), recipeId = 7,
                menuId = 2, title = "된장국", reason = "예산 내 선호", salePrice = 5000, priceBasis = "검증용 합성 판매가",
                reference = "KAMIS 2026-07-30 · 오래된 참고값", source = "식약처", candidates = new[] {"된장국: 선택 가능"} };
            var model = 업무배달관찰ApiMapper.Map(dto);
            Assert.That(model.MenuSelection.Title, Is.EqualTo("된장국"));
            Assert.That(model.MenuSelection.Reference, Does.Contain("오래된"));
            var store = new 업무배달관찰Store(); Assert.That(store.Accept(model,DateTime.UtcNow), Is.True);
            dto.menuSelection.menuId = 3;
            Assert.That(store.Accept(업무배달관찰ApiMapper.Map(dto),DateTime.UtcNow), Is.False);
            dto.menuSelection.salePrice = double.NaN;
            Assert.Throws<FormatException>(()=>업무배달관찰ApiMapper.Map(dto));
        }
        [Test] public void 메뉴조건대기는_주문없는_종료상태로_표시한다()
        {
            var dto = Dto(); dto.status = "Waiting"; dto.orderNo = "";
            var model = 업무배달관찰ApiMapper.Map(dto);
            Assert.That(model.IsTerminal, Is.True); Assert.That(model.OrderNo, Is.Empty);
            Assert.That(업무배달관찰Store.상태이름(model.Status), Does.Contain("주문 없음"));
        }

        [Test] public void 서버계약을_Simulation상태가_아닌_관찰결과로_매핑한다()
        {
            var model = Model(); Assert.That(model.OrderNo, Is.EqualTo("ORDER-ONE"));
            Assert.That(model.Actors.Length, Is.EqualTo(4)); Assert.That(model.Actors[0].LastAction, Is.EqualTo("주문 등록"));
            Assert.That(model.Actors[3].X, Is.EqualTo(-20)); Assert.That(model.Events[0].ActorId, Is.EqualTo("customer"));
        }
        [Test] public void 다른Schema와_중복주체와_역할불일치와_비정상좌표는_거부한다()
        {
            var dto = Dto(); dto.schemaVersion = "other"; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
            dto = Dto(); dto.actors[1].id = "customer"; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
            dto = Dto(); dto.actors[1].role = "driver"; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
            dto = Dto(); dto.actors[2].x = float.NaN; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
        }
        [Test] public void 중복이벤트와_다른검증시간은_거부한다()
        {
            var dto = Dto(); dto.events = new[] { dto.events[0], dto.events[0] }; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
            dto = Dto(); dto.durationSeconds = 1800; Assert.Throws<FormatException>(() => 업무배달관찰ApiMapper.Map(dto));
        }
        [Test] public void 실행전_서버준비실패도_표본대체없이_표시할_수_있다()
        {
            var dto = Dto(); dto.status = "Failed"; dto.runId = ""; dto.orderNo = "";
            dto.message = "격리 DB 준비 실패"; dto.elapsedSeconds = 0;
            var model = 업무배달관찰ApiMapper.Map(dto);
            Assert.That(model.Status, Is.EqualTo("Failed")); Assert.That(model.Message, Is.EqualTo(dto.message)); Assert.That(model.OrderNo, Is.Empty);
        }
        [Test] public void 낮은판본이나_다른주문번호는_마지막성공을_교체하지_않는다()
        {
            var store = new 업무배달관찰Store(); var first = Model(); store.Accept(first, DateTime.UtcNow);
            var incoming = Model(); incoming.Revision = 2; Assert.That(store.Accept(incoming, DateTime.UtcNow), Is.False);
            incoming = Model(); incoming.Revision = 4; incoming.OrderNo = "OTHER";
            Assert.That(store.Accept(incoming, DateTime.UtcNow), Is.False); Assert.That(store.State, Is.SameAs(first));
        }
        [Test] public void 다른실행으로_자동전환하지_않고_명시적시작만_허용한다()
        {
            var store = new 업무배달관찰Store(); var old = Model(); old.Status = "Idle"; store.Accept(old, DateTime.UtcNow);
            var next = Model(); next.RunId = "new-run"; next.OrderNo = "NEW-ORDER";
            Assert.That(store.Accept(next, DateTime.UtcNow), Is.False);
            store.AllowExplicitStart(); Assert.That(store.Accept(next, DateTime.UtcNow), Is.True);
        }
        [Test] public void 조회중복을_막고_실패와_오래된결과를_표시한다()
        {
            var store = new 업무배달관찰Store(); var now = DateTime.UtcNow;
            store.Accept(Model(), now); Assert.That(store.TryBegin(), Is.True); Assert.That(store.TryBegin(), Is.False); store.End();
            Assert.That(store.TryBegin(), Is.True); store.End();
            Assert.That(store.ConnectionLabel(now.AddSeconds(5)), Does.Contain("오래된 조회"));
            store.Fail("서버 연결 실패"); Assert.That(store.State.OrderNo, Is.EqualTo("ORDER-ONE"));
            Assert.That(store.ConnectionLabel(now.AddSeconds(6)), Does.Contain("갱신 실패"));
        }
        [TestCase("Failed")]
        [TestCase("Completed")]
        public void 종료된실행에서_새Idle은_명시적조회확인_후에만_수용한다(string status)
        {
            var now = DateTime.UtcNow; var store = new 업무배달관찰Store(); var previous = Model(); previous.Status = status;
            store.Accept(previous, now); var idle = Model(); idle.Status = "Idle"; idle.RunId = ""; idle.OrderNo = ""; idle.Revision = 1;
            Assert.That(store.Accept(idle, now), Is.False); Assert.That(store.State, Is.SameAs(previous));
            Assert.That(store.PendingIdle, Is.SameAs(idle)); Assert.That(store.CanAcceptPendingIdle(now.AddSeconds(4)), Is.False);
            Assert.That(store.AcceptPendingIdle(now), Is.True); Assert.That(store.State.Status, Is.EqualTo("Idle"));
            Assert.That(store.State.OrderNo, Is.Empty); Assert.That(store.InFlight, Is.False);
        }
        [Test] public void 실행중에는_새Idle교체가_없고_재연결된_같은실행은_그대로_갱신된다()
        {
            var now = DateTime.UtcNow; var store = new 업무배달관찰Store(); store.Accept(Model(), now);
            var idle = Model(); idle.Status = "Idle"; idle.RunId = ""; idle.OrderNo = ""; idle.Revision = 1;
            Assert.That(store.Accept(idle, now), Is.False); Assert.That(store.PendingIdle, Is.Null); Assert.That(store.AcceptPendingIdle(now), Is.False);
            Assert.Throws<InvalidOperationException>(() => store.AllowExplicitStart());
            var incoming = Model(); incoming.Revision++; Assert.That(store.Accept(incoming, now), Is.True); Assert.That(store.Error, Is.Empty);
        }
        [Test] public void 업무서버전용잠금은_기존월드전환_입력을_실행하지_않는다()
        {
            var root = new GameObject("전환 차단 시험"); root.SetActive(false);
            try {
                var modes = root.AddComponent<통합월드ModePresenter>(); modes.업무서버관찰전용으로잠금();
                Assert.DoesNotThrow(() => { modes.ShowWorldOverview(); modes.ShowNatureExploration(); modes.ShowFarmFirstPerson(); modes.ShowFarmTactical(); modes.ShowJinbuInbound(); });
                Assert.That(modes.CurrentModeCode, Is.EqualTo(통합월드ModeCodes.WorldOverview));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void 새서버조회확인_카드는_시작명령과_분리된다()
        {
            var root = new GameObject("새 서버 확인 시험");
            try {
                var store = new 업무배달관찰Store(); var now = DateTime.UtcNow; var previous = Model(); previous.Status = "Failed"; store.Accept(previous, now);
                var idle = Model(); idle.Status = "Idle"; idle.RunId = ""; idle.OrderNo = ""; idle.Revision = 1; store.Accept(idle, now);
                var phone = root.AddComponent<업무배달관찰휴대폰View>(); phone.Configure(store); phone.Shell.열기();
                int starts = 0; phone.StartRequested += () => starts++;
                root.GetComponentsInChildren<Button>().Single(b => b.name == "관찰 카드 · 새 검증 서버 결과 확인").onClick.Invoke();
                Assert.That(store.State.Status, Is.EqualTo("Idle")); Assert.That(starts, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "관찰 카드 · 5분 검증 시작"), Is.True);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void 업무서버프로필은_LocalRuntime설치를_명시적으로_차단한다()
        {
            var previous = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var installed = SimulationWorldLocalRuntimeScope.IsInstalled;
            try {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", "6");
                Assert.That(SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested, Is.True);
                Assert.Throws<InvalidOperationException>(() => SimulationWorldLocalRuntimeScope.Install());
                Assert.That(SimulationWorldLocalRuntimeScope.IsInstalled, Is.EqualTo(installed));
            } finally { Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", previous); }
        }
        [Test] public void 업무서버프로필은_Editor또는DevelopmentBuild에서만_허용한다()
        {
            Assert.That(SimulationWorldLocalRuntimeScope.CanUseBusinessServerObserver(true, false), Is.True);
            Assert.That(SimulationWorldLocalRuntimeScope.CanUseBusinessServerObserver(false, true), Is.True);
            Assert.That(SimulationWorldLocalRuntimeScope.CanUseBusinessServerObserver(false, false), Is.False);
        }
        [Test] public void 업무서버휴대폰은_가상Npc관찰과_실제기사배차없음을_표시한다()
        {
            var root = new GameObject("권위 경계 표시 시험");
            try {
                var store = new 업무배달관찰Store(); store.Accept(Model(), DateTime.UtcNow);
                var phone = root.AddComponent<업무배달관찰휴대폰View>(); phone.Configure(store); phone.Shell.열기();
                var texts = root.GetComponentsInChildren<Text>().Select(text => text.text).ToArray();
                Assert.That(texts.Any(text => text.Contains("가상 NPC 관찰 · 실제 기사 배차 없음")), Is.True);
                Assert.That(texts.Any(text => text.Contains("실제 기사 위치·수락·픽업·전달·정산은 실행하지 않습니다")), Is.True);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase("http://127.0.0.1:5215", true)]
        [TestCase("http://127.0.0.1:5215/", true)]
        [TestCase("https://example.com", false)]
        [TestCase("http://127.0.0.1:5000", false)]
        [TestCase("http://secret@127.0.0.1:5215", false)]
        [TestCase("http://127.0.0.1:5215/path", false)]
        public void 격리검증주소만_허용한다(string address, bool expected)
            => Assert.That(업무배달관찰ApiAdapter.AllowedEndpoint(address), Is.EqualTo(expected));
        [UnityTest] public IEnumerator 연결파일이_없으면_네트워크나_fallback없이_실패한다()
        {
            var handler = new FakeHandler(); using var api = new 업무배달관찰ApiAdapter(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), handler);
            var request = api.조회Async(CancellationToken.None); var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!request.IsCompleted && DateTime.UtcNow < deadline) yield return null;
            Assert.That(request.IsCompleted, Is.True, "연결 파일 검사 완료 시간 초과");
            Assert.Throws<FileNotFoundException>(() => request.GetAwaiter().GetResult());
            Assert.That(handler.Methods.Count, Is.Zero);
        }
        [UnityTest] public IEnumerator 명시적시작_POST뒤에는_같은API를_GET재조회한다()
        {
            var path = Path.Combine(Path.GetTempPath(), "observer-contract-" + Guid.NewGuid() + ".json");
            File.WriteAllText(path, "{\"baseUrl\":\"http://127.0.0.1:5215\",\"accessKey\":\"test-only-not-a-secret\"}");
            try {
                var handler = new FakeHandler(); using var api = new 업무배달관찰ApiAdapter(path, handler);
                var request = api.시작Async(CancellationToken.None);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!request.IsCompleted && DateTime.UtcNow < deadline) yield return null;
                Assert.That(request.IsCompleted, Is.True, "가짜 HTTP 요청 완료 시간 초과");
                var model = request.GetAwaiter().GetResult();
                Assert.That(handler.Methods, Is.EqualTo(new[] { "POST", "GET" }));
                Assert.That(handler.Paths, Is.EqualTo(new[] { "/verification/food-delivery/start", "/verification/food-delivery" }));
                Assert.That(handler.HeaderPresent, Is.True); Assert.That(model.Status, Is.EqualTo("Idle"));
            } finally { File.Delete(path); }
        }
        [Test] public void 휴대폰_갱신은_NPC선택을_바꾸지_않고_같은주문을_표시한다()
        {
            var root = new GameObject("업무 서버 휴대폰 시험");
            try {
                var store = new 업무배달관찰Store(); store.Accept(Model(), DateTime.UtcNow);
                var phone = root.AddComponent<업무배달관찰휴대폰View>(); phone.Configure(store);
                int starts = 0; phone.StartRequested += () => starts++;
                phone.SelectActor("customer"); var count = phone.Shell.CreatedRowCount;
                for (int i = 0; i < 10; i++) phone.Refresh();
                Assert.That(phone.SelectedActorId, Is.EqualTo("customer")); Assert.That(phone.Shell.CreatedRowCount, Is.EqualTo(count));
                Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("ORDER-ONE")), Is.True);
                Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("조리 중")), Is.True);
                phone.Shell.닫기(); phone.Shell.열기(); Assert.That(phone.SelectedActorId, Is.EqualTo("customer"));
                phone.Shell.홈(); Assert.That(phone.SelectedActorId, Is.Empty); Assert.That(phone.Shell.IsOpen, Is.True);
                phone.Shell.홈(); Assert.That(phone.Shell.IsOpen, Is.True); phone.Shell.뒤로(); Assert.That(phone.Shell.IsOpen, Is.False);
                Assert.That(starts, Is.Zero);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void 시작은_Idle의_명시적버튼에서만_요청된다()
        {
            var root = new GameObject("명시적 시작 시험");
            try {
                var model = Model(); model.Status = "Idle"; model.RunId = ""; model.OrderNo = "";
                var store = new 업무배달관찰Store(); store.Accept(model, DateTime.UtcNow);
                var phone = root.AddComponent<업무배달관찰휴대폰View>(); phone.Configure(store); phone.Shell.열기();
                int starts = 0; phone.StartRequested += () => starts++;
                phone.Refresh(); Assert.That(starts, Is.Zero);
                var button = root.GetComponentsInChildren<Button>().Single(b => b.name == "관찰 카드 · 5분 검증 시작");
                Assert.That(button.interactable, Is.True); button.onClick.Invoke(); Assert.That(starts, Is.EqualTo(1));
                store.TryBegin(); phone.Refresh(); Assert.That(button.interactable, Is.False); store.End();
                model.Status = "Running"; phone.Refresh();
                Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "관찰 카드 · 5분 검증 시작"), Is.False);
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private sealed class FakeHandler : HttpMessageHandler
        {
            public readonly System.Collections.Generic.List<string> Methods = new(), Paths = new();
            public bool HeaderPresent = true;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
            {
                Methods.Add(request.Method.Method); Paths.Add(request.RequestUri.AbsolutePath);
                HeaderPresent &= request.Headers.Contains("X-Verification-Key");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                    "{\"schemaVersion\":\"food-delivery-observer.r1\",\"runId\":\"\",\"revision\":0,\"status\":\"Idle\",\"durationSeconds\":300,\"actors\":[],\"events\":[]}") });
            }
        }
    }
}
