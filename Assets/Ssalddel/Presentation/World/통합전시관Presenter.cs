using System;
using System.Linq;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Runtime.Exhibition;
using Ssalddel.Unity.Runtime.ExhibitionFixtures;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 통합전시관ActionCode
    {
        public const string 첫번째전시 = "ShowExhibit0";
        public const string 두번째전시 = "ShowExhibit1";
        public const string 세번째전시 = "ShowExhibit2";
        public const string 네번째전시 = "ShowExhibit3";
        public const string 다섯번째전시 = "ShowExhibit4";
        public const string 여섯번째전시 = "ShowExhibit5";
    }

    [DisallowMultipleComponent]
    public sealed class 통합전시관Presenter : MonoBehaviour
    {
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text summaryText = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text evidenceText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private Text footerText = null!;
        [SerializeField] private Button[] exhibitButtons = Array.Empty<Button>();
        [SerializeField] private Renderer[] zoneBeacons = Array.Empty<Renderer>();
        [SerializeField] private string initialExhibitStableId = "exhibit:city:food-delivery";

        private 통합전시관Snapshot snapshot = null!;
        private 통합전시관SimulationSessionState? simulationSession;
        private string selectedExhibitStableId = string.Empty;
        private bool listenersBound;

        public int 전시수 => snapshot?.Exhibits.Length ?? 0;
        public string 선택ExhibitStableId => selectedExhibitStableId;
        public string ManifestRevision => snapshot?.Revision ?? string.Empty;
        public long SimulationSessionRevision => simulationSession?.Revision ?? -1;
        public bool 운영Command제공여부 => false;

        public void Configure(
            Text title,
            Text summary,
            Text state,
            Text detail,
            Text evidence,
            Text boundary,
            Text footer,
            Button[] buttons,
            Renderer[] beacons)
        {
            titleText = title;
            summaryText = summary;
            stateText = state;
            detailText = detail;
            evidenceText = evidence;
            boundaryText = boundary;
            footerText = footer;
            exhibitButtons = buttons ?? Array.Empty<Button>();
            zoneBeacons = beacons ?? Array.Empty<Renderer>();
        }

        private void Start() => Initialize();

        public void Initialize()
        {
            simulationSession = null;
            InitializeSnapshot(new 통합전시관Mapper().Map(
                통합전시관FixtureApiModelFactory.CreateFixtureApiModel()));
        }

        public void Initialize(통합전시관Snapshot source)
        {
            simulationSession = null;
            InitializeSnapshot(source);
        }

        public void Initialize(통합전시관ServerBoundSnapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Validate();
            simulationSession = source.Session;
            InitializeSnapshot(source.Snapshot);
        }

        private void InitializeSnapshot(통합전시관Snapshot source)
        {
            ValidateWiring();
            snapshot = source ?? throw new ArgumentNullException(nameof(source));
            if (snapshot.Exhibits == null || snapshot.Exhibits.Length == 0)
                throw new InvalidOperationException("IntegratedExhibitionSnapshotEmpty");

            BindListeners();
            if (!string.IsNullOrWhiteSpace(initialExhibitStableId)
                && snapshot.Exhibits.Any(value => value.ExhibitStableId == initialExhibitStableId))
                SelectExhibit(initialExhibitStableId);
            else
                SelectVisible(0);
        }

        private void OnDestroy()
        {
            if (!listenersBound) return;
            foreach (var button in exhibitButtons) button.onClick.RemoveAllListeners();
        }

        public void Execute(string actionCode)
        {
            switch (actionCode)
            {
                case 통합전시관ActionCode.첫번째전시:
                    SelectVisible(0);
                    break;
                case 통합전시관ActionCode.두번째전시:
                    SelectVisible(1);
                    break;
                case 통합전시관ActionCode.세번째전시:
                    SelectVisible(2);
                    break;
                case 통합전시관ActionCode.네번째전시:
                    SelectVisible(3);
                    break;
                case 통합전시관ActionCode.다섯번째전시:
                    SelectVisible(4);
                    break;
                case 통합전시관ActionCode.여섯번째전시:
                    SelectVisible(5);
                    break;
                default:
                    SelectExhibit(actionCode);
                    break;
            }
        }

        public void SelectExhibit(string exhibitStableId)
        {
            if (snapshot == null) throw new InvalidOperationException("IntegratedExhibitionNotInitialized");
            if (snapshot.Exhibits.All(value => value.ExhibitStableId != exhibitStableId))
                throw new InvalidOperationException("IntegratedExhibitionSelectionUnknown:" + exhibitStableId);
            selectedExhibitStableId = exhibitStableId;
            Render();
        }

        public void ValidateWiring()
        {
            if (titleText == null || summaryText == null || stateText == null
                || detailText == null || evidenceText == null || boundaryText == null
                || footerText == null || exhibitButtons == null || exhibitButtons.Length != 6
                || exhibitButtons.Any(value => value == null)
                || zoneBeacons == null || zoneBeacons.Length != 6
                || zoneBeacons.Any(value => value == null))
                throw new InvalidOperationException("IntegratedExhibitionPresenterWiringMissing");
        }

        private void SelectVisible(int index)
        {
            if (snapshot == null) throw new InvalidOperationException("IntegratedExhibitionNotInitialized");
            if (index < 0 || index >= snapshot.Exhibits.Length) return;
            SelectExhibit(snapshot.Exhibits[index].ExhibitStableId);
        }

        private void Render()
        {
            var selected = snapshot.Exhibits.Single(value =>
                value.ExhibitStableId == selectedExhibitStableId);
            titleText.text = "통합 모판·전시관 · EXH-5";
            summaryText.text = "모판 1 · 읽기 전시 1 · Simulation 계약 4 · 운영 실행 0\n"
                + "같은 manifest에서 근거·업무·표현을 보되 권위를 합치지 않습니다.";
            stateText.text = "DATA  " + StateLabel(selected.DataStateCode)
                + "    MODE  " + ModeLabel(selected.ExperienceModeCode)
                + "    STATE  " + CompletionLabel(selected.CompletionStateCode)
                + "    SCOPE  " + selected.AuthorizationScopeCode;

            for (var i = 0; i < exhibitButtons.Length; i++)
            {
                var exhibit = snapshot.Exhibits[i];
                exhibitButtons[i].GetComponentInChildren<Text>().text = exhibit.DisplayName + "\n"
                    + StateLabel(exhibit.DataStateCode) + " · " + ModeLabel(exhibit.ExperienceModeCode);
                exhibitButtons[i].image.color = exhibit.ExhibitStableId == selectedExhibitStableId
                    ? new Color(.86f, .57f, .18f, 1f)
                    : new Color(.1f, .16f, .18f, .96f);
                zoneBeacons[i].sharedMaterial.color = exhibit.ExhibitStableId == selectedExhibitStableId
                    ? new Color(1f, .66f, .2f)
                    : StateColor(exhibit.DataStateCode);
            }

            var source = selected.SourcePlan[0];
            var lineage = selected.WorkflowCheckpoints.Length == 0
                ? string.Join("\n", selected.CanonicalRecordRelations.Select(value =>
                    value.SourceStableId + "\n  " + value.RelationCode + " → " + value.TargetStableId
                    + "  [expected " + value.ExpectedTargetRevision + "]"))
                : selected.WorkflowCheckpoints[0].LineageStableId + "\n"
                  + string.Join(" → ", selected.WorkflowCheckpoints.Select(value =>
                      value.StateCode + "[" + DisclosureLabel(value.DisclosureScopeCode) + "]"
                      + (value.RequiresSeparateConfirmation ? "*" : string.Empty)))
                  + "\n* 별도 Confirm이 필요한 업무 경계";
            detailText.text = selected.DisplayName + "\n"
                + selected.ExhibitStableId + "\n\n"
                + "출처 계획\n" + source.SourceKey + "\n"
                + source.SourceStableId.Value + " @ " + source.SourceRevision + "\n\n"
                + "계보·checkpoint\n" + lineage;

            evidenceText.text = "독립 증거 축\n" + string.Join("\n", selected.Evidence.Select(value =>
                "[" + EvidenceLabel(value.StatusCode) + "] " + EvidenceKindLabel(value.EvidenceKindCode)
                + "  " + value.Reference));

            boundaryText.text = "허용된 상호작용\n"
                + string.Join(" · ", selected.AllowedInteractionIntentCodes)
                + "\n\n차단·한계\n"
                + (selected.BlockedReasonCodes.Length == 0
                    ? "명시적 차단 없음"
                    : string.Join("\n", selected.BlockedReasonCodes));
            footerText.text = selected.ExhibitKindCode == "FoodDeliveryLineage"
                ? "기사 후보는 전달 권역만 · 기사 수락 뒤 상세 · 전달 완료 ≠ 주문자 수령 확인 · 운영 Command 0"
                : selected.ExhibitKindCode == "OrdererGroupUrbanMarketLineage"
                    ? "개인 의향 비공개 · 집단화 Preview ≠ 참여 확정 · 판매가 ≠ KAMIS 관측 · 운영 Command 0"
                    : "관람 전용 · generic Confirm 없음 · 도착 ≠ 입고 ≠ 검수 ≠ 보관 · 운영 Command 0";
            RefreshTextGeometry();
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            for (var i = 0; i < exhibitButtons.Length; i++)
            {
                var index = i;
                exhibitButtons[i].onClick.AddListener(() => SelectVisible(index));
            }
            listenersBound = true;
        }

        private void RefreshTextGeometry()
        {
            foreach (var text in GetComponentsInChildren<Text>(true)) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }

        private static string StateLabel(string code)
            => code == 통합전시관DataStateCodes.Fixture ? "FIXTURE"
                : code == 통합전시관DataStateCodes.Uncollected ? "미수집"
                : code == 통합전시관DataStateCodes.Live ? "LIVE"
                : code == 통합전시관DataStateCodes.Cached ? "CACHED"
                : code == 통합전시관DataStateCodes.Failed ? "FAILED"
                : code;

        private static string ModeLabel(string code)
            => code == 통합전시관ExperienceModeCodes.Research ? "모판 연구"
                : code == 통합전시관ExperienceModeCodes.ReadOnly ? "읽기 전시"
                : code == 통합전시관ExperienceModeCodes.Simulation ? "Simulation"
                : "운영 인계";

        private static string CompletionLabel(string code)
            => code == 통합전시관CompletionStateCodes.Verified ? "검증됨"
                : code == 통합전시관CompletionStateCodes.Blocked ? "차단"
                : code == 통합전시관CompletionStateCodes.Linked ? "연결됨"
                : code == 통합전시관CompletionStateCodes.Promoted ? "승격됨"
                : "후보";

        private static string EvidenceLabel(string code)
            => code == 통합전시관EvidenceStatusCodes.Verified ? "확인"
                : code == 통합전시관EvidenceStatusCodes.Partial ? "부분"
                : code == 통합전시관EvidenceStatusCodes.NotApplicable ? "해당없음"
                : "미확인";

        private static string EvidenceKindLabel(string code)
            => code == 통합전시관EvidenceKindCodes.Code ? "코드"
                : code == 통합전시관EvidenceKindCodes.FocusedTest ? "집중 test"
                : code == 통합전시관EvidenceKindCodes.Runtime ? "Runtime"
                : "운영 연결";

        private static string DisclosureLabel(string code)
            => code == 통합전시관DisclosureScopeCodes.OwnerPrivate ? "본인 비공개"
                : code == 통합전시관DisclosureScopeCodes.PrivacySafeAggregate ? "개인정보 제거 집계"
                : code == 통합전시관DisclosureScopeCodes.OrdererPublic ? "주문자 공개"
                : code == 통합전시관DisclosureScopeCodes.MarketOperatorAuthorized ? "마트 운영자 전용"
                : code == 통합전시관DisclosureScopeCodes.RestaurantAuthorized ? "음식점 전용"
                : code == 통합전시관DisclosureScopeCodes.DriverCandidateApproximate ? "기사 후보 권역 축약"
                : code == 통합전시관DisclosureScopeCodes.AssignedDriverAuthorized ? "확정 기사 전용"
                : code;

        private static Color StateColor(string code)
            => code == 통합전시관DataStateCodes.Uncollected
                ? new Color(.78f, .25f, .2f)
                : code == 통합전시관DataStateCodes.Fixture
                    ? new Color(.2f, .55f, .72f)
                    : new Color(.35f, .62f, .38f);

    }
}
