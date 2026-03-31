# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

COMP3329 is a 2D side-scrolling platformer built in Unity using the **Pixel Adventure 1** asset pack. The player character is "Pink Man" with double-jump mechanics.

- **Unity version**: Check Unity Hub (project name "3329", product GUID `86551d87a31f64b4989a18e0a6182906`)
- **Render pipeline**: Built-in (Linear color space, orthographic 2D camera)
- **Target resolution**: 1920×1080

## Common Editor Tasks

All custom automation lives under the **Tools** menu in the Unity Editor:

| Menu Item | Script | What it does |
|---|---|---|
| `Tools/Setup PinkMan Animations` | `Assets/Editor/PinkManAnimationSetup.cs` | Creates/rebuilds all animation clips and the `PinkMan.controller` animator |
| `Tools/Setup Level1 Scene` | `Assets/Editor/Level1Setup.cs` | Wires player, camera, GameManager references in the active scene |
| `Tools/Build Terrain Visuals` | `Assets/Editor/TerrainBuilder.cs` | Rebuilds Ground, Platform1, Platform2 from terrain sprites |
| `Tools/Setup Build Settings` | `Assets/Editor/BuildSettingsSetup.cs` | Sets scene build order (MainMenu→Level1→LevelComplete) |
| `Tools/Setup Main Menu Scene` | `Assets/Editor/MainMenuSetup.cs` | Full main menu canvas/button wiring; includes BFS background removal on sprites |
| `Tools/Setup EndPoint Sprites & Animation` | `Assets/Editor/EndPointSetup.cs` | Slices checkpoint sprite sheet and creates Idle/Pressed animator |
| `Tools/Setup Phase3 Objects` | `Assets/Editor/Phase3Setup.cs` | Places Spikes, MovingSaw, MovingPlatform, Enemy in Level1 |
| `Tools/Setup Phase3 Sprites` | `Assets/Editor/Phase3SpriteSetup.cs` | Assigns sprites to Phase3 obstacles after creation |
| `Tools/Setup Terrain Sprites` | `Assets/Editor/TerrainSpriteSetup.cs` | Applies correct surface/underground sprites to platform tiles |
| `Tools/Fix Platform Tiles` | `Assets/Editor/FixPlatformTiles.cs` | Repairs tile positions and recalculates BoxCollider2D from `tile_X_Y` naming |
| `Tools/Setup Background` | `Assets/Editor/BackgroundSetup.cs` | Creates parallax mountain background prefab (`Assets/Prefabs/Background.prefab`), sets camera sky-blue, adds `Background` sorting layer |
| `Tools/Rebuild Cave Ground` | `Assets/Editor/RebuildCaveGround.cs` | Rebuilds all scene `Ground*` objects with cave-style terrain sprites (20×30 tile grid) and recalculates their `BoxCollider2D` |
| `Tools/Setup Cave Tilemap Prefab` | `Assets/Editor/CaveTilemapSetup.cs` | Creates `Assets/Prefabs/Environment/CaveTilemapGrid.prefab` (Grid + Tilemap_Background + Tilemap_Ground + Tilemap_Ground_Coll) |
| `Tools/Setup Cave Tilemap in Level1` | `Assets/Editor/CaveTilemapSetup.cs` | Instantiates the cave tilemap prefab in the active scene (Grid scale 0.16), then scans old `Ground*`/`Platform*` colliders and paints `CaveTerrainTiles_64` over the same world-space area |
| `Tools/Rebuild Cave Tile Assets` | `Assets/Editor/RebuildCaveTileAssets.cs` | Fixes pink/magenta tile assets by updating stale `m_Sprite` fileID references in-place (preserves GUIDs) after a texture reimport regenerates sub-sprite IDs |
| `Tools/Fix Cave Tile File IDs` | `Assets/Editor/FixCaveTileFileIDs.cs` | Parses `internalIDToNameTable` from `.meta` files and rewrites tile YAML with correct fileIDs; deletes `.asset` files but keeps `.meta` so GUIDs are preserved |
| `Tools/Fix Cave Tile Sprites` | `Assets/Editor/FixCaveTileSprites.cs` | Force-reimports cave textures then reassigns sprites on tile assets; use when tiles show as pink after texture changes |

`AutoSave.cs` (`[InitializeOnLoad]`) automatically saves scenes before entering Play Mode — no menu item needed.

To enter Play Mode from the MCP: use `mcp__coplay-mcp__play_game` / `mcp__coplay-mcp__stop_game`.

## Running Tests

Tests use the Unity Test Runner (Window > General > Test Runner in the Editor).

