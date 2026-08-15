using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class WorldActorRoleCodes
    {
        public const string FarmerProducer = "farmer-producer";
        public const string Shipper = "shipper";
        public const string WarehouseOperator = "warehouse-operator";
        public const string TransportOperator = "transport-operator";
        public const string FreightDeliveryDriver = "freight-delivery-driver";
        public const string FoodDeliveryDriver = "food-delivery-driver";
        public const string Seller = "seller";
        public const string Orderer = "orderer";
        public const string Unresolved = "unresolved";

        public static IReadOnlyList<string> Playable { get; } = new[]
        {
            FarmerProducer, Shipper, WarehouseOperator, TransportOperator,
            FreightDeliveryDriver, FoodDeliveryDriver, Seller, Orderer,
        };

        public static bool IsKnown(string value)
            => value == Unresolved || Playable.Contains(value, StringComparer.Ordinal);
    }

    public static class WorldActorWorkflowContextCodes
    {
        public const string General = "general";
        public const string Farm = "farm";
        public const string Warehouse = "warehouse";
        public const string FreightDelivery = "freight-delivery";
        public const string FoodDelivery = "food-delivery";
        public const string MarketOrder = "market-order";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            General, Farm, Warehouse, FreightDelivery, FoodDelivery, MarketOrder,
        };

        public static bool IsKnown(string value)
            => All.Contains(value, StringComparer.Ordinal);
    }

    public static class WorldActorAppearanceFamilyCodes
    {
        public const string AdultA = "adult-a";
        public const string AdultB = "adult-b";
        public const string AdultSeniorA = "adult-senior-a";
        public const string Neutral = "adult-neutral";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            AdultA, AdultB, AdultSeniorA, Neutral,
        };

        public static bool IsKnown(string value)
            => All.Contains(value, StringComparer.Ordinal);
    }

    public static class WorldCharacterVisualKeys
    {
        public const string FarmWorkerA = "character.farm.worker.a";
        public const string FarmWorkerB = "character.farm.worker.b";
        public const string FarmWorkerSeniorA = "character.farm.worker.senior-a";
        public const string BusinessOperatorA = "character.business.operator.a";
        public const string BusinessOperatorB = "character.business.operator.b";
        public const string BusinessOperatorC = "character.business.operator.c";
        public const string LogisticsWorkerA = "character.logistics.worker.a";
        public const string LogisticsWorkerB = "character.logistics.worker.b";
        public const string CommunityCitizenA = "character.community.citizen.a";
        public const string CommunityCitizenB = "character.community.citizen.b";
        public const string CommunityCitizenC = "character.community.citizen.c";
        public const string CommunityCitizenD = "character.community.citizen.d";
        public const string TownResidentA = "character.town.resident.a";
        public const string TownResidentB = "character.town.resident.b";
        public const string TownResidentC = "character.town.resident.c";
        public const string TownResidentD = "character.town.resident.d";
        public const string TownSellerA = "character.town.seller.a";
        public const string NeutralAdultA = "character.neutral.adult.a";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            FarmWorkerA, FarmWorkerB, FarmWorkerSeniorA,
            BusinessOperatorA, BusinessOperatorB, BusinessOperatorC,
            LogisticsWorkerA, LogisticsWorkerB,
            CommunityCitizenA, CommunityCitizenB, CommunityCitizenC, CommunityCitizenD,
            TownResidentA, TownResidentB, TownResidentC, TownResidentD,
            TownSellerA, NeutralAdultA,
        };

        public static bool IsKnown(string value)
            => All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class WorldActorRoleNormalizationResult
    {
        public string SourceRoleCode = string.Empty;
        public string WorkflowContextCode = string.Empty;
        public string ActorRoleCode = WorldActorRoleCodes.Unresolved;
        public string DiagnosticCode = string.Empty;

        public bool IsResolved => ActorRoleCode != WorldActorRoleCodes.Unresolved;
    }

    public static class WorldActorRoleNormalizer
    {
        public static WorldActorRoleNormalizationResult Normalize(
            string? sourceRoleCode,
            string? workflowContextCode)
        {
            var source = sourceRoleCode?.Trim() ?? string.Empty;
            var context = string.IsNullOrWhiteSpace(workflowContextCode)
                ? WorldActorWorkflowContextCodes.General
                : workflowContextCode!.Trim();
            if (!WorldActorWorkflowContextCodes.IsKnown(context))
                context = WorldActorWorkflowContextCodes.General;

            var role = source switch
            {
                "농부" or "생산자" => WorldActorRoleCodes.FarmerProducer,
                "화주" => WorldActorRoleCodes.Shipper,
                "창고관리자" or "보세창고운영자" or "풀필먼트운영자"
                    => WorldActorRoleCodes.WarehouseOperator,
                "보세운송사" or "택배운송사" => WorldActorRoleCodes.TransportOperator,
                "용달기사" => WorldActorRoleCodes.FreightDeliveryDriver,
                "배달기사" => WorldActorRoleCodes.FoodDeliveryDriver,
                "판매자" or "음식점" => WorldActorRoleCodes.Seller,
                "주문자" or "orderer" => WorldActorRoleCodes.Orderer,
                "기사" when context == WorldActorWorkflowContextCodes.FreightDelivery
                    => WorldActorRoleCodes.FreightDeliveryDriver,
                "기사" when context == WorldActorWorkflowContextCodes.FoodDelivery
                    => WorldActorRoleCodes.FoodDeliveryDriver,
                _ => WorldActorRoleCodes.Unresolved,
            };

            return new WorldActorRoleNormalizationResult
            {
                SourceRoleCode = source,
                WorkflowContextCode = context,
                ActorRoleCode = role,
                DiagnosticCode = role == WorldActorRoleCodes.Unresolved
                    ? "actor.role-unresolved" : string.Empty,
            };
        }
    }

    [Serializable]
    public sealed class WorldActorAppearanceProfile
    {
        public string ActorStableId = string.Empty;
        public string SelectedAppearanceFamilyCode = WorldActorAppearanceFamilyCodes.Neutral;
        public bool ExplicitlySelected;
        public bool PresentationOnly = true;

        public bool Validate()
            => !string.IsNullOrWhiteSpace(ActorStableId)
                && WorldActorAppearanceFamilyCodes.IsKnown(SelectedAppearanceFamilyCode)
                && PresentationOnly;
    }

    [Serializable]
    public sealed class WorldCharacterAssignmentCandidate
    {
        public string VisualKey = string.Empty;
        public string[] AllowedActorRoleCodes = Array.Empty<string>();
        public string[] AppearanceFamilyCodes = Array.Empty<string>();
        public int Weight = 1;
        public bool PlayerEligible = true;
        public bool PresentationOnly = true;

        public bool Validate()
            => WorldCharacterVisualKeys.IsKnown(VisualKey)
                && AllowedActorRoleCodes.Length > 0
                && AllowedActorRoleCodes.All(WorldActorRoleCodes.IsKnown)
                && AppearanceFamilyCodes.Length > 0
                && AppearanceFamilyCodes.All(WorldActorAppearanceFamilyCodes.IsKnown)
                && Weight > 0 && PresentationOnly;
    }

    [Serializable]
    public sealed class WorldCharacterAssignmentResult
    {
        public string ActorStableId = string.Empty;
        public string ActorRoleCode = WorldActorRoleCodes.Unresolved;
        public string AppearanceFamilyCode = WorldActorAppearanceFamilyCodes.Neutral;
        public string VisualKey = WorldCharacterVisualKeys.NeutralAdultA;
        public string CatalogRevision = string.Empty;
        public string DiagnosticCode = string.Empty;
        public bool PresentationOnly = true;
    }

    public static class WorldCharacterAssignmentPolicy
    {
        public static WorldCharacterAssignmentResult Assign(
            WorldActorAppearanceProfile profile,
            string actorRoleCode,
            string catalogRevision,
            IReadOnlyList<WorldCharacterAssignmentCandidate> candidates)
        {
            if (profile == null || !profile.Validate()
                || !WorldActorRoleCodes.IsKnown(actorRoleCode)
                || string.IsNullOrWhiteSpace(catalogRevision)
                || candidates == null || candidates.Count == 0
                || candidates.Any(value => value == null || !value.Validate()))
                throw new ArgumentException("WorldCharacterAssignmentInputInvalid");

            var requestedRole = actorRoleCode;
            var roleCandidates = candidates.Where(value =>
                    value.AllowedActorRoleCodes.Contains(requestedRole, StringComparer.Ordinal))
                .ToArray();
            var diagnostic = string.Empty;
            if (roleCandidates.Length == 0)
            {
                roleCandidates = candidates.Where(value =>
                        value.AllowedActorRoleCodes.Contains(
                            WorldActorRoleCodes.Unresolved, StringComparer.Ordinal))
                    .ToArray();
                diagnostic = "character.role-candidate-missing:neutral-fallback";
            }
            if (roleCandidates.Length == 0)
                throw new InvalidOperationException("WorldCharacterNeutralCandidateMissing");

            var preferredFamily = profile.SelectedAppearanceFamilyCode;
            var familyCandidates = roleCandidates.Where(value =>
                    value.AppearanceFamilyCodes.Contains(preferredFamily, StringComparer.Ordinal))
                .ToArray();
            if (familyCandidates.Length == 0)
            {
                familyCandidates = roleCandidates;
                diagnostic = string.IsNullOrEmpty(diagnostic)
                    ? "character.appearance-family-unavailable:role-fallback"
                    : diagnostic;
            }

            var ordered = familyCandidates.OrderBy(value => value.VisualKey, StringComparer.Ordinal)
                .ToArray();
            var totalWeight = ordered.Sum(value => value.Weight);
            var hash = StableHash(profile.ActorStableId + "|" + requestedRole + "|"
                + catalogRevision + "|" + preferredFamily);
            var selection = (int)(hash % (uint)totalWeight);
            var selected = ordered[0];
            foreach (var candidate in ordered)
            {
                if (selection < candidate.Weight)
                {
                    selected = candidate;
                    break;
                }
                selection -= candidate.Weight;
            }

            return new WorldCharacterAssignmentResult
            {
                ActorStableId = profile.ActorStableId,
                ActorRoleCode = requestedRole,
                AppearanceFamilyCode = preferredFamily,
                VisualKey = selected.VisualKey,
                CatalogRevision = catalogRevision,
                DiagnosticCode = diagnostic,
                PresentationOnly = true,
            };
        }

        private static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= prime;
            }
            return hash;
        }
    }
}
