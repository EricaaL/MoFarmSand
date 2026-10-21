# Task 01: ScriptableObject Architecture & Core Contracts

**Depends on:** none  **Unblocks:** 02-06

## Goal
Data definitions exist as assets so all other systems read from a single fact owner.

## Context
plan.md Phase 1; AGENTS.md section 3 and 5 (ScriptableObjects, no JSON).

## Files to create
- `Assets/Scripts/Data/CropData.cs`
- `Assets/Scripts/Data/BossData.cs`
- `Assets/Data/Crop_Tomato.asset`, `Crop_Turnip.asset`, `Boss_Caterpillar.asset` (use `[CreateAssetMenu]`; folder `Assets/Data/` or `Assets/Scripts/Data/` assets-wise, pick one and note it)

## Steps
1. `CropData`: fields listed in the index contract; `CropType` enum.
2. `BossData`: `maxFullness`, `chaseSpeed`, `damage`.
3. Tomato: Projectile, growTime 6, fullnessValue 20. Turnip: Support, growTime 8, shieldCharges 1. Caterpillar: maxFullness 100, chaseSpeed 3.5, damage 1.
4. Add `OnValidate` clamps (no negatives).

## Contracts
Exactly the `CropData` / `BossData` shapes in [00-index.md](00-index.md).

## Acceptance criteria
- Compiles with no console errors; three assets exist with the values above.
- Assets editable in Inspector.

## Out of scope
CropRegistry (only add if a lookup is actually needed later), JSON.
