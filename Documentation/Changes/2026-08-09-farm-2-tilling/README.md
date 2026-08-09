# FARM-2 Tilling Vertical Slice

Scene: `Assets/SsalddelGenerated/Farm/FarmTillingVerticalSlice.unity`

Flow: `Select → Preview → Confirm → Simulation Tick → revision 2 Snapshot → Reconcile → Tilled Dirt Row`

- Preview and Confirm keep snapshot revision 1 unchanged.
- Tick returns a new revision 2 snapshot; it does not mutate the source snapshot.
- Tile selection, NPC arrival, animation and FX do not confirm work or advance the Tick.
- Operational failures are not replaced by this Simulation fixture.

Evidence:

- `2026-08-09-farm-2-selected.png`
- `2026-08-09-farm-2-preview.png`
- `2026-08-09-farm-2-confirmed.png`
- `2026-08-09-farm-2-applied.png`

Validation: core 10/10, Farm View 6/6, full Unity EditMode 55/55.

Next gate: FARM-3 worker movement and minimal animation as Presentation only.
