# Educational Stages Implementation Plan

## Overview

Add 5 educational stages to the Show Working flow, positioned **after Turbine Start** and **before Legacy Explorer transition**. Each stage represents a phase of jet engine operation with sub-steps that the user advances through via the Next button.

---

## 1. Architecture Decision: Minimal Extension (No Strategy Pattern Yet)

### Recommendation: Extend existing system, NOT the full Strategy Pattern

**Why:** The Strategy Pattern (`IShowWorkingHandler` + Registry) adds architectural overhead that isn't needed yet. The 5 educational stages can be implemented by:
1. Adding 3 new fields to [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307)
2. Adding 2 new [`InteractiveStepType`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1285) enum values (or reusing `PartTap`)
3. Extending 1-2 switch cases in the controller

**Keep the Strategy Pattern document** ([`docs/OPTIMIZED_STEP_HANDLER_ARCHITECTURE.md`](docs/OPTIMIZED_STEP_HANDLER_ARCHITECTURE.md)) for a future refactor when the number of step types grows beyond 7-8.

---

## 2. Stage → Step Mapping

### Concept: "Flat array with optional StageName grouping"

Each [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307) already has `stepName` and `instruction` which map to the tablet/monitor UI. We add:
- `stageName` (optional string) — when non-null and different from the previous step's stageName, the controller sets this as the **Stage Title** in both monitor & tablet "Engine Part Name" fields.
- Each sub-step's `stepName` + `instruction` continue to appear in the **Description** fields.

### Full Step Sequence

```
Index  StageName          StepName                StepType         Actions
────── ────────────────── ────────────────────── ──────────────── ──────────────────────────
  0    (null)             Remove Left Outer Cover GrabRemove       Part grabbed & removed
  1    (null)             Remove Right Outer Cover GrabRemove      Part grabbed & removed
  2    (null)             Start Turbine           TurbineStart     Legacy airflow → 20%, Intake visual

  3    Stage 1:           Air Intake - Entry      PartTap          ACTIVATE "Airflow Jet Engine Intake"
       Air Intake                                                              
  4    Stage 1:           Air Intake - Bypass     PartTap          ACTIVATE "Airflow Jet Engine - Bypass air"
       Air Intake                                                              

  5    Stage 2:           Compression Chamber     PartTap          Legacy airflow → 50%
       Air Compression                                                         
  6    Stage 2:           Compressor Blades       PartTap          HIGHLIGHT Compressor Blades + hover panel
       Air Compression                                                         

  7    Stage 3:           Fuel Injection Zone     PartTap          Legacy airflow → 70%
       Combustion                                                               
  8    Stage 3:           Fuel System             PartTap          HIGHLIGHT Fuel Cap + Fuel Injector + hover panels
       Combustion                                                               

  9    Stage 4:           HP Turbine Entry        PartTap          Legacy airflow → 85%
       Conversion of Energy                                                    
 10    Stage 4:           HP Mid Blades           PartTap          HIGHLIGHT High Pressure Mid Blades + hover panel
       Conversion of Energy                                                    
 11    Stage 4:           HP Compressor           PartTap          HIGHLIGHT High Pressure Compressor + hover panel
       Conversion of Energy                                                    
 12    Stage 4:           Rear HP Blades          PartTap          HIGHLIGHT Rear High Pressure Blades + hover panel
       Conversion of Energy                                                    

 13    Stage 5:           Exhaust Nozzle          PartTap          Legacy airflow → 100%
       Exhaust                                                                  
 14    Stage 5:           Full Exhaust Flow       PartTap          Final stage display
       Exhaust                                                                  

 15    To Conclude       Conclusion               PartTap          Final message + transition to explorer
```

---

## 3. New Fields to Add to [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307)

```csharp
[Header("Stage Grouping")]
[Tooltip("Optional stage title shown on monitor/tablet Engine Part Name. "
       + "When non-null and changes from previous step, updates the stage header. "
       + "Leave null for non-stage steps (cover removal, turbine start).")]
public string stageName;

[Header("Visual Activation")]
[Tooltip("GameObjects to activate when this step starts (NEW VISUAL ENABLED). "
       + "These are typically child GameObjects of Airflow_v6.1 or other visual systems "
       + "that should appear at specific educational stages.")]
public GameObject[] activateOnStepStart;

[Header("Hover Highlights")]
[Tooltip("Engine parts to highlight + show hover panels for (HOVER PANEL ENABLED). "
       + "Each part should have a PartHoverPanel child component. "
       + "The parts are lifted + outlined, and their hover panels are shown.")]
public GameObject[] highlightParts;
```

