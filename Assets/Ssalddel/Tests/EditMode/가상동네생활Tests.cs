using System;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 가상동네생활Tests
    {
        [Test]
        public void 선택필드추가전_r4파일의원문검증과_변조차단을유지한다()
        {
            var folder=Path.Combine(Path.GetTempPath(),"ssalddel-life-save-"+Guid.NewGuid().ToString("N"));
            try
            {
                var scope=SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.OrderFlow,folder);
                scope.SaveCurrent();SimulationWorldLocalRuntimeScope.Reset();
                var path=Path.Combine(folder,가상배달관찰표본.OrderFlowSlotId+".ssalddel");
                var original=File.ReadAllText(path);
                using var document=JsonDocument.Parse(original);
                var package=document.RootElement.GetProperty("Package").GetRawText();
                var legacy=package;
                foreach(var name in new[]{"NeighborhoodLife","ObserverWorkingTicks","ObserverRestTicks","ObserverRestockThreshold"})legacy=legacy.Replace(",\""+name+"\":null",string.Empty);
                Assert.AreNotEqual(package,legacy,"이전 판본의 누락 선택 필드가 있어야 한다");
                string hash;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(legacy))).Replace("-",string.Empty).ToLowerInvariant();
                var envelope=original.Replace(package,legacy).Replace(document.RootElement.GetProperty("PackageSha256").GetString(),hash);
                File.WriteAllText(path,envelope);
                var runtime=LocalSimulationUnityRuntime.Create(folder);
                Assert.DoesNotThrow(()=>runtime.Sessions.VerifySlotAsync(가상배달관찰표본.OrderFlowSlotId).GetAwaiter().GetResult());
                // 원문 checksum만 변경해도 거절한다. 시험 폴더의 자동 백업은 이 변조 시험에서 제외한다.
                var backup=path+".bak";if(File.Exists(backup))File.Delete(backup);
                File.WriteAllText(path,envelope.Replace(hash,"invalid"));
                var error=Assert.Catch<Exception>(()=>runtime.Sessions.VerifySlotAsync(가상배달관찰표본.OrderFlowSlotId).GetAwaiter().GetResult());
                Assert.AreEqual("SimulationLocalSaveCorrupted",error.Message);
            }
            finally {SimulationWorldLocalRuntimeScope.Reset();if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }

        [Test]
        public void 생활프로필은_Runtime설치전에_무관한서버초기화를차단한다()
        {
            var previous=Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root=new GameObject("생활 초기화 경계 시험");
            try
            {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY","5");
                var daily=root.AddComponent<오늘작업계획SceneCompositionRoot>();
                var nature=root.AddComponent<네이처탐험조우CompositionRoot>();
                var hub=root.AddComponent<진부Hub입고UiSceneCompositionRoot>();
                var f=BindingFlags.Instance|BindingFlags.NonPublic;
                daily.GetType().GetMethod("Start",f).Invoke(daily,null);
                nature.GetType().GetMethod("Start",f).Invoke(nature,null);
                hub.InitializeAsync().GetAwaiter().GetResult();
                Assert.IsFalse(daily.서버기준사용중);
                Assert.IsFalse(nature.ServerAuthorityEnabled);
                Assert.IsFalse(hub.서버기준사용중);
                Assert.IsFalse(nature.RequestInFlight);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY",previous);}
        }

        [Test]
        public void 삼십분프로필은_기존저장과분리되고_표현은사본만읽는다()
        {
            var previous=Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root=new GameObject("생활 표현 시험");
            try
            {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY","5");
                Assert.AreEqual(Nature생존LaunchProfile.NeighborhoodLife,SimulationWorldLocalRuntimeScope.DetectLaunchProfile());
                Assert.AreEqual(1800,가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid()).DurationTicks);
                Assert.AreEqual(365,가상배달관찰표본.CreateOrderFlow(Guid.NewGuid()).DurationTicks);
                Assert.AreNotEqual(가상배달관찰표본.OrderFlowSlotId,가상배달관찰표본.NeighborhoodLifeSlotId);
                var life=new 가상동네생활Snapshot();
                life.Depot.X=-22;life.Depot.Z=-14;life.Depot.Stock[0]=7;
                var state=new 경영SimulationSessionSnapshot {CurrentTick=1800,DurationTicks=1800,IsCompleted=true,
                    WaitingFleet=new 가상배달대기Snapshot {NeighborhoodLife=life,Mart=new 가상마트Snapshot()}};
                var c=root.AddComponent<가상배달관찰Controller>();
                var f=BindingFlags.Instance|BindingFlags.NonPublic;
                c.GetType().GetMethod("Build",f).Invoke(c,null);
                c.GetType().GetField("state",f).SetValue(c,state);
                c.GetType().GetMethod("Render",f).Invoke(c,null);
                Assert.AreEqual(new Vector3(-22,.9f,-14),root.transform.Find("창고 작업자").position);
                Assert.AreEqual(-3,root.transform.Find("보충 화물차").position.z);
                Assert.AreEqual(90,root.transform.Find("바닥").localScale.x);
                Assert.IsTrue(root.transform.Find("보충 재고 0").gameObject.activeSelf);
                Assert.NotNull(root.transform.Find("화물 정차점 -26"));
                Assert.LessOrEqual(root.transform.Find("보충 화물차").GetComponent<Renderer>().bounds.max.z,-2);
                Assert.AreEqual(7,life.Depot.Stock[0]);
                // 종료 후 Tick은 Runtime 접근이나 저장을 시작하지 않는다.
                ((Task)c.GetType().GetMethod("Tick",f).Invoke(c,null)).GetAwaiter().GetResult();
                Assert.AreEqual(1800,state.CurrentTick);
                Assert.AreEqual(false,c.GetType().GetField("저장복구필요",f).GetValue(c));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY",previous);}
        }
    }
}
