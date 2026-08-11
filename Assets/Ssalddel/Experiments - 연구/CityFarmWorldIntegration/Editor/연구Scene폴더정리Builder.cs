using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 연구Scene폴더정리Builder
    {
        private static readonly SceneMove[] Moves =
        {
            Potato("감자재배수확흐름"),
            Potato("감자수확포장상차흐름"),
            Potato("감자농장출발단계구현"),
            Potato("감자농장물류거점이동"),
            Potato("감자화물전체이동흐름"),
            Potato("감자물류거점입고검수흐름"),
            Potato("감자물류거점판로분배흐름"),
            Potato("감자도시도착단계구현"),
            Producer("수확물판로선택"),
            Producer("생산자조합인수흐름"),
            Producer("생산자온라인직판흐름"),
            AssetStudy("신티에셋연구소"),
        };

        [MenuItem("Ssalddel/연구 Scene/맥락별 폴더 정리")]
        public static void Organize()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
                throw new InvalidOperationException("저장되지 않은 Scene 변경이 있어 폴더 정리를 중단했습니다.");

            var activePath = activeScene.path;
            var expectedGuids = Moves.ToDictionary(
                value => value,
                value => AssetDatabase.AssetPathToGUID(AssetDatabase.LoadAssetAtPath<SceneAsset>(value.Source) != null
                    ? value.Source
                    : value.Destination));
            if (expectedGuids.Any(value => string.IsNullOrWhiteSpace(value.Value)))
                throw new InvalidOperationException("이동할 연구 Scene 가운데 찾을 수 없는 항목이 있습니다.");

            EnsureFolder(연구Scene경로.감자생산유통);
            EnsureFolder(연구Scene경로.생산자판로);
            EnsureFolder(연구Scene경로.에셋연구);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var move in Moves)
            {
                var sourceExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(move.Source) != null;
                var destinationExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(move.Destination) != null;
                if (!sourceExists && destinationExists) continue;
                if (!sourceExists || destinationExists)
                    throw new InvalidOperationException("연구 Scene 이동 경로가 충돌합니다: " + move.Source);
                var error = AssetDatabase.MoveAsset(move.Source, move.Destination);
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException("연구 Scene 이동 실패: " + error);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (var move in Moves)
                if (AssetDatabase.AssetPathToGUID(move.Destination) != expectedGuids[move])
                    throw new InvalidOperationException("연구 Scene GUID가 보존되지 않았습니다: " + move.Destination);

            var reopen = Moves.FirstOrDefault(value => value.Source == activePath);
            if (reopen != null) EditorSceneManager.OpenScene(reopen.Destination);
            Debug.Log("연구Scene폴더정리Complete:" + Moves.Length);
        }

        private static SceneMove Potato(string name)
            => Move(name, 연구Scene경로.감자생산유통);

        private static SceneMove Producer(string name)
            => Move(name, 연구Scene경로.생산자판로);

        private static SceneMove AssetStudy(string name)
            => Move(name, 연구Scene경로.에셋연구);

        private static SceneMove Move(string name, string destinationFolder)
            => new SceneMove(
                연구Scene경로.Root + "/" + name + ".unity",
                destinationFolder + "/" + name + ".unity");

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class SceneMove
        {
            public SceneMove(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }

            public string Source { get; }
            public string Destination { get; }
        }
    }
}
