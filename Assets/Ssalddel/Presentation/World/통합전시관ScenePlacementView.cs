using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 통합전시관ScenePlacementView : MonoBehaviour
    {
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string sceneStableId = string.Empty;
        [SerializeField] private string zoneStableId = string.Empty;
        [SerializeField] private string placementProfileRevision = string.Empty;
        [SerializeField] private string sceneAnchorKey = string.Empty;
        [SerializeField] private string dataBindingKey = string.Empty;
        [SerializeField] private 통합전시관SeedbedObjectRoot objectRoot = null!;

        public string PlacementStableId => placementStableId;
        public string SceneStableId => sceneStableId;
        public string ZoneStableId => zoneStableId;
        public string PlacementProfileRevision => placementProfileRevision;
        public string SceneAnchorKey => sceneAnchorKey;
        public string DataBindingKey => dataBindingKey;
        public 통합전시관SeedbedObjectRoot ObjectRoot => objectRoot;

        public void Configure(
            string placementId,
            string targetSceneId,
            string targetZoneId,
            string profileRevision,
            string anchorKey,
            string bindingKey,
            통합전시관SeedbedObjectRoot seedbedObjectRoot)
        {
            placementStableId = placementId;
            sceneStableId = targetSceneId;
            zoneStableId = targetZoneId;
            placementProfileRevision = profileRevision;
            sceneAnchorKey = anchorKey;
            dataBindingKey = bindingKey;
            objectRoot = seedbedObjectRoot;
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(placementStableId)
               && !string.IsNullOrWhiteSpace(sceneStableId)
               && !string.IsNullOrWhiteSpace(zoneStableId)
               && !string.IsNullOrWhiteSpace(placementProfileRevision)
               && !string.IsNullOrWhiteSpace(sceneAnchorKey)
               && !string.IsNullOrWhiteSpace(dataBindingKey)
               && objectRoot != null
               && objectRoot.ValidateWiring()
               && objectRoot.transform.parent == transform;
    }
}
