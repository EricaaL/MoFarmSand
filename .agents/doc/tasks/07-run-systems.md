# Plan: seasons and disasters, run map, economy and save slots

## Context
The user asked to implement three systems that AGENTS.md listed as out of scope:
1. A 4-season climate simulation with procedural weather disasters.
2. A procedural node map in the style of Slay the Spire.
3. A persistent save system across runs, with an economy.

User decisions:
- **Full systems**, not a trailer facade.
- **The map drives the run**: each map node is one farm day, moving along the map moves the calendar forward, and coins and unlocks persist across runs.
- **Update AGENTS.md** so these systems count as in scope.

What exists today (explored):
- One scene, `GoldenSliceScene`.
- `GameManager` (Day/Night/Won/Lost) reloads the scene after a win or a loss.
- `DayNightLighting` pins COZY `currentTime` between 0.45 and 0.73.
- The HUD is IMGUI (`DebugHUD`).
- There is no currency, save, registry or season code.
- COZY 3.6.23 is embedded (`Packages/com.distantlands.cozy.core`), and the Cozy Weather Sphere is already in the scene.
- `Batch2Builder` is the additive scene-builder pattern to copy.
- There are 6 EditMode tests.

## Architecture
- **One scene, reloaded at every node boundary.** The reload is the reset.
  - A static `RunSession`, reset in `SubsystemRegistration`, holds the active slot and `SaveFile`.
  - The state is committed to disk at every boundary.
- **Pure core** in `Assets/Scripts/Sim/`, inside the existing MoFarmSand asmdef.
  - It must not use MonoBehaviour, `UnityEngine.Random`, `Time`, COZY or `Find*`.
  - `System.IO` is allowed only in `FileSaveStore`.
  - A source-scan test enforces this.
  - It may read SO fields as immutable templates.
- **Presentation and scene glue** go in `Assets/Scripts/Run/`, screens in `Assets/Scripts/UI/` (IMGUI, consistent with DebugHUD).
- **Determinism.** A custom `SeededRng` (SplitMix64) with `Derive(runSeed, nodeId, stream)`. Every daily roll is a function of (seed, node), so resuming a save mid-day gives the same day.
- **Single fact owners.**
  - `PriceTable` SO owns prices; there is no price on CropData.
  - `CalendarConfig` owns run length; map layers = total days + the boss layer.
  - `RunState` owns state between days; live components own it during a day; `RunDirector` is the only place that snapshots and restores.
- **Registries.** A generic `IdRegistry<T> : ScriptableObject` (`IHasId`), with one-line subclasses for Crop, Disaster, NodeType, Unlock, RunModifier and Event. A `GameDatabase` SO aggregates every registry and config. Saves refer to SOs by id.
- **JSON** is used only for runtime save data (`JsonUtility`). All templates stay ScriptableObjects.
- **Events only at seams:** `PlayerWallet.OnChanged`, `DisasterDirector.OnDisasterStarted`, and the existing `OnPhaseChanged`.

## Files by system

### A. Foundations
- New:
  - `Sim/Common/SeededRng.cs`, `RngStreams.cs`, `ModifierStack.cs`
  - `Data/IHasId.cs`, `IdRegistry.cs` plus the registry subclasses
  - `Data/GameDatabase.cs`
  - `Data/RunConfig.cs` (starting loadout and coins, banking rates, `loseRunOnOutOfAmmo`, `dayEndDelay`)
- Modify `Data/CropData.cs` (additive only): implement `IHasId` and add a `CropClimate climate` block: ideal temperature and moisture, tolerances, and a `List<SeasonAffinity{SeasonData, mult}>`.

### A2. Seeds as their own items (user requirement)
Planting uses up a **seed**, which is a separate item from the harvested crop. Each run starts with a random set of seeds. More seeds are bought from the merchant with coins earned by selling harvested crops.

