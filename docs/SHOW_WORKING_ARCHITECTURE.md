# Show Working Mode Architecture

## Overview

Show Working mode is an interactive step-by-step guided tour of an engine's internal operation. The user explores each engine part one at a time, while a **continuous airflow gradient tube** visually demonstrates air/fuel/exhaust flow through the engine, filling progressively from intake to exhaust as the user navigates deeper.

The system integrates across 7+ coordinated components spanning view management, part visual states, airflow animation, and UI.

---

## Flow Diagram

```
User taps "Show Working" button
         │
         ▼
┌─────────────────────────────────────────────────┐
│ EngineViewManager.ActivateShowWorkingView()     │
│  • Sets IsShowWorkingActive = true              │
│  • Exits any other active mode (X-Ray, Explode, │
│    Grab) with proper cleanup                    │
│  • Hides all buttons except stopShowWorkingBtn  │
│  • Calls SimplePartExplorer.StartExplorer()     │
└──────────────────────┬──────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────┐
│ SimplePartExplorer.StartExplorer()               │
│  • Loads parts from EngineSceneLoader's active   │
│    engine manifest (EnginePartManifest SO)       │
│  • Calls JetEngineShowWorking.OnShowWorkingStart │
│  • Disables EngineInteractor (no hover/highlight)│
│  • Shows the explorer UI panel                   │
│  • Calls NextPart() to show first part           │
└──────────────────────┬──────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────┐
│ SimplePartExplorer.ShowPart(index)               │
│  • Lifts ACTIVE part up (EnginePart.LiftUp)      │
│  • Sets ACTIVE part to full-opacity + white      │
│    outline (EnginePartVisuals.SetShowWorking)    │
│  • Lowers all OTHER parts down                   │
│  • Sets OTHER parts to X-Ray transparent         │
│    (EnginePartVisuals.SetShowWorkingBackground)  │
│  • Updates tablet + monitor UI text              │
│  • Plays audio explanation clip                  │
│  • Calls JetEngineShowWorking.OnPartShown(name)  │
└──────────────────────┬──────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────┐
│ JetEngineShowWorking.OnPartShown(partName)       │
│  • Looks up part name in ProgressMapping[]       │
│  • progress = -1  → StartAirflow (slide covers)  │
│  • progress = 0..1 → fill tube cumulatively      │
│  • progress = -2  → no visual change (skip)      │
│  • Calls airflowController.SetProgress(value)    │
└──────────────────────┬──────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────┐
│ JetEngineAirflowController                       │
│  BuildSingleTube() — procedural mesh on Awake()  │
│  StartAirflow() — slides covers off, shows tube  │
│  SetProgress(p) — animates _Progress shader prop │
│  StopAirflow() — hides tube, restores covers     │
└─────────────────────────────────────────────────┘
```

---

## Core Components

### 1. [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs) — View Orchestrator

The central manager that coordinates all view modes (Default, X-Ray, Exploded, Grab, Show Working).

**Show Working entry** ([`ActivateShowWorkingView()`](Assets/Scripts/EngineViewManager.cs:368)):
- Exits any active conflicting mode (X-Ray, Exploded, Grab) with proper cleanup
- Sets [`IsShowWorkingActive = true`](Assets/Scripts/EngineViewManager.cs:34) (static, readable by other components)
- Hides all mode buttons; shows only [`stopShowWorkingButton`](Assets/Scripts/EngineViewManager.cs:16)
- Delegates to [`simplePartExplorer.StartExplorer()`](Assets/Scripts/EngineViewManager.cs:432)

**Show Working exit** ([`StopShowWorkingIfActive()`](Assets/Scripts/EngineViewManager.cs:440)):
- Called when transitioning to any other mode (prevents mode conflicts)
- Calls `simplePartExplorer.StopExplorer()`
- Sets `IsShowWorkingActive = false`

The button state matrix for Show Working:

