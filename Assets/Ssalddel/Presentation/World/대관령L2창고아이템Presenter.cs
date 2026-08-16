using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 대관령 L2 Barn 내부의 Simulation 재고 상태 사본을 Unity 상호작용에 연결한다.
    /// 아이템 획득 성공은 애니메이션이 아니라 서버 Confirm 뒤 최신 원장 재조회로만 확정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 대관령L2창고아이템Presenter : MonoBehaviour
    {
        [SerializeField] private string tileKey = 대관령L2창고아이템Codes.TileKey;
        [SerializeField] private string buildingStableId =
            대관령L2창고아이템Codes.BuildingStableId;
        [SerializeField] private string playerStableId =
            대관령L2창고아이템Codes.PlayerStableId;
        [SerializeField] private bool presentationOnly = true;

        private 대관령L2창고아이템Coordinator coordinator;
        private CancellationTokenSource lifetimeCancellation;
        private int commandSequence;

        public event Action 상태사본Changed = delegate { };
        public 대관령L2창고InventorySnapshot Current => coordinator?.Current;
        public 대관령L2아이템획득PreviewSnapshot Preview => coordinator?.Preview;
        public bool IsReady => Current != null && !IsBusy;
        public bool IsBusy { get; private set; }
        public bool PresentationOnly => presentationOnly;
        public string TileKey => tileKey;
        public string BuildingStableId => buildingStableId;
        public string PlayerStableId => playerStableId;

        public void ConfigureIdentity(string tile, string building, string player)
        {
            tileKey = tile;
            buildingStableId = building;
            playerStableId = player;
            ValidateWiring();
        }

        public bool ValidateWiring()
            => tileKey == 대관령L2창고아이템Codes.TileKey
               && buildingStableId == 대관령L2창고아이템Codes.BuildingStableId
               && playerStableId == 대관령L2창고아이템Codes.PlayerStableId
               && presentationOnly;

        public async Task InitializeAsync(
            I대관령L2창고아이템AuthorityClient authority,
            string sessionStableId)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryPresenterWiringInvalid");
            coordinator = new 대관령L2창고아이템Coordinator(authority);
            RenewCancellation();
            await Run(() => coordinator.LoadAsync(sessionStableId, Token()));
        }

        public async Task PreviewOneAsync()
            => await Run(() => coordinator.PreviewOneAsync(Token()));

        public async Task ConfirmAsync()
        {
            var commandId = "command:unity:daegwallyeong-item-acquire:"
                            + (++commandSequence);
            await Run(() => coordinator.ConfirmAsync(commandId, Token()));
        }

        public void CancelPreview()
        {
            if (coordinator == null || IsBusy) return;
            coordinator.ClearPreview();
            상태사본Changed();
        }

        public string 상태요약()
        {
            if (Current == null) return "대관령 창고 상태를 불러오는 중";
            var stack = Current.RequiredItemStack(
                대관령L2창고아이템Codes.ItemStackStableId);
            var held = Current.PlayerQuantity(playerStableId, stack.ItemCode);
            return "대관령 L2 · Barn 내부 · " + stack.KoreanName
                   + " · 팔레트 " + stack.Quantity + stack.UnitCode
                   + " · 플레이어 " + held + stack.UnitCode
                   + " · r" + Current.WorldRevision;
        }

        private async Task Run(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (IsBusy || coordinator == null)
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryPresenterNotReady");
            IsBusy = true;
            try
            {
                await action();
            }
            catch (Exception error)
            {
                coordinator.MarkStale(error);
                throw;
            }
            finally
            {
                IsBusy = false;
                상태사본Changed();
            }
        }

        private void RenewCancellation()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();
        }

        private CancellationToken Token()
            => lifetimeCancellation?.Token ?? CancellationToken.None;

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }
    }
}
