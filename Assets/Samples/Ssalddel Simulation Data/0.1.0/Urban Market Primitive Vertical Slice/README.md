# Urban Market Primitive Vertical Slice

이 sample은 실제 Unity project가 없는 현재 repository에서도 도심마트 Presentation 계약을 보존하기 위한 importable sample이다.

## 포함 범위

- `도심마트SceneController`
- `도심마트View`
- 상품진열대 3개
- 상품상자, 가격표, 재고 상태와 정보 키오스크 View socket
- 상품 선택 시 상세 정보 panel
- `Simulated도심마트조회UseCase`를 사용하는 명시적 simulation fixture
- 기존 공개 aggregate `GET api/v1/orderer/mart/products`용 operational ApiClient·Mapper·Repository·UseCase
- VContainer에서 simulation과 operational 구성을 명시적으로 선택하는 LifetimeScope
- primitive scene을 생성하고 Inspector reference를 연결하는 Editor builder
- `도심마트ManagerRuntime`의 summary·queue·shelf·task·source-plan·detail change set을 적용하는 관리자 View
- 관리자 shelf 선택을 World selection으로 되돌리고 30초 refresh에서 last-success를 유지하는 Controller
- 공개 상품 compatibility Scene과 분리된 `UrbanMarketManagerPrimitive` Editor builder
- 대표 NPC 선택으로 여는 7장 `ConceptCardView` deck과 Concept·Status·Reason·Action visual skin
- Synty 원본 prefab을 수정하지 않고 `VisualRoot`·Mecanim Animator만 교체하는 asset 경계
- manager desk·대표 route waypoint·임시 NavMesh와 Animator parameter를 검증하는 Editor test

결제, 주문 Command와 외부 asset은 포함하지 않는다. operational 모드는 서버가 공개 가능하다고 판정한 상품·판매가·판매 가능 수량·재고 기준시각만 읽으며 내부 창고 재고, 주소, 연락처, 결제·계약 정보는 요청하지 않는다.

## 사용

1. Package Manager에서 `Ssalddel Simulation Data` package의 sample을 import한다.
2. Unity 메뉴에서 `Ssalddel/Samples/Create Urban Market Primitive Scene`을 실행한다.
3. 생성된 `Assets/Ssalddel/Scenes/UrbanMarketPrimitive.unity`를 연다.
4. PlayMode에서 진열대 3개, 상품·가격·재고·출처 표시와 상품 선택 panel을 확인한다.

관리자 surface sample은 Unity 메뉴 `Ssalddel/Samples/Create Urban Market Manager Primitive Scene`에서 별도 Scene으로 만든다. 이 Scene은 manager role이 승인된 `AuthorizedUserWorld` Simulation context, 두 진열대, 30초 summary·queue·task·SourcePlan·detail surface를 사용한다. 기존 공개 상품 Scene을 덮어쓰지 않는다.

관리자 Scene의 공동주택 대표를 선택하면 CC2가 만든 7장 카드 deck이 열린다. 카드 선택은 Presentation 선택 표시만 바꾸며 주문·계약·결제를 확정하지 않는다. 외부 캐릭터를 도입할 때는 `공동주택대표NpcView.VisualRoot` 아래 외형과 Humanoid Animator를 교체하고 stable ID·route·dialogue component는 wrapper에 유지한다.

CLI에서는 Unity project 경로를 지정하여 다음 Editor method를 실행할 수 있다.

```text
Unity -batchmode -quit -projectPath <UnityProject> \
  -executeMethod Ssalddel.Unity.Samples.UrbanMarket.Editor.도심마트PrimitiveSceneBuilder.CreateScene
```

실제 API를 연결할 때는 `도심마트LifetimeScope.ConfigureOperationalApi()` 또는 Inspector에서 operational 모드와 API origin을 지정한다. API 실패를 simulation fixture로 대체하지 않으며 DTO와 Repository를 View 또는 Controller에 전달하지 않는다. 상품 선택은 읽기 전용 상세 panel만 열고, 주문은 별도 확인 panel → server UseCase → canonical 재조회가 구현되기 전까지 실행하지 않는다.
