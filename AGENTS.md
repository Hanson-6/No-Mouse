# AGENTS.md

This file provides guidance for AI coding agents operating in this repository.

## Project Overview

- **Type:** Unity 2D game project
- **Engine:** Unity (Tuanjie Hub fork) 2022.3.62f3c1 LTS
- **Language:** C# (.NET Standard 2.1)
- **Template:** `com.unity.template.2d@7.0.4`
- **Render Pipeline:** Built-in Render Pipeline (not URP/HDRP)
- **Input:** Legacy Input Manager (`UnityEngine.Input`)
- **Editor Path:** `E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`

## Directory Structure

```
Assets/           # All user-authored content (scripts, scenes, prefabs, sprites, etc.)
Packages/         # UPM package manifest and lock file
ProjectSettings/  # Version-controlled project configuration
UserSettings/     # Per-user editor preferences (not version-controlled)
Library/          # Editor-generated cache (never edit manually, not version-controlled)
Logs/             # Editor log files
Temp/             # Temporary runtime files
```

Place all C# scripts under `Assets/Scripts/` (create subdirectories as needed).
Place all test scripts under `Assets/Tests/` with separate EditMode and PlayMode folders.
Place scenes in `Assets/Scenes/`.

## Build Commands

Unity projects are built from the Unity Editor or via command-line batch mode.

### Open project in editor
```
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST"
```

### Run all Edit Mode tests (command line)
```
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults-EditMode.xml ^
  -testPlatform EditMode
```

### Run all Play Mode tests (command line)
```
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults-PlayMode.xml ^
  -testPlatform PlayMode
```

### Run a single test or test class (by filter)
```
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -runTests -batchmode -nographics ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -testResults TestResults.xml ^
  -testPlatform EditMode ^
  -testFilter "MyTestClassName.MyTestMethodName"
```

The `-testFilter` flag accepts NUnit-style filters: a fully qualified test name, class name, or partial match. Examples:
- `-testFilter "PlayerTests"` runs all tests in the `PlayerTests` class
- `-testFilter "PlayerTests.TestHealth"` runs a single test method
- `-testFilter "Movement"` runs all tests whose name contains "Movement"

### Build the project (standalone Windows)
```
"E:/Application/Develop/Software/Tuanjie_Hub/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" ^
  -batchmode -nographics -quit ^
  -projectPath "E:/Application/Develop/Software/Tuanjie_Hub/Project/COMP3329_TEST" ^
  -buildWindows64Player "Builds/Game.exe"
```

## Test Framework

- **Framework:** Unity Test Framework 1.1.33 (NUnit-based)
- **Package:** `com.unity.test-framework`
- Tests require assembly definition files (`.asmdef`) with `"testAssemblies": true`
- Edit Mode tests go in `Assets/Tests/EditMode/` with an `.asmdef` referencing `UnityEngine.TestRunner` and `UnityEditor.TestRunner`
- Play Mode tests go in `Assets/Tests/PlayMode/` with an `.asmdef` referencing `UnityEngine.TestRunner`
- Use `[Test]` for synchronous tests, `[UnityTest]` for coroutine-based tests
- Results are output as NUnit XML to the path specified by `-testResults`

## Code Style Guidelines

### File Organization
- One MonoBehaviour/class per file; filename must match the class name exactly
- Every `.cs` file in Assets must have a corresponding `.meta` file (Unity generates these)
- Never manually edit `.meta` files unless fixing GUIDs

### Naming Conventions
- **PascalCase**: Classes, structs, enums, methods, properties, public fields, constants, events
- **camelCase with underscore prefix**: Private and protected fields (`_health`, `_moveSpeed`)
- **camelCase**: Local variables, method parameters
- **IPascalCase**: Interfaces prefixed with `I` (e.g., `IDamageable`)
- **ALL_CAPS**: Not used; use PascalCase for constants (`MaxHealth`, not `MAX_HEALTH`)
- **Namespace**: Use `DefaultCompany.COMP3329` or a meaningful project namespace

### Imports / Using Directives
```csharp
// 1. System namespaces
using System;
using System.Collections;
using System.Collections.Generic;

// 2. Unity namespaces
using UnityEngine;
using UnityEngine.UI;

// 3. Third-party / project namespaces
using TMPro;
```

