using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 가상수온어군Tests
    {
        [Test] public void 어군기호는실제렌더러를함께가진다()
        {
            var go=new UnityEngine.GameObject("fish",typeof(UnityEngine.RectTransform),typeof(가상수온어군Graphic));
            try { Assert.That(go.GetComponent<UnityEngine.CanvasRenderer>(),Is.Not.Null); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private static 해수면수온PreviewCell[] Frame(params double[] values) => Enumerable.Range(0,9)
            .Select(i=>new 해수면수온PreviewCell {latitude=37.075-i/3*.05,longitude=130.025+i%3*.05,
                time="2026-09-01T12:00:00Z",mask=1,hasValue=true,celsius=values.Length==9?values[i]:30}).ToArray();
        private static 가상수온어군Model Model(해수면수온PreviewCell[] cells)
        { var m=new 가상수온어군Model(); m.SetFrame(cells); m.SetPreference(27.5); return m; }

        [Test] public void 인접개선격자로보간한뒤확정한다()
        {
            var m=Model(Frame(30,30,30,28,30,30,27.5,30,30));
            m.Advance(1); Assert.That(m.Current,Is.EqualTo(4)); Assert.That(m.Target,Is.EqualTo(3));
            Assert.That(m.Column,Is.EqualTo(.5)); Assert.That(m.Longitude,Is.EqualTo(130.05).Within(1e-8));
            m.Advance(1); Assert.That(m.Current,Is.EqualTo(3));
            m.Advance(2); Assert.That(m.Current,Is.EqualTo(6)); Assert.That(m.Moves,Is.EqualTo(2));
            m.Advance(100); Assert.That(m.Current,Is.EqualTo(6)); Assert.That(m.Moves,Is.EqualTo(2));
        }
        [Test] public void 같은입력은프레임분할과무관하다()
        {
            var f=Frame(30,30,30,28,30,30,27.5,30,30); var a=Model(f); var b=Model(f);
            a.Advance(3); for(var i=0;i<30;i++) b.Advance(.1);
            Assert.That(b.Current,Is.EqualTo(a.Current)); Assert.That(b.Target,Is.EqualTo(a.Target));
            Assert.That(b.Progress,Is.EqualTo(a.Progress).Within(1e-8));
        }
        [Test] public void 결측현재칸은정지한다()
        { var f=Frame(); f[4].hasValue=false; var m=Model(f); m.Advance(10); Assert.That(m.Current,Is.EqualTo(4)); Assert.That(m.Reason,Does.Contain("결측")); }
        [Test] public void 육지와결측과대각선으로이동하지않는다()
        {
            var f=Frame(27.5,27.5,30,27.5,30,30,30,30,30); f[1].mask=2; f[3].hasValue=false;
            var m=Model(f); m.Advance(10); Assert.That(m.Current,Is.EqualTo(4));
        }
        [Test] public void 동일점수는북쪽인덱스를택하고동률에진동하지않는다()
        { var m=Model(Frame(30,28,30,28,30,28,30,28,30)); m.Advance(20); Assert.That(m.Current,Is.EqualTo(1)); Assert.That(m.Moves,Is.EqualTo(1)); }
        [Test] public void 정지는시간을쌓지않고재개한다()
        {
            var m=Model(Frame(30,28,30,30,30,30,30,30,30)); m.Advance(1); m.SetRunning(false); m.Advance(50);
            Assert.That(m.Progress,Is.EqualTo(.5)); m.SetRunning(true); m.Advance(1); Assert.That(m.Current,Is.EqualTo(1));
        }
        [Test] public void 선호변경은마지막확정칸에서새판정을한다()
        {
            var m=Model(Frame(30,27.5,30,30,30,32,30,30,30)); m.Advance(1); m.SetPreference(32); m.Advance(2);
            Assert.That(m.Current,Is.EqualTo(5)); Assert.That(m.Moves,Is.EqualTo(1));
        }
        [Test] public void 새날짜는중앙으로초기화하고원본변경이실험에침투하지않는다()
        {
            var f=Frame(30,28,30,30,30,30,30,30,30); var m=Model(f); f[1].celsius=40; m.Advance(2);
            Assert.That(m.Current,Is.EqualTo(1)); m.SetFrame(Frame()); Assert.That(m.Current,Is.EqualTo(4)); Assert.That(m.Moves,Is.Zero);
        }
        [Test] public void 잘못된시간과선호와좌표를거절한다()
        {
            var m=Model(Frame()); Assert.Throws<ArgumentOutOfRangeException>(()=>m.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>m.Advance(-1)); Assert.Throws<ArgumentOutOfRangeException>(()=>m.SetPreference(double.PositiveInfinity));
            var f=Frame(); f[1].latitude=0; Assert.Throws<ArgumentException>(()=>m.SetFrame(f));
        }
    }
}