| Button | Show Working Active |
|---|---|
| `xrayButton` | ❌ hidden |
| `xrayResetButton` | ❌ hidden |
| `explodeButton` | ❌ hidden |
| `grabButton` | ❌ hidden |
| `reassembleButton` | ❌ hidden |
| `showWorkingButton` | ❌ hidden |
| `defaultViewButton` | ❌ hidden |
| **`stopShowWorkingButton`** | ✅ **visible** |

---

### 2. [`SimplePartExplorer`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs) — Step-by-Step Part Navigator

The core driver for the Show Working experience. Loads parts from the currently active engine's [`EnginePartManifest`](...ScriptableObjects) ScriptableObject and manages forward/backward navigation.

**Key Properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| `liftAmount` | float | 0.4 | How far the active part lifts up (local Y) |
| `liftDuration` | float | 0.35 | Duration of lift/lower animation |
| `explorerPanel` | GameObject | — | Optional parent panel activated when explorer starts |
| `tabletPartName` | TMP_Text | — | Tablet UI — part name |
| `tabletPartDescription` | TMP_Text | — | Tablet UI — part description |
| `monitorPartName` | TMP_Text | — | Wall monitor — part name |
| `monitorPartDescription` | TMP_Text | — | Wall monitor — part description |
| `stepCounter` | TMP_Text | — | "Part 3 / 12" counter |
| `previousButton` / `nextButton` | Button | — | Navigation buttons |
| `audioSource` | AudioSource | — | Plays part explanation audio |

**Lifecycle:**

1. [`StartExplorer()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:100) — Called by [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs:432)
   - Validates: delegates to `EngineViewManager.ActivateShowWorkingView()` if not already active
   - Loads parts from manifest via [`LoadPartsFromManifest()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:398)
   - Finds `JetEngineShowWorking` → calls `OnShowWorkingStart()`
   - Disables `EngineInteractor` (prevents hover highlighting conflicts)
   - Shows explorer panel
   - Calls `NextPart()` to show first part

2. [`ShowPart(index)`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:264) — Core visual update
   - **Active part**: `LowerDown()` (reset) → `SetVisible(true)` → `SetShowWorkingActive()` → `LiftUp(amount, duration)`
   - **Other parts**: `LowerDown()` → `SetVisible(true)` → `SetShowWorkingBackground()`
   - Updates all UI text elements
   - Plays audio clip (prefers `PartData.audioExplanation`, falls back to `EnginePart.AudioClip`)
   - Calls `_showWorking.OnPartShown(enginePart.gameObject.name)` — triggers airflow update

3. [`NextPart()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:227) — Advances to next part; calls `StopExplorer()` when reaching the end

4. [`PreviousPart()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:246) — Goes back; blocked at index 0

5. [`StopExplorer()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:158) — Cleanup
   - Delegates to `EngineViewManager.ActivateDefaultView()` if `IsShowWorkingActive`
   - Restores all parts: `LowerDown()` + `SetVisible(true)` + `RestoreOriginal()`
   - Re-enables `EngineInteractor`
   - Hides UI panels
   - Calls `_showWorking.OnShowWorkingStop()`

6. [`LoadPartsFromManifest()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:398) — Dynamic part loading
   - Finds `EngineSceneLoader` in scene
   - Reads `ActiveEngineData.partManifest` (an `EnginePartManifest` SO)
   - Iterates `manifest.parts` in **index order** (respects SO serialization order)
   - For each entry, finds the matching `EnginePart` GameObject under `engineRoot` by `entry.gameObjectName`
   - Populates `partsList` (PartData) and `enginePartsList` (EnginePart) in parallel

---

### 3. [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs) — Airflow Progress Manager

Maps part names to airflow tube progress values. The tube fills **cumulatively** — progress never regresses, creating a flame-like fill effect.

**Progress Mapping System:**

