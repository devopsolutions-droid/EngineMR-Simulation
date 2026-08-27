// This script runs the simple sequential Show Working tour specifically for the Jet Engine.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Simple multi-stage Show Working controller.
///
/// Stage 0 (steps 1-3): Remove covers → turbine starts (20% legacy airflow + blades + visuals)
/// Stage 1 (step 4-5):  Air Intake (50%) + Bypass (65%)
/// Stage 2 (steps 6-14): Sequential steps advancing airflow
///
/// State machine using a flat _step integer:
///   1  = Remove outer cover
///   2  = Remove inner cover
///   3  = Click Next → turbine starts (blades + 20% airflow + intake visual)
///   4  = Click Next → Air Intake (50% legacy airflow + bypass visual)
///   5  = Click Next → Advance to 65%
///   6  = Click Next → Advance to 65% (airflow already there, set again)
///   7  = Click Next → Step 7 description
///   8  = Click Next → Step 8 description
///   9  = Click Next → Step 9 description
///   10 = Click Next → Step 10 description
///   11 = Click Next → Step 11 description
///   12 = Click Next → Step 12 description
///   13 = Click Next → Step 13 description
///   14 = Complete
///
/// Legacy airflow tube advances progressively:
///   Step 1  done → 5%   (outer cover removed — faint glow)
///   Step 2  done → 10%  (inner cover removed — clearly visible)
///   Step 3  done → 20%  (turbine start — blades + visuals)
///   Step 4  done → 50%  (Air Intake)
///   Step 5  done → 65%  (Bypass)
///   Step 6  done → 65%  (Step 7 airflow visual)
///   Steps 7-14 use per-step airflowProgress set in the Inspector
///
/// Next / Previous buttons are always visible so the user can freely navigate.
/// </summary>
public class SimpleShowWorkingController : MonoBehaviour, IShowWorkingController
{
    [System.Serializable]
    public class StepData
    {
        [Tooltip("Step title shown in the tablet UI.")]
        public string title = "Step";

        [Tooltip("Step description shown in the tablet UI.")]
        [TextArea(2, 4)]
        public string description = "Description";

        [Tooltip("Airflow advancement % (0–100) for this step.\n"
            + "Enter a whole number like 10, 23, 55, 90 etc.\n"
            + "This value is used when advancing TO this step.")]
        [Range(0, 100)]
        public int airflowProgress = 0;

        [Tooltip("Audio clip that plays when this step becomes active.")]
        public AudioClip stepAudio;

        [Tooltip("Optional graph GameObject to show during this step.")]
        public GameObject graphObject;
    }

    [System.Serializable]
    public class StepHoverPanels
    {
        [Tooltip("Hover panel GameObjects activated when this step is active.")]
        public GameObject[] panels = new GameObject[0];
    }

    // ── Cover references removed — starting directly at Step 3 ──

    [Header("── Airflow Visual: Step 4 (Air Intake) ──")]
    [Tooltip("Air intake airflow visual GameObject.\nActivated on Step 3→4 transition.")]
    public GameObject intakeAirflowVisual;
    [Tooltip("Fade duration for the intake airflow visual (0→1 _Progress).")]
    public float intakeFadeDuration = 1.5f;

    [Header("── Airflow Visual: Step 5 (Bypass) ──")]
    [Tooltip("Bypass airflow visual GameObject.\nActivated on Step 4→5 transition.")]
    public GameObject bypassAirflowVisual;
    [Tooltip("Fade duration for the bypass airflow visual (0→1 _Progress).")]
    public float bypassFadeDuration = 1.5f;

    [Header("── Airflow Visual: Step 10 ──")]
    [Tooltip("Step 10 airflow visual GameObject.\nActivated on Step 9→10 transition.")]
    public GameObject step8AirflowVisual;
    [Tooltip("Fade duration for the step 10 airflow visual (0→1 _Progress).")]
    public float step8FadeDuration = 1.5f;

    [Header("── Airflow Visual: Step 14 ──")]
    [Tooltip("Step 14 airflow visual GameObject.\nActivated on Step 13→14 transition.")]
    public GameObject step14AirflowVisual;
    [Tooltip("Fade duration for the step 14 airflow visual (0→1 _Progress).")]
    public float step14FadeDuration = 1.5f;

    [Header("Blade Rotation")]
    [Tooltip("Drag individual blade GameObjects from the Hierarchy here.\n"
        + "Only these GameObjects will rotate when Step 3 starts.")]
    public GameObject[] bladesToRotate = new GameObject[0];

    [Header("Blade Rotation Settings")]
    [Tooltip("Local rotation axis for each blade (e.g. Vector3.forward, Vector3.up, Vector3.right).")]
    public Vector3 rotationAxis = Vector3.forward;

    [Tooltip("Peak rotation speed in degrees per second.")]
    public float rotationSpeed = 45f;

    [Tooltip("Smooth acceleration curve from 0→1 over accelerationDuration.")]
    public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Seconds to smoothly accelerate from 0 to full rotationSpeed.")]
    public float accelerationDuration = 2f;

    [Header("Model Validation")]
    [Tooltip("Assign an EngineData asset here to restrict this script to a specific engine model. Leave empty for no restriction.")]
    [FormerlySerializedAs("jetEngineData")]
    public EngineData targetEngineData;

    [Header("System References")]
    public EngineGrabManager grabManager;
    public JetEngineShowWorking showWorking;
    public SimplePartExplorer partExplorer;

