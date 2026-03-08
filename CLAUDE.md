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

To enter Play Mode from the MCP: use `mcp__coplay-mcp__play_game` / `mcp__coplay-mcp__stop_game`.

## Architecture

### Runtime Scripts (`Assets/Scripts/`)

- **`GameManager.cs`** — Singleton (`GameManager.Instance`). Holds the `respawnPoint` Transform and handles scene reloading (`RestartLevel`, `LoadNextLevel` by build index). Must exist in every gameplay scene.
- **`PlayerController.cs`** — Requires `Rigidbody2D` + `Animator`. Reads `Input.GetAxisRaw("Horizontal")` and `Input.GetButtonDown("Jump")`. Supports double-jump (`maxJumpCount = 2`). Jump system uses **gravityScale modulation** (not velocity addition): `rb.gravityScale` is multiplied by `fallGravityMultiplier` when falling and by `lowJumpMultiplier` when rising without the button held. A **jump cut** (`jumpCutMultiplier`) immediately bleeds upward velocity on early button release. On death calls `GameManager.Instance.GetRespawnPoint()` and respawns after 1.5 s. Exposes `LockInput()` for external scripts (e.g. EndPoint) to freeze the player without triggering the Die animation. Ground detection uses `Physics2D.OverlapCircle` on a child `GroundCheck` Transform against the **Ground** layer.
- **`CameraFollow.cs`** — Smooth lerp follow in `LateUpdate`. Optional `useBounds` clamping (minX/maxX/minY/maxY).
- **`DeathZone.cs`** — Any 2D trigger collider; calls `player.Die()` on overlap.
- **`EndPoint.cs`** — _(teammate-owned, pull before editing)_ Trigger that calls `player.LockInput()` then `GameManager.Instance.LoadNextLevel()` after a delay. Swaps sprite from idle → pressed on activation.

### Animator Parameters (PinkMan.controller)

| Parameter | Type | Description |
|---|---|---|
| `Speed` | Float | `Mathf.Abs(moveInput)` — drives Idle↔Run |
| `IsGrounded` | Bool | Ground check result — drives ground/air transitions |
| `VelocityY` | Float | `rb.velocity.y` — distinguishes Jump vs Fall |
| `Die` | Trigger | Any-state → Hit animation |

### Scenes

- `Assets/Scenes/SampleScene.unity` — scratch/testing scene
- `Assets/Scenes/Level1.unity` — main game level; use `Tools/Setup Level1 Scene` after initial setup
- `Assets/Pixel Adventure 1/Scenes/Demo.unity` — asset pack demo (do not edit)

### Key Asset Paths

- Player sprites: `Assets/Pixel Adventure 1/Assets/Main Characters/Pink Man/`
- Terrain sprites: `Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png` (16 px tiles, PPU=100 → `tileSize = 0.16f`)
- Animation clips & controller: `Assets/Animations/`
- Background images: `Assets/Pixel Adventure 1/Assets/Background/`
- End checkpoint sprites: `Assets/Pixel Adventure 1/Assets/Items/Checkpoints/End/`

### Physics Setup

- Player `Rigidbody2D`: `gravityScale = 3` (base), Continuous collision detection, Freeze Z rotation. **gravityScale is modulated at runtime** by `PlayerController` — do not assume it stays at 3 during play.
- Player `BoxCollider2D`: size `(0.2, 0.3)`, offset `(0, 0)`
- Platforms use a single parent `BoxCollider2D` sized to the full tile grid; individual tile child objects are visual-only `SpriteRenderer`s
- Ground objects must be on the **Ground** layer for `PlayerController` ground detection to work
