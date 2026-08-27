# Setup Checklist: Educational Stages — Rebuilt Architecture

The Show Working system has been rebuilt into a clean, optimized architecture using the **strategy pattern**.
The monolithic 1763-line file is now split into 8 focused files in `Assets/Scripts/ShowWorking/`.

For full architectural details, see [`docs/REBUILT_SHOW_WORKING_ARCHITECTURE.md`](REBUILT_SHOW_WORKING_ARCHITECTURE.md).

## Step 0: Let Unity Recompile

After saving all files in `Assets/Scripts/ShowWorking/`, wait for Unity to finish compiling (bottom-right spinner stops).

The new files are:
- [`InteractiveStepType.cs`](../Assets/Scripts/ShowWorking/InteractiveStepType.cs) — enum (5 step types)
- [`ShowWorkingStep.cs`](../Assets/Scripts/ShowWorking/ShowWorkingStep.cs) — step data model (same 30+ serialized fields)
- [`RuntimeBladeGroup.cs`](../Assets/Scripts/ShowWorking/RuntimeBladeGroup.cs) — blade group tracking
- [`StepContext.cs`](../Assets/Scripts/ShowWorking/StepContext.cs) — shared context for handlers
- [`IStepHandler.cs`](../Assets/Scripts/ShowWorking/IStepHandler.cs) — strategy pattern interface
- [`StepHandlers.cs`](../Assets/Scripts/ShowWorking/StepHandlers.cs) — 5 handler implementations
- [`StageAutoPopulate.cs`](../Assets/Scripts/ShowWorking/StageAutoPopulate.cs) — ContextMenu auto-populate logic
- [`ShowWorkingInteractiveController.cs`](../Assets/Scripts/ShowWorking/ShowWorkingInteractiveController.cs) — orchestrator (~350 lines)

**All serialized field names and public API are IDENTICAL** — your existing Scene configuration is fully compatible.

---

## Step 1: Auto-Populate the 14 Stage Steps

1. Select the **ShowWorkingInteractiveController** GameObject in the Hierarchy
2. In the Inspector, click the **3-dot menu (⋮)** at the top-right of the script component
3. Click **"Populate Educational Stage Steps"**
4. Check the Console — you should see: *"Educational stages populated: 17 total steps"*

Your existing 3 steps (covers + turbine start) are **preserved**. Steps 3-16 are now appended with all text filled in.

---

## Step 2: Create 2 Visual Wrapper GameObjects

**Important:** These are EMPTY wrapper GameObjects. You parent your existing Intake/Bypass effects UNDER them. Only the wrapper gets unchecked.

```
Engine Root (or wherever)
├── Intake_Airflow_Visual      ← NEW wrapper, UNCHECKED ❌ (code activates it)
│   └── [Your existing Intake airflow effect]  ← Keep CHECKED ✅
│
└── Bypass_Air_Visual          ← NEW wrapper, UNCHECKED ❌ (code activates it)
    └── [Your existing Bypass air effect]      ← Keep CHECKED ✅
```

1. Right-click in Hierarchy → Create Empty → name it **`Intake_Airflow_Visual`**
2. **UNCHECK** the checkbox next to `Intake_Airflow_Visual` (the wrapper starts disabled)
3. Drag your **existing Intake airflow GameObject** inside as a child of `Intake_Airflow_Visual`
4. Repeat for **`Bypass_Air_Visual`** — UNCHECK it, then drag your existing Bypass effect inside

The existing child effects stay CHECKED (active). When the code enables the wrapper parent at the right step, the child activates automatically.

---

## Step 3: Assign Target Parts (Optional for PartTap Steps)

`targetPart` is **OPTIONAL**. It points to a single GameObject for the **highlight line** (the white outline that follows a part). If there is no single part to highlight, **leave it empty** (Size = 0).

- For **blade rotation**: This is handled automatically by the existing `TurbineBladeRotator` system — you do NOT need to assign any parts for rotation.
- For **highlighting multiple parts**: Use `highlightParts[]` (Step 6) instead.

For each step below, expand `targetPart` in the Inspector and **drag the matching part from the Hierarchy** into the slot, or leave empty:

| Step | Step Name | targetPart → drag this GameObject (or leave empty) |
|------|-----------|---------------------------------------------------|
| 0 | Remove Left Outer Cover | Left outer cover part |
| 1 | Remove Left Inner Cover | Left inner cover part |
| 2 | Start Turbine | *(leave empty — blade rotation is automatic)* |
| 3 | Air Intake - Diffuser | *(leave empty, or drag 1 blade from the 26 if you want a highlight line)* |
| 4 | Air Intake - Bypass | Bypass duct part |
| 5 | Compression Chamber | Compressor section part |
| 6 | Compressor Blades | *(leave empty — use `highlightParts[]` instead)* |
| 7 | Fuel Injection Zone | Combustion chamber part |
| 8 | Fuel System | *(leave empty — use `highlightParts[]` instead)* |
| 9 | HP Turbine Entry | HP turbine entry part |
| 10 | HP Mid Blades | *(leave empty — use `highlightParts[]` instead)* |
| 11 | HP Compressor | *(leave empty — use `highlightParts[]` instead)* |
| 12 | Rear HP Blades | *(leave empty — use `highlightParts[]` instead)* |
| 13 | LP Turbine | LP turbine part |
| 14 | Exhaust Nozzle | Exhaust nozzle part |
| 15 | Full Exhaust Flow | *(leave empty)* |
| 16 | To Conclude | *(leave empty)* |

