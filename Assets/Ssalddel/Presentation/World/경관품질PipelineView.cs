using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 경관품질PipelineView : MonoBehaviour
    {
        [SerializeField] private 경관RenderingProfile profile = new();
        [SerializeField] private Volume globalVolume = null!;
        [SerializeField] private Camera firstPersonCamera = null!;
        [SerializeField] private Camera playerCamera = null!;

        public string ProfileStableId => profile.ProfileStableId;
        public string RuleRevision => profile.RuleRevision;
        public string ProfileHashSha256 => profile == null
            ? string.Empty
            : 경관RenderingProfileHash.Compute(profile);
        public float ShadowDistance => profile.ShadowDistance;
        public int ShadowCascadeCount => profile.ShadowCascadeCount;
        public bool PresentationOnly => profile.PresentationOnly;

        public void Configure(
            경관RenderingProfile value, Volume volume, Camera firstPerson, Camera player)
        {
            profile = value;
            globalVolume = volume;
            firstPersonCamera = firstPerson;
            playerCamera = player;
        }

        public bool ValidateWiring()
            => profile != null && profile.Validate()
                && globalVolume != null && globalVolume.isGlobal
                && globalVolume.profile != null
                && firstPersonCamera != null
                && playerCamera != null
                && ProfileHashSha256.Length == 64
                && PresentationOnly;
    }
}
