# 홍달 경영 시뮬레이션 (Unity)

**실제 서울 · 수도권 지도 위에서 동네 가게와 배달을 운영해 보는 경영 시뮬레이션 게임**입니다. 지금 개발하고 있는 판이라 화면과 기능은 자주 바뀝니다.

| 수도권 지도 | 동 디오라마(공개판) | 서울 25구 비교 |
| --- | --- | --- |
| [![수도권 전체를 한 화면에 본 지도](Documentation/readme/capital-map.png)](Documentation/readme/capital-map.png) | [![수원 행궁동을 공개 자료로 만든 입체 동네](Documentation/readme/public-dong-diorama.png)](Documentation/readme/public-dong-diorama.png) | [![서울 25개 구를 일반음식점 수로 칠한 지도](Documentation/readme/seoul-gu-compare.png)](Documentation/readme/seoul-gu-compare.png) |
| 수도권을 한 장으로 보고, 휠로 구 · 동까지 다가갑니다 | 칸을 두 번 누르면 그 동이 입체 모형(디오라마)으로 열립니다 | 공표 통계로 구를 칠하고, 구를 누르면 숫자 카드가 나옵니다 |

## 어떤 게임인가요?

- 플레이어는 **카페 · 음식점 같은 가게와 배달 기사, 물류**를 맡아 하루하루 운영합니다. 주문이 들어오면 기사를 보내고, 재료가 떨어지면 보충합니다.
- 지도는 **전국 → 수도권 → 구 → 타일(300 m 육각형 칸) → 동(입체 디오라마)** 순서로 가까워집니다.
- 지도에는 **실제 공개 자료**가 올라갑니다. 건물 · 길 · 지하철 승하차 · 날씨 · 버스 · 생활 시설(정류장 · 그늘막 · 화장실 …) · 구별 통계 같은 것들입니다.
- **게임 속 가게 · 기사 · 주문 · 돈은 모두 가상**입니다. 실제 판매 · 결제 · 배차와 연결되어 있지 않습니다.

## 처음 보는 분을 위한 낱말

| 낱말 | 뜻 |
| --- | --- |
| 디오라마 | 동네 하나를 건물 · 길 · 나무까지 입체로 만든 작은 모형 화면 |
| 동(행정동) | 주민센터 단위의 동네(예: 면목본동). 게임 지도의 가장 작은 지역 단위 |
| 타일 | 구 지도를 300 m 간격의 육각형 칸으로 나눈 것. 칸마다 건물 수 · 음식점 수 · 사람 수를 담습니다 |
| 굽기 | 공개 자료(건물 · 길 · 경계)를 게임이 바로 읽는 파일로 미리 만들어 두는 일 |
| 팩 | 게임이 읽는 자료 묶음 파일(`Resources/hongdal-data-*.json` 등). 게임은 원본 자료를 직접 읽지 않고 팩만 읽습니다 |
| 커널 | 주문 · 돈 · 재고 같은 게임 규칙을 화면과 따로 계산하는 부분(`WindowsGame/Kernel`) |
| 자료 창 · 입장 카드 | 지도에 무엇을 켤지 고르는 창. 「배달 기사」 「음식점 사장」처럼 입장을 고르면 그 사람에게 필요한 자료가 켜집니다 |

## 폴더 안내

| 위치 | 들어 있는 것 |
| --- | --- |
| `Assets/Ssalddel/WindowsGame/` | **지금 주력인 Windows판 게임.** `Runtime/` 화면 코드, `Kernel/` 게임 규칙, `Resources/` 자료 팩 · 그림, `Editor/` 빌드, `Tools~/` 자료 굽기 도구(Python), `Checks~/` 콘솔 검사 |
| `Assets/Ssalddel/MobileGame/` | 휴대폰(Android) 세로 화면판 |
| `Assets/Ssalddel/` 의 나머지 | 이전 연구 — 농장에서 도시까지 농산물이 흘러가는 3D 월드([연구 개요](Documentation/세계구축-연구-개요.md)) |
| `Documentation/Changes/` | 날짜별 변경 기록과 실제 화면 촬영 |
| `Packages/`, `ProjectSettings/` | Unity 패키지와 프로젝트 설정 |

