using System;
using System.Text.Json;
using Ssalddel.Simulation.Application;
using UnityEditor;

namespace Ssalddel.Unity.Editor
{
    public static class 오프라인생활Menu
    {
        [MenuItem("Ssalddel/오프라인 생활/시작 JSON 선택·검사")]
        public static void SelectBundle()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new InvalidOperationException("OfflineLifeEditorNotStopped");
            var path=EditorUtility.OpenFilePanel("Mongo에서 내보낸 오프라인 생활 JSON","","json");if(string.IsNullOrWhiteSpace(path))return;
            var bytes=로컬생활시작자료.ReadBounded(path,로컬생활시작자료.MaxBundleBytes);
            using var doc=JsonDocument.Parse(bytes);var hash=doc.RootElement.GetProperty("ContentSha256").GetString();
            var bundle=로컬생활시작자료.Read(path,hash);
            Environment.SetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_BUNDLE",path);
            Environment.SetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_SHA256",hash);
            UnityEngine.Debug.Log("오프라인 생활 JSON 검사 완료. SimulationWorldShell의 다음 Play에서 시작합니다. NPC "+bundle.Payload.InitialRequest.LocalLife.Actors.Length+" / "+hash);
        }
        [MenuItem("Ssalddel/오프라인 생활/명시 프로필 해제")]
        public static void Disable()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("OfflineLifeEditorNotStopped");
            Environment.SetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_BUNDLE",null);
            Environment.SetEnvironmentVariable("SSALDDEL_OFFLINE_LIFE_SHA256",null);
        }
    }
}
