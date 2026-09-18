using System;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Campaigns;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 절기 운영 Campaign 화면 모델을 공식 World의 읽기 전용 View Socket으로
    /// 보관합니다. 실제 UI가 연결되지 않아도 상태 판독과 시험은 가능합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E5,
        "SimulationWorldShell에서 절기 운영 Campaign 국면과 원천 지연을 읽기 전용으로 표시한다.",
        Boundary = "Campaign 전이·업무 Command·GameObject 생성·상태 사본 저장을 수행하지 않는다.")]
    public sealed class 절기운영CampaignWorldView : MonoBehaviour,
        ISeasonalCampaignPresentationTarget
    {
        [SerializeField] private Text summaryText;
        [SerializeField] private Text sourceStatusText;
        [SerializeField] private string sessionStableId = string.Empty;
        [SerializeField] private string areaStableId = string.Empty;
        [SerializeField] private string campaignStableId = string.Empty;
        [SerializeField] private string currentPhaseCode = string.Empty;
        [SerializeField] private long campaignRevision;
        [SerializeField] private bool actionsDisabled;
        [SerializeField] private string sourceSummary = string.Empty;

        public string SessionStableId => sessionStableId;
        public string AreaStableId => areaStableId;
        public string CampaignStableId => campaignStableId;
        public string CurrentPhaseCode => currentPhaseCode;
        public long CampaignRevision => campaignRevision;
        public bool ActionsDisabled => actionsDisabled;
        public string SourceSummary => sourceSummary;

        public void Configure(Text summary, Text sourceStatus)
        {
            summaryText = summary;
            sourceStatusText = sourceStatus;
            Render();
        }

        public void ApplySeasonalCampaign(
            SeasonalCampaignScreenModel screenModel,
            SeasonalCampaignSourceDiagnostic[] sourceDiagnostics,
            SeasonalCampaignCompositionRevision[] compositionRevisionSet)
        {
            if (screenModel == null || !screenModel.PresentationOnly)
                throw new InvalidOperationException(
                    "SeasonalCampaignWorldViewAuthorityInvalid");
            sessionStableId = screenModel.SessionStableId ?? string.Empty;
            areaStableId = screenModel.AreaStableId ?? string.Empty;
            campaignStableId = screenModel.CampaignStableId ?? string.Empty;
            currentPhaseCode = screenModel.CurrentPhaseCode ?? string.Empty;
            campaignRevision = screenModel.CampaignRevision;
            actionsDisabled = screenModel.ActionsDisabled;
            sourceSummary = string.Join(" · ",
                (sourceDiagnostics ?? Array.Empty<
                    SeasonalCampaignSourceDiagnostic>())
                .OrderBy(value => value.SourceCode, StringComparer.Ordinal)
                .Select(value => value.SourceCode + ":"
                    + value.FreshnessCode));
            Render();
        }

        public void Clear()
        {
            sessionStableId = string.Empty;
            areaStableId = string.Empty;
            campaignStableId = string.Empty;
            currentPhaseCode = string.Empty;
            campaignRevision = 0;
            actionsDisabled = false;
            sourceSummary = string.Empty;
            Render();
        }

        private void Render()
        {
            if (summaryText != null)
                summaryText.text = string.IsNullOrWhiteSpace(campaignStableId)
                    ? "절기 운영 Campaign · 대기"
                    : "절기 운영 Campaign · " + currentPhaseCode
                      + " · r" + campaignRevision;
            if (sourceStatusText != null)
                sourceStatusText.text = sourceSummary;
        }
    }
}
