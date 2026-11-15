using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.WindowsGame.Kernel;

namespace Ssalddel.WindowsGame.SimulationBridge
{
    /// <summary>
    /// 큰 Simulation의 가맹 공급망을 게임의 공급망 문으로 잇는 어댑터.
    /// 경영SimulationSessionAggregate를 게임 안에 하나 열고, 이미 있는 네 단계
    /// (등록 → 조달계약 → 매장 공급안 → 매장 발주)를 미리보기·확정 그대로 부른다. 규칙은 한 줄도 옮겨 적지 않는다.
    /// Simulation 패키지가 프로젝트에 있을 때만 컴파일된다(HONGDAL_SIMULATION_DOMAIN).
    /// 게임은 이 형식을 이름으로 찾는다. 없으면 대역(LocalFranchiseSupplyPort)을 쓴다.
    /// </summary>
    public sealed class SessionFranchiseSupplyPort : IFranchiseSupplyPort
    {
        private const string ContractId = "franchise-contract:hongdal-milk";
        private const string ContractItemId = "franchise-contract-item:hongdal-milk-1l";
        private const string OfferId = "franchise-offer:hongdal-cafe-standard";
        private const string OfferItemId = "franchise-offer-item:hongdal-milk-1l";

        private 경영SimulationSessionAggregate session;
        private FirstChainPack pack;
        private string[] sources = new string[0];

        public string Kind { get { return "SessionAggregate"; } }

        public void Initialize(FirstChainPack source, long seed)
        {
            pack = source ?? throw new ArgumentNullException(nameof(source));
            sources = new[] { "pack:" + pack.id };
            var requestId = new byte[16];
            BitConverter.GetBytes(seed).CopyTo(requestId, 0);
            requestId[15] = 0x71;      // 씨앗이 0이어도 빈 번호가 되지 않게 한다
            session = new 경영SimulationSessionAggregate(new 경영SimulationSession생성Request
            {
                ClientRequestId = new Guid(requestId),
                ScenarioStableId = "scenario:hongdal-franchise-first-chain",
                ScenarioDataRevision = pack.version,
                ScenarioSeed = (int)(seed & 0x7fffffff),
                RuleRevision = 프랜차이즈공급망SimulationContractCodes.RuleRevision,
                DurationTicks = 28,
                WorldContext = new SimulationWorldContext생성Request
                {
                    FactionStableId = "faction:hongdal",
                    TerritoryStableId = "territory:seoul-jungnang",
                    SettlementStableId = "settlement:sagajeong",
                    GameDateStartsOn = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                },
            });

            var network = pack.network;
            Run(new 프랜차이즈공급망초기화PreviewRequest
            {
                ExpectedRevision = session.Revision,
                NetworkStableId = network.id,
                DataRevision = pack.version,
                Headquarters = new 프랜차이즈본부SimulationInput
                {
                    HeadquartersStableId = network.headquarters.id,
                    HeadquartersCode = network.headquarters.code,
                    DisplayName = network.headquarters.name,
                },
                Stores = new[]
                {
                    new 프랜차이즈매장SimulationInput
                    {
                        StoreStableId = network.store.id,
                        StoreCode = network.store.code,
                        DisplayName = network.store.name,
                        SemanticPlaceStableId = network.store.placeId,
                        MembershipStableId = network.store.membershipId,
                    },
                },
                Suppliers = new[]
                {
                    new 프랜차이즈공급자SimulationInput
                    {
                        SupplierStableId = network.supplier.id,
                        SupplierKey = network.supplier.code,
                        DisplayName = network.supplier.name,
                    },
                },
                SourceStableIds = sources,
            }, session.PreviewFranchiseSupplyInitialization, session.ConfirmFranchiseSupplyInitialization, "hongdal-franchise-initialization");

            Run(new 프랜차이즈조달계약PreviewRequest
            {
                ExpectedRevision = session.Revision,
                ContractStableId = ContractId,
                HeadquartersStableId = network.headquarters.id,
                SupplierStableId = network.supplier.id,
                ContractNumber = "HD-SRC-0001",
                ContractDocumentVersion = pack.version,
                EffectiveFromWorldTick = 0,
                CurrencyCode = "KRW",
                Items = new[]
                {
                    new 프랜차이즈조달계약품목SimulationInput
                    {
                        ContractItemStableId = ContractItemId,
                        ProductStableId = pack.item.productId,
                        SupplierSku = "NYJ-MILK-1L",
                        ItemName = pack.item.name,
                        OrderUnitCode = pack.item.orderUnit,
                        PackageContentQuantity = pack.item.contentQuantity,
                        PackageContentUnitCode = pack.item.contentUnit,
                        ConversionRuleRevision = 프랜차이즈공급망SimulationContractCodes.UnitConversionRevision,
                        UnitPrice = pack.item.supplyPrice,
                        MinimumOrderQuantity = pack.item.minimum,
                        MaximumOrderQuantity = pack.item.maximum,
                        StorageConditionCode = pack.item.storage,
                        SourceStableIds = sources,
                    },
                },
                SourceStableIds = sources,
            }, session.PreviewFranchiseSourcingAgreement, session.ConfirmFranchiseSourcingAgreement, "hongdal-franchise-sourcing-agreement");

            Run(new 프랜차이즈매장공급안PreviewRequest
            {
                ExpectedRevision = session.Revision,
                OfferStableId = OfferId,
                HeadquartersStableId = network.headquarters.id,
                OfferNumber = "HD-OFFER-0001",
                OfferDocumentVersion = pack.version,
                EffectiveFromWorldTick = 0,
                CurrencyCode = "KRW",
                Items = new[]
                {
                    new 프랜차이즈매장공급안품목SimulationInput
                    {
                        OfferItemStableId = OfferItemId,
                        SourceContractStableId = ContractId,
                        SourceContractItemStableId = ContractItemId,
                        StoreSku = "CAFE-MILK-1L",
                        UnitPrice = pack.item.storePrice,
                        MinimumOrderQuantity = pack.item.minimum,
                        MaximumOrderQuantity = pack.item.maximum,
                        SourceStableIds = sources,
                    },
                },
                SourceStableIds = sources,
            }, session.PreviewFranchiseStoreSupplyOffer, session.ConfirmFranchiseStoreSupplyOffer, "hongdal-franchise-store-offer");
        }

