using UnityEngine;

/// <summary>
/// A single step in the interactive Show Working flow.
/// All fields preserved for Unity serialization compatibility with existing Inspector config.
/// </summary>
[System.Serializable]
public class ShowWorkingStep
{
    // ── Step Info ─────────────────────────────────────────────────────────────
    [Header("Step Info")]
    [Tooltip("Name of this step (shown on tablet part name TMP).")]
    public string stepName = "Step";

    [Tooltip("Instruction text (shown on tablet part description TMP).")]
    [TextArea(2, 4)]
    public string instruction = "Press Next to continue.";

    // ── Stage Grouping ─────────────────────────────────────────────────────────
    [Header("Stage Grouping")]
    [Tooltip("Optional stage title shown on monitor/tablet Engine Part Name. When non-null and changes from previous step, updates the stage header. E.g. 'Stage 1: Air Intake'")]
    public string stageName;

    // ── Visual Activation ──────────────────────────────────────────────────────
    [Header("Visual Activation")]
    [Tooltip("GameObjects to activate when this step starts (NEW VISUAL ENABLED). These are deactivated when stepping back.")]
    public GameObject[] activateOnStepStart;

    // ── Hover Highlights ───────────────────────────────────────────────────────
    [Header("Hover Highlights")]
    [Tooltip("Engine parts to highlight + show hover panels for (HOVER PANEL ENABLED). Each should have a PartHoverPanel child.")]
    public GameObject[] highlightParts;

    [Tooltip("Direct hover panel GameObjects to show on this step. Drag the HoverPanel GameObject (the one with PartHoverPanel component) directly here.")]
    public GameObject[] hoverPanels;

    // ── Step Type ──────────────────────────────────────────────────────────────
    [Header("Step Type")]
    [Tooltip("The type of interaction for this step. Determines how the user progresses.")]
    public InteractiveStepType stepType = InteractiveStepType.GrabRemove;

    // ── Target ─────────────────────────────────────────────────────────────────
    [Header("Target")]
    [Tooltip("The GameObject the user must interact with. Required for GrabRemove and PartTap. Leave EMPTY for TurbineStart and IgniteButton.")]
    public GameObject targetPart;

    [Tooltip("How far (world units) the part must be moved from its starting position to count as removed (GrabRemove only).")]
    public float advanceDistance = 0.3f;

    // ── Visual ─────────────────────────────────────────────────────────────────
    [Header("Visual")]
    [Tooltip("If true, the part is NOT lifted up. For GrabRemove: user grabs from natural position. For PartTap: part stays in place (no LiftUp).")]
    public bool skipLift = false;

    // ── Turbine Start ──────────────────────────────────────────────────────────
    [Header("Turbine Start (TurbineStart type)")]
    [Tooltip("How much the airflow tube should fill when the turbine starts (0..1).")]
    [Range(0f, 1f)]
    public float turbineStartAirflowProgress = 0.07f;

    // ── Blade Spin ─────────────────────────────────────────────────────────────
    [Header("Blade Spin (BladeSpin type)")]
    [Tooltip("Drag the individual blade GameObjects that should rotate as a group. A runtime parent will be created automatically.")]
    public GameObject[] bladeTargets;

    [Tooltip("Rotation axis for this blade group (local space). Default (0,0,1) = Z-axis.")]
    public Vector3 bladeRotationAxis = new Vector3(0, 0, 1);

    // ── VFX Controllers ────────────────────────────────────────────────────────
    [Header("VFX Controllers (PartTap / IgniteButton)")]
    [Tooltip("Air Compression controller for PartTap steps that compress air (tube narrowing).")]
    public AirCompressionController airCompressionController;

    [Tooltip("Fuel Spray controller for PartTap steps that inject fuel (particle spray).")]
    public FuelSprayController fuelSprayController;

    [Tooltip("Combustion controller for IgniteButton steps (chamber glow, flame, shake, audio).")]
    public CombustionController combustionController;

    [Tooltip("Slow Motion controller — triggers slow-mo before an IgniteButton step if triggerSlowMotion is true.")]
    public SlowMotionController slowMotionController;

    [Tooltip("Engine Audio controller for audio crossfade during IgniteButton steps.")]
    public EngineAudioController engineAudioController;

    [Tooltip("Optional part glow controller for highlighting the target part during PartTap steps.")]
    public PartGlowController partGlowController;

    // ── VFX Timing ─────────────────────────────────────────────────────────────
    [Header("VFX Timing")]
    [Tooltip("Duration override for the step's VFX animation. 0 = use default from controller.")]
    public float vfxDuration = 0f;

    [Tooltip("If true, triggers slow-motion effect for this step (typically IgniteButton).")]
    public bool triggerSlowMotion = false;

    // ── Audio ──────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("Optional: airflow tube progress to set when this step activates (0..1). -1 = no change.")]
    public float airflowProgress = -1f;

    [Tooltip("Optional: audio clip for this step's narration.")]
    public AudioClip stepAudio;

    [Tooltip("Optional: audio clip for this step's narration (string lookup via SimplePartExplorer).")]
    public string audioNameOverride;

    [Tooltip("Optional: audio clip played specifically when the turbine starts spinning (TurbineStart).")]
    public AudioClip turbineStartAudio;

    // ── Ignition Audio ─────────────────────────────────────────────────────────
    [Header("Ignition Audio")]
    [Tooltip("Optional: audio clip played when the Ignite button is pressed (IgniteButton).")]
    public AudioClip ignitionButtonAudio;
}