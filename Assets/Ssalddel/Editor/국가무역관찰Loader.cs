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
    public static class 국가무역관찰Loader
    {
        [MenuItem("Tools/Mirror/2025 국가별 무역 사본 열기")]
        public static void Open()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Play Mode에서 DB 검증 무역 사본을 선택하세요.");return;}
            var path=EditorUtility.OpenFilePanel("globe-preview.json 선택","","json");if(!string.IsNullOrEmpty(path))Load(path);
        }
        public static 국가무역관찰View Load(string path)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("TradePreviewRequiresPlayMode");
            if(new FileInfo(path).Length>128*1024)throw new InvalidDataException("TradePreviewTooLarge");
            var bytes=File.ReadAllBytes(path);using(var sha=SHA256.Create())
                if(string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")))!=File.ReadAllText(path+".sha256").Trim())throw new InvalidDataException("TradePreviewHashMismatch");
            var json=Encoding.UTF8.GetString(bytes);JsonUtility.FromJson<국가무역Snapshot>(json).Validate();
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();if(globe==null)throw new InvalidOperationException("TradeGlobeMissing");
            var view=globe.GetComponent<국가무역관찰View>()??globe.gameObject.AddComponent<국가무역관찰View>();view.hideFlags=HideFlags.DontSave;view.Load(json,globe);return view;
        }
    }
}
