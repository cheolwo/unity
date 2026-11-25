using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.MobileGame.Editor
{
    public static class MobileGameBuild
    {
        private const string ScenePath = "Assets/HongdalMobileReview.unity";
        private static string Output => Environment.GetEnvironmentVariable("HONGDAL_MOBILE_OUTPUT")
            ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../player"));

        private static void Prepare()
        {
            PlayerSettings.companyName = "Hongdal";
            PlayerSettings.productName = "홍달 경영 시뮬레이션";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hongdal.simulation.preview");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Mobile Background", typeof(Camera));
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color32(16,26,37,255);
            new GameObject("Hongdal Mobile Session", typeof(MobileGameEntry));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        public static void Windows()
        {
            Prepare();
            Directory.CreateDirectory(Output);
            Check(BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(Output, "HongdalMobileReview.exe"), options = BuildOptions.None
            }));
        }

        public static void Android()
        {
            Directory.CreateDirectory(Output);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                File.WriteAllText(Path.Combine(Output, "android-build-blocked.json"),
                    "{\"status\":\"blocked\",\"reason\":\"Android Build Support module is not installed\",\"apkProduced\":false}");
                throw new BuildFailedException("Android Build Support가 설치되지 않아 APK를 생성하지 않았습니다.");
            }
            Prepare();
            EditorUserBuildSettings.buildAppBundle = false;
            Check(BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = Path.Combine(Output, "HongdalSimulation-preview.apk"), options = BuildOptions.None
            }));
        }

        private static void Check(BuildReport report)
        {
            File.WriteAllText(Path.Combine(Output, "build-summary.json"), JsonUtility.ToJson(new Summary
            {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, platform = report.summary.platform.ToString(),
                output = report.summary.outputPath
            }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("모바일 검토 빌드 실패");
        }
        [Serializable] private sealed class Summary { public string result, platform, output; public int errors, warnings; }
    }
}
