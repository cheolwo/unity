using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 오프라인생활Tests
    {
        private static 로컬생활시작묶음 Bundle()
        {
            var path=Environment.GetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_TEST_BUNDLE");
            var hash=Environment.GetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_TEST_SHA256");
            if(string.IsNullOrWhiteSpace(path))Assert.Ignore("검증된 내보내기 파일을 명시해야 하는 독립 시험");
            return 로컬생활시작자료.Read(path,hash);
        }
        [TestCase(1)] [TestCase(4)]
        public void 명시기사수와ID로표현을만들고반복조회는중복생성하지않는다(int couriers)
        {
            var payload=Bundle().Payload;payload.InitialRequest=로컬생활구성표본.Create(Guid.NewGuid(),couriers,"edit-test:");
            var b=로컬생활시작자료.Seal(payload);var core=new 경영SimulationSessionAggregate(b.Payload.InitialRequest);
            var root=new GameObject("오프라인 수명 시험");오프라인생활Controller c=null;Material[] ownedMaterials=Array.Empty<Material>();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            try {
                c=root.AddComponent<오프라인생활Controller>();
                typeof(오프라인생활Controller).GetField("bundle",flags).SetValue(c,b);
                typeof(오프라인생활Controller).GetMethod("Build",flags).Invoke(c,null);
                ownedMaterials=root.GetComponentsInChildren<Renderer>().Select(x=>x.sharedMaterial).Distinct().ToArray();
                Assert.AreEqual(9+6+couriers+couriers+1,c.OwnedObjectCount);
                var ids=c.OwnedIds;var owned=root.GetComponentsInChildren<Transform>().Select(x=>x.GetEntityId()).ToArray();
                var state=core.Snapshot();var bytes=로컬생활시작자료.JsonBytes(state);
                typeof(오프라인생활Controller).GetField("state",flags).SetValue(c,state);
                for(int i=0;i<3;i++)typeof(오프라인생활Controller).GetMethod("Render",flags).Invoke(c,null);
                CollectionAssert.AreEqual(bytes,로컬생활시작자료.JsonBytes(state));CollectionAssert.AreEqual(ids,c.OwnedIds);
                CollectionAssert.AreEqual(owned,root.GetComponentsInChildren<Transform>().Select(x=>x.GetEntityId()).ToArray());
                Assert.IsTrue(root.GetComponentsInChildren<Collider>().All(x=>!x.enabled));
            } finally {
                // 비실행 EditMode에서는 Start/OnDestroy의 Play 수명을 가정하지 않고 해제 경로를 명시 호출한다.
                if(c!=null)typeof(오프라인생활Controller).GetMethod("OnDestroy",flags).Invoke(c,null);
                UnityEngine.Object.DestroyImmediate(root);
            }
            Assert.IsTrue(ownedMaterials.All(x=>x==null),"실제로 소유한 재질 모두 해제");
        }
        [Test]
        public void 전용슬롯저장복원과보고는네트워크없이같은묶음에결속한다()
        {
            var b=Bundle();var folder=Path.Combine(Path.GetTempPath(),"offline-unity-test-"+Guid.NewGuid().ToString("N"));
            var names=new[]{"SSALDDEL_OFFLINE_LIFE_BUNDLE","SSALDDEL_OFFLINE_LIFE_SHA256","SSALDDEL_SYNTHETIC_DELIVERY"};
            var old=names.Select(Environment.GetEnvironmentVariable).ToArray();
            Assert.IsFalse(SimulationWorldLocalRuntimeScope.IsInstalled,"기존 점유 없는 독립 시험에서만 실행");
            try {
                var path=Path.Combine(folder,"bundle.json");로컬생활시작자료.WriteOnce(path,로컬생활시작자료.JsonBytes(b),로컬생활시작자료.MaxBundleBytes);
                Environment.SetEnvironmentVariable(names[0],path);Environment.SetEnvironmentVariable(names[1],b.ContentSha256);Environment.SetEnvironmentVariable(names[2],null);
                Assert.AreEqual(Nature생존LaunchProfile.OfflineLife,SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                var scope=SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.OfflineLife,folder);
                Assert.IsFalse(scope.Runtime.Descriptor.RequiresNetwork);
                var s=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).GetAwaiter().GetResult();
                scope.Runtime.Sessions.AdvanceWorldTickAsync(s.SessionStableId,new 경영SimulationTick진행Request {CommandId="test:one",ExpectedRevision=s.Revision,TickCount=1}).GetAwaiter().GetResult();
                scope.SaveCurrent();var report=scope.Runtime.GetLocalLifeReportAsync(s.SessionStableId,b).GetAwaiter().GetResult();
                SimulationWorldLocalRuntimeScope.Reset();scope=SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.OfflineLife,folder);
                Assert.IsTrue(scope.RestoredFromSlot);Assert.AreEqual(1,scope.Runtime.Sessions.GetAsync(scope.SessionStableId).GetAwaiter().GetResult().CurrentTick);
                Assert.AreEqual(report.ContentSha256,scope.Runtime.GetLocalLifeReportAsync(scope.SessionStableId,b).GetAwaiter().GetResult().ContentSha256);
            } finally {SimulationWorldLocalRuntimeScope.Reset();for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],old[i]);if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [TestCase("save")] [TestCase("report")]
        public void 로컬쓰기실패는추가Tick을막고기존성공파일을보존한다(string failure)
        {
            var b=Bundle();var folder=Path.Combine(Path.GetTempPath(),"offline-failure-test-"+Guid.NewGuid().ToString("N"));
            var names=new[]{"SSALDDEL_OFFLINE_LIFE_BUNDLE","SSALDDEL_OFFLINE_LIFE_SHA256","SSALDDEL_SYNTHETIC_DELIVERY"};
            var old=names.Select(Environment.GetEnvironmentVariable).ToArray();GameObject root=null;
            Assert.IsFalse(SimulationWorldLocalRuntimeScope.IsInstalled,"기존 점유 없는 독립 시험");
            try {
                var path=Path.Combine(folder,"bundle.json");var bytes=로컬생활시작자료.JsonBytes(b);
                로컬생활시작자료.WriteOnce(path,bytes,로컬생활시작자료.MaxBundleBytes);
                Environment.SetEnvironmentVariable(names[0],path);Environment.SetEnvironmentVariable(names[1],b.ContentSha256);Environment.SetEnvironmentVariable(names[2],null);
                var scope=SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.OfflineLife,folder);
                var s=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).GetAwaiter().GetResult();
                for(int i=0;i<29;i++)s=scope.Runtime.Sessions.AdvanceWorldTickAsync(s.SessionStableId,new 경영SimulationTick진행Request {
                    CommandId="test:failure:"+i,ExpectedRevision=s.Revision,TickCount=1}).GetAwaiter().GetResult();
                var invalid=Path.Combine(folder,"report-parent-is-file");로컬생활시작자료.WriteOnce(invalid,new byte[]{1},1);
                if(failure=="save")Directory.CreateDirectory(Path.Combine(folder,scope.SlotStableId+".ssalddel"));
                root=new GameObject("오프라인 저장 실패 시험");var c=root.AddComponent<오프라인생활Controller>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(오프라인생활Controller);
                type.GetField("scope",flags).SetValue(c,scope);type.GetField("bundle",flags).SetValue(c,b);
                type.GetField("state",flags).SetValue(c,s);type.GetField("reportFolder",flags).SetValue(c,invalid);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex(".+"));
                type.GetMethod("Step",flags).Invoke(c,null);
                Assert.IsTrue((bool)type.GetField("failed",flags).GetValue(c));
                Assert.IsFalse((bool)type.GetField("playing",flags).GetValue(c));
                var stopped=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).GetAwaiter().GetResult();
                type.GetMethod("Step",flags).Invoke(c,null);
                Assert.AreEqual(stopped.Revision,scope.Runtime.Sessions.GetAsync(scope.SessionStableId).GetAwaiter().GetResult().Revision);
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));CollectionAssert.AreEqual(new byte[]{1},File.ReadAllBytes(invalid));
                if(failure=="report")Assert.IsTrue(File.Exists(Path.Combine(folder,scope.SlotStableId+".ssalddel")),"보고 실패 전에 성공한 저장은 보존");
            } finally {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);SimulationWorldLocalRuntimeScope.Reset();
                for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],old[i]);
                if(Directory.Exists(folder))Directory.Delete(folder,true);
            }
        }
    }
}
