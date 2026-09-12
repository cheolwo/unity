using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 건물높이보완Tests
    {
        private static TextAsset Asset => Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(Asset.text);
        private static 건물높이보완자료 Overlay => JsonUtility.FromJson<건물높이보완자료>(
            Resources.Load<TextAsset>("SagajeongBuildingHeightOverlay").text);
        private static 건물높이보완조회 Check(건물높이보완자료 overlay) => 건물높이보완조회.Validate(overlay, Map, Asset.bytes);

        [Test] public void 두양수높이만_표현하고_층수미상높이와원본은유지한다()
        {
            var map=Map; var original=JsonUtility.ToJson(map); var overlay=Overlay;
            var heights=건물높이보완조회.Validate(overlay,map,Asset.bytes);
            Assert.That(heights.Count,Is.EqualTo(2));
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1240882409")),Is.EqualTo(8.3f));
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1240882396")),Is.EqualTo(19.1f));
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1240882352")),Is.EqualTo(4f));
            overlay.buildings[0].heightMeters=100;
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1240882409")),Is.EqualTo(8.3f),"검사 뒤 DTO 변경과 분리");
            Assert.That(JsonUtility.ToJson(map),Is.EqualTo(original));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(201f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void 결손이나비정상높이를_거부한다(float height)
        {
            var data=Overlay;data.buildings[0].heightMeters=height;
            Assert.Throws<ArgumentException>(()=>Check(data));
        }

        [TestCase("revision")] [TestCase("hash")] [TestCase("source")] [TestCase("authority")]
        [TestCase("duplicate")] [TestCase("unknown")] [TestCase("address")] [TestCase("evidence")]
        [TestCase("date")] [TestCase("identity")]
        public void 판본과출처와후보경계를_검사한다(string failure)
        {
            var data=Overlay;
            switch(failure)
            {
                case "revision":data.mapRevision="other";break;
                case "hash":data.mapHash=new string('0',64);break;
                case "source":data.sourceUrl="unknown";break;
                case "authority":data.presentationOnly=false;break;
                case "duplicate":data.buildings[1]=data.buildings[0];break;
                case "unknown":data.buildings[0].id="osm:way:unknown";break;
                case "address":data.buildings[0].houseNumber="999";break;
                case "evidence":data.buildings[0].sourceHash="invalid";break;
                case "date":data.buildings[0].recordCreationDate="20261399";break;
                case "identity":data.buildings[0].matchingStatus="VerifiedIdentity";break;
            }
            Assert.Throws<ArgumentException>(()=>Check(data));
        }

        [Test] public void 중복주소와기존출처높이는_덮어쓰지않는다()
        {
            var map=Map; var data=Overlay;
            var building=map.buildings.Single(b=>b.id==data.buildings[0].id);
            building.heightKind="OsmHeightTag";
            Assert.Throws<ArgumentException>(()=>건물높이보완조회.Validate(data,map,Asset.bytes));
            map=Map;
            map.buildings[0].street=data.buildings[0].street;
            map.buildings[0].houseNumber=data.buildings[0].houseNumber;
            Assert.Throws<ArgumentException>(()=>건물높이보완조회.Validate(data,map,Asset.bytes));
        }

        [Test] public void 벽과지붕은_보완높이를읽고_원본좌표와나머지는보존한다()
        {
            var map=Map;var original=JsonUtility.ToJson(map);var heights=건물높이보완조회.Validate(Overlay,map,Asset.bytes);
            var baseline=new System.Collections.Generic.List<Vector3>();
            var updated=new System.Collections.Generic.List<Vector3>();
            사가정지리MeshBuilder.Build(map,(name,v,t,c)=> {if(name.Contains("건물"))baseline.AddRange(v);});
            사가정지리MeshBuilder.Build(map,(name,v,t,c)=> {if(name.Contains("건물"))updated.AddRange(v);},heights);
            Assert.That(updated.Count,Is.EqualTo(baseline.Count));
            int changed=0;
            for(int i=0;i<updated.Count;i++)
            {
                Assert.That(updated[i].x,Is.EqualTo(baseline[i].x));
                Assert.That(updated[i].z,Is.EqualTo(baseline[i].z));
                if(updated[i].y==baseline[i].y)continue;
                changed++;
                Assert.That(baseline[i].y,Is.EqualTo(4.06f).Within(.001f));
                Assert.That(Math.Abs(updated[i].y-8.36f)<.001f || Math.Abs(updated[i].y-19.16f)<.001f,Is.True);
            }
            Assert.That(changed,Is.GreaterThan(0));
            Assert.That(updated.Any(v=>Math.Abs(v.y-8.36f)<.001f),Is.True);
            Assert.That(updated.Any(v=>Math.Abs(v.y-19.16f)<.001f),Is.True);
            Assert.That(JsonUtility.ToJson(map),Is.EqualTo(original));
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1112326971")),Is.EqualTo(44));
            Assert.That(heights.Resolve(map.buildings.Single(b=>b.id=="osm:way:1256615238")),Is.EqualTo(40));
        }

        [Test] public void 실제View조립에서_보완상태와두높이를확인한다()
        {
            var root=new GameObject("height-overlay-test");
            try
            {
                var view=root.AddComponent<사가정운영디오라마View>();
                view.Initialize(); // EditMode에서는 일반 MonoBehaviour의 Awake를 자동 실행하지 않는다.
                Assert.That(view.MapStatus,Is.EqualTo("Ready"));
                Assert.That(view.HeightStatus,Is.EqualTo("대장 높이 후보 연결 2건"));
                var fallbackHasHeight=root.GetComponentsInChildren<MeshFilter>()
                    .Where(x=>x.name.Contains("건물"))
                    .Any(x=>x.sharedMesh.vertices.Any(v=>Math.Abs(v.y-19.16f)<.001f));
                var syntyHasHeight=view.SyntyPlacements.Any(value=>
                    Math.Abs(value.TargetHeight-19.1f)<.001f
                    && Math.Abs(value.ActualHeight-value.TargetHeight)/value.TargetHeight
                    <=사가정Synty표현Planner.MaximumHeightErrorRatio);
                Assert.That(fallbackHasHeight||syntyHasHeight,Is.True);
                Assert.That(view.Places.Count,Is.EqualTo(6));
                typeof(사가정운영디오라마View).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(view,null);
                Assert.That(view.HeightStatus,Is.EqualTo("NotLoaded"));
                view.Initialize();
                Assert.That(view.HeightStatus,Is.EqualTo("대장 높이 후보 연결 2건"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
