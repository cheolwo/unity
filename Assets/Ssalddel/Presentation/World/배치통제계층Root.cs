using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 배치통제계층LevelRecord
    {
        public string LevelCode = string.Empty;
        public string DisplayNameKo = string.Empty;
        public string ParentLevelCode = string.Empty;
        public string BoundaryRoleCode = string.Empty;
    }

    [Serializable]
    public sealed class Player상대ScaleBandRecord
    {
        public string ScaleBandCode = string.Empty;
        public float MinimumHeightRatio;
        public float MaximumHeightRatio;
        public float MinimumFootprintRatio;
        public float MaximumFootprintRatio;

        public bool Contains(float heightRatio, float footprintRatio) =>
            heightRatio >= MinimumHeightRatio - .01f
            && heightRatio <= MaximumHeightRatio + .01f
            && footprintRatio >= MinimumFootprintRatio - .01f
            && footprintRatio <= MaximumFootprintRatio + .01f;
    }

    /// <summary>
    /// H1~H5의 의미 계층과 별개로, Player 실측 크기에서 시작해 H1 발자국이
    /// H2 블록·H3 경관·AreaSet 외곽 안에 놓였는지를 통제하는 표현 전용 기록입니다.
    /// 원본 Synty Transform의 부모를 바꾸지 않고 결정된 소속과 비율만 보존합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 배치통제계층Root : MonoBehaviour
    {
        public const string RequiredStableId =
            "placement-hierarchy:simulation-world-shell.v1";
        public const string RequiredRevision = "placement-control-hierarchy.v1";

        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string revision = string.Empty;
        [SerializeField] private Transform worldEnvelope = null!;
        [SerializeField] private 플레이어경관Controller playerReference = null!;
        [SerializeField] private float playerReferenceHeight;
        [SerializeField] private string moduleOrchestrationPolicyCode =
            string.Empty;
        [SerializeField] private 배치통제계층LevelRecord[] levels =
            Array.Empty<배치통제계층LevelRecord>();
        [SerializeField] private Player상대ScaleBandRecord[] scaleBands =
            Array.Empty<Player상대ScaleBandRecord>();
        [SerializeField] private bool presentationOnly = true;

        public string StableId => stableId;
        public string Revision => revision;
        public Transform WorldEnvelope => worldEnvelope;
        public 플레이어경관Controller PlayerReference => playerReference;
        public float PlayerReferenceHeight => playerReferenceHeight;
        public string ModuleOrchestrationPolicyCode =>
            moduleOrchestrationPolicyCode;
        public 배치통제계층LevelRecord[] Levels => levels;
        public Player상대ScaleBandRecord[] ScaleBands => scaleBands;
        public bool PresentationOnly => presentationOnly;

        public void Configure(Transform world, 플레이어경관Controller player,
            float measuredPlayerHeight)
        {
            stableId = RequiredStableId;
            revision = RequiredRevision;
            worldEnvelope = world;
            playerReference = player;
            playerReferenceHeight = measuredPlayerHeight;
            moduleOrchestrationPolicyCode =
                "H3Axis-H2Frontage-H1ModuleAssembly";
            levels = new[]
            {
                Level("WorldEnvelope", "세계 배치 외곽", "", "Containment"),
                Level("AreaEnvelope", "AreaSet 배치 외곽", "WorldEnvelope", "Containment"),
                Level("LandscapeEnvelope", "H3 경관 외곽", "AreaEnvelope", "SurfaceAndRidge"),
                Level("BlockEnvelope", "H2 블록 외곽", "LandscapeEnvelope", "Layout"),
                Level("ObjectFootprint", "H1 객체 발자국", "BlockEnvelope", "ClearanceAndScale"),
                Level("CharacterReference", "Player 기준 크기", "WorldEnvelope", "ScaleReference"),
            };
            scaleBands = new[]
            {
                Band("Building", .25f, 3.5f, .25f, 4.2f),
                Band("FenceChain", .01f, 1.2f, .02f, 4f),
                Band("Ridge", .05f, 8f, .1f, 8f),
                Band("General", .005f, 2.5f, .005f, 3.8f),
                Band("RoadFrontage", .05f, 3.5f, 2f, 12f),
            };
            presentationOnly = true;
            Validate();
        }

        public Player상대ScaleBandRecord FindBand(string code) =>
            scaleBands.Single(value => value.ScaleBandCode == code);

        public void Validate()
        {
            var levelCodes = levels.Select(value => value.LevelCode)
                .ToArray();
            if (stableId != RequiredStableId || revision != RequiredRevision
                || worldEnvelope == null || playerReference == null
                || playerReference.VisualRoot == null
                || playerReferenceHeight is < .8f or > 3f
                || moduleOrchestrationPolicyCode !=
                    "H3Axis-H2Frontage-H1ModuleAssembly"
                || levels.Length != 6
                || levelCodes.Distinct(StringComparer.Ordinal).Count() != 6
                || !levelCodes.Contains("WorldEnvelope")
                || !levelCodes.Contains("AreaEnvelope")
                || !levelCodes.Contains("LandscapeEnvelope")
                || !levelCodes.Contains("BlockEnvelope")
                || !levelCodes.Contains("ObjectFootprint")
                || !levelCodes.Contains("CharacterReference")
                || levels.Any(value => string.IsNullOrWhiteSpace(value.DisplayNameKo)
                    || string.IsNullOrWhiteSpace(value.BoundaryRoleCode)
                    || value.LevelCode != "WorldEnvelope"
                    && !levelCodes.Contains(value.ParentLevelCode))
                || scaleBands.Length != 5
                || scaleBands.Select(value => value.ScaleBandCode)
                    .Distinct(StringComparer.Ordinal).Count() != 5
                || scaleBands.Any(value => value.MinimumHeightRatio <= 0f
                    || value.MaximumHeightRatio <= value.MinimumHeightRatio
                    || value.MinimumFootprintRatio <= 0f
                    || value.MaximumFootprintRatio <= value.MinimumFootprintRatio)
                || !presentationOnly)
                throw new InvalidOperationException(
                    "PlacementControlHierarchyInvalid:"
                    + $"stable={stableId};revision={revision};"
                    + $"playerHeight={playerReferenceHeight:F3};"
                    + $"policy={moduleOrchestrationPolicyCode};"
                    + $"levels={levels.Length};bands={scaleBands.Length};"
                    + $"presentationOnly={presentationOnly}");
        }

        private static 배치통제계층LevelRecord Level(string code, string name,
            string parent, string role) => new()
        {
            LevelCode = code,
            DisplayNameKo = name,
            ParentLevelCode = parent,
            BoundaryRoleCode = role,
        };

        private static Player상대ScaleBandRecord Band(string code,
            float minHeight, float maxHeight, float minFootprint,
            float maxFootprint) => new()
        {
            ScaleBandCode = code,
            MinimumHeightRatio = minHeight,
            MaximumHeightRatio = maxHeight,
            MinimumFootprintRatio = minFootprint,
            MaximumFootprintRatio = maxFootprint,
        };
    }
}
