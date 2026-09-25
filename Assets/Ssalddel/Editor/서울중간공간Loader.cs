using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Ssalddel.Unity.Presentation.WorldMap;
namespace Ssalddel.Unity.Editor
{
    [InitializeOnLoad]
    public static class 서울중간공간Loader
    {
        private static string PreferenceKey=>"Mirror.SeoulPreview."+UnityEngine.Application.dataPath;
        private static double deadline;
        public static string AutomaticStatus {get;private set;}="NotScheduled";
        static 서울중간공간Loader()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode)Schedule();
                else if(state==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=TryAutomaticLoad;
            };
            if(EditorApplication.isPlaying)Schedule();
        }
        [MenuItem("Tools/Mirror/행정 경계 자동 준비 파일 지정")]
        public static void ChooseAutomaticSource()
        {
            var path=EditorUtility.OpenFilePanel("seoul-globe-preview.json 선택","","json");
            if(!string.IsNullOrEmpty(path))ConfigureAutomaticSource(path);
        }
        public static void ConfigureAutomaticSource(string path)
        {
            var json=ReadSnapshot(path,null);var data=JsonUtility.FromJson<서울경계사본>(json);
            if(data.schemaVersion!="seoul-intermediate-preview.v3")throw new InvalidDataException("AutomaticPreviewRequiresGeographicV3");
            // 파일/sidecar 동시 변경도 재승인 없이 적용하지 않도록 기대 hash를 별도로 기억한다.
            EditorPrefs.SetString(PreferenceKey,Path.GetFullPath(path));
            EditorPrefs.SetString(PreferenceKey+".hash",Digest(System.Text.Encoding.UTF8.GetBytes(json)));
            AutomaticStatus="Configured";if(EditorApplication.isPlaying)Schedule();
        }
        [MenuItem("Tools/Mirror/행정 경계 자동 준비 해제")]
        public static void DisableAutomaticSource()
        {
            EditorPrefs.DeleteKey(PreferenceKey);EditorPrefs.DeleteKey(PreferenceKey+".hash");
            EditorApplication.update-=TryAutomaticLoad;AutomaticStatus="Disabled";
        }
        private static void Schedule()
        {
            EditorApplication.update-=TryAutomaticLoad;
            if(!EditorPrefs.HasKey(PreferenceKey)){AutomaticStatus="NotConfigured";return;}
            deadline=EditorApplication.timeSinceStartup+15;AutomaticStatus="WaitingForGlobe";
            EditorApplication.update+=TryAutomaticLoad;
        }
        private static void TryAutomaticLoad()
        {
            if(!EditorApplication.isPlaying){EditorApplication.update-=TryAutomaticLoad;return;}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="SimulationWorldShell")
            {EditorApplication.update-=TryAutomaticLoad;AutomaticStatus="SceneNotSupported";return;}
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();
            if(globe==null || globe.transform.Find(세계지구본View.GlobeCameraName)==null)
            {
                if(EditorApplication.timeSinceStartup<deadline)return;
                EditorApplication.update-=TryAutomaticLoad;AutomaticStatus="GlobeNotReady";
                Debug.LogWarning("행정 경계 자동 준비: GlobeNotReady");return;
            }
            EditorApplication.update-=TryAutomaticLoad;
            try
            {
                var json=ReadSnapshot(EditorPrefs.GetString(PreferenceKey),EditorPrefs.GetString(PreferenceKey+".hash"));
                var existing=globe.GetComponent<서울중간공간View>();
                if(existing!=null&&existing.AreaCount>0){AutomaticStatus="ExistingPreviewPreserved";return;}
                var view=existing??globe.gameObject.AddComponent<서울중간공간View>();
                view.hideFlags=HideFlags.DontSave;view.Load(json,globe);AutomaticStatus="Ready";
            }
            catch(Exception error){AutomaticStatus="SourceRejected";Debug.LogWarning("행정 경계 자동 준비 실패: "+error.GetType().Name+" · 파일을 다시 지정하세요. 지구본을 유지합니다.");}
        }
        private static string Digest(byte[] bytes)
        {using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")));}
        public static string ReadSnapshot(string path,string expectedHash)
        {
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("SeoulPreviewSizeExceeded");
            var bytes=File.ReadAllBytes(path);var actual=Digest(bytes);
            if(actual!=File.ReadAllText(path+".sha256").Trim() || (expectedHash!=null&&actual!=expectedHash))
                throw new InvalidDataException("SeoulPreviewHashMismatch");
            var json=System.Text.Encoding.UTF8.GetString(bytes);
            var candidate=JsonUtility.FromJson<서울경계사본>(json);candidate.Validate();
            행정구역Hierarchy.FromSeoul(candidate);return json;
        }
        [MenuItem("Tools/Mirror/서울 중간 공간 검토 사본 열기")]
        public static void Open(){var path=EditorUtility.OpenFilePanel("seoul-preview.json 선택","","json");if(!string.IsNullOrEmpty(path))Load(path);}
        public static 서울중간공간View Load(string path)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("SeoulPreviewRequiresPlay");
            var json=ReadSnapshot(path,null);
            var globe=UnityEngine.Object.FindFirstObjectByType<세계지구본View>();
            if(globe==null)throw new InvalidOperationException("SeoulGlobeMissing");
            var view=globe.GetComponent<서울중간공간View>()??globe.gameObject.AddComponent<서울중간공간View>();
            view.hideFlags=HideFlags.DontSave;view.Load(json,globe);return view;
        }
    }
}
