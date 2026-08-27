# Simple Part Explorer - Quick Start

## 3-Minute Setup

### 1. Add Component
```
GameObject: SimplePartExplorer
Component: SimplePartExplorer
```

### 2. Assign UI
```
Tablet Part Name: [TextMeshProUGUI]
Tablet Part Description: [TextMeshProUGUI]
Previous Button: [Button]
Next Button: [Button]
Step Counter: [TextMeshProUGUI]
Monitor Part Name: [TextMeshProUGUI]
Monitor Part Description: [TextMeshProUGUI]
Audio Source: [AudioSource]
```

### 3. Wire Button
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
Auto-detects active engine
    ↓
Loads its manifest
    ↓
Shows first part
├─ Highlights
├─ Fades others
├─ Shows info on tablet
├─ Shows info on monitor
└─ Plays audio
    ↓
Click Next/Previous
    ↓
Shows next part
```

## Multi-Engine

Works with:
- ✓ V8 Hot Rod
- ✓ Ramjet Engine
- ✓ Any engine
- ✓ 100 engines tomorrow

No changes needed!

## Inspector

```
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

Settings:
  Fade Duration: 0.5
  Ghost Alpha: 0.2
```

## That's All!

Auto-detects engine. Auto-finds manifest. Works with any engine.

**3 minutes. Done.**

---

**See MULTI_ENGINE_SETUP.md for detailed guide.**
