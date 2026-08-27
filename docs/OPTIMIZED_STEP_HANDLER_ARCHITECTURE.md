# Optimized Step Handler Architecture — Strategy Pattern

## 1. The Problem

Currently, adding a new [`InteractiveStepType`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:1285) requires modifying **6 separate switch statements** scattered throughout [`ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs):

| # | Method | Line | Purpose |
|---|--------|------|---------|
| 1 | [`OnNextPressed()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:299) | 309 | Routes Next button press |
| 2 | [`AdvanceToNextStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:354) | 382 | Sets up step visuals/state |
| 3 | [`GoBackToPreviousStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:697) | 706 | Reverses step on Previous |
| 4 | [`GetAirflowForStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:833) | 837 | Calculates progress contribution |
| 5 | [`PlayStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:890) | 892 | Routes VFX playback |
| 6 | [`StopCurrentStepVFX()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:934) | 941 | Stops active VFX |

This is:
- **Fragile** — you must remember to update all 6 or a new step type won't work properly
- **Time-consuming** — every new step type = changes in 6+ locations
- **Error-prone** — the Previous/GoBack switch is especially easy to miss

## 2. The Solution: Strategy Pattern

Instead of a big switch statement in the controller, each step type gets its own **handler class** that implements a common interface. The controller only talks to the interface — it never needs to know which concrete type it's dealing with.

### 2.1 Core Interface

```csharp
// Every step type handler implements this
public interface IShowWorkingHandler
{
    void Setup(ShowWorkingStep step);
    void Execute(ShowWorkingStep step, Action onComplete);
    void Revert(ShowWorkingStep step);
    float GetAirflowProgress(ShowWorkingStep step);
    void StopVFX(ShowWorkingStep step);
}
```

### 2.2 Handler Registry

```csharp
// One-time registration — every handler registers itself
// The controller only has ONE lookup call
public class ShowWorkingHandlerRegistry
{
    private Dictionary<InteractiveStepType, IShowWorkingHandler> _handlers;

    public void Register(InteractiveStepType type, IShowWorkingHandler handler)
    {
        _handlers[type] = handler;
    }

    public IShowWorkingHandler Get(InteractiveStepType type)
    {
        return _handlers.TryGetValue(type, out var handler) ? handler : null;
    }
}
```

### 2.3 Controller Becomes Stateless

The controller's 6 switch statements collapse into **6 one-liner lookups**:

```csharp
// Before (BIG switch — 6 cases):
switch (step.stepType)
{
    case InteractiveStepType.GrabRemove:  StartGrabRemoveStep(step); break;
    case InteractiveStepType.TurbineStart: ... break;
    case InteractiveStepType.PartTap:     ... break;
    case InteractiveStepType.IgniteButton: ... break;
    case InteractiveStepType.BladeSpin:   ... break;
}

// After (ONE lookup — no cases):
var handler = _registry.Get(step.stepType);
handler?.Setup(step);
```

## 3. Complete Architecture

### 3.1 New Files

We create a new folder: `Assets/Scripts/ShowWorking/Handlers/`

```
Assets/Scripts/ShowWorking/
├── Handlers/
│   ├── IShowWorkingHandler.cs           ← Interface
│   ├── ShowWorkingHandlerRegistry.cs    ← Registry
│   ├── GrabRemoveHandler.cs             ← GrabRemove logic
│   ├── TurbineStartHandler.cs           ← TurbineStart logic
│   ├── PartTapHandler.cs                ← PartTap logic
│   ├── IgniteButtonHandler.cs           ← IgniteButton logic
│   └── BladeSpinHandler.cs              ← BladeSpin logic
├── ShowWorkingInteractiveController.cs  ← Simplified controller
└── ShowWorkingStep.cs                   ← Unchanged (data only)
```

### 3.2 Each Handler Owns Its Logic

**GrabRemoveHandler.cs** handles:
- `Setup()` — save original position, call `showWorking.OnPartShown()`, highlight + lift part, enable grab events
- `Execute()` — hide part (auto-skip) OR wait for physical grab → `CompleteCurrentStep()`
- `Revert()` — lower part, restore position, reverse airflow via registry helper
- `GetAirflowProgress()` — call `showWorking.GetProgressForPart()`

**TurbineStartHandler.cs** handles:
- `Setup()` — show `startTurbineButton`, play step audio
- `Execute()` — find TurbineBladeRotator, start rotation, advance airflow, call `onComplete()`
- `Revert()` — stop turbine, hide button
- `GetAirflowProgress()` — return `step.turbineStartAirflowProgress`

**PartTapHandler.cs** handles:
- `Setup()` — highlight + lift target part, play step audio
- `Execute()` — call `PlayStepVFX()` (compression or fuel), advance airflow, lower part, call `onComplete()`
- `Revert()` — lower part, stop VFX
- `GetAirflowProgress()` — check both `airflowProgress` and `airCompressionController`/`fuelSprayController`

**IgniteButtonHandler.cs** handles:
- `Setup()` — show ignite button, play step audio
- `Execute()` — trigger slow-mo, play combustion VFX, advance airflow to 1.0, resume time, call `onComplete()`
- `Revert()` — hide button, resume time, stop combustion
- `GetAirflowProgress()` — return `airflowProgress` or 1.0

**BladeSpinHandler.cs** handles:
- `Setup()` — play step audio, log info
- `Execute()` — create runtime parent, add TurbineBladeRotator, start spin, advance airflow, call `onComplete()`
- `Revert()` — stop rotation, restore blade parents, destroy runtime parent
- `GetAirflowProgress()` — return `airflowProgress` or 0

### 3.3 Controller Becomes Simple

```csharp
public class ShowWorkingInteractiveController : MonoBehaviour
{
    public ShowWorkingStep[] steps;
    public SimplePartExplorer partExplorer;
    public JetEngineShowWorking showWorking;
    // ... other serialized fields ...

    private ShowWorkingHandlerRegistry _registry;
    private int _currentStepIndex = -1;

    void Awake()
    {
        _registry = new ShowWorkingHandlerRegistry();
        _registry.Register(InteractiveStepType.GrabRemove,   new GrabRemoveHandler(this));
        _registry.Register(InteractiveStepType.TurbineStart, new TurbineStartHandler(this));
        _registry.Register(InteractiveStepType.PartTap,      new PartTapHandler(this));
        _registry.Register(InteractiveStepType.IgniteButton, new IgniteButtonHandler(this));
        _registry.Register(InteractiveStepType.BladeSpin,    new BladeSpinHandler(this));
    }

    public void OnNextPressed()
    {
        var step = steps[_currentStepIndex];
        var handler = _registry.Get(step.stepType);
        handler?.Execute(step, CompleteCurrentStep);
    }

    private void AdvanceToNextStep()
    {
        var step = steps[++_currentStepIndex];
        var handler = _registry.Get(step.stepType);
        ShowNavigationUI(step);
        handler?.Setup(step);
    }

    private void GoBackToPreviousStep()
    {
        var prevStep = steps[_currentStepIndex];
        var handler = _registry.Get(prevStep.stepType);
        handler?.Revert(prevStep);
        handler?.StopVFX(prevStep);
        _currentStepIndex--;
        // Show previous step's setup
        var step = steps[_currentStepIndex];
        var prevHandler = _registry.Get(step.stepType);
        prevHandler?.Setup(step);
    }

    private float GetAirflowForStep(ShowWorkingStep step)
    {
        var handler = _registry.Get(step.stepType);
        return handler?.GetAirflowProgress(step) ?? 0f;
    }
}
```

## 4. Adding a New Step Type (The One-Location Change)

### Before (Current System):
To add a new `InteractiveStepType`:
1. Add enum value
2. Add case in `OnNextPressed()`
3. Add case in `AdvanceToNextStep()`
4. Add case in `GoBackToPreviousStep()`
5. Add case in `GetAirflowForStep()`
6. Add case in `PlayStepVFX()`
7. Add case in `StopCurrentStepVFX()`
8. Add new fields to `ShowWorkingStep`

**That's 7-8 modifications across 1 file.**

### After (Strategy Pattern):
To add a new `InteractiveStepType`:
1. Add enum value
2. Create one new file: `NewStepTypeHandler.cs` implementing `IShowWorkingHandler`
3. Register it in `Awake()` with one line
4. Add new fields to `ShowWorkingStep`

**That's 3 modifications — one being a new file, one being a registration line, one being data fields.**

## 5. Concrete Example: Adding "FuelDrain" Step Type

### Step 1: Add enum value
```csharp
// InteractiveStepType.cs (extracted from controller)
public enum InteractiveStepType
{
    GrabRemove,
    TurbineStart,
    PartTap,
    IgniteButton,
    BladeSpin,
    FuelDrain  // ← NEW
}
```

### Step 2: Create one new file
```csharp
// Handlers/FuelDrainHandler.cs
public class FuelDrainHandler : IShowWorkingHandler
{
    private readonly ShowWorkingInteractiveController _ctrl;

    public FuelDrainHandler(ShowWorkingInteractiveController ctrl) => _ctrl = ctrl;

    public void Setup(ShowWorkingStep step)
    {
        // Highlight fuel drain valve
        var ep = step.targetPart?.GetComponent<EnginePart>();
        ep?.SetVisible(true);
        ep?.SetShowWorkingActive();
        _ctrl.partExplorer?.SetAllOtherPartsBackground(ep);
        _ctrl.PlayStepAudio(step);
    }

    public void Execute(ShowWorkingStep step, Action onComplete)
    {
        // Animate fuel draining — use FuelSprayController in reverse
        step.fuelSprayController?.StartSpray(() => {
            _ctrl.showWorking?.SetAirflowProgressDirect(step.airflowProgress, cumulative: true);
            onComplete?.Invoke();
        });
    }

    public void Revert(ShowWorkingStep step)
    {
        step.fuelSprayController?.StopSpray();
        var ep = step.targetPart?.GetComponent<EnginePart>();
        ep?.LowerDown(_ctrl.liftDuration * 0.5f);
    }

    public float GetAirflowProgress(ShowWorkingStep step)
        => step.airflowProgress >= 0 ? step.airflowProgress : 0f;

    public void StopVFX(ShowWorkingStep step)
        => step.fuelSprayController?.StopSpray();
}
```

### Step 3: Register in Awake()
```csharp
void Awake()
{
    _registry.Register(InteractiveStepType.FuelDrain, new FuelDrainHandler(this));
}
```

### Step 4: Add fields to ShowWorkingStep (if needed)
```csharp
[Header("Fuel Drain (FuelDrain type)")]
public FuelSprayController fuelSprayController; // ← already exists
```

**That's it. No switch statements touched. No existing code modified.**

## 6. Handler Access to Controller

Handlers need access to the controller's:
- `partExplorer` — for UI updates
- `showWorking` — for airflow progress
- `grabManager` — for grab events (GrabRemove only)
- `audioSource` — for step audio
- `liftDuration`, `liftAmount` — for animation parameters
- `startTurbineButton`, `igniteButton` — for button visibility

These are passed via the constructor (the handler holds a reference to the controller) or via the `ShowWorkingStep` itself which already has all VFX controller references.

## 7. Backward Compatibility

The `ShowWorkingInteractiveController` remains 100% compatible:
- All existing `steps[]` serialized arrays keep working
- All existing Inspector configurations stay valid
- `TabletUIController` button routing stays unchanged
- `EngineViewManager.ActivateShowWorkingView()` stays unchanged
- `AirflowV61Controller` is unaffected

## 8. Migration Path

Phase 1 — Create the framework (no functional change):
1. Create `Assets/Scripts/ShowWorking/Handlers/` folder
2. Create `IShowWorkingHandler.cs` — the interface
3. Create `ShowWorkingHandlerRegistry.cs` — the registry
4. Extract `InteractiveStepType` to its own file
5. Extract `ShowWorkingStep` to its own file

Phase 2 — Move logic into handlers (no functional change):
1. Create `GrabRemoveHandler.cs` — move GrabRemove logic from controller
2. Create `TurbineStartHandler.cs` — move TurbineStart logic
3. Create `PartTapHandler.cs` — move PartTap logic
4. Create `IgniteButtonHandler.cs` — move IgniteButton logic
5. Create `BladeSpinHandler.cs` — move BladeSpin logic

Phase 3 — Refactor controller (no functional change):
1. Replace 6 switch statements with registry lookups
2. Delete `AutoExecute*()` and `Start*Step()` methods from controller
3. Keep `CompleteCurrentStep()`, `CompleteAllSteps()`, `TransitionToLegacyExplorer()` in controller
4. Keep `ShowNavigationUI()`, `PlayStepAudio()` in controller

Phase 4 — Add new step types:
1. Create new handler file
2. Register in `Awake()`
3. Add step to Inspector

## 9. Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Handler forgets to call `onComplete` | Default timeout fallback in registry |
| Handler uses wrong controller reference | Constructor injection — reference is read-only |
| Existing scenes break | No scene changes needed — serialized fields unchanged |
| Performance overhead | Dictionary lookup by enum (O(1)) — negligible |
| Circular dependencies | Handlers are one-way: Handler → Controller. Controller → Registry → Handler |

## 10. File Summary

| File | Action | Lines |
|------|--------|-------|
| `Handlers/IShowWorkingHandler.cs` | **CREATE** | ~15 |
| `Handlers/ShowWorkingHandlerRegistry.cs` | **CREATE** | ~30 |
| `Handlers/GrabRemoveHandler.cs` | **CREATE** | ~80 |
| `Handlers/TurbineStartHandler.cs` | **CREATE** | ~60 |
| `Handlers/PartTapHandler.cs` | **CREATE** | ~70 |
| `Handlers/IgniteButtonHandler.cs` | **CREATE** | ~70 |
| `Handlers/BladeSpinHandler.cs` | **CREATE** | ~70 |
| `ShowWorkingInteractiveController.cs` | **REFACTOR** | 1389 → ~500 |
| `InteractiveStepType.cs` (extract) | **CREATE** | ~20 |
| `ShowWorkingStep.cs` (extract) | **CREATE** | ~90 |

**Total: ~500 new lines, ~900 removed, ~500 refactored.**