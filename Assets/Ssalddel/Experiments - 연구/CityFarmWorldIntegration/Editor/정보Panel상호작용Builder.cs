using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 정보Panel상호작용Builder
    {
        public static 정보Panel상호작용Controller Attach(
            Transform canvas,
            RectTransform contentPanel,
            string panelLabel)
        {
            var root = new GameObject(panelLabel + "_정보Panel상호작용", typeof(RectTransform),
                typeof(정보Panel상호작용Controller));
            root.transform.SetParent(canvas, false);
            Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one);

            var controls = Container(root.transform, "펼침상태도구",
                new Vector2(contentPanel.anchorMax.x - .13f, contentPanel.anchorMax.y - .06f),
                new Vector2(contentPanel.anchorMax.x - .008f, contentPanel.anchorMax.y - .008f));
            var collapse = Button(controls, "접기Button", "접기",
                new Vector2(0f, 0f), new Vector2(.48f, 1f), new Color(.12f, .19f, .22f, .98f));
            var close = Button(controls, "닫기Button", "닫기",
                new Vector2(.52f, 0f), Vector2.one, new Color(.25f, .11f, .1f, .98f));

            var expandTab = Button(root.transform, "펼치기Tab", panelLabel + " 펼치기",
                new Vector2(contentPanel.anchorMax.x - .2f, contentPanel.anchorMax.y - .07f),
                new Vector2(contentPanel.anchorMax.x - .008f, contentPanel.anchorMax.y - .008f),
                new Color(.07f, .22f, .19f, .98f));
            var reopenTab = Button(root.transform, "다시열기Tab", panelLabel + " 다시 열기",
                new Vector2(contentPanel.anchorMin.x, contentPanel.anchorMin.y),
                new Vector2(Mathf.Min(contentPanel.anchorMin.x + .2f, .98f),
                    Mathf.Min(contentPanel.anchorMin.y + .065f, .98f)),
                new Color(.12f, .2f, .3f, .98f));

            var controller = root.GetComponent<정보Panel상호작용Controller>();
            controller.Configure(contentPanel.gameObject, controls.gameObject,
                expandTab.gameObject, reopenTab.gameObject,
                collapse, close, expandTab, reopenTab);
            return controller;
        }

        private static RectTransform Container(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            Stretch(rect, min, max);
            return rect;
        }

        private static Button Button(
            Transform parent, string name, string label, Vector2 min, Vector2 max, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            Stretch(rect, min, max);
            value.GetComponent<Image>().color = color;

            var labelObject = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(value.transform, false);
            Stretch((RectTransform)labelObject.transform, new Vector2(.04f, .06f), new Vector2(.96f, .94f));
            var text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return value.GetComponent<Button>();
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
