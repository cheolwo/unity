using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.Runtime.Configuration;
using Ssalddel.Unity.Runtime.Identity;

namespace Ssalddel.Unity.Runtime.Ledgers
{
    public sealed class UnityLedgerProjection
    {
        public string LedgerId { get; set; } = string.Empty;
        public string LedgerTypeCode { get; set; } = string.Empty;
        public string SubjectStableId { get; set; } = string.Empty;
        public string WorldObjectStableId { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string ViewerRoleCode { get; set; } = UnityRoleCodes.Community;
        public long Revision { get; set; }
        public string SourceCode { get; set; } = UnityDataSourceCodes.Live;
        public string[] AvailableActionCodes { get; set; } = Array.Empty<string>();
        public string[] EvidenceIds { get; set; } = Array.Empty<string>();

        public void Validate()
        {
            StableDataId.EnsureValid(LedgerId, nameof(LedgerId));
            StableDataId.EnsureValid(SubjectStableId, nameof(SubjectStableId));
            StableDataId.EnsureValid(WorldObjectStableId, nameof(WorldObjectStableId));
            if (string.IsNullOrWhiteSpace(LedgerTypeCode) || string.IsNullOrWhiteSpace(StatusCode))
            {
                throw new InvalidOperationException("원장 종류와 상태가 필요합니다.");
            }

            if (!UnityRoleCodes.IsSupported(ViewerRoleCode))
            {
                throw new InvalidOperationException("원장 projection의 조회 역할이 올바르지 않습니다.");
            }

            if (Revision < 0)
            {
                throw new InvalidOperationException("원장 revision은 음수일 수 없습니다.");
            }

            if (!UnityDataSourceCodes.IsSupported(SourceCode))
            {
                throw new InvalidOperationException("지원하지 않는 원장 source입니다.");
            }
        }
    }

    public interface IUnityLedgerProjectionRepository
    {
        Task<UnityLedgerProjection[]> ListVisibleAsync(
            UnitySessionSnapshot session,
            string worldId,
            CancellationToken cancellationToken);
    }
}
