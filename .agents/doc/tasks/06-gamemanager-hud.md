# Task 06: Day/Night Cycle, Win/Loss States & Debug UI

**Depends on:** 01-05

## Goal
The full loop is playable: farm by day, ring the bell, fight at night, win or reset.

## Context
plan.md Phase 6; COZY Weather MCP (AGENTS.md 8.2) for lighting if the package is installed, otherwise a simple directional-light/skybox swap.

## Files to create
- `Assets/Scripts/Core/GameManager.cs`
- `Assets/Scripts/Core/DebugHUD.cs`
- `Assets/Prefabs/Bell_Trigger.prefab`

## Steps
1. `GameManager`: `GamePhase` Day/Night/Won/Lost, `CanFarm`, `OnPhaseChanged`. Bell interaction (`E`) switches to Night: lighting shifts spooky, `CaterpillarBoss.Wake()`, farming disabled.
2. Win on `BossHunger.OnBossSatisfied`. Lose on `PlayerHealth.OnDied` or when inventory is empty (`TotalCount()==0`, no projectile or shield left) while fullness < 100%. Lose resets the scene to Day.
3. `DebugHUD` (`OnGUI`): top-center Fullness bar with %, top-left HP and shield indicator, bottom-center 5 hotbar boxes with active highlight and counts, center banner for win/lose.
4. Hierarchy tidy, full playthrough.

## Contracts
Consumes everything in [00-index.md](00-index.md); defines `GameManager` section.

## Acceptance criteria
- Compiles, no console errors; complete run farm -> bell -> win works; both lose conditions reset cleanly; HUD matches state.

## Out of scope
Polished UI, menus, saves.
