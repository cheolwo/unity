using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 도로명주소표현Tests
    {
        private static TextAsset MapAsset=>Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map=>JsonUtility.FromJson<공간참고자료>(MapAsset.text);
        private static 도로명표현자료 Data=>JsonUtility.FromJson<도로명표현자료>(Resources.Load<TextAsset>("SagajeongRoadNameOverlay").text);
        private static 도로명주소표현조회 Check(도로명표현자료 data)=>도로명주소표현조회.Validate(data,Map,MapAsset.bytes);

        [Test] public void 도로명78개와_구간1109개와_주소건물568개를_동결한다()
        {
            var query=Check(Data);
            Assert.That(query.Labels.Count,Is.EqualTo(78));
            Assert.That(query.NamedSegmentCount,Is.EqualTo(1109));
            Assert.That(query.LinkedBuildingCount,Is.EqualTo(568));
            Assert.That(query.Labels.Count(x=>x.importanceCode=="Major"),Is.EqualTo(4));
            CollectionAssert.AreEquivalent(new[]{"사가정로","면목로","용마산로","면목천로"},
                query.Labels.Where(x=>x.importanceCode=="Major").Select(x=>x.name));
            Assert.That(query.Labels.Sum(x=>x.segmentCount),Is.EqualTo(1109));
            Assert.That(query.Labels.Sum(x=>x.buildingCount),Is.EqualTo(568));
        }

        [Test] public void 전체보기는_큰길만_확대보기는_가까운생활도로까지_제한한다()
        {
            var query=Check(Data);
            var whole=query.SelectLabels(500,550,8,18);
            Assert.That(whole.Count,Is.EqualTo(4));
            Assert.That(whole.All(x=>x.importanceCode=="Major"),Is.True);
            var close=query.SelectLabels(90,550,8,18);
            Assert.That(close.Count,Is.EqualTo(18));
            Assert.That(close.Count(x=>x.importanceCode=="Major"),Is.EqualTo(4));
            Assert.That(close.Any(x=>x.importanceCode=="Local"),Is.True);
            Assert.Throws<ArgumentException>(()=>query.SelectLabels(float.NaN,0,0,18));
            Assert.Throws<ArgumentException>(()=>query.SelectLabels(100,0,0,41));
        }

        [Test] public void 도로명선택은_공개건물번호와_관찰좌표만돌려준다()
        {
            var query=Check(Data);
            var sagajeong=query.Addresses("사가정로");
            Assert.That(sagajeong.Count,Is.EqualTo(64));
            Assert.That(sagajeong.All(x=>x.Label.StartsWith("사가정로 ",StringComparison.Ordinal)),Is.True);
            Assert.That(sagajeong.Select(x=>x.BuildingId).Distinct().Count(),Is.EqualTo(64));
            Assert.That(sagajeong.All(x=>float.IsFinite(x.X)&&float.IsFinite(x.Y)&&float.IsFinite(x.Z)),Is.True);
            Assert.That(query.Addresses("없는 도로"),Is.Empty);
            Assert.That(query.Addresses("사가정로39길").Any(x=>x.Label=="사가정로39길 79"),Is.True);
        }

        [TestCase("mapRevision")] [TestCase("mapHash")] [TestCase("rawHash")] [TestCase("source")]
        [TestCase("attribution")] [TestCase("authority")] [TestCase("boundary")]
        public void 출처와표현경계변조를_거부한다(string failure)
        {
            var data=Data;
            switch(failure)
            {
                case "mapRevision":data.mapRevision="other";break;
                case "mapHash":data.mapHash=new string('0',64);break;
                case "rawHash":data.rawSourceHash=new string('0',64);break;
                case "source":data.sourceUrl="unknown";break;
                case "attribution":data.attribution="missing";break;
                case "authority":data.presentationOnly=false;break;
                case "boundary":data.boundary="NavigationAuthority";break;
            }
            Assert.Throws<ArgumentException>(()=>Check(data));
        }

        [TestCase("duplicateName")] [TestCase("duplicateWay")] [TestCase("missingWay")] [TestCase("importance")]
        [TestCase("segmentCount")] [TestCase("buildingCount")] [TestCase("anchor")] [TestCase("yaw")]
        public void 도로연결과대표점변조를_거부한다(string failure)
        {
            var data=Data;
            switch(failure)
            {
                case "duplicateName":data.roads[1].name=data.roads[0].name;break;
                case "duplicateWay":data.roads[1].wayIds[0]=data.roads[0].wayIds[0];break;
                case "missingWay":data.roads[0].wayIds[0]="osm:way:missing";break;
                case "importance":data.roads[0].importanceCode="Local";break;
                case "segmentCount":data.roads[0].segmentCount++;break;
                case "buildingCount":data.roads[0].buildingCount++;break;
                case "anchor":data.roads[0].x+=10;break;
                case "yaw":data.roads[0].yaw=91;break;
            }
            Assert.Throws<ArgumentException>(()=>Check(data));
        }

        [Test] public void 검사결과와화면조립은_원자료변경과분리된다()
        {
            var map=Map;var before=JsonUtility.ToJson(map);var data=Data;var query=도로명주소표현조회.Validate(data,map,MapAsset.bytes);
            var first=query.Labels[0].name;data.roads[0].name="changed";
            Assert.That(query.Labels[0].name,Is.EqualTo(first));
            Assert.That(JsonUtility.ToJson(map),Is.EqualTo(before));
            var root=new GameObject("road-address-view-test");
            try
            {
                var view=root.AddComponent<사가정운영디오라마View>();view.Initialize();
                Assert.That(view.MapStatus,Is.EqualTo("Ready"));
                Assert.That(view.RoadNameStatus,Is.EqualTo("도로명 78 · 주소 건물 568"));
                Assert.That(view.Places.Count,Is.EqualTo(6));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void 마지막주소까지_페이지로선택하고_전체보기로귀환한다()
        {
            var root=new GameObject("road-address-page-test");
            try
            {
                var view=root.AddComponent<사가정운영디오라마View>();
                view.Initialize();
                Assert.That(view.SelectRoad("사가정로"),Is.True);
                Assert.That(view.AddressPageCount,Is.EqualTo(11));
                view.SetAddressPage(int.MaxValue);
                Assert.That(view.AddressPage,Is.EqualTo(10));
                var last=Check(Data).Addresses("사가정로").Last();
                Assert.That(view.SelectAddress(last.BuildingId),Is.True);
                Assert.That(view.SelectedAddress,Is.EqualTo(last.Label));
                Assert.That(view.AddressPage,Is.EqualTo(10));
                Assert.That(view.ObservationCamera.orthographicSize,Is.EqualTo(90));
                Assert.That(view.SelectAddress("osm:way:missing"),Is.False);
                Assert.That(view.SelectRoad("없는 도로"),Is.False);
                Assert.That(view.SelectedAddress,Is.EqualTo(last.Label));
                view.ShowWhole();
                Assert.That(view.SelectedRoad,Is.Empty);
                Assert.That(view.SelectedAddress,Is.Empty);
                Assert.That(view.AddressPage,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void 주소집중은_부모이동을반영하고_도로변경은선택을초기화한다()
        {
            var root=new GameObject("road-address-transform-test");
            root.transform.position=new Vector3(100,20,300);
            try
            {
                var view=root.AddComponent<사가정운영디오라마View>();
                view.Initialize();
                Assert.That(view.SelectRoad("동일로96길"),Is.True);
                var address=Check(Data).Addresses("동일로96길").Single(x=>x.Label=="동일로96길 51");
                Assert.That(view.SelectAddress(address.BuildingId),Is.True);
                var camera=view.ObservationCamera;
                var center=camera.transform.position+camera.transform.forward*1800;
                Assert.That(Vector3.Distance(center,root.transform.TransformPoint(new Vector3(address.X,address.Y,address.Z))),Is.LessThan(.01f));
                view.SelectRoad("사가정로");
                Assert.That(view.SelectedAddress,Is.Empty);
                Assert.That(view.AddressPage,Is.Zero);
                view.SetAddressPage(-100);
                Assert.That(view.AddressPage,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
