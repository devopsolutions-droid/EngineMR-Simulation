# Simple Part Explorer - Quick Reference

## The Simplest Setup Ever

### Step 1: Add Component
```
GameObject: SimplePartExplorer
Component: SimplePartExplorer
```

### Step 2: Assign Manifest
```
Engine Part Manifest: V8HotRedManifest
```

### Step 3: Assign UI (Tablet)
```
Tablet Part Name: [TextMeshProUGUI]
Tablet Part Description: [TextMeshProUGUI]
Previous Button: [Button]
Next Button: [Button]
Step Counter: [TextMeshProUGUI]
```

### Step 4: Assign UI (Monitor)
```
Monitor Part Name: [TextMeshProUGUI]
Monitor Part Description: [TextMeshProUGUI]
Audio Source: [AudioSource]
```

### Step 5: Wire Button
```
"Show Working" Button
→ On Click () → Add
→ SimplePartExplorer.StartExplorer()
```

## Done! 🎉

## What Happens

```
Click "Show Working"
    ↓
Part 1 highlights
Other parts fade
Name shows on tablet
Description shows on tablet
Name shows on monitor
Description shows on monitor
Audio plays
    ↓
Click "Next"
    ↓
Part 2 highlights
(repeat)
    ↓
Click "Next" on last part
    ↓
Explorer ends
All parts restore
```

## Inspector Fields

```
Data Source:
  Engine Part Manifest: V8HotRedManifest

UI - Tablet:
  Tablet Part Name: [assign]
  Tablet Part Description: [assign]
  Previous Button: [assign]
  Next Button: [assign]
  Step Counter: [assign]

UI - Wall Monitor:
  Monitor Part Name: [assign]
  Monitor Part Description: [assign]
  Audio Source: [assign]

Engine Parts:
  Engine Root: [leave empty]

Settings:
  Fade Duration: 0.5
  Ghost Alpha: 0.2
```

## That's All!

No complex setup. No manual data entry. No extra tools.

**5 minutes. Done.**

---

**See SIMPLE_SETUP.md for detailed instructions.**
