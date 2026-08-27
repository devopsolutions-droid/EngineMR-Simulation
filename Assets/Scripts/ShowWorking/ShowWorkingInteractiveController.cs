// This script orchestrates the interactive Show Working tutorial using the Strategy Pattern.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// ShowWorkingInteractiveController — Rebuilt, Optimized Architecture
/// ═══════════════════════════════════════════════════════════════════════════════
/// ORCHESTRATOR. No per-step-type logic lives here — all delegated to IStepHandler
/// implementations via strategy pattern. Educational stages are a horizontal concern
/// handled before/after handler invocation.
///
/// Keeps all existing field names + public API surface for Inspector + TabletUIController
/// backward compatibility.
/// </summary>
public class ShowWorkingInteractiveController : MonoBehaviour, IShowWorkingController
{
    // ═══════════════════════════════════════════════════════════════════════════
    // ── Serialized Fields (IDENTICAL names for Inspector compatibility) ────────
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Step Configuration")]
    public ShowWorkingStep[] steps;

    [Header("Show Working Object Hiding")]
    [Tooltip("Drag and drop GameObjects here that you want to hide when Show Working mode starts, and restore when it stops.")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    private Dictionary<GameObject, bool> _hiddenObjectsOriginalState = new Dictionary<GameObject, bool>();

    [Header("Model Validation")]
    [Tooltip("Assign an EngineData asset here to restrict this script to a specific engine model. Leave empty for no restriction.")]
    [FormerlySerializedAs("jetEngineData")]
    public EngineData targetEngineData;

    [Header("References")]
    public SimplePartExplorer partExplorer;
    public JetEngineShowWorking showWorking;
    public EngineGrabManager grabManager;
    public AudioSource audioSource;

    [Header("Settings")]
    public float defaultAdvanceDistance = 0.3f;
    public float liftDuration = 0.35f;
    public float liftAmount = 0.4f;
    public float advanceDelay = 0.5f;
    public float highlightScale = 0f;

    [Header("Legacy Transition")]
    public bool transitionToLegacyExplorer = false;
    public float transitionDelay = 0.5f;

    [Header("Audio Clips")]
    public AudioClip completionAudio;

    [Header("Turbine Start")]
    public GameObject startTurbineButton;

    [Header("Ignite Button")]
    public GameObject igniteButton;

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Events ────────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    public Action OnAllStepsCompleted;
    public Action<int> OnStepCompleted;

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Runtime State ─────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private int _currentStepIndex = -1;
    private bool _isRunning = false;
    private bool _flowCompleted = false;
    private bool _stepAdvancing = false;

    // Educational stage tracking
    private string _currentStageName = "";

    // Step Context (shared with handlers)
    private StepContext _ctx;
    private bool _ctxReady = false;

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Handler Registry (strategy pattern — no switch statements) ────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private readonly Dictionary<InteractiveStepType, IStepHandler> _handlers
        = new Dictionary<InteractiveStepType, IStepHandler>();

    private void BuildHandlerRegistry()
    {
        _handlers[InteractiveStepType.GrabRemove]   = new GrabRemoveHandler();
        _handlers[InteractiveStepType.TurbineStart]  = new TurbineStartHandler();
        _handlers[InteractiveStepType.PartTap]       = new PartTapHandler();
        _handlers[InteractiveStepType.IgniteButton]  = new IgniteButtonHandler();
        _handlers[InteractiveStepType.BladeSpin]     = new BladeSpinHandler();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Public API ────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    public int CurrentStepIndex => _currentStepIndex;
    public int TotalSteps       => steps?.Length ?? 0;
    public bool IsRunning       => _isRunning;
    public bool HasCompleted    => _flowCompleted;

    public void StartInteractiveFlow()
    {
        if (!IsActiveEngineValid()) return;
        if (_isRunning) return;
        if (steps == null || steps.Length == 0)
        {
            Debug.LogError("[SWIC] No steps defined!");
            return;
        }

        EnsureContext();
        EnsureHandlers();

        _isRunning = true;
        _flowCompleted = false;
        _currentStepIndex = -1;
        _currentStageName = "";

        // Hide specified GameObjects
        _hiddenObjectsOriginalState.Clear();
        if (objectsToHide != null)
        {
            foreach (var go in objectsToHide)
            {
                if (go != null)
                {
                    _hiddenObjectsOriginalState[go] = go.activeSelf;
                    go.SetActive(false);
                }
            }
        }

        // Prevent JetEngineShowWorking from auto-hiding/sliding covers
        if (showWorking != null)
        {
            showWorking.skipAutoCoverRemoval = true;
            showWorking.OnShowWorkingStart();
        }

        // Enable grabbing + subscribe to events
        if (grabManager != null)
        {
            grabManager.allowGrabbing = true;
            grabManager.OnGrabStarted += OnPartGrabbed;
            grabManager.OnGrabEnded   += OnPartReleased;
        }

        AdvanceToNextStep();
    }

    public void StopInteractiveFlow()
    {
        if (!IsActiveEngineValid()) return;
        if (!_isRunning) return;

        _isRunning = false;
        _flowCompleted = true;

        // Restore specified GameObjects
        if (objectsToHide != null)
        {
            foreach (var go in objectsToHide)
            {
                if (go != null)
                {
                    if (_hiddenObjectsOriginalState.TryGetValue(go, out bool originalState))
                    {
                        go.SetActive(originalState);
                    }
                    else
                    {
                        go.SetActive(true);
                    }
                }
            }
        }
        _hiddenObjectsOriginalState.Clear();

        HideSpecialButtons();

        // Cleanup all handlers
        if (_ctxReady && steps != null)
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (_handlers.TryGetValue(step.stepType, out var h))
                    h.Cleanup(step, _ctx);
            }
        }

        // Unsubscribe grab events
        if (grabManager != null)
        {
            grabManager.allowGrabbing = false;
            grabManager.OnGrabStarted -= OnPartGrabbed;
            grabManager.OnGrabEnded   -= OnPartReleased;
        }

        // Restore all parts
        RestoreAllParts();

        // Restore JetEngineShowWorking state
        if (showWorking != null)
        {
            showWorking.skipAutoCoverRemoval = false;
            showWorking.OnShowWorkingStop();
        }

        // Stop all VFX (via stored step reference)
        StopCurrentStepVFX();

        Time.timeScale = 1f;

        // Deactivate all stage visuals
        if (steps != null)
        {
            foreach (var s in steps)
            {
                if (s == null) continue;
                DeactivateAllVisualsForStep(s);
                HideAllHighlightPartsForStep(s);
            }
        }

        // Clear all grabbableInShowWorking flags so no parts remain grabbable
        ClearAllGrabbableFlags();

        ClearRuntimeState();
    }

