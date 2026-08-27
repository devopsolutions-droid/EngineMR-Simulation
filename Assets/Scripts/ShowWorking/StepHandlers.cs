using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// ── GrabRemove Handler ─────────────────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════════

public class GrabRemoveHandler : IStepHandler
{
    public void OnStepEnter(ShowWorkingStep step, StepContext ctx)
    {
        if (step.targetPart == null)
        {
            Debug.LogError($"[GrabRemoveHandler] Step has no targetPart assigned! Skipping.");
            ctx.completeAndAdvance?.Invoke();
            return;
        }

        ctx.currentTargetPart = step.targetPart;
        ctx.targetOriginalPosition = step.targetPart.transform.position;
        ctx.targetOriginalParent = step.targetPart.transform.parent;

        if (!ctx.originalPositions.ContainsKey(step.targetPart))
        {
            ctx.originalPositions[step.targetPart] = (
                step.targetPart.transform.position,
                step.targetPart.transform.parent
            );
        }

        ctx.currentEnginePart = step.targetPart.GetComponent<EnginePart>();
        ctx.currentGrabController = step.targetPart.GetComponent<EnginePartGrabController>();

        // Update airflow via JetEngineShowWorking part-name mapping
        if (ctx.showWorking != null && !step.skipLift &&
            ctx.currentTargetPart != null && !string.IsNullOrEmpty(ctx.currentTargetPart.name))
        {
            ctx.showWorking.OnPartShown(ctx.currentTargetPart.name);
        }

        ctx.playStepAudio?.Invoke(step);

        // Highlight the target part
        if (ctx.currentEnginePart != null)
        {
            ctx.currentEnginePart.SetVisible(true);
            ctx.currentEnginePart.SetShowWorkingActive();
            ctx.partExplorer?.SetAllOtherPartsBackground(ctx.currentEnginePart);

            if (!step.skipLift)
                ctx.currentEnginePart.LiftUp(ctx.liftAmount, ctx.liftDuration);
        }
    }

    public void OnNextPressed(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.currentTargetPart != null)
            ctx.currentTargetPart.SetActive(false);

        ctx.completeAndAdvance?.Invoke();
    }

    public void OnStepExit(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.currentEnginePart != null && !step.skipLift)
            ctx.currentEnginePart.LowerDown(ctx.liftDuration * 0.5f);

        if (ctx.currentTargetPart != null && !ctx.currentTargetPart.activeSelf)
            ctx.currentTargetPart.SetActive(true);

        RestorePartToOriginal(step.targetPart, ctx);
    }

    public void Cleanup(ShowWorkingStep step, StepContext ctx)
    {
        // Nothing specific beyond what the controller handles
    }

    private static void RestorePartToOriginal(GameObject part, StepContext ctx)
    {
        if (part == null) return;

        if (ctx.originalPositions.TryGetValue(part, out var saved))
        {
            var enginePart = part.GetComponent<EnginePart>();
            if (enginePart != null)
            {
                enginePart.RestoreOriginal();
                enginePart.LowerDown(ctx.liftDuration * 0.5f);
            }
            part.transform.SetParent(saved.parent);
            part.transform.position = saved.pos;
            part.SetActive(true);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ── TurbineStart Handler ────────────────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════════

public class TurbineStartHandler : IStepHandler
{
    public void OnStepEnter(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.startTurbineButton != null)
            ctx.startTurbineButton.SetActive(true);

        ctx.playStepAudio?.Invoke(step);
    }

    public void OnNextPressed(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.startTurbineButton != null)
            ctx.startTurbineButton.SetActive(false);

        ctx.playStepAudio?.Invoke(step);

        // Find and start TurbineBladeRotator
        if (ctx.turbineBladeRotator == null)
            ctx.turbineBladeRotator = Object.FindFirstObjectByType<TurbineBladeRotator>();

        if (ctx.turbineBladeRotator != null)
        {
            ctx.turbineBladeRotator.rotationAxis = step.bladeRotationAxis;

            if (step.turbineStartAudio != null && ctx.turbineBladeRotator.startAudio == null)
                ctx.turbineBladeRotator.startAudio = step.turbineStartAudio;

            ctx.turbineBladeRotator.StartRotation();
            ctx.turbineWasStarted = true;
        }
        else
        {
            Debug.LogWarning("[TurbineStartHandler] No TurbineBladeRotator found!");
        }

        // Play turbine start audio
        if (step.turbineStartAudio != null && ctx.audioSource != null)
        {
            if (ctx.audioSource.isPlaying) ctx.audioSource.Stop();
            ctx.audioSource.clip = step.turbineStartAudio;
            ctx.audioSource.Play();
        }

        // Advance airflow
        if (ctx.showWorking != null)
            ctx.showWorking.AdvanceAirflowTo(step.turbineStartAirflowProgress);

        ctx.completeAndAdvance?.Invoke();
    }

    public void OnStepExit(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.turbineBladeRotator != null && ctx.turbineBladeRotator.IsRotating)
            ctx.turbineBladeRotator.StopRotation();

        ctx.turbineWasStarted = false;

        if (ctx.startTurbineButton != null)
            ctx.startTurbineButton.SetActive(false);
    }

    public void Cleanup(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.turbineBladeRotator != null && ctx.turbineBladeRotator.IsRotating)
            ctx.turbineBladeRotator.StopRotation();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ── PartTap Handler ────────────────────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════════

