using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 평창4Pack경관AreaCodes
    {
        public const string Overview = "overview";
        public const string DaegwallyeongFarm = "daegwallyeong-farm";
        public const string FarmHubCorridor = "farm-hub-corridor";
        public const string JinbuHub = "jinbu-hub";
        public const string PyeongchangTown = "pyeongchang-town";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Overview, DaegwallyeongFarm, FarmHubCorridor,
            JinbuHub, PyeongchangTown,
        };
    }

    [Serializable]
    public sealed class 평창4Pack경관Weight
    {
        public string AreaCode = string.Empty;
        public int Nature;
        public int Farm;
        public int Town;
        public int City;

        public int Total => Nature + Farm + Town + City;

        public int Resolve(string packCode)
        {
            if (packCode == 월드CompositionPackCodes.Nature) return Nature;
            if (packCode == 월드CompositionPackCodes.Farm) return Farm;
            if (packCode == 월드CompositionPackCodes.Town) return Town;
            if (packCode == 월드CompositionPackCodes.City) return City;
            throw new InvalidOperationException("PyeongchangFourPackUnknown:" + packCode);
        }

        public bool Validate()
            => 평창4Pack경관AreaCodes.All.Contains(AreaCode, StringComparer.Ordinal)
                && Nature >= 0 && Farm >= 0 && Town >= 0 && City >= 0
                && Total == 100;
    }

    public sealed class 평창4Pack경관Profile
    {
        private readonly 평창4Pack경관Weight[] _weights;

        public 평창4Pack경관Profile(IEnumerable<평창4Pack경관Weight> weights)
        {
            _weights = weights?.ToArray() ?? Array.Empty<평창4Pack경관Weight>();
            Validate();
        }

        public IReadOnlyList<평창4Pack경관Weight> Weights => _weights;

        public static 평창4Pack경관Profile CreateDefault() => new(new[]
        {
            Weight(평창4Pack경관AreaCodes.Overview, 85, 6, 5, 4),
            Weight(평창4Pack경관AreaCodes.DaegwallyeongFarm, 48, 44, 6, 2),
            Weight(평창4Pack경관AreaCodes.FarmHubCorridor, 35, 30, 20, 15),
            Weight(평창4Pack경관AreaCodes.JinbuHub, 20, 10, 25, 45),
            Weight(평창4Pack경관AreaCodes.PyeongchangTown, 20, 5, 55, 20),
        });

        public 평창4Pack경관Weight Resolve(string areaCode)
            => _weights.SingleOrDefault(value => value.AreaCode == areaCode)
                ?? throw new InvalidOperationException(
                    "PyeongchangFourPackAreaMissing:" + areaCode);

        public string ResolvePack(
            string areaCode,
            int lodGroup,
            string slotStableKey,
            int seed)
        {
            if (lodGroup is < 0 or > 2)
                throw new ArgumentOutOfRangeException(nameof(lodGroup));
            if (string.IsNullOrWhiteSpace(slotStableKey))
                throw new ArgumentException("SlotStableKeyRequired", nameof(slotStableKey));

            var weight = Resolve(areaCode);
            var roll = (int)(StableHash(
                seed + "|" + areaCode + "|" + lodGroup + "|" + slotStableKey) % 100u);
            if (roll < weight.Nature) return 월드CompositionPackCodes.Nature;
            roll -= weight.Nature;
            if (roll < weight.Farm) return 월드CompositionPackCodes.Farm;
            roll -= weight.Farm;
            if (roll < weight.Town) return 월드CompositionPackCodes.Town;
            return 월드CompositionPackCodes.City;
        }

        public void Validate()
        {
            if (_weights.Length != 평창4Pack경관AreaCodes.All.Count
                || _weights.Any(value => value == null || !value.Validate())
                || _weights.Select(value => value.AreaCode)
                    .Distinct(StringComparer.Ordinal).Count() != _weights.Length
                || 평창4Pack경관AreaCodes.All.Any(code =>
                    _weights.All(value => value.AreaCode != code)))
                throw new InvalidOperationException("PyeongchangFourPackProfileInvalid");
        }

        private static 평창4Pack경관Weight Weight(
            string areaCode,
            int nature,
            int farm,
            int town,
            int city) => new()
        {
            AreaCode = areaCode,
            Nature = nature,
            Farm = farm,
            Town = town,
            City = city,
        };

        private static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class 평창4Pack경관ProfileView : MonoBehaviour
    {
        [SerializeField] private 평창4Pack경관Weight[] areaWeights =
            Array.Empty<평창4Pack경관Weight>();
        [SerializeField] private bool presentationOnly = true;

        public IReadOnlyList<평창4Pack경관Weight> AreaWeights => areaWeights;
        public bool PresentationOnly => presentationOnly;

        public void Configure(평창4Pack경관Profile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.Validate();
            areaWeights = profile.Weights.Select(value => new 평창4Pack경관Weight
            {
                AreaCode = value.AreaCode,
                Nature = value.Nature,
                Farm = value.Farm,
                Town = value.Town,
                City = value.City,
            }).ToArray();
            presentationOnly = true;
        }

        public bool ValidateWiring()
        {
            if (!presentationOnly) return false;
            try
            {
                new 평창4Pack경관Profile(areaWeights).Validate();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
