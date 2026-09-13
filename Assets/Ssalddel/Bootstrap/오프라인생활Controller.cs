using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>명시적으로 선택한 JSON 시작 묶음의 표현만 소유한다. NPC의 업무 권위는 LocalSimulationRuntime이다.</summary>
    public sealed class 오프라인생활Controller : MonoBehaviour
    {
        private const int ViewLayer = 관찰표현LayerContract.오프라인생활Layer;
        private readonly Dictionary<string,GameObject> objects = new(StringComparer.Ordinal);
        private readonly List<Material> materials = new();
        private SimulationWorldLocalRuntimeScope scope;
        private 로컬생활시작묶음 bundle;
        private 경영SimulationSessionSnapshot state;
        private Transform viewRoot;
        private Camera cameraView;
        private bool playing, failed, ownsScope;
        private float elapsed;
        private string message="준비 중", reportFolder;
        private Vector2 scroll;
        public int OwnedObjectCount => objects.Count;
        public string[] OwnedIds => objects.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToArray();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!SimulationWorldLocalRuntimeScope.OfflineLifeRequested || SceneManager.GetActiveScene().name != "SimulationWorldShell"
                || FindAnyObjectByType<오프라인생활Controller>() != null) return;
            new GameObject("오프라인 생활 · 명시 구성").AddComponent<오프라인생활Controller>();
        }
        private void Start()
        {
            try {
                if(SimulationWorldLocalRuntimeScope.IsInstalled) throw new InvalidOperationException("OfflineLifeScopeAlreadyOwned");
                if(ViewLayerOccupied(FindObjectsByType<Transform>(
                       FindObjectsInactive.Include,FindObjectsSortMode.None)))
                    throw new InvalidOperationException("OfflineLifeViewLayerOccupied");
                scope=SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.OfflineLife,null);ownsScope=true;
                bundle=로컬생활시작자료.Copy(scope.OfflineBundle);
                reportFolder=Path.Combine(Environment.GetEnvironmentVariable("SSALDDEL_LOCAL_SAVE_ROOT") ??
                    Path.Combine(UnityEngine.Application.persistentDataPath,"Saves"),"offline-life-reports",bundle.ContentSha256);
                state=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).AsTask().GetAwaiter().GetResult();
                Build();Render();Persist(true);
                playing=!state.IsCompleted;message=scope.RestoredFromSlot?"저장된 시점부터 이어갑니다. 종료 시간 따라잡기 없음.":"오프라인 생활 시작 · 서버와 DB 요청 없음";
            } catch(Exception e) { Fail(e);CleanupView();if(ownsScope){SimulationWorldLocalRuntimeScope.Reset();ownsScope=false;} }
        }
        private static bool ViewLayerOccupied(IEnumerable<Transform> candidates)
            => candidates != null && candidates.Any(x => x != null && x.gameObject.layer == ViewLayer);
        private void Update()
        {
            if(!playing || failed || state==null || state.IsCompleted) {elapsed=0;return;}
            elapsed+=Mathf.Min(Time.unscaledDeltaTime,1f);
            if(elapsed<1f)return;elapsed=0;Step();
        }
        private void Step()
        {
            if(failed || state==null || state.IsCompleted)return;
            try {
                scope.Runtime.Sessions.AdvanceWorldTickAsync(state.SessionStableId,new 경영SimulationTick진행Request {
                    CommandId="offline:tick:"+state.CurrentTick,ExpectedRevision=state.Revision,TickCount=1}).AsTask().GetAwaiter().GetResult();
                state=scope.Runtime.Sessions.GetAsync(scope.SessionStableId).AsTask().GetAwaiter().GetResult();
                Persist(state.CurrentTick%30==0 || state.IsCompleted);Render();
                if(state.IsCompleted){playing=false;message="1800 Tick 종료 · 보고 사본 보관 대기";}
            } catch(Exception e) {Fail(e);}
        }
        private void Persist(bool report)
        {
            scope.SaveCurrent();
            if(!report)return;
            var r=scope.Runtime.GetLocalLifeReportAsync(scope.SessionStableId,bundle).AsTask().GetAwaiter().GetResult();
            var path=Path.Combine(reportFolder,r.ContentSha256+".json");
            로컬생활시작자료.WriteOnce(path,로컬생활시작자료.JsonBytes(r),로컬생활시작자료.MaxReportBytes);
        }
        private void Build()
        {
            viewRoot=new GameObject("오프라인 생활 · 소유 표현").transform;viewRoot.SetParent(transform,false);
            var cam=new GameObject("오프라인 생활 카메라");cam.transform.SetParent(viewRoot,false);
            cameraView=cam.AddComponent<Camera>();cameraView.orthographic=true;cameraView.orthographicSize=44;
            cameraView.transform.position=new Vector3(0,65,-47);cameraView.transform.LookAt(new Vector3(0,0,7));
            cameraView.clearFlags=CameraClearFlags.SolidColor;cameraView.backgroundColor=new Color(.10f,.13f,.16f);
            cameraView.cullingMask=관찰표현LayerContract.오프라인생활Mask;cameraView.depth=100;
            foreach(var item in bundle.Payload.PlacementMap.GetProperty("instances").EnumerateArray()) {
                var slot=item.GetProperty("id").GetString();var binding=bundle.Payload.InitialRequest.LocalLife.Facilities.FirstOrDefault(x=>x.SlotKey==slot);
                var road=item.GetProperty("kind").GetString()=="Road";
                var id=binding?.FacilityStableId??slot;
                Add(id,PrimitiveType.Cube,new Vector3((float)item.GetProperty("width").GetDouble(),road ? .08f : 2f,(float)item.GetProperty("depth").GetDouble()),
                    road?new Color(.25f,.29f,.33f):new Color(.64f,.66f,.57f)).transform.position=
                    new Vector3((float)item.GetProperty("x").GetDouble(),road?-.06f:1f,(float)item.GetProperty("z").GetDouble());
            }
            foreach(var actor in bundle.Payload.InitialRequest.LocalLife.Actors) {
                Add(actor.ActorStableId,PrimitiveType.Capsule,new Vector3(.65f,.85f,.65f),actor.Role=="Resident"?Color.cyan:actor.Role=="RestaurantOperator"?new Color(.9f,.3f,.2f):Color.white);
                if(actor.Role=="Courier" || actor.Role=="FreightDriver")
                    Add("vehicle:"+actor.ActorStableId,PrimitiveType.Cube,actor.Role=="Courier"?new Vector3(.5f,.5f,1.1f):new Vector3(1.4f,1.6f,3),new Color(.2f,.45f,.7f));
            }
        }
        private GameObject Add(string id,PrimitiveType shape,Vector3 scale,Color color)
        {
            if(objects.ContainsKey(id))throw new InvalidOperationException("OfflineLifeDuplicateViewId");
            var obj=GameObject.CreatePrimitive(shape);obj.name=id;obj.transform.SetParent(viewRoot,false);obj.layer=ViewLayer;
            objects.Add(id,obj);obj.transform.localScale=scale;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("OfflineLifeShaderMissing");
            var material=new Material(shader);materials.Add(material);material.SetColor("_BaseColor",color);obj.GetComponent<Renderer>().sharedMaterial=material;
            // 표현 Collider가 생활 Core의 도착/픽업/수령을 결정하지 않는다.
            obj.GetComponent<Collider>().enabled=false;return obj;
        }
        private void Render()
        {
            var fleet=state.WaitingFleet;var life=fleet.NeighborhoodLife;
            foreach(var a in life.Actors) {
                if(!objects.TryGetValue(a.ActorId,out var obj))throw new InvalidOperationException("OfflineLifeViewBindingMissing");
                var target=new Vector3((float)a.X,.85f,(float)a.Z);var delta=target-obj.transform.position;
                if(delta.x*delta.x+delta.z*delta.z>.001f)obj.transform.rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z));
                obj.transform.position=target;
            }
            foreach(var driver in fleet.Drivers) MoveVehicle(driver.Courier);
            MoveVehicle(life.Depot.Truck);
        }
        private void MoveVehicle(가상배달기사Snapshot courier) {
            var obj=objects["vehicle:"+courier.ActorStableId];obj.transform.position=new Vector3((float)courier.VehicleX,.35f,(float)courier.VehicleZ);
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12,12,Mathf.Min(370,Screen.width-24),Mathf.Min(600,Screen.height-24)),GUI.skin.box);
            GUILayout.Label("오프라인 생활 · 합성 동네 / 운영 주문 아님");GUILayout.Label(message);
            if(state!=null) {
                GUILayout.Label($"{state.CurrentTick}/1800 Tick · Revision {state.Revision} · NPC {state.WaitingFleet.NeighborhoodLife.Actors.Length}");
                GUI.enabled=!failed&&!state.IsCompleted;
                if(GUILayout.Button(playing?"일시정지":"계속")){playing=!playing;elapsed=0;if(!playing)try{Persist(true);}catch(Exception e){Fail(e);}}
                if(GUILayout.Button("1 Tick 진행")){playing=false;Step();}GUI.enabled=!failed;
                if(GUILayout.Button("저장 · 보고 사본 남기기"))try{Persist(true);message="로컬 저장 완료 · Mongo 보관은 별도 도구에서 수행";}catch(Exception e){Fail(e);}
                GUI.enabled=true;scroll=GUILayout.BeginScrollView(scroll);
                foreach(var a in state.WaitingFleet.NeighborhoodLife.Actors) {
                    var name=bundle.Payload.InitialRequest.LocalLife.Actors.Single(x=>x.ActorStableId==a.ActorId).DisplayName;
                    GUILayout.Label(name+" · "+a.Role+" · "+a.Stage+"\n"+a.Reason+" · "+a.TaskId);
                } GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
        private void Fail(Exception e){playing=false;failed=true;message="중단: "+e.Message;Debug.LogException(e);}
        private void OnApplicationPause(bool paused){if(paused){playing=false;elapsed=0;}}
        private void OnDestroy() {
            playing=false;
            if(ownsScope){if(!failed&&state!=null)try{Persist(true);}catch(Exception e){Debug.LogException(e);}
                SimulationWorldLocalRuntimeScope.Reset();ownsScope=false;}
            CleanupView();
        }
        private void CleanupView(){objects.Clear();if(viewRoot!=null)DestroyOwned(viewRoot.gameObject);viewRoot=null;
            foreach(var m in materials)if(m!=null)DestroyOwned(m);materials.Clear();}
        private static void DestroyOwned(UnityEngine.Object obj){if(UnityEngine.Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
    }
}
