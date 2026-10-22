# AGENTS.MD — Project Guidelines & Context: MoFarmSand

Welcome, Agent. This repository contains the Unity prototype for **MoFarmSand** (Working Titles: *Cursed Rancher*, *The Miracle Berry*, *Dogsend*).

This document serves as your operational manual, architectural blueprint, and guardrails when reading, creating, or modifying code and assets in this project.

---

## 1. Project Overview & Pitch Context

* **Genre:** Creepy-Cute Roguelike / Farming Sim Hybrid (Think *Stardew Valley* meets *Cult of the Lamb* / *Atomicrops*).
* **Core Fantasy:** A canine farmer protecting an enchanted farm using crops as weapons and tools against gluttonous pests.
* **Signature Mechanic (Inverted Boss Fight):** Boss HP **increases** as you feed them crops to burst/satisfy them rather than chipping down a standard health bar.
* **Deliverable Target:** Academic pitch for **DIGI 237: The Art of Storytelling**.
  * **Final Deliverable:** Comprehensive Pitch Deck (PowerPoint) + Rendered H.264 Cinematic Gameplay Pitch Trailer.
  * **Target Audience:** Young adult (13+).
* **Team:**
  * **Alley:** Producer, Market Research, Slides & Pitch Deck, Video Editing.
  * **Erica:** 3D Environment & Crop Props, UI Design, Unity Engine Setup & Integration.
  * **Elisha:** Character & Boss Design (Dog Farmer, Caterpillar, Merchant), Storyboard Trailer.

---

## 2. Core Directive: Scope Guardrails (Trailer-First Development)

> [!IMPORTANT]
> **This project is built to power a pitch video and prototype showcase, NOT a commercially feature-complete roguelike.**
> Do **NOT** over-engineer complex backend systems. Prioritize visual feel, responsive controls, and camera-ready moments over deep simulation logic.

### In-Engine Scope ("The Golden Slice")
1. **Spring Season Farm:** A single compact farm plot (6–12 tiles).
2. **Day/Night Cycle:** Lighting transitions from calm, pastel daylight (farming) to spooky nighttime mood (boss fight).
3. **Dog Character Controller:** 3rd-person / top-down movement with Mixamo Humanoid animations (Idle, Run, Farm/Interact, Throw).
4. **Two Working Crops:**
   * **Throwable / Projectile Crop (e.g., Tomato / Berry):** Fired at bosses to feed them.
   * **Support / Healing Crop (e.g., Carrot / Turnip):** Grants shield or restoration.
5. **One Boss (The Caterpillar):** Simple state machine (Crawling/Chasing, Eating, Satisfied/Bursting) with a "Fullness / Overfed" meter.
6. **Cinematic Tools:** Unity Cinemachine & Unity Recorder setup for capturing trailer shots.
7. **Boss Evolution:** After feeding the caterpillar, it evolves into a butterfly (boss stage 2).

