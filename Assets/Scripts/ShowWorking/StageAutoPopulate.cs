using UnityEngine;

/// <summary>
/// Contains the ContextMenu-driven auto-populate logic for educational stage steps.
/// Kept separate from the main controller to keep it clean.
/// </summary>
public static class StageAutoPopulate
{
    /// <summary>
    /// Populates 14 educational stage steps after the user's existing 3 steps (covers + turbine).
    /// Call this from a ContextMenu method on the controller.
    /// </summary>
    public static ShowWorkingStep[] Populate(ShowWorkingStep[] existingSteps)
    {
        // Preserve the user's existing 3 steps (covers + turbine start)
        var preserved = new ShowWorkingStep[Mathf.Min(existingSteps != null ? existingSteps.Length : 0, 3)];
        for (int i = 0; i < preserved.Length; i++)
            preserved[i] = existingSteps[i];

        // ── Step 3 (TurbineStart, index 2): slot for Intake_Airflow_Visual ────
        if (preserved.Length >= 3)
            preserved[2].activateOnStepStart = new GameObject[1];

        // ── Define 14 educational stage steps ─────────────────────────────────
        var stageSteps = new ShowWorkingStep[]
        {
            // ── Stage 1: Air Intake ───────────────────────────────────────────
            // Step 4 — NEW VISUAL: Airflow Jet Engine Intake (slot [0])
            new ShowWorkingStep
            {
                stepName = "Air Intake - Diffuser",
                instruction = "The front lip and intake section of the engine constitute the diffuser which slows down the air delivery to the compressor. This slow down helps reduce any flow losses in the combustion chamber. Slower air helps in stabilizing the combustion flame.",
                stageName = "Stage 1: Air Intake",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                activateOnStepStart = new GameObject[1],  // drag Intake_Airflow_Visual here
                airflowProgress = -1f
            },
            // Step 5 — NEW VISUAL: Airflow Jet Engine - Bypass air (slot [0])
            new ShowWorkingStep
            {
                stepName = "Air Intake - Bypass",
                instruction = "After the air is sucked in at high speeds the air splits into two parts, a large part of the air passes on the outside of the core of the engine through bypass ducts. A large part of the thrust is provided by the air that moves through the bypass duct.",
                stageName = "Stage 1: Air Intake",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                activateOnStepStart = new GameObject[1],  // drag Bypass_Air_Visual here
                airflowProgress = -1f
            },

            // ── Stage 2: Air Compression ──────────────────────────────────────
            // Step 6 — EXISTING AIRFLOW → 50% + HOVER PANEL: Compressor Blades (slot [0])
            new ShowWorkingStep
            {
                stepName = "Compression Chamber",
                instruction = "In the core, the air passes through a series of compressors that dramatically increases the pressure and causes a slight increase in temperature.",
                stageName = "Stage 2: Air Compression",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                highlightParts = new GameObject[1],  // drag Compressor Blades here
                airflowProgress = 0.50f
            },
            // Step 7 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "Compressor Blades",
                instruction = "With the increase in pressure, the volume of the air decreases as shown by the line between 1 and 2 of the P-V diagram. On the other hand, an increase in temperature keeps the entropy constant as shown by the line between 1 and 2 of the T-S diagram.",
                stageName = "Stage 2: Air Compression",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            },

            // ── Stage 3: Combustion ───────────────────────────────────────────
            // Step 8 — EXISTING AIRFLOW → 70% + HOVER PANELS: Fuel Cap (slot [0]), Fuel Injector (slot [1])
            new ShowWorkingStep
            {
                stepName = "Fuel Injection Zone",
                instruction = "This hot and compressed air is then pushed into the combustion chamber where multiple nozzles spray fuel into the airstream. This mixture of fuel and hot compressed air gets ignited in the combustion chamber.",
                stageName = "Stage 3: Combustion",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                highlightParts = new GameObject[2],  // drag Fuel Cap [0], Fuel Injector [1]
                airflowProgress = 0.70f
            },
            // Step 9 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "Fuel System",
                instruction = "This results in the release of a lot of heat energy, which in turn causes a dramatic increase in temperature and entropy of the system as shown by the line drawn between 2 and 3 of the T-S diagram. On the other hand, the increase in the volume keeps the pressure constant as shown by the line between 2 and 3 of the P-V diagram.",
                stageName = "Stage 3: Combustion",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            },

