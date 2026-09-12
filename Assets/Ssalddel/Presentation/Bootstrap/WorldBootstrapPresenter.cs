using Ssalddel.Unity.Application.Bootstrap;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.Bootstrap
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
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
