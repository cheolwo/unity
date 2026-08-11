# JOURNEY-1 Potato Cargo Farm-to-Hub Lifecycle

Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자화물전체이동흐름.unity`

Flow: `Loaded → Dispatch Review → Confirm → Simulation Tick → InTransit → 3 Route Ticks → ArrivedAtHub`

- Preview and Confirm leave the cargo and Van unchanged; only the explicit Tick starts transit.
- Route ticks advance the game date and deterministically place the Van along the Farm-to-Hub route.
- Cargo stable ID, 15 boxes, 300kg quantity, HarvestLot, PackageLot, and source lineage remain unchanged across the journey.
- Arrival does not confirm inspection, receiving, inventory, or sale.
- Three route ticks and their dates are `Simulation/Fixture`, not real transport time or an operational fallback.

## Evidence

- `potato-cargo-journey-game-view.png`: connected Unity Editor Play Mode Game View at `ArrivedAtHub`, date `2026-04-10`, data revision 3
- JOURNEY-1 headless tests: 6/6 passed
- Unity JOURNEY-1 EditMode tests: 4/4 passed
- Unity core full regression: 284/284 passed
- Final Play Mode produced no new runtime error; the persistent Console still contains historical entries from earlier corrected iterations

The next gate is explicit Hub receiving and inspection. `ArrivedAtHub` must not become accepted inventory merely because the Van reached the endpoint.
