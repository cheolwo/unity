using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 농장풍경CompositionAdapter
    {
        private static readonly string[] DetailTiers =
        {
            월드CompositionDetailTierCodes.World,
            월드CompositionDetailTierCodes.Zone,
            월드CompositionDetailTierCodes.Object,
        };

        public static IReadOnlyList<월드CompositionDescriptor> Adapt(
            농장풍경CompositionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            var descriptors = catalog.Entries.Select(Adapt).ToArray();
            월드CompositionContractValidator.Validate(descriptors);
            return descriptors;
        }

        private static 월드CompositionDescriptor Adapt(
            농장풍경CompositionCatalogEntry entry)
        {
            var view = entry.Prefab.GetComponent<농장풍경CompositionSetView>()
                ?? throw new InvalidOperationException(
                    "FarmCompositionViewMissing:" + entry.CompositionKey);
            var sockets = view.Sockets.Select(value =>
            {
                var contract = new 월드CompositionSocketContract();
                contract.Configure(
                    value.SocketCode,
                    ResolveCategory(value.SocketCode),
                    value.transform.localPosition,
                    value.transform.localEulerAngles);
                return contract;
            }).ToArray();

            var descriptor = new 월드CompositionDescriptor();
            descriptor.Configure(
                월드CompositionDescriptor.BuildKey(
                    월드CompositionPackCodes.Farm,
                    entry.SetName,
                    entry.VariantCode),
                entry.SetName,
                entry.VariantCode,
                월드CompositionPackCodes.Farm,
                월드CompositionSourceKinds.SyntyNestedPrefab,
                entry.Footprint,
                Vector2.one,
                true,
                false,
                false,
                월드CompositionJourneyKindCodes.None,
                DetailTiers.ToArray(),
                Array.Empty<월드CompositionConnectorContract>(),
                sockets);
            return descriptor;
        }

        private static string ResolveCategory(string socketCode)
        {
            if (socketCode == 농장풍경SocketCodes.실제감자밭)
                return 월드CompositionSocketCategoryCodes.SimulationTarget;
            if (socketCode == 농장풍경SocketCodes.농부)
                return 월드CompositionSocketCategoryCodes.Actor;
            if (socketCode == 농장풍경SocketCodes.차량)
                return 월드CompositionSocketCategoryCodes.Vehicle;
            if (socketCode == 농장풍경SocketCodes.농기계)
                return 월드CompositionSocketCategoryCodes.Implement;
            if (socketCode == 농장풍경SocketCodes.화물)
                return 월드CompositionSocketCategoryCodes.Cargo;
            if (socketCode == 농장풍경SocketCodes.상호작용)
                return 월드CompositionSocketCategoryCodes.Interaction;
            throw new InvalidOperationException("FarmCompositionSocketUnknown:" + socketCode);
        }
    }
}
