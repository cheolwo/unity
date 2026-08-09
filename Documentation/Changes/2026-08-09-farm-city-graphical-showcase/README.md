# Farm City Graphical Showcase

## Scene

- `Assets/Ssalddel/Experiments/CityFarmWorld/FarmCityGraphicalShowcase.unity`
- source Scene: `CityFarmVisualQualityGate.unity` (unchanged)
- environment root: `WorldBootstrap/Farm City Graphical Environment`

## Presentation inventory

- environment wrappers: 351
- Farm keys: 263 instances
- City keys: 88 instances
- environment renderers: 370
- catalog: `FarmCityEnvironmentCatalog.asset`
- all prefab sources remain under `Assets/Synty/`

## Captures

- `Farm.png`: final Farm composition
- `Transition.png`: rural road to City transition
- `Market.png`: final City furnishing pass
- `Overview.png`: macro composition captured before the last City furnishing pass; Farm and route layout are unchanged

## Verification

- Unity script compilation completed without compiler errors.
- Builder validation passed for wrapper wiring, vendor prefab connection, shader references, and missing scripts.
- Console Error was 0 immediately after the final successful Scene build.
- Four dedicated EditMode tests were added, but the Pipeline Test Runner remained in `running` after a domain reload and produced no completed result. Do not report these tests as passed.
- Final Overview recapture and final profiling require a safe Unity Editor restart.

## Boundaries

- No vendor prefab or material edits.
- No Simulation or Operational authority added.
- No URP Asset or Build Settings edit.
- No season, day/night, large weather, streaming, interior, or new Zone expansion.
- No commit or push.