Order: System first, then Unity, then third-party/project. Remove unused `using` statements.

### Type Annotations & Access Modifiers
- Always specify access modifiers explicitly (`private`, `public`, `protected`)
- Use `[SerializeField]` for inspector-exposed private fields instead of making fields public
- Prefer `private` by default; only expose what is necessary
- Use `var` only when the type is obvious from the right-hand side
- Unsafe code is disabled (`allowUnsafeCode: 0`)

```csharp
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private int _maxHealth = 100;

    private Rigidbody2D _rigidbody;
    private int _currentHealth;

    public int CurrentHealth => _currentHealth;
}
```

### MonoBehaviour Lifecycle Methods
Order lifecycle methods consistently in this sequence:
1. `Awake()` - Component initialization, `GetComponent` calls
2. `OnEnable()` / `OnDisable()` - Event subscription/unsubscription
3. `Start()` - First-frame initialization
4. `Update()` - Per-frame logic
5. `FixedUpdate()` - Physics-rate logic
6. `LateUpdate()` - Post-update logic
7. `OnDestroy()` - Cleanup
8. Custom public methods
9. Custom private methods

### Error Handling
- Use `Debug.LogError()` for runtime errors, `Debug.LogWarning()` for non-critical issues
- Use `Debug.Assert()` for development-time invariant checks
- Null-check references obtained via `GetComponent<T>()` or `FindObjectOfType<T>()`
- Prefer `TryGetComponent<T>()` over `GetComponent<T>()` when the component may not exist
- Never silently swallow exceptions in try/catch blocks

### Unity-Specific Patterns
- Prefer `CompareTag("Enemy")` over `tag == "Enemy"` (avoids GC allocation)
- Cache component references in `Awake()` rather than calling `GetComponent` repeatedly
- Use `[RequireComponent(typeof(Rigidbody2D))]` to enforce component dependencies
- Avoid `Find*` methods at runtime; use serialized references or events instead
- Use coroutines (`IEnumerator`) or async/await for asynchronous operations
- For 2D physics, use `Rigidbody2D`, `Collider2D`, `OnTriggerEnter2D`, etc.

### Formatting
- Use 4-space indentation (no tabs)
- Allman-style braces (opening brace on its own line)
- One blank line between methods
- Keep lines under 120 characters
- Use expression-bodied members for simple getters: `public int Health => _health;`

## Key Packages Available

| Package | Purpose |
|---|---|
| `com.unity.2d.animation` | 2D skeletal animation |
| `com.unity.2d.spriteshape` | Sprite shape rendering |
| `com.unity.2d.tilemap.extras` | Extended tilemap tools |
| `com.unity.2d.pixel-perfect` | Pixel-perfect camera |
| `com.unity.textmeshpro` | Advanced text (use `TMPro` namespace) |
| `com.unity.ugui` | Unity UI (Canvas, Button, etc.) |
| `com.unity.burst` | High-performance compiled jobs |
| `com.unity.collections` | Native collections for Jobs |
| `com.unity.mathematics` | SIMD math library |

## Gesture Recognition Module

The project contains a hand gesture recognition module under `Assets/Scripts/GestureRecognition/`.

### Architecture

