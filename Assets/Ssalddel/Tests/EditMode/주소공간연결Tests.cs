using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Bootstrap;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 동결 로컬 DB 재조회 사본과 실제 OSM JSON 연결 검사. Play/Game View 증거는 아니다.
    public sealed class 주소공간연결Tests
    {
        private static TextAsset MapAsset()=>Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map()=>JsonUtility.FromJson<공간참고자료>(MapAsset().text);
        private static 주소공간연결자료 Data()=>JsonUtility.FromJson<주소공간연결자료>(Encoding.UTF8.GetString(
            주소공간연결검사.ReadFrozen(면목동주소연결View.LocalPath,MapAsset().bytes)));
        [Test] public void 동결사본213행을_121건물에_연결하고_나머지는_보존한다()
        {
            var data=Data();var map=Map();주소공간연결검사.Validate(data,map);
            Assert.That(data.inputCount,Is.EqualTo(2006));Assert.That(data.connectedCount,Is.EqualTo(213));
            Assert.That(data.groups.Length,Is.EqualTo(121));Assert.That(data.unlinkedCount,Is.EqualTo(1793));
            Assert.That(data.links.Count(l=>l.result=="MultipleBuildingsForAddress"),Is.EqualTo(22));
            Assert.That(data.links.Count(l=>l.result=="AddressMissing"),Is.EqualTo(45));
            Assert.That(data.groups.SelectMany(g=>g.records).Count(r=>r.kind=="restaurant"),Is.EqualTo(145));
            Assert.That(data.groups.SelectMany(g=>g.records).Count(r=>r.kind=="clinic"),Is.EqualTo(63));
        }
        [Test] public void 검사와재조회는_지도와연결입력을_변경하지않는다()
        {
            var data=Data();var map=Map();var before=JsonUtility.ToJson(data);var beforeMap=JsonUtility.ToJson(map);
            주소공간연결검사.Validate(data,map);주소공간연결검사.Validate(data,map);
            Assert.That(JsonUtility.ToJson(data),Is.EqualTo(before));Assert.That(JsonUtility.ToJson(map),Is.EqualTo(beforeMap));
            Assert.That(JsonUtility.ToJson(Data()),Is.EqualTo(before));Assert.That(map.buildings.Length,Is.EqualTo(602));
        }
        [Test] public void 지도hash와계약판본_변경은_차단한다()
        {
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.ReadFrozen(면목동주소연결View.LocalPath,new byte[]{1}));
            var data=Data();data.mapRevision="old";Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,Map()));
        }
        [Test] public void 중복과잘못된건물참조는_차단한다()
        {
            var data=Data();data.links[1].item.id=data.links[0].item.id;
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,Map()));
            data=Data();data.groups[0].buildingId="missing";
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,Map()));
        }
        [Test] public void 복수후보를_단일연결로_바꾸지못한다()
        {
            var data=Data();var l=data.links.First(x=>x.result=="MultipleBuildingsForAddress");l.result=주소공간연결검사.Unique;
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,Map()));
        }
        [Test] public void 표시내용변조와비유한좌표는_차단한다()
        {
            var data=Data();data.groups[0].records[0].name="changed";
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,Map()));
            data=Data();var map=Map();map.buildings.First(b=>b.id==data.groups[0].buildingId).points[0].x=float.NaN;
            Assert.Throws<InvalidDataException>(()=>주소공간연결검사.Validate(data,map));
        }
        [Test] public void 의료폐업과불명상태를_정상으로표시하지않는다()
        {
            Assert.That(주소공간연결검사.Show(new 주소자료Item{kind="clinic",status="영업/정상"},false),Is.True);
            foreach(var status in new[]{"폐업","휴업","",null})
                Assert.That(주소공간연결검사.Show(new 주소자료Item{kind="clinic",status=status},false),Is.False);
            Assert.That(주소공간연결검사.Show(new 주소자료Item{kind="clinic",status="폐업"},true),Is.True);
            Assert.That(주소공간연결검사.Show(new 주소자료Item{kind="restaurant",status=""},false),Is.True);
        }
        [Test] public void 읽기소비자는_표현조립이나상태주입없이_연결된다()
        {
            var go=new GameObject("주소연결 시험 소유 객체");var map=Map();var before=JsonUtility.ToJson(map);
            try
            {
                var view=go.AddComponent<면목동주소연결View>();view.Initialize(map,MapAsset().bytes,null);
                Assert.That(view.ConnectedBuildings,Is.EqualTo(204));Assert.That(view.ConnectedRecords,Is.EqualTo(801));
                Assert.That(go.transform.childCount,Is.Zero);Assert.That(go.GetComponentsInChildren<Renderer>(),Is.Empty);
                Assert.That(go.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(JsonUtility.ToJson(map),Is.EqualTo(before));
                Assert.Throws<InvalidOperationException>(()=>view.Initialize(map,MapAsset().bytes,null));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
