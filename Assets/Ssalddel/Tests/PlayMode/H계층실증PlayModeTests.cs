using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class H계층실증PlayModeTests
    {
        private const string ShellRootName = "SimulationWorldShell";
        private const string EvidenceRootName = "H계층실증Root_PresentationOnly";

        [UnityTest]
        public IEnumerator SimulationWorldShell은_Player와_H계층환경을_함께표시한다()
        {
            yield return SceneManager.LoadSceneAsync(
                "SimulationWorldShell", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var shell = GameObject.Find(ShellRootName);
            Assert.That(shell, Is.Not.Null);
            var evidenceRoot = shell.transform.Find(EvidenceRootName);
            Assert.That(evidenceRoot, Is.Not.Null);
            Assert.That(evidenceRoot!.gameObject.activeInHierarchy, Is.True);
            var evidence = evidenceRoot.GetComponent<H계층배치증거Root>();
            Assert.That(evidence, Is.Not.Null);
            Assert.DoesNotThrow(evidence!.Validate);
            Assert.That(evidence.ShowHierarchyOverlayOnPlay, Is.False);
            Assert.That(evidence.AreaSetRoots.All(value =>
                !value.H3Boundaries.gameObject.activeInHierarchy
                && !value.H2Boundaries.gameObject.activeInHierarchy), Is.True);
            Assert.DoesNotThrow(evidence.PlacementRuleEvidence.Validate);
            Assert.That(evidence.PlacementRuleEvidence.Violations, Is.Empty);
            Assert.That(evidence.PlacementRuleEvidence.RelocatedObjectCount,
                Is.GreaterThan(0));
            var placementHierarchy = evidence.PlacementRuleEvidence
                .PlacementControlHierarchy;
            Assert.That(placementHierarchy, Is.Not.Null);
            Assert.DoesNotThrow(placementHierarchy.Validate);
            Assert.That(placementHierarchy.PlayerReferenceHeight,
                Is.InRange(.8f, 3f));
            Assert.That(evidence.PlacementRuleEvidence.H1Records.All(value =>
                placementHierarchy.FindBand(value.ScaleBandCode).Contains(
                    value.HeightToPlayerRatio,
                    value.FootprintToPlayerRatio)), Is.True);
            var frontage = evidence.PlacementRuleEvidence
                .RoadsideFrontagePatterns.Single();
            Assert.DoesNotThrow(frontage.Validate);
            Assert.That(frontage.EntrancePathClear, Is.True);
            Assert.That(frontage.H3LandscapeNodeName,
                Is.EqualTo("H3_HomeProcessing"));
            Assert.That(frontage.H2BlockNodeName,
                Is.EqualTo("H2_h2-candidate:farm-worker-support"));

            var policy = shell.GetComponent<단일PlayerViewController>();
            Assert.That(policy, Is.Not.Null);
            policy!.ApplyView();
            Assert.That(shell.GetComponentsInChildren<플레이어경관Controller>(true),
                Has.Length.EqualTo(1));
            Assert.That(evidenceRoot.GetComponentsInChildren<Renderer>(true),
                Is.Not.Empty);
            Assert.That(evidenceRoot.GetComponentsInChildren<Renderer>(true)
                .Any(value => value.enabled && value.gameObject.activeInHierarchy),
                Is.True);
            Assert.That(evidenceRoot.GetComponentsInChildren<Renderer>(true)
                .Where(IsReviewHelper).All(value => !value.enabled
                    || !value.gameObject.activeInHierarchy), Is.True,
                "단일 Player 보기 갱신이 검토용 H 경계·연결선을 되살리면 안 된다.");
        }

        private static bool IsReviewHelper(Renderer renderer) =>
            renderer.sharedMaterials.Any(material => material != null &&
                new[]
                {
                    "H3경계", "H2경계", "연결지점", "WI동선",
                    "H1공간경계", "H2기준바닥", "H2내부관계", "H2외부연결구",
                }.Any(prefix => material.name.StartsWith(prefix,
                    System.StringComparison.Ordinal)));
    }
}
