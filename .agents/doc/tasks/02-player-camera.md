# Task 02: Player Movement & Animal Crossing Camera

**Depends on:** 01  **Unblocks:** 03, 04, 05

## Goal
A primitive dog walks around with WASD under a smooth high-angle follow camera.

## Context
plan.md Phase 2; AGENTS.md section 4 (New Input System, CharacterController).

## Files to create
- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Scripts/Core/AnimalCrossingCamera.cs`
- `Assets/Prefabs/Player_Primitive.prefab`
- `Assets/Scenes/GoldenSliceScene.unity` (create if missing; groups: `--- ENVIRONMENT ---`, `--- MANAGERS ---`, `--- LIGHTING ---`)

## Steps
1. `PlayerController`: WASD via `InputSystem_Actions.inputactions` (reuse existing Move action), CharacterController, rotate toward movement, gravity/ground clamp.
2. Prefab: sphere body + cube snout facing forward, tagged `Player`.
3. `AnimalCrossingCamera`: offset (0, 9, -10), fixed rotation (40, 0, 0), `SmoothDamp` follow.
4. Ground plane in scene.

## Contracts
Player prefab has tag `Player` and a `CharacterController`; camera exposes `Camera.main` usable for mouse raycasts.

## Acceptance criteria
- Compiles, no console errors; in Play mode the dog moves and faces movement direction; camera follows smoothly without jitter.

## Out of scope
Mixamo animation (primitives only), Cinemachine.
