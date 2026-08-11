using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class PotatoHarvestCargoLifecycleBuilder
    {
        public const string ScenePath = 연구Scene경로.감자생산유통 + "/감자수확포장상차흐름.unity";
        public const string RootName = "CARGO-1 Potato Harvest Cargo Lifecycle";
        public const string EvidencePath = "Documentation/Changes/2026-08-10-potato-harvest-cargo-lifecycle/potato-harvest-cargo-game-view.png";

        [MenuItem("Ssalddel/CARGO-1/Build Potato Harvest Cargo Lifecycle")]
        public static void Build()
        {
            EditorSceneManager.OpenScene(PotatoCultivationLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap") ?? throw new InvalidOperationException("CargoWorldMissing");
            var cultivation = world.transform.Find(PotatoCultivationLifecycleBuilder.RootName)
                ?.GetComponent<PotatoCultivationLifecyclePresenter>()
                ?? throw new InvalidOperationException("CargoCultivationPresenterMissing");
            var oldCanvas = cultivation.transform.Find("PotatoCultivationLifecycleCanvas");
            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
            var baseRoot = world.transform.Find(PotatoJourneyFarmVerticalSliceBuilder.RootName)
                ?? throw new InvalidOperationException("CargoBaseRootMissing");
            var cargoVisual = baseRoot.Find("FarmYardCargoAnchor_Potato")
                ?? throw new InvalidOperationException("CargoVisualMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var presenter = root.AddComponent<PotatoHarvestCargoLifecyclePresenter>();
            var packageMarker = Marker(root.transform, cargoVisual.position + new Vector3(.7f, 2.25f, .4f),
                "PackageLotMarker_15Boxes", new Color(.98f, .72f, .16f));
            var cargoMarker = Marker(root.transform, cargoVisual.position + new Vector3(1.45f, 2.45f, .4f),
                "CargoMarker_300kg", new Color(.25f, .9f, .55f));
            var ui = BuildUi(root.transform, presenter);
            presenter.Configure(cultivation, cargoVisual.gameObject, packageMarker, cargoMarker,
                ui.State, ui.Package, ui.Cargo, ui.Lineage, ui.Action, ui.Limitation, true);
            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("CargoSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("PotatoHarvestCargoLifecycleBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/CARGO-1/Validate Potato Harvest Cargo Lifecycle")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("CargoRootMissing");
            var presenter = root.GetComponent<PotatoHarvestCargoLifecyclePresenter>()
                ?? throw new InvalidOperationException("CargoPresenterMissing");
            presenter.ResetLifecycle();
            var actionCount = root.GetComponentsInChildren<PotatoHarvestCargoActionButton>(true).Length;
            if (!presenter.ValidateWiring() || presenter.CurrentModel.StateCode != "Harvested" || actionCount != 7)
                throw new InvalidOperationException("CargoInitialPresentationInvalid:actions=" + actionCount);
        }

        [MenuItem("Ssalddel/CARGO-1/Capture Potato Harvest Cargo Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("CargoCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<PotatoHarvestCargoLifecyclePresenter>()
                ?? throw new InvalidOperationException("CargoPresenterMissing");
            presenter.RunGoldenPath();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("PotatoHarvestCargoGameViewCaptureRequested:" + absolute);
        }

        private static GameObject Marker(Transform parent, Vector3 position, string name, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name; marker.transform.SetParent(parent, false); marker.transform.position = position;
            marker.transform.localScale = new Vector3(.34f, .07f, .34f);
            marker.SetActive(false); return marker;
        }

        private static Ui BuildUi(Transform parent, PotatoHarvestCargoLifecyclePresenter presenter)
        {
            var canvasObject = new GameObject("PotatoHarvestCargoCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100, 506);
            var panel = Panel(canvasObject.transform, "CargoPanel", new Vector2(.035f, .035f), new Vector2(.635f, .49f), new Color(.025f, .043f, .052f, .95f));
            Panel(panel, "CargoAccent", new Vector2(0f, .955f), Vector2.one, new Color(.96f, .58f, .12f));
            Text(panel, "CargoEyebrow", "CARGO-1 · HARVEST LOT TO SIMULATION CARGO", new Vector2(.035f, .86f), new Vector2(.965f, .955f), 18, new Color(1f, .78f, .3f), FontStyle.Bold);
            var state = Text(panel, "State", "STATE", new Vector2(.035f, .68f), new Vector2(.965f, .86f), 22, Color.white, FontStyle.Bold);
            var package = Text(panel, "Package", "PACKAGE", new Vector2(.035f, .56f), new Vector2(.965f, .68f), 15, new Color(1f, .84f, .4f), FontStyle.Bold);
            var cargo = Text(panel, "Cargo", "CARGO", new Vector2(.035f, .44f), new Vector2(.965f, .56f), 15, new Color(.4f, .92f, .72f), FontStyle.Bold);
            var lineage = Text(panel, "Lineage", "LINEAGE", new Vector2(.035f, .30f), new Vector2(.965f, .44f), 11, new Color(.72f, .86f, .95f), FontStyle.Bold);
            var action = Text(panel, "Action", "ACTION", new Vector2(.035f, .22f), new Vector2(.965f, .30f), 13, new Color(.42f, .85f, .94f), FontStyle.Bold);
            var limitation = Text(panel, "Limitation", "LIMIT", new Vector2(.035f, .15f), new Vector2(.965f, .22f), 11, new Color(.72f, .75f, .72f), FontStyle.Normal);
            var actions = new[] { ("RESET",PotatoHarvestCargoActionCodes.Reset), ("PACK REVIEW",PotatoHarvestCargoActionCodes.ReviewPacking),
                ("CONFIRM",PotatoHarvestCargoActionCodes.Confirm), ("APPLY TICK",PotatoHarvestCargoActionCodes.ApplyTick),
                ("LOAD REVIEW",PotatoHarvestCargoActionCodes.ReviewLoading), ("FINISH LOAD",PotatoHarvestCargoActionCodes.FinishLoading),
                ("GOLDEN PATH",PotatoHarvestCargoActionCodes.GoldenPath) };
            for (var i=0;i<actions.Length;i++) { var min=.035f+i*.133f; Button(panel,"Action_"+actions[i].Item2,actions[i].Item1,actions[i].Item2,presenter,new Vector2(min,.04f),new Vector2(min+.12f,.145f)); }
            정보Panel상호작용Builder.Attach(canvasObject.transform, panel, "수확·포장·상차");
            return new Ui(state, package, cargo, lineage, action, limitation);
        }

        private static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        { var o=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));o.transform.SetParent(parent,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;o.GetComponent<Image>().color=color;return r; }
        private static Text Text(Transform parent,string name,string value,Vector2 min,Vector2 max,int size,Color color,FontStyle style)
        { var o=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));o.transform.SetParent(parent,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;var t=o.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.fontStyle=style;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t; }
        private static void Button(Transform parent,string name,string label,string code,PotatoHarvestCargoLifecyclePresenter presenter,Vector2 min,Vector2 max)
        { var o=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(PotatoHarvestCargoActionButton));o.transform.SetParent(parent,false);var r=(RectTransform)o.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;o.GetComponent<Image>().color=new Color(.15f,.16f,.12f,1f);o.GetComponent<PotatoHarvestCargoActionButton>().Configure(presenter,code);var t=Text(o.transform,"Label",label,new Vector2(.03f,.05f),new Vector2(.97f,.95f),11,Color.white,FontStyle.Bold);t.alignment=TextAnchor.MiddleCenter; }
        private readonly struct Ui { public Ui(Text state,Text package,Text cargo,Text lineage,Text action,Text limitation){State=state;Package=package;Cargo=cargo;Lineage=lineage;Action=action;Limitation=limitation;} public Text State{get;}public Text Package{get;}public Text Cargo{get;}public Text Lineage{get;}public Text Action{get;}public Text Limitation{get;} }
    }
}
