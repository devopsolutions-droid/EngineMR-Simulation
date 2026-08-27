# Snap-to-Assembly System Architecture

## Overview

When the user enters **Grab Mode**, engine parts separate into exploded positions. The user can then grab individual parts and bring them close to their original assembled position. When a part is within a **snap threshold** distance of its original position, it **magnetically snaps into place** with a satisfying visual/audio feedback. This allows the user to manually assemble the entire engine piece by piece.

The system provides three layers of feedback:
1. **SnapGhost** — A semi-transparent mesh outline at the target position (visible as soon as the part is grabbed)
2. **SnapZoneIndicator** — A thin LineRenderer ring that pulses brighter as the part approaches
3. **Magnetic Pull** — A per-frame position offset that gently guides the part toward the target

---

## Core Components

### 1. [`SnapGhost`](Assets/Scripts/SnapGhost.cs) — *NEW COMPONENT*

Attached to each engine part (as a child GameObject). Spawns semi-transparent mesh copies of the part at the snap target position, giving the user a clear visual hint: _"place the part HERE"_.

| Property | Type | Default | Description |
|---|---|---|---|
| `ghostMaterial` | Material | Sprites/Default (fallback) | Material used for ghost rendering |
| `ghostColor` | Color | (0.15, 0.9, 0.3, 0.28) | Base color of the ghost (green tint, 28% alpha) |
| `pulseSpeed` | float | 1.5 | Speed of the idle alpha oscillation |
| `pulseAmount` | float | 0.2 | How much alpha oscillates during pulse (0=static) |
| `flashDuration` | float | 0.6 | Duration of the snap flash-and-fade |

**Behaviour:**
- **IdlePulse**: Gentle alpha sine-wave oscillation when the part is grabbed but far from target
- **SnapFlash**: Bright white flash that fades to ghostColor and then to full transparency (triggered on snap)

**Lifecycle:**
- `CreateGhost(Transform source, Vector3 position, Quaternion rotation)` — Copies all MeshFilter+MeshRenderer hierarchy from the source part into a new ghost root
- `Show()` — Activates ghost with idle pulsing
- `UpdateTarget(Vector3 position, Quaternion rotation)` — Syncs ghost position each frame
- `ClearGhost()` — Destroys all ghost meshes
- `FlashSnap()` — Triggers snap flash animation

**Integration:**
- Discovered automatically by [`EnginePartSnapController`](Assets/Scripts/EnginePartSnapController.cs:68) via `GetComponentInChildren<SnapGhost>()`
- Forwarded through snap controller: `CreateSnapGhost()`, `ShowSnapGhost()`, `UpdateSnapGhost()`, `ClearSnapGhost()`, `FlashSnapGhost()`

---

### 2. [`SnapZoneIndicator`](Assets/Scripts/SnapZoneIndicator.cs) — *NEW COMPONENT*

Attached to each engine part. A LineRenderer-based ring that visually guides the user to the snap target.

| Property | Type | Default | Description |
|---|---|---|---|
| `circleSegments` | int | 24 | Resolution of the ring |
| `circleRadius` | float | 0.08 | Radius of the ring in world-space |
| `idleColor` | Color | (0, 1, 0.3, 0.15) | Color when hidden/faint |
| `approachColor` | Color | (0, 1, 0.3, 0.6) | Color when part is near target |
| `snappedColor` | Color | (0, 1, 0.3, 1) | Color on snap flash |

**States:**
- **Hidden**: Ring invisible
- **Idle** (Show): Subtle pulse
- **Approach** (UpdateApproach): Bright proportional glow based on distance to target (closer = brighter)
- **Snapped** (OnSnapped): Flash bright and fade out over 0.5s

---

### 3. [`EnginePartSnapController`](Assets/Scripts/EnginePartSnapController.cs) — *NEW COMPONENT*

The core snap logic. Placed on each engine part alongside `EnginePart`, `EnginePartVisuals`, `EnginePartExplode`.

| Property | Type | Default | Description |
|---|---|---|---|
| `magnetRange` | float | 0.4 | World-space distance at which magnetic pull STARTS |
| `magnetStrength` | float | **0.6** | Strength of magnetic pull (0=none, 1=snap instantly). Increased from 0.18 to overcome grab lerp inertia (followSpeed=0.35) |
| `snapDistance` | float | **0.3** | World-space distance at which the part snaps. Increased from 0.2 for more forgiving trigger |
| `snapDuration` | float | 0.2 | Duration of snap bounce animation |
| `snapBounceHeight` | float | 0.02 | Overshoot Y height for bounce feel |
| `audioSource` | AudioSource | — | Audio source for snap sound |
| `snapSound` | AudioClip | — | Snap sound effect |
| `snapParticles` | ParticleSystem | — | Optional particle burst on snap |
| `snapGhost` | SnapGhost | — | Auto-discovered child SnapGhost component |

