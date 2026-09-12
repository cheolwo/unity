using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 역할CharacterVisualInstanceView : MonoBehaviour
    {
        [SerializeField] private WorldActorAppearanceProfile appearanceProfile = new();
        [SerializeField] private WorldCharacterAssignmentResult assignment = new();
        [SerializeField] private string areaRoleCode = string.Empty;
        [SerializeField] private 역할CharacterVisualCatalog catalog = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private GameObject prefabInstanceRoot = null!;
        [SerializeField] private bool presentationOnly = true;

        public WorldActorAppearanceProfile AppearanceProfile => appearanceProfile;
        public WorldCharacterAssignmentResult Assignment => assignment;
        public string AreaRoleCode => areaRoleCode;
        public Transform VisualRoot => visualRoot;
        public GameObject PrefabInstanceRoot => prefabInstanceRoot;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            WorldActorAppearanceProfile profile,
            WorldCharacterAssignmentResult value,
            string areaRole,
            역할CharacterVisualCatalog sourceCatalog,
            Transform root,
            GameObject instance)
        {
            appearanceProfile = profile;
            assignment = value;
            areaRoleCode = areaRole ?? string.Empty;
            catalog = sourceCatalog;
            visualRoot = root;
            prefabInstanceRoot = instance;
            presentationOnly = true;
        }

        public bool ValidateWiring()
        {
            if (appearanceProfile == null || !appearanceProfile.Validate()
                || assignment == null || !assignment.PresentationOnly
                || catalog == null || visualRoot == null || prefabInstanceRoot == null
                || !prefabInstanceRoot.transform.IsChildOf(visualRoot)
                || !presentationOnly)
                return false;
            var entry = catalog.Resolve(assignment.VisualKey);
            return entry.PresentationOnly
                && entry.AllowedActorRoleCodes.Contains(assignment.ActorRoleCode)
                && entry.AppearanceFamilyCodes.Contains(assignment.AppearanceFamilyCode)
                && entry.AllowedAreaRoleCodes.Contains(areaRoleCode);
        }
    }
}
