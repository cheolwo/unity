using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Presentation.WorldMap;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class 해수면수온미리보기Loader
    {
        [MenuItem("Tools/Mirror/로컬 수온 사본 열기")]
        public static void Open()
        {
            if(!EditorApplication.isPlaying) { Debug.LogWarning("Play Mode에서 수온 사본을 선택하세요."); return; }
            var path=EditorUtility.OpenFilePanel("DB 검증 sst-preview.json 선택", "", "json");
            if(!string.IsNullOrEmpty(path)) Load(path);
        }
        public static 해수면수온미리보기View Load(string path)
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("SstPreviewRequiresPlayMode");
            if(new FileInfo(path).Length>128*1024) throw new InvalidDataException("SstPreviewTooLarge");
            var bytes=File.ReadAllBytes(path);
            using(var sha=SHA256.Create())
            {
                var hash=string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")));
                if(hash!=File.ReadAllText(path+".sha256").Trim()) throw new InvalidDataException("SstPreviewHashMismatch");
            }
            var json=Encoding.UTF8.GetString(bytes);
            JsonUtility.FromJson<해수면수온PreviewSnapshot>(json).Validate();
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();
            if(globe==null) throw new InvalidOperationException("SstPreviewGlobeMissing");
            var view=globe.GetComponent<해수면수온미리보기View>() ?? globe.gameObject.AddComponent<해수면수온미리보기View>();
            view.hideFlags=HideFlags.DontSave; view.Load(json,globe); return view;
        }
    }
}
