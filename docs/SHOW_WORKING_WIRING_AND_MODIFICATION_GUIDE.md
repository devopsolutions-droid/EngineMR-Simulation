# Show Working System — Complete Wiring & Modification Guide

## 1. System Overview

The Show Working system has **two parallel tracks** that both drive the same airflow progress:

```
User taps "Show Working"
    │
    ▼
EngineViewManager.ActivateShowWorkingView()
    │
    ├── ShowWorkingInteractiveController found?
    │   YES → StartInteractiveFlow()  ────┐
    │   NO  → SimplePartExplorer.StartExplorer()  ──┐
    │                                             │
    ▼                                             ▼
[Interactive Track]                      [Legacy Track]
GrabRemove / TurbineStart               Next/Previous buttons
PartTap / IgniteButton / BladeSpin      navigate engine parts
                                            │
    │                                       │
    ▼                                       ▼
    └────── Both call ──────┘
              │
              ▼
    JetEngineShowWorking
    (CurrentAirflowProgress)
              │
              ▼
    JetEngineAirflowController
    (legacy tube animation)
              │
              ▼
    AirflowV61Controller  ←── OUR COMPONENT
    (polls CurrentAirflowProgress, writes to MPB)
```

**Key insight**: [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) is a **passive observer**. It never calls anything — it only polls [`JetEngineShowWorking.CurrentAirflowProgress`](Assets/Scripts/JetEngineShowWorking.cs:203) every frame. It works identically with BOTH tracks.

---

## 2. Complete Wiring Map

### 2.1 Entry Point

| Step | File | Method |
|------|------|--------|
| User taps "Show Working" | [`TabletUIController.cs`](Assets/Scripts/TabletUIController.cs:137) | `OnShowWorkingClicked()` |
| Delegate to view manager | [`TabletUIController.cs`](Assets/Scripts/TabletUIController.cs:142) | `engineViewManager.ActivateShowWorkingView()` |
| Activate Show Working | [`EngineViewManager.cs`](Assets/Scripts/EngineViewManager.cs:377) | `ActivateShowWorkingView()` |

[`EngineViewManager.ActivateShowWorkingView()`](Assets/Scripts/EngineViewManager.cs:377) does:
1. Reset any active view (X-Ray, Explode, Grab)
2. Hide all mode buttons (xray, explode, grab, showWorking, etc.)
3. Show `stopShowWorkingButton`, hide `startTurbineButton` and `igniteButton`
4. Find [`ShowWorkingInteractiveController`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:26) via `FindFirstObjectByType`
5. If found → call [`StartInteractiveFlow()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:126)
6. If NOT found → fallback to [`SimplePartExplorer.StartExplorer()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:100)

### 2.2 Interactive Track (ShowWorkingInteractiveController)

#### StartInteractiveFlow()
[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:126-157)

```
1. Set _isRunning = true, _flowCompleted = false, _currentStepIndex = -1
2. Set showWorking.skipAutoCoverRemoval = true
3. Call showWorking.OnShowWorkingStart()
4. Enable grabManager.allowGrabbing = true
5. Subscribe: grabManager.OnGrabStarted += OnPartGrabbed
6. Subscribe: grabManager.OnGrabEnded += OnPartReleased
7. Call AdvanceToNextStep()  ← starts at step 0
```

#### AdvanceToNextStep()
[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:354-423)

Routes by `step.stepType`:

| Step Type | Setup | Trigger | Auto-Execute |
|-----------|-------|---------|--------------|
| **GrabRemove** | Save original position, call `showWorking.OnPartShown()`, highlight+lift part, enable grab | User pulls part away OR presses Next | `CompleteCurrentStep()` |
| **TurbineStart** | Show `startTurbineButton`, play audio | Next pressed | Start `TurbineBladeRotator`, call `showWorking.AdvanceAirflowTo()` |
| **PartTap** | Highlight+lift target part, play audio | Next pressed | Route VFX via `PlayStepVFX()`, advance airflow |
| **IgniteButton** | Show `igniteButton`, play audio | Next pressed | Optionally slow-mo, route combustion VFX, advance airflow to 1.0 |
| **BladeSpin** | Log info, play audio | Next pressed | Create runtime parent, add `TurbineBladeRotator`, start rotation, advance airflow |

