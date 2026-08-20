# Synty Nature·Farm·Town·City·Construction 전수 기술 대장 요약

- 스캔 규칙: `synty-pack-inventory.v2`
- 원본 묶음 hash: `fa8202612ce842530dafd60f2e519b47aba50d446b9674a9ed302e0ae0ee1654`
- 전체 Prefab: 2346개
- 의미 자산군: 1499개
- 자동 분류: 2345개
- 사람 검토 대기: 1개
- 이 문서는 수량과 배치 원칙만 공개하며 유료 원본 파일명·경로·GUID는 기록하지 않는다.
- 파일 경로의 `Synty3Pack` 이름은 기존 Unity 문서 GUID를 보존하기 위한 호환 경로다.

## 팩·정규화 분류별 수량

| 팩 | 분류 | 수량 |
| --- | --- | ---: |
| city | Buildings | 76 |
| city | Characters | 9 |
| city | Environments | 65 |
| city | FX | 2 |
| city | Props | 174 |
| city | Vehicles | 9 |
| construction | Buildings | 74 |
| construction | Characters | 44 |
| construction | Environments | 36 |
| construction | Generic | 19 |
| construction | Items | 49 |
| construction | Props | 300 |
| construction | Tools | 39 |
| construction | Vehicles | 23 |
| farm | Buildings | 17 |
| farm | Characters | 14 |
| farm | Environments | 67 |
| farm | FX | 11 |
| farm | Generic | 39 |
| farm | Plants | 173 |
| farm | Props | 166 |
| farm | Vehicles | 11 |
| nature | FX | 24 |
| nature | ManualReview | 1 |
| nature | Plants | 42 |
| nature | Props | 31 |
| nature | Rocks | 30 |
| nature | Terrain | 33 |
| nature | Trees | 66 |
| town | Buildings | 143 |
| town | Characters | 9 |
| town | Environments | 97 |
| town | Generic | 33 |
| town | Items | 72 |
| town | Props | 340 |
| town | Vehicles | 8 |

## 팩·주 활용 트랙별 수량

| 팩 | 활용 트랙 | Prefab | 자산군 |
| --- | --- | ---: | ---: |
| city | actor | 9 | 9 |
| city | functional-prop | 174 | 115 |
| city | spatial-base | 141 | 86 |
| city | state-fx | 2 | 2 |
| city | vehicle | 9 | 9 |
| construction | actor | 44 | 37 |
| construction | functional-prop | 319 | 181 |
| construction | spatial-base | 110 | 53 |
| construction | tool-or-item | 88 | 60 |
| construction | vehicle | 23 | 18 |
| farm | actor | 14 | 12 |
| farm | functional-prop | 205 | 123 |
| farm | spatial-base | 257 | 207 |
| farm | state-fx | 11 | 10 |
| farm | vehicle | 11 | 11 |
| nature | functional-prop | 31 | 21 |
| nature | manual-review | 1 | 1 |
| nature | spatial-base | 171 | 85 |
| nature | state-fx | 24 | 24 |
| town | actor | 9 | 7 |
| town | functional-prop | 373 | 221 |
| town | spatial-base | 240 | 149 |
| town | tool-or-item | 72 | 50 |
| town | vehicle | 8 | 8 |

## 승격 경계

- 전수 기술 대장 등록은 월드 배치 승인을 뜻하지 않는다.
- 사람이 의미와 토지피복·경사·동선을 검토한 항목만 `VisualKey` 또는 `CompositionKey`로 승격한다.
- Character·Vehicle·Item·Tool·FX는 기술 대장에는 포함하지만 정적 경관 자동 배치에서 제외한다.
- 모든 항목은 `PresentationOnly`이며 공간 사실이나 Simulation 상태를 만들지 않는다.
