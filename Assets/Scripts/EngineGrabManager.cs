// This script manages the XR raycast and thumbstick depth inputs for grabbing engine parts.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the grab loop for all engine parts.
/// Place ONE instance of this anywhere in the engine scene.
///
/// How it works:
///   Every frame, casts a ray from the XRRayInteractor.
///   On trigger down  → records the hit part + the world-space depth of the hit point
///   Every frame held → X/Y follow the ray (position only, no rotation)
///                      Z is driven by the joystick (thumbstick forward / back)
///   On trigger up    → releases, part stays in place
///
/// Rules enforced:
///   • Only ONE part grabbed at a time
///   • Part never jumps — it moves from where it already is
///   • Part never rotates — only position changes
///   • Grabbing is blocked while EngineInteractor has an active selected part
///     (i.e. isolation / ghost mode) so the two systems don't conflict
/// </summary>
public class EngineGrabManager : MonoBehaviour
{
    [Header("Assembly UI (Tablet)")]
    [Tooltip("Drag the tablet's title text component here.")]
    public TMPro.TextMeshProUGUI stepNameText;
    
    [Tooltip("Drag the tablet's description text component here.")]
    public TMPro.TextMeshProUGUI stepDescriptionText;

    [Tooltip("Audio source to play the step's voiceover/audio clip.")]
    public AudioSource stepAudioSource;

    [Tooltip("Audio clip to play when all assembly steps are completed.")]
    public AudioClip assemblyCompleteAudio;

    [Header("Grab Sound")]
    [Tooltip("Drag any AudioClip here — plays every time the user grabs an engine part.")]
    public AudioClip grabSound;

    [Tooltip("AudioSource used to play the grab sound. Auto-created at runtime if left empty.")]
    public AudioSource grabAudioSource;

    [Tooltip("Drag any AudioClip here — plays when the ray hovers over a grabbable part.")]
    public AudioClip hoverSound;

    [Tooltip("AudioSource used to play the hover sound. Auto-created at runtime if left empty.")]
    public AudioSource hoverAudioSource;

    [Header("Background Music")]
    [Tooltip("Drag a music AudioClip here — it will loop automatically for the entire scene.")]
    public AudioClip bgMusic;

    [Tooltip("Volume of the background music. 0 = silent, 1 = full volume.")]
    [Range(0f, 1f)]
    public float bgMusicVolume = 0.3f;

    [Tooltip("AudioSource used for background music. Auto-created at runtime if left empty.")]
    public AudioSource bgMusicSource;

    private AssemblyStep[] _assemblySteps;
    private int            _currentStepIndex = -1;

    public bool IsStepByStepActive => _assemblySteps != null && _currentStepIndex >= 0;

    /// <summary>
    /// Starts step-by-step assembly. Only the part at index 0 is grabbable.
    /// Pass null or empty array to disable step enforcement (free grab mode).
    /// </summary>
    public void StartStepByStepAssembly(AssemblyStep[] steps)
    {
        allowGrouping  = true;
        _assemblySteps  = (steps != null && steps.Length > 0) ? steps : null;
        _currentStepIndex = (_assemblySteps != null) ? 0 : -1;
        if (_assemblySteps != null)
        {
            Debug.Log($"[EngineGrabManager] Step-by-step assembly started. Step 1/{_assemblySteps.Length}: {_assemblySteps[0].stepName} ({_assemblySteps[0].part?.name})");
            UpdateAssemblyUI();
        }
    }

    public void StopStepByStepAssembly()
    {
        _assemblySteps    = null;
        _currentStepIndex = -1;
        
        if (stepNameText != null) stepNameText.text = "Reassemble Engine";
        if (stepDescriptionText != null) stepDescriptionText.text = "Grab the highlighted parts and place them in the correct sequence.";
        if (stepAudioSource != null && stepAudioSource.isPlaying) stepAudioSource.Stop();
    }

