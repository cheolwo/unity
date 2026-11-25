using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>선택·가상 표찰 입력과 상태 표시만 담당하며 업무 상태는 변경하지 않는다.</summary>
    public sealed class 다중센터출고View : MonoBehaviour
    {
        public event Action 주문검토요청, 예약요청, 피킹요청, 포장준비요청, 취소요청, 조회요청, 저장요청;
        public event Action<string> 센터선택요청, 지시선택요청;
        public event Action<bool> 분할변경요청;
        public Button 주문검토버튼 { get; private set; }
        public Button 예약버튼 { get; private set; }
        public Button 피킹버튼 { get; private set; }
        public Button 포장준비버튼 { get; private set; }
        public Button 취소버튼 { get; private set; }
        public Button 조회버튼 { get; private set; }
        public Button 저장버튼 { get; private set; }
        public InputField 위치입력 { get; private set; }
        public InputField 상품입력 { get; private set; }
        public InputField 수량입력 { get; private set; }
        public Toggle 분할입력 { get; private set; }
        public string 표시안내 => message == null ? "" : message.text;
        public string 표시상태 => status == null ? "" : status.text;
        public int 표시후보수 { get; private set; }
        public int 표시지시수 { get; private set; }
        private Font font;
        private Text message, status;
        private RectTransform candidates, instructions;
        private Button allCenters;
        private bool built;
        private readonly Color ink = new(.89f, .94f, .98f);
        private readonly Color teal = new(.15f, .57f, .5f);

        public void 입력환경구성()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var root = new GameObject("출고 연습 UI 입력", typeof(EventSystem), typeof(InputSystemUIInputModule));
            root.transform.SetParent(transform, false);
        }

        public void 구성()
        {
            if (built) return; built = true;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 20);
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30100;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var header = Panel("출고 연습 상단", transform, new Vector2(0, .922f), Vector2.one, new Color(.035f, .065f, .095f, .98f));
            var title = Label(header, "출고 운영실  /  센터에서 선반까지", 27, 32);
            title.rectTransform.anchorMin = new Vector2(0, .4f); title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(24, 0); title.rectTransform.offsetMax = new Vector2(-24, -3);
            var subtitle = Label(header, "가상 표본 · 실제 주문/재고 변경 없음 · 선택 → 예약 → 피킹 → 포장 준비", 16, 24);
            subtitle.rectTransform.anchorMin = Vector2.zero; subtitle.rectTransform.anchorMax = new Vector2(1, .42f);
            subtitle.rectTransform.offsetMin = new Vector2(25, 2); subtitle.rectTransform.offsetMax = new Vector2(-24, 0);

            var panel = Panel("출고 작업대", transform, new Vector2(.60f, 0), new Vector2(1, .922f), new Color(.055f, .09f, .13f, .98f));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(17, 17, 12, 12);
            layout.spacing = 6; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            Label(panel, "01   센터와 출고안", 21, 27);
            status = Label(panel, "조회 전", 16, 42);
            var orderRow = Row(panel, 38);
            주문검토버튼 = Button(orderRow, "샘플 주문 검토", () => 주문검토요청?.Invoke(), teal);
            allCenters = Button(orderRow, "전체 센터", () => 센터선택요청?.Invoke(""), new Color(.2f, .3f, .39f));
            분할입력 = SplitToggle(orderRow);
            candidates = Scroll(panel, "센터 후보 목록", 122);
            var reserveRow = Row(panel, 39);
            예약버튼 = Button(reserveRow, "출고 예약 확정", () => 예약요청?.Invoke(), teal);
            취소버튼 = Button(reserveRow, "미피킹 예약 취소", () => 취소요청?.Invoke(), new Color(.34f, .22f, .23f));
            Label(panel, "02   선반 지시와 가상 피킹", 21, 27);
            instructions = Scroll(panel, "선반 지시 목록", 90);
            위치입력 = CodeRow(panel, "위치 코드", "선반 표찰 코드");
            상품입력 = CodeRow(panel, "상품 코드", "상품 바코드");
            var pickRow = Row(panel, 39);
            Label(pickRow, "확인 수량", 17, 36);
            수량입력 = Input(pickRow, "수량", true); 수량입력.gameObject.GetComponent<LayoutElement>().preferredWidth = 85;
            피킹버튼 = Button(pickRow, "입력 수량 피킹", () => 피킹요청?.Invoke(), teal);
            포장준비버튼 = Button(panel, "03   전량 피킹 확인 · 포장 준비", () => 포장준비요청?.Invoke(), new Color(.27f, .37f, .57f), 40);
            var utilities = Row(panel, 36);
            조회버튼 = Button(utilities, "최신 조회", () => 조회요청?.Invoke(), new Color(.19f, .26f, .34f));
            저장버튼 = Button(utilities, "진행 저장", () => 저장요청?.Invoke(), new Color(.19f, .26f, .34f));
            message = Label(panel, "출고 상태를 조회하고 있습니다.", 15, 49);

            var guide = Panel("모의 공간 안내", transform, new Vector2(0, 0), new Vector2(.60f, .11f), new Color(.035f, .065f, .095f, .93f));
            var guidance = Label(guide, "선반 지시를 선택하면 해당 센터와 선반이 노란색으로 강조됩니다.\n표찰 입력은 게임 안의 확인이며 실제 카메라 스캔이 아닙니다. 포장 준비와 기사 인계는 다릅니다.", 17, 65);
            guidance.rectTransform.anchorMax = Vector2.one; guidance.rectTransform.offsetMin = new Vector2(20, 8); guidance.rectTransform.offsetMax = new Vector2(-20, -8);
            표시(null, false, "조회 전", null, false);
        }

        public void 시작실패(string text)
        {
            구성(); 표시(null, false, "실행 준비 실패 · " + text, null, false);
            조회버튼.interactable = false;
        }

        public void 표시(출고센터선택Presenter card, bool busy, string text, 출고피킹지시표시 selected, bool refreshInputs)
        {
            if (!built) 구성();
            var loaded = card?.조회됨 == true;
            var available = loaded && card.구성됨 && !busy;
            var active = available && (card.현재주문상태 == SimulationFulfillmentStateCodes.Reserved || card.현재주문상태 == SimulationFulfillmentStateCodes.Picking);
            status.text = !loaded ? "권위 상태 조회 필요" : "세션 판본 " + card.현재개정 + " · " + card.선택단계
                + "\n" + card.현재주문상태표시 + (card.선택센터목록.Length == 0 ? "" : " · " + string.Join(" / ", card.선택센터목록));
            입력가능상태(available, busy, card);
            주문검토버튼.interactable = allCenters.interactable = 분할입력.interactable = available && !active;
            피킹버튼.interactable = active && selected != null && selected.남은수량 > 0;
            포장준비버튼.interactable = active && card.피킹지시.Length > 0 && card.피킹지시.All(value => value.남은수량 == 0);
            취소버튼.interactable = active && card.피킹지시.All(value => value.피킹수량 == 0);
            위치입력.interactable = 상품입력.interactable = 수량입력.interactable = active && selected != null;
            message.text = busy ? "처리 중 · 중복 입력을 막고 있습니다." : text;
            RebuildCandidates(card, available && !active);
            RebuildInstructions(card, available, selected?.배분코드);
            if (refreshInputs)
            {
                위치입력.SetTextWithoutNotify(selected?.위치바코드 ?? "");
                상품입력.SetTextWithoutNotify(selected?.상품바코드 ?? "");
                수량입력.SetTextWithoutNotify(selected == null ? "" : Math.Min(1m, selected.남은수량).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private void 입력가능상태(bool available, bool busy, 출고센터선택Presenter card)
        {
            주문검토버튼.interactable = allCenters.interactable = available;
            예약버튼.interactable = available && card.예약확정가능;
            조회버튼.interactable = !busy;
            저장버튼.interactable = card?.조회됨 == true && !busy;
            분할입력.interactable = available;
        }

        private void RebuildCandidates(출고센터선택Presenter card, bool enabled)
        {
            Clear(candidates);
            var values = card?.센터후보 ?? Array.Empty<출고센터후보표시>(); 표시후보수 = values.Length;
            if (values.Length == 0)
            {
                var active = card?.현재주문상태 == SimulationFulfillmentStateCodes.Reserved
                    || card?.현재주문상태 == SimulationFulfillmentStateCodes.Picking;
                Label(candidates, active ? "예약한 센터의 지시를 처리 중입니다. 현재 작업을 마친 뒤 새 주문을 검토하세요."
                    : "‘샘플 주문 검토’에서 센터별 가능한 조건을 확인하세요.", 16, 50);
                return;
            }
            foreach (var value in values)
            {
                var id = value.센터코드;
                var button = Button(candidates, value.이름 + "\n" + value.요약,
                    () => 센터선택요청?.Invoke(id), value.선택가능 ? new Color(.15f, .29f, .31f) : new Color(.24f, .22f, .23f), 76);
                button.interactable = enabled && value.선택가능;
            }
        }
        private void RebuildInstructions(출고센터선택Presenter card, bool enabled, string selected)
        {
            Clear(instructions);
            var values = card?.피킹지시 ?? Array.Empty<출고피킹지시표시>(); 표시지시수 = values.Length;
            if (values.Length == 0) { Label(instructions, "출고안을 검토하면 위치와 담당 지시가 나타납니다.", 16, 45); return; }
            foreach (var value in values)
            {
                var id = value.배분코드;
                var button = Button(instructions, value.요약 + "\n남은 수량 " + value.남은수량 + " " + value.단위,
                    () => 지시선택요청?.Invoke(id), id == selected ? new Color(.34f, .34f, .18f) : new Color(.15f, .24f, .31f), 66);
                button.interactable = enabled;
            }
        }

        private InputField CodeRow(Transform parent, string label, string placeholder)
        {
            var row = Row(parent, 35); Label(row, label, 16, 35); return Input(row, placeholder, false);
        }
        private Toggle SplitToggle(Transform parent)
        {
            var root = Rect("여러 센터 분할", parent); root.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;
            var toggle = root.gameObject.AddComponent<Toggle>();
            var box = Rect("체크 배경", root); box.anchorMin = new Vector2(0, .2f); box.anchorMax = new Vector2(0, .8f); box.offsetMax = new Vector2(24, 0);
            var background = box.gameObject.AddComponent<Image>(); background.color = new Color(.2f, .29f, .34f);
            var check = Rect("체크", box); check.anchorMax = Vector2.one; check.offsetMin = new Vector2(5, 5); check.offsetMax = new Vector2(-5, -5);
            var graphic = check.gameObject.AddComponent<Image>(); graphic.color = new Color(.3f, .85f, .69f);
            toggle.targetGraphic = background; toggle.graphic = graphic;
            var text = Label(root, "분할 허용", 15, 32).rectTransform; text.anchorMax = Vector2.one; text.offsetMin = new Vector2(30, 0); text.offsetMax = Vector2.zero;
            // EditMode의 Toggle.Rebuild도 onValueChanged를 호출한다. 화면 배치가 업무 명령이 되지 않게 한다.
            toggle.onValueChanged.AddListener(value => { if (UnityEngine.Application.isPlaying) 분할변경요청?.Invoke(value); });
            return toggle;
        }
        private RectTransform Scroll(Transform parent, string name, float height)
        {
            var root = Rect(name, parent); root.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            root.gameObject.AddComponent<Image>().color = new Color(.08f, .13f, .18f);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", root); viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(5, 3); viewport.offsetMax = new Vector2(-5, -3); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 5; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; return content;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var root = Rect(name, parent); root.anchorMin = min; root.anchorMax = max; root.offsetMin = root.offsetMax = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = color; return root;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static RectTransform Row(Transform parent, float height)
        {
            var row = Rect("입력 행", parent); row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 7; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; return row;
        }
        private Text Label(Transform parent, string value, int size, float height)
        {
            var root = Rect("안내 · " + value, parent); var text = root.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size;
            text.text = value; text.color = ink; text.supportRichText = false; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = height; layout.preferredWidth = 95; return text;
        }
        private Button Button(Transform parent, string label, Action action, Color color, float height = 38)
        {
            var root = Rect(label, parent); var image = root.gameObject.AddComponent<Image>(); image.color = color;
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            root.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;
            var text = Label(root, label, 16, height); text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 16;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(7, 2); text.rectTransform.offsetMax = new Vector2(-7, -2);
            button.onClick.AddListener(() => action()); return button;
        }
        private InputField Input(Transform parent, string placeholder, bool numeric)
        {
            var root = Rect(placeholder, parent); root.gameObject.AddComponent<Image>().color = new Color(.14f, .21f, .28f);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.flexibleWidth = 1; layout.preferredWidth = 330;
            var input = root.gameObject.AddComponent<InputField>(); input.contentType = numeric ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
            input.characterLimit = numeric ? 16 : 200;
            var text = Label(root, "", 17, 35); text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(8, 0); text.rectTransform.offsetMax = new Vector2(-8, 0);
            input.textComponent = text;
            var hint = Label(root, placeholder, 16, 35); hint.color = new Color(.52f, .62f, .7f); hint.rectTransform.anchorMax = Vector2.one;
            hint.rectTransform.offsetMin = new Vector2(8, 0); hint.rectTransform.offsetMax = new Vector2(-8, 0); input.placeholder = hint; return input;
        }
        private static void Clear(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                var child = root.GetChild(index); child.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
        }
        private void OnDestroy() { if (font != null) { if (UnityEngine.Application.isPlaying) Destroy(font); else DestroyImmediate(font); } }
    }
}
