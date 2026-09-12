# Synty 13팩 자산 기능 대장

> Unity 대장에서 자동 생성된다. 원본 Prefab 경로는 공개 문서에 기록하지 않는다.

- 대장 revision: `synty-all-asset-modules.r2`
- 전체 Prefab: `4211`
- 기능군: `12`
- 사람이 읽는 분류: `실외 표현 / 실내 표현 / 공통 표현`
- 분류 순서: `범위 → 기능군 → 세부 기능군 → 자산 종류 → 자산 계열 → 실제 Prefab`
- 명시적 보류: `527`
- Construction은 AreaSet이 아니라 건설·복구 상태를 공유하는 표현 계층이다.
- Generic은 공유 기반, Starter는 prototype fallback으로만 사용한다.
- 애니메이션 3팩은 이 대장에서는 sample Prefab만 집계하며, 실제 Clip은 별도 애니메이션 원천 프로필에서 관리한다.
- Alpine·Dungeon·Dwarven 팩은 새 Area가 아니라 기존 WI/H가 선택할 수 있는 검토 후보이다.

## 팩별 수량

| 팩 | Prefab | 생산 후보 | 검토/보류 |
| --- | ---: | ---: | ---: |
| alpine-mountain | 125 | 0 | 125 |
| animation-base-locomotion | 4 | 0 | 4 |
| animation-emotes-and-taunts | 2 | 0 | 2 |
| animation-sword-combat | 1 | 0 | 1 |
| city | 335 | 0 | 335 |
| construction | 584 | 0 | 584 |
| dungeon-realms | 1128 | 0 | 1128 |
| dwarven-dungeon-map | 52 | 0 | 52 |
| farm | 498 | 0 | 498 |
| generic | 495 | 0 | 495 |
| nature | 227 | 0 | 227 |
| starter | 58 | 0 | 58 |
| town | 702 | 0 | 702 |

## 기능군별 수량

| 표현 범위 | 기능군 | Stable Code | Prefab |
| --- | --- | --- | ---: |
| 실외 표현 | 월드 지면 | `world-surface` | 325 |
| 실외 표현 | 자연 식생 | `nature-vegetation` | 741 |
| 실외 표현 | 실외 구조물 | `exterior-structure` | 654 |
| 실외 표현 | 실외 기능 소품 | `exterior-functional-prop` | 1613 |
| 실외 표현 | 도로·통행망 | `world-network` | 258 |
| 실외 표현 | 영역 전이 | `world-transition` | 48 |
| 실내 표현 | 실내 구조 | `interior-structure` | 29 |
| 실내 표현 | 실내 설비 | `interior-fixture` | 186 |
| 실내 표현 | 실내 소품 | `interior-loose-item` | 229 |
| 공통 표현 | 건설·복구 표현 | `construction-state` | 584 |
| 공통 표현 | 인물·차량·도구 | `actor-vehicle-tool` | 484 |
| 공통 표현 | 세계 피드백 효과 | `world-feedback-fx` | 195 |

대장 등록은 Scene 배치 승인이나 E 승격을 뜻하지 않는다. PlayableLoop 슬롯이 기능 모듈과 자산 계열을 함께 요구하고 실제 배치 검증을 통과해야 한다.
