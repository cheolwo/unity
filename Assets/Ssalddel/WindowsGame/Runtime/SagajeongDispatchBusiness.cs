using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 0단계 뼈대 S4(건물 역할표)·S5(표현 층 틀)와 첫 인계 사슬의 화면 (2026-11-12).
    // 화면은 규칙을 계산하지 않는다. 커널의 상태 사본과 투영(FirstChainProjection)을 읽어 그리고, 명령만 보낸다.
    //  - 사업은 건물의 역할로 얹힌다: 색이 있는 건물이 내가 운영하는 곳이다.
    //  - 사업마다 표현 층이 하나다. 층은 켜고 끌 수 있고, 어느 높이에서 무엇을 보일지는 LayerVisible 한곳에서 정한다.
    //  - 사업 사이는 인계 사슬 한 줄로 보인다.
    public sealed partial class SagajeongDispatchView
    {
        private const string RoleBookResource = "hongdal-building-roles";
        private const int BusinessFood = 0, BusinessRestaurant = 1, BusinessFranchise = 2, BusinessCargo = 3,
            BusinessCenter = 4, BusinessProducer = 5;
        private static readonly Color Sky = Ink(0x8FB4FF);
        private static readonly Rect BusinessLayerPanel = new(22, 388, 188, 126);
        private static readonly string[] BusinessTitles = { "기사 운영", "음식점", "가맹점", "창고·화물", "물류센터", "생산자" };
        private static readonly string[] BusinessRoles =
        {
            "감 · 배송", "태 · 최종 조리   기토 · 판매", "공급안 · 보충 발주 · 검수",
            "보관 · 출고 · 운송", "출고 회차 · 간선 화물", "진 · 원재료 생산"
        };
        private static readonly string[] BusinessDescriptions =
        {
            "기사와 준비된 주문을 연결하고 출근·교대를 살펴봅니다. 기존 배차 데스크에서 같은 회차를 관리합니다.",
            "주문자 가까운 매장에서 재료를 조리·조합해 완성품을 준비하고 판매합니다.",
            "본부가 정한 공급안으로 점포가 재료를 보충 발주합니다. 들어온 화물을 검수하고, 인수한 만큼만 쓸 수 있는 재고가 됩니다.",
            "창고에서 물건을 내고 화물차가 점포까지 옮깁니다. 지금은 센터에서 점포로 가는 화물 한 대가 다닙니다.",
            "여러 점포의 발주를 회차로 묶어 출고합니다. 수도권 지도에서 직접 운영하게 됩니다. 지금은 스스로 출고합니다.",
            "필요한 원재료와 품목을 정하고, 생산자와 공급 수량·준비 시각을 연결하는 영역입니다."
        };
        private static readonly string[] LayerTitles = { "음식 배달", "음식점", "가맹점", "창고·화물" };

        [Serializable]
        private sealed class BuildingRoleBook
        {
            public string schema;
            public BuildingRole[] roles;
            public PlaceRoute[] routes;
        }

        [Serializable]
        private sealed class BuildingRole
        {
            public string placeId;
            public string buildingId;
            public string business;
            public string owner;
            public string title;
            public string storeId;
        }

        [Serializable]
        private sealed class PlaceRoute
        {
            public string id;
            public string toPlaceId;
            public double[] latlon;
        }

        private BuildingRoleBook roleBook;
        private bool roleBookTried;
        private readonly Dictionary<int, BuildingRole> roleByBuilding = new();
        private readonly Dictionary<string, int> buildingByPlace = new();
        private readonly bool[] layerOn = { true, true, true, true };
        private ChainView chain = new();
        private int businessStance;
        private int orderQuantity;
        private int inspectAccept, inspectQuarantine, inspectReject;
        private string inspectReceipt;
        private string businessNotice;
        private bool businessNoticeOk;

        private static Color BusinessColor(int business) => business switch
        {
            BusinessFood => Mint,
            BusinessRestaurant => Gold,
            BusinessFranchise => Lilac,
            BusinessCargo => Sky,
            BusinessCenter => Sky,
            _ => Soft,
        };

        // 위쪽 띠(인계 사슬 · 신호)의 왼쪽 끝. 왼쪽에 동 관리 창이나 매장 창이 떠 있으면 그 오른쪽에서 시작해 창에 가리지 않는다.
        private float TopStripLeft => DongDeskVisible || StoreDeskVisible ? 330f : 222f;
        private Rect ChainStrip => new(TopStripLeft, mapLevel == MapLevel.Dong ? 92 : 136, 976f - TopStripLeft, 78);
        private bool ChainStripVisible => expansionView && !mapOnly && !showDesk && chain.Started && mapLevel != MapLevel.Nation;
        private bool BusinessCovers(Vector2 point) => !showDesk &&
            (SidePanelsVisible && BusinessLayerPanel.Contains(point) || ChainStripVisible && ChainStrip.Contains(point));

        // 축척별 표시 규칙을 여기 한곳에 둔다. 동네 높이에서만 건물 역할과 차량을 그리고, 멀리 볼 때는 이름표를 접는다.
        private bool LayerVisible(int business) =>
            mapLevel == MapLevel.Dong && (business >= layerOn.Length || layerOn[business]);
        private bool LayerLabelsVisible => mapZoom >= FarViewZoom;

        private T ModuleSnapshot<T>(string osId) where T : class => engine?.Kernel?.Find(osId)?.Snapshot() as T;
        private long BusinessNow => state?.elapsedMs ?? 0;

        private void RefreshBusiness()
        {
            if (Event.current.type != EventType.Layout) return;
            chain = engine?.Kernel == null ? new ChainView() : FirstChainProjection.Build(engine.Kernel, BusinessNow);
        }

        private GameResult SendBusiness(string osId, string action, Action<GameCommand> fill)
        {
            var command = new GameCommand
            {
                OsId = osId,
                Action = action,
                CommandId = "game-" + Guid.NewGuid().ToString("N"),
                ElapsedMs = BusinessNow,
            };
            fill?.Invoke(command);
            var result = engine.Business(command);
            businessNotice = result.Message;
            businessNoticeOk = result.Ok;
            chain = FirstChainProjection.Build(engine.Kernel, BusinessNow);
            return result;
        }

        // ── 건물 역할표 ─────────────────────────────────────────────────────────────

        private void LoadRoleBook()
        {
            if (roleBookTried) return;
            roleBookTried = true;
            LoadBuildingIndex();
            roleBook = ReadMapResource<BuildingRoleBook>(RoleBookResource);
            if (roleBook?.roles == null || buildingIndex == null)
            {
                roleBook = null;
                return;
            }
            var indexById = new Dictionary<string, int>(buildingShapes.Length);
            for (var index = 0; index < buildingShapes.Length; index++) indexById[buildingShapes[index].Record.id] = index;
            foreach (var role in roleBook.roles)
            {
                // r54: 행정동 모듈 색인을 쓰면 역할표의 건물 ID를 같은 자리의 모듈 건물로 옮긴다. 대응이 없으면 역할을 얹지 않는다.
                var buildingId = DongModulesActive ? DongRoleBuilding(role.buildingId) : role.buildingId;
                if (string.IsNullOrEmpty(buildingId) || !indexById.TryGetValue(buildingId, out var index)) continue;
                roleByBuilding[index] = role;
                buildingByPlace[role.placeId] = index;
            }
        }

        private static int RoleBusiness(BuildingRole role) => role.business switch
        {
            "restaurant" => BusinessRestaurant,
            "franchise" => BusinessFranchise,
            _ => BusinessCargo,
        };

        private BuildingRole SelectedRole()
        {
            if (selectedBuilding == null) return null;
            LoadRoleBook();
            return roleByBuilding.TryGetValue(selectedBuilding.Value, out var role) ? role : null;
        }

        private void SelectPlace(string placeId)
        {
            LoadRoleBook();
            if (!buildingByPlace.TryGetValue(placeId, out var index)) return;
            if (mapLevel != MapLevel.Dong) ReturnToDong();
            ShowDongOf(buildingShapes[index].Record.region);     // 한 동만 보는 중이면 그 건물의 동으로 넘어간다
            selectedBuilding = index;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
        }

        private Vector2 RoofCentre(BuildingShape shape)
        {
            var height = DioramaVisible ? shape.Record.height : 0f;
            var sum = Vector2.zero;
            for (var i = 0; i < shape.Ring.Length; i++) sum += PlotBuilding(shape, i, height);
            return sum / shape.Ring.Length;
        }

        private void DrawBuildingOutline(BuildingShape shape, Color color, float roofWidth, float baseWidth)
        {
            var count = shape.Ring.Length;
            var height = DioramaVisible ? shape.Record.height : 0f;
            var bases = new Vector2[count];
            var roofs = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                bases[i] = PlotBuilding(shape, i);
                roofs[i] = PlotBuilding(shape, i, height);
            }
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                Line(bases[i], bases[next], color, DioramaVisible ? baseWidth : roofWidth);
                if (!DioramaVisible) continue;
                Line(bases[i], roofs[i], color, baseWidth);
                Line(roofs[i], roofs[next], color, roofWidth);
            }
        }

        // 색이 있는 건물 = 내가 운영하는 곳. 메시는 건드리지 않고 테두리와 이름표만 얹는다.
        private void DrawRoleBuildings()
        {
            LoadRoleBook();
            if (roleBook == null) return;
            foreach (var pair in roleByBuilding)
            {
                var business = RoleBusiness(pair.Value);
                if (!LayerVisible(business)) continue;
                var shape = buildingShapes[pair.Key];
                if (shape.Record.fill && (!DioramaVisible || !dioramaFillOn)) continue;   // 예시 건물은 2.5D에서만 보인다
                var color = BusinessColor(business);
                var selected = selectedBuilding == pair.Key && actorFocus == null;
                if (Event.current.type == EventType.Repaint && !selected) DrawBuildingOutline(shape, color, 2.5f, 1.2f);
                if (business == BusinessRestaurant) continue;       // 매장은 기존 표식이 이름표다
                var top = RoofCentre(shape);
                var status = RoleStatus(pair.Value);
                if (DrawRoleLight(pair.Key, pair.Value, top, status, color)) continue;      // 빛으로 보는 동안에는 이름표 대신 빛 (SagajeongDispatchRoam.cs). 멀리서도 보인다
                if (!LayerLabelsVisible) continue;                  // 이름표는 가까이 볼 때만
                var width = Mathf.Max(pair.Value.title.Length, status.Length) * 11f + 22f;
                var plate = new Rect(top.x - width / 2, top.y - 52, width, 36);
                if (Event.current.type == EventType.Repaint) Line(top, new Vector2(top.x, plate.yMax), color, 1.5f);
                Paint(plate, color, 8);
                Paint(new Rect(plate.x + 1.5f, plate.y + 1.5f, plate.width - 3, plate.height - 3), Canvas, 7);
                Text(new Rect(plate.x, plate.y + 2, plate.width, 17), pair.Value.title, 12, Paper, true, TextAnchor.MiddleCenter);
                Text(new Rect(plate.x, plate.y + 17, plate.width, 16), status, 10, color, align: TextAnchor.MiddleCenter);
                if (HitMapMarker(plate)) SelectPlace(pair.Value.placeId);
            }
        }

        private string RoleStatus(BuildingRole role)
        {
            if (RoleBusiness(role) != BusinessFranchise) return "가상 창고";
            if (!chain.Started) return "우유 없음";
            if (chain.WaitingInspection) return "검수 대기";
            if (chain.TruckMoving) return "화물 오는 중";
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            if (store == null) return "가맹점";
            if (store.Receipts.Count == 0) return "출고 준비 중";
            return chain.ActionKey == "produce" ? "라떼 주문 대기" : "우유 " + store.AvailableContent.ToString("N0") + store.ContentUnit;
        }

        // ── 화물차 ───────────────────────────────────────────────────────────────

        private void DrawCargoTruck()
        {
            if (!LayerVisible(BusinessCargo) || !(chain.TruckMoving || chain.WaitingInspection)) return;
            LoadRoleBook();
            var route = roleBook?.routes?.FirstOrDefault();
            if (route?.latlon == null || route.latlon.Length < 4) return;
            // 길의 앞 절반은 지도 밖(센터에서 오는 길)이다. 뒤 절반 동안 동쪽 끝에서 점포까지 들어온다.
            var entering = chain.TruckMoving && chain.TruckProgress < 0.5f;
            var along = chain.WaitingInspection ? 1f : Mathf.Clamp01((chain.TruckProgress - 0.5f) * 2f);
            var position = AlongRoute(route.latlon, along);
            var point = PlotScene(position.x, position.y);
            if (Event.current.type == EventType.Repaint && chain.TruckMoving)
            {
                for (var i = 2; i < route.latlon.Length; i += 2)
                    Line(PlotScene(route.latlon[i - 2], route.latlon[i - 1]), PlotScene(route.latlon[i], route.latlon[i + 1]), Sky, 2, true);
            }
            Paint(Centered(point, 34, 34), Canvas, 17);
            Paint(Centered(point, 30, 30), Sky, 15);
            Paint(new Rect(point.x - 10, point.y - 6, 13, 12), Canvas, 2);
            Paint(new Rect(point.x + 4, point.y - 3, 6, 9), Canvas, 2);
            // 화물차를 누르면 오른쪽에 홍달 화물 휴대폰 (SagajeongDispatchRolePhones.cs). 보기만 한다.
            if (expansionView && HitMapMarker(Centered(point, 34, 34))) OpenRolePhone(FocusKind.Cargo, "cargo");
            // 세워 둔 동안에는 점포 이름표가 "검수 대기"를 말해 준다. 이름표를 겹치지 않는다.
            if (!LayerLabelsVisible || chain.WaitingInspection) return;
            var left = chain.TruckSecondsLeft;
            var line = entering ? $"센터에서 오는 중 · {left / 60}:{left % 60:00}" : $"도착까지 {left / 60}:{left % 60:00}";
            var plate = new Rect(point.x - 78, point.y - 60, 156, 36);
            Paint(plate, Sky, 8);
            Paint(new Rect(plate.x + 1.5f, plate.y + 1.5f, plate.width - 3, plate.height - 3), Canvas, 7);
            Text(new Rect(plate.x, plate.y + 2, plate.width, 17), chain.TruckName + " · " + chain.TruckLoad, 12, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(plate.x, plate.y + 17, plate.width, 16), line, 10, Sky, align: TextAnchor.MiddleCenter);
        }

        // 꺾은선(위도, 경도, 위도, 경도 …)의 처음부터 비율 t만큼 간 지점. x에 위도, y에 경도를 담아 돌려준다.
        private (double x, double y) AlongRoute(double[] latlon, float t)
        {
            var count = latlon.Length / 2;
            var lengths = new double[count];
            for (var i = 1; i < count; i++)
            {
                var dz = (latlon[i * 2] - latlon[i * 2 - 2]) * 111200;
                var dx = (latlon[i * 2 + 1] - latlon[i * 2 - 1]) * longitudeScale;
                lengths[i] = lengths[i - 1] + Math.Sqrt(dx * dx + dz * dz);
            }
            var target = lengths[count - 1] * Mathf.Clamp01(t);
            for (var i = 1; i < count; i++)
            {
                if (lengths[i] < target) continue;
                var span = lengths[i] - lengths[i - 1];
                var f = span <= 0 ? 0 : (target - lengths[i - 1]) / span;
                return (latlon[i * 2 - 2] + (latlon[i * 2] - latlon[i * 2 - 2]) * f,
                    latlon[i * 2 - 1] + (latlon[i * 2 + 1] - latlon[i * 2 - 1]) * f);
            }
            return (latlon[count * 2 - 2], latlon[count * 2 - 1]);
        }

        // ── 사업 층 ──────────────────────────────────────────────────────────────

        private void DrawBusinessLayers()
        {
            Paint(BusinessLayerPanel, Panel, 10);
            Text(new Rect(35, 396, 100, 18), "사업 층", 12, Mint, true);
            Text(new Rect(110, 396, 88, 18), "눌러서 켜고 끔", 10, Soft, align: TextAnchor.MiddleRight);
            for (var layer = 0; layer < LayerTitles.Length; layer++)
            {
                var row = new Rect(30, 419 + layer * 23, 172, 21);
                var color = BusinessColor(layer);
                var dot = new Rect(row.x + 5, row.y + 6, 9, 9);
                Paint(dot, color, 5);
                if (!layerOn[layer]) Paint(new Rect(dot.x + 2, dot.y + 2, 5, 5), Panel, 3);
                Text(new Rect(row.x + 21, row.y, 62, 21), LayerTitles[layer], 12, layerOn[layer] ? Paper : Soft, true);
                Text(new Rect(row.x + 82, row.y, 90, 21), LayerStatus(layer), 10, layerOn[layer] ? color : Soft, align: TextAnchor.MiddleRight);
                if (Hit(row)) layerOn[layer] = !layerOn[layer];
            }
        }

        private string LayerStatus(int layer)
        {
            if (state == null) return "";
            switch (layer)
            {
                case BusinessFood:
                    return $"대기 {state.orders.Count(order => order.stage == "Waiting")} · 배달 {state.orders.Count(order => order.stage is "ToPickup" or "AtPickup" or "ToDropoff")}";
                case BusinessRestaurant:
                    return $"매장 {state.stores.Length}곳";
                case BusinessFranchise:
                    return engine.Pack == null ? "자료 없음" : !chain.Started ? "발주 필요" : chain.WaitingInspection ? "검수 대기" :
                        chain.ActionKey == "produce" ? "제조 대기" : chain.Finished ? "라떼 나감" : "공급 중";
                default:
                    return engine.Pack == null ? "자료 없음" : chain.TruckMoving ?
                        $"화물 {chain.TruckSecondsLeft / 60}:{chain.TruckSecondsLeft % 60:00}" : chain.WaitingInspection ? "하차" : "대기";
            }
        }

        private string BusinessState(int business, out Color accent)
        {
            accent = Mint;
            if (business <= BusinessRestaurant) return "운영 중";
            if (business == BusinessFranchise || business == BusinessCargo)
            {
                if (engine?.Pack == null) { accent = Soft; return "자료 없음"; }
                var running = business == BusinessFranchise ? chain.Started
                    : ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips.Count > 0;
                if (running) return "운영 중";
                accent = Gold;
                return "새로 열림";
            }
            accent = Soft;
            return business == BusinessCenter ? "수도권 지도에서" : "잠김";
        }

        // ── 인계 사슬 ─────────────────────────────────────────────────────────────

        private void DrawChainStrip()
        {
            if (!ChainStripVisible) return;
            var strip = ChainStrip;
            Paint(strip, Rim, 10);
            Paint(new Rect(strip.x + 1, strip.y + 1, strip.width - 2, strip.height - 2), Canvas, 9);
            Text(new Rect(strip.x + 12, strip.y + 4, 330, 16), chain.Title, 10, Soft);
            Text(new Rect(strip.x + 330, strip.y + 4, strip.width - 342, 16), chain.Headline, 10,
                string.IsNullOrEmpty(chain.ActionKey) ? Soft : Gold, align: TextAnchor.MiddleRight);
            var cell = (strip.width - 16) / Math.Max(1, chain.Steps.Count);
            for (var index = 0; index < chain.Steps.Count; index++)
            {
                var step = chain.Steps[index];
                var x = strip.x + 8 + index * cell;
                var now = step.State == ChainStepStates.Now;
                var done = step.State == ChainStepStates.Done;
                var accent = done ? Mint : now ? Gold : Rim;
                if (now) Paint(new Rect(x + 1, strip.y + 24, cell - 2, 50), Panel, 5);
                Paint(new Rect(x + 1, strip.y + 23, cell - 2, 3), accent, 1);
                Text(new Rect(x + 6, strip.y + 29, cell - 12, 16), step.Title, 12, done ? Mint : now ? Gold : Soft, true);
                Text(new Rect(x + 6, strip.y + 45, cell - 12, 28), step.Line, 10, done || now ? Paper : Soft, wrap: true);
                // 「센터 출고」 · 「화물 운송」 칸을 누르면 그 사람의 휴대폰(홍달 창고 · 홍달 화물). 보기만 한다 (SagajeongDispatchRolePhones.cs)
                if ((index == 1 || index == 2) && Hit(new Rect(x, strip.y + 22, cell, strip.height - 24)))
                    OpenRolePhone(index == 1 ? FocusKind.Center : FocusKind.Cargo, index == 1 ? "center" : "cargo");
            }
            if ((chain.ActionKey == "inspect" || chain.ActionKey == "produce") && Hit(strip)) GoToPlace(engine.Pack.network.store.placeId);
        }

        // ── 건물 카드의 역할 부분 ──────────────────────────────────────────────────

        private void DrawRoleSection(BuildingRole role, float x, float y, float width)
        {
            var business = RoleBusiness(role);
            var color = BusinessColor(business);
            Paint(new Rect(x, y, width, 44), Tile, 7);
            Text(new Rect(x + 10, y + 4, width - 78, 19), BusinessTitles[business] + " · " + role.title, 13, color, true);
            var badge = new Rect(x + width - 62, y + 6, 52, 16);
            Paint(badge, Mint, 8);
            Text(badge, "내 운영", 10, Canvas, true, TextAnchor.MiddleCenter);
            Text(new Rect(x + 10, y + 23, width - 20, 17), "가상 운영 · 실제 건물의 쓰임과 무관", 10, Soft);
            Text(new Rect(x, y + 52, width, 16), "지금 여기서 하는 일", 10, Soft);
            if (business == BusinessFranchise && engine.Pack != null)
            {
                // 매장 운영 창이 있는 화면에서는 명령을 왼쪽 창으로 모으고 여기에는 문 하나만 둔다 (r215, 계획 D3)
                if (StoreDeskReady && StoreRole(role.placeId) != null) DrawStoreDoor(role, x, y + 70, width);
                else DrawCafeRole(x, y + 70, width);
            }
            else if (business == BusinessRestaurant) DrawRestaurantRole(role, x, y + 70, width);
            else
                Text(new Rect(x, y + 70, width, 70),
                    "창고·화물 사업의 거점입니다. 첫 사슬에서는 물류센터에서 점포로 바로 가므로 여기를 거치지 않습니다.", 12, Paper, wrap: true);
        }

        private void FactRow(float x, float y, float width, string title, string value, Color color)
        {
            Text(new Rect(x, y, 70, 18), title, 11, Soft);
            Text(new Rect(x + 62, y, width - 62, 18), value, 12, color, true, TextAnchor.MiddleRight);
        }

        private void DrawRestaurantRole(BuildingRole role, float x, float y, float width)
        {
            var order = state?.orders?.FirstOrDefault(item => item.storeId == role.storeId);
            FactRow(x, y, width, "주문", order == null ? "없음" : order.menu, Paper);
            FactRow(x, y + 19, width, "단계", order == null ? "—" : StageLabel(order.stage), order?.stage == "Waiting" ? Gold : Mint);
            FactRow(x, y + 38, width, "기사", order == null || string.IsNullOrEmpty(order.driverId) ? "미배정" : "기사 " + Letter(order.driverId), Paper);
            var known = state?.stores?.Any(store => store.id == role.storeId) == true;
            if (ExpansionButton(new Rect(x, y + 70, width, 30), "이 매장의 주문 보기", known, Mint))
            {
                showDetails = true;
                actorFocus = (FocusKind.Store, role.storeId);
            }
        }

        private void DrawCafeRole(float x, float y, float width)
        {
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            var supply = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply);
            if (store == null || supply == null) return;
            var receipt = store.Receipts.LastOrDefault();
            FactRow(x, y, width, supply.ItemName, $"가용 {store.AvailableContent:N0}{store.ContentUnit} · 격리 {store.QuarantineContent:N0}{store.ContentUnit}",
                store.AvailableContent > 0 ? Mint : Gold);
            FactRow(x, y + 19, width, store.Menu + " 주문", store.WaitingCups > 0 ? $"{store.WaitingCups:0}잔 대기" :
                store.Productions.Count > 0 ? $"{store.Productions.Sum(item => item.Cups):0}잔 나감" : "없음", store.WaitingCups > 0 ? Gold : Paper);
            FactRow(x, y + 38, width, "들어온 차", chain.WaitingInspection ? $"{chain.TruckName} · 하차 {receipt?.Unloaded:0}{supply.Unit}" :
                chain.TruckMoving ? $"{chain.TruckName} · {chain.TruckSecondsLeft / 60}:{chain.TruckSecondsLeft % 60:00} 뒤" : "없음",
                chain.WaitingInspection ? Gold : chain.TruckMoving ? Sky : Soft);

            // 같은 일을 보는 세 입장
            var stanceTitles = new[] { "점주", "화물 기사", "본부" };
            for (var index = 0; index < stanceTitles.Length; index++)
            {
                var tab = new Rect(x + index * 80, y + 64, 76, 22);
                Paint(tab, businessStance == index ? Gold : Tile, 6);
                Text(tab, stanceTitles[index], 11, businessStance == index ? Canvas : Paper, true, TextAnchor.MiddleCenter);
                if (Hit(tab)) businessStance = index;
            }
            Paint(new Rect(x, y + 91, width, 50), Tile, 7);
            Text(new Rect(x + 9, y + 94, width - 18, 44), StanceText(businessStance, supply, store, receipt), 11, Paper, wrap: true);

            var actionY = y + 149;
            if (chain.ActionKey == "order")
            {
                if (orderQuantity <= 0) orderQuantity = (int)supply.Recommended;
                Stepper(new Rect(x, actionY, width, 24), "수량 (" + supply.Unit + ")", ref orderQuantity, (int)supply.Minimum, (int)supply.Maximum);
                var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.FranchiseSupply, Action = "order" }
                    .With("quantity", orderQuantity));
                Text(new Rect(x, actionY + 26, width, 16), preview.CanConfirm ? "추천 " + supply.Recommended.ToString("0") + supply.Unit +
                    " · 한 잔에 " + store.ContentPerCup.ToString("0") + store.ContentUnit : preview.Message, 10, preview.CanConfirm ? Soft : Red);
                if (ExpansionButton(new Rect(x, actionY + 45, width, 32), preview.CanConfirm ? "보충 발주 · " + preview.Message : "보충 발주",
                        preview.CanConfirm, Mint))
                    SendBusiness(BusinessIds.FranchiseSupply, "order", command => command.With("quantity", orderQuantity));
            }
            else if (chain.ActionKey == "inspect" && receipt != null)
            {
                if (inspectReceipt != receipt.Id)
                {
                    inspectReceipt = receipt.Id;
                    inspectAccept = (int)receipt.SuggestAccept;
                    inspectQuarantine = (int)receipt.SuggestQuarantine;
                    inspectReject = 0;
                }
                var unloaded = (int)receipt.Unloaded;
                Stepper(new Rect(x, actionY, 76, 24), "인수", ref inspectAccept, 0, unloaded);
                Stepper(new Rect(x + 81, actionY, 76, 24), "격리", ref inspectQuarantine, 0, unloaded);
                Stepper(new Rect(x + 162, actionY, 76, 24), "거절", ref inspectReject, 0, unloaded);
                var preview = engine.BusinessPreview(InspectCommand(new GameCommand { OsId = BusinessIds.StoreOperations, Action = "inspect" }));
                Text(new Rect(x, actionY + 26, width, 16), preview.CanConfirm ? "추천: " + receipt.SuggestReason : preview.Message, 10,
                    preview.CanConfirm ? Soft : Red);
                if (ExpansionButton(new Rect(x, actionY + 45, width, 32), "검수 확정", preview.CanConfirm, Mint))
                    SendBusiness(BusinessIds.StoreOperations, "inspect", command => InspectCommand(command));
            }
            else if (chain.ActionKey == "produce")
            {
                var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.StoreOperations, Action = "produce" }.With("cups", 1m));
                Text(new Rect(x, actionY + 6, width, 34), preview.Message, 11, preview.CanConfirm ? Soft : Red, wrap: true);
                if (ExpansionButton(new Rect(x, actionY + 45, width, 32), store.Menu + " 1잔 제조", preview.CanConfirm, Mint))
                    SendBusiness(BusinessIds.StoreOperations, "produce", command => command.With("cups", 1m));
            }
            else
                Text(new Rect(x, actionY + 6, width, 70), businessNotice ?? chain.Headline, 11, Soft, wrap: true);
        }

        private GameCommand InspectCommand(GameCommand command) => command.With("accept", inspectAccept)
            .With("quarantine", inspectQuarantine).With("reject", inspectReject);

        private void Stepper(Rect rect, string label, ref int value, int minimum, int maximum)
        {
            Paint(rect, Tile, 6);
            var minus = new Rect(rect.x + 2, rect.y + 2, 20, rect.height - 4);
            var plus = new Rect(rect.xMax - 22, rect.y + 2, 20, rect.height - 4);
            Text(minus, "−", 14, value > minimum ? Paper : Rim, true, TextAnchor.MiddleCenter);
            Text(plus, "+", 14, value < maximum ? Paper : Rim, true, TextAnchor.MiddleCenter);
            Text(new Rect(minus.xMax, rect.y, plus.x - minus.xMax, rect.height), label + " " + value, 11, Paper, true, TextAnchor.MiddleCenter);
            if (Hit(minus) && value > minimum) value--;
            if (Hit(plus) && value < maximum) value++;
        }

        private string StanceText(int stance, FranchiseSupplySnapshot supply, StoreOperationsSnapshot store, StoreReceiptState receipt)
        {
            var unit = supply.Unit;
            var order = supply.Orders.LastOrDefault();
            var quantity = order == null ? supply.Recommended : order.AcceptedQuantity;
            var phase = !chain.Started ? 0 : chain.WaitingInspection ? 3 : chain.TruckMoving ? 2 :
                receipt == null ? 1 : chain.ActionKey == "produce" ? 4 : 5;
            var inspected = receipt == null ? "" : $"{receipt.Accepted:0}{unit} 인수, {receipt.Quarantined:0}{unit} 격리";
            return stance switch
            {
                0 => phase switch
                {
                    0 => $"{supply.ItemName}가 떨어져 {store.Menu} 주문이 밀려 있다. 보충 발주부터 내야 한다.",
                    1 => "발주는 받아들여졌다. 센터에서 피킹과 상차가 끝나길 기다린다.",
                    2 => "화물차가 오고 있다. 도착하면 수량과 상태부터 확인한다.",
                    3 => $"방금 온 {receipt?.Unloaded:0}{unit}을 확인한다. 쓸 수 있는 것만 인수하고 이상한 것은 따로 둔다.",
                    4 => $"인수한 것으로 밀린 {store.Menu}를 만든다. 한 잔에 {store.ContentPerCup:0}{store.ContentUnit}가 든다.",
                    _ => $"{store.Menu}가 나갔다. 남은 {store.AvailableContent:N0}{store.ContentUnit}로 다음 주문을 받는다.",
                },
                1 => phase switch
                {
                    0 => "아직 실을 화물이 없다.",
                    1 => "센터에서 상차를 기다린다. 실물 수량과 보관 상태를 확인하고 출발한다.",
                    2 => $"{supply.ItemName} {quantity:0}{unit}을 싣고 점포로 간다. 도착하면 하차 수량을 적는다.",
                    3 => $"{receipt?.Unloaded:0}{unit}을 내렸다. 점주의 확인이 끝나야 인수 완료가 된다.",
                    _ => "인수 완료를 받았다. 이 운송은 끝났다.",
                },
                _ => phase switch
                {
                    0 => $"{store.StoreName}의 발주를 기다린다. 공급안은 {supply.ItemName} {supply.Minimum:0}~{supply.Maximum:0}{unit}이다.",
                    1 => $"발주 {quantity:0}{unit}을 모두 수락했다. 센터의 출고 후보로 넘겼다.",
                    2 => $"출고 {quantity:0}{unit}이 화물로 넘어갔다. 점포 검수 결과를 기다린다.",
                    3 => "하차까지 확인했다. 점포가 인수한 만큼 공급대금이 정해진다.",
                    _ => $"{inspected}로 받았다. 격리분은 공급대금 조정의 근거다.",
                },
            };
        }
    }
}
