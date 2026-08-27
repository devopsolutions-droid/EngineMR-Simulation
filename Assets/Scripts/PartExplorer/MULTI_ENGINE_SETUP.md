# Simple Part Explorer - Multi-Engine Setup

## The Best Part

**You don't need to assign the manifest!**

SimplePartExplorer automatically detects which engine is loaded and uses its manifest.

This means:
- ✓ Works with V8 Hot Rod
- ✓ Works with Ramjet Engine
- ✓ Works with any engine you add
- ✓ Works with 100 engines tomorrow
- ✓ No changes needed!

## How It Works

1. When you click "Show Working"
2. SimplePartExplorer finds the active engine in the scene
3. Looks up that engine in EngineSceneLoader
4. Gets the engine's manifest automatically
5. Loads all parts from that manifest
6. Shows them one at a time

## Setup (3 Minutes!)

### Step 1: Add Component
```
Create empty GameObject "SimplePartExplorer"
Add SimplePartExplorer component
```

### Step 2: Assign Tablet UI
```
Tablet Part Name: [Your TextMeshProUGUI]
Tablet Part Description: [Your TextMeshProUGUI]
Previous Button: [Your Button]
Next Button: [Your Button]
Step Counter: [Your TextMeshProUGUI]
```

### Step 3: Assign Monitor UI
```
Monitor Part Name: [Your TextMeshProUGUI]
Monitor Part Description: [Your TextMeshProUGUI]
Audio Source: [Your AudioSource]
```

### Step 4: Wire Button
```
"Show Working" Button
→ On Click () → Add
→ SimplePartExplorer.StartExplorer()
```

## That's It!

No manifest assignment needed. It auto-detects!

## How Auto-Detection Works

```
SimplePartExplorer.StartExplorer()
    ↓
Find engine root in scene
    ↓
Find EngineSceneLoader
    ↓
Look up which engine is active
    ↓
Get that engine's manifest
    ↓
Load all parts from manifest
    ↓
Show first part
```

## Multi-Engine Support

### V8 Hot Rod
```
EngineSceneLoader has entry:
├─ Engine Data: V8HotRedData
├─ Scene Root: V8HotRed (in scene)
└─ Manifest: V8HotRedManifest
    ↓
SimplePartExplorer finds this and uses it!
```

### Ramjet Engine
```
EngineSceneLoader has entry:
├─ Engine Data: RamjetData
├─ Scene Root: Ramjet (in scene)
└─ Manifest: RamjetManifest
    ↓
SimplePartExplorer finds this and uses it!
```

### Any New Engine
```
EngineSceneLoader has entry:
├─ Engine Data: NewEngineData
├─ Scene Root: NewEngine (in scene)
└─ Manifest: NewEngineManifest
    ↓
SimplePartExplorer finds this and uses it!
```

## Scaling to 100 Engines

When you add a new engine:

1. Create EngineData asset
2. Create EnginePartManifest
3. Add entry to EngineSceneLoader
4. Done!

SimplePartExplorer automatically works with it!

No code changes. No new components. Nothing!

## Inspector Setup

```
SimplePartExplorer (Component)
├─ UI - Tablet
│  ├─ Tablet Part Name: [assign]
│  ├─ Tablet Part Description: [assign]
│  ├─ Previous Button: [assign]
│  ├─ Next Button: [assign]
│  └─ Step Counter: [assign]
├─ UI - Wall Monitor
│  ├─ Monitor Part Name: [assign]
│  ├─ Monitor Part Description: [assign]
│  └─ Audio Source: [assign]
└─ Settings
   ├─ Fade Duration: 0.5
   └─ Ghost Alpha: 0.2
```

Notice: **No Data Source field!** It auto-detects!

## What Gets Auto-Detected

1. **Engine Root** - Finds the active engine in scene
2. **EngineSceneLoader** - Finds the scene loader
3. **Engine Data** - Looks up which engine is active
4. **Manifest** - Gets the engine's manifest
5. **Parts** - Loads all parts from manifest

All automatic!

## Testing with Multiple Engines

### Test 1: V8 Hot Rod
1. Load V8 Hot Rod engine
2. Click "Show Working"
3. See V8 parts with V8 descriptions

### Test 2: Ramjet Engine
1. Load Ramjet engine
2. Click "Show Working"
3. See Ramjet parts with Ramjet descriptions

### Test 3: New Engine
1. Load new engine
2. Click "Show Working"
3. See new engine parts with descriptions

All work without any changes!

## Future-Proof

When you add engine #100:

1. Create its EngineData
2. Create its EnginePartManifest
3. Add to EngineSceneLoader
4. It just works!

No modifications to SimplePartExplorer needed.

## Benefits

✓ **One component for all engines**
✓ **Auto-detects active engine**
✓ **Uses correct manifest automatically**
✓ **Scales to any number of engines**
✓ **No manual setup per engine**
✓ **Future-proof**

## Summary

**3-minute setup. Works with any engine. Scales to 100 engines.**

Just add the component, assign UI, wire the button, and it works for all engines!

---

**That's it! SimplePartExplorer handles everything!**
