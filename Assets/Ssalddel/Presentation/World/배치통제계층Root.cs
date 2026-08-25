using System;
using System.Linq;
using Ssalddel.Unity.PresentationContracts;
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

    [Serializable]
    public sealed class H공간성립단계Record
    {
        public string SourceLevelCode = string.Empty;
        public string TargetLevelCode = string.Empty;
        public string DecisionKindCode = string.Empty;
        public bool CreatesSpatialInstance;
    }

    [Serializable]
    public sealed class 감각표현ChannelRecord
    {
        public string DomainCode = string.Empty;
        public string ResponsibilityCode = string.Empty;
        public bool MutatesCanonicalState;
        public bool ConfirmsBusinessCompletion;
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
        public const string InteriorStableId =
            "placement-hierarchy:simulation-world-shell.hub-warehouse.v2";
        public const string InteriorRevision = "placement-control-hierarchy.v2";
        public const string CompositionStableId =
            "placement-composition:simulation-world-shell.hub-warehouse.v3";
        public const string CompositionRevision =
            "placement-control-hierarchy.v3";
        public const string SensoryPresentationStableId =
            "placement-composition:simulation-world-shell.sensory-presentation.v4";
        public const string SensoryPresentationRevision =
            "placement-control-hierarchy.v4";
        public const string BottomUpCompositionDirection =
            "BottomUp:H1-H2-H3-H4Readiness";
        public const string TopDownConstraintDirection =
            "TopDown:WorldEnvelope-H1-Interior";
        public const string SensoryPresentationDirection =
            "CrossCutting:Graphics-Camera-Animation-Lighting-Audio-UI";
        public const string SensoryPresentationBindingSchema =
            "placement-presentation-bindings.v1";

        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string revision = string.Empty;
        [SerializeField] private Transform worldEnvelope = null!;
        [SerializeField] private 플레이어경관Controller playerReference = null!;
        [SerializeField] private float playerReferenceHeight;
        [SerializeField] private string moduleOrchestrationPolicyCode =
            string.Empty;
        [SerializeField] private string primaryCompositionDirectionCode =
            string.Empty;
        [SerializeField] private string constraintEvaluationDirectionCode =
            string.Empty;
        [SerializeField] private string presentationEvaluationDirectionCode =
            string.Empty;
        [SerializeField] private string presentationBindingSchemaCode =
            string.Empty;
        [SerializeField] private H공간성립단계Record[] compositionSteps =
            Array.Empty<H공간성립단계Record>();
        [SerializeField] private 감각표현ChannelRecord[] presentationChannels =
            Array.Empty<감각표현ChannelRecord>();
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
        public string PrimaryCompositionDirectionCode =>
            primaryCompositionDirectionCode;
        public string ConstraintEvaluationDirectionCode =>
            constraintEvaluationDirectionCode;
        public string PresentationEvaluationDirectionCode =>
            presentationEvaluationDirectionCode;
        public string PresentationBindingSchemaCode =>
            presentationBindingSchemaCode;
        public H공간성립단계Record[] CompositionSteps => compositionSteps;
        public 감각표현ChannelRecord[] PresentationChannels => presentationChannels;
        public 배치통제계층LevelRecord[] Levels => levels;
        public Player상대ScaleBandRecord[] ScaleBands => scaleBands;
        public bool PresentationOnly => presentationOnly;

        public void Configure(Transform world, 플레이어경관Controller player,
            float measuredPlayerHeight)
        {
            ApplyProfile(RequiredStableId, RequiredRevision,
                "H3Axis-H2Frontage-H1ModuleAssembly", world, player,
                measuredPlayerHeight, ExteriorLevels(), BaseScaleBands());
        }

        public void ConfigureInterior(Transform world,
            플레이어경관Controller player, float measuredPlayerHeight)
        {
            ApplyProfile(InteriorStableId, InteriorRevision,
                "H3Axis-H2Block-H1Zone-InteriorFixtureAssembly", world,
                player, measuredPlayerHeight, InteriorLevels(),
                InteriorScaleBands());
        }

        /// <summary>
        /// v2 정밀 배치 제약은 그대로 사용하고, 공간의 주 성립 판단만 H1에서
        /// H4 준비도로 올라가도록 명시합니다. H4는 새 공간 인스턴스를 만들지
        /// 않고 AreaSet 준비도만 평가합니다.
        /// </summary>
        public void ConfigureCompositionV3(Transform world,
            플레이어경관Controller player, float measuredPlayerHeight)
        {
            ApplyProfile(CompositionStableId, CompositionRevision,
                "H1Evidence-H2Formation-H3Formation-H4Readiness", world,
                player, measuredPlayerHeight, InteriorLevels(),
                InteriorScaleBands(), BottomUpCompositionDirection,
                TopDownConstraintDirection, new[]
                {
                    CompositionStep("H1", "H2",
                        "FormFromOperationalChildren", true),
                    CompositionStep("H2", "H3",
                        "FormFromQualifiedChildren", true),
                    CompositionStep("H3", "H4",
                        "AssessAreaSetReadiness", false),
                });
        }

        /// <summary>
        /// v3의 상향식 성립과 하향식 제약을 보존한 채, 배치가 플레이어에게
        /// 보이고 들리며 조작 가능한지를 검사하는 감각 표현축을 교차 결속합니다.
        /// 표현축은 H 단계가 아니고 Simulation 상태를 만들거나 완료하지 않습니다.
        /// </summary>
        public void ConfigureSensoryPresentationV4(Transform world,
            플레이어경관Controller player, float measuredPlayerHeight)
        {
            ApplyProfile(SensoryPresentationStableId,
                SensoryPresentationRevision,
                "H1Formation-H4Readiness-WithSensoryPresentation", world,
                player, measuredPlayerHeight, InteriorLevels(),
                InteriorScaleBands(), BottomUpCompositionDirection,
                TopDownConstraintDirection, new[]
                {
                    CompositionStep("H1", "H2",
                        "FormFromOperationalChildren", true),
                    CompositionStep("H2", "H3",
                        "FormFromQualifiedChildren", true),
                    CompositionStep("H3", "H4",
                        "AssessAreaSetReadiness", false),
                }, SensoryPresentationDirection,
                SensoryPresentationBindingSchema, SensoryChannels());
        }

        public Player상대ScaleBandRecord FindBand(string code) =>
            scaleBands.Single(value => value.ScaleBandCode == code);

        public void Validate()
        {
            var levelCodes = levels.Select(value => value.LevelCode)
                .ToArray();
            var isExteriorV1 = stableId == RequiredStableId
                               && revision == RequiredRevision;
            var isInteriorV2 = stableId == InteriorStableId
                               && revision == InteriorRevision;
            var isCompositionV3 = stableId == CompositionStableId
                                  && revision == CompositionRevision;
            var isSensoryPresentationV4 = stableId ==
                SensoryPresentationStableId
                && revision == SensoryPresentationRevision;
            var isInterior = isInteriorV2 || isCompositionV3
                             || isSensoryPresentationV4;
            var isComposition = isCompositionV3 || isSensoryPresentationV4;
            var expectedLevelCount = isInterior ? 12 : 6;
            var expectedBandCount = isInterior ? 7 : 5;
            var expectedPolicy = isSensoryPresentationV4
                ? "H1Formation-H4Readiness-WithSensoryPresentation"
                : isCompositionV3
                    ? "H1Evidence-H2Formation-H3Formation-H4Readiness"
                : isInteriorV2
                    ? "H3Axis-H2Block-H1Zone-InteriorFixtureAssembly"
                    : "H3Axis-H2Frontage-H1ModuleAssembly";
            if ((!isExteriorV1 && !isInteriorV2 && !isCompositionV3
                    && !isSensoryPresentationV4)
                || worldEnvelope == null || playerReference == null
                || playerReference.VisualRoot == null
                || playerReferenceHeight is < .8f or > 3f
                || moduleOrchestrationPolicyCode != expectedPolicy
                || levels.Length != expectedLevelCount
                || levelCodes.Distinct(StringComparer.Ordinal).Count()
                    != expectedLevelCount
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
                || isInterior && (!levelCodes.Contains("BuildingShell")
                    || !levelCodes.Contains("WorkZone")
                    || !levelCodes.Contains("TraversalCorridor")
                    || !levelCodes.Contains("FixtureWrapper")
                    || !levelCodes.Contains("SourcePrefab")
                    || !levelCodes.Contains("PlacementReceipt"))
                || isComposition
                && (primaryCompositionDirectionCode
                        != BottomUpCompositionDirection
                    || constraintEvaluationDirectionCode
                        != TopDownConstraintDirection
                    || compositionSteps.Length != 3
                    || compositionSteps[0].SourceLevelCode != "H1"
                    || compositionSteps[0].TargetLevelCode != "H2"
                    || !compositionSteps[0].CreatesSpatialInstance
                    || compositionSteps[1].SourceLevelCode != "H2"
                    || compositionSteps[1].TargetLevelCode != "H3"
                    || !compositionSteps[1].CreatesSpatialInstance
                    || compositionSteps[2].SourceLevelCode != "H3"
                    || compositionSteps[2].TargetLevelCode != "H4"
                    || compositionSteps[2].CreatesSpatialInstance)
                || isSensoryPresentationV4
                && (presentationEvaluationDirectionCode
                        != SensoryPresentationDirection
                    || presentationBindingSchemaCode
                        != SensoryPresentationBindingSchema
                    || presentationChannels.Length
                        != 표현규칙영역Codes.All.Length
                    || presentationChannels.Select(value => value.DomainCode)
                        .Distinct(StringComparer.Ordinal).Count()
                        != 표현규칙영역Codes.All.Length
                    || 표현규칙영역Codes.All.Any(domain =>
                        !presentationChannels.Any(value =>
                            value.DomainCode == domain))
                    || presentationChannels.Any(value =>
                        string.IsNullOrWhiteSpace(value.ResponsibilityCode)
                        || value.MutatesCanonicalState
                        || value.ConfirmsBusinessCompletion))
                || !isSensoryPresentationV4
                && (!string.IsNullOrEmpty(presentationEvaluationDirectionCode)
                    || !string.IsNullOrEmpty(presentationBindingSchemaCode)
                    || presentationChannels.Length != 0)
                || scaleBands.Length != expectedBandCount
                || scaleBands.Select(value => value.ScaleBandCode)
                    .Distinct(StringComparer.Ordinal).Count()
                    != expectedBandCount
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

        private static H공간성립단계Record CompositionStep(string source,
            string target, string decisionKind, bool createsInstance) => new()
        {
            SourceLevelCode = source,
            TargetLevelCode = target,
            DecisionKindCode = decisionKind,
            CreatesSpatialInstance = createsInstance,
        };

        private void ApplyProfile(string profileStableId,
            string profileRevision, string policyCode, Transform world,
            플레이어경관Controller player, float measuredPlayerHeight,
            배치통제계층LevelRecord[] profileLevels,
            Player상대ScaleBandRecord[] profileScaleBands,
            string compositionDirection = "",
            string constraintDirection = "",
            H공간성립단계Record[]? profileCompositionSteps = null,
            string presentationDirection = "",
            string presentationSchema = "",
            감각표현ChannelRecord[]? profilePresentationChannels = null)
        {
            stableId = profileStableId;
            revision = profileRevision;
            worldEnvelope = world;
            playerReference = player;
            playerReferenceHeight = measuredPlayerHeight;
            moduleOrchestrationPolicyCode = policyCode;
            primaryCompositionDirectionCode = compositionDirection;
            constraintEvaluationDirectionCode = constraintDirection;
            presentationEvaluationDirectionCode = presentationDirection;
            presentationBindingSchemaCode = presentationSchema;
            compositionSteps = profileCompositionSteps
                               ?? Array.Empty<H공간성립단계Record>();
            presentationChannels = profilePresentationChannels
                                   ?? Array.Empty<감각표현ChannelRecord>();
            levels = profileLevels;
            scaleBands = profileScaleBands;
            presentationOnly = true;
            Validate();
        }

        private static 배치통제계층LevelRecord[] ExteriorLevels() => new[]
        {
            Level("WorldEnvelope", "세계 배치 외곽", "", "Containment"),
            Level("AreaEnvelope", "AreaSet 배치 외곽", "WorldEnvelope", "Containment"),
            Level("LandscapeEnvelope", "H3 경관 외곽", "AreaEnvelope", "SurfaceAndRidge"),
            Level("BlockEnvelope", "H2 블록 외곽", "LandscapeEnvelope", "Layout"),
            Level("ObjectFootprint", "H1 객체 발자국", "BlockEnvelope", "ClearanceAndScale"),
            Level("CharacterReference", "Player 기준 크기", "WorldEnvelope", "ScaleReference"),
        };

        private static 배치통제계층LevelRecord[] InteriorLevels() => new[]
        {
            Level("WorldEnvelope", "세계 배치 외곽", "", "Containment"),
            Level("AreaEnvelope", "AreaSet 배치 외곽", "WorldEnvelope", "Containment"),
            Level("LandscapeEnvelope", "H3 경관 외곽", "AreaEnvelope", "SurfaceAndRidge"),
            Level("BlockEnvelope", "H2 업무 블록 외곽", "LandscapeEnvelope", "Layout"),
            Level("ObjectFootprint", "H1 업무 공간 발자국", "BlockEnvelope", "ClearanceAndScale"),
            Level("BuildingShell", "25x20m 창고 외피", "ObjectFootprint", "BuildingBounds"),
            Level("WorkZone", "업무 구역", "BuildingShell", "ZoneOwnership"),
            Level("TraversalCorridor", "보행·수레 통로", "WorkZone", "TraversalClearance"),
            Level("FixtureWrapper", "미세 조정 Wrapper", "WorkZone", "AuthoringAdjustment"),
            Level("SourcePrefab", "Synty 원본 Prefab", "FixtureWrapper", "ImmutableSource"),
            Level("CharacterReference", "Player 기준 크기", "WorldEnvelope", "ScaleReference"),
            Level("PlacementReceipt", "배치 검증 기록", "FixtureWrapper", "Evidence"),
        };

        private static Player상대ScaleBandRecord[] BaseScaleBands() => new[]
        {
            Band("Building", .25f, 3.5f, .25f, 4.2f),
            Band("FenceChain", .01f, 1.2f, .02f, 4f),
            Band("Ridge", .05f, 8f, .1f, 8f),
            Band("General", .005f, 2.5f, .005f, 3.8f),
            Band("RoadFrontage", .05f, 3.5f, 2f, 12f),
        };

        private static Player상대ScaleBandRecord[] InteriorScaleBands() =>
            BaseScaleBands().Concat(new[]
            {
                Band("InteriorFixture", .05f, 1.8f, .05f, 2.5f),
                Band("WarehouseAisle", .01f, .5f, .6f, 8f),
            }).ToArray();

        private static 감각표현ChannelRecord[] SensoryChannels() => new[]
        {
            Channel(표현규칙영역Codes.Graphics,
                "Renderer-FX-LOD-ObjectState"),
            Channel(표현규칙영역Codes.Camera,
                "Focus-Framing-Occlusion-Cutaway"),
            Channel(표현규칙영역Codes.Animation,
                "Actor-Tool-Work-StatePlayback"),
            Channel(표현규칙영역Codes.Lighting,
                "Light-Ambient-Fog-TimeOfDay"),
            Channel(표현규칙영역Codes.Audio,
                "SpatialSfx-Ambient-MusicRouting"),
            Channel(표현규칙영역Codes.UI,
                "Prompt-Progress-BlockedReason"),
        };

        private static 감각표현ChannelRecord Channel(string domain,
            string responsibility) => new()
        {
            DomainCode = domain,
            ResponsibilityCode = responsibility,
        };
    }
}
