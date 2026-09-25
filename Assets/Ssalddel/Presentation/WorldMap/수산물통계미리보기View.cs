#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable]
    public sealed class 수산물통계PreviewSnapshot
    {
        public string schemaVersion, country, sourceId, month, batchId, collectedAtUtc, sourceUrl;
        public bool localReviewOnly, distributionApproved, temperatureAvailable;
        public 수산물통계PreviewItem[] items;

        public void Validate()
        {
            if (schemaVersion != "marine-trade-preview.v1" || country != "KR" || sourceId != "kcs"
                || !localReviewOnly || distributionApproved || temperatureAvailable
                || sourceUrl != "https://www.data.go.kr/data/15100475/openapi.do"
                || !DateTime.TryParseExact(month, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !DateTimeOffset.TryParse(collectedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || string.IsNullOrEmpty(batchId) || items == null || items.Length != 3)
                throw new InvalidOperationException("MarinePreviewContractInvalid");
            foreach (var item in items)
                if (item == null || !new[] { "0302140000", "0303540000", "0303630000" }.Contains(item.hsCode)
                    || item.partner != (item.hsCode == "0303630000" ? "RU" : "NO")
                    || string.IsNullOrWhiteSpace(item.name) || !Valid(item.importKg) || !Valid(item.importUsd)
                    || !Hash(item.weightRevision) || !Hash(item.amountRevision))
                    throw new InvalidOperationException("MarinePreviewItemInvalid");
            if (items.Select(x => x.hsCode).Distinct().Count() != 3)
                throw new InvalidOperationException("MarinePreviewDuplicateItem");
        }

        private static bool Valid(double number) => !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0;
        private static bool Hash(string value) => value != null && value.Length == 64 && value.All(x => "0123456789abcdef".Contains(x));
    }

    [Serializable]
    public sealed class 수산물통계PreviewItem
    {
        public string hsCode, name, partner, weightRevision, amountRevision;
        public double importKg, importUsd;
        public string DisplayName => hsCode == "0302140000" ? "연어류 · 신선/냉장"
            : hsCode == "0303540000" ? "고등어류 · 냉동" : "대구류 · 냉동";
    }

    /// <summary>Editor 로컬 검토 전용. DB 검증 사본만 메모리에 받고 빌드·Save·Replay에 포함하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class 수산물통계미리보기View : MonoBehaviour
    {
        private 수산물통계PreviewSnapshot snapshot;
        private 세계지구본View globe;
        private Transform globeRoot;
        private Camera globeCamera;
        private Canvas canvas;
        private GameObject panel, marker;
        private Text body, heading;
        private int selected;
        public bool IsExpanded => panel != null && panel.activeSelf;
        public int ItemCount => snapshot?.items?.Length ?? 0;
        public string SelectedHsCode => snapshot?.items[selected].hsCode;

        public void Load(string json, 세계지구본View target)
        {
            var parsed = JsonUtility.FromJson<수산물통계PreviewSnapshot>(json);
            if (parsed == null) throw new InvalidOperationException("MarinePreviewMissing");
            parsed.Validate();
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.InitializeForTests();
            var root = target.transform.Find(세계지구본View.GlobeVisualRootName);
            var camera = target.GetComponentsInChildren<Camera>(true).First(x => x.name == 세계지구본View.GlobeCameraName);
            snapshot = parsed; globe = target; globeRoot = root; globeCamera = camera; selected = 0;
            if (canvas == null) Build();
            Select(0);
            SetExpanded(false);
        }

        public void SetExpanded(bool value) { if (panel != null) panel.SetActive(value); }
        public void Select(int index)
        {
            if (snapshot == null || index < 0 || index >= snapshot.items.Length) return;
            selected = index;
            var item = snapshot.items[index];
            heading.text = item.DisplayName;
            body.text = $"{(item.partner == "NO" ? "노르웨이" : "러시아")} → 대한민국\n"
                + $"{snapshot.month[..4]}년 {snapshot.month[4..]}월 · 월간 수입 실적\n\n"
                + $"수입 중량\n{item.importKg.ToString("N0", CultureInfo.InvariantCulture)} kg\n\n"
                + $"수입 금액 · CIF\n{item.importUsd.ToString("N0", CultureInfo.InvariantCulture)} USD\n\n"
                + $"HSK {item.hsCode}\n원천 품목: {item.name}\n\n"
                + "출처: 관세청 · 공공데이터포털\n"
                + $"확인: {DateTimeOffset.Parse(snapshot.collectedAtUtc, CultureInfo.InvariantCulture):yyyy-MM-dd} · 로컬 DB 사본\n\n"
                + "국가 통계이며 실제 어군·어획지·항로가 아닙니다.\n수온: 미확보 · 인증 확인 필요";
        }

        private void LateUpdate()
        {
            if (canvas == null || globe == null) return;
            canvas.enabled = globe.IsGlobeVisible && globeCamera.enabled;
            var point = 세계지구구면좌표.FromLatitudeLongitude(36.3, 127.8);
            var normal = globeRoot.TransformDirection(new Vector3(-(float)point.X, (float)point.Y, (float)point.Z));
            var world = globeRoot.position + normal * 18.15f;
            var screen = globeCamera.WorldToScreenPoint(world);
            marker.SetActive(canvas.enabled && screen.z > 0 && Vector3.Dot(normal, globeCamera.transform.position - world) > 0);
            var rect = canvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local))
                marker.GetComponent<RectTransform>().anchoredPosition = local;
        }

        private void Build()
        {
            var root = new GameObject("수산물 통계 로컬 미리보기 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.hideFlags = HideFlags.DontSave;
            // 기존 지구본 Canvas처럼 독립 루트에 둔다. PlayerView 하위 Canvas 자동 비활성화에서 분리한다.
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 950;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            Button(root.transform, "통계 열기", "수산물 통계  ·  2026.06", new Vector2(22, -88), new Vector2(270, 46), () => SetExpanded(!IsExpanded));
            panel = Box(root.transform, "통계 카드", new Vector2(22, -148), new Vector2(450, 676), new Color(.025f, .075f, .12f, .97f));
            Label(panel.transform, "범위", "대한민국 수산물 수입  /  로컬 검토", new Vector2(22, -16), new Vector2(400, 30), 19, new Color(.4f, .86f, .85f));
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                Button(panel.transform, "품목" + i, new[] { "연어류", "고등어류", "대구류" }[i],
                    new Vector2(22 + 136 * i, -58), new Vector2(126, 42), () => Select(index));
            }
            heading = Label(panel.transform, "품목 제목", "", new Vector2(22, -118), new Vector2(408, 38), 26, Color.white);
            body = Label(panel.transform, "품목 근거", "", new Vector2(22, -164), new Vector2(404, 447), 18, new Color(.84f, .91f, .95f));
            Button(panel.transform, "접기", "접기", new Vector2(22, -622), new Vector2(126, 38), () => SetExpanded(false));
            marker = Button(root.transform, "국가 통계 표식", "● 대한민국\n수산물 통계", Vector2.zero, new Vector2(164, 61), () => SetExpanded(!IsExpanded)).gameObject;
            var markerRect = marker.GetComponent<RectTransform>(); markerRect.anchorMin = markerRect.anchorMax = new Vector2(.5f, .5f); markerRect.pivot = new Vector2(.5f, .5f);
        }

        private static GameObject Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size; item.GetComponent<Image>().color = color; return item;
        }
        private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = item.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
            text.color = color; text.text = value; text.supportRichText = false; text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Overflow; return text;
        }
        private static Button Button(Transform parent, string name, string value, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var item = Box(parent, name, position, size, new Color(.07f, .30f, .37f, .98f));
            var button = item.AddComponent<Button>(); button.targetGraphic = item.GetComponent<Image>(); button.onClick.AddListener(action);
            var text = Label(item.transform, "문구", value, new Vector2(8, -3), size - new Vector2(16, 6), 18, Color.white);
            text.alignment = TextAnchor.MiddleCenter; return button;
        }
        private void OnDestroy()
        {
            snapshot = null;
            if (canvas != null) { if (UnityEngine.Application.isPlaying) Destroy(canvas.gameObject); else DestroyImmediate(canvas.gameObject); }
        }
    }
}
#endif
