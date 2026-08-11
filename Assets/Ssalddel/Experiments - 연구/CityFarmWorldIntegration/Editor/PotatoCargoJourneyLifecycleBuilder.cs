using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class PotatoCargoJourneyLifecycleBuilder
    {
        public const string ScenePath=연구Scene경로.감자생산유통+"/감자화물전체이동흐름.unity";
        public const string RootName="JOURNEY-1 Potato Cargo Farm Hub";
        public const string EvidencePath="Documentation/Changes/2026-08-10-potato-cargo-journey-lifecycle/potato-cargo-journey-game-view.png";

        [MenuItem("Ssalddel/JOURNEY-1/Build Potato Cargo Journey Lifecycle")]
        public static void Build()
        {
            if(!File.Exists(PotatoJourneyHubRouteBuilder.ScenePath))PotatoJourneyHubRouteBuilder.Build();
            EditorSceneManager.OpenScene(PotatoJourneyHubRouteBuilder.ScenePath,OpenSceneMode.Single);
            var scene=SceneManager.GetActiveScene();var world=GameObject.Find("WorldBootstrap")??throw new InvalidOperationException("JourneyWorldMissing");
            var previous=world.transform.Find(RootName);if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var cargo=world.transform.Find(PotatoHarvestCargoLifecycleBuilder.RootName)?.GetComponent<PotatoHarvestCargoLifecyclePresenter>()??throw new InvalidOperationException("JourneyCargoMissing");
            var cargoCanvas=cargo.transform.Find("PotatoHarvestCargoCanvas");if(cargoCanvas!=null)cargoCanvas.gameObject.SetActive(false);
            var hub=world.transform.Find(PotatoJourneyHubRouteBuilder.RootName)?.GetComponent<PotatoJourneyHubRoutePresenter>()??throw new InvalidOperationException("JourneyHubRouteMissing");
            var hubCanvas=hub.transform.Find("PotatoJourneyHubDataCanvas");if(hubCanvas!=null)hubCanvas.gameObject.SetActive(false);
            var root=new GameObject(RootName);root.transform.SetParent(world.transform,false);var presenter=root.AddComponent<PotatoCargoJourneyLifecyclePresenter>();
            var ui=BuildUi(root.transform,presenter);presenter.Configure(cargo,hub.RouteFollower,ui.State,ui.Cargo,ui.Progress,ui.Lineage,ui.Action,ui.Limitation,true);
            ValidateOpenScene();Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("JourneySceneSaveFailed");AssetDatabase.SaveAssets();Selection.activeGameObject=root;
            Debug.Log("PotatoCargoJourneyLifecycleBuilt:"+ScenePath);
        }

        [MenuItem("Ssalddel/JOURNEY-1/Validate Potato Cargo Journey Lifecycle")]
        public static void ValidateOpenScene()
        {
            var root=GameObject.Find("WorldBootstrap/"+RootName)??throw new InvalidOperationException("JourneyRootMissing");var presenter=root.GetComponent<PotatoCargoJourneyLifecyclePresenter>()??throw new InvalidOperationException("JourneyPresenterMissing");presenter.ResetJourney();
            var actions=root.GetComponentsInChildren<PotatoCargoJourneyActionButton>(true).Length;
            if(!presenter.ValidateWiring()||presenter.CurrentModel.StateCode!="Loaded"||actions!=7)throw new InvalidOperationException("JourneyInitialPresentationInvalid:actions="+actions);
        }

        [MenuItem("Ssalddel/JOURNEY-1/Capture Potato Cargo Journey Play Mode")]
        public static void CapturePlayMode()
        {if(!EditorApplication.isPlaying)throw new InvalidOperationException("JourneyCaptureRequiresPlayMode");var presenter=GameObject.Find("WorldBootstrap/"+RootName)?.GetComponent<PotatoCargoJourneyLifecyclePresenter>()??throw new InvalidOperationException("JourneyPresenterMissing");presenter.RunGoldenPath();var absolute=Path.GetFullPath(EvidencePath);Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);ScreenCapture.CaptureScreenshot(absolute,1);Debug.Log("PotatoCargoJourneyCaptureRequested:"+absolute);}

        private static Ui BuildUi(Transform parent,PotatoCargoJourneyLifecyclePresenter presenter)
        {
            var canvasObject=new GameObject("PotatoCargoJourneyCanvas");canvasObject.transform.SetParent(parent,false);var canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;canvasObject.AddComponent<GraphicRaycaster>();var scaler=canvasObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1100,506);
            var panel=Panel(canvasObject.transform,"JourneyPanel",new Vector2(.035f,.035f),new Vector2(.635f,.49f),new Color(.025f,.043f,.052f,.95f));Panel(panel,"JourneyAccent",new Vector2(0,.955f),Vector2.one,new Color(.08f,.74f,.94f));
            Text(panel,"Eyebrow","JOURNEY-1 · LOADED TO ARRIVED AT HUB",new Vector2(.035f,.86f),new Vector2(.965f,.955f),18,new Color(.35f,.88f,1f),FontStyle.Bold);
            var state=Text(panel,"State","STATE",new Vector2(.035f,.70f),new Vector2(.965f,.86f),20,Color.white,FontStyle.Bold);
            var cargo=Text(panel,"Cargo","CARGO",new Vector2(.035f,.58f),new Vector2(.965f,.70f),14,new Color(.4f,.92f,.72f),FontStyle.Bold);
            var progress=Text(panel,"Progress","ROUTE",new Vector2(.035f,.47f),new Vector2(.965f,.58f),15,new Color(1f,.8f,.3f),FontStyle.Bold);
            var lineage=Text(panel,"Lineage","LINEAGE",new Vector2(.035f,.32f),new Vector2(.965f,.47f),11,new Color(.72f,.86f,.95f),FontStyle.Bold);
            var action=Text(panel,"Action","ACTION",new Vector2(.035f,.235f),new Vector2(.965f,.32f),13,new Color(.42f,.85f,.94f),FontStyle.Bold);
            var limit=Text(panel,"Limit","LIMIT",new Vector2(.035f,.155f),new Vector2(.965f,.235f),11,new Color(.72f,.75f,.72f),FontStyle.Normal);
            var actions=new[]{("RESET",PotatoCargoJourneyActionCodes.Reset),("DISPATCH",PotatoCargoJourneyActionCodes.ReviewDispatch),("CONFIRM",PotatoCargoJourneyActionCodes.Confirm),("APPLY TICK",PotatoCargoJourneyActionCodes.ApplyTick),("+1 ROUTE",PotatoCargoJourneyActionCodes.AdvanceOne),("TO HUB",PotatoCargoJourneyActionCodes.AdvanceToHub),("GOLDEN",PotatoCargoJourneyActionCodes.GoldenPath)};
            for(var i=0;i<actions.Length;i++){var min=.035f+i*.133f;Button(panel,"Action_"+actions[i].Item2,actions[i].Item1,actions[i].Item2,presenter,new Vector2(min,.04f),new Vector2(min+.12f,.145f));}
            return new Ui(state,cargo,progress,lineage,action,limit);
        }
        private static RectTransform Panel(Transform p,string n,Vector2 min,Vector2 max,Color c){var o=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));o.transform.SetParent(p,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;o.GetComponent<Image>().color=c;return r;}
        private static Text Text(Transform p,string n,string v,Vector2 min,Vector2 max,int s,Color c,FontStyle f){var o=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));o.transform.SetParent(p,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=o.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=v;t.fontSize=s;t.fontStyle=f;t.color=c;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        private static void Button(Transform p,string n,string label,string code,PotatoCargoJourneyLifecyclePresenter presenter,Vector2 min,Vector2 max){var o=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(PotatoCargoJourneyActionButton));o.transform.SetParent(p,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;o.GetComponent<Image>().color=new Color(.07f,.16f,.19f,1f);o.GetComponent<PotatoCargoJourneyActionButton>().Configure(presenter,code);var t=Text(o.transform,"Label",label,new Vector2(.03f,.05f),new Vector2(.97f,.95f),11,Color.white,FontStyle.Bold);t.alignment=TextAnchor.MiddleCenter;}
        private readonly struct Ui{public Ui(Text state,Text cargo,Text progress,Text lineage,Text action,Text limitation){State=state;Cargo=cargo;Progress=progress;Lineage=lineage;Action=action;Limitation=limitation;}public Text State{get;}public Text Cargo{get;}public Text Progress{get;}public Text Lineage{get;}public Text Action{get;}public Text Limitation{get;}}
    }
}
