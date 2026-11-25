using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.WindowsGame.Editor
{
    // 일괄 빌드 진입점. 예:
    // Unity.exe -batchmode -quit -projectPath <p> -executeMethod Ssalddel.WindowsGame.Editor.WindowsGameBuild.Build
    //   -hongdalWindowsOutput <새 빈 폴더>\HongdalSimulation.exe
    // 장면은 모듈 폴더 안 Generated/에 만들고, 출력 폴더는 비어 있는 새 폴더여야 한다.
    public static class WindowsGameBuild
    {
        private const string OutputFlag = "-hongdalWindowsOutput";

        [MenuItem("Ssalddel/Hongdal Windows/Build Player")]
        public static void BuildFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("빈 출력 폴더", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            Run(Path.Combine(folder, "HongdalSimulation.exe"));
        }

        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, OutputFlag);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException(OutputFlag + " <exe 경로>가 필요합니다.");
            var ok = Run(Path.GetFullPath(args[index + 1]));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(string exePath)
        {
            var outputDirectory = Path.GetDirectoryName(exePath);
            if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
                throw new IOException("출력 폴더가 비어 있지 않습니다: " + outputDirectory);
            Directory.CreateDirectory(outputDirectory);

            PlayerSettings.productName = "홍달 경영 시뮬레이션";
            PlayerSettings.companyName = "Ssalddel";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // 뼈대 관문 검사: 커널·모듈·첫 인계 사슬을 코드로 돌려 보고 결과를 실행판 옆에 적는다. 실패해도 빌드는 계속한다.
            string checks;
            try
            {
                checks = HongdalKernelChecks.Run();
            }
            catch (Exception exception)
            {
                checks = "Hongdal kernel checks: 0 passed, 1 failed, 0 skipped\n[FAIL] 검사를 돌리지 못했습니다 — " + exception;
            }
            File.WriteAllText(Path.Combine(outputDirectory, "kernel-checks.txt"), checks, new System.Text.UTF8Encoding(false));
            Debug.Log(checks);

            var scenePath = CreateScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            var result = new BuildResult
            {
                result = summary.result.ToString(),
                outputPath = exePath,
                totalSizeBytes = (long)summary.totalSize,
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                durationSeconds = summary.totalTime.TotalSeconds,
                unityVersion = Application.unityVersion,
                builtAtUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(Path.Combine(outputDirectory, "build-result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("Hongdal windows build: " + result.result + " → " + exePath);
            return summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }

        internal static string CreateScene()      // AndroidGameBuild도 같은 장면을 쓴다
        {
            var asmdef = AssetDatabase.FindAssets("Ssalddel.WindowsGame t:AssemblyDefinitionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .First(path => Path.GetFileName(path) == "Ssalddel.WindowsGame.asmdef");
            var moduleRoot = Path.GetDirectoryName(Path.GetDirectoryName(asmdef)).Replace('\\', '/');
            var folder = moduleRoot + "/Generated";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(moduleRoot, "Generated");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("MainCamera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(17, 27, 33, 255);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            new GameObject("Hongdal Windows").AddComponent<SagajeongDispatchView>();
            var path = folder + "/HongdalWindows.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        [Serializable]
        private sealed class BuildResult
        {
            public string result;
            public string outputPath;
            public long totalSizeBytes;
            public int totalErrors;
            public int totalWarnings;
            public double durationSeconds;
            public string unityVersion;
            public string builtAtUtc;
        }
    }
}
