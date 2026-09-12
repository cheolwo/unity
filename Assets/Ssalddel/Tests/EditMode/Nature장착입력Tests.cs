using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.PlayerActivities;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 소유 Preview의 hit 처리/권위 횟수와 GUI 표시 경로 검사.
    // OS 입력이나 IMGUI 이벤트 순서 재현, 실제 손도끼 품질 검증은 아니다.
    public sealed class Nature장착입력Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene scene;
        private GameObject root;
        private Nature생존Controller controller;
        private 플레이어경관Controller player;
        private Nature생존CoreRuntimeAdapter engine;
        private CursorLockMode cursorLock;
        private bool cursorVisible;

        [SetUp] public void SetUp()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Owned_NatureEquipmentInput");
            SceneManager.MoveGameObjectToScene(root, scene);
            player = root.AddComponent<플레이어경관Controller>();
            controller = root.AddComponent<Nature생존Controller>();
            SetActivity(PlayerActivityCodes.Exploration);
            Set("player", player);
            Assert.That(player.TryBindNatureHud(controller), Is.True);
            // Scope/환경변수/기존 슬롯을 읽거나 저장하지 않는 새 메모리 Runtime.
            engine = new Nature생존CoreRuntimeAdapter(LocalSimulationUnityRuntime.Create(
                Path.Combine(Path.GetTempPath(), "ssalddel-equipment-fixture", Guid.NewGuid().ToString("N"))));
            engine.AcquireAxe();
            Set("engine", engine);
        }
        [TearDown] public void TearDown()
        {
            try
            {
                Set("engine", null);
                engine?.Dispose();
                if (root != null) Object.DestroyImmediate(root);
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            }
        }
        private void Set(string name, object value) => typeof(Nature생존Controller).GetField(name, Private).SetValue(controller, value);
        private void SetActivity(string value) => typeof(플레이어경관Controller).GetField("_currentActivityCode", Private).SetValue(player, value);
        private bool Hit(string method, Vector2 point) => (bool)typeof(Nature생존Controller).GetMethod(method, Private).Invoke(controller, new object[] { point });
        private static Rect RectOf(string method, params object[] args) => (Rect)typeof(Nature생존Controller).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private bool Toggle() => Hit(controller.EquipmentPanelOpen ? "HandleEquipmentPrimaryPressed" : "HandleHudPrimaryPressed", RectOf("EquipmentToggleButtonGuiRect").center);

        [Test] public void 한번누름마다_열기닫기만하며_권위개정은바꾸지않는다()
        {
            var revision = engine.Snapshot().Revision;
            Assert.That(Toggle(), Is.True); Assert.That(controller.EquipmentPanelOpen, Is.True);
            Assert.That(Toggle(), Is.True); Assert.That(controller.EquipmentPanelOpen, Is.False);
            Assert.That(Toggle(), Is.True); Assert.That(controller.EquipmentPanelOpen, Is.True);
            Assert.That(engine.Snapshot().Revision, Is.EqualTo(revision));
        }
        [Test] public void 장착엔진미지원이어도_열린패널상단을닫는다()
        {
            Toggle(); Set("engine", null);
            Assert.That(Toggle(), Is.True); Assert.That(controller.EquipmentPanelOpen, Is.False);
        }
        [Test] public void 진단확장시_숨은상단토글은동작하지않는다()
        {
            Toggle(); Set("diagnosticHudExpanded", true);
            Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentToggleButtonGuiRect").center);
            Assert.That(controller.EquipmentPanelOpen, Is.True);
        }
        [Test] public void Nature문맥을떠나면_숨은패널입력을거부한다()
        {
            Toggle(); var before = engine.Snapshot().Revision;
            SetActivity(PlayerActivityCodes.WorldOverview);
            Assert.That(Toggle(), Is.False);
            Assert.That(Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentInventoryButtonGuiRect", 0).center), Is.False);
            Assert.That(engine.Snapshot().Revision, Is.EqualTo(before));
        }
        [Test] public void 닫힌패널의_물품좌표는장착하지않는다()
        {
            Assert.That(Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentInventoryButtonGuiRect", 0).center), Is.False);
            Assert.That(engine.ActorEquipmentSnapshot().CapabilityCodes, Is.Empty);
        }
        [Test] public void 패널빈곳은소비하고_바깥은거부한다()
        {
            Toggle(); var before = engine.Snapshot().Revision;
            var panel = RectOf("EquipmentPanelGuiRect");
            Assert.That(Hit("HandleEquipmentPrimaryPressed", new Vector2(panel.x + 4, panel.y + 4)), Is.True);
            Assert.That(Hit("HandleEquipmentPrimaryPressed", new Vector2(-20, -20)), Is.False);
            Assert.That(engine.Snapshot().Revision, Is.EqualTo(before));
        }
        [Test] public void 장착과해제는_각누름처리에서한번씩개정된다()
        {
            Toggle(); var before = engine.ActorEquipmentSnapshot();
            Assert.That(Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentInventoryButtonGuiRect", 0).center), Is.True);
            var equipped = engine.ActorEquipmentSnapshot();
            Assert.That(equipped.EquipmentRevision, Is.EqualTo(before.EquipmentRevision + 1));
            var slot = Array.FindIndex(equipped.Slots, value => !string.IsNullOrEmpty(value.EquippedItemInstanceStableId));
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentSlotButtonGuiRect", slot).center), Is.True);
            Assert.That(engine.ActorEquipmentSnapshot().EquipmentRevision, Is.EqualTo(equipped.EquipmentRevision + 1));
            Assert.That(engine.ActorEquipmentSnapshot().Slots.All(value => string.IsNullOrEmpty(value.EquippedItemInstanceStableId)), Is.True);
        }
        [TestCase("private void OnGUI()", "private void SaveWorld()")]
        [TestCase("private void DrawEquipmentPanel()", "private void TryChangeEquipment(")]
        public void GUI표시본문은_토글과장착명령을호출하지않는다(string start, string end)
        {
            // 명시적 소스 구조 검사이며 실제 GUI 이벤트 재현으로 세지 않는다.
            var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "Ssalddel/Presentation/World/Nature생존Controller.cs"));
            var from = source.IndexOf(start, StringComparison.Ordinal);
            var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
            var body = source.Substring(from, to - from);
            Assert.That(body, Does.Not.Contain("SetEquipmentPanelOpen("));
            Assert.That(body, Does.Not.Contain("ToggleDiagnosticHud("));
            Assert.That(body, Does.Not.Contain("TryChangeEquipment("));
            Assert.That(body, Does.Not.Contain("actorEquipment.Equip("));
            Assert.That(body, Does.Not.Contain("actorEquipment.Unequip("));
        }

        private static Vector2 ScreenPoint(Vector2 guiPoint)
            => new(guiPoint.x, Screen.height - guiPoint.y);

        private Rect DiagnosticRect() => (Rect)typeof(Nature생존Controller)
            .GetMethod("DiagnosticToggleButtonGuiRect", Private).Invoke(controller, null);

        [Test] public void 진단클릭열닫기는_상태표시만바꾸고_권위개정은보존한다()
        {
            var revision = engine.Snapshot().Revision;
            Assert.That(Hit("HandleHudPrimaryPressed", DiagnosticRect().center), Is.True);
            Assert.That(controller.DiagnosticHudExpanded, Is.True);
            Assert.That(controller.BlocksWorldPointerForEquipment, Is.True);
            Assert.That(Hit("HandleHudPrimaryPressed", DiagnosticRect().center), Is.True);
            Assert.That(controller.DiagnosticHudExpanded, Is.False);
            Assert.That(controller.BlocksWorldPointerForEquipment, Is.True);
            Assert.That(engine.Snapshot().Revision, Is.EqualTo(revision));
        }

        [Test] public void 장착패널뒤에숨은진단버튼은_동작하지않는다()
        {
            Toggle();
            Assert.That(Hit("HandleHudPrimaryPressed", DiagnosticRect().center), Is.False);
            Assert.That(controller.DiagnosticHudExpanded, Is.False);
            Assert.That(controller.EquipmentPanelOpen, Is.True);
        }

        [Test] public void 진단버튼은_Player선행조회에서UI이고_닫기전이도보호한다()
        {
            Assert.That(player.IsNaturePresentationPointerBlocked(ScreenPoint(DiagnosticRect().center)), Is.True);
            Hit("HandleHudPrimaryPressed", DiagnosticRect().center);
            Set("equipmentUiTransitionFrame", Time.frameCount - 1);
            Assert.That(player.IsNaturePresentationPointerBlocked(ScreenPoint(DiagnosticRect().center)), Is.True);
            Hit("HandleHudPrimaryPressed", DiagnosticRect().center);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.True);
            Set("equipmentUiTransitionFrame", Time.frameCount - 1);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.False);
        }

        [Test] public void 숨은문맥이나다른소유자의진단클릭은_표시를바꾸지않는다()
        {
            SetActivity(PlayerActivityCodes.WorldOverview);
            Assert.That(Hit("HandleHudPrimaryPressed", DiagnosticRect().center), Is.False);
            SetActivity(PlayerActivityCodes.Exploration);
            player.ReleaseNatureHud(controller);
            Assert.That(player.TryBindNatureHud(CreateOtherOwner()), Is.True);
            Assert.That(Hit("HandleHudPrimaryPressed", DiagnosticRect().center), Is.False);
            Assert.That(controller.DiagnosticHudExpanded, Is.False);
        }
        private static Vector2 WorldPoint => new(-20f, Screen.height / 2f);
        private Nature생존Controller CreateOtherOwner()
        {
            // Controller는 DisallowMultipleComponent이므로 같은 Actor에 중복 추가하지 않는다.
            var otherRoot = new GameObject("Owned_OtherNatureHud");
            otherRoot.transform.SetParent(root.transform, false);
            var other = otherRoot.AddComponent<Nature생존Controller>();
            Assert.That(other, Is.Not.Null);
            return other;
        }

        [Test] public void Player선행조회도_닫힌상단클릭을월드포인터로보내지않는다()
        {
            var point = ScreenPoint(RectOf("EquipmentToggleButtonGuiRect").center);
            Assert.That(player.IsNaturePresentationPointerBlocked(point), Is.True);
            Assert.That(controller.EquipmentPanelOpen, Is.False);
            Toggle();
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.True);
        }
        [Test] public void Nature선행닫기당일은보호하고_다음프레임세계클릭은허용한다()
        {
            Toggle(); Toggle();
            Assert.That(controller.EquipmentPanelOpen, Is.False);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.True);
            Set("equipmentUiTransitionFrame", Time.frameCount - 1);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.False);
            Assert.That(플레이어경관Controller.ShouldCaptureFirstPersonCursor(true,
                player.IsNaturePresentationPointerBlocked(WorldPoint)), Is.True);
        }
        [Test] public void 패널이열린동안은_프레임이달라져도전체포인터를보호한다()
        {
            Toggle(); Set("equipmentUiTransitionFrame", Time.frameCount - 1);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.True);
        }
        [Test] public void 숨은Nature문맥은_열린상태가남아도포인터를점유하지않는다()
        {
            Toggle(); SetActivity(PlayerActivityCodes.WorldOverview);
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.False);
        }
        [Test] public void 비활성창과자기소유해제는_다른창을해제하지않는다()
        {
            Toggle(); controller.enabled = false;
            Assert.That(player.IsNaturePresentationPointerBlocked(WorldPoint), Is.False);
            // EditMode에서는 자동 OnDisable 실행을 전제하지 않고 소유해제 API를 검사한다.
            player.ReleaseNatureHud(controller);
            var other = CreateOtherOwner();
            Assert.That(player.TryBindNatureHud(other), Is.True);
            Assert.That(player.ReleaseNatureHud(controller), Is.False);
            Assert.That(player.TryBindNatureHud(controller), Is.False);
            Assert.That(player.ReleaseNatureHud(other), Is.True);
        }
        [Test] public void 타창은기존소유를덮어쓰거나해제하지못한다()
        {
            var other = CreateOtherOwner();
            Assert.That(player.TryBindNatureHud(other), Is.False);
            Assert.That(player.ReleaseNatureHud(other), Is.False);
            Assert.That(player.TryBindNatureHud(controller), Is.True);
        }
        [Test] public void 이전창재활성결속실패는_표시입력을막고새소유자를보존한다()
        {
            Toggle();
            typeof(Nature생존Controller).GetMethod("OnDisable", Private).Invoke(controller, null);
            var other = CreateOtherOwner();
            Assert.That(player.TryBindNatureHud(other), Is.True);
            typeof(Nature생존Controller).GetMethod("OnEnable", Private).Invoke(controller, null);
            Assert.That(player.OwnsNatureHud(other), Is.True);
            Assert.That(controller.IsPresentationPointerBlocked(WorldPoint), Is.False);
            Assert.That(typeof(Nature생존Controller).GetProperty("NatureHudVisible", Private).GetValue(controller), Is.False);
            Assert.That(Toggle(), Is.False);
            Assert.That(Hit("HandleEquipmentPrimaryPressed", RectOf("EquipmentInventoryButtonGuiRect", 0).center), Is.False);
            Assert.That(engine.ActorEquipmentSnapshot().CapabilityCodes, Is.Empty);
            typeof(Nature생존Controller).GetMethod("OnDisable", Private).Invoke(controller, null);
            Assert.That(player.OwnsNatureHud(other), Is.True);
        }
        [TestCase(false, true, false, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(false, false, true, false)]
        public void Player선행키전이조회는_기존눌림정책을소비하지않는다(
            bool open, bool inventory, bool escape, bool expected)
        {
            Set("equipmentPanelOpen", open);
            var method = typeof(Nature생존Controller).GetMethod("EquipmentKeyTransitionPending",
                Private, null, new[] { typeof(bool), typeof(bool) }, null);
            Assert.That(method.Invoke(controller, new object[] { inventory, escape }), Is.EqualTo(expected));
            Assert.That(controller.EquipmentPanelOpen, Is.EqualTo(open));
            Set("inventoryKeyWasHeld", inventory); Set("escapeKeyWasHeld", escape);
            Assert.That(method.Invoke(controller, new object[] { inventory, escape }), Is.False);
        }
        [Test] public void 삼인칭패널보호는_기존선택제스처만취소한다()
        {
            var controls = root.GetComponent<플레이어전술조작Controller>();
            if (controls == null) controls = root.AddComponent<플레이어전술조작Controller>();
            typeof(플레이어전술조작Controller).GetField("selectionPointerActive", Private).SetValue(controls, true);
            typeof(플레이어전술조작Controller).GetField("selectionDragging", Private).SetValue(controls, true);
            Toggle();
            var before = player.transform.position;
            var method = typeof(플레이어경관Controller).GetMethod("ProtectNatureTacticalPointer", Private);
            Assert.That(method.Invoke(player, new object[] { WorldPoint, controls }), Is.True);
            Assert.That(controls.SelectionPointerActive, Is.False);
            Assert.That(controls.SelectionDragging, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(before));
        }
    }
}
