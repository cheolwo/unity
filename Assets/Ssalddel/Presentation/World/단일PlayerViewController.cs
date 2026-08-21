using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 공식 플레이 Scene의 상태와 상호작용 객체는 유지하면서,
    /// Game View에는 플레이어 한 명과 시점 전환 UI만 남기는 표현 정책입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 단일PlayerViewController : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private Canvas modeCanvas = null!;
        [SerializeField] private bool applyOnPlay = true;

        private int nextRefreshFrame;

        public 플레이어경관Controller Player => player;
        public Canvas ModeCanvas => modeCanvas;
        public bool ApplyOnPlay => applyOnPlay;
        public bool PresentationOnly => true;

        public void Configure(
            플레이어경관Controller playerController,
            Canvas perspectiveModeCanvas,
            bool enabledOnPlay = true)
        {
            player = playerController;
            modeCanvas = perspectiveModeCanvas;
            applyOnPlay = enabledOnPlay;
            ValidateWiring();
        }

        public void ValidateWiring()
        {
            if (player == null || modeCanvas == null
                || !player.transform.IsChildOf(transform)
                || !modeCanvas.transform.IsChildOf(transform))
                throw new InvalidOperationException("SinglePlayerViewWiringInvalid");
        }

        private void Awake()
        {
            // AddComponent 직후 Configure하는 Editor 조립 과정에서는 아직 참조가 비어 있다.
            // 저장 Scene의 누락 배선은 전용 검증과 시험에서 잡고, Play 진입 시에만 적용한다.
            if (applyOnPlay && player != null && modeCanvas != null) ApplyView();
        }

        private void LateUpdate()
        {
            if (!applyOnPlay || Time.frameCount < nextRefreshFrame) return;

            // 공간 스트리밍과 fixture가 Play 진입 뒤 표현물을 만들 수 있으므로
            // 저빈도 재검사로 새 Renderer/Canvas도 같은 정책에 편입한다.
            ApplyView();
            nextRefreshFrame = Time.frameCount + 30;
        }

        public void ApplyView()
        {
            ValidateWiring();
            var visualRoot = player.VisualRoot;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = renderer.transform == visualRoot
                                   || renderer.transform.IsChildOf(visualRoot);

            foreach (var terrain in GetComponentsInChildren<Terrain>(true))
                terrain.enabled = false;

            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(canvas == modeCanvas);
        }
    }
}