public class PartTapHandler : IStepHandler
{
    public void OnStepEnter(ShowWorkingStep step, StepContext ctx)
    {
        ctx.tapTargetPart = step.targetPart;

        if (ctx.tapTargetPart != null)
        {
            var enginePart = ctx.tapTargetPart.GetComponent<EnginePart>();
            if (enginePart != null)
            {
                enginePart.SetVisible(true);
                enginePart.SetShowWorkingActive();
                ctx.partExplorer?.SetAllOtherPartsBackground(enginePart);

                if (!step.skipLift)
                    enginePart.LiftUp(ctx.liftAmount * 0.5f, ctx.liftDuration);
            }
        }

        ctx.playStepAudio?.Invoke(step);
    }

    public void OnNextPressed(ShowWorkingStep step, StepContext ctx)
    {
        PlayStepVFX(step, ctx, () =>
        {
            if (ctx.showWorking != null && step.airflowProgress >= 0f)
                ctx.showWorking.SetAirflowProgressDirect(step.airflowProgress, cumulative: true);

            var enginePart = ctx.tapTargetPart?.GetComponent<EnginePart>();
            if (enginePart != null)
                enginePart.LowerDown(ctx.liftDuration * 0.5f);

            ctx.completeAndAdvance?.Invoke();
        });
    }

    public void OnStepExit(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.tapTargetPart != null)
        {
            var ep = ctx.tapTargetPart.GetComponent<EnginePart>();
            if (ep != null)
                ep.LowerDown(ctx.liftDuration * 0.5f);
        }

