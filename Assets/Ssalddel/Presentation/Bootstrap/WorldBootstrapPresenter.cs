using Ssalddel.Unity.Application.Bootstrap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.Bootstrap
{
    public sealed class WorldBootstrapPresenter : MonoBehaviour
    {
        [SerializeField] private string worldId = string.Empty;
        [SerializeField] private string roleCode = string.Empty;
        [SerializeField] private string executionMode = string.Empty;
        [SerializeField] private long revision;
        [SerializeField] private int visibleLedgerCount;

        public void Apply(WorldBootstrapResult result)
        {
            if (result == null || result.Session == null || result.World == null)
            {
                throw new System.ArgumentNullException(nameof(result));
            }

            worldId = result.World.WorldId;
            roleCode = result.Session.RoleCode;
            executionMode = result.World.ExecutionMode;
            revision = result.World.Revision;
            visibleLedgerCount = result.Ledgers?.Length ?? 0;
        }
    }
}
