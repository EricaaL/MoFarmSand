# MoFarmSand

A creepy-cute roguelike / farming sim prototype (working titles: *Cursed Rancher*, *The Miracle Berry*, *Dogsend*). You play a canine farmer defending an enchanted farm, using crops as weapons against gluttonous pests. Think *Stardew Valley* meets *Cult of the Lamb* / *Atomicrops*.

The signature mechanic is the **inverted boss fight**: you don't chip a health bar down. You feed the boss crops until its fullness meter fills, and it bursts and evolves (caterpillar to butterfly).

Built for the DIGI 237: The Art of Storytelling pitch (pitch deck plus a rendered gameplay trailer). Target audience: 13+.

## Team

| Name | Role |
|---|---|
| Alley | Producer, market research, slides and pitch deck, video editing |
| Erica | 3D environment and crop props, UI design, Unity setup and integration |
| Elisha | Character and boss design (dog farmer, caterpillar, merchant), storyboard trailer |

## How it plays

- **Farm by day.** Plant seeds, wait for crops to ripen, harvest them, sell some to the merchant. The day clock runs down and the bell rings by itself when daylight is gone.
- **Feed the boss at night.** Hit it with crops until it is full. If dawn breaks first, the year is lost.
- **Crop types.** Projectile crops are lobbed (tomato, pumpkin, garlic). Shooter crops auto-fire (peas, corn). Support crops give a Bark Shield (lettuce, turnip, spinach).
- **A year is 4 seasons of 6 days:** 5 normal days, then a boss night. Days are picked on a seeded, Slay-the-Spire-style route map (farm, merchant, disaster, event and more).
- **Seasons and disasters.** Planting out of season is slow and weak. Droughts, storms, locusts, blizzards and more can strike.
- **Bank and upgrades.** Coins you deposit at the end of a season buy permanent upgrades (farmland, stockroom, vault size). Unspent coins are lost when the year ends.

### Controls

| Input | Action |
|---|---|
| W A S D | Move |
| 1-8 / mouse wheel | Pick a hotbar slot |
| E | Interact (plant, harvest, trade, ring the bell) |
| Left-click | Throw (hold to aim) or fire the held crop |
| Right-click | Eat a support crop |
| R | Pick an empty placed farmland plot back up |
| Tab | Inventory |
| Esc | Pause menu |
| T | Trailer cameras (in Play mode) |

## Setup

1. Install **Unity 6000.0.46f1** (see `ProjectSettings/ProjectVersion.txt`). The project uses URP and the new Input System.
2. Clone the repo and import the Asset Store packs that are git-ignored for size. GUIDs match, so scene and prefab references reconnect:
   - Poly Universal Pack (`Assets/polyperfect/`)
   - Low Poly Trees Pack (`Assets/Low Poly Trees Pack/`)
   - Cartoon Grass (`Assets/CartoonGrass/`)
   - Low Poly Fantasy Bundle (`Assets/LowPolyFantasyBundle/`)
   - COZY: Stylized Weather 3 (`Packages/com.distantlands.cozy.core/`)
3. Optional: copy the **Beautiful Magic** font (`BeautifulMagic.ttf`) into `Assets/Art/UI/Fonts/`. It is personal-use only and git-ignored. Without it, headings fall back to Open Sans Bold.
4. Open the project and the scene `Assets/Scenes/GoldenSliceScene.unity`, then press Play.

`Library/`, `Recordings/` and generated `.csproj` / `.sln` files are ignored.

## Project layout

```text
Assets/
  Art/          Models, animations, materials, UI art
  Audio/        Music and sound effects
  Data/         ScriptableObject assets (crops, seasons, events, prices, tips...)
  Editor/       Builders and menu tools (MoFarmSand menu)
  Prefabs/      Crops, characters, FX, systems
  Scenes/       GoldenSliceScene (main), BetaTestScene, TrailerEpilogueScene
  Scripts/
    Sim/        Pure C# rules: seeded RNG, calendar, map, economy, bank, save
    Run/        RunDirector, disasters, COZY bridge, merchant
    Farming/    Plots and crops
    Player/     Movement, combat, health
    Boss/       Hunger meter, evolution, reactions
    UI/         IMGUI screens and HUD
  Tests/        EditMode tests
.agents/doc/    Architecture guide and task notes
```

### Architecture in short

- **ScriptableObjects, not JSON, for all templates** (crops, bosses, seasons, events, prices). JSON is used only for save data.
- **One owner per fact.** A rule or stat lives in exactly one asset or registry.
- **`Assets/Scripts/Sim/` stays pure:** no MonoBehaviours, `UnityEngine.Random`, frame time, COZY or scene lookups. A test enforces it.
- **Events only at seams** (HUD, audio, camera). The main game path calls methods directly.
- The UI is IMGUI and stays that way.

More detail is in [`AGENTS.md`](AGENTS.md) and [`.agents/doc/implementation-guide.md`](.agents/doc/implementation-guide.md).

## Editor tools

Most setup is additive and idempotent, under the **MoFarmSand** menu (for example *Add Demo Polish*, *Add UI Theme*, *Add Blizzard*, *Add Chocobo*). Re-running one keeps values you have tuned.

Trailer capture: gameplay uses Cinemachine (`CM_Gameplay`), trailer shots are `CM_*` cameras driven by `TrailerDirector`. Use **MoFarmSand > Record Trailer Take** to record an H.264 MP4 into `Recordings/` with Unity Recorder.

## Tests

EditMode tests live in `Assets/Tests/EditMode`. Run them from the Unity Test Runner, or headless with the editor closed:

```powershell
& "<Unity 6000.0.46f1>\Editor\Unity.exe" -batchmode -nographics -projectPath "<project>" `
  -runTests -testPlatform EditMode -testResults "$env:TEMP\results.xml" -logFile "$env:TEMP\tests.log"
```

## Scope

This is a prototype built to power a pitch video and showcase, not a feature-complete commercial game. Multiple farm layouts, online features and mod support are out of scope.

## Credits

Art packs, fonts and weather tools are third-party assets used under their own licences (see the in-game Credits page). Animations are from Mixamo.