**Magnetic Pull Gradient:**
```
GetMagneticPull(currentPosition):
  if IsSnapped → zero
  if already snapping → zero
  if distance > magnetRange (0.4m) → zero
  if distance ≤ snapDistance (0.3m) → zero (TrySnap handles this)
  
  t = 1 - clamp((dist - snapDistance) / (magnetRange - snapDistance))
  pullStrength = t * magnetStrength
  return direction_to_target.normalized * pullStrength
```

The pull is strongest at the `snapDistance` edge and tapers to zero at `magnetRange`.

**Snap Animation (two-phase bounce):**
1. **Phase 1** (60% of duration): Lerp from current position to `SnapTargetWorld + (0, snapBounceHeight, 0)` — overshoot
2. **Phase 2** (40% of duration): Lerp from overshoot peak down to exact `SnapTargetWorld` — settle

**Integration with `EnginePartExplode`:**
- On Awake, reads `_assembledLocalPos` from `EnginePartExplode` (exposed as `AssembledLocalPos`)
- Computes `SnapTargetWorld = transform.parent.TransformPoint(_assembledLocalPos)` at `Start()`
- `RefreshSnapTarget()` allows recomputation if the parent moves (e.g., engine re-centering)

---

### 4. Modifications to [`EngineGrabManager`](Assets/Scripts/EngineGrabManager.cs)

**On `OnTriggerDown()`** (lines 184-198):
```csharp
_grabbedSnap = grab.GetComponent<EnginePartSnapController>();
if (_grabbedSnap != null)
{
    _grabbedSnap.ResetSnap();
    _grabbedIndicator = grab.GetComponentInChildren<SnapZoneIndicator>(true);
    if (_grabbedIndicator != null)
        _grabbedIndicator.Show(_grabbedSnap.SnapTargetWorld);

    _grabbedSnap.CreateSnapGhost();
    _grabbedSnap.ShowSnapGhost();
}
```

**In `MoveGrabbedPart()`** (lines 235-271):
```csharp
// Every frame: apply magnetic pull + update ghost
if (_grabbedSnap != null && !_grabbedSnap.IsSnapped)
{
    Vector3 pull = _grabbedSnap.GetMagneticPull(next);
    if (pull.sqrMagnitude > 0.0001f)
        next += pull * Time.deltaTime * 60f;

    _grabbedSnap.UpdateSnapGhost();
}

_grabbed.transform.position = next;

// Check proximity and auto-release on snap
if (_grabbedSnap != null && !_grabbedSnap.IsSnapped)
{
    if (_grabbedIndicator != null)
        _grabbedIndicator.UpdateApproach(next);

    if (_grabbedSnap.TrySnap())
    {
        _grabbedIndicator?.OnSnapped();
        _grabbed.OnGrabEnd();
        _triggerHeld = false;
        // Clear references...
    }
}
```

**On `ReleaseGrab()`** (lines 410-451):
- Final `TrySnap()` check on manual release
- If snap triggered: ghost flash handled by `BeginSnap()`
- If not snapping: clear ghost via `ClearSnapGhost()`
- All three code paths clean up the ghost

---

### 5. Modification to [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs)

**`ResetAllSnapStates()`** (line 457):
- Calls `snapCtrl.ResetSnap()` on all parts
- Calls `snapCtrl.ClearSnapGhost()` — cleans up any lingering ghost meshes
- Hides all SnapZoneIndicators

Called from:
- `ActivateDefaultView()` (line 138)
- `ActivateXRayView()` (line 187) — when exiting Grab Mode
- `ActivateExplodedView()` (line 243) — when exiting Grab Mode
- `DeactivateGrabMode()` (line 346)
- `ActivateShowWorkingView()` (line 392) — when exiting Grab Mode
- `RefreshAfterLoad()` (line 495)

---

## Data Flow Diagram

