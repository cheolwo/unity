using System;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 실제E5AreaSetNetworkHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text networkStatusText = null!;
        [SerializeField] private Text activeAreaText = null!;
        [SerializeField] private Text interactionReadinessText = null!;
        [SerializeField] private Text regionalCausalityText = null!;

        public string ActiveAreaLabel => activeAreaText != null
            ? activeAreaText.text : string.Empty;

        public void Configure(Text networkStatus, Text activeArea,
            Text interactionReadiness, Text regionalCausality)
        {
            networkStatusText = networkStatus;
            activeAreaText = activeArea;
            interactionReadinessText = interactionReadiness;
            regionalCausalityText = regionalCausality;
        }

        public void Show(실제E5AreaSetNetworkStreamingBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            var graphCount = batch.Network.RouteGraphs.Length
                             + batch.AreaBatches.Sum(value =>
                                 value.AreaSet.LandscapeGraphs.Length);
            networkStatusText.text = "실제 E5 공간 · 4개 지역 / " + graphCount + "개 Graph"
                                     + " · " + batch.Network.EvidenceStageCode;
            activeAreaText.text = "현재 지역 · " + AreaLabel(batch.ActiveAreaSetStableId)
                                  + "  [1 Nature · 2 Farm · 3 Hub · 4 Town]";
            interactionReadinessText.text = "WI 공간 결속 · 직접 "
                                            + batch.InteractionReadiness.DirectBindings.Length
                                            + " / 문맥 "
                                            + batch.InteractionReadiness.ContextualBindings.Length
                                            + " / 비공간 "
                                            + batch.InteractionReadiness.NonSpatialBindings.Length
                                            + " · "
                                            + batch.InteractionReadiness.OverallStatusCode;
        }

        public void ShowRegionalCausality(실제E5RegionalCausalityData state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Validate();
            regionalCausalityText.text = "지역 인과 · 위협 " + state.ThreatScore
                                         + " / 회복 " + state.RecoveryScore
                                         + " / 결과 " + OutcomeLabel(state.OutcomeCode);
        }

        public void ShowUnavailable(string reason)
        {
            networkStatusText.text = "실제 E5 공간 · 연결 대기";
            activeAreaText.text = reason;
            interactionReadinessText.text = "WI 공간 결속 · 서버 재조회 필요";
        }

        private static string AreaLabel(string stableId)
        {
            if (stableId == 실제E5AreaSetNetworkCodes.NatureAreaSet) return "Nature 생활 거점";
            if (stableId == 실제E5AreaSetNetworkCodes.FarmAreaSet) return "Farm 생산";
            if (stableId == 실제E5AreaSetNetworkCodes.HubAreaSet) return "City/Hub 물류";
            if (stableId == 실제E5AreaSetNetworkCodes.TownAreaSet) return "Town 시장";
            return stableId;
        }

        private static string OutcomeLabel(string code)
        {
            if (code == "Opportunity") return "기회";
            if (code == "Threat") return "위협";
            if (code == "Recovery") return "회복";
            return "보통";
        }
    }
}
