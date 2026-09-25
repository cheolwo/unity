using System;
using System.IO;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.EditorTools
{
    /// <summary>
    /// 배포 자산과 분리된 로컬 검토용 건물 보조 레이어를 명시적으로 연결한다.
    /// 경로는 EditorPrefs에만 저장하며 Scene·Prefab·빌드에는 기록하지 않는다.
    /// </summary>
    public static class 사가정공개건물보조레이어검토설정
    {
        private const string RelativeArtifactPath =
            "artifacts/local/sagajeong-spatial-presentation/overture-private-review.json";

        [MenuItem("Ssalddel/통합 월드/운영 관찰/사가정 공개 건물 보조 레이어 연결")]
        public static void Connect()
        {
            var overlayPath = ResolveOverlayPath();
            if (!File.Exists(overlayPath))
                throw new FileNotFoundException("SagajeongOverturePrivateOverlayMissing");

            var mapAsset = Resources.Load<TextAsset>("SagajeongReference")
                ?? throw new InvalidOperationException("SagajeongReferenceMissing");
            var map = JsonUtility.FromJson<공간참고자료>(mapAsset.text)
                ?? throw new InvalidOperationException("SagajeongReferenceInvalid");
            var composition = 사가정공간표현OverlayPipeline.ParseAndCompose(
                File.ReadAllBytes(overlayPath), map, mapAsset.bytes,
                사가정공간표현OverlayBoundary.LocalPrivateReview);
            if (!composition.OverlayApplied
                || composition.Revision != 사가정공간표현OverlayComposer.OverturePrivateReviewRevision)
                throw new InvalidOperationException(
                    $"SagajeongOverturePrivateOverlayRejected:{composition.DiagnosticCode}");

            EditorPrefs.SetString(
                사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey, overlayPath);
            Debug.Log(
                $"SAGAJEONG-OVERTURE-PRIVATE-OVERLAY: connected=true; "
                + $"revision={composition.Revision}; buildings={composition.Buildings.Count}; "
                + "presentationOnly=true; distributionApproved=false");
        }

        [MenuItem("Ssalddel/통합 월드/운영 관찰/사가정 공개 건물 보조 레이어 연결 해제")]
        public static void Disconnect()
        {
            EditorPrefs.DeleteKey(사가정공간표현OverlayPipeline.LocalPrivateReviewPathKey);
            Debug.Log("SAGAJEONG-OVERTURE-PRIVATE-OVERLAY: connected=false");
        }

        [MenuItem("Ssalddel/통합 월드/운영 관찰/사가정 공개 건물 보조 레이어 Runtime 검증")]
        public static void ValidateRuntime()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PlayModeRequired");
            var view = UnityEngine.Object.FindFirstObjectByType<사가정운영디오라마View>()
                ?? throw new InvalidOperationException("SagajeongDioramaMissing");
            var report = view.MiniatureBuildReport
                ?? throw new InvalidOperationException("SagajeongMiniatureBuildMissing");
            if (!view.SpatialOverlayApplied
                || view.SpatialPresentationRevision
                != 사가정공간표현OverlayComposer.OverturePrivateReviewRevision
                || report.OverlayBuildingCount != 2247
                || report.RenderedBuildingCount != 2849)
                throw new InvalidOperationException("SagajeongOvertureBuildingFillMismatch");

            Debug.Log(
                $"SAGAJEONG-OVERTURE-RUNTIME: overlay={report.OverlayBuildingCount}; "
                + $"rendered={report.RenderedBuildingCount}; revision={view.SpatialPresentationRevision}; "
                + "readOnly=true; traversalReady=false; gameplayReady=false");
        }

        private static string ResolveOverlayPath()
        {
            var configuredRoot = Environment.GetEnvironmentVariable("SSALDDEL_HONGDAL_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot))
                return Path.GetFullPath(Path.Combine(configuredRoot, RelativeArtifactPath));

            var unityRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("UnityProjectRootMissing");
            var siblingRoot = Path.GetFullPath(Path.Combine(
                unityRoot, "..", "source", "repos", "Hongdal"));
            return Path.Combine(siblingRoot, RelativeArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
