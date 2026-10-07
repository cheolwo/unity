using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Runtime.WorldMap;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>조회 Controller를 유지하면서 한 지역 음식 업무의 상세 표현만 늦게 해제합니다.</summary>
    [DisallowMultipleComponent]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E5,
        "동일한 읽기 사본을 거리 상세·동 요약·구 표본 요약으로 조정한다.",
        Boundary = "구 전체 통계·지형 스트리밍·실제 업무 실행이 아니며 상태 사본을 저장하지 않는다.")]
    public sealed class 생활관찰표현Layer : MonoBehaviour
    {
        private const string 음식OS = OperationalWorldOperatingSystemIds.FoodDelivery;
        private 생활관찰표현Session session;
        private 운영관찰WorldView world;
        private 다중Os생명주기검증Layer lifecycle;
        private 사가정운영디오라마View diorama;
        private OperationalWorldPlacementInstruction[] 다른업무 = Array.Empty<OperationalWorldPlacementInstruction>();
        private OperationalWorldSceneApplyDiagnostic[] 진단 = Array.Empty<OperationalWorldSceneApplyDiagnostic>();
        private string 지역Id;
        private string 입력진단 = string.Empty;
        private bool 전체사본있음;
        private bool 표시;
        private bool 유지;
        private bool 카메라기록있음;
        private Vector3 이전위치;
        private Quaternion 이전회전;
        private float 이전크기;
        private double 마지막이동초;
        private GUIStyle labelStyle;

        public 생활관찰단계 CurrentStage => session?.CurrentStage ?? 생활관찰단계.Neighborhood;
        public bool DetailVisible => session?.DetailVisible ?? false;
        public bool DetailRetained => session?.DetailRetained ?? false;
        public int WorkCount => session?.WorkCount ?? 0;
        public string DiagnosticCode => string.IsNullOrEmpty(입력진단)
            ? session?.DiagnosticCode ?? "NotConfigured" : 입력진단;
        public IReadOnlyList<OperationalWorldPlacementInstruction> CurrentItems =>
            session?.CurrentItems ?? Array.Empty<OperationalWorldPlacementInstruction>();

        public void Configure(운영관찰WorldView view, 다중Os생명주기검증Layer lifecycleView,
            사가정운영디오라마View map, string areaStableId)
        {
            if (session != null && world == view && lifecycle == lifecycleView && diorama == map
                && 지역Id == areaStableId) return;
            Clear();
            world = view != null ? view : throw new ArgumentNullException(nameof(view));
            lifecycle = lifecycleView != null ? lifecycleView : throw new ArgumentNullException(nameof(lifecycleView));
            diorama = map;
            지역Id = areaStableId;
            session = new 생활관찰표현Session(areaStableId);
            표현정책적용();
        }

        /// <summary>기존 해석기가 승인한 전체 계획만 받습니다. 표본 실패 fallback을 만들지 않습니다.</summary>
        public bool Apply(OperationalWorldPlacementPlan plan,
            IReadOnlyList<OperationalWorldSceneApplyDiagnostic> diagnostics, DateTime utcNow)
        {
            if (session == null) throw new InvalidOperationException("LivingObservationNotConfigured");
            if (plan == null || !plan.Accepted || plan.Instructions == null) return false;
            // 기존 endpoint의 지역 식별자를 보존합니다. 다른 지역 응답을 빈 음식 목록으로 숨기지 않습니다.
            입력진단 = plan.Instructions.Any(item => item != null && item.OperatingSystemId == 음식OS
                && item.AreaStableId != 지역Id) ? "LivingObservationAreaMismatch" : string.Empty;
            var accepted = string.IsNullOrEmpty(입력진단) && session.Apply(plan, utcNow);
            다른업무 = plan.Instructions.Where(item => item != null && item.OperatingSystemId != 음식OS
                    && item.ExpiresAtUtc > utcNow).Select(복제).ToArray();
            진단 = (diagnostics ?? Array.Empty<OperationalWorldSceneApplyDiagnostic>())
                .Where(item => item != null).Select(item => new OperationalWorldSceneApplyDiagnostic
                {
                    SnapshotStableId = item.SnapshotStableId, ErrorCode = item.ErrorCode,
                    ExistingObjectFrozen = item.ExistingObjectFrozen
                }).ToArray();
            전체사본있음 = true;
            표현정책적용();
            전체사본표현(utcNow);
            return accepted;
        }

        public void RequestStage(생활관찰단계 stage, double monotonicSeconds, bool moveCamera = true)
        {
            if (session == null || !session.Request(stage, monotonicSeconds)) return;
            if (!moveCamera || diorama == null) return;
            Vector3? target = null;
            var first = session.CurrentItems.FirstOrDefault();
            if (first != null && diorama.Places != null
                && diorama.Places.TryGetValue(first.SemanticPlaceStableId, out var place))
                target = diorama.transform.TransformPoint(new Vector3(place.x, place.y, place.z));
            diorama.SetLivingObservationStage(stage, target);
        }

        public void Advance(double monotonicSeconds, DateTime utcNow)
        {
            if (session == null) return;
            session.Advance(monotonicSeconds, utcNow);
            사본만료(utcNow);
            var reenter = !유지 && session.DetailRetained;
            표현정책적용();
            if (reenter && 전체사본있음) 전체사본표현(utcNow);
            world?.Expire(utcNow);
            lifecycle?.Expire(utcNow);
        }

        private void 사본만료(DateTime utcNow)
        {
            session.Expire(utcNow);
            다른업무 = 다른업무.Where(item => item.ExpiresAtUtc > utcNow).ToArray();
            var ids = new HashSet<string>(session.CurrentItems.Select(item => item.ObjectStableId)
                .Concat(다른업무.Select(item => item.ObjectStableId)), StringComparer.Ordinal);
            진단 = 진단.Where(item => ids.Contains(item.SnapshotStableId)).ToArray();
        }

        private void 표현정책적용()
        {
            표시 = session?.DetailVisible ?? false;
            유지 = session?.DetailRetained ?? false;
            if (world != null) world.SetOperatingSystemPresentation(음식OS, 표시, 유지);
            if (lifecycle != null) lifecycle.SetOperatingSystemPresentation(음식OS, 표시, 유지);
        }

        private void 전체사본표현(DateTime utcNow)
        {
            // 부분 계획을 기존 Reconciler에 전달하면 다른 OS를 삭제하므로 항상 전체 사본을 보냅니다.
            var plan = new OperationalWorldPlacementPlan
            {
                Accepted = true,
                Instructions = 다른업무.Concat(session.CurrentItems)
                    .Where(item => item.ExpiresAtUtc > utcNow).Select(복제).ToArray()
            };
            world.Apply(plan, 진단);
            lifecycle.Apply(plan);
            lifecycle.Expire(utcNow);
        }

        private void LateUpdate()
        {
            if (session == null) return;
            var now = Time.unscaledTimeAsDouble;
            var camera = diorama != null ? diorama.ObservationCamera : null;
            if (camera != null && diorama.ModuleVisible && !세계지도표현상태.GlobeOverlayVisible)
            {
                if (!카메라기록있음 || (camera.transform.position - 이전위치).sqrMagnitude > .0001f
                    || Quaternion.Angle(camera.transform.rotation, 이전회전) > .01f
                    || Mathf.Abs(camera.orthographicSize - 이전크기) > .01f)
                    마지막이동초 = now;
                카메라기록있음 = true;
                이전위치 = camera.transform.position;
                이전회전 = camera.transform.rotation;
                이전크기 = camera.orthographicSize;
                var ratio = camera.orthographicSize / Mathf.Max(1f, diorama.WholeViewSize);
                // 경계 주변의 미세한 휠 입력은 단계를 왕복시키지 않도록 이력 폭을 둡니다.
                var stage = ratio < (CurrentStage == 생활관찰단계.Street ? .34f : .29f)
                    ? 생활관찰단계.Street
                    : ratio > (CurrentStage == 생활관찰단계.District ? 1.08f : 1.18f)
                        ? 생활관찰단계.District : 생활관찰단계.Neighborhood;
                session.Request(stage, now);
                if (session.RequestedStage != session.CurrentStage && now - 마지막이동초 < .5)
                {
                    사본만료(DateTime.UtcNow);
                    world?.Expire(DateTime.UtcNow);
                    lifecycle?.Expire(DateTime.UtcNow);
                    return;
                }
            }
            else
            {
                카메라기록있음 = false;
                session.Request(생활관찰단계.District, now);
            }
            Advance(now, DateTime.UtcNow);
        }

        private void OnGUI()
        {
            if (session == null || diorama == null || !diorama.ModuleVisible
                || 세계지도표현상태.GlobeOverlayVisible) return;
            GUI.depth = -150;
            var width = Mathf.Min(420, Screen.width - 24);
            var rect = new Rect((Screen.width - width) * .5f, 12, width, 106);
            GUI.Box(rect, GUIContent.none);
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            var samples = session.CurrentItems.Any(item => item.SourceKindCode == OperationalWorldSceneSourceKinds.VerificationSample);
            GUI.Label(new Rect(rect.x + 8, 16, width - 16, 24),
                $"{(samples ? "검증 표본" : "읽기 전용 관찰")} · 음식 업무 {WorkCount}건 · {단계이름(CurrentStage)}", labelStyle);
            var stages = new[] { 생활관찰단계.District, 생활관찰단계.Neighborhood, 생활관찰단계.Street };
            for (var i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (GUI.Button(new Rect(rect.x + 8 + i * (width - 16) / 3, 43, (width - 22) / 3, 30), 단계이름(stage)))
                    RequestStage(stage, Time.unscaledTimeAsDouble);
            }
            GUI.Label(new Rect(rect.x + 8, 77, width - 16, 32), WorkCount == 0
                ? "유효한 음식 상태 사본 없음 · 연결/만료 상태 확인"
                : "사가정 1개 지역 표본 · 구 전체 통계 아님", labelStyle);
        }

        private static string 단계이름(생활관찰단계 stage) => stage switch
        {
            생활관찰단계.District => "구 표본 요약",
            생활관찰단계.Neighborhood => "동 표본 요약",
            _ => "거리 상세"
        };

        public void Clear()
        {
            session?.Clear();
            다른업무 = Array.Empty<OperationalWorldPlacementInstruction>();
            진단 = Array.Empty<OperationalWorldSceneApplyDiagnostic>();
            전체사본있음 = false;
            입력진단 = string.Empty;
            카메라기록있음 = false;
            표현정책적용();
            if (lifecycle != null) lifecycle.ClearOperatingSystem(음식OS);
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();

        private static OperationalWorldPlacementInstruction 복제(OperationalWorldPlacementInstruction item) => new()
        {
            ObjectStableId = item.ObjectStableId, AreaStableId = item.AreaStableId,
            OperatingSystemId = item.OperatingSystemId, AnchorKey = item.AnchorKey, VisualKey = item.VisualKey,
            ActivityCode = item.ActivityCode, WorkStableId = item.WorkStableId,
            LifecycleStageCode = item.LifecycleStageCode, AttentionStateCode = item.AttentionStateCode,
            ObjectKindCode = item.ObjectKindCode, SemanticPlaceStableId = item.SemanticPlaceStableId,
            SourceKindCode = item.SourceKindCode, ScenarioRunStableId = item.ScenarioRunStableId,
            Revision = item.Revision, ExpiresAtUtc = item.ExpiresAtUtc
        };
    }
}
