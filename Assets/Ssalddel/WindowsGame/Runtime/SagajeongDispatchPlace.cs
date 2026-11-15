using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 홍달 플레이스 · 주문자 뷰 (2026-11-15, 사용자: 「화면부터 … 주문자 화면부터 만들어두고 … 아래로 수직으로」).
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-홍달플레이스-종합-정본하나뷰셋.md 플5의 첫 조각(시안 C-1 · C-2).
    //  - 아래 띠의 「홍달 플레이스」를 누르면 동네를 이어서 보기로 바꾸고(이웃 동도 보임), 경영 창들을 접고(지도만 보기와 같은 상태),
    //    왼쪽에 밝은 앱 판을 연다. 닫으면 보던 동 보기 · 확대 · 위치 · 패널로 되돌린다. 검색 범위는 시험 자료의 동(면목제7동)이다. 검색어 고르기 · 목록 카드 · 쪽 · 지도 번호 핀 · 장소 상세(홈 · 메뉴 · 소식 · 사진 · 후기 · 정보).
    //  - 장소 앱의 짜임만 참고하고 이름 · 색 · 표식은 홍달 것이다. 실제 서비스의 로고 · 고유 색 · 문구는 쓰지 않는다.
    //  - 정본 모듈(플3)이 생기기 전이라 시험 자료(Resources/hongdal-place-search.json)와 순수 규칙(플레이스Policy)으로
    //    연 때와 검색어를 바꿀 때 순위표를 한 번 만든다. 화면은 주문자 투영(플레이스카드: 허용 목록)만 읽는다. 점수 · 검색량은 이 화면에 없다.
    //  - 둘러보기는 읽기만 한다: 검색량 · 노출 · 광고비 · 주문 · 시계 · 원장을 바꾸지 않는다(r193). 쪽을 넘겨도 다시 정렬하지 않는다.
    //  - 경쟁점 자리는 그 동의 근린생활시설 후보(관리건물Policy 순서)의 뒤쪽부터, 역할 · 제휴 자리를 빼고 고른다. 가상 자리이며 실제 점포와 무관하다.
    //    거리는 그 건물과 동 중심 사이의 직선거리다. 자리를 모르면 점수를 매기지 않고 뺀다(0이나 추정으로 채우지 않음).
    //  - 내 매장(카페 1호점)은 역할표의 자리와 업종 팩의 이름 · 메뉴 · 가격을 읽는다. 처음 프로필의 판매 태그는 비어 있다(태그 보완은 플8).
    public sealed partial class SagajeongDispatchView
    {
        private const string PlacePackResource = "hongdal-place-search";
        private static readonly Rect PlacePanel = new(12, 12, 392, 696);
        private static readonly Rect PlaceButton = new(700, 666, 150, 32);
        private static readonly Rect PlaceBanner = new(418, 12, 470, 34);
        private static readonly Rect PlaceViewSwitch = new(896, 12, 372, 34);
        private const float PlaceListTop = 150f;
        private const float PlaceListBottom = 652f;
        private const float PlaceCardHeight = 128f;
        private static readonly Vector2 PlaceMapCentre = new(842, 384);     // 앱 판 오른쪽 지도 영역의 가운데(번호 핀을 여기에 모은다)
        private static readonly string[] PlaceTabs = { "홈", "메뉴", "소식", "사진", "후기", "정보" };

        // 밝은 앱 판의 색. 다른 파일의 정적 색에 기대지 않는다(부분 클래스 사이의 초기화 순서는 정해져 있지 않다).
        private static readonly Color PlaceWhite = Ink(0xFAFBFC);
        private static readonly Color PlaceInkText = Ink(0x181E26);
        private static readonly Color PlaceGrey = Ink(0x707882);
        private static readonly Color PlaceLine = Ink(0xE2E6EA);
        private static readonly Color PlaceField = Ink(0xF0F3F6);
        private static readonly Color PlaceHover = Ink(0xF2F7F6);
        private static readonly Color PlaceTeal = Ink(0x109680);
        private static readonly Color PlaceTealDark = Ink(0x08685A);
        private static readonly Color PlaceTealLight = Ink(0xE5F7F2);
        private static readonly Color PlaceWarn = Ink(0xC47828);
        private static readonly Color PlaceDim = Ink(0x96A0A8);
        private static readonly Color PlaceNight = Ink(0x141A24);

        [Serializable] private sealed class PlacePack { public string schema; public string version; public string note; public string region; public string defaultKeyword; public PlacePackMine mine; public PlacePackKeyword[] keywords; public PlacePackWeights weights; public PlacePackPlace[] rivals; public PlacePackPlace[] others; }
        [Serializable] private sealed class PlacePackMine { public string placeId; public string category; public string[] tags; }
        [Serializable] private sealed class PlacePackKeyword { public string id; public string label; public string kind; public string value; public string category; }
        [Serializable] private sealed class PlacePackWeights { public double fit; public double access; public double info; public double prior; public double categoryOnlyFit; public double neutralPrior; public double reachMetres; public int pageSize; public int maxPages; }
        [Serializable] private sealed class PlacePackPlace { public string id; public string name; public string category; public string[] tags; public double info; public string state; }
        [Serializable] private sealed class PlaceChainPack { public PlaceChainNetwork network; public PlaceChainRecipe recipe; }
        [Serializable] private sealed class PlaceChainNetwork { public PlaceChainStore store; }
        [Serializable] private sealed class PlaceChainStore { public string id; public string name; public string placeId; }
        [Serializable] private sealed class PlaceChainRecipe { public string menu; public int price; }

        private bool placeOpen;
        private bool placeRestoreMapOnly;
        private DongView placeRestoreDongView;         // 닫을 때 보던 동 보기 · 확대 · 위치로 되돌린다
        private string placeRestoreSolo;
        private float placeRestoreZoom;
        private Vector2 placeRestoreOffset;
        private int placeFrameIn;                      // 동 보기를 바꾼 뒤 범위가 새로 잡히면(몇 그림 뒤) 핀에 맞춘다
        private bool placePackTried;
        private PlacePack placePack;
        private 플레이스비중 placeWeights = new();
        private List<플레이스검색어> placeKeywords = new();
        private string placeStoreId;                   // 내 매장의 장소 ID(업종 팩의 점포 ID)
        private string placeStoreName = "카페 1호점";
        private string placeMenuName;
        private int placeMenuPrice;
        private readonly List<플레이스장소> placePlaces = new();
        private int placeKeyword;
        private int placePage = 1;
        private float placeScroll;
        private string placeDetail;                    // 연 장소 ID(null이면 목록)
        private int placeTab;
        private 플레이스순위표 placeTable = new();
        private List<플레이스카드> placeCards = new();
        private string placeNote;
        private float placeFlashUntil;
        private Texture2D[] placePictures;             // 가상 그림(가게 · 컵 · 디저트) × 색 셋
        private readonly List<(Rect Rect, string PlaceId)> placePinRects = new();
        private readonly Dictionary<(int, bool), GUIStyle> placeMeasure = new();
        private int placeCardsDrawn;                   // 이번 그림의 카드 수 (촬영이 읽는다)
        private int placePinsDrawn;                    //   지도 번호 핀 수

        private bool PlaceAvailable => expansionView && DongModulesActive;
        private bool PlaceVisible => placeOpen && PlaceAvailable && !SettingsVisible && !DongPickerVisible;
        private string PlaceDongName => DongModuleById(placePack?.region ?? string.Empty)?.Entry?.name ?? "이 동";

        private bool PlaceCovers(Vector2 point) => PlaceVisible &&
            (PlacePanel.Contains(point) || PlaceBanner.Contains(point) || PlaceViewSwitch.Contains(point) || placePinRects.Any(pin => pin.Rect.Contains(point)));

        // 「홍달 플레이스」: 그 동으로 옮기고 경영 창들을 접은 뒤 주문자 뷰를 연다. 열었으면 true.
        private bool OpenPlace()
        {
            if (placeOpen) return true;
            LoadPlacePack();
            if (placePack == null)
            {
                notice = "홍달 플레이스 · 시험 자료를 읽지 못해 열지 않았어요.";
                return false;
            }
            var module = DongModuleById(placePack.region);
            if (module?.Entry == null || !module.Loaded)
            {
                notice = "홍달 플레이스 · 이 실행판에 그 동의 모듈이 없어 열지 않았어요.";
                return false;
            }
            // 장소 앱의 지도처럼 동네를 이어서 본다(이웃 동도 보임). 검색 범위는 시험 자료의 동이다.
            // 한 동만 보기에서는 지도가 그 동 안에만 머물러, 동 가장자리의 핀을 앱 판 오른쪽으로 옮길 수 없다.
            placeRestoreDongView = dongView;
            placeRestoreSolo = soloRegion;
            placeRestoreZoom = mapZoom;
            placeRestoreOffset = mapOffset;
            dongDeskOpen = false;
            if (dongView != DongView.Joined) SetDongView(DongView.Joined);
            placeRestoreMapOnly = mapOnly;
            mapOnly = true;
            placeOpen = true;
            helpOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            selectedBuilding = null;
            placeDetail = null;
            placeTab = 0;
            placePage = 1;
            placeScroll = 0;
            BuildPlaceIndex();
            PlaceFrame();
            placeFrameIn = 3;
            return true;
        }

        private void ClosePlace()
        {
            if (!placeOpen) return;
            placeOpen = false;
            placeDetail = null;
            placePinRects.Clear();
            mapOnly = placeRestoreMapOnly;
            if (dongView != placeRestoreDongView)
            {
                if (!string.IsNullOrEmpty(placeRestoreSolo)) focusDongRegion = placeRestoreSolo;
                SetDongView(placeRestoreDongView);
            }
            mapZoom = placeRestoreZoom;
            mapOffset = placeRestoreOffset;
            SyncCloseLens();
            localTextureDirty = true;
        }

        // Esc: 주문자 뷰가 열려 있으면 닫고 true(설정은 열지 않는다).
        private bool ClosePlaceByEscape()
        {
            if (!placeOpen) return false;
            ClosePlace();
            return true;
        }

        private void LoadPlacePack()
        {
            if (placePackTried) return;
            placePackTried = true;
            var asset = Resources.Load<TextAsset>(PlacePackResource);
            if (asset == null) return;
            try { placePack = JsonUtility.FromJson<PlacePack>(asset.text); }
            catch (ArgumentException) { placePack = null; }
            if (placePack?.keywords == null || placePack.keywords.Length == 0 || placePack.weights == null || placePack.rivals == null ||
                placePack.mine == null || string.IsNullOrEmpty(placePack.region))
            {
                placePack = null;
                return;
            }
            var w = placePack.weights;
            placeWeights = new 플레이스비중
            {
                Fit = w.fit, Access = w.access, Info = w.info, Prior = w.prior, CategoryOnlyFit = w.categoryOnlyFit,
                NeutralPrior = w.neutralPrior, ReachMetres = w.reachMetres, PageSize = Math.Max(1, w.pageSize), MaxPages = Math.Max(1, w.maxPages),
            };
            placeKeywords = placePack.keywords.Select(k => new 플레이스검색어
            {
                Id = k.id ?? string.Empty, Label = k.label ?? string.Empty, Kind = k.kind ?? string.Empty,
                Value = k.value ?? string.Empty, Category = k.category ?? string.Empty,
            }).ToList();
            placeKeyword = Math.Max(0, placeKeywords.FindIndex(k => k.Id == placePack.defaultKeyword));
            var chainAsset = Resources.Load<TextAsset>(HongdalKernelHost.FirstChainPackResource);
            if (chainAsset == null) return;
            try
            {
                var chain = JsonUtility.FromJson<PlaceChainPack>(chainAsset.text);
                placeStoreId = chain?.network?.store?.id;
                if (!string.IsNullOrEmpty(chain?.network?.store?.name)) placeStoreName = chain.network.store.name;
                placeMenuName = chain?.recipe?.menu;
                placeMenuPrice = chain?.recipe?.price ?? 0;
            }
            catch (ArgumentException) { }
        }

        // 장소 표를 채우고 순위표를 만든다. 자리는 건물 색인 · 역할표 · 가상 자리 후보에서 읽기만 한다.
        private void BuildPlaceIndex()
        {
            placePlaces.Clear();
            placeNote = null;
            LoadRoleBook();
            var module = DongModuleById(placePack.region);
            if (buildingShapes == null || buildingShapes.Length == 0) placeNote = "건물 색인을 읽지 못해 자리를 정하지 못했어요.";
            else EnsureManagedCandidates();
            var mineBuilding = -1;
            foreach (var pair in roleByBuilding)
                if (pair.Value.placeId == placePack.mine.placeId)
                {
                    mineBuilding = pair.Key;
                    break;
                }
            var closed = state?.campaign?.phase == "closed";
            placePlaces.Add(new 플레이스장소
            {
                Id = string.IsNullOrEmpty(placeStoreId) ? placePack.mine.placeId : placeStoreId,
                Name = placeStoreName,
                Category = placePack.mine.category ?? "카페",
                Tags = placePack.mine.tags ?? Array.Empty<string>(),
                Info = 1,
                State = closed ? "오늘 영업 마감" : "영업 중",
                DistanceMetres = PlaceDistance(module, mineBuilding),
                Building = mineBuilding,
            });
            var taken = new HashSet<int>(roleByBuilding.Keys);
            foreach (var site in managedPartnerSites) taken.Add(site);
            managedCandidates.TryGetValue(placePack.region, out var candidates);
            var free = candidates == null ? new List<int>() : candidates.Where(index => !taken.Contains(index)).Reverse().ToList();
            var all = placePack.rivals.Concat(placePack.others ?? Array.Empty<PlacePackPlace>()).ToArray();
            for (var i = 0; i < all.Length; i++)
            {
                var building = i < free.Count ? free[i] : -1;
                placePlaces.Add(new 플레이스장소
                {
                    Id = all[i].id ?? $"virtual:{i}",
                    Name = all[i].name ?? "가상 매장",
                    Category = all[i].category ?? string.Empty,
                    Tags = all[i].tags ?? Array.Empty<string>(),
                    Info = all[i].info,
                    State = string.IsNullOrEmpty(all[i].state) ? "영업 중" : all[i].state,
                    DistanceMetres = PlaceDistance(module, building),
                    Building = building,
                });
            }
            RankPlaces();
        }

        // 건물과 동 중심 사이의 직선거리(m). 모르면 NaN.
        private double PlaceDistance(DongModuleState module, int building)
        {
            if (module?.Entry == null || buildingShapes == null || building < 0 || building >= buildingShapes.Length) return double.NaN;
            var shape = buildingShapes[building];
            if (shape.Latitudes == null || shape.Longitudes == null || shape.Latitudes.Length == 0) return double.NaN;
            var latitude = shape.Latitudes.Average();
            var longitude = shape.Longitudes.Average();
            var north = (latitude - module.Entry.centerLatitude) * 110540.0;
            var east = (longitude - module.Entry.centerLongitude) * 111320.0 * Math.Cos(module.Entry.centerLatitude * Math.PI / 180.0);
            return Math.Sqrt(north * north + east * east);
        }

        private void RankPlaces()
        {
            if (placeKeywords.Count == 0) return;
            placeKeyword = Mathf.Clamp(placeKeyword, 0, placeKeywords.Count - 1);
            placeTable = 플레이스Policy.Rank(placePlaces, placeKeywords[placeKeyword], placeWeights);
            var pages = 플레이스Policy.PageCount(placeTable.Ranked.Count, placeWeights);
            placePage = pages == 0 ? 1 : Mathf.Clamp(placePage, 1, pages);
            placeCards = 플레이스Policy.ForCustomer(placeTable.Ranked, placePage, placeWeights);
            placeScroll = 0;
        }

        private void SelectPlaceKeyword(int index)
        {
            if (index < 0 || index >= placeKeywords.Count) return;
            placeKeyword = index;
            placePage = 1;
            placeDetail = null;
            RankPlaces();
            PlaceFrame();
        }

        private void SelectPlaceKeyword(string id) => SelectPlaceKeyword(placeKeywords.FindIndex(k => k.Id == id));

        // 쪽 넘기기: 같은 순위표를 자르기만 한다(다시 정렬하지 않음).
        private void SetPlacePage(int page)
        {
            var pages = 플레이스Policy.PageCount(placeTable.Ranked.Count, placeWeights);
            if (page < 1 || page > pages) return;
            placePage = page;
            placeCards = 플레이스Policy.ForCustomer(placeTable.Ranked, placePage, placeWeights);
            placeScroll = 0;
            PlaceFrame();
        }

        // 지금 쪽의 번호 핀이 앱 판 오른쪽 지도 영역에 모두 들어오게 지도를 맞춘다(보기만 바뀐다).
        private void PlaceFrame()
        {
            if (buildingShapes == null || metresPerPixel <= 0) return;
            var points = placeCards.Where(card => card.Building >= 0 && card.Building < buildingShapes.Length)
                .Select(card => PlaceLatLon(card.Building)).Where(point => point.HasValue).Select(point => point.Value).ToList();
            if (points.Count == 0) return;
            var latitude = points.Average(point => point.Latitude);
            var longitude = points.Average(point => point.Longitude);
            var cos = Math.Cos(latitude * Math.PI / 180.0);
            var extent = 150.0;
            foreach (var a in points)
                foreach (var b in points)
                {
                    var north = (a.Latitude - b.Latitude) * 110540.0;
                    var east = (a.Longitude - b.Longitude) * 111320.0 * cos;
                    extent = Math.Max(extent, Math.Sqrt(north * north + east * east));
                }
            PlaceFocus(latitude, longitude, (float)(metresPerPixel * 520.0 / extent));
        }

        // 그 건물 하나에 가까이 맞춘다(「지도에서 보기」).
        private void PlaceFrameBuilding(int building)
        {
            var point = PlaceLatLon(building);
            if (point == null || metresPerPixel <= 0) return;
            PlaceFocus(point.Value.Latitude, point.Value.Longitude, Mathf.Max(mapZoom, (float)(metresPerPixel * 520.0 / 220.0)));
        }

        private (double Latitude, double Longitude)? PlaceLatLon(int building)
        {
            if (buildingShapes == null || building < 0 || building >= buildingShapes.Length) return null;
            var shape = buildingShapes[building];
            if (shape.Latitudes == null || shape.Longitudes == null || shape.Latitudes.Length == 0) return null;
            return (shape.Latitudes.Average(), shape.Longitudes.Average());
        }

        // ViewProofCentre와 같은 식으로 지도를 옮기되, 그 점을 앱 판 오른쪽 지도 영역의 가운데에 둔다.
        private void PlaceFocus(double latitude, double longitude, float zoom)
        {
            if (metresPerPixel <= 0) return;
            mapZoom = Mathf.Clamp(zoom, DongMinZoom, MaxMapZoom);
            SyncCloseLens();
            var metres = (float)metresPerPixel / mapZoom;
            var east = (float)((longitude - centerLongitude) * longitudeScale);
            var north = (float)((latitude - centerLatitude) * 111200);
            var centre = Field.size * 0.5f;
            mapOffset = new Vector2(centre.x * (1f - mapZoom) - east / metres, centre.y * (1f - mapZoom) + north / metres);
            mapOffset += FlatDeltaFor(dioramaOn, PlaceMapCentre - Field.position - centre);
            mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
            localTextureDirty = true;
        }

        private void OpenPlaceDetail(string placeId)
        {
            if (placeCards.All(card => card.PlaceId != placeId)) return;
            placeDetail = placeId;
            placeTab = 0;
        }

        private 플레이스카드 PlaceDetailCard => placeDetail == null ? null : placeCards.FirstOrDefault(card => card.PlaceId == placeDetail);
        private bool PlaceIsMine(플레이스카드 card) => card != null && card.PlaceId == (string.IsNullOrEmpty(placeStoreId) ? placePack?.mine?.placeId : placeStoreId);

        private void DrawPlaceButton()
        {
            if (!PlaceAvailable) return;
            var button = PlaceButton;
            Paint(button, Mint, 7);
            Paint(new Rect(button.x + 1, button.y + 1, button.width - 2, button.height - 2), Ink(0x18372F), 6);
            Text(new Rect(button.x + 12, button.y, 100, button.height), "홍달 플레이스", 13, Paper, true);
            Text(new Rect(button.x + 66, button.y, 76, button.height), "주문자 화면", 9, Mint, align: TextAnchor.MiddleRight);
            if (Hit(button)) OpenPlace();
        }

        private void DrawPlace()
        {
            if (Event.current.type == EventType.Repaint)
            {
                placeCardsDrawn = 0;
                placePinsDrawn = 0;
            }
            if (!PlaceVisible)
            {
                placePinRects.Clear();
                return;
            }
            EnsurePlacePictures();
            if (Event.current.type == EventType.Repaint && placeFrameIn > 0 && --placeFrameIn == 0) PlaceFrame();
            DrawPlacePins();
            Paint(PlaceBanner, Fade(PlaceNight, 0.94f), 10);
            Text(new Rect(PlaceBanner.x + 14, PlaceBanner.y, PlaceBanner.width - 28, PlaceBanner.height),
                "홍달 플레이스 · 주문자 뷰 · 고객이 되어 둘러보는 중 — 검색량 · 노출을 만들지 않아요", 11, Paper);
            DrawPlaceViewSwitch();
            Paint(PlacePanel, PlaceLine, 14);
            Paint(new Rect(PlacePanel.x + 1, PlacePanel.y + 1, PlacePanel.width - 2, PlacePanel.height - 2), PlaceWhite, 13);
            if (PlaceDetailCard != null) DrawPlaceDetail(PlaceDetailCard);
            else
            {
                placeDetail = null;
                DrawPlaceList();
            }
        }

        private void DrawPlaceViewSwitch()
        {
            var box = PlaceViewSwitch;
            Paint(box, Fade(PlaceNight, 0.94f), 17);
            Text(new Rect(box.x + 14, box.y, 40, box.height), "보기", 11, Soft);
            var x = box.x + 52;
            foreach (var (name, on) in new[] { ("주문자", true), ("운영자", false), ("근거", false) })
            {
                var segment = new Rect(x, box.y + 4, 100, box.height - 8);
                if (on) Paint(segment, Mint, 13);
                Text(segment, name, 12, on ? Canvas : Fade(Soft, 0.55f), on, TextAnchor.MiddleCenter);
                x += 104;
            }
            Text(new Rect(box.x, box.yMax + 2, box.width - 8, 16), "운영자 · 근거 뷰는 다음 걸음(같은 정본을 다른 칸으로 읽음)", 9, Fade(Soft, 0.85f),
                align: TextAnchor.MiddleRight);
        }

        private void DrawPlacePins()
        {
            placePinRects.Clear();
            if (buildingShapes == null) return;
            var detail = PlaceDetailCard;
            foreach (var card in placeCards.OrderByDescending(item => item.Order))
            {
                if (card.Building < 0 || card.Building >= buildingShapes.Length) continue;
                if (detail != null && card.PlaceId == detail.PlaceId) continue;
                var at = RoofCentre(buildingShapes[card.Building]) + Field.position;
                if (at.x < 0 || at.y < 0 || at.x > Width || at.y > Height) continue;
                DrawPlacePin(at, card.Order, false, detail != null);
                var head = new Rect(at.x - 12, at.y - 40, 24, 40);
                if (!PlacePanel.Contains(head.center)) placePinRects.Add((head, card.PlaceId));
                if (Event.current.type == EventType.Repaint) placePinsDrawn++;
            }
            if (detail != null && detail.Building >= 0 && detail.Building < buildingShapes.Length)
            {
                var at = RoofCentre(buildingShapes[detail.Building]) + Field.position;
                if (at.x >= 0 && at.y >= 0 && at.x <= Width && at.y <= Height)
                {
                    if (Time.unscaledTime < placeFlashUntil)
                    {
                        var pulse = 22 + 10 * Mathf.PingPong(Time.unscaledTime * 2.4f, 1f);
                        Paint(Centered(at, pulse * 2, pulse * 1.2f), Fade(PlaceTeal, 0.35f), pulse);
                    }
                    DrawPlacePin(at, detail.Order, true, false);
                    var label = new Rect(at.x - 60, at.y - 64, 120, 22);
                    Paint(label, PlaceTealDark, 8);
                    Paint(new Rect(label.x + 1, label.y + 1, label.width - 2, label.height - 2), PlaceWhite, 7);
                    Text(label, detail.Name, 11, PlaceInkText, true, TextAnchor.MiddleCenter);
                    if (Event.current.type == EventType.Repaint) placePinsDrawn++;
                }
            }
            foreach (var (rect, placeId) in placePinRects.ToArray())
                if (Hit(rect)) OpenPlaceDetail(placeId);
        }

        private void DrawPlacePin(Vector2 at, int number, bool big, bool dim)
        {
            var radius = big ? 14f : 10f;
            var head = new Vector2(at.x, at.y - radius - 10);
            var colour = big ? PlaceTealDark : dim ? PlaceDim : PlaceTeal;
            Paint(new Rect(at.x - 1.5f, head.y, 3, at.y - head.y), colour);
            Paint(new Rect(at.x - 3f, at.y - 3f, 6, 6), colour, 3);
            Paint(Centered(head, radius * 2 + 4, radius * 2 + 4), Color.white, radius + 2);
            Paint(Centered(head, radius * 2, radius * 2), colour, radius);
            Text(Centered(head, radius * 2, radius * 2), number.ToString(), big ? 12 : 10, Color.white, true, TextAnchor.MiddleCenter);
        }

        private void DrawPlaceList()
        {
            var p = PlacePanel;
            Paint(new Rect(p.x + 14, p.y + 14, 24, 24), PlaceTeal, 7);
            Text(new Rect(p.x + 14, p.y + 14, 24, 24), "홍", 12, Color.white, true, TextAnchor.MiddleCenter);
            Text(new Rect(p.x + 46, p.y + 12, 180, 28), "홍달 플레이스", 16, PlaceInkText, true);
            Text(new Rect(p.x + 200, p.y + 14, 146, 24), "게임 속 가상 서비스", 9, PlaceGrey, align: TextAnchor.MiddleRight);
            var close = new Rect(p.xMax - 36, p.y + 12, 28, 28);
            Text(close, "×", 18, PlaceGrey, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                ClosePlace();
                return;
            }
            var keyword = placeKeywords.Count == 0 ? null : placeKeywords[Mathf.Clamp(placeKeyword, 0, placeKeywords.Count - 1)];
            var field = new Rect(p.x + 12, p.y + 46, p.width - 24, 36);
            Paint(field, PlaceField, 10);
            Paint(new Rect(field.x + 12, field.y + 10, 14, 14), PlaceGrey, 7);
            Paint(new Rect(field.x + 14.5f, field.y + 12.5f, 9, 9), PlaceField, 4.5f);
            Line(new Vector2(field.x + 23, field.y + 22), new Vector2(field.x + 28, field.y + 27), PlaceGrey, 2.5f);
            Text(new Rect(field.x + 38, field.y, 240, field.height), keyword?.Label ?? "검색어 없음", 15, PlaceInkText, true);

            var chipY = p.y + 90;
            Text(new Rect(p.x + 14, chipY, 50, 22), "검색어", 10, PlaceGrey);
            var chipX = p.x + 58f;
            for (var i = 0; i < placeKeywords.Count; i++)
            {
                var on = i == placeKeyword;
                var label = placeKeywords[i].Label;
                var chip = new Rect(chipX, chipY, PlaceTextWidth(label, 11, on) + 24, 22);
                Paint(chip, on ? PlaceTeal : PlaceLine, 11);
                Paint(new Rect(chip.x + 1, chip.y + 1, chip.width - 2, chip.height - 2), on ? PlaceTealLight : PlaceWhite, 10);
                Text(chip, label, 11, on ? PlaceTealDark : PlaceInkText, on, TextAnchor.MiddleCenter);
                if (Hit(chip) && !on) SelectPlaceKeyword(i);
                chipX += chip.width + 6;
            }
            Text(new Rect(p.xMax - 112, chipY, 100, 22), "정렬 · 추천순", 10, PlaceGrey, align: TextAnchor.MiddleRight);
            var dong = PlaceDongName;
            var total = placeTable.Ranked.Count;
            Text(new Rect(p.x + 14, p.y + 116, 200, 22), $"{dong} · {total}곳", 12, PlaceInkText, true);
            Text(new Rect(p.x + 170, p.y + 118, p.width - 184, 18), $"거리 기준: {dong} 중심", 9, PlaceGrey, align: TextAnchor.MiddleRight);
            Paint(new Rect(p.x + 12, PlaceListTop - 4, p.width - 24, 1), PlaceLine);

            var list = new Rect(p.x, PlaceListTop, p.width, PlaceListBottom - PlaceListTop);
            var maxScroll = Mathf.Max(0, placeCards.Count * PlaceCardHeight - list.height);
            var current = Event.current;
            if (current.type == EventType.ScrollWheel && list.Contains(current.mousePosition))
            {
                placeScroll += current.delta.y * 16f;
                current.Use();
            }
            placeScroll = Mathf.Clamp(placeScroll, 0, maxScroll);
            if (placeCards.Count == 0)
                Text(new Rect(list.x + 14, list.y + 20, list.width - 28, 40), placeNote ?? "이 검색어에 맞는 곳이 없어요.", 12, PlaceGrey, wrap: true);
            GUI.BeginGroup(list);
            for (var i = 0; i < placeCards.Count; i++)
            {
                var y = i * PlaceCardHeight - placeScroll;
                if (y + PlaceCardHeight < 0 || y > list.height) continue;
                if (DrawPlaceCard(placeCards[i], y, list.width, i)) break;
            }
            GUI.EndGroup();
            if (maxScroll > 0)
            {
                var track = list.height - 8;
                var thumb = Mathf.Max(30, track * list.height / (placeCards.Count * PlaceCardHeight));
                Paint(new Rect(p.xMax - 7, list.y + 4 + (track - thumb) * placeScroll / maxScroll, 3, thumb), PlaceLine, 1.5f);
            }

            Paint(new Rect(p.x + 12, PlaceListBottom + 2, p.width - 24, 1), PlaceLine);
            var pages = 플레이스Policy.PageCount(total, placeWeights);
            var footY = PlaceListBottom + 14;
            var width = pages * 30 + (placePage > 1 ? 44 : 0) + (placePage < pages ? 44 : 0);
            var footX = p.center.x - width / 2f;
            if (placePage > 1)
            {
                var previous = new Rect(footX, footY, 40, 26);
                Text(previous, "이전", 11, PlaceGrey, align: TextAnchor.MiddleCenter);
                if (Hit(previous)) SetPlacePage(placePage - 1);
                footX += 44;
            }
            for (var page = 1; page <= pages; page++)
            {
                var on = page == placePage;
                var button = new Rect(footX, footY, 26, 26);
                Paint(button, on ? PlaceTeal : PlaceLine, 6);
                if (!on) Paint(new Rect(button.x + 1, button.y + 1, 24, 24), PlaceWhite, 5);
                Text(button, page.ToString(), 11, on ? Color.white : PlaceInkText, on, TextAnchor.MiddleCenter);
                if (Hit(button)) SetPlacePage(page);
                footX += 30;
            }
            if (placePage < pages)
            {
                var next = new Rect(footX, footY, 40, 26);
                Text(next, "다음", 11, PlaceGrey, align: TextAnchor.MiddleCenter);
                if (Hit(next)) SetPlacePage(placePage + 1);
            }
            var shown = 플레이스Policy.Shown(total, placeWeights);
            Text(new Rect(p.x + 14, footY, 120, 26), shown < total ? $"후보 {total}곳 중 {shown}곳 표시" : string.Empty, 9, PlaceGrey);
            Text(new Rect(p.xMax - 120, footY, 108, 26), "사진은 가상 그림", 9, PlaceGrey, align: TextAnchor.MiddleRight);
        }

        // 목록 카드 하나(목록 묶음 안의 좌표). 눌러서 상세로 갔으면 true.
        private bool DrawPlaceCard(플레이스카드 card, float y, float width, int index)
        {
            var row = new Rect(0, y, width, PlaceCardHeight);
            if (row.Contains(Event.current.mousePosition)) Paint(new Rect(4, y + 2, width - 8, PlaceCardHeight - 4), PlaceHover, 10);
            const float x = 14f;
            Paint(new Rect(x, y + 12, 20, 20), PlaceTeal, 10);
            Text(new Rect(x, y + 12, 20, 20), card.Order.ToString(), 10, Color.white, true, TextAnchor.MiddleCenter);
            Text(new Rect(x + 28, y + 8, 250, 26), card.Name, 15, PlaceInkText, true);
            var nameWidth = PlaceTextWidth(card.Name, 15, true);
            Text(new Rect(x + 34 + nameWidth, y + 12, 90, 20), card.Category, 10, PlaceGrey);
            var open = card.State == "영업 중";
            Text(new Rect(x + 28, y + 34, 300, 18), card.State, 11, open ? PlaceTeal : PlaceWarn, open);
            Text(new Rect(x + 28, y + 52, 320, 18), $"{PlaceDongName} · 약 {card.DistanceMetres:N0} m", 10, PlaceGrey);
            var tagX = x + 28;
            if (card.Tags.Length == 0) Text(new Rect(tagX, y + 72, 200, 18), "판매 태그 없음", 9, PlaceGrey);
            foreach (var tag in card.Tags) tagX += PlacePill(tagX, y + 72, tag, 9) + 5;
            for (var k = 0; k < 3; k++)
                GUI.DrawTexture(new Rect(x + 28 + k * 112, y + 95, 106, 26), PlacePicture(k, index + k), ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 5);
            Paint(new Rect(x, y + PlaceCardHeight - 1, width - 28, 1), PlaceLine);
            if (Event.current.type == EventType.Repaint) placeCardsDrawn++;
            if (!Hit(row)) return false;
            OpenPlaceDetail(card.PlaceId);
            return true;
        }

        private void DrawPlaceDetail(플레이스카드 card)
        {
            var p = PlacePanel;
            var mine = PlaceIsMine(card);
            var back = new Rect(p.x + 12, p.y + 12, 110, 26);
            Text(back, "← 목록으로", 12, PlaceGrey);
            var keyword = placeKeywords.Count == 0 ? string.Empty : placeKeywords[Mathf.Clamp(placeKeyword, 0, placeKeywords.Count - 1)].Label;
            Text(new Rect(p.xMax - 150, p.y + 12, 136, 26), $"「{keyword}」 {card.Order}번째", 10, PlaceGrey, align: TextAnchor.MiddleRight);
            if (Hit(back))
            {
                placeDetail = null;
                return;
            }
            var cover = new Rect(p.x + 12, p.y + 44, p.width - 24, 128);
            GUI.DrawTexture(cover, PlacePicture(0, card.Order), ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 10);
            var tag = new Rect(cover.x + 8, cover.yMax - 26, 176, 18);
            Paint(tag, Fade(Color.white, 0.92f), 9);
            Text(tag, "가상 그림 · 게임 장면에서 구울 예정", 9, PlaceGrey, align: TextAnchor.MiddleCenter);
            Text(new Rect(p.x + 14, p.y + 178, 260, 30), card.Name, 19, PlaceInkText, true);
            Text(new Rect(p.x + 22 + PlaceTextWidth(card.Name, 19, true), p.y + 186, 90, 20), card.Category, 11, PlaceGrey);
            var open = card.State == "영업 중";
            Text(new Rect(p.x + 14, p.y + 210, 340, 20), card.State + (mine ? " · 영업시간 미정(게임 일정 연결 전)" : string.Empty), 11,
                open ? PlaceTeal : PlaceWarn, open);
            Text(new Rect(p.x + 14, p.y + 230, 340, 20), $"{PlaceDongName} · {PlaceDongName} 중심에서 약 {card.DistanceMetres:N0} m", 11, PlaceGrey);
            var map = new Rect(p.x + 12, p.y + 258, 178, 36);
            Paint(map, PlaceField, 10);
            Text(map, "지도에서 보기", 12, PlaceTealDark, true, TextAnchor.MiddleCenter);
            if (Hit(map))
            {
                placeFlashUntil = Time.unscaledTime + 2.5f;
                PlaceFrameBuilding(card.Building);
            }
            var order = new Rect(p.x + 202, p.y + 258, 178, 36);
            Paint(order, PlaceField, 10);
            Text(order, "주문 · 준비 전", 12, PlaceDim, align: TextAnchor.MiddleCenter);
            var tabWidth = (p.width - 24) / PlaceTabs.Length;
            for (var i = 0; i < PlaceTabs.Length; i++)
            {
                var tab = new Rect(p.x + 12 + i * tabWidth, p.y + 302, tabWidth, 26);
                var on = i == placeTab;
                Text(tab, PlaceTabs[i], 12, on ? PlaceInkText : PlaceGrey, on, TextAnchor.MiddleCenter);
                if (on) Paint(new Rect(tab.x + 12, tab.yMax, tab.width - 24, 3), PlaceTeal, 1.5f);
                if (Hit(tab)) placeTab = i;
            }
            Paint(new Rect(p.x + 12, p.y + 331, p.width - 24, 1), PlaceLine);
            var y = p.y + 342;
            switch (placeTab)
            {
                case 0:
                    y = PlaceSection(y, "메뉴");
                    y = PlaceMenuRows(card, mine, y, 1);
                    y = PlaceSection(y + 6, "이용 방법");
                    var x = p.x + 14f;
                    if (mine)
                    {
                        x += PlacePill(x, y, "배달", 10) + 6;
                        x += PlacePill(x, y, "포장 · 미정", 10, PlaceGrey, PlaceWhite, PlaceLine) + 6;
                        PlacePill(x, y, "매장 이용 · 미정", 10, PlaceGrey, PlaceWhite, PlaceLine);
                    }
                    else Text(new Rect(x, y, 300, 18), "이용 방법 정보 없음", 10, PlaceGrey);
                    y = PlaceSection(y + 28, "위치");
                    var where = new Rect(p.x + 14, y, 240, 20);
                    Text(where, $"{PlaceDongName} · 지도에서 보기 →", 11, PlaceTealDark, true);
                    if (Hit(where))
                    {
                        placeFlashUntil = Time.unscaledTime + 2.5f;
                        PlaceFrameBuilding(card.Building);
                    }
                    Text(new Rect(p.x + 14, y + 20, 340, 18), "가상 매장 · 실제 건물의 쓰임과 무관", 9, PlaceGrey);
                    y = PlaceSection(y + 46, "후기");
                    Text(new Rect(p.x + 14, y, 340, 20), "아직 이용 후기가 없어요", 11, PlaceInkText);
                    break;
                case 1:
                    PlaceMenuRows(card, mine, y, 8);
                    break;
                case 2:
                    Text(new Rect(p.x + 14, y, 340, 20), "아직 소식이 없어요", 12, PlaceInkText);
                    Text(new Rect(p.x + 14, y + 22, 340, 18), "가상 점주가 개업 · 휴무 · 행사를 알리면 여기에 보여요", 9, PlaceGrey);
                    break;
                case 3:
                    for (var k = 0; k < 3; k++)
                    {
                        var picture = new Rect(p.x + 12 + k * 124, y, 116, 116);
                        GUI.DrawTexture(picture, PlacePicture(k, card.Order + k), ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 8);
                    }
                    Text(new Rect(p.x + 14, y + 124, 360, 18), "모두 가상 그림이에요 · 게임 장면에서 구울 예정 · 실제 사진 아님", 9, PlaceGrey);
                    break;
                case 4:
                    Text(new Rect(p.x + 14, y, 340, 20), "아직 이용 후기가 없어요", 12, PlaceInkText);
                    Text(new Rect(p.x + 14, y + 22, 360, 34), "가상 고객이 실제로 주문 · 이용한 뒤에 생겨요. 처음부터 채우지 않고, 별점은 없어요.", 9, PlaceGrey, wrap: true);
                    break;
                default:
                    var info = new[]
                    {
                        ("분류", card.Category),
                        ("영업", mine ? card.State + " · 영업시간 미정(게임 일정 연결 전)" : card.State),
                        ("위치", $"{PlaceDongName} · 실제 건물의 쓰임과 무관"),
                        ("자료", mine ? "게임 가맹 매장(가상)" : "시험 자료의 가상 매장(지어낸 이름)"),
                    };
                    foreach (var (name, value) in info)
                    {
                        Text(new Rect(p.x + 14, y, 60, 20), name, 11, PlaceGrey);
                        Text(new Rect(p.x + 76, y, 300, 20), value, 11, PlaceInkText);
                        y += 26;
                    }
                    break;
            }
        }

        private float PlaceSection(float y, string title)
        {
            Text(new Rect(PlacePanel.x + 14, y, 200, 20), title, 13, PlaceInkText, true);
            return y + 24;
        }

        // 메뉴 줄. 내 매장은 업종 팩의 메뉴 · 가격, 시험 자료의 가상 매장은 판매 태그를 메뉴 이름으로(가격 정보 없음).
        private float PlaceMenuRows(플레이스카드 card, bool mine, float y, int limit)
        {
            var p = PlacePanel;
            var rows = new List<(string Name, string Price, string Status)>();
            if (mine && !string.IsNullOrEmpty(placeMenuName)) rows.Add((placeMenuName, placeMenuPrice > 0 ? $"{placeMenuPrice:N0}원" : "가격 미정", "판매 중"));
            else foreach (var tag in card.Tags) rows.Add((tag, "가격 정보 없음", string.Empty));
            if (rows.Count == 0)
            {
                Text(new Rect(p.x + 14, y, 340, 20), "메뉴 정보 없음", 11, PlaceGrey);
                return y + 26;
            }
            foreach (var (name, price, status) in rows.Take(limit))
            {
                GUI.DrawTexture(new Rect(p.x + 14, y, 46, 40), PlacePicture(1, name.Length), ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 6);
                Text(new Rect(p.x + 70, y, 200, 20), name, 13, PlaceInkText, true);
                Text(new Rect(p.x + 70, y + 20, 200, 18), price, 11, mine ? PlaceInkText : PlaceGrey);
                if (!string.IsNullOrEmpty(status)) PlacePill(p.xMax - 76, y + 10, status, 9);
                y += 48;
            }
            return y;
        }

        private float PlacePill(float x, float y, string text, int size) => PlacePill(x, y, text, size, PlaceTealDark, PlaceTealLight, PlaceTealLight);

        private float PlacePill(float x, float y, string text, int size, Color ink, Color fill, Color edge)
        {
            var width = PlaceTextWidth(text, size, false) + 14;
            var pill = new Rect(x, y, width, 18);
            Paint(pill, edge, 9);
            Paint(new Rect(x + 1, y + 1, width - 2, 16), fill, 8);
            Text(pill, text, size, ink, align: TextAnchor.MiddleCenter);
            return width;
        }

        private float PlaceTextWidth(string text, int size, bool bold)
        {
            if (!placeMeasure.TryGetValue((size, bold), out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = bold ? boldFont : font, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, fontSize = size, padding = new RectOffset(),
                };
                placeMeasure[(size, bold)] = style;
            }
            return style.CalcSize(new GUIContent(text ?? string.Empty)).x;
        }

        // ── 가상 그림: 가게 · 컵 · 디저트를 색 셋으로 그린다(실제 사진 · 상표 없음) ──
        private Texture2D PlacePicture(int kind, int tint)
        {
            EnsurePlacePictures();
            return placePictures[((kind % 3) + 3) % 3 * 3 + ((tint % 3) + 3) % 3];
        }

        private void EnsurePlacePictures()
        {
            if (placePictures != null) return;
            var tints = new[] { Ink(0xC4D6DE), Ink(0xDED2C4), Ink(0xCEDECE) };
            placePictures = new Texture2D[9];
            for (var kind = 0; kind < 3; kind++)
                for (var tint = 0; tint < 3; tint++)
                    placePictures[kind * 3 + tint] = DrawPlacePicture(kind, tints[tint]);
        }

        private static Texture2D DrawPlacePicture(int kind, Color tint)
        {
            const int w = 192, h = 96;
            var pixels = new Color32[w * h];
            Color32 background = tint;
            for (var i = 0; i < pixels.Length; i++) pixels[i] = background;
            void Put(int x, int y, Color32 colour)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[(h - 1 - y) * w + x] = colour;          // 위가 0인 좌표로 그린다
            }
            void Box(float x0, float y0, float x1, float y1, Color colour)
            {
                for (var y = (int)y0; y < (int)y1; y++)
                    for (var x = (int)x0; x < (int)x1; x++) Put(x, y, colour);
            }
            void Oval(float cx, float cy, float rx, float ry, Color colour)
            {
                for (var y = (int)(cy - ry); y <= (int)(cy + ry); y++)
                    for (var x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                    {
                        var dx = (x - cx) / rx;
                        var dy = (y - cy) / ry;
                        if (dx * dx + dy * dy <= 1) Put(x, y, colour);
                    }
            }
            void Tri(Vector2 a, Vector2 b, Vector2 c, Color colour)
            {
                var minX = (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                var maxX = (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                var minY = (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                var maxY = (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                float Edge(Vector2 p, Vector2 q, Vector2 r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
                var area = Edge(a, b, c);
                for (var y = minY; y <= maxY; y++)
                    for (var x = minX; x <= maxX; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        var w0 = Edge(b, c, point);
                        var w1 = Edge(c, a, point);
                        var w2 = Edge(a, b, point);
                        if (area > 0 ? w0 >= 0 && w1 >= 0 && w2 >= 0 : w0 <= 0 && w1 <= 0 && w2 <= 0) Put(x, y, colour);
                    }
            }
            var cream = Ink(0xF4EEE2);
            switch (kind)
            {
                case 0:      // 가게 앞
                    Box(0, 80, w, h, Ink(0xB2BEC6));
                    Box(40, 34, 152, 84, Ink(0x203A42));
                    for (var i = 0; i < 9; i++) Box(34 + i * 14, 20, 48 + i * 14, 38, i % 2 == 0 ? Ink(0x109680) : Ink(0xFAFAFA));
                    Box(52, 46, 104, 76, Ink(0x96C4D2));
                    Box(116, 46, 140, 84, Ink(0x5A463C));
                    break;
                case 1:      // 컵
                    Oval(96, 80, 40, 7, cream);
                    Tri(new Vector2(72, 30), new Vector2(120, 30), new Vector2(112, 78), cream);
                    Tri(new Vector2(72, 30), new Vector2(112, 78), new Vector2(80, 78), cream);
                    Oval(124, 52, 12, 14, cream);
                    Oval(124, 52, 6, 8, tint);
                    Oval(96, 31, 24, 6, Ink(0x96684A));
                    break;
                default:     // 디저트 한 조각
                    Tri(new Vector2(60, 76), new Vector2(132, 76), new Vector2(112, 26), cream);
                    Box(60, 70, 132, 80, Ink(0xECB4AA));
                    Oval(112, 22, 7, 7, Ink(0xD23C46));
                    break;
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Hongdal place picture", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // 방향 촬영: 주문자 뷰를 열어 「라떼」 1쪽 · 2쪽 · 내 매장 상세 · 메뉴 탭 · 「카페」 1쪽을 찍고 Esc로 닫는다.
        // 열고 보는 동안 시계 · 원장이 그대로이고, 닫으면 지도만 보기 상태가 원래대로 돌아오는지 본다.
        private IEnumerator ViewProofPlace(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var mapOnlyBefore = mapOnly;
            yield return null;
            var viewBefore = dongView;
            var soloBefore = soloRegion;
            receipt.placeOpened = OpenPlace() && PlaceVisible;
            receipt.placeFoldedPanels = mapOnly;
            SelectPlaceKeyword("kw:latte");
            placeFrameIn = 3;
            for (var frame = 0; frame < 10; frame++) yield return null;
            receipt.placeKeyword = placeKeywords.Count > 0 ? placeKeywords[placeKeyword].Label : string.Empty;
            receipt.placeCandidates = placeTable.Ranked.Count;
            receipt.placeExcluded = placeTable.Excluded.Count;
            receipt.placePages = 플레이스Policy.PageCount(placeTable.Ranked.Count, placeWeights);
            receipt.placeSitesPlaced = placePlaces.Count(place => place.Building >= 0);
            receipt.placePlaces = placePlaces.Count;
            receipt.placeFirstPageCards = placeCards.Count;
            receipt.placeCardsDrawn = placeCardsDrawn;
            receipt.placePinsDrawn = placePinsDrawn;
            receipt.placePinsClear = placePinRects.Count;
            var mine = placeTable.Ranked.FirstOrDefault(row => row.Place.Id == placeStoreId);
            receipt.placeMineRank = mine?.Rank ?? 0;
            receipt.placeMinePage = mine?.Page ?? 0;
            receipt.placeOrderMatches = placeCards.Select(card => card.PlaceId)
                .SequenceEqual(placeTable.Ranked.Where(row => row.Page == 1).Select(row => row.Place.Id));
            receipt.placeCustomerFieldsOnly = 플레이스Policy.CustomerFieldsOnly();
            yield return SaveFrame(directory, "93-place-list.png");

            var rankedBefore = string.Join(",", placeTable.Ranked.Select(row => row.Place.Id + ":" + row.Milli));
            SetPlacePage(2);
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.placeSecondPageCards = placeCards.Count;
            receipt.placePageKeepsOrder = placePage == 2 && rankedBefore == string.Join(",", placeTable.Ranked.Select(row => row.Place.Id + ":" + row.Milli));
            yield return SaveFrame(directory, "94-place-page2.png");

            var mineCard = placeCards.FirstOrDefault(card => card.PlaceId == placeStoreId);
            if (mineCard == null)
            {
                SetPlacePage(receipt.placeMinePage);
                mineCard = placeCards.FirstOrDefault(card => card.PlaceId == placeStoreId);
            }
            if (mineCard != null) OpenPlaceDetail(mineCard.PlaceId);
            placeFlashUntil = Time.unscaledTime + 5f;
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.placeDetailOpened = placeDetail != null && placeDetail == placeStoreId;
            yield return SaveFrame(directory, "95-place-detail.png");
            placeTab = 1;
            for (var frame = 0; frame < 3; frame++) yield return null;
            yield return SaveFrame(directory, "96-place-menu.png");

            placeDetail = null;
            SelectPlaceKeyword("kw:cafe");
            for (var frame = 0; frame < 4; frame++) yield return null;
            var mineCafe = placeTable.Ranked.FirstOrDefault(row => row.Place.Id == placeStoreId);
            receipt.placeMineRankCafe = mineCafe?.Rank ?? 0;
            receipt.placeCafeCandidates = placeTable.Ranked.Count;
            yield return SaveFrame(directory, "97-place-cafe.png");

            var wasOpen = placeOpen;
            receipt.placeEscClosed = wasOpen && ClosePlaceByEscape() && !placeOpen && !settingsOpen && mapOnly == mapOnlyBefore &&
                dongView == viewBefore && (viewBefore != DongView.Solo || soloRegion == soloBefore);
            yield return null;
            receipt.placeUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