---

## 4. New Step Types Needed

### 4.1 Option A: Reuse `PartTap` (Recommended — Minimal Change)

Extend the existing `PartTap` handler to also process `activateOnStepStart[]` and `highlightParts[]`:

| Action | When it executes |
|--------|-----------------|
| Activate `activateOnStepStart[]` | On `AdvanceToNextStep()` — GameObjects are SetActive(true) |
| Set `airflowProgress` | On `AdvanceToNextStep()` — calls `showWorking.AdvanceAirflowTo(progress)` |
| Highlight `highlightParts[]` | On `AdvanceToNextStep()` — each gets `SetShowWorkingActive()` + `LiftUp()` |
| Show hover panels | On `AdvanceToNextStep()` — each part's `PartHoverPanel.Show()` |
| Stage name update | On `AdvanceToNextStep()` — if `stageName` changed, update tablet+monitor text |

**No new enum values needed. The existing `PartTap` logic is simply extended with optional processing of the new arrays.**

### 4.2 Option B: New `StageAdvance` enum value

If you prefer explicit separation:
```csharp
InteractiveStepType.StageAdvance  // Educational stage sub-step with activation + highlighting
```

This requires adding a new `case` to all 6 switch statements. Since we're extending rather than adding many new types, Option A is recommended.

---

## 5. Required Code Changes

### 5.1 [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1307)

Add the 3 new fields listed in Section 3. Estimated: +15 lines.

### 5.2 [`ShowWorkingInteractiveController`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs)

**5.2.1 New field: track current stage**
```csharp
private string _currentStageName = "";  // in the runtime fields section (~line 220)
```

**5.2.2 Extend [`AdvanceToNextStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:354)**

After the existing `ShowNavigationUI(step)` call (line 379), add:
```csharp
// ── Stage name update ──────────────────────────────────────
if (!string.IsNullOrEmpty(step.stageName) && step.stageName != _currentStageName)
{
    _currentStageName = step.stageName;
    UpdateStageTitle(step.stageName, step.stepName, step.instruction);
}

// ── Activate visual objects ────────────────────────────────
if (step.activateOnStepStart != null && step.activateOnStepStart.Length > 0)
{
    foreach (var go in step.activateOnStepStart)
    {
        if (go != null)
        {
            go.SetActive(true);
            Debug.Log($"[ShowWorkingInteractive] Activated visual: {go.name}");
        }
    }
}

// ── Highlight parts + show hover panels ────────────────────
if (step.highlightParts != null && step.highlightParts.Length > 0)
{
    foreach (var part in step.highlightParts)
    {
        if (part == null) continue;
        var enginePart = part.GetComponent<EnginePart>();
        if (enginePart != null)
        {
            partExplorer.SetPartActiveVisual(enginePart);
        }
        var hoverPanel = part.GetComponentInChildren<PartHoverPanel>();
        if (hoverPanel != null)
            hoverPanel.Show();
        else
            Debug.LogWarning($"[ShowWorkingInteractive] Part '{part.name}' has no PartHoverPanel child.", part);
    }
}
```

**5.2.3 New method: `UpdateStageTitle()`**
```csharp
private void UpdateStageTitle(string stageName, string stepName, string instruction)
{
    // Update tablet AND monitor: Part Name = Stage Title, Description = Sub-step info
    if (partExplorer != null)
    {
        partExplorer.SetInteractiveUIText(stageName, $"{stepName}\n{instruction}", 
            _currentStepIndex + 1, steps.Length);
    }
}
```

**5.2.4 Modify [`ShowNavigationUI()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1040)**

When a step has no `stageName` change, keep showing the existing stage title + current step info:
```csharp
private void ShowNavigationUI(ShowWorkingStep step)
{
    if (partExplorer == null) return;

    if (!string.IsNullOrEmpty(_currentStageName))
    {
        // We're inside a stage — show stage title + sub-step description
        partExplorer.SetInteractiveUIText(
            _currentStageName,
            $"{step.stepName}\n{step.instruction}",
            _currentStepIndex + 1,
            steps.Length
        );
    }
    else
    {
        // Normal step (covers, turbine start)
        partExplorer.SetInteractiveUIText(
            step.stepName,
            step.instruction,
            _currentStepIndex + 1,
            steps.Length
        );
    }
    
    partExplorer.ShowNavigationButtons();
}
```