    private void UpdateAssemblyUI()
    {
        if (_assemblySteps != null && _currentStepIndex >= 0 && _currentStepIndex < _assemblySteps.Length)
        {
            if (stepNameText != null) stepNameText.text = _assemblySteps[_currentStepIndex].stepName;
            if (stepDescriptionText != null) stepDescriptionText.text = _assemblySteps[_currentStepIndex].stepDescription;
            
            // Auto-fallback to the same AudioSource used by separated/exploded view
            if (stepAudioSource == null)
            {
                var interactor = FindFirstObjectByType<EngineInteractor>();
                if (interactor != null) stepAudioSource = interactor.GetComponent<AudioSource>();
            }

            if (stepAudioSource != null && _assemblySteps[_currentStepIndex].stepAudio != null)
            {
                if (!stepAudioSource.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning("[EngineGrabManager] The Audio Source is attached to a disabled GameObject! It cannot play sound.");
                }
                else
                {
                    stepAudioSource.Stop();
                    stepAudioSource.clip = _assemblySteps[_currentStepIndex].stepAudio;
                    stepAudioSource.Play();
                    Debug.Log($"[EngineGrabManager] Playing step audio: {stepAudioSource.clip.name} using {stepAudioSource.gameObject.name}");
                }
            }
            else if (stepAudioSource == null && _assemblySteps[_currentStepIndex].stepAudio != null)
            {
                Debug.LogWarning("[EngineGrabManager] You have an audio clip for this step, but no AudioSource could be found!");
            }
        }
    }

    public bool IsCurrentStepPart(EnginePart part)
    {
        if (part == null) return false;
        if (_assemblySteps == null || _currentStepIndex < 0) return true; // no steps = free grab
        if (_currentStepIndex >= _assemblySteps.Length) return false;     // all steps done

        GameObject targetPart = _assemblySteps[_currentStepIndex].part;
        if (targetPart == part.gameObject) return true;

        // Allow grabbing if the part belongs to a group that contains the target part
        if (allowGrouping)
        {
            foreach (var p in part.GetGroupParts())
            {
                if (p != null && p.gameObject == targetPart) return true;
            }
        }

        return false;
    }

    private bool IsCurrentStepPart(EnginePartGrabController grab)
    {
        if (grab == null) return false;
        return IsCurrentStepPart(grab.GetComponent<EnginePart>());
    }

    private void AdvanceStep()
    {
        if (_assemblySteps == null) return;
        _currentStepIndex++;
        int total = _assemblySteps.Length;
        OnStepCompleted?.Invoke(_currentStepIndex, total);
        if (_currentStepIndex < total)
        {
            Debug.Log($"[EngineGrabManager] Step {_currentStepIndex + 1}/{total}: {_assemblySteps[_currentStepIndex].stepName} ({_assemblySteps[_currentStepIndex].part?.name})");
            UpdateAssemblyUI();
        }
        else
        {
            Debug.Log("[EngineGrabManager] All assembly steps complete!");
            if (stepNameText != null) stepNameText.text = "Assembly Complete";
            if (stepDescriptionText != null) stepDescriptionText.text = "Congratulations! Your assembly is complete. All parts have been successfully placed.";

            // Play completion audio if configured
            if (stepAudioSource != null && assemblyCompleteAudio != null)
            {
                stepAudioSource.Stop();
                stepAudioSource.clip = assemblyCompleteAudio;
                stepAudioSource.Play();
            }
        }
    }

    // ── Events for Show Working Interactive mode ──────────────────────────────
    /// <summary>Fired when ANY part is grabbed (including Show Working interactive).</summary>
    public System.Action<EnginePartGrabController> OnGrabStarted;

    /// <summary>Fired when ANY part is released (including Show Working interactive).</summary>
    public System.Action<EnginePartGrabController> OnGrabEnded;

    /// <summary>Fired when a step snaps. Args: (stepsCompleted, totalSteps).</summary>
    public System.Action<int, int> OnStepCompleted;

    /// <summary>
    /// When true, the grab system works even when IsGrabModeActive is false.
    /// Set by ShowWorkingInteractiveController so the user can grab parts during
    /// the interactive flow without entering full Grab Mode.
    /// </summary>
    [HideInInspector]
    public bool allowGrabbing = false;

