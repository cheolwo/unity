using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class 연구Scene한국어이름Tests
    {
        private const string 연구Root = "Assets/Ssalddel/Experiments - 연구";

        [Test]
        public void 연구폴더의모든Scene은_한국어중심파일명을사용한다()
        {
            var paths = ScenePaths();
            Assert.That(paths, Has.Length.EqualTo(27));
            Assert.That(paths.Select(Path.GetFileNameWithoutExtension)
                .Where(name => name.Any(character => character is >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z')), Is.Empty);
        }

        [Test]
        public void 이름을바꾼스물일곱Scene은_모두열수있다()
        {
            foreach (var path in ScenePaths())
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Assert.That(scene.IsValid(), Is.True, path);
                Assert.That(scene.rootCount, Is.GreaterThan(0), path);
                Assert.That(scene.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)), path);
            }
        }

        private static string[] ScenePaths()
            => AssetDatabase.FindAssets("t:Scene", new[] { 연구Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(value => value, System.StringComparer.Ordinal)
                .ToArray();
    }
}
