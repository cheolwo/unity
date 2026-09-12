using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>현재 LH/Collider를 읽기만 한다. 초기화·지면 생성·높이 보정·조립·저장은 호출하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "LS01 실제 셀·표면 사본과 고정 변환 입력의 일치를 검사한다.",
        Boundary = "읽기 Probe는 Resolver 등록이나 실제 조립·통행·E5 승인이 아니다.")]
    public static class Farm경관LS01실측Probe
    {
        public const string Revision = "farm-ls01-live-probe.d375.r1";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Field(object value, string name) =>
            value?.GetType().GetField(name, Fields)?.GetValue(value);
        private static 공간LHStreamingEngine Engine() =>
            Object.FindFirstObjectByType<공간LHStreamingEngine>(FindObjectsInactive.Include)
            ?? throw new InvalidOperationException("LS01:EngineMissing");
        private static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };
        private static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
        private static string Hash(string value) {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private static object Cell(string key) {
            var cells = (IDictionary)Field(Engine(), "cells");
            if (cells == null || !cells.Contains(key)) throw new InvalidOperationException("LS01:TrackedCellMissing");
            return cells[key];
        }
        private static I공간동적표면Provider Provider() {
            var repository = Field(Engine(), "repository");
            var local = repository is 로컬공간LHWorldEngine ? repository : Field(repository, "lhEngine");
            return Field(local, "surfaceProvider") as I공간동적표면Provider
                ?? throw new InvalidOperationException("LS01:ExistingSurfaceProviderMissing");
        }
        private static object Pose(Transform t) => t == null ? null : new {
            path = PathOf(t), world = V(t.position), local = V(t.localPosition),
            rotation = V(t.eulerAngles), scale = V(t.lossyScale)
        };

        public static string Capture()
        {
            var e = Engine();
            var roots = Enumerable.Range(0, SceneManager.sceneCount).SelectMany(i => SceneManager.GetSceneAt(i).GetRootGameObjects()).ToArray();
            var transforms = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var state = transforms.Select(t => new {path=PathOf(t), active=t.gameObject.activeSelf, local=V(t.localPosition),
                rotation=V(t.localEulerAngles), scale=V(t.localScale),
                components=t.GetComponents<Component>().Select(c=>c==null?"MissingScript":c.GetType().FullName).ToArray()}).ToArray();
            var cells = (IDictionary)Field(e, "cells");
            var rows = new List<object>();
            if (cells != null) foreach (DictionaryEntry entry in cells) {
                var runtime=entry.Value;
                var floor=Field(runtime,"Floor") as GameObject;
                var collider=floor == null ? null : floor.GetComponent<Collider>();
                rows.Add(new { key=entry.Key, plan=Field(runtime,"Plan"), state=Field(runtime,"State")?.ToString(),
                    root=Pose((Field(runtime,"Root") as GameObject)?.transform),floor=Pose(floor?.transform),
                    collider=collider==null?null:new{type=collider.GetType().FullName,collider.enabled,collider.isTrigger,
                        active=collider.gameObject.activeInHierarchy,center=V(collider.bounds.center),size=V(collider.bounds.size)} });
            }
            object provider=null;string providerFailure="";
            try {var p=Provider();provider=new {type=p.GetType().FullName,p.SurfaceModeCode,p.SurfaceRuleRevision,p.SurfaceConfigurationHashSha256};}
            catch(InvalidOperationException ex){providerFailure=ex.Message;}
            object session=null;
            if(SimulationWorldLocalRuntimeScope.TryGet(out var scope)) {
                var current=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).AsTask().GetAwaiter().GetResult();
                session=new {scope.SessionStableId,scope.SlotStableId,scope.RestoredFromSlot,scope.RecoveredFromBackup,
                    current.Revision,current.WorldContext.WorldRevision,current.WorldContext.WorldTick};
            }
            return JsonConvert.SerializeObject(new {revision=Revision,utc=DateTime.UtcNow,
                playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,
                scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Select(s=>new{s.path,s.isDirty,s.rootCount}).ToArray(),
                selection=Selection.entityIds.Select(x=>x.ToString()).ToArray(),preview=UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount,
                objectCount=state.Length,sceneStateHash=Hash(JsonConvert.SerializeObject(state)),
                e.IsInitialized,e.SourceModeCode,e.PlayerCellKey,e.L3CellWorldSize,origin=V(e.CellOriginWorldPosition(공간LHCellKey.L3(공간LHWorldCodes.CenterL3X,공간LHWorldCodes.CenterL3Y))),
                provider,providerFailure,session,cells=rows});
        }

        public static string Sample(string cellKey, double[][] localPoints)
        {
            var reader = new 관찰SurfaceReader(cellKey);
            var rows = localPoints.Select(p => {
                if(p == null || p.Length!=2) throw new ArgumentException("LS01:ExpectedLocalXZ");
                var sample=reader.Read(reader.Origin.x+p[0],reader.Origin.z+p[1]);
                return new {localX=p[0],localZ=p[1],sample};
            }).ToArray();
            return JsonConvert.SerializeObject(new {probeRevision=Revision,cellKey,reader.Revision,reader.HashSha256,origin=V(reader.Origin),rows});
        }

        public static string ValidateFrozen(string cellKey, string requestJson)
        {
            var reader = new 관찰SurfaceReader(cellKey);
            var request = System.Text.Json.JsonSerializer.Deserialize<SimulationFarm경관고정배치Request>(requestJson)
                ?? throw new ArgumentException("LS01:InputMissing");
            var a=request.BaseRequest;
            var plan=(공간LHCellPlanData)Field(Cell(cellKey),"Plan");
            if(plan.WorldAssetAssembly==null) throw new InvalidOperationException("LS01:WorldRevisionSnapshotMissing");
            if(a.OwnerCellStableId!=cellKey || a.CellSizeMeters!=Engine().L3CellWorldSize
                || a.CellWorldOriginXMeters!=reader.Origin.x || a.CellWorldOriginYMeters!=reader.Origin.y || a.CellWorldOriginZMeters!=reader.Origin.z
                || a.MapPlan.SourceWorldRevision!=plan.WorldAssetAssembly.SourceWorldRevision
                || request.SurfaceEvidenceKindCode!="ProvidedSurfaceSnapshot")
                throw new InvalidOperationException("LS01:ActualFrameOrRevisionMismatch");
            // 현재 Unity RuntimeCore 패키지가 변환기를 노출하지 않을 수 있다. 새 DLL을 로드하거나 복제하지 않는다.
            var serviceType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(
                "Ssalddel.Simulation.Application.SimulationFarm경관고정배치Service",false)).FirstOrDefault(t=>t!=null)
                ?? throw new InvalidOperationException("LS01:ApprovedConverterAssemblyUnavailable");
            var method=serviceType.GetMethod("Convert",new[]{typeof(SimulationFarm경관고정배치Request),typeof(ISimulationFarmH2SurfaceReader)})
                ?? throw new InvalidOperationException("LS01:ApprovedConverterSignatureUnavailable");
            var result=method.Invoke(Activator.CreateInstance(serviceType),new object[]{request,reader});
            return System.Text.Json.JsonSerializer.Serialize(result,result.GetType());
        }

        private sealed class 관찰SurfaceReader : ISimulationFarmH2SurfaceReader
        {
            private readonly string cellKey;
            private readonly Collider floor;
            private readonly I공간동적표면Provider provider;
            private readonly int cellX,cellY;
            private readonly string initial;
            public Vector3 Origin { get; }
            public string Revision => Farm경관LS01실측Probe.Revision;
            public string HashSha256 => Fingerprint();
            public 관찰SurfaceReader(string key) {
                cellKey=key;var runtime=Cell(key);var root=Field(runtime,"Root") as GameObject;
                var plan=(공간LHCellPlanData)Field(runtime,"Plan");
                if(root==null || plan==null || !EditorApplication.isPlaying) throw new InvalidOperationException("LS01:LiveCellRequired");
                if(root.transform.lossyScale!=Vector3.one || Quaternion.Angle(root.transform.rotation,Quaternion.identity)>.0001f)
                    throw new InvalidOperationException("LS01:UnsupportedCellTRS");
                floor=(Field(runtime,"Floor") as GameObject)?.GetComponent<Collider>();
                if(floor==null || !floor.enabled || floor.isTrigger || !floor.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("LS01:ActiveGroundColliderMissing");
                provider=Provider();cellX=plan.CellX;cellY=plan.CellY;Origin=root.transform.position;initial=Fingerprint();
            }
            private string Fingerprint() {
                var plan=(공간LHCellPlanData)Field(Cell(cellKey),"Plan");
                return Hash(JsonConvert.SerializeObject(new {cellKey,plan.BasePlanHashSha256,plan.Surface,
                    provider.SurfaceModeCode,provider.SurfaceRuleRevision,provider.SurfaceConfigurationHashSha256,
                    pose=Pose(floor.transform),center=V(floor.bounds.center),size=V(floor.bounds.size),
                    collider=EditorJsonUtility.ToJson(floor),floor.enabled,floor.isTrigger,active=floor.gameObject.activeInHierarchy}));
            }
            public SimulationFarmH2SurfaceSample Read(double worldX,double worldZ) {
                if(Fingerprint()!=initial) throw new InvalidOperationException("LS01:LiveSurfaceChanged");
                var x=worldX-Origin.x;var z=worldZ-Origin.z;var size=Engine().L3CellWorldSize;
                if(double.IsNaN(x)||double.IsNaN(z)||double.IsInfinity(x)||double.IsInfinity(z)||Math.Abs(x)>size/2||Math.Abs(z)>size/2)
                    return new SimulationFarmH2SurfaceSample {Supported=false};
                if(!provider.TrySample(cellX*size+x,cellY*size+z,out var source) || source==null)
                    return new SimulationFarmH2SurfaceSample {Supported=false};
                var ray=new Ray(new Vector3((float)worldX,floor.bounds.max.y+10,(float)worldZ),Vector3.down);
                if(!floor.Raycast(ray,out var hit,floor.bounds.size.y+20))
                    return new SimulationFarmH2SurfaceSample {Supported=false};
                return new SimulationFarmH2SurfaceSample {Supported=true,PlacementAllowed=source.배치허용,
                    HeightMeters=hit.point.y,SlopeDegrees=Math.Max(source.표면경사Degrees,Vector3.Angle(hit.normal,Vector3.up))};
            }
        }
    }
}
