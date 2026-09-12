using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 공식 플레이 Scene의 상태와 상호작용 객체는 유지하면서,
    /// Game View에는 플레이어 한 명, 승인된 환경 Root와 시점 전환 UI만 남기는 표현 정책입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 단일PlayerViewController : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private Canvas modeCanvas = null!;
        [SerializeField] private Transform[] approvedEnvironmentRoots =
            Array.Empty<Transform>();
        [SerializeField] private bool applyOnPlay = true;

        private int nextRefreshFrame;
        private readonly Dictionary<Renderer, bool> suppressedRenderers = new();
        private readonly Dictionary<Transform, object> runtimeEnvironmentOwners = new();
        private readonly Dictionary<Transform, Dictionary<Renderer, Transform[]>> runtimeRendererPaths = new();
        private readonly Dictionary<Transform, Transform[]> runtimeRootPaths = new();

        public 플레이어경관Controller Player => player;
        public Canvas ModeCanvas => modeCanvas;
        public Transform[] ApprovedEnvironmentRoots => approvedEnvironmentRoots;
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

        public void SetApprovedEnvironmentRoots(params Transform[] roots)
        {
            approvedEnvironmentRoots = roots?
                .Where(value => value != null)
                .Distinct()
                .ToArray() ?? Array.Empty<Transform>();
            ValidateWiring();
        }

        public void ValidateWiring()
        {
            if (player == null || modeCanvas == null
                || !player.transform.IsChildOf(transform)
                || !modeCanvas.transform.IsChildOf(transform))
                throw new InvalidOperationException("SinglePlayerViewWiringInvalid");
            if (approvedEnvironmentRoots.Any(value => value == null
                || !value.IsChildOf(transform)))
                throw new InvalidOperationException(
                    "SinglePlayerViewEnvironmentRootInvalid");
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
            foreach (var destroyed in suppressedRenderers.Keys.Where(value => value == null).ToArray())
                suppressedRenderers.Remove(destroyed);
            foreach (var destroyed in runtimeEnvironmentOwners.Keys.Where(value => value == null).ToArray())
            {
                runtimeEnvironmentOwners.Remove(destroyed);
                runtimeRendererPaths.Remove(destroyed);
                runtimeRootPaths.Remove(destroyed);
            }
            var visualRoot = player.VisualRoot;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var isPlayerVisual = renderer.transform == visualRoot
                                     || renderer.transform.IsChildOf(visualRoot);
                // 승인된 환경은 표시할 수 있다는 뜻이지, 장면 제작자가 경계선·
                // 소켓처럼 의도적으로 끈 Renderer까지 다시 켜라는 뜻은 아니다.
                // 따라서 승인 밖의 표현만 끄고 승인 안의 기존 활성 상태는 보존한다.
                if (!isPlayerVisual && !IsApprovedEnvironment(renderer.transform))
                {
                    if (renderer.enabled && !suppressedRenderers.ContainsKey(renderer))
                        suppressedRenderers.Add(renderer, true);
                    renderer.enabled = false;
                }
            }

            foreach (var terrain in GetComponentsInChildren<Terrain>(true))
                terrain.enabled = IsApprovedEnvironment(terrain.transform);

            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(canvas == modeCanvas);
        }

        private bool IsApprovedEnvironment(Transform candidate)
        {
            if (runtimeEnvironmentOwners.Keys.Any(root => root != null
                    && MatchesOwnedPath(root, runtimeRootPaths[root])
                    && (candidate == root || candidate.IsChildOf(root))))
                return true;
            if (approvedEnvironmentRoots.Any(root =>
                    candidate == root || candidate.IsChildOf(root)))
                return true;

            // LH가 Play 중 만든 Cell·실외/실내 배치도 승인된 세계 표현이다.
            // 저장 Scene에 아직 존재하지 않는 동적 자식이므로 고정 Root 목록만으로
            // 판정하면 다음 저빈도 ApplyView에서 Renderer가 다시 꺼진다.
            return candidate.GetComponentInParent<공간LHStreamingEngine>(true)
                   != null;
        }

        public bool TryRegisterRuntimeEnvironment(
            Transform root, object owner, out string diagnostic)
        {
            diagnostic = "SinglePlayerRuntimeEnvironmentInvalid";
            if (root == null || root == transform || owner == null || !root.IsChildOf(transform))
                return false;
            if (runtimeEnvironmentOwners.TryGetValue(root, out var currentOwner))
            {
                if (!ReferenceEquals(currentOwner, owner))
                {
                    diagnostic = "SinglePlayerRuntimeEnvironmentOwnedByOther";
                    return false;
                }
                diagnostic = "SinglePlayerRuntimeEnvironmentAlreadyOwned";
                return true;
            }

            runtimeEnvironmentOwners.Add(root, owner);
            runtimeRootPaths.Add(root, CaptureOwnedPath(root));
            var rendererPaths = root.GetComponentsInChildren<Renderer>(true)
                .ToDictionary(value => value, value => CaptureOwnedPath(value.transform));
            runtimeRendererPaths.Add(root, rendererPaths);
            foreach (var renderer in rendererPaths.Keys)
            {
                // 이 정책이 실제 true→false로 바꾼 항목만 복원한다.
                if (!suppressedRenderers.Remove(renderer)) continue;
                renderer.enabled = true;
            }
            diagnostic = "SinglePlayerRuntimeEnvironmentRegistered";
            return true;
        }

        public bool UnregisterRuntimeEnvironment(Transform root, object owner)
        {
            if (root == null || owner == null
                || !runtimeEnvironmentOwners.TryGetValue(root, out var currentOwner)
                || !ReferenceEquals(currentOwner, owner)) return false;
            runtimeEnvironmentOwners.Remove(root);
            var rootStillOwned = MatchesOwnedPath(root, runtimeRootPaths[root]);
            var rendererPaths = runtimeRendererPaths[root];
            runtimeRootPaths.Remove(root);
            runtimeRendererPaths.Remove(root);
            // 등록 당시 객체만 반환한다. 재부모화·새 외부 자식은 해제 쓰기 대상이 아니다.
            foreach (var entry in rendererPaths)
            {
                var renderer = entry.Key;
                if (!rootStillOwned || renderer == null
                    || !MatchesOwnedPath(renderer.transform, entry.Value)) continue;
                if (IsApprovedEnvironment(renderer.transform)) continue;
                if (renderer.enabled && !suppressedRenderers.ContainsKey(renderer))
                    suppressedRenderers.Add(renderer, true);
                renderer.enabled = false;
            }
            return true;
        }

        private Transform[] CaptureOwnedPath(Transform value)
        {
            var path = new List<Transform>();
            for (var current = value; current != null; current = current.parent)
            {
                path.Add(current);
                if (current == transform) return path.ToArray();
            }
            return Array.Empty<Transform>();
        }

        private bool MatchesOwnedPath(Transform value, Transform[] path)
        {
            if (value == null || path.Length == 0) return false;
            var current = value;
            foreach (var expected in path)
            {
                if (current == null || current != expected) return false;
                if (current == transform) return expected == path[path.Length - 1];
                current = current.parent;
            }
            return false;
        }
    }
}
