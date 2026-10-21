# Task 05: Caterpillar Boss Chaser & Inverted Hunger

**Depends on:** 01, 02, 03  **Parallel with:** 04  **Unblocks:** 06

## Goal
A segmented caterpillar chases the player; feeding it fills a Fullness meter until it bursts.

## Context
plan.md Phase 5; signature inverted-boss mechanic (AGENTS.md section 1).

## Files to create
- `Assets/Scripts/Boss/CaterpillarBoss.cs`
- `Assets/Scripts/Boss/BossHunger.cs`
- `Assets/Prefabs/Caterpillar_Primitive.prefab`

## Steps
1. Prefab: 3-4 linked spheres trailing the head with squash/stretch.
2. `CaterpillarBoss`: inactive until enabled/woken; chases the `Player` at `BossData.chaseSpeed`; on contact calls `PlayerHealth.TakeDamage(damage)` with knockback and a short contact cooldown.
3. `BossHunger`: `Feed(float)` adds fullness, scales boss slightly larger with a wobble, fires `OnBossFed(current,max)`; at >= max fires `OnBossSatisfied` once, plays a burst particle and stops chasing.
4. Public `Wake()` / `Sleep()` on the boss so task 06 controls it.

## Contracts
`BossHunger` shape in [00-index.md](00-index.md); `Wake()`/`Sleep()` on `CaterpillarBoss`; consumes `PlayerHealth.TakeDamage`.

## Acceptance criteria
- Compiles, no console errors; tomato hits raise fullness by 20 each; five hits trigger `OnBossSatisfied`; contact with a shielded player consumes the shield instead of HP.

## Out of scope
Other bosses, attack variety, boss HP going down.
