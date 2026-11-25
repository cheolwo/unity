using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ssalddel.MobileGame
{
    /// <summary>명시적인 검토 실행에서만 uGUI와 내장 코어를 대조한다. 물리 터치 검증과 구분한다.</summary>
    public sealed class MobileGamePlayerProof : MonoBehaviour
    {
        private MobileGameEntry entry;
        private string output;
        private int checks;
        private readonly JArray events = new JArray();
        public void Begin(MobileGameEntry value, string directory)
        {
            entry = value; output = directory; Directory.CreateDirectory(output);
            StartCoroutine(Guarded());
        }
        private IEnumerator Guarded()
        {
            var iterator = Verify();
            while (true)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = iterator.MoveNext(); if (next) current = iterator.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    File.WriteAllText(Path.Combine(output,"proof.json"), new JObject
                    { ["status"]="failed",["checks"]=checks,["error"]=failure.ToString(),["events"]=events }.ToString(Formatting.Indented));
                    Debug.LogException(failure); Application.Quit(2); yield break;
                }
                if (!next) yield break;
                yield return current;
            }
        }
        private void Check(bool condition,string name)
        {
            if(!condition)throw new InvalidOperationException(name);
            checks++;events.Add(new JObject{["check"]=name,["result"]="passed"});
        }
        private IEnumerator WaitTask(Task task)
        {
            while(!task.IsCompleted)yield return null;
            task.GetAwaiter().GetResult();
        }
        private IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,file));
            yield return null; yield return null;
        }
        private void Tap(Vector2 point)
        {
            var data=new PointerEventData(EventSystem.current){position=point,pointerId=-1,button=PointerEventData.InputButton.Left};
            entry.View.Map.OnPointerDown(data);entry.View.Map.OnPointerUp(data);
        }
        private IEnumerator Verify()
        {
            yield return null;yield return null;
            Check(entry.Session!=null && entry.View!=null,"same-process session and canvas attached");
            Check(Resources.Load<Font>("HongdalMobile/Korean")!=null,"packaged Korean font");
            foreach(var name in MobileTouchStateChecks.Run()) Check(true,"gesture regression: "+name);
            yield return Capture("00-ready.png");
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"start\"}"));
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"pause\"}"));
            yield return null;
            var initial=entry.View.CurrentState;
            Check(initial.orders.Length==2 && initial.couriers.Length==2,"two virtual orders and couriers");
            Check(initial.orders.All(x=>x.stage=="Waiting"),"initial orders unassigned");
            Check(JObject.Parse(entry.Session.SnapshotJson)["clock"]["paused"].Value<bool>(),"explicit pause");
            var fixedTime=initial.elapsedMs;
            yield return new WaitForSeconds(.25f);
            Check(entry.View.CurrentState.elapsedMs==fixedTime,"pause does not advance simulation");
            var gesture=new MobileTouchState();
            gesture.Begin(0,0,true);gesture.Move(20,0,5);Check(!gesture.End(true),"drag is not dispatch tap");
            gesture.Begin(0,0,true);gesture.CancelForPinch();Check(!gesture.End(true),"pinch cancels tap");
            gesture.AllPointersReleased();gesture.Begin(2,2,true);Check(gesture.End(true),"tap works after fingers released");
            foreach(var pair in initial.orders.Select((order,index)=>new {order, courier=initial.couriers[index]}).ToArray())
            {
                events.Add(new JObject{["screen"]=$"{Screen.width}x{Screen.height}",
                    ["courierPoint"]=entry.View.Map.ScreenPointForCourier(pair.courier.id).ToString(),
                    ["storePoint"]=entry.View.Map.ScreenPointForStore(pair.order.storeId).ToString(),
                    ["mapRect"]=entry.View.Map.rectTransform.rect.ToString()});
                Tap(entry.View.Map.ScreenPointForCourier(pair.courier.id));
                yield return null;
                Check(entry.View.Map.AssignmentArmed,"unassigned courier tap arms dispatch");
                Tap(entry.View.Map.ScreenPointForStore(pair.order.storeId));
                yield return null;yield return null;
                Check(entry.View.CurrentState.orders.Single(x=>x.id==pair.order.id).driverId==pair.courier.id,"map tap assigned "+pair.order.id);
                Check(!entry.View.Map.AssignmentArmed,"accepted assignment disarms without losing focus");
            }
            yield return Capture("01-assigned.png");
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"set-speed\",\"speed\":4}"));
            var deadline=Time.realtimeSinceStartup+120;
            while(!entry.View.CurrentState.orders.All(x=>x.stage=="AtPickup" && x.canConfirmPickup))
            {
                if(Time.realtimeSinceStartup>=deadline)throw new InvalidOperationException("pickup arrival timeout");
                yield return new WaitForSeconds(.15f);
            }
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"pause\"}"));
            Check(entry.View.CurrentState.orders.All(x=>x.stage=="AtPickup"),"manual pickup gate holds both orders");
            var pausedTime=entry.View.CurrentState.elapsedMs;
            yield return new WaitForSeconds(.3f);
            Check(entry.View.CurrentState.elapsedMs==pausedTime,"pickup pause stable");
            yield return Capture("02-pickup-wait.png");
            foreach(var order in entry.View.CurrentState.orders.ToArray())
            {
                entry.View.SelectCourier(order.driverId);yield return null;
                var button=entry.View.GetComponentsInChildren<Button>(true).Single(x=>x.gameObject.activeInHierarchy &&
                    x.GetComponentInChildren<Text>()!=null && x.GetComponentInChildren<Text>().text=="픽업 확인");
                Check(button.interactable,"pickup button enabled for assigned courier");
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                yield return null;yield return null;
                Check(entry.View.CurrentState.orders.Single(x=>x.id==order.id).stage!="AtPickup","app pickup changes game "+order.id);
            }
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"set-speed\",\"speed\":4}"));
            yield return Capture("03-delivering.png");
            deadline=Time.realtimeSinceStartup+120;
            while(!entry.View.CurrentState.settled)
            {
                if(Time.realtimeSinceStartup>deadline)throw new InvalidOperationException("settlement timeout");
                yield return new WaitForSeconds(.15f);
            }
            var settlement=entry.View.CurrentState.settlement;
            Check(settlement.revenue==9600 && settlement.driverPay==7200 && settlement.balance==3600,"legacy virtual settlement preserved");
            entry.View.ShowResults();yield return null;
            yield return Capture("04-settled.png");
            var restoreTime=entry.View.CurrentState.elapsedMs;
            entry.Session.gameObject.SendMessage("OnApplicationPause",true,SendMessageOptions.RequireReceiver);
            yield return new WaitForSeconds(.2f);
            entry.Session.gameObject.SendMessage("OnApplicationPause",false,SendMessageOptions.RequireReceiver);
            yield return null;
            Check(entry.View.CurrentState.elapsedMs==restoreTime,"background lifecycle does not advance time");
            var host=new GameObject("Restore Verification");
            var restored=MobileGameSession.Attach(host,Resources.Load<TextAsset>("HongdalMobile/delivery-sample").text,
                Resources.Load<TextAsset>("HongdalMobile/road-graph").text,Path.Combine(output,"session"));
            var restoredState=JObject.Parse(restored.SnapshotJson);
            Check(restoredState["state"]["settled"].Value<bool>(),"confirmed save restored");
            Check(restoredState["state"]["settlement"]["balance"].Value<int>()==3600,"restore does not duplicate pay");
            Check(restoredState["clock"]["paused"].Value<bool>(),"restored game waits for resume");
            Destroy(host);
            File.WriteAllText(Path.Combine(output,"final-state.json"),entry.Session.SnapshotJson);
            var logPath=Path.Combine(output,"session/game-experiences.v1.jsonl");
            var records=File.ReadAllLines(logPath).Select(JObject.Parse).ToArray();
            Check(records.Count(x=>(string)x["action"]=="round-outcome")==1,"completed result recorded once across restore");
            Check(records.Single(x=>(string)x["action"]=="round-outcome")["settlement"]!=null,"experience includes confirmed virtual settlement");
            File.WriteAllText(Path.Combine(output,"session/confirmed-session.v1.json"),"{invalid-test-save");
            var recoveryHost=new GameObject("Previous Save Verification");
            var recovery=MobileGameSession.Attach(recoveryHost,Resources.Load<TextAsset>("HongdalMobile/delivery-sample").text,
                Resources.Load<TextAsset>("HongdalMobile/road-graph").text,Path.Combine(output,"session"));
            Check(JObject.Parse(recovery.SnapshotJson)["state"]["settled"].Value<bool>(),"corrupt primary recovers previous confirmed save");
            Destroy(recoveryHost);
            var files=new TestFiles();entry.Session.SetFileExchange(files);
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"export\"}"));
            Check(!string.IsNullOrEmpty(files.Text),"explicit export reaches file exchange port");
            var exported=JObject.Parse(files.Text);
            Check((string)exported["sourceKind"]=="Simulation" && (bool?)exported["operationalActionsAllowed"]!=true,"export retains simulation boundary");
            files.Read=files.Text;
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"import\"}"));
            Check(JObject.Parse(entry.Session.SnapshotJson)["ok"].Value<bool>(),"supported reference import");
            Check(entry.View.CurrentState.settled && entry.View.CurrentState.settlement.balance==3600,"reference import does not overwrite game or ledger");
            files.Read="{\"schemaVersion\":\"reality-game-context.v1\",\"sourceKind\":\"Derived\",\"items\":[{\"customerPhone\":\"SYNTHETIC-TEST\"}]}";
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"import\"}"));
            Check(!JObject.Parse(entry.Session.SnapshotJson)["ok"].Value<bool>(),"customer field rejected before import");
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"start\"}"));
            yield return WaitTask(entry.Session.RequestJsonAsync("{\"action\":\"pause\"}"));
            var first=entry.View.CurrentState.orders.First();
            yield return WaitTask(entry.Session.RequestJsonAsync(new JObject{["action"]="ai-trial",["orderId"]=first.id}.ToString(Formatting.None)));
            Check(entry.View.CurrentState.orders.Single(x=>x.id==first.id).stage!="Waiting","rule advisor assigns an eligible virtual courier");
            var trial=File.ReadAllLines(logPath).Select(JObject.Parse).Last(x=>(string)x["action"]=="ai-trial");
            Check((bool?)trial["modelInvoked"]!=true && (bool?)trial["weightsTrained"]!=true && (bool?)trial["approvedForTraining"]!=true,"rule experience is not model training or human approval");
            yield return Capture("05-rule-advisor.png");
            File.WriteAllText(Path.Combine(output,"proof.json"),new JObject
            { ["status"]="passed",["checks"]=checks,["physicalAndroidTouchVerified"]=false,
              ["scope"]="Compiled Unity Windows mobile-layout review with uGUI pointer events and in-process core",
              ["events"]=events }.ToString(Formatting.Indented));
            Application.Quit(0);
        }
        private sealed class TestFiles : IMobileGameFileExchange
        {
            public string Text,Read;
            public Task<string> ReadTextAsync()=>Task.FromResult(Read);
            public Task ShareTextAsync(string name,string text,string mime){Text=text;return Task.CompletedTask;}
        }
    }
}
