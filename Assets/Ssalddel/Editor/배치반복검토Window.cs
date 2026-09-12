using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public sealed class 배치반복검토Window : EditorWindow
    {
        private Transform 대상;
        private Collider 지지면;
        private Transform 주변묶음;
        private Collider 통행보호;
        private readonly 배치검토설정 설정 = new();
        private 배치검토결과 결과;
        private Vector2 스크롤;

        [MenuItem("Ssalddel/통합 월드/배치/선택 객체 반복 검토")]
        public static void 열기() => GetWindow<배치반복검토Window>("배치 반복 검토");

        private void OnEnable() => SceneView.duringSceneGui += 표본표시;
        private void OnDisable() => SceneView.duringSceneGui -= 표본표시;

        private void OnGUI()
        {
            스크롤 = EditorGUILayout.BeginScrollView(스크롤);
            EditorGUILayout.HelpBox("프로젝트 배치 Wrapper를 선택하세요. 검사는 읽기 전용이며 보정은 높이만 변경합니다. "
                + "Scene 자동 저장·원본 Prefab 수정·게임 상태 변경은 없습니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            대상 = (Transform)EditorGUILayout.ObjectField("배치 객체", 대상, typeof(Transform), true);
            지지면 = (Collider)EditorGUILayout.ObjectField("지지면 Collider", 지지면, typeof(Collider), true);
            주변묶음 = (Transform)EditorGUILayout.ObjectField("주변 묶음 (직계 자식)", 주변묶음, typeof(Transform), true);
            통행보호 = (Collider)EditorGUILayout.ObjectField("통행 보호 범위", 통행보호, typeof(Collider), true);
            설정.플레이어높이 = EditorGUILayout.FloatField("플레이어 높이(m)", 설정.플레이어높이);
            설정.최소이격 = EditorGUILayout.FloatField("최소 이격(m)", 설정.최소이격);
            설정.최대경사 = EditorGUILayout.FloatField("최대 지면 경사(도)", 설정.최대경사);
            설정.최대높이편차 = EditorGUILayout.FloatField("최대 지면 편차(m)", 설정.최대높이편차);
            설정.최대높이보정 = EditorGUILayout.FloatField("최대 높이 보정(m)", 설정.최대높이보정);
            if (EditorGUI.EndChangeCheck()) 결과 = null;
            if (GUILayout.Button("현재 선택을 배치 객체로 사용"))
            { 대상 = Selection.activeTransform; 결과 = null; }
            if (GUILayout.Button("현재 배치 다시 검사")) 검사(false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
                || 결과 == null || !결과.높이보정가능))
                if (GUILayout.Button("현재 입력 재검사 후 높이만 보정 (Undo 가능)")) 검사(true);

            if (결과 != null)
            {
                EditorGUILayout.HelpBox(결과.표본검사통과 ? "지정 범위의 표본 검사 통과" : "배치 재검토 필요",
                    결과.표본검사통과 ? MessageType.Info : MessageType.Warning);
                EditorGUILayout.LabelField("측정 당시 크기(m)", 결과.가시범위.size.ToString("F2"));
                EditorGUILayout.LabelField("플레이어 대비 높이", 결과.플레이어대비높이.ToString("F2") + "배");
                EditorGUILayout.LabelField("지지 표본", 결과.지지표본.Count + "/9");
                EditorGUILayout.LabelField("지면 편차 / 경사", $"{결과.높이편차:F3}m / {결과.경사:F1}도");
                EditorGUILayout.LabelField("높이 보정 제안", 결과.높이보정량.ToString("F3") + "m");
                foreach (var 문제 in 결과.문제) EditorGUILayout.HelpBox(문제, MessageType.Warning);
                foreach (var 안내 in 결과.안내) EditorGUILayout.HelpBox(안내, MessageType.Info);
            }
            EditorGUILayout.HelpBox("결과와 Scene 표본은 마지막 검사 시점 기준입니다. 이동·회전·크기 수정 뒤 다시 검사하세요. "
                + "표본 사이 돌출·실제 메시 충돌·NavMesh·문 개폐·Game View는 별도 검증입니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void 검사(bool 보정)
        {
            try
            {
                var 주변 = 주변묶음 == null ? Array.Empty<Transform>() : 주변묶음.Cast<Transform>()
                    .Where(t => t != 대상).ToArray();
                var 보호 = 통행보호 == null ? Array.Empty<Collider>() : new[] { 통행보호 };
                결과 = 보정 ? 배치반복검토Service.높이보정(대상, 지지면, 설정, 주변, 보호)
                    : 배치반복검토Service.검사(대상, 지지면, 설정, 주변, 보호);
            }
            catch (InvalidOperationException 오류)
            { 결과 = new 배치검토결과(); 결과.문제.Add(오류.Message); }
            SceneView.RepaintAll();
        }

        private void 표본표시(SceneView view)
        {
            if (결과 == null) return;
            var 이전색 = Handles.color;
            Handles.color = 결과.표본검사통과 ? Color.green : Color.yellow;
            Handles.DrawWireCube(결과.가시범위.center, 결과.가시범위.size);
            foreach (var 표본 in 결과.지지표본) Handles.DrawWireDisc(표본, Vector3.up, .06f);
            Handles.color = 이전색;
        }
    }
}
