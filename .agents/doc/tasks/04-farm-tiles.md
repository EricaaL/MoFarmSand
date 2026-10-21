# Task 04: Farm Soil Grid & Contextual Interaction

**Depends on:** 01, 02, 03  **Parallel with:** 05  **Unblocks:** 06

## Goal
Player plants, waits 5-10s, and harvests crops into the hotbar using one key.

## Context
plan.md Phase 4.

## Files to create
- `Assets/Scripts/Farming/FarmTile.cs`
- `Assets/Prefabs/FarmTile.prefab`
- Grid of 6-12 tiles (3x3 or 2x4) in `GoldenSliceScene.unity`

## Steps
1. `FarmTile` state machine: Empty -> Growing -> Harvestable (visual states: tilled soil, sprout, mature, highlight). Growth duration comes from `CropData.growTime`.
2. Interact key `E`/Space on the nearest tile within range: Empty plants the selected crop (default Tomato); Growing shows a remaining-time label; Harvestable calls `PlayerInventory.AddItem` and resets.
3. Tile refuses interaction when `GameManager.CanFarm` is false (read via a null-safe check until task 06 exists).
4. Squash/pop feedback on plant and harvest.

## Contracts
Uses `PlayerInventory.AddItem(CropData,int)`. Does not touch boss code.

## Acceptance criteria
- Compiles, no console errors; full plant-grow-harvest loop works on every tile; harvested count appears in the hotbar.

## Out of scope
Watering/tilling as separate steps, tile persistence.
