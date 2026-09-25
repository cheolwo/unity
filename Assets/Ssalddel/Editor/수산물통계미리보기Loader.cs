using System.IO;
using Ssalddel.Unity.Presentation.WorldMap;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 수산물통계미리보기Loader
    {
        [MenuItem("Tools/Mirror/로컬 수산물 통계 사본 열기")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Play Mode에서 기존 지구본을 연 뒤 사본을 선택하세요."); return; }
            var path = EditorUtility.OpenFilePanel("DB 검증을 마친 globe-preview.json 선택", "", "json");
            if (!string.IsNullOrEmpty(path)) Load(path);
        }

        public static 수산물통계미리보기View Load(string path)
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("MarinePreviewRequiresPlayMode");
            var globe = Object.FindFirstObjectByType<세계지구본View>();
            if (globe == null) throw new System.InvalidOperationException("MarinePreviewGlobeMissing");
            var json = File.ReadAllText(path);
            JsonUtility.FromJson<수산물통계PreviewSnapshot>(json).Validate();
            var view = globe.GetComponent<수산물통계미리보기View>() ?? globe.gameObject.AddComponent<수산물통계미리보기View>();
            view.hideFlags = HideFlags.DontSave;
            view.Load(json, globe);
            return view;
        }
    }
}
