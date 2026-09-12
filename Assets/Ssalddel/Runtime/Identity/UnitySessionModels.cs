using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.Runtime.Configuration;

namespace Ssalddel.Unity.Runtime.Identity
{
    public static class UnityRoleCodes
    {
        public const string Community = "Community";
        public const string Orderer = "Orderer";
        public const string Seller = "Seller";
        public const string Shipper = "Shipper";
        public const string CargoDriver = "CargoDriver";
        public const string FoodDeliveryDriver = "FoodDeliveryDriver";
        public const string WarehouseManager = "WarehouseManager";
        public const string RestaurantOperator = "RestaurantOperator";

        public static bool IsSupported(string value)
            => value == Community
               || value == Orderer
               || value == Seller
               || value == Shipper
               || value == CargoDriver
               || value == FoodDeliveryDriver
               || value == WarehouseManager
               || value == RestaurantOperator;
    }

    public sealed class UnitySessionSnapshot
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserStableId { get; set; } = string.Empty;
        public string RoleCode { get; set; } = UnityRoleCodes.Community;
        public string OrganizationStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string SourceCode { get; set; } = UnityDataSourceCodes.Live;

        public void Validate()
        {
            StableDataId.EnsureValid(SessionId, nameof(SessionId));
            StableDataId.EnsureValid(UserStableId, nameof(UserStableId));
            if (!string.IsNullOrWhiteSpace(OrganizationStableId))
            {
                StableDataId.EnsureValid(OrganizationStableId, nameof(OrganizationStableId));
            }

            if (!UnityRoleCodes.IsSupported(RoleCode))
            {
                throw new InvalidOperationException("지원하지 않는 Unity 역할입니다.");
            }

            if (Revision < 0)
            {
                throw new InvalidOperationException("Session revision은 음수일 수 없습니다.");
            }

            if (!UnityDataSourceCodes.IsSupported(SourceCode))
            {
                throw new InvalidOperationException("지원하지 않는 session source입니다.");
            }
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface IUnitySessionRepository
    {
        Task<UnitySessionSnapshot> LoadCurrentAsync(CancellationToken cancellationToken);
    }
}
