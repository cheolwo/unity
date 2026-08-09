# WORLD-3 City Farm business View integration

## Scope

- Scene: `Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmBusinessViewIntegration.unity`
- Source baseline preserved: `CityFarmSyntyWorldPrototype.unity`
- Existing Views: Farm 6x6 soil tile, Logistics facility, Urban Market shelf and Concept Card, Residential pickup
- Fallback boundary: `WorldPresentationFallbackView` swaps only Synty `VisualRoot` and primitive child

The Scene does not contain a Simulation tick controller, Operational client, or `LifetimeScope`. Stable IDs and selection remain on the existing business Views when the visual child changes.

## Final Game View captures

- `world-overview.png`
- `farm-production.png`
- `urban-logistics.png`
- `urban-market.png`

These are Edit Mode Game View renders from the saved Scene. The small 3D TextMesh labels are evidence aids; final Card/UI readability and occlusion remain WORLD-5 work.

## Verification

- WORLD-3 focused EditMode: 5/5 passed
- Full Unity EditMode regression: 41/41 passed
- Final recompile: completed successfully
- Final Console errors: 0
- Saved Scene: active and not dirty
- Representative active counts: MeshRenderer 200, Animator 1, ParticleSystem 0
- Primitive fallback sockets: 41

The existing Market and Residential sample Views now apply status colors through `MaterialPropertyBlock`, avoiding Edit Mode material instances and preserving vendor materials.

## Next gate

WORLD-4 connects one potato cargo stable ID and lineage across Farm Yard, vehicle, logistics, and market anchors. Arrival, animation, FX, and camera focus remain Presentation and cannot complete canonical work.
