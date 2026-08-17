using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    internal sealed class WI공간모판UiBindings
    {
        internal WI공간모판UiBindings(
            Text modeText,
            Text titleText,
            Text summaryText,
            Text detailText,
            Text lineageText,
            Text boundaryText,
            Button overviewButton,
            Button sheetButton,
            Button[] seedbedButtons,
            Button[] spaceButtons,
            Button[] candidateButtons)
        {
            ModeText = modeText;
            TitleText = titleText;
            SummaryText = summaryText;
            DetailText = detailText;
            LineageText = lineageText;
            BoundaryText = boundaryText;
            OverviewButton = overviewButton;
            SheetButton = sheetButton;
            SeedbedButtons = seedbedButtons;
            SpaceButtons = spaceButtons;
            CandidateButtons = candidateButtons;
        }

        internal Text ModeText { get; }
        internal Text TitleText { get; }
        internal Text SummaryText { get; }
        internal Text DetailText { get; }
        internal Text LineageText { get; }
        internal Text BoundaryText { get; }
        internal Button OverviewButton { get; }
        internal Button SheetButton { get; }
        internal Button[] SeedbedButtons { get; }
        internal Button[] SpaceButtons { get; }
        internal Button[] CandidateButtons { get; }
    }

    internal static class WI공간모판UiFactory
    {
        internal static WI공간모판UiBindings Build(
            Transform parent,
            Camera uiCamera,
            Color[] seedbedColors)
        {
            var canvasObject = new GameObject("검토UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;

            var panel = Panel(canvasObject.transform, "정보Panel", new Vector2(0f, 0f),
                new Vector2(.34f, 1f), new Color(.035f, .055f, .06f, .95f));
            var mode = Text(panel, "Mode", "증거 E4 · 공간 계층 H1", new Vector2(20f, -18f),
                new Vector2(500f, 34f), 17, new Color(.42f, .82f, 1f), FontStyle.Bold);
            var title = Text(panel, "Title", string.Empty, new Vector2(20f, -55f),
                new Vector2(500f, 64f), 25, Color.white, FontStyle.Bold);
            var summary = Text(panel, "Summary", string.Empty, new Vector2(20f, -122f),
                new Vector2(500f, 82f), 15, new Color(.86f, .9f, .88f), FontStyle.Normal);

            var overviewButton = Button(panel, "전체개요", "전체 개요", new Vector2(20f, -210f),
                new Vector2(150f, 36f), new Color(.13f, .45f, .56f));
            var sheetButton = Button(panel, "후보비교", "선택 모판 후보 비교", new Vector2(180f, -210f),
                new Vector2(220f, 36f), new Color(.38f, .31f, .58f));

            var seedbedButtons = new Button[5];
            for (var index = 0; index < seedbedButtons.Length; index++)
                seedbedButtons[index] = Button(panel, $"모판{index + 1:00}", $"모판 {index + 1}",
                    new Vector2(20f, -255f - index * 39f), new Vector2(380f, 34f),
                    seedbedColors[index] * .75f);

            var detail = Text(panel, "Detail", string.Empty, new Vector2(20f, -460f),
                new Vector2(500f, 170f), 14, new Color(.94f, .95f, .90f), FontStyle.Normal);
            var lineage = Text(panel, "Lineage", string.Empty, new Vector2(20f, -635f),
                new Vector2(500f, 70f), 13, new Color(.65f, .82f, .86f), FontStyle.Normal);

            var spaceButtons = new Button[3];
            for (var index = 0; index < spaceButtons.Length; index++)
                spaceButtons[index] = Button(panel, $"공간{index + 1:00}", $"공간 {index + 1}",
                    new Vector2(20f + index * 130f, -710f), new Vector2(120f, 32f),
                    new Color(.28f, .38f, .30f));

            var candidateButtons = new Button[9];
            for (var index = 0; index < candidateButtons.Length; index++)
            {
                var row = index / 3;
                var column = index % 3;
                candidateButtons[index] = Button(panel, $"후보{index + 1:00}", $"후보 {index + 1}",
                    new Vector2(20f + column * 130f, -750f - row * 34f),
                    new Vector2(120f, 28f), new Color(.34f, .25f, .17f));
            }

            var boundaryPanel = Panel(canvasObject.transform, "경계고지Panel",
                new Vector2(.35f, 0f), new Vector2(1f, .10f), new Color(.04f, .06f, .07f, .9f));
            var boundary = AnchoredText(boundaryPanel, "Boundary", string.Empty, 15,
                new Color(1f, .84f, .36f), TextAnchor.MiddleCenter);

            return new WI공간모판UiBindings(mode, title, summary, detail, lineage, boundary,
                overviewButton, sheetButton, seedbedButtons, spaceButtons, candidateButtons);
        }

        private static Transform Panel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            item.GetComponent<Image>().color = color;
            return item.transform;
        }

        private static Text Text(
            Transform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Color color,
            FontStyle style)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static Text AnchoredText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            var text = Text(parent, name, value, Vector2.zero, Vector2.zero,
                fontSize, color, FontStyle.Bold);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(16f, 6f);
            rect.offsetMax = new Vector2(-16f, -6f);
            text.alignment = alignment;
            return text;
        }

        private static Button Button(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            var text = AnchoredText(item.transform, "Label", label, 13, Color.white,
                TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            return item.GetComponent<Button>();
        }
    }
}
