# Engine Tutorial System - Setup Guide

## Overview
This tutorial system provides step-by-step visualization of engine processes with synchronized UI panels and particle effects.

## Components

### 1. **TutorialStepData.cs**
- `TutorialStep`: Defines a single tutorial step with title, description, and visual effects
- `TutorialData`: ScriptableObject containing all steps for an engine

### 2. **EngineFlowVisualizer.cs**
- Manages particle systems for airflow, combustion, and exhaust
- Smoothly transitions between visual states
- Handles particle emission rates and colors

### 3. **TutorialController.cs**
- Main orchestrator for tutorial flow
- Manages step progression (Next/Previous)
- Broadcasts events for UI synchronization
- Supports auto-play mode

### 4. **TutorialUIPanel.cs**
- Updates top panel with step title and description
- Manages tablet panel with navigation buttons
- Handles button state (enabled/disabled)

### 5. **TutorialLauncher.cs**
- Simple helper to start tutorial from a button click

## Setup Instructions

### Step 1: Create Particle Systems
In your engine scene, create three empty GameObjects with ParticleSystem components:

1. **Airflow Particles**
   - Position: At engine intake
   - Color: Blue (0.2, 0.6, 1.0)
   - Emission: 50 particles/sec (will be controlled by script)
   - Lifetime: 2-3 seconds
   - Velocity: Forward direction

2. **Combustion Particles**
   - Position: At combustion chamber
   - Color: Orange (1.0, 0.5, 0.0)
   - Emission: 80 particles/sec
   - Lifetime: 1-2 seconds
   - Velocity: Radial outward

3. **Exhaust Particles**
   - Position: At engine exhaust
   - Color: Red (1.0, 0.2, 0.2)
   - Emission: 60 particles/sec
   - Lifetime: 2-3 seconds
   - Velocity: Backward direction

### Step 2: Create Tutorial Data
Option A: Create via Inspector
- Right-click in Assets folder → Create → Engine VR → Tutorial → Engine Tutorial Data
- Name it (e.g., "RamjetTutorial")
- Add steps in the Inspector

Option B: Use SampleRamjetTutorial.cs
- Attach to any GameObject in scene
- It will populate tutorial data programmatically

### Step 3: Set Up Scene Objects
1. Create an empty GameObject called "TutorialSystem"
2. Add these components:
   - **EngineFlowVisualizer**
     - Assign the three particle systems
     - Adjust colors if needed
   - **TutorialController**
     - Assign TutorialData
     - Assign EngineFlowVisualizer
     - Enable/disable auto-play as needed

### Step 4: Create UI Panels
1. **Top Panel** (Canvas)
   - Create a Panel at top of screen
   - Add TextMeshProUGUI for title
   - Add TextMeshProUGUI for description
   - Style as needed

2. **Tablet Panel** (Existing Tablet)
   - Add TextMeshProUGUI for step counter
   - Add Button for "Previous"
   - Add Button for "Next"
   - Position on left side of tablet

### Step 5: Wire Up TutorialUIPanel
1. Add TutorialUIPanel component to a UI manager GameObject
2. Assign:
   - TutorialController reference
   - Top Panel GameObject
   - Step Title Text
   - Step Description Text
   - Tablet Panel GameObject
   - Previous Button
   - Next Button
   - Step Counter Text

### Step 6: Add Tutorial Launch Button
1. Add a button to your existing tablet UI (e.g., "Show Working")
2. Add TutorialLauncher component to it
3. Assign TutorialController reference
4. Wire button's OnClick → TutorialLauncher.LaunchTutorial()

## Usage

### Starting Tutorial
```csharp
tutorialController.StartTutorial();
```

### Navigation
```csharp
tutorialController.NextStep();
tutorialController.PreviousStep();
tutorialController.GoToStep(stepIndex);
```

### Stopping Tutorial
```csharp
tutorialController.EndTutorial();
```

### Checking State
```csharp
bool isActive = tutorialController.IsTutorialActive();
int currentStep = tutorialController.GetCurrentStepIndex();
int totalSteps = tutorialController.GetTotalSteps();
```

## Customization

### Adjust Particle Effects
Edit in EngineFlowVisualizer.cs:
- `airflowColor`: Change blue color
- `combustionColor`: Change orange color
- `exhaustColor`: Change red color
- Emission rates in UpdateAirflow(), UpdateCombustion(), UpdateExhaust()

### Modify Step Transitions
In TutorialStep:
- `transitionDuration`: How long to fade between steps (default 1 second)
- `airflowIntensity`: 0-1 scale for airflow strength
- `combustionIntensity`: 0-1 scale for combustion strength
- `exhaustIntensity`: 0-1 scale for exhaust strength

### Auto-Play Mode
In TutorialController Inspector:
- Enable "Enable Auto Play"
- Set "Auto Play Step Duration" (seconds per step)

## Troubleshooting

### Particles not showing
- Check particle system is assigned in EngineFlowVisualizer
- Verify particle system has correct material
- Check particle lifetime and emission settings

### UI not updating
- Ensure TutorialUIPanel is assigned to TutorialController
- Check TextMeshProUGUI components are assigned
- Verify buttons are wired correctly

### Steps not progressing
- Check TutorialData has steps added
- Verify TutorialController is assigned TutorialData
- Check console for warnings

## Example: Ramjet Engine Tutorial

The system includes a sample tutorial for Ramjet engines with 8 steps:
1. Introduction
2. Intake (airflow only)
3. Compression (airflow)
4. Fuel Injection (airflow)
5. Ignition (airflow + combustion)
6. Expansion (airflow + combustion)
7. Exhaust (all effects)
8. Conclusion (all effects)

Each step progressively reveals more of the engine's working process.

## Performance Notes

- Particle systems are pooled and reused
- Transitions use smooth interpolation (not frame-by-frame)
- Material updates use property blocks (no allocation)
- Suitable for VR with minimal overhead

## Integration with Existing Code

The tutorial system is completely independent and doesn't modify:
- EngineGrabManager
- EngineViewManager
- TabletUIController
- Any existing engine scripts

Simply add the tutorial components alongside existing systems.
