# CARGO-1 Potato Harvest Cargo Lifecycle

Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자수확포장상차흐름.unity`

Flow: `300kg HarvestLot → Pack Review → Confirm → Simulation Tick → 15 × 20kg Box → Load Review → Confirm → Simulation Tick → 300kg Cargo / 400kg Capacity`

- Harvest, package, and cargo keep separate stable IDs and an explicit source lineage.
- Preview and Confirm do not mutate quantities or revisions; only the Simulation Tick creates package or cargo state.
- Package quantity must equal the HarvestLot quantity, and cargo quantity must equal the package quantity.
- Loading is rejected when no PackageLot exists or when vehicle capacity is exceeded.
- The right product and price evidence card remains read only and does not become cargo, inventory, or sale authority.
- The 20kg box and 400kg vehicle capacity are `Simulation/Fixture`, not operational packaging or transport rules.

## Evidence

- `potato-harvest-cargo-game-view.png`: connected Unity Editor Play Mode Game View at `Loaded`, revision 3
- CARGO-1 headless tests: 6/6 passed
- Unity CARGO-1 EditMode tests: 4/4 passed
- Unity core full regression: 276/276 passed
- Final Play Mode produced no new runtime errors after the material-instancing warning was removed

The next gate is to adapt this exact Simulation Cargo snapshot into the existing PVS6 Farm-to-Hub route instead of using a separately invented cargo fixture.