**5.2.5 Extend [`GoBackToPreviousStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:697)**

When going back, deactivate any visuals that were activated for the current step, and re-hide hover panels. Add reversal logic for `activateOnStepStart[]` and `highlightParts[]`.

### 5.3 [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs)

**No changes needed.** The existing `AdvanceAirflowTo()` method already handles:
- Cumulative progress (only advances forward)
- Auto-triggering `StartAirflow()` if covers haven't been removed
- Setting progress on the legacy `JetEngineAirflowController`

### 5.4 [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs)

**No changes needed.** It already polls `CurrentAirflowProgress` every frame and smoothly animates the _Progress MPB value.

### 5.5 [`TabletUIController`](Assets/Scripts/TabletUIController.cs)

**No changes needed.** The `OnNextClicked()` already routes to the interactive controller when it's running.

### 5.6 [`SimplePartExplorer`](Assets/Scripts/PartExplorer/SimplePartExplorer.cs)

**No changes needed.** The `SetInteractiveUIText()` method (line 381) already updates both tablet and monitor text fields.

### 5.7 [`PartHoverPanel`](Assets/Scripts/PartHoverPanel.cs)

**No changes needed.** Already has `Show()` and `Hide()` methods. The controller calls them.

---

## 6. Implementation Phases

### Phase 1: Extend `ShowWorkingStep` data model

**Files to change:**
- [`ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) — add 3 new fields to `ShowWorkingStep` class (~line 1307)

**Estimated effort:** 15 minutes

### Phase 2: Extend controller logic