#### CompleteCurrentStep()
[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:956-978)

```
1. Fire OnStepCompleted event
2. If GrabRemove with lifted part → LowerDown
3. Start DelayedAdvance coroutine → AdvanceToNextStep()
```

#### CompleteAllSteps()
[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1063-1085)

```
1. Play completion audio
2. If transitionToLegacyExplorer == true → TransitionToLegacyExplorer()
   ELSE → StopInteractiveFlow()
```

#### TransitionToLegacyExplorer()
[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1099-1138)

```
1. Collect all GrabRemove targetParts as removedParts list
2. Unsubscribe grab events, disable grabManager.allowGrabbing
3. Call partExplorer.LoadAndSkipParts(removedParts)
4. Call partExplorer.ResumeExplorerAt(0)
5. Call partExplorer.ShowNavigationButtons()
```

### 2.3 Legacy Track (SimplePartExplorer)

| Step | File | Method |
|------|------|--------|
| Start explorer | [`SimplePartExplorer.cs`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:100) | `StartExplorer()` |
| Next part | [`SimplePartExplorer.cs`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:232) | `NextPart()` |
| Previous part | [`SimplePartExplorer.cs`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:251) | `PreviousPart()` |
| Show part | [`SimplePartExplorer.cs`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:269) | `ShowPart(int index)` |
| Advance airflow | [`JetEngineShowWorking.cs`](Assets/Scripts/JetEngineShowWorking.cs:157) | `OnPartShown(partName)` |

[`SimplePartExplorer.ShowPart()`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:269) calls:
```csharp
jetEngineShowWorking.OnPartShown(partName);
```

[`JetEngineShowWorking.OnPartShown()`](Assets/Scripts/JetEngineShowWorking.cs:157) looks up [`progressMappings[]`](Assets/Scripts/JetEngineShowWorking.cs:25) substring match:
- `-1` → StartAirflow (slide covers off)
- `-2` → Skip (no progress change)
- `0..1` → Cumulative tube fill (never regresses via `_highestProgress` guard)

### 2.4 Tablet UI Routing

[`TabletUIController.OnNextClicked()`](Assets/Scripts/TabletUIController.cs:145):
```
1. If interactive controller HAS COMPLETED → ignore
2. If interactive controller IS RUNNING → route to interactive.OnNextPressed()
3. ELSE → legacy explorer.NextPart()
```

[`TabletUIController.OnPreviousClicked()`](Assets/Scripts/TabletUIController.cs:172):
```
1. If interactive controller HAS COMPLETED → ignore
2. If interactive controller IS RUNNING → route to interactive.OnPreviousPressed()
3. ELSE → legacy explorer.PreviousPart()
```

[`TabletUIController.OnSpecialButtonPressed()`](Assets/Scripts/TabletUIController.cs:203):
- Routes to `interactive.OnSpecialButtonPressed()`
- Which then routes to `OnTurbineStarted()` or `OnIgniteButtonPressed()` based on step type

### 2.5 AirflowV61Controller (Our Component)

[Source](Assets/Scripts/Effects/AirflowV61Controller.cs:11-126)

| Method | What it does |
|--------|-------------|
| `Start()` | Auto-discovers `JetEngineShowWorking` via `FindFirstObjectByType` |
| `Update()` | Polls `showWorking.CurrentAirflowProgress` each frame |
| `ApplyProgress(progress)` | Writes `_Progress` float to all renderers via `MaterialPropertyBlock` |
| `SetProgress(value)` | Direct override (unused in normal flow) |

**NO CHANGES NEEDED** for step reassignment or modification. It watches `CurrentAirflowProgress` which is correctly set by whichever track is active.

