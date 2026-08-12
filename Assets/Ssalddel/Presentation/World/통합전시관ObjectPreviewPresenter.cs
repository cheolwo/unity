using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 통합전시관ObjectPreviewActionCodes
    {
        public const string 감자수확상자 = "PreviewSeedbedObject0";
        public const string Hub입고Gate = "PreviewSeedbedObject1";
        public const string 음식픽업인계상자 = "PreviewSeedbedObject2";
    }

    [DisallowMultipleComponent]
    public sealed class 통합전시관ObjectPreviewPresenter : MonoBehaviour
    {
        [SerializeField] private 통합전시관ObjectVisualCatalog catalog = null!;
        [SerializeField] private Transform previewHost = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private Button[] objectButtons = Array.Empty<Button>();
        [SerializeField] private string initialObjectStableId = string.Empty;
        [SerializeField] private float rotationDegreesPerSecond = 18f;

        private GameObject? previewInstance;
        private string selectedObjectStableId = string.Empty;
        private bool listenersBound;

        public int Catalog개수 => catalog?.Entries.Count ?? 0;
        public string 선택ObjectStableId => selectedObjectStableId;
        public bool 운영Command제공여부 => false;
        public 통합전시관SeedbedObjectRoot? 현재ObjectRoot =>
            previewInstance?.GetComponent<통합전시관SeedbedObjectRoot>();

        public void Configure(
            통합전시관ObjectVisualCatalog visualCatalog,
            Transform host,
            Text title,
            Text detail,
            Text boundary,
            Button[] buttons,
            string initialStableId = "")
        {
            catalog = visualCatalog;
            previewHost = host;
            titleText = title;
            detailText = detail;
            boundaryText = boundary;
            objectButtons = buttons ?? Array.Empty<Button>();
            initialObjectStableId = initialStableId ?? string.Empty;
        }

        private void Start() => Initialize();

        private void Update()
        {
            if (UnityEngine.Application.isPlaying && previewHost != null)
                previewHost.Rotate(Vector3.up, rotationDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        private void OnDestroy()
        {
            if (!listenersBound || objectButtons == null) return;
            foreach (var button in objectButtons.Where(value => value != null))
                button.onClick.RemoveAllListeners();
        }

        public void Initialize()
        {
            ValidateWiring();
            BindListeners();
            SelectObject(string.IsNullOrWhiteSpace(initialObjectStableId)
                ? catalog.Entries[0].ObjectStableId
                : initialObjectStableId);
        }

        public void Execute(string actionCode)
        {
            switch (actionCode)
            {
                case 통합전시관ObjectPreviewActionCodes.감자수확상자:
                    SelectVisible(0);
                    break;
                case 통합전시관ObjectPreviewActionCodes.Hub입고Gate:
                    SelectVisible(1);
                    break;
                case 통합전시관ObjectPreviewActionCodes.음식픽업인계상자:
                    SelectVisible(2);
                    break;
                default:
                    SelectObject(actionCode);
                    break;
            }
        }

        public void SelectObject(string objectStableId)
        {
            var entry = catalog.Resolve(objectStableId);
            for (var index = previewHost.childCount - 1; index >= 0; index--)
            {
                var previous = previewHost.GetChild(index).gameObject;
                if (UnityEngine.Application.isPlaying)
                {
                    previous.SetActive(false);
                    Destroy(previous);
                }
                else DestroyImmediate(previous);
            }

            previewHost.localRotation = Quaternion.identity;
            previewInstance = Instantiate(entry.Prefab, previewHost);
            previewInstance.name = "ObjectPreview_" + entry.DisplayName.Replace(" ", string.Empty);
            previewInstance.transform.localPosition = Vector3.zero;
            previewInstance.transform.localRotation = Quaternion.identity;
            previewInstance.transform.localScale = Vector3.one;
            foreach (var collider in previewInstance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            selectedObjectStableId = entry.ObjectStableId;
            Render(entry);
        }

        public void ValidateWiring()
        {
            var missing = new[]
            {
                catalog == null ? "Catalog" : string.Empty,
                previewHost == null ? "PreviewHost" : string.Empty,
                titleText == null ? "Title" : string.Empty,
                detailText == null ? "Detail" : string.Empty,
                boundaryText == null ? "Boundary" : string.Empty,
                objectButtons == null ? "Buttons" : string.Empty,
                objectButtons != null && catalog != null
                    && objectButtons.Length != catalog.Entries.Count ? "ButtonCount" : string.Empty,
                objectButtons != null && objectButtons.Any(value => value == null) ? "ButtonItem" : string.Empty,
            }.Where(value => !string.IsNullOrEmpty(value)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("IntegratedExhibitionObjectPreviewWiringMissing:"
                    + string.Join(",", missing));
            catalog.Validate();
        }

        private void SelectVisible(int index)
        {
            if (index < 0 || index >= catalog.Entries.Count) return;
            SelectObject(catalog.Entries[index].ObjectStableId);
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            for (var index = 0; index < objectButtons.Length; index++)
            {
                var captured = index;
                objectButtons[index].onClick.AddListener(() => SelectVisible(captured));
            }
            listenersBound = true;
        }

        private void Render(통합전시관ObjectVisualCatalogEntry entry)
        {
            titleText.text = "통합 Object 모판 · O5 Preview\n" + entry.DisplayName;
            detailText.text = entry.ObjectStableId + "\n"
                + "Visual  " + entry.VisualVariantKey + "\n"
                + "Placement  " + entry.PlacementProfileKey + "\n"
                + "Footprint  " + entry.Footprint.x.ToString("0.0") + " × "
                + entry.Footprint.y.ToString("0.0") + "m\n"
                + "Bounds  " + entry.MeasuredBoundsSize.x.ToString("0.0") + " × "
                + entry.MeasuredBoundsSize.y.ToString("0.0") + " × "
                + entry.MeasuredBoundsSize.z.ToString("0.0") + "m\n"
                + "Sockets\n· " + string.Join("\n· ", entry.RequiredSocketCodes);
            boundaryText.text = "Object 단독 Preview\n"
                + "Scene 업무 상태·재고·배차·주문 권위를 소유하지 않습니다.\n"
                + "현재 O5 Preview 검증 대상 · O6 Scene 배치는 별도 승격입니다.";
            for (var index = 0; index < objectButtons.Length; index++)
            {
                var item = catalog.Entries[index];
                objectButtons[index].GetComponentInChildren<Text>().text = item.DisplayName;
                objectButtons[index].image.color = item.ObjectStableId == selectedObjectStableId
                    ? new Color(.92f, .57f, .16f, 1f)
                    : new Color(.09f, .17f, .18f, .96f);
            }
            foreach (var text in GetComponentsInChildren<Text>(true)) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
    }
}
