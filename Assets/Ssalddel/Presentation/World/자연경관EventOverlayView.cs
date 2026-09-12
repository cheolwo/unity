using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 자연경관EventOverlayView : MonoBehaviour
    {
        [SerializeField] private string presentationKey = string.Empty;
        [SerializeField] private string overlayName = string.Empty;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private bool eventOnly = true;
        [SerializeField] private bool presentationOnly = true;

        public string PresentationKey => presentationKey;
        public string OverlayName => overlayName;
        public Transform VisualRoot => visualRoot;
        public bool EventOnly => eventOnly;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string key, string name, Transform root)
        {
            presentationKey = key ?? string.Empty;
            overlayName = name ?? string.Empty;
            visualRoot = root;
            eventOnly = true;
            presentationOnly = true;
        }

        public bool ValidateWiring()
            => 자연경관EventPresentationKeys.OverlayKeys.Contains(
                    presentationKey, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(overlayName)
                && visualRoot != null && visualRoot.IsChildOf(transform)
                && visualRoot.childCount >= 3 && eventOnly && presentationOnly;
    }
}
