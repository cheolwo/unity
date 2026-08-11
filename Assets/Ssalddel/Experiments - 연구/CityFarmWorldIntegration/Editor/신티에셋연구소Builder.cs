using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 신티에셋연구소Builder
    {
        public const string ScenePath =
            연구Scene경로.에셋연구 + "/신티에셋연구소.unity";
        public const string IndexPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/에셋원본Index.asset";
        public const string StudyCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/에셋연구Catalog.asset";
        public const string PublicObservationCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/에셋공공관측Catalog.asset";
        public const string PublicObservationSourceCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/공공관측SourceCatalog.asset";
        public const string RootName = "에셋연구소";

        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs";
        private const string TownRoot = "Assets/Synty/PolygonTown/Prefabs";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs";
        private const string GreenhousePath =
            FarmRoot + "/Buildings/SM_Bld_Greenhouse_01.prefab";
        private const string TownHousePath =
            TownRoot + "/Buildings/Presets/SM_Bld_House_Preset_01.prefab";
        private const string CityHallPath =
            CityRoot + "/Buildings/SM_Bld_CityHall_01.prefab";
        private const string PotatoBoxPath =
            FarmRoot + "/Plants/SM_Prop_Box_Potato_01.prefab";
        private const string DirtRowsPath =
            FarmRoot + "/Environments/SM_Env_Dirt_Rows_01.prefab";
        private const string PotatoPlantPath =
            FarmRoot + "/Plants/SM_Prop_Plant_Potato_01_L.prefab";
        private const string SprinklerPath =
            FarmRoot + "/Props/SM_Prop_Sprinkler_01.prefab";
        private const string FarmProductVisualCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmProductVisualCatalog.asset";

        [MenuItem("Ssalddel/에셋 연구소/에셋연구-0 생성")]
        public static void Build()
        {
            EnsureFolder(Path.GetDirectoryName(IndexPath)!.Replace('\\', '/'));
            var index = BuildIndex();
            var studies = BuildStudyCatalog();
            var observationSources = BuildPublicObservationSourceCatalog();
            var observations = BuildPublicObservationCatalog();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "신티에셋연구소";
            var root = new GameObject(RootName).transform;
            BuildStage(root);
            var rig = BuildCamera(root);
            var showroom = new GameObject("전시장Root").transform;
            showroom.SetParent(root, false);
            var presenter = root.gameObject.AddComponent<에셋연구소Presenter>();
            var ui = BuildUi(root, presenter, rig.GetComponent<Camera>());
            presenter.Configure(index, studies, observations, observationSources, showroom, rig,
                ui.Scope, ui.DetailTitle, ui.DetailBody);
            presenter.RefreshForEditor();
            presenter.Execute(에셋연구소ActionCode.감자토양모판보기);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .transform.SetParent(root, false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            ValidateOpenScene();
            Debug.Log($"에셋연구-0 생성 완료: {index.Entries.Count}개 원본 / {studies.Entries.Count}개 연구 기록");
        }

        public static 에셋원본Index BuildIndex()
        {
            var entries = new List<에셋원본IndexEntry>();
            AddPack(entries, FarmRoot, 에셋PackCode.농장);
            AddPack(entries, TownRoot, 에셋PackCode.마을);
            AddPack(entries, CityRoot, 에셋PackCode.도시);
            var ordered = entries.OrderBy(value => value.Pack, StringComparer.Ordinal)
                .ThenBy(value => value.분류값, StringComparer.Ordinal)
                .ThenBy(value => value.원본경로값, StringComparer.Ordinal).ToArray();

            var index = AssetDatabase.LoadAssetAtPath<에셋원본Index>(IndexPath);
            if (index == null)
            {
                if (File.Exists(IndexPath)) AssetDatabase.DeleteAsset(IndexPath);
                index = ScriptableObject.CreateInstance<에셋원본Index>();
                AssetDatabase.CreateAsset(index, IndexPath);
            }
            index.Configure("신티 농장·마을·도시 원본 에셋 자동 색인 v1", ordered);
            index.Validate();
            EditorUtility.SetDirty(index);
            return index;
        }

        public static 에셋연구Catalog BuildStudyCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<에셋연구Catalog>(StudyCatalogPath);
            if (catalog == null)
            {
                if (File.Exists(StudyCatalogPath)) AssetDatabase.DeleteAsset(StudyCatalogPath);
                catalog = ScriptableObject.CreateInstance<에셋연구Catalog>();
                AssetDatabase.CreateAsset(catalog, StudyCatalogPath);
            }

            var entries = catalog.Entries.ToList();
            AddStudyIfMissing(entries, GreenhousePath,
                "에셋연구:농장:온실:01", "온실 01",
                "투명한 벽과 지붕을 가진 폐쇄형 재배 건물이다. 출입구와 내부 재배 공간이 구분된다.",
                "외부 기후를 그대로 따르지 않고 온도·습도·급수를 조절하며 작물을 기르는 시설재배 공간이다.",
                "농장 생산시설 후보. 아직 실제 생산량이나 운영 상태를 확정하지 않는다.",
                "물탱크, 물뿌리개, 작물, 농장 작업자, 수확 상자, 진입로, 운송 차량",
                "온도, 습도, 재배 작물, 재배면적, 생육 상태, 관측 기준 시각, 자료 출처",
                "farm.facility.greenhouse");
            AddStudyIfMissing(entries, TownHousePath,
                "에셋연구:마을:단독주택:01", "마을 단독주택 01",
                "현관, 창문, 지붕과 생활 공간의 덩어리가 한 채로 구성된 저층 주택이다.",
                "한 가구나 이웃이 머물며 식사·휴식·보관과 일상 소비를 이어가는 생활 거점이다.",
                "마을 주거 장소 후보. 거주 인원, 소유 관계나 실제 생활 상태를 모델만으로 확정하지 않는다.",
                "정원, 우편함, 쓰레기통, 승용차, 주민, 보행로, 동네 상점, 배달 차량",
                "가구 수, 인구, 생활권, 건축물 용도, 대중교통 접근성, 관측 기준 시각, 자료 출처",
                "town.place.detached-house");
            AddStudyIfMissing(entries, CityHallPath,
                "에셋연구:도시:시청:01", "도시 시청 01",
                "넓은 정면과 중앙 출입구를 가진 독립형 공공 건물로 주변에서 중심 시설처럼 읽힌다.",
                "주민과 도시 조직이 행정 안내, 공공 업무와 지역 의사결정을 접하는 도시 기능 거점이다.",
                "도시 행정 장소 후보. 실제 기관, 관할, 민원 처리나 행정 권한을 모델이 대신하지 않는다.",
                "광장, 안내 표지, 버스 정류장, 공무원, 주민, 도로, 주차 공간, 공공 게시판",
                "행정구역, 공공시설 위치, 개방 시간, 교통 접근성, 기준 시각, 자료 출처",
                "city.place.civic-hall");
            AddStudyIfMissing(entries, PotatoBoxPath,
                "에셋연구:농장:감자수확상자:01", "감자 수확 상자 01",
                "나무 상자 안에 감자 표본이 담긴 수확물 소품이다. 상자 자체에는 중량·품질·소유자 정보가 없다.",
                "수확한 감자를 선별·보관·상차·판매 같은 다음 작업으로 인계하는 물리적 용기다.",
                "수확물 인계 표식 후보. 보이는 감자 수나 상자 수를 실제 HarvestLot 수량으로 사용하지 않는다.",
                "감자밭, 농장 작업자, 저울, 팔레트, 트럭, 물류센터, 시장 판매대, 저장고",
                "KAMIS 감자 가격 관측, 조사일, 유통단계, 품종·등급·단위, 원천 revision",
                "farm.harvest.potato-box");
            AddStudyIfMissing(entries, DirtRowsPath,
                "에셋연구:농장:밭고랑토양:01", "밭고랑 토양 표본",
                "여러 줄의 이랑과 고랑이 반복되는 경작지 바닥 에셋이다. 흙의 성분이나 실제 필지 경계는 표현하지 않는다.",
                "작물을 심고 물과 양분을 관리하는 토양 기반 생산 공간이다.",
                "필지 토양검정 자료를 읽기 시작하는 장소 표본. 에셋의 색으로 토양 상태를 판정하지 않는다.",
                "감자 재배체, 농장 작업자, 관수 설비, 수확 상자, 농로, 토양 시료 표지",
                "필지 식별자, 시료 채취일, 산도, 유기물, 유효인산, 전기전도도, 출처와 이용조건",
                "farm.soil.ridge-row");
            AddStudyIfMissing(entries, PotatoPlantPath,
                "에셋연구:농장:감자재배체:01", "감자 재배체 표본",
                "감자 이름으로 제공되는 잎과 줄기 중심의 재배체 에셋이다. 품종·생육 단계·수확량은 외형만으로 알 수 없다.",
                "토양과 기상 조건 속에서 관리되는 감자 생산 과정의 생물 표본이다.",
                "감자 재배 적합도와 생육 자료를 질문하는 표본. 실제 재배 사실이나 권장 처방을 확정하지 않는다.",
                "밭고랑, 관수 설비, 작업자, 수확 상자, 저장고, 운송 차량",
                "작물 코드, 토양 적합도 등급, 적합 면적과 비율, 지역, 기준일, 출처",
                "farm.crop.potato-plant");
            AddStudyIfMissing(entries, SprinklerPath,
                "에셋연구:농장:관수스프링클러:01", "밭 관수 스프링클러",
                "지면 위에서 물을 분사하는 관수 소품이다. 현재 작동 여부나 급수량은 모델에 포함되지 않는다.",
                "강수와 토양 수분만으로 부족할 때 재배지에 물을 공급하는 농업 설비다.",
                "기상 관측과 물 관리 질문을 잇는 설비 표본. 관수 필요량이나 실행 명령은 만들지 않는다.",
                "밭고랑, 물탱크, 펌프, 배관, 감자 재배체, 작업자",
                "강수량, 기온, 습도, 관측 지점과 시각, 토양 수분, 급수량, 출처",
                "farm.facility.field-sprinkler");
            foreach (var crop in LoadDirectKamisCrops())
            {
                var prefabPath = AssetDatabase.GetAssetPath(crop.Prefab);
                AddStudyIfMissing(entries, prefabPath,
                    $"에셋연구:농장:KAMIS작물:{crop.CategoryCode}:{crop.ItemCode}",
                    crop.DisplayName + " 대표 작물",
                    $"POLYGON Farm이 {crop.DisplayName} 이름으로 제공하는 대표 재배·수확물 에셋이다. 외형만으로 품종·등급·중량·생육 상태는 알 수 없다.",
                    $"KAMIS의 {crop.DisplayName} 품목을 현실 관측과 함께 살펴보기 위한 시각 표본이다.",
                    "KAMIS 작물 모판의 연구 표본. 출처와 관측 범위를 구체화하기 전에는 실제 Farm Scene의 생산 상태로 승격하지 않는다.",
                    "재배 구역, 농장 작업자, 수확 상자, 저울, 저장고, 운송 차량, 판매대",
                    $"KAMIS 분류 {crop.CategoryCode}, 품목 {crop.ItemCode}, 지역, 조사일, 유통단계, 품종·등급·단위, 표본 수, 출처 revision",
                    crop.VisualKey);
            }
            catalog.Configure(entries.ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        public static 공공관측SourceCatalog BuildPublicObservationSourceCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공공관측SourceCatalog>(
                PublicObservationSourceCatalogPath);
            if (catalog == null)
            {
                if (File.Exists(PublicObservationSourceCatalogPath))
                    AssetDatabase.DeleteAsset(PublicObservationSourceCatalogPath);
                catalog = ScriptableObject.CreateInstance<공공관측SourceCatalog>();
                AssetDatabase.CreateAsset(catalog, PublicObservationSourceCatalogPath);
            }

            catalog.Configure(new[]
            {
                Source("at-kamis-daily-food-price", "KAMIS",
                    "KAMIS 일별 도·소매 가격정보", "한국농수산식품유통공사(aT)",
                    "https://www.kamis.or.kr", "JSON", "시장·지역", "조사일",
                    "품목, 품종, 등급, 유통단계, 가격, 단위",
                    "공식 서비스 이용조건 확인 필요",
                    "에셋은 특정 KAMIS 관측의 실물이나 특정 농장의 출하물을 증명하지 않는다."),
                Source("mafra-farmmap-soil-analysis", "15058655",
                    "팜맵 기반 토양검정 정보", "농림수산식품교육문화정보원",
                    "https://www.data.go.kr/data/15058655/openapi.do", "JSON·XML",
                    "좌표·PNU·팜맵 필지", "연도·시료 채취일",
                    "산도(pH), 유기물, 유효인산, 전기전도도 등 토양검정 항목",
                    "공공누리 제1유형",
                    "필지·시료 시점이 맞아야 하며 전시장 에셋의 토양 상태를 직접 측정한 값이 아니다.",
                    "/B552895/rest/farmmap/getFarmmapSoilAnalysisService/getCoordinateBasedSoilAnalsInfo",
                    "serviceKey, numOfRows, pageNo, type, positionX, positionY · year는 선택",
                    "미호출 · 사용자 위치와 필지 식별자를 선택하지 않음"),
                Source("rda-soil-crop-fit-v2", "15144182",
                    "작물별 토양적성도 조회 V2", "농촌진흥청 국립농업과학원",
                    "https://www.data.go.kr/data/15144182/openapi.do", "XML",
                    "지역·토양도 단위", "서비스 기준 시점",
                    "작물, 토양 적합도 등급, 적합 면적과 비율",
                    "공공누리 제4유형",
                    "감자 공식 코드는 흙토람 작물 목록의 CR032다. 특정 지역의 재배 권고가 아니라 법정동별 토양적성 면적 통계다.",
                    "/1390802/SoilEnviron/SoilFitStat/V2/getSoilCropFitInfo",
                    "serviceKey, STDG_CD 법정동코드 10자리, soil_Crop_CD=CR032 감자",
                    "2026-08-11 호출 차단 · 기존 공통 인증키는 이 API 활용 미승인(HTTP 403)"),
                Source("mafra-farmmap-ag-weather", "15058627",
                    "팜맵 기반 농업기상 정보", "농림수산식품교육문화정보원",
                    "https://www.data.go.kr/data/15058627/openapi.do", "확인 예정",
                    "좌표·PNU·팜맵 필지", "관측 시각 확인 예정",
                    "기온, 강수, 습도 등 농업기상 후보 항목",
                    "공식 메타데이터에서 확인 예정",
                    "현재는 자료 후보이며 관수 필요량, 급수량 또는 실행 판단으로 사용할 수 없다."),
                Source("rda-soil-chemistry-v2", "15144647",
                    "토양 화학성 조회 V2", "농촌진흥청 국립농업과학원",
                    "https://www.data.go.kr/data/15144647/openapi.do", "XML",
                    "지역·토양도 단위", "서비스 기준 시점",
                    "토양 화학성 항목",
                    "공식 이용조건 확인 필요",
                    "필지 토양검정 자료와 범위·시점·항목이 같은지 별도 대조해야 한다."),
                Source("rda-soil-characteristics-v3", "15144225",
                    "토양 특성 조회 V3", "농촌진흥청 국립농업과학원",
                    "https://www.data.go.kr/data/15144225/openapi.do", "XML",
                    "지역·토양도 단위", "서비스 기준 시점",
                    "토양 유형과 물리적 특성",
                    "공식 이용조건 확인 필요",
                    "에셋의 색과 형태만으로 실제 토양 유형을 대응시키지 않는다."),
            });
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static 공공관측SourceEntry Source(string key, string datasetId, string name,
            string provider, string url, string format, string spatial, string temporal,
            string fields, string useCondition, string limitation, string operationPath = "",
            string requiredInputs = "", string sampleCallResult = "")
        {
            var entry = new 공공관측SourceEntry();
            entry.Configure(key, datasetId, name, provider, url, format, spatial, temporal,
                fields, useCondition, limitation, "2026-08-11", operationPath,
                requiredInputs, sampleCallResult);
            return entry;
        }

        public static 에셋공공관측Catalog BuildPublicObservationCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(PublicObservationCatalogPath);
            if (catalog == null)
            {
                if (File.Exists(PublicObservationCatalogPath)) AssetDatabase.DeleteAsset(PublicObservationCatalogPath);
                catalog = ScriptableObject.CreateInstance<에셋공공관측Catalog>();
                AssetDatabase.CreateAsset(catalog, PublicObservationCatalogPath);
            }

            var potatoBoxGuid = AssetDatabase.AssetPathToGUID(PotatoBoxPath);
            if (string.IsNullOrWhiteSpace(potatoBoxGuid))
                throw new InvalidOperationException("감자 수확 상자 원본 Prefab을 찾을 수 없습니다: " + PotatoBoxPath);

            var entries = catalog.Entries.ToList();
            if (entries.All(value => value.관측연결Id값 != "에셋공공관측:농장:감자상자:KAMIS"))
            {
                var observation = new 에셋공공관측Entry();
                observation.Configure(
                    "에셋공공관측:농장:감자상자:KAMIS", potatoBoxGuid,
                    현실관측연결상태Code.실제관측미수집,
                    "at-kamis-daily-food-price",
                    "한국농수산식품유통공사(aT) KAMIS 일별 도·소매 가격정보",
                    "/api/v1/agricultural-fisheries/items/0701/domestic-price",
                    "product:potato ↔ KAMIS 식량작물 100 / 감자 152 · Confirmed",
                    string.Empty, string.Empty, string.Empty,
                    "실제 관측값 없음", string.Empty,
                    "감자 품목의 조사일·유통단계·품종·등급·단위가 보존된 가격 관측을 연결하면 지역과 기간별 가격 범위를 살펴볼 수 있다.",
                    "이 상자의 실제 중량·품질·산지·소유자·판매가격과 특정 농장의 출하 사실은 알 수 없다.",
                    "기존 2,450 KRW/kg 값은 Simulation Fixture이며 실제 KAMIS 관측값이 아니다.");
                entries.Add(observation);
            }

            foreach (var crop in LoadDirectKamisCrops())
            {
                var sourcePath = AssetDatabase.GetAssetPath(crop.Prefab);
                var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                var id = $"에셋공공관측:농장:KAMIS작물:{crop.CategoryCode}:{crop.ItemCode}";
                if (entries.Any(value => value.관측연결Id값 == id)) continue;

                var observation = new 에셋공공관측Entry();
                observation.Configure(
                    id, sourceGuid,
                    현실관측연결상태Code.실제관측미수집,
                    "at-kamis-daily-food-price",
                    "한국농수산식품유통공사(aT) KAMIS 일별 도·소매 가격정보",
                    "/api/v1/agriculture/products/common-identities/" + crop.StableId,
                    $"{crop.StableId} ↔ KAMIS 분류 {crop.CategoryCode} / {crop.DisplayName} {crop.ItemCode} · Confirmed · 시각 대응 Direct",
                    string.Empty, string.Empty, string.Empty,
                    "실제 관측값 없음", string.Empty,
                    $"{crop.DisplayName}의 KAMIS 품목 관계와, 이후 수집할 지역·조사일·유통단계·품종·등급·단위별 관측을 함께 살펴볼 수 있다.",
                    $"이 에셋이 실제 {crop.DisplayName}인지, 어느 농장의 어떤 품종·등급·수량인지, 현재 가격이나 출하 상태가 무엇인지는 알 수 없다.",
                    "현재 카드는 Simulation 수치를 사용하지 않는다. 이후 Simulation 값이 생겨도 실제 KAMIS 관측과 별도 비교 자료로만 표시한다.",
                    에셋전시모음Code.KAMIS작물모판);
                entries.Add(observation);
            }

            AddSoilNurseryObservation(entries, DirtRowsPath,
                "에셋공공관측:농장:밭고랑:토양검정", "mafra-farmmap-soil-analysis",
                "농림수산식품교육문화정보원 팜맵 기반 토양검정 정보",
                "서버 연결 전 · 공공데이터포털 Dataset 15058655",
                "밭고랑은 필지 토양검정 자료를 읽기 시작하는 시각 표본이다. 에셋의 흙 색이나 모양을 측정값으로 해석하지 않는다.",
                "필지 식별자와 시료 시점이 맞으면 산도·유기물·유효인산·전기전도도 같은 토양검정 결과를 살펴볼 수 있다.",
                "이 전시장 밭고랑의 실제 토양 성분, 작물 처방, 비료 투입량과 현재 경작 사실은 알 수 없다.",
                "Simulation 토양 상태가 생겨도 공공 토양검정 원본과 별도 층으로 비교한다.",
                공공관측자료연구단계Code.Metadata확인);
            AddSoilNurseryObservation(entries, PotatoPlantPath,
                "에셋공공관측:농장:감자재배체:토양적성", "rda-soil-crop-fit-v2",
                "농촌진흥청 작물별 토양적성도 조회 V2",
                "서버 연결 전 · 공공데이터포털 Dataset 15144182",
                "product:potato 재배체 표본과 작물별 토양 적합도 자료의 연구 연결이다. 흙토람 공식 작물 목록에서 감자 코드는 CR032로 확인했다.",
                "지역과 작물 코드가 확인되면 감자 재배에 대한 토양 적합도 등급·면적·비율을 살펴볼 수 있다.",
                "이 에셋이 실제로 재배 중인 감자인지, 현재 생육 상태·수확량·권장 처방이 무엇인지는 알 수 없다.",
                "Simulation의 감자 생육 단계와 현실의 토양 적합도는 원인·결과로 자동 결합하지 않는다.",
                공공관측자료연구단계Code.Metadata확인);
            AddSoilNurseryObservation(entries, PotatoBoxPath,
                "에셋공공관측:농장:감자상자:토양모판KAMIS", "at-kamis-daily-food-price",
                "한국농수산식품유통공사(aT) KAMIS 일별 도·소매 가격정보",
                "/api/v1/agricultural-fisheries/items/0701/domestic-price",
                "감자 수확 상자는 토양에서 생산된 작물이 선별·저장·유통 자료로 넘어가는 경계 표본이다.",
                "감자 품목의 조사일·유통단계·품종·등급·단위가 보존된 가격 관측을 살펴볼 수 있다.",
                "이 상자의 실제 중량·품질·산지·소유자·판매가격과 특정 필지의 생산물인지는 알 수 없다.",
                "Simulation 수확 상자와 실제 KAMIS 가격 관측은 같은 사실로 합치지 않는다.",
                공공관측자료연구단계Code.Metadata확인);
            AddSoilNurseryObservation(entries, SprinklerPath,
                "에셋공공관측:농장:스프링클러:농업기상", "mafra-farmmap-ag-weather",
                "농림수산식품교육문화정보원 팜맵 기반 농업기상 정보",
                "서버 연결 전 · 공공데이터포털 Dataset 15058627",
                "스프링클러는 농업기상과 물 관리 질문을 시작하는 설비 표본이다. 관측값만으로 관수 실행을 명령하지 않는다.",
                "표본 응답의 항목·지점·시각을 확인한 뒤 강수·기온·습도와 관수 연구의 관계를 살펴볼 수 있다.",
                "현재 토양 수분, 필요한 급수량, 설비 작동 여부와 수리권은 알 수 없다.",
                "Simulation 관수 판단은 현실 관측 원본과 출처·시각을 분리해 비교해야 한다.",
                공공관측자료연구단계Code.자료후보);

            catalog.Configure(entries.ToArray());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void AddSoilNurseryObservation(List<에셋공공관측Entry> entries,
            string sourcePath, string id, string sourceKey, string sourceName, string serverState,
            string relation, string known, string unknown, string simulation, string researchStage)
        {
            entries.RemoveAll(value => value.관측연결Id값 == id);
            var guid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("감자 토양 모판 원본 Prefab을 찾을 수 없습니다: " + sourcePath);
            var observation = new 에셋공공관측Entry();
            observation.Configure(id, guid, 현실관측연결상태Code.실제관측미수집,
                sourceKey, sourceName, serverState, relation,
                string.Empty, string.Empty, string.Empty, "실제 관측값 없음", string.Empty,
                known, unknown, simulation, 에셋전시모음Code.감자토양모판, researchStage);
            entries.Add(observation);
        }

        private static void AddStudyIfMissing(List<에셋연구Entry> entries, string sourcePath,
            string id, string koreanName, string observedFacts, string realWorldMeaning,
            string worldRoleCandidates, string companionCandidates, string dataCandidates,
            string visualKeyCandidate)
        {
            var guid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("연구할 원본 Prefab을 찾을 수 없습니다: " + sourcePath);
            if (entries.Any(value => value.원본Guid값 == guid)) return;

            var study = new 에셋연구Entry();
            study.Configure(id, guid, koreanName, 에셋연구상태Code.해석됨,
                observedFacts, realWorldMeaning, worldRoleCandidates, companionCandidates,
                dataCandidates, visualKeyCandidate);
            entries.Add(study);
        }

        private static IReadOnlyList<DirectKamisCrop> LoadDirectKamisCrops()
        {
            var visualCatalog = AssetDatabase.LoadAssetAtPath<FarmProductVisualCatalog>(
                FarmProductVisualCatalogPath);
            if (visualCatalog == null)
            {
                FarmProductVisualCatalogBuilder.Build();
                visualCatalog = AssetDatabase.LoadAssetAtPath<FarmProductVisualCatalog>(
                    FarmProductVisualCatalogPath);
            }
            if (visualCatalog == null)
                throw new InvalidOperationException("Farm 상품 Visual Catalog를 찾을 수 없습니다.");

            visualCatalog.Validate();
            var crops = visualCatalog.Entries
                .Where(value => value.MappingStatusCode == FarmProductVisualMappingStatusCodes.Direct)
                .Select(CreateDirectKamisCrop)
                .OrderBy(value => value.CategoryCode, StringComparer.Ordinal)
                .ThenBy(value => value.ItemCode, StringComparer.Ordinal)
                .ToArray();
            if (crops.Length != 18)
                throw new InvalidOperationException("KAMIS 직접 대응 작물은 18종이어야 합니다.");
            return crops;
        }

        private static DirectKamisCrop CreateDirectKamisCrop(FarmProductVisualCatalogEntry entry)
        {
            if (entry.Prefab == null)
                throw new InvalidOperationException("KAMIS 직접 대응 작물 Prefab이 없습니다: "
                                                    + entry.CanonicalProductStableId);
            if (entry.CanonicalProductStableId == "product:potato")
                return new DirectKamisCrop(entry, "100", "152");

            var parts = entry.CanonicalProductStableId.Split(':');
            if (parts.Length != 4 || parts[0] != "product" || parts[1] != "food")
                throw new InvalidOperationException("KAMIS 상품 stable ID 형식이 올바르지 않습니다: "
                                                    + entry.CanonicalProductStableId);
            return new DirectKamisCrop(entry, parts[2], parts[3]);
        }

        public static void ValidateOpenScene()
        {
            var root = GameObject.Find(RootName)
                       ?? throw new InvalidOperationException("에셋 연구소 Root가 없습니다.");
            var presenter = root.GetComponent<에셋연구소Presenter>()
                            ?? throw new InvalidOperationException("에셋 연구소 Presenter가 없습니다.");
            presenter.ValidateWiring();
            if (presenter.현재표본수 != 4
                || presenter.현재전시모음값 != 에셋전시모음Code.감자토양모판)
                throw new InvalidOperationException("첫 전시는 감자 토양 모판 4개 표본이어야 합니다.");
            if (root.GetComponentsInChildren<에셋연구표본View>(true).Any(value =>
                    value.transform.Find("VisualRoot/SyntyPrefabInstance") == null))
                throw new InvalidOperationException("VisualRoot 아래에 있지 않은 원본 표본이 있습니다.");

            ValidatePackStudy(presenter, 에셋연구소ActionCode.마을보기,
                에셋PackCode.마을, "마을 단독주택 01");
            ValidatePackStudy(presenter, 에셋연구소ActionCode.도시보기,
                에셋PackCode.도시, "도시 시청 01");
            ValidatePackStudy(presenter, 에셋연구소ActionCode.농장보기,
                에셋PackCode.농장, "온실 01");
            presenter.ShowScope(에셋PackCode.농장, 에셋분류Code.식물);
            presenter.Execute(에셋연구소ActionCode.KAMIS작물모판보기);
            var observationCatalog = AssetDatabase.LoadAssetAtPath<에셋공공관측Catalog>(PublicObservationCatalogPath);
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<공공관측SourceCatalog>(
                PublicObservationSourceCatalogPath);
            sourceCatalog.Validate();
            if (sourceCatalog.Find("mafra-farmmap-soil-analysis")?.자료식별자값 != "15058655"
                || sourceCatalog.Find("rda-soil-crop-fit-v2")?.자료식별자값 != "15144182")
                throw new InvalidOperationException("감자 토양 모판의 공식 자료 식별자가 올바르지 않습니다.");
            var nursery = observationCatalog.FindByCollection(에셋전시모음Code.KAMIS작물모판);
            if (nursery.Count != 18
                || nursery.Any(value => value.연결상태값 != 현실관측연결상태Code.실제관측미수집
                                        || value.관측값표시값 != "실제 관측값 없음"))
                throw new InvalidOperationException("KAMIS 작물 모판의 현실 관측 경계가 올바르지 않습니다.");
            presenter.Execute(에셋연구소ActionCode.감자토양모판보기);
            var soilNursery = observationCatalog.FindByCollection(에셋전시모음Code.감자토양모판);
            if (soilNursery.Count != 4
                || soilNursery.Select(value => value.원본Guid값).Distinct(StringComparer.Ordinal).Count() != 4
                || soilNursery.Any(value => value.연결상태값 != 현실관측연결상태Code.실제관측미수집))
                throw new InvalidOperationException("감자 토양 모판의 4개 연구 표본 경계가 올바르지 않습니다.");
        }

        private static void ValidatePackStudy(에셋연구소Presenter presenter, string actionCode,
            string expectedPack, string expectedKoreanName)
        {
            presenter.Execute(actionCode);
            presenter.ShowScope(expectedPack, 에셋분류Code.건물);
            if (presenter.현재Pack값 != expectedPack || presenter.현재표본수 != 에셋연구소Presenter.쪽당표본수)
                throw new InvalidOperationException(expectedPack + " 묶음의 첫 전시가 올바르지 않습니다.");

            var catalog = AssetDatabase.LoadAssetAtPath<에셋연구Catalog>(StudyCatalogPath);
            var selected = catalog.FindBySourceGuid(presenter.선택된원본Guid);
            if (selected == null || selected.한국어이름값 != expectedKoreanName)
                throw new InvalidOperationException(expectedPack + " 대표 연구 카드가 올바르지 않습니다.");
        }

        private static void AddPack(List<에셋원본IndexEntry> target, string root, string pack)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var entry = new 에셋원본IndexEntry();
                entry.Configure(guid, Path.GetFileNameWithoutExtension(path), path, pack,
                    CategoryFromPath(path), prefab);
                target.Add(entry);
            }
        }

        private static string CategoryFromPath(string path)
        {
            if (path.Contains("/Buildings/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.건물;
            if (path.Contains("/Props/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.소품;
            if (path.Contains("/Environments/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.환경;
            if (path.Contains("/Plants/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.식물;
            if (path.Contains("/Vehicles/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.차량;
            if (path.Contains("/Characters/", StringComparison.OrdinalIgnoreCase)) return 에셋분류Code.인물;
            return 에셋분류Code.기타;
        }

        private static void BuildStage(Transform parent)
        {
            var stage = new GameObject("연구전시장").transform;
            stage.SetParent(parent, false);
            Primitive(stage, "바닥", PrimitiveType.Cube, new Vector3(0f, -.35f, 0f),
                new Vector3(42f, .6f, 30f), new Color(.12f, .16f, .15f));
            Primitive(stage, "뒤배경", PrimitiveType.Cube, new Vector3(0f, 6f, 13.8f),
                new Vector3(42f, 12f, .5f), new Color(.16f, .23f, .21f));
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 3; column++)
            {
                var position = 에셋연구소Layout.전시칸위치(row * 3 + column)
                               + new Vector3(0f, .03f, 0f);
                Primitive(stage, $"전시구역_{row + 1}_{column + 1}", PrimitiveType.Cube,
                    position, new Vector3(6.8f, .08f, 5.2f),
                    (row + column) % 2 == 0
                        ? new Color(.19f, .25f, .22f)
                        : new Color(.17f, .22f, .2f));
            }

            var light = new GameObject("연구소주조명").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Directional;
            light.color = new Color(1f, .93f, .8f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .75f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .66f, .7f);
            RenderSettings.ambientEquatorColor = new Color(.38f, .44f, .4f);
            RenderSettings.ambientGroundColor = new Color(.1f, .13f, .12f);
        }

        private static DioramaTopDownCameraRig BuildCamera(Transform parent)
        {
            var cameraObject = new GameObject("에셋연구Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .12f, .12f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 250f;

            var bindings = new List<DioramaCameraFocusBinding>();
            var overview = new GameObject("전체Focus").transform;
            overview.SetParent(parent, false);
            overview.localPosition = Vector3.zero;
            bindings.Add(Binding("에셋연구소:전체", DioramaCameraFocusLevelCodes.World, overview));
            for (var slot = 0; slot < 에셋연구소Presenter.쪽당표본수; slot++)
            {
                var anchor = new GameObject($"전시칸Focus_{slot + 1:00}").transform;
                anchor.SetParent(parent, false);
                anchor.localPosition = 에셋연구소Layout.전시칸위치(slot)
                                       + new Vector3(0f, 1.8f, 0f);
                bindings.Add(Binding("에셋연구소:전시칸:" + slot,
                    DioramaCameraFocusLevelCodes.Object, anchor));
            }
            var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            rig.Configure(camera, bindings.ToArray(), "에셋연구소:전체");
            rig.ConfigureComposition(50f, 49f, 24f, 12f, 34f, 30f, 28f, 70f);
            rig.Initialize();
            return rig;
        }

        private static UiReferences BuildUi(Transform parent, 에셋연구소Presenter presenter,
            Camera worldCamera)
        {
            var canvasObject = new GameObject("에셋연구소Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = .5f;
            canvas.sortingOrder = 60;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var header = Panel(canvasObject.transform, "윗정보띠",
                new Vector2(.025f, .89f), new Vector2(.64f, .975f), new Color(.025f, .055f, .05f, .96f));
            Text(header, "연구소제목", "신티 에셋 연구소 · 현실 관측 전시장",
                new Vector2(.025f, .5f), new Vector2(.57f, .94f), 25,
                new Color(.96f, .76f, .31f), FontStyle.Bold);
            var scope = Text(header, "현재범위", "농장 · 건물",
                new Vector2(.59f, .12f), new Vector2(.98f, .92f), 16,
                new Color(.85f, .94f, .87f), FontStyle.Bold);

            var controls = Panel(canvasObject.transform, "연구도구",
                new Vector2(.025f, .025f), new Vector2(.64f, .145f), new Color(.025f, .055f, .05f, .96f));
            Button(controls, "농장보기Button", "농장", 에셋연구소ActionCode.농장보기,
                presenter, new Vector2(.01f, .56f), new Vector2(.12f, .94f));
            Button(controls, "마을보기Button", "마을", 에셋연구소ActionCode.마을보기,
                presenter, new Vector2(.125f, .56f), new Vector2(.235f, .94f));
            Button(controls, "도시보기Button", "도시", 에셋연구소ActionCode.도시보기,
                presenter, new Vector2(.24f, .56f), new Vector2(.35f, .94f));
            Button(controls, "분류바꾸기Button", "분류", 에셋연구소ActionCode.분류바꾸기,
                presenter, new Vector2(.355f, .56f), new Vector2(.465f, .94f));
            Button(controls, "이전쪽Button", "이전 쪽", 에셋연구소ActionCode.이전쪽,
                presenter, new Vector2(.47f, .56f), new Vector2(.58f, .94f));
            Button(controls, "다음쪽Button", "다음 쪽", 에셋연구소ActionCode.다음쪽,
                presenter, new Vector2(.585f, .56f), new Vector2(.695f, .94f));
            Button(controls, "전체보기Button", "전체 보기", 에셋연구소ActionCode.전체보기,
                presenter, new Vector2(.7f, .56f), new Vector2(.81f, .94f));
            Button(controls, "에셋연구보기Button", "에셋 연구", 에셋연구소ActionCode.에셋연구보기,
                presenter, new Vector2(.815f, .56f), new Vector2(.925f, .94f));
            Button(controls, "현실관측보기Button", "현실 관측", 에셋연구소ActionCode.현실관측보기,
                presenter, new Vector2(.93f, .56f), new Vector2(.995f, .94f));
            Button(controls, "KAMIS작물모판Button", "KAMIS 작물 모판", 에셋연구소ActionCode.KAMIS작물모판보기,
                presenter, new Vector2(.01f, .08f), new Vector2(.245f, .46f));
            Button(controls, "감자토양모판Button", "감자 토양 모판", 에셋연구소ActionCode.감자토양모판보기,
                presenter, new Vector2(.25f, .08f), new Vector2(.49f, .46f));

            var detail = Panel(canvasObject.transform, "연구카드",
                new Vector2(.675f, .025f), new Vector2(.975f, .975f), new Color(.025f, .048f, .045f, .97f));
            Panel(detail, "강조선", new Vector2(0f, .99f), Vector2.one, new Color(.96f, .64f, .18f));
            Text(detail, "카드눈썹", "에셋 하나를 세계의 의미 단위로 읽기",
                new Vector2(.055f, .91f), new Vector2(.945f, .975f), 15,
                new Color(.55f, .83f, .62f), FontStyle.Bold);
            var title = Text(detail, "상세제목", "선택된 에셋",
                new Vector2(.055f, .81f), new Vector2(.945f, .915f), 25,
                Color.white, FontStyle.Bold);
            var body = Text(detail, "상세내용", "에셋을 선택해 주세요.",
                new Vector2(.055f, .055f), new Vector2(.945f, .8f), 16,
                new Color(.86f, .91f, .87f), FontStyle.Normal);
            body.alignment = TextAnchor.UpperLeft;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            정보Panel상호작용Builder.Attach(canvasObject.transform, detail, "에셋 연구 카드");
            return new UiReferences(scope, title, body);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min,
            Vector2 max, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text Text(Transform parent, string name, string value, Vector2 min,
            Vector2 max, int size, Color color, FontStyle style)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = target.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Button(Transform parent, string name, string label, string actionCode,
            에셋연구소Presenter presenter, Vector2 min, Vector2 max)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(에셋연구소ActionButton));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            target.GetComponent<Image>().color = new Color(.15f, .29f, .23f);
            target.GetComponent<에셋연구소ActionButton>().Configure(presenter, actionCode);
            var text = Text(target.transform, "이름", label, new Vector2(.04f, .04f),
                new Vector2(.96f, .96f), 14, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static DioramaCameraFocusBinding Binding(string id, string level, Transform anchor)
            => new() { AnchorId = id, LevelCode = level, Anchor = anchor };

        private static void Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private readonly struct UiReferences
        {
            public UiReferences(Text scope, Text detailTitle, Text detailBody)
            {
                Scope = scope;
                DetailTitle = detailTitle;
                DetailBody = detailBody;
            }
            public Text Scope { get; }
            public Text DetailTitle { get; }
            public Text DetailBody { get; }
        }

        private sealed class DirectKamisCrop
        {
            public DirectKamisCrop(FarmProductVisualCatalogEntry entry,
                string categoryCode, string itemCode)
            {
                StableId = entry.CanonicalProductStableId;
                DisplayName = entry.DisplayName;
                VisualKey = entry.VisualKey;
                Prefab = entry.Prefab;
                CategoryCode = categoryCode;
                ItemCode = itemCode;
            }

            public string StableId { get; }
            public string DisplayName { get; }
            public string VisualKey { get; }
            public GameObject Prefab { get; }
            public string CategoryCode { get; }
            public string ItemCode { get; }
        }
    }
}
