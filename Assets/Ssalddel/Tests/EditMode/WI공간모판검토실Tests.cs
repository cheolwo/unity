using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class WI공간모판검토실Tests
    {
        [Test]
        public void VisualCatalog는_5개모판_9개공간_13개WI_27개후보를_보존한다()
        {
            var catalog = LoadCatalog();

            catalog.Validate();
            Assert.That(WI공간모판VisualCatalog.HierarchyLevelCode, Is.EqualTo("H1"));
            Assert.That(catalog.Entries.Count, Is.EqualTo(5));
            Assert.That(catalog.Entries.Sum(value => value.Spaces.Count), Is.EqualTo(9));
            Assert.That(catalog.Entries.SelectMany(value => value.IncludedWiIds)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(13));
            Assert.That(catalog.Entries.SelectMany(value => value.UniqueCandidates)
                .Select(value => value.CompositionKey)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(27));
            Assert.That(catalog.Entries.SelectMany(value => value.UniqueCandidates)
                .All(value => value.Prefab != null), Is.True);
        }

        [Test]
        public void VisualCatalog는_원본과_Unity경관대장의_Hash계보를_가진다()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.SourceCatalogRevision,
                Is.EqualTo("simulation-world-interaction-spatial-seedbeds.r1"));
            Assert.That(catalog.WorldInteractionCatalogRevision,
                Is.EqualTo("simulation-world-interactions.r3"));
            Assert.That(catalog.LandscapeGrammarRevision,
                Is.EqualTo("pyeongchang-landscape-grammar.v1"));
            Assert.That(catalog.SourceCatalogHashSha256, Has.Length.EqualTo(64));
            Assert.That(catalog.UnityCompositionCatalogHashSha256, Has.Length.EqualTo(64));
            Assert.That(catalog.SyntyBindingHashSha256, Has.Length.EqualTo(64));
            Assert.That(catalog.Entries.All(value =>
                value.SourceDefinitionHashSha256.Length == 64), Is.True);
            Assert.That(catalog.PresentationOnly, Is.True);
        }

        [Test]
        public void 읽기전용Mirror는_H2_H4공간조립과_E5증거를_추가하지않는다()
        {
            var files = Directory.GetFiles(WI공간모판검토실Builder.MirrorRoot,
                "*.json", SearchOption.AllDirectories);
            Assert.That(files, Has.Length.EqualTo(7));
            var json = string.Join("\n", files.Select(File.ReadAllText));

            Assert.That(json, Does.Not.Contain("AreaSetStableId"));
            Assert.That(json, Does.Not.Contain("LandscapeGraphStableId"));
            Assert.That(json, Does.Not.Contain("TileKey"));
            Assert.That(json, Does.Not.Contain("worldPosition"));
            Assert.That(json, Does.Not.Contain("prefabPath"));
            Assert.That(json, Does.Not.Contain("assetGuid"));
            Assert.That(json, Does.Contain("\"presentationOnly\": true"));
            Assert.That(json, Does.Contain("\"isOperationalState\": false"));
        }

        [Test]
        public void 검토실Scene은_개요와_InputSystem전용UI를_저장한다()
        {
            EditorSceneManager.OpenScene(WI공간모판검토실Builder.ScenePath,
                OpenSceneMode.Single);
            var presenter = UnityEngine.Object.FindFirstObjectByType<WI공간모판검토Presenter>();

            Assert.That(presenter, Is.Not.Null);
            presenter.ValidateWiring();
            Assert.That(UnityEngine.Object.FindObjectsByType<WI공간모판OverviewItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(9));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>(),
                Is.Not.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<StandaloneInputModule>(), Is.Null);
            Assert.That(GameObject.Find("WI공간모판검토실"), Is.Not.Null);
            Assert.That(GameObject.Find("SimulationWorldShell"), Is.Null);
        }

        [Test]
        public void 변경없는_빠른새로고침은_Catalog과_Scene을_다시쓰지않는다()
        {
            WI공간모판검토실Builder.RefreshSourceAndCatalog();
            var trackedPaths = Directory.GetFiles(WI공간모판검토실Builder.MirrorRoot,
                    "*.json", SearchOption.AllDirectories)
                .Append(WI공간모판검토실Builder.VisualCatalogPath)
                .Append(WI공간모판검토실Builder.ScenePath)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var before = trackedPaths.ToDictionary(value => value,
                value => File.GetLastWriteTimeUtc(value).Ticks, StringComparer.Ordinal);

            WI공간모판검토실Builder.RefreshSourceAndCatalog();

            Assert.That(trackedPaths.All(value =>
                File.GetLastWriteTimeUtc(value).Ticks == before[value]), Is.True);
        }

        [Test]
        public void 모든모판의_대표와_후보비교를_선택할수있다()
        {
            EditorSceneManager.OpenScene(WI공간모판검토실Builder.ScenePath,
                OpenSceneMode.Single);
            var presenter = UnityEngine.Object.FindFirstObjectByType<WI공간모판검토Presenter>();
            Assert.That(presenter, Is.Not.Null);
            presenter.Initialize();

            Assert.That(presenter.CurrentModeLabel, Does.Contain("증거 E4"));
            Assert.That(presenter.CurrentModeLabel, Does.Contain("공간 계층 H1"));

            foreach (var entry in presenter.Catalog.Entries)
            {
                presenter.ShowSeedbed(entry.StableId);
                Assert.That(presenter.Mode, Is.EqualTo(WI공간모판검토Mode.Detail));
                Assert.That(presenter.SelectedSeedbedStableId, Is.EqualTo(entry.StableId));
                Assert.That(presenter.ActiveDetailCandidateCount, Is.EqualTo(1));
                var preferredGround = GameObject.Find("선호크기바닥");
                Assert.That(preferredGround, Is.Not.Null);
                Assert.That(preferredGround.transform.localScale.x,
                    Is.EqualTo(entry.PreferredSizeMeters.x).Within(.01f));
                Assert.That(preferredGround.transform.localScale.z,
                    Is.EqualTo(entry.PreferredSizeMeters.y).Within(.01f));
                foreach (var space in entry.Spaces)
                {
                    presenter.SelectSpace(space.SpaceCode);
                    Assert.That(presenter.SelectedSpaceCode, Is.EqualTo(space.SpaceCode));
                    foreach (var candidate in space.Candidates)
                    {
                        presenter.SelectCandidate(candidate.CompositionKey);
                        Assert.That(presenter.SelectedCompositionKey,
                            Is.EqualTo(candidate.CompositionKey));
                        Assert.That(presenter.ActiveDetailCandidateCount, Is.EqualTo(1));
                    }
                }

                presenter.ShowCandidateSheet(entry.StableId);
                Assert.That(presenter.Mode, Is.EqualTo(WI공간모판검토Mode.CandidateSheet));
                Assert.That(presenter.ActiveSheetCandidateCount,
                    Is.EqualTo(entry.UniqueCandidates.Count()));
            }

            presenter.ShowOverview();
            Assert.That(presenter.Mode, Is.EqualTo(WI공간모판검토Mode.Overview));
            Assert.That(presenter.ActiveDetailCandidateCount, Is.Zero);
            Assert.That(presenter.ActiveSheetCandidateCount, Is.Zero);
        }

        private static WI공간모판VisualCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WI공간모판VisualCatalog>(
                WI공간모판검토실Builder.VisualCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }
    }
}