---

## 3. Step Configuration Architecture

### 3.1 The `steps[]` Array — Interactive Flow Order

File: [`ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs)

This is a **serialized array** — edited in the Unity Inspector. The INDEX determines the order.

Each element is a [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307-1388):

| Field | Type | Applies To | Purpose |
|-------|------|------------|---------|
| `stepName` | string | All | Displayed on tablet as part name |
| `instruction` | string | All | Displayed on tablet as instruction text |
| `stepType` | InteractiveStepType | All | Controls routing logic |
| `targetPart` | GameObject | GrabRemove, PartTap | The part the user interacts with |
| `advanceDistance` | float | GrabRemove | Distance in world units to count as removed |
| `skipLift` | bool | GrabRemove, PartTap | If true, part stays in natural position |
| `turbineStartAirflowProgress` | float | TurbineStart | How much tube fills (0..1) |
| `bladeTargets` | GameObject[] | BladeSpin | Blades to group and rotate |
| `bladeRotationAxis` | Vector3 | BladeSpin | Local rotation axis (default 0,0,1) |
| `airCompressionController` | AirCompressionController | PartTap | Tube narrowing VFX |
| `fuelSprayController` | FuelSprayController | PartTap | Fuel injection particle VFX |
| `combustionController` | CombustionController | IgniteButton | Chamber glow, flame, shake, audio |
| `slowMotionController` | SlowMotionController | IgniteButton | Time scale manipulation |
| `engineAudioController` | EngineAudioController | IgniteButton | Audio crossfade |
| `partGlowController` | PartGlowController | PartTap | Part highlighting glow |
| `vfxDuration` | float | PartTap, IgniteButton | Duration override (0 = default) |
| `triggerSlowMotion` | bool | IgniteButton | Enable slow-mo before ignition |
| `airflowProgress` | float | PartTap, IgniteButton, BladeSpin | Direct progress value (-1 = no change) |
| `stepAudio` | AudioClip | All | Narration audio for this step |
| `audioNameOverride` | string | All | String lookup for audio |
| `turbineStartAudio` | AudioClip | TurbineStart | Audio for turbine start |
| `ignitionButtonAudio` | AudioClip | IgniteButton | Audio for ignition press |

### 3.2 The `progressMappings[]` Array — Part→Progress Lookup

File: [`JetEngineShowWorking.cs`](Assets/Scripts/JetEngineShowWorking.cs:25-82)

Also a **serialized array** — edited in the Unity Inspector.

| Field | Type | Purpose |
|-------|------|---------|
| `partNameContains` | string | Substring to match against part name |
| `progress` | float | -1=slide covers, -2=skip, 0..1=tube fill |

**Used by:**
1. [`SimplePartExplorer`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs:269) — calls `OnPartShown(partName)` for each part
2. [`ShowWorkingInteractiveController.StartGrabRemoveStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:427) — calls `showWorking.OnPartShown()` for GrabRemove steps
3. [`ShowWorkingInteractiveController.GetAirflowForStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:833) — calls `showWorking.GetProgressForPart()` for GrabRemove steps

### 3.3 InteractiveStepType Enum

[Source](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1285-1301)

```csharp
public enum InteractiveStepType
{
    GrabRemove,    // User physically grabs and pulls part away
    TurbineStart,  // Auto-starts turbine blade rotation + airflow
    PartTap,       // Auto-plays VFX (compression/fuel spray) on Next
    IgniteButton,  // Auto-plays combustion VFX on Next
    BladeSpin      // Auto-spins a runtime blade group on Next
}
```

### 3.4 VFX Routing

[`PlayStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:890-929):

```
PartTap step:
    if step.airCompressionController != null → StartCompression()
    else if step.fuelSprayController != null → StartSpray()
    else → invoke callback immediately

IgniteButton step:
    if step.combustionController != null → StartCombustion()
    else → invoke callback immediately
```

