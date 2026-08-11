# HUB-2 / WORLD-8 Potato Hub Disposition

Scene: `Assets/Ssalddel/Experiments - 연구/CityFarmWorld/감자생산유통/감자물류거점판로분배흐름.unity`

Flow: `AcceptedAtHub → Split Review → Confirm → Tick → LotsSeparated → Outbound Review → Confirm → Tick → OutboundCandidate`

- The accepted inspection result is split into a 288kg accepted lot and a 12kg rejected/loss lot.
- Both lots preserve the inspection result and source Cargo lineage; their sum remains 300kg.
- Only the accepted lot can become the source of the 288kg Hub-to-City outbound candidate.
- The rejected/loss lot remains explicit with reason `DamageFixture` and cannot enter the candidate lineage.
- `CandidateOnly` is not a departed Cargo, Hub inventory, City receipt, market stock, or sale.
- All quantities and the route are `Simulation/Fixture`, not operational logistics records.

## Evidence

- `potato-hub-disposition-game-view.png`: connected Unity Editor Play Mode Game View at `OutboundCandidate`, data revision 3
- HUB-2/WORLD-8 headless tests: 7/7 passed
- Unity HUB-2/WORLD-8 EditMode tests: 4/4 passed
- Unity core full regression: 297/297 passed
- Unity full EditMode regression: 150/150 passed
- Final Play Mode produced no runtime Error; one missing-script Warning from inherited World content remained visible

The next gate creates an explicit outbound Cargo and Hub-to-City Journey from the accepted-lot candidate, without converting it into City inventory on arrival.