```
User grabs part via trigger
       │
       ▼
EngineGrabManager.OnTriggerDown()
       │
       ├──► ResetSnap() — unlock part for fresh grab
       ├──► SnapZoneIndicator.Show(target) — show thin ring
       ├──► CreateSnapGhost() + ShowSnapGhost() — show mesh outline
       │
       ▼
EngineGrabManager.MoveGrabbedPart()  [every frame]
       │
       ├──► Compute X/Y/Z from ray + thumbstick
       │
       ├──► GetMagneticPull(next)
       │      ├──► dist > 0.4m → no pull
       │      ├──► dist ≤ 0.3m → no pull (TrySnap handles)
       │      └──► 0.3m < dist < 0.4m → gradient pull (0 → 0.6)
       │
       ├──► next += pull * Time.deltaTime * 60f
       ├──► UpdateSnapGhost() — sync ghost to target
       │
       ├──► transform.position = next
       │
       └──► TrySnap()
              ├──► dist > 0.3m → nothing
              └──► dist ≤ 0.3m → BeginSnap()
                      ├──► Set IsSnapped = true
                      ├──► Play snap sound
                      ├──► Burst particles
                      ├──► FlashSnapGhost() — bright flash and fade
                      ├──► StartCoroutine(AnimateSnap())
                      │      ├──► Phase 1: overshoot to peak (60%)
                      │      └──► Phase 2: settle to target (40%)
                      └──► OnPartSnapped → EngineGrabManager auto-releases

User releases part
       │
       ▼
EngineGrabManager.ReleaseGrab()
       │
       ├──► Final TrySnap() check
       │      ├──► Success → ghost flash in BeginSnap()
       │      └──► Failed → ClearSnapGhost() + indicator.Hide()
       │
       └──► Clear all references

View transition (Default/X-Ray/Exploded/ShowWorking)
       │
       ▼
EngineViewManager.ResetAllSnapStates()
       │
       ├──► ResetSnap() on all parts
       ├──► ClearSnapGhost() on all parts
       └──► Hide all indicators
```

---

## Edge Cases Handled

| Scenario | Behaviour |
|---|---|
| **User snaps a part then exits Grab Mode** | Part stays in snapped position; `ResetAllSnapStates()` clears snap flag but part stays at assembled position |
| **User snaps a part then enters another view** | `ResetAllSnapStates()` clears ghost + indicator; `AnimateToAssembled()` runs — snapped part is already at target, no visual change |
| **User drops part far from target (manual release)** | No snap triggered; ghost and indicator are hidden; part stays where released |
| **User drops part very close to target** | Final `TrySnap()` in `ReleaseGrab()` catches it; snap animation plays + ghost flashes |
| **Magnetic pull fighting user intent** | Pull is only applied as a gentle offset (`magnetStrength * deltaTime * 60f`) — user can easily overpower it with thumbstick or ray movement |
| **Multiple parts grabbed sequentially** | Each part has its own independent snap controller; `ReleaseGrab()` clears all refs before next grab |
| **dismantledSceneRoot is assigned** | `SnapTargetWorld` still uses `transform.parent.TransformPoint(AssembledLocalPos)` — the original local position is always the snap target, not the dismantled position |
| **SnapGhost material missing** | Falls back to `Sprites/Default` shader with a runtime instance for tinting |

---

## Files

### New Components

1. **`Assets/Scripts/SnapGhost.cs`** — Visual ghost outline at snap target
2. **`Assets/Scripts/EnginePartSnapController.cs`** — Core snap logic with magnetic pull gradient
3. **`Assets/Scripts/SnapZoneIndicator.cs`** — Visual snap zone ring indicator

### Modified Files

1. **`Assets/Scripts/EngineGrabManager.cs`** — SnapGhost lifecycle (create/show/update/clear) + magnetic pull every frame
2. **`Assets/Scripts/EngineViewManager.cs`** — `ResetAllSnapStates()` clears ghosts
3. **`Assets/Scripts/EnginePartExplode.cs`** — Exposed `_assembledLocalPos` as `AssembledLocalPos` (public)

### Configuration Summary

| Parameter | Old Value | New Value | Why |
|---|---|---|---|
| `magnetStrength` | 0.18 | **0.6** | Was too weak against `followSpeed=0.35` lerp inertia |
| `snapDistance` | 0.2 | **0.3** | Too tight; user had to be nearly pixel-perfect |
| `ghostMaterial` | — | Sprites/Default | New SnapGhost component needs a transparent material |
| `ghostColor` | — | (0.15, 0.9, 0.3, 0.28) | Green tint, subtle 28% alpha for ghost outline |

---

## Visual Feedback Layers (in order of user perception)

| Layer | Component | When Visible | Purpose |
|---|---|---|---|
| **SnapGhost** | SnapGhost | Part grabbed → snapped | Semi-transparent mesh outline showing exact target position |
| **SnapZoneIndicator** | SnapZoneIndicator | Part grabbed → snapped | Thin ring at target that glows brighter as part approaches |
| **Magnetic Pull** | EnginePartSnapController | Within 0.4m of target | Per-frame position offset gently guiding the part toward target |
| **Snap Animation** | EnginePartSnapController | On snap trigger | Two-phase bounce: overshoot + settle |
| **Snap Flash** | SnapGhost | On snap trigger | Bright white→green flash that fades out |
| **Snap Sound** | EnginePartSnapController | On snap trigger | Audio feedback |
| **Snap Particles** | EnginePartSnapController | On snap trigger | Optional particle burst |