    [Tooltip("TabletUIController for the onboard tablet display. Its partNameText and "
        + "engineDescriptionText are updated with the step title & description during "
        + "Show Working so the wall monitor / tablet display area shows step info.\n"
        + "Auto-found on Start if left empty.")]
    public TabletUIController tabletController;

    [Tooltip("The EngineInteractor that handles ray-hover outlines + hover panels.\n"
        + "Auto-found on Start if left empty. Disabled during Show Working flow\n"
        + "so engine parts don't show outlines/hover panels when the user looks at them.")]
    public EngineInteractor engineInteractor;

    [Header("── Graph Display Pane ──")]
    [Tooltip("The Graph Display Panel GameObject.")]
    public GameObject graphDisplayPanel;

    [Header("Settings")]
    // ── removeDistance removed — no cover detection needed ──

    [Header("Show Working Object Hiding")]
    [Tooltip("Drag and drop GameObjects here that you want to hide when Show Working mode starts, and restore when it stops.")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    private Dictionary<GameObject, bool> _hiddenObjectsOriginalState = new Dictionary<GameObject, bool>();

    [Header("Audio")]
    [Tooltip("AudioSource used to play per-step audio clips.\n"
        + "If left empty, one will be auto-created at runtime.")]
    public AudioSource stepAudioSource;

    [Header("Step Titles & Descriptions (editable in Inspector)")]
    [Tooltip("Step 1 — outer cover removal.")]
    public StepData step1 = new StepData { title = "Step 1 of 4", description = "Remove the outer cover and keep it away from the engine.", airflowProgress = 5 };

    [Tooltip("Step 2 — inner cover removal.")]
    public StepData step2 = new StepData { title = "Step 2 of 4", description = "Remove the inner cover away from the engine.", airflowProgress = 10 };

    [Tooltip("Step 3 — start turbine.")]
    public StepData step3 = new StepData { title = "Step 3 of 4", description = "Click Next to start the turbine.", airflowProgress = 20 };

    [Tooltip("Step 4 — air intake stage.")]
    public StepData step4 = new StepData { title = "Stage 1: Air Intake", description = "Air Intake flow — advance airflow to 50%. Click Next to proceed.", airflowProgress = 50 };

    [Tooltip("Step 5 — advance airflow to 65%.")]
    public StepData step5 = new StepData { title = "Stage 1: Air Intake", description = "Airflow advancing to 65%. Click Next to proceed.", airflowProgress = 65 };

    [Tooltip("Step 6 — advance airflow to 65% for step 7 setup.")]
    public StepData step6 = new StepData { title = "Step 6 of 13", description = "Advancing airflow to 65%. Click Next to proceed.", airflowProgress = 65 };

    [Tooltip("Step 7 — edit in Inspector.")]
    public StepData step7 = new StepData { title = "Step 7 of 13", description = "Step 7 desc", airflowProgress = 65 };

    [Tooltip("Step 8 — edit in Inspector.")]
    public StepData step8 = new StepData { title = "Step 8 of 13", description = "Step 8 desc", airflowProgress = 70 };

    [Tooltip("Step 9 — edit in Inspector.")]
    public StepData step9 = new StepData { title = "Step 9 of 13", description = "Step 9 desc", airflowProgress = 75 };

    [Tooltip("Step 10 — edit in Inspector.")]
    public StepData step10 = new StepData { title = "Step 10 of 13", description = "Step 10 desc", airflowProgress = 80 };

    [Tooltip("Step 11 — edit in Inspector.")]
    public StepData step11 = new StepData { title = "Step 11 of 13", description = "Step 11 desc", airflowProgress = 85 };

    [Tooltip("Step 12 — edit in Inspector.")]
    public StepData step12 = new StepData { title = "Step 12 of 13", description = "Step 12 desc", airflowProgress = 90 };

    [Tooltip("Step 13 — edit in Inspector.")]
    public StepData step13 = new StepData { title = "Step 13 of 13", description = "Step 13 desc", airflowProgress = 95 };

    [Tooltip("Step 14 — edit in Inspector.")]
    public StepData step14 = new StepData { title = "Complete", description = "Turbine is now running with full flow!", airflowProgress = 0 };

    [Tooltip("Step 15 — edit in Inspector.")]
    public StepData step15 = new StepData { title = "Step 15 of 13", description = "Step 15 desc", airflowProgress = 0 };

    [Header("Hover Panels — activated when their step is shown")]
    [Tooltip("Panels active during Step 1 (remove outer cover).")]
    public StepHoverPanels step1HoverPanels;
    [Tooltip("Panels active during Step 2 (remove inner cover).")]
    public StepHoverPanels step2HoverPanels;
    [Tooltip("Panels active during Step 3 (start turbine).")]
    public StepHoverPanels step3HoverPanels;
    [Tooltip("Panels active during Step 4 (Air Intake stage).")]
    public StepHoverPanels step4HoverPanels;
    [Tooltip("Panels active during Step 5 (advance to 65%).")]
    public StepHoverPanels step5HoverPanels;
    [Tooltip("Panels active during Step 6 (advance airflow).")]
    public StepHoverPanels step6HoverPanels;
    [Tooltip("Panels active during Step 7.")]
    public StepHoverPanels step7HoverPanels;
    [Tooltip("Panels active during Step 8.")]
    public StepHoverPanels step8HoverPanels;
    [Tooltip("Panels active during Step 9.")]
    public StepHoverPanels step9HoverPanels;
    [Tooltip("Panels active during Step 10.")]
    public StepHoverPanels step10HoverPanels;
    [Tooltip("Panels active during Step 11.")]
    public StepHoverPanels step11HoverPanels;
    [Tooltip("Panels active during Step 12.")]
    public StepHoverPanels step12HoverPanels;
    [Tooltip("Panels active during Step 13.")]
    public StepHoverPanels step13HoverPanels;
    [Tooltip("Panels active during Step 14.")]
    public StepHoverPanels step14HoverPanels;

    [Tooltip("Panels active during Step 15.")]
    public StepHoverPanels step15HoverPanels;

    // ── State ──────────────────────────────────────────────────────────────────
    private int _step;          // 3..15  (0 = not started, skipping 1-2)
    private bool _isRunning;
    private bool _hasCompleted;

    // ── Shared MPB for smooth _Progress animation on airflow visuals ────────────
    // AudioSource auto-created at runtime if inspector field is null
    private AudioSource _runtimeAudioSource;

    private MaterialPropertyBlock _mpb;
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");

    // Cached MeshRenderer lists for each visual wrapper (discovered at start)
    private List<MeshRenderer> _intakeRenderers;
    private List<MeshRenderer> _bypassRenderers;
    private List<MeshRenderer> _step8Renderers;
    private List<MeshRenderer> _step14Renderers;

    // Track running coroutines so we can stop them on hide / stop
    private Coroutine _intakeAnim;
    private Coroutine _bypassAnim;
    private Coroutine _step8Anim;
    private Coroutine _step14Anim;

    // ── Direct Blade Rotation State ─────────────────────────────────────────────
    private Transform[] _bladeTransforms;
    private Quaternion[] _originalBladeRotations;
    private bool _bladesRotating;
    private float _currentBladeSpeed;
    private Coroutine _bladeAccelCoroutine;

    // ── Unity Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-find references when not assigned in the Inspector.
    /// Also caches MeshRenderers and creates a shared MaterialPropertyBlock for
    /// smooth _Progress animation on Intake_Airflow_Visual / Bypass_Air_Visual.
    /// Follows the same pattern as ShowWorkingInteractiveController.Start() — line 337-340.
    /// This ensures the controller works even when the Inspector references need
    /// manual setup or become unlinked.
    /// </summary>
    private void EnsureAudioSource()
    {
        if (stepAudioSource != null) return;
        if (_runtimeAudioSource != null) return;
        _runtimeAudioSource = gameObject.AddComponent<AudioSource>();
        _runtimeAudioSource.playOnAwake = false;
        _runtimeAudioSource.spatialBlend = 0f; // 2D sound by default
    }

    void Start()
    {
        // Auto-find all system references if not already wired in the Inspector
        EnsureAudioSource();

        DeactivateAllGraphObjects();

        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(false);
        }

        if (engineInteractor == null) engineInteractor = FindFirstObjectByType<EngineInteractor>();
        if (tabletController == null) tabletController = FindFirstObjectByType<TabletUIController>();
        if (grabManager == null) grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (partExplorer == null) partExplorer = FindFirstObjectByType<SimplePartExplorer>();
        if (showWorking == null)  showWorking  = FindFirstObjectByType<JetEngineShowWorking>();

        // ── Cache Transforms from the bladesToRotate array ───────────────────
        // Priority 1: use directly-assigned blade GameObjects
        if (bladesToRotate != null && bladesToRotate.Length > 0)
        {
            _bladeTransforms = new Transform[bladesToRotate.Length];
            _originalBladeRotations = new Quaternion[bladesToRotate.Length];
            for (int i = 0; i < bladesToRotate.Length; i++)
            {
                if (bladesToRotate[i] != null)
                {
                    _bladeTransforms[i] = bladesToRotate[i].transform;
                    _originalBladeRotations[i] = bladesToRotate[i].transform.localRotation;
                }
            }
            Debug.Log($"[SimpleShowWorking] Cached {_bladeTransforms.Length} blade transform(s) from bladesToRotate.");
        }
        else
        {
            // Priority 2: fallback — auto-discover TurbineBladeRotator components
            // for backward compatibility (user hasn't migrated to bladesToRotate yet)
            var legacyRotators = FindObjectsByType<TurbineBladeRotator>(FindObjectsSortMode.None);
            if (legacyRotators != null && legacyRotators.Length > 0)
            {
                int count = 0;
                foreach (var rot in legacyRotators)
                {
                    // Use the rotator's own blades array if populated
                    if (rot.blades != null && rot.blades.Length > 0)
                        count += rot.blades.Length;
                    else
                        count++; // fallback: use the rotator's own transform
                }
                _bladeTransforms = new Transform[count];
                _originalBladeRotations = new Quaternion[count];
                int idx = 0;
                foreach (var rot in legacyRotators)
                {
                    if (rot.blades != null && rot.blades.Length > 0)
                    {
                        foreach (var blade in rot.blades)
                        {
                            if (blade != null)
                            {
                                _bladeTransforms[idx] = blade.transform;
                                _originalBladeRotations[idx] = blade.transform.localRotation;
                                idx++;
                            }
                        }
                    }
                    else
                    {
                        _bladeTransforms[idx] = rot.transform;
                        _originalBladeRotations[idx] = rot.transform.localRotation;
                        idx++;
                    }
                }
                Debug.Log($"[SimpleShowWorking] Auto-discovered {count} blade transform(s) from {legacyRotators.Length} TurbineBladeRotator(s).");
            }
            else
            {
                _bladeTransforms = Array.Empty<Transform>();
                Debug.Log("[SimpleShowWorking] No blades assigned — nothing will rotate.");
            }
        }

        // ── Prepare smooth _Progress animation infrastructure ────────────────
        _mpb = new MaterialPropertyBlock();
        _intakeRenderers = CacheMeshRenderers(intakeAirflowVisual);
        _bypassRenderers = CacheMeshRenderers(bypassAirflowVisual);
        _step8Renderers = CacheMeshRenderers(step8AirflowVisual);
        _step14Renderers = CacheMeshRenderers(step14AirflowVisual);
    }

