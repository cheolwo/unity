using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Application;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 역방어준비Tests
    {
        private GameObject root;
        private 역방어준비View view;
        private 사가정운영디오라마View map;
        [SetUp] public void Setup()
        {
            root=new GameObject("defense-test");root.AddComponent<운영관찰WorldView>();
            map=root.AddComponent<사가정운영디오라마View>();map.Initialize();
            view=root.AddComponent<역방어준비View>();
            var catalog=view.LoadCatalog();
            Assert.That(catalog,Is.Not.Null,"동결 지도와 옥상 기호 후보 검사 실패");
            view.Configure(역방어준비RuntimeFactory.Create("test-session",catalog.mapRevision,catalog.mapHash,catalog.slots.Select(x=>x.id)));
        }
        [TearDown] public void Teardown(){if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        [Test] public void 준비레이어는_기본비활성이며_운영카메라를재사용한다()
        {Assert.That(view.IsOpen,Is.False);view.SetOpen(true);Assert.That(view.IsOpen,Is.True);Assert.That(map.GameOverlayVisible,Is.True);Assert.That(root.GetComponentsInChildren<Camera>(true).Length,Is.EqualTo(1));view.SetOpen(false);Assert.That(map.GameOverlayVisible,Is.False);}
        [Test] public void 선택취소는_인원을생성하지않는다()
        {view.SetOpen(true);Assert.That(view.SelectSlot("synthetic:defense-roof:a"),Is.True);Assert.That(view.Snapshot.Revision,Is.Zero);view.CancelSelection();Assert.That(view.UnitTransform.gameObject.activeSelf,Is.False);Assert.That(view.Confirm(),Is.False);}
        [Test] public void 확인한자리만_같은판본의인원으로표현한다()
        {view.SetOpen(true);view.SelectSlot("synthetic:defense-roof:a");Assert.That(view.Confirm(),Is.True);Assert.That(view.Snapshot.ActionCount,Is.EqualTo(1));Assert.That(view.UnitTransform.localPosition.x,Is.EqualTo(684.65656f).Within(.01));Assert.That(view.UnitTransform.localPosition.y,Is.EqualTo(47.5f).Within(.01));Assert.That(view.Confirm(),Is.True);Assert.That(view.Snapshot.ActionCount,Is.EqualTo(1));}
        [Test] public void 재배치와모드전환은_중복인원을만들지않는다()
        {view.SetOpen(true);view.SelectSlot("synthetic:defense-roof:a");view.Confirm();view.SelectSlot("synthetic:defense-roof:b");view.Confirm();view.SetOpen(false);view.SetOpen(true);Assert.That(view.Snapshot.AssignedSlotId,Is.EqualTo("synthetic:defense-roof:b"));Assert.That(view.UnitTransform.localPosition.x,Is.EqualTo(503.98993f).Within(.01));Assert.That(root.GetComponentsInChildren<Transform>(true).Count(x=>x.name=="방어 인원 · 확대 기호"),Is.EqualTo(1));}
        [Test] public void 미등록자리는_선택을거절한다()
        {view.SetOpen(true);Assert.That(view.SelectSlot("unknown"),Is.False);Assert.That(view.Snapshot.Revision,Is.Zero);}
        [Test] public void 종료는_게임표현을제거하고_운영표시를복구한다()
        {view.SetOpen(true);view.SelectSlot("synthetic:defense-roof:a");view.Confirm();view.Configure(null);Assert.That(view.IsOpen,Is.False);Assert.That(map.GameOverlayVisible,Is.False);Assert.That(view.UnitTransform,Is.Null);}
        [Test] public void 지도해시불일치는_배치를차단한다()
        {var data=view.LoadCatalog();data.mapHash=new string('0',64);var source=Resources.Load<TextAsset>("SagajeongReference");Assert.Throws<ArgumentException>(()=>역방어배치검사.Validate(data,JsonUtility.FromJson<공간참고자료>(source.text),source.bytes));}
        [Test] public void 다각형밖과경계근처는_옥상후보에서제외한다()
        {var points=new[]{new 참고점{x=0,z=0},new 참고점{x=40,z=0},new 참고점{x=40,z=40},new 참고점{x=0,z=40},new 참고점{x=0,z=0}};Assert.That(역방어배치검사.InsideWithClearance(points,20,20,12),Is.True);Assert.That(역방어배치검사.InsideWithClearance(points,3,3,12),Is.False);Assert.That(역방어배치검사.InsideWithClearance(points,60,20,12),Is.False);}
    }
}
