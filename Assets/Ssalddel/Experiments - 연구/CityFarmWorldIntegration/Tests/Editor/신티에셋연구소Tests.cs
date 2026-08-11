using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class 신티에셋연구소Tests
    {
        [OneTimeSetUp]
        public void EnsureScene()
        {
            신티에셋연구소Builder.BuildStudyCatalog();
            신티에셋연구소Builder.BuildPublicObservationSourceCatalog();
            신티에셋연구소Builder.BuildPublicObservationCatalog();
            신티에셋연구소Builder.Build();
        }

        [SetUp]
        public void OpenScene()
            => EditorSceneManager.OpenScene(신티에셋연구소Builder.ScenePath, OpenSceneMode.Single);

        [Test]
        public void 자동Index는_세Pack과1500개이상의Prefab을원본사실로보존한다()
        {
            var index = AssetDatabase.LoadAssetAtPath<에셋원본Index>(신티에셋연구소Builder.IndexPath);
            index.Validate();
            Assert.That(index.Entries.Count, Is.GreaterThan(1500));
            Assert.That(index.Entries.Select(value => value.Pack).Distinct(),
                Is.EquivalentTo(에셋PackCode.전체));
            Assert.That(index.Entries.All(value => value.Prefab != null), Is.True);
        }

        [Test]
        public void 연구Catalog는_자동Index와분리되고온실의해석을한국어로보존한다()
        {
            var index = AssetDatabase.LoadAssetAtPath<에셋원본Index>(신티에셋연구소Builder.IndexPath);
            var catalog = AssetDatabase.LoadAssetAtPath<에셋연구Catalog>(신티에셋연구소Builder.StudyCatalogPath);
            catalog.Validate();
            var greenhouse = catalog.Entries.Single(value => value.한국어이름값 == "온실 01");
            Assert.That(index.Entries.Any(value => value.원본Guid값 == greenhouse.원본Guid값), Is.True);
            Assert.That(greenhouse.연구상태값, Is.EqualTo(에셋연구상태Code.해석됨));
            Assert.That(greenhouse.현실의미값, Does.Contain("시설재배"));
            Assert.That(greenhouse.승격후보VisualKey값, Is.EqualTo("farm.facility.greenhouse"));
        }

        [Test]
        public void 연구Catalog는_마을주택과도시시청의의미를한국어로보존한다()
        {
            var index = AssetDatabase.LoadAssetAtPath<에셋원본Index>(신티에셋연구소Builder.IndexPath);
            var catalog = AssetDatabase.LoadAssetAtPath<에셋연구Catalog>(신티에셋연구소Builder.StudyCatalogPath);
            var townHouse = catalog.Entries.Single(value => value.한국어이름값 == "마을 단독주택 01");
            var cityHall = catalog.Entries.Single(value => value.한국어이름값 == "도시 시청 01");

            Assert.That(index.Entries.Single(value => value.원본Guid값 == townHouse.원본Guid값).Pack,
                Is.EqualTo(에셋PackCode.마을));
            Assert.That(townHouse.현실의미값, Does.Contain("생활 거점"));
            Assert.That(townHouse.승격후보VisualKey값, Is.EqualTo("town.place.detached-house"));
            Assert.That(index.Entries.Single(value => value.원본Guid값 == cityHall.원본Guid값).Pack,
                Is.EqualTo(에셋PackCode.도시));
            Assert.That(cityHall.현실의미값, Does.Contain("도시 기능 거점"));
            Assert.That(cityHall.승격후보VisualKey값, Is.EqualTo("city.place.civic-hall"));
        }

        [Test]
        public void 농장마을도시Button은_각묶음의대표연구카드를바로보여준다()
        {
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            var title = GameObject.Find("에셋연구소/에셋연구소Canvas/연구카드/상세제목")
                .GetComponent<Text>();

            presenter.Execute(에셋연구소ActionCode.마을보기);
            Assert.That(presenter.현재Pack값, Is.EqualTo(에셋PackCode.마을));
            Assert.That(title.text, Does.Contain("마을 단독주택"));
            Assert.That(presenter.현재표본수, Is.EqualTo(12));

            presenter.Execute(에셋연구소ActionCode.도시보기);
            Assert.That(presenter.현재Pack값, Is.EqualTo(에셋PackCode.도시));
            Assert.That(title.text, Does.Contain("도시 시청"));
            Assert.That(presenter.현재표본수, Is.EqualTo(12));

            presenter.Execute(에셋연구소ActionCode.농장보기);
            Assert.That(presenter.현재Pack값, Is.EqualTo(에셋PackCode.농장));
            Assert.That(title.text, Does.Contain("온실"));
        }

        [Test]
        public void 감자수확상자는_KAMIS연결과실제관측미수집경계를보존한다()
        {
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            var catalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(
                신티에셋연구소Builder.PublicObservationCatalogPath);
            var observation = catalog.Entries.Single(value =>
                value.관측연결Id값 == "에셋공공관측:농장:감자상자:KAMIS");
            presenter.ShowScope(에셋PackCode.농장, 에셋분류Code.식물);
            presenter.Execute(에셋연구소ActionCode.현실관측보기);
            presenter.Select(observation.원본Guid값, 0);
            var detail = GameObject.Find("에셋연구소/에셋연구소Canvas/연구카드/상세내용")
                .GetComponent<Text>().text;

            Assert.That(observation, Is.Not.Null);
            Assert.That(observation.연결상태값, Is.EqualTo(현실관측연결상태Code.실제관측미수집));
            Assert.That(observation.서버조회경로값,
                Is.EqualTo("/api/v1/agricultural-fisheries/items/0701/domestic-price"));
            Assert.That(detail, Does.Contain("실제 관측값 없음"));
            Assert.That(detail, Does.Contain("Simulation Fixture"));
            Assert.That(detail, Does.Contain("실제 KAMIS 관측값이 아니다"));
        }

        [Test]
        public void KAMIS작물모판은_직접대응18종을실제관측미수집상태로전시한다()
        {
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            var catalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(
                신티에셋연구소Builder.PublicObservationCatalogPath);
            var nursery = catalog.FindByCollection(에셋전시모음Code.KAMIS작물모판);

            Assert.That(nursery.Count, Is.EqualTo(18));
            Assert.That(nursery.Select(value => value.원본Guid값).Distinct().Count(), Is.EqualTo(18));
            Assert.That(nursery, Has.All.Matches<에셋공공관측Entry>(value =>
                value.연결상태값 == 현실관측연결상태Code.실제관측미수집
                && value.상품관계근거값.Contains("시각 대응 Direct")
                && value.Simulation비교값.Contains("별도 비교")));

            presenter.Execute(에셋연구소ActionCode.KAMIS작물모판보기);
            Assert.That(presenter.현재Pack값, Is.EqualTo(에셋PackCode.농장));
            Assert.That(presenter.현재분류값, Is.EqualTo(에셋분류Code.식물));
            Assert.That(presenter.현재전시모음값, Is.EqualTo(에셋전시모음Code.KAMIS작물모판));
            Assert.That(presenter.현재보기Mode값, Is.EqualTo(에셋연구소보기Mode.현실관측));
            Assert.That(presenter.현재표본수, Is.EqualTo(12));
            Assert.That(GameObject.Find("에셋연구소/에셋연구소Canvas/윗정보띠/현재범위")
                .GetComponent<Text>().text, Does.Contain("전체 18개"));
        }

        [Test]
        public void 공공관측출처표는_토양자료의식별자와공간시간기준을보존한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공공관측SourceCatalog>(
                신티에셋연구소Builder.PublicObservationSourceCatalogPath);
            catalog.Validate();
            var soil = catalog.Find("mafra-farmmap-soil-analysis");
            var suitability = catalog.Find("rda-soil-crop-fit-v2");

            Assert.That(soil, Is.Not.Null);
            Assert.That(soil!.자료식별자값, Is.EqualTo("15058655"));
            Assert.That(soil.공간기준값, Does.Contain("팜맵"));
            Assert.That(soil.시간기준값, Does.Contain("시료 채취일"));
            Assert.That(soil.이용조건값, Does.Contain("제1유형"));
            Assert.That(suitability, Is.Not.Null);
            Assert.That(suitability!.자료식별자값, Is.EqualTo("15144182"));
            Assert.That(suitability.필수입력값, Does.Contain("soil_Crop_CD=CR032"));
            Assert.That(suitability.요청경로값, Does.EndWith("/getSoilCropFitInfo"));
            Assert.That(suitability.표본호출결과값, Does.Contain("HTTP 403"));
            Assert.That(suitability.한계값, Does.Contain("면적 통계"));
        }

        [Test]
        public void 감자토양모판은_서로다른네에셋을공공자료연구단계와함께전시한다()
        {
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            var catalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(
                신티에셋연구소Builder.PublicObservationCatalogPath);
            var nursery = catalog.FindByCollection(에셋전시모음Code.감자토양모판);

            Assert.That(nursery.Count, Is.EqualTo(4));
            Assert.That(nursery.Select(value => value.원본Guid값).Distinct().Count(), Is.EqualTo(4));
            Assert.That(nursery.Count(value =>
                value.자료연구단계값 == 공공관측자료연구단계Code.Metadata확인), Is.EqualTo(3));
            Assert.That(nursery.Count(value =>
                value.자료연구단계값 == 공공관측자료연구단계Code.자료후보), Is.EqualTo(1));

            presenter.Execute(에셋연구소ActionCode.감자토양모판보기);
            Assert.That(presenter.현재전시모음값, Is.EqualTo(에셋전시모음Code.감자토양모판));
            Assert.That(presenter.현재표본수, Is.EqualTo(4));
            var detail = GameObject.Find("에셋연구소/에셋연구소Canvas/연구카드/상세내용")
                .GetComponent<Text>().text;
            Assert.That(detail, Does.Contain("자료 단계"));
            Assert.That(detail, Does.Contain("에셋·자료 관계"));
            Assert.That(detail, Does.Contain("실제 관측값 없음"));
        }

        [Test]
        public void 같은감자상자도_선택한모판의관측연결을우선한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(
                신티에셋연구소Builder.PublicObservationCatalogPath);
            var soilEntry = catalog.Entries.Single(value =>
                value.관측연결Id값 == "에셋공공관측:농장:감자상자:토양모판KAMIS");

            Assert.That(catalog.FindPrimaryBySourceGuid(
                    soilEntry.원본Guid값, 에셋전시모음Code.감자토양모판)?.관측연결Id값,
                Is.EqualTo(soilEntry.관측연결Id값));
        }

        [Test]
        public void 첫전시는_감자토양모판4개표본과VisualRoot를제공한다()
        {
            신티에셋연구소Builder.ValidateOpenScene();
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            Assert.That(presenter.현재Pack값, Is.EqualTo(에셋PackCode.농장));
            Assert.That(presenter.현재전시모음값, Is.EqualTo(에셋전시모음Code.감자토양모판));
            Assert.That(presenter.현재표본수, Is.EqualTo(4));
            Assert.That(GameObject.Find("에셋연구소/에셋연구소Canvas/윗정보띠/현재범위")
                .GetComponent<Text>().text, Does.Contain("감자 토양 모판"));
            Assert.That(Object.FindObjectsByType<에셋연구표본View>(FindObjectsSortMode.None)
                .All(value => value.transform.Find("VisualRoot/SyntyPrefabInstance") != null), Is.True);
        }

        [Test]
        public void 표본선택은_카메라초점과연구카드만바꾸고업무상태를확정하지않는다()
        {
            var presenter = Object.FindFirstObjectByType<에셋연구소Presenter>();
            presenter.Execute(에셋연구소ActionCode.에셋연구보기);
            var second = Object.FindObjectsByType<에셋연구표본View>(FindObjectsSortMode.None)
                .Single(value => value.전시칸번호값 == 1);
            presenter.Select(second.원본Guid값, second.전시칸번호값);
            Assert.That(presenter.선택된원본Guid, Is.EqualTo(second.원본Guid값));
            Assert.That(Object.FindFirstObjectByType<Ssalddel.Unity.Presentation.World.DioramaTopDownCameraRig>()
                .CurrentFocusAnchorId, Is.EqualTo("에셋연구소:전시칸:1"));
            Assert.That(GameObject.Find("에셋연구소/에셋연구소Canvas/연구카드/상세내용")
                .GetComponent<Text>().text, Does.Contain("연구 상태"));
            Assert.That(GameObject.Find(신티에셋연구소Builder.RootName)
                .GetComponentsInChildren<MonoBehaviour>(true).Any(value =>
                    value.GetType().Name.Contains("Command")
                    || value.GetType().Name.Contains("Simulation")
                    || value.GetType().Name.Contains("Operational")), Is.False);
        }
    }
}