    /// <summary>
    /// Apply per-frame rotation to all cached blade Transforms when blades are spinning.
    /// Uses the currentBladeSpeed (accelerated or full) so the spin ramps up smoothly.
    /// </summary>
    void Update()
    {
        if (!IsActiveEngineValid()) return;
        if (!_bladesRotating || _bladeTransforms == null) return;

        float step = _currentBladeSpeed * Time.deltaTime;
        for (int i = 0; i < _bladeTransforms.Length; i++)
        {
            if (_bladeTransforms[i] != null)
                _bladeTransforms[i].Rotate(rotationAxis, step, Space.Self);
        }
    }

    /// <summary>
    /// Collect all MeshRenderers in the children of the given root GameObject.
    /// If root is null, returns an empty list.
    /// </summary>
    private static List<MeshRenderer> CacheMeshRenderers(GameObject root)
    {
        var list = new List<MeshRenderer>();
        if (root == null) return list;
        root.GetComponentsInChildren(true, list);
        return list;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public bool IsRunning     => _isRunning;
    public bool HasCompleted  => _hasCompleted;

    /// <summary>Called by EngineViewManager when Show Working is activated (highest priority).</summary>
    public void StartInteractiveFlow()
    {
        if (!IsActiveEngineValid()) return;
        if (_isRunning) return;
        Debug.Log("[SimpleShowWorking] Starting interactive flow (beginning at Step 3).");

        DeactivateAllGraphObjects();

        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(false);
        }

        _isRunning    = true;
        _hasCompleted = false;
        _step         = 3;  // Start directly at Step 3, skipping cover removal

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

        // ── Initialize JetEngineShowWorking ─────────────────────────────────
        // Reset the legacy airflow system: _coversRemoved = false, _highestProgress = 0.
        // skipAutoCoverRemoval = true because the user removes covers manually
        // in this flow — we don't want the system to auto-hide those.
        if (showWorking != null)
        {
            showWorking.skipAutoCoverRemoval = true;
            showWorking.OnShowWorkingStart();
            Debug.Log("[SimpleShowWorking] JetEngineShowWorking initialized (skipAutoCoverRemoval=true).");
        }
        else
        {
            Debug.LogWarning("[SimpleShowWorking] showWorking reference is null — cannot initialize legacy airflow system.");
        }

        // ── No cover origin tracking needed — starting at step 3 ──

        // Ensure all airflow visuals are OFF at start (also resets _Progress to 0)
        SetVisualProgressInstantly(intakeAirflowVisual, _intakeRenderers, 0f);
        SetVisualProgressInstantly(bypassAirflowVisual, _bypassRenderers, 0f);
        SetVisualProgressInstantly(step8AirflowVisual, _step8Renderers, 0f);
        SetVisualProgressInstantly(step14AirflowVisual, _step14Renderers, 0f);

        // ── Grabbing disabled since no covers to remove ──
        if (grabManager != null)
        {
            grabManager.allowGrabbing = false;
        }

        // ── Disable EngineInteractor ──────────────────────────────────
        // Prevents outlines + hover panels on engine parts during Show Working
        if (engineInteractor != null)
        {
            engineInteractor.DisableInteraction();
            Debug.Log("[SimpleShowWorking] EngineInteractor disabled — outlines + hover panels suppressed.");
        }

        // Next / Previous buttons remain visible so the user can navigate freely
        if (partExplorer != null)
            partExplorer.ShowNavigationButtons();

        ShowCurrentStepUI();
    }

