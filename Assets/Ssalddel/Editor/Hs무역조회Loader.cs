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
    public static class Hs무역조회Loader
    {
        [MenuItem("Tools/Mirror/HS 코드별 DB 무역 사본 열기")]
        public static void Open(){if(!EditorApplication.isPlaying){Debug.LogWarning("Play Mode에서 HS 사본을 여세요.");return;}var p=EditorUtility.OpenFilePanel("hs-index JSON","","json");if(!string.IsNullOrEmpty(p))Load(p);}
        public static Hs무역조회View Load(string path)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("HsRequiresPlay");
            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("HsSizeLimit");
            var bytes=File.ReadAllBytes(path);using(var sha=SHA256.Create())if(string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")))!=File.ReadAllText(path+".sha256").Trim())throw new InvalidDataException("HsHashMismatch");
            var json=Encoding.UTF8.GetString(bytes);JsonUtility.FromJson<Hs조회사본>(json).Validate();
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();if(globe==null)throw new InvalidOperationException("HsGlobeMissing");
            // 통계 화면 겹침과 특정 선박이 선택 품목을 싣는다는 오해를 피한다. 원래 모드는 다시 열 수 있다.
            var mode=globe.GetComponent<운송수단관찰View>();if(mode!=null)UnityEngine.Object.DestroyImmediate(mode);
            var trade=globe.GetComponent<국가무역관찰View>();if(trade!=null)UnityEngine.Object.DestroyImmediate(trade);
            var view=globe.GetComponent<Hs무역조회View>()??globe.gameObject.AddComponent<Hs무역조회View>();view.hideFlags=HideFlags.DontSave;view.Load(json,globe);return view;
        }
    }
}
