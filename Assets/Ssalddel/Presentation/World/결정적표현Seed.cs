using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 표현 개체의 안정 식별자로만 재현 가능한 시각 변형값을 만든다.
    /// Simulation 판정 seed나 업무 결과 계산에는 사용하지 않는다.
    /// </summary>
    public static class 결정적표현Seed
    {
        public static uint Hash(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("DeterministicVisualStableIdMissing");
            var hash = 2166136261u;
            foreach (var value in stableId.Trim())
            {
                hash ^= value;
                hash *= 16777619u;
            }
            return hash;
        }

        public static float PhaseRadians(string stableId)
            => Hash(stableId) % 1000u / 1000f * Mathf.PI * 2f;

        public static float PlaybackScale(string stableId)
            => .95f + Hash(stableId) % 101u / 1000f;
    }
}