### Run Systems (now in scope; see `.agents/doc/tasks/07-run-systems.md`)
8. **Seasonal climate & disasters:** 4 seasons driven by `CalendarConfig` / `SeasonData` ScriptableObjects. Climate curves affect crop growth. Procedural disasters (Drought, Heatwave, Storm, Frost, Locust Swarm, Blight) are composed from `DisasterEffect` ScriptableObjects, with COZY weather visuals.
9. **Run map:** A seeded, Slay-the-Spire-style layered node map. One node = one farm day (Farm, Merchant, Disaster, Elite, Rest, Event, Treasure, Boss). Advancing on the map advances the calendar. Every season is **6 days: 5 normal days, then a mandatory boss night** (`season_boss` node, forced on `SeasonCalendar.SeasonBossDays()`); winter's 6th day is the final boss, which still leaves a last bank stop before the year ends (`RunState.endYearAfterBanking`).
10. **Seeds & economy:** Planting consumes a **seed** item (separate from the harvested crop). Runs start with random seeds; more are bought from the merchant with coins earned by selling crops. `PriceTable` is the single price "spreadsheet" (seasonal multipliers, supply/demand drift). The merchant is always at his booth but has **limited stock**: a small starter shelf at run start, restocked (full seeds plus passive charms) only on Merchant map days; on other days you can only buy what is left.
11. **Bank & persistence:** 3 save slots (versioned JSON *save data* in `persistentDataPath`, atomic writes). Each run is a **Year** (Year 1, Year 2, ...). Coins never carry into the next year's purse, which always starts at `RunConfig.startingCoins`. At the end of each season the player may deposit a **limited** amount (`BankConfig.maxDepositPerSeason`, with a fee) into the vault; vault coins buy permanent upgrades. The bank screen can also **withdraw** vault coins back into the purse (`BankLogic.Withdraw`) for a fee whose percentage rises with the amount (`BankConfig.withdrawFee*`: 10 coins -> fee 1, 50 -> fee 10); the purse is still lost at year end, so withdrawing is closed on the last bank stop (`RunState.endYearAfterBanking`). Unspent purse coins are lost when a year ends. When a year ends, the summary screen shows the upgrade shop before the next year starts. **Esc** opens the in-run menu (resume, route map, items, save & exit, abandon year); screens opened mid-day freeze the world.

12. **Day clock:** `DayClock` limits time per session. Daylight counts down (`RunConfig.dayLengthSeconds`, scaled per season by `SeasonData.dayLengthMultiplier`: autumn 0.75, winter 0.6) and night falls by itself when it runs out. At night a second timer (`nightLengthSeconds`) runs; if dawn breaks before the boss is fed the run is lost. The boss only eats at night. `GoldenSlice` mode has no clock.

13. **Season fit & crop strength:** planting out of season is slow and weak. `ClimateConfig.seasonPenaltyExponent` squares sub-1 season affinities, `minGrowth` is 0.12, no crop ever finishes faster than `ClimateConfig.minGrowSeconds` (20 s, enforced by `ClimateModel.CapRate`; base grow times are 22-34 s), and each harvested stack carries a `quality` (0.25-1.25) that scales boss damage and shield charges. **Boss days start at night** (`NodeTypeData.startsAtNight`), so bring crops harvested on earlier days.

14. **Lucky Sprout (permanent unlock):** bought with vault coins on the title screen. It gives a hidden run modifier (`RunModifierData.harvestSeedChance`, `shopItem = false`) so each harvest has a 12% chance to also drop one random seed. The roll is seeded (`HarvestBonus`), so reloading a day can't fish for drops.

15. **Crop roster (2 per season):** Spring peas + lettuce, Summer tomato + corn, Autumn turnip + pumpkin, Winter garlic + spinach (the old Frostberry and the four vault-unlocked season variants were removed; `CropRosterMigration.Apply` does the one-off asset move). Crop kinds (`CropType`): **Projectile** (tomato, pumpkin, garlic: lobbed arc, one crop per throw), **Shooter** (peas, corn: hold left-click to auto-fire a straight bullet toward the cursor, one harvested crop = `CropData.ammoPerCrop` = 10 shots, `fullnessValue` is per bullet, loaded rounds tracked by the pure `CropMagazine` and shown as `loaded / 10` plus spare crops in the bottom-right held-item card, `HeldItemView`, with its wording in the pure `HeldItemText`) and **Support** (lettuce, turnip, spinach: eaten for Bark Shield charges that wear off after `CropData.shieldSeconds` = 20 s, tracked by `PlayerHealth.ShieldTimeLeft`). **Garlic** is a thrown crop with `CropData.scareSeconds = 3`: a hit calls `BossHunger.Scare`, and `CaterpillarBoss` / `ButterflyBoss` flee from the player (no contact damage) for 3 s, then chase again. `UnlockData.unlocksSeeds` is still the single owner of which seeds are locked (`CropAvailability`), though no shipped crop uses it now. `RunState.unlockedIds` snapshots what applies for the year.