            // ── Stage 4: Conversion of Energy ─────────────────────────────────
            // Step 10 — EXISTING AIRFLOW → 85%
            new ShowWorkingStep
            {
                stepName = "HP Turbine Entry",
                instruction = "The hot gases drive the turbine, which converts the motion of the gases into rotational energy, further these gases expand and cool down in the turbine section.",
                stageName = "Stage 4: Conversion of Energy",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = 0.85f
            },
            // Step 11 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "HP Mid Blades",
                instruction = "It results in a steep decline in pressure and an increase in volume as shown by the line between 3 and 4 of the P-V diagram. On the other hand, as the gases get colder, the temperature decreases keeping the entropy constant as shown by the line between 3 and 4 of the T-S diagram.",
                stageName = "Stage 4: Conversion of Energy",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            },
            // Step 12 — HOVER PANELS: HP Mid Blades [0], HP Compressor [1], Rear HP Blades [2]
            new ShowWorkingStep
            {
                stepName = "HP Compressor",
                instruction = "The high pressure turbine is connected to the high pressure shaft and powers the high pressure compressor.",
                stageName = "Stage 4: Conversion of Energy",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                highlightParts = new GameObject[3],  // drag HP Mid Blades [0], HP Compressor [1], Rear HP Blades [2]
                airflowProgress = -1f
            },
            // Step 13 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "LP Turbine",
                instruction = "The low pressure turbine is connected to the low pressure shaft powers the low pressure compressor and the intake fan.",
                stageName = "Stage 4: Conversion of Energy",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            },

            // ── Stage 5: Exhaust ──────────────────────────────────────────────
            // Step 14 — EXISTING AIRFLOW → 100%
            new ShowWorkingStep
            {
                stepName = "Exhaust Nozzle",
                instruction = "The hot gases exhaust through the nozzle at the back producing additional thrust that propels the aircraft forward.",
                stageName = "Stage 5: Exhaust",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = 1.00f
            },
            // Step 15 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "Full Exhaust Flow",
                instruction = "This results in a decrease in the entropy and the temperature of the system as shown by the line between 4 and 1 of the T-S diagram. On the other hand, the volume of the air decreases keeping the pressure constant as shown by the line between 4 and 1 of the P-V diagram.",
                stageName = "Stage 5: Exhaust",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            },

            // ── Conclusion ────────────────────────────────────────────────────
            // Step 16 — no visuals, no airflow change
            new ShowWorkingStep
            {
                stepName = "To Conclude",
                instruction = "The fan pulls a stream of air through the engine.\nCompressor dramatically increases the pressure of the air and its temperature.\nThe combustion chamber dramatically increases the temperature of the air-fuel mixture by releasing heat energy from the fuel.\nThe gases drive the turbine, which converts the motion of the gases into rotational energy.\nThe exhaust nozzle dramatically increases the velocity of the exhaust gases.",
                stageName = "To Conclude",
                stepType = InteractiveStepType.PartTap,
                skipLift = true,
                airflowProgress = -1f
            }
        };

        // ── Build final array: 3 preserved + 14 new = 17 steps ────────────────
        var result = new ShowWorkingStep[preserved.Length + stageSteps.Length];
        for (int i = 0; i < preserved.Length; i++)
            result[i] = preserved[i];
        for (int i = 0; i < stageSteps.Length; i++)
            result[preserved.Length + i] = stageSteps[i];

        Debug.Log($"[StageAutoPopulate] Educational stages populated: {result.Length} total steps " +
                  $"(preserved {preserved.Length} user steps + {stageSteps.Length} educational stages).\n" +
                  "Assign in Inspector:\n" +
                  "  Step 3 activateOnStepStart[0] → Intake_Airflow_Visual\n" +
                  "  Step 4 activateOnStepStart[0] → Intake_Airflow_Visual\n" +
                  "  Step 5 activateOnStepStart[0] → Bypass_Air_Visual\n" +
                  "  Step 6 highlightParts[0] → Compressor Blades\n" +
                  "  Step 8 highlightParts[0] → Fuel Cap, [1] → Fuel Injector\n" +
                  "  Step 12 highlightParts[0] → HP Mid Blades, [1] → HP Compressor, [2] → Rear HP Blades");

        return result;
    }
}