    /// <summary>
    /// When true, parts will attempt to snap to their assembled positions.
    /// Set to false in Manual Separate Mode.
    /// </summary>
    [HideInInspector]
    public bool allowSnapping = true;

    /// <summary>
    /// When true, parts in a group are grabbed and moved together.
    /// Set to false in Manual Separate Mode so each part can be grabbed and moved individually.
    /// </summary>
    [HideInInspector]
    public bool allowGrouping = true;

    [Header("References")]
    [Tooltip("The XRRayInteractor on the right controller.")]
    public XRRayInteractor rayInteractor;

    [Tooltip("The InputActionReference for the grab/trigger button.")]
    public InputActionReference grabAction;

    [Tooltip("Optional thumbstick action for Z (XRI Left/Right Hand Move). " +
             "Leave empty to read controllers directly — recommended.")]
    public InputActionReference depthAction;

    [Header("Layer")]
    [Tooltip("Must match the EngineParts layer set in EngineInteractor.")]
    public LayerMask enginePartsLayer = ~0;

    [Header("Settings")]
    [Tooltip("How smoothly the part follows the ray on X/Y. 1 = instant, 0.1 = very smooth/laggy.")]
    [Range(0.05f, 1f)]
    public float followSpeed = 0.35f;

    [Tooltip("World-space Z movement speed (m/s) at full thumbstick deflection.")]
    [Min(0.01f)]
    public float depthMoveSpeed = 0.8f;

    public enum DepthStickHand
    {
        Left,
        Right,
        BothUseStrongest
    }

    [Tooltip("Which controller thumbstick moves Z.")]
    public DepthStickHand depthStickHand = DepthStickHand.Left;

    [Tooltip("Which axis of the thumbstick Vector2 drives Z (0 = X, 1 = Y). Y = forward/back on most controllers.")]
    [Range(0, 1)]
    public int depthStickAxis = 1;

    [Tooltip("Flip thumbstick direction for Z.")]
    public bool invertDepthAxis = false;

    [Tooltip("Ignore thumbstick input below this magnitude.")]
    [Range(0f, 0.5f)]
    public float depthInputDeadzone = 0.08f;

    [Header("Locomotion")]
    [Tooltip("Disable XR walk/turn while a part is held so the thumbstick only moves the part on Z.")]
    public bool disableLocomotionWhileGrabbing = true;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private EnginePartGrabController _grabbed;      // currently grabbed part
    private System.Collections.Generic.List<EnginePartGrabController> _grabbedGroup; // all parts in group
    private Vector3[]                _grabOffsets;  // offset from part center to hit point
    private float[]                  _grabZs;       // initial Z position for each part
    private float                    _grabDepth;    // world-space depth at grab time
    private float                    _grabZDelta;   // Z movement delta
    private EnginePartGrabController _hovered;      // part the ray is currently over

    // ── Snap-to-Assembly ──────────────────────────────────────────────────────
    private EnginePartSnapController _grabbedSnap;      // snap controller on the grabbed part (null if part has no snap)
    private SnapZoneIndicator        _grabbedIndicator; // visual snap zone indicator on the grabbed part

    private bool _triggerHeld = false;

    private bool _locomotionSuppressed;
    private readonly List<LocomotionBackup> _locomotionBackups = new();

    private struct LocomotionBackup
    {
        public MonoBehaviour Component;
        public bool WasEnabled;
        public float SavedSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Enforce using the Left hand thumbstick for Z movement
        depthStickHand = DepthStickHand.Left;
        ResolveDepthActionFallback();

        // Auto-create a dedicated AudioSource for grab sounds if none assigned
        if (grabAudioSource == null)
        {
            grabAudioSource = gameObject.AddComponent<AudioSource>();
            grabAudioSource.playOnAwake = false;
            grabAudioSource.spatialBlend = 0f;
            grabAudioSource.volume = 1f;
        }

        // Auto-create a dedicated AudioSource for hover sounds if none assigned
        if (hoverAudioSource == null)
        {
            hoverAudioSource = gameObject.AddComponent<AudioSource>();
            hoverAudioSource.playOnAwake = false;
            hoverAudioSource.spatialBlend = 0f;
            hoverAudioSource.volume = 1f;
        }

