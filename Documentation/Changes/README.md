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

- [2026-08-15 서버 결과 기반 전술 분대 이동](2026-08-15-tactical-squad-movement/README.md): 6대6 Synty 분대의 선형·쐐기·종대 전환, 전술 전용 NavMesh 이동과 절차형 동작 검증
- [2026-08-14 자료 조사 기반 L2 스트리밍 창](2026-08-14-researched-l2-stream-window/README.md): 공식 엔진 원칙을 500m L2에 환산한 3×3 상세·5×5 활성·9×9 준비와 경계 선행 준비 검증
- [2026-08-14 시야 기반 동적 월드](2026-08-14-visibility-driven-world-streaming/README.md): 카메라 시야·예측에 따른 건물 프록시→Synty 상세 승격과 이동 안전 경계·진단 트리 검증
- [2026-08-14 1인칭 동적 공간 타일](2026-08-14-first-person-tile-streaming/README.md): 3×3 활성·5×5 준비 창과 실제 자료 대기 상태를 통합 Scene에서 검증
- [2026-08-14 SimulationWorldShell 통합](2026-08-14-simulation-world-integration/README.md): 농장 1인칭·전술 시점과 진부 입고 정보판을 하나의 Play Scene에 연결