| partNameContains | progress | Meaning |
|---|---|---|
| `"Left Outer Cover"`, `"Right Outer Cover"`, `"Outer_cover"` | **-1** | Slide covers off (triggers `StartAirflow()`) |
| `"Fan Blades"` | **0.03** | Intake / fan area (cold blue) |
| `"Frontcap"` | **0.10** | Intake duct |
| `"Innercover1"`, `"Inner Cover"` | **0.20** | Compression start (cyan) |
| `"Shaft"` | **0.30** | Compression deepens |
| `"Fuel Cap"` | **0.42** | Combustion begins |
| `"Fuel Injector"` | **0.45** | White-hot ignition |
| `"High pressure Blades"` | **0.60** | Turbine (orange) |
| `"Highpressure Turbine Shaft"` | **0.72** | Turbine fully active |
| `"Cap"` | **0.85** | Exhaust entry |
| `"Wires"` | **1.00** | Full exhaust plume |
| `"Blade"` | **-2** | No visual change (skip) |

**Key Properties:**

| Property | Type | Description |
|---|---|---|
| `airflowController` | JetEngineAirflowController | Reference to the airflow tube controller |
| `progressMappings` | ProgressMapping[] | Part name → progress value mappings (serialized in Inspector) |
| `partsToHideOnStart` | GameObject[] | **New.** GameObjects deactivated when Show Working starts and restored on stop. Used for parts like "Left Outer Cover" and "Innercover2" that obstruct the view of internal components. |

**Key Methods:**

- [`OnShowWorkingStart()`](Assets/Scripts/JetEngineShowWorking.cs:92) — Resets `_highestProgress = 0`, `_coversRemoved = false`, then calls [`HidePartsOnStart()`](Assets/Scripts/JetEngineShowWorking.cs:114) to deactivate any objects in `partsToHideOnStart`
- [`OnShowWorkingStop()`](Assets/Scripts/JetEngineShowWorking.cs:102) — Calls `airflowController.StopAirflow()`, resets state, then calls [`RestoreHiddenParts()`](Assets/Scripts/JetEngineShowWorking.cs:133) to restore hidden objects to their original active state
- [`OnPartShown(string partName)`](Assets/Scripts/JetEngineShowWorking.cs:149) — Called every time a new part is shown
  - Progress **-2**: No change (skip)
  - Progress **-1**: Slide covers off via `airflowController.StartAirflow()`
  - Progress **0..1**: Only advances tube if `progress > _highestProgress` (cumulative)
  - Calls `airflowController.SetProgress(_highestProgress)` with animated smooth fill

#### 3.1 Part Hiding on Start (`partsToHideOnStart`)

This feature ensures certain GameObjects are hidden when Show Working mode begins and restored when it ends, preventing external shells or covers from blocking the view of internal components.

**Lifecycle Integration:**

```
OnShowWorkingStart()
  └── HidePartsOnStart()
        ├── Saves original active state of each object in partsToHideOnStart[]
        ├── Calls SetActive(false) on each object
        └── Logs each hidden object with name + original state

OnShowWorkingStop()
  └── RestoreHiddenParts()
        └── Calls SetActive(originalState) on each object in partsToHideOnStart[]
```

**Inspector Setup:**

1. Select the GameObject with the [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs) component
2. In the Inspector, find the **Parts to Hide on Start** section
3. Set the `Size` of `partsToHideOnStart` to the number of objects to hide
4. Drag the GameObjects (e.g. "Left Outer Cover", "Innercover2") from the Hierarchy into the array slots

**Important:** The hide/restore lifecycle is fully integrated into the existing `OnShowWorkingStart()` → `OnShowWorkingStop()` flow. `SimplePartExplorer` calls these methods automatically at lines [136](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:136) and [172](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:172) — no additional wiring is needed.

---

### 4. [`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs) — Procedural Airflow Tube

Generates a **single continuous mesh tube** along the engine axis with a position-based gradient shader.

