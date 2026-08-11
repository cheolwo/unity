# HUB-1 Potato Receiving and Inspection

Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자물류거점입고검수흐름.unity`

Flow: `ArrivedAtHub → Receiving Review → Confirm → Tick → Inspection → Inspection Review → Confirm → Tick → Accepted`

- Arrival alone does not create receiving, inspection, inventory, or sale state.
- The fixture conserves the received 300kg as `288kg accepted + 12kg rejected` with reason `DamageFixture`.
- Cargo, HarvestLot, and PackageLot stable IDs remain traceable through the inspection result.
- Accepted quantity is not yet a stock lot or outbound cargo; rejected quantity is not silently discarded.
- The decision and loss are `Simulation/Fixture`, not an operational quality judgment.

## Evidence

- `potato-hub-receiving-game-view.png`: connected Unity Editor Play Mode Game View at `Accepted`, data revision 3
- HUB-1 headless tests: 6/6 passed
- Unity HUB-1 EditMode tests: 4/4 passed
- Unity core full regression: 290/290 passed
- Final Play Mode produced no new runtime error

The next gate creates separate accepted and rejected lots before any City outbound route or market inventory can exist.