- **EditMode** (`Assets/Tests/EditMode/`): `GestureClassifierTests` — pure math tests, no scene required.
- **PlayMode** (`Assets/Tests/PlayMode/`): `GestureIntegrationTests` — requires entering Play Mode.

`GestureClassifier` exposes static per-gesture methods (`ClassifyFist`, `ClassifyOpenPalm`, `ClassifyShoot`, `ClassifyLift`) that can be unit-tested directly with synthetic landmark arrays.

## Architecture

### Scene Build Order

| Index | Scene | Purpose |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | Title screen with Start/Quit |
| 1 | `Assets/Scenes/Level1.unity` | Main gameplay level |
| 2 | `Assets/Scenes/LevelComplete.unity` | Completion/transition screen |
| — | `Assets/Scenes/SampleScene.unity` | Scratch/testing (not in build) |
| — | `Assets/Scenes/GestureTestScene.unity` | Gesture recognition testing (not in build) |

### Static Game State (`GameData.cs`)

Not a MonoBehaviour — plain static class. Holds `CurrentLevel`, `Lives`, `Score`. Call `GameData.Reset()` at game start (done by `MainMenu.cs`).

### Runtime Scripts (`Assets/Scripts/`)

- **`GameManager.cs`** — Singleton (`GameManager.Instance`). Holds the `respawnPoint` Transform and handles scene reloading (`RestartLevel` reloads current scene; `LoadNextLevel` loads by build index). Must exist in every gameplay scene.
- **`PlayerController.cs`** — Requires `Rigidbody2D` + `Animator`. Reads `Input.GetAxisRaw("Horizontal")` and `Input.GetButtonDown("Jump")`. Supports double-jump (`maxJumpCount = 2`). Jump system uses **gravityScale modulation** (not velocity addition): `rb.gravityScale` is multiplied by `fallGravityMultiplier` (2.5f) when falling and by `lowJumpMultiplier` (2f) when rising without the button held. **Jump cut** (`jumpCutMultiplier = 0.45f`) bleeds upward velocity on early release. On death respawns after 1.5 s via `GameManager.Instance.GetRespawnPoint()`. `LockInput()` freezes the player without triggering Die animation. Ground detection uses `Physics2D.OverlapCircle` on a child `GroundCheck` Transform against the **Ground** layer.
- **`CameraFollow.cs`** — Smooth lerp follow in `LateUpdate`. Optional `fixedY`, `useBounds` clamping (minX/maxX/minY/maxY), configurable `smoothSpeed` (default 5f).
- **`DeathZone.cs`** — Any 2D trigger collider; calls `player.Die()` on overlap.
- **`EndPoint.cs`** — Trigger that calls `player.LockInput()`, sets `GameData.CurrentLevel`, then loads `"LevelComplete"` scene after 0.5 s. Swaps sprite Idle→Pressed on activation. Has a `triggered` guard to prevent re-entry.
- **`LevelComplete.cs`** — UI controller for the LevelComplete scene. `NextLevel()` increments `GameData.CurrentLevel` and loads by build index. `GoToMainMenu()` loads scene index 0.
- **`WinPanel.cs`** — Alternative win-panel UI; uses `transform.Find()` instead of `GameObject.Find()`, resets `Time.timeScale = 1f` before loading.
- **`MainMenu.cs`** — Primary main menu script. Calls `GameData.Reset()` on Start, loads Level1 by build index (`firstLevelIndex = 1`). Auto-finds Start/Quit buttons by name.
- **`MainMenuController.cs`** — Alternative main menu script (does NOT call `GameData.Reset()`). Loads by scene name `"Level1"`. Wires buttons in `Awake` with `RemoveAllListeners`. Has `#if UNITY_EDITOR` quit handler.
- **`Enemy.cs`** — Patrol enemy. Walks left/right, flips at edges (downward raycast). Stomp detection: contact normal + falling velocity (`< 0.2f`) — bounces player up 8 f/s and destroys self after 0.3 s. Side contact kills player.
- **`MovingPlatform.cs`** — Lerps between `pointA` / `pointB`. Parents the player to the platform on contact so they ride it; un-parents on exit.
- **`MovingSaw.cs`** — Rotates continuously while lerping between `pointA` / `pointB`. IsTrigger — kills player on enter.
- **`Spike.cs`** — Static trigger collider; kills player on enter.
- **`ParallaxLayer.cs`** — Attach to any `SpriteRenderer` GameObject. Spawns a seamless tile copy at runtime and scrolls it based on `parallaxFactor` (0 = world-fixed / full parallax effect; 1 = camera-locked / no effect). Supports `autoScrollSpeed` for constant drift (e.g. drifting clouds) and `lockYToCamera` to pin the layer's bottom edge to the camera bottom regardless of vertical camera movement.

