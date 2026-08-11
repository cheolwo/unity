using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class 에셋연구소ActionCode
    {
        public const string 이전쪽 = "이전쪽";
        public const string 다음쪽 = "다음쪽";
        public const string 묶음바꾸기 = "묶음바꾸기";
        public const string 농장보기 = "농장보기";
        public const string 마을보기 = "마을보기";
        public const string 도시보기 = "도시보기";
        public const string 분류바꾸기 = "분류바꾸기";
        public const string 전체보기 = "전체보기";
        public const string 에셋연구보기 = "에셋연구보기";
        public const string 현실관측보기 = "현실관측보기";
        public const string KAMIS작물모판보기 = "KAMIS작물모판보기";
        public const string 감자토양모판보기 = "감자토양모판보기";
    }

    public static class 에셋연구소보기Mode
    {
        public const string 에셋연구 = "에셋 연구";
        public const string 현실관측 = "현실 관측";
    }

    [DisallowMultipleComponent]
    public sealed class 에셋연구소Presenter : MonoBehaviour
    {
        public const int 쪽당표본수 = 12;

        [SerializeField] private 에셋원본Index 원본Index = null!;
        [SerializeField] private 에셋연구Catalog 연구Catalog = null!;
        [SerializeField] private 에셋공공관측Catalog 공공관측Catalog = null!;
        [SerializeField] private 공공관측SourceCatalog 공공관측출처Catalog = null!;
        [SerializeField] private Transform 전시장Root = null!;
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;
        [SerializeField] private Text 현재범위Text = null!;
        [SerializeField] private Text 상세제목Text = null!;
        [SerializeField] private Text 상세내용Text = null!;
        [SerializeField] private string 현재Pack = 에셋PackCode.농장;
        [SerializeField] private string 현재분류 = 에셋분류Code.건물;
        [SerializeField] private int 현재쪽;
        [SerializeField] private string 현재보기Mode = 에셋연구소보기Mode.에셋연구;
        [SerializeField] private string 현재전시모음 = string.Empty;

        private readonly List<에셋연구표본View> 표본Views = new();
        private bool initialized;

        public string 현재Pack값 => 현재Pack;
        public string 현재분류값 => 현재분류;
        public int 현재쪽값 => 현재쪽;
        public string 현재보기Mode값 => 현재보기Mode;
        public string 현재전시모음값 => 현재전시모음;
        public int 현재표본수 => 표본Views.Count > 0
            ? 표본Views.Count
            : 전시장Root == null ? 0 : 전시장Root.GetComponentsInChildren<에셋연구표본View>(true).Length;
        public string 선택된원본Guid { get; private set; } = string.Empty;

        private void Awake() => Initialize();

        public void Configure(에셋원본Index sourceIndex, 에셋연구Catalog studyCatalog,
            에셋공공관측Catalog observationCatalog,
            공공관측SourceCatalog observationSourceCatalog,
            Transform showroomRoot, DioramaTopDownCameraRig rig, Text scopeText,
            Text detailTitle, Text detailBody)
        {
            원본Index = sourceIndex;
            연구Catalog = studyCatalog;
            공공관측Catalog = observationCatalog;
            공공관측출처Catalog = observationSourceCatalog;
            전시장Root = showroomRoot;
            cameraRig = rig;
            현재범위Text = scopeText;
            상세제목Text = detailTitle;
            상세내용Text = detailBody;
            initialized = false;
        }

        public void Initialize()
        {
            if (initialized) return;
            ValidateWiring();
            ApplyKoreanFont();
            initialized = true;
            ShowCurrentPage();
        }

        public void RefreshForEditor()
        {
            ValidateWiring();
            initialized = true;
            ShowCurrentPage();
        }

        public void Execute(string actionCode)
        {
            Initialize();
            switch (actionCode)
            {
                case 에셋연구소ActionCode.이전쪽:
                    현재쪽 = Math.Max(0, 현재쪽 - 1);
                    break;
                case 에셋연구소ActionCode.다음쪽:
                    현재쪽 = Math.Min(PageCount() - 1, 현재쪽 + 1);
                    break;
                case 에셋연구소ActionCode.묶음바꾸기:
                    현재전시모음 = string.Empty;
                    현재Pack = Next(에셋PackCode.전체, 현재Pack);
                    현재쪽 = 0;
                    break;
                case 에셋연구소ActionCode.농장보기:
                    ChangePack(에셋PackCode.농장);
                    break;
                case 에셋연구소ActionCode.마을보기:
                    ChangePack(에셋PackCode.마을);
                    break;
                case 에셋연구소ActionCode.도시보기:
                    ChangePack(에셋PackCode.도시);
                    break;
                case 에셋연구소ActionCode.분류바꾸기:
                    현재전시모음 = string.Empty;
                    현재분류 = Next(에셋분류Code.전체, 현재분류);
                    현재쪽 = 0;
                    break;
                case 에셋연구소ActionCode.전체보기:
                    cameraRig.Focus("에셋연구소:전체");
                    return;
                case 에셋연구소ActionCode.에셋연구보기:
                    현재보기Mode = 에셋연구소보기Mode.에셋연구;
                    RefreshSelectedDetail();
                    return;
                case 에셋연구소ActionCode.현실관측보기:
                    현재보기Mode = 에셋연구소보기Mode.현실관측;
                    RefreshSelectedDetail();
                    return;
                case 에셋연구소ActionCode.KAMIS작물모판보기:
                    현재Pack = 에셋PackCode.농장;
                    현재분류 = 에셋분류Code.식물;
                    현재전시모음 = 에셋전시모음Code.KAMIS작물모판;
                    현재보기Mode = 에셋연구소보기Mode.현실관측;
                    현재쪽 = 0;
                    ShowCurrentPage();
                    return;
                case 에셋연구소ActionCode.감자토양모판보기:
                    현재Pack = 에셋PackCode.농장;
                    현재분류 = 에셋분류Code.식물;
                    현재전시모음 = 에셋전시모음Code.감자토양모판;
                    현재보기Mode = 에셋연구소보기Mode.현실관측;
                    현재쪽 = 0;
                    ShowCurrentPage();
                    return;
                default:
                    throw new InvalidOperationException("알 수 없는 에셋 연구소 동작입니다: " + actionCode);
            }
            ShowCurrentPage();
        }

        public void ShowScope(string pack, string category)
        {
            Initialize();
            if (!에셋PackCode.IsKnown(pack) || !에셋분류Code.IsKnown(category))
                throw new InvalidOperationException("알 수 없는 에셋 연구 범위입니다.");
            현재Pack = pack;
            현재분류 = category;
            현재전시모음 = string.Empty;
            현재쪽 = 0;
            ShowCurrentPage();
        }

        private void ChangePack(string pack)
        {
            if (!에셋PackCode.IsKnown(pack))
                throw new InvalidOperationException("알 수 없는 에셋 묶음입니다: " + pack);
            현재Pack = pack;
            현재분류 = 에셋분류Code.건물;
            현재전시모음 = string.Empty;
            현재쪽 = 0;
        }

        public void Select(string sourceGuid, int slotNumber)
            => SelectInternal(sourceGuid, slotNumber, true);

        private void SelectInternal(string sourceGuid, int slotNumber, bool focusCamera)
        {
            Initialize();
            var source = 원본Index.Entries.SingleOrDefault(value => value.원본Guid값 == sourceGuid)
                         ?? throw new InvalidOperationException("선택한 에셋을 원본 Index에서 찾을 수 없습니다.");
            선택된원본Guid = sourceGuid;
            foreach (var view in 표본Views)
                view.ApplySelection(view.원본Guid값 == sourceGuid);

            var study = 연구Catalog.FindBySourceGuid(sourceGuid);
            상세제목Text.text = study == null
                ? source.원본이름값
                : study.한국어이름값 + "\n" + source.원본이름값;
            상세내용Text.text = 현재보기Mode == 에셋연구소보기Mode.현실관측
                ? BuildObservationDetail(source,
                    공공관측Catalog.FindPrimaryBySourceGuid(sourceGuid, 현재전시모음))
                : BuildDetail(source, study);
            if (focusCamera) cameraRig.Focus("에셋연구소:전시칸:" + slotNumber);
        }

        public void ValidateWiring()
        {
            if (원본Index == null || 연구Catalog == null || 공공관측Catalog == null
                || 공공관측출처Catalog == null || 전시장Root == null
                || cameraRig == null || 현재범위Text == null || 상세제목Text == null || 상세내용Text == null)
                throw new InvalidOperationException("에셋 연구소 연결이 완성되지 않았습니다.");
            원본Index.Validate();
            연구Catalog.Validate();
            공공관측Catalog.Validate();
            공공관측출처Catalog.Validate();
        }

        private void ShowCurrentPage()
        {
            ClearShowroom();
            var filtered = FilteredEntries();
            var pageCount = Math.Max(1, (filtered.Count + 쪽당표본수 - 1) / 쪽당표본수);
            현재쪽 = Mathf.Clamp(현재쪽, 0, pageCount - 1);
            var page = filtered.Skip(현재쪽 * 쪽당표본수).Take(쪽당표본수).ToArray();

            for (var index = 0; index < page.Length; index++)
                CreateSample(page[index], index);

            var rangeName = string.IsNullOrWhiteSpace(현재전시모음)
                ? $"{현재Pack} · {현재분류}"
                : 현재전시모음;
            현재범위Text.text = $"{rangeName} · {현재쪽 + 1}/{pageCount}쪽\n"
                             + $"현재 {page.Length}개 / 전체 {filtered.Count}개";
            if (page.Length > 0)
                SelectInternal(page[0].원본Guid값, 0, false);
            else
            {
                선택된원본Guid = string.Empty;
                상세제목Text.text = "전시할 에셋이 없습니다";
                상세내용Text.text = "다른 Pack이나 분류를 선택해 주세요.";
            }
        }

        private List<에셋원본IndexEntry> FilteredEntries()
            => 원본Index.Entries
                .Where(value => value.Pack == 현재Pack)
                .Where(value => !string.IsNullOrWhiteSpace(현재전시모음)
                                || value.분류값 == 현재분류)
                .Where(value => string.IsNullOrWhiteSpace(현재전시모음)
                                || 공공관측Catalog.IsInCollection(value.원본Guid값, 현재전시모음))
                .OrderByDescending(value => 연구Catalog.FindBySourceGuid(value.원본Guid값) != null)
                .ThenBy(value => value.원본이름값, StringComparer.Ordinal)
                .ToList();

        private int PageCount()
            => Math.Max(1, (FilteredEntries().Count + 쪽당표본수 - 1) / 쪽당표본수);

        private void CreateSample(에셋원본IndexEntry source, int slotNumber)
        {
            var wrapper = new GameObject($"전시칸_{slotNumber + 1:00}_{source.원본이름값}");
            wrapper.transform.SetParent(전시장Root, false);
            wrapper.transform.localPosition = 에셋연구소Layout.전시칸위치(slotNumber);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "선택표시받침";
            pedestal.transform.SetParent(wrapper.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, .12f, 0f);
            pedestal.transform.localScale = new Vector3(3.2f, .18f, 3.2f);
            var pedestalRenderer = pedestal.GetComponent<Renderer>();
            pedestal.GetComponent<Collider>().enabled = false;

            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = Instantiate(source.Prefab, visualRoot);
            instance.name = "SyntyPrefabInstance";
            Normalize(instance);

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var bounds = CalculateLocalBounds(wrapper.transform, instance);
            var selectionCollider = wrapper.AddComponent<BoxCollider>();
            selectionCollider.center = bounds.center;
            selectionCollider.size = new Vector3(
                Mathf.Max(1f, bounds.size.x), Mathf.Max(1f, bounds.size.y), Mathf.Max(1f, bounds.size.z));

            var view = wrapper.AddComponent<에셋연구표본View>();
            view.Configure(source.원본Guid값, slotNumber, pedestalRenderer, this);
            표본Views.Add(view);
        }

        private static void Normalize(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var scale = maxSize > .001f ? Mathf.Clamp(5.2f / maxSize, .2f, 3.5f) : 1f;
            instance.transform.localScale = Vector3.one * scale;

            renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var localCenter = instance.transform.parent.InverseTransformPoint(bounds.center);
            var localMin = instance.transform.parent.InverseTransformPoint(bounds.min);
            instance.transform.localPosition += new Vector3(-localCenter.x, .34f - localMin.y, -localCenter.z);
        }

        private static Bounds CalculateLocalBounds(Transform wrapper, GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(new Vector3(0f, 1f, 0f), Vector3.one * 2f);
            var world = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) world.Encapsulate(renderer.bounds);
            return new Bounds(wrapper.InverseTransformPoint(world.center), world.size);
        }

        private void ClearShowroom()
        {
            표본Views.Clear();
            for (var index = 전시장Root.childCount - 1; index >= 0; index--)
            {
                var child = 전시장Root.GetChild(index).gameObject;
                if (UnityEngine.Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        private static string BuildDetail(에셋원본IndexEntry source, 에셋연구Entry? study)
        {
            if (study == null)
                return $"연구 상태  {에셋연구상태Code.미검토}\n\n"
                     + $"묶음  {source.Pack}\n분류  {source.분류값}\n\n"
                     + "관찰된 사실\n아직 기록되지 않았습니다.\n\n"
                     + "다음 질문\n이 에셋이 존재한다는 것은 이 세계에서 무엇이 일어난다는 뜻일까요?";

            return $"연구 상태  {study.연구상태값}\n\n"
                 + $"묶음  {source.Pack} / 분류  {source.분류값}\n\n"
                 + $"관찰된 사실\n{study.관찰된사실값}\n\n"
                 + $"현실 의미\n{study.현실의미값}\n\n"
                 + $"월드 역할 후보\n{study.월드역할후보값}\n\n"
                 + $"함께 둘 에셋\n{study.함께둘에셋후보값}\n\n"
                 + $"연결할 자료 후보\n{study.연결할Data후보값}\n\n"
                 + $"승격 후보\n{study.승격후보VisualKey값}";
        }

        private void RefreshSelectedDetail()
        {
            if (string.IsNullOrWhiteSpace(선택된원본Guid)) return;
            var slot = 표본Views.FirstOrDefault(value => value.원본Guid값 == 선택된원본Guid)
                ?.전시칸번호값 ?? 0;
            SelectInternal(선택된원본Guid, slot, false);
        }

        private string BuildObservationDetail(에셋원본IndexEntry source,
            에셋공공관측Entry? observation)
        {
            if (observation == null)
                return $"현실 관측 연결  없음\n\n묶음  {source.Pack} / 분류  {source.분류값}\n\n"
                     + "이 에셋과 연결해 검토할 공공데이터 출처가 아직 등록되지 않았습니다.\n\n"
                     + "에셋의 모양만으로 현실의 가격·수량·상태를 추정하지 않습니다.";

            var sourceMetadata = 공공관측출처Catalog.Find(observation.출처Key값);
            var sourceSummary = sourceMetadata == null
                ? "출처 세부표 미등록"
                : $"자료 ID  {sourceMetadata.자료식별자값}\n제공기관  {sourceMetadata.제공기관값}\n"
                  + $"공간 기준  {Blank(sourceMetadata.공간기준값)}\n시간 기준  {Blank(sourceMetadata.시간기준값)}\n"
                  + $"응답 형식  {Blank(sourceMetadata.응답형식값)}\n이용 조건  {Blank(sourceMetadata.이용조건값)}\n"
                  + $"메타데이터 확인  {sourceMetadata.확인기준일값}\n"
                  + $"요청 경로  {Blank(sourceMetadata.요청경로값)}\n"
                  + $"필수 입력  {Blank(sourceMetadata.필수입력값)}\n"
                  + $"표본 호출  {Blank(sourceMetadata.표본호출결과값)}";

            return $"연결 상태  {observation.연결상태값}\n자료 단계  {observation.자료연구단계값}\n\n"
                 + $"공식 출처\n{observation.출처이름값}\n{observation.출처Key값}\n\n"
                 + sourceSummary + "\n\n"
                 + $"에셋·자료 관계\n{observation.상품관계근거값}\n\n"
                 + $"관측 범위\n지역  {Blank(observation.지역값)}\n기준기간  {Blank(observation.기준기간값)}\n"
                 + $"유통단계  {Blank(observation.유통단계값)}\n관측값  {observation.관측값표시값}"
                 + (string.IsNullOrWhiteSpace(observation.단위값) ? string.Empty : " " + observation.단위값)
                 + $"\n\n이 자료로 알 수 있는 것\n{observation.알수있는것값}\n\n"
                 + $"이 자료로 알 수 없는 것\n{observation.알수없는것값}\n\n"
                 + $"Simulation 비교\n{observation.Simulation비교값}\n\n"
                 + $"서버 연결 상태\n{observation.서버조회경로값}";
        }

        private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "미수집" : value;

        private void ApplyKoreanFont()
        {
            var font = KoreanFont();
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = font;
        }

        private static Font KoreanFont()
        {
            var installed = Font.GetOSInstalledFontNames();
            foreach (var candidate in new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial Unicode MS" })
                if (installed.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    return Font.CreateDynamicFontFromOSFont(candidate, 18);
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static string Next(IReadOnlyList<string> values, string current)
        {
            var index = values.ToList().FindIndex(value => value == current);
            return values[(index + 1 + values.Count) % values.Count];
        }
    }
}
