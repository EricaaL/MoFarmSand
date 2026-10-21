# Task 03: Hotbar, Throw Aiming Arc & Player Health

**Depends on:** 01, 02  **Unblocks:** 04, 05, 06

## Goal
Player can select hotbar slots, aim with the mouse, lob tomatoes along a visible arc, and eat a turnip for a one-hit Bark Shield.

## Context
plan.md Phase 3; `PlayerHealth.cs` added here because plan.md gave HP/shield no owner.

## Files to create
- `Assets/Scripts/Player/PlayerInventory.cs`
- `Assets/Scripts/Player/PlayerHealth.cs`
- `Assets/Scripts/Player/PlayerCombat.cs`
- `Assets/Scripts/Player/TrajectoryRenderer.cs`
- `Assets/Scripts/Farming/CropProjectile.cs`
- `Assets/Prefabs/Tomato_Projectile.prefab`

## Steps
1. `PlayerInventory`: 5 slots, keys 1-5 and scroll wheel, stack counts, `OnChanged` event.
2. `PlayerHealth`: HP (default 3), `HasShield`, `GrantShield`, `TakeDamage` (shield absorbs one hit), `OnChanged`, `OnDied`.
3. `TrajectoryRenderer`: LineRenderer ballistic arc, r(t) = p0 + v0 t + 1/2 g t^2, solved toward the mouse/ground-plane hit; landing decal.
4. `CropProjectile`: Rigidbody parabolic flight; on hitting a `BossHunger` call `Feed(crop.fullnessValue)` directly, then destroy with a splat particle; on ground, destroy.
5. `PlayerCombat`: left-click throws active Projectile crop (consumes 1, small cooldown); right-click on active Support crop consumes 1 and calls `GrantShield`.
6. For testing before farming exists, a debug `[ContextMenu]` that adds starter crops.

## Contracts
`PlayerInventory`, `PlayerHealth`, projectile -> `BossHunger.Feed(float)` as defined in [00-index.md](00-index.md). Task 05 provides `BossHunger`; until then stub only via a null-safe `GetComponentInParent` hit check.

## Acceptance criteria
- Compiles, no console errors; arc matches where the tomato lands; count decrements; shield flag toggles and absorbs one `TakeDamage`.

## Out of scope
Item icons/polish, crafting, drops.
