using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    /// <summary>
    /// 로컬 r7 파일 검사는 SSALDDEL_R54_REPO_ROOT가 있을 때만 수행합니다.
    /// Scene은 시험에서만 Additive로 열며 저장하지 않습니다. 실제 방문 위치를 승인하지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "동결된 한 동의 전체 지도·여섯 overlay·검토 범위와 임시 View의 소유 수명을 검증한다.",
        Boundary = "실제 방문 위치 확정, 실제 Play 전환·Game View, DB 전송 또는 E 승격의 증거가 아니다.")]
    public sealed class 방문검토지도진입Tests
    {
        private const string CanonicalScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        private const string Area = "region:kr:hjd:1126057500";
        private const string ProjectionHash = "fb5627097f1c380b7a24744174d10551fcfb171056a6c83a18cf4ecf4e939cfe";
        private const string BundleHash = "E1DBB3B99347B4139DE157274C84C881E9821A49BF064BB579813F347925E28A";
        private const string FocusHash = "30E44F71DDA3319FD73C195CD60F14FB7FE3349292798C8EFB86CEA9C30F8713";
        private const string IndexRelativePath = "artifacts/local/validation/admin-dong-diorama-observation-review/r7/input/generations/"
            + "13a3b1c6d7854d1a4a03bfdfdd7766966d9b4b447f3e7fb3c662555707c7b753/index.json";
        private readonly List<GameObject> 외부시험객체 = new();

        [Test]
        public void 동결자료검사후단일동을읽어도_전체목록과작은선택목록을보존한다()
        {
            var repo = 저장소선택();
            var scene = SceneManager.GetActiveScene();
            var 원래Root = RootIds(scene);
            var sceneCount = SceneManager.sceneCount;
            var 입력 = 방문순서재생검토진입.ValidateFrozenSources(repo);
            var 지도 = 방문순서재생검토진입.LoadFrozenSnapshot(repo, out var 범위);

            전체목록검사(지도);
            Assert.That(입력.SourceTileCount, Is.EqualTo(12));
            Assert.That(입력.SourceBuildingCount, Is.EqualTo(2700));
            Assert.That(입력.SourceRoadCount, Is.EqualTo(291));
            Assert.That(범위.Snapshot, Is.SameAs(지도));
            Assert.That(범위.SelectedBuildings, Has.Count.EqualTo(86));
            Assert.That(범위.SelectedRoads, Has.Count.EqualTo(9));
            Assert.That(범위.FocusContentHashSha256, Is.EqualTo(FocusHash));
            Assert.That(범위.SourceBundleHashSha256, Is.EqualTo(BundleHash));
            Assert.That(범위.SelectedBuildings.Select(v => v.BuildingStableId),
                Is.EquivalentTo(입력.SelectedBuildingIds));
            Assert.That(범위.SelectedRoads.Select(v => v.RoadStableId), Is.EquivalentTo(입력.SelectedRoadIds));
            Assert.That(범위.SelectedBuildings.All(v => 지도.Buildings.Contains(v)), Is.True);
            Assert.That(범위.SelectedRoads.All(v => 지도.Roads.Contains(v)), Is.True);
            Assert.That(범위.CoordinateFrameKey, Is.EqualTo(입력.CoordinateFrameKey));
            Assert.That(지도.SourceCenterX, Is.EqualTo(입력.SourceCenterX));
            Assert.That(지도.SourceCenterZ, Is.EqualTo(입력.SourceCenterZ));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(RootIds(scene), Is.EqualTo(원래Root), "파일 해석만으로 Scene 객체를 생성하면 안 됩니다.");
        }

        [Test]
        public void 여섯비공개Overlay의판본과내용Hash를그대로유지한다()
        {
            var 지도 = 방문순서재생검토진입.LoadFrozenSnapshot(저장소선택());
            Assert.That(지도.HasPrivateObservationOverlay, Is.True);
            Assert.That(지도.HasPrivateWalkNetworkOverlay, Is.True);
            Assert.That(지도.HasPrivateRoadDirectionOverlay, Is.True);
            Assert.That(지도.HasPrivateBiotopeOverlay, Is.True);
            Assert.That(지도.HasPrivateLaneMarkingOverlay, Is.True);
            Assert.That(지도.HasPrivateHorizontalContourTraceOverlay, Is.True);
            Assert.That(new[] {
                지도.PrivateObservationOverlay.Revision, 지도.PrivateWalkNetworkOverlay.Revision,
                지도.PrivateRoadDirectionOverlay.Revision, 지도.PrivateBiotopeOverlay.Revision,
                지도.PrivateLaneMarkingOverlay.Revision, 지도.PrivateHorizontalContourTraceOverlay.Revision
            }, Is.EqualTo(new[] {
                "northeast-seoul-private-observation-overlay.r1", "northeast-seoul-private-walk-network-overlay.r1",
                "northeast-seoul-private-road-direction-overlay.r1", "northeast-seoul-private-biotope-outline-overlay.r1",
                "northeast-seoul-private-lane-marking-overlay.r1", "northeast-seoul-horizontal-contour-trace-private-review.r1"
            }));
            Assert.That(new[] {
                지도.PrivateObservationOverlay.OverlaySetHashSha256, 지도.PrivateWalkNetworkOverlay.OverlaySetHashSha256,
                지도.PrivateRoadDirectionOverlay.OverlaySetHashSha256, 지도.PrivateBiotopeOverlay.OverlaySetHashSha256,
                지도.PrivateLaneMarkingOverlay.OverlaySetHashSha256, 지도.PrivateHorizontalContourTraceOverlay.OverlaySetHashSha256
            }, Is.EqualTo(new[] {
                "2BC8DCD1B1A693159F2AEE26618E3A731038D3F2F0463B211221917F73E71F49",
                "2BC976C61466F5C6879438A481340BE743A42BF7E55CBDC5BCE2CEA1F6291866",
                "E984E71DABB6164E271913E22E201F5A84C77EFA3CE10D18D0B9B39CA07C63CB",
                "3951FB30D3C7669339C2B14A9AC72E20DA34CF39B182C7B9AB9C8A04B9DC0C4B",
                "16DC656153354D34DCAA61F0F0BCE3DD71786D2C058E6F2D3C576FE3768FD3CC",
                "08B264EB18BA7F146C578BCCB97503091CB4863C68B2D56933FCF2A25403CAF7"
            }));
            Assert.That(지도.PrivateObservationOverlay.PublicDisplayAllowed, Is.False);
            Assert.That(지도.PrivateWalkNetworkOverlay.RuntimeAuthorized, Is.False);
            Assert.That(지도.PrivateRoadDirectionOverlay.RenderedArrowAuthorized, Is.False);
        }

        [Test]
        public void 동결파일사본의한바이트변경을거절하고_원본과Scene을보존한다()
        {
            var repo = 저장소선택();
            var 원본경로 = Path.Combine(repo, IndexRelativePath);
            var 원본Hash = 파일Hash(원본경로);
            var scene = SceneManager.GetActiveScene();
            var 원래Root = RootIds(scene);
            // 시험 소유의 새 임시 디렉터리만 사용하며 동결 원본은 읽기만 합니다.
            var 임시Root = Path.Combine(Path.GetTempPath(), "ssalddel-r54-test-" + Guid.NewGuid().ToString("N"));
            var 사본경로 = Path.Combine(임시Root, IndexRelativePath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(사본경로));
                var bytes = File.ReadAllBytes(원본경로);
                Array.Resize(ref bytes, bytes.Length + 1);
                bytes[bytes.Length - 1] = (byte)' ';
                File.WriteAllBytes(사본경로, bytes);
                var 오류 = Assert.Throws<InvalidOperationException>(() =>
                    방문순서재생검토진입.LoadFrozenSnapshot(임시Root));
                Assert.That(오류.Message, Is.EqualTo("VisitReviewFrozenFileHashMismatch"));
                Assert.That(오류.InnerException, Is.Null);
                Assert.That(RootIds(scene), Is.EqualTo(원래Root));
                Assert.That(파일Hash(원본경로), Is.EqualTo(원본Hash));
            }
            finally
            {
                // 생성한 파일과 빈 부모만 차례로 지웁니다. 저장소나 다른 임시 디렉터리는 삭제하지 않습니다.
                if (File.Exists(사본경로)) File.Delete(사본경로);
                var 경계 = Path.GetFullPath(임시Root);
                var 현재 = Path.GetDirectoryName(사본경로);
                while (현재 != null && (현재 == 경계 || 현재.StartsWith(경계 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    if (Directory.Exists(현재)) Directory.Delete(현재, false);
                    if (현재 == 경계) break;
                    현재 = Path.GetDirectoryName(현재);
                }
            }
        }

        [Test]
        public void canonical밖에서는_자료를읽거나지도를만들지않는다()
        {
            var 이전 = SceneManager.GetActiveScene();
            // Test Runner의 저장되지 않은 untitled Scene도 이미 유효한 거절 대상입니다.
            // 이때 Additive NewScene을 호출하면 Unity가 시험 준비 자체를 거절합니다.
            var 새로열었음 = 이전.path == CanonicalScenePath;
            var 시험Scene = 새로열었음
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive) : 이전;
            var 창 = ScriptableObject.CreateInstance<방문순서재생검토진입>();
            try
            {
                SceneManager.SetActiveScene(시험Scene);
                var 원래Root = RootIds(시험Scene);
                Assert.That(창.TryPrepareFrozenMap(string.Empty), Is.False);
                Assert.That(창.DiagnosticCode, Is.EqualTo("VisitReviewCanonicalSceneRequired"));
                Assert.That(창.PreparedMap == null, Is.True);
                Assert.That(창.Presenter == null, Is.True);
                Assert.That(RootIds(시험Scene), Is.EqualTo(원래Root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(창);
                if (이전.IsValid() && 이전.isLoaded) SceneManager.SetActiveScene(이전);
                if (새로열었음) EditorSceneManager.CloseScene(시험Scene, true);
            }
        }

        [Test]
        public void 명시준비는_한동전체지도와focus만만들고_DontSave로유지한다()
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.PreparedMap == null, Is.True, "창 생성만으로 자료를 읽거나 지도를 만들면 안 됩니다.");
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                전체목록검사(창.PreparedMap.Snapshot);
                Assert.That(창.PreparedMap.gameObject.scene, Is.EqualTo(scene));
                Assert.That(창.PreparedMap.ModelRoot, Is.Not.Null);
                Assert.That(창.PreparedMap.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
                Assert.That(창.Presenter.BoundView, Is.SameAs(창.PreparedMap));
                Assert.That(창.Presenter.FocusRoot, Is.Not.Null);
                Assert.That(창.Presenter.MarkerCount, Is.Zero, "실제 기록의 위치를 임의 생성하면 안 됩니다.");
                Assert.That(창.Presenter.CurrentFrame.State, Is.EqualTo(방문재생상태.Empty));
            });
        }

        [Test]
        public void 같은파일의반복준비는_View와Presenter를재사용하고Root를누적하지않는다()
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                var 지도 = 창.PreparedMap;
                var presenter = 창.Presenter;
                var modelRoot = 지도.ModelRoot;
                var focusRoot = presenter.FocusRoot;
                var rootIds = RootIds(scene);
                var 객체수 = 지도.GetComponentsInChildren<Transform>(true).Length;
                for (var 반복 = 0; 반복 < 3; 반복++)
                    Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                Assert.That(창.PreparedMap, Is.SameAs(지도));
                Assert.That(창.Presenter, Is.SameAs(presenter));
                Assert.That(창.PreparedMap.ModelRoot, Is.SameAs(modelRoot));
                Assert.That(창.Presenter.FocusRoot, Is.SameAs(focusRoot));
                Assert.That(RootIds(scene), Is.EqualTo(rootIds));
                Assert.That(지도.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(객체수));
            });
        }

        [Test]
        public void 준비실패는_마지막정상지도와focus와전체Root를보존한다()
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                var 지도 = 창.PreparedMap;
                var 사본 = 지도.Snapshot;
                var presenter = 창.Presenter;
                var focusRoot = presenter.FocusRoot;
                var rootIds = RootIds(scene);
                Assert.That(창.TryPrepareFrozenMap(string.Empty), Is.False);
                Assert.That(창.DiagnosticCode, Is.Not.Empty);
                Assert.That(창.PreparedMap, Is.SameAs(지도));
                Assert.That(창.PreparedMap.Snapshot, Is.SameAs(사본));
                Assert.That(창.Presenter, Is.SameAs(presenter));
                Assert.That(창.Presenter.FocusRoot, Is.SameAs(focusRoot));
                Assert.That(RootIds(scene), Is.EqualTo(rootIds));
            });
        }

        [Test]
        public void 정리는_이창의객체만지우고외부View와저장Scene을보존한다()
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                var 소유지도 = 창.PreparedMap;
                var presenter = 창.Presenter;
                var 외부Root = new GameObject("r54-synthetic-external-view") { hideFlags = HideFlags.DontSave };
                외부시험객체.Add(외부Root);
                var 외부View = 외부Root.AddComponent<행정동디오라마검토View>();
                외부View.BuildFromSnapshot(소유지도.Snapshot, false);
                외부Root.transform.position = new Vector3(7, 11, 13);
                var 사본 = 외부View.Snapshot;
                var modelRoot = 외부View.ModelRoot;
                var 객체수 = 외부Root.GetComponentsInChildren<Transform>(true).Length;
                창.ClearReview();
                Assert.That(소유지도 == null, Is.True);
                Assert.That(presenter == null, Is.True);
                Assert.That(창.PreparedMap == null, Is.True);
                Assert.That(창.Presenter == null, Is.True);
                Assert.That(외부View != null, Is.True);
                Assert.That(외부View.Snapshot, Is.SameAs(사본));
                Assert.That(외부View.ModelRoot, Is.SameAs(modelRoot));
                Assert.That(외부Root.transform.position, Is.EqualTo(new Vector3(7, 11, 13)));
                Assert.That(외부Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(객체수));
                Assert.DoesNotThrow(창.ClearReview);
            });
        }

        [Test]
        public void 창을닫으면_그창의지도와Presenter가함께정리된다()
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                var 지도 = 창.PreparedMap;
                var presenter = 창.Presenter;
                UnityEngine.Object.DestroyImmediate(창);
                Assert.That(지도 == null, Is.True);
                Assert.That(presenter == null, Is.True);
            });
        }

        [TestCase(PlayModeStateChange.ExitingEditMode)]
        [TestCase(PlayModeStateChange.ExitingPlayMode)]
        public void Play전환알림은_임시지도수명을종료한다(PlayModeStateChange 상태)
        {
            var repo = 저장소선택();
            Canonical에서((scene, 창) =>
            {
                Assert.That(창.TryPrepareFrozenMap(repo), Is.True, 창.DiagnosticCode);
                var 지도 = 창.PreparedMap;
                var presenter = 창.Presenter;
                // 실제 Play 모드 전환을 시작하지 않고 Editor가 전달하는 수명 콜백만 회귀 검증합니다.
                var callback = typeof(방문순서재생검토진입).GetMethod("OnPlayModeChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(callback, Is.Not.Null);
                callback.Invoke(창, new object[] { 상태 });
                Assert.That(지도 == null, Is.True);
                Assert.That(presenter == null, Is.True);
                Assert.That(창.PreparedMap == null, Is.True);
                Assert.That(창.Presenter == null, Is.True);
            });
        }

        [Test]
        public void 실제일곱방문원본은_근거미결속상태를유지하고재생하지않는다()
        {
            var 기록경로 = Environment.GetEnvironmentVariable("SSALDDEL_R53_RECORD_PATH");
            if (string.IsNullOrWhiteSpace(기록경로))
                Assert.Ignore("SSALDDEL_R53_RECORD_PATH가 없어 실제 방문 원본 검사를 건너뜁니다.");
            var 원본Hash = 파일Hash(기록경로);
            var 기록 = new 방문기록JsonDecoder().Decode(File.ReadAllText(기록경로));
            Assert.That(기록.Visits, Has.Length.EqualTo(7));
            Assert.That(기록.LocalReviewOnly, Is.True);
            Assert.That(기록.IsOperationalState, Is.False);
            Assert.That(기록.Visits.All(v => !v.PickupCompletionConfirmed), Is.True);
            var 준비 = 방문순서재생검토진입.PrepareRecordFile(기록경로, string.Empty, false);
            Assert.That(준비.CanReplay, Is.False);
            Assert.That(준비.Visits, Is.Empty);
            Assert.That(준비.Diagnostics, Is.Not.Empty);
            Assert.That(파일Hash(기록경로), Is.EqualTo(원본Hash));
        }

        private static string 저장소선택()
        {
            var repo = Environment.GetEnvironmentVariable("SSALDDEL_R54_REPO_ROOT");
            if (string.IsNullOrWhiteSpace(repo))
                Assert.Ignore("SSALDDEL_R54_REPO_ROOT가 없어 실제 동결 자료 검사를 건너뜁니다.");
            return repo;
        }

        private void Canonical에서(Action<Scene, 방문순서재생검토진입> 검사)
        {
            var 이전Scene = SceneManager.GetActiveScene();
            var 원본Hash = 파일Hash(CanonicalScenePath);
            var scene = SceneManager.GetSceneByPath(CanonicalScenePath);
            var 시험에서열었음 = !scene.IsValid() || !scene.isLoaded;
            if (시험에서열었음) scene = EditorSceneManager.OpenScene(CanonicalScenePath, OpenSceneMode.Additive);
            var 원래Root = RootIds(scene);
            string[] 정리후Root = null;
            var 창 = ScriptableObject.CreateInstance<방문순서재생검토진입>();
            try
            {
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                검사(scene, 창);
            }
            finally
            {
                try
                {
                    if (창 != null) { 창.ClearReview(); UnityEngine.Object.DestroyImmediate(창); }
                    foreach (var 외부객체 in 외부시험객체)
                        if (외부객체 != null) UnityEngine.Object.DestroyImmediate(외부객체);
                    외부시험객체.Clear();
                    정리후Root = RootIds(scene);
                }
                finally
                {
                    if (이전Scene.IsValid() && 이전Scene.isLoaded) SceneManager.SetActiveScene(이전Scene);
                    if (시험에서열었음) EditorSceneManager.CloseScene(scene, true);
                }
            }
            Assert.That(정리후Root, Is.EqualTo(원래Root), "정리 후 기존 Scene의 root 목록이 달라졌습니다.");
            Assert.That(파일Hash(CanonicalScenePath), Is.EqualTo(원본Hash), "저장 Scene 파일을 변경하면 안 됩니다.");
        }

        private static void 전체목록검사(행정동디오라마검토Snapshot 지도)
        {
            Assert.That(지도.AdministrativeAreaStableId, Is.EqualTo(Area));
            Assert.That(지도.ProjectionHashSha256, Is.EqualTo(ProjectionHash).IgnoreCase);
            Assert.That(지도.TileCount, Is.EqualTo(12));
            Assert.That(지도.Buildings, Has.Count.EqualTo(2700));
            Assert.That(지도.Roads, Has.Count.EqualTo(291));
            Assert.That(지도.Buildings.Select(v => v.BuildingStableId).Distinct().Count(), Is.EqualTo(2700));
            Assert.That(지도.Roads.Select(v => v.RoadStableId).Distinct().Count(), Is.EqualTo(291));
            Assert.That(지도.ObservationPresentationOnly, Is.True);
            Assert.That(지도.DistributionApproved, Is.False);
            Assert.That(지도.TraversalReady, Is.False);
            Assert.That(지도.GameplayReady, Is.False);
        }

        private static string[] RootIds(Scene scene) => !scene.IsValid() || !scene.isLoaded ? Array.Empty<string>() :
            scene.GetRootGameObjects().Select(v => v.GetEntityId().ToString()).OrderBy(v => v, StringComparer.Ordinal).ToArray();

        private static string 파일Hash(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
