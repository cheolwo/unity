using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class AreaSet구성패턴검토실Tests
    {
        [Test]
        public void 공용검토실은_네AreaSet의_기준안과변형안을_구조와Synty로_보존한다()
        {
            Assert.That(File.Exists(AreaSet구성패턴검토실Builder.ScenePath), Is.True);
            EditorSceneManager.OpenScene(
                AreaSet구성패턴검토실Builder.ScenePath, OpenSceneMode.Single);
            AreaSet구성패턴검토실Builder.ValidateOpenScene();

            var root = GameObject.Find(AreaSet구성패턴검토실Builder.RootName);
            var director = root.GetComponent<AreaSet구성패턴검토Director>();
            Assert.That(director.PatternRoots, Has.Length.EqualTo(8));
            Assert.That(director.PatternRoots.Count(value =>
                value.PatternKindCode == "Baseline"), Is.EqualTo(4));
            Assert.That(director.PatternRoots.Count(value =>
                value.PatternKindCode == "Variant"), Is.EqualTo(4));
            Assert.That(director.PatternRoots.Select(value => value.AreaRoleCode)
                .Distinct(StringComparer.Ordinal), Is.EquivalentTo(new[]
            {
                "NatureHome", "Farm", "Town", "CityHub",
            }));
            Assert.That(director.PatternRoots.All(value => value.Validate()), Is.True);
            Assert.That(director.PatternRoots.All(value =>
                value.H3Boundaries.childCount >= 3
                && value.H2Boundaries.childCount >= 6
                && value.Connectors.childCount >= 6
                && value.WiRoutes.childCount >= 3
                && value.SyntyPresentation.childCount > 0), Is.True);
        }

        [Test]
        public void 기준안변형안과_구조Synty전환은_표현Root만_바꾼다()
        {
            EditorSceneManager.OpenScene(
                AreaSet구성패턴검토실Builder.ScenePath, OpenSceneMode.Single);
            var director = GameObject.Find(AreaSet구성패턴검토실Builder.RootName)
                .GetComponent<AreaSet구성패턴검토Director>();

            director.SelectArea(1);
            Assert.That(director.SelectedPattern.AreaRoleCode, Is.EqualTo("Farm"));
            Assert.That(director.SelectedPattern.PatternKindCode, Is.EqualTo("Baseline"));
            director.SetVariant(true);
            Assert.That(director.SelectedPattern.AreaRoleCode, Is.EqualTo("Farm"));
            Assert.That(director.SelectedPattern.PatternKindCode, Is.EqualTo("Variant"));
            Assert.That(director.PatternRoots.Count(value => value.gameObject.activeSelf),
                Is.EqualTo(1));
            Assert.That(director.PatternRoots.All(value => value.PresentationOnly), Is.True);
        }

        [Test]
        public void 원본영수증은_Hongdal결정적구성계획의_revision과hash를_보존한다()
        {
            var plan = File.ReadAllText(
                AreaSet구성패턴검토실Builder.MirroredPlanPath);
            var receipt = File.ReadAllText(
                AreaSet구성패턴검토실Builder.SourceReceiptPath);
            StringAssert.Contains(
                "\"schemaVersion\": \"simulation-world-area-set-composition-plans.v1\"",
                plan);
            StringAssert.Contains(
                "\"revision\": \"simulation-world-area-set-composition-plans.r1\"",
                plan);
            StringAssert.Contains(
                "\"sourceRevision\": \"simulation-world-area-set-composition-plans.r1\"",
                receipt);
            StringAssert.IsMatch("\"sourceSha256\"\\s*:\\s*\"[0-9a-f]{64}\"",
                receipt);
            var receiptHash = Regex.Match(receipt,
                "\"sourceSha256\"\\s*:\\s*\"(?<hash>[0-9a-f]{64})\"")
                .Groups["hash"].Value;
            using var sha256 = SHA256.Create();
            var mirroredHash = string.Concat(sha256.ComputeHash(
                    File.ReadAllBytes(AreaSet구성패턴검토실Builder.MirroredPlanPath))
                .Select(value => value.ToString("x2")));
            Assert.That(receiptHash, Is.EqualTo(mirroredHash));
            StringAssert.Contains("\"presentationOnly\": true", receipt);
            StringAssert.Contains("\"isOperationalState\": false", receipt);
        }
    }
}