    public void OnNextPressed()
    {
        if (!IsActiveEngineValid()) return;
        if (!_isRunning || _stepAdvancing) return;
        if (!IndexInRange()) return;

        var step = steps[_currentStepIndex];
        if (step == null) return;

        Debug.Log($"[SWIC] Next pressed on step {_currentStepIndex + 1}: \"{step.stepName}\" ({step.stepType})");

        // ═══ GUARD: Prevent double-press while VFX is playing ═══
        // Handlers like PartTap and IgniteButton play VFX coroutines that take
        // seconds to complete. Without this guard, a second press kills the
        // first coroutine via StopCoroutine, the completion callback never
        // fires, and the step is stuck forever.
        _stepAdvancing = true;

        if (_handlers.TryGetValue(step.stepType, out var handler))
        {
            handler.OnNextPressed(step, _ctx);
        }
        else
        {
            Debug.LogError($"[SWIC] No handler for step type {step.stepType}");
            _stepAdvancing = false; // Reset guard if no handler exists
        }
    }

    public void OnPreviousPressed()
    {
        if (!IsActiveEngineValid()) return;
        if (!_isRunning || _stepAdvancing || _currentStepIndex < 0) return;

        Debug.Log($"[SWIC] Previous pressed on step {_currentStepIndex + 1}");

        var step = steps[_currentStepIndex];
        if (step == null) return;

        // ── 1. Stop VFX ──────────────────────────────────────────────────────
        StopCurrentStepVFX();

        // ── 2. Delegate to handler exit ──────────────────────────────────────
        if (_handlers.TryGetValue(step.stepType, out var handler))
            handler.OnStepExit(step, _ctx);

        // ── 3. Deactivate visuals + hide panels for this step ────────────────
        DeactivateAllVisualsForStep(step);
        HideAllHighlightPartsForStep(step);
        HideSpecialButtons();

        // ── 4. Resume normal time if slow-motion ─────────────────────────────
        if (step.triggerSlowMotion && step.slowMotionController != null)
            step.slowMotionController.ResumeNormalTime();
        Time.timeScale = 1f;

        // ── 5. Roll back airflow ─────────────────────────────────────────────
        if (showWorking != null)
            showWorking.SetAirflowProgressDirect(GetHighestAirflowBeforeCurrent(), cumulative: false);

        // ── 6. Decrement index ───────────────────────────────────────────────
        _currentStepIndex--;
        _stepAdvancing = false;

        // ── 7. Re-show previous step ─────────────────────────────────────────
        if (IndexInRange())
        {
            var prevStep = steps[_currentStepIndex];
            ShowNavigationUI(prevStep);
            PlayStepAudio(prevStep);

            // Resolve stage name going backwards
            _currentStageName = "";
            for (int i = _currentStepIndex; i >= 0; i--)
            {
                var s = steps[i];
                if (s != null && !string.IsNullOrEmpty(s.stageName))
                {
                    _currentStageName = s.stageName;
                    break;
                }
            }

            // Re-activate visuals + panels
            ActivateVisualsForStep(prevStep);
            ShowHighlightPanelsForStep(prevStep);

            // Re-enter the previous step's handler
            if (_handlers.TryGetValue(prevStep.stepType, out var prevHandler))
                prevHandler.OnStepEnter(prevStep, _ctx);
        }
        else
        {
            if (partExplorer != null)
            {
                partExplorer.SetInteractiveUIText("Show Working", "Press Next to begin", 0, steps.Length);
                partExplorer.HideNavigationButtons();
            }
        }

        Debug.Log($"[SWIC] Previous pressed — returned to step {_currentStepIndex + 1}/{steps.Length}");
    }

