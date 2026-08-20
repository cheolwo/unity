using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public sealed class SyntyH공간조립검토Window : EditorWindow
    {
        private static readonly string[] HierarchyLevels = { "H1", "H2", "H3", "H4" };

        private GameObject selectedRoot;
        private int hierarchyLevelIndex;
        private string h1StableId = string.Empty;
        private string h2StableId = string.Empty;
        private string h3StableId = string.Empty;
        private string h4StableId = string.Empty;
        private string displayName = string.Empty;
        private string variantCode = "A";
        private string stateProfileCode = "Default";
        private string batchStableId = string.Empty;
        private string reviewItemStableId = string.Empty;
        private string compositionStableId = string.Empty;
        private string planHash = string.Empty;
        private string parentCaptureBundleHash = string.Empty;
        private long expectedReviewItemRevision;
        private bool isRunning;
        private Vector2 scroll;

        [MenuItem("Ssalddel/Synty Web 검토/H1-H4 조합물 촬영·전송")]
        public static void Open()
        {
            var window = GetWindow<SyntyH공간조립검토Window>();
            window.titleContent = new GUIContent("H 조합물 검토");
            window.minSize = new Vector2(490f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            if (selectedRoot == null)
                selectedRoot = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("H1~H4 조합물 모바일 검토", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "선택한 장면 루트를 저장 장면 변경 없이 임시 장면에서 촬영합니다. " +
                "이미지는 표현 검토 증거이며 H 정의나 공간 권위를 자동 승인하지 않습니다.",
                MessageType.Info);

            selectedRoot = (GameObject)EditorGUILayout.ObjectField(
                "촬영할 조합물 Root", selectedRoot, typeof(GameObject), true);
            if (GUILayout.Button("현재 Hierarchy 선택 사용"))
                selectedRoot = Selection.activeGameObject;

            hierarchyLevelIndex = EditorGUILayout.Popup(
                "주 검토 계층", hierarchyLevelIndex, HierarchyLevels);
            h1StableId = EditorGUILayout.TextField("H1 고유 식별자", h1StableId);
            using (new EditorGUI.DisabledScope(hierarchyLevelIndex < 1))
                h2StableId = EditorGUILayout.TextField("H2 고유 식별자", h2StableId);
            using (new EditorGUI.DisabledScope(hierarchyLevelIndex < 2))
                h3StableId = EditorGUILayout.TextField("H3 고유 식별자", h3StableId);
            using (new EditorGUI.DisabledScope(hierarchyLevelIndex < 3))
                h4StableId = EditorGUILayout.TextField("H4 고유 식별자", h4StableId);

            displayName = EditorGUILayout.TextField("휴대폰 표시 이름", displayName);
            variantCode = EditorGUILayout.Popup("표현 변형", VariantIndex(variantCode), new[] { "A", "B", "C" }) switch
            {
                0 => "A",
                1 => "B",
                _ => "C",
            };
            stateProfileCode = EditorGUILayout.TextField("상태 표현", stateProfileCode);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("검토 계보", EditorStyles.boldLabel);
            batchStableId = EditorGUILayout.TextField("검토 묶음 ID", batchStableId);
            reviewItemStableId = EditorGUILayout.TextField("검토 항목 ID", reviewItemStableId);
            compositionStableId = EditorGUILayout.TextField("조합물 ID", compositionStableId);
            planHash = EditorGUILayout.TextField("검토 계획 SHA-256", planHash);
            if (GUILayout.Button("검토용 ID와 계획 해시 채우기"))
                FillReviewIdentity();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("재촬영 계보(선택)", EditorStyles.boldLabel);
            parentCaptureBundleHash = EditorGUILayout.TextField(
                "부모 촬영 묶음 SHA-256", parentCaptureBundleHash);
            expectedReviewItemRevision = EditorGUILayout.LongField(
                "예상 검토 개정", expectedReviewItemRevision);

            var level = HierarchyLevels[hierarchyLevelIndex];
            EditorGUILayout.HelpBox(
                $"{level} 표준 촬영은 {Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount(level)}시점입니다. " +
                "서버 주소와 관리자 토큰은 기존 환경 변수 설정을 사용합니다.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(isRunning))
            {
                if (GUILayout.Button("로컬 촬영", GUILayout.Height(34f)))
                    CaptureLocal();
                if (GUILayout.Button("촬영 후 서버 전송·검토 등록", GUILayout.Height(42f)))
                    CaptureAndUpload();
            }
            if (isRunning)
                EditorGUILayout.HelpBox("촬영 또는 업로드를 진행하고 있습니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void FillReviewIdentity()
        {
            var target = TargetStableId();
            if (selectedRoot == null || string.IsNullOrWhiteSpace(target))
            {
                EditorUtility.DisplayDialog("H 조합물 검토", "촬영 Root와 주 검토 계층의 고유 식별자를 먼저 입력하세요.", "확인");
                return;
            }

            var identity = Synty공간조립Web검토CapturePipeline.Sha256(Encoding.UTF8.GetBytes(
                "unity-h-review.v1|" + target + "|" + GlobalObjectId.GetGlobalObjectIdSlow(selectedRoot)))[..16];
            batchStableId = "review-batch:unity-h." + identity;
            reviewItemStableId = "review-item:unity-h." + identity;
            compositionStableId = "composition:unity-h." + identity;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = selectedRoot.name;
            var job = BuildJob();
            planHash = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
        }

        private void CaptureLocal()
        {
            try
            {
                isRunning = true;
                var bundle = Synty공간조립Web검토CapturePipeline.CaptureHierarchySelection(
                    selectedRoot, BuildJob(), parentCaptureBundleHash, expectedReviewItemRevision);
                EditorUtility.RevealInFinder(bundle.OutputFolder);
                Debug.Log($"H 조합물 로컬 촬영 완료: {bundle.OutputFolder}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("H 조합물 촬영 실패", exception.Message, "확인");
            }
            finally
            {
                isRunning = false;
                Repaint();
            }
        }

        private async void CaptureAndUpload()
        {
            try
            {
                isRunning = true;
                Repaint();
                var bundle = await Synty공간조립Web검토CapturePipeline
                    .CaptureUploadAndRegisterHierarchySelectionAsync(
                        selectedRoot, BuildJob(), parentCaptureBundleHash, expectedReviewItemRevision);
                Debug.Log($"H 조합물 촬영·서버 전송·검토 등록 완료: {bundle.ReviewItemStableId}");
                EditorUtility.DisplayDialog(
                    "H 조합물 검토 등록 완료",
                    "휴대폰 웹 검토함에서 확인할 수 있습니다.\n" + bundle.ReviewItemStableId,
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("H 조합물 전송 실패", exception.Message, "확인");
            }
            finally
            {
                isRunning = false;
                Repaint();
            }
        }

        private SyntyH공간조립검토Job BuildJob()
        {
            var level = HierarchyLevels[hierarchyLevelIndex];
            return new SyntyH공간조립검토Job
            {
                BatchStableId = batchStableId,
                BatchTitle = string.IsNullOrWhiteSpace(displayName) ? "Unity H 조합물 검토" : displayName + " 검토",
                ReviewItemStableId = reviewItemStableId,
                CompositionStableId = compositionStableId,
                DisplayName = displayName,
                ReviewTargetLevelCode = level,
                ReviewTargetStableId = TargetStableId(),
                H1StableId = h1StableId,
                H2StableId = hierarchyLevelIndex >= 1 ? h2StableId : string.Empty,
                H3StableId = hierarchyLevelIndex >= 2 ? h3StableId : string.Empty,
                H4StableId = hierarchyLevelIndex >= 3 ? h4StableId : string.Empty,
                VariantCode = variantCode,
                StateProfileCode = stateProfileCode,
                PlanHash = planHash,
            };
        }

        private string TargetStableId()
            => hierarchyLevelIndex switch
            {
                0 => h1StableId,
                1 => h2StableId,
                2 => h3StableId,
                _ => h4StableId,
            };

        private static int VariantIndex(string value)
            => value == "B" ? 1 : value == "C" ? 2 : 0;
    }
}
