using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 통합월드ModeCodes
    {
        public const string WorldOverview = "WorldOverview";
        public const string NatureExploration = "NatureExploration";
        public const string FarmFirstPerson = "FarmFirstPerson";
        public const string FarmTactical = "FarmTactical";
        public const string JinbuInbound = "JinbuInbound";
    }

    /// <summary>
    /// 하나의 SimulationWorldShell 안에서 월드·Farm 플레이·Hub 업무 화면을 전환합니다.
    /// 카메라와 UI만 바꾸며 WorldTick, revision, 업무 완료 상태를 만들지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 통합월드ModePresenter : MonoBehaviour
    {
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 진부Hub입고UiPresenter inboundUi = null!;
        [SerializeField] private Button worldButton = null!;
        [SerializeField] private Button natureButton = null!;
        [SerializeField] private Button firstPersonButton = null!;
        [SerializeField] private Button tacticalButton = null!;
        [SerializeField] private Button inboundButton = null!;
        [SerializeField] private bool startInTacticalMode;

        private bool listenersBound;

        public string CurrentModeCode { get; private set; } = 통합월드ModeCodes.WorldOverview;
        public bool StartsInTacticalMode => startInTacticalMode;
        public bool PresentationOnly => true;

        public void Configure(
            SimulationWorldShellPresenter worldShell,
            플레이어경관Controller playerController,
            진부Hub입고UiPresenter inboundPresenter,
            Button worldOverviewButton,
            Button natureExplorationButton,
            Button farmFirstPersonButton,
            Button farmTacticalButton,
            Button jinbuInboundButton)
        {
            shell = worldShell;
            player = playerController;
            inboundUi = inboundPresenter;
            worldButton = worldOverviewButton;
            natureButton = natureExplorationButton;
            firstPersonButton = farmFirstPersonButton;
            tacticalButton = farmTacticalButton;
            inboundButton = jinbuInboundButton;
        }

        private void Awake()
        {
            player ??= FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            ValidateWiring();
            BindListeners();
            ApplyButtonState();
        }

        private void Start()
        {
            if (startInTacticalMode) ShowFarmTactical();
        }

        public void SetStartInTacticalMode(bool enabled) => startInTacticalMode = enabled;

        private void OnDestroy()
        {
            if (!listenersBound) return;
            worldButton.onClick.RemoveListener(ShowWorldOverview);
            natureButton.onClick.RemoveListener(ShowNatureExploration);
            firstPersonButton.onClick.RemoveListener(ShowFarmFirstPerson);
            tacticalButton.onClick.RemoveListener(ShowFarmTactical);
            inboundButton.onClick.RemoveListener(ShowJinbuInbound);
        }

        public void ShowWorldOverview()
        {
            player.ExitPlayerMode();
            inboundUi.SetContextVisible(false);
            shell.ShowWorldMap();
            SetMode(통합월드ModeCodes.WorldOverview);
        }

        public void ShowFarmFirstPerson()
        {
            inboundUi.SetContextVisible(false);
            shell.FocusWorldPresentation(
                SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:farm");
            player.EnterFarmManagementFirstPersonMode();
            SetMode(통합월드ModeCodes.FarmFirstPerson);
        }

        public void ShowNatureExploration()
        {
            inboundUi.SetContextVisible(false);
            player.EnterExplorationFirstPersonMode();
            var nature = FindFirstObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include);
            nature?.PreparePlayerEntryView();
            SetMode(통합월드ModeCodes.NatureExploration);
        }

        public void ShowFarmTactical()
        {
            inboundUi.SetContextVisible(false);
            shell.FocusWorldPresentation(
                SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:farm");
            player.EnterFarmManagementMode();
            SetMode(통합월드ModeCodes.FarmTactical);
        }

        public void ShowFarmDefault() => ShowFarmTactical();

        public void ShowJinbuInbound()
        {
            player.ExitPlayerMode();
            shell.FocusWorldPresentation(
                SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:logistics");
            inboundUi.SetContextVisible(true);
            SetMode(통합월드ModeCodes.JinbuInbound);
        }

        public void ValidateWiring()
        {
            if (shell == null || player == null || inboundUi == null
                || worldButton == null || natureButton == null
                || firstPersonButton == null
                || tacticalButton == null || inboundButton == null)
                throw new InvalidOperationException("UnifiedWorldModeWiringMissing");
            if (!player.PresentationOnly)
                throw new InvalidOperationException("UnifiedWorldPlayerAuthorityLeak");
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            worldButton.onClick.AddListener(ShowWorldOverview);
            natureButton.onClick.AddListener(ShowNatureExploration);
            firstPersonButton.onClick.AddListener(ShowFarmFirstPerson);
            tacticalButton.onClick.AddListener(ShowFarmTactical);
            inboundButton.onClick.AddListener(ShowJinbuInbound);
            listenersBound = true;
        }

        private void SetMode(string modeCode)
        {
            CurrentModeCode = modeCode;
            ApplyButtonState();
        }

        private void ApplyButtonState()
        {
            Apply(worldButton, CurrentModeCode == 통합월드ModeCodes.WorldOverview);
            Apply(natureButton, CurrentModeCode == 통합월드ModeCodes.NatureExploration);
            Apply(firstPersonButton, CurrentModeCode == 통합월드ModeCodes.FarmFirstPerson);
            Apply(tacticalButton, CurrentModeCode == 통합월드ModeCodes.FarmTactical);
            Apply(inboundButton, CurrentModeCode == 통합월드ModeCodes.JinbuInbound);
        }

        private static void Apply(Button button, bool active)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = active
                    ? new Color(.94f, .42f, .05f, .98f)
                    : new Color(.15f, .19f, .20f, .94f);
        }
    }
}
