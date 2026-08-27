# Airflow_v6.1 Blue Animation System — Architecture Blueprint

## 1. Overview

Apply the **legacy airflow visual effects** (scrolling flow rings, rim glow, noise turbulence, progress fill) onto the **Airflow_v6.1 static mesh prefab** — using **only the blue color** (`R:0, G:0.4, B:1`), with no gradient. Run alongside the existing legacy procedural tube without modifying it.

### Key Design Decisions

**1. UV Flip Shader:** The Airflow_v6.1 mesh pieces have UV.y going **Exhaust(0) → Intake(1)**, opposite to the legacy tube. To fix the fill direction, [`BlueAirflowEffect.shader`](Assets/Shaders/BlueAirflowEffect.shader) (`Custom/AirflowEffectFlipped`) is an exact copy of the legacy shader with **one line changed** — `(1.0 - i.uv.y)` instead of `i.uv.y` in the progress cutoff.

**2. All-Blue Colours:** All 5 gradient colours (`_Colour1` through `_Colour5`) on the shader are set to the exact same blue value, causing the gradient to collapse into a uniform blue while every other visual feature works identically to the legacy tube.

**3. No Bridge, No State Machine:** The controller auto-discovers `JetEngineShowWorking` in the scene, polls `CurrentAirflowProgress`, and applies it via MaterialPropertyBlock. No separate `ShowFlow()`/`HideFlow()` methods.

### Key Constraints

