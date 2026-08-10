# Three Region terrain relief and night cargo raid

## Outcome

- Preserved the 300 m regional spacing and added `ART1 Terrain Relief` with 14 low-poly landforms: four Farm bunds, three Town hills, three Hub cut/fill slopes, and four City ridges.
- Added five roadside composition groups with 10 detached houses and at least 30 tree views.
- Added a Simulation-only evening raid presenter: three Synty skeleton actors stop the presentation truck, two carry cargo props, and the operational cargo stage and lineage remain unchanged.
- Reused the world time presenter at 19:45 for a readable blue-hour encounter and kept the saved Scene in the daytime state.

## Play Mode evidence

- `terrain-relief-game-view.png`: Hub Zone and surrounding road/grade relationship.
- `roadside-day-game-view.png`: daytime Hub-City corridor and relief edges.
- `night-monster-looting-game-view.png`: the same corridor with the stopped van and three raiders.

## Validation

- `terrain-relief-evidence-editmode.xml`: 13/13 passed.
- Prior scoped camera and graphical regression: 8/8 passed.
- The screenshots were produced from an Editor Play Mode session through `ScreenCapture`, not from Scene View.

## Known visual follow-up

- This is a first terrain-relief pass. The landforms are intentionally outside the flat driveable pads; spline blending, terrain-texture transitions, denser roadside props, and mobile profiling remain follow-up work.