**Tube Construction ([`BuildSingleTube()`](Assets/Scripts/JetEngineAirflowController.cs:141)):**
- Procedural mesh with 24 radial segments × 24 height segments
- **Radius profile** (engine axis `t = 0..1`):
  - `t=0.0` (intake): 1.4× wide
  - `t=0.2` (compression): 0.9× narrower
  - `t=0.4` (combustion): 0.6× narrowest
  - `t=0.6` (turbine): 0.7×
  - `t=0.8` (exhaust): 1.0×
  - `t=1.0` (exit): 1.6× flared
- Uses [`Custom/AirflowEffect`](Assets/Shaders/AirflowEffect.shader) shader with `_Progress` float property (0→1)
- Tube transform can be **overridden** via `overrideTubeTransform` values for precise positioning

**Cover Management ([`SlideAndFadeCovers()`](Assets/Scripts/JetEngineAirflowController.cs:296)):**
- Slides outer covers along `coverSlideDirection` (default: Vector3.up) over `coverSlideDuration` (0.6s)
- Fades cover alpha from 1→0 during slide
- Deactivates covers at end; restores on `StopAirflow()`

**Progress Animation ([`AnimateProgress()`](Assets/Scripts/JetEngineAirflowController.cs:272)):**
- Smoothly interpolates `_currentProgress` to `_targetProgress` over `progressFillDuration` (0.8s)
- Sets `_tubeMaterial.SetFloat("_Progress", value)` each frame — drives shader gradient

**Key Properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| `airflowMaterial` | Material | Custom/AirflowEffect | Shader material for the tube |
| `engineAxis` | Vector3 | Vector3.right | Direction the engine faces |
| `engineLength` | float | 2.0 | Total engine length (world units) |
| `baseRadius` | float | 0.18 | Base tube radius |
| `radiusScale` | float [0..1] | 0.15 | Scale multiplier on radius |
| `outerCovers` | GameObject[] | — | Covers to slide off on start |
| `coverSlideDirection` | Vector3 | Vector3.up | Direction covers slide |
| `coverSlideDistance` | float | 1.5 | How far covers slide |
| `progressFillDuration` | float [0.3..3] | 0.8 | Duration of tube fill animation |
| `overrideTubeTransform` | bool | true | Use precise local transform values |

---

### 5. [`EnginePartVisuals`](Assets/Scripts/EnginePartVisuals.cs) — Part Visual State

Manages the visual appearance of each engine part across all modes.

**Show Working states:**

| Method | What it does |
|---|---|
| [`SetShowWorkingActive()`](Assets/Scripts/EnginePartVisuals.cs:197) | Restores original full-opacity materials; clears emission; applies **white outline** (width × 1.5×) — no colored glow, preserving airflow tube visibility |
| [`SetShowWorkingBackground()`](Assets/Scripts/EnginePartVisuals.cs:235) | Semi-transparent with **neutral tint** (`showWorkingBgColor` white, `showWorkingBgAlpha` 0.12) — makes surrounding parts ghost-like so the airflow tube glows through clearly |

**Why white outline for active part?** The standard colored emissive glow would hide the airflow tube colors underneath. White outline clearly marks the active part without visual interference.

---

### 6. [`EnginePartExplode`](Assets/Scripts/EnginePartExplode.cs) — Part Position Animation

Handles the lift/lower mechanics used during Show Working.

| Method | Description |
|---|---|
| [`LiftUp(amount, duration)`](Assets/Scripts/EnginePartExplode.cs:133) | Animates part from assembled position to `AssembledLocalPos + (0, amount, 0)` |
| [`LowerDown(duration)`](Assets/Scripts/EnginePartExplode.cs:145) | Animates part back to `AssembledLocalPos` |

Both methods stop any running lift/explode coroutines before starting, preventing conflicts.

---

### 7. [`ShowWorkingObjectHider`](Assets/Scripts/ShowWorkingObjectHider.cs) — Optional Object Hiding

