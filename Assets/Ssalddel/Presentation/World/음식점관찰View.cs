using System;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Warehouse;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>입력과 표시만 담당한다. Tick·NPC 규칙·저장을 실행하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "음식점 설정과 권위 주문 상태를 카드 UI로 표시한다.",
        Boundary = "UI 색상·텍스트는 주문 완료나 실제 Game View 검증의 근거가 아니다.")]
    public sealed class 음식점관찰View : MonoBehaviour
    {
        public event Action 새로고침요청;
        public event Action 주문요청;
        public event Action 진행요청;
        public event Action 저장요청;
        public event Action 적용요청;
        public InputField 자리입력 { get; private set; }
        public InputField 시간입력 { get; private set; }
        public Toggle 자동배정입력 { get; private set; }
        public Button 진행버튼 { get; private set; }
        public Button 주문버튼 { get; private set; }
        public Button 적용버튼 { get; private set; }
        public Button 조회버튼 { get; private set; }
        public Button 저장버튼 { get; private set; }
        public string 표시안내 => 안내.text;
        private Text 안내, 상태, 주문목록;
        private Font font;

        public void 입력환경구성()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var input = new GameObject("음식점 UI 입력", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(transform, false);
        }
        public void 시작실패(string message)
        {
            구성(); 표시(null, false, "실행 준비 실패 · " + message, false);
            조회버튼.interactable = false;
        }

        public void 구성()
        {
            if (안내 != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var panel = Rect("음식점 관찰 카드", transform);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero; panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .13f, .98f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 20); layout.spacing = 10;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            Label(panel, "음식점 관찰 · 로컬 공통 Core", 32, 38);
            Label(panel, "모의 주문 → NPC 접수 → 조리 대기 → 조리 → 픽업 준비\n자동 시간 진행 없음 · Tick은 초가 아닙니다 · 배달/운영 서버 연결 제외", 18, 52);
            상태 = Label(panel, "조회 전", 20, 52);
            var settings = Row(panel, 44);
            Label(settings, "조리 자리", 18, 40);
            자리입력 = Input(settings, "자리 수");
            Label(settings, "조리 시간(Tick)", 18, 40);
            시간입력 = Input(settings, "조리 시간");
            var toggleRoot = Rect("새 조리 배정", settings);
            toggleRoot.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            자동배정입력 = toggleRoot.gameObject.AddComponent<Toggle>();
            var box = Rect("체크", toggleRoot); box.anchorMin = new Vector2(0, .2f); box.anchorMax = new Vector2(0, .8f);
            box.offsetMin = Vector2.zero; box.offsetMax = new Vector2(26, 0);
            var background = box.gameObject.AddComponent<Image>(); background.color = new Color(.25f, .3f, .36f);
            var check = Rect("활성", box); check.anchorMax = Vector2.one; check.offsetMin = Vector2.one * 5; check.offsetMax = Vector2.one * -5;
            var graphic = check.gameObject.AddComponent<Image>(); graphic.color = new Color(.35f, .85f, .65f);
            자동배정입력.targetGraphic = background; 자동배정입력.graphic = graphic;
            var toggleLabel = Label(toggleRoot, "새 조리 배정", 18, 40).rectTransform;
            toggleLabel.anchorMax = Vector2.one; toggleLabel.offsetMin = new Vector2(34, 0); toggleLabel.offsetMax = Vector2.zero;
            var actions = Row(panel, 44);
            적용버튼 = Button(actions, "설정 적용", () => 적용요청?.Invoke());
            주문버튼 = Button(actions, "샘플 주문 추가", () => 주문요청?.Invoke());
            진행버튼 = Button(actions, "1 Tick 진행", () => 진행요청?.Invoke());
            조회버튼 = Button(actions, "새로고침", () => 새로고침요청?.Invoke());
            저장버튼 = Button(actions, "저장", () => 저장요청?.Invoke());
            안내 = Label(panel, "조회 중", 18, 56);
            var scrollRoot = Rect("주문 스크롤", panel);
            scrollRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            scrollRoot.gameObject.AddComponent<Image>().color = new Color(.10f, .14f, .19f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", scrollRoot); viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12, 8); viewport.offsetMax = new Vector2(-12, -8);
            viewport.gameObject.AddComponent<RectMask2D>();
            주문목록 = Label(viewport, "주문 없음", 19, 40);
            var content = 주문목록.rectTransform; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            주문목록.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            표시(null, false, "조회 전", false);
        }

        public void 표시(음식점정책카드Presenter card, bool busy, string message, bool draftRefresh)
        {
            var loaded = card?.조회됨 == true;
            var edit = loaded && card.편집가능 && !busy;
            자리입력.interactable = 시간입력.interactable = 자동배정입력.interactable = 적용버튼.interactable = edit;
            조회버튼.interactable = !busy;
            저장버튼.interactable = loaded && !busy;
            주문버튼.interactable = 진행버튼.interactable = edit && card.진행가능;
            if (draftRefresh && edit)
            {
                자리입력.SetTextWithoutNotify(card.조리자리수?.ToString() ?? "");
                시간입력.SetTextWithoutNotify(card.조리시간?.ToString() ?? "");
                자동배정입력.SetIsOnWithoutNotify(card.자동조리배정);
            }
            상태.text = loaded ? $"Tick {card.현재Tick} · 판본 {card.판본} · {card.설정상태}" : "권위 상태 조회 필요";
            안내.text = busy ? "처리 중 · 중복 입력 차단" : message;
            주문목록.text = !loaded ? "조회되지 않은 주문은 표시하지 않습니다." : card.주문목록.Length == 0
                ? "주문 없음 · ‘샘플 주문 추가’를 눌러 시작하세요."
                : string.Join("\n\n", card.주문목록.Select(order => order.주문식별자 + "\n" + order.표시상태
                    + (order.조리시작Tick.HasValue ? $" · 조리 일정 Tick {order.조리시작Tick}~{order.조리완료예정Tick}" : "")));
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        private static RectTransform Row(Transform parent, float height)
        {
            var row = Rect("입력 행", parent); row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12;
            layout.childControlHeight = true; layout.childControlWidth = true; return row;
        }
        private Text Label(Transform parent, string text, int size, float height)
        {
            var label = Rect(text, parent).gameObject.AddComponent<Text>(); label.font = font;
            label.text = text; label.fontSize = size; label.color = new Color(.88f, .93f, .98f);
            label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
            var layout = label.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = height;
            layout.preferredWidth = 150; return label;
        }
        private InputField Input(Transform parent, string name)
        {
            var root = Rect(name, parent); root.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;
            root.gameObject.AddComponent<Image>().color = new Color(.2f, .25f, .31f);
            var input = root.gameObject.AddComponent<InputField>(); input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 4;
            input.textComponent = Label(root, "", 20, 40);
            input.textComponent.rectTransform.anchorMax = Vector2.one;
            input.textComponent.rectTransform.offsetMin = new Vector2(8, 0);
            input.textComponent.rectTransform.offsetMax = new Vector2(-8, 0); return input;
        }
        private Button Button(Transform parent, string text, Action action)
        {
            var root = Rect(text, parent); root.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            var image = root.gameObject.AddComponent<Image>(); image.color = new Color(.19f, .36f, .43f);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var label = Label(root, text, 18, 40); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(() => action()); return button;
        }
        private void OnDestroy()
        {
            if (font == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(font); else DestroyImmediate(font);
        }
    }
}
