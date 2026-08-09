using System;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public sealed class PublicWorldMapDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button detailButton;
        [SerializeField] private Button closeButton;
        private Action<PublicWorldMarker> navigate;

        public PublicWorldMarker SelectedMarker { get; private set; }
        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        public void Configure(GameObject root, Text title, Text body, Button detail, Button close)
        {
            panelRoot = root;
            titleText = title;
            bodyText = body;
            detailButton = detail;
            closeButton = close;
        }

        public void Bind(Action<PublicWorldMarker> navigateToDetail)
        {
            navigate = navigateToDetail;
            detailButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
            detailButton.onClick.AddListener(() => { if (SelectedMarker != null) navigate?.Invoke(SelectedMarker); });
            closeButton.onClick.AddListener(Close);
            Close();
        }

        public void Show(PublicWorldMarker marker)
        {
            SelectedMarker = marker ?? throw new ArgumentNullException(nameof(marker));
            titleText.text = marker.Title;
            var evidenceTime = marker.EvidenceAsOfUtc.HasValue
                ? marker.EvidenceAsOfUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "-";
            bodyText.text = $"{marker.CountryName} · {marker.LocationPrecisionCode}\n"
                + $"기준 시각: {evidenceTime}\n"
                + $"최신성: {marker.FreshnessCode}\n출처: {marker.SourceName}\n\n{marker.Summary}\n\n{marker.BoundaryNotice}";
            detailButton.interactable = !string.IsNullOrWhiteSpace(marker.DetailHref);
            panelRoot.SetActive(true);
        }

        public void Close()
        {
            SelectedMarker = null;
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
