using System;
using System.Globalization;
using System.Linq;
using System.Text;
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
        private string npcRouteStateCode = Npc물류운송Codes.Planned;

        public 물류이동AuthoritySnapshot? CurrentAuthoritySnapshot
            => coordinator == null ? null : coordinator.CurrentSnapshot;
        public string CurrentPhaseCode
            => coordinator == null ? string.Empty : coordinator.PhaseCode;
        public string NpcRouteStateCode => npcRouteStateCode;

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

        public async Task ApplyNpcRouteCheckpointAsync(Npc물류RouteCheckpointData checkpoint)
        {
            await Run(async () =>
            {
                await coordinator.ApplyNpcRouteCheckpointAsync(checkpoint);
                npcRouteStateCode = coordinator.PhaseCode == 물류이동PhaseCodes.Arrived
                    ? Npc물류운송Codes.Arrived
                    : Npc물류운송Codes.Moving;
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.Settlement.ToWorldShellSnapshot());
            });
        }

        public void SetNpcRoutePresentationState(string stateCode)
        {
            npcRouteStateCode = stateCode ?? string.Empty;
            ApplyPresentation();
        }

        public async Task RunGoldenPathAsync()
        {
            await PreviewAsync();
            await ConfirmAsync();
            while (coordinator.PhaseCode != 물류이동PhaseCodes.Arrived)
            {
                var snapshot = coordinator.CurrentSnapshot;
                var sequence = snapshot.CompletedRouteTicks + 1;
                await ApplyNpcRouteCheckpointAsync(new Npc물류RouteCheckpointData
                {
                    CheckpointStableId = snapshot.RouteStableId + ":checkpoint:" + sequence,
                    RouteStableId = snapshot.RouteStableId,
                    CargoOrOrderStableId = snapshot.CargoStableId,
                    NpcStableId = snapshot.CarrierCandidateStableId,
                    VehicleStableId = snapshot.VehicleStableId,
                    Sequence = sequence,
                    ExpectedRevision = snapshot.Revision,
                });
            }
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
            SetButtonLabel(previewButton, "배차 미리보기");
            SetButtonLabel(confirmButton, "추천 기사 확정");
            SetButtonLabel(tickButton, "NPC 자동 운행");
            previewButton.interactable = !busy
                && coordinator.PhaseCode == 물류이동PhaseCodes.CargoSelected;
            confirmButton.interactable = !busy
                && coordinator.PhaseCode == 물류이동PhaseCodes.PreviewReady;
            tickButton.interactable = false;
        }

        private string BuildDetail(물류이동AuthoritySnapshot snapshot)
        {
            if (coordinator.PhaseCode == 물류이동PhaseCodes.Failed)
                return "차단: " + coordinator.ErrorCode;
            if (coordinator.CurrentPreview == null)
                return "농장 포장장 → 지역 물류 거점\n"
                    + "300kg 감자를 실을 가상 기사를 비교합니다.\n"
                    + "미리보기는 재고·배차·차량을 변경하지 않습니다.";

            var preview = coordinator.CurrentPreview;
            if (coordinator.PhaseCode == 물류이동PhaseCodes.PreviewReady)
            {
                var builder = new StringBuilder();
                builder.Append("배차 후보 ").Append(preview.CandidateEvaluations.Length)
                    .Append("명 · 규칙 ").Append(preview.DispatchRuleRevision);
                foreach (var candidate in preview.CandidateEvaluations)
                {
                    builder.Append("\n").Append(candidate.IsRecommended ? "★ 추천 " : "· ")
                        .Append(CandidateLabel(candidate.CarrierCandidateStableId)).Append(" · ")
                        .Append(Number(candidate.VehicleCapacity)).Append("kg · ");
                    if (candidate.IsEligible)
                        builder.Append(Number(candidate.TotalScore)).Append("점 · 상차 ")
                            .Append(Number(candidate.PickupDistanceKm ?? 0m)).Append("km");
                    else
                        builder.Append("차단 · ").Append(BlockLabel(candidate.BlockReasonCodes));
                }
                builder.Append("\n\n확정 전: 재고 300kg 사용 가능 · 차량 미출발");
                return builder.ToString();
            }

            return "배차  " + CandidateLabel(snapshot.CarrierCandidateStableId)
                + " · " + snapshot.VehicleStableId
                + "\n상태  " + snapshot.DispatchStateCode
                + "\n운송  " + snapshot.CompletedRouteTicks + " / " + snapshot.RequiredRouteTicks
                + "구간 · " + snapshot.MovementStateCode
                + "\nNPC 경로  " + NpcRouteLabel(npcRouteStateCode)
                + "\n출발지 재고  " + Number(snapshot.SourceAvailableQuantity) + "kg"
                + " · 예약 " + Number(snapshot.ReservedQuantity) + "kg"
                + "\n도착 후보  " + preview.DestinationStockCandidateStableId
                + "\n\nNPC가 경로 체크포인트를 순서대로 통과할 때만 로컬 권위가 진행됩니다.";
        }

        private static string PhaseLabel(string phase) => phase switch
        {
            물류이동PhaseCodes.CargoSelected => "화물 선택됨",
            물류이동PhaseCodes.PreviewReady => "배차 미리보기 · 변경 없음",
            물류이동PhaseCodes.Reserved => "배차 확정 · 재고 예약",
            물류이동PhaseCodes.InTransit => "운송 중",
            물류이동PhaseCodes.Arrived => "도착 · 인수 대기",
            _ => "진행 실패",
        };

        private static string CandidateLabel(string stableId)
            => stableId switch
            {
                "carrier-candidate:sim.small-van" => "소형 밴 기사",
                "carrier-candidate:sim.stale-truck" => "위치 미확인 트럭 기사",
                "carrier-candidate:sim.waiting-truck" => "대기 중인 지역 트럭 기사",
                _ => stableId,
            };

        private static string NpcRouteLabel(string stateCode) => stateCode switch
        {
            Npc물류운송Codes.Planned => "배차 계획됨",
            Npc물류운송Codes.AwaitingRouteCells => "경로 준비 대기",
            Npc물류운송Codes.Moving => "경로 이동 중",
            Npc물류운송Codes.PausedByStreaming => "다음 공간 생성 대기",
            Npc물류운송Codes.Arrived => "목적지 도착",
            _ => stateCode,
        };

        private static string BlockLabel(string[] codes)
            => string.Join(", ", codes.Select(value => value switch
            {
                "VehicleCapacityExceeded" => "적재량 부족",
                "CandidateLocationStale" => "위치 정보 오래됨",
                _ => value,
            }));

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        private static string Number(decimal value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
