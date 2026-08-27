# Interactive Stages Architecture — Air Compression, Fuel Injection, Combustion/Ignition

## 1. Goal

Add three new **interactive stages** to the Show Working flow, positioned **after the Turbine Start (step 3)** and **before the Legacy Explorer transition**. Each stage requires the user to interact (tap part / press button) to trigger animated VFX, audio narration, and airflow progress.

## 2. Current Flow (Baseline)

```
Steps 0-2:  Grab-removal of covers                 [skipLift=true, isTurbineStartStep=false]
Step 3:     Turbine Start (button press)            [isTurbineStartStep=true]
            → blades spin, airflow to 7%
            → CompleteAllSteps()
            → TransitionToLegacyExplorer()
            → SimplePartExplorer Next/Prev buttons
```

## 3. New Flow (After Implementation)

```
Steps 0-2:  Grab-removal of covers                  [skipLift=true]
Step 3:     Turbine Start (button press)             [isTurbineStartStep=true]
            → blades spin, airflow to 7%
── NEW STAGES INSERTED HERE ──────────────────────────────────
Step 4:     Air Compression Stage                    [stepType=PartTap]
            → User taps "High pressure Blades" part
            → Blades glow orange, airflow tube narrows + brightens
            → Narration + airflow progress to ~20-30%
Step 5:     Fuel Injection Stage                     [stepType=PartTap]
            → User selects "Fuel Injector" part
            → Fuel spray particles, injector pulses, slow-motion
            → Narration + airflow progress to ~45%
Step 6:     Combustion/Ignition Stage                [stepType=IgniteButton]
            → User presses "Ignite" button
            → Chamber glows orange, flame particles expand, sound intensifies
            → Narration + airflow progress to ~60%
── THEN TRANSITION ──────────────────────────────────────────
            → TransitionToLegacyExplorer()
            → SimplePartExplorer Next/Prev (remaining parts)
```

## 4. New Step Types

The current [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:657) uses boolean flags (`skipLift`, `isTurbineStartStep`). We replace these with an enum:

```csharp
public enum InteractiveStepType
{
    GrabRemove,     // User grabs part and moves it away (current cover steps)
    TurbineStart,   // User presses "Start Turbine" button (current step 3)
    PartTap,        // User taps/selects a highlighted part → triggers VFX
    IgniteButton    // User presses "Ignite" button → triggers combustion VFX
}
```

### 4.1 `PartTap` — Behaviour

1. Controller highlights the target part (lift + outline, same as current non-cover steps)
2. User **taps/clicks** on the part (not grab — uses `EngineInteractor.OnPartSelected` or `IPointerClickHandler`)
3. On tap:
   - Play narration audio
   - Activate step-specific VFX controller
   - Advance airflow progress
   - Optionally trigger slow-motion
   - Auto-advance to next step after VFX duration

### 4.2 `IgniteButton` — Behaviour

1. Show "Ignite" button on tablet (like Start Turbine button)
2. Hide standard navigation buttons
3. Highlight combustion chamber area visually
4. User presses the button:
   - Play ignition audio
   - Activate flame particles + chamber glow
   - Intensify engine audio
   - Advance airflow progress
   - Auto-advance after VFX duration

## 5. New Components (VFX System)

### 5.1 [`PartGlowController`](Assets/Scripts/Effects/PartGlowController.cs) (NEW)

Controls **emission glow** on engine part renderers.

```csharp
public class PartGlowController : MonoBehaviour
{
    public Renderer targetRenderer;
    public int materialIndex = 0;
    public Color glowColor = new Color(1f, 0.5f, 0f); // orange default
    [Range(0f, 10f)] public float maxGlowIntensity = 3f;
    public AnimationCurve glowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float glowDuration = 1.5f;

    private MaterialPropertyBlock _mpb; // non-allocating material updates
    private Coroutine _glowCoroutine;

    public void ActivateGlow(System.Action onComplete = null);
    public void StopGlow();
}
```