- ✅ Apply directly to [`Airflow_v6.1`](Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab) prefab
- ✅ Run alongside legacy tube — do not replace or modify it
- ✅ NO changes to legacy system ([`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs), [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs), etc.)
- ✅ Single blue color only — NO gradient through cyan/white/orange/red
- ✅ Do NOT change mesh shape or geometry
- ✅ Progress fills from Intake (front) → Exhaust (back), matching legacy direction

---

## 2. Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                     Show Working Lifecycle                        │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │             JetEngineShowWorking                          │    │
│  │  ┌─ OnShowWorkingStart()     ──────────────────────────┐ │    │
│  │  │  └─ calls StartAirflow() on JetEngineAirflowController│ │    │
│  │  │                                                       │ │    │
│  │  ├─ CurrentAirflowProgress (float 0-1) ──────────────────┤ │    │
│  │  │    (read-only, ~highest progress)                     │ │    │
│  │  │                                                       │ │    │
│  │  └─ OnShowWorkingStop() ─────────────────────────────────┤ │    │
│  │     └─ calls StopAirflow() on legacy controller          │ │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
                            │
                            ▼ Progress (0-1) polled each frame
                            │
┌──────────────────────────────────────────────────────────────────┐
│                    AirflowV61Controller.cs                        │
│  (attached to Airflow_v6.1 root GameObject)                      │
│                                                                   │
│  DESIGN: Minimal MPB driver                                      │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Serialized Fields:                                      │    │
│  │  ├─ JetEngineShowWorking showWorkingRef                  │    │
│  │  │    (optional — auto-discovered via FindObjectOfType)   │    │
│  │  └─ float smoothingSpeed = 3f                            │    │
│  │                                                           │    │
│  │  Runtime:                                                 │    │
│  │  ├─ MeshRenderer[] meshRenderers (6 children, auto-found) │    │
│  │  ├─ MaterialPropertyBlock mpb                             │    │
│  │  ├─ float currentProgress, targetProgress                 │    │
│  │  └─ bool isFlowing (auto-detected from progress value)    │    │
│  │                                                           │    │
│  │  Update loop:                                             │    │
│  │  1. Poll showWorkingRef.CurrentAirflowProgress            │    │
│  │  2. If > 0.01 && !isFlowing → isFlowing = true           │    │
│  │  3. If < 0.001 && isFlowing → isFlowing = false, snap 0  │    │
│  │  4. If flowing: Lerp current→target, apply via MPB       │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
                            │
                            ▼ MaterialPropertyBlock _Progress per renderer
                            │
┌──────────────────────────────────────────────────────────────────┐
│           BlueAirflowEffect.mat                                   │
│  (uses UV-flipped variant: Custom/AirflowEffectFlipped)           │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  Shader: Custom/AirflowEffectFlipped                      │    │
│  │  (guid: e4f3d2c1b0a9f8e7d6c5b4a392817060)               │    │
│  │  Queue: Transparent (CustomRenderQueue: 3050)            │    │
│  │  Blend: One One (additive)                               │    │
│  │  EnableInstancingVariants: true  ← required for MPB      │    │
│  │                                                           │    │
│  │  Gradient: ALL 5 colours set to blue:                    │    │
│  │  ├─ _Colour1: (0, 0.4, 1, 1)                            │    │
│  │  ├─ _Colour2: (0, 0.4, 1, 1)                            │    │
│  │  ├─ _Colour3: (0, 0.4, 1, 1)                            │    │
│  │  ├─ _Colour4: (0, 0.4, 1, 1)                            │    │
│  │  └─ _Colour5: (0, 0.4, 1, 1)                            │    │
│  │                                                           │    │
│  │  Key positions (unchanged from legacy):                  │    │
│  │  ├─ _Key1: 0                                             │    │
│  │  ├─ _Key2: 0.2                                           │    │
│  │  ├─ _Key3: 0.42                                          │    │
│  │  ├─ _Key4: 0.7                                           │    │
│  │  └─ _Key5: 1.0                                           │    │
│  │                                                           │    │
│  │  Noise texture (from project):                           │    │
│  │  └─ WhiteNoise_T_mask.png (guid: f155b245e32ed...)       │    │
│  │                                                           │    │
│  │  Default params (same as legacy):                        │    │
│  │  ├─ _Progress: 0 (set via MPB at runtime)                │    │
│  │  ├─ _ProgressFade: 0.08                                  │    │
│  │  ├─ _ScrollSpeed: 2                                      │    │
│  │  ├─ _FlowTiling: 6                                       │    │
│  │  ├─ _NoiseSpeed: 1.2                                     │    │
│  │  ├─ _NoiseStrength: 0.3                                  │    │
│  │  ├─ _NoiseTiling: 4                                      │    │
│  │  ├─ _RimPower: 2.5                                       │    │
│  │  ├─ _RimIntensity: 1.5                                   │    │
│  │  ├─ _Opacity: 0.85                                       │    │
│  │  └─ _GlowIntensity: 2                                    │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. File-by-File Specification

### 3.1 Material: [`BlueAirflowEffect.mat`](Assets/Prefabs/Airflow_v6.1/Materials/BlueAirflowEffect.mat)

**Type:** Unity Material
**Shader:** [`Custom/AirflowEffectFlipped`](Assets/Shaders/BlueAirflowEffect.shader) (GUID: `e4f3d2c1b0a9f8e7d6c5b4a392817060`)
**Purpose:** Provides the visual appearance — all shader features identical to legacy tube, but rendered in uniform blue, with UV.y flipped to match Airflow_v6.1 mesh orientation

**Key properties:**

| Property | Value | Purpose |
|----------|-------|---------|
| `m_Shader` | `guid: e4f3d2c1b0a9f8e7d6c5b4a392817060` | Points to the UV-flipped variant `Custom/AirflowEffectFlipped` |
| `m_EnableInstancingVariants` | `1` | Required for per-renderer MaterialPropertyBlock |
| `_Colour1` through `_Colour5` | `(0, 0.4, 1, 1)` | All 5 gradient colours → same blue → no gradient |
| `_Key1` through `_Key5` | `0, 0.2, 0.42, 0.7, 1.0` | Key positions unchanged (gradientAt will return same blue regardless) |
| `_NoiseTex` | `guid: f155b245e32ed9849bbcf9db7c61029b` | Real noise texture from the project |
| `_Progress` | `0` | Set dynamically via MPB at runtime |

**Why this works:** The shader's `GradientAt()` function interpolates between `_Colour1`–`_Colour5` based on position along `_Key1`–`_Key5`. When all colours are identical, every position returns the same blue regardless of key position. The gradient is effectively "disabled" without any shader code changes. The UV flip (`1.0 - i.uv.y`) corrects the fill direction because Airflow_v6.1 meshes have `uv.y = 0` at Exhaust and `uv.y = 1` at Intake.

### 3.2 Controller: [`AirflowV61Controller.cs`](Assets/Scripts/Effects/AirflowV61Controller.cs)

**Type:** C# MonoBehaviour
**Attached to:** Root `Airflow_v6.1` GameObject in the prefab
**Purpose:** Drives `_Progress` on all 6 child mesh renderers via MaterialPropertyBlock

**Lifecycle:**

```
Start()
  ├── Find all MeshRenderer children (GetComponentsInChildren)
  ├── Create MaterialPropertyBlock
  └── Auto-discover JetEngineShowWorking (FindObjectOfType)
       └── If null → disable self with error log

Update()
  ├── Poll showWorkingRef.CurrentAirflowProgress → targetProgress
  ├── Auto-detect flow state:
  │   ├── targetProgress > 0.01 && !isFlowing → isFlowing = true, reset current to 0
  │   └── targetProgress < 0.001 && isFlowing → isFlowing = false, ApplyProgress(0)
  ├── If flowing:
  │   ├── SmoothLerp currentProgress toward targetProgress
  │   └── ApplyProgress(currentProgress)
  └── If not flowing → return

ApplyProgress(progress)
  ├── mpb.SetFloat("_Progress", progress)
  └── For each renderer: renderer.SetPropertyBlock(mpb)
```

**Design decisions:**
- **No bridge script needed** — the controller directly polls progress from the existing `JetEngineShowWorking` that's already in every scene
- **No renderer enabling/disabling** — meshes stay enabled at all times; the shader's `_Progress` controls what's visible via the progress cutoff in the fragment shader
- **Auto-detect flow** — instead of requiring external `ShowFlow()`/`HideFlow()` calls, the controller watches the progress value itself. If it rises above 0.01, animation starts. If it drops to 0, animation stops
- **Per-renderer MPB** — all 6 mesh pieces share the same material, but each gets its own `_Progress` value via `SetPropertyBlock`. Currently all 6 get the same value, but this architecture supports per-piece variation if needed
- **Smooth interpolation** — `Mathf.Lerp` with configurable speed prevents visual popping when progress jumps suddenly

### 3.3 Prefab: [`Airflow_v6.1.prefab`](Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab)

**Structure:**
```
Airflow_v6.1 (root)
  ├── MeshRenderer: Intake        → BlueAirflowEffect.mat
  ├── MeshRenderer: Bypass        → BlueAirflowEffect.mat
  └── Air_Flow (child)
       ├── MeshRenderer: Compressor  → BlueAirflowEffect.mat
       ├── MeshRenderer: Combustion  → BlueAirflowEffect.mat
       ├── MeshRenderer: Turbine     → BlueAirflowEffect.mat
       └── MeshRenderer: Exhaust     → BlueAirflowEffect.mat
```

**Changes from original:**
- All 6 MeshRenderer material slots: changed from their original materials to [`BlueAirflowEffect.mat`](Assets/Prefabs/Airflow_v6.1/Materials/BlueAirflowEffect.mat) (GUID: `f536e9e4bd460dd45bf40f277b071b98`)
- Added [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) MonoBehaviour to root (script GUID: `3bec2db9eb7431841ad597b1c21c239b`)
- Controller's `showWorkingRef` left empty (auto-discovered at runtime)

### 3.4 Legacy Shader (UNCHANGED): [`AirflowEffect.shader`](Assets/Shaders/AirflowEffect.shader)

**Type:** URP Unlit Shader (custom HLSL)
**Queue:** Transparent+50
**Blend:** One One (additive)
**ZWrite:** Off
**Cull:** Off

**Key features used:**
- `_Progress` cutoff via UV.y: `float distFromProgress = _Progress - i.uv.y`
- 5-colour gradient via `GradientAt()` — colours come from material properties
- Scrolling flow rings: sin wave modulated by `_ScrollSpeed` and `_FlowTiling`
- Noise turbulence: `tex2D(_NoiseTex, ...)` displaces ring UVs
- Fresnel rim glow: pow(1 - dot(normal, viewDir), _RimPower) * _RimIntensity
- Additive fade at progress front: saturate(distFromProgress / _ProgressFade)

**UV Direction Issue:** The legacy shader assumes `uv.y = 0` at Intake (front) and `uv.y = 1` at Exhaust (back). However, the Airflow_v6.1 meshes have UV.y going in the **opposite** direction: `uv.y = 0` at Exhaust and `uv.y = 1` at Intake. This causes the fill to start from the wrong side if the shader is used as-is. A shader variant is required to flip the UV coordinate.

---

### 3.5 Shader Variant (NEW): [`BlueAirflowEffect.shader`](Assets/Shaders/BlueAirflowEffect.shader)

**Type:** URP Unlit Shader (custom HLSL) — exact copy of `Custom/AirflowEffect` with one line changed
**Shader Name:** `Custom/AirflowEffectFlipped`
**GUID:** `e4f3d2c1b0a9f8e7d6c5b4a392817060`
**Purpose:** Flip UV.y direction to match Airflow_v6.1 mesh orientation

**The critical change (line 149):**

```hlsl
// Airflow_v6.1 meshes have UV.y going EXHAUST(0) → INTAKE(1)
// Flip UV.y so 0 = Intake, 1 = Exhaust (matching legacy shader expectation)
float distFromProgress = _Progress - (1.0 - i.uv.y);
```

**Mapping:**
| Mesh UV.y | Flipped Value | Meaning |
|-----------|---------------|---------|
| 0 (Exhaust end) | `1.0 - 0 = 1` | Fully "filled" (distFromProgress = _Progress - 1, negative → invisible) |
| 1 (Intake end) | `1.0 - 1 = 0` | Progress start point |

This means:
- When `_Progress = 0.3`, the front 30% of the mesh (near Intake) is visible
- When `_Progress = 1.0`, the entire mesh is filled
- The animation direction matches the legacy tube: Intake → Exhaust

**All other features identical to legacy:**
- Blending: One One (additive), ZWrite Off, Cull Off
- 5-colour gradient via `GradientAt()` (all set to same blue via material)
- Scrolling flow rings, noise turbulence, fresnel rim glow
- All material properties (`_ScrollSpeed`, `_NoiseStrength`, `_RimPower`, etc.)

---

## 4. Data Flow

```
Show Working starts
  → JetEngineShowWorking.CurrentAirflowProgress rises (0→1)
  → AirflowV61Controller.Update() polls progress each frame
  → Progress is smoothed via Lerp
  → MaterialPropertyBlock.SetFloat("_Progress", value)
  → All 6 renderers receive the value via SetPropertyBlock
  → Shader's fragment shader renders blue glow where (1.0 - i.uv.y) < _Progress
     (UV flipped: mesh's Exhaust=0 → shader's 1, mesh's Intake=1 → shader's 0)
  → Blue airflow fills from Intake (front) toward Exhaust (back)
  → Legacy tube runs alongside, completely unchanged

Show Working ends
  → CurrentAirflowProgress drops to 0
  → Controller detects isFlowing=false, snaps progress to 0
  → All meshes become fully transparent (nothing is "in front of" progress)
```

---

## 5. MaterialPropertyBlock vs Shared Material

| Aspect | Shared Material | MaterialPropertyBlock |
|--------|----------------|----------------------|
| Where defined | On the material asset | Set per-renderer at runtime |
| Persists? | Saved in asset | Runtime only, not serialized |
| Used for | Static defaults (colour, tiling, etc.) | Per-frame progress updates |
| Why needed | Same material on all 6 pieces | Each renderer gets its own `_Progress` (future flexibility) |

Currently all 6 pieces receive the same `_Progress` value, but using MPB allows per-piece timing offsets if desired later.

---

## 6. Evolution from Previous (Over-Engineered) Approach

The initial implementation was too complex. Here's what changed:

| Aspect | Initial (REJECTED) | Simplified (Phase 7) | Current (Phase 8 — UV fix) |
|--------|-------------------|---------------------|---------------------------|
| Shader | New `BlueAirflowEffect.shader` (replaced UV.y with local X) | Used existing `Custom/AirflowEffect` as-is | **Minimal variant** `Custom/AirflowEffectFlipped` (one line changed: `(1.0 - i.uv.y)` |
| Colours | Single `_FlowColor` property | All 5 `_Colour1`–`_Colour5` set to same blue | Same blue approach |
| UV Direction | Local X position | Used UV.y directly → **wrong direction** on Airflow_v6.1 meshes | **`(1.0 - i.uv.y)`** — flips UV to match mesh orientation |
| Bridge | `ShowWorkingFlowBridge.cs` with state machine | **No bridge** — controller auto-discovers | Still no bridge |
| Renderers | Disabled on Awake, enabled on ShowFlow | **Always enabled** — `_Progress` controls visibility | Still always enabled |
| Noise texture | Placeholder GUID (all zeros) | **Real** noise texture from project assets | Real noise texture |
| Instancing | `m_EnableInstancingVariants: 0` | **`m_EnableInstancingVariants: 1`** (required for MPB) | Instancing enabled |
| Particle system | Required `[RequireComponent(typeof(ParticleSystem))]` | **Removed entirely** — shader-only visuals | Shader-only |
| Complexity | ~450 lines total (shader + controller + bridge) | **~125 lines** in one file | **~195 lines** shader (1 line changed) + ~125 lines controller |

**Lessons:**
1. **All-blue gradient:** Setting 5 material properties to the same colour value achieves the "no gradient" requirement without gradient-related shader changes.
2. **UV direction matters:** Different mesh pieces may have different UV orientation. The simplest fix is a minimal shader variant with `(1.0 - i.uv.y)` instead of `i.uv.y` — a single-line change that corrects the fill direction while preserving every other visual feature (scroll rings, noise, rim glow, etc.).
3. **No bridge needed:** The controller can directly poll `JetEngineShowWorking.CurrentAirflowProgress` and auto-detect flow state from the progress value itself.