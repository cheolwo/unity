using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public enum WI공간모판검토Mode
    {
        Overview,
        Detail,
        CandidateSheet,
    }

    /// <summary>
    /// E4 증거 대상인 H1 WI 공간 모판을 사람이 비교하기 위한 읽기 전용 표현 계층이다.
    /// 실제 H2~H4 공간 조립이나 E5 증거, 업무 상태를 생성하거나 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WI공간모판검토Presenter : MonoBehaviour
    {
        public const string OverviewAnchorId = "wi-seedbed-overview";
        public const string DetailAnchorId = "wi-seedbed-detail";
        public const string SheetAnchorId = "wi-seedbed-candidate-sheet";

        [SerializeField] private WI공간모판VisualCatalog catalog = null!;
        [SerializeField] private Transform overviewRoot = null!;
        [SerializeField] private Transform detailRoot = null!;
        [SerializeField] private Transform candidateSheetRoot = null!;
        [SerializeField] private Transform detailPrefabHost = null!;
        [SerializeField] private Transform candidateSheetPrefabHost = null!;
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;
        [SerializeField] private Text modeText = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text summaryText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private Button overviewButton = null!;
        [SerializeField] private Button candidateSheetButton = null!;
        [SerializeField] private Button[] seedbedButtons = Array.Empty<Button>();
        [SerializeField] private Button[] spaceButtons = Array.Empty<Button>();
        [SerializeField] private Button[] candidateButtons = Array.Empty<Button>();

        private WI공간모판VisualEntry selectedSeedbed = null!;
        private WI공간모판SpaceView selectedSpace = null!;
        private WI공간모판CandidateView selectedCandidate = null!;
        private bool initialized;

        public WI공간모판검토Mode Mode { get; private set; }
        public string SelectedSeedbedStableId => selectedSeedbed?.StableId ?? string.Empty;
        public string SelectedSpaceCode => selectedSpace?.SpaceCode ?? string.Empty;
        public string SelectedCompositionKey => selectedCandidate?.CompositionKey ?? string.Empty;
        public int ActiveDetailCandidateCount => detailPrefabHost == null
            ? 0 : detailPrefabHost.childCount;
        public int ActiveSheetCandidateCount => candidateSheetPrefabHost == null
            ? 0 : candidateSheetPrefabHost.childCount;
        public WI공간모판VisualCatalog Catalog => catalog;
        public string CurrentModeLabel => modeText == null ? string.Empty : modeText.text;

        private void Awake()
        {
            if (catalog != null) Initialize();
        }

        public void Configure(
            WI공간모판VisualCatalog sourceCatalog,
            Transform overview,
            Transform detail,
            Transform sheet,
            Transform detailHost,
            Transform sheetHost,
            DioramaTopDownCameraRig rig,
            Text modeLabel,
            Text titleLabel,
            Text summaryLabel,
            Text detailLabel,
            Text lineageLabel,
            Text boundaryLabel,
            Button overviewControl,
            Button sheetControl,
            Button[] seedbedControls,
            Button[] spaceControls,
            Button[] candidateControls)
        {
            catalog = sourceCatalog;
            overviewRoot = overview;
            detailRoot = detail;
            candidateSheetRoot = sheet;
            detailPrefabHost = detailHost;
            candidateSheetPrefabHost = sheetHost;
            cameraRig = rig;
            modeText = modeLabel;
            titleText = titleLabel;
            summaryText = summaryLabel;
            detailText = detailLabel;
            lineageText = lineageLabel;
            boundaryText = boundaryLabel;
            overviewButton = overviewControl;
            candidateSheetButton = sheetControl;
            seedbedButtons = seedbedControls ?? Array.Empty<Button>();
            spaceButtons = spaceControls ?? Array.Empty<Button>();
            candidateButtons = candidateControls ?? Array.Empty<Button>();
            initialized = false;
        }

        public void Initialize()
        {
            if (initialized) return;
            ValidateWiring();

            overviewButton.onClick.RemoveAllListeners();
            overviewButton.onClick.AddListener(ShowOverview);
            candidateSheetButton.onClick.RemoveAllListeners();
            candidateSheetButton.onClick.AddListener(ShowSelectedCandidateSheet);

            for (var index = 0; index < seedbedButtons.Length; index++)
            {
                var button = seedbedButtons[index];
                button.onClick.RemoveAllListeners();
                var stableId = catalog.Entries[index].StableId;
                button.onClick.AddListener(() => ShowSeedbed(stableId));
                SetButtonLabel(button, catalog.Entries[index].Title.Replace(" 공간 모판", string.Empty));
            }

            initialized = true;
            ShowOverview();
        }

        public void ValidateWiring()
        {
            if (catalog == null) throw new InvalidOperationException("WiSpatialSeedbedCatalogMissing");
            catalog.Validate();
            if (overviewRoot == null || detailRoot == null || candidateSheetRoot == null
                || detailPrefabHost == null || candidateSheetPrefabHost == null
                || cameraRig == null || modeText == null || titleText == null
                || summaryText == null || detailText == null || lineageText == null
                || boundaryText == null || overviewButton == null || candidateSheetButton == null)
                throw new InvalidOperationException("WiSpatialSeedbedReviewWiringMissing");
            if (seedbedButtons.Length != WI공간모판VisualCatalog.ExpectedEntryCount
                || seedbedButtons.Any(value => value == null)
                || spaceButtons.Length < 3 || spaceButtons.Any(value => value == null)
                || candidateButtons.Length < 9 || candidateButtons.Any(value => value == null))
                throw new InvalidOperationException("WiSpatialSeedbedReviewControlsInvalid");
            if (overviewRoot.GetComponentsInChildren<WI공간모판OverviewItem>(true).Length
                != WI공간모판VisualCatalog.ExpectedSpaceCount)
                throw new InvalidOperationException("WiSpatialSeedbedOverviewItemsInvalid");
        }

        public void ShowOverview()
        {
            EnsureInitializedFlag();
            Mode = WI공간모판검토Mode.Overview;
            overviewRoot.gameObject.SetActive(true);
            detailRoot.gameObject.SetActive(false);
            candidateSheetRoot.gameObject.SetActive(false);
            ClearChildren(detailPrefabHost);
            ClearChildren(candidateSheetPrefabHost);
            SetDynamicButtons(Array.Empty<WI공간모판SpaceView>(), Array.Empty<WI공간모판CandidateView>());

            modeText.text = "증거 E4 · 공간 계층 H1 · 5개 공간 모판 / 9개 내부 공간";
            titleText.text = "H1 행위를 품는 재사용 공간 모판";
            summaryText.text = "생산 → 집하 → 포장 → 상차 → Farm Gate → 회랑 → Hub 하차 → 검수 → 보관";
            detailText.text = "13개 E3 WI를 위치 독립적인 H1 공간 역할·능력·용량·연결구로 묶고 E4 증거로 검토하는 화면입니다.";
            lineageText.text = $"원본 {catalog.SourceCatalogRevision} · WI {catalog.WorldInteractionCatalogRevision}\n"
                + $"경관 문법 {catalog.LandscapeGrammarRevision} · Synty {catalog.SyntyBindingRevision}";
            boundaryText.text = "H1 연결구 미리보기 · 실제 H2 Block / 도로 / 좌표 / 운영 상태가 아닙니다.";
            Focus(OverviewAnchorId);
        }

        public void ShowSeedbed(string stableId)
        {
            EnsureInitializedFlag();
            selectedSeedbed = catalog.Resolve(stableId);
            selectedSpace = selectedSeedbed.Spaces[0];
            selectedCandidate = selectedSpace.Candidates[0];
            Mode = WI공간모판검토Mode.Detail;
            overviewRoot.gameObject.SetActive(false);
            detailRoot.gameObject.SetActive(true);
            candidateSheetRoot.gameObject.SetActive(false);
            UpdateDetailBoundary();
            BuildDetailCandidate();
            RefreshDetailUi();
            Focus(DetailAnchorId);
        }

        public void SelectSpace(string spaceCode)
        {
            if (selectedSeedbed == null) throw new InvalidOperationException("WiSpatialSeedbedNotSelected");
            selectedSpace = selectedSeedbed.Spaces.Single(value => value.SpaceCode == spaceCode);
            selectedCandidate = selectedSpace.Candidates[0];
            Mode = WI공간모판검토Mode.Detail;
            detailRoot.gameObject.SetActive(true);
            candidateSheetRoot.gameObject.SetActive(false);
            BuildDetailCandidate();
            RefreshDetailUi();
            Focus(DetailAnchorId);
        }

        public void SelectCandidate(string compositionKey)
        {
            if (selectedSpace == null) throw new InvalidOperationException("WiSpatialSeedbedSpaceNotSelected");
            selectedCandidate = selectedSpace.Candidates.Single(value => value.CompositionKey == compositionKey);
            Mode = WI공간모판검토Mode.Detail;
            detailRoot.gameObject.SetActive(true);
            candidateSheetRoot.gameObject.SetActive(false);
            BuildDetailCandidate();
            RefreshDetailUi();
            Focus(DetailAnchorId);
        }

        public void ShowSelectedCandidateSheet()
        {
            if (selectedSeedbed == null)
                selectedSeedbed = catalog.Entries[0];
            ShowCandidateSheet(selectedSeedbed.StableId);
        }

        public void ShowCandidateSheet(string stableId)
        {
            EnsureInitializedFlag();
            selectedSeedbed = catalog.Resolve(stableId);
            selectedSpace = selectedSeedbed.Spaces[0];
            selectedCandidate = selectedSpace.Candidates[0];
            Mode = WI공간모판검토Mode.CandidateSheet;
            overviewRoot.gameObject.SetActive(false);
            detailRoot.gameObject.SetActive(false);
            candidateSheetRoot.gameObject.SetActive(true);
            ClearChildren(candidateSheetPrefabHost);

            var candidates = selectedSeedbed.UniqueCandidates
                .OrderBy(value => value.CompositionKey, StringComparer.Ordinal).ToArray();
            var columns = 3;
            var spacingX = Mathf.Max(42f, candidates.Max(value => value.NativeFootprintMeters.x) + 8f);
            var spacingZ = Mathf.Max(34f, candidates.Max(value => value.NativeFootprintMeters.y) + 8f);
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                var instance = Instantiate(candidate.Prefab, candidateSheetPrefabHost);
                instance.name = "후보_" + candidate.CompositionKey;
                instance.transform.localPosition = new Vector3(
                    (index % columns) * spacingX,
                    0f,
                    -(index / columns) * spacingZ);
                instance.transform.localRotation = Quaternion.identity;
                PreparePresentationInstance(instance);
            }

            SetDynamicButtons(selectedSeedbed.Spaces.ToArray(), candidates);
            modeText.text = "증거 E4 · H1 후보 비교표 · 실제 크기 / 자동 축척 없음";
            titleText.text = selectedSeedbed.Title + " — 경관 후보 " + candidates.Length + "개";
            summaryText.text = selectedSeedbed.Summary;
            detailText.text = string.Join("\n", candidates.Select(value =>
                $"{value.CompositionKey} · {value.TopologyCode} · {FormatSize(value.NativeFootprintMeters)}"));
            lineageText.text = "후보는 H1 허용 목록과 Unity Synty Binding의 교집합입니다.";
            boundaryText.text = "후보 비교 전용 · E5 배치 적합성 승인이나 실제 도로 연결을 뜻하지 않습니다.";
            Focus(SheetAnchorId);
        }

        private void BuildDetailCandidate()
        {
            if (selectedCandidate == null) return;
            ClearChildren(detailPrefabHost);
            var instance = Instantiate(selectedCandidate.Prefab, detailPrefabHost);
            instance.name = "선택후보_" + selectedCandidate.CompositionKey;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            PreparePresentationInstance(instance);
        }

        private void UpdateDetailBoundary()
        {
            if (selectedSeedbed == null) return;
            var preferredGround = detailRoot.Find("선호크기바닥");
            var maximumOutline = detailRoot.Find("최대허용경계");
            if (preferredGround == null || maximumOutline == null)
                throw new InvalidOperationException("WiSpatialSeedbedDetailBoundaryMissing");

            preferredGround.localScale = new Vector3(
                selectedSeedbed.PreferredSizeMeters.x,
                .2f,
                selectedSeedbed.PreferredSizeMeters.y);
            var maximum = selectedSeedbed.MaximumSizeMeters;
            UpdateBoundaryLine(maximumOutline, "North",
                new Vector3(maximum.x, .12f, .22f),
                new Vector3(0f, .02f, maximum.y * .5f));
            UpdateBoundaryLine(maximumOutline, "South",
                new Vector3(maximum.x, .12f, .22f),
                new Vector3(0f, .02f, -maximum.y * .5f));
            UpdateBoundaryLine(maximumOutline, "East",
                new Vector3(.22f, .12f, maximum.y),
                new Vector3(maximum.x * .5f, .02f, 0f));
            UpdateBoundaryLine(maximumOutline, "West",
                new Vector3(.22f, .12f, maximum.y),
                new Vector3(-maximum.x * .5f, .02f, 0f));
        }

        private static void UpdateBoundaryLine(
            Transform parent,
            string name,
            Vector3 scale,
            Vector3 position)
        {
            var line = parent.Find(name);
            if (line == null)
                throw new InvalidOperationException("WiSpatialSeedbedDetailBoundaryLineMissing:" + name);
            line.localScale = scale;
            line.localPosition = position;
        }

        private void RefreshDetailUi()
        {
            if (selectedSeedbed == null || selectedSpace == null || selectedCandidate == null) return;
            var capacities = string.Join(", ", selectedSpace.Capacities.Select(value =>
                $"{value.CapacityCode} {value.Quantity}{value.UnitCode}"));
            var connectors = selectedSeedbed.ConnectorStubs
                .Where(value => value.InternalSpaceCode == selectedSpace.SpaceCode)
                .Select(value => $"{value.FlowDirectionCode} {value.StubCode} → {value.AdjacentWorldInteractionId}")
                .ToArray();
            var connectorText = connectors.Length == 0 ? "외부 연결구 없음" : string.Join(" / ", connectors);
            var fit = BuildFitStatus(selectedSeedbed, selectedCandidate);

            modeText.text = "증거 E4 · H1 모판 상세 · 후보 선택";
            titleText.text = selectedSeedbed.Title;
            summaryText.text = selectedSeedbed.Summary;
            detailText.text = $"공간 {selectedSpace.SpaceCode} · 역할 {selectedSpace.SpatialRoleCode}\n"
                + $"능력 {string.Join(", ", selectedSpace.CapabilityCodes)}\n"
                + $"업무 용량 {capacities}\n"
                + $"후보 {selectedCandidate.CompositionKey}\n"
                + $"형태 {selectedCandidate.TopologyCode} · 원형 크기 {FormatSize(selectedCandidate.NativeFootprintMeters)} · {fit}";
            lineageText.text = $"포함 WI: {string.Join(" → ", selectedSeedbed.IncludedWiIds)}\n{connectorText}";
            boundaryText.text = $"허용 크기 최소 {FormatSize(selectedSeedbed.MinimumSizeMeters)} · 선호 "
                + $"{FormatSize(selectedSeedbed.PreferredSizeMeters)} · 최대 {FormatSize(selectedSeedbed.MaximumSizeMeters)}\n"
                + "H1 연결구 미리보기 · 실제 H2 Block / 도로 / 좌표 / 운영 상태가 아닙니다.";
            SetDynamicButtons(selectedSeedbed.Spaces.ToArray(), selectedSpace.Candidates.ToArray());
        }

        private void SetDynamicButtons(
            IReadOnlyList<WI공간모판SpaceView> spaces,
            IReadOnlyList<WI공간모판CandidateView> candidates)
        {
            for (var index = 0; index < spaceButtons.Length; index++)
            {
                var active = index < spaces.Count;
                spaceButtons[index].gameObject.SetActive(active);
                spaceButtons[index].onClick.RemoveAllListeners();
                if (!active) continue;
                var space = spaces[index];
                SetButtonLabel(spaceButtons[index], space.SpaceCode);
                spaceButtons[index].onClick.AddListener(() => SelectSpace(space.SpaceCode));
            }

            for (var index = 0; index < candidateButtons.Length; index++)
            {
                var active = index < candidates.Count;
                candidateButtons[index].gameObject.SetActive(active);
                candidateButtons[index].onClick.RemoveAllListeners();
                if (!active) continue;
                var candidate = candidates[index];
                SetButtonLabel(candidateButtons[index], ShortCandidateLabel(candidate.CompositionKey));
                candidateButtons[index].onClick.AddListener(() => SelectCandidate(candidate.CompositionKey));
            }

            candidateSheetButton.interactable = selectedSeedbed != null || catalog.Entries.Count > 0;
        }

        private static string BuildFitStatus(
            WI공간모판VisualEntry entry,
            WI공간모판CandidateView candidate)
        {
            var footprint = candidate.NativeFootprintMeters;
            if (footprint.x > entry.MaximumSizeMeters.x || footprint.y > entry.MaximumSizeMeters.y)
                return "최대 크기 초과 — E5 배치 전 재검토";
            if (footprint.x < entry.MinimumSizeMeters.x || footprint.y < entry.MinimumSizeMeters.y)
                return "모판 여유 공간 필요";
            return "허용 크기 안";
        }

        private static string ShortCandidateLabel(string compositionKey)
        {
            var separator = compositionKey.IndexOf(':');
            return separator >= 0 ? compositionKey[(separator + 1)..] : compositionKey;
        }

        private static string FormatSize(Vector2 value) => $"{value.x:0.#}×{value.y:0.#}m";

        private void Focus(string anchorId)
        {
            if (cameraRig != null && cameraRig.isActiveAndEnabled)
                cameraRig.Focus(anchorId);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        private static void PreparePresentationInstance(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).gameObject;
                if (UnityEngine.Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void EnsureInitializedFlag()
        {
            if (!initialized)
            {
                initialized = true;
                ValidateWiring();
            }
        }
    }
}
