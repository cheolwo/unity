using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공간StreamingTreeDiagnosticPresenter : MonoBehaviour
    {
        [SerializeField] private 공간TileStreamingController tileStreaming = null!;
        [SerializeField] private 공간시야ObjectStreamingController objectStreaming = null!;
        [SerializeField] private 공간안전이동Gate movementGate = null!;
        [SerializeField] private GameObject panelRoot = null!;
        [SerializeField] private Text treeText = null!;
        [SerializeField] private Text toggleText = null!;
        [SerializeField] private bool presentationOnly = true;
        private float nextRefreshTime;

        public bool PresentationOnly => presentationOnly;
        public string TreeTextContent => treeText == null ? string.Empty : treeText.text;
        public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

        public void Configure(
            공간TileStreamingController tileController,
            공간시야ObjectStreamingController objectController,
            공간안전이동Gate gate,
            GameObject panel,
            Text label,
            Text buttonLabel)
        {
            tileStreaming = tileController;
            objectStreaming = objectController;
            movementGate = gate;
            panelRoot = panel;
            treeText = label;
            toggleText = buttonLabel;
            presentationOnly = true;
            RefreshNow();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) Toggle();
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + .25f;
            RefreshNow();
        }

        public void Toggle()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (toggleText != null)
                toggleText.text = panelRoot.activeSelf ? "진단 트리 닫기  F8" : "진단 트리 열기  F8";
        }

        public void RefreshNow()
        {
            if (treeText == null || tileStreaming == null || objectStreaming == null) return;
            var builder = new StringBuilder(640);
            builder.AppendLine("사용자 이동·시야 기반 동적 월드");
            builder.AppendLine("├─ 수평 타일 창  " + TileKey());
            builder.AppendLine("│  ├─ 선행 중심  " + tileStreaming.PreparedCenterTileKey);
            builder.AppendLine("│  ├─ 상세  " + tileStreaming.DetailTileCount + " / "
                               + tileStreaming.DetailWindowCapacity);
            builder.AppendLine("│  ├─ 활성  " + tileStreaming.ActiveTileCount + " / "
                               + tileStreaming.ActiveWindowCapacity);
            builder.AppendLine("│  ├─ 준비·추적  " + tileStreaming.PreparedTileCount + " / "
                               + tileStreaming.PrefetchWindowCapacity);
            builder.AppendLine("│  ├─ 동시 로드 제한  "
                               + tileStreaming.MaxConcurrentTileLoads);
            builder.AppendLine("│  └─ 범위 밖  " + tileStreaming.OutsideCoverageCount);
            builder.AppendLine("├─ 타일 수직 처리");
            builder.AppendLine("│  ├─ 공간 Manifest 조회");
            builder.AppendLine("│  ├─ DEM·배치 마스크  자료 대기 " + tileStreaming.WaitingTileCount);
            builder.AppendLine("│  ├─ 안전 지면 판정  " + Safety());
            builder.AppendLine("│  └─ WorldTick / 활동 Revision  "
                               + tileStreaming.ObservedWorldTick + " / "
                               + tileStreaming.ObservedActivityRevision);
            builder.AppendLine("├─ 카메라 시야  " + objectStreaming.ActiveCameraName);
            builder.AppendLine("│  ├─ 실제 절두체 안  " + objectStreaming.ActualVisibleCount);
            builder.AppendLine("│  └─ 이동 예측·여백  " + objectStreaming.PredictedVisibleCount);
            builder.AppendLine("└─ 건물 표현 승격  (시나리오·PresentationOnly)");
            builder.AppendLine("   ├─ 선언  " + objectStreaming.DeclaredCount);
            builder.AppendLine("   ├─ 프록시  " + objectStreaming.ProxyActiveCount);
            builder.AppendLine("   ├─ Synty 상세  " + objectStreaming.DetailActiveCount);
            builder.AppendLine("   ├─ 화면 밖 캐시  " + objectStreaming.HiddenCachedCount);
            builder.Append("   └─ 실패  " + objectStreaming.FailedCount);
            treeText.text = builder.ToString();
        }

        private string TileKey()
            => tileStreaming.IsInitialized
                ? "kr5186:l2:" + tileStreaming.CurrentCenterX + ":" + tileStreaming.CurrentCenterY
                : "초기화 중";

        private string Safety()
        {
            if (movementGate == null) return "연결 안 됨";
            if (movementGate.LastProbeTileKey.Length == 0) return "이동 전";
            return movementGate.LastMoveAllowed
                ? "통과 · " + movementGate.LastProbeTileKey
                : "안전 경계 대기 · " + movementGate.LastProbeTileKey;
        }
    }
}
