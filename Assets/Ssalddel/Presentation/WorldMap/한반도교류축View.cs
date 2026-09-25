using System;
using System.Linq;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>
    /// 한반도 서부 교류축의 출처·자료 상태를 상대적 순서로 보여 주는 읽기 전용 View다.
    /// 정밀 지리, 현재 통행 또는 실제 운행을 확정하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 한반도교류축View : MonoBehaviour
    {
        public const string CatalogResourceName = "KoreanPeninsulaCorridorCatalog";
        public const string CanvasName = "한반도 교류축 Canvas";

        private 한반도교류축Catalog catalog;
        private Canvas canvas;
        private Text detailTitle;
        private Text detailBody;
        private RectTransform northboundTrain;
        private RectTransform southboundTrain;
        private 한반도교류축Hub selectedHub;
        private bool initialized;

        public bool IsVisible => canvas != null && canvas.enabled;
        public int HubCount => catalog?.hubs?.Length ?? 0;
        public int SegmentCount => catalog?.segments?.Length ?? 0;
        public int AnimatedConvoyCount => northboundTrain != null && southboundTrain != null ? 2 : 0;
        public bool ScenarioPresentationOnly => catalog != null
            && catalog.observationPresentationOnly
            && !catalog.operationalAuthority;
        public 한반도교류축Hub SelectedHub => selectedHub;
        public 한반도교류축Catalog Catalog => catalog;

        private void Update()
        {
            if (!IsVisible || northboundTrain == null || southboundTrain == null) return;
            var progress = Mathf.PingPong(Time.unscaledTime / 12f, 1f);
            northboundTrain.anchoredPosition = new Vector2(314f, Mathf.Lerp(-450f, -114f, progress));
            southboundTrain.anchoredPosition = new Vector2(432f, Mathf.Lerp(-114f, -450f, progress));
        }

        public void Initialize()
        {
            if (initialized) return;
            var asset = Resources.Load<TextAsset>(CatalogResourceName)
                ?? throw new InvalidOperationException("KoreanPeninsulaCorridorCatalogResourceMissing");
            catalog = JsonUtility.FromJson<한반도교류축Catalog>(asset.text)
                ?? throw new InvalidOperationException("KoreanPeninsulaCorridorCatalogJsonInvalid");
            catalog.Validate();
            BuildInterface();
            initialized = true;
            Hide();
        }

        public void Show()
        {
            Initialize();
            canvas.enabled = true;
        }

        public void SetRenderCamera(Camera renderCamera)
        {
            Initialize();
            if (renderCamera == null) throw new ArgumentNullException(nameof(renderCamera));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = renderCamera;
            canvas.planeDistance = 1f;
        }

        public void Hide()
        {
            if (canvas != null) canvas.enabled = false;
        }

        public 한반도교류축Hub SelectHub(string researchSubjectId)
        {
            Initialize();
            var hub = catalog.FindHub(researchSubjectId);
            if (hub == null) return null;
            selectedHub = hub;
            detailTitle.text = hub.displayName;
            detailBody.text = $"자료 상태: {EvidenceLabel(hub.evidenceStateCode)}\n"
                + $"운행·연결 상태: {OperationLabel(hub.operationStateCode)}\n"
                + $"최대 관찰 단계: {hub.maximumObservationLevel}\n\n"
                + $"제한: {hub.limitationText}\n\n"
                + $"출처 영수증: {hub.sourceReceiptStableIds.Length}건";
            return hub;
        }

        private void OnDestroy()
        {
            if (canvas == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(canvas.gameObject);
            else DestroyImmediate(canvas.gameObject);
        }

        private void BuildInterface()
        {
            canvas = CreateCanvas();
            var panel = CreatePanel(canvas.transform, "PeninsulaCorridorPanel",
                new Vector2(22f, -150f), new Vector2(520f, 520f));
            var title = CreateLabel(panel.transform, "Title", new Vector2(18f, -14f),
                new Vector2(484f, 34f), 24, TextAnchor.MiddleLeft, Color.white);
            title.text = "한반도 서부 교류축 · 관찰 자료";
            var notice = CreateLabel(panel.transform, "Notice", new Vector2(18f, -50f),
                new Vector2(484f, 54f), 13, TextAnchor.UpperLeft,
                new Color(.78f, .87f, .93f));
            notice.text = "현재·역사·단절·국제 회랑을 구분합니다.\n정밀 지리와 현재 통행을 확정하지 않습니다.";

            var ordered = catalog.hubs.OrderBy(value => value.displayOrder).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var hub = ordered[index];
                var button = CreateButton(panel.transform, "Hub." + hub.researchSubjectId,
                    hub.displayName, new Vector2(18f, -112f - index * 43f),
                    new Vector2(172f, 36f), StateColor(hub.operationStateCode));
                var capturedId = hub.researchSubjectId;
                button.onClick.AddListener(() => SelectHub(capturedId));
            }

            detailTitle = CreateLabel(panel.transform, "HubDetailTitle",
                new Vector2(210f, -116f), new Vector2(292f, 38f), 21,
                TextAnchor.MiddleLeft, Color.white);
            detailTitle.text = "거점을 선택하세요";
            detailBody = CreateLabel(panel.transform, "HubDetailBody",
                new Vector2(210f, -160f), new Vector2(292f, 318f), 14,
                TextAnchor.UpperLeft, new Color(.88f, .93f, .96f));
            detailBody.text = "부산에서 신의주까지의 거점은 출처와 자료 상태에 따라 제한적으로 표시됩니다.";

            BuildScenarioMap();
        }

        private void BuildScenarioMap()
        {
            var panel = CreatePanel(canvas.transform, "ExchangeScenarioPanel",
                new Vector2(558f, -150f), new Vector2(700f, 520f));
            var title = CreateLabel(panel.transform, "ScenarioTitle", new Vector2(18f, -14f),
                new Vector2(664f, 32f), 23, TextAnchor.MiddleLeft, Color.white);
            title.text = "가상 교류 시나리오 · 철도 생활상";
            var notice = CreateLabel(panel.transform, "ScenarioBoundary", new Vector2(18f, -48f),
                new Vector2(664f, 48f), 13, TextAnchor.UpperLeft,
                new Color(1f, .79f, .35f));
            notice.text = "SimulationAnalog · 실제 운행/통행/교역량이 아닙니다.\n선 색은 현재·역사·단절·검토·국제 회랑 자료 상태를 구분합니다.";

            var ordered = catalog.hubs.OrderBy(value => value.displayOrder).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var hub = ordered[index];
                var y = -450f + index * 48f;
                var node = CreateButton(panel.transform, "ScenarioHub." + hub.researchSubjectId,
                    "●  " + hub.displayName, new Vector2(26f, y - 14f),
                    new Vector2(190f, 30f), StateColor(hub.operationStateCode));
                var capturedId = hub.researchSubjectId;
                node.onClick.AddListener(() => SelectHub(capturedId));
                if (index == ordered.Length - 1) continue;
                var segment = catalog.segments.First(value =>
                    value.fromResearchSubjectId == hub.researchSubjectId);
                CreateSegment(panel.transform, segment, new Vector2(258f, y + 5f));
            }

            northboundTrain = CreateConvoy(panel.transform, "NorthboundScenarioTrain",
                "▲ 북행 물자", new Color(.95f, .42f, .13f));
            southboundTrain = CreateConvoy(panel.transform, "SouthboundScenarioTrain",
                "▼ 남행 물자", new Color(.16f, .55f, .86f));
            northboundTrain.anchoredPosition = new Vector2(314f, -450f);
            southboundTrain.anchoredPosition = new Vector2(432f, -114f);

            var cargo = CreateLabel(panel.transform, "ScenarioCargo", new Vector2(282f, -404f),
                new Vector2(390f, 96f), 14, TextAnchor.UpperLeft,
                new Color(.86f, .93f, .98f));
            cargo.text = "관찰 카드\n■ 식료품   ■ 의약품\n■ 생활물자 ■ 완성품\n\n교류 성립 시의 생활상을 비유합니다.";
        }

        private static void CreateSegment(
            Transform parent,
            한반도교류축Segment segment,
            Vector2 position)
        {
            var item = new GameObject("ScenarioSegment." + segment.segmentStableId,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = segment.segmentStateCode == "Disconnected"
                ? new Vector2(10f, 24f)
                : new Vector2(6f, 42f);
            item.GetComponent<Image>().color = StateColor(segment.segmentStateCode);

            var state = CreateLabel(parent, "ScenarioSegmentState." + segment.segmentStableId,
                position + new Vector2(18f, -2f), new Vector2(220f, 24f), 11,
                TextAnchor.MiddleLeft, new Color(.72f, .82f, .88f));
            state.text = OperationLabel(segment.segmentStateCode);
        }

        private static RectTransform CreateConvoy(
            Transform parent,
            string name,
            string label,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(108f, 28f);
            item.GetComponent<Image>().color = color;
            var text = CreateLabel(item.transform, "Label", Vector2.zero,
                rect.sizeDelta, 12, TextAnchor.MiddleCenter, Color.white);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            text.text = label;
            return rect;
        }

        private static Canvas CreateCanvas()
        {
            var item = new GameObject(CanvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            item.layer = 세계지구본View.GlobeLayer;
            var result = item.GetComponent<Canvas>();
            result.renderMode = RenderMode.ScreenSpaceOverlay;
            result.sortingOrder = 905;
            var scaler = item.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            return result;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = new Color(.025f, .065f, .11f, .94f);
            return item;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            var text = CreateLabel(item.transform, "Label", Vector2.zero, size, 14,
                TextAnchor.MiddleCenter, Color.white);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            text.text = label;
            return item.GetComponent<Button>();
        }

        private static Color StateColor(string state)
            => state switch
            {
                "OperationalDomestic" => new Color(.12f, .45f, .30f),
                "HistoricalConnection" => new Color(.35f, .39f, .44f),
                "Disconnected" => new Color(.56f, .20f, .20f),
                "AwaitingIdentityReview" => new Color(.56f, .40f, .12f),
                _ => new Color(.18f, .31f, .48f),
            };

        private static string EvidenceLabel(string state)
            => state switch
            {
                "ConfirmedCurrentDomestic" => "현재 국내 관찰 확인",
                "ConfirmedHistoricalEvent" => "역사 사건 확인",
                "ConfirmedInternationalCorridorDescription" => "국제 회랑 기술 확인",
                "AwaitingIdentityReview" => "거점 관계 검토 중",
                _ => "추가 조사 필요",
            };

        private static string OperationLabel(string state)
            => state switch
            {
                "OperationalDomestic" => "현재 국내 운행",
                "HistoricalConnection" => "역사적 연결",
                "Disconnected" => "현재 단절",
                "AwaitingIdentityReview" => "관계 검토 중",
                "InternationalCorridorDescription" => "국제 회랑 기술",
                _ => "조사 대기",
            };
    }
}
