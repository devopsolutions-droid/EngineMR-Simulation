# Simple Part Explorer - Visual Setup Guide

## The Flow

```
User clicks "Show Working"
    ↓
SimplePartExplorer.StartExplorer()
    ↓
Load parts from V8HotRedManifest
    ↓
Show Part 1
├─ Highlight Part 1 (glow)
├─ Fade all other parts (invisible)
├─ Display name on tablet
├─ Display description on tablet
├─ Display name on monitor
├─ Display description on monitor
└─ Play audio explanation
    ↓
User clicks "Next"
    ↓
Show Part 2 (same as above)
    ↓
... repeat for all parts ...
    ↓
User clicks "Next" on last part
    ↓
StopExplorer()
├─ Restore all parts
├─ Hide UI
└─ Stop audio
```

## Inspector Setup

```
SimplePartExplorer (Component)
├─ Data Source
│  └─ Engine Part Manifest: V8HotRedManifest
├─ UI - Tablet
│  ├─ Tablet Part Name: [TextMeshProUGUI]
│  ├─ Tablet Part Description: [TextMeshProUGUI]
│  ├─ Previous Button: [Button]
│  ├─ Next Button: [Button]
│  └─ Step Counter: [TextMeshProUGUI]
├─ UI - Wall Monitor
│  ├─ Monitor Part Name: [TextMeshProUGUI]
│  ├─ Monitor Part Description: [TextMeshProUGUI]
│  └─ Audio Source: [AudioSource]
├─ Engine Parts
│  └─ Engine Root: [Leave empty - auto-finds]
└─ Settings
   ├─ Fade Duration: 0.5
   └─ Ghost Alpha: 0.2
```

## Button Wiring

```
"Show Working" Button
    ↓
On Click ()
    ↓
+ Add
    ↓
Drag SimplePartExplorer GameObject
    ↓
Select SimplePartExplorer
    ↓
Select StartExplorer()
```

## What Happens at Each Step

### Step 1: Part 1 Visible
```
Engine View:
├─ Part 1: HIGHLIGHTED (glowing)
├─ Part 2: FADED (20% visible)
├─ Part 3: FADED (20% visible)
└─ ... all others FADED

Tablet:
├─ Part Name: "Cylinder Head"
├─ Description: "The Cylinder Head is..."
└─ Counter: "Part 1 / 15"

Monitor:
├─ Part Name: "Cylinder Head"
├─ Description: "The Cylinder Head is..."
└─ Audio: Playing explanation

Buttons:
├─ Previous: DISABLED (grayed out)
└─ Next: ENABLED (bright)
```

### Step 2: User Clicks Next
```
Engine View:
├─ Part 1: RESTORED (normal)
├─ Part 2: HIGHLIGHTED (glowing)
├─ Part 3: FADED (20% visible)
└─ ... all others FADED

Tablet:
├─ Part Name: "Crankshaft"
├─ Description: "The Crankshaft converts..."
└─ Counter: "Part 2 / 15"

Monitor:
├─ Part Name: "Crankshaft"
├─ Description: "The Crankshaft converts..."
└─ Audio: Playing new explanation

Buttons:
├─ Previous: ENABLED (bright)
└─ Next: ENABLED (bright)
```

### Last Step: User Clicks Next
```
Explorer ends
    ↓
All parts RESTORED (normal)
All UI HIDDEN
Audio STOPPED
```

## Data Flow

```
V8HotRedManifest
├─ Part 1: Cube_Chrome.001_0
│  └─ PartData
│     ├─ partName: "Cylinder Head"
│     ├─ description: "The Cylinder Head is..."
│     └─ audioExplanation: [AudioClip]
├─ Part 2: Cube_Material.005_0
│  └─ PartData
│     ├─ partName: "Crankshaft"
│     ├─ description: "The Crankshaft converts..."
│     └─ audioExplanation: [AudioClip]
└─ ... 13 more parts

SimplePartExplorer reads this and:
1. Extracts all part data
2. Finds each part in scene
3. Shows one at a time
4. Displays info
5. Plays audio
```

## Scene Hierarchy

```
Scene
├─ Main Scene (or your scene)
├─ V8HotRed (engine root - auto-found)
│  ├─ Cube_Chrome.001_0 (Part 1)
│  ├─ Cube_Material.005_0 (Part 2)
│  ├─ Cylinder.002_Material.001_0 (Part 3)
│  └─ ... 12 more parts
├─ Tablet (UI)
│  ├─ PartNameText
│  ├─ PartDescriptionText
│  ├─ PreviousButton
│  ├─ NextButton
│  └─ StepCounterText
├─ WallMonitor (UI)
│  ├─ PartNameText
│  └─ PartDescriptionText
├─ AudioSource (for playing explanations)
└─ SimplePartExplorer (component on any GameObject)
```

## What Gets Highlighted

When showing Part 1:
```
Part 1: SetHighlight(true)
├─ Glow effect
├─ Bright color
└─ Stands out

All Other Parts: SetGhost()
├─ Semi-transparent (20% visible)
├─ Faded appearance
└─ Barely visible
```

## UI Updates

When showing Part 1:
```
Tablet:
├─ PartNameText.text = "Cylinder Head"
├─ PartDescriptionText.text = "The Cylinder Head is..."
└─ StepCounterText.text = "Part 1 / 15"

Monitor:
├─ PartNameText.text = "Cylinder Head"
└─ PartDescriptionText.text = "The Cylinder Head is..."

Audio:
└─ audioSource.Play() [plays audio explanation]
```

## Button States

```
At Part 1:
├─ Previous Button: DISABLED (can't go back)
└─ Next Button: ENABLED (can go forward)

At Part 8 (middle):
├─ Previous Button: ENABLED (can go back)
└─ Next Button: ENABLED (can go forward)

At Part 15 (last):
├─ Previous Button: ENABLED (can go back)
└─ Next Button: ENABLED (clicking ends explorer)
```

## That's It!

Simple, straightforward, and uses your existing data!

**5 minutes to set up, infinite possibilities!**
