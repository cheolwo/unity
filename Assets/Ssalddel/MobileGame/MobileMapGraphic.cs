using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ssalddel.MobileGame
{
    /// <summary>공개 도로·기존 도시 표면·기사 상태 사본을 같은 위경도 평면에 그린다. 업무 상태는 바꾸지 않는다.</summary>
    public sealed class MobileMapGraphic : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<string> CourierTapped;
        public Action<string> StoreTapped;
        /// <summary>기사를 배차 대상으로 새로 선택한 동안만 매장 연결을 우선한다. 상세 초점과 별개다.</summary>
        public bool AssignmentArmed { get; set; }
        private MobileGameState state;
        private MobileGameRoad[] roads = Array.Empty<MobileGameRoad>();
        private readonly MobileTouchState gesture = new MobileTouchState();
        private readonly Dictionary<string, Image> bikes = new Dictionary<string, Image>();
        private readonly Dictionary<string, Text> storeLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, float> headings = new Dictionary<string, float>();
        private RawImage surface;
        private MobileMapBounds surfaceBounds;
        private Texture2D motorcycle;
        private Sprite motorcycleSprite;
        private Font font;
        private double centerLat = 37.5808, centerLon = 127.087;
        private double pixelsPerDegree = 130000;
        private Vector2 pan, previousPointer;
        private float zoom = 1, previousPinch;
        private bool fitted, pinching;
        private string selectedCourier;
        private static readonly Color Mint = new Color32(110, 231, 204, 255);
        private static readonly Color Lilac = new Color32(198, 172, 255, 255);
        private static readonly Color Gold = new Color32(255, 211, 149, 255);

        public void Configure(RawImage citySurface, MobileMapBounds bounds, Texture2D bike, Font textFont)
        {
            surface = citySurface; surfaceBounds = bounds; motorcycle = bike; font = textFont;
            if (motorcycle != null)
                motorcycleSprite = Sprite.Create(motorcycle, new Rect(0, 0, motorcycle.width, motorcycle.height), new Vector2(.5f,.5f), 100);
            raycastTarget = true;
        }

        public void Apply(MobileGameState snapshot, MobileGameRoad[] background, string selected)
        {
            state = snapshot; selectedCourier = selected;
            if (background != null && background.Length > 0) roads = background;
            if (!fitted && state != null && state.stores != null && state.stores.Length > 0) Fit();
            UpdateObjects(); SetVerticesDirty();
        }

        public void ResetViewport() { fitted = false; pan = Vector2.zero; zoom = 1; Fit(); }

        private void Fit()
        {
            if (state == null) return;
            var south = double.MaxValue; var north = double.MinValue;
            var west = double.MaxValue; var east = double.MinValue;
            Action<double, double> point = (latitude, longitude) =>
            {
                if (latitude == 0 || longitude == 0) return;
                south = Math.Min(south, latitude); north = Math.Max(north, latitude);
                west = Math.Min(west, longitude); east = Math.Max(east, longitude);
            };
            foreach (var store in state.stores ?? Array.Empty<MobileGameStore>()) point(store.latitude, store.longitude);
            foreach (var courier in state.couriers ?? Array.Empty<MobileGameCourier>()) point(courier.latitude, courier.longitude);
            foreach (var destination in state.destinations ?? Array.Empty<MobileGameDestination>()) point(destination.latitude, destination.longitude);
            if (south == double.MaxValue || rectTransform.rect.width < 1 || rectTransform.rect.height < 1) return;
            centerLat = (south + north) * .5; centerLon = (west + east) * .5;
            var longitudeRatio = Math.Cos(centerLat * Math.PI / 180);
            pixelsPerDegree = Math.Min(rectTransform.rect.width * .67 / Math.Max(.001, (east-west)*longitudeRatio),
                rectTransform.rect.height * .64 / Math.Max(.001, north-south));
            zoom = 1; pan = Vector2.zero; fitted = true;
        }

        private Vector2 Plot(double latitude, double longitude)
        {
            return new Vector2((float)((longitude-centerLon)*Math.Cos(centerLat*Math.PI/180)*pixelsPerDegree*zoom),
                (float)((latitude-centerLat)*pixelsPerDegree*zoom)) + pan;
        }

        public Vector2 ScreenPointForCourier(string id, Camera eventCamera = null)
        {
            foreach (var courier in state?.couriers ?? Array.Empty<MobileGameCourier>())
                if (courier.id == id) return RectTransformUtility.WorldToScreenPoint(eventCamera, rectTransform.TransformPoint(Plot(courier.latitude,courier.longitude)));
            return Vector2.zero;
        }
        public Vector2 ScreenPointForStore(string id, Camera eventCamera = null)
        {
            foreach (var store in state?.stores ?? Array.Empty<MobileGameStore>())
                if (store.id == id) return RectTransformUtility.WorldToScreenPoint(eventCamera, rectTransform.TransformPoint(Plot(store.latitude,store.longitude)));
            return Vector2.zero;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (var road in roads)
                if (road?.a != null && road.b != null) Line(mesh, Plot(road.a.latitude,road.a.longitude), Plot(road.b.latitude,road.b.longitude), 3.5f, new Color32(88,113,125,210));
            foreach (var courier in state?.couriers ?? Array.Empty<MobileGameCourier>())
            {
                var route = courier.route;
                if (route != null) for (var i=1;i<route.Length;i++)
                    Line(mesh, Plot(route[i-1].latitude,route[i-1].longitude), Plot(route[i].latitude,route[i].longitude), 5f, RiderColor(courier.id));
                if (courier.id == selectedCourier) Circle(mesh,Plot(courier.latitude,courier.longitude),32,new Color32(110,231,204,55));
                if (motorcycleSprite == null) Circle(mesh,Plot(courier.latitude,courier.longitude),13,RiderColor(courier.id));
            }
            foreach (var destination in state?.destinations ?? Array.Empty<MobileGameDestination>())
                Circle(mesh,Plot(destination.latitude,destination.longitude),8,new Color32(171,193,203,255));
            foreach (var store in state?.stores ?? Array.Empty<MobileGameStore>())
            {
                var position = Plot(store.latitude,store.longitude);
                Circle(mesh,position,18,Gold);
                var waiting=0;
                foreach (var order in state.orders ?? Array.Empty<MobileGameOrder>()) if (order.storeId==store.id && order.stage=="Waiting") waiting++;
                if (waiting>0) Circle(mesh,position+new Vector2(15,18),10,new Color32(240,108,115,255));
            }
        }

        private static Color RiderColor(string id) { return id != null && id.EndsWith("b",StringComparison.OrdinalIgnoreCase) ? Lilac : Mint; }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            var delta=b-a;if(delta.sqrMagnitude<.01f)return;
            var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;var n=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
        private static void Circle(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            var n=mesh.currentVertCount;mesh.AddVert(center,color,Vector2.zero);
            for(var i=0;i<=24;i++){var angle=i*Mathf.PI*2/24;mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);}
            for(var i=0;i<24;i++)mesh.AddTriangle(n,n+i+1,n+i+2);
        }

        private void UpdateObjects()
        {
            if(surface != null && surfaceBounds != null)
            {
                var lower=Plot(surfaceBounds.south,surfaceBounds.west);var upper=Plot(surfaceBounds.north,surfaceBounds.east);
                surface.rectTransform.anchoredPosition=(lower+upper)*.5f;surface.rectTransform.sizeDelta=upper-lower;
            }
            foreach(var bike in bikes.Values)bike.gameObject.SetActive(false);
            foreach(var label in storeLabels.Values)label.gameObject.SetActive(false);
            foreach(var courier in state?.couriers ?? Array.Empty<MobileGameCourier>())
            {
                if(motorcycleSprite==null)continue;
                if(!bikes.TryGetValue(courier.id,out var bike))
                {
                    var go=new GameObject("기사 "+courier.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(transform,false);
                    bike=go.GetComponent<Image>();bike.sprite=motorcycleSprite;bike.raycastTarget=false;bike.preserveAspect=true;
                    bike.rectTransform.sizeDelta=new Vector2(48,48);bikes.Add(courier.id,bike);
                }
                bike.gameObject.SetActive(true);bike.rectTransform.anchoredPosition=Plot(courier.latitude,courier.longitude);
                bike.color=Color.white;
                if(courier.route != null && courier.route.Length>1)
                {
                    var nearest=double.MaxValue;var heading=0f;
                    for(var i=1;i<courier.route.Length;i++)
                    {
                        var a=Plot(courier.route[i-1].latitude,courier.route[i-1].longitude);var b=Plot(courier.route[i].latitude,courier.route[i].longitude);var p=Plot(courier.latitude,courier.longitude);
                        var d=b-a;if(d.sqrMagnitude<.001f)continue;var t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);var distance=(p-(a+d*t)).sqrMagnitude;
                        if(distance>nearest)continue;nearest=distance;heading=Mathf.Atan2(d.x,d.y)*Mathf.Rad2Deg;
                    }
                    headings[courier.id]=heading;
                }
                if(headings.TryGetValue(courier.id,out var angle))bike.rectTransform.localRotation=Quaternion.Euler(0,0,-angle);
            }
            foreach(var store in state?.stores ?? Array.Empty<MobileGameStore>())
            {
                if(!storeLabels.TryGetValue(store.id,out var label))
                {
                    var go=new GameObject("매장 "+store.id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(transform,false);
                    label=go.GetComponent<Text>();label.font=font;label.fontSize=18;label.alignment=TextAnchor.MiddleCenter;label.color=Gold;label.raycastTarget=false;
                    label.rectTransform.sizeDelta=new Vector2(180,32);storeLabels.Add(store.id,label);
                }
                label.gameObject.SetActive(true);label.text=store.name;label.rectTransform.anchoredPosition=Plot(store.latitude,store.longitude)+new Vector2(0,-35);
            }
        }

        private bool IsMapTopAt(Vector2 screen)
        {
            if(EventSystem.current==null)return false;
            var data=new PointerEventData(EventSystem.current){position=screen};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            return hits.Count>0 && hits[0].gameObject==gameObject;
        }
        private void Update()
        {
            var touchscreen=Touchscreen.current;
            if(touchscreen==null)return;
            var active=0;var a=Vector2.zero;var b=Vector2.zero;
            foreach(var touch in touchscreen.touches)
                if(touch.press.isPressed){if(active==0)a=touch.position.ReadValue();else if(active==1)b=touch.position.ReadValue();active++;}
            if(active>=2)
            {
                if(!pinching)
                {
                    if(!IsMapTopAt(a)||!IsMapTopAt(b))return;
                    pinching=true;gesture.CancelForPinch();previousPinch=Vector2.Distance(a,b);return;
                }
                var distance=Vector2.Distance(a,b);
                if(previousPinch>1 && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,(a+b)*.5f,null,out var focus))
                {
                    var next=MobileTouchState.Zoom(zoom,distance/previousPinch);pan=focus-(focus-pan)*(next/zoom);zoom=next;
                    UpdateObjects();SetVerticesDirty();
                }
                previousPinch=distance;
            }
            else if(active==0 && pinching){pinching=false;gesture.AllPointersReleased();}
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left||pinching)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out previousPointer))
                gesture.Begin(previousPointer.x,previousPointer.y,true);
        }
        public void OnDrag(PointerEventData data)
        {
            if(pinching)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var point))return;
            gesture.Move(point.x,point.y,12);
            if(gesture.IsDragging){pan+=point-previousPointer;UpdateObjects();SetVerticesDirty();}
            previousPointer=point;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(pinching)return;
            if(!gesture.End(RectTransformUtility.RectangleContainsScreenPoint(rectTransform,data.position,data.pressEventCamera)))return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var point))return;
            MobileGameCourier closest=null;MobileGameStore storeHit=null;var nearest=32f*32f;
            foreach(var courier in state?.couriers ?? Array.Empty<MobileGameCourier>())
            {var distance=(Plot(courier.latitude,courier.longitude)-point).sqrMagnitude;if(distance<=nearest){nearest=distance;closest=courier;}}
            foreach(var store in state?.stores ?? Array.Empty<MobileGameStore>())
            {
                var distance=(Plot(store.latitude,store.longitude)-point).sqrMagnitude;
                if(distance<nearest || (distance==nearest && AssignmentArmed))
                {nearest=distance;storeHit=store;}
            }
            // 가까운 기사 때문에 매장 중심 탭이 가려지지 않도록 두 종류의 거리를 함께 비교한다.
            if(storeHit!=null){StoreTapped?.Invoke(storeHit.id);return;}
            if(closest!=null)CourierTapped?.Invoke(closest.id);
        }
        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();UpdateObjects();SetVerticesDirty();}
        protected override void OnDestroy(){base.OnDestroy();if(motorcycleSprite!=null)Destroy(motorcycleSprite);}
    }
}
