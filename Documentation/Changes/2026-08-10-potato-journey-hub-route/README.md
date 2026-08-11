# Potato Journey Farm to Hub Gate

This WORLD-6/PVS6 evidence combines the PVS5 Farm data card, CARGO-1 ledger, and Farm-to-Hub route.

- Route: `farm-yard.potato-cargo` to `hub.inbound-dock`
- Cargo: `cargo:sim.potato.20260407.r3` from the CARGO-1 snapshot
- Quantity: `15 × 20kg Box = 300kg`, vehicle capacity `400kg`
- Lineage: `HarvestLot → PackageLot → Cargo` remains visible in the route Scene
- Source mode: `SimulationFixture`
- Linkage: `SimulationLinked`
- Route adapter: the prior separately invented Hub cargo fixture has been removed
- Cargo ledger state: `Loaded`; vehicle movement is presentation only and does not change it to `InTransit`
- Vehicle movement: does not confirm dispatch, transport, arrival, or receiving

The runtime also includes a Bearer-authenticated UnityWebRequest client and a Newtonsoft wire JSON parser that preserves nullable quantities. Parser and failure boundaries were tested, but no live authenticated server call was made.

## Verification

- WORLD-6/PVS6 focused headless tests: 7/7 passed
- Unity core full regression: 278/278 passed
- Unity Hub route EditMode tests: 3/3 passed
- Connected Unity Editor Play Mode Game View captured in `potato-journey-hub-route-game-view.png`
