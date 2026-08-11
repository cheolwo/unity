# Potato Journey Farm Vertical Slice

This evidence records the PVS3-PVS5 read-only vertical slice.

- Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자농장출발단계구현.unity`
- Default field selection: `SimulationLinked`
- Cargo-box selection: `ProductOnly`
- Data card: domestic wholesale observation, source lineage, HS 0701, observed date, linkage limitation
- Authority boundary: presentation only; no harvest, dispatch, inventory, receiving, or operational completion command
- Runtime source: explicit `SimulationFixture`; it is not an operational API fallback

## Evidence

- `potato-journey-farm-game-view.png`: connected Unity Editor Play Mode Game View
- Unity Core focused tests: 9/9 passed
- Unity PVS5 EditMode tests: 3/3 passed

The authenticated Unity HTTP transport and canonical cargo relationship remain follow-up work.
