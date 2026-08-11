using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class 에셋PackCode
    {
        public const string 농장 = "농장";
        public const string 마을 = "마을";
        public const string 도시 = "도시";

        public static readonly string[] 전체 = { 농장, 마을, 도시 };
        public static bool IsKnown(string value) => 전체.Contains(value, StringComparer.Ordinal);
    }

    public static class 에셋분류Code
    {
        public const string 건물 = "건물";
        public const string 소품 = "소품";
        public const string 환경 = "환경";
        public const string 식물 = "식물";
        public const string 차량 = "차량";
        public const string 인물 = "인물";
        public const string 기타 = "기타";

        public static readonly string[] 전체 = { 건물, 소품, 환경, 식물, 차량, 인물, 기타 };
        public static bool IsKnown(string value) => 전체.Contains(value, StringComparer.Ordinal);
    }

    public static class 에셋연구상태Code
    {
        public const string 미검토 = "미검토";
        public const string 관찰됨 = "관찰됨";
        public const string 해석됨 = "해석됨";
        public const string 장소검증됨 = "장소 검증됨";
        public const string 체계검증됨 = "체계 검증됨";
        public const string 월드목록승격 = "월드 목록 승격";

        public static readonly string[] 전체 =
        {
            미검토, 관찰됨, 해석됨, 장소검증됨, 체계검증됨, 월드목록승격,
        };

        public static bool IsKnown(string value) => 전체.Contains(value, StringComparer.Ordinal);
    }

    public static class 현실관측연결상태Code
    {
        public const string 실제관측미수집 = "실제 관측 미수집";
        public const string 실제관측연결됨 = "실제 관측 연결됨";

        public static readonly string[] 전체 = { 실제관측미수집, 실제관측연결됨 };
        public static bool IsKnown(string value) => 전체.Contains(value, StringComparer.Ordinal);
    }

    public static class 공공관측자료연구단계Code
    {
        public const string 자료후보 = "자료 후보";
        public const string Metadata확인 = "메타데이터 확인";
        public const string 표본응답확인 = "표본 응답 확인";
        public const string 실제관측연결 = "실제 관측 연결";

        public static readonly string[] 전체 = { 자료후보, Metadata확인, 표본응답확인, 실제관측연결 };
        public static bool IsKnown(string value) => 전체.Contains(value, StringComparer.Ordinal);
    }

    public static class 에셋전시모음Code
    {
        public const string KAMIS작물모판 = "KAMIS 작물 모판";
        public const string 감자토양모판 = "감자 토양 모판";
    }

    [Serializable]
    public sealed class 공공관측SourceEntry
    {
        [SerializeField] private string 출처Key = string.Empty;
        [SerializeField] private string 자료식별자 = string.Empty;
        [SerializeField] private string 자료이름 = string.Empty;
        [SerializeField] private string 제공기관 = string.Empty;
        [SerializeField] private string 공식주소 = string.Empty;
        [SerializeField] private string 응답형식 = string.Empty;
        [SerializeField] private string 공간기준 = string.Empty;
        [SerializeField] private string 시간기준 = string.Empty;
        [SerializeField, TextArea] private string 관측항목 = string.Empty;
        [SerializeField] private string 이용조건 = string.Empty;
        [SerializeField, TextArea] private string 한계 = string.Empty;
        [SerializeField] private string 확인기준일 = string.Empty;
        [SerializeField] private string 요청경로 = string.Empty;
        [SerializeField, TextArea] private string 필수입력 = string.Empty;
        [SerializeField, TextArea] private string 표본호출결과 = string.Empty;

        public string 출처Key값 => 출처Key;
        public string 자료식별자값 => 자료식별자;
        public string 자료이름값 => 자료이름;
        public string 제공기관값 => 제공기관;
        public string 공식주소값 => 공식주소;
        public string 응답형식값 => 응답형식;
        public string 공간기준값 => 공간기준;
        public string 시간기준값 => 시간기준;
        public string 관측항목값 => 관측항목;
        public string 이용조건값 => 이용조건;
        public string 한계값 => 한계;
        public string 확인기준일값 => 확인기준일;
        public string 요청경로값 => 요청경로;
        public string 필수입력값 => 필수입력;
        public string 표본호출결과값 => 표본호출결과;

        public void Configure(string sourceKey, string datasetId, string sourceName,
            string provider, string officialUrl, string responseFormat, string spatialUnit,
            string temporalUnit, string observedFields, string useCondition,
            string limitation, string checkedDate, string operationPath = "",
            string requiredInputs = "", string sampleCallResult = "")
        {
            출처Key = sourceKey;
            자료식별자 = datasetId;
            자료이름 = sourceName;
            제공기관 = provider;
            공식주소 = officialUrl;
            응답형식 = responseFormat;
            공간기준 = spatialUnit;
            시간기준 = temporalUnit;
            관측항목 = observedFields;
            이용조건 = useCondition;
            한계 = limitation;
            확인기준일 = checkedDate;
            요청경로 = operationPath;
            필수입력 = requiredInputs;
            표본호출결과 = sampleCallResult;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(출처Key)
               && !string.IsNullOrWhiteSpace(자료식별자)
               && !string.IsNullOrWhiteSpace(자료이름)
               && !string.IsNullOrWhiteSpace(제공기관)
               && !string.IsNullOrWhiteSpace(공식주소)
               && !string.IsNullOrWhiteSpace(한계)
               && !string.IsNullOrWhiteSpace(확인기준일);
    }

    public static class 에셋연구소Layout
    {
        public static Vector3 전시칸위치(int slotNumber)
        {
            var column = slotNumber % 3;
            var row = slotNumber / 3;
            return new Vector3(-12f + column * 8f, 0f, 9f - row * 6.3f);
        }
    }

    [Serializable]
    public sealed class 에셋원본IndexEntry
    {
        [SerializeField] private string 원본Guid = string.Empty;
        [SerializeField] private string 원본이름 = string.Empty;
        [SerializeField] private string 원본경로 = string.Empty;
        [SerializeField] private string pack = string.Empty;
        [SerializeField] private string 분류 = string.Empty;
        [SerializeField] private GameObject prefab = null!;

        public string 원본Guid값 => 원본Guid;
        public string 원본이름값 => 원본이름;
        public string 원본경로값 => 원본경로;
        public string Pack => pack;
        public string 분류값 => 분류;
        public GameObject Prefab => prefab;

        public void Configure(string guid, string name, string path, string packCode,
            string categoryCode, GameObject sourcePrefab)
        {
            원본Guid = guid;
            원본이름 = name;
            원본경로 = path;
            pack = packCode;
            분류 = categoryCode;
            prefab = sourcePrefab;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(원본Guid)
               && !string.IsNullOrWhiteSpace(원본이름)
               && !string.IsNullOrWhiteSpace(원본경로)
               && 에셋PackCode.IsKnown(pack)
               && 에셋분류Code.IsKnown(분류)
               && prefab != null;
    }

    [Serializable]
    public sealed class 에셋연구Entry
    {
        [SerializeField] private string 연구Id = string.Empty;
        [SerializeField] private string 원본Guid = string.Empty;
        [SerializeField] private string 한국어이름 = string.Empty;
        [SerializeField] private string 연구상태 = 에셋연구상태Code.미검토;
        [SerializeField, TextArea] private string 관찰된사실 = string.Empty;
        [SerializeField, TextArea] private string 현실의미 = string.Empty;
        [SerializeField, TextArea] private string 월드역할후보 = string.Empty;
        [SerializeField, TextArea] private string 함께둘에셋후보 = string.Empty;
        [SerializeField, TextArea] private string 연결할Data후보 = string.Empty;
        [SerializeField] private string 승격후보VisualKey = string.Empty;

        public string 연구Id값 => 연구Id;
        public string 원본Guid값 => 원본Guid;
        public string 한국어이름값 => 한국어이름;
        public string 연구상태값 => 연구상태;
        public string 관찰된사실값 => 관찰된사실;
        public string 현실의미값 => 현실의미;
        public string 월드역할후보값 => 월드역할후보;
        public string 함께둘에셋후보값 => 함께둘에셋후보;
        public string 연결할Data후보값 => 연결할Data후보;
        public string 승격후보VisualKey값 => 승격후보VisualKey;

        public void Configure(string id, string sourceGuid, string koreanName, string status,
            string observedFacts, string realWorldMeaning, string worldRoleCandidates,
            string companionCandidates, string dataCandidates, string visualKeyCandidate)
        {
            연구Id = id;
            원본Guid = sourceGuid;
            한국어이름 = koreanName;
            연구상태 = status;
            관찰된사실 = observedFacts;
            현실의미 = realWorldMeaning;
            월드역할후보 = worldRoleCandidates;
            함께둘에셋후보 = companionCandidates;
            연결할Data후보 = dataCandidates;
            승격후보VisualKey = visualKeyCandidate;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(연구Id)
               && !string.IsNullOrWhiteSpace(원본Guid)
               && !string.IsNullOrWhiteSpace(한국어이름)
               && 에셋연구상태Code.IsKnown(연구상태);
    }

    [Serializable]
    public sealed class 에셋공공관측Entry
    {
        [SerializeField] private string 관측연결Id = string.Empty;
        [SerializeField] private string 원본Guid = string.Empty;
        [SerializeField] private string 연결상태 = 현실관측연결상태Code.실제관측미수집;
        [SerializeField] private string 자료연구단계 = 공공관측자료연구단계Code.자료후보;
        [SerializeField] private string 출처Key = string.Empty;
        [SerializeField] private string 출처이름 = string.Empty;
        [SerializeField] private string 서버조회경로 = string.Empty;
        [SerializeField] private string 상품관계근거 = string.Empty;
        [SerializeField] private string 지역 = string.Empty;
        [SerializeField] private string 기준기간 = string.Empty;
        [SerializeField] private string 유통단계 = string.Empty;
        [SerializeField] private string 관측값표시 = string.Empty;
        [SerializeField] private string 단위 = string.Empty;
        [SerializeField] private string 전시모음 = string.Empty;
        [SerializeField, TextArea] private string 알수있는것 = string.Empty;
        [SerializeField, TextArea] private string 알수없는것 = string.Empty;
        [SerializeField, TextArea] private string Simulation비교 = string.Empty;

        public string 관측연결Id값 => 관측연결Id;
        public string 원본Guid값 => 원본Guid;
        public string 연결상태값 => 연결상태;
        public string 자료연구단계값 => 자료연구단계;
        public string 출처Key값 => 출처Key;
        public string 출처이름값 => 출처이름;
        public string 서버조회경로값 => 서버조회경로;
        public string 상품관계근거값 => 상품관계근거;
        public string 지역값 => 지역;
        public string 기준기간값 => 기준기간;
        public string 유통단계값 => 유통단계;
        public string 관측값표시값 => 관측값표시;
        public string 단위값 => 단위;
        public string 전시모음값 => 전시모음;
        public string 알수있는것값 => 알수있는것;
        public string 알수없는것값 => 알수없는것;
        public string Simulation비교값 => Simulation비교;

        public void Configure(string id, string sourceGuid, string status, string sourceKey,
            string sourceName, string serverPath, string productRelationEvidence, string region,
            string period, string marketStage, string observedValue, string unit,
            string whatCanBeKnown, string whatCannotBeKnown, string simulationComparison,
            string exhibitionCollection = "",
            string researchStage = 공공관측자료연구단계Code.자료후보)
        {
            관측연결Id = id;
            원본Guid = sourceGuid;
            연결상태 = status;
            자료연구단계 = researchStage;
            출처Key = sourceKey;
            출처이름 = sourceName;
            서버조회경로 = serverPath;
            상품관계근거 = productRelationEvidence;
            지역 = region;
            기준기간 = period;
            유통단계 = marketStage;
            관측값표시 = observedValue;
            단위 = unit;
            전시모음 = exhibitionCollection;
            알수있는것 = whatCanBeKnown;
            알수없는것 = whatCannotBeKnown;
            Simulation비교 = simulationComparison;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(관측연결Id)
               && !string.IsNullOrWhiteSpace(원본Guid)
               && 현실관측연결상태Code.IsKnown(연결상태)
               && 공공관측자료연구단계Code.IsKnown(자료연구단계)
               && !string.IsNullOrWhiteSpace(출처Key)
               && !string.IsNullOrWhiteSpace(출처이름)
               && !string.IsNullOrWhiteSpace(서버조회경로)
               && !string.IsNullOrWhiteSpace(상품관계근거)
               && !string.IsNullOrWhiteSpace(관측값표시)
               && !string.IsNullOrWhiteSpace(알수없는것)
               && !string.IsNullOrWhiteSpace(Simulation비교);
    }

}