### Gesture Recognition Subsystem (`Assets/Scripts/GestureRecognition/`)

Camera-based gesture input layer built on MediaPipe. Pipeline: Camera → `MediaPipeBridge` → `GestureClassifier` → `GestureEvents`.

**Detection layer** (internal — do NOT add to GameObjects manually; `GestureService` creates these at runtime):
- **`MediaPipeBridge`** — Wraps all MediaPipe Unity Plugin calls behind `#if MEDIAPIPE_INSTALLED`. When the plugin is absent, a stub compiles in so the rest of the project builds and mock data still flows through for UI/testing. Produces `HandLandmarkData` structs (21 `Vector3` landmarks, normalized [0,1]).
- **`CameraManager`** — Manages `WebCamTexture` lifecycle; exposes `CameraTexture` (assign to `RawImage.texture`) and `GetCurrentFrame()` for per-frame CPU readback.
- **`HandTracker`** — Pure-logic class (no `MonoBehaviour`). Derives a smoothed palm-center `Vector2` from landmarks via exponential lerp (`smoothingFactor`, default 0.5). Reset when hand is lost.

**Service / consumption layer**:
- **`GestureService`** — Singleton facade; add to a GameObject, assign a `GestureConfig` asset, call `StartRecognition()` or enable Auto Start. Three consumption options: singleton polling (`Instance.CurrentResult`), event subscription (`GestureEvents.OnGestureChanged`), or per-frame event (`GestureEvents.OnGestureUpdated`).
- **`GestureResult`** — Immutable `readonly struct` passed to event subscribers. Fields: `Type`, `Confidence` [0,1], `HandPosition` ((-1,-1) when no hand), `IsHandDetected`, `Timestamp`. Use `GestureResult.Empty` as a sentinel.
- **`GestureClassifier`** — Classifies from 21 normalized MediaPipe landmarks. Supports `RegisterClassifier()` for custom gestures.
- **`GestureConfig`** — ScriptableObject with `ConfidenceThreshold` and per-gesture sprite mappings.
- **`GesturePanelManager`** / **`GestureOverlay`** / **`GestureDisplayPanel`** — UI feedback layer (in `Service`/`UI` sub-namespaces).

**Current `GestureType` enum values**: `None`, `Push`, `Lift`, `Shoot`, `Fist`, `OpenPalm`.

**To add a new gesture**:
1. Add a value to `GestureType` enum (`Assets/Scripts/GestureRecognition/Core/GestureType.cs`) before `Count`.
2. Add classification logic in `GestureClassifier.cs`.
3. Add a `GestureEntry` in the `GestureConfig` ScriptableObject (Inspector).

### Animator Parameters (PinkMan.controller)

| Parameter | Type | Description |
|---|---|---|
| `Speed` | Float | `Mathf.Abs(moveInput)` — drives Idle↔Run |
| `IsGrounded` | Bool | Ground check result — drives ground/air transitions |
| `VelocityY` | Float | `rb.velocity.y` — distinguishes Jump vs Fall |
| `Die` | Trigger | Any-state → Hit animation |

### Key Asset Paths

- Player sprites: `Assets/Pixel Adventure 1/Assets/Main Characters/Pink Man/`
- Terrain sprites: `Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png` (16 px tiles, PPU=100 → `tileSize = 0.16f`)
- Animation clips & controller: `Assets/Animations/`
- Background images: `Assets/Pixel Adventure 1/Assets/Background/`
- Cave tile assets: `Assets/CaveAssets/Tiles/` (textures), `Assets/CaveAssets/Scenes/tiles/` (`.asset` tile objects)
- Cave tilemap prefab: `Assets/Prefabs/Environment/CaveTilemapGrid.prefab`
- End checkpoint sprites: `Assets/Pixel Adventure 1/Assets/Items/Checkpoints/End/`
- UI button textures: `Assets/Textures/` (processed versions with transparent backgrounds)

### Physics Setup

- Player `Rigidbody2D`: `gravityScale = 3` (base), Continuous collision detection, Freeze Z rotation. **gravityScale is modulated at runtime** by `PlayerController`.
- Player `BoxCollider2D`: size `(0.2, 0.3)`, offset `(0, 0)`
- Platforms use a single parent `BoxCollider2D` sized to the full tile grid; individual tile child objects are visual-only `SpriteRenderer`s named `tile_X_Y`
- Ground objects must be on the **Ground** layer for `PlayerController` ground detection to work
