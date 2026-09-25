using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Editor
{
    public static class 운송수단관찰Loader
    {
        [MenuItem("Tools/Mirror/2025 해상·항공 DB 사본 열기")]
        public static void Open()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Play Mode에서 transport-preview.json을 선택하세요.");return;}
            var path=EditorUtility.OpenFilePanel("DB 검증 운송수단 사본","","json");if(!string.IsNullOrEmpty(path))Load(path);
        }
        public static 운송수단관찰View Load(string path)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("TransportRequiresPlay");
            if(new FileInfo(path).Length>128*1024)throw new InvalidDataException("TransportSizeLimit");
            var bytes=File.ReadAllBytes(path);using(var sha=SHA256.Create())if(string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")))!=File.ReadAllText(path+".sha256").Trim())throw new InvalidDataException("TransportHashMismatch");
            var json=Encoding.UTF8.GetString(bytes);JsonUtility.FromJson<운송수단사본>(json).Validate();
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();if(globe==null||globe.transform.Find("GlobeVisualRoot")==null)throw new InvalidOperationException("TransportGlobeMissing");
            var old=globe.GetComponent<국가무역관찰View>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var hs=globe.GetComponent<Hs무역조회View>();if(hs!=null)UnityEngine.Object.DestroyImmediate(hs);
            var view=globe.GetComponent<운송수단관찰View>()??globe.gameObject.AddComponent<운송수단관찰View>();view.hideFlags=HideFlags.DontSave;view.Load(json,globe);return view;
        }
    }
}
