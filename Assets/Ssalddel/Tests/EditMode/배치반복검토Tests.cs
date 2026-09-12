using System;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 배치반복검토Tests
    {
        private Scene 시험장면;
        private Transform 대상;
        private BoxCollider 바닥;
        private 배치검토설정 설정;

        [SetUp]
        public void 준비()
        {
            시험장면 = EditorSceneManager.NewPreviewScene();
            대상 = 상자("PlacementReviewTarget", new Vector3(0, -.5f, 0), Vector3.one).transform;
            바닥 = 상자("PlacementReviewSupport", new Vector3(0, -.25f, 0), new Vector3(100, .5f, 100))
                .GetComponent<BoxCollider>();
            설정 = new 배치검토설정();
        }

        [TearDown]
        public void 정리() => EditorSceneManager.ClosePreviewScene(시험장면);

        [Test]
        public void 검사만으로는_매몰객체를_이동하지_않는다()
        {
            var 이전 = 대상.position;
            var 결과 = 배치반복검토Service.검사(대상, 바닥, 설정);
            Assert.That(대상.position, Is.EqualTo(이전));
            Assert.That(결과.문제, Does.Contain("가시 하단 매몰"));
            Assert.That(결과.지지표본.Count, Is.EqualTo(9));
            Assert.That(결과.높이보정가능, Is.True);
            Assert.That(결과.안내.Count, Is.EqualTo(2));
        }

        [Test]
        public void 명시적_보정은_Y만_바꾸고_재보정은_멱등이다()
        {
            대상.rotation = Quaternion.Euler(0, 32, 0);
            var 회전 = 대상.rotation;
            var 크기 = 대상.localScale;
            var 결과 = 배치반복검토Service.높이보정(대상, 바닥, 설정);
            Assert.That(결과.표본검사통과, Is.True, string.Join(",", 결과.문제));
            var 보정위치 = 대상.position;
            배치반복검토Service.높이보정(대상, 바닥, 설정);
            Assert.That(대상.position, Is.EqualTo(보정위치));
            Assert.That(대상.position.x, Is.Zero);
            Assert.That(대상.position.z, Is.Zero);
            Assert.That(대상.rotation, Is.EqualTo(회전));
            Assert.That(대상.localScale, Is.EqualTo(크기));
        }

        [Test]
        public void 높이보정은_Undo로_원상복구할_수_있다()
        {
            Undo.IncrementCurrentGroup();
            var 이전 = 대상.position;
            배치반복검토Service.높이보정(대상, 바닥, 설정);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(대상.position, Is.EqualTo(이전));
        }

        [Test]
        public void 보정전_지지면이_이동하면_과거의_통과판정을_재사용하지_않는다()
        {
            Assert.That(배치반복검토Service.검사(대상, 바닥, 설정).높이보정가능, Is.True);
            바닥.transform.position += Vector3.right * 200;
            var 이전 = 대상.position;
            Assert.Throws<InvalidOperationException>(() => 배치반복검토Service.높이보정(대상, 바닥, 설정));
            Assert.That(대상.position, Is.EqualTo(이전));
        }

        [Test]
        public void 경사와_부분지지는_높이보정으로_숨기지_않는다()
        {
            바닥.transform.rotation = Quaternion.Euler(0, 0, 20);
            var 경사 = 배치반복검토Service.검사(대상, 바닥, 설정);
            Assert.That(경사.경사, Is.GreaterThan(19));
            Assert.That(경사.높이보정가능, Is.False);
            바닥.transform.rotation = Quaternion.identity;
            바닥.transform.localScale = new Vector3(.25f, .5f, .25f);
            var 부분 = 배치반복검토Service.검사(대상, 바닥, 설정);
            Assert.That(부분.지지표본.Count, Is.LessThan(9));
            Assert.That(부분.높이보정가능, Is.False);
        }

        [Test]
        public void 주변간격과_출입구를_침범하면_위치를_다시_선택해야_한다()
        {
            var 이웃 = 상자("Neighbor", new Vector3(.6f, .5f, 0), Vector3.one).transform;
            var 문 = 상자("DoorClearance", new Vector3(0, 1, 0), new Vector3(1, 2, 3)).GetComponent<BoxCollider>();
            문.isTrigger = true;
            var 결과 = 배치반복검토Service.검사(대상, 바닥, 설정, new[] { 이웃 }, new[] { 문 });
            Assert.That(결과.문제, Does.Contain("주변 간격 부족: Neighbor"));
            Assert.That(결과.문제, Does.Contain("통행 보호 구역 침범: DoorClearance"));
            Assert.That(결과.높이보정가능, Is.False);
            이웃.position += Vector3.right * 10;
            문.transform.position += Vector3.left * 10;
            결과 = 배치반복검토Service.높이보정(대상, 바닥, 설정, new[] { 이웃 }, new[] { 문 });
            Assert.That(결과.표본검사통과, Is.True);
        }

        [Test]
        public void 자기Collider와_Trigger지지면은_거부한다()
        {
            Assert.That(배치반복검토Service.검사(대상, 대상.GetComponent<Collider>(), 설정).높이보정가능, Is.False);
            바닥.isTrigger = true;
            Assert.That(배치반복검토Service.검사(대상, 바닥, 설정).높이보정가능, Is.False);
        }

        [Test]
        public void 비활성Renderer와_비정상설정은_통과하지_않는다()
        {
            대상.GetComponent<Renderer>().enabled = false;
            Assert.That(배치반복검토Service.검사(대상, 바닥, 설정).높이보정가능, Is.False);
            대상.GetComponent<Renderer>().enabled = true;
            설정.최소이격 = float.NaN;
            Assert.That(배치반복검토Service.검사(대상, 바닥, 설정).문제, Does.Contain("검토 설정 오류"));
        }

        [Test]
        public void 과도한보정은_거부하고_부유도_실패로_표시한다()
        {
            대상.position = new Vector3(0, 10, 0);
            var 결과 = 배치반복검토Service.검사(대상, 바닥, 설정);
            Assert.That(결과.문제, Does.Contain("가시 하단 부유"));
            Assert.That(결과.문제, Does.Contain("높이 보정 한도 초과"));
            Assert.That(결과.높이보정가능, Is.False);
        }

        [TestCase("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Stump_01.prefab")]
        [TestCase("Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_Shelter_01.prefab")]
        [TestCase("Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_StorageShelf_01.prefab")]
        public void 실제Synty를_위치크기회전별로_배치하고_보정후_재판정한다(string 경로)
        {
            var 원본 = AssetDatabase.LoadAssetAtPath<GameObject>(경로);
            Assert.That(원본, Is.Not.Null, 경로);
            var 원본위치 = 원본.transform.localPosition;
            var 원본크기 = 원본.transform.localScale;
            var 객체 = (GameObject)PrefabUtility.InstantiatePrefab(원본, 시험장면);
            대상.gameObject.SetActive(false);
            // 공급자 Prefab은 손대지 않고 프로젝트 Wrapper만 옮긴다.
            var 래퍼 = new GameObject("SyntyPlacementTrialWrapper");
            SceneManager.MoveGameObjectToScene(래퍼, 시험장면);
            객체.transform.SetParent(래퍼.transform, false);
            for (var i = 0; i < 3; i++)
            {
                래퍼.transform.position = new Vector3(i * 5, 0, i * -3);
                래퍼.transform.localScale = Vector3.one * (.7f + i * .3f);
                래퍼.transform.rotation = Quaternion.Euler(0, i * 37, 0);
                배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(래퍼.transform, 0);
                래퍼.transform.position -= Vector3.up * .3f;
                Assert.That(배치반복검토Service.검사(래퍼.transform, 바닥, 설정).표본검사통과, Is.False);
                var 결과 = 배치반복검토Service.높이보정(래퍼.transform, 바닥, 설정);
                Assert.That(결과.표본검사통과, Is.True, 경로 + ":" + string.Join(",", 결과.문제));
                TestContext.WriteLine($"{경로};trial={i};samples={결과.지지표본.Count};size={결과.가시범위.size};heightRatio={결과.플레이어대비높이:F2}");
            }
            Assert.That(원본.transform.localPosition, Is.EqualTo(원본위치));
            Assert.That(원본.transform.localScale, Is.EqualTo(원본크기));
        }

        private GameObject 상자(string 이름, Vector3 위치, Vector3 크기)
        {
            var 객체 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            객체.name = 이름;
            SceneManager.MoveGameObjectToScene(객체, 시험장면);
            객체.transform.position = 위치;
            객체.transform.localScale = 크기;
            return 객체;
        }
    }
}
