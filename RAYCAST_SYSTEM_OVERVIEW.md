# Raycast System Overview

## Architecture

The project uses **XRRayInteractor** from the XR Interaction Toolkit to cast rays from the VR controller. There are **two independent raycast systems** that work together:

```
┌─────────────────────────────────────────────────────────────┐
│                    XRRayInteractor                          │
│              (Right Controller Ray Pointer)                 │
└────────────────────┬────────────────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
        ▼                         ▼
┌──────────────────┐    ┌──────────────────┐
│ EngineInteractor │    │ EngineGrabManager│
│   (Hover/Select) │    │   (Grab/Move)    │
└──────────────────┘    └──────────────────┘
```

---

## System 1: EngineInteractor (Hover & Selection)

**File**: `Assets/Scripts/EngineInteractor.cs`

### Purpose
- Detects when the ray hovers over engine parts
- Shows hover panels and outlines
- Handles part selection (isolation mode)
- Plays audio explanations
- Manages the info panel on the tablet

### How It Works

#### 1. **Raycast Detection** (Every Frame)
```csharp
if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
    (enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
{
    var part = hit.collider.GetComponentInParent<EnginePart>();
    // Part found!
}
```

#### 2. **Hover Debounce** (80ms)
- Prevents flickering when ray bounces between parts
- Waits 80ms before confirming a new hover target
- Smooth transitions between parts

#### 3. **Hover States**
```
Ray on Part A
    ↓
Pending (80ms debounce)
    ↓
Stable Hover
    ├─ SetHighlight(true) → Red outline appears
    ├─ ShowPanel() → Hover panel appears
    └─ OnPartHovered event fired
```

#### 4. **Selection (Trigger Press)**
When trigger is pressed on a part:
- **Normal Mode**: Isolate the part
  - Selected part: Full brightness + glow
  - Other parts: Ghost (semi-transparent)
  - Panel shows selected part info
  - Audio plays
  
- **Exploded View**: Audio + info only
  - No ghosting
  - No position changes
  - Just plays audio and shows info
  
- **X-Ray View**: Selection blocked
  - Parts are transparent wireframes
  - Selection makes no sense

#### 5. **Layer Filtering**
```csharp
enginePartsLayer = LayerMask.NameToLayer("EngineParts");
// Only raycasts hit objects on this layer
```

### Key Features
- ✅ Hover debounce prevents flickering
- ✅ Respects view modes (X-Ray, Exploded, Normal)
- ✅ Blocks interaction during movement (joystick input)
- ✅ Fires events for tablet UI to subscribe to
- ✅ Disables when `InteractionEnabled = false`

---

## System 2: EngineGrabManager (Grab & Movement)

**File**: `Assets/Scripts/EngineGrabManager.cs`

### Purpose
- Detects when the ray hits a part during grab mode
- Moves grabbed parts with the ray
- Allows Z-axis movement via thumbstick
- Suppresses locomotion while grabbing

### How It Works

#### 1. **Raycast Detection** (Every Frame)
```csharp
if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
{
    var grab = hit.collider.GetComponentInParent<EnginePartGrabController>();
    // Grab controller found!
}
```

#### 2. **Grab Trigger** (Trigger Press)
```
Trigger Down
    ↓
Check: IsGrabModeActive? (EngineViewManager.IsGrabModeActive)
    ↓
Check: EngineInteractor not busy? (no isolation mode)
    ↓
Raycast hit?
    ↓
Record:
  - _grabOffset = hit.point - part.position
  - _grabDepth = distance along ray to hit point
  - _grabZ = part's current Z position
```

#### 3. **Movement Loop** (While Trigger Held)
```
Every Frame:
  1. Read ray position & direction
  2. Calculate hit point on ray at saved depth
  3. Apply saved offset to get target position
  4. Read thumbstick for Z movement
  5. Lerp part position to target (X/Y only, Z from thumbstick)
  6. Part follows ray smoothly
```

#### 4. **Position Calculation**
```csharp
// Ray hit point (where the ray intersects the part)
Vector3 hitPointTarget = rayOrigin + rayDirection * _grabDepth;

// Part center (offset from hit point)
Vector3 target = hitPointTarget - _grabOffset;

// Z is controlled by thumbstick, not the ray
target.z = _grabZ;

// Smooth follow
part.position = Vector3.Lerp(current, target, followSpeed);
```

#### 5. **Z-Axis Control** (Thumbstick)
```
Thumbstick Forward → Move part away (positive Z)
Thumbstick Back    → Move part closer (negative Z)
Deadzone: 0.08 (prevents drift)
Speed: 0.8 m/s at full deflection
```

