using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 법정동시각연결상태Codes
    {
        public const string 연결가능 = "Ready";
        public const string 시각키누락 = "VisualKeyMissing";
        public const string 토지피복불일치 = "LandCoverRejected";
        public const string 영역역할불일치 = "RegionRoleRejected";
        public const string 경사불일치 = "SlopeRejected";
        public const string Prefab누락 = "PrefabMissing";
    }

    public sealed class 법정동Synty후단연결결과
    {
        public 법정동Synty후단연결결과(
            string placementStableId,
            string visualKey,
            string statusCode,
            string sourcePack,
            GameObject prefab,
            bool presentationOnly)
        {
            PlacementStableId = placementStableId;
            VisualKey = visualKey;
            StatusCode = statusCode;
            SourcePack = sourcePack;
            Prefab = prefab;
            PresentationOnly = presentationOnly;
        }

        public string PlacementStableId { get; }
        public string VisualKey { get; }
        public string StatusCode { get; }
        public string SourcePack { get; }
        public GameObject Prefab { get; }
        public bool PresentationOnly { get; }
        public bool 연결가능여부 => StatusCode == 법정동시각연결상태Codes.연결가능;
    }

    /// <summary>
    /// 공간·면적·건물 배치 계획이 끝난 뒤 의미 기반 VisualKey를 보유 Synty Prefab으로 해석합니다.
    /// 결과는 표현 전용이며 배치 고유 식별자, 공간 근거와 Simulation 상태를 변경하지 않습니다.
    /// </summary>
    public sealed class 법정동Synty후단연결기
    {
        public IReadOnlyList<법정동Synty후단연결결과> 연결계획(
            법정동경관PlanData plan,
            법정동경관VisualCatalog catalog,
            Func<법정동경관PlacementData, float> physicalSlopeDegrees)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (physicalSlopeDegrees == null)
                throw new ArgumentNullException(nameof(physicalSlopeDegrees));
            법정동경관PlanValidator.Validate(plan);
            catalog.Validate();
            return plan.Placements
                .Select(placement => 연결(
                    placement, catalog, physicalSlopeDegrees(placement)))
                .ToArray();
        }

        public 법정동Synty후단연결결과 연결(
            법정동경관PlacementData placement,
            법정동경관VisualCatalog catalog,
            float physicalSlopeDegrees)
        {
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            법정동경관VisualCatalogEntry entry;
            try
            {
                entry = catalog.Resolve(placement.VisualKey);
            }
            catch (InvalidOperationException)
            {
                return Rejected(placement, 법정동시각연결상태Codes.시각키누락);
            }

            if (!entry.AllowedLandCoverCodes.Contains(placement.LandCoverCode))
                return Rejected(placement, 법정동시각연결상태Codes.토지피복불일치);
            if (!entry.AllowedRegionRoleCodes.Contains(placement.RegionRoleCode))
                return Rejected(placement, 법정동시각연결상태Codes.영역역할불일치);
            if (physicalSlopeDegrees < entry.SlopeRange.x
                || physicalSlopeDegrees > entry.SlopeRange.y)
                return Rejected(placement, 법정동시각연결상태Codes.경사불일치);
            if (entry.Prefab == null)
                return Rejected(placement, 법정동시각연결상태Codes.Prefab누락);

            return new 법정동Synty후단연결결과(
                placement.PlacementStableId,
                placement.VisualKey,
                법정동시각연결상태Codes.연결가능,
                entry.SourcePack,
                entry.Prefab,
                true);
        }

        private static 법정동Synty후단연결결과 Rejected(
            법정동경관PlacementData placement,
            string statusCode)
            => new(
                placement.PlacementStableId,
                placement.VisualKey,
                statusCode,
                string.Empty,
                null!,
                true);
    }
}