    // ── Legacy Button Support ─────────────────────────────────────────────────

    public void OnTurbineStarted()
    {
        if (!_isRunning || _stepAdvancing) return;
        var step = GetCurrentStep(InteractiveStepType.TurbineStart);
        if (step != null) OnNextPressed();
    }

    public void OnIgniteButtonPressed()
    {
        if (!_isRunning || _stepAdvancing) return;
        var step = GetCurrentStep(InteractiveStepType.IgniteButton);
        if (step != null) OnNextPressed();
    }

    public void OnSpecialButtonPressed()
    {
        if (!_isRunning) return;
        var step = IndexInRange() ? steps[_currentStepIndex] : null;
        if (step == null) return;

        switch (step.stepType)
        {
            case InteractiveStepType.TurbineStart: OnTurbineStarted(); break;
            case InteractiveStepType.IgniteButton: OnIgniteButtonPressed(); break;
            default: Debug.LogWarning($"[SWIC] OnSpecialButtonPressed no handler for {step.stepType}"); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Unity Lifecycle ──────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    void Start()
    {
        // Cache audio source fallback
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && partExplorer != null)
                audioSource = FindFirstObjectByType<AudioSource>();
        }

        // Auto-find references
        if (grabManager == null) grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (partExplorer == null) partExplorer = FindFirstObjectByType<SimplePartExplorer>();
        if (showWorking == null) showWorking = FindFirstObjectByType<JetEngineShowWorking>();

        EnsureContext();
        EnsureHandlers();
    }

