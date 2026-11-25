using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ssalddel.WindowsGame.Editor
{
    // Android 일괄 빌드 진입점(2026-11-24 18:08 사용자: 「안드로이드 버전에서는 그냥 1.0 … 구지도까지만」 · 「세로로 하는 설정 파일 손대도 되고」).
    // Unity.exe -batchmode -quit -buildTarget Android -projectPath <p> -executeMethod Ssalddel.WindowsGame.Editor.AndroidGameBuild.Build
    //   -hongdalAndroidOutput <폴더>\hongdal.apk
    // - 동 디오라마 자료(hongdal-dong-* · hongdal-diorama.bytes · hongdal-buildings.json)는 auto-build-android.cmd가 복사 단계에서 뺀다(1.0 · 구 지도).
    // - 1단계 「맛보기」: 지금 화면(IMGUI · 1280×720 가로)을 고치지 않고 폰에 올려 본다. 그래서 지금은 가로.
    //   세로 우선 새 화면(4단계)이 들어오면 PortraitFirst를 true로 바꾼다.
    public static class AndroidGameBuild
    {
        private const string OutputFlag = "-hongdalAndroidOutput";
        private const bool PortraitFirst = true;       // 2026-11-25: 세로 화면(SagajeongDispatchPortrait.cs)이 들어와 세로로

        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, OutputFlag);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException(OutputFlag + " <apk 경로>가 필요합니다.");
            var ok = Run(Path.GetFullPath(args[index + 1]));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(string apkPath)
        {
#if UNITY_ANDROID
            var outputDirectory = Path.GetDirectoryName(apkPath);
            Directory.CreateDirectory(outputDirectory);
            if (File.Exists(apkPath)) File.Delete(apkPath);

            PlayerSettings.productName = "홍달 경영 시뮬레이션";
            PlayerSettings.companyName = "Ssalddel";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ssalddel.hongdal");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.runInBackground = false;                      // 폰: 뒤로 가면 멈추고, 다시 열면 그 사이를 셈한다
            PlayerSettings.defaultInterfaceOrientation = PortraitFirst ? UIOrientation.Portrait : UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle = false;               // 내 폰에 USB로 바로 넣는 APK(스토어용 AAB는 나중)

            var scenePath = WindowsGameBuild.CreateScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            var result = new AndroidBuildResult
            {
                result = summary.result.ToString(),
                outputPath = apkPath,
                totalSizeBytes = (long)summary.totalSize,
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                durationSeconds = summary.totalTime.TotalSeconds,
                unityVersion = Application.unityVersion,
                orientation = PortraitFirst ? "portrait" : "landscape",
                builtAtUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(Path.Combine(outputDirectory, "build-result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("Hongdal android build: " + result.result + " → " + apkPath);
            return summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
#else
            Debug.LogError("Hongdal android build: 빌드 대상이 Android가 아닙니다. -buildTarget Android로 여세요(Android Build Support 모듈 필요).");
            return false;
#endif
        }

        [Serializable]
        private sealed class AndroidBuildResult
        {
            public string result;
            public string outputPath;
            public long totalSizeBytes;
            public int totalErrors;
            public int totalWarnings;
            public double durationSeconds;
            public string unityVersion;
            public string orientation;
            public string builtAtUtc;
        }
    }
}
