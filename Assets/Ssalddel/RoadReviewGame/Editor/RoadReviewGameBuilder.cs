using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.RoadReviewGame.Editor
{
    /// <summary>공식 Scene은 저장하지 않고 전용 로컬 플레이어의 빌드 사본만 조립한다.</summary>
    [InitializeOnLoad]
    public static class RoadReviewGameBuilder
    {
        public const string CanonicalScene = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        const string PreviewSetupKey = "RoadReviewGame.PreviewSceneSetup";
        internal static bool BuildingRoadGame { get; private set; }

        [Serializable] sealed class SavedSetup { public SceneSetup[] scenes; }

        static RoadReviewGameBuilder()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                var json = SessionState.GetString(PreviewSetupKey, "");
                if (string.IsNullOrEmpty(json)) return;
                SessionState.EraseString(PreviewSetupKey);
                // 이 메뉴가 만든 비저장 연습 사본만 닫고, 시작 전의 저장된 Scene 구성을 되돌린다.
                EditorApplication.delayCall += () =>
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SavedSetup>(json).scenes);
            };
        }

        [MenuItem("Ssalddel/도로 탐험/로컬 게임 빌드")]
        public static void Build()
        {
            var output = Argument("-buildOutput");
            if (string.IsNullOrEmpty(output))
                output = Path.GetFullPath("artifacts/road-review-game-r1/player/RoadReviewGame.exe");
            output = Path.GetFullPath(output);
            if (!output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "RoadReviewGame.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var before = Hash(CanonicalScene);
            var originalBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                BuildingRoadGame = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CanonicalScene },
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = output,
                    options = BuildOptions.Development,
                });
                var after = Hash(CanonicalScene);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-result.json"),
                    JsonConvert.SerializeObject(new
                    {
                        schema = "private-road-review-game-build.v1",
                        utc = DateTime.UtcNow.ToString("O"),
                        result = report.summary.result.ToString(),
                        errors = report.summary.totalErrors,
                        warnings = report.summary.totalWarnings,
                        canonicalScene = CanonicalScene,
                        canonicalSceneBeforeSha256 = before,
                        canonicalSceneAfterSha256 = after,
                        canonicalScenePreserved = before == after,
                        sourceFiles = Directory.GetFiles("Assets/Ssalddel/RoadReviewGame", "*.cs", SearchOption.AllDirectories)
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .Select(value => new { path = value.Replace('\\', '/'), sha256 = Hash(value) }).ToArray(),
                        output,
                        operationalWrites = false,
                        userApprovalsExecutedByBuild = false,
                    }, Formatting.Indented));
                if (before != after) throw new InvalidOperationException("CanonicalSceneChanged");
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("RoadGameBuildFailed:" + report.summary.result);
                Debug.Log("RoadReviewGameBuildSucceeded:" + output);
            }
            finally
            {
                BuildingRoadGame = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, originalBackend);
            }
        }

        [MenuItem("Ssalddel/도로 탐험/현재 Scene에서 연습 시작 (저장 안 함)")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("StopPlayBeforeRoadGamePreview");
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("SaveCurrentUserSceneFirst");
            SessionState.SetString(PreviewSetupKey, JsonUtility.ToJson(new SavedSetup
                { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            var scene = EditorSceneManager.OpenScene(CanonicalScene, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
            var host = new GameObject("RoadReviewGame_LocalOnly_DoNotSave");
            host.AddComponent<RoadReviewGameController>().ConfigureBoardShader(
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorApplication.isPlaying = true;
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }

    sealed class RoadReviewGameSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 10000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // 명시 빌드 중에만 빌드 메모리의 Scene을 교체한다. 저장 원본은 변경하지 않는다.
            if (!RoadReviewGameBuilder.BuildingRoadGame || report == null) return;
            if (scene.path != RoadReviewGameBuilder.CanonicalScene)
                throw new InvalidOperationException("UnexpectedRoadGameBuildScene");
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            var host = new GameObject("RoadReviewGame_LocalOnly");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<RoadReviewGameController>().ConfigureBoardShader(
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        }
    }
}