**Item data:**
- New `Data/ItemData.cs`: `abstract ItemData : ScriptableObject, IHasId` with `itemId`, `displayName`, `icon` and `color`.
  - `CropData` becomes a subclass of `ItemData`.
  - The existing fields (`cropId`, `displayName`, `icon`, `color`) stay on `CropData` with the same names, so serialized assets keep their values. `Id => cropId`.
- New `Data/SeedData.cs : ItemData` with `CropData grows` and an optional seed-packet tint.
  - The seed asset is the single owner of which crop it grows.
  - The crop asset still owns grow time, yield and combat values.
- `ItemRegistry`: every crop and every seed. Saves and inventory snapshots look items up by id through it.

**Inventory** (`Player/PlayerInventory.cs`):
- `Slot.crop` becomes `ItemData item`, with `[FormerlySerializedAs("crop")]`.
- `ActiveItem` and `ActiveSeed` are added, and `ActiveCrop` is kept (it returns `ActiveItem as CropData`), so `PlayerCombat` and `DebugHUD` keep working unchanged.
- Slot count goes from 5 to 8 so seeds and crops both fit.
  - Keys 1–5 work as they do now, and 6–8 are added.
  - The hotbar shows a seed-packet glyph on seed slots.
- Throwing and eating a Support crop only accept `CropData`. Seeds can't be thrown.

**Planting** (`Farming/FarmTile.cs`):
- When the active slot holds a `SeedData`, it uses up 1 seed and grows `seed.grows`.
- Harvest adds the crop's `harvestYield` units of the **crop** item. It drops no seeds.
- The legacy `defaultCrop` free-planting fallback stays only behind `allowFreePlanting`.
  - `allowFreePlanting` defaults to true, so the legacy trailer scene is unchanged.
  - `RunDirector` sets it to false in Roguelike mode.
- The prompt reads "[E] Plant Tomato seed (3 left)", or "Need seeds — visit the Merchant" when you have none.

**Starting seeds** (`RunConfig`):
- `startingSeedPool`: a weighted list of `{SeedData, Vector2Int countRange}`, plus `startingSeedPicks` (default 2–3 seed types).
- Picks are seeded from `runSeed` through `RngStreams.Loadout`, so the same seed always gives the same start.
- The pool only includes seeds in season on day 0.
- New runs start with 0 crops and only a few starting coins.

**Economy changes:**
- Each `PriceTable` row now covers both sides: `{ CropData crop; SeedData seed; int seedCost; int baseSell; season multipliers; availableSeasons }`.
- The merchant **sells seeds** (from the in-season rows) and **buys crops**.
- `ShopGenerator` offers only seeds and run modifiers.
- Instead of the no-arbitrage invariant, a profitability test: for every row in an available season, `seedCost < crop.harvestYield × SellPrice(demand=1)`. Farming always pays at base demand, while saturation can still erase the profit.
- Effects and unlocks change accordingly:
  - `AddCropEffect` becomes `AddItemEffect`.
  - Seed Pouch gives +3 Turnip seeds.
  - Pumpkin Patch adds Pumpkin seeds to the starting pool.

**Builder:**
- Creates `Seed_Tomato`, `Seed_Turnip`, `Seed_Pumpkin` and `Seed_Frostberry`, each tinted like its crop with a small packet icon colour.
- Fills in the `PriceTable` seed column and the `ItemRegistry`.

**Tests:**
- `InventoryTests`: seed and crop stacking stays separate.
- `FarmTileSeedTests`: planting uses up exactly 1 seed, it can't plant with no seed when `allowFreePlanting` is false, and harvest gives the crop item.
- `RunLogicTests`: the starting seed loadout is deterministic per seed and only includes in-season seeds.

### A3. Bank: deposits at season end, nothing banked automatically (user requirement)
Coins don't carry between runs on their own. The only way to keep money for the next run is to deposit it at the **Bank** at the end of each season. Any coin left in your wallet when the run ends is lost. Each deposit is a trade-off: coins saved for later can't be spent on seeds now, and that stops money from snowballing.