#### 6. **Locomotion Suppression**
While grabbing:
- Disables `ActionBasedContinuousMoveProvider` (walk)
- Disables `ActionBasedContinuousTurnProvider` (turn)
- Disables `ActionBasedSnapTurnProvider` (snap turn)
- Thumbstick only moves the part on Z

### Key Features
- ✅ Only works in grab mode (`IsGrabModeActive`)
- ✅ One part at a time
- ✅ Part never jumps (offset locked)
- ✅ Part never rotates (position only)
- ✅ X/Y from ray, Z from thumbstick
- ✅ Smooth following with lerp
- ✅ Locomotion disabled while grabbing

---

## Raycast Filtering

Both systems use the same layer filtering:

```csharp
// Only hit objects on the "EngineParts" layer
if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
{
    // Valid hit
}
```

### Layer Setup
- **EngineParts**: All engine part meshes
- **Other layers**: Ignored by both systems

---

## Ray Origin & Direction

Both systems get the ray from the same source:

```csharp
// Ray origin: controller position
Vector3 rayOrigin = rayInteractor.transform.position;

// Ray direction: controller forward
Vector3 rayDirection = rayInteractor.transform.forward;

// Ray equation: point = origin + direction * distance
```

---

## Interaction Modes & Raycast Behavior

### 1. **Default View** (Normal Mode)
- ✅ Hover shows outline + panel
- ✅ Trigger selects part (isolation)
- ✅ Grab mode disabled

### 2. **X-Ray View**
- ✅ Hover shows outline + panel
- ❌ Trigger blocked (selection disabled)
- ✅ Grab mode disabled

### 3. **Exploded View**
- ✅ Hover shows outline + panel
- ✅ Trigger plays audio + shows info (no isolation)
- ✅ Grab mode disabled

### 4. **Grab Mode**
- ✅ Hover shows outline only (no panel)
- ❌ Trigger blocked (selection disabled)
- ✅ Trigger grabs part + moves it
- ✅ Locomotion disabled

---

## Event Flow

### Hover Event
```
Ray enters part
    ↓
Pending (80ms debounce)
    ↓
OnPartHovered event fired
    ↓
Tablet UI updates display
```

### Selection Event
```
Trigger pressed on part
    ↓
OnPartSelected event fired
    ↓
Tablet UI updates info panel
```

### Grab Event
```
Trigger pressed in grab mode
    ↓
OnGrabStart() called
    ↓
Part follows ray
    ↓
Trigger released
    ↓
OnGrabEnd() called
```

---

## Collision Detection

### Colliders Used
- **MeshCollider** (convex) on each engine part
- Added by Engine Part Setup Tool
- Layer: "EngineParts"

### Raycast Targets
- Only MeshColliders on "EngineParts" layer
- Ignores UI, terrain, other objects

---

## Performance Considerations

### Raycast Frequency
- **EngineInteractor**: Every frame (Update)
- **EngineGrabManager**: Every frame (Update)
- Total: 2 raycasts per frame (minimal cost)

### Optimization
- Uses `XRRayInteractor.TryGetCurrent3DRaycastHit()` (cached result)
- Not doing manual `Physics.Raycast()` calls
- Layer filtering reduces collision checks

---

## Debugging

### Enable Hover Debug Logs
In `EngineInteractor.cs`, uncomment the debug block:
```csharp
Debug.Log($"[HoverDebug] Hit: '{hit.collider.gameObject.name}' " +
          $"layer={hit.collider.gameObject.layer} " +
          $"layerName={LayerMask.LayerToName(hit.collider.gameObject.layer)} | " +
          $"EnginePart found: {(part != null ? part.gameObject.name : "NULL")} | " +
          $"hoverPanel: {(part != null ? (part.hoverPanel != null ? part.hoverPanel.name : "NULL") : "n/a")}");
```

### Common Issues
1. **Ray not hitting parts**
   - Check layer assignment (must be "EngineParts")
   - Check MeshCollider exists and is convex
   - Check `enginePartsLayer` is assigned in Inspector

2. **Hover panel not showing**
   - Check `hoverPanel` is assigned in EnginePart Inspector
   - Check `PartHoverPanel` script is on the panel
   - Check panel is not already active

3. **Grab not working**
   - Check `IsGrabModeActive` is true
   - Check `EnginePartGrabController` is on the part
   - Check `rayInteractor` is assigned in EngineGrabManager

---

## Summary

| System | Purpose | Trigger | Output |
|--------|---------|---------|--------|
| **EngineInteractor** | Hover & Select | Ray + Trigger | Outline, Panel, Audio, Events |
| **EngineGrabManager** | Grab & Move | Ray + Trigger (in grab mode) | Part Movement, Locomotion Suppression |

Both systems:
- Use the same `XRRayInteractor`
- Filter by "EngineParts" layer
- Respect view modes
- Fire events for UI integration
- Work independently without conflicts
