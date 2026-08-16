using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public sealed class 정적경관배치ReviewWindow : EditorWindow
    {
        private const float LeftWidth = 285f;
        private const float RightWidth = 360f;

        private 정적경관배치PlanData? _basePlan;
        private 정적경관배치OverridePlanData? _overridePlan;
        private 정적경관배치PlanData? _mergedPlan;
        private 정적경관배치ReviewStatusData? _review;
        private 정적경관배치ValidationReportData? _report;
        private IReadOnlyList<정적경관배치ContainerInfoData> _containers =
            Array.Empty<정적경관배치ContainerInfoData>();
        private 정적경관배치ItemData? _editing;
        private string _selectedContainerId = string.Empty;
        private string _selectedPlacementId = string.Empty;
        private string _search = string.Empty;
        private string _reviewNote = string.Empty;
        private string _message = string.Empty;
        private MessageType _messageType = MessageType.Info;
        private Vector2 _containerScroll;
        private Vector2 _placementScroll;
        private Vector2 _detailScroll;
        private Vector2 _issueScroll;
        private Vector2 _canvasPan;
        private float _canvasZoom = 1f;

        [MenuItem("Ssalddel/WORLD-PLAN 평창 정적 경관 배치 검토창")]
        public static void Open()
        {
            var window = GetWindow<정적경관배치ReviewWindow>();
            window.titleContent = new GUIContent("평창 경관 배치 검토");
            window.minSize = new Vector2(1120f, 720f);
            window.Show();
        }

        private void OnEnable() => RefreshData();

        private void OnGUI()
        {
            DrawToolbar();
            if (!string.IsNullOrWhiteSpace(_message))
                EditorGUILayout.HelpBox(_message, _messageType);
            if (_basePlan == null || _overridePlan == null || _mergedPlan == null)
            {
                EditorGUILayout.HelpBox(
                    "WORLD-PLAN-1을 실행해 기본·보정 계획과 기획서를 준비하십시오.",
                    MessageType.Warning);
                return;
            }

            DrawSummary();
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            DrawContainerAndPlacementList();
            DrawCanvasColumn();
            DrawDetailColumn();
            EditorGUILayout.EndHorizontal();
            DrawValidationPanel();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("기획서 열기", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                Run(정적경관배치ReviewService.OpenBrief, "경관 배치 기획서를 열었습니다.");
            if (GUILayout.Button("팩 기준 열기", EditorStyles.toolbarDropDown, GUILayout.Width(90f)))
                ShowPackGuideMenu();
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                RefreshData();
            if (GUILayout.Button("검증·Staging", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                ValidateAndRefresh();
            GUILayout.FlexibleSpace();
            _reviewNote = GUILayout.TextField(_reviewNote, EditorStyles.toolbarTextField,
                GUILayout.MinWidth(180f), GUILayout.MaxWidth(320f));
            if (GUILayout.Button("검토 완료", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                Approve(정적경관배치ReviewStateCodes.Reviewed);
            if (GUILayout.Button("Scene 적용 승인", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                Approve(정적경관배치ReviewStateCodes.ApprovedForSceneApply);
            using (new EditorGUI.DisabledScope(_report == null || !_report.CanApply))
                if (GUILayout.Button("승인 계획 적용", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    Run(정적경관배치PlanPipeline.ApplyValidatedPlanToScene,
                        "승인된 정적 경관 계획을 Scene에 적용했습니다.");
            EditorGUILayout.EndHorizontal();
        }

        private static void ShowPackGuideMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Nature / 숲·자연"), false,
                정적경관배치ReviewService.OpenNatureGuide);
            menu.AddItem(new GUIContent("Farm / 농촌·생산"), false,
                정적경관배치ReviewService.OpenFarmGuide);
            menu.AddItem(new GUIContent("Town / 읍내·생활"), false,
                정적경관배치ReviewService.OpenTownGuide);
            menu.AddItem(new GUIContent("City / 도시·물류"), false,
                정적경관배치ReviewService.OpenCityGuide);
            menu.ShowAsContext();
        }

        private void DrawSummary()
        {
            var reviewState = _review?.EffectiveReviewStateCode
                ?? 정적경관배치ReviewStateCodes.Draft;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"계획: {_mergedPlan!.PlanStableId}  |  배치: {_mergedPlan.Placements.Count(item => item.Enabled)}개  "
                + $"|  컨테이너: {_containers.Count}개",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"원본: {_mergedPlan.Source.KindCode}/{_mergedPlan.Source.SpatialEvidenceStatusCode}  "
                + $"|  검토: {reviewState}  |  승인 입력 일치: {_review?.ReviewMatchesInputs == true}");
            if (_review?.EffectiveReviewStateCode == 정적경관배치ReviewStateCodes.Stale)
                EditorGUILayout.HelpBox(
                    "승인 뒤 입력이 변경되었습니다: " + _review.MismatchReason,
                    MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private void DrawContainerAndPlacementList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));
            EditorGUILayout.LabelField("영역과 배치 객체", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField("검색", _search);
            _containerScroll = EditorGUILayout.BeginScrollView(
                _containerScroll, GUILayout.Height(170f));
            foreach (var container in _containers)
            {
                var count = _mergedPlan!.Placements.Count(item =>
                    item.TargetContainerStableId == container.StableId && item.Enabled);
                var selected = container.StableId == _selectedContainerId;
                if (GUILayout.Toggle(selected, $"{container.DisplayName} ({count})", "Button")
                    && !selected)
                    SelectContainer(container.StableId);
            }
            EditorGUILayout.EndScrollView();

            var placements = FilteredPlacements().ToArray();
            EditorGUILayout.LabelField($"배치 목록 ({placements.Length})", EditorStyles.boldLabel);
            _placementScroll = EditorGUILayout.BeginScrollView(_placementScroll);
            foreach (var placement in placements)
            {
                var change = _overridePlan!.Changes.FirstOrDefault(item =>
                    item.PlacementStableId == placement.PlacementStableId);
                var prefix = change?.OperationCode switch
                {
                    정적경관배치OverrideOperationCodes.Add => "+ ",
                    정적경관배치OverrideOperationCodes.Modify => "~ ",
                    정적경관배치OverrideOperationCodes.Disable => "× ",
                    _ => string.Empty,
                };
                var selected = placement.PlacementStableId == _selectedPlacementId;
                if (GUILayout.Toggle(selected, prefix + placement.AssetKey, "Button") && !selected)
                    SelectPlacement(placement.PlacementStableId);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCanvasColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("2D 배치도 — 휠 확대, 가운데 버튼/Alt+드래그 이동",
                EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(
                360f, 10000f, 420f, 10000f, GUILayout.ExpandWidth(true));
            DrawPlanCanvas(rect);
            EditorGUILayout.EndVertical();
        }

        private void DrawPlanCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(.09f, .11f, .13f));
            var container = _containers.FirstOrDefault(item =>
                item.StableId == _selectedContainerId);
            if (container == null)
            {
                GUI.Label(rect, "왼쪽에서 컨테이너를 선택하십시오.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandleCanvasInput(rect, container);
            DrawGrid(rect, container);
            var baseById = _basePlan!.Placements.ToDictionary(
                item => item.PlacementStableId, StringComparer.Ordinal);
            foreach (var placement in _mergedPlan!.Placements.Where(item =>
                         item.TargetContainerStableId == container.StableId))
            {
                var point = WorldToCanvas(placement.Position.X, placement.Position.Z, rect, container);
                var change = _overridePlan!.Changes.FirstOrDefault(item =>
                    item.PlacementStableId == placement.PlacementStableId);
                var color = !placement.Enabled
                    ? new Color(.45f, .45f, .45f)
                    : change?.OperationCode == 정적경관배치OverrideOperationCodes.Add
                        ? new Color(.25f, .9f, .45f)
                        : change != null
                            ? new Color(1f, .66f, .15f)
                            : new Color(.2f, .65f, 1f);
                if (!baseById.ContainsKey(placement.PlacementStableId))
                    color = new Color(.25f, .9f, .45f);
                var size = placement.PlacementStableId == _selectedPlacementId ? 9f : 6f;
                EditorGUI.DrawRect(new Rect(point.x - size / 2f, point.y - size / 2f, size, size), color);
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f),
                $"{container.DisplayName}  확대 {_canvasZoom:0.0}x",
                EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 23f, rect.width - 16f, 20f),
                "파랑 기본  ·  주황 수정  ·  초록 추가  ·  회색 비활성",
                EditorStyles.whiteMiniLabel);
        }

        private void HandleCanvasInput(Rect rect, 정적경관배치ContainerInfoData container)
        {
            var current = Event.current;
            if (!rect.Contains(current.mousePosition)) return;
            if (current.type == EventType.ScrollWheel)
            {
                _canvasZoom = Mathf.Clamp(
                    _canvasZoom * (current.delta.y > 0f ? .9f : 1.1f), .6f, 6f);
                current.Use();
                Repaint();
                return;
            }
            if (current.type == EventType.MouseDrag
                && (current.button == 2 || (current.button == 0 && current.alt)))
            {
                var span = container.Maximum - container.Minimum;
                _canvasPan.x -= current.delta.x / rect.width * span.x / _canvasZoom;
                _canvasPan.y += current.delta.y / rect.height * span.y / _canvasZoom;
                current.Use();
                Repaint();
                return;
            }
            if (current.type != EventType.MouseDown || current.button != 0 || current.alt) return;
            var nearest = _mergedPlan!.Placements
                .Where(item => item.TargetContainerStableId == container.StableId)
                .Select(item => new
                {
                    Item = item,
                    Distance = Vector2.Distance(
                        current.mousePosition,
                        WorldToCanvas(item.Position.X, item.Position.Z, rect, container)),
                })
                .Where(item => item.Distance <= 12f)
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
            if (nearest == null) return;
            SelectPlacement(nearest.Item.PlacementStableId);
            current.Use();
        }

        private void DrawGrid(Rect rect, 정적경관배치ContainerInfoData container)
        {
            for (var index = 0; index <= 10; index++)
            {
                var x = Mathf.Lerp(rect.x, rect.xMax, index / 10f);
                var y = Mathf.Lerp(rect.y, rect.yMax, index / 10f);
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, .06f));
                EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1f), new Color(1f, 1f, 1f, .06f));
            }
        }

        private Vector2 WorldToCanvas(
            float x,
            float z,
            Rect rect,
            정적경관배치ContainerInfoData container)
        {
            var center = (container.Minimum + container.Maximum) * .5f + _canvasPan;
            var span = (container.Maximum - container.Minimum) / _canvasZoom;
            var normalizedX = (x - center.x) / span.x + .5f;
            var normalizedZ = (z - center.y) / span.y + .5f;
            return new Vector2(
                Mathf.Lerp(rect.x, rect.xMax, normalizedX),
                Mathf.Lerp(rect.yMax, rect.y, normalizedZ));
        }

        private void DrawDetailColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightWidth));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("선택 배치 보정", EditorStyles.boldLabel);
            if (GUILayout.Button("새 배치", GUILayout.Width(70f))) CreateNewPlacement();
            EditorGUILayout.EndHorizontal();
            if (_editing == null)
            {
                EditorGUILayout.HelpBox("배치 객체를 선택하십시오.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            using (new EditorGUI.DisabledScope(
                       _basePlan!.Placements.Any(item =>
                           item.PlacementStableId == _editing.PlacementStableId)))
                _editing.PlacementStableId = EditorGUILayout.TextField(
                    "배치 ID", _editing.PlacementStableId);
            _editing.AssetReferenceKindCode = EditorGUILayout.TextField(
                "참조 종류", _editing.AssetReferenceKindCode);
            _editing.AssetKey = EditorGUILayout.TextField("의미 키", _editing.AssetKey);
            _editing.TargetContainerStableId = DrawContainerPopup(
                _editing.TargetContainerStableId);
            _editing.TargetNodeStableId = EditorGUILayout.TextField(
                "대상 node", _editing.TargetNodeStableId);
            _editing.LandCoverCode = EditorGUILayout.TextField(
                "토지피복", _editing.LandCoverCode);
            _editing.RegionRoleCode = EditorGUILayout.TextField(
                "영역 역할", _editing.RegionRoleCode);
            _editing.EvidenceKindCode = EditorGUILayout.TextField(
                "근거", _editing.EvidenceKindCode);
            _editing.Position.X = EditorGUILayout.FloatField("X", _editing.Position.X);
            _editing.Position.Z = EditorGUILayout.FloatField("Z", _editing.Position.Z);
            _editing.RotationY = EditorGUILayout.FloatField("Y 회전", _editing.RotationY);
            _editing.UniformScale = EditorGUILayout.FloatField("균일 축척", _editing.UniformScale);
            _editing.DensityTier = EditorGUILayout.IntSlider("밀도 단계", _editing.DensityTier, 0, 2);
            _editing.LodGroup = EditorGUILayout.IntSlider("LOD", _editing.LodGroup, 0, 2);
            _editing.HasWaterMask = EditorGUILayout.Toggle("수계 마스크", _editing.HasWaterMask);
            _editing.SeasonCode = EditorGUILayout.TextField("계절", _editing.SeasonCode);
            _editing.MoodCode = EditorGUILayout.TextField("분위기", _editing.MoodCode);
            _editing.Enabled = EditorGUILayout.Toggle("활성", _editing.Enabled);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("보정 저장")) SaveEditing();
            if (GUILayout.Button("비활성/추가 취소")) DisableOrRemove();
            if (GUILayout.Button("보정 되돌리기")) RevertOverride();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private string DrawContainerPopup(string current)
        {
            var names = _containers.Select(item => item.DisplayName).ToArray();
            var index = Math.Max(0, _containers.ToList().FindIndex(item => item.StableId == current));
            var selected = EditorGUILayout.Popup("컨테이너", index, names);
            return _containers.Count == 0 ? current : _containers[selected].StableId;
        }

        private void DrawValidationPanel()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(180f));
            EditorGUILayout.LabelField("검증 기록", EditorStyles.boldLabel);
            if (_report == null)
            {
                EditorGUILayout.LabelField("검증·Staging을 실행하면 오류와 예산이 표시됩니다.");
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.LabelField(
                $"오류 {_report.ErrorCount} / 경고 {_report.WarningCount}  |  "
                + $"Staging {_report.CanStage} / Scene 적용 {_report.CanApply}");
            DrawBudget("Triangle", _report.PerformanceTotal.Triangles, _report.PerformanceBudget.TriangleLimit);
            DrawBudget("Draw Call", _report.PerformanceTotal.DrawCalls, _report.PerformanceBudget.DrawCallLimit);
            _issueScroll = EditorGUILayout.BeginScrollView(_issueScroll);
            foreach (var issue in _report.Issues)
                EditorGUILayout.LabelField(
                    $"[{issue.SeverityCode}] {issue.IssueCode} {issue.PlacementStableId} — {issue.Detail}",
                    EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawBudget(string label, long value, long limit)
        {
            var rect = EditorGUILayout.GetControlRect(false, 17f);
            EditorGUI.ProgressBar(rect, limit <= 0 ? 1f : Mathf.Clamp01((float)value / limit),
                $"{label}: {value} / {limit}");
        }

        private IEnumerable<정적경관배치ItemData> FilteredPlacements()
        {
            var values = _mergedPlan!.Placements.Where(item =>
                item.TargetContainerStableId == _selectedContainerId);
            if (!string.IsNullOrWhiteSpace(_search))
                values = values.Where(item =>
                    item.PlacementStableId.Contains(_search, StringComparison.OrdinalIgnoreCase)
                    || item.AssetKey.Contains(_search, StringComparison.OrdinalIgnoreCase)
                    || item.LandCoverCode.Contains(_search, StringComparison.OrdinalIgnoreCase)
                    || item.RegionRoleCode.Contains(_search, StringComparison.OrdinalIgnoreCase));
            return values.OrderBy(item => item.PlacementStableId, StringComparer.Ordinal);
        }

        private void SelectContainer(string stableId)
        {
            _selectedContainerId = stableId;
            _selectedPlacementId = string.Empty;
            _editing = null;
            _canvasPan = Vector2.zero;
            _canvasZoom = 1f;
            Repaint();
        }

        private void SelectPlacement(string stableId)
        {
            var placement = _mergedPlan!.Placements.First(item =>
                item.PlacementStableId == stableId);
            _selectedPlacementId = stableId;
            _selectedContainerId = placement.TargetContainerStableId;
            _editing = 정적경관배치PlanMerger.Clone(placement);
            Repaint();
        }

        private void CreateNewPlacement()
        {
            var source = _mergedPlan!.Placements.FirstOrDefault(item =>
                item.TargetContainerStableId == _selectedContainerId)
                ?? _mergedPlan.Placements.First();
            _editing = 정적경관배치PlanMerger.Clone(source);
            _editing.PlacementStableId = "scenic:manual:pyeongchang:"
                + Guid.NewGuid().ToString("N");
            _editing.TargetContainerStableId = string.IsNullOrWhiteSpace(_selectedContainerId)
                ? source.TargetContainerStableId
                : _selectedContainerId;
            _editing.Enabled = true;
            _editing.PresentationOnly = true;
            _selectedPlacementId = _editing.PlacementStableId;
        }

        private void SaveEditing()
        {
            if (_editing == null || _basePlan == null || _overridePlan == null) return;
            Run(() =>
            {
                정적경관배치PlanValidator.ValidatePlacement(_editing);
                var basePlacement = _basePlan.Placements.FirstOrDefault(item =>
                    item.PlacementStableId == _editing.PlacementStableId);
                var change = basePlacement == null
                    ? new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Add,
                        PlacementStableId = _editing.PlacementStableId,
                        Placement = 정적경관배치PlanMerger.Clone(_editing),
                    }
                    : new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Modify,
                        PlacementStableId = _editing.PlacementStableId,
                        ExpectedPlacementHashSha256 =
                            정적경관배치PlanHash.ComputePlacement(basePlacement),
                        Adjustment = FullAdjustment(_editing),
                    };
                SetChange(change);
                SaveOverrideAndRefresh();
            }, "보정 계획을 저장했습니다. 기존 Scene과 기본 계획은 변경하지 않았습니다.");
        }

        private void DisableOrRemove()
        {
            if (_editing == null || _basePlan == null || _overridePlan == null) return;
            Run(() =>
            {
                var basePlacement = _basePlan.Placements.FirstOrDefault(item =>
                    item.PlacementStableId == _editing.PlacementStableId);
                if (basePlacement == null)
                    RemoveChange(_editing.PlacementStableId);
                else
                    SetChange(new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Disable,
                        PlacementStableId = basePlacement.PlacementStableId,
                        ExpectedPlacementHashSha256 =
                            정적경관배치PlanHash.ComputePlacement(basePlacement),
                    });
                SaveOverrideAndRefresh();
            }, "배치를 비활성화하거나 수동 추가를 취소했습니다.");
        }

        private void RevertOverride()
        {
            if (_editing == null || _overridePlan == null) return;
            Run(() =>
            {
                RemoveChange(_editing.PlacementStableId);
                SaveOverrideAndRefresh();
            }, "선택 배치의 사람 보정을 되돌렸습니다.");
        }

        private void SetChange(정적경관배치OverrideChangeData change)
        {
            var changes = _overridePlan!.Changes
                .Where(item => item.PlacementStableId != change.PlacementStableId)
                .Append(change)
                .OrderBy(item => item.PlacementStableId, StringComparer.Ordinal)
                .ToArray();
            _overridePlan.Changes = changes;
        }

        private void RemoveChange(string placementStableId) =>
            _overridePlan!.Changes = _overridePlan.Changes
                .Where(item => item.PlacementStableId != placementStableId)
                .ToArray();

        private void SaveOverrideAndRefresh()
        {
            _overridePlan!.ExpectedBasePlanHashSha256 =
                정적경관배치PlanHash.Compute(_basePlan!);
            정적경관배치PlanPipeline.SaveOverridePlan(_overridePlan);
            RefreshData(_selectedPlacementId);
        }

        private static 정적경관배치AdjustmentData FullAdjustment(
            정적경관배치ItemData value) => new()
        {
            TargetContainerStableId = value.TargetContainerStableId,
            TargetNodeStableId = value.TargetNodeStableId,
            AssetReferenceKindCode = value.AssetReferenceKindCode,
            AssetKey = value.AssetKey,
            LandCoverCode = value.LandCoverCode,
            RegionRoleCode = value.RegionRoleCode,
            EvidenceKindCode = value.EvidenceKindCode,
            Position = new 정적경관배치PositionData
            {
                X = value.Position.X,
                Z = value.Position.Z,
                ExplicitY = value.Position.ExplicitY,
            },
            HeightPolicyCode = value.HeightPolicyCode,
            RotationY = value.RotationY,
            UniformScale = value.UniformScale,
            DensityTier = value.DensityTier,
            LodGroup = value.LodGroup,
            HasWaterMask = value.HasWaterMask,
            SeasonCode = value.SeasonCode,
            MoodCode = value.MoodCode,
            ViewDistance = value.ViewDistance,
            Enabled = value.Enabled,
        };

        private void ValidateAndRefresh()
        {
            Run(() =>
            {
                _report = 정적경관배치PlanPipeline.ValidateAndStage();
                RefreshData(_selectedPlacementId, preserveReport: true);
            }, "계획을 검증하고 Staging Prefab을 갱신했습니다.");
        }

        private void Approve(string state)
        {
            Run(() =>
            {
                var report = 정적경관배치PlanPipeline.ValidateAndStage();
                if (!report.CanStage)
                    throw new InvalidOperationException(
                        "StaticSceneryReviewBlockedByValidation:" + report.ErrorCount);
                정적경관배치ReviewService.ApproveCurrent(state, _reviewNote);
                _report = 정적경관배치PlanPipeline.ValidateAndStage();
                RefreshData(_selectedPlacementId, preserveReport: true);
            }, state == 정적경관배치ReviewStateCodes.ApprovedForSceneApply
                ? "현재 기획서와 병합 계획을 Scene 적용 대상으로 승인했습니다."
                : "현재 기획서와 병합 계획의 검토를 완료했습니다.");
        }

        private void RefreshData(string? restorePlacement = null, bool preserveReport = false)
        {
            if (!preserveReport) _report = null;
            try
            {
                _containers = 정적경관배치PlanPipeline.GetContainerInfos();
                _basePlan = 정적경관배치PlanPipeline.LoadBasePlan();
                _overridePlan = 정적경관배치PlanPipeline.LoadOverridePlan();
                _mergedPlan = 정적경관배치PlanMerger.Merge(_basePlan, _overridePlan);
                _review = 정적경관배치ReviewService.EvaluateCurrent(
                    _basePlan, _overridePlan, _mergedPlan);
                if (string.IsNullOrWhiteSpace(_selectedContainerId))
                    _selectedContainerId = _containers.First().StableId;
                var selection = restorePlacement ?? _selectedPlacementId;
                if (!string.IsNullOrWhiteSpace(selection)
                    && _mergedPlan.Placements.Any(item => item.PlacementStableId == selection))
                    SelectPlacement(selection);
                else
                {
                    _selectedPlacementId = string.Empty;
                    _editing = null;
                }
                _message = string.Empty;
            }
            catch (Exception error)
            {
                _basePlan = null;
                _overridePlan = null;
                _mergedPlan = null;
                _review = null;
                _message = error.Message;
                _messageType = MessageType.Error;
            }
            Repaint();
        }

        private void Run(Action action, string successMessage)
        {
            try
            {
                action();
                _message = successMessage;
                _messageType = MessageType.Info;
            }
            catch (Exception error)
            {
                _message = error.Message;
                _messageType = MessageType.Error;
                Debug.LogException(error);
            }
            Repaint();
        }
    }
}
