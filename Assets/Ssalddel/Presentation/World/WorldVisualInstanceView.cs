using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class WorldVisualInstanceView : MonoBehaviour
    {
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private WorldVisualCatalog sourceCatalog = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private GameObject prefabInstanceRoot = null!;

        public string VisualKey => visualKey;
        public WorldVisualCatalog SourceCatalog => sourceCatalog;
        public Transform VisualRoot => visualRoot;
        public GameObject PrefabInstanceRoot => prefabInstanceRoot;

        public void Configure(
            string key,
            WorldVisualCatalog catalog,
            Transform root,
            GameObject instance)
        {
            visualKey = key;
            sourceCatalog = catalog;
            visualRoot = root;
            prefabInstanceRoot = instance;
        }

        public bool ValidateWiring()
        {
            if (!WorldVisualKeys.IsKnown(visualKey) || sourceCatalog == null
                || visualRoot == null || prefabInstanceRoot == null
                || !prefabInstanceRoot.transform.IsChildOf(visualRoot))
            {
                return false;
            }

            try
            {
                return sourceCatalog.Resolve(visualKey).Prefab != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