    /// <summary>Called by EngineViewManager when Show Working is stopped.</summary>
    public void StopInteractiveFlow()
    {
        if (!IsActiveEngineValid()) return;
        if (!_isRunning) return;
        Debug.Log("[SimpleShowWorking] Stopping interactive flow.");

        DeactivateAllGraphObjects();

        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(false);
        }

        _isRunning = false;
        _step      = 0;

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

        // ── No grab cleanup needed (covers removed) ──
        if (grabManager != null)
        {
            grabManager.allowGrabbing = false;
        }

        // ── Re-enable EngineInteractor ─────────────────────────────────
        // Restore normal ray-hover outlines + hover panels
        if (engineInteractor != null)
        {
            engineInteractor.EnableInteraction();
            Debug.Log("[SimpleShowWorking] EngineInteractor re-enabled — outlines + hover panels restored.");
        }

        // Stop blade rotation and hard-reset to original positions
        StopAllBladeRotators();
        ResetBladeRotations();

        // Hide all airflow visuals — stop any running animation, reset _Progress to 0
        StopAndHideVisual(ref _intakeAnim, intakeAirflowVisual, _intakeRenderers);
        StopAndHideVisual(ref _bypassAnim, bypassAirflowVisual, _bypassRenderers);
        StopAndHideVisual(ref _step8Anim, step8AirflowVisual, _step8Renderers);
        StopAndHideVisual(ref _step14Anim, step14AirflowVisual, _step14Renderers);

        // ── Tear down JetEngineShowWorking ──────────────────────────────────
        // Reset airflow to 0 and restore any parts that were hidden on start
        if (showWorking != null)
        {
            showWorking.SetAirflowProgressDirect(0f, false);
            showWorking.OnShowWorkingStop();
            Debug.Log("[SimpleShowWorking] JetEngineShowWorking stopped — airflow reset, hidden parts restored.");
        }