        StopCurrentStepVFX(step);
    }

    public void Cleanup(ShowWorkingStep step, StepContext ctx)
    {
        StopCurrentStepVFX(step);
    }

    private static void PlayStepVFX(ShowWorkingStep step, StepContext ctx, System.Action onComplete)
    {
        if (step.airCompressionController != null)
        {
            step.airCompressionController.StartCompression(onComplete);
        }
        else if (step.fuelSprayController != null)
        {
            step.fuelSprayController.StartSpray(onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    private static void StopCurrentStepVFX(ShowWorkingStep step)
    {
        if (step.airCompressionController != null)
            step.airCompressionController.ResetCompression();

        if (step.fuelSprayController != null)
            step.fuelSprayController.StopSpray();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ── IgniteButton Handler ───────────────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════════

public class IgniteButtonHandler : IStepHandler
{
    public void OnStepEnter(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.igniteButton != null)
            ctx.igniteButton.SetActive(true);

        ctx.playStepAudio?.Invoke(step);
    }

    public void OnNextPressed(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.igniteButton != null)
            ctx.igniteButton.SetActive(false);

        if (step.triggerSlowMotion && step.slowMotionController != null)
            step.slowMotionController.TriggerSlowMotion(null);

        ctx.playStepAudio?.Invoke(step);

        PlayCombustionVFX(step, ctx, () =>
        {
            float targetProgress = step.airflowProgress >= 0f ? step.airflowProgress : 1f;
            if (ctx.showWorking != null)
                ctx.showWorking.SetAirflowProgressDirect(targetProgress, cumulative: true);

            if (step.triggerSlowMotion && step.slowMotionController != null)
                step.slowMotionController.ResumeNormalTime();

            ctx.completeAndAdvance?.Invoke();
        });
    }

    public void OnStepExit(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.igniteButton != null)
            ctx.igniteButton.SetActive(false);

        if (step.triggerSlowMotion && step.slowMotionController != null)
            step.slowMotionController.ResumeNormalTime();

        StopCombustionVFX(step);
    }

    public void Cleanup(ShowWorkingStep step, StepContext ctx)
    {
        if (step.triggerSlowMotion && step.slowMotionController != null)
            step.slowMotionController.ResumeNormalTime();

        StopCombustionVFX(step);
    }

    private static void PlayCombustionVFX(ShowWorkingStep step, StepContext ctx, System.Action onComplete)
    {
        if (step.combustionController != null)
            step.combustionController.StartCombustion(onComplete);
        else
            onComplete?.Invoke();
    }

    private static void StopCombustionVFX(ShowWorkingStep step)
    {
        if (step.combustionController != null)
            step.combustionController.StopCombustion();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ── BladeSpin Handler ─────────────────────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════════

public class BladeSpinHandler : IStepHandler
{
    public void OnStepEnter(ShowWorkingStep step, StepContext ctx)
    {
        if (step.bladeTargets != null && step.bladeTargets.Length > 0)
            Debug.Log($"[BladeSpinHandler] Step ready: {step.bladeTargets.Length} target blades.");

        ctx.playStepAudio?.Invoke(step);
    }

    public void OnNextPressed(ShowWorkingStep step, StepContext ctx)
    {
        ctx.playStepAudio?.Invoke(step);

        // Create runtime parent and start rotation
        if (step.bladeTargets != null && step.bladeTargets.Length > 0)
        {
            var validBlades = new List<GameObject>(step.bladeTargets.Length);
            var savedParents = new List<(GameObject blade, Transform originalParent)>(step.bladeTargets.Length);

            foreach (var blade in step.bladeTargets)
            {
                if (blade != null)
                {
                    validBlades.Add(blade);
                    savedParents.Add((blade, blade.transform.parent));
                }
            }

            if (validBlades.Count > 0)
            {
                GameObject tempParent = new GameObject($"BladeGroup_Runtime_{ctx.activeBladeGroups.Count}");
                tempParent.transform.position = validBlades[0].transform.position;
                tempParent.transform.rotation = validBlades[0].transform.rotation;

                foreach (var blade in validBlades)
                    blade.transform.SetParent(tempParent.transform, worldPositionStays: true);

                var rotator = tempParent.AddComponent<TurbineBladeRotator>();
                rotator.rotationAxis = step.bladeRotationAxis;
                if (step.turbineStartAudio != null && rotator.startAudio == null)
                    rotator.startAudio = step.turbineStartAudio;

                rotator.StartRotation();

                var group = new RuntimeBladeGroup(tempParent, rotator, savedParents);
                ctx.activeBladeGroups.Add(group);
            }
        }

        // Advance airflow
        if (ctx.showWorking != null && step.airflowProgress >= 0f)
            ctx.showWorking.SetAirflowProgressDirect(step.airflowProgress, cumulative: true);

        ctx.completeAndAdvance?.Invoke();
    }

    public void OnStepExit(ShowWorkingStep step, StepContext ctx)
    {
        if (ctx.activeBladeGroups.Count == 0) return;

        var group = ctx.activeBladeGroups[ctx.activeBladeGroups.Count - 1];

        if (group.rotator != null && group.rotator.IsRotating)
            group.rotator.StopRotation();

        foreach (var (blade, originalParent) in group.originalParents)
        {
            if (blade != null)
                blade.transform.SetParent(originalParent, worldPositionStays: true);
        }

        if (group.tempParent != null)
            GameObject.Destroy(group.tempParent);

        ctx.activeBladeGroups.RemoveAt(ctx.activeBladeGroups.Count - 1);
    }

    public void Cleanup(ShowWorkingStep step, StepContext ctx)
    {
        foreach (var group in ctx.activeBladeGroups)
        {
            if (group.rotator != null && group.rotator.IsRotating)
                group.rotator.StopRotation();

            foreach (var (blade, originalParent) in group.originalParents)
            {
                if (blade != null)
                    blade.transform.SetParent(originalParent, worldPositionStays: true);
            }

            if (group.tempParent != null)
                GameObject.Destroy(group.tempParent);
        }

        ctx.activeBladeGroups.Clear();
    }
}