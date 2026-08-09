# Synty City Pack vertical slices

This folder keeps the purchased City Pack at the Unity presentation boundary.
The source prefabs under `Assets/Synty/PolygonCity` are not modified.

## Generated scenes

- `UrbanMarketCityPackVerticalSlice.unity`
- `UrbanLogisticsCityPackVerticalSlice.unity`

Use the `Ssalddel > City Pack` Editor menu to rebuild or validate either scene.
The builders first create the existing data-first sample scene and then replace
only the relevant `VisualRoot` objects with City Pack prefabs. Stable IDs,
simulation/operational mode boundaries, presenters, selection, and runtime
controllers therefore remain owned by Ssalddel code.

## Current asset coverage

- Urban market: shop, apartment, manager, residential representative, desk,
  and shelves.
- Urban logistics center: facility facade, van, pallet, and boxes.
- Characters: imported Humanoid avatars are wired to existing view sockets.

City Pack contains no farming soil/crop tiles and no animation clips or
Animator Controller assets. Farm tile visuals and real walk/work animation are
intentionally left as separate follow-up work instead of being inferred from
the purchased pack.
