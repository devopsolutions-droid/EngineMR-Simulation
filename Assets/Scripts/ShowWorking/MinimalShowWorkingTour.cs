using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

[System.Serializable]
public class SimpleTourStep
{
    [Tooltip("Step title shown in the tablet UI.")]
    public string stepName = "Step";

    [Tooltip("Step description shown in the tablet UI.")]
    [TextArea(2, 4)]
    public string instruction = "Press Next to continue.";

    [Tooltip("Audio clip for this step's narration.")]
    public AudioClip stepAudio;

    [Tooltip("Engine parts to highlight on this step (shown with outline/glow).")]
    public GameObject[] highlightParts;

    [Tooltip("Direct hover panel GameObjects to display on this step.")]
    public GameObject[] hoverPanels;

    [Tooltip("GameObjects to activate when this step starts, and deactivate when it ends.")]
    public GameObject[] activateOnStepStart;

    [Tooltip("Optional graph GameObject to show during this step.")]
    public GameObject graphObject;

    [Tooltip("Optional Animator to play an animation clip when this step starts.")]
    public Animator stepAnimator;

    [Tooltip("Optional Animation Clip to play on the Animator when this step starts.")]
    public AnimationClip stepAnimationClip;

    [Header("Water Progress")]
    [Range(0f, 1f)]
    [Tooltip("Water flow progress for this step (0 = empty, 1 = full flow).")]
    public float waterProgress = 0f;
}

[System.Serializable]
public class RotationGroup
{
    [Tooltip("Group name for organization in the inspector.")]
    public string groupName = "Rotation Group";

    [Tooltip("Target GameObjects (e.g. gears, shafts) in this group.")]
    public GameObject[] targets = new GameObject[0];

    [Tooltip("Local rotation axis for this group.")]
    public Vector3 rotationAxis = Vector3.forward;

    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 45f;
}

/// <summary>
/// Simplified, generic tour controller for engine walkthroughs.
/// Rotates target blades when active, and displays step name, instruction,
/// activates GameObjects, and shows hover panels for each step.
/// </summary>
public class MinimalShowWorkingTour : MonoBehaviour, IShowWorkingController
{
    [Header("Steps")]
    public SimpleTourStep[] steps;

    [Header("Model Validation")]
    [Tooltip("Assign an EngineData asset here to restrict this script to a specific engine model. Leave empty for no restriction.")]
    [FormerlySerializedAs("jetEngineData")]
    public EngineData targetEngineData;

    [Header("Continuous Rotation (Legacy)")]
    [Tooltip("Target GameObjects (e.g. blades, flywheels, gears) that should spin continuously while the tour is running.")]
    public GameObject[] rotatingObjects = new GameObject[0];
    public Vector3 rotationAxis = Vector3.forward;
    public float rotationSpeed = 45f;

    [Header("Continuous Rotation Groups")]
    [Tooltip("Define multiple groups of objects to rotate at different speeds and axes.")]
    public List<RotationGroup> rotationGroups = new List<RotationGroup>();

    private Dictionary<GameObject, float> _accumulatedRotations = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, Quaternion> _originalRotations = new Dictionary<GameObject, Quaternion>();

    [Header("Hiding Objects")]
    [Tooltip("Drag and drop GameObjects here that you want to hide when Show Working starts, and restore when it stops.")]
    public List<GameObject> objectsToHide = new List<GameObject>();

    [Header("Audio")]
    [Tooltip("AudioSource used to play step narration clips. Auto-discovered if left empty.")]
    public AudioSource audioSource;

    [Header("Graph Display")]
    [Tooltip("The shared Graph Display Panel GameObject (like a TV/monitor screen).")]
    public GameObject graphDisplayPanel;

    [Header("Water Visuals")]
    [Tooltip("The GameObject containing the outlet water meshes (parent or individual mesh).")]
    public GameObject outletWaterObject;
    [Tooltip("The GameObject containing the outside water meshes (parent or individual mesh).")]
    public GameObject outsideWaterObject;
    [Tooltip("How fast the water fills the pipe (units per second).")]
    public float waterFillSpeed = 2f;

