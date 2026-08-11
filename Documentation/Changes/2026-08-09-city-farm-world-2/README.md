# WORLD-2 City Farm Synty presentation prototype

## Scope

- Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시신티월드시제품.unity`
- Source baseline preserved: `농장도시공간배치초안.unity`
- Catalogs: Farm, Urban, and Transition `WorldVisualCatalog` assets
- Visual boundary: `VisualKey -> WorldVisualCatalog -> WorldVisualInstanceView -> VisualRoot -> Synty prefab instance`
- Dedicated post-processing: global Volume profile with Color Adjustments, Neutral Tonemapping, and restrained Bloom

The builder did not modify vendor prefabs or materials, the existing PC/Mobile URP assets or renderers, or Build Settings. The City and Farm packs remain Presentation assets and do not authorize Simulation ticks or Operational commands.

## Final Game View captures

- `world-overview.png`
- `farm-production.png`
- `urban-logistics.png`
- `urban-market.png`

These are Edit Mode Game View renders from the saved Scene. Play Mode was not required for this catalog and composition gate; runtime business interactions remain WORLD-3 and later work.

## Verification

- WORLD-2 catalog and saved Scene tests: 3/3 passed
- Full Unity EditMode regression: 36/36 passed
- Final recompile: up to date
- Final Console errors: 0
- Saved Scene: active and not dirty
- Representative counts: MeshRenderer 142, Animator 1, ParticleSystem 0

The Pipeline test runner emitted status-file sharing and duplicate completion callback errors after reporting passing results. The Console was cleared and a clean recompile confirmed that these were runner-side artifacts rather than product compile or Scene errors.

## Next gate

WORLD-3 connects the existing Farm tile, logistics facility, market, and residential Presentation views to these wrappers. Replacing the Synty child with the WORLD-1 primitive fallback must preserve stable IDs, selection, and Presentation wiring.