**Implementation detail:** Uses [`MaterialPropertyBlock`](https://docs.unity3d.com/ScriptReference/MaterialPropertyBlock.html) to avoid creating material instances. Sets `_EmissiveColor` or `_EmissionColor` property.

### 5.2 [`AirCompressionController`](Assets/Scripts/Effects/AirCompressionController.cs) (NEW)

Manages the **airflow tube narrowing + color shift** for Stage 4.

```csharp
public class AirCompressionController : MonoBehaviour
{
    public JetEngineAirflowController airflowController;
    
    [Header("Tube Narrowing")]
    public AnimationCurve narrowCurve; // radius scale over time
    public float narrowDuration = 1.5f;
    public float targetRadiusScale = 0.6f; // narrow to 60% of current
    
    [Header("Color Shift")]
    public Color targetColor = new Color(0.3f, 0.8f, 1f); // bright cyan
    public float colorShiftDuration = 1.0f;

    private Coroutine _compressionCoroutine;

    public void StartCompression(System.Action onComplete = null);
    public void ResetCompression();
}
```

Integration with [`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs):
- Add public methods: `AnimateRadiusScale(float target, float duration, AnimationCurve curve)`
- Add public methods: `AnimateTubeColor(Color target, float duration)`
- Will require adding a `_radiusScaleMultiplier` float that stacks on top of `radiusScale`

### 5.3 [`FuelSprayController`](Assets/Scripts/Effects/FuelSprayController.cs) (NEW)

Manages **fuel spray particle effect** for Stage 5.

```csharp
public class FuelSprayController : MonoBehaviour
{
    public ParticleSystem fuelSprayPrefab;    // assign in Inspector
    public Transform injectorLocation;         // where to spawn spray
    public float sprayDuration = 2.0f;
    public float sprayRate = 50f;
    
    private ParticleSystem _activeSpray;

    public void StartSpray(System.Action onComplete = null);
    public void StopSpray();
}
```

**Spawning strategy:** Particle system is **instantiated as a child** of the injector location Transform. This keeps the spray positioned correctly even if the injector moves/animates.

### 5.4 [`CombustionController`](Assets/Scripts/Effects/CombustionController.cs) (NEW)

Manages the **ignition + flame expansion** for Stage 6.

```csharp
public class CombustionController : MonoBehaviour
{
    [Header("Chamber Glow")]
    public PartGlowController chamberGlow;
    public Color chamberGlowColor = new Color(1f, 0.4f, 0f); // orange-red
    
    [Header("Flame Particles")]
    public ParticleSystem flamePrefab;          // expanding backward flames
    public Transform flameOrigin;               // combustion chamber center
    public float flameDuration = 2.5f;
    
    [Header("Audio")]
    public AudioClip ignitionBoom;              // one-shot boom
    public AudioClip intensifiedEngineLoop;     // engine sound to crossfade to
    public float audioCrossfadeDuration = 1.0f;
    
    [Header("Camera Shake")]
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.1f;

    public void StartCombustion(System.Action onComplete = null);
}
```

### 5.5 [`SlowMotionController`](Assets/Scripts/Effects/SlowMotionController.cs) (NEW)

Simple **Time.timeScale modifier** for the Fuel Injection "slow-motion moment."

```csharp
public class SlowMotionController : MonoBehaviour
{
    public float timeScale = 0.3f;       // 30% speed
    public float duration = 1.5f;
    public bool fadeIn = true;
    public bool fadeOut = true;
    public float fadeDuration = 0.3f;

    private static SlowMotionController _instance;

    public void TriggerSlowMotion(System.Action onComplete = null);
    public void ResumeNormalTime();
}
```

**Critical:** Must reset `Time.timeScale = 1f` in `OnDestroy()` or when the Show Working flow stops, to prevent stuck slow-motion.

## 6. Modifications to Existing Components

### 6.1 [`ShowWorkingStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:657)

**Additions:**

```csharp
[System.Serializable]
public class ShowWorkingStep
{
    // ── Existing fields (unchanged) ──
    public string stepName;
    public string instruction;
    public GameObject targetPart;
    public float advanceDistance;
    public bool skipLift;
    public bool isTurbineStartStep;            // KEPT for backward compat
    public float turbineStartAirflowProgress;
    public float airflowProgress;
    public AudioClip stepAudio;
    public AudioClip turbineStartAudio;

    // ── NEW: Step type ──
    public InteractiveStepType stepType = InteractiveStepType.GrabRemove;

    // ── NEW: VFX Controllers (assigned in Inspector) ──
    public PartGlowController partGlowController;
    public AirCompressionController airCompressionController;
    public FuelSprayController fuelSprayController;
    public CombustionController combustionController;
    public SlowMotionController slowMotionController;

    // ── NEW: VFX Settings ──
    [Header("VFX Timing")]
    public float vfxDuration = 2.0f;            // how long VFX plays before auto-advance
    public bool triggerSlowMotion = false;       // enable slow-mo for this step

    [Header("Ignition Step (stepType=IgniteButton)")]
    public AudioClip ignitionButtonAudio;        // "Ignite" button press sound
    public GameObject igniteButton;              // reference to the Ignite button GO
}
```

### 6.2 [`ShowWorkingInteractiveController`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs)

**Key modifications:**

| Method | Change |
|--------|--------|
| [`AdvanceToNextStep()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:244) | Switch on `step.stepType` instead of boolean flags |
| New: `OnPartTapped(EnginePart)` | Handler for PartTap steps — reads from `EngineInteractor.OnPartSelected` |
| [`OnTurbineStarted()`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:408) | Refactored into generic `OnSpecialButtonPressed()` that handles both TurbineStart and IgniteButton |
| New: `StartPartTapStep(ShowWorkingStep)` | Highlights part + subscribes to tap event |
| New: `StartIgniteStep(ShowWorkingStep)` | Shows Ignite button + combustion VFX prep |
| New: `PlayStepVFX(ShowWorkingStep)` | Routes to the correct VFX controller based on step type |

**Tap detection strategy for PartTap:**
- Register a listener on [`EngineInteractor.OnPartSelected`](Assets/Scripts/EngineInteractor.cs) (already exists in the codebase)
- When the selected part matches `step.targetPart`, trigger the VFX
- Alternatively, add an `IPointerClickHandler` to the part or use a `Physics.Raycast` approach

```csharp
private void StartPartTapStep(ShowWorkingStep step)
{
    // 1. Highlight target part (same as current non-cover steps)
    if (step.targetPart != null)
    {
        _currentTargetPart = step.targetPart;
        _currentEnginePart = step.targetPart.GetComponent<EnginePart>();
        // ... lift + highlight ...
    }

    // 2. Subscribe to part selection
    var interactor = FindFirstObjectByType<EngineInteractor>();
    if (interactor != null)
        interactor.OnPartSelected += OnPartTapped;

    // 3. Play narration
    PlayStepAudio(step);
}

private void OnPartTapped(EnginePart part)
{
    if (part == null || part.gameObject != _currentTargetPart) return;
    if (_stepAdvancing) return;

    // Part was correctly tapped!
    var step = steps[_currentStepIndex];
    
    // 1. Unsubscribe from tap events
    var interactor = FindFirstObjectByType<EngineInteractor>();
    if (interactor != null)
        interactor.OnPartSelected -= OnPartTapped;

    // 2. Play step VFX
    StartCoroutine(PlayStepVFX(step, () => {
        // 3. Advance airflow
        AdvanceAirflowForStep(step);
        
        // 4. Complete step
        CompleteCurrentStep();
    }));
}
```

### 6.3 [`JetEngineAirflowController`](Assets/Scripts/JetEngineAirflowController.cs)

**Additions:**

```csharp
// ── NEW: Dynamic radius scale multiplier ──
private float _radiusScaleMultiplier = 1f;

public void AnimateRadiusScale(float targetMultiplier, float duration, AnimationCurve curve)
{
    // Smoothly changes _radiusScaleMultiplier from current to target
    // Rebuilds the mesh or applies vertex displacement
    // Simple approach: scale the tube GameObject's localScale.xz
}

public void AnimateTubeColor(Color targetColor, float duration)
{
    // Animates the _TubeColor or _BaseColor property on _tubeMaterial
}
```

**Two approaches for tube narrowing:**

| Approach | Pros | Cons |
|-----------|------|------|
| **A. Scale tube GO** | Simple, no mesh rebuild | May affect position; imprecise |
| **B. Rebuild mesh** | Vertex-precise narrowing | More expensive; triggers GC |

**Recommendation:** Approach A — scale `_airflowTube.transform.localScale` on X/Z axes with a smoothing animation. This is performant and visually adequate for VR.

### 6.4 [`JetEngineShowWorking`](Assets/Scripts/JetEngineShowWorking.cs)

**Addition to ProgressMappings:** Ensure "High pressure Blades", "Fuel Injector", and any "Combustion Chamber" parts are mapped:

```csharp
new ProgressMapping { partNameContains = "High pressure Blades", progress = 0.30f },
    // was 0.60f — now used for air compression (stage 4, earlier in flow)
new ProgressMapping { partNameContains = "Fuel Injector",        progress = 0.45f },
    // was 0.45f — unchanged
```

**New method:**

```csharp
public void SetAirflowProgressDirect(float progress, bool cumulative = true)
{
    // Similar to AdvanceAirflowTo but with optional cumulative guard
    if (cumulative && progress <= _highestProgress) return;
    _highestProgress = progress;
    airflowController.ShowTubeImmediate();
    airflowController.SetProgress(_highestProgress);
}
```

### 6.5 [`EngineViewManager`](Assets/Scripts/EngineViewManager.cs)

**Add ignite button handling:**

```csharp
[Header("Button References")]
// ... existing ...
public GameObject startTurbineButton;  // existing
public GameObject igniteButton;        // NEW

// In ActivateShowWorkingView():
if (igniteButton != null)
    igniteButton.SetActive(false);     // initially hidden

// In DisableViewButtons():
if (igniteButton != null)
    igniteButton.SetActive(false);

// In EnableViewButtons():
// Keep ignition button hidden — it's managed by InteractiveController
```

### 6.6 [`TabletUIController`](Assets/Scripts/TabletUIController.cs)

**Add:**

```csharp
[Header("Show Working")]
public GameObject startTurbineButton;  // existing
public GameObject igniteButton;        // NEW

public void OnIgniteClicked()
{
    Debug.Log("[TabletUIController] OnIgniteClicked()");
    var interactive = FindFirstObjectByType<ShowWorkingInteractiveController>();
    if (interactive == null) { Debug.LogError("..."); return; }
    interactive.OnSpecialButtonPressed();  // or OnIgniteButtonPressed()
}
```

## 7. Audio Pipeline

Each stage needs:

| Stage | Narration | VFX Sound | Engine Audio |
|-------|-----------|-----------|--------------|
| 4. Air Compression | "The compressor squeezes incoming air, increasing pressure and temperature." | Subtle air pressure hiss | Unchanged (turbine hum) |
| 5. Fuel Injection | "Fuel is injected into the compressed air before combustion." | Spray hiss | Unchanged |
| 6. Combustion | "The fuel-air mixture ignites, producing high-energy gases." | Boom + rush | Crossfade to intensified loop |

**Implementation:** 
- Narration → `step.stepAudio` (existing system, plays through `audioSource`)
- VFX sounds → `PlayOneShot` on a secondary `AudioSource` dedicated to one-shot effects
- Engine crossfade → A third `AudioSource` with crossfade coroutine

**New component for engine audio crossfade:**

```csharp
public class EngineAudioController : MonoBehaviour
{
    public AudioSource engineAudioSource;      // primary engine hum
    public AudioClip normalEngineLoop;         // current turbine hum
    public AudioClip intensifiedEngineLoop;    // post-ignition rumble
    public float crossfadeDuration = 1.0f;

    public void CrossfadeToIntensified();
    public void CrossfadeToNormal();
}
```

## 8. Step-by-Step Stage Implementations

### Stage 4: Air Compression

| Element | Implementation |
|---------|---------------|
| **Trigger** | User taps "High pressure Blades" part |
| **Part glow** | [`PartGlowController`](Assets/Scripts/Effects/PartGlowController.cs) — orange glow on compressor blades, 1.5s duration |
| **Tube narrowing** | [`AirCompressionController`](Assets/Scripts/Effects/AirCompressionController.cs) — scale tube narrower, shift to bright cyan |
| **Narration** | `step.stepAudio` — "The compressor squeezes incoming air..." |
| **Airflow** | Advance to 30% (matches "Shaft" progress mapping) |
| **Auto-advance** | After VFX completes (~2s) |

### Stage 5: Fuel Injection

| Element | Implementation |
|---------|---------------|
| **Trigger** | User taps "Fuel Injector" part |
| **Slow-motion** | [`SlowMotionController`](Assets/Scripts/Effects/SlowMotionController.cs) — 1.5s at 30% speed |
| **Fuel spray** | [`FuelSprayController`](Assets/Scripts/Effects/FuelSprayController.cs) — particle spray at injector location |
| **Injector pulse** | Scale pulse animation on injector GameObject |
| **Narration** | `step.stepAudio` — "Fuel is injected into the compressed air..." |
| **Airflow** | Advance to 45% (matches "Fuel Injector" progress mapping) |
| **Auto-advance** | After slow-motion + spray complete (~2.5s) |

### Stage 6: Combustion / Ignition

| Element | Implementation |
|---------|---------------|
| **Trigger** | User presses "Ignite" button on tablet |
| **Ignite button** | Same pattern as `startTurbineButton` — new `igniteButton` GameObject |
| **Chamber glow** | [`PartGlowController`](Assets/Scripts/Effects/PartGlowController.cs) — orange-red glow on combustion chamber parts |
| **Flame particles** | [`CombustionController`](Assets/Scripts/Effects/CombustionController.cs) — expanding backward flame particles |
| **Audio** | Ignition boom (one-shot) + crossfade engine to intensified loop |
| **Camera shake** | Brief 0.3s shake for impact |
| **Narration** | Plays after ignition VFX starts — "The fuel-air mixture ignites..." |
| **Airflow** | Advance to 60% (matches "High pressure Blades" original progress) |
| **Auto-advance** | After flame + audio complete (~3s) |

## 9. New Progress Mapping

The airflow progress values need rebalancing since "High pressure Blades" moves earlier:

| Part Name | Old Progress | New Progress | Stage |
|-----------|-------------|-------------|-------|
| (Turbine Start) | — | 0.07 | 3 |
| High pressure Blades | 0.60 | **0.30** | 4 — Air Compression |
| Fuel Injector | 0.45 | **0.45** | 5 — Fuel Injection |
| (Combustion) | — | **0.60** | 6 — Ignition |
| Highpressure Turbine Shaft | 0.72 | 0.72 | Legacy explorer |
| Cap | 0.85 | 0.85 | Legacy explorer |
| Wires | 1.00 | 1.00 | Legacy explorer |

**Important:** The [`progressMappings`](Assets/Scripts/JetEngineShowWorking.cs:42) array uses substring matching. "High pressure Blades" now matches both Stage 4 (interactive) AND the legacy explorer. Since Stage 4 is handled by the interactive controller (not `OnPartShown`), this won't conflict — by the time legacy explorer shows "High pressure Blades," airflow is already at 60%+.

## 10. Implementation Order

| Step | File(s) | Description |
|------|---------|-------------|
| 1 | [`docs/INTERACTIVE_STAGES_PLAN.md`](docs/INTERACTIVE_STAGES_PLAN.md) | **THIS DOCUMENT** — architectural plan |
| 2 | [`Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) (line 657) | Add `InteractiveStepType` enum before `ShowWorkingStep` class |
| 3 | Same file — `ShowWorkingStep` class | Add new fields: `stepType`, VFX controller refs, `vfxDuration`, `triggerSlowMotion`, `igniteButton`, `ignitionButtonAudio` |
| 4 | [`Assets/Scripts/Effects/PartGlowController.cs`](Assets/Scripts/Effects/PartGlowController.cs) | NEW file — emission glow with MaterialPropertyBlock |
| 5 | [`Assets/Scripts/Effects/AirCompressionController.cs`](Assets/Scripts/Effects/AirCompressionController.cs) | NEW file — tube narrowing + color shift |
| 6 | [`Assets/Scripts/Effects/FuelSprayController.cs`](Assets/Scripts/Effects/FuelSprayController.cs) | NEW file — fuel spray particle system |
| 7 | [`Assets/Scripts/Effects/CombustionController.cs`](Assets/Scripts/Effects/CombustionController.cs) | NEW file — chamber glow, flame particles, audio crossfade |
| 8 | [`Assets/Scripts/Effects/SlowMotionController.cs`](Assets/Scripts/Effects/SlowMotionController.cs) | NEW file — Time.timeScale modifier |
| 9 | [`Assets/Scripts/Effects/EngineAudioController.cs`](Assets/Scripts/Effects/EngineAudioController.cs) | NEW file — engine audio crossfade system |
| 10 | [`Assets/Scripts/JetEngineAirflowController.cs`](Assets/Scripts/JetEngineAirflowController.cs) | Add `AnimateRadiusScale()` and `AnimateTubeColor()` |
| 11 | [`Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) | Refactor `AdvanceToNextStep()` — switch on `stepType` |
| 12 | Same file | Add `StartPartTapStep()` method |
| 13 | Same file | Add `OnPartTapped()` handler (subscribes to `EngineInteractor.OnPartSelected`) |
| 14 | Same file | Add `StartIgniteStep()` method |
| 15 | Same file | Add `PlayStepVFX()` — routes to correct controller |
| 16 | Same file | Add `OnSpecialButtonPressed()` — handles both TurbineStart and IgniteButton |
| 17 | [`Assets/Scripts/JetEngineShowWorking.cs`](Assets/Scripts/JetEngineShowWorking.cs) | Add `SetAirflowProgressDirect(float, bool)` method |
| 18 | [`Assets/Scripts/EngineViewManager.cs`](Assets/Scripts/EngineViewManager.cs) | Add `igniteButton` reference + visibility management |
| 19 | [`Assets/Scripts/TabletUIController.cs`](Assets/Scripts/TabletUIController.cs) | Add `igniteButton` field + `OnIgniteClicked()` method |
| 20 | Same file — `OnStartTurbineClicked()` | Refactor both button clicks to use `OnSpecialButtonPressed()` pattern |
| 21 | Inspector setup (manual) | — Assign VFX controller references in ShowWorkingStep[] Inspector |
| 22 | Inspector setup (manual) | — Wire Ignite button OnClick → TabletUIController.OnIgniteClicked() |
| 23 | Inspector setup (manual) | — Configure particle system prefabs for fuel spray + flames |
| 24 | Testing | — Run through full flow: covers → turbine → compression → fuel injection → ignition → legacy explorer |

## 11. Backward Compatibility

The [`skipLift`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:677) and [`isTurbineStartStep`](Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs:681) boolean flags are **kept** for existing step configurations. The new `stepType` enum is checked FIRST; if it's not explicitly set (defaults to `GrabRemove`), the legacy boolean flags determine behaviour.

```csharp
// In AdvanceToNextStep():
switch (step.stepType)
{
    case InteractiveStepType.PartTap:
        StartPartTapStep(step);
        return;
    case InteractiveStepType.IgniteButton:
        StartIgniteStep(step);
        return;
    case InteractiveStepType.GrabRemove:
    default:
        // Existing boolean-flag logic unchanged
        if (step.isTurbineStartStep) { /* existing */ }
        else { /* existing grab-remove */ }
        break;
}
```

This ensures **no existing scenes break** — they simply don't set `stepType` and continue using the old flags.

## 12. Stretch Goals / Optional Enhancements

- **HDRP/URP Volume integration:** Subtle color grading shift during combustion (warmer tones)
- **VR Controller haptics:** Brief haptic pulse on part tap and ignition button press
- **Post-processing bloom:** Increase bloom intensity during combustion for more dramatic flame glow
- **Thermal distortion:** Shader-based heat shimmer above combustion chamber using screen-space distortion

---

*Document version 1.0 — Architectural plan for Interactive Stages 4-6*