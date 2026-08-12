using System;
using System.Globalization;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// Read-only shell for switching observation scale over one Simulation snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationWorldShellPresenter : MonoBehaviour
    {
        public const string WorldMapFocusAnchorId = "camera-focus:simulation-world-map";
        public const string SettlementFocusAnchorId = "camera-focus:simulation-settlement";
        public const string DistrictFocusAnchorPrefix = "camera-focus:simulation-district:";
        public const string ObjectFocusAnchorPrefix = "camera-focus:simulation-object:";

        [SerializeField] private GameObject worldMapRoot = null!;
        [SerializeField] private GameObject settlementInteriorRoot = null!;
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;
        [SerializeField] private Text modeText = null!;
        [SerializeField] private Text identityText = null!;
        [SerializeField] private Text economyText = null!;
        [SerializeField] private Text selectionText = null!;
        [SerializeField] private Button worldMapButton = null!;
        [SerializeField] private Button settlementButton = null!;
        [SerializeField] private Button backButton = null!;
        [SerializeField] private Button pauseButton = null!;
        [SerializeField] private Button speedButton = null!;

        private SimulationWorldShellStateMachine stateMachine = null!;
        private bool listenersBound;

        public event Action? PresentationChanged;

        public string ObservationScaleCode => stateMachine?.State.ObservationScaleCode ?? string.Empty;
        public string SessionStableId => stateMachine?.Snapshot.SessionStableId ?? string.Empty;
        public long WorldRevision => stateMachine?.Snapshot.WorldRevision ?? -1;
        public long WorldTick => stateMachine?.Snapshot.WorldTick ?? -1;
        public bool IsWorldMapVisible => worldMapRoot != null && worldMapRoot.activeSelf;
        public bool IsSettlementVisible => settlementInteriorRoot != null
            && settlementInteriorRoot.activeSelf;
        public string SelectedSettlementStableId => stateMachine?.State.SelectedSettlementStableId
            ?? string.Empty;
        public string SelectedDistrictStableId => stateMachine?.State.SelectedDistrictStableId
            ?? string.Empty;
        public string SelectedObjectStableId => stateMachine?.State.SelectedObjectStableId
            ?? string.Empty;
        public string CurrentFocusAnchorId => cameraRig != null
            ? cameraRig.CurrentFocusAnchorId : string.Empty;

        private void Awake() => Initialize(SimulationWorldShellFixture.CreateSnapshot());

        private void OnDestroy()
        {
            if (!listenersBound) return;
            worldMapButton.onClick.RemoveListener(ShowWorldMap);
            settlementButton.onClick.RemoveListener(ShowSettlement);
            backButton.onClick.RemoveListener(Back);
        }

        public void Configure(
            GameObject mapRoot,
            GameObject settlementRoot,
            DioramaTopDownCameraRig rig,
            Text mode,
            Text identity,
            Text economy,
            Text selection,
            Button mapButton,
            Button settlementViewButton,
            Button navigationBackButton,
            Button disabledPauseButton,
            Button disabledSpeedButton)
        {
            worldMapRoot = mapRoot;
            settlementInteriorRoot = settlementRoot;
            cameraRig = rig;
            modeText = mode;
            identityText = identity;
            economyText = economy;
            selectionText = selection;
            worldMapButton = mapButton;
            settlementButton = settlementViewButton;
            backButton = navigationBackButton;
            pauseButton = disabledPauseButton;
            speedButton = disabledSpeedButton;
        }

        public void Initialize(SimulationWorldShellSnapshot snapshot)
        {
            ValidateWiring();
            stateMachine = new SimulationWorldShellStateMachine(snapshot);
            BindListeners();
            pauseButton.interactable = false;
            speedButton.interactable = false;
            ApplyPresentation(WorldMapFocusAnchorId);
        }

        public void ShowWorldMap()
        {
            stateMachine.ShowWorldMap();
            ApplyPresentation(WorldMapFocusAnchorId);
        }

        public void ShowSettlement()
        {
            stateMachine.ShowSettlement(SimulationWorldShellFixture.SettlementStableId);
            ApplyPresentation(SettlementFocusAnchorId);
        }

        public void NavigateTo(SimulationWorldNavigationTargetView target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.Validate();
            if (target.ObservationScaleCode == SimulationObservationScaleCodes.Settlement)
                stateMachine.ShowSettlement(target.SettlementStableId);
            else if (target.ObservationScaleCode == SimulationObservationScaleCodes.District)
            {
                if (stateMachine.State.SelectedSettlementStableId != target.SettlementStableId)
                    stateMachine.ShowSettlement(target.SettlementStableId);
                stateMachine.ShowDistrict(target.DistrictStableId);
            }
            else if (target.ObservationScaleCode == SimulationObservationScaleCodes.Object)
            {
                if (stateMachine.State.SelectedSettlementStableId != target.SettlementStableId)
                    stateMachine.ShowSettlement(target.SettlementStableId);
                if (stateMachine.State.SelectedDistrictStableId != target.DistrictStableId)
                    stateMachine.ShowDistrict(target.DistrictStableId);
                stateMachine.ShowObject(target.ObjectStableId);
            }
            else
                throw new InvalidOperationException("SimulationNavigationTargetScaleUnsupported");
            ApplyPresentation(target.FocusAnchorId);
        }

        public void Back()
        {
            stateMachine.Back();
            ApplyPresentation(ResolveFocusAnchorId());
        }

        public void ApplySnapshotForTests(SimulationWorldShellSnapshot snapshot)
            => ApplyAuthoritativeSnapshot(snapshot);

        public void ApplyAuthoritativeSnapshot(SimulationWorldShellSnapshot snapshot)
        {
            stateMachine.ApplySnapshot(snapshot);
            ApplyPresentation(ResolveFocusAnchorId());
        }

        public void ValidateWiring()
        {
            if (worldMapRoot == null || settlementInteriorRoot == null || cameraRig == null
                || modeText == null || identityText == null || economyText == null
                || selectionText == null || worldMapButton == null || settlementButton == null
                || backButton == null || pauseButton == null || speedButton == null)
                throw new InvalidOperationException("SimulationWorldShellWiringMissing");
            if (worldMapRoot == settlementInteriorRoot)
                throw new InvalidOperationException("SimulationWorldShellSurfaceDuplicate");
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            worldMapButton.onClick.AddListener(ShowWorldMap);
            settlementButton.onClick.AddListener(ShowSettlement);
            backButton.onClick.AddListener(Back);
            listenersBound = true;
        }

        private void ApplyPresentation(string focusAnchorId)
        {
            var worldMapVisible = stateMachine.State.ObservationScaleCode
                == SimulationObservationScaleCodes.WorldMap;
            worldMapRoot.SetActive(worldMapVisible);
            settlementInteriorRoot.SetActive(!worldMapVisible);
            cameraRig.Focus(focusAnchorId);
            cameraRig.ApplyNowForTests();
            ApplyTargetSelection();

            var snapshot = stateMachine.Snapshot;
            var authorityLabel = snapshot.SourceModeCode == "SimulationServer"
                ? "SIMULATION SERVER"
                : snapshot.SourceModeCode == "SimulationFixtureAuthority"
                    ? "SIMULATION FIXTURE AUTHORITY"
                    : "SIMULATION FIXTURE · READ ONLY";
            modeText.text = authorityLabel + "\n"
                + (worldMapVisible ? "WORLD MAP" : "SETTLEMENT INTERIOR");
            identityText.text = snapshot.GameDateLabel
                + "  ·  Tick " + snapshot.WorldTick
                + "  ·  Revision " + snapshot.WorldRevision
                + "\n" + snapshot.SessionStableId;
            economyText.text = "Treasury  " + Number(snapshot.Treasury)
                + "\nLabor  " + Number(snapshot.LaborAvailable)
                + " available / " + Number(snapshot.LaborReserved) + " reserved"
                + "\nMarket Food  " + Number(snapshot.MarketFoodSupplyKg) + " kg"
                + "\nReserve Food  " + Number(snapshot.ReserveFoodKg) + " kg"
                + "\nFood Security  " + Number(snapshot.FoodSecurityDays) + " days"
                + "\nActive Tasks  " + snapshot.ActiveTaskCount;
            selectionText.text = BuildSelectionText();
            backButton.interactable = stateMachine.State.ObservationScaleCode
                != SimulationObservationScaleCodes.WorldMap;
            PresentationChanged?.Invoke();
        }

        private string ResolveFocusAnchorId()
        {
            var state = stateMachine.State;
            if (state.ObservationScaleCode == SimulationObservationScaleCodes.WorldMap)
                return WorldMapFocusAnchorId;
            if (state.ObservationScaleCode == SimulationObservationScaleCodes.Settlement)
                return SettlementFocusAnchorId;
            if (state.ObservationScaleCode == SimulationObservationScaleCodes.District)
                return DistrictFocusAnchorPrefix + state.SelectedDistrictStableId;
            return ObjectFocusAnchorPrefix + state.SelectedObjectStableId;
        }

        private void ApplyTargetSelection()
        {
            foreach (var target in FindObjectsByType<SimulationWorldNavigationTargetView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var state = stateMachine.State;
                var selected = target.ObservationScaleCode switch
                {
                    SimulationObservationScaleCodes.Settlement =>
                        target.SettlementStableId == state.SelectedSettlementStableId,
                    SimulationObservationScaleCodes.District =>
                        target.DistrictStableId == state.SelectedDistrictStableId,
                    SimulationObservationScaleCodes.Object =>
                        target.ObjectStableId == state.SelectedObjectStableId,
                    _ => false,
                };
                target.ApplySelected(selected);
            }
        }

        private string BuildSelectionText()
        {
            var state = stateMachine.State;
            if (state.ObservationScaleCode == SimulationObservationScaleCodes.WorldMap)
                return string.IsNullOrEmpty(state.SelectedSettlementStableId)
                    ? "Settlement marker를 선택해 정착지 내부로 이동"
                    : "최근 정착지: " + state.SelectedSettlementStableId;
            var breadcrumb = state.SelectedSettlementStableId;
            if (!string.IsNullOrEmpty(state.SelectedDistrictStableId))
                breadcrumb += "  ›  " + state.SelectedDistrictStableId;
            if (!string.IsNullOrEmpty(state.SelectedObjectStableId))
                breadcrumb += "  ›  " + state.SelectedObjectStableId;
            return state.ObservationScaleCode.ToUpperInvariant() + "\n" + breadcrumb;
        }

        private static string Number(decimal value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
