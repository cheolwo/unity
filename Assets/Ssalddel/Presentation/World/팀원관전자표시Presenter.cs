using System;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 팀원관전자표시Presenter : MonoBehaviour
    {
        [SerializeField] private Text label = null!;
        [SerializeField] private bool presentationOnly = true;

        public int ActiveObserverCount { get; private set; }
        public bool IsVisible => label != null && label.gameObject.activeSelf;

        public void Configure(Text indicatorLabel)
        {
            label = indicatorLabel;
            presentationOnly = true;
            Clear();
        }

        public void Apply(TeamObserverIndicatorApiModel source)
        {
            if (label == null || source == null || !source.PresentationOnly
                || source.ActiveObserverCount < 0
                || source.ObserverActorStableIds == null
                || source.ActiveObserverCount
                    != source.ObserverActorStableIds.Length)
                throw new InvalidOperationException(
                    "TeamObserverIndicatorBoundaryInvalid");
            ActiveObserverCount = source.ActiveObserverCount;
            label.text = $"팀원 {ActiveObserverCount}명이 관전 중";
            label.gameObject.SetActive(source.ShowIndicator
                                       && ActiveObserverCount > 0);
        }

        public void Clear()
        {
            ActiveObserverCount = 0;
            if (label == null) return;
            label.text = string.Empty;
            label.gameObject.SetActive(false);
        }
    }
}
