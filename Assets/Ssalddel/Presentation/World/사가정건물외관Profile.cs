using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 비공개 외관 검토의 입력이다. ReviewedPrivatePresentation은 지도 선분의 사용자 승인,
    /// 공개 허가, 건물 동일성 또는 실측 정확도를 뜻하지 않으며 서로 자동 승계하지 않는다.
    /// 원본 경로, OCR 원문과 고객 정보는 전달하지 않고 별도 비공개 관측의 식별자만 참조한다.
    /// </summary>
    [Serializable]
    public sealed class 사가정건물외관Profile
    {
        public const string SchemaVersion = "station-building-appearance-profile.v1";
        public const string ReviewedState = "ReviewedPrivatePresentation";
        public const string CanonicalScope = "CanonicalSagajeong";
        public const string PrivateStudyScope = "PrivateBuildingStudy";
        public string schemaVersion = SchemaVersion;
        public string scopeKind = CanonicalScope;
        public string profileStableId = string.Empty;
        public string targetBuildingId = string.Empty;
        // 연구 scope에서는 정본 지도 hash가 아니라 연구의 명시적 기반 형상 묶음 hash다.
        public string baseMapHashSha256 = string.Empty;
        public string spatialContentHashSha256 = string.Empty;
        public string reviewState = string.Empty;
        public string reviewerKind = string.Empty;
        public string reviewReference = string.Empty;
        public string evidenceObservationId = string.Empty;
        public string evidenceSourceSha256 = string.Empty;
        public long evidenceSourcePts;
        public long evidenceTimeBaseNumerator;
        public long evidenceTimeBaseDenominator;
        public string visualKey = string.Empty;
        public string visualCatalogRevision = string.Empty;
        public string moduleKind = string.Empty;
        // 기존 geography 부모의 미터 좌표다. 영상 픽셀이나 지리 위경도가 아니다.
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public bool presentationOnly = true;
        public bool distributionApproved;
    }

    /// <summary>
    /// 기존 VisualKey 중 별도로 외관 부품임을 검토한 허용 목록. 새 자산 대장이 아니며
    /// 비어 있으면 아무것도 적용하지 않는다. 전체 건물 자산은 부품으로 허용하지 않는다.
    /// </summary>
    public sealed class 사가정외관ModuleReview
    {
        public string visualKey = string.Empty;
        public string catalogRevision = string.Empty;
        public string moduleKind = string.Empty;
        public string reviewerKind = string.Empty;
        public string reviewReference = string.Empty;
    }
}