    private AudioSource _runtimeAudioSource;
    private int _currentStepIndex = -1;
    private bool _isRunning;
    private Dictionary<GameObject, bool> _hiddenObjectsOriginalState = new Dictionary<GameObject, bool>();
    private SimplePartExplorer _partExplorer;
    private EngineViewManager _viewManager;
    private float _currentWaterHeight = 0f;
    private float _targetWaterHeight = 0f;
    private Coroutine _waterAnimCoroutine;
    private MaterialPropertyBlock _waterMpb;
    private List<MeshRenderer> _outletRenderers = new List<MeshRenderer>();
    private List<MeshRenderer> _outsideRenderers = new List<MeshRenderer>();

    public bool IsRunning => _isRunning;

    private void EnsureAudioSource()
    {
        if (audioSource != null) return;
        if (_runtimeAudioSource != null) return;

        // Try to find a local AudioSource component on this GameObject first
        _runtimeAudioSource = GetComponent<AudioSource>();
        if (_runtimeAudioSource == null)
        {
            // Create a local 2D AudioSource so narration is always perfectly audible to the player
            _runtimeAudioSource = gameObject.AddComponent<AudioSource>();
            _runtimeAudioSource.playOnAwake = false;
            _runtimeAudioSource.spatialBlend = 0f; // 2D sound (non-spatialized, equal in both ears)
            Debug.Log($"[MinimalShowWorkingTour] Created runtime 2D AudioSource on '{gameObject.name}'");
        }
    }

    private void Start()
    {
        _partExplorer = FindFirstObjectByType<SimplePartExplorer>();
        _viewManager = FindFirstObjectByType<EngineViewManager>();
        
        EnsureAudioSource();

        DeactivateAllGraphObjects();
        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(false);
        }

        // Deactivate all step hover panels at startup to ensure they are hidden initially
        if (steps != null)
        {
            foreach (var step in steps)
            {
                if (step == null) continue;
                if (step.hoverPanels != null)
                {
                    foreach (var panel in step.hoverPanels)
                    {
                        if (panel != null) panel.SetActive(false);
                    }
                }
            }
        }

        _waterMpb = new MaterialPropertyBlock();

        if (outletWaterObject != null)
            _outletRenderers.AddRange(outletWaterObject.GetComponentsInChildren<MeshRenderer>(true));