16. **Bosses scale by year:** `DifficultyConfig` (single owner) makes every boss stage need more feeding (+25%/year, cap 3x), move faster (+5%/year, cap 1.1x so no stage outruns the farmer) and hit one heart harder every 3 years. `BossScaling.ForYear` is pure; `BossHunger.ApplyScaling` applies it to every stage, and the map tooltip and Items tab show this year's numbers.

17. **Extra farmland:** the field starts as **2×2 plots**. A repeatable, **unlimited** upgrade bought with vault coins (`FarmlandConfig`: base price, growth per owned farmland, so every purchase costs more with no price ceiling; `maxFarmlands` 0 = no cap). Each farmland gives `PlotsPerFarmland` (the starting field's column height) **Farmland Plot items** (`FarmlandPlotItem`, `FarmlandConfig.plotItem`) in the hotbar, and the player places them by hand on open ground (`PlayerPlotPlacer`: green/red ghost, left-click or drag from the hotbar; `[R]` picks an empty placed plot back up). Where plots were put is `MetaProgress.placedPlots` (permanent); plots waiting in the hotbar are derived each day as `FarmlandLogic.UnplacedPlots` (owned minus placed) and are never saved in the inventory. `RunDirector` rebuilds placed plots at day start (`FarmlandSpawner.SpawnAt`) and exposes `TryPlacePlot` / `TryPickUpPlot`; spots snap to the starting field's grid (`FarmlandLogic.SnapToField`) and keep `minPlotSpacing` apart. `MetaProgress.farmlands` is permanent and `RunState.farmlands` is the year's snapshot.

18. **Bigger Stockroom:** a repeatable, **unlimited** upgrade (`StockroomConfig`, vault coins, price rises every level via the shared `TierPricing`, with no ceiling; `maxLevel` 0 = no cap). Each level adds units of every seed on the merchant's shelf (restock and starter shelf), +1 seed kind at level 2 and +1 charm at level 3. `MetaProgress.stockroomLevel` is permanent; `RunState.stockroomLevel` is the year's snapshot.

19. **Bigger Vault:** a repeatable, **unlimited** upgrade that raises the vault limit, so ever-rising upgrade prices stay payable (`BankConfig`: `vaultCap` is level 0, `capGrowth`, `upgradeBasePrice`, `upgradePriceGrowth`; pure rules in `VaultLogic`; `MetaProgress.vaultLevel` is permanent). The price grows slower than the limit, so every level is buyable.

20. **Hotbar & void:** hovering a hotbar slot shows the item's `ItemInfoBuilder` description; dragging a slot off the bar uses it where dropped (seed -> plot, attack crop -> thrown there, defense crop -> eaten; `HotbarView` + `PlayerCombat.TryUseAt`). Having no crops at night no longer loses the run. Falling into the void (`GameManager.voidY`) costs 1 heart: on a boss night you respawn and the fight goes on, otherwise the day ends and the run moves on.

21. **Lost season boss:** losing a season boss (`NodeTypeData.survivableLoss`) does not end the year: no reward, at least 1 heart, the bank stop still happens, and the next season's crops grow slower (`RunState.growthDebuffSeason/Mult`, `lossGrowthMultiplier` 0.65, applied in `DayPlanner`). Losing the final boss or any ordinary day still ends the year.

22. **Demo polish (showcase build):** every crop has 3 Poly Universal Pack growth models (`CropData.growthStages`; peas, turnip, spinach and garlic use the closest-looking pack plant). `FarmTile` raises `Ripened` / `Harvested` / `Withered` (next to `Planted`) and bobs a ripe model; `FarmFx` turns those into dirt, sparkle and leaf particles. `BossReactions` (on the boss) staggers it on big feeds (`BossHunger.Stagger` / `IsStaggered`, read by both boss movers), pops a "!" on `OnScared` and blushes and sweats at `BossData.blushTier` / `stuffedTier`; the feed flash in `GameFeedback` hands back to it through `BossReactions.Reapply`. In-play tips: pure `TipBook` (+ `TipIds`) decides the next tip, `TipsView` raises them from existing events and draws a non-blocking card, `TipData` assets live in `TipRegistry` (`GameDatabase.tips`), and Options has Tips on/off and "Show tips again" (`GameSettings`). Audio: `AudioDirector` plays `AudioLibrary` cues from existing events plus `PlayerCombat.Fired`, `CropProjectile.Landed` and `BellTrigger.Rung`; a cue with no clips is silent, so clips can be dropped into `Assets/Data/Audio/Cue_*.asset` later. All of it is wired by the additive, idempotent `MoFarmSand ▸ Add Demo Polish` (`DemoPolishBuilder.AddDemoPolish`).

`RunDirector.runMode` can be set to `GoldenSlice` to keep the original single-day loop for trailer capture.

### Out of Scope
* Multiple farm layouts or extra gameplay scenes.
* Cloud / cross-device saves, online features, mod support.
* A uGUI / UI Toolkit rewrite (the HUD and run screens stay IMGUI).

---

## 3. Architecture Standards & Implementation Guide (`.agents/doc/`)

All agents operating in this repository must align their design and implementation with the project's engineering principles documented in [`.agents/doc/implementation-guide.md`](file:///F:/Projects/Pitch%20Project/MoFarmSand/.agents/doc/implementation-guide.md).

### Core Principles Translated to Unity:
1. **Declare It, Don't Code It (Data-Driven Base via ScriptableObjects):**
   * **Use ScriptableObjects instead of JSON for templates:** All templates and definitions (crop parameters, growth stages, projectile stats, boss thresholds) must be created as `ScriptableObject` assets (`.asset`), NOT parsed from JSON files. ScriptableObjects allow direct drag-and-drop references to Unity assets (prefabs, materials, meshes, audio clips), native Inspector validation, and zero JSON parsing overhead.
2. **Single Fact Owner:**
   * Every game rule, crop attribute, or stat definition lives in exactly one canonical asset or registry. Other scripts reference or read from that asset rather than maintaining redundant variables.
3. **Registries Instead of Branching:**
   * Avoid giant `switch` or `if/else` ladders for item IDs or enemy types. Use ScriptableObject registries or dictionary lookups indexed by stable identifiers (`CropId`, `EnemyId`).
4. **Events Only to Decouple:**
   * Use typed C# events/actions (`System.Action<T>`) strictly at architectural seams (e.g., notifying the UI HUD, audio manager, or camera shake system when a crop is planted or a boss is fed). Main execution paths (moving, planting, calculating collision) call methods directly.
5. **Hard Boundaries & Pure Core:**
   * Separate gameplay math and state (pure C# logic) from presentation layers (animator controllers, particle systems, camera shakes).

---

## 4. Tech Stack & Engine Conventions

> [!NOTE]
> **Setup after cloning:** open the project with Unity `6000.0.46f1`, then import the Asset Store packs that are git-ignored for size: **Poly Universal Pack** (`Assets/polyperfect/`), **Low Poly Trees Pack** (`Assets/Low Poly Trees Pack/`) and **COZY: Stylized Weather 3** (`Packages/com.distantlands.cozy.core/`). GUIDs match, so scene and prefab references reconnect. `Library/`, `Recordings/` and generated `.csproj`/`.sln` files are also ignored.

* **Engine:** Unity (URP - Universal Render Pipeline).
* **Input:** Unity New Input System (`InputSystem_Actions.inputactions`).
* **Physics:** 3D Physics (`CharacterController` or `Rigidbody` + kinematic colliders).
* **Animation:** Mecanim Humanoid Rigging (tailored for Mixamo imports).
* **Visuals & Capture:**
  * Universal Render Pipeline with post-processing (Vignette, Color Adjustments, Bloom).
  * **COZY: Stylized Weather 3 (Distant Lands):** Day/night cycle transitions, stylized sky/fog, seasons, and weather events.
  * **Cinemachine** for smooth follow and cinematic dolly cameras.
  * **Unity Recorder** for high-resolution video exports.

---

## 5. Architecture & C# Best Practices

### Data-Driven Design: ScriptableObjects (Strictly No JSON for Templates)
Always decouple data from logic. **Use ScriptableObjects instead of JSON for all data templates and definitions.**
* **Why ScriptableObjects over JSON:**
  * Directly links Unity references (`GameObject` prefabs, `Sprite` icons, `Mesh`, `AudioClip`) in the Inspector without messy `Resources.Load` or asset path strings.
  * Instant type safety and compile-time error checking.
  * Real-time tuning during Play mode in the Unity Editor.
* **Core ScriptableObject Templates to Use:**
  * **CropData (`CropData : ScriptableObject`):** Crop name, seed mesh, growth stage prefabs, harvest yield, combat effect (projectile speed, damage/fullness value).
  * **BossData (`BossData : ScriptableObject`):** Name, target fullness threshold, movement speed, attack rate.
  * **CropRegistry / Database (`CropRegistry : ScriptableObject`):** Central collection of all crop templates for lookups.

### Run Systems Templates & Save Data
* **JSON is allowed only for runtime *save data*** (`SaveFile` via `JsonUtility`, in `persistentDataPath/saves`). All templates stay ScriptableObjects; saves reference them by stable id through registries (`ItemRegistry`, `DisasterRegistry`, `NodeTypeRegistry`, ...), all reachable from `GameDatabase`.
* **Additional ScriptableObject templates:** `SeasonData`, `CalendarConfig`, `ClimateConfig`, `SeedData`, `DisasterData` + `DisasterEffect` strategies, `NodeTypeData`, `MapGenConfig`, `PriceTable` (the single owner of prices), `EconomyConfig`, `BankConfig`, `RunConfig`, `RunModifierData`, `RunEffect` strategies, `EventData`, `UnlockData`, `CozyWeatherMap`.
* **`Assets/Scripts/Sim/` must stay pure:** no MonoBehaviour, `UnityEngine.Random`, frame time, COZY, scene lookups or file IO (except `FileSaveStore`). `DataDriftTests.SimFolderStaysPure` enforces this.

### Code Style Guidelines
* **Naming Conventions:**
  * PascalCase for Classes, Enums, Structs, and public Methods/Properties (`CropController`, `FeedBoss()`).
  * camelCase for local variables and parameters (`cropData`, `targetTransform`).
  * `[SerializeField] private` with `_camelCase` or camelCase for Inspector-exposed fields:
    ```csharp
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private CropData activeCrop;
    ```
* **No "God Objects":** Keep scripts modular:
  * `PlayerMovement.cs` (Handles input, moving, rotating).
  * `PlayerFarming.cs` (Handles soil interaction, planting, harvesting).
  * `PlayerCombat.cs` (Handles throwing crops, aiming).
  * `FarmTile.cs` (Tracks tile state: Empty, Planted, Growing, Harvestable).
  * `BossHunger.cs` (Tracks fullness meter, triggers win state when full).

### Unity Performance & Cleanliness
* Avoid calling `GetComponent<T>()` or `FindObjectOfType<T>()` in `Update()`. Cache references in `Awake()` or `Start()`.
* Use Unity events or C# actions (`System.Action`) to decouple UI and gameplay (e.g., `OnBossFed?.Invoke(currentHunger, maxHunger)`).
* Keep the hierarchy clean with parent empty GameObjects (e.g., `--- ENVIRONMENT ---`, `--- LIGHTING ---`, `--- MANAGERS ---`).

---

## 6. Folder Structure Standard

Keep `Assets/` and documentation organized following this layout:

```text
MoFarmSand/
├── .agents/
│   └── doc/              # Core architecture guides (implementation-guide.md, tasks/)
├── agents.md             # Project roadmap, guardrails & MCP instructions
├── Assets/
│   ├── Art/
│   │   ├── Animations/   # Mixamo FBX clips and Animator Controllers
│   │   ├── Characters/   # Dog, Boss, Merchant models & textures
│   │   ├── Environment/  # Farm props, soil tiles, fences, trees
│   │   ├── Materials/    # URP Toon / Stylized materials
│   │   └── UI/           # Icons, hunger meters, dialogue frames
│   ├── Prefabs/
│   │   ├── Crops/        # Projectiles, harvested items, growth meshes
│   │   ├── Characters/   # Dog farmer prefab, Boss prefab
│   │   └── Systems/      # Managers, lighting rigs, virtual cameras
│   ├── Scenes/
│   ├── Settings/         # URP & Graphics settings
│   └── Scripts/
│       ├── Characters/   # Player & Boss logic
│       ├── Data/         # ScriptableObject definitions (seasons, disasters, map, economy, registries)
│       ├── Farming/      # Tiles, planting, harvesting
│       ├── Run/          # RunDirector, FarmConditions, DisasterDirector, CozyBridge, merchant stall
│       ├── Sim/          # PURE C# core: seeded RNG, calendar/climate, map generator, economy, bank, save
│       └── UI/           # HUD, fullness bars, prompts, IMGUI run screens (title, map, shop, bank)
```

---

## 7. Guidance for Autonomous Agents & AI Helpers

1. **Verify Before Overwriting:** Always check if a script or prefab already exists in `Assets/Scripts/` or `Assets/Prefabs/` before creating new ones.
2. **Non-Destructive Changes:** When modifying existing mechanics, preserve working input bindings and Animator parameter names.
3. **Keep It Visual:** If asked to implement a feature, always consider how it will look in the camera frame for the pitch video (particle bursts, squash & stretch, camera shakes).

---

## 8. MCP (Model Context Protocol) Integration

### 8.1 Unity Editor MCP Server
When interacting with this project via an active **Unity MCP Server**, agents have direct programmatic access to the running Unity Editor. Follow these operating protocols:

* **Scene & Hierarchy Inspection:** Query the active scene hierarchy, inspect GameObject transforms, parent-child relationships, and active components before writing new scripts.
* **Component & Property Configuration:** Modify component serialized fields, assign Material references, configure Cinemachine Virtual Cameras, and hook up UI elements directly in the editor.
* **Console Monitoring & Debugging:** Check the Unity Console logs via MCP immediately after script compilation or Play mode runs to detect compilation errors, warnings, and `NullReferenceExceptions`.
* **Play Mode Verification:** Trigger Play/Pause mode to verify mechanics (e.g. testing crop throwing, camera follow, boss fullness progression).
* **Asset Refresh & Compilation:** Trigger `AssetDatabase.Refresh()` after generating or updating `.cs` scripts to ensure Unity completes domain reloading without syntax errors.

#### MCP Safety & Guardrails
1. **Save Before Batch Edits:** Always invoke scene and asset save commands (`EditorSceneManager.SaveOpenScenes`, `AssetDatabase.SaveAssets`) before performing structural scene alterations.
2. **Support Undo:** When generating editor utilities or modifying scene objects via MCP, wrap mutations with `Undo.RecordObject` to protect the artist/designer's working state.
3. **Prefab Discipline:** Prefer instantiating and editing Prefabs in Prefab Mode or staging areas rather than creating loose, unpackaged scene GameObjects.
4. **Never Lock Play Mode:** When running automated Play mode tests via MCP, ensure a timeout or safety exit is in place so the Editor does not hang indefinitely.

---

### 8.2 COZY: Stylized Weather Documentation MCP
* **Endpoint URL:** `https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/~gitbook/mcp`
* **Purpose:** Live documentation retrieval for **COZY: Stylized Weather 3** (Distant Lands), the core environment package used for daytime farming ambiance, seasonal progression, and creepy nighttime boss atmosphere.
* **Key Topics Covered via this MCP:**
  * **Day/Night Cycle & Time:** Setting up `CozyWeather.instance`, controlling `CozyTime` (hours, ticks, time of day speed), and triggering transition curves.
  * **Weather Profiles & Events:** Configuring weather profiles (e.g., sunny spring, fog, rain, storm), swapping profiles dynamically via code, and hooking into weather events.
  * **Seasons Module:** Configuring seasonal parameters (Spring, Summer, Autumn, Winter) and temperature/precipitation curves.
  * **Ambience & Lighting:** Styling fog density, volumetric light rays, star filters, and stylized cloud shaders to achieve the "creepy cute" tone shift.
* **MCP Client Setup (e.g. Claude Desktop / Cursor / Antigravity):**
  ```json
  {
    "mcpServers": {
      "cozy-weather-docs": {
        "url": "https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/~gitbook/mcp"
      }
    }
  }
  ```
  *(Agents can query this server directly to look up exact C# API methods, property names, and configuration schemas without hallucinating parameters).*

---

### 8.3 Headless Unity (Batch Mode)
Use headless Unity to compile, build the Golden Slice, or run tests **when the Unity Editor is NOT open on this project** (and no Unity MCP connection is available). Prefer the Unity MCP (8.1) whenever the editor is already open.

> [!WARNING]
> Unity locks a project to one process. If `Temp/UnityLockfile` is held (the Editor is open), batch mode on the same project path fails. Check first: `Get-Process Unity`. Never kill the user's open Editor to run headless.

* **Editor path (this machine):** `D:\unity\6000.0.46f1\Editor\Unity.exe` (must match `ProjectSettings/ProjectVersion.txt`).
* **Required flags:** `-batchmode -nographics -quit -projectPath "<project>" -logFile "<file>"`. Always write to a log file and read it afterwards; batch mode prints nothing to the console.
* **Compile check only:**
  ```powershell
  & "D:\unity\6000.0.46f1\Editor\Unity.exe" -batchmode -nographics -quit `
    -projectPath "F:\Projects\Pitch Project\MoFarmSand" -logFile "$env:TEMP\unity-compile.log"
  ```
  Then search the log for `error CS` and `Compilation failed`.
* **Build the Golden Slice (assets, prefabs, scene):** call the public static builder method.
  ```powershell
  & "D:\unity\6000.0.46f1\Editor\Unity.exe" -batchmode -nographics -quit `
    -projectPath "F:\Projects\Pitch Project\MoFarmSand" `
    -executeMethod GoldenSliceBuilder.Build -logFile "$env:TEMP\unity-build.log"
  ```
  `GoldenSliceBuilder.RebuildScene` **overwrites the whole scene and wipes hand-added content (e.g. COZY)**; prefer the additive, idempotent `Batch2Builder.AddBatch2` for scene changes.
* **Add the run systems (assets + scene wiring, additive and idempotent):**
  ```powershell
  & "D:\unity\6000.0.46f1\Editor\Unity.exe" -batchmode -nographics -quit `
    -projectPath "F:\Projects\Pitch Project\MoFarmSand" `
    -executeMethod RunSystemsBuilder.AddRunSystems -logFile "$env:TEMP\unity-runsystems.log"
  ```
  It only creates assets that don't exist yet, so tuned values survive re-runs. Set `RunDirector.runMode` to `GoldenSlice` to restore the original single-day trailer loop.
* **Camera & capture:** gameplay camera is Cinemachine (`CM_Gameplay`); trailer shots are `CM_*` cameras driven by `TrailerDirector` (press `T` in Play mode). Record with the menu **MoFarmSand ▸ Record Trailer Take** (Unity Recorder, H.264 MP4 into `Recordings/`).
* **Tests:** EditMode tests live in `Assets/Tests/EditMode`. Run with MCP `run_tests`, or headless: `-runTests -testPlatform EditMode -testResults "$env:TEMP\results.xml"`.
* **Tests (if a test assembly exists):** add `-runTests -testPlatform EditMode -testResults "$env:TEMP\results.xml"` (drop `-quit`).
* **Exit codes:** `0` success; non-zero means failure, so inspect the log.
* **Rules:**
  1. Any new automation entry point must be `public static` in an `Editor/` script, so it can be used with `-executeMethod`.
  2. Run it in the background or with a timeout so it cannot hang the session (see 8.1 "Never Lock Play Mode").
  3. Report a headless check that could not run (e.g. Editor open) as **blocked**, never as passed.



