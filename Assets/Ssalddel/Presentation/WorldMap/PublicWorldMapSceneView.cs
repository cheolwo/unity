using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    public sealed class PublicWorldMapSceneView : MonoBehaviour
    {
        [SerializeField] private Text statusText;
        [SerializeField] private Text metadataText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button refreshButton;

        public PublicWorldMapSceneStatus VisibleStatus { get; private set; } = PublicWorldMapSceneStatus.Idle;
        public string VisibleMessage => statusText == null ? string.Empty : statusText.text;

        public void Configure(Text status, Text metadata, Button retry, Button refresh)
        {
            statusText = status;
            metadataText = metadata;
            retryButton = retry;
            refreshButton = refresh;
        }

        public void Bind(Action retry, Action refresh)
        {
            retryButton.onClick.RemoveAllListeners();
            refreshButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => retry());
            refreshButton.onClick.AddListener(() => refresh());
        }

        public void Apply(PublicWorldMapSceneState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            VisibleStatus = state.Status;
            statusText.text = BuildMessage(state);
            metadataText.text = BuildMetadata(state);
            retryButton.gameObject.SetActive(state.Status == PublicWorldMapSceneStatus.InitialLoadError);
            refreshButton.gameObject.SetActive(state.Status == PublicWorldMapSceneStatus.Success
                || state.Status == PublicWorldMapSceneStatus.RefreshError);
        }

        private static string BuildMessage(PublicWorldMapSceneState state)
        {
            switch (state.Status)
            {
                case PublicWorldMapSceneStatus.Idle: return "공개 세계지도를 준비하고 있습니다.";
                case PublicWorldMapSceneStatus.Loading: return "공개 세계지도 정보를 불러오는 중입니다.";
                case PublicWorldMapSceneStatus.Success: return $"관측 정보 {state.MarkerCount}건";
                case PublicWorldMapSceneStatus.InitialLoadError: return "공개 세계지도 정보를 불러오지 못했습니다.";
                case PublicWorldMapSceneStatus.Refreshing: return $"기존 관측 정보 {state.MarkerCount}건을 유지하며 갱신 중입니다.";
                case PublicWorldMapSceneStatus.RefreshError: return $"관측 정보 {state.MarkerCount}건을 유지했습니다. 최신 정보 갱신에 실패했습니다.";
                default: return state.Status.ToString();
            }
        }

        private static string BuildMetadata(PublicWorldMapSceneState state)
        {
            var generated = state.GeneratedAtUtc.HasValue
                ? state.GeneratedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "-";
            var error = string.IsNullOrWhiteSpace(state.ErrorMessage) ? string.Empty : $"\n오류: {state.ErrorMessage}";
            return $"기준 시각: {generated}\nRevision: {state.Revision}{error}";
        }
    }
}