        // Stop any playing audio
        AudioSource src = stepAudioSource != null ? stepAudioSource : _runtimeAudioSource;
        if (src != null)
        {
            src.Stop();
            src.clip = null;
        }

        // Deactivate all hover panels on stop
        RefreshHoverPanels();

        Debug.Log("[SimpleShowWorking] Flow stopped — grab disabled, rotation stopped, airflow reset, hover panels hidden.");
    }

    /// <summary>Called by TabletUIController.OnNextClicked() when this controller is running.</summary>
    public void OnNextPressed()
    {
        if (!IsActiveEngineValid()) return;
        switch (_step)
        {
            case 3:
                // ── Step 3 → Step 4: Turbine Start ──────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 3 → 4 — starting turbine effects.");
                _step = 4;

                // Advance legacy airflow tube to step 4's configured value
                // slideCovers: false — covers already removed manually by the user.
                AdvanceToStepAirflow(_step);

                // Start blade rotation NOW — entering step 4
                StartAllBladeRotators();

                // ── Intake visual only on step 4 ─────────────────────────────────
                // Bypass visual is delayed until step 5 (4→5 transition).
                if (intakeAirflowVisual != null) intakeAirflowVisual.SetActive(true);

                // Smoothly fade in intake airflow visual (0→100% _Progress)
                _intakeAnim = StartCoroutine(
                    AnimateVisualProgress(_intakeRenderers, 0f, 1f, intakeFadeDuration, null));

                ShowCurrentStepUI();
                return;

            case 4:
                // ── Step 4 → Step 5: Stage 1 — Air Intake + Bypass ──────────────
                Debug.Log("[SimpleShowWorking] Step 4 → 5 — Stage 1: Air Intake + Bypass.");
                _step = 5;

                // Advance legacy airflow tube to step 5's configured value
                // slideCovers: false — covers already removed manually by the user.
                AdvanceToStepAirflow(_step);

                // ── Bypass visual activates NOW (delayed from step 4) ────────────
                if (bypassAirflowVisual != null) bypassAirflowVisual.SetActive(true);
                _bypassAnim = StartCoroutine(
                    AnimateVisualProgress(_bypassRenderers, 0f, 1f, bypassFadeDuration, null));

                ShowCurrentStepUI();
                return;

            case 5:
                // ── Step 5 → Step 6: Advance airflow to 65% ─────────────────────
                Debug.Log("[SimpleShowWorking] Step 5 → 6 — advancing airflow.");
                _step = 6;

                // Advance legacy airflow tube to step 6's configured value (65%)
                AdvanceToStepAirflow(_step);

                ShowCurrentStepUI();
                return;

            case 6:
                // ── Step 6 → Step 7 ────────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 6 → 7.");
                _step = 7;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 7:
                // ── Step 7 → Step 8 ────────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 7 → 8.");
                _step = 8;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 8:
                // ── Step 8 → Step 9 ────────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 8 → 9.");
                _step = 9;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 9:
                // ── Step 9 → Step 10 — activating step 8 airflow visual ────────
                Debug.Log("[SimpleShowWorking] Step 9 → 10 — activating step 8 airflow visual.");
                _step = 10;
                AdvanceToStepAirflow(_step);

                // Activate and fade in step 8 airflow visual (moved from step 7→8)
                if (step8AirflowVisual != null) step8AirflowVisual.SetActive(true);
                _step8Anim = StartCoroutine(
                    AnimateVisualProgress(_step8Renderers, 0f, 1f, step8FadeDuration, null));

                ShowCurrentStepUI();
                return;

            case 10:
                // ── Step 10 → Step 11 ──────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 10 → 11.");
                _step = 11;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 11:
                // ── Step 11 → Step 12 ──────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 11 → 12.");
                _step = 12;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 12:
                // ── Step 12 → Step 13 ──────────────────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 12 → 13.");
                _step = 13;
                AdvanceToStepAirflow(_step);
                ShowCurrentStepUI();
                return;

            case 13:
                // ── Step 13 → Step 14 — activating step 14 airflow visual ────
                Debug.Log("[SimpleShowWorking] Step 13 → 14 — activating step 14 airflow visual.");
                _step = 14;
                AdvanceToStepAirflow(_step);

                // Activate and fade in step 14 airflow visual
                if (step14AirflowVisual != null) step14AirflowVisual.SetActive(true);
                _step14Anim = StartCoroutine(
                    AnimateVisualProgress(_step14Renderers, 0f, 1f, step14FadeDuration, null));

                ShowCurrentStepUI();
                return;

            case 14:
                // ── Step 14 → Step 15: Complete ───────────────────────────────
                Debug.Log("[SimpleShowWorking] Step 14 → 15 — complete.");
                _step = 15;
                AdvanceToStepAirflow(_step);
                _hasCompleted = true;
                ShowCurrentStepUI();
                return;

            default:
                Debug.Log($"[SimpleShowWorking] OnNextPressed ignored — step is {_step}, expected 3..14.");
                return;
        }
    }

    /// <summary>Called by TabletUIController.OnPreviousClicked() when this controller is running.</summary>
    public void OnPreviousPressed()
    {
        if (!IsActiveEngineValid()) return;
        switch (_step)
        {
            case 3:
                // Step 3 is the minimum — cannot go back further (covers removed)
                Debug.Log("[SimpleShowWorking] Previous pressed but already at Step 3 (minimum step). Cannot go back.");
                return;

            case 4:
                // Step 4 → Step 3: Go back before turbine start
                _step = 3;
                // Blades should NOT be spinning in step 3 — stop them
                StopAllBladeRotators();
                // Hide airflow visuals instantly when going back (snappy Previous response)
                StopAndHideVisual(ref _intakeAnim, intakeAirflowVisual, _intakeRenderers);
                StopAndHideVisual(ref _bypassAnim, bypassAirflowVisual, _bypassRenderers);
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 3 (turbine start).");
                ShowCurrentStepUI();
                return;

            case 5:
                // Step 5 → Step 4: Go back to Air Intake stage
                _step = 4;
                // Hide bypass visual — it only belongs on step 5
                StopAndHideVisual(ref _bypassAnim, bypassAirflowVisual, _bypassRenderers);
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 4 (Air Intake).");
                ShowCurrentStepUI();
                return;

            case 6:
                // Step 6 → Step 5: Go back to Bypass stage
                _step = 5;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 5 (Bypass).");
                ShowCurrentStepUI();
                return;

            case 7:
                // Step 7 → Step 6: Go back
                _step = 6;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 6.");
                ShowCurrentStepUI();
                return;

            case 8:
                // Step 8 → Step 7: Go back
                _step = 7;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 7.");
                ShowCurrentStepUI();
                return;

            case 9:
                // Step 9 → Step 8: Go back
                _step = 8;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 8.");
                ShowCurrentStepUI();
                return;

            case 10:
                // Step 10 → Step 9: Go back — hide step 8 airflow visual
                _step = 9;
                StopAndHideVisual(ref _step8Anim, step8AirflowVisual, _step8Renderers);
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 9.");
                ShowCurrentStepUI();
                return;

            case 11:
                // Step 11 → Step 10: Go back
                _step = 10;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 10.");
                ShowCurrentStepUI();
                return;

            case 12:
                // Step 12 → Step 11: Go back
                _step = 11;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 11.");
                ShowCurrentStepUI();
                return;

            case 13:
                // Step 13 → Step 12: Go back
                _step = 12;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 12.");
                ShowCurrentStepUI();
                return;

            case 14:
                // Step 14 → Step 13: Go back — hide step 14 airflow visual
                _step = 13;
                StopAndHideVisual(ref _step14Anim, step14AirflowVisual, _step14Renderers);
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 13.");
                ShowCurrentStepUI();
                return;

            case 15:
                // Step 15 → Step 14: Go back from Complete
                _step = 14;
                _hasCompleted = false;
                SetToStepAirflow(_step);
                Debug.Log("[SimpleShowWorking] Previous pressed — back to step 14.");
                ShowCurrentStepUI();
                return;

            default:
                Debug.Log($"[SimpleShowWorking] OnPreviousPressed ignored — step is {_step}.");
                return;
        }
    }

    // ── Smooth _Progress Animation ─────────────────────────────────────────────

    /// <summary>
    /// Coroutine that smoothly animates _Progress from [from]→[to] on all
    /// MeshRenderers in the list using a shared MaterialPropertyBlock.
    /// Enables the wrapper GameObject at start, disables it after fade-out (to===0).
    /// </summary>
    private IEnumerator AnimateVisualProgress(List<MeshRenderer> renderers,
        float from, float to, float duration, System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float progress = Mathf.Lerp(from, to, t);
            ApplyProgressToRenderers(renderers, progress);
            yield return null;
        }

        // Snap to final value
        ApplyProgressToRenderers(renderers, to);

        // If fading out completely, deactivate the wrapper
        if (to < 0.001f)
        {
            // Find which wrapper owns these renderers and deactivate it
            if (renderers == _intakeRenderers && intakeAirflowVisual != null)
                intakeAirflowVisual.SetActive(false);
            else if (renderers == _bypassRenderers && bypassAirflowVisual != null)
                bypassAirflowVisual.SetActive(false);
            else if (renderers == _step14Renderers && step14AirflowVisual != null)
                step14AirflowVisual.SetActive(false);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Apply _Progress to all renderers in the list via MaterialPropertyBlock.
    /// </summary>
    private void ApplyProgressToRenderers(List<MeshRenderer> renderers, float progress)
    {
        if (renderers == null || _mpb == null) return;
        _mpb.SetFloat(ProgressID, progress);
        foreach (var r in renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Instantly set _Progress to a value and toggle the wrapper GameObject.
    /// </summary>
    private void SetVisualProgressInstantly(GameObject wrapper,
        List<MeshRenderer> renderers, float progress)
    {
        bool active = progress > 0.001f;
        if (wrapper != null && wrapper.activeSelf != active)
            wrapper.SetActive(active);
        ApplyProgressToRenderers(renderers, progress);
    }

    /// <summary>
    /// Stop a running animation coroutine, reset _Progress to 0, and
    /// deactivate the wrapper GameObject.
    /// </summary>
    private void StopAndHideVisual(ref Coroutine anim,
        GameObject wrapper, List<MeshRenderer> renderers)
    {
        if (anim != null)
        {
            StopCoroutine(anim);
            anim = null;
        }
        ApplyProgressToRenderers(renderers, 0f);
        if (wrapper != null)
            wrapper.SetActive(false);
    }

    private void DeactivateAllGraphObjects()
    {
        if (step1.graphObject != null) step1.graphObject.SetActive(false);
        if (step2.graphObject != null) step2.graphObject.SetActive(false);
        if (step3.graphObject != null) step3.graphObject.SetActive(false);
        if (step4.graphObject != null) step4.graphObject.SetActive(false);
        if (step5.graphObject != null) step5.graphObject.SetActive(false);
        if (step6.graphObject != null) step6.graphObject.SetActive(false);
        if (step7.graphObject != null) step7.graphObject.SetActive(false);
        if (step8.graphObject != null) step8.graphObject.SetActive(false);
        if (step9.graphObject != null) step9.graphObject.SetActive(false);
        if (step10.graphObject != null) step10.graphObject.SetActive(false);
        if (step11.graphObject != null) step11.graphObject.SetActive(false);
        if (step12.graphObject != null) step12.graphObject.SetActive(false);
        if (step13.graphObject != null) step13.graphObject.SetActive(false);
        if (step14.graphObject != null) step14.graphObject.SetActive(false);
        if (step15.graphObject != null) step15.graphObject.SetActive(false);
    }

    // ── Internal ───────────────────────────────────────────────────────────────
    // ── OnPartReleased removed — no cover detection needed (starting at step 3) ──

    private void ShowCurrentStepUI()
    {
        if (partExplorer == null)
        {
            Debug.LogWarning("[SimpleShowWorking] partExplorer not assigned — cannot update tablet UI.");
            return;
        }

        // Next / Previous buttons are always visible for navigation
        partExplorer.ShowNavigationButtons();

        StepData data;
        int stepNumber;
        int totalSteps = 14;

        switch (_step)
        {
            case 1:
                data = step1;
                stepNumber = 1;
                break;

            case 2:
                data = step2;
                stepNumber = 2;
                break;

            case 3:
                data = step3;
                stepNumber = 3;
                break;

            case 4:
                data = step4;
                stepNumber = 4;
                break;

            case 5:
                data = step5;
                stepNumber = 5;
                break;

            case 6:
                data = step6;
                stepNumber = 6;
                break;

            case 7:
                data = step7;
                stepNumber = 7;
                break;

            case 8:
                data = step8;
                stepNumber = 8;
                break;

            case 9:
                data = step9;
                stepNumber = 9;
                break;

            case 10:
                data = step10;
                stepNumber = 10;
                break;

            case 11:
                data = step11;
                stepNumber = 11;
                break;

            case 12:
                data = step12;
                stepNumber = 12;
                break;

            case 13:
                data = step13;
                stepNumber = 13;
                break;

            case 14:
                data = step14;
                stepNumber = 14;
                break;

            case 15:
                data = step15;
                stepNumber = 14;
                break;

            default:
                return;
        }

        if (data != null)
        {
            partExplorer.SetInteractiveUIText(data.title, data.description, stepNumber, totalSteps);

            // ── Update Graph Display Panel based on step number ──
            if (graphDisplayPanel != null)
            {
                bool shouldBeActive = (_step >= 6 && _isRunning && data.graphObject != null);
                graphDisplayPanel.SetActive(shouldBeActive);

                DeactivateAllGraphObjects();

                if (shouldBeActive && data.graphObject != null)
                {
                    data.graphObject.SetActive(true);
                }

                Debug.Log($"[SimpleShowWorking] Step {_step} ({data.title}): shouldBeActive={shouldBeActive}, hasGraphObject={data.graphObject != null}");
            }

            // ── Push step info to the wall monitor / tablet display area ───────
            // TabletUIController.partNameText → "Engine Part Name Monitor"
            // TabletUIController.engineDescriptionText → "Engine Part Description Monitor"
            if (tabletController != null)
            {
                if (tabletController.partNameText != null)
                    tabletController.partNameText.text = data.title;
                if (tabletController.engineDescriptionText != null)
                    tabletController.engineDescriptionText.text = data.description;
            }

            // ── Play per-step audio ──────────────────────────────────────────────
            AudioSource src = stepAudioSource != null ? stepAudioSource : _runtimeAudioSource;
            if (src != null)
            {
                if (data.stepAudio != null)
                {
                    src.Stop();
                    src.clip = data.stepAudio;
                    src.Play();
                }
                else
                {
                    // No clip assigned for this step — stop any currently playing audio
                    src.Stop();
                    src.clip = null;
                }
            }
        }

        // ── Activate / deactivate hover panels for the current step ──────────
        RefreshHoverPanels();
    }

    // ── Hover Panel Auto-Activation ────────────────────────────────────────────

    /// <summary>
    /// Deactivates all hover panels across every step, then activates only the
    /// panels belonging to the current _step. Call this whenever the step changes
    /// or the flow stops.
    /// </summary>
    private void RefreshHoverPanels()
    {
        // Deactivate ALL panels first (clean slate)
        DeactivateAllHoverPanels();

        // Activate only the panels for the current step
        if (_step == 0) return; // stopped — keep everything off
        StepHoverPanels current = GetCurrentStepPanels();
        if (current?.panels == null) return;
        foreach (GameObject panel in current.panels)
        {
            if (panel != null)
                panel.SetActive(true);
        }
    }

    /// <summary>Deactivates every panel in all fourteen StepHoverPanels fields.</summary>
    private void DeactivateAllHoverPanels()
    {
        DeactivatePanels(step1HoverPanels);
        DeactivatePanels(step2HoverPanels);
        DeactivatePanels(step3HoverPanels);
        DeactivatePanels(step4HoverPanels);
        DeactivatePanels(step5HoverPanels);
        DeactivatePanels(step6HoverPanels);
        DeactivatePanels(step7HoverPanels);
        DeactivatePanels(step8HoverPanels);
        DeactivatePanels(step9HoverPanels);
        DeactivatePanels(step10HoverPanels);
        DeactivatePanels(step11HoverPanels);
        DeactivatePanels(step12HoverPanels);
        DeactivatePanels(step13HoverPanels);
        DeactivatePanels(step14HoverPanels);
        DeactivatePanels(step15HoverPanels);
    }

    private static void DeactivatePanels(StepHoverPanels step)
    {
        if (step?.panels == null) return;
        foreach (GameObject panel in step.panels)
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }

    /// <summary>Returns the StepHoverPanels that corresponds to the current _step.</summary>
    private StepHoverPanels GetCurrentStepPanels()
    {
        switch (_step)
        {
            case 1:  return step1HoverPanels;
            case 2:  return step2HoverPanels;
            case 3:  return step3HoverPanels;
            case 4:  return step4HoverPanels;
            case 5:  return step5HoverPanels;
            case 6:  return step6HoverPanels;
            case 7:  return step7HoverPanels;
            case 8:  return step8HoverPanels;
            case 9:  return step9HoverPanels;
            case 10: return step10HoverPanels;
            case 11: return step11HoverPanels;
            case 12: return step12HoverPanels;
            case 13: return step13HoverPanels;
            case 14: return step14HoverPanels;
            case 15: return step15HoverPanels;
            default: return null;
        }
    }

    /// <summary>
    /// Returns the configured airflowProgress (0–100 int) converted to 0..1
    /// for the legacy airflow system.
    /// </summary>
    private float GetAirflowForStep(int step)
    {
        int raw;
        switch (step)
        {
            case 1:  raw = step1.airflowProgress;  break;
            case 2:  raw = step2.airflowProgress;  break;
            case 3:  raw = step3.airflowProgress;  break;
            case 4:  raw = step4.airflowProgress;  break;
            case 5:  raw = step5.airflowProgress;  break;
            case 6:  raw = step6.airflowProgress;  break;
            case 7:  raw = step7.airflowProgress;  break;
            case 8:  raw = step8.airflowProgress;  break;
            case 9:  raw = step9.airflowProgress;  break;
            case 10: raw = step10.airflowProgress; break;
            case 11: raw = step11.airflowProgress; break;
            case 12: raw = step12.airflowProgress; break;
            case 13: raw = step13.airflowProgress; break;
            case 14: raw = step14.airflowProgress; break;
            case 15: raw = step15.airflowProgress; break;
            default: return 0f;
        }
        return Mathf.Clamp01(raw / 100f);
    }

    /// <summary>
    /// Advance legacy airflow TO the configured value for the given step.
    /// Uses AdvanceAirflowTo (cumulative — won't go backwards).
    /// </summary>
    private void AdvanceToStepAirflow(int step)
    {
        if (showWorking == null) return;
        float progress = GetAirflowForStep(step);
        showWorking.AdvanceAirflowTo(progress, slideCovers: false);
        Debug.Log($"[SimpleShowWorking] Legacy airflow advanced to {progress:P0} (step {step}).");
    }

    /// <summary>
    /// Set legacy airflow directly TO the configured value for the given step.
    /// Uses SetAirflowProgressDirect (absolute — will go backwards).
    /// </summary>
    private void SetToStepAirflow(int step)
    {
        if (showWorking == null) return;
        float progress = GetAirflowForStep(step);
        showWorking.SetAirflowProgressDirect(progress, false);
        Debug.Log($"[SimpleShowWorking] Legacy airflow set to {progress:P0} (step {step}).");
    }

    // ── Multi-Blade-Rotator Helpers ─────────────────────────────────────────

    /// <summary>Start rotating all cached blade Transforms with smooth acceleration.</summary>
    private void StartAllBladeRotators()
    {
        if (_bladeTransforms == null || _bladeTransforms.Length == 0) return;

        _bladesRotating = true;

        // Start the acceleration coroutine (restart if already running)
        if (_bladeAccelCoroutine != null)
            StopCoroutine(_bladeAccelCoroutine);
        _bladeAccelCoroutine = StartCoroutine(AccelerateBlades());

        Debug.Log($"[SimpleShowWorking] Started rotating {_bladeTransforms.Length} blade(s).");
    }

    /// <summary>Stop blade rotation immediately and reset speed.</summary>
    private void StopAllBladeRotators()
    {
        _bladesRotating = false;
        _currentBladeSpeed = 0f;

        if (_bladeAccelCoroutine != null)
        {
            StopCoroutine(_bladeAccelCoroutine);
            _bladeAccelCoroutine = null;
        }

        Debug.Log("[SimpleShowWorking] All blade rotation stopped.");
    }

    /// <summary>
    /// Hard‑reset every blade back to its original local rotation (snapped on Start).
    /// Called from StopInteractiveFlow() so blades return to their exact scene‑load pose.
    /// </summary>
    private void ResetBladeRotations()
    {
        if (_bladeTransforms == null || _originalBladeRotations == null) return;

        for (int i = 0; i < _bladeTransforms.Length; i++)
        {
            if (_bladeTransforms[i] != null && i < _originalBladeRotations.Length)
                _bladeTransforms[i].localRotation = _originalBladeRotations[i];
        }

        Debug.Log($"[SimpleShowWorking] Reset {_bladeTransforms.Length} blade(s) to original rotation.");
    }

    /// <summary>
    /// Smoothly ramps _currentBladeSpeed from 0 to rotationSpeed over accelerationDuration,
    /// using the accelerationCurve for easing.
    /// </summary>
    private IEnumerator AccelerateBlades()
    {
        float elapsed = 0f;
        while (elapsed < accelerationDuration)
        {
            // Keep spinning even while accelerating
            float t = elapsed / accelerationDuration;
            _currentBladeSpeed = rotationSpeed * accelerationCurve.Evaluate(t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _currentBladeSpeed = rotationSpeed;
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