using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 기존 52개 의미군 × A/B/C CompositionKey를 읽기 전용 기능 모듈 참조로 바꾼다.
    /// 신규 배치 정의는 이 Adapter의 입력 형식을 작성하지 않는다.
    /// </summary>
    public static class LegacyCompositionAdapter
    {
        public const string PolicyRevision = "legacy-synty-composition.readonly.r1";

        public static IReadOnlyList<Synty자산ModuleRef> ToModuleRefs(
            string legacyCompositionKey)
        {
            if (string.IsNullOrWhiteSpace(legacyCompositionKey))
                return Array.Empty<Synty자산ModuleRef>();

            var value = legacyCompositionKey.ToLowerInvariant();
            var modules = new HashSet<string>(StringComparer.Ordinal);
            if (ContainsAny(value, "terrain", "ground", "surface", "landscape"))
                modules.Add(Synty자산ModuleCodes.WorldSurface);
            if (ContainsAny(value, "nature", "forest", "tree", "plant", "rock"))
                modules.Add(Synty자산ModuleCodes.NatureVegetation);
            if (ContainsAny(value, "road", "network", "street", "route"))
                modules.Add(Synty자산ModuleCodes.WorldNetwork);
            if (ContainsAny(value, "transition", "gate", "portal", "corridor"))
                modules.Add(Synty자산ModuleCodes.WorldTransition);
            if (ContainsAny(value, "construction", "build", "repair", "recovery"))
                modules.Add(Synty자산ModuleCodes.ConstructionState);
            if (ContainsAny(value, "interior", "inside", "room"))
            {
                modules.Add(Synty자산ModuleCodes.InteriorStructure);
                modules.Add(Synty자산ModuleCodes.InteriorFixture);
            }
            if (ContainsAny(value, "city", "town", "farm", "building", "house"))
                modules.Add(Synty자산ModuleCodes.ExteriorStructure);
            if (modules.Count == 0)
                modules.Add(Synty자산ModuleCodes.ExteriorFunctionalProp);

            return modules.OrderBy(module => module, StringComparer.Ordinal)
                .Select(module => new Synty자산ModuleRef(module, true))
                .ToArray();
        }

        private static bool ContainsAny(string value, params string[] tokens) =>
            tokens.Any(value.Contains);
    }
}
