using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// canonical SimulationWorldShell에 저장된 Missing Script를 GameObject 경로로 검출한다.
    /// Scene이나 Prefab을 수정하지 않는 읽기 전용 검증기다.
    /// </summary>
    public static class CanonicalSceneMissingScriptValidator
    {
        private const string CanonicalScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [MenuItem("Ssalddel/검증/canonical Scene Missing Script 검사")]
        public static void ValidateCanonicalScene()
        {
            var scene = EditorSceneManager.OpenScene(
                CanonicalScenePath, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(value => new MissingScriptEntry(
                    HierarchyPath(value),
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        value.gameObject)))
                .Where(value => value.Count > 0)
                .OrderBy(value => value.Path, StringComparer.Ordinal)
                .ToArray();

            foreach (var entry in missing)
                Debug.LogError(
                    $"CanonicalSceneMissingScript:path={entry.Path};count={entry.Count}");
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"CanonicalSceneMissingScriptDetected:objects={missing.Length};"
                    + $"components={missing.Sum(value => value.Count)}");

            Debug.Log("CanonicalSceneMissingScriptValidationPassed:objects=0");
        }

        private static string HierarchyPath(Transform value)
        {
            var names = new Stack<string>();
            for (var cursor = value; cursor != null; cursor = cursor.parent)
                names.Push(cursor.name);
            return string.Join("/", names);
        }

        private readonly struct MissingScriptEntry
        {
            public MissingScriptEntry(string path, int count)
            {
                Path = path;
                Count = count;
            }

            public string Path { get; }
            public int Count { get; }
        }
    }
}
