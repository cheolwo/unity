using System;
using Ssalddel.Unity.Npcs;
using UnityEngine;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    public sealed class CargoWarehouseHandoffView
        : MonoBehaviour, ICargoWarehouseHandoffTarget
    {
        [SerializeField]
        private WorldNpcMovementRouter movementRouter = null!;

        [SerializeField]
        private GameObject inTransitCargoRoot = null!;

        [SerializeField]
        private GameObject inboundDockCargoRoot = null!;

        [SerializeField]
        private GameObject storageCargoRoot = null!;

        [SerializeField]
        private TextMesh stateLabel = null!;

        public void Configure(
            WorldNpcMovementRouter router,
            GameObject transitCargo,
            GameObject dockCargo,
            GameObject storedCargo,
            TextMesh label)
        {
            movementRouter = router;
            inTransitCargoRoot = transitCargo;
            inboundDockCargoRoot = dockCargo;
            storageCargoRoot = storedCargo;
            stateLabel = label;
        }

        public void ApplyHandoff(CargoWarehouseHandoffSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var unresolved = movementRouter.Apply(snapshot.Movements);
            if (unresolved.Length > 0)
            {
                throw new InvalidOperationException(
                    "화물 인계 NPC View를 찾을 수 없습니다: " + string.Join(",", unresolved));
            }

            var inTransit = string.Equals(
                snapshot.HandoffStateCode,
                CargoHandoffStateCodes.InTransit,
                StringComparison.Ordinal);
            var atDock = string.Equals(
                snapshot.HandoffStateCode,
                CargoHandoffStateCodes.ArrivedAtWarehouse,
                StringComparison.Ordinal);
            var stored = string.Equals(
                snapshot.HandoffStateCode,
                CargoHandoffStateCodes.ReceivingCompleted,
                StringComparison.Ordinal);

            inTransitCargoRoot.SetActive(inTransit);
            inboundDockCargoRoot.SetActive(atDock);
            storageCargoRoot.SetActive(stored);
            stateLabel.text = snapshot.HandoffStateCode + "\n" + snapshot.CargoStableId;
        }

        public bool ValidateWiring()
        {
            return movementRouter != null
                && movementRouter.ValidateWiring()
                && inTransitCargoRoot != null
                && inboundDockCargoRoot != null
                && storageCargoRoot != null
                && stateLabel != null;
        }
    }
}