        // Auto-create and start looping background music
        if (bgMusicSource == null)
        {
            bgMusicSource = gameObject.AddComponent<AudioSource>();
            bgMusicSource.playOnAwake = false;
            bgMusicSource.spatialBlend = 0f;
        }
        bgMusicSource.loop = true;
        bgMusicSource.volume = bgMusicVolume;
        if (bgMusic != null)
        {
            bgMusicSource.clip = bgMusic;
            bgMusicSource.Play();
        }

        // Pre-warm grab & hover AudioSources to eliminate first-play delay.
        // Unity buffers audio on first use — playing at volume 0 forces
        // it to initialize immediately so all subsequent calls are instant.
        PreWarmAudioSource(grabAudioSource, grabSound);
        PreWarmAudioSource(hoverAudioSource, hoverSound);
    }

    private void PreWarmAudioSource(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.volume = 0f;
        source.Play();
        source.Stop();
        source.volume = 1f;
        source.clip = null;
    }

    void OnEnable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed += OnTriggerDown;
            grabAction.action.canceled  += OnTriggerUp;
            grabAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (grabAction != null)
        {
            grabAction.action.performed -= OnTriggerDown;
            grabAction.action.canceled  -= OnTriggerUp;
        }

        ReleaseGrab();
    }

    void Update()
    {
        if (rayInteractor == null) return;

        // ── While a part is grabbed: move it ──────────────────────────────────
        if (_grabbed != null && _triggerHeld)
        {
            MoveGrabbedPart();
            return;   // skip hover logic while holding
        }

        // ── No grab active: update hover highlight ────────────────────────────
        UpdateHover();
    }

    // ── Trigger input ─────────────────────────────────────────────────────────

    private void OnTriggerDown(InputAction.CallbackContext ctx)
    {
        _triggerHeld = true;

        // Allow grabbing if: Grab Mode is active, OR allowGrabbing is true (Show Working interactive)
        if (!EngineViewManager.IsGrabModeActive && !allowGrabbing) return;

        // Don't grab if EngineInteractor is in isolation mode
        if (IsInteractorBusy()) return;

        if (!TryRaycast(out RaycastHit hit)) return;

        var grab = hit.collider.GetComponentInParent<EnginePartGrabController>();
        if (grab == null) return;

        // During Show Working, only allow grabbing parts marked by the active controller
        if (allowGrabbing && !grab.grabbableInShowWorking) return;

        // ── Step-by-step: only allow grabbing the current step part ──────────
        if (!IsCurrentStepPart(grab)) return;

        // Release any previously grabbed part first
        ReleaseGrab();

        // Record grab
        _grabbed = grab;
        _grabZDelta = 0f;

        var groupParts = allowGrouping
            ? _grabbed.GetComponent<EnginePart>().GetGroupParts()
            : new System.Collections.Generic.List<EnginePart> { _grabbed.GetComponent<EnginePart>() };
        _grabbedGroup = new System.Collections.Generic.List<EnginePartGrabController>();
        foreach (var p in groupParts)
        {
            var g = p.GetComponent<EnginePartGrabController>();
            if (g != null) _grabbedGroup.Add(g);
        }

        _grabOffsets = new Vector3[_grabbedGroup.Count];
        _grabZs = new float[_grabbedGroup.Count];

        for (int i = 0; i < _grabbedGroup.Count; i++)
        {
            _grabOffsets[i] = hit.point - _grabbedGroup[i].transform.position;
            _grabZs[i] = _grabbedGroup[i].transform.position.z;
            _grabbedGroup[i].OnGrabStart();
        }

        // Calculate depth: where the hit point sits on the ray
        Vector3 rayOrigin = GetRayOrigin();
        Vector3 rayDir    = GetRayDirection();
        Vector3 toHitPoint = hit.point - rayOrigin;
        _grabDepth = Vector3.Dot(toHitPoint, rayDir);

        // Clear hover on the grabbed part
        if (_hovered == grab)
        {
            _hovered = null;
        }

        // ── Fire public event (used by ShowWorkingInteractiveController) ─────
        OnGrabStarted?.Invoke(_grabbed);

        // ── Play grab sound ──────────────────────────────────────────────────
        if (grabSound != null && grabAudioSource != null)
        {
            grabAudioSource.PlayOneShot(grabSound);
        }

        // ── Snap-to-Assembly: skip if allowGrabbing (Show Working interactive) ──
        // In interactive mode, we don't want snap indicators or ghost visuals.
        if (!allowGrabbing && allowSnapping)
        {
            _grabbedSnap = grab.GetComponent<EnginePartSnapController>();
            if (_grabbedSnap != null)
            {
                _grabbedSnap.ResetSnap(); // part may have been snapped in a previous session

                // Show SnapZoneIndicator (thin ring at the target)
                _grabbedIndicator = grab.GetComponentInChildren<SnapZoneIndicator>(true);
                if (_grabbedIndicator != null)
                    _grabbedIndicator.Show(_grabbedSnap.SnapTargetWorld);

                // Create and show SnapGhost (semi-transparent mesh outline at target)
                _grabbedSnap.CreateSnapGhost();
                _grabbedSnap.ShowSnapGhost();
            }
        }

        SuppressLocomotion();
        Debug.Log($"[EngineGrabManager] Grabbed: {grab.gameObject.name} at depth {_grabDepth:F2}m, offset {_grabOffsets[0]}");
    }

    private void OnTriggerUp(InputAction.CallbackContext ctx)
    {
        _triggerHeld = false;
        ReleaseGrab();
    }

    // ── Move grabbed part ─────────────────────────────────────────────────────

    private void MoveGrabbedPart()
    {
        if (_grabbed == null) return;

        // ── Z: joystick forward / back (independent of the ray) ─────────────────
        float stickInput = ReadDepthStickInput();
        if (Mathf.Abs(stickInput) > depthInputDeadzone)
            _grabZDelta += stickInput * depthMoveSpeed * Time.deltaTime;

        Vector3 origin         = GetRayOrigin();
        Vector3 direction      = GetRayDirection();
        Vector3 hitPointTarget = origin + direction * _grabDepth;

        int primaryIndex = _grabbedGroup.IndexOf(_grabbed);
        if (primaryIndex < 0) primaryIndex = 0;

        Vector3 pTarget = hitPointTarget - _grabOffsets[primaryIndex];
        Vector3 pNext = new Vector3(
            Mathf.Lerp(_grabbed.transform.position.x, pTarget.x, followSpeed),
            Mathf.Lerp(_grabbed.transform.position.y, pTarget.y, followSpeed),
            _grabZs[primaryIndex] + _grabZDelta
        );

        Vector3 pull = Vector3.zero;
        if (allowSnapping && _grabbedSnap != null && !_grabbedSnap.IsSnapped)
        {
            pull = _grabbedSnap.GetMagneticPull(pNext);
            if (pull.sqrMagnitude > 0.0001f)
                pull *= Time.deltaTime * 60f;

            _grabbedSnap.UpdateSnapGhost();
        }

        // Apply to all grouped parts
        for (int i = 0; i < _grabbedGroup.Count; i++)
        {
            var g = _grabbedGroup[i];
            Vector3 target = hitPointTarget - _grabOffsets[i];
            Vector3 next = new Vector3(
                Mathf.Lerp(g.transform.position.x, target.x, followSpeed),
                Mathf.Lerp(g.transform.position.y, target.y, followSpeed),
                _grabZs[i] + _grabZDelta
            );
            
            next += pull;
            g.transform.position = next;
        }

        // ── Snap-to-Assembly: check proximity & auto-release on snap ────────
        if (allowSnapping && _grabbedSnap != null && !_grabbedSnap.IsSnapped)
        {
            if (_grabbedIndicator != null)
                _grabbedIndicator.UpdateApproach(pNext + pull);

            if (_grabbedSnap.TrySnap())
            {
                if (_grabbedIndicator != null)
                    _grabbedIndicator.OnSnapped();

                foreach (var g in _grabbedGroup)
                {
                    var s = g.GetComponent<EnginePartSnapController>();
                    if (s != null && s != _grabbedSnap) s.ForceSnap();
                    g.OnGrabEnd();
                }
                
                _triggerHeld = false;
                AdvanceStep();

                _grabbed = null;
                _grabbedGroup = null;
                _grabbedSnap = null;
                _grabbedIndicator = null;

                RestoreLocomotion();
            }
        }
    }

    /// <summary>
    /// Reads thumbstick for Z. Prefers direct XR hardware (always works when a controller is connected),
    /// then falls back to the optional InputActionReference.
    /// </summary>
    private float ReadDepthStickInput()
    {
        float xr = ReadDepthFromXRDevices();
        if (Mathf.Abs(xr) > depthInputDeadzone)
            return xr;

        return ReadDepthFromAction();
    }

    private float ReadDepthFromXRDevices()
    {
        float best = 0f;

        switch (depthStickHand)
        {
            case DepthStickHand.Left:
                TryReadThumbstick(XRNode.LeftHand, ref best);
                break;
            case DepthStickHand.Right:
                TryReadThumbstick(XRNode.RightHand, ref best);
                break;
            default:
                TryReadThumbstick(XRNode.LeftHand, ref best);
                TryReadThumbstick(XRNode.RightHand, ref best);
                break;
        }

        return best;
    }

    private void TryReadThumbstick(XRNode node, ref float best)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;
        if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis)) return;

        float val = depthStickAxis == 0 ? axis.x : axis.y;
        if (invertDepthAxis) val = -val;

        if (Mathf.Abs(val) > Mathf.Abs(best))
            best = val;
    }

    private float ReadDepthFromAction()
    {
        if (depthAction == null || depthAction.action == null) return 0f;

        var action = depthAction.action;
        if (!action.enabled)
            action.Enable();

        float raw;
        try
        {
            Vector2 stick = action.ReadValue<Vector2>();
            raw = depthStickAxis == 0 ? stick.x : stick.y;
        }
        catch
        {
            raw = action.ReadValue<float>();
        }

        return invertDepthAxis ? -raw : raw;
    }

    /// <summary>
    /// If depthAction is not assigned, use the Move action from the other controller
    /// (ray is usually on the right hand → left thumbstick for Z).
    /// </summary>
    private void ResolveDepthActionFallback()
    {
        if (depthAction != null) return;

        EngineInteractor onRay = null;
        if (rayInteractor != null)
        {
            onRay = rayInteractor.GetComponent<EngineInteractor>();
            if (onRay == null)
                onRay = rayInteractor.GetComponentInParent<EngineInteractor>();
        }

        foreach (var ei in FindObjectsByType<EngineInteractor>(FindObjectsSortMode.None))
        {
            if (ei == null || ei.moveAction == null || ei == onRay) continue;
            depthAction = ei.moveAction;
            return;
        }

        if (onRay != null && onRay.moveAction != null)
            depthAction = onRay.moveAction;
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    private void UpdateHover()
    {
        // In grab mode, don't show hover panels or audio — just outline + hover sound
        if (EngineViewManager.IsGrabModeActive)
        {
            EnginePartGrabController hitGrab = null;

            if (TryRaycast(out RaycastHit hit))
                hitGrab = hit.collider.GetComponentInParent<EnginePartGrabController>();

            if (hitGrab == _hovered) return;

            // Exit previous hover
            _hovered?.OnHoverExit();

            // Enter new hover
            _hovered = hitGrab;
            _hovered?.OnHoverEnter();

            // Play hover sound on enter
            if (_hovered != null && hoverSound != null && hoverAudioSource != null)
                hoverAudioSource.PlayOneShot(hoverSound);

            return;
        }

        // Normal mode: show hover panels (handled by EngineInteractor)
        // We don't manage hover in normal mode — EngineInteractor does
    }

    // ── Release ───────────────────────────────────────────────────────────────

    private void ReleaseGrab()
    {
        RestoreLocomotion();

        if (_grabbedGroup == null || _grabbedGroup.Count == 0)
        {
            if (_grabbedSnap != null)
                _grabbedSnap.ClearSnapGhost();
            _grabbedSnap = null;
            _grabbedIndicator = null;
            return;
        }

        if (allowSnapping && _grabbedSnap != null && !_grabbedSnap.IsSnapped)
        {
            if (_grabbedSnap.TrySnap())
            {
                if (_grabbedIndicator != null)
                    _grabbedIndicator.OnSnapped();

                foreach (var g in _grabbedGroup)
                {
                    var s = g.GetComponent<EnginePartSnapController>();
                    if (s != null && s != _grabbedSnap) s.ForceSnap();
                }

                AdvanceStep();
            }
            else
            {
                if (_grabbedIndicator != null)
                    _grabbedIndicator.Hide();

                _grabbedSnap.ClearSnapGhost();
            }
        }
        else
        {
            if (_grabbedIndicator != null)
                _grabbedIndicator.Hide();

            if (_grabbedSnap != null)
                _grabbedSnap.ClearSnapGhost();
        }

        foreach (var g in _grabbedGroup)
        {
            g.OnGrabEnd();
        }

        var releasedPart = _grabbed;
        OnGrabEnded?.Invoke(releasedPart);

        Debug.Log($"[EngineGrabManager] Released: {(_grabbed != null ? _grabbed.gameObject.name : "null")}");

        _grabbed = null;
        _grabbedGroup = null;
        _grabbedSnap = null;
        _grabbedIndicator = null;
    }

    private void SuppressLocomotion()
    {
        if (!disableLocomotionWhileGrabbing || _locomotionSuppressed) return;

        foreach (var move in FindObjectsByType<ActionBasedContinuousMoveProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = move,
                WasEnabled = move.enabled,
                SavedSpeed = move.moveSpeed
            });
            move.moveSpeed = 0f;
            move.enabled = false;
        }

        foreach (var turn in FindObjectsByType<ActionBasedContinuousTurnProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = turn,
                WasEnabled = turn.enabled,
                SavedSpeed = turn.turnSpeed
            });
            turn.turnSpeed = 0f;
            turn.enabled = false;
        }

        foreach (var snap in FindObjectsByType<ActionBasedSnapTurnProvider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            _locomotionBackups.Add(new LocomotionBackup
            {
                Component = snap,
                WasEnabled = snap.enabled,
                SavedSpeed = 0f
            });
            snap.enabled = false;
        }

        _locomotionSuppressed = true;
    }

    private void RestoreLocomotion()
    {
        if (!_locomotionSuppressed) return;

        foreach (var backup in _locomotionBackups)
        {
            if (backup.Component == null) continue;

            switch (backup.Component)
            {
                case ActionBasedContinuousMoveProvider move:
                    move.moveSpeed = backup.SavedSpeed;
                    break;
                case ActionBasedContinuousTurnProvider turn:
                    turn.turnSpeed = backup.SavedSpeed;
                    break;
            }

            backup.Component.enabled = backup.WasEnabled;
        }

        _locomotionBackups.Clear();
        _locomotionSuppressed = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryRaycast(out RaycastHit hit)
    {
        hit = default;
        if (rayInteractor == null) return false;

        // Use the XRRayInteractor's current 3D raycast hit if available
        if (rayInteractor.TryGetCurrent3DRaycastHit(out hit))
        {
            // Only count hits on the EngineParts layer
            if ((enginePartsLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                return true;
        }

        return false;
    }

    private Vector3 GetRayOrigin()
    {
        // XRRayInteractor exposes the ray origin via its transform
        return rayInteractor.transform.position;
    }

    private Vector3 GetRayDirection()
    {
        // The ray points forward from the interactor's transform
        return rayInteractor.transform.forward;
    }

    /// <summary>
    /// Returns true if EngineInteractor currently has a part selected (isolation mode).
    /// In that state the trigger is owned by EngineInteractor, not us.
    /// </summary>
    private bool IsInteractorBusy()
    {
        var interactor = FindFirstObjectByType<EngineInteractor>();
        return interactor != null && interactor.HasActivePart;
    }
}