All VFX controllers referenced in [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307) already exist in the codebase:
- [`AirCompressionController`](Assets/Scripts/Effects/AirCompressionController.cs:11) — tube narrowing
- [`FuelSprayController`](Assets/Scripts/Effects/FuelSprayController.cs:10) — fuel particles
- [`CombustionController`](Assets/Scripts/Effects/CombustionController.cs:14) — chamber glow, flame, shake, audio
- [`SlowMotionController`](Assets/Scripts/Effects/SlowMotionController.cs:11) — time scale
- [`EngineAudioController`](Assets/Scripts/Effects/EngineAudioController.cs:12) — audio crossfade
- [`PartGlowController`](Assets/Scripts/Effects/PartGlowController.cs:11) — part glow highlight

---

## 4. Easiest Way to Make Changes (Without Breaking Anything)

### 4.1 Adding a New Interactive Step

**Inspector-only** (no code changes):

1. In the Inspector for [`ShowWorkingInteractiveController`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs), increase the `steps[]` array size
2. Fill in:
   - `stepName` — display name on tablet
   - `instruction` — instruction text on tablet
   - `stepType` — choose from enum
   - Required fields per step type (see table in 3.1)
3. Optionally: add a [`progressMappings`](Assets/Scripts/JetEngineShowWorking.cs:84) entry if the part name should be recognized by the legacy system

**Example: Adding a Fuel Injection (PartTap) step:**
- Add new element to `steps[]`
- `stepName` = "Fuel Injection"
- `instruction` = "Watch the fuel spray into the combustion chamber."
- `stepType` = PartTap
- `targetPart` = the fuel injector GameObject
- `fuelSprayController` = the [`FuelSprayController`](Assets/Scripts/Effects/FuelSprayController.cs) component in the scene
- `airflowProgress` = 0.65

### 4.2 Reordering Steps

**Inspector-only**: Drag array elements up/down in the Unity Inspector.

**The step index = the order.** No code changes needed.

### 4.3 Removing a Step

**Inspector-only**:
1. Reduce the `steps[]` array size (removes the last element)
2. OR set individual elements to null/empty

**Cleanup** (optional):
- Remove any `progressMappings` entry for the removed part
- Remove any VFX controller references that are no longer used by any step

### 4.4 Modifying an Existing Step

**Inspector-only**: Change any field on the [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307):
- Change `targetPart` to point to a different GameObject
- Change `advanceDistance` to require more/less pull distance
- Swap between `airCompressionController` and `fuelSprayController` on PartTap steps
- Change `airflowProgress` to advance more/less tube fill
- Change `stepAudio` to different narration

### 4.5 What NEVER Breaks

