# Airflow_v6.1 Blue Animation System — Setup Guide

## Overview

This system applies the **same animation** as the legacy airflow tube onto the [`Airflow_v6.1`](Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab) static mesh — **but with all colours set to blue** (no gradient).

**Key design:** A minimal shader variant [`BlueAirflowEffect.shader`](Assets/Shaders/BlueAirflowEffect.shader) (`Custom/AirflowEffectFlipped`) that is an exact copy of the legacy [`Custom/AirflowEffect`](Assets/Shaders/AirflowEffect.shader) shader with **one line changed** — the UV.y coordinate is flipped (`1.0 - i.uv.y`) so the progress fill direction matches the Airflow_v6.1 mesh UV layout. All 5 gradient colours (`_Colour1` through `_Colour5`) are set to the exact same blue value, so the gradient disappears and the entire effect renders in a uniform blue.

The [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) auto-discovers [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs) in the scene, polls `CurrentAirflowProgress` each frame, and drives `_Progress` on all 6 mesh renderers via **MaterialPropertyBlock**.

**No legacy files were modified.** The legacy procedural tube, [`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs), [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs), [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs), and [`ShowWorkingInteractiveController`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) are all untouched.

---

## Files Involved

| File | Purpose |
|------|---------|
| [`Assets/Shaders/BlueAirflowEffect.shader`](Assets/Shaders/BlueAirflowEffect.shader) | Minimal shader variant — exact copy of `Custom/AirflowEffect` with **UV.y flipped** (`1.0 - i.uv.y`) to match Airflow_v6.1 mesh direction |
| [`Assets/Prefabs/Airflow_v6.1/Materials/BlueAirflowEffect.mat`](Assets/Prefabs/Airflow_v6.1/Materials/BlueAirflowEffect.mat) | Material using the flipped shader, all 5 colours set to blue `(R:0, G:0.4, B:1)`, real noise texture assigned |
| [`Assets/Scripts/Effects/AirflowV61Controller.cs`](Assets/Scripts/Effects/AirflowV61Controller.cs) | Auto-discovers `JetEngineShowWorking`, polls `CurrentAirflowProgress`, drives `_Progress` on all 6 child mesh renderers via MPB |
| [`Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab`](Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab) | Prefab updated — all 6 renderers reference `BlueAirflowEffect.mat`, `AirflowV61Controller` component on root |

---

## How It Works

### The UV Flip

The Airflow_v6.1 mesh pieces have their UV.y coordinate going **Exhaust(0) → Intake(1)**, which is the opposite direction from the legacy tube. The shader's progress cutoff formula is:

```
// Legacy (UV.y = 0 = Intake, 1 = Exhaust):
float distFromProgress = _Progress - i.uv.y;

// BlueAirflowEffect (UV.y = 0 = Exhaust, 1 = Intake):
// Flip UV.y so 0 = Intake, 1 = Exhaust
float distFromProgress = _Progress - (1.0 - i.uv.y);
```

This single-line change makes the progress fill start from the Intake (front) side and move toward Exhaust (back), matching the legacy tube direction.

### The Shader Trick (All-Blue Gradient)

The shader uses a 5-colour gradient (`_Colour1` through `_Colour5`) with key positions (`_Key1` through `_Key5`). By setting **all 5 colours** to the exact same blue `(R:0, G:0.4, B:1)`, the gradient collapses into a single uniform blue:

| Property | Value |
|----------|-------|
| `_Colour1` through `_Colour5` | `R:0, G:0.4, B:1, A:1` |

The shader still does everything else exactly as the legacy tube:
- UV.y-based progress cutoff (per-piece)
- Scrolling flow rings (sin wave + noise turbulence)
- Fresnel rim glow at progress front
- Additive blending
- Noise texture variation

### Controller Logic

The [`AirflowV61Controller`](Assets/Scripts/Effects/AirflowV61Controller.cs) is a simple MPB driver:

1. **Start:** Discovers all child `MeshRenderer`s, creates a `MaterialPropertyBlock`, auto-finds `JetEngineShowWorking` in the scene
2. **Update:** Polls `CurrentAirflowProgress` from `JetEngineShowWorking`, smoothly interpolates toward it, applies `_Progress` value to all renderers via `SetPropertyBlock`
3. **Auto-detect flow:** If progress > 0.01, starts flowing. If progress drops to 0, stops flowing. No separate `ShowFlow()`/`HideFlow()` methods needed.

---

## Setup Instructions

### Step 1: Instantiate the Prefab

1. Drag [`Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab`](Assets/Prefabs/Airflow_v6.1/Airflow_v6.1.prefab) into your scene
2. Position it so it aligns with the legacy tube (typically overlapping the same X-axis span)

> The prefab already has `BlueAirflowEffect.mat` assigned to all 6 renderers and `AirflowV61Controller` on the root.

### Step 2: Verify the Controller

Select the root `Airflow_v6.1` GameObject and check the `AirflowV61Controller` component:

| Field | Expected | Notes |
|-------|----------|-------|
| `Show Working Ref` | `None` (auto-discovered) | Leave empty — auto-finds `JetEngineShowWorking` at runtime |
| `Smoothing Speed` | `3` | How fast the animation responds to progress changes |

### Step 3: Test

Play the scene and trigger Show Working (via Tablet UI or any flow that calls `AdvanceAirflowTo()` on `JetEngineShowWorking`). The Airflow_v6.1 meshes should fill with blue from **Intake (front) → Exhaust (back)**, with scrolling rings and rim glow, exactly matching the legacy tube's direction and timing but in uniform blue.

---

## Verification Checklist

- [ ] Blue glow appears on all 6 individual mesh pieces
- [ ] Progress fill moves from **Intake (front) → Exhaust (back)**
- [ ] Scrolling flow ring waves visible
- [ ] Rim glow brighter at progress front edge
- [ ] Noise turbulence adds variation
- [ ] Everything fades when Show Working ends
- [ ] Legacy procedural tube still works and is unaffected
- [ ] No errors in Console

---

## Tuning Tips

| Effect | Property to Adjust | Material Property | Range |
|--------|-------------------|-------------------|-------|
| Ring speed | `Scroll Speed` | `_ScrollSpeed` | 0–10 |
| Ring density | `Flow Tiling` | `_FlowTiling` | 1–20 |
| Rim brightness | `Rim Intensity` | `_RimIntensity` | 0–3 |
| Turbulence amount | `Noise Strength` | `_NoiseStrength` | 0–1 |
| Overall brightness | `Glow Intensity` | `_GlowIntensity` | 1–5 |
| Progress front softness | `Progress Fade` | `_ProgressFade` | 0–0.3 |
| Progress reaction speed | `Smoothing Speed` | (controller field) | 0.5–10 |

> To tune, open [`BlueAirflowEffect.mat`](Assets/Prefabs/Airflow_v6.1/Materials/BlueAirflowEffect.mat) in the Inspector and adjust the shader properties directly.