**Decision point:**
- After the last day of each season, and before the next node is chosen, `RunDirector` shows the `BankScreen` ("Season's End — The Bank").
- `SeasonCalendar.IsLastDayOfSeason(dayIndex)` decides when. The map structure doesn't change.
- After winter (the final season) the next stop is the boss, so there are **4 decision points per run**.
- The screen offers:
  - a deposit slider or buttons (+1, +10, All), showing the current wallet and the vault total;
  - a "Skip" button.
- `RunState.phase` gets a `Banking` value, so quitting during the screen resumes back on it.

**Rules** (new `Data/Economy/BankConfig.cs`, the single owner):
- `depositFeePercent` (default 10%): the bank keeps a cut, so depositing everything always costs something.
- `maxDepositPerSeason` (default 0, meaning no cap) as a tuning knob.
- `vaultCap` (default 200) stops the vault growing forever across runs.
- No interest. No withdrawals during the run: a deposit is locked until the next run.

**Vault and meta progress:**
- `MetaProgress.bankedCoins` becomes `vaultCoins`, and gets `lastRunDeposited` for the summary.
- The deposits made in the current run are tracked in `RunState.depositsThisRun`.
  - Deposits move into the vault immediately and are saved straight away, so a crash can't lose them. The vault therefore always holds exactly what was deposited.
- **Starting the next run** (Title screen → New Run):
  - The vault can be spent on permanent unlocks (`UnlockData.cost`).
  - Whatever is left is **withdrawn in full into the new run's starting wallet**, and the vault goes back to 0.
  - Money can't build up over several runs without being deposited again each season.
- **Removed from the earlier plan:** the automatic `MetaRewards.Bank` at the end of a run, and the `RunConfig` fields `metaBankRateOnWin`, `metaBankRateOnLoss` and `bankPerLayerReached`. Run end now only records stats.

**Pure logic:** `Sim/Economy/BankLogic.cs`
- `int Deposit(RunState run, MetaProgress meta, int amount, BankConfig cfg)`:
  - clamps to the wallet, the season cap and the vault cap;
  - takes the fee;
  - returns the coins actually credited.
- `int WithdrawAllForNewRun(MetaProgress meta)`.

**UI:**
- `BankScreen.cs`: piggy-bank or vault theme for the trailer, with season colours.
- `RunSummaryScreen` shows "Coins lost: X · Vault: Y".
- `TitleScreen` shows the vault in each slot card.

**Tests** (`BankTests`):
- the fee is applied;
- a deposit can't go above the wallet or either cap;
- the vault survives a save round trip;
- a new run withdraws the whole vault and leaves it at 0;
- wallet coins are lost at run end;
- the bank triggers exactly once per season end, and resumes in the `Banking` phase.

### B. Seasons and climate
- `Data/SeasonData.cs`: temperature and precipitation curves and variance, `lengthInDays`, `disasterChanceBonus`, `uiColor`, COZY year mapping (`cozyYearStart01`, `cozyYearSpan01`, `initialSnow01`).
- `Data/CalendarConfig.cs` and `Data/ClimateConfig.cs`: growth clamp, and weather-kind threshold rows (data, no switch).
- `Sim/Calendar/SeasonCalendar.cs`: `DateOf(dayIndex)` returns a `CalendarDate`.
- `Sim/Calendar/DayWeather.cs`.
- `Sim/Calendar/ClimateModel.cs`:
  - `SampleDay(date, rng)`
  - `GrowthMultiplier(crop, date, weather, cfg)` = season affinity × temperature factor × moisture factor, clamped.
- `Run/FarmConditions.cs`: today's date and weather, `ModifierStack Growth`, `GrowthRateFor(crop)` (cached).
- Modify `Farming/FarmTile.cs`:
  - Optional `conditions` reference; when it is null the rate is 1, which is the legacy behavior.
  - `timer -= dt * rate`.
  - New: `Crop`, `Progress01`, `Wither()`, `Block(s)`, `IsBlocked`, and `IFarmPlot`.
  - The prompt shows the rate and "Flooded".

### C. Disasters
- `Sim/Disasters/IFarmWorld.cs` (`IFarmPlot`, `IFarmWorld`; a fake implementation is used in tests), `DisasterRoll.cs`, and `DisasterRoller.cs`.
  - The roller picks by season weight. Chance = node chance + season bonus. Severity is reduced by resist modifiers.
- `Data/DisasterData.cs`: per-season weights, `minDay`, `severityRange`, `activeAtNight`, `List<DisasterEffect>`.
- `Data/Disasters/DisasterEffect.cs`: an SO factory that creates a plain runtime object (`Begin`/`Tick`/`End`). Effects:
  - `GrowthMultiplierEffect`
  - `WitherPlotsEffect`
  - `StrikeHazardEffect`: telegraph ring, then a strike that damages the player inside its radius and shakes the camera.
  - `BlockPlotsEffect`
  - `PriceShockEffect`
  - `PlayerSlowEffect`: only if `PlayerController` can take a `SpeedMultiplier` in one line.
- Six disaster assets, composed from those effects:
  - Drought
  - Heatwave
  - Storm
  - Frost
  - Locust Swarm
  - Blight
- `Run/DisasterDirector.cs`: adapts `IFarmWorld` to FarmConditions, PlayerHealth, GameFeedback and FX; ticks effects during Day (and at night if `activeAtNight`).
- `Data/CozyWeatherMap.cs`: (season, weather kind) → COZY `WeatherProfile`, and disaster → profile plus ambient FX. This keeps COZY types out of the gameplay SOs.

### D. Map
- `Sim/Map/MapGraph.cs`: serializable nodes with `id`, `layer`, `lane`, `nodeTypeId` and `next`. This is also the saved form.
- `Sim/Map/MapGenerator.cs`: Slay-the-Spire rules.
  - 7 lanes and 6 paths; each step moves the lane by −1, 0 or +1; no crossing edges.
  - Nodes on the same layer and lane are merged.
  - Node types come from forced layers, then from weighted layer bands filtered by `minLayer`, `noConsecutive`, `maxPerPath` and sibling diversity.
  - Attempts are bounded, with a fallback to Farm.
  - The boss node is appended last.
- `Sim/Map/MapValidator.cs`: used by the tests and at runtime. On failure it regenerates with seed+1, up to 5 times.
- `Data/Map/NodeTypeData.cs`: the day shape is declared as data, not switched on:
  - `farmingAllowed`, `encounter` (BossData or null), `merchantActive`, `disasterChance`, `healOnStart`, `growthBonus`, `eventPool`, `coinReward`, `endsRun`
  - placement rules
- Node types:
  - Farm
  - Merchant
  - Disaster
  - Elite: new `Boss_Caterpillar_Elite`, with no evolution
  - Rest
  - Event
  - Treasure
  - Boss: Caterpillar, which evolves into the Butterfly
- `Data/Map/MapGenConfig.cs` and `Data/Events/EventData.cs`: options are lists of `RunEffect`, each with a coin cost.

### E. Economy and merchant
- `Sim/Economy/MarketState.cs`: demand per crop, saved.
- `Sim/Economy/EconomyModel.cs`:
  - Sell = base × season multiplier × demand × shock.
  - Seed buy price = seed cost × season multiplier × markup × shock.
  - Each crop sale saturates demand; demand recovers each day with a seeded drift.
- `Sim/Economy/ShopGenerator.cs`: **seed** offers for the current season, plus run-modifier offers. The merchant buys crops and sells seeds (see A2).
- `Data/Economy/PriceTable.cs`: the spreadsheet. Each row has crop, seed, seed cost, base sell, season multipliers and available seasons.
- `Data/Economy/EconomyConfig.cs`.
- `Data/Run/RunModifierData.cs`: passive buffs, e.g. Scarecrow, Rain Barrel, Fertilizer.
- `Data/Run/RunEffect.cs`: AddCoins, AddCrop, Heal, Damage and AddModifier. Each applies to `RunState`.
- New `Player/PlayerWallet.cs`.
- Modify `Player/PlayerInventory.cs` (additive): `CountOf`, `TryRemove` (any slot), `Clear`, `Snapshot`, `Restore`.
- `Run/MerchantStall.cs`: an `IInteractable` placeholder booth that opens the ShopScreen.
- Optional: an editor-only CSV export of `PriceTable` for the pitch deck. There is no import, to keep one owner for prices.

### F. Runs, saves and meta progress
- `Sim/Run/RunState.cs`: seed, map, current and visited nodes, `dayIndex`, `RunPhase`, `pendingNodeId`, coins, HP, inventory snapshot, market, modifiers, stats.
- `Sim/Run/MetaProgress.cs`: banked coins, unlocks, runs started and won, best layer.
- `Sim/Run/SaveFile.cs`: `version` = 1, meta, `hasRun`, run.
- `Sim/Run/RunLogic.cs`: `NewRun` (withdraws the vault into the wallet, see A3), `CanEnter`, `BeginNode`, `CompleteNode` (moves to the `Banking` phase at a season end), `FailRun`. There is no automatic banking.
- `Sim/Run/DayPlanner.cs`: deterministic `Plan(run, nodeId)` returns date, weather, node, disaster, encounter, shop offers, event and reward.
- `Sim/Save/ISaveStore.cs` and `FileSaveStore.cs`: saves go under `persistentDataPath/saves`. Writes are atomic: write a .tmp file, then `File.Replace` it into place with a .bak.
- `Sim/Save/SaveService.cs`:
  - 3 slots.
  - Load returns Ok, Empty, Corrupt (after trying the .bak) or TooNew. It never throws, and a TooNew file is never overwritten.
  - `Peek` gives a slot summary.
  - `Sanitize` drops unknown ids, clamps values and validates the map.
- `Sim/Save/SaveMigrations.cs`: an ordered migration registry.
- `Data/Meta/UnlockData.cs`: cost in banked coins plus `onRunStart` RunEffects. Unlocks: Hardy Paws, Seed Pouch, Nest Egg, Pumpkin Patch.
- `Run/RunSession.cs`: static active slot and file, `Commit()`.

### G. Orchestration and integration
- `Run/RunDirector.cs`. It has two modes: `RunMode { GoldenSlice, Roguelike }`. **GoldenSlice keeps the legacy reload loop, for trailer takes.**
  - On Start, the player is frozen, then it routes:
    - no slot → Title
    - ChoosingNode → Map
    - InDay → resume the day
    - Won/Lost → Summary
  - `StartDay` steps:
    1. Get the plan.
    2. Show the event, if the node has one.
    3. Apply rest healing.
    4. Restore inventory, health and wallet.
    5. Configure FarmConditions, DisasterDirector and CozyBridge (one frame later).
    6. Call `GameManager.Configure(DayRules)` and `BossHunger.Configure(encounter)`.
    7. Toggle the boss and the merchant stall.
    8. Unfreeze the player.
  - Its `IDayOutcomeHandler` takes a snapshot, then calls `CompleteNode` or `FailRun` plus banking, then `Commit`, then reloads.
  - Context menus for trailer capture: Force Disaster, Jump to Boss Node, Give Coins, Wipe Slot.
- `Run/CozyBridge.cs`:
  - Sets `timeModule.currentDay` from the season mapping and never touches `currentTime`.
  - Sets `weatherModule.Ecosystem.weatherSelectionMode = manual`, then calls `SetWeather(profile, transition)`.
  - Seeds `climateModule.snowAmount` and spawns ambient FX.
  - Is null-safe when COZY is missing.
  - Verify the exact COZY member names against the package source before writing it.