        public SupplyOrderPreview PreviewOrder(string orderId, string orderNumber, decimal quantity)
        {
            var result = new SupplyOrderPreview
            {
                ContentQuantity = quantity * pack.item.contentQuantity,
                Amount = quantity * pack.item.storePrice,
            };
            try
            {
                var preview = session.PreviewFranchiseStoreOrder(OrderRequest(orderId, orderNumber, quantity));
                result.CanConfirm = preview.CanConfirm;
                result.BlockReason = preview.BlockReasonCodes.FirstOrDefault() ?? string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                // 계약·충돌 예외의 메시지가 차단 사유 코드다.
                result.CanConfirm = false;
                result.BlockReason = exception.Message;
            }
            return result;
        }

        public SupplyOrderReceipt ConfirmOrder(string orderId, string orderNumber, decimal quantity, string commandId)
        {
            var existing = FindOrder(session.GetFranchiseSupplyState(), orderId);
            if (existing != null) return Receipt(existing);
            try
            {
                var state = Run(OrderRequest(orderId, orderNumber, quantity), session.PreviewFranchiseStoreOrder,
                    session.ConfirmFranchiseStoreOrder, commandId);
                var order = FindOrder(state, orderId);
                return order == null ? new SupplyOrderReceipt { Ok = false, Note = "FranchiseSupplyStoreOrderNotFound" } : Receipt(order);
            }
            catch (InvalidOperationException exception)
            {
                return new SupplyOrderReceipt { Ok = false, Note = exception.Message };
            }
        }

        private 프랜차이즈매장발주PreviewRequest OrderRequest(string orderId, string orderNumber, decimal quantity)
        {
            return new 프랜차이즈매장발주PreviewRequest
            {
                ExpectedRevision = session.Revision,
                OrderStableId = orderId,
                StoreMembershipStableId = pack.network.store.membershipId,
                OfferStableId = OfferId,
                OrderNumber = orderNumber,
                DeliverySiteStableId = pack.network.store.placeId,
                RequestedDeliveryWorldTick = 1,
                Lines = new[]
                {
                    new 프랜차이즈매장발주품목SimulationInput
                    {
                        OrderLineStableId = orderId + ":line-1",
                        OfferItemStableId = OfferItemId,
                        RequestedOrderUnitQuantity = quantity,
                    },
                },
                SourceStableIds = sources,
            };
        }

        private static 프랜차이즈매장발주Snapshot FindOrder(프랜차이즈공급망StateSnapshot state, string orderId)
        {
            return state == null ? null : state.StoreOrders.FirstOrDefault(order => order.OrderStableId == orderId);
        }

        private static SupplyOrderReceipt Receipt(프랜차이즈매장발주Snapshot order)
        {
            return new SupplyOrderReceipt { Ok = true, Status = order.StatusCode, Amount = order.TotalAmountSnapshot };
        }

        // 미리보기에서 받은 해시를 그대로 확정에 넣는다. 미리보기가 막히면 그 사유로 멈춘다.
        private static 프랜차이즈공급망StateSnapshot Run<T>(T request,
            Func<T, 프랜차이즈공급망PreviewSnapshot> preview,
            Func<프랜차이즈공급망CommandConfirmRequest<T>, 프랜차이즈공급망StateSnapshot> confirm,
            string commandId)
        {
            var previewed = preview(request);
            if (!previewed.CanConfirm)
                throw new InvalidOperationException(previewed.BlockReasonCodes.FirstOrDefault() ?? "FranchiseSupplyCommandBlocked");
            return confirm(new 프랜차이즈공급망CommandConfirmRequest<T>
            {
                ClientRequestId = commandId,
                ExpectedPreviewHash = previewed.PreviewHash,
                PreviewRequest = request,
            });
        }
    }
}