Utility component that hides arbitrary GameObjects during Show Working and restores them on exit.

> **Note:** For most use cases, prefer the [`partsToHideOnStart`](Assets/Scripts/JetEngineShowWorking.cs:30) approach on [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs) (see [§3.1](docs/SHOW_WORKING_ARCHITECTURE.md#31-part-hiding-on-start)), which is more explicit and tied directly to the Show Working lifecycle. `ShowWorkingObjectHider` is a legacy fallback that polls `Update()`.

**Key features:**
- Saves original active states on `Start()`
- Monitors `EngineViewManager.IsShowWorkingActive` changes in `Update()`
- **Auto-excludes** objects managed by `JetEngineAirflowController.outerCovers` (prevents competing `SetActive` calls)
- Only acts on state **transitions** (not per-frame)

---

### 8. [`EnginePart`](Assets/Scripts/EnginePart.cs) — Part Facade

Convenience pass-through methods that delegate to `EnginePartVisuals` and `EnginePartExplode`:

```csharp
// Show Working pass-throughs
public void SetShowWorkingActive()      => Visuals?.SetShowWorkingActive();
public void SetShowWorkingBackground()  => Visuals?.SetShowWorkingBackground();
public void LiftUp(float amount, float duration) => Explode?.LiftUp(amount, duration);
public void LowerDown(float duration)             => Explode?.LowerDown(duration);
```

---

## Data Flow

### Part Loading Sequence

```
EngineSceneLoader (in scene)
  └── ActiveEngineData (EngineData SO)
        └── partManifest (EnginePartManifest SO)
              └── parts[] (list of PartManifestEntry)
                    ├── gameObjectName (string) → matched to scene GameObject
                    └── partData (PartData SO)
                          ├── partName
                          ├── description
                          └── audioExplanation
```

### Airflow Progress Flow

```
SimplePartExplorer.ShowPart(index)
  └── JetEngineShowWorking.OnPartShown(partName)
        ├── GetProgressForPart(partName)
        │     └── progressMappings[] match by substring (case-insensitive)
        ├── progress = -1 → airflowController.StartAirflow()
        │     └── SlideAndFadeCovers() → show tube at progress 0
        ├── progress > _highestProgress → update _highestProgress
        │     └── airflowController.SetProgress(_highestProgress)
        │           └── AnimateProgress(target) → shader _Progress float
        └── progress ≤ _highestProgress → no change (cumulative fill)
```

---

## Button State Matrix

| Mode | xray | xrayReset | explode | default | grab | reassemble | showWorking | stopShowWorking |
|---|---|---|---|---|---|---|---|---|
| **Default** | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ |
| **X-Ray** | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Exploded** | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Grab** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Show Working** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

---

## Key Design Decisions

1. **Cumulative progress (never regresses)**: The airflow tube fills from intake to exhaust and stays filled. Pressing Previous does NOT empty the tube — this creates a natural "flame building up" visual that makes sense for engine operation flow.

2. **White outline instead of glow for active part**: The active part's original materials are preserved at full opacity so the airflow tube gradient colors (blue→cyan→white→orange→red) are visible through/underneath the part geometry. A colored emissive glow would wash out these colors.

3. **Neutral tint for background parts**: Uses `showWorkingBgColor` (white) with 12% alpha instead of the X-Ray blue tint — this ensures the airflow tube colors remain accurate and visible through semi-transparent background parts.

4. **Part lifting**: The active part lifts up 0.4 units for clear line-of-sight viewing. All others lower back to assembled position, preventing visual clutter.

5. **Audio auto-play**: Each part's explanation audio plays automatically when it becomes active, enabling a hands-free guided experience.

6. **Overridable tube transform**: The airflow tube uses explicit `tubeLocalPosition`/`tubeLocalEulerAngles`/`tubeLocalScale` values instead of procedural positioning, ensuring pixel-perfect alignment with the model regardless of mesh variations.