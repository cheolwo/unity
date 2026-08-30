# 선택형 로컬 NPC 대화 지원 — D337

기본값은 비활성이다. Scene에 자동 설치하거나 게임 원장/Save를 변경하지 않는다.
이 폴더는 설치된 모델 없이도 기본 대사로 돌아오는 코드 기반이며 실제 NPC UI 연결은 없다.

## 책임

- `Core`: 신뢰된 읽기 사본의 NPC/플레이어/Session/WorldRevision/자료 개정·공개범위를 제한한 뒤 키워드 검색한다. 잠긴 지식은 Hidden, 해당 NPC가 모르는 자료는 후보에서 제외한다. 출처와 고유 식별자는 결과에도 남긴다.
- `VendorAdapter`: LLMUnity 3.0.3의 `UndreamAI.LlamaLib.LLMService` 로컬 파일 생성자와 `CompletionAsync`를 직접 사용한다. URL/원격 클라이언트·도구 실행·다운로드·자동 모델 탐색은 제공하지 않는다. 대화 이력과 슬롯을 파일에 저장하지 않는다.
- `Tests`: 동결 Fixture와 Fake 모델 시험. 임베딩/의미 검색·실제 모델 추론·Unity 실행 증거가 아니다.

## 소비 순서

1. 기존 Simulation 조회 담당이 허용된 사실만 `대화Context`로 투영한다. UI 질문을 사실로 승격하지 않는다. 현재 실제 Session 조회 연결은 후속이다.
2. `new LLMUnity로컬ModelAdapter()` 및 `new 로컬Npc대화Service(model)`는 비활성이며 기본 대사를 반환한다.
3. 후속 모델 승인이 있을 때만 Adapter 생성자의 `enabled: true`, 서비스 `new 로컬대화Options(enabled: true)`를 사용한다. 기존 로컬 `.gguf` 경로를 `InitializeAsync`에 명시한다. UNC/원격 URL/네트워크 드라이브·reparse 경로를 거부한다. 기본 backend와 한국어 모델 품질/라이선스 검증은 별개다.
4. `SetContext(trustedProjection)` 후 같은 불변 사본으로 `대화Request`를 만들고 `GenerateAsync`를 호출한다. 자료 변경/Session 전환은 새 사본 등록, 창 닫기/Actor 해제는 `InvalidateContext(npcId)`와 취소 토큰을 사용한다.
5. 결과는 **plain text**로만 표시한다. rich text, HTML, 링크/명령 실행으로 해석하지 않는다. `IsAuthoritative=false`; 자원량·보상·가격·선택 가능 여부는 Core UI를 사용한다. 기본 대사도 생성 성공과 구분한다.
6. 읽기 사본 변경/취소/timeout은 즉시 기본 대사로 복귀하고 늦은 결과를 버린다. 취소를 무시하는 네이티브 작업이 실제 종료되기 전 새 작업을 겹쳐 실행하지 않는다. Adapter는 단일 슬롯이며 해제 시 `ShutdownAsync`를 기다린다. 프로세스 강제 종료는 제공하지 않는다.

## 제한

자료 128개/NPC 인지목록 64개, 질문512자, 근거 최대4개, 기본 prompt8192자/응답256자/동시1/timeout15초. 실제 Adapter는 prompt12000자/출력512자/최대128token 제한을 별도 적용한다. 모델과 keyword 검색은 데이터 안의 지시를 도구로 실행하지 않지만 환각/부정확한 대사를 완전히 방지하지 못한다.

## 설치 상태와 재개

고정 의존성: `https://github.com/undreamai/LLMUnity.git#9931a9ea46906b1f43adaf9498c05ccf227b5d0b` (`ai.undream.llm` 3.0.3, Apache-2.0). 실제 UPM 등록/해석 완료 및 Unity6000.5.6f1에서 Core·VendorAdapter·두 시험 어셈블리 로드를 확인했다. 초기 패키지 import/컴파일 목록 불일치는 정상 Editor 재시작 뒤 해소됐으며 임시 설치용 define 제외는 모두 제거했다. 사용자 승인 백업은 `_Recovery`에 보존했고 canonical Scene에 적용/저장하지 않았다.

backend `LlamaLib-v2.0.5.zip`은 공식 hash 검증 후 vendor 설치기가 StreamingAssets에 설치했고 progress=1과 완료 표식을 확인했다. 모델 파일이 아니며 GGUF 모델 다운로드·추론은 하지 않았다. 패키지 라이선스는 모델 사용권을 대신하지 않는다. 기존 Newtonsoft3.2.2와 lock의 interior.domain→simulation.contracts0.1.0 변경을 보존했다. 기본 비활성을 유지하며 실제 NPC/Session/UI 연결 및 모델 품질은 미검증이다.

## 재실행

전용 프로젝트는 .NET10 시험 실행기/C#9를 사용하고 bin/obj를 Assets 밖으로 보낸다. vendor API 컴파일은 실제 고정 소스를 소비하지만 Unity Editor 전체 컴파일을 대신하지 않는다.

```powershell
dotnet test Assets/Ssalddel/LocalNpcDialogue/Tests/LocalNpcDialogue.FixtureTests.csproj -p:BaseIntermediateOutputPath=C:/Users/user/ssalddel/artifacts/local/validation/local-npc-dialogue/core-obj/ -p:OutputPath=C:/Users/user/ssalddel/artifacts/local/validation/local-npc-dialogue/core-bin/ --logger 'trx;LogFileName=core-fixture.trx' --results-directory artifacts/local/validation/local-npc-dialogue
dotnet test Assets/Ssalddel/LocalNpcDialogue/VendorAdapter/Tests/VendorSmoke.csproj -p:VendorSourceRoot=C:/Users/user/source/repos/Hongdal/artifacts/local/vendor-inspection/LLMUnity-v3.0.3 -p:BaseIntermediateOutputPath=C:/Users/user/ssalddel/artifacts/local/validation/local-npc-dialogue/vendor-obj/ -p:BaseOutputPath=C:/Users/user/ssalddel/artifacts/local/validation/local-npc-dialogue/vendor-bin/ --logger 'trx;LogFileName=vendor-smoke.trx' --results-directory artifacts/local/validation/local-npc-dialogue
```

`VendorSourceRoot`는 위 commit을 확인한 checkout이어야 한다. vendor 파일은 고치지 않는다. 전체 결과/승인/외부 경로 소유는 Hongdal의 `docs/Reports/로컬Npc대화지원-D337-구현-2026-08-30.md`와 지원 작업 명세가 기록한다.
