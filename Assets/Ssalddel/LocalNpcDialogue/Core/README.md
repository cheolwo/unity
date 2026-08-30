# 로컬 NPC 대화 비권위 Core

D-337 `local-npc-dialogue-foundation.design.r1`의 선택형 지원 기반이다. Unity·Simulation·HTTP·파일·도구 실행·저장·대화 cache 의존성이 없다. `Enabled=false`가 기본이며 실제 모델 설치·추론·NPC 화면 연결은 별도다.

## 읽기 사본과 호출

1. 신뢰된 게임 조회/Projection에서 NPC·플레이어·Session·WorldRevision·EvidenceRevision이 결속된 불변 `대화Context`를 만든다. 사용자 질의에서 근거 사실을 만들면 안 된다. 이 Core는 caller가 넘긴 허용 사본을 암호학적으로 인증하는 보안 경계가 아니다.
2. `SetContext`로 현재 사본을 등록한다. `GenerateAsync(new 대화Request(context, query, fallback), token)`는 등록된 동일 사본만 사용한다. Session/세계/자료 변경·로그아웃·NPC 대화 종료 시 `SetContext` 또는 `InvalidateContext`를 호출해야 한다. 새 판본을 자동으로 알아내지 않는다. 같은 NPC·플레이어·Session의 세계/자료 판본 역행은 거부한다. 의도적 Replay는 별도 Session 또는 명시적인 문맥 해제 후 등록한다.
3. NPC에게 알려진 자료, 동일 플레이어/Session/WorldRevision/자료 판본, 공개 범위부터 제한한 후 키워드 일치 점수·근거 식별자 순으로 최대 4개를 고른다. `Keyword`는 임베딩이나 의미 검색이 아니다. 충돌·중복 식별자는 제외한다.
4. 결과의 `Status`로 기본 대사와 생성을 구별한다. `IsAuthoritative=false`이며 근거 식별자·판본·출처를 유지한다. 정확한 자원·가격·보상은 권위 화면에서 따로 읽는다. 비동기 결과를 화면에 적용할 때도 현재 문맥과 같은지 소비자가 확인해야 한다.

## 상한과 실패

- 등록 NPC 64, 근거 후보 128, 근거당 1024자, NPC 허용 목록 64, 질의 512자, 검색어 16개, 결과 근거 4개.
- 프롬프트 기본 8192자(최대16384), 출력 기본256자(최대2048), 동시 호출 기본1(최대4), 제한 시간 기본15초(최대2분). 입력 상한 초과는 거부하고 출력은 자른다.
- 모델 연결부는 로컬 파일 승인·가용 여부와 로컬 추론을 책임진다. 연결부가 비동기 계약을 어기고 호출 진입을 동기적으로 막으면 Core 제한 시간으로 그 동기 정지를 중단할 수 없다.
- 비활성·미설치·원격거부·무근거·입력오류·사용중·취소·제한시간·오래된응답·오류는 명시적 기본 대사를 반환한다. 예외 상세는 대사에 노출하지 않는다.
- 같은 NPC의 중복 요청은 `Busy`다. 취소/제한시간/문맥 교체 뒤 늦은 결과는 폐기하며, 실제 모델 Task가 종료되기 전에는 점유를 해제하지 않는다. 취소를 영구 무시하는 모델은 새 요청을 막으므로 연결부 수명주기 복구가 필요하다.
- 프롬프트 자료는 이스케이프된 인용 문자열이다. 이것만으로 모델의 프롬프트 주입·환각을 완전히 막는다고 주장하지 않는다. Core에 도구나 권위 쓰기 경로가 없다는 경계는 유지된다.

## 검증

`Tests` NUnit은 Unity EditMode에서 사용할 수 있는 소스이며 standalone .NET 실행 결과를 Unity 시험으로 간주하지 않는다. `LocalNpcDialogue.Core.Compatibility.csproj`는 Core의 C#9/netstandard2.1 컴파일을 따로 확인한다. 시험 실행 시 `BaseIntermediateOutputPath`와 `OutputPath`를 `artifacts/local/validation/` 아래로 지정해 Assets에 생성 C#을 남기지 않는다. 실제 vendor LlamaLib 연결부 검증은 별도 시험 프로젝트 책임이다.
