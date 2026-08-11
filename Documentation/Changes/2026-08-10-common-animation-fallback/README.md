# CMP4-A 공용 Animation Fallback

- 화면: `CommonAnimationPreview` Play Mode
- 대상: Farm 농부, Town 주민, City 주민 대표 actor 각 1명
- 계약: `locomotion.idle.v1`, `locomotion.walk.v1`
- 이동 권위: `공용ActorRouteFollower`
- 표현: `공용AnimationAdapter`; root motion 비활성
- Source 상태: Synty standalone/imported character clip 0, controller 0, Town missing controller reference 8
- Fallback: `humanoid.procedural-locomotion.v1`; 실제 Synty clip이나 retarget 완료로 간주하지 않음
- 검증: 전용 EditMode 6/6, 전체 EditMode 94/94, Console Error 0, 저장 Preview Scene과 Play Mode Game View 확인

![공용 Animation fallback Play Mode](common-animation-fallback-playmode.png)
