# Integration with Existing Engine VR Code

This guide explains how the tutorial system integrates with your existing codebase without modifying any core scripts.

## Architecture Overview

```
Existing Systems          Tutorial System
─────────────────         ───────────────
EngineGrabManager    ←→   TutorialController
EngineViewManager         EngineFlowVisualizer
TabletUIController   ←→   TutorialUIPanel
EngineInteractor         TutorialLauncher
EnginePart
```

## Non-Intrusive Integration

The tutorial system is completely independent:

✓ **No modifications** to EngineGrabManager.cs  
✓ **No modifications** to EngineViewManager.cs  
✓ **No modifications** to TabletUIController.cs  
✓ **No modifications** to EngineInteractor.cs  
✓ **No modifications** to any existing engine scripts  

All tutorial functionality is self-contained in the Tutorial folder.

## How It Works

### 1. Parallel UI System
- Existing tablet UI continues to work normally
- Tutorial UI panels are added alongside (not replacing)
- Both can coexist without conflicts

### 2. Independent Particle Effects
- Tutorial uses separate particle systems
- Doesn't interfere with existing engine visuals
- Can be toggled on/off independently

### 3. Event-Based Communication
- Tutorial broadcasts events when steps change
- Other systems can listen if they want
- No forced dependencies

## Adding Tutorial to Your Scene

### Step 1: Add Tutorial Components
In your main engine scene, add these GameObjects:

```
Scene
├── [Existing] Engine
├── [Existing] Tablet
├── [Existing] UI Canvas
├── [NEW] TutorialSystem
│   ├── EngineFlowVisualizer (component)
│   ├── TutorialController (component)
│   └── [Particle Systems]
│       ├── Airflow Particles
│       ├── Combustion Particles
│       └── Exhaust Particles
└── [NEW] TutorialUIManager
    └── TutorialUIPanel (component)
```

### Step 2: Wire Tutorial Button
Add to your existing tablet UI:

```csharp
// In your existing tablet button setup
public void OnShowWorkingClicked()
{
    tutorialLauncher.LaunchTutorial();
}
```

Or simply add TutorialLauncher to the button:
- Add TutorialLauncher component
- Assign TutorialController
- Wire button's OnClick → TutorialLauncher.LaunchTutorial()

### Step 3: Optional - Listen to Tutorial Events
If you want other systems to react to tutorial steps:

```csharp
// In any existing script
public class MyEngineSystem : MonoBehaviour
{
    private TutorialController tutorialController;
    
    void Start()
    {
        tutorialController = FindFirstObjectByType<TutorialController>();
        if (tutorialController != null)
        {
            tutorialController.OnStepChanged += HandleTutorialStep;
        }
    }
    
    void HandleTutorialStep(int stepIndex, TutorialStep step)
    {
        // React to tutorial step changes
        Debug.Log($"Tutorial step {stepIndex}: {step.stepTitle}");
    }
}
```

## Coexistence with Existing Features

### With EngineGrabManager
- Tutorial particles don't interfere with grab interactions
- Users can grab engine parts while tutorial is running
- Grab mode can be toggled independently

### With EngineViewManager
- Tutorial works with all view modes (default, X-ray, exploded)
- Particle effects adapt to current view
- View buttons remain functional during tutorial

### With TabletUIController
- Tutorial UI panels are separate from tablet UI
- Existing tablet buttons continue to work
- Tutorial can be launched from tablet or elsewhere

### With EngineInteractor
- Part selection/hovering works during tutorial
- Part info panels can show alongside tutorial
- No conflicts with existing interaction system

## Example: Complete Integration

Here's a complete example of adding tutorial to your existing scene:

### 1. Create Tutorial Data (Inspector)
```
Right-click in Assets
→ Create → Engine VR → Tutorial → Engine Tutorial Data
Name: "RamjetTutorial"
Add steps with titles and descriptions
```

### 2. Add Components to Scene
```
Create empty GameObject "TutorialSystem"
├── Add EngineFlowVisualizer
│   ├── Assign Airflow Particles
│   ├── Assign Combustion Particles
│   └── Assign Exhaust Particles
└── Add TutorialController
    ├── Assign TutorialData
    └── Assign EngineFlowVisualizer
```

