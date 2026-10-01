# Base building (planned)

Buildings are Resources (`BuildingData`: id, name_key, level costs in
`ResourceWallet` ids, unlocked card ids). The base scene is a routed scene
(`Routes.BASE`, not yet registered) that spends from `GameSession.wallet`.
Card upgrades are simple swaps or single-stat tweaks (blueprint scope rule).
