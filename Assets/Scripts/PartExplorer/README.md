# Part Explorer System

A simple system to explore engine parts one at a time with highlighting and fading effects.

## Features

✓ **One Part at a Time** - Highlight current part, fade others  
✓ **Easy Navigation** - Previous/Next buttons  
✓ **Part Info Display** - Name and description on tablet  
✓ **Smooth Transitions** - Fade effects between parts  
✓ **Auto Button State** - Buttons enable/disable at boundaries  
✓ **Zero Code Modifications** - Works with existing code  

## Quick Start

1. Create PartExplorerData ScriptableObject
2. Add all engine parts to it
3. Add PartExplorerController to scene
4. Create UI elements on tablet
5. Add PartExplorerUIPanel
6. Wire "Show Working" button to PartExplorerLauncher

See SETUP_GUIDE.md for detailed instructions.

## How It Works

```
User clicks "Show Working"
    ↓
PartExplorerLauncher.LaunchExplorer()
    ↓
PartExplorerController.StartExplorer()
    ↓
First part highlights, others fade
    ↓
UI updates with part name/description
    ↓
User clicks Next/Previous
    ↓
Parts transition smoothly
    ↓
UI updates
    ↓
User reaches last part and clicks Next
    ↓
Explorer ends, all parts restore
```

## Components

### PartExplorerData
- ScriptableObject containing all parts
- Each part has name, description, and EnginePart reference

### PartExplorerController
- Main orchestrator
- Manages part highlighting and fading
- Broadcasts events for UI sync

### PartExplorerUIPanel
- Updates UI elements
- Manages button states
- Listens to controller events

### PartExplorerLauncher
- Simple button launcher
- Starts/stops explorer

## API

### PartExplorerController

```csharp
// Start/Stop
StartExplorer()
EndExplorer()

// Navigation
NextPart()
PreviousPart()
GoToPart(int index)

// Query
IsExplorerActive() → bool
GetCurrentPartIndex() → int
GetCurrentPart() → ExplorerPart
GetTotalParts() → int
CanGoNext() → bool
CanGoPrevious() → bool

// Events
OnPartChanged += (index, part) => {}
OnExplorerStarted += () => {}
OnExplorerEnded += () => {}
```

## Customization

### Change Transparency

In PartExplorerController Inspector:
```
Ghost Alpha: 0.2  (0 = invisible, 1 = opaque)
```

### Change Fade Speed

In PartExplorerController Inspector:
```
Fade Duration: 0.5  (seconds)
```

### Add/Remove Parts

In PartExplorerData Inspector:
- Click "+" to add
- Click "-" to remove
- Drag EnginePart components

## Example: V8 Hot Rod

```
Parts:
1. Cylinder Block
2. Cylinder Head
3. Pistons
4. Crankshaft
5. Connecting Rods
6. Valves
7. Spark Plugs
8. Oil Pan
9. Intake Manifold
10. Exhaust Manifold
```

Each part highlights one at a time with description.

## Files

| File | Purpose |
|------|---------|
| PartExplorerData.cs | Data structure |
| PartExplorerController.cs | Main controller |
| PartExplorerUIPanel.cs | UI management |
| PartExplorerLauncher.cs | Button launcher |
| SETUP_GUIDE.md | Detailed setup |
| README.md | This file |

## Performance

- Memory: ~1 MB
- CPU: Minimal (updates on part change only)
- GPU: Depends on part complexity
- VR-Ready: Yes

## Integration

Works alongside existing systems:
- ✓ EngineGrabManager
- ✓ EngineViewManager
- ✓ TabletUIController
- ✓ EngineInteractor

No modifications to existing code required.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Parts don't highlight | Check EnginePart components assigned |
| UI doesn't update | Verify TextMeshProUGUI components assigned |
| Parts don't fade | Check Ghost Alpha > 0 |
| Navigation doesn't work | Verify buttons wired correctly |

## Next Steps

1. Read SETUP_GUIDE.md
2. Create PartExplorerData
3. Add all V8 parts
4. Set up scene components
5. Create UI elements
6. Wire buttons
7. Test!

---

**Ready to implement?** Start with SETUP_GUIDE.md!
