using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Observation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>읽기·탐색·명시적 요청만 담당한다. 닫기와 앱 이동은 세계 시간을 변경하지 않는다.</summary>
    public sealed partial class 동네관찰휴대폰View : MonoBehaviour
    {
        public event Action<string> 명령요청;
        public event Action<string> 주체관찰요청;
        public event Action<string> 시설관찰요청;
        public event Action<string> AreaSet관찰요청;
        public event Action 생활현장복귀요청;
        public bool IsOpen { get; private set; }
        public 동네휴대폰App SelectedApp { get; private set; }
        public 동네정책Draft Draft { get; } = new 동네정책Draft();
        public int CreatedRowCount => rows.Count;
        public RectTransform PhoneRect => panel;
        private readonly List<Card> rows = new();
        private readonly Stack<(동네휴대폰App app, string id)> history = new();
        private RectTransform panel, content;
        private Text status, title, iconText;
        private GameObject icon;
        private Font font;
        private ScrollRect scroll;
        private 동네관찰ScreenModel model;
        private string detailId = "", message = "", metrics = "";
        private 운영지도AreaViewModel[] 운영지도Areas = Array.Empty<운영지도AreaViewModel>();
        private string 운영지도Notice = "실제 E5 AreaSet을 확인하는 중입니다.";
        private bool busy, pending, playing, blocked, complete, confirmRestart, debug;
        private static readonly Color Ink = new(.10f, .16f, .22f), Muted = new(.34f, .40f, .45f), Paper = new(.94f, .96f, .95f);
        private static readonly Color Accent = new(.08f, .37f, .31f);
        private sealed class Card { public GameObject Root; public Text Text; public Button Button; }

        public void 구성()
        {
            if (panel != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("동네 휴대폰 입력", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
            var canvasObject = new GameObject("동네 관찰 휴대폰 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
            var launcher = 버튼(canvasObject.transform, "휴대폰 열기", () => 열기());
            icon = launcher.gameObject; iconText = launcher.GetComponentInChildren<Text>();
            고정((RectTransform)launcher.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 22), new Vector2(94, 82));
            launcher.GetComponent<Image>().color = Accent; iconText.color = Color.white; iconText.fontSize = 19;
            panel = 상자(canvasObject.transform, "휴대폰 패널", Ink);
            panel.anchorMin = new Vector2(1, 0); panel.anchorMax = Vector2.one; panel.pivot = new Vector2(1, .5f);
            panel.offsetMin = new Vector2(-412, 16); panel.offsetMax = new Vector2(-16, -16);
            var screen = 상자(panel, "휴대폰 화면", Paper); 채움(screen, 9, 9, 9, 9);
            var top = 상자(screen, "상태 표시줄", Accent);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1); top.sizeDelta = new Vector2(0, 42);
            status = 글(top, "상태", 14, Color.white); 채움(status.rectTransform, 12, 0, 12, 0);
            var header = 상자(screen, "앱 제목", Color.white);
            header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one; header.pivot = new Vector2(.5f, 1);
            header.anchoredPosition = new Vector2(0, -42); header.sizeDelta = new Vector2(0, 52);
            title = 글(header, "동네 관찰", 22, Ink); 채움(title.rectTransform, 16, 0, 16, 0);
            var viewport = 상자(screen, "앱 스크롤", Paper); 채움(viewport, 10, 102, 10, 60);
            viewport.gameObject.AddComponent<RectMask2D>(); scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport;
            content = new GameObject("앱 내용", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = content;
            var nav = 상자(screen, "휴대폰 탐색", Color.white);
            nav.anchorMin = Vector2.zero; nav.anchorMax = new Vector2(1, 0); nav.pivot = new Vector2(.5f, 0); nav.sizeDelta = new Vector2(0, 52);
            var navLayout = nav.gameObject.AddComponent<HorizontalLayoutGroup>(); navLayout.padding = new RectOffset(6, 6, 4, 4); navLayout.spacing = 5;
            버튼(nav, "뒤로", 뒤로); 버튼(nav, "홈", 홈); 버튼(nav, "닫기", 닫기);
            panel.gameObject.SetActive(false);
            iconText.text = "▯\n휴대폰";
        }

        public void 표시(동네관찰ScreenModel value, bool isPlaying, bool isBusy, bool saveBlocked, bool isComplete, string notice, string performance, bool commandPending = false)
        {
            model = value; playing = isPlaying; busy = isBusy; blocked = saveBlocked; complete = isComplete; message = notice; metrics = performance;
            pending = commandPending;
            if (panel == null) 구성();
            var mode = blocked ? "저장 확인 필요" : complete ? "관찰 완료" : playing ? "진행 중" : "일시정지";
            status.text = $"{model.Time}    ·    {mode}";
            iconText.text = $"▯  휴대폰\n{(playing ? "진행 중" : complete ? "완료" : "정지")}";
            if (IsOpen) 그리기();
        }

        public void 운영지도표시(IReadOnlyList<운영지도AreaViewModel> areas, string notice)
        {
            운영지도Areas = areas?.ToArray() ?? Array.Empty<운영지도AreaViewModel>();
            운영지도Notice = notice ?? "";
            if (IsOpen && SelectedApp == 동네휴대폰App.운영지도) 그리기();
        }
        public void 열기() { IsOpen = true; panel.gameObject.SetActive(true); icon.SetActive(false); 그리기(); }
        public void 닫기() { IsOpen = false; panel.gameObject.SetActive(false); icon.SetActive(true); EventSystem.current?.SetSelectedGameObject(null); }
        public void 홈() { if (외부홈 != null) { 외부홈(); return; } history.Clear(); SelectedApp = 동네휴대폰App.홈; detailId = ""; confirmRestart = false; 새화면(); }
        public void 뒤로()
        {
            if (외부뒤로 != null) { 외부뒤로(); return; }
            if (confirmRestart) { confirmRestart = false; 새화면(); return; }
            if (history.Count == 0) { if (SelectedApp == 동네휴대폰App.홈) 닫기(); else 홈(); return; }
            var previous = history.Pop(); SelectedApp = previous.app; detailId = previous.id; 새화면();
        }
        public void 이동(동네휴대폰App app, string id = "")
        { history.Push((SelectedApp, detailId)); SelectedApp = app; detailId = id; confirmRestart = false; 새화면(); }
        private void 새화면() { 그리기(); scroll.verticalNormalizedPosition = 1; }
        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) 뒤로();
        }
        private void 그리기()
        {
            if (외부카드 != null) { 외부그리기(); return; }
            if (model == null) return;
            title.text = SelectedApp == 동네휴대폰App.홈 ? "동네 관찰·운영" : SelectedApp.ToString();
            int count = 0;
            void Row(string name, string body = "", Action click = null, bool enabled = true, bool primary = false)
            {
                if (rows.Count <= count)
                {
                    var button = 버튼(content, "앱 카드 " + count, null);
                    var element = button.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 84;
                    var text = button.GetComponentInChildren<Text>(); text.alignment = TextAnchor.MiddleLeft; text.fontSize = 17;
                    채움(text.rectTransform, 14, 7, 12, 7);
                    rows.Add(new Card { Root = button.gameObject, Text = text, Button = button });
                }
                var row = rows[count++]; row.Root.SetActive(true); row.Root.name = "카드 · " + name;
                var copy = click; row.Button.onClick.RemoveAllListeners(); if (copy != null) row.Button.onClick.AddListener(() => copy());
                row.Button.interactable = enabled && click != null;
                row.Button.GetComponent<Image>().color = primary ? Accent : Color.white;
                row.Text.color = primary ? Color.white : Ink;
                row.Text.text = name + (body.Length == 0 ? "" : "\n" + body);
                var lines = row.Text.text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 21f)));
                row.Root.GetComponent<LayoutElement>().preferredHeight = Math.Max(64, 22 + lines * 25);
                var colors = row.Button.colors; colors.disabledColor = enabled ? Color.white : new Color(.75f, .78f, .78f); row.Button.colors = colors;
            }
            if (SelectedApp == 동네휴대폰App.홈)
            {
                Row("지금, 동네는", model.Summary);
                Row("관찰 경계", model.BoundaryNotice);
                foreach (var app in new[] { 동네휴대폰App.운영지도, 동네휴대폰App.동네, 동네휴대폰App.주문, 동네휴대폰App.NPC, 동네휴대폰App.창고, 동네휴대폰App.정책 })
                { var target = app; Row(app.ToString(), 설명(app), () => 이동(target), true, app == 동네휴대폰App.동네); }
            }
            else if (SelectedApp == 동네휴대폰App.운영지도)
            {
                Row("실제 E5 AreaSet 운영지도", 운영지도Notice + "\n관찰 초점만 바꾸며 Player·NPC·Simulation 상태는 변경하지 않습니다.");
                foreach (var area in 운영지도Areas)
                {
                    var target = area.StableId;
                    var state = area.Selected ? "현재 관찰 지역" : area.Reason;
                    Row((area.Selected ? "● " : "") + area.DisplayName, state,
                        () => AreaSet관찰요청?.Invoke(target), area.Selectable, area.Selected);
                }
                if (운영지도Areas.Length == 0)
                    Row("표시할 실제 AreaSet 없음", "현재 runtime network가 아직 준비되지 않았습니다.");
                Row("현재 생활 현장으로 복귀", "프로필5 생활 관찰 카메라로 돌아갑니다.",
                    () => 생활현장복귀요청?.Invoke(), true, true);
            }
            else if (SelectedApp == 동네휴대폰App.동네)
            {
                Row("30분 동네 관찰", model.Summary + "\n" + model.BoundaryNotice);
                Row("자율 음식 배달", "재생 한 번 후 주문·수락·조리·배차·픽업·수령을 NPC가 처리합니다.\n기존 음식/마트 교대와 표본 종료 규칙을 유지합니다.");
                foreach (var cycle in model.FoodLifeCycles)
                {
                    var target = cycle;
                    Row(target.Title, target.Body + "\n눌러서 주문자·음식점·배달 기사를 선택", () => 이동(동네휴대폰App.주문, target.OrderId));
                }
                foreach (var cycle in model.FoodCycles)
                {
                    var target = cycle;
                    Row(target.Title, target.Body, () => 이동(동네휴대폰App.NPC, target.ActorId));
                }
                Row(pending ? "요청 대기·처리 중…" : busy ? "진행·저장 중…" : message, "앱을 닫아도 열어도 현재 재생 상태를 유지합니다.");
                Row(playing ? "일시정지" : "재생", "진행 중 저장은 마무리합니다. 종료 시간 따라잡기 없음", () => 명령요청?.Invoke("play"), !blocked && !complete, true);
                Row("1초 진행", "현재 저장 뒤 정지 상태에서 한 단계 확인", () => 명령요청?.Invoke("step"), !pending && !blocked && !complete);
                Row(blocked ? "저장 다시 시도" : "지금 저장", "진행 중 저장 뒤 한 건만 요청합니다", () => 명령요청?.Invoke("save"), !pending);
                Row("전체 지도 보기", "주체 따라가기를 종료", () => 명령요청?.Invoke("overview"));
                if (complete)
                {
                    Row(confirmRestart ? "새 표본 시작 확인" : "새 30분 표본", "완료된 전용 저장을 새 표본으로 교체합니다", () => {
                        if (confirmRestart) { confirmRestart = false; 명령요청?.Invoke("restart"); } else { confirmRestart = true; 그리기(); }
                    }, !pending && !blocked);
                    if (confirmRestart) Row("취소 · 기존 결과 유지", "", () => { confirmRestart = false; 그리기(); });
                }
                Row(debug ? "개발 정보 접기" : "개발 정보", "저장 성능과 조회 판본", () => { debug = !debug; 그리기(); });
                if (debug) Row("조회 판본 " + model.Revision, metrics);
            }
            else if (SelectedApp == 동네휴대폰App.정책)
            {
                Row(Draft.Dirty ? "편집 중 · 아직 적용하지 않음" : "현재 적용된 정책", "세계가 진행되어도 편집 내용은 유지됩니다.");
                Row("자동 업무 · " + (Draft.Automation ? "켜짐" : "꺼짐"), "새 업무 수락/배정. 기존 업무는 마무리합니다", () => { Draft.변경(automation: !Draft.Automation); 그리기(); }, !pending);
                Row("조리 시간 · " + Draft.Cooking + "초", "누르면 2초 / 5초 전환", () => { Draft.변경(cooking: Draft.Cooking == 2 ? 5 : 2); 그리기(); }, !pending);
                Row("입고 우선순위 · " + Draft.Priority, "50 주문 준비 우선 / 100 보충 입고 우선", () => { Draft.변경(priority: Draft.Priority == 100 ? 50 : 100); 그리기(); }, !pending);
                Row("보충 기준 · " + Draft.Threshold + "개", "누르면 상품별 2개 / 3개 기준 전환", () => { Draft.변경(threshold: Draft.Threshold == 2 ? 3 : 2); 그리기(); }, !pending);
                Row($"근무 {Draft.Work}초 / 휴식 {Draft.Rest}초", "420/120 ↔ 300/90. 현재 업무를 강제 취소하지 않음", () => { bool standard = Draft.Work == 420; Draft.변경(work: standard ? 300 : 420, rest: standard ? 90 : 120); 그리기(); }, !pending);
                Row("변경 적용", "현재 저장 뒤 한 건 적용 · 재생 상태 유지", () => 명령요청?.Invoke("policy"), Draft.Dirty && !pending && !blocked && !complete, true);
                Row("편집 취소", "현재 원장의 정책을 다시 읽습니다", () => 명령요청?.Invoke("discard"), Draft.Dirty && !pending);
                if (blocked) Row("저장 확인 필요", "동네 앱에서 저장을 다시 시도하세요");
            }
            else
            {
                var source = SelectedApp == 동네휴대폰App.주문 ? model.Orders : SelectedApp == 동네휴대폰App.NPC ? model.Actors : model.Warehouses;
                var selected = source.FirstOrDefault(x => x.Id == detailId);
                if (selected == null)
                {
                    Row(SelectedApp + " · " + source.Length, "항목을 눌러 담당자와 관련 시설을 확인하세요");
                    foreach (var item in source) { var target = item.Id; Row(item.Title, item.Body, () => 이동(SelectedApp, target)); }
                }
                else
                {
                    Row(selected.Title, selected.Body);
                    var foodLife = model.FoodLifeCycles.FirstOrDefault(x => x.OrderId == selected.Id);
                    if (foodLife != null)
                    {
                        Row("주문자 · " + 동네관찰Presenter.이름(foodLife.OrdererActorId), "NPC 상세에서 명시적으로 따라가기", () => 이동(동네휴대폰App.NPC, foodLife.OrdererActorId));
                        Row("음식점 · " + 동네관찰Presenter.이름(foodLife.RestaurantActorId), "NPC 상세 또는 시설 위치를 선택", () => 이동(동네휴대폰App.NPC, foodLife.RestaurantActorId));
                        if (foodLife.DriverActorId.Length > 0)
                            Row("배달 기사 · " + 동네관찰Presenter.이름(foodLife.DriverActorId), "NPC 상세에서 명시적으로 따라가기", () => 이동(동네휴대폰App.NPC, foodLife.DriverActorId));
                        Row("현재 관련 시설 · " + 동네관찰Presenter.시설이름(foodLife.CurrentFacilityId), "사용자가 선택할 때만 카메라 이동", () => 시설관찰요청?.Invoke(foodLife.CurrentFacilityId));
                    }
                    if (selected.ActorId.Length > 0)
                    {
                        if (SelectedApp != 동네휴대폰App.NPC) Row("담당 NPC · " + 동네관찰Presenter.이름(selected.ActorId), "업무·휴식과 대기 사유", () => 이동(동네휴대폰App.NPC, selected.ActorId));
                        Row("이 NPC 따라가기", "패널은 유지하고 지도 카메라만 이동", () => 주체관찰요청?.Invoke(selected.ActorId), true, true);
                    }
                    if (selected.RelatedId.Length > 0) Row("관련 주문 보기", "같은 업무 원장으로 이동", () => 이동(동네휴대폰App.주문, selected.RelatedId));
                    if (selected.FacilityId.Length > 0)
                    {
                        Row("지도 · " + 동네관찰Presenter.시설이름(selected.FacilityId), "시설 위치 관찰", () => 시설관찰요청?.Invoke(selected.FacilityId));
                        if (selected.FacilityId == "mart" || selected.FacilityId == "depot") Row("재고·보충 운송", "창고 앱으로 이동", () => 이동(동네휴대폰App.창고));
                    }
                    Row(debug ? "개발 정보 접기" : "고유 식별자 확인", "개발 정보", () => { debug = !debug; 그리기(); });
                    if (debug) Row("원장 ID", selected.Id);
                }
            }
            for (int i = count; i < rows.Count; i++) rows[i].Root.SetActive(false);
        }
        private static string 설명(동네휴대폰App app) => app switch {
            동네휴대폰App.운영지도 => "Nature · Farm · Hub · Town 실제 E5 AreaSet 관찰",
            동네휴대폰App.동네 => "흐르는 시간 · 재생과 저장", 동네휴대폰App.주문 => "음식과 마트 · 준비에서 수령까지",
            동네휴대폰App.NPC => "지금 하는 일 · 따라가기", 동네휴대폰App.창고 => "보관 · 예약 · 보충 운송", _ => "자동 업무 · 조리 · 근무와 휴식" };
        private RectTransform 상자(Transform parent, string name, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.GetComponent<Image>().color = color; return rect;
        }
        private Text 글(Transform parent, string name, int size, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>(); text.transform.SetParent(parent, false);
            text.font = font; text.fontSize = size; text.color = color; text.text = name; text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private Button 버튼(Transform parent, string name, Action click)
        {
            var rect = 상자(parent, name, Color.white); var button = rect.gameObject.AddComponent<Button>();
            var text = 글(rect, name, 17, Ink); text.alignment = TextAnchor.MiddleCenter; 채움(text.rectTransform, 5, 0, 5, 0);
            if (click != null) button.onClick.AddListener(() => click()); return button;
        }
        private static void 채움(RectTransform rect, float left, float top, float right, float bottom)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
        private static void 고정(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private void OnDestroy() { if (font != null) { if (UnityEngine.Application.isPlaying) Destroy(font); else DestroyImmediate(font); } }
    }
}
