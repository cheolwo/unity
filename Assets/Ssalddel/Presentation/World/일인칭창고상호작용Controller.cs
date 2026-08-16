using System;
using System.Linq;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버 재고와 연결된 Pallet의 시선 선택 범위와 수량 표현만 담당한다.
    /// Collider와 상자 표시는 업무 상태를 만들지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 대관령L2창고상호작용TargetView : MonoBehaviour
    {
        [SerializeField] private string buildingStableId =
            대관령L2창고아이템Codes.BuildingStableId;
        [SerializeField] private string containerStableId =
            대관령L2창고아이템Codes.ContainerStableId;
        [SerializeField] private string itemStackStableId =
            대관령L2창고아이템Codes.ItemStackStableId;
        [SerializeField] private string koreanName = "대관령 감자 상자 Pallet";
        [SerializeField] private Collider interactionCollider = null!;
        [SerializeField] private GameObject focusHighlight = null!;
        [SerializeField] private GameObject[] quantityVisuals = Array.Empty<GameObject>();
        [SerializeField] private bool presentationOnly = true;

        public string BuildingStableId => buildingStableId;
        public string ContainerStableId => containerStableId;
        public string ItemStackStableId => itemStackStableId;
        public string KoreanName => koreanName;
        public Collider InteractionCollider => interactionCollider;
        public int VisibleQuantity { get; private set; }
        public bool PresentationOnly => presentationOnly;

        public void Configure(Collider collider, GameObject highlight,
            GameObject[] visuals)
        {
            interactionCollider = collider;
            focusHighlight = highlight;
            quantityVisuals = visuals ?? Array.Empty<GameObject>();
            SetFocused(false);
        }

        public bool ValidateWiring()
            => buildingStableId == 대관령L2창고아이템Codes.BuildingStableId
               && containerStableId == 대관령L2창고아이템Codes.ContainerStableId
               && itemStackStableId == 대관령L2창고아이템Codes.ItemStackStableId
               && interactionCollider != null
               && interactionCollider.transform.IsChildOf(transform)
               && focusHighlight != null
               && focusHighlight.transform.IsChildOf(transform)
               && quantityVisuals != null
               && quantityVisuals.Length > 0
               && quantityVisuals.All(value => value != null
                   && value.transform.IsChildOf(transform))
               && presentationOnly;

        public bool Owns(Collider value)
            => value != null
               && (value == interactionCollider
                   || value.transform.IsChildOf(interactionCollider.transform));

        public void ApplySnapshot(대관령L2창고InventorySnapshot snapshot)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException(
                    "DaegwallyeongWarehouseTargetWiringInvalid");
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.Validate();
            var stack = snapshot.RequiredItemStack(itemStackStableId);
            var visible = Mathf.Clamp(
                Mathf.CeilToInt((float)stack.Quantity), 0, quantityVisuals.Length);
            for (var index = 0; index < quantityVisuals.Length; index++)
                quantityVisuals[index].SetActive(index < visible);
            VisibleQuantity = visible;
        }

        public void SetFocused(bool focused)
        {
            if (focusHighlight != null) focusHighlight.SetActive(focused);
        }
    }

    /// <summary>
    /// 1인칭 시선으로 Pallet을 선택하고 E Preview, Enter Confirm을 연결한다.
    /// 실제 획득은 Presenter가 서버 Confirm 뒤 원장을 다시 조회한 경우에만 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 일인칭창고상호작용Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private Camera firstPersonCamera = null!;
        [SerializeField] private 대관령L2창고아이템Presenter presenter = null!;
        [SerializeField] private 대관령L2창고상호작용TargetView target = null!;
        [SerializeField, Min(.5f)] private float maximumDistance = 3.2f;
        [SerializeField] private bool presentationOnly = true;

        private bool subscribed;
        private bool focused;
        private string lastError = string.Empty;
#if UNITY_EDITOR
        private string editorScanDiagnostic = "초기화 대기";
        private float nextEditorDiagnosticAt;
