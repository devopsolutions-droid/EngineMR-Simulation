using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Context object passed to all step handlers.
/// Contains all shared references and state that handlers need to execute.
/// </summary>
public class StepContext
{
    // ── Shared references ──────────────────────────────────────────────────────
    public SimplePartExplorer partExplorer;
    public JetEngineShowWorking showWorking;
    public EngineGrabManager grabManager;
    public AudioSource audioSource;

    // ── Settings ───────────────────────────────────────────────────────────────
    public float defaultAdvanceDistance = 0.3f;
    public float liftDuration = 0.35f;
    public float liftAmount = 0.4f;
    public float advanceDelay = 0.5f;
    public float highlightScale = 0f;

    // ── UI Buttons ─────────────────────────────────────────────────────────────
    public GameObject startTurbineButton;
    public GameObject igniteButton;

    // ── Runtime state ──────────────────────────────────────────────────────────
    public int currentStepIndex;
    public float airflowAtStepStart;

    // ── GrabRemove state ───────────────────────────────────────────────────────
    public GameObject currentTargetPart;
    public Vector3 targetOriginalPosition;
    public Transform targetOriginalParent;
    public bool correctPartGrabbed;
    public EnginePart currentEnginePart;
    public EnginePartGrabController currentGrabController;

    // ── PartTap state ──────────────────────────────────────────────────────────
    public GameObject tapTargetPart;

    // ── Turbine state ──────────────────────────────────────────────────────────
    public TurbineBladeRotator turbineBladeRotator;
    public bool turbineWasStarted;

    // ── BladeSpin state ────────────────────────────────────────────────────────
    public List<RuntimeBladeGroup> activeBladeGroups = new List<RuntimeBladeGroup>();

    // ── Original positions dictionary ──────────────────────────────────────────
    public Dictionary<GameObject, (Vector3 pos, Transform parent)> originalPositions
        = new Dictionary<GameObject, (Vector3, Transform)>();

    // ── Callbacks (set by the controller) ─────────────────────────────────────
    public System.Action<ShowWorkingStep> playStepAudio;
    public System.Action completeAndAdvance;
    public System.Action<ShowWorkingStep> showNavigationUI;
}