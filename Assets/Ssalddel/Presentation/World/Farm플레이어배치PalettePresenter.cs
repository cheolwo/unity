using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class Farm플레이어배치PalettePresenter : MonoBehaviour
    {
        [SerializeField] private Farm플레이어배치Controller controller = null!;
        [SerializeField] private GameObject panel = null!;
        [SerializeField] private Button openButton = null!;
        [SerializeField] private Button closeButton = null!;
        [SerializeField] private Button[] blueprintButtons = Array.Empty<Button>();
        [SerializeField] private Button rotateButton = null!;
        [SerializeField] private Button nudgeLeftButton = null!;
        [SerializeField] private Button nudgeRightButton = null!;
        [SerializeField] private Button nudgeForwardButton = null!;
        [SerializeField] private Button nudgeBackButton = null!;
        [SerializeField] private Button previewButton = null!;
        [SerializeField] private Button confirmButton = null!;
        [SerializeField] private Button tickButton = null!;
        [SerializeField] private Text statusText = null!;
        private bool bound;

        public void Configure(Farm플레이어배치Controller placementController,
            GameObject palettePanel, Button showButton, Button hideButton,
            Button[] objectButtons, Button rotate, Button nudgeLeft,
            Button nudgeRight, Button nudgeForward, Button nudgeBack,
            Button preview, Button confirm, Button tick, Text status)
        {
            controller = placementController;
            panel = palettePanel;
            openButton = showButton;
            closeButton = hideButton;
            blueprintButtons = objectButtons ?? Array.Empty<Button>();
            rotateButton = rotate;
            nudgeLeftButton = nudgeLeft;
            nudgeRightButton = nudgeRight;
            nudgeForwardButton = nudgeForward;
            nudgeBackButton = nudgeBack;
            previewButton = preview;
            confirmButton = confirm;
            tickButton = tick;
            statusText = status;
            Bind();
            panel.SetActive(false);
        }

        public bool ValidateWiring()
            => controller != null && panel != null && openButton != null
               && closeButton != null && rotateButton != null
               && nudgeLeftButton != null && nudgeRightButton != null
               && nudgeForwardButton != null && nudgeBackButton != null
               && previewButton != null && confirmButton != null
               && tickButton != null && statusText != null
               && blueprintButtons.Length == controller.BlueprintCount
               && blueprintButtons.All(value => value != null);

        private void Start()
        {
            Bind();
            Render();
        }

        private void Update()
        {
            if (!bound) return;
            Render();
        }

        private void Bind()
        {
            if (bound) return;
            if (!ValidateWiring())
                throw new InvalidOperationException("FarmPlacementPaletteWiringInvalid");
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            for (var index = 0; index < blueprintButtons.Length; index++)
            {
                var captured = index;
                blueprintButtons[index].onClick.AddListener(() =>
                    controller.SelectBlueprintAt(captured));
                blueprintButtons[index].GetComponentInChildren<Text>().text =
                    controller.BlueprintDisplayName(index);
            }
            rotateButton.onClick.AddListener(controller.RotateCandidate);
            nudgeLeftButton.onClick.AddListener(() => controller.NudgeCandidate(-25, 0));
            nudgeRightButton.onClick.AddListener(() => controller.NudgeCandidate(25, 0));
            nudgeForwardButton.onClick.AddListener(() => controller.NudgeCandidate(0, 25));
            nudgeBackButton.onClick.AddListener(() => controller.NudgeCandidate(0, -25));
            previewButton.onClick.AddListener(() => _ = ExecuteAsync(controller.PreviewAsync));
            confirmButton.onClick.AddListener(() => _ = ExecuteAsync(controller.ConfirmAsync));
            tickButton.onClick.AddListener(() => _ = ExecuteAsync(
                controller.AdvanceOneTickAsync));
            bound = true;
        }

        private void Open()
        {
            controller.EnterPlacementMode();
            panel.SetActive(true);
            Render();
        }

        private void Close()
        {
            controller.ExitPlacementMode();
            panel.SetActive(false);
        }

        private async System.Threading.Tasks.Task ExecuteAsync(Func<
            System.Threading.Tasks.Task> action)
        {
            try { await action(); }
            catch (Exception exception)
            {
                statusText.text = "배치 오류 · " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void Render()
        {
            if (statusText != null) statusText.text = controller.Status;
            var hasCandidate = controller.CurrentCandidate != null;
            rotateButton.interactable = hasCandidate && !controller.Busy;
            nudgeLeftButton.interactable = hasCandidate && !controller.Busy;
            nudgeRightButton.interactable = hasCandidate && !controller.Busy;
            nudgeForwardButton.interactable = hasCandidate && !controller.Busy;
            nudgeBackButton.interactable = hasCandidate && !controller.Busy;
            previewButton.interactable = hasCandidate && controller.CandidateLocked
                                         && !controller.Busy;
            confirmButton.interactable = controller.CurrentPreview?.CanConfirm == true
                                         && !controller.Busy;
            tickButton.interactable = controller.CurrentContext?.Objects.Any(value =>
                value.LifecycleCode == "Planned"
                || value.LifecycleCode == "UnderConstruction") == true
                && !controller.Busy;
        }

        private void OnDestroy()
        {
            if (!bound) return;
            openButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
            foreach (var button in blueprintButtons) button.onClick.RemoveAllListeners();
            rotateButton.onClick.RemoveAllListeners();
            nudgeLeftButton.onClick.RemoveAllListeners();
            nudgeRightButton.onClick.RemoveAllListeners();
            nudgeForwardButton.onClick.RemoveAllListeners();
            nudgeBackButton.onClick.RemoveAllListeners();
            previewButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
            tickButton.onClick.RemoveAllListeners();
        }
    }
}