#endif

        public bool IsFocused => focused;
        public string LastError => lastError;
        public float MaximumDistance => maximumDistance;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            플레이어경관Controller playerController,
            Camera camera,
            대관령L2창고아이템Presenter inventoryPresenter,
            대관령L2창고상호작용TargetView interactionTarget,
            float distance = 3.2f)
        {
            Unsubscribe();
            player = playerController;
            firstPersonCamera = camera;
            presenter = inventoryPresenter;
            target = interactionTarget;
            maximumDistance = Mathf.Max(.5f, distance);
            Subscribe();
        }

        public bool ValidateWiring()
            => player != null
               && firstPersonCamera != null
               && firstPersonCamera == player.FirstPersonCamera
               && presenter != null
               && presenter.ValidateWiring()
               && target != null
               && target.ValidateWiring()
               && maximumDistance >= .5f
               && presentationOnly;

        public static bool CanScan(플레이어시점Mode mode,
            bool cameraTransitioning, bool presenterReady)
            => mode == 플레이어시점Mode.FirstPerson
               && !cameraTransitioning
               && presenterReady;

        public bool TryResolveTarget(Ray ray)
        {
            if (!ValidateWiring()) return false;
            if (Physics.Raycast(ray, out var hit, maximumDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && target.Owns(hit.collider))
            {
#if UNITY_EDITOR
                editorScanDiagnostic = "직접 시선 적중";
#endif
                return true;
            }

            // 작은 Pallet 앞에 장식용 Synty Collider가 겹쳐도, 실제 대상 면이
            // 가까운 중앙 시야 안에 있으면 선택을 보정한다. 거리와 각도를 함께
            // 제한하므로 벽 너머나 화면 밖의 대상을 획득하지는 않는다.
            var bounds = target.InteractionCollider.bounds;
            var closest = bounds.ClosestPoint(ray.origin);
            var distance = Vector3.Distance(ray.origin, closest);
            if (distance > maximumDistance)
            {
#if UNITY_EDITOR
                editorScanDiagnostic = $"대상 거리 {distance:0.00}m / {maximumDistance:0.00}m";
#endif
                return false;
            }
            var direction = bounds.center - ray.origin;
            var angle = direction.sqrMagnitude > .001f
                ? Vector3.Angle(ray.direction, direction)
                : 180f;
#if UNITY_EDITOR
            editorScanDiagnostic = $"대상 거리 {distance:0.00}m · 시야각 {angle:0.0}° / 24°";
#endif
            return angle <= 24f;
        }

        public async Task<bool> PreviewAsync()
        {
            if (!focused || !presenter.IsReady || presenter.Preview != null) return false;
            lastError = string.Empty;
            try
            {
                await presenter.PreviewOneAsync();
                return presenter.Preview?.CanConfirm == true;
            }
            catch (Exception error)
            {
                lastError = error.Message;
                return false;
            }
        }

        public async Task<bool> ConfirmAsync()
        {
            if (!focused || !presenter.IsReady
                || presenter.Preview?.CanConfirm != true) return false;
            lastError = string.Empty;
            try
            {
                await presenter.ConfirmAsync();
                return true;
            }
            catch (Exception error)
            {
                lastError = error.Message;
                return false;
            }
        }

        public void CancelPreview()
        {
            presenter?.CancelPreview();
            lastError = string.Empty;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Time.unscaledTime >= nextEditorDiagnosticAt
                && player != null
                && player.CurrentMode == 플레이어시점Mode.FirstPerson)
            {
                nextEditorDiagnosticAt = Time.unscaledTime + 2f;
                Debug.Log($"DaegwallyeongWarehouseScan:{editorScanDiagnostic}"
                          + $":Wiring={ValidateWiring()}"
                          + $":Ready={presenter != null && presenter.IsReady}"
                          + $":Focused={focused}");
            }
#endif
            if (!ValidateWiring())
            {
#if UNITY_EDITOR
                editorScanDiagnostic = "Scene 배선 오류";
#endif
                return;
            }
            if (!CanScan(player.CurrentMode, player.IsCameraTransitioning,
                    presenter.IsReady))
            {
#if UNITY_EDITOR
                editorScanDiagnostic = presenter.IsReady
                    ? "1인칭 전환 완료 대기"
                    : "Fixture 상태 사본 준비 대기";
#endif
                SetFocused(false, cancelPreview: true);
                return;
            }

            var centerRay = firstPersonCamera.ViewportPointToRay(
                new Vector3(.5f, .5f, 0f));
            SetFocused(TryResolveTarget(centerRay), cancelPreview: true);
            if (!focused || presenter.IsBusy || Keyboard.current == null) return;

            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame && presenter.Preview != null)
            {
                CancelPreview();
                return;
            }
            if (keyboard.eKey.wasPressedThisFrame && presenter.Preview == null)
                _ = PreviewAsync();
            else if (keyboard.enterKey.wasPressedThisFrame
                     && presenter.Preview?.CanConfirm == true)
                _ = ConfirmAsync();
        }

        private void SetFocused(bool value, bool cancelPreview)
        {
            if (focused == value) return;
            focused = value;
            target?.SetFocused(value);
            if (!value && cancelPreview && presenter?.Preview != null)
                presenter.CancelPreview();
        }

        private void Subscribe()
        {
            if (subscribed || presenter == null) return;
            presenter.상태사본Changed += RefreshVisual;
            subscribed = true;
            RefreshVisual();
        }

        private void Unsubscribe()
        {
            if (!subscribed || presenter == null) return;
            presenter.상태사본Changed -= RefreshVisual;
            subscribed = false;
        }

        private void RefreshVisual()
        {
            if (target != null && presenter?.Current != null)
                target.ApplySnapshot(presenter.Current);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            SetFocused(false, cancelPreview: true);
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (player == null || player.CurrentMode != 플레이어시점Mode.FirstPerson)
                return;
            GUI.color = focused ? new Color(1f, .75f, .2f, 1f) : Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 8f, Screen.height * .5f - 12f,
                20f, 24f), "+");
            GUI.color = Color.white;
            if (!focused)
            {
#if UNITY_EDITOR
                GUI.color = new Color(0f, 0f, 0f, .78f);
                GUI.DrawTexture(new Rect(Screen.width * .5f - 170f,
                    Screen.height * .5f + 20f, 340f, 30f),
                    Texture2D.whiteTexture);
                GUI.color = new Color(.25f, 1f, 1f, 1f);
                GUI.Label(new Rect(Screen.width * .5f - 160f,
                    Screen.height * .5f + 24f, 320f, 24f),
                    "창고 감지: " + editorScanDiagnostic);
                GUI.color = Color.white;
#endif
                return;
            }

            var preview = presenter.Preview;
            var prompt = presenter.IsBusy
                ? "서버 상태를 확인하는 중..."
                : preview?.CanConfirm == true
                    ? $"{target.KoreanName} · 상자 1개 획득\nEnter 확정 · Esc 취소"
                    : $"{target.KoreanName} · 현재 {target.VisibleQuantity}개\nE 획득 미리보기";
            if (!string.IsNullOrWhiteSpace(lastError))
                prompt += "\n차단: " + lastError;
            GUI.color = new Color(0f, 0f, 0f, .72f);
            GUI.DrawTexture(new Rect(Screen.width * .5f - 190f,
                Screen.height * .5f + 28f, 380f, 74f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 176f,
                Screen.height * .5f + 38f, 352f, 58f), prompt);
        }
    }
}