## 직접 실행해 보기

1. **Unity 6000.5.6f1**을 Unity Hub로 설치합니다(Windows Build Support 포함).
2. 저장소를 받습니다. 그림 · 모델 파일은 Git LFS로 올라가 있으니 LFS를 켠 채로 받습니다.
   ```text
   git lfs install
   git clone https://github.com/cheolwo/unity.git
   ```
3. Unity Hub에서 받은 폴더를 프로젝트로 엽니다(처음 여는 데 시간이 걸립니다).
4. 메뉴 **Ssalddel › Hongdal Windows › Build Player**를 누르고 **빈 폴더**를 고르면 `HongdalSimulation.exe`가 만들어집니다. 그 파일을 실행하면 게임이 열립니다.
   - 명령줄로 만들 때: `Unity.exe -batchmode -quit -projectPath <저장소 폴더> -executeMethod Ssalddel.WindowsGame.Editor.WindowsGameBuild.Build -hongdalWindowsOutput <빈 폴더>\HongdalSimulation.exe`
   - 빌드 폴더 옆의 `kernel-checks.txt`에 게임 규칙 검사 결과가 적힙니다.
5. 진행은 `%USERPROFILE%\AppData\LocalLow\Ssalddel\홍달 경영 시뮬레이션\windows-game\`에 저장됩니다.

> **이 저장소에 없는 자료가 있습니다.** 일부 지도 자료는 이용 조건을 아직 확인하지 못했거나 외부 반출이 금지된 원본에서 나와서 공개 저장소에 올리지 않았습니다(구 지도의 세밀한 바탕, 지하철 노선 층, 예전 방식으로 구운 동 모형, 생활인구 · 보안등 등). 받은 그대로 빌드하면 그 부분은 비어 있거나 「자료 준비 전」으로 보입니다. 수도권 지도와 공개판 동 디오라마는 공개 자료만으로 만들어 그대로 동작합니다.

## 자료 출처

화면 아래 줄에도 출처를 표시합니다. 주요 출처는 다음과 같습니다.

- 길 · 건물(사가정 일대): © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors, ODbL
- 땅 덮개: © ESA WorldCover 2021, CC BY 4.0 · 지형: Copernicus DEM GLO-30
- 건물(공개판 디오라마): 국토교통부 GIS건물통합정보 · 길: 국토교통부 표준노드링크 — 공공데이터포털, 이용허락범위 제한 없음
- 행정동 경계: 통계청 SGIS · [vuski/admdongkor](https://github.com/vuski/admdongkor) (공공누리 제1유형 · CC BY 4.0)
- 지하철 승하차 · 버스 · 길 속도 · 생활 시설 · 구별 통계: 서울 열린데이터광장 · 공공데이터포털 각 자료(출처 표시)
- 한글 글꼴: SIL Open Font License · 타로 그림: 1909년 원본(Public Domain)
- 지도 아이콘: 이 프로젝트에서 직접 그림

## 더 읽어 보기

- [Windows판 개발 메모](Assets/Ssalddel/WindowsGame/README.md) — 화면 · 저장 · 빌드의 자세한 동작
- 기획 · 진행 문서는 함께 쓰는 [Mirror 저장소](https://github.com/cheolwo/mirror)의 `docs/AI/`에 있습니다
- [변경 기록](Documentation/Changes/README.md)
- [이전 월드 연구 개요](Documentation/세계구축-연구-개요.md) — 예전 첫 화면 설명(서버 구조 · 공간 설계 단계)

## 지금 상태

개발 중입니다. 숫자와 화면은 시험 값이 섞여 있고, 판본마다 달라질 수 있습니다. 게임 속 매장 · 기사 · 주문은 가상이며 실제 업체 · 사람과 관계없습니다.