---

## Step 4: Fill `activateOnStepStart[]` Arrays

Only 2 steps need these. All others keep `Size = 0`.

**Important:** The Intake visual activates when the turbine starts (step 2), NOT at step 3. The Diffuser step just explains the part — the visual is already ON.

- [ ] **Step 2** ("Start Turbine") → expand `activateOnStepStart` → drag `Intake_Airflow_Visual` into `Element 0`
- [ ] **Step 4** ("Air Intake - Bypass") → expand `activateOnStepStart` → drag `Bypass_Air_Visual` into `Element 0`
- [ ] **All other steps** → `activateOnStepStart` → set **Size = 0** (leave empty)

---

## Step 5: Add PartHoverPanel Components

For each part used in Step 6 below, add a child GameObject with `PartHoverPanel` component:

1. Select the part in the Hierarchy
2. Right-click → Create Empty child → name it `HoverPanel`
3. Add `PartHoverPanel` component to it
4. Ensure the child starts **disabled** (UNCHECK the checkbox)
5. Configure the panel's LineRenderer and UI text as needed

- [ ] **Compressor Blades** → has HoverPanel child
- [ ] **Fuel Cap** → has HoverPanel child
- [ ] **Fuel Injector** → has HoverPanel child
- [ ] **High Pressure Mid Blades** → has HoverPanel child
- [ ] **High Pressure Compressor** → has HoverPanel child
- [ ] **Rear High Pressure Blades** → has HoverPanel child

---

## Step 6: Fill `highlightParts[]` Arrays

Only 5 steps need these. All others keep `Size = 0`.

- [ ] **Step 6** ("Compressor Blades") → expand `highlightParts`
  - **Size = 1**
  - `Element 0` → drag **Compressor Blades** part GameObject

- [ ] **Step 8** ("Fuel System") → expand `highlightParts`
  - **Size = 2**
  - `Element 0` → drag **Fuel Cap** part GameObject
  - `Element 1` → drag **Fuel Injector** part GameObject

- [ ] **Step 10** ("HP Mid Blades") → expand `highlightParts`
  - **Size = 1**
  - `Element 0` → drag **High Pressure Mid Blades** part GameObject

- [ ] **Step 11** ("HP Compressor") → expand `highlightParts`
  - **Size = 1**
  - `Element 0` → drag **High Pressure Compressor** part GameObject

- [ ] **Step 12** ("Rear HP Blades") → expand `highlightParts`
  - **Size = 1**
  - `Element 0` → drag **Rear High Pressure Blades** part GameObject

---

## Step 7: Verify Airflow Values (Already Pre-Filled)

These are already set by the auto-populate — just confirm they look right:

| Step | airflowProgress | What it does |
|------|----------------|--------------|
| 2 | turbineStartAirflowProgress = 0.20 | Legacy tube fills to 20% |
| 5 | airflowProgress = 0.50 | Legacy tube fills to 50% |
| 7 | airflowProgress = 0.70 | Legacy tube fills to 70% |
| 9 | airflowProgress = 0.85 | Legacy tube fills to 85% |
| 14 | airflowProgress = 1.00 | Legacy tube fills to 100% |
| All others | -1 | No change to tube |

---

## Step 8: Verify VFX Controller References

For steps that need VFX controllers, expand the "VFX Controllers" section and drag the matching component from the Hierarchy:

| Step | Controller Field | Drag this component |
|------|-----------------|-------------------|
| 5 (Compression Chamber) | `airCompressionController` | AirCompressionController component |
| 7 (Fuel Injection Zone) | `fuelSprayController` | FuelSprayController component |

These may already be assigned if you had them on your existing PartTap steps.

---

## Done!

Press **Play** and click **Show Working** to test the full flow:
- Steps 0-1: Remove outer + inner covers
- Step 2: Start Turbine → blades rotate, legacy airflow tube fills to 20%, **Intake visual appears**
- Step 3: Title shows "Stage 1: Air Intake" + Diffuser part explanation
- Step 4: Same stage ("Stage 1: Air Intake") + Bypass visual appears
- Step 5: Title switches to "Stage 2: Air Compression" + tube fills to 50%
- Steps 6-16: Each stage with correct title, highlights, and airflow advances