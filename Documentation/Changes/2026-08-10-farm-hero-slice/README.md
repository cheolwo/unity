# Farm Hero Slice

## Result

- Added a dedicated `FarmHeroShowcase` Scene instead of changing the existing multi-region showcase contract.
- Reframed the initial camera around the Farm production anchor with a 46-degree pitch, 33-unit zone distance, and 31-degree field of view.
- Added 97 Presentation-only Synty wrappers across entrance/road, working-yard, and crop/vegetation groups.
- Added warm soft-shadow lighting, restrained fog, ACES tonemapping, low bloom, and reduced saturation.
- Added the `ART4` motion layer: 59 phase-distributed, low-amplitude crop/sunflower sway presenters and one looping working-tractor Presentation route.
- Preserved the server/Simulation authority boundary: vendor prefab names remain below `VisualRoot` and do not decide work state.

## Files

- Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장대표풍경전시.unity`
- Builder: `Assets/Ssalddel/Experiments - 연구/CityFarmWorldIntegration/Editor/FarmHeroShowcaseBuilder.cs`
- Volume profile: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Profiles/FarmHeroVolumeProfile.asset`
- Game View: `farm-hero-slice.png` (1600x900, Play Mode)
- ART4 Game View: `farm-hero-art4-motion.png` (1600x900, Play Mode)

## Verification

- `FarmHeroShowcaseTests`: 4/4 passed.
- `FarmCityGraphicalShowcaseTests`: 4/4 passed.
- `DioramaCameraTests`: 4/4 passed.
- Total focused and regression EditMode verification: 12/12 passed.
- The ART4 PNG was captured from the Unity Game View in Play Mode and opened at original resolution for visual verification.
- Play Mode observation confirmed the working tractor traversing its short route and the environment presenters running without breaking the Farm composition. The still PNG proves runtime composition, not temporal smoothness by itself.

## Scope boundary

- This closes the first Farm-specific `ART0-ART4` hero composition and ambient-motion slice.
- It does not claim parity with the Synty marketing render. Denser production props, authored character/vehicle animation, particles, data-state art, final camera/post-processing, and mobile profiling remain later `ART5-ART7` work.
- No vendor asset was edited. No commit or push was performed.
