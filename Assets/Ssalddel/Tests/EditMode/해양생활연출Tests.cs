using NUnit.Framework;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 해양생활연출Tests
    {
        [Test] public void 태평양은날짜변경선으로연결되고유럽으로돌지않는다()
        {
            var p=가상해양항로Catalog.Interpolate(new Vector2(24,170),new Vector2(30,-150),.5f);
            Assert.That(p.y,Is.EqualTo(-170).Within(.001));
            Assert.That(p.x,Is.EqualTo(27).Within(.001));
        }
        [Test] public void 모든세계시연항로는저해상도윤곽에서바다이다()
        {
            var map=JsonUtility.FromJson<세계지구본Catalog>(Resources.Load<TextAsset>(세계지구본View.CatalogResourceName).text);
            Assert.That(가상해양항로Catalog.Count,Is.EqualTo(6));
            for(var r=0;r<가상해양항로Catalog.Count;r++)
                Assert.That(가상해양항로Catalog.IsOcean(r,(lat,lon)=>map.FindAt(lat,lon)==null),Is.True,가상해양항로Catalog.Name(r));
        }
        [Test] public void 국제항로는백오십초마다동일하게왕복한다()
        {
            for(var r=1;r<가상해양항로Catalog.Count;r++)
            {
                Assert.That(가상해양항로Catalog.Ship(r,0,0),Is.EqualTo(가상해양항로Catalog.Ship(r,150,0)));
                Assert.That(가상해양항로Catalog.Ship(r,76,0),Is.EqualTo(가상해양항로Catalog.Point(r,1)));
                Assert.That(가상해양항로Catalog.Ship(r,0,0),Is.Not.EqualTo(가상해양항로Catalog.Ship(r,0,1)));
            }
        }
        [Test] public void 물고기는수면위아래로이동한다()
        {
            Assert.That(해양생활연출View.FishRadius(Mathf.PI/2/1.1f,0),Is.EqualTo(18.46f).Within(.001));
            Assert.That(해양생활연출View.FishRadius(Mathf.PI*1.5f/1.1f,0),Is.EqualTo(17.54f).Within(.001));
            Assert.That(해양생활연출View.FishRadius(0,1),Is.Not.EqualTo(해양생활연출View.FishRadius(0,0)));
        }
        [Test] public void 선박은양끝에서대기한뒤왕복한다()
        {
            var start=해양생활연출View.RoutePoint(0); var end=해양생활연출View.RoutePoint(1);
            Assert.That(해양생활연출View.ShipCoordinate(1,0),Is.EqualTo(start));
            Assert.That(해양생활연출View.ShipCoordinate(26,0),Is.EqualTo(end));
            Assert.That(해양생활연출View.ShipCoordinate(50,0),Is.EqualTo(start));
            Assert.That(해양생활연출View.ShipCoordinate(14,0),Is.EqualTo(해양생활연출View.ShipCoordinate(39,0)));
        }
        [Test] public void 항로에육지판정이있으면차단한다()
        {
            Assert.That(해양생활연출View.IsRouteOcean((lat,lon)=>true),Is.True);
            Assert.That(해양생활연출View.IsRouteOcean((lat,lon)=>lat<36),Is.False);
        }
        [Test] public void 가상항로는기존저해상도국가윤곽에서바다이다()
        {
            var asset=Resources.Load<TextAsset>(세계지구본View.CatalogResourceName);
            Assert.That(asset,Is.Not.Null);
            var map=JsonUtility.FromJson<세계지구본Catalog>(asset.text);
            Assert.That(해양생활연출View.IsRouteOcean((lat,lon)=>map.FindAt(lat,lon)==null),Is.True);
        }
    }
}