    void OnDestroy()
    {
        if (grabManager != null)
        {
            grabManager.OnGrabStarted -= OnPartGrabbed;
            grabManager.OnGrabEnded   -= OnPartReleased;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Step Management ──────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void AdvanceToNextStep()
    {
        if (!_isRunning) return;

        int nextIndex = _currentStepIndex + 1;
        if (nextIndex >= steps.Length)
        {
            CompleteAllSteps();
            return;
        }

        // Clear grabbable flag on the previous step's target part
        ClearPreviousStepGrabbableFlag();

        _currentStepIndex = nextIndex;
        _stepAdvancing = false;

        var step = steps[_currentStepIndex];

        // Hide special buttons if the incoming step doesn't need them
        if (step.stepType != InteractiveStepType.TurbineStart && startTurbineButton != null)
            startTurbineButton.SetActive(false);
        if (step.stepType != InteractiveStepType.IgniteButton && igniteButton != null)
            igniteButton.SetActive(false);

        // Save airflow at step start
        _ctx.airflowAtStepStart = showWorking != null ? showWorking.CurrentAirflowProgress : 0f;

        // Educational stage transition (horizontal concern)
        HandleStageTransition(step);

        // Activate visuals + show panels + update UI
        ActivateVisualsForStep(step);
        ShowHighlightPanelsForStep(step);
        ShowNavigationUI(step);

        // Delegate to handler for per-type setup
        if (_handlers.TryGetValue(step.stepType, out var handler))
            handler.OnStepEnter(step, _ctx);
        else
            Debug.LogError($"[SWIC] Unknown step type {step.stepType}! Skipping.");

        // Mark the new step's target part as grabbable during Show Working
        if (step.stepType == InteractiveStepType.GrabRemove && step.targetPart != null)
        {
            var gc = step.targetPart.GetComponent<EnginePartGrabController>();
            if (gc != null) gc.grabbableInShowWorking = true;
        }
    }

    private void CompleteCurrentStep()
    {
        OnStepCompleted?.Invoke(_currentStepIndex);

        var step = steps[_currentStepIndex];

        if (step != null && step.stepType == InteractiveStepType.GrabRemove)
        {
            if (_ctx.currentEnginePart != null && !step.skipLift)
                _ctx.currentEnginePart.LowerDown(liftDuration * 0.5f);
            StartCoroutine(DelayedAdvance(advanceDelay));
        }
        else
        {
            // _stepAdvancing was set to true in OnNextPressed before calling
            // the handler; ForceAdvanceToNextStep will reset it to false
            ForceAdvanceToNextStep();
        }
    }

    // Advance without the _isRunning guard — used for synchronous PartTap completion
    // where _isRunning could be momentarily inconsistent
    private void ForceAdvanceToNextStep()
    {
        int nextIndex = _currentStepIndex + 1;
        if (nextIndex >= steps.Length)
        {
            _stepAdvancing = false;
            CompleteAllSteps();
            return;
        }

        // Clear grabbable flag on the previous step's target part
        ClearPreviousStepGrabbableFlag();

        _currentStepIndex = nextIndex;
        _stepAdvancing = false;

        var step = steps[_currentStepIndex];

        if (step.stepType != InteractiveStepType.TurbineStart && startTurbineButton != null)
            startTurbineButton.SetActive(false);
        if (step.stepType != InteractiveStepType.IgniteButton && igniteButton != null)
            igniteButton.SetActive(false);

        _ctx.airflowAtStepStart = showWorking != null ? showWorking.CurrentAirflowProgress : 0f;

        HandleStageTransition(step);
        ActivateVisualsForStep(step);
        ShowHighlightPanelsForStep(step);
        ShowNavigationUI(step);

        if (_handlers.TryGetValue(step.stepType, out var handler))
            handler.OnStepEnter(step, _ctx);

        // Mark the new step's target part as grabbable during Show Working
        if (step.stepType == InteractiveStepType.GrabRemove && step.targetPart != null)
        {
            var gc = step.targetPart.GetComponent<EnginePartGrabController>();
            if (gc != null) gc.grabbableInShowWorking = true;
        }

        Debug.Log($"[SWIC] Advanced to step {_currentStepIndex + 1}: \"{step.stepName}\" ({step.stepType})");
    }

    private void CompleteAllSteps()
    {
        Debug.Log("[SWIC] All interactive steps completed.");

        if (audioSource != null && completionAudio != null)
        {
            audioSource.clip = completionAudio;
            audioSource.Play();
        }

        if (transitionToLegacyExplorer)
        {
            StartCoroutine(DelayedTransition(transitionDelay));
            return;
        }

        StopInteractiveFlow();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Grab Event Handlers ──────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnPartGrabbed(EnginePartGrabController grabController)
    {
        if (!_isRunning || _stepAdvancing) return;
        var step = GetCurrentStep(InteractiveStepType.GrabRemove);
        if (step == null) return;

        if (grabController != null && grabController.gameObject == _ctx.currentTargetPart)
        {
            _ctx.correctPartGrabbed = true;
        }
    }

    private void OnPartReleased(EnginePartGrabController grabController)
    {
        if (!_isRunning || _stepAdvancing) return;
        var step = GetCurrentStep(InteractiveStepType.GrabRemove);
        if (step == null) return;

        if (_ctx.correctPartGrabbed && grabController != null &&
            grabController.gameObject == _ctx.currentTargetPart)
        {
            float distance = Vector3.Distance(
                _ctx.currentTargetPart.transform.position,
                _ctx.targetOriginalPosition
            );

            float threshold = step.advanceDistance > 0f ? step.advanceDistance : defaultAdvanceDistance;

            if (distance >= threshold)
            {
                Debug.Log($"[SWIC] Part moved {distance:F2}m — completing step {_currentStepIndex + 1}.");
                CompleteCurrentStep();
            }
            else
            {
                _ctx.correctPartGrabbed = false;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Educational Stage Helpers (horizontal concern) ────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleStageTransition(ShowWorkingStep step)
    {
        if (step == null) return;

        if (!string.IsNullOrEmpty(step.stageName) && step.stageName != _currentStageName)
        {
            _currentStageName = step.stageName;
            Debug.Log($"[SWIC] Stage transition: \"{_currentStageName}\" at step {_currentStepIndex + 1}");
        }
    }

    private void ActivateVisualsForStep(ShowWorkingStep step)
    {
        if (step?.activateOnStepStart == null) return;
        foreach (var go in step.activateOnStepStart)
        {
            if (go != null && !go.activeSelf)
            {
                go.SetActive(true);
                Debug.Log($"[SWIC] Activated visual '{go.name}' for step {_currentStepIndex + 1}");
            }
        }
    }

    private void ShowHighlightPanelsForStep(ShowWorkingStep step)
    {
        if (step?.highlightParts != null)
        {
            foreach (var part in step.highlightParts)
            {
                if (part == null) continue;
                var panel = part.GetComponentInChildren<PartHoverPanel>(true);
                if (panel != null) panel.Show();
            }
        }

        if (step?.hoverPanels != null)
        {
            foreach (var go in step.hoverPanels)
            {
                if (go == null) continue;
                var panel = go.GetComponent<PartHoverPanel>();
                if (panel != null) panel.Show();
                else go.SetActive(true);
            }
        }
    }

    private void DeactivateAllVisualsForStep(ShowWorkingStep step)
    {
        if (step?.activateOnStepStart == null) return;
        foreach (var go in step.activateOnStepStart)
        {
            if (go != null && go.activeSelf)
                go.SetActive(false);
        }
    }

    private void HideAllHighlightPartsForStep(ShowWorkingStep step)
    {
        if (step?.highlightParts != null)
        {
            foreach (var part in step.highlightParts)
            {
                if (part == null) continue;
                var panel = part.GetComponentInChildren<PartHoverPanel>(true);
                if (panel != null) panel.Hide();
            }
        }

        if (step?.hoverPanels != null)
        {
            foreach (var go in step.hoverPanels)
            {
                if (go == null) continue;
                var panel = go.GetComponent<PartHoverPanel>();
                if (panel != null) panel.Hide();
                else go.SetActive(false);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── UI Helpers ───────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void ShowNavigationUI(ShowWorkingStep step)
    {
        if (partExplorer == null) return;

        string displayName = step.stepName;
        if (!string.IsNullOrEmpty(_currentStageName))
            displayName = $"{_currentStageName}\n{step.stepName}";

        partExplorer.SetInteractiveUIText(displayName, step.instruction, _currentStepIndex + 1, steps.Length);
        partExplorer.ShowNavigationButtons();
    }

    private void PlayStepAudio(ShowWorkingStep step)
    {
        AudioSource src = audioSource ?? FindFirstObjectByType<AudioSource>();
        if (src == null) return;

        AudioClip clip = step.stepAudio;
        if (clip == null && _ctx.currentEnginePart != null)
            clip = _ctx.currentEnginePart.AudioClip;

        if (clip != null)
        {
            if (src.isPlaying) src.Stop();
            src.clip = clip;
            src.Play();
        }
    }

    private void HideSpecialButtons()
    {
        if (startTurbineButton != null) startTurbineButton.SetActive(false);
        if (igniteButton != null) igniteButton.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── VFX ──────────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private void StopCurrentStepVFX()
    {
        if (!IndexInRange()) return;
        var step = steps[_currentStepIndex];
        if (step == null) return;

        if (step.airCompressionController != null) step.airCompressionController.ResetCompression();
        if (step.fuelSprayController != null) step.fuelSprayController.StopSpray();
        if (step.combustionController != null) step.combustionController.StopCombustion();
        if (step.slowMotionController != null) step.slowMotionController.ResumeNormalTime();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Helpers ──────────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private bool IndexInRange() => _currentStepIndex >= 0 && _currentStepIndex < (steps?.Length ?? 0);

    private ShowWorkingStep GetCurrentStep(InteractiveStepType expectedType)
    {
        if (!IndexInRange()) return null;
        var step = steps[_currentStepIndex];
        return step != null && step.stepType == expectedType ? step : null;
    }

    private float GetHighestAirflowBeforeCurrent()
    {
        float highest = 0f;
        for (int i = 0; i < _currentStepIndex; i++)
        {
            var s = steps[i];
            if (s == null) continue;
            float af = GetAirflowForStep(s);
            if (af > highest) highest = af;
        }
        return highest;
    }

    private float GetAirflowForStep(ShowWorkingStep step)
    {
        if (step == null) return 0f;

        switch (step.stepType)
        {
            case InteractiveStepType.GrabRemove:
                if (step.targetPart != null && showWorking != null)
                    return Mathf.Max(0f, showWorking.GetProgressForPart(step.targetPart.name));
                return 0f;

            case InteractiveStepType.TurbineStart:
                return step.turbineStartAirflowProgress;

            case InteractiveStepType.PartTap:
            case InteractiveStepType.BladeSpin:
                return Mathf.Max(0f, step.airflowProgress);

            case InteractiveStepType.IgniteButton:
                return Mathf.Max(0f, step.airflowProgress >= 0f ? step.airflowProgress : 1f);

            default:
                return 0f;
        }
    }

    private void RestoreAllParts()
    {
        foreach (var kvp in _ctx?.originalPositions ?? new Dictionary<GameObject, (Vector3, Transform)>())
        {
            GameObject go = kvp.Key;
            if (go == null) continue;

            var enginePart = go.GetComponent<EnginePart>();
            if (enginePart != null)
            {
                enginePart.RestoreOriginal();
                enginePart.LowerDown(liftDuration * 0.5f);
            }

            go.transform.SetParent(kvp.Value.parent);
            go.transform.position = kvp.Value.pos;
            go.SetActive(true);
        }

        if (_ctx != null) _ctx.originalPositions.Clear();
    }

    // ── Grabbable-in-Show-Working helpers ─────────────────────────────────────

    /// <summary>
    /// Clears the grabbableInShowWorking flag on the previous step's target part
    /// so it can no longer be grabbed once we've moved on.
    /// </summary>
    private void ClearPreviousStepGrabbableFlag()
    {
        if (_currentStepIndex < 0 || _currentStepIndex >= steps.Length) return;
        var prevStep = steps[_currentStepIndex];
        if (prevStep == null || prevStep.stepType != InteractiveStepType.GrabRemove) return;
        if (prevStep.targetPart == null) return;
        var gc = prevStep.targetPart.GetComponent<EnginePartGrabController>();
        if (gc != null) gc.grabbableInShowWorking = false;
    }

    /// <summary>
    /// Clears the grabbableInShowWorking flag on ALL steps' target parts.
    /// Called on StopInteractiveFlow and TransitionToLegacyExplorer.
    /// </summary>
    private void ClearAllGrabbableFlags()
    {
        if (steps == null) return;
        foreach (var step in steps)
        {
            if (step == null || step.targetPart == null) continue;
            var gc = step.targetPart.GetComponent<EnginePartGrabController>();
            if (gc != null) gc.grabbableInShowWorking = false;
        }
    }

    private void ClearRuntimeState()
    {
        _ctx.currentTargetPart = null;
        _ctx.currentEnginePart = null;
        _ctx.currentGrabController = null;
        _ctx.tapTargetPart = null;
        _ctx.correctPartGrabbed = false;
        _stepAdvancing = false;
        _currentStepIndex = -1;
        _currentStageName = "";
    }

    private void EnsureContext()
    {
        if (_ctx != null) return;

        _ctx = new StepContext
        {
            partExplorer            = partExplorer,
            showWorking             = showWorking,
            grabManager             = grabManager,
            audioSource             = audioSource,
            defaultAdvanceDistance  = defaultAdvanceDistance,
            liftDuration            = liftDuration,
            liftAmount              = liftAmount,
            advanceDelay            = advanceDelay,
            highlightScale          = highlightScale,
            startTurbineButton      = startTurbineButton,
            igniteButton            = igniteButton,
            playStepAudio           = PlayStepAudio,
            completeAndAdvance      = CompleteCurrentStep,
            showNavigationUI        = ShowNavigationUI
        };

        _ctxReady = true;
    }

    private void EnsureHandlers()
    {
        if (_handlers.Count == 0) BuildHandlerRegistry();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── Coroutines ───────────────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator DelayedAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        AdvanceToNextStep();
    }

    private IEnumerator DelayedTransition(float delay)
    {
        yield return new WaitForSeconds(delay);
        TransitionToLegacyExplorer();
    }

    private void TransitionToLegacyExplorer()
    {
        List<GameObject> removedParts = new List<GameObject>();
        if (steps != null)
        {
            foreach (var step in steps)
            {
                if (step != null && step.stepType == InteractiveStepType.GrabRemove && step.targetPart != null)
                    removedParts.Add(step.targetPart);
            }
        }

        // Clear all grabbableInShowWorking flags before transitioning
        ClearAllGrabbableFlags();

        if (grabManager != null)
        {
            grabManager.allowGrabbing = false;
            grabManager.OnGrabStarted -= OnPartGrabbed;
            grabManager.OnGrabEnded   -= OnPartReleased;
        }

        _isRunning = false;
        ClearRuntimeState();

        if (partExplorer != null)
        {
            partExplorer.LoadAndSkipParts(removedParts.ToArray());
            partExplorer.ResumeExplorerAt(0);
            partExplorer.ShowNavigationButtons();
        }

        OnAllStepsCompleted?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ── ContextMenu Helpers ──────────────────────────────────────────────────
    // ═══════════════════════════════════════════════════════════════════════════

    [ContextMenu("Log Step Configuration")]
    private void LogStepConfig()
    {
        if (steps == null) { Debug.Log("No steps configured."); return; }
        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            Debug.Log($"Step {i + 1}: \"{s.stepName}\" → type={s.stepType}, " +
                      $"target={s.targetPart?.name ?? "null"}, " +
                      $"advanceDist={s.advanceDistance}, airflow={s.airflowProgress}");
        }
    }

    [ContextMenu("Populate Educational Stage Steps")]
    private void PopulateEducationalStageSteps()
    {
        steps = StageAutoPopulate.Populate(steps);
    }

    private bool IsActiveEngineValid()
    {
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader == null) return true;

        EngineData activeData = loader.ActiveEngineData != null ? loader.ActiveEngineData : loader.fallbackEngine;
        if (activeData == null) return true;

        // If a specific engine data constraint is configured, only run if it matches
        if (targetEngineData != null)
        {
            return activeData == targetEngineData;
        }

        // If attached directly to a prefab root of the active loaded engine, it is valid
        if (loader.ActiveEngineRoot != null && (loader.ActiveEngineRoot == gameObject || transform.IsChildOf(loader.ActiveEngineRoot.transform)))
        {
            return true;
        }

        // Fallback for standalone scene-level objects (backward-compatible check for Jet Engine)
        return activeData.engineName != null && activeData.engineName.IndexOf("Jet Engine", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
