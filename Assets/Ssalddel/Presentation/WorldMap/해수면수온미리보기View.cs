#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable]
    public sealed class 해수면수온PreviewCell
    {
        public string time;
        public double latitude, longitude, celsius, errorCelsius;
        public bool hasValue, hasError;
        public int mask;
    }

    [Serializable]
    public sealed class 해수면수온PreviewSnapshot
    {
        public string schemaVersion, sourceId, datasetId, sourceUrl, sourceHash, resultHash, collectedAtUtc, unit, sourceKind;
        public bool localReviewOnly, distributionApproved;
        public double gridDegrees;
        public 해수면수온PreviewCell[] cells;
        public void Validate()
        {
            if (schemaVersion != "marine-sst-preview.v1" || sourceId != "noaa-coastwatch"
                || datasetId != "noaacwBLENDEDsstDNDaily" || unit != "degree_C" || sourceKind != "L4Analysis"
                || sourceUrl != "https://coastwatch.noaa.gov/erddap/info/noaacwBLENDEDsstDNDaily/index.json"
                || !localReviewOnly || distributionApproved || gridDegrees != .05 || !Hash(sourceHash) || !Hash(resultHash)
                || !DateTimeOffset.TryParse(collectedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || cells == null || cells.Length != 63) throw new InvalidOperationException("SstPreviewContractInvalid");
            var first = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
            foreach (var c in cells)
            {
                if (c == null || !DateTimeOffset.TryParseExact(c.time, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var time) || time < first || time > first.AddDays(6)
                    || (time - first).TotalDays % 1 != 0 || c.mask != 1
                    || !new[] {36.975,37.025,37.075}.Contains(c.latitude)
                    || !new[] {130.025,130.075,130.125}.Contains(c.longitude)
                    || (c.hasValue ? !Valid(c.celsius,-2.00001,40) : c.celsius != 0)
                    || (c.hasError ? !Valid(c.errorCelsius,0,5) : c.errorCelsius != 0))
                    throw new InvalidOperationException("SstPreviewCellInvalid");
            }
            if (cells.Select(c => (c.time,c.latitude,c.longitude)).Distinct().Count() != 63)
                throw new InvalidOperationException("SstPreviewDuplicateCell");
        }
        public 해수면수온PreviewCell[] Frame(int day)
        {
            if (day < 0 || day > 6) throw new ArgumentOutOfRangeException(nameof(day));
            var date = new DateTimeOffset(2026,9,1,12,0,0,TimeSpan.Zero).AddDays(day).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            return cells.Where(c => c.time == date).OrderByDescending(c => c.latitude).ThenBy(c => c.longitude).ToArray();
        }
        private static bool Valid(double x,double min,double max) => !double.IsNaN(x) && !double.IsInfinity(x) && x >= min && x <= max;
        private static bool Hash(string s) => s != null && s.Length == 64 && s.All(c => "0123456789abcdef".Contains(c));
    }

    /// <summary>과거 수온과 비권위 가상 실험. 실제 어군 위치나 운영 상태를 생성하지 않는다.</summary>
    public sealed class 해수면수온미리보기View : MonoBehaviour
    {
        private 해수면수온PreviewSnapshot snapshot;
        private 세계지구본View globe;
        private Transform globeRoot;
        private Camera cameraView;
        private Canvas canvas;
        private GameObject panel;
        private RectTransform marker;
        private Text heading, detail, markerText;
        private readonly Button[] cellButtons = new Button[9];
        private readonly Text[] cellTexts = new Text[9];
        private int day, selected;
        private readonly 가상수온어군Model school = new 가상수온어군Model();
        private RectTransform cardFish, globeFish;
        private Text fishStatus, playText;
        private 해수면수온PreviewCell[] currentFrame;
        public 가상수온어군Model School => school;
        public int Day => day;
        public int CellCount => snapshot?.cells?.Length ?? 0;
        public string DetailText => detail != null ? detail.text : "";
        public bool Expanded => panel != null && panel.activeSelf;

        public void Load(string json, 세계지구본View target)
        {
            var candidate = JsonUtility.FromJson<해수면수온PreviewSnapshot>(json);
            if (candidate == null) throw new InvalidOperationException("SstPreviewMissing");
            candidate.Validate();
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.InitializeForTests();
            globe = target; globeRoot = target.transform.Find(세계지구본View.GlobeVisualRootName);
            cameraView = target.GetComponentsInChildren<Camera>(true).First(c => c.name == 세계지구본View.GlobeCameraName);
            snapshot = candidate; day = 0; selected = 4;
            currentFrame = snapshot.Frame(day); school.SetFrame(currentFrame); school.SetRunning(true);
            if (canvas == null) Build();
            Refresh(); SetExpanded(true);
        }
        public void SetExpanded(bool value) { if (panel != null) panel.SetActive(value); }
        public void SetDay(int value) { if (snapshot == null || value < 0 || value > 6 || value == day) return; day = value; currentFrame=snapshot.Frame(day); school.SetFrame(currentFrame); Refresh(); }
        public void SelectCell(int value) { if (snapshot == null || value < 0 || value > 8) return; selected = value; Refresh(); }
        public void SetPreference(double value) { school.SetPreference(value); }
        public void SetRunning(bool value) { school.SetRunning(value); if(playText!=null) playText.text=value?"일시 정지":"재생"; }
        public void ResetSchool() { school.Reset(); }
        private void Refresh()
        {
            var frame = currentFrame;
            heading.text = $"동해 수온  ·  2026.09.{day+1:00} 12:00 UTC";
            for (var i=0;i<9;i++)
            {
                var c = frame[i];
                cellTexts[i].text = c.hasValue ? c.celsius.ToString("F2",CultureInfo.InvariantCulture)+"°C" : "결측";
                cellButtons[i].image.color = i == selected ? new Color(.05f,.48f,.55f) : new Color(.08f,.2f,.3f);
            }
            var cell = frame[selected];
            detail.text = FormattableString.Invariant($"{cell.latitude:F3}°N / {cell.longitude:F3}°E\n")
                + (cell.hasValue ? $"해수면 분석 수온  {cell.celsius.ToString("F2",CultureInfo.InvariantCulture)} °C\n" : "수온 결측 · 반응 갱신 불가\n")
                + (cell.hasError ? $"분석오차(표준편차)  {cell.errorCelsius.ToString("F2",CultureInfo.InvariantCulture)} °C\n" : "분석오차 미제공\n")
                + "NOAA CoastWatch / L4 / 원본 0.05°\n과거 분석값 · 현재 실측 아님\n격자표는 확대 도식 · 지도 축척과 다름";
            markerText.text = "● 동해 수온\n" + (cell.hasValue ? cell.celsius.ToString("F2",CultureInfo.InvariantCulture)+" °C" : "결측");
        }
        private void LateUpdate()
        {
            if (canvas == null || globe == null || snapshot == null) return;
            canvas.enabled = globe.IsGlobeVisible && cameraView.enabled;
            school.Advance(Time.unscaledDeltaTime);
            var c = currentFrame[selected];
            var local = 지구위경도선View.Coordinate(c.latitude,c.longitude,18.2f);
            var world = globeRoot.TransformPoint(local);
            var screen = cameraView.WorldToScreenPoint(world);
            marker.gameObject.SetActive(canvas.enabled && screen.z>0 && Vector3.Dot(world-globeRoot.position,cameraView.transform.position-world)>0);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(),screen,null,out var p)) marker.anchoredPosition=p;
            var position = new Vector2(71+(float)school.Column*120,-150-(float)school.Row*42);
            cardFish.anchoredPosition=position;
            var dx=school.Target%3-school.Current%3; var dy=-(school.Target/3-school.Current/3);
            var angle=dx==0 && dy==0 ? 0 : Mathf.Atan2(dy,dx)*Mathf.Rad2Deg;
            cardFish.localRotation=Quaternion.Euler(0,0,angle);
            var fishWorld=globeRoot.TransformPoint(지구위경도선View.Coordinate(school.Latitude,school.Longitude,18.24f));
            var fishScreen=cameraView.WorldToScreenPoint(fishWorld);
            var marine=globe.GetComponent<해양생활연출View>();
            globeFish.gameObject.SetActive((marine==null || !marine.Visible) && canvas.enabled && fishScreen.z>0 && Vector3.Dot(fishWorld-globeRoot.position,cameraView.transform.position-fishWorld)>0);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(),fishScreen,null,out var fp)) globeFish.anchoredPosition=fp;
            // 작은 실제 표본의 위치는 유지한다. 기호 크기만 확대하며 분포로 오인하지 않도록 카드에 표시한다.
            marker.anchoredPosition+=new Vector2(0,-60);
            fishStatus.text=FormattableString.Invariant($"가상 실험 어군 · 선호 {school.Preference:F1}°C\n{school.Reason}\n{school.Latitude:F3}°N / {school.Longitude:F3}°E\n모의 2초/칸 · 실제 이동 속도 아님\n어종/분포 예측 아님 · 물고기 크기 확대");
        }
        private void Build()
        {
            var go = new GameObject("수온 로컬 검토 Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.hideFlags = HideFlags.DontSave; canvas = go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=960;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1000,850); scaler.matchWidthOrHeight=1f;
            MakeButton(go.transform,"수온 자료",new Vector2(18,-112),new Vector2(150,42),()=>SetExpanded(!Expanded));
            panel=Box(go.transform,"수온 카드",new Vector2(18,-160),new Vector2(395,665),new Color(.025f,.075f,.12f,.97f));
            heading=Label(panel.transform,"",new Vector2(16,-12),new Vector2(365,36),18);
            MakeButton(panel.transform,"이전 날짜",new Vector2(16,-55),new Vector2(130,34),()=>SetDay(day-1));
            MakeButton(panel.transform,"다음 날짜",new Vector2(156,-55),new Vector2(130,34),()=>SetDay(day+1));
            Label(panel.transform,"북 ↑  /  동 →   ·   격자 선택",new Vector2(16,-96),new Vector2(360,28),16);
            for(var i=0;i<9;i++)
            {
                var index=i;
                var button=MakeButton(panel.transform,"",new Vector2(16+(i%3)*120,-132-(i/3)*42),new Vector2(110,36),()=>SelectCell(index));
                cellButtons[i]=button; cellTexts[i]=button.GetComponentInChildren<Text>();
            }
            detail=Label(panel.transform,"",new Vector2(16,-265),new Vector2(365,190),17);
            fishStatus=Label(panel.transform,"",new Vector2(16,-420),new Vector2(365,118),16);
            MakeButton(panel.transform,"선호 -0.1",new Vector2(16,-545),new Vector2(112,34),()=>SetPreference(Math.Max(-2,Math.Round(school.Preference-.1,1))));
            MakeButton(panel.transform,"선호 +0.1",new Vector2(136,-545),new Vector2(112,34),()=>SetPreference(Math.Min(40,Math.Round(school.Preference+.1,1))));
            playText=MakeButton(panel.transform,"일시 정지",new Vector2(256,-545),new Vector2(120,34),()=>SetRunning(!school.Running)).GetComponentInChildren<Text>();
            MakeButton(panel.transform,"처음부터",new Vector2(16,-590),new Vector2(112,34),ResetSchool);
            MakeButton(panel.transform,"접기",new Vector2(136,-590),new Vector2(100,34),()=>SetExpanded(false));
            cardFish=Fish(panel.transform,"격자 어군",new Vector2(30,22));
            globeFish=Fish(go.transform,"지구본 가상 어군 위치 기호",new Vector2(42,28));
            globeFish.anchorMin=globeFish.anchorMax=globeFish.pivot=new Vector2(.5f,.5f);
            var markerButton=MakeButton(go.transform,"",Vector2.zero,new Vector2(130,55),()=>SetExpanded(!Expanded));
            marker=markerButton.GetComponent<RectTransform>(); marker.anchorMin=marker.anchorMax=marker.pivot=new Vector2(.5f,.5f);
            markerText=markerButton.GetComponentInChildren<Text>();
        }
        private static RectTransform Fish(Transform parent,string name,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(가상수온어군Graphic)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(.5f,.5f); rect.sizeDelta=size;
            var graphic=go.GetComponent<가상수온어군Graphic>(); graphic.color=new Color(1f,.68f,.18f,.9f); graphic.raycastTarget=false; return rect;
        }
        private static GameObject Box(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=pos; rect.sizeDelta=size;
            go.GetComponent<Image>().color=color; return go;
        }
        private static Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int font)
        {
            var go=new GameObject("문구",typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=pos; rect.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=font; text.color=Color.white; text.text=value; text.raycastTarget=false; return text;
        }
        private static Button MakeButton(Transform parent,string value,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {
            var go=Box(parent,value,pos,size,new Color(.07f,.3f,.37f)); var button=go.AddComponent<Button>(); button.targetGraphic=go.GetComponent<Image>(); button.onClick.AddListener(action);
            var text=Label(go.transform,value,Vector2.zero,size,17); text.alignment=TextAnchor.MiddleCenter; return button;
        }
        private void OnDestroy()
        {
            snapshot=null;
            if(canvas!=null) { if(UnityEngine.Application.isPlaying) Destroy(canvas.gameObject); else DestroyImmediate(canvas.gameObject); }
        }
    }
}
#endif
