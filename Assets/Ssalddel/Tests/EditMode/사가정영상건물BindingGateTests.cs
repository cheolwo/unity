using System;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정영상건물BindingGateTests
    {
        private static string Hash(char c)=>new(c,64);
        private static Vector2[] Ring=>new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero};
        private static string Fixture()=>"{\"schema\":\""+사가정영상건물BindingGate.Schema+"\","
            +"\"canonicalAppliedCount\":0,\"currentMap\":{\"reference\":{\"sha256\":\""+Hash('a')+"\"},"
            +"\"baseReference\":{\"sha256\":\""+Hash('b')+"\"},\"baseMapHash\":\""+Hash('b')+"\"},"
            +"\"bindings\":[{\"targetBuildingId\":\"exact-gis\",\"sourceBuildingId\":\"exact-gis\","
            +"\"status\":\"SourceBackedPrivateLayer\",\"targetScope\":\"SourceBackedPrivateLayer\","
            +"\"sourceEvidence\":{\"sha256\":\""+Hash('c')+"\"},\"sourceGeometryHash\":\""+Hash('d')+"\","
            +"\"canonicalApplied\":false,\"canonicalBinding\":{\"status\":\"Held\"},"
            +"\"localParts\":[{\"outer\":{\"points\":[{\"x\":0,\"z\":0},{\"x\":1,\"z\":0},"
            +"{\"x\":1,\"z\":1},{\"x\":0,\"z\":1},{\"x\":0,\"z\":0}]},\"holes\":[]}]}]}";

        private static string Validate(string text,Vector2[] ring=null)=>사가정영상건물BindingGate.ValidateJson(
            text,"exact-gis",Hash('c'),Hash('d'),Hash('b'),Hash('a'),ring??Ring);

        [Test] public void 정확원형과_현재판본의_비공개추가레이어만_허용한다()
            =>Assert.That(Validate(Fixture()),Is.EqualTo("SourceBackedPrivateLayer"));

        [TestCase("\"canonicalAppliedCount\":0","\"canonicalAppliedCount\":1")]
        [TestCase("\"canonicalApplied\":false","\"canonicalApplied\":true")]
        [TestCase("\"sourceBuildingId\":\"exact-gis\"","\"sourceBuildingId\":\"nearby\"")]
        [TestCase("\"status\":\"Held\"","\"status\":\"Approved\"")]
        [TestCase("\"targetScope\":\"SourceBackedPrivateLayer\"","\"targetScope\":\"Canonical\"")]
        public void 정본승격이나_근접대체를_거절한다(string oldValue,string newValue)
            =>Assert.Throws<InvalidOperationException>(()=>Validate(Fixture().Replace(oldValue,newValue)));

        [TestCase('a')] [TestCase('b')] [TestCase('c')] [TestCase('d')]
        public void 입력이나_근거판본이_달라지면_거절한다(char changed)
            =>Assert.Throws<InvalidOperationException>(()=>Validate(Fixture().Replace(Hash(changed),Hash('e'))));

        [Test] public void 같은대상의_중복이나_실제원형차이를_거절한다()
        {
            var text=Fixture();var start=text.IndexOf("{\"targetBuildingId\"",StringComparison.Ordinal);
            var row=text.Substring(start,text.Length-start-2);
            Assert.Throws<InvalidOperationException>(()=>Validate(text.Insert(text.Length-2,","+row)));
            var changed=Ring;changed[1]=new Vector2(1.02f,0);
            Assert.Throws<InvalidOperationException>(()=>Validate(text,changed));
        }
    }
}
