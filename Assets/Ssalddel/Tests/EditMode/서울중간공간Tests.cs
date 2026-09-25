using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Tests.EditMode
{
    public class 서울중간공간Tests
    {
        private static 서울경계사본 Fixture()=>new 서울경계사본 {
            schemaVersion="seoul-intermediate-preview.v1",observationPresentationOnly=true,metersPerUnit=1000,
            areas=Enumerable.Range(0,41).Select(i=>new 서울경계Area{code=i.ToString(),level=i<25?0:1,
                triangles=new[]{new 서울경계점(),new 서울경계점{x=1},new 서울경계점{z=1}},rings=new 서울경계Ring[0]}).ToArray()};
        [Test] public void 계층수와비권위계약을확인한다(){Assert.DoesNotThrow(()=>Fixture().Validate());}
        [Test] public void 배포와통행승격을거절한다(){var data=Fixture();data.traversalReady=true;Assert.Throws<ArgumentException>(()=>data.Validate());}
        [Test] public void 중복코드를거절한다(){var data=Fixture();data.areas[1].code=data.areas[0].code;Assert.Throws<ArgumentException>(()=>data.Validate());}
        [Test] public void 비정상좌표를거절한다(){var data=Fixture();data.areas[0].triangles[0].x=float.NaN;Assert.Throws<ArgumentException>(()=>data.Validate());}
        [Test] public void 역세권연결은확인한동하나만허용한다()
        {Assert.IsTrue(서울중간공간View.HasStationPreview("11260570"));Assert.IsFalse(서울중간공간View.HasStationPreview("11350560"));Assert.IsFalse(서울중간공간View.HasStationPreview(null));}
        [Test] public void 도형내부만선택한다()
        {var a=Fixture().areas[0];Assert.IsTrue(서울중간공간View.ContainsPoint(a,new UnityEngine.Vector2(.2f,.2f)));Assert.IsFalse(서울중간공간View.ContainsPoint(a,new UnityEngine.Vector2(2,2)));}
        private static 서울경계사본 Expanded()
        {
            var data=Fixture();var list=data.areas.Take(25).ToList();
            var codes=new[]{"11260","11230","11215","11350"};var counts=new[]{16,14,15,19};
            for(int i=0;i<4;i++)
            {
                list[i].code=codes[i];
                for(int j=0;j<counts[i];j++)list.Add(new 서울경계Area{code=codes[i]+j.ToString("D3"),parentCode=codes[i],level=1,triangles=list[0].triangles,rings=list[0].rings});
            }
            data.schemaVersion="seoul-intermediate-preview.v2";data.areas=list.ToArray();return data;
        }
        [Test] public void 네개구64행정동을허용한다(){Assert.DoesNotThrow(()=>Expanded().Validate());}
        [Test] public void 기존판본을명시적부모관계로변환한다()
        {
            var legacy=Fixture();legacy.areas[0].code="11260";
            var h=행정구역Hierarchy.FromSeoul(legacy);
            Assert.That(h.Children("11260").Length,Is.EqualTo(16));
            Assert.That(h.IsChild("25","11260"),Is.True);
            var expanded=행정구역Hierarchy.FromSeoul(Expanded());
            Assert.That(expanded.Children("11350").Length,Is.EqualTo(19));
            Assert.That(expanded.HasChildren("4"),Is.False);
        }
        [Test] public void 잘못된부모구를거절한다(){var data=Expanded();data.areas[25].parentCode="11350";Assert.Throws<ArgumentException>(()=>data.Validate());}
        [Test] public void 누락된행정동을거절한다(){var data=Expanded();data.areas=data.areas.Skip(1).ToArray();Assert.Throws<ArgumentException>(()=>data.Validate());}
        private static 서울경계사본 Geographic()
        {
            var data=Expanded();data.schemaVersion="seoul-intermediate-preview.v3";
            foreach(var area in data.areas){area.center=new 서울경계점{latitude=37.58,longitude=127.08};
                foreach(var p in area.triangles){p.latitude=37.58;p.longitude=127.08;}}
            return data;
        }
        [Test] public void 위경도판본을허용한다(){Assert.DoesNotThrow(()=>Geographic().Validate());}
        [Test] public void 누락위경도를거절한다(){var d=Geographic();d.areas[0].center.latitude=0;Assert.Throws<ArgumentException>(()=>d.Validate());}
        [Test] public void 광역참고는별도판본과좌표를검증한다()
        {
            var c=new 광역경계Context{schemaVersion="korea-admin1-context.v1",sourceHash=new string('a',64),attribution="Natural Earth",areas=Enumerable.Range(0,28).Select(i=>new 서울경계Area{code="KOR-"+i,parentCode="KOR",level=-1,center=new 서울경계점{latitude=37,longitude=127},rings=new[]{new 서울경계Ring{points=new[]{new 서울경계점{latitude=37,longitude=127},new 서울경계점{latitude=37.1,longitude=127},new 서울경계점{latitude=37,longitude=127.1},new 서울경계점{latitude=37,longitude=127}}}}}).ToArray()};
            Assert.DoesNotThrow(()=>c.Validate());c.areas[0].center.latitude=double.NaN;Assert.Throws<ArgumentException>(()=>c.Validate());
        }
        [Test] public void 긴경계선은원본끝점과곡면반경을보존한다()
        {
            var points=new[]{new 서울경계점{latitude=37,longitude=126},new 서울경계점{latitude=38,longitude=127}};
            var projected=서울중간공간View.ProjectBoundary(points,18.076f);
            Assert.That(projected.Length,Is.GreaterThan(100));
            Assert.That(UnityEngine.Vector3.Distance(projected[0],지구위경도선View.Coordinate(37,126,18.076f)),Is.LessThan(.000001f));
            Assert.That(UnityEngine.Vector3.Distance(projected[projected.Length-1],지구위경도선View.Coordinate(38,127,18.076f)),Is.LessThan(.000001f));
            foreach(var p in projected)Assert.That(p.magnitude,Is.EqualTo(18.076f).Within(.00001f));
        }
        [Test] public void 잘못된경계투영을거절한다()
        {Assert.Throws<ArgumentException>(()=>서울중간공간View.ProjectBoundary(new[]{new 서울경계점{latitude=double.NaN}},18));}
        [Test] public void 수도권육지범위와메시인덱스를검증한다()
        {
            var p=new 수도권육지Patch{west=125,east=128,south=36,north=39,vertices=new[]{new 서울경계점{latitude=37,longitude=126},new 서울경계점{latitude=37.1,longitude=126},new 서울경계점{latitude=37,longitude=126.1}},indices=new[]{0,1,2}};
            Assert.DoesNotThrow(()=>p.Validate());Assert.IsTrue(p.Contains(37.5,127));Assert.IsFalse(p.Contains(35,127));
            p.indices[2]=3;Assert.Throws<ArgumentException>(()=>p.Validate());p.indices[2]=2;
            p.vertices[0].latitude=double.NaN;Assert.Throws<ArgumentException>(()=>p.Validate());
        }
        [Test] public void 실제높이는미터단위보간하고결측과배포승격을거절한다()
        {
            var p=new 지형높이Patch{schemaVersion="globe-relief-review.v1",review="LocalEditorOnly_NotForDistribution",sourceHash=new string('a',64),riverSourceHash=new string('b',64),west=127,east=128,south=37,north=38,width=2,heightMeters=new[]{0f,100f,200f,300f},rivers=new[]{new 서울경계Ring{points=new[]{new 서울경계점{latitude=37.5,longitude=127.2},new 서울경계점{latitude=37.6,longitude=127.3}}}}};
            Assert.DoesNotThrow(()=>p.Validate());Assert.That(p.Sample(37.5,127.5),Is.EqualTo(150));Assert.That(p.Sample(38,127),Is.Zero);
            p.heightMeters[0]=float.NaN;Assert.Throws<ArgumentException>(()=>p.Validate());p.heightMeters[0]=0;
            p.review="Approved";Assert.Throws<ArgumentException>(()=>p.Validate());
        }
        [Test] public void 무한위경도를거절한다(){var d=Geographic();d.areas[0].triangles[0].longitude=double.PositiveInfinity;Assert.Throws<ArgumentException>(()=>d.Validate());}
        [Test] public void 다른지역확대나뒷면은서울진입을허용하지않는다()
        {
            Assert.IsTrue(서울중간공간View.IsRegionInFocus(new UnityEngine.Vector3(.5f,.5f,10),true));
            Assert.IsFalse(서울중간공간View.IsRegionInFocus(new UnityEngine.Vector3(.5f,.5f,10),false));
            Assert.IsFalse(서울중간공간View.IsRegionInFocus(new UnityEngine.Vector3(.9f,.5f,10),true));
            Assert.IsFalse(서울중간공간View.IsRegionInFocus(new UnityEngine.Vector3(.5f,.5f,-1),true));
        }
    }
}
