# Rebuilt Show Working Architecture

## Design Principles

1. **Strategy Pattern** — No switch statements. Each step type has its own handler class.
2. **Separation of Concerns** — Step data, step logic, and orchestration are in separate files.
3. **First-Class Educational Stages** — Not patched in; handled naturally via stage manager.
4. **Backward Compatible API** — TabletUIController's calls to the controller remain unchanged.

## File Structure

```
Assets/Scripts/ShowWorking/
  ├── InteractiveStepType.cs        ← enum (extracted from monolithic file)
  ├── RuntimeBladeGroup.cs          ← class (extracted from monolithic file)
  ├── ShowWorkingStep.cs            ← serializable step data model (cleaned)
  ├── StepContext.cs                ← context passed to handlers
  ├── IStepHandler.cs               ← handler interface
  ├── StepHandlers.cs               ← all 5 handler implementations
  ├── StageAutoPopulate.cs          ← ContextMenu helper (moved out)
  └── ShowWorkingInteractiveController.cs ← orchestrator (~250 lines)
```

## StepContext

A class holding all references + state that handlers need:

| Field | Used By |
|-------|---------|
| `showWorking` | All |
| `partExplorer` | GrabRemove, PartTap |
| `grabManager` | GrabRemove |
| `audioSource` | All |
| `liftAmount/liftDuration` | GrabRemove, PartTap |
| `defaultAdvanceDistance` | GrabRemove |
| `currentTargetPart/originalPos` | GrabRemove |
| `currentEnginePart/grabController` | GrabRemove |
| `correctPartGrabbed` | GrabRemove |
| `tapTargetPart` | PartTap |
| `turbineBladeRotator` | TurbineStart |
| `activeBladeGroups` | BladeSpin |
| `originalPositions` | GrabRemove |
| `playStepAudio(step)` | All |
| `completeAndAdvance()` | All |
| `getAirflowForStep(step)` | Previous rollback |

## Handler Interface

```csharp
public interface IStepHandler
{
    void OnStepEnter(ShowWorkingStep step, StepContext ctx);
    void OnNextPressed(ShowWorkingStep step, StepContext ctx);
    void OnStepExit(ShowWorkingStep step, StepContext ctx); // cleanup on Previous
    void Cleanup(ShowWorkingStep step, StepContext ctx);    // full cleanup on stop
}
```

## Controller Orchestration Flow

### StartInteractiveFlow()
1. Set `_isRunning = true`, reset state
2. Setup grab manager events
3. Call `AdvanceToNextStep()`

### AdvanceToNextStep()
1. Increment `_currentStepIndex`
2. Save `_airflowAtStepStart`
3. **HandleStageTransition** — check `stageName` change, update `_currentStageName`
4. **ActivateVisualsForStep** — `activateOnStepStart[]`
5. **ShowHighlightPanelsForStep** — `highlightParts[]`
6. **ShowNavigationUI** — update tablet display
7. **Delegate to handler's `OnStepEnter`** — no switch

### OnNextPressed()
1. Guard checks
2. **Delegate to handler's `OnNextPressed`** — no switch
3. Handler calls `ctx.completeAndAdvance()` when done

### OnPreviousPressed()
1. Stop VFX, lower parts, stop turbine/blades
2. **Delegate to handler's `OnStepExit`** — no switch
3. Deactivate visuals, hide panels
4. Rollback airflow
5. Decrement index, re-show previous step

### Handler: GrabRemove
- **OnStepEnter**: Save original pos, show part highlighted + lifted, call OnPartShown for airflow
- **OnNextPressed**: Hide part, call completeAndAdvance
- **OnStepExit**: Restore part to original position, lower down
- **Cleanup**: Restore part if still grabbed

### Handler: TurbineStart
- **OnStepEnter**: Show startTurbineButton, play audio
- **OnNextPressed**: Hide button, find TurbineBladeRotator, StartRotation(), AdvanceAirflowTo()
- **OnStepExit**: Stop rotation, hide button
- **Cleanup**: Stop rotation if active

### Handler: PartTap
- **OnStepEnter**: Show target part highlighted + lifted, play audio
- **OnNextPressed**: Play VFX (AirCompression/FuelSpray), advance airflow, lower part
- **OnStepExit**: Lower part if lifted
- **Cleanup**: Reset VFX

### Handler: IgniteButton
- **OnStepEnter**: Show igniteButton, play audio
- **OnNextPressed**: Hide button, play combustion VFX, advance airflow, slow-motion
- **OnStepExit**: Hide button, resume normal time
- **Cleanup**: Stop combustion, resume normal time

### Handler: BladeSpin
- **OnStepEnter**: Log blade count, play audio
- **OnNextPressed**: Create runtime parent, reparent blades, add TurbineBladeRotator, StartRotation(), advance airflow
- **OnStepExit**: Stop rotation, restore blade parents, destroy temp parent
- **Cleanup**: Same as OnStepExit

## Educational Stage Handling

Stays in the controller as before — it's a **horizontal concern** applied to ALL step types:

```
AdvanceToNextStep:
  → HandleStageTransition(step)   ← checks stageName, updates _currentStageName
  → ActivateVisualsForStep(step)  ← activateOnStepStart[] SetActive(true)
  → ShowHighlightPanelsForStep(step) ← PartHoverPanel Show()
  → ShowNavigationUI(step)        ← prepends _currentStageName to display name
  → handler.OnStepEnter(step, ctx)
```

On Previous, the reverse happens:
```
GoBackToPreviousStep:
  → handler.OnStepExit(step, ctx)
  → DeactivateAllVisualsForStep(step)
  → HideAllHighlightPartsForStep(step)
  → (decrement index, then re-activate previous step's visuals)
```

## Auto-Populate

The `PopulateEducationalStageSteps()` ContextMenu method is moved to `StageAutoPopulate.cs` as a static method with a wrapper ContextMenu on the controller that calls it. This keeps the controller clean.

## Backward Compatibility

All TabletUIController-facing APIs remain identical:

| Method/Property | Signature | Unchanged |
|----------------|-----------|-----------|
| `StartInteractiveFlow()` | `public void` | ✓ |
| `StopInteractiveFlow()` | `public void` | ✓ |
| `OnNextPressed()` | `public void` | ✓ |
| `OnPreviousPressed()` | `public void` | ✓ |
| `OnTurbineStarted()` | `public void` | ✓ |
| `OnIgniteButtonPressed()` | `public void` | ✓ |
| `OnSpecialButtonPressed()` | `public void` | ✓ |
| `CurrentStepIndex` | `public int` | ✓ |
| `TotalSteps` | `public int` | ✓ |
| `IsRunning` | `public bool` | ✓ |
| `HasCompleted` | `public bool` | ✓ |
| `OnAllStepsCompleted` | `public Action` | ✓ |
| `OnStepCompleted` | `public Action<int>` | ✓ |

All serialized Inspector fields are preserved with the same names so the user's existing Scene configuration is NOT lost.