- Modifications to existing files:
  - `Core/GameManager.cs`:
    - Append `DayEnd` to the end of `GamePhase`.
    - Add `DayRules` with `Configure`; `hasEncounter` defaults to true.
    - Add an `OutcomeHandler` property. Win, lose and DayEnd go through `Resolve()`, which calls the handler if one is set and otherwise does the legacy reload.
    - New `Core/IDayOutcomeHandler.cs`.
  - `Core/BellTrigger.cs`: on days with no encounter the prompt is "end the day", and the ammo check is skipped.
  - `Boss/BossHunger.cs`: add `Configure(BossData)`.
  - `Boss/CaterpillarBoss.cs`, `ButterflyBoss.cs`: read `hunger.Data` when it is present, falling back to their own field. This removes the duplicated BossData source.
  - `Player/PlayerHealth.cs`: add `Restore(hp, max)`.
  - `Core/DayNightLighting.cs`: comment only. It owns `currentTime`; the bridge owns date and weather.
  - `Core/DebugHUD.cs`: skip drawing while a modal is open; hide the boss bar on days with no encounter; add the "Day complete!" banner.

### H. UI (IMGUI), in `Assets/Scripts/UI/`
- `ImGuiKit.cs`: fill, line, circle and button helpers, cached styles, scale based on `Screen.height / 1080`.
- `RunScreens.cs`: router with `AnyModalOpen`.
- `TitleScreen.cs`: 3 slot cards with Continue, New and Delete, plus a meta-unlock shop.
- `MapScreen.cs`: vertical map with season-coloured bands, nodes coloured by type, edges, pulsing available nodes, tooltips, scrolling. Framed for the trailer.
- `ShopScreen.cs`: buy and sell columns with season arrows and a demand bar.
- `EventScreen.cs`.
- `RunSummaryScreen.cs`.
- `RunHUD.cs`: coins, a date and weather chip, the disaster banner and a node chip.

### I. Editor builder: `Assets/Editor/RunSystemsBuilder.cs`
- `public static AddRunSystems()`, on menu `MoFarmSand/Add Run Systems`, following the Batch2 pattern. It is additive and idempotent and never calls `NewScene`.
- **Assets** go in `Assets/Data/{Seasons,Climate,Disasters,Map,Economy,Run,Meta,Events,Registries,Presentation}`.
  - Asset `init` runs only on create, so tuned values are never overwritten. Registries are "ensure contains".
  - Content to create:
    - 4 seasons, 3 days each
    - the effects and the 6 disasters
    - the 8 node types and the map config
    - `Boss_Caterpillar_Elite`
    - new crops `Crop_Pumpkin` (autumn projectile) and `Crop_Frostberry` (winter support)
    - climate blocks on Tomato and Turnip, only if they are empty
    - PriceTable, EconomyConfig, the modifiers, unlocks and events
    - CozyWeatherMap, loading profiles from `Packages/com.distantlands.cozy.core/Content/Resources/Profiles/Weather Profiles/`
    - GameDatabase
- **Simple particle FX prefabs** in `Assets/Prefabs/Systems/FX/`: rain, snow, heat, locusts, spores, wither puff, strike ring.
- **Scene changes:**
  - Under `--- MANAGERS ---`: RunDirector (with the screens and RunHUD), FarmConditions, DisasterDirector, CozyBridge.
  - `--- RUN ---` root containing the MerchantStall.
  - `PlayerWallet` on the Player.
  - `conditions` wired on every FarmTile.
  - All references wired, and `runMode = Roguelike`.
- **Save before and after.**

### J. Docs
- AGENTS.md:
  - §2: restore the missing "Out of scope" heading. Move seasons and disasters, the run map, the economy and saves, and the butterfly stage into "In-Engine Scope". Write a new out-of-scope list: multiple farm scenes, cloud saves, a uGUI rewrite, online features, mods.
  - §5: clarify that JSON is allowed only for runtime save data, and list the new SO templates.
  - §6: add the `Sim/`, `Run/` and `UI/` folders.
  - §8.3: add `-executeMethod RunSystemsBuilder.AddRunSystems`.
