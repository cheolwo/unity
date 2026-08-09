# Urban Logistics Center Primitive Vertical Slice

도심 물류센터의 공유 World, 운송자 Role Perspective, 작업자 NPC와 물류 거점 사이를 이동하는 Truck projection을 한 Scene에 배선하는 sample이다.

## 포함 범위

- 입고 Dock, 분류 Zone과 출고 Dock primitive
- `transport:71`, pickup·dropoff stable-ID Role View socket
- `vehicle-gate`, `loading-bay`, `vehicle-exit` semantic waypoint
- `npc:transport-driver.71` 운송자 NPC와 NavMeshAgent
- `network.logistics-center` → `network.warehouse` 운송 회랑 waypoint
- `truck-projection:cargo:transport-71` TruckView와 cargo VisualRoot
- Role Perspective, NPC movement와 cargo handoff simulated API client
- VContainer composition root
- primitive Scene Builder와 저장 후 wiring validator

먼저 `Zone NPC Movement Sockets` sample을 import한 뒤 이 sample을 import한다. Scene Builder 실행 후 Unity Navigation 도구로 Ground NavMesh를 bake해야 NPC가 실제로 이동한다. 현재 builder는 프로젝트별 Navigation package와 bake 설정을 임의로 변경하지 않는다.

```text
Ssalddel/Samples/Create Urban Logistics Center Primitive Scene
Ssalddel/Samples/Validate Urban Logistics Center Primitive Scene
```

simulation fixture는 operational data가 아니다. 실제 연결 시 LifetimeScope의 simulated API client를 UnityWebRequest adapter로 교체한다. 서버가 `InTransit`으로 판정한 화물 인계만 Truck movement로 투영하며, 도착 후에는 canonical handoff를 다시 조회해 TruckView를 숨긴다. NPC나 트럭의 Unity 도착은 animation만 실행하며 상차·하차·입고 완료 endpoint를 호출하지 않는다.
