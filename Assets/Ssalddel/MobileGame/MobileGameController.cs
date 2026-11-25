using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ssalddel.MobileGame
{
    /// <summary>모바일 가로 지도 HUD. 회차의 최종 상태와 시간을 소유하지 않고 서비스가 돌려준 사본을 표시한다.</summary>
    public sealed class MobileGameController : MonoBehaviour
    {
        private IMobileGameSession session;
        private MobileGameState state;
        private MobileGameRoad[] roads;
        private MobileMapGraphic map;
        private RectTransform safeArea, panel;
        private Text clock, status, panelTitle, panelBody, settlementText, recommendationNote;
        private Button startButton, pickupButton, resultButton, recommendationButton;
        private GameObject results;
        private Font font;
        private string selectedCourier, panelMode, renderedPanel;
        private string lastNotice = "운영 시작을 누른 뒤 기사와 매장을 차례로 탭하세요.";
        private bool sending, paused, resultShown;
        private int speed = 1;
        private Rect lastSafeArea;
        private Vector2 lastScreenSize;
        private static readonly Color CanvasColor = new Color32(16,26,37,255);
        private static readonly Color PanelColor = new Color32(25,39,53,245);
        private static readonly Color Tile = new Color32(34,53,69,255);
        private static readonly Color Paper = new Color32(236,245,248,255);
        private static readonly Color Soft = new Color32(171,193,203,255);
        private static readonly Color Mint = new Color32(110,231,204,255);

        public MobileGameState CurrentState { get { return state; } }
        public MobileMapGraphic Map { get { return map; } }
        public bool IsCommandPending { get { return sending; } }

        private void Awake() { BuildInterface(); }
        public void AttachSession(IMobileGameSession value)
        {
            if(session!=null)session.Changed-=ApplyJson;
            session=value;
            if(session!=null)
            {
                session.Changed+=ApplyJson;
                if(!string.IsNullOrWhiteSpace(session.SnapshotJson))ApplyJson(session.SnapshotJson);
                else ShowNotice("운영 시작을 눌러 가상 주문을 열어 주세요.");
            }
            RefreshControls();
        }
        public void ConfigureFont(Font value)
        {
            if(value==null)return;font=value;
            foreach(var text in GetComponentsInChildren<Text>(true))text.font=value;
        }
        public void ShowNotice(string message)
        {
            if(string.IsNullOrWhiteSpace(message))return;
            lastNotice=message;if(status!=null)status.text=message;
        }
        public void ApplyJson(string json)
        {
            if(string.IsNullOrWhiteSpace(json))return;
            try
            {
                var trimmed=json.Trim();
                if(!trimmed.Contains("\"state\"") && trimmed.Contains("\"orders\""))
                    state=JsonUtility.FromJson<MobileGameState>(json);
                else
                {
                    var reply=JsonUtility.FromJson<MobileGameReply>(json);
                    if(reply==null)return;
                    if(reply.state!=null)state=reply.state;
                    if(reply.roads!=null && reply.roads.Length>0)roads=reply.roads;
                    if(reply.clock!=null){paused=reply.clock.paused;speed=reply.clock.speed;}
                    ShowNotice(string.IsNullOrWhiteSpace(reply.notice)?reply.message:reply.notice);
                }
                if(state==null)return;
                if(map.AssignmentArmed && !CanArmAssignment(selectedCourier))map.AssignmentArmed=false;
                if(clock!=null)clock.text=(state.now??"11:30:00")+"\n"+(paused?"일시정지":speed+"배속");
                map.Apply(state,roads,selectedCourier);
                RefreshControls();RenderPanel();
                if(state.settled && !resultShown){resultShown=true;ShowResults();}
            }
            catch(Exception error) when(error is ArgumentException || error is InvalidOperationException)
            {ShowNotice("게임 상태를 표시할 수 없습니다. 저장된 회차를 확인해 주세요.");}
        }
        public async Task SendActionAsync(MobileGameRequest request)
        {
            if(session==null){ShowNotice("가상 회차 연결을 기다리고 있습니다.");return;}
            if(sending){ShowNotice("앞선 요청을 확인하고 있습니다.");return;}
            sending=true;RefreshControls();
            try
            {
                var reply=await session.RequestJsonAsync(JsonUtility.ToJson(request));
                if(this!=null)ApplyJson(reply);
            }
            catch(Exception error)
            {
                if(this!=null)ShowNotice("요청 결과를 확인하지 못했습니다. "+error.Message);
            }
            finally{sending=false;if(this!=null){renderedPanel=null;RefreshControls();RenderPanel();}}
        }
        private async void Command(string action)
        {
            if(action=="start"){resultShown=false;selectedCourier=null;map.AssignmentArmed=false;ClosePanel();map.ResetViewport();}
            if(action=="pause")paused=true;
            if(action=="resume")paused=false;
            await SendActionAsync(new MobileGameRequest{action=action,elapsedMs=state?.elapsedMs??0,manualPickup=true});
        }
        private async void Speed(int value)
        {
            speed=value;paused=value==0;
            await SendActionAsync(new MobileGameRequest{action="set-speed",speed=value,elapsedMs=state?.elapsedMs??0});
        }
        public void SelectCourier(string id)
        {
            selectedCourier=id;panelMode="courier";panel.gameObject.SetActive(true);renderedPanel=null;
            map.AssignmentArmed=CanArmAssignment(id);
            ShowNotice(map.AssignmentArmed?"선택한 기사와 연결할 매장을 탭하세요. 지도는 끌거나 두 손가락으로 확대할 수 있습니다.":"선택한 기사의 현재 배달 업무를 확인합니다.");
            map.Apply(state,roads,selectedCourier);RenderPanel();
        }
        private bool CanArmAssignment(string id)
        {
            return !string.IsNullOrEmpty(id) && (state?.couriers??Array.Empty<MobileGameCourier>()).Any(courier=>courier.id==id)
                && (state?.orders??Array.Empty<MobileGameOrder>()).Any(order=>order.stage=="Waiting")
                && !(state.orders??Array.Empty<MobileGameOrder>()).Any(order=>order.driverId==id && order.stage!="Settled");
        }
        public async void ConnectStore(string id)
        {
            var order=(state?.orders??Array.Empty<MobileGameOrder>()).FirstOrDefault(item=>item.storeId==id && item.stage=="Waiting");
            if(order==null){ShowNotice("이 매장에는 배차 대기 주문이 없습니다.");return;}
            if(string.IsNullOrEmpty(selectedCourier) || !map.AssignmentArmed){ShowNotice("배차할 기사를 먼저 선택한 다음 주문이 있는 매장을 탭하세요.");OpenPanel("riders");return;}
            if((state.orders??Array.Empty<MobileGameOrder>()).Any(item=>item.driverId==selectedCourier && item.stage!="Settled"))
            {ShowNotice("선택한 기사는 다른 배달을 맡고 있습니다.");return;}
            await SendActionAsync(new MobileGameRequest{action="assign",orderId=order.id,driverId=selectedCourier,
                commandId="mobile-"+Guid.NewGuid().ToString("N"),revision=order.revision,elapsedMs=state.elapsedMs});
            if((state?.orders??Array.Empty<MobileGameOrder>()).Any(item=>item.id==order.id && item.driverId==selectedCourier))
                map.AssignmentArmed=false;
        }
        public async void ConfirmPickup()
        {
            var order=SelectedOrder();
            if(order==null || order.stage!="AtPickup"){ShowNotice("배정된 기사가 매장에 도착하면 픽업을 확인할 수 있습니다.");return;}
            if(!order.canConfirmPickup){ShowNotice("음식 준비가 끝나면 픽업을 확인할 수 있습니다.");return;}
            await SendActionAsync(new MobileGameRequest{action="pickup-confirm",orderId=order.id,driverId=order.driverId,
                commandId="mobile-pickup-"+Guid.NewGuid().ToString("N"),revision=order.revision,elapsedMs=state.elapsedMs});
        }
        public async void AssignRecommended()
        {
            var order=PanelOrder();
            if(order==null || order.stage!="Waiting"){ShowNotice("현재 배차 대기 주문을 먼저 확인해 주세요.");return;}
            await SendActionAsync(new MobileGameRequest{action="ai-trial",orderId=order.id,elapsedMs=state.elapsedMs});
            var assigned=(state?.orders??Array.Empty<MobileGameOrder>()).FirstOrDefault(item=>item.id==order.id && !string.IsNullOrEmpty(item.driverId));
            if(assigned==null)return;
            selectedCourier=assigned.driverId;
            map.AssignmentArmed=false;
            map.Apply(state,roads,selectedCourier);
            renderedPanel=null;RenderPanel();
        }
        private MobileGameOrder PanelOrder()
        {
            return SelectedOrder()??(state?.orders??Array.Empty<MobileGameOrder>()).FirstOrDefault(order=>order.stage=="Waiting");
        }
        private MobileGameOrder SelectedOrder()
        {
            var orders=state?.orders??Array.Empty<MobileGameOrder>();
            if(!string.IsNullOrEmpty(selectedCourier))return orders.FirstOrDefault(order=>order.driverId==selectedCourier && order.stage!="Settled")
                ?? orders.FirstOrDefault(order=>order.driverId==selectedCourier);
            return orders.FirstOrDefault(order=>order.stage=="AtPickup")??orders.FirstOrDefault(order=>!string.IsNullOrEmpty(order.driverId));
        }
        public void OpenPanel(string mode)
        {
            panelMode=mode;renderedPanel=null;panel.gameObject.SetActive(true);RenderPanel();
        }
        public void ClosePanel()
        {
            if(panel!=null)panel.gameObject.SetActive(false);panelMode=null;renderedPanel=null;
        }
        public void Back()
        {
            if(results!=null && results.activeSelf){results.SetActive(false);return;}
            if(panel!=null && panel.gameObject.activeSelf){ClosePanel();return;}
            if(!string.IsNullOrEmpty(selectedCourier)){selectedCourier=null;map.AssignmentArmed=false;map.Apply(state,roads,null);ShowNotice("기사 선택을 해제했습니다.");}
        }
        private void Update()
        {
            FitSafeArea();
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)Back();
        }
        private void OnDestroy(){if(session!=null)session.Changed-=ApplyJson;}

        private void BuildInterface()
        {
            font=Resources.Load<Font>("HongdalMobile/Korean");
            if(font==null)
            {
                var names=Font.GetOSInstalledFontNames();
                var candidate=names.FirstOrDefault(name=>name.Contains("Noto Sans CJK") || name.Contains("Malgun") || name.Contains("맑은"));
                if(!string.IsNullOrEmpty(candidate))font=Font.CreateDynamicFontFromOSFont(candidate,22);
            }
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=1;
            gameObject.AddComponent<GraphicRaycaster>();
            safeArea=Box("안전 영역",transform,CanvasColor);Stretch(safeArea,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var mapArea=Box("지도 영역",safeArea,CanvasColor);Stretch(mapArea,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            mapArea.gameObject.AddComponent<RectMask2D>();
            var surface=NewRect("기존 OSM 도시 표면",mapArea).gameObject.AddComponent<RawImage>();
            surface.texture=Resources.Load<Texture2D>("HongdalMobile/Map/surface");surface.color=Color.white;surface.raycastTarget=false;
            surface.rectTransform.anchorMin=surface.rectTransform.anchorMax=new Vector2(.5f,.5f);surface.rectTransform.pivot=new Vector2(.5f,.5f);
            var graph=NewRect("도로·기사·매장",mapArea);Stretch(graph,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            map=graph.gameObject.AddComponent<MobileMapGraphic>();
            var raw=Resources.Load<TextAsset>("HongdalMobile/Map/bounds");
            var bounds=raw==null?null:JsonUtility.FromJson<MobileMapBounds>(raw.text);
            map.Configure(surface,bounds,Resources.Load<Texture2D>("HongdalMobile/Motorcycle/rider-b"),font);
            map.CourierTapped=SelectCourier;map.StoreTapped=ConnectStore;

            var header=Box("상단 상태",safeArea,PanelColor);Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(0,-86),Vector2.zero);
            Label("홍달 경영 시뮬레이션",header,new Vector2(18,-12),new Vector2(390,34),26,Mint);
            Label("음식 배달 · 가상 연습",header,new Vector2(20,-48),new Vector2(320,26),17,Soft);
            clock=Label("11:30:00\n운영 대기",header,new Vector2(-660,-10),new Vector2(130,68),23,Paper,TextAnchor.MiddleCenter,new Vector2(1,1));
            ButtonAt("정지",header,new Vector2(-526,-13),new Vector2(68,60),()=>Command("pause"),new Vector2(1,1));
            ButtonAt("재개",header,new Vector2(-450,-13),new Vector2(68,60),()=>Command("resume"),new Vector2(1,1));
            ButtonAt("1배",header,new Vector2(-374,-13),new Vector2(56,60),()=>Speed(1),new Vector2(1,1));
            ButtonAt("2배",header,new Vector2(-310,-13),new Vector2(56,60),()=>Speed(2),new Vector2(1,1));
            ButtonAt("4배",header,new Vector2(-246,-13),new Vector2(56,60),()=>Speed(4),new Vector2(1,1));
            ButtonAt("불러오기",header,new Vector2(-182,-13),new Vector2(80,60),()=>Command("import"),new Vector2(1,1));
            ButtonAt("내보내기",header,new Vector2(-94,-13),new Vector2(80,60),()=>Command("export"),new Vector2(1,1));

            var dock=Box("하단 조작",safeArea,PanelColor);Stretch(dock,Vector2.zero,new Vector2(1,0),new Vector2(12,12),new Vector2(-12,102));
            var layout=dock.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.padding=new RectOffset(10,10,10,10);
            layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=true;
            startButton=DockButton("운영 시작",dock,()=>Command("start"),Mint);
            DockButton("기사 목록",dock,()=>OpenPanel("riders"),Paper);
            DockButton("업무 연습",dock,()=>OpenPanel("practice"),Paper);
            resultButton=DockButton("결산",dock,ShowResults,Paper);
            var statusBar=Box("안내",safeArea,new Color32(16,26,37,225));Stretch(statusBar,Vector2.zero,new Vector2(1,0),new Vector2(12,110),new Vector2(-12,154));
            status=Label(lastNotice,statusBar,new Vector2(12,-3),new Vector2(1000,36),18,Paper);Stretch(status.rectTransform,Vector2.zero,Vector2.one,new Vector2(12,0),new Vector2(-12,0));
            var attribution=Label("© OpenStreetMap contributors · ODbL",safeArea,new Vector2(14,174),new Vector2(390,20),12,Soft,TextAnchor.UpperLeft,new Vector2(0,0));
            attribution.raycastTarget=false;

            panel=Box("선택·업무 패널",safeArea,PanelColor);Stretch(panel,new Vector2(1,0),Vector2.one,new Vector2(-398,168),new Vector2(-14,-98));
            panelTitle=Label("업무 연습",panel,new Vector2(18,-14),new Vector2(280,40),24,Mint);
            ButtonAt("닫기",panel,new Vector2(-88,-12),new Vector2(74,46),Back,new Vector2(1,1));
            panelBody=Label("",panel,new Vector2(18,-68),new Vector2(340,310),19,Paper,TextAnchor.UpperLeft);
            Stretch(panelBody.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,92),new Vector2(-18,-70));
            panelBody.verticalOverflow=VerticalWrapMode.Truncate;panelBody.horizontalOverflow=HorizontalWrapMode.Wrap;
            pickupButton=ButtonAt("픽업 확인",panel,new Vector2(18,78),new Vector2(348,60),ConfirmPickup,new Vector2(0,0));
            recommendationButton=ButtonAt("추천 기사로 배차",panel,new Vector2(18,78),new Vector2(348,60),AssignRecommended,new Vector2(0,0));
            recommendationNote=Label("현재 규칙 추천 · 모델 미연결",panel,new Vector2(18,110),new Vector2(348,24),16,Soft,TextAnchor.MiddleCenter,new Vector2(0,0));
            recommendationButton.gameObject.SetActive(false);recommendationNote.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            var shade=Box("결산 표시",safeArea,new Color32(10,18,26,235));Stretch(shade,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);results=shade.gameObject;
            var card=Box("결산 카드",shade,PanelColor);card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);card.sizeDelta=new Vector2(600,500);card.pivot=new Vector2(.5f,.5f);
            Label("가상 회차 결산",card,new Vector2(28,-22),new Vector2(520,42),30,Mint);
            settlementText=Label("",card,new Vector2(30,-88),new Vector2(540,300),23,Paper);
            ButtonAt("지도로 돌아가기",card,new Vector2(28,82),new Vector2(544,60),()=>results.SetActive(false),new Vector2(0,0));
            results.SetActive(false);FitSafeArea();RefreshControls();
        }

        private void RenderPanel()
        {
            if(panel==null || !panel.gameObject.activeSelf)return;
            var signature=panelMode+"|"+selectedCourier+"|"+sending+"|"+string.Join(";",(state?.orders??Array.Empty<MobileGameOrder>()).Select(x=>x.id+":"+x.revision+":"+x.stage));
            if(signature==renderedPanel)return;renderedPanel=signature;
            foreach(Transform child in panel)if(child.name.StartsWith("목록 기사 ",StringComparison.Ordinal))Destroy(child.gameObject);
            if(panelMode=="riders")
            {
                panelTitle.text="기사 선택";panelBody.text="지도 위 오토바이나 아래 기사를 선택하세요.\n선택한 뒤 매장 핀을 누르면 연결합니다.";
                var couriers=state?.couriers??Array.Empty<MobileGameCourier>();var index=0;
                foreach(var courier in couriers.Take(3))
                {
                    var id=courier.id;
                    var button=ButtonAt(courier.label+" · "+Stage(courier.phase),panel,new Vector2(18,-154-index*68),new Vector2(348,58),()=>SelectCourier(id));
                    button.name="목록 기사 "+id;index++;
                }
                pickupButton.gameObject.SetActive(false);recommendationButton.gameObject.SetActive(false);recommendationNote.gameObject.SetActive(false);return;
            }
            var order=PanelOrder();var driver=(state?.couriers??Array.Empty<MobileGameCourier>()).FirstOrDefault(x=>x.id==selectedCourier);
            panelTitle.text=panelMode=="practice"?"기사 업무 연습":driver?.label??"기사 상세";
            if(order==null)
            {
                panelBody.text=driver==null?"기사와 매장을 연결하면 같은 가상 주문이 이 화면에 표시됩니다.":driver.label+"\n신규 배차 대기\n\n주문 알림이 있는 매장을 탭하세요.";
            }
            else
            {
                panelBody.text=(driver?.label??order.driverId??"배차 대기 주문")+"\n"+order.storeName+" · "+order.menu+"\n\n현재 단계  "+Stage(order.stage)+"\n"+
                    (order.stage=="Waiting"?"지도에서 직접 기사를 연결하거나 현재 규칙 추천으로 배차할 수 있습니다.":order.stage=="AtPickup"?"매장에 도착했습니다. 준비된 음식을 확인하고 픽업해 주세요.":order.stage=="Settled"?"수령과 가상 정산을 마쳤습니다.":"지도와 이 화면은 같은 가상 회차를 표시합니다.")+"\n\n"+
                    string.Join("\n",(state.events??Array.Empty<MobileGameEvent>()).Where(x=>x.orderId==order.id).Reverse().Take(3).Reverse().Select(x=>(x.at??"").Split('.')[0]+"  "+x.text));
            }
            var waiting=order?.stage=="Waiting";
            Stretch(panelBody.rectTransform,Vector2.zero,Vector2.one,new Vector2(18,waiting?126:92),new Vector2(-18,-70));
            pickupButton.gameObject.SetActive(!waiting);pickupButton.interactable=session!=null && !sending && order?.canConfirmPickup==true;
            recommendationButton.gameObject.SetActive(waiting);recommendationNote.gameObject.SetActive(waiting);
            recommendationButton.interactable=session!=null && !sending && waiting;
        }
        private void RefreshControls()
        {
            if(startButton!=null)startButton.interactable=session!=null && !sending && (state==null || state.settled);
            if(resultButton!=null)resultButton.interactable=state!=null;
            if(pickupButton!=null)pickupButton.interactable=session!=null && !sending && SelectedOrder()?.canConfirmPickup==true;
            if(recommendationButton!=null)recommendationButton.interactable=session!=null && !sending && PanelOrder()?.stage=="Waiting";
        }
        public void ShowResults()
        {
            if(results==null)return;
            var result=state?.settlement;
            if(result==null)
            {settlementText.text="아직 회차를 마치지 않았습니다.\n\n배차 → 매장 이동 → 픽업 확인 → 전달 → 수령을 진행해 주세요.\n\n가상 정산은 실제 결제·지급이 아닙니다.";}
            else
            {settlementText.text="제시간 배차  "+result.onTimeCount+" / "+(state.orders?.Length??0)+"건\n플랫폼 수입  "+result.revenue.ToString("N0")+"원\n기사 보수  "+result.driverPay.ToString("N0")+"원\n보너스  "+result.bonus.ToString("N0")+"원\n차액  "+result.balance.ToString("N0")+"원\n\n"+string.Join("\n",(state.orders??Array.Empty<MobileGameOrder>()).Select(x=>x.storeName+"  "+x.deliveryMinutes.ToString("0.0")+"분"))+"\n실제 결제·지급 없음";}
            results.SetActive(true);
        }
        private void FitSafeArea()
        {
            if(safeArea==null || (lastSafeArea==Screen.safeArea && lastScreenSize==new Vector2(Screen.width,Screen.height)))return;
            lastSafeArea=Screen.safeArea;lastScreenSize=new Vector2(Screen.width,Screen.height);
            safeArea.anchorMin=new Vector2(lastSafeArea.xMin/Mathf.Max(1,Screen.width),lastSafeArea.yMin/Mathf.Max(1,Screen.height));
            safeArea.anchorMax=new Vector2(lastSafeArea.xMax/Mathf.Max(1,Screen.width),lastSafeArea.yMax/Mathf.Max(1,Screen.height));safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        private static string Stage(string code)
        {
            switch(code){case "Waiting":case "Idle":return "배차 대기";case "Assigned":return "기사 지정";case "Accepted":return "배정 수락";case "ToPickup":return "매장으로 이동";case "AtPickup":return "매장 도착 · 픽업 대기";case "ToDropoff":return "배송지로 이동";case "Delivered":return "전달 완료";case "Received":return "수령 확인";case "Settled":return "가상 정산 완료";default:return string.IsNullOrEmpty(code)?"상태 확인":code;}
        }
        private static RectTransform NewRect(string name,Transform parent)
        {var value=new GameObject(name,typeof(RectTransform));value.transform.SetParent(parent,false);return value.GetComponent<RectTransform>();}
        private static RectTransform Box(string name,Transform parent,Color color)
        {var rt=NewRect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.color=color;return rt;}
        private static void Stretch(RectTransform rect,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;}
        private Text Label(string value,Transform parent,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.UpperLeft,Vector2? anchor=null)
        {var rt=NewRect(value.Length>30?value.Substring(0,30):value,parent);rt.anchorMin=rt.anchorMax=anchor??new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;var text=rt.gameObject.AddComponent<Text>();text.font=font;text.fontSize=fontSize;text.color=color;text.alignment=alignment;text.text=value;text.raycastTarget=false;return text;}
        private Button ButtonAt(string caption,Transform parent,Vector2 position,Vector2 size,Action action,Vector2? anchor=null)
        {var rt=Box(caption,parent,Tile);rt.anchorMin=rt.anchorMax=anchor??new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;var button=rt.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>action());var label=Label(caption,rt,Vector2.zero,size,20,Paper,TextAnchor.MiddleCenter);Stretch(label.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);return button;}
        private Button DockButton(string caption,Transform parent,Action action,Color color)
        {var button=ButtonAt(caption,parent,Vector2.zero,new Vector2(200,66),action);button.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;button.GetComponentInChildren<Text>().color=color;return button;}
    }
}
