using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Perspectives;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.Unity.Transport;
using UnityEngine;
using VContainer;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class 도심물류센터SceneController : MonoBehaviour
    {
        private AuthorizedRoleProjectionQuery authorizedRoleQuery = null!;
        private RolePresentationPerspectiveCoordinator rolePresentationCoordinator = null!;
        private NpcMovementQueryUseCase npcMovementQuery = null!;
        private NpcMovementInterpreter npcMovementInterpreter = null!;
        private NpcMovementPresenter npcMovementPresenter = null!;
        private 도심물류센터View zoneView = null!;
        private UrbanLogisticsCenterPresentationQueryUseCase logisticsPresentationQuery = null!;
        private TruckMovementApplicator truckApplicator = null!;
        private CancellationTokenSource? lifetime;

        [Inject]
        public void Construct(
            AuthorizedRoleProjectionQuery roleQuery,
            RolePresentationPerspectiveCoordinator presentationCoordinator,
            NpcMovementQueryUseCase movementQuery,
            NpcMovementInterpreter movementInterpreter,
            NpcMovementPresenter movementPresenter,
            UrbanLogisticsCenterPresentationQueryUseCase presentationQuery,
            TruckMovementApplicator movementApplicator,
            도심물류센터View view)
        {
            authorizedRoleQuery = roleQuery;
            rolePresentationCoordinator = presentationCoordinator;
            npcMovementQuery = movementQuery;
            npcMovementInterpreter = movementInterpreter;
            npcMovementPresenter = movementPresenter;
            logisticsPresentationQuery = presentationQuery;
            truckApplicator = movementApplicator;
            zoneView = view;
        }

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            if (!zoneView.ValidateWiring())
            {
                Debug.LogError("도심물류센터 View wiring이 완료되지 않았습니다.", this);
                return;
            }

            try
            {
                var authorizedRole = await authorizedRoleQuery.ExecuteAsync(
                    new 역할관점조회Request
                    {
                        RequestedRoleCode = RolePerspectiveCodes.Transporter,
                        WorldZoneCode = WorldZoneCodes.UrbanLogisticsCenter,
                    }, lifetime!.Token);
                rolePresentationCoordinator.Apply(
                    authorizedRole,
                    zoneView.GetRolePresentationTargets(),
                    zoneView.GetRolePresentationInteractionSink());

                var movement = await npcMovementQuery.실행Async(
                    new NpcMovementQuery
                    {
                        WorldZoneCode = WorldZoneCodes.UrbanLogisticsCenter,
                    },
                    lifetime.Token);
                if (movement != null)
                {
                    var movementModel = npcMovementPresenter.Present(
                        npcMovementInterpreter.Interpret(movement));
                    var unresolved = zoneView.ApplyNpcMovement(movementModel);
                    if (unresolved.Length > 0)
                    {
                        Debug.LogWarning("표현할 NPC View가 없습니다: " + string.Join(", ", unresolved), this);
                    }
                }

                var logistics = await logisticsPresentationQuery.ExecuteAsync(lifetime.Token);
                zoneView.ApplyFacilityOverview(logistics.Facility);
                zoneView.ApplyTransportCorridor(logistics.Truck, truckApplicator);
            }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