        if (outsideWaterObject != null)
            _outsideRenderers.AddRange(outsideWaterObject.GetComponentsInChildren<MeshRenderer>(true));
    }

    private void LateUpdate()
    {
        if (!_isRunning) return;

        // 1. Process legacy rotatingObjects
        if (rotatingObjects != null && rotatingObjects.Length > 0)
        {
            float delta = rotationSpeed * Time.deltaTime;
            for (int i = 0; i < rotatingObjects.Length; i++)
            {
                GameObject go = rotatingObjects[i];
                if (go == null) continue;

                if (!_accumulatedRotations.TryGetValue(go, out float angle))
                {
                    angle = 0f;
                }
                angle += delta;
                _accumulatedRotations[go] = angle;

                if (_originalRotations.TryGetValue(go, out Quaternion orig))
                {
                    go.transform.localRotation = orig * Quaternion.AngleAxis(angle, rotationAxis);
                }
            }
        }

        // 2. Process rotationGroups
        if (rotationGroups != null && rotationGroups.Count > 0)
        {
            foreach (var group in rotationGroups)
            {
                if (group == null || group.targets == null || group.targets.Length == 0) continue;
                float delta = group.rotationSpeed * Time.deltaTime;
                for (int i = 0; i < group.targets.Length; i++)
                {
                    GameObject go = group.targets[i];
                    if (go == null) continue;

                    if (!_accumulatedRotations.TryGetValue(go, out float angle))
                    {
                        angle = 0f;
                    }
                    angle += delta;
                    _accumulatedRotations[go] = angle;

                    if (_originalRotations.TryGetValue(go, out Quaternion orig))
                    {
                        go.transform.localRotation = orig * Quaternion.AngleAxis(angle, group.rotationAxis);
                    }
                }
            }
        }
    }

    public void StartInteractiveFlow()
    {
        Debug.Log($"[MinimalShowWorkingTour] StartInteractiveFlow called on '{gameObject.name}'. IsActiveEngineValid={IsActiveEngineValid()}, _isRunning={_isRunning}");

        if (!IsActiveEngineValid())
        {
            Debug.LogWarning($"[MinimalShowWorkingTour] IsActiveEngineValid() returned FALSE — engine mismatch or targetEngineData not matched. This engine won't run Show Working.");
            return;
        }
        if (_isRunning) { Debug.LogWarning("[MinimalShowWorkingTour] Already running, ignoring Start call."); return; }

        if (steps == null || steps.Length == 0)
        {
            Debug.LogError("[MinimalShowWorkingTour] No steps configured. Add steps in the Inspector.");
            return;
        }

        Debug.Log($"[MinimalShowWorkingTour] Starting flow with {steps.Length} steps.");
        _isRunning = true;
        _currentStepIndex = 0;

        // Hide specified GameObjects
        _hiddenObjectsOriginalState.Clear();
        foreach (var go in objectsToHide)
        {
            if (go != null)
            {
                _hiddenObjectsOriginalState[go] = go.activeSelf;
                go.SetActive(false);
            }
        }

        if (_waterAnimCoroutine != null) StopCoroutine(_waterAnimCoroutine);
        _currentWaterHeight = 0f;
        _targetWaterHeight = 0f;
        ApplyWaterProgressImmediate(0f);

        // Cache original rotations for rotatingObjects and groups
        _originalRotations.Clear();
        _accumulatedRotations.Clear();

        if (rotatingObjects != null)
        {
            foreach (var go in rotatingObjects)
            {
                if (go != null && !_originalRotations.ContainsKey(go))
                {
                    _originalRotations[go] = go.transform.localRotation;
                }
            }
        }

        if (rotationGroups != null)
        {
            foreach (var group in rotationGroups)
            {
                if (group == null || group.targets == null) continue;
                foreach (var go in group.targets)
                {
                    if (go != null && !_originalRotations.ContainsKey(go))
                    {
                        _originalRotations[go] = go.transform.localRotation;
                    }
                }
            }
        }

        ShowStep(_currentStepIndex);
    }

    public void StopInteractiveFlow()
    {
        if (!_isRunning) return;
        _isRunning = false;

        // Restore hidden GameObjects
        foreach (var go in objectsToHide)
        {
            if (go != null && _hiddenObjectsOriginalState.TryGetValue(go, out bool wasActive))
            {
                go.SetActive(wasActive);
            }
        }
        _hiddenObjectsOriginalState.Clear();

        // Restore original rotations of rotating objects
        foreach (var kvp in _originalRotations)
        {
            if (kvp.Key != null)
            {
                kvp.Key.transform.localRotation = kvp.Value;
            }
        }
        _originalRotations.Clear();
        _accumulatedRotations.Clear();

        // Stop playing audio
        AudioSource src = audioSource != null ? audioSource : _runtimeAudioSource;
        if (src != null)
        {
            src.Stop();
            src.clip = null;
        }

        // Deactivate all graph objects
        DeactivateAllGraphObjects();
        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(false);
        }

        // Deactivate all step visual objects and hover panels, and restore materials
        foreach (var step in steps)
        {
            if (step == null) continue;
            DeactivateStepVisuals(step);

            if (step.activateOnStepStart != null)
            {
                foreach (var go in step.activateOnStepStart)
                {
                    if (go != null) go.SetActive(false);
                }
            }
        }

        RestoreAllPartsVisuals();

        if (_waterAnimCoroutine != null) StopCoroutine(_waterAnimCoroutine);
        _currentWaterHeight = 0f;
        _targetWaterHeight = 0f;
        ApplyWaterProgressImmediate(0f);

        _currentStepIndex = -1;
    }

    public void OnNextPressed()
    {
        if (!_isRunning) { Debug.LogWarning("[MinimalShowWorkingTour] OnNextPressed called but flow is NOT running."); return; }

        Debug.Log($"[MinimalShowWorkingTour] OnNextPressed — current step {_currentStepIndex}, advancing.");

        // Deactivate visuals of current step
        if (_currentStepIndex >= 0 && _currentStepIndex < steps.Length)
        {
            DeactivateStepVisuals(steps[_currentStepIndex]);
        }

        _currentStepIndex++;
        if (_currentStepIndex >= steps.Length)
        {
            Debug.Log($"[MinimalShowWorkingTour] All {steps.Length} steps completed. Ending flow.");
            // Completed
            if (_viewManager != null)
                _viewManager.ActivateDefaultView();
            else
                StopInteractiveFlow();
            return;
        }

        ShowStep(_currentStepIndex);
    }

    public void OnPreviousPressed()
    {
        if (!_isRunning) { Debug.LogWarning("[MinimalShowWorkingTour] OnPreviousPressed called but flow is NOT running."); return; }
        if (_currentStepIndex <= 0) { Debug.LogWarning("[MinimalShowWorkingTour] Already at first step, cannot go back."); return; }

        Debug.Log($"[MinimalShowWorkingTour] OnPreviousPressed — going from step {_currentStepIndex} to {_currentStepIndex - 1}.");

        // Deactivate visuals of current step
        if (_currentStepIndex >= 0 && _currentStepIndex < steps.Length)
        {
            DeactivateStepVisuals(steps[_currentStepIndex]);
        }

        _currentStepIndex--;
        ShowStep(_currentStepIndex);
    }

    private void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Length) { Debug.LogError($"[MinimalShowWorkingTour] ShowStep index {index} out of range (0–{steps.Length - 1})."); return; }
        var step = steps[index];
        if (step == null) { Debug.LogError($"[MinimalShowWorkingTour] Step at index {index} is null!"); return; }

        Debug.Log($"[MinimalShowWorkingTour] === ShowStep [{index}] '{step.stepName}' ===");

        // Animate water progress
        _targetWaterHeight = step.waterProgress;
        if (_waterAnimCoroutine != null) StopCoroutine(_waterAnimCoroutine);
        _waterAnimCoroutine = StartCoroutine(AnimateWaterProgress(_targetWaterHeight));

        // Update tablet text
        if (_partExplorer != null)
        {
            _partExplorer.SetInteractiveUIText(
                string.IsNullOrEmpty(step.stepName) ? $"Step {index + 1}" : step.stepName,
                string.IsNullOrEmpty(step.instruction) ? "" : step.instruction,
                index + 1,
                steps.Length
            );
            Debug.Log($"[MinimalShowWorkingTour] Tablet UI updated for step {index}.");
        }
        else
        {
            Debug.LogWarning($"[MinimalShowWorkingTour] _partExplorer is NULL — tablet text will NOT update. Is SimplePartExplorer in the scene?");
        }

        // Play narration audio
        AudioSource src = audioSource != null ? audioSource : _runtimeAudioSource;
        if (src != null)
        {
            if (step.stepAudio != null)
            {
                if (src.isPlaying) src.Stop();
                src.clip = step.stepAudio;
                src.Play();
                Debug.Log($"[MinimalShowWorkingTour] Playing narration audio: '{step.stepAudio.name}'.");
            }
            else
            {
                src.Stop();
                src.clip = null;
                Debug.Log($"[MinimalShowWorkingTour] Step {index} has no stepAudio assigned — skipping audio.");
            }
        }
        else
        {
            Debug.LogWarning($"[MinimalShowWorkingTour] No AudioSource available for step {index}.");
        }

        // ── ANIMATION ──────────────────────────────────────────────────────────
        // Use animator.Play(stateName, 0, 0f) to FORCE the state from time=0
        // immediately. SetTrigger relies on the Animator's next Update cycle and
        // same-frame state reads are unreliable. State names in MS.controller
        // are MS0, MS1, MS2, MS3 — matching step index directly.
        if (step.stepAnimator != null)
        {
            string stateName = $"MS{index}";

            // Verify the state exists in the controller before playing
            bool stateExists = false;
            foreach (var clipInfo in step.stepAnimator.runtimeAnimatorController != null
                         ? new UnityEngine.RuntimeAnimatorController[] { step.stepAnimator.runtimeAnimatorController }
                         : new UnityEngine.RuntimeAnimatorController[0])
            {
                stateExists = true; // controller exists
            }
            stateExists = step.stepAnimator.runtimeAnimatorController != null;

            if (stateExists && index <= 3) // MS0-MS3 exist in the controller
            {
                if (index == 0)
                {
                    DumpHierarchyToFile(step.stepAnimator.transform);
                }

                // Force state play from beginning on layer 0
                step.stepAnimator.Play(stateName, 0, 0f);
                Debug.Log($"[MinimalShowWorkingTour] animator.Play('{stateName}', 0, 0f) called on Animator '{step.stepAnimator.name}'" +
                          $" | controller='{step.stepAnimator.runtimeAnimatorController.name}'");

                // Verify transition in 2 frames (after Animator has processed the Play call)
                StartCoroutine(VerifyAnimatorStateAfterPlay(step.stepAnimator, stateName, index));
            }
            else if (index > 3)
            {
                Debug.Log($"[MinimalShowWorkingTour] Step {index} ('{step.stepName}'): index > 3, no animation clip to play.");
            }
            else
            {
                Debug.LogWarning($"[MinimalShowWorkingTour] Step {index}: Animator has no runtimeAnimatorController — cannot play.");
            }
        }
        else
        {
            Debug.LogWarning($"[MinimalShowWorkingTour] Step {index} '{step.stepName}': stepAnimator is NULL — no animation will play.");

        }

        // Manage step activation objects dynamically based on the current step index (keeping past steps active)
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null || steps[i].activateOnStepStart == null) continue;
            foreach (var go in steps[i].activateOnStepStart)
            {
                if (go != null)
                {
                    go.SetActive(i <= index);
                }
            }
        }

        // Apply visual highlights
        if (step.highlightParts != null)
        {
            foreach (var part in step.highlightParts)
            {
                if (part == null) continue;

                var ep = part.GetComponent<EnginePart>();
                if (ep != null)
                {
                    ep.SetVisible(true);
                    ep.SetShowWorkingActive();
                }

                // Show hover panels attached to highlighted parts
                var panel = part.GetComponentInChildren<PartHoverPanel>(true);
                if (panel != null) panel.Show();
            }
        }

        // Show specified hover panels
        if (step.hoverPanels != null)
        {
            foreach (var go in step.hoverPanels)
            {
                if (go == null) continue;
                var panel = go.GetComponent<PartHoverPanel>();
                if (panel != null) panel.Show();
                else go.SetActive(true);
            }
        }

        // Manage graph object display for this step (no step limit, runs for all steps if a graph is configured)
        DeactivateAllGraphObjects();
        bool hasGraph = step.graphObject != null;
        if (graphDisplayPanel != null)
        {
            graphDisplayPanel.SetActive(hasGraph);
        }
        if (hasGraph)
        {
            step.graphObject.SetActive(true);
        }
    }

    private void DeactivateStepVisuals(SimpleTourStep step)
    {
        if (step.hoverPanels != null)
        {
            foreach (var go in step.hoverPanels)
            {
                if (go == null) continue;
                var panel = go.GetComponent<PartHoverPanel>();
                if (panel != null) panel.Hide();
                else go.SetActive(false);
            }
        }

        if (step.highlightParts != null)
        {
            foreach (var part in step.highlightParts)
            {
                if (part == null) continue;
                var panel = part.GetComponentInChildren<PartHoverPanel>(true);
                if (panel != null) panel.Hide();

                var ep = part.GetComponent<EnginePart>();
                if (ep != null)
                {
                    ep.RestoreOriginal();
                }
            }
        }
    }

    private void DeactivateAllGraphObjects()
    {
        if (steps == null) return;
        foreach (var s in steps)
        {
            if (s != null && s.graphObject != null)
            {
                s.graphObject.SetActive(false);
            }
        }
    }

    private void RestoreAllPartsVisuals()
    {
        foreach (var part in GetAllEngineParts())
        {
            if (part != null) part.RestoreOriginal();
        }
    }

    private IEnumerable<EnginePart> GetAllEngineParts()
    {
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader != null && loader.ActiveEngineRoot != null)
        {
            return loader.ActiveEngineRoot.GetComponentsInChildren<EnginePart>(true);
        }
        return FindObjectsByType<EnginePart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void DumpHierarchyToFile(Transform root)
    {
        try
        {
            string path = @"C:\Users\ADMIN\Desktop\Debojit\EngineVR Simulation\EngineVRSimulation\hierarchy_dump.txt";
            using (var writer = new System.IO.StreamWriter(path))
            {
                writer.WriteLine($"Hierarchy Dump for '{root.name}' at {System.DateTime.Now}");
                WriteNode(root, "", writer);
            }
            Debug.Log($"[MinimalShowWorkingTour] Dumped hierarchy to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MinimalShowWorkingTour] Failed to dump hierarchy: {e.Message}");
        }
    }

    private void WriteNode(Transform t, string indent, System.IO.StreamWriter writer)
    {
        writer.WriteLine($"{indent}{t.name}");
        for (int i = 0; i < t.childCount; i++)
        {
            WriteNode(t.GetChild(i), indent + "  ", writer);
        }
    }

    private System.Collections.IEnumerator VerifyAnimatorStateAfterPlay(Animator anim, string expectedState, int stepIndex)
    {
        yield return null; // wait frame 1
        yield return null; // wait frame 2

        if (anim == null) yield break;

        var stateInfo  = anim.GetCurrentAnimatorStateInfo(0);
        var clipInfos  = anim.GetCurrentAnimatorClipInfo(0);
        string clipName = clipInfos.Length > 0 ? clipInfos[0].clip.name : "NONE (no clip on state)";
        bool isExpected = stateInfo.IsName(expectedState);

        string avatarDesc = anim.avatar != null
            ? anim.avatar.name + (anim.avatar.isHuman ? " [HUMANOID]" : " [GENERIC]")
            : "NULL — no avatar!";

        Debug.Log($"[MinimalShowWorkingTour][DIAG-VERIFY] 2 frames after Play('{expectedState}'):" +
                  $"\n  IsInState('{expectedState}') = {isExpected}" +
                  $"\n  Current clip playing       = '{clipName}'" +
                  $"\n  normalizedTime             = {stateInfo.normalizedTime:F4}" +
                  $"\n  isLooping                  = {stateInfo.loop}" +
                  $"\n  isInTransition             = {anim.IsInTransition(0)}" +
                  $"\n  Avatar                     = '{avatarDesc}'");

        if (!isExpected)
            Debug.LogError($"[MinimalShowWorkingTour][DIAG-VERIFY] State did NOT transition to '{expectedState}'!");
        else
            Debug.Log($"[MinimalShowWorkingTour][DIAG-VERIFY] State '{expectedState}' confirmed at step {stepIndex}.");

        // Always log animated paths — finds hierarchy mismatches even when state IS correct
        #if UNITY_EDITOR
        if (clipInfos.Length > 0 && clipInfos[0].clip != null)
        {
            var clip = clipInfos[0].clip;
            var allBindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            var uniquePaths = new System.Collections.Generic.HashSet<string>();
            foreach (var b in allBindings) uniquePaths.Add(b.path);

            Debug.Log($"[MinimalShowWorkingTour][DIAG-PATH] Clip '{clip.name}': {uniquePaths.Count} unique paths. Animator root='{anim.gameObject.name}'");
            int shown = 0;
            foreach (var path in uniquePaths)
            {
                if (shown++ >= 10) { Debug.Log("[MinimalShowWorkingTour][DIAG-PATH]   ...(showing first 10 only)"); break; }
                Transform found = anim.transform.Find(path);
                string status = found != null ? "FOUND" : "NOT FOUND <- PATH MISMATCH";
                Debug.Log($"[MinimalShowWorkingTour][DIAG-PATH]   [{status}] '{path}'");
            }
        }
        else
        {
            Debug.LogWarning("[MinimalShowWorkingTour][DIAG-PATH] No clip info — Animator may have no motion assigned.");
        }
        #endif
    }


    private bool IsActiveEngineValid()
    {
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader == null)
        {
            Debug.Log("[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: No EngineSceneLoader found → returning TRUE.");
            return true;
        }

        EngineData activeData = loader.ActiveEngineData != null ? loader.ActiveEngineData : loader.fallbackEngine;
        if (activeData == null)
        {
            Debug.Log("[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: No activeData → returning TRUE.");
            return true;
        }

        Debug.Log($"[MinimalShowWorkingTour][DIAG] IsActiveEngineValid CHECK on '{gameObject.name}'" +
                  $"\n  activeData='{activeData.engineName}'" +
                  $"\n  targetEngineData={(targetEngineData != null ? targetEngineData.name : "NULL (not assigned)")}" +
                  $"\n  ActiveEngineRoot={(loader.ActiveEngineRoot != null ? loader.ActiveEngineRoot.name : "NULL")}");

        if (targetEngineData != null)
        {
            bool match = activeData == targetEngineData;
            Debug.Log($"[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: targetEngineData path → match={match}");
            return match;
        }

        if (loader.ActiveEngineRoot != null)
        {
            bool isRoot  = loader.ActiveEngineRoot == gameObject;
            bool isChild = transform.IsChildOf(loader.ActiveEngineRoot.transform);
            Debug.Log($"[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: hierarchy check → isRoot={isRoot}, isChild={isChild}");
            if (isRoot || isChild) return true;
        }
        else
        {
            Debug.LogWarning("[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: loader.ActiveEngineRoot is NULL — cannot match by hierarchy.");
        }

        bool jetFallback = activeData.engineName != null &&
                           activeData.engineName.IndexOf("Jet Engine", System.StringComparison.OrdinalIgnoreCase) >= 0;
        Debug.LogWarning($"[MinimalShowWorkingTour][DIAG] IsActiveEngineValid: All checks failed. Jet Engine name fallback → {jetFallback}. Active engine: '{activeData.engineName}'. This script is on '{gameObject.name}'.");
        return jetFallback;
    }

    private IEnumerator AnimateWaterProgress(float target)
    {
        float start = _currentWaterHeight;
        float elapsed = 0f;
        float duration = Mathf.Abs(target - start) / waterFillSpeed;
        
        if (duration > 0f)
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _currentWaterHeight = Mathf.Lerp(start, target, elapsed / duration);
                ApplyWaterVisualProperties(_currentWaterHeight);
                yield return null;
            }
        }
        
        _currentWaterHeight = target;
        ApplyWaterVisualProperties(_currentWaterHeight);
    }

    private void ApplyWaterVisualProperties(float progress)
    {
        foreach (var r in _outletRenderers)
        {
            if (r != null)
            {
                r.GetPropertyBlock(_waterMpb);
                _waterMpb.SetFloat("_Height", progress);
                r.SetPropertyBlock(_waterMpb);
            }
        }
        foreach (var r in _outsideRenderers)
        {
            if (r != null)
            {
                r.GetPropertyBlock(_waterMpb);
                _waterMpb.SetFloat("_Transparency", progress);
                r.SetPropertyBlock(_waterMpb);
            }
        }
    }

    private void ApplyWaterProgressImmediate(float progress)
    {
        _currentWaterHeight = progress;
        ApplyWaterVisualProperties(progress);
    }
}