### 3. Add UI Panels
```
In your Canvas, add:
├── Top Panel (for step title/description)
└── Tablet Panel (for navigation buttons)
```

### 4. Wire Everything
```
Create "TutorialUIManager" GameObject
Add TutorialUIPanel component
├── Assign TutorialController
├── Assign all UI elements
└── Enable auto-hide
```

### 5. Add Launch Button
```
On your existing "Show Working" button:
├── Add TutorialLauncher component
├── Assign TutorialController
└── Wire button's OnClick → LaunchTutorial()
```

## Performance Impact

The tutorial system has minimal performance overhead:

- **Particle Systems**: Standard Unity particles (optimized)
- **UI Updates**: Only when step changes (not every frame)
- **Memory**: ~2-3 MB for tutorial data and components
- **CPU**: Negligible (smooth interpolation, not frame-by-frame)
- **GPU**: Depends on particle count (adjustable)

## Customization Without Code Changes

All customization can be done in the Inspector:

### TutorialData
- Add/remove/edit steps
- Adjust titles and descriptions
- Set effect intensities
- Configure transition durations

### EngineFlowVisualizer
- Change particle colors
- Adjust emission rates
- Modify particle lifetime

### TutorialController
- Enable/disable auto-play
- Set auto-play duration
- Adjust transition speeds

### TutorialUIPanel
- Show/hide panels
- Adjust panel positions
- Customize button styles

## Troubleshooting Integration

### Tutorial doesn't start
- Check TutorialController is in scene
- Verify TutorialData is assigned
- Check console for errors

### Particles don't show
- Verify particle systems are assigned
- Check particle system materials
- Ensure particles aren't culled

### UI doesn't update
- Verify TutorialUIPanel is assigned
- Check all UI elements are assigned
- Verify buttons are wired

### Conflicts with existing features
- Tutorial system is independent
- Check for naming conflicts
- Verify no script modifications

## Advanced Integration

### Listen to Tutorial Events
```csharp
tutorialController.OnStepChanged += (index, step) => 
{
    // Do something when step changes
};

tutorialController.OnTutorialStarted += () => 
{
    // Do something when tutorial starts
};

tutorialController.OnTutorialEnded += () => 
{
    // Do something when tutorial ends
};
```

### Control Tutorial Programmatically
```csharp
// Start tutorial
tutorialController.StartTutorial();

// Navigate
tutorialController.NextStep();
tutorialController.PreviousStep();
tutorialController.GoToStep(2);

// Stop
tutorialController.EndTutorial();

// Query state
bool active = tutorialController.IsTutorialActive();
int current = tutorialController.GetCurrentStepIndex();
```

### Extend Tutorial Functionality
```csharp
// Create custom tutorial launcher
public class CustomTutorialLauncher : MonoBehaviour
{
    public void LaunchTutorialForEngine(string engineType)
    {
        // Load appropriate tutorial data
        // Start tutorial
        // Show custom UI
    }
}
```

## Migration Path

If you want to integrate tutorial into existing UI:

1. **Phase 1**: Add tutorial as separate system (current approach)
2. **Phase 2**: Add tutorial button to existing tablet UI
3. **Phase 3**: Integrate tutorial panels into existing UI layout
4. **Phase 4**: Add tutorial to other engine types

Each phase is independent and can be done without affecting others.

## Support

For integration questions:
1. Check this file for common scenarios
2. Review SETUP_GUIDE.md for detailed setup
3. Check IMPLEMENTATION_CHECKLIST.md for step-by-step guide
4. Review example code in SampleRamjetTutorial.cs

## Summary

The tutorial system is designed to:
- ✓ Work alongside existing code
- ✓ Require no modifications to existing scripts
- ✓ Be easily customizable
- ✓ Have minimal performance impact
- ✓ Be easy to integrate and remove

Simply add the tutorial components to your scene and wire up the UI buttons. Everything else is self-contained.
