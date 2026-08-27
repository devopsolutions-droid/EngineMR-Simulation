# Part Explorer - Setup Guide

## Overview

The Part Explorer system lets users explore engine parts one at a time. When viewing a part:
- That part is **highlighted** (glowing)
- All other parts **fade to ghost** (semi-transparent)
- Part name and description display on the tablet
- Users navigate with Previous/Next buttons

## Quick Setup (10 Minutes)

### Step 1: Create PartExplorerData

1. In your Assets folder, right-click
2. Create → Engine VR → Part Explorer → Part Explorer Data
3. Name it "V8HotRodExplorer"

### Step 2: Add Parts to PartExplorerData

In the Inspector, add each V8 engine part:

**Example parts for V8 Hot Rod:**
- Cylinder Block
- Cylinder Head
- Pistons
- Crankshaft
- Connecting Rods
- Valves
- Spark Plugs
- Oil Pan
- Intake Manifold
- Exhaust Manifold

For each part:
1. Click "+" to add a new part
2. Enter Part Name (e.g., "Cylinder Block")
3. Enter Part Description (what it does)
4. Drag the EnginePart component from the scene into the "Engine Part" field

### Step 3: Add Components to Scene

1. Create empty GameObject called "PartExplorerSystem"
2. Add **PartExplorerController** component
   - Assign "V8HotRodExplorer" to Explorer Data field
   - Leave Fade Duration at 0.5
   - Leave Ghost Alpha at 0.2

### Step 4: Create UI Elements on Tablet

On your tablet UI, add:

1. **Part Name Text** (TextMeshProUGUI)
   - Name: "PartNameText"
   - Font size: 28
   - Color: White
   - Position: Top of tablet

2. **Part Description Text** (TextMeshProUGUI)
   - Name: "PartDescriptionText"
   - Font size: 18
   - Color: Light gray
   - Enable word wrapping
   - Position: Below part name

3. **Part Counter Text** (TextMeshProUGUI)
   - Name: "PartCounterText"
   - Font size: 16
   - Color: Gray
   - Position: Top-right corner

4. **Previous Button**
   - Name: "PreviousButton"
   - Text: "← Previous"
   - Position: Bottom-left of tablet

5. **Next Button**
   - Name: "NextButton"
   - Text: "Next →"
   - Position: Bottom-right of tablet

### Step 5: Wire UI Panel

1. Create empty GameObject called "PartExplorerUIManager"
2. Add **PartExplorerUIPanel** component
3. Assign in Inspector:
   - Explorer Controller: [Drag PartExplorerController]
   - Part Name Text: [Drag PartNameText]
   - Part Description Text: [Drag PartDescriptionText]
   - Part Counter Text: [Drag PartCounterText]
   - Previous Button: [Drag PreviousButton]
   - Next Button: [Drag NextButton]
   - Check "Auto Hide Panels When Inactive"

### Step 6: Add Launch Button

Find your "Show Working" button on the tablet:

1. Add **PartExplorerLauncher** component to it
2. Assign PartExplorerController reference
3. Wire button's OnClick:
   - Click "+"
   - Drag the GameObject with PartExplorerLauncher
   - Select PartExplorerLauncher → LaunchExplorer()

### Step 7: Test!

1. Play the scene
2. Click "Show Working" button
3. You should see:
   - First part highlighted (glowing)
   - Other parts faded (semi-transparent)
   - Part name and description on tablet
   - Previous button disabled (at first part)
   - Next button enabled

4. Click "Next" to see next part
   - Previous part fades back to normal
   - New part highlights
   - UI updates

5. Click "Previous" to go back

## How It Works

### Part Highlighting
- **Current Part**: `SetHighlight(true)` - Shows glow effect
- **Other Parts**: `SetGhost()` - Fades to semi-transparent

### Navigation
- **Next Button**: Moves to next part
- **Previous Button**: Moves to previous part
- Buttons auto-disable at boundaries

### UI Updates
- Part name displays
- Part description displays
- Counter shows "Part X / Y"
- All update automatically

## Customization

### Change Ghost Alpha (Transparency)

In PartExplorerController Inspector:
```
Ghost Alpha: 0.2  (0 = invisible, 1 = fully opaque)
```

Lower values = more transparent
Higher values = more visible

### Change Fade Duration

In PartExplorerController Inspector:
```
Fade Duration: 0.5  (seconds)
```

Lower values = faster transitions
Higher values = slower transitions

### Add More Parts

In PartExplorerData Inspector:
1. Click "+" to add new part
2. Fill in name and description
3. Drag EnginePart component

### Remove Parts

In PartExplorerData Inspector:
1. Select the part
2. Click "-" to remove

## Troubleshooting

### Parts don't highlight
- Check EnginePart components are assigned in PartExplorerData
- Verify parts are in the scene
- Check console for errors

### UI doesn't update
- Verify all TextMeshProUGUI components are assigned
- Check buttons are wired correctly
- Verify PartExplorerUIPanel is assigned to PartExplorerController

### Parts don't fade
- Check Ghost Alpha is > 0
- Verify EnginePart.SetGhost() is working
- Check materials support transparency

### Navigation doesn't work
- Verify buttons are wired to PartExplorerUIPanel
- Check PartExplorerController is in scene
- Verify PartExplorerData has parts

## API Reference

### PartExplorerController

```csharp
// Control
StartExplorer()
EndExplorer()
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

## Example: V8 Hot Rod Parts

Here's a complete example of parts for a V8 engine:

1. **Cylinder Block**
   - Description: "The main engine block that houses the cylinders and pistons"

2. **Cylinder Head**
   - Description: "Sits on top of the block and contains the valves and spark plugs"

3. **Pistons**
   - Description: "Move up and down inside cylinders, converting fuel combustion into motion"

4. **Crankshaft**
   - Description: "Converts the linear motion of pistons into rotational motion"

5. **Connecting Rods**
   - Description: "Connect pistons to the crankshaft, transferring motion"

6. **Valves**
   - Description: "Control the flow of air and fuel into and exhaust out of cylinders"

7. **Spark Plugs**
   - Description: "Ignite the fuel-air mixture to start combustion"

8. **Oil Pan**
   - Description: "Stores engine oil for lubrication"

9. **Intake Manifold**
   - Description: "Distributes air-fuel mixture to each cylinder"

10. **Exhaust Manifold**
    - Description: "Collects exhaust gases from cylinders and directs them out"

## Performance

- **Memory**: ~1 MB for explorer system
- **CPU**: Minimal (only updates on part change)
- **GPU**: Depends on part complexity
- **VR-Ready**: Yes

## Integration

The Part Explorer works alongside existing systems:
- ✓ Doesn't modify existing engine code
- ✓ Works with EngineGrabManager
- ✓ Works with EngineViewManager
- ✓ Works with TabletUIController
- ✓ Can be toggled on/off independently

## Next Steps

1. Create PartExplorerData
2. Add all V8 parts to it
3. Add components to scene
4. Create UI elements
5. Wire everything together
6. Test and customize

That's it! You now have a working part explorer system.
