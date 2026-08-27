# Engine Tutorial System

A complete step-by-step tutorial system for VR engine simulations with synchronized visual effects and UI panels.

## Quick Start

1. **Create Particle Systems** for airflow, combustion, and exhaust
2. **Create TutorialData** ScriptableObject with steps
3. **Add Components** to scene:
   - EngineFlowVisualizer
   - TutorialController
   - TutorialUIPanel
4. **Wire UI** panels and buttons
5. **Launch** tutorial from a button

## Key Features

✓ **Progressive Visualization**: Engine effects reveal step-by-step  
✓ **Smooth Transitions**: Animated fade between states  
✓ **Synchronized UI**: Top panel + tablet panel updates  
✓ **Easy Navigation**: Previous/Next buttons with state management  
✓ **Auto-Play Mode**: Optional automatic step progression  
✓ **Event System**: Broadcast step changes to other systems  
✓ **Fully Customizable**: All colors, timings, and effects adjustable  
✓ **Zero Dependencies**: Works with existing engine code  

## File Structure

```
Tutorial/
├── TutorialStepData.cs      # Data structures
├── EngineFlowVisualizer.cs  # Particle effects
├── TutorialController.cs    # Main orchestrator
├── TutorialUIPanel.cs       # UI management
├── TutorialLauncher.cs      # Button launcher
├── SampleRamjetTutorial.cs  # Example setup
├── SETUP_GUIDE.md           # Detailed setup
└── README.md                # This file
```

## Tutorial Flow

```
Start Tutorial
    ↓
Step 1: Intake (Blue airflow)
    ↓
Step 2: Compression (Airflow continues)
    ↓
Step 3: Fuel Injection (Airflow + chamber highlight)
    ↓
Step 4: Ignition (Airflow + combustion starts)
    ↓
Step 5: Expansion (Full combustion)
    ↓
Step 6: Exhaust (Red exhaust flow)
    ↓
Conclusion (All effects visible)
    ↓
End Tutorial
```

## API Reference

### TutorialController

```csharp
// Start/Stop
tutorialController.StartTutorial();
tutorialController.EndTutorial();

// Navigation
tutorialController.NextStep();
tutorialController.PreviousStep();
tutorialController.GoToStep(int index);

// Query State
bool isActive = tutorialController.IsTutorialActive();
int current = tutorialController.GetCurrentStepIndex();
int total = tutorialController.GetTotalSteps();
TutorialStep step = tutorialController.GetCurrentStep();

// Check Navigation
bool canNext = tutorialController.CanGoNext();
bool canPrev = tutorialController.CanGoPrevious();

// Events
tutorialController.OnStepChanged += (index, step) => { };
tutorialController.OnTutorialStarted += () => { };
tutorialController.OnTutorialEnded += () => { };
```

### TutorialStep

```csharp
public class TutorialStep
{
    public string stepTitle;
    public string stepDescription;
    
    public bool showAirflow;
    public float airflowIntensity;      // 0-1
    
    public bool showCombustion;
    public float combustionIntensity;   // 0-1
    
    public bool showExhaust;
    public float exhaustIntensity;      // 0-1
    
    public List<string> highlightedParts;
    public float transitionDuration;    // seconds
}
```

## Example: Creating a Tutorial

### Via Inspector
1. Right-click → Create → Engine VR → Tutorial → Engine Tutorial Data
2. Add steps in Inspector
3. Set title, description, and effect intensities

### Via Code
```csharp
var tutorialData = ScriptableObject.CreateInstance<TutorialData>();

tutorialData.steps.Add(new TutorialStep
{
    stepTitle = "Intake",
    stepDescription = "Air enters the engine...",
    showAirflow = true,
    airflowIntensity = 1f,
    transitionDuration = 1f
});

// Assign to TutorialController
tutorialController.tutorialData = tutorialData;
```

## Customization Examples

### Change Particle Colors
```csharp
// In EngineFlowVisualizer.cs
airflowColor = new Color(0.2f, 0.6f, 1f, 0.7f);      // Blue
combustionColor = new Color(1f, 0.5f, 0f, 0.8f);     // Orange
exhaustColor = new Color(1f, 0.2f, 0.2f, 0.7f);      // Red
```

### Adjust Emission Rates
```csharp
// In EngineFlowVisualizer.cs UpdateAirflow()
emission.rateOverTime = 50f * _currentAirflowIntensity;  // Adjust 50f
```

### Enable Auto-Play
```csharp
// In Inspector or code
tutorialController.enableAutoPlay = true;
tutorialController.autoPlayStepDuration = 5f;  // 5 seconds per step
```

## Integration Notes

- **Independent**: Doesn't modify existing engine scripts
- **Non-Intrusive**: Adds alongside existing systems
- **Event-Based**: Uses events for loose coupling
- **Modular**: Each component can be used separately

## Performance

- Particle pooling: Reuses particles efficiently
- Smooth transitions: Uses interpolation, not frame-by-frame updates
- Material updates: Uses property blocks (no allocation)
- VR-Ready: Minimal overhead, suitable for VR headsets

## Support

For issues or questions:
1. Check SETUP_GUIDE.md for detailed instructions
2. Review SampleRamjetTutorial.cs for example implementation
3. Check console for debug warnings
4. Verify all components are assigned in Inspector

## License

Part of EngineVR Simulation project.
