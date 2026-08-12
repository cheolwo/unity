using System;
using System.Globalization;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 물류이동Presenter : MonoBehaviour
    {
        [SerializeField] private SimulationWorldShellPresenter shellPresenter = null!;
        [SerializeField] private GameObject cardRoot = null!;
        [SerializeField] private Text cargoText = null!;
        [SerializeField] private Text phaseText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Button previewButton = null!;
        [SerializeField] private Button confirmButton = null!;
        [SerializeField] private Button tickButton = null!;

        private 물류이동Coordinator coordinator = null!;
        private bool busy;
        private bool listenersBound;

        private void Awake()
        {
            InitializeAuthority(
                new 물류이동FixtureAuthorityClient(
                    물류이동FixtureAuthorityClient.CreateInitialSnapshot()),
                물류이동FixtureAuthorityClient.CreateInitialSnapshot());
            BindListeners();
        }

        private void OnEnable()
        {
            if (shellPresenter != null) shellPresenter.PresentationChanged += ApplyPresentation;
        }

        private void OnDisable()
        {
            if (shellPresenter != null) shellPresenter.PresentationChanged -= ApplyPresentation;
        }

        public void Configure(
            SimulationWorldShellPresenter shell,
            GameObject root,
            Text cargo,
            Text phase,
            Text detail,
            Button preview,
            Button confirm,
            Button tick)
        {
            shellPresenter = shell;
            cardRoot = root;
            cargoText = cargo;
            phaseText = phase;
            detailText = detail;
            previewButton = preview;
            confirmButton = confirm;
            tickButton = tick;
        }

        public void InitializeAuthority(
            I물류이동AuthorityClient authority,
            물류이동AuthoritySnapshot initial)
        {
            ValidateWiring();
            coordinator = new 물류이동Coordinator(authority, initial);
            ApplyPresentation();
        }

        public async Task PreviewAsync() => await Run(async () => await coordinator.PreviewAsync());

        public async Task ConfirmAsync()
        {
            await Run(async () =>
            {
                await coordinator.ConfirmAsync();
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.Settlement.ToWorldShellSnapshot());
            });
        }

        public async Task AdvanceAsync()
        {
            await Run(async () =>
            {
                await coordinator.AdvanceAsync();
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.Settlement.ToWorldShellSnapshot());
            });
        }

        public async Task RunGoldenPathAsync()
        {
            await PreviewAsync();
            await ConfirmAsync();
            await AdvanceAsync();
            await AdvanceAsync();
            await AdvanceAsync();
        }

        public void ValidateWiring()
        {
            if (shellPresenter == null || cardRoot == null || cargoText == null
                || phaseText == null || detailText == null || previewButton == null
                || confirmButton == null || tickButton == null)
                throw new InvalidOperationException("LogisticsMovementWiringMissing");
        }

        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            try { await action(); }
            catch (Exception error) { Debug.LogError("LogisticsMovementFailed:" + error.Message); }
            finally { busy = false; ApplyPresentation(); }
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            previewButton.onClick.AddListener(() => _ = PreviewAsync());
            confirmButton.onClick.AddListener(() => _ = ConfirmAsync());
            tickButton.onClick.AddListener(() => _ = AdvanceAsync());
            listenersBound = true;
        }

        private void ApplyPresentation()
        {
            if (coordinator == null || shellPresenter == null || cardRoot == null) return;
            var selected = shellPresenter.SelectedObjectStableId == 물류이동Fixture.CargoStableId;
            cardRoot.SetActive(selected);
            if (!selected) return;

            var snapshot = coordinator.CurrentSnapshot;
            cargoText.text = "감자 CARGO · 300 kg\n" + snapshot.CargoStableId;
            phaseText.text = PhaseLabel(coordinator.PhaseCode)
                + " · WorldTick " + snapshot.WorldTick
                + " · Revision " + snapshot.Revision;
            detailText.text = BuildDetail(snapshot);
            previewButton.interactable = !busy
                && coordinator.PhaseCode == 물류이동PhaseCodes.CargoSelected;
            confirmButton.interactable = !busy
                && coordinator.PhaseCode == 물류이동PhaseCodes.PreviewReady;
            tickButton.interactable = !busy
                && (coordinator.PhaseCode == 물류이동PhaseCodes.Reserved
                    || coordinator.PhaseCode == 물류이동PhaseCodes.InTransit);
        }

        private string BuildDetail(물류이동AuthoritySnapshot snapshot)
        {
            if (coordinator.PhaseCode == 물류이동PhaseCodes.Failed)
                return "차단: " + coordinator.ErrorCode;
            if (coordinator.CurrentPreview == null)
                return "Farm packing → Regional hub\n"
                    + "Preview는 출발·재고·차량을 변경하지 않습니다.";
            return "Route  " + snapshot.RouteStableId
                + "\nProgress  " + snapshot.CompletedRouteTicks + " / " + snapshot.RequiredRouteTicks
                + " ticks\nOrigin stock  " + Number(snapshot.SourceAvailableQuantity) + " kg available"
                + "\nReserved  " + Number(snapshot.ReservedQuantity) + " kg"
                + "\nArrival candidate\n" + coordinator.CurrentPreview.DestinationStockCandidateStableId
                + "\n\n차량 animation은 Presentation이며 도착은 WorldTick Task가 확정합니다.";
        }

        private static string PhaseLabel(string phase) => phase switch
        {
            물류이동PhaseCodes.CargoSelected => "CARGO SELECTED",
            물류이동PhaseCodes.PreviewReady => "PREVIEW · NO MUTATION",
            물류이동PhaseCodes.Reserved => "CONFIRMED · STOCK RESERVED",
            물류이동PhaseCodes.InTransit => "IN TRANSIT",
            물류이동PhaseCodes.Arrived => "ARRIVED · RECEIVING PENDING",
            _ => "FAILED",
        };

        private static string Number(decimal value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