| Component | Why it's safe |
|-----------|---------------|
| [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) | Passive observer — polls `CurrentAirflowProgress`, never sends commands |
| [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs) | Driven by calls from controllers — no direct coupling to steps |
| [`TabletUIController`](Assets/Scripts/TabletUIController.cs) | Auto-discovers interactive controller, routes Next/Previous by runtime state |
| [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs) | Finds controller and calls `StartInteractiveFlow()` — no step awareness |
| Legacy [`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs) | Driven by `SetProgress()` — doesn't know about steps |

### 4.6 What COULD Break (And How to Avoid It)

| Risk | Scenario | Prevention |
|------|----------|------------|
| Null ref on missing targetPart | PartTap/GrabRemove step without targetPart | Always assign targetPart for these types |
| Missing VFX controller | PartTap step with no airCompressionController AND no fuelSprayController | Assign at least one, or leave empty to skip VFX (safe, just plays no VFX) |
| Inconsistent progress | Removing a GrabRemove step but keeping its part in progressMappings | Remove the mapping entry if the part won't be shown anymore |
| BladeSpin without blades | BladeSpin step with empty bladeTargets[] | Add at least one blade GameObject to the array |
| IgniteButton without combustion | IgniteButton step with no combustionController | Assign one, or it just advances progress without VFX |

### 4.7 When Code Changes ARE Needed

1. **Adding a new `InteractiveStepType` enum value** — requires:
   - New enum value in [`InteractiveStepType`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1285)
   - New case in [`AdvanceToNextStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:382) switch
   - New case in [`OnNextPressed()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:309) switch
   - New case in [`GoBackToPreviousStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:697) switch (if reversable)
   - New case in [`GetAirflowForStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:837) switch
   - New case in [`PlayStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:892) switch (if VFX needed)
   - New cleanup logic in [`StopCurrentStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:934)
   - New fields in [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307)

2. **Adding a new type of VFX controller** — requires:
   - New serialized field in [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307)
   - New case in [`PlayStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:892)
   - New cleanup case in [`StopCurrentStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:934)

---

## 5. Concrete Examples

### Example A: Reordering Existing Steps (Inspector Only)

Current steps (in scene):
1. Remove Left Outer Cover (GrabRemove)
2. Remove Right Outer Cover (GrabRemove)
3. Start Turbine (TurbineStart)

Want to swap "Start Turbine" to before cover removal:

1. In Inspector, open `ShowWorkingInteractiveController` component
2. Drag step index 3 above step index 1
3. **No code changes**
4. The flow now goes: Start Turbine → Remove Left Outer Cover → Remove Right Outer Cover

### Example B: Adding a New "Fuel Injection" Step (Inspector Only)

1. Increase `steps[]` array size from 3 to 4
2. Fill new element:
   - `stepName` = "Fuel Injection"
   - `instruction` = "Fuel is sprayed into the combustion chamber."
   - `stepType` = PartTap
   - `targetPart` = the Fuel Injector GameObject
   - `fuelSprayController` = drag the FuelSprayController from scene
   - `airflowProgress` = 0.55
3. Add to `progressMappings[]`: `partNameContains` = "Fuel", `progress` = 0.55
4. **No code changes**

### Example C: Changing Which Part a GrabRemove Step Targets (Inspector Only)

1. Find the step in `steps[]` array
2. Change `targetPart` from old GameObject to new GameObject
3. If the new part has a different name, optionally add to `progressMappings[]`
4. **No code changes**

---

## 6. Files Summary

| File | Purpose | Change Frequency |
|------|---------|-----------------|
| [`ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) | Interactive step flow, step type routing, VFX pipeline | Rare (new step types) |
| [`JetEngineShowWorking.cs`](Assets/Scripts/JetEngineShowWorking.cs) | Progress mapping, cumulative guard, lifecycle | Rare |
| [`TabletUIController.cs`](Assets/Scripts/TabletUIController.cs) | Button routing to interactive/legacy | Never for step changes |
| [`EngineViewManager.cs`](Assets/Scripts/EngineViewManager.cs) | View activation, button visibility | Never for step changes |
| [`AirflowV61Controller.cs`](Assets/Scripts/Effects/AirflowV61Controller.cs) | Polls progress, writes MPB | Never (passive observer) |
| [`JetEngineAirflowController.cs`](Assets/Scripts/JetEngineAirflowController.cs) | Legacy tube animation | Never |
| [`SimplePartExplorer.cs`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs) | Legacy part navigation | Rare |
| [`ShowWorkingStep` (class)](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307) | Step data definition | Rare (new fields) |
| [`InteractiveStepType` (enum)](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1285) | Step type enum | Rare (new types) |
| VFX controllers (6 files) | Individual VFX implementations | Rare |

---

**Bottom Line**: 90% of modifications — adding, removing, reordering steps, changing targets, swapping VFX controllers, adjusting progress values — are **Inspector-only operations**. The system is designed so that the [`steps[]`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307) array IS the configuration. [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) requires **zero changes** regardless of step changes, because it simply reads [`CurrentAirflowProgress`](Assets/Scripts/JetEngineShowWorking.cs:203) which is always correctly set by whichever track is active.