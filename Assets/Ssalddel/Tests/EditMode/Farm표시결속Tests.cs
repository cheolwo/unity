using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 자기 Preview의 표시와 순수 초점 판정만. canonical/전역 카메라·선택/Scope/Save를 호출하지 않는다.
    public sealed class Farm표시결속Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene scene;
        private GameObject owner;
        private Transform settlement, districts, farm;
        private GameObject map, terrain, town, battle, exampleLot;
        private 플레이어경관Controller player;
        private 단일PlayerViewController policy;
        private SimulationWorldShellPresenter shell;
        private Farm표시범위Lease lease;

        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("D382OwnedFarmViewFixture");
            owner.SetActive(false);
            SceneManager.MoveGameObjectToScene(owner, scene);
            map = Child(owner.transform, "WorldMapRoot");
            settlement = Child(owner.transform, "SettlementInteriorRoot").transform;
            settlement.gameObject.SetActive(false);
            districts = Child(settlement, "Districts").transform;
            farm = Child(districts, "FarmDistrict").transform;
            foreach (var name in Farm표시범위Lease.RequiredChildNames)
            {
                var child = Child(farm, name);
                if (name.StartsWith("FarmManagementHighlight", StringComparison.Ordinal))
                    child.SetActive(false);
            }
            terrain = Child(settlement, "Terrain");
            town = Child(districts, "TownDistrict");
            battle = Child(farm, "TacticalBattleRoot");
            battle.SetActive(false);
            exampleLot = Child(farm, "HarvestLot_Potato_001");
            player = Child(map.transform, "Player").AddComponent<플레이어경관Controller>();
            Set(player, "visualRoot", Child(player.transform, "VisualRoot").transform);
            var canvas = Child(owner.transform, "ModeCanvas").AddComponent<Canvas>();
            policy = owner.AddComponent<단일PlayerViewController>();
            policy.Configure(player, canvas, false);
            shell = Child(owner.transform, "Runtime").AddComponent<SimulationWorldShellPresenter>();
            Set(shell, "worldMapRoot", map);
            Set(shell, "settlementInteriorRoot", settlement.gameObject);
        }

        [TearDown] public void TearDown()
        {
            try
            {
                if (shell != null) shell.ReleaseFarmPresentation();
                lease?.Dispose();
                if (owner != null) Object.DestroyImmediate(owner);
            }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void Farm만표시하고_기존Lot전투와바닥은켜지않는다()
        {
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
            Assert.That(settlement.gameObject.activeSelf, Is.True);
            Assert.That(farm.gameObject.activeSelf, Is.True);
            Assert.That(terrain.activeSelf, Is.False);
            Assert.That(town.activeSelf, Is.False);
            Assert.That(battle.activeSelf, Is.False);
            Assert.That(exampleLot.activeSelf, Is.False);
            Assert.That(farm.Find("FarmManagementHighlight_0_0").gameObject.activeSelf, Is.False);
            Assert.That(map.activeSelf, Is.True);
        }

        [Test] public void 임대왕복은_전체활성과TRS를보존하고_객체를추가하지않는다()
        {
            var values = owner.GetComponentsInChildren<Transform>(true);
            var active = values.Select(value => value.gameObject.activeSelf).ToArray();
            var matrices = values.Select(value => value.localToWorldMatrix).ToArray();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
                lease.Dispose(); lease.Dispose();
            }
            Assert.That(owner.GetComponentsInChildren<Transform>(true), Is.EqualTo(values));
            Assert.That(values.Select(value => value.gameObject.activeSelf), Is.EqualTo(active));
            Assert.That(values.Select(value => value.localToWorldMatrix), Is.EqualTo(matrices));
        }

        [Test] public void 허용자식누락은_활성변경전에거부한다()
        {
            Object.DestroyImmediate(farm.Find("VisualBaseRoot").gameObject);
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("FarmViewAllowedChildMissing"));
            Assert.That(settlement.gameObject.activeSelf, Is.False);
            Assert.That(exampleLot.activeSelf, Is.True);
        }

        [Test] public void 동명자식은_임의첫자식선택대신거부한다()
        {
            Child(farm, "VisualBaseRoot");
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("FarmViewDuplicateChild"));
        }

        [Test] public void 조상RuntimeComponent는_활성화전에거부한다()
        {
            settlement.gameObject.AddComponent<BoxCollider>();
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("FarmViewAncestorHasRuntimeComponent"));
        }

        [Test] public void 허용하위Animator는_조용히실행하지않고거부한다()
        {
            farm.Find("VisualBaseRoot").gameObject.AddComponent<Animator>();
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("FarmViewUnsupportedComponent"));
        }

        [Test] public void 이미사용중인Settlement는_숨기거나인수하지않는다()
        {
            settlement.gameObject.SetActive(true);
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("FarmViewSettlementAlreadyInUse"));
            Assert.That(terrain.activeSelf, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void 임대중계층추가또는재부모화는_원래범위로간주하지않는다(bool reparent)
        {
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
            if (reparent) exampleLot.transform.SetParent(town.transform, true);
            else Child(farm, "UnreviewedChild");
            Assert.That(lease.ValidateTopology(), Is.False);
        }

        [Test] public void 반납시_외부에서바꾼제외자식값은덮어쓰지않는다()
        {
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
            town.SetActive(true);
            lease.Dispose();
            Assert.That(town.activeSelf, Is.True);
            Assert.That(settlement.gameObject.activeSelf, Is.False);
        }

        [Test] public void 외부활성부모로옮긴제외객체는_반납시켜지않는다()
        {
            var external = Child(owner.transform, "External");
            external.transform.SetParent(null, true); // 자기 Preview 안의 별도 소유자
            Assert.That(external.activeInHierarchy, Is.True);
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
            exampleLot.transform.SetParent(external.transform, true);
            Assert.That(lease.ValidateTopology(), Is.False);
            lease.Dispose(); lease.Dispose();
            Assert.That(exampleLot.activeSelf, Is.False);
            Assert.That(exampleLot.activeInHierarchy, Is.False);
            Assert.That(terrain.activeSelf, Is.True);
            Assert.That(town.activeSelf, Is.True);
            Assert.That(settlement.gameObject.activeSelf, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void 상위소유경로가옮겨지면_반납은그하위활성을쓰지않는다(bool moveOwner)
        {
            var external = Child(owner.transform, "External");
            external.transform.SetParent(null, true);
            Assert.That(Farm표시범위Lease.TryAcquire(settlement, out lease, out _), Is.True);
            (moveOwner ? owner.transform : settlement).SetParent(external.transform, true);
            var values = settlement.GetComponentsInChildren<Transform>(true);
            var active = values.Select(value => value.gameObject.activeSelf).ToArray();
            Assert.That(lease.ValidateTopology(), Is.False);
            lease.Dispose(); lease.Dispose();
            Assert.That(values.Select(value => value.gameObject.activeSelf), Is.EqualTo(active));
        }

        [Test] public void Renderer반납은_등록뒤들어온자식과옮겨진자식을건드리지않는다()
        {
            var root = farm.Find("VisualBaseRoot");
            var kept = Mesh(root, "Kept");
            var moved = Mesh(root, "Moved");
            policy.ApplyView();
            Assert.That(policy.TryRegisterRuntimeEnvironment(root, this, out _), Is.True);
            var external = Child(owner.transform, "External");
            external.transform.SetParent(null, true);
            moved.transform.SetParent(external.transform, true);
            var newlyAttached = Mesh(root, "NewExternalRenderer");
            Assert.That(policy.UnregisterRuntimeEnvironment(root, this), Is.True);
            Assert.That(kept.enabled, Is.False);
            Assert.That(moved.enabled, Is.True);
            Assert.That(newlyAttached.enabled, Is.True);
            Assert.That(policy.UnregisterRuntimeEnvironment(root, this), Is.False);
            Assert.That(moved.enabled, Is.True);
        }

        [Test] public void 허용Root자체가외부로옮겨지면_Renderer반납쓰기가없다()
        {
            var root = farm.Find("VisualBaseRoot");
            var renderer = Mesh(root, "MovedRootRenderer");
            policy.ApplyView();
            Assert.That(policy.TryRegisterRuntimeEnvironment(root, this, out _), Is.True);
            var external = Child(owner.transform, "External");
            external.transform.SetParent(null, true);
            root.SetParent(external.transform, true);
            Assert.That(policy.UnregisterRuntimeEnvironment(root, this), Is.True);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(policy.UnregisterRuntimeEnvironment(root, this), Is.False);
        }

        [Test] public void 정책이숨긴Renderer만복원하고_원래disabled는보존한다()
        {
            var visible = Mesh(farm.Find("VisualBaseRoot"), "Visible");
            var disabled = Mesh(farm.Find("VisualBaseRoot"), "AuthoredDisabled");
            disabled.enabled = false;
            policy.ApplyView();
            Assert.That(visible.enabled, Is.False);
            Assert.That(policy.TryRegisterRuntimeEnvironment(visible.transform.parent, this, out _), Is.True);
            Assert.That(visible.enabled, Is.True);
            Assert.That(disabled.enabled, Is.False);
            policy.ApplyView(); policy.ApplyView();
            Assert.That(visible.enabled, Is.True);
            Assert.That(disabled.enabled, Is.False);
        }

        [Test] public void 복원기록은한번소비하여_후속외부비활성을다시켜지않는다()
        {
            var renderer = Mesh(farm.Find("VisualBaseRoot"), "Visible");
            policy.ApplyView();
            Assert.That(policy.TryRegisterRuntimeEnvironment(renderer.transform.parent, this, out _), Is.True);
            renderer.enabled = false;
            policy.ApplyView();
            Assert.That(policy.TryRegisterRuntimeEnvironment(renderer.transform.parent, this, out _), Is.True);
            Assert.That(renderer.enabled, Is.False);
        }

        [Test] public void 외부허용소유자는_덮어쓰거나반납할수없다()
        {
            var root = farm.Find("VisualBaseRoot");
            var other = new object();
            Assert.That(policy.TryRegisterRuntimeEnvironment(root, other, out _), Is.True);
            Assert.That(policy.TryRegisterRuntimeEnvironment(root, this, out _), Is.False);
            Assert.That(policy.UnregisterRuntimeEnvironment(root, this), Is.False);
            Assert.That(policy.UnregisterRuntimeEnvironment(root, other), Is.True);
        }

        [Test] public void Shell재진입은_기준선을덮지않고_같은권위사본을유지한다()
        {
            var state = new SimulationWorldShellStateMachine(SimulationWorldShellFixture.CreateSnapshot());
            Set(shell, "stateMachine", state);
            var snapshot = shell.CurrentSnapshot;
            Assert.That(shell.TryShowFarmPresentation(out _), Is.True);
            Assert.That(shell.TryShowFarmPresentation(out _), Is.True);
            Assert.That(shell.CurrentSnapshot, Is.SameAs(snapshot));
            Assert.That(shell.WorldTick, Is.EqualTo(12));
            Assert.That(shell.WorldRevision, Is.EqualTo(12));
            shell.ReleaseFarmPresentation();
            Assert.That(settlement.gameObject.activeSelf, Is.False);
            Assert.That(exampleLot.activeSelf, Is.True);
        }

        [TestCase(-359.6f, -109.600006f, false)]
        [TestCase(-797.1f, 327.900024f, true)]
        public void 실제초기후기범위는_같은Farm초점을달리판정한다(float minX, float maxX, bool expected)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, -63, -114.75f), new Vector3(maxX, 63, 135.25f));
            Assert.That(플레이어경관Controller.IsTacticalFocusInsideBounds(
                new Vector3(-27, 1.5f, 2), bounds), Is.EqualTo(expected));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(11f)]
        public void 잘못된초점은_clamp로성공시키지않는다(float x)
            => Assert.That(플레이어경관Controller.IsTacticalFocusInsideBounds(
                new Vector3(x, 1, 0), new Bounds(Vector3.zero, Vector3.one * 20)), Is.False);

        [Test] public void 경계미제공실패는_Player모드와위치를바꾸지않는다()
        {
            var position = player.transform.position;
            var activity = player.CurrentActivityCode;
            Assert.That(player.TryEnterFarmManagementModeAt(new Vector3(-27, 1.5f, 2), out _), Is.False);
            Assert.That(player.CurrentActivityCode, Is.EqualTo(activity));
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.IsCameraTransitioning, Is.False);
        }

        private GameObject Child(Transform parent, string name)
        {
            var value = new GameObject(name);
            value.transform.SetParent(parent, false);
            return value;
        }
        private MeshRenderer Mesh(Transform parent, string name)
            => Child(parent, name).AddComponent<MeshRenderer>();
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
