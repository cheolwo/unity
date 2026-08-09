using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
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
