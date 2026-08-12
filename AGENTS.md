# Ssalddel Unity 작업 지침

## Unity 자동화 우선순위

- 이 프로젝트에는 Unity CLI와 `com.unity.pipeline` 패키지가 설치되어 있다. Unity 관련 작업은 두 도구를 함께 활용한다.
- **Unity Pipeline/MCP 우선**: 연결된 Editor가 있을 때 Scene 계층, Game View, Console, 선택한 GameObject, 실행 중 상태처럼 Editor가 가진 사실을 확인하거나 변경할 때 사용한다.
- **Unity CLI 병행**: `unity run`은 재현 가능한 Scene 생성·asset import·batch 작업에, `unity test`는 EditMode/PlayMode 결과 XML을 남기는 자동 검증에 사용한다.
- Pipeline 연결을 사용할 수 없거나 Editor가 열려 있지 않으면 CLI batch 검증으로 대체하고, 최종 보고에 Pipeline 미사용 사유와 미검증 범위를 적는다.
- 화면·Scene 변경은 가능하면 Pipeline에서 Game View와 Console을 확인한 뒤, CLI EditMode와 PlayMode 테스트로 다시 검증한다.

## P2 World Map 검증 기준

- 공개 지도 API는 읽기 전용이며 실패를 fixture 데이터로 숨기지 않는다.
- Pipeline으로 실제 Scene을 볼 때는 `WorldBootstrapScene`의 상태 패널, marker 수, marker 선택 상세 패널, Console 오류를 확인한다.
- API 주소와 WebApp 상세 페이지 주소는 별도 설정으로 유지한다. 개인 위치, 원장 원본, 신청·운송 실행 데이터를 공개 marker에 넣지 않는다.

## 한국어 중심 용어와 작업 보고

- 문서, README, 사람이 읽는 주석, 진행·완료 보고와 검증 설명은 한국어를 기본으로 한다.
- 프로젝트 개념은 `구성 대장`, `업무 흐름`, `배치 객체`, `고유 식별자`, `데이터 연결`, `상태 사본`, `관점별 조회 결과`, `시각 자산 대장`, `연결 지점`, `배치 기준점`, `배치 검증 기록`, `데이터 계보`, `인계`처럼 한국어를 먼저 쓴다.
- Unity, C#, GameObject, MonoBehaviour, Prefab, Scene, Game View, Play Mode, EditMode, HTTP, API, DTO, JSON, GUID 같은 기술 고유명은 영어를 유지한다.
- 기존 클래스명, 고유 식별자, API·직렬화 계약, 저장 값과 `.meta` GUID는 한국어화를 이유로 바꾸지 않는다.
- O0~O6은 단계 코드만 쓰지 않고 O5 `모판 실행 검증 완료`, O6 `실제 World 배치 검증 완료`처럼 의미를 함께 적는다.
- 서버가 최종 사실과 업무 권한을 가진다. Unity 표현과 애니메이션은 업무 완료의 근거가 아니며 실제 완료는 서버 기준 원장 재조회로 확인한다고 설명한다.
- 상세 단일 기준은 Hongdal 저장소의 `docs/AI/UnityKoreanTerminologyGuide.md`를 따른다.

## 작업 트리와 Git

- `Assets/Ssalddel/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/EditorBuildSettings.asset` 이외의 기존 변경은 다른 작업일 수 있으므로 되돌리거나 함께 stage하지 않는다.
- commit과 push는 사용자의 명시 요청이 있을 때만 수행한다.
- Scene·prefab·material·camera·UI 변경으로 Game View가 달라지면 Pipeline에서 최종 Game View PNG를 다시 캡처한다. 대표 PNG는 `Documentation/Changes/<날짜>-<주제>/`에 두고 관련 코드·Scene·변경 기록과 같은 맥락의 커밋에 포함한다. 중간 캡처와 test output은 `artifacts/`에만 두고 commit하지 않는다.
- Scene View는 배선·배치 설명을 위한 보조 증거이며 최종 Game View를 대신하지 않는다. Play Mode를 실행할 수 없으면 이유와 대체 검증을 변경 기록에 명시한다.
