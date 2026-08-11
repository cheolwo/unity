# FARM-3 Potato Cultivation Lifecycle

Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자재배수확흐름.unity`

Flow: `Tilled → Sowing Preview → Confirm → Simulation Tick → Growth → HarvestReady → Harvest Preview → Confirm → Simulation Tick → 300kg HarvestLot`

- The left panel owns eight explicit Simulation actions and shows game date, calendar fixture provenance, revision, growth stage, and canonical lineage.
- Preview and Confirm do not mutate the snapshot; only an explicit Simulation Tick creates the cultivation cycle or harvest lot.
- Plant scale is Presentation derived from the growth stage. Harvest completion hides the field plants and reveals the existing cargo-box visual and harvest-lot marker.
- The right panel remains server product identity and price evidence. It does not become cultivation, inventory, cargo, or sale authority.
- Calendar dates and the 300kg yield are `Simulation/Fixture`; they are not official cultivation advice or an operational fallback.

## Evidence

- `potato-cultivation-lifecycle-game-view.png`: connected Unity Editor Play Mode Game View at `Harvested`, revision 5
- FARM-3 lifecycle EditMode tests: 4/4 passed
- Existing PVS5 potato journey EditMode tests: 3/3 passed
- Play Mode entered successfully and produced no new runtime error after the final compile

The next gate is `CARGO-1`: turn this Simulation harvest lot into explicit package units and a canonical Simulation cargo relation without promoting it to operational cargo.
