using System;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>
    /// 한반도 거점 지형에서 준비된 행정동·역세권·디오라마로 내려가는
    /// 읽기 전용 선택 UI다. 공간 권위나 Actor 위치는 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 공간의미확대전환View : MonoBehaviour
    {
        public const string PanelName = "SpatialSemanticTransitionPanel";

        private readonly 공간의미확대전환흐름 flow = new();
        private Func<bool> openPreparedDiorama;
        private GameObject panel;
        private Text title;
        private Text stableId;
        private Text boundary;
        private Text actionStatus;
        private Button backButton;
        private Button nextButton;
        private Button openButton;
        private bool initialized;

        public 공간의미확대전환단계 CurrentStage => flow.CurrentStage;
        public string CurrentStableId => flow.CurrentStableId;
        public string DiagnosticCode => flow.DiagnosticCode;
        public bool IsVisible => panel != null && panel.activeSelf;
        public bool CanOpenPreparedDiorama => flow.CanOpenPreparedDiorama;
        public string LastActionStatus { get; private set; } = "WaitingForHubLevel";

        public void Initialize(Canvas hostCanvas, Func<bool> openDiorama)
        {
            if (initialized) return;
            if (hostCanvas == null) throw new ArgumentNullException(nameof(hostCanvas));
            openPreparedDiorama = openDiorama ?? throw new ArgumentNullException(nameof(openDiorama));
            BuildInterface(hostCanvas.transform);
            initialized = true;
            UpdatePresentation();
        }

        public void SetTerrainStage(한반도지형미리보기단계 stage)
        {
            flow.SetTerrainHubLevel(stage == 한반도지형미리보기단계.Hubs);
            LastActionStatus = stage == 한반도지형미리보기단계.Hubs
                ? flow.DiagnosticCode
                : "WaitingForHubLevel";
            UpdatePresentation();
        }

        public bool AdvanceForTests()
        {
            var changed = flow.TryAdvance();
            if (changed) LastActionStatus = flow.DiagnosticCode;
            UpdatePresentation();
            return changed;
        }

        public bool RetreatForTests()
        {
            var changed = flow.TryRetreat();
            if (changed) LastActionStatus = flow.DiagnosticCode;
            UpdatePresentation();
            return changed;
        }

        public bool OpenPreparedDioramaForTests()
        {
            if (!flow.CanOpenPreparedDiorama)
            {
                LastActionStatus = "StationAreaSelectionRequired";
                UpdatePresentation();
                return false;
            }
            if (!openPreparedDiorama())
            {
                LastActionStatus = "PreparedDioramaUnavailable";
                UpdatePresentation();
                return false;
            }

            flow.MarkLocalDioramaOpened();
            LastActionStatus = flow.DiagnosticCode;
            UpdatePresentation();
            return true;
        }

        public void RestoreStationArea()
        {
            flow.RestoreStationArea();
            LastActionStatus = flow.DiagnosticCode;
            UpdatePresentation();
        }

        private void BuildInterface(Transform parent)
        {
            panel = new GameObject(PanelName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-18f, -18f);
            rect.sizeDelta = new Vector2(390f, 324f);
            panel.GetComponent<Image>().color = new Color(.025f, .065f, .11f, .94f);

            title = Label(panel.transform, "TransitionTitle", new Vector2(18f, -16f),
                new Vector2(354f, 36f), 23, Color.white);
            stableId = Label(panel.transform, "TransitionStableId", new Vector2(18f, -58f),
                new Vector2(354f, 62f), 13, new Color(.75f, .88f, .95f));
            boundary = Label(panel.transform, "TransitionBoundary", new Vector2(18f, -126f),
                new Vector2(354f, 70f), 15, new Color(.92f, .90f, .75f));
            actionStatus = Label(panel.transform, "TransitionStatus", new Vector2(18f, -202f),
                new Vector2(354f, 30f), 12, new Color(.68f, .80f, .88f));

            backButton = Button(panel.transform, "TransitionBackButton", "상위로",
                new Vector2(18f, -244f), new Vector2(108f, 48f),
                new Color(.18f, .29f, .38f));
            backButton.onClick.AddListener(() => RetreatForTests());
            nextButton = Button(panel.transform, "TransitionNextButton", "더 자세히",
                new Vector2(136f, -244f), new Vector2(116f, 48f),
                new Color(.16f, .42f, .60f));
            nextButton.onClick.AddListener(() => AdvanceForTests());
            openButton = Button(panel.transform, "OpenPreparedDioramaButton", "디오라마 열기",
                new Vector2(262f, -244f), new Vector2(110f, 48f),
                new Color(.20f, .48f, .34f));
            openButton.onClick.AddListener(() => OpenPreparedDioramaForTests());
        }

        private void UpdatePresentation()
        {
            if (!initialized || panel == null) return;
            var visible = flow.CurrentStage != 공간의미확대전환단계.Hidden
                          && flow.CurrentStage != 공간의미확대전환단계.LocalDiorama;
            panel.SetActive(visible);
            if (visible)
            {
                title.text = $"공간 의미 확대 · {StageCode(flow.CurrentStage)}\n{flow.CurrentDisplayName}";
                stableId.text = $"고유 식별자\n{flow.CurrentStableId}";
                boundary.text = flow.TruthBoundary;
                actionStatus.text = "상태 · " + LastActionStatus;
                backButton.interactable = flow.CanRetreat;
                nextButton.interactable = flow.CanAdvance;
                openButton.interactable = flow.CanOpenPreparedDiorama;
            }
            UpdateRuntimeLayer();
        }

        private void UpdateRuntimeLayer()
        {
            if (!UnityEngine.Application.isPlaying) return;
            var layer = flow.CurrentStage switch
            {
                공간의미확대전환단계.Region => 세계지도표현계층.Region,
                공간의미확대전환단계.AdministrativeArea =>
                    세계지도표현계층.AdministrativeArea,
                공간의미확대전환단계.StationArea => 세계지도표현계층.StationArea,
                공간의미확대전환단계.LocalDiorama => 세계지도표현계층.Diorama,
                _ => 세계지도표현계층.RegionalTerrain,
            };
            세계지도표현상태.SetObservationLayer(layer);
        }

        private static string StageCode(공간의미확대전환단계 stage)
            => stage switch
            {
                공간의미확대전환단계.Region => "Z3 권역",
                공간의미확대전환단계.AdministrativeArea => "Z4 행정동",
                공간의미확대전환단계.StationArea => "Z4.5 역세권",
                공간의미확대전환단계.LocalDiorama => "Z5 디오라마",
                _ => "대기",
            };

        private static Text Label(Transform parent, string name, Vector2 position,
            Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(Transform parent, string name, string value,
            Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            var button = item.GetComponent<Button>();
            var label = Label(item.transform, "Label", Vector2.zero, size, 14, Color.white);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = value;
            return button;
        }
    }
}
