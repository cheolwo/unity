# Unity visual change evidence

Scene, prefab, material, camera, or UI changes that affect the rendered result
must include a final Game View PNG in the same contextual commit as the related
code and Scene files.

- Keep exploratory captures and test output under `artifacts/`.
- Keep only representative final PNG files under `<date>-<topic>/`.
- Prefer a Play Mode Game View. Record the limitation when only Edit Mode can
  be captured.
- Scene View images are supplemental and do not replace Game View evidence.
- Do not capture credentials, personal information, or operational private data.

## 최근 기록

- [2026-09-14 동북서울 30개 행정동 기초 디오라마](2026-09-14-administrative-dong-base-geometry/README.md): 역사 경계 후보의 건물·도로 기초 형상을 실제 Play Mode Game View 30개 전체 조망과 면목제3·8동 근접 화면으로 비교한 비공개 기준선. 주소·필지·출입구·이동·생활상 완결 증거는 아님
- [2026-09-13 사가정 네 OS 생명주기](2026-09-13-sagajeong-multi-os-lifecycle/README.md): 음식배달·화물·창고·마트 정상/회복 8개 표본, 음식점 아이콘 12개와 일반화 기준점 보간의 실제 Play Mode 검증
- [2026-09-13 중랑구 역세권 관찰 디오라마](2026-09-13-station-diorama-gallery/README.md): 사가정역·면목역·용마산역 1km 실제 Play Mode Game View와 역별 자료·권위 경계
- [2026-09-07 동네 관찰·운영 휴대폰](2026-09-07-observer-phone/README.md): 다섯 앱·기존 Session 입력/조회/저장 분리, 두 해상도 실제 입력·30분 관찰·저장 재진입 확인. 기록 증가에 따른 저장 지연은 남음
- [2026-08-27 LH 지도와 Nature 세계자산배치 분리](2026-08-27-world-map-asset-placement/README.md): 지면·셀과 실외·실내 객체 생명주기를 분리하고 실제 Nature 입력을 확인했으나 밤 대상 식별성과 Console 잠금 오류로 Game View E6·E7 미통과
- [2026-08-25 Hub 창고 정밀 배치](2026-08-25-hub-warehouse-precision-placement/README.md): 25×20m Synty 창고·7개 구역·NPC 업무 경로의 저장 Scene 구조(코드·시험 검증, Game View 미검증)
- [2026-08-24 Nature E7 검증 폐루프](2026-08-24-nature-e7-loop/README.md): 분리 슬롯·명시 저장·후퇴 복원을 canonical Scene에 연결한 코드와 시험 경계(Game View 미검증)
- [2026-08-23 Nature 생존 생활 거점](2026-08-23-nature-survival-homestead/README.md): 서버 없이 실행되는 도끼·벌목·통나무·오두막·해질녘 해골 조우 세로 조각(코드·시험 검증, Game View 미검증)
- [2026-08-15 서버 결과 기반 전술 분대 이동](2026-08-15-tactical-squad-movement/README.md): 6대6 Synty 분대의 선형·쐐기·종대 전환, 전술 전용 NavMesh 이동과 절차형 동작 검증
- [2026-08-14 자료 조사 기반 L2 스트리밍 창](2026-08-14-researched-l2-stream-window/README.md): 공식 엔진 원칙을 500m L2에 환산한 3×3 상세·5×5 활성·9×9 준비와 경계 선행 준비 검증
- [2026-08-14 시야 기반 동적 월드](2026-08-14-visibility-driven-world-streaming/README.md): 카메라 시야·예측에 따른 건물 프록시→Synty 상세 승격과 이동 안전 경계·진단 트리 검증
- [2026-08-14 1인칭 동적 공간 타일](2026-08-14-first-person-tile-streaming/README.md): 3×3 활성·5×5 준비 창과 실제 자료 대기 상태를 통합 Scene에서 검증
- [2026-08-14 SimulationWorldShell 통합](2026-08-14-simulation-world-integration/README.md): 농장 1인칭·전술 시점과 진부 입고 정보판을 하나의 Play Scene에 연결
