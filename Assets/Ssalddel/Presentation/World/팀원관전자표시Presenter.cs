using System;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
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