```
Assets/Scripts/GestureRecognition/
├── Core/                          # Data types, events, config
│   ├── GestureType.cs             # Enum: None, Push, Lift, Shoot, Fist, OpenPalm
│   ├── GestureResult.cs           # Readonly struct: per-frame result
│   ├── GestureEvents.cs           # Static event hub for frontend subscriptions
│   └── GestureConfig.cs           # ScriptableObject: gesture-to-sprite mapping
├── Detection/                     # Camera, MediaPipe, classification, tracking
│   ├── CameraManager.cs           # WebCamTexture lifecycle management
│   ├── MediaPipeBridge.cs         # Stub (real MediaPipe integration pending)
│   ├── GestureClassifier.cs       # Pure logic: landmark-based classification
│   └── HandTracker.cs             # Palm center computation + smoothing
├── Service/                       # Facade singleton for frontend
│   ├── GestureService.cs          # Orchestrates Camera→MediaPipe→Classifier→Events
│   └── GesturePanelManager.cs     # Convenience API: one-line show/hide/resize panel
├── UI/                            # Display panel + debug overlay
│   ├── GestureDisplayPanel.cs     # Resizable, draggable floating panel (3 display modes)
│   └── GestureOverlay.cs          # Debug landmark dot visualization
├── Editor/                        # Editor-only tools
│   ├── GenerateGestureSprites.cs  # Menu: Tools > Generate Placeholder Sprites
│   └── GestureRecognition.Editor.asmdef
└── GestureRecognition.asmdef      # Assembly definition

Assets/Tests/
├── EditMode/
│   ├── GestureClassifierTests.cs  # Unit tests for classifier + tracker + result
│   └── EditModeTests.asmdef
└── PlayMode/
    ├── GestureIntegrationTests.cs # Integration tests (service, events, bridge)
    └── PlayModeTests.asmdef

Assets/Scenes/
├── SampleScene.unity              # Default scene
└── GestureTestScene.unity         # Gesture recognition test/demo scene

Assets/Resources/GestureSprites/   # Placeholder sprites (generate via editor menu)
```

### Namespaces

| Namespace | Purpose |
|---|---|
| `GestureRecognition.Core` | Data types, events, config |
| `GestureRecognition.Detection` | Camera, MediaPipe bridge, classifier, tracker |
| `GestureRecognition.Service` | GestureService facade |
| `GestureRecognition.UI` | Display panel, debug overlay |
| `GestureRecognition.Editor` | Editor utilities (sprite generation) |

### Key Extension Points

- **New gesture**: Add enum in `GestureType.cs` → Add classifier in `GestureClassifier.cs` → Add sprite in GestureConfig asset
- **Custom classifier**: Call `GestureClassifier.RegisterClassifier(type, func)` at runtime
- **Frontend integration**: Subscribe to `GestureEvents.OnGestureChanged` or poll `GestureService.Instance.CurrentResult`
- **Panel management**: Use `GesturePanelManager.Instance.ShowPanel()` / `.HidePanel()` for one-line panel control
- **Testing without camera**: Use `GestureService.Instance.Bridge.InjectMockData()` in code or run GestureTestScene with `_autoStart` enabled

### MediaPipe Integration Status

The MediaPipe Unity Plugin v0.16.3 is **installed** via `.tgz` (UPM format). The `MediaPipeBridge.cs` uses **conditional compilation** (`#if MEDIAPIPE_INSTALLED`) to support both real and stub modes:

- **Real mode** (plugin installed): Uses `HandLandmarker` Task API in `VIDEO` running mode (CPU only). The `InitializeAsync()` coroutine loads the `hand_landmarker.bytes` model via `LocalResourceManager` (Editor) or `StreamingAssetsResourceManager` (builds), then creates the `HandLandmarker` instance.
- **Stub mode** (plugin absent): `Initialize()` works synchronously. `ProcessFrame()` always returns "no hand detected". Mock data injection via `InjectMockData()` still works for testing.
- **Two init methods**: `Initialize()` is synchronous (stub/mock-friendly, used by tests). `InitializeAsync()` is a coroutine that also loads the MediaPipe model (used by `GestureService`).
- **For builds**: Copy `hand_landmarker.bytes` to `StreamingAssets/` before building. See `MEDIAPIPE_SETUP.md`.

## Notes for Agents

- This is a Windows development environment
- The package registry uses `packages.unity.cn` (Chinese Unity mirror via Tuanjie Hub)
- No `.gitignore` exists yet; if initializing git, exclude `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/`, and `*.csproj` / `*.sln`
- No linting or static analysis tools are configured; follow the style guidelines above
- No CI/CD pipeline exists; test commands above can be used to set one up
- When creating new scripts, Unity will auto-generate `.meta` files on next editor refresh
- Always ensure scene files referenced in `EditorBuildSettings.asset` are kept in sync
- The `GestureRecognition.asmdef` defines the assembly — all test `.asmdef` files reference it
- Editor scripts are isolated in `GestureRecognition.Editor.asmdef` (Editor platform only)
- **Language requirement**: All responses to the user must be in **Chinese (中文)**. Code comments and identifiers remain in English.