- `.agents/doc/tasks/07-run-systems.md`: a copy of this plan.

## Implementation order (compile check after each phase)
| Phase | Work |
|---|---|
| 0 | Docs |
| 1 | Foundations |
| 2 | Climate, plus the FarmTile rate hook |
| 3 | Disasters |
| 4 | Map |
| 5 | Economy, wallet and inventory API |
| 6 | Run, save and meta |
| 7 | Integration: GameManager, bell, bosses, health, CozyBridge, RunDirector, merchant |
| 8 | UI |
| 9 | Builder; run it |
| 10 | Balance, VFX timing, debug menus |

Compile checks:
- Preferred: Unity MCP `read_console` and `run_tests`, if the Editor is connected.
- Otherwise, headless with `D:\unity\6000.0.46f1\Editor\Unity.exe`, after checking `Get-Process Unity`.
- A check that cannot run is reported as **blocked**.
- Add `DistantLands.Cozy.Runtime` to the references in `Assets/Tests/EditMode/MoFarmSand.Tests.EditMode.asmdef`.

## Verification
- **EditMode tests**, one file per system, in `Assets/Tests/EditMode/`:
  - `SeededRngTests`
  - `RegistryTests`
  - `ClimateModelTests`: season boundaries, growth peaks at the ideal values, clamp, determinism.
  - `DisasterRollerTests`: deterministic; no Frost in Summer over 10k rolls; a disaster node always rolls; resist modifiers reduce severity.
  - `DisasterEffectTests`: against a fake `IFarmWorld`.
  - `MapGeneratorTests`: 500 seeds; one boss in the last layer; everything reachable and no dead ends; no crossing edges; forced layers and rules respected; same seed gives identical JSON.
  - `EconomyTests`: formulas, saturation and recovery, seeds profitable at base demand across the real PriceTable, shop offers only in-season seeds.
  - `FarmTileSeedTests`: see A2.
  - `InventoryTests`
  - `RunLogicTests`
  - `DayPlannerTests`
  - `SaveServiceTests`: round trip, no leftover .tmp, .bak fallback, corrupt file, slots independent, v0 migration, TooNew untouched, sanitize.
  - `DataDriftTests`: registries valid; exactly one price row per crop; node types registered; every season has a COZY mapping.
  - `SimBoundaryTests`
  - The existing 6 tests must still pass.
- **Play-mode smoke test via MCP:**
  1. New run in slot 1, then the map.
  2. A Spring farm day: ring the bell, see the day-end banner, back to the map.
  3. A disaster day: the growth multiplier shows in the tile prompt and the storm strikes.
  4. Seeds and merchant:
     - The run starts with random seeds and no crops.
     - Planting uses up a seed, and you can't plant with no seeds.
     - Sell the harvested Tomatoes, buy Pumpkin seeds, and see demand drop.
  5. Elite night win.
  6. Quit mid-day, then Continue: the same day plan comes back.
  7. At the end of Spring the Bank screen appears; deposit 20 coins and see the fee taken. Then win the boss: the summary shows wallet coins lost and the vault total. Start a new run: buy an unlock from the vault, the rest goes into the starting wallet, and the vault returns to 0.
  8. Set `runMode = GoldenSlice`: the legacy loop and trailer flow are identical.
  9. Check the console for errors after every play.

## Risks
- COZY forecast mode overriding the weather. Handled by setting manual mode, applied one frame after Start.
- Snow builds up over time. Handled by seeding `snowAmount`.
- `JsonUtility` limits: no dictionaries and no polymorphism, so the save uses lists, ids and the `hasRun` flag.
- The bosses' data source changes. Covered by the GoldenSlice regression play test.
- The builder overwriting tuned data. Handled because asset init runs only on create.
- Map generation not terminating. Handled with bounded attempts, a validator and regeneration.