**Files to change:**
- [`ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs)
  - Add `_currentStageName` field
  - Modify `AdvanceToNextStep()` — add activation + highlighting logic
  - Modify `ShowNavigationUI()` — add stage name awareness
  - Add `UpdateStageTitle()` method
  - Extend `GoBackToPreviousStep()` — add reversal logic

**Estimated effort:** 1-2 hours

### Phase 3: Configure the step array in Inspector

**No code changes — purely Inspector work:**
1. Open the scene with the interactive controller GameObject
2. Expand the `steps[]` array
3. For each stage step:
   - Set `stageName` (e.g. "Stage 1: Air Intake")
   - Set `stepName` (e.g. "Air Intake - Entry")
   - Set `instruction` (educational description text)
   - Set `stepType = PartTap`
   - Drag GameObjects into `activateOnStepStart[]` or `highlightParts[]` as needed
   - Set `airflowProgress` (0.50, 0.70, 0.85, 1.0) where needed

**Estimated effort:** 30 minutes

### Phase 4: Create NEW VISUAL GameObjects

**IMPORTANT CORRECTION — Only 2 visual GameObjects exist (both for Stage 1):** Stages 2-5 do NOT have dedicated visual GameObjects. Their visual effects come from the existing VFX controllers (AirCompressionController, FuelSprayController, CombustionController) already wired into PartTap step types via `PlayStepVFX()`, PLUS the legacy airflow tube advancing to higher percentages via `AdvanceAirflowTo()`.

**What needs to exist in the scene hierarchy — create only 2 disabled GameObjects:**

1. **`Intake_Airflow_Visual`** — Shows the intake airflow entering the engine. Parent as child of engine root or relevant intake part. Starts **disabled** (`SetActive(false)`). Assigned to Stage 1, Sub-step 1's `activateOnStepStart[]` array.

2. **`Bypass_Air_Visual`** — Shows the bypass air animation routing around the core. Parent appropriately. Starts **disabled**. Assigned to Stage 1, Sub-step 2's `activateOnStepStart[]` array.

For **all other stages** (Stages 2-5), leave `activateOnStepStart[]` arrays **empty (Size = 0)**. The existing VFX system handles them:
- Stage 2 (Compression): AirCompressionController fires via PartTap's PlayStepVFX() + legacy airflow → 50%
- Stage 3 (Fuel Injection): FuelSprayController fires via PartTap's PlayStepVFX() + legacy airflow → 70%
- Stage 4 (Conversion of Energy): Existing blade spin VFX + legacy airflow → 85%
- Stage 5 (Exhaust): Legacy airflow → 100% (tube fully lit)

### Phase 5: Prepare HOVER PANEL parts

**For each part that needs a hover panel in stages 2-4:**
- Compressor Blades (Stage 2 — highlightParts)
- Fuel Cap (Stage 3 — highlightParts)
- Fuel Injector (Stage 3 — highlightParts)
- High Pressure Mid Blades (Stage 4 — highlightParts)
- High Pressure Compressor (Stage 4 — highlightParts)
- Rear High Pressure Blades (Stage 4 — highlightParts)

1. Ensure each part has a `PartHoverPanel` component in its children
2. Design the hover panel UI (part name label, line connector)
3. Ensure panels start `SetActive(false)`
4. Drag each part into the appropriate step's `highlightParts[]` array in the Inspector

---

## 7. Airflow Progress Mapping

The user specified these progress values:

| Stage | Action | Legacy Airflow Progress |
|-------|--------|------------------------|
| Turbine Start | Start turbine | 20% (0.20) |
| Stage 2: Air Compression | Advancing compression | 50% (0.50) |
| Stage 3: Combustion | Fuel injection zone | 70% (0.70) |
| Stage 4: Conversion of Energy | HP Turbine entry | 85% (0.85) |
| Stage 5: Exhaust | Full exhaust flow | 100% (1.00) |

**Note:** These values override some existing progress mappings in [`JetEngineShowWorking.progressMappings`](Assets/Scripts/JetEngineShowWorking.cs:43). The legacy explorer after the interactive flow will use whatever progress remains. Since `AdvanceAirflowTo()` is cumulative, the tube will already be at 100% when the explorer starts, so all subsequent legacy parts will see a fully filled tube.

---

## 8. Monitor & Tablet Display Logic

### How the UI updates during stages:

| UI Element | What it shows | How |
|-----------|--------------|-----|
| Monitor Part Name | Stage title (e.g. "Stage 1: Air Intake") | `SimplePartExplorer.SetInteractiveUIText(name=stageName, ...)` |
| Monitor Description | Sub-step info (e.g. "Air Intake - Entry\nAir enters through the intake...") | `SimplePartExplorer.SetInteractiveUIText(..., description=stepName+\n+instruction)` |
| Tablet Part Name | Same stage title | Same call — updates both tablet and monitor |
| Tablet Description | Same sub-step info | Same call |
| Step Counter | "Step X / Total" | Existing behavior unchanged |

### When the stageName changes:
1. The controller detects `step.stageName != _currentStageName`
2. Calls `UpdateStageTitle(stageName, stepName, instruction)`
3. Which calls `partExplorer.SetInteractiveUIText(stageName, combinedDesc, stepNumber, totalSteps)`

### When stageName stays the same (sub-step within same stage):
1. The controller calls `ShowNavigationUI(step)` which detects `_currentStageName` is set
2. Shows the existing stage title + new sub-step info

---

## 9. What Could Go Wrong & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| `activateOnStepStart[]` objects not found in scene | NullReferenceException | Add null checks in the activation loop; log warnings |
| Hover panel parts don't have `PartHoverPanel` component | Panel doesn't show | Log clear warning: `"[...] Part '{name}' has no PartHoverPanel child"` |
| `stageName` not reset on stop/restart | Stale title shown | Reset `_currentStageName = ""` in `StopInteractiveFlow()` and `StartInteractiveFlow()` |
| Previous button doesn't reverse `activateOnStepStart` | Visuals stuck visible | Add deactivation logic in `GoBackToPreviousStep()` |
| Airflow progress mismatch with JetEngineShowWorking mappings | Tube jumps unexpectedly | `AdvanceAirflowTo()` already handles cumulative — only ever advances forward |
| Too many steps in array | User fatigue pressing Next | Each stage has only 2-4 sub-steps; total interactive steps = ~16 (reasonable) |

---

## 10. Summary of Changes

| File | Change Type | Lines Changed |
|------|------------|---------------|
| `ShowWorkingInteractiveController.cs` | Add fields to `ShowWorkingStep` | +15 |
| `ShowWorkingInteractiveController.cs` | Modify `AdvanceToNextStep()` | +35 |
| `ShowWorkingInteractiveController.cs` | Modify `ShowNavigationUI()` | +20 |
| `ShowWorkingInteractiveController.cs` | Add `UpdateStageTitle()` method | +15 |
| `ShowWorkingInteractiveController.cs` | Extend `GoBackToPreviousStep()` | +20 |
| `ShowWorkingInteractiveController.cs` | Reset `_currentStageName` on stop/start | +4 |
| **Total code changes** | | **~109 lines** |
| Inspector configuration | Fill `steps[]` array with 16 entries | 30 min |
| Create NEW VISUAL GameObjects | 2 new GameObjects in hierarchy | Design-dependent |

**No changes needed to:** `JetEngineShowWorking.cs`, `AirflowV61Controller.cs`, `TabletUIController.cs`, `SimplePartExplorer.cs`, `PartHoverPanel.cs`