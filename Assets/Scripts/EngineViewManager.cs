using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Generic;

[System.Serializable]
public struct AssemblyStep
{
    [Tooltip("The engine part for this step.")]
    public GameObject part;
    
    [Tooltip("Name of the assembly step.")]
    public string stepName;
    
    [TextArea]
    [Tooltip("Description of what the user needs to do.")]
    public string stepDescription;

    [Tooltip("Optional audio clip to play when this step begins.")]
    public AudioClip stepAudio;
}

[System.Serializable]
public struct EnginePartGroupData
{
    public string groupName;
    public System.Collections.Generic.List<EnginePart> parts;
}

public class EngineViewManager : MonoBehaviour
{
    private EnginePart[] _allParts;
    private bool _isModelSwapped = false;

    [Header("Button References")]
    public GameObject xrayButton;
    public GameObject xrayResetButton;
    public GameObject explodeButton;
    public GameObject defaultViewButton;
    public GameObject grabButton;
    public GameObject reassembleButton;
    public GameObject showWorkingButton;
    public GameObject stopShowWorkingButton;
    public GameObject startTurbineButton;
    public GameObject igniteButton;

    [Header("Exploded View Settings")]
    public Transform engineRoot;
    [Range(0.1f, 3f)] public float explodeDuration = 1.2f;
    [Range(0.1f, 5f)] public float globalExplodeDistance = 1.0f;

    [Header("Dismantled Scene Root (Optional)")]
    public GameObject dismantledSceneRoot;

    [Header("Step-by-Step Assembly Order")]
    [Tooltip("Define the assembly sequence here. Only the current step part is grabbable.")]
    public AssemblyStep[] assemblySteps;

    [Header("Part Grouping")]
    [Tooltip("Group multiple engine parts together (e.g. blades) so they can be hovered, grabbed, and snapped as one.")]
    public System.Collections.Generic.List<EnginePartGroupData> partGroups;

    [Header("Explode Position Overrides")]
    [Tooltip("Drag parts here and set exact local positions they should animate to in Grab Mode (e.g. the 5 Caps).")]
    public ExplodeOverride[] explodeOverrides;

    [System.Serializable]
    public struct ExplodeOverride
    {
        public GameObject part;
        public Vector3    localPosition;
    }

    [Header("References")]
    public EngineInteractor engineInteractor;
    public SimplePartExplorer simplePartExplorer;
    public SimpleShowWorkingController simpleShowWorking;
    public ShowWorkingInteractiveController showWorkingInteractive;
    public MinimalShowWorkingTour minimalShowWorkingTour;
    public XRayVisionController xRayController;

    public static bool IsXRayActive { get; private set; } = false;
    public static bool IsExplodedActive { get; private set; } = false;
    public static bool IsGrabModeActive { get; private set; } = false;
    /// <summary>True only when in free Manual Separation mode — hover shows part info.
    /// False during step-by-step assembly grab — hover must not override step UI.</summary>
    public static bool IsManualGrabModeActive { get; private set; } = false;
    public static bool IsShowWorkingActive { get; private set; } = false;

    /// <summary>
    /// Fired whenever IsShowWorkingActive changes (event-driven alternative to polling).
    /// Subscribers receive the new state (true = active, false = inactive).
    /// </summary>
    public static event Action<bool> OnShowWorkingActiveChanged;

    void Start()
    {
        DisableViewButtons();
    }

    void RefreshParts()
    {
        _allParts = FindObjectsByType<EnginePart>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        // Link grouped parts
        if (partGroups != null)
        {
            foreach (var group in partGroups)
            {
                if (group.parts == null) continue;
                foreach (var part in group.parts)
                {
                    if (part != null) part.groupedParts = group.parts;
                }
            }
        }
        
        Debug.Log($"[EngineViewManager] Found {_allParts.Length} EngineParts.");
    }

    void InitExplodeTargets()
    {
        if (_allParts == null || _allParts.Length == 0) return;

        if (dismantledSceneRoot != null)
        {
            ApplyDismantledPositions();
            return;
        }

        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;
        foreach (var part in _allParts)
        {
            if (part == null) continue;
            part.explodeDistance = globalExplodeDistance;
            part.ComputeExplodeTarget(center);
        }

        // Synchronize grouped parts to explode in the same direction
        if (partGroups != null)
        {
            foreach (var group in partGroups)
            {
                if (group.parts == null || group.parts.Count <= 1) continue;
                var master = group.parts[0];
                if (master == null || master.Explode == null) continue;

                Vector3 masterWorldAssembled = master.transform.parent != null ? master.transform.parent.TransformPoint(master.Explode.AssembledLocalPos) : master.Explode.AssembledLocalPos;
                Vector3 masterWorldExploded = master.transform.parent != null ? master.transform.parent.TransformPoint(master.Explode.ExplodedLocalPos) : master.Explode.ExplodedLocalPos;
                Vector3 worldDelta = masterWorldExploded - masterWorldAssembled;

                for (int i = 1; i < group.parts.Count; i++)
                {
                    var p = group.parts[i];
                    if (p == null || p.Explode == null) continue;
                    Vector3 pWorldAssembled = p.transform.parent != null ? p.transform.parent.TransformPoint(p.Explode.AssembledLocalPos) : p.Explode.AssembledLocalPos;
                    p.SetExplodeWorldTarget(pWorldAssembled + worldDelta);
                }
            }
        }
    }

    void ApplyDismantledPositions()
    {
        bool wasActive = dismantledSceneRoot.activeSelf;
        dismantledSceneRoot.SetActive(true);

        var worldPosMap = new Dictionary<string, Vector3>();
        foreach (Transform t in dismantledSceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!worldPosMap.ContainsKey(t.gameObject.name))
                worldPosMap[t.gameObject.name] = t.position;
        }

        dismantledSceneRoot.SetActive(wasActive);

        int matched = 0;
        Vector3 center = engineRoot != null ? engineRoot.position : Vector3.zero;

        foreach (var part in _allParts)
        {
            if (part == null) continue;

            if (worldPosMap.TryGetValue(part.gameObject.name, out Vector3 targetWorldPos))
            {
                part.SetExplodeWorldTarget(targetWorldPos);
                matched++;
            }
            else
            {
                part.explodeDistance = globalExplodeDistance;
                part.ComputeExplodeTarget(center);
            }
        }

        Debug.Log($"[EngineViewManager] Dismantled root matched {matched}/{_allParts.Length} parts.");
    }

    public void DisableViewButtons()
    {
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(false);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        if (startTurbineButton != null) startTurbineButton.SetActive(false);
        if (igniteButton != null)       igniteButton.SetActive(false);
    }

    public void EnableViewButtons()
    {
        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(true);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        if (startTurbineButton != null) startTurbineButton.SetActive(false);
        if (igniteButton != null)       igniteButton.SetActive(false);
    }

    public void ActivateDefaultView()
    {
        Debug.Log("[EngineViewManager] ActivateDefaultView() called");
        EnsureParts();

        if (IsXRayActive && xRayController != null)
            xRayController.DeactivateXRay();

        IsXRayActive = false;
        IsExplodedActive = false;
        IsGrabModeActive = false;
        IsManualGrabModeActive = false;

        StopShowWorkingIfActive();

        // ── Hard‑reset blade rotations from any TurbineBladeRotator ─────────
        // Catches runtime-created rotators (e.g. BladeSpinHandler in interactive
        // Show Working) so blades snap back to their original scene‑load pose.
        var bladeRotators = FindObjectsByType<TurbineBladeRotator>(FindObjectsSortMode.None);
        foreach (var rot in bladeRotators)
            rot.ResetRotation();

        // ── Snap-to-Assembly: reset snap state (parts may be scattered) ─────
        ResetAllSnapStates();

        if (_allParts != null)
        {
            // If parts are scattered from Grab Mode, animate them back
            foreach (var part in _allParts)
            {
                if (part != null)
                {
                    part.RestoreOriginal();
                    part.AnimateToAssembled(explodeDuration);
                }
            }
        }

        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null)
        {
            grabManager.allowGrouping = true;
            grabManager.allowSnapping = true;
        }

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        // Reset the tablet UI texts back to default engine overview
        var tabletUI = FindFirstObjectByType<TabletUIController>();
        if (tabletUI != null)
        {
            var loader = FindFirstObjectByType<EngineSceneLoader>();
            tabletUI.currentEngineData = loader != null ? loader.ActiveEngineData : loader.fallbackEngine;
            tabletUI.PopulateEngineDisplay();
        }

        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(true);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        if (startTurbineButton != null) startTurbineButton.SetActive(false);
        if (igniteButton != null)       igniteButton.SetActive(false);
    }

    public void ActivateXRayView()
    {
        Debug.Log("[EngineViewManager] ActivateXRayView() called");
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogWarning("[EngineViewManager] Cannot enter X-Ray: No EngineParts found.");
            return;
        }

        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        if (IsGrabModeActive)
        {
            IsGrabModeActive = false;
            IsManualGrabModeActive = false;
            ResetAllSnapStates();
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        StopShowWorkingIfActive();

        IsXRayActive = true;

        // Use advanced XRayVisionController if assigned, otherwise fall back to per-part SetXRayView
        if (xRayController != null)
            xRayController.ActivateXRay();
        else
            foreach (var part in _allParts)
                if (part != null) part.SetXRayView();

        // Hide ALL buttons
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(false);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        
        // Show ONLY Xray Reset button
        if (xrayResetButton != null)
        {
            xrayResetButton.SetActive(true);
        }
        else
        {
            Debug.LogError("[EngineViewManager] xrayResetButton is NOT assigned in Inspector!");
        }
    }

    public void ActivateExplodedView()
    {
        Debug.Log("[EngineViewManager] ActivateExplodedView() called");
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogWarning("[EngineViewManager] Cannot enter Exploded View: No EngineParts found.");
            return;
        }

        if (IsXRayActive)
        {
            IsXRayActive = false;
            if (xRayController != null)
                xRayController.DeactivateXRay();
            else
            {
                foreach (var part in _allParts)
                    if (part != null) part.RestoreOriginal();
            }
        }

        if (IsGrabModeActive)
        {
            IsGrabModeActive = false;
            ResetAllSnapStates();
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        StopShowWorkingIfActive();

        IsExplodedActive = true;
        IsGrabModeActive = false;
        IsManualGrabModeActive = false;
        InitExplodeTargets();

        foreach (var part in _allParts)
        {
            if (part != null)
            {
                part.HidePanel();
                part.AnimateToExploded(explodeDuration);
            }
        }

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        // Hide ALL buttons
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(false);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        
        // Show ONLY Default View button
        if (defaultViewButton != null)  defaultViewButton.SetActive(true);
    }

    public void ActivateGrabMode()
    {
        Debug.Log("[EngineViewManager] ActivateGrabMode() called");
        EnsureParts();

        // ── Snap-to-Assembly: auto-add components if missing ────────────────
        EnsureSnapComponents();

        if (_allParts == null || _allParts.Length == 0)
        {
            Debug.LogWarning("[EngineViewManager] Cannot enter Grab Mode: No EngineParts found.");
            return;
        }

        if (IsXRayActive)
        {
            IsXRayActive = false;
            if (xRayController != null)
                xRayController.DeactivateXRay();
            else
            {
                foreach (var part in _allParts)
                    if (part != null) part.RestoreOriginal();
            }
        }

        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            foreach (var part in _allParts)
                if (part != null) part.AnimateToAssembled(0f);
        }

        StopShowWorkingIfActive();

        IsGrabModeActive = true;
        IsManualGrabModeActive = false;

        // Step 1: Restore to original materials and hide hover panels
        foreach (var part in _allParts)
        {
            if (part != null)
            {
                part.RestoreOriginal();
                part.HidePanel();
            }
        }

        // Step 2: Separate parts into exploded positions so the user can grab each one
        InitExplodeTargets();
        ApplyExplodeOverrides();
        foreach (var part in _allParts)
        {
            if (part != null)
                part.AnimateToExploded(explodeDuration);
        }

        // Keep hover enabled in grab mode so panels still appear on hover
        if (engineInteractor != null) engineInteractor.EnableInteraction();

        // ── Ensure Free Grab is disabled, and EngineGrabManager is enabled ──
        var freeGrab = FindFirstObjectByType<FreeGrabController>();
        if (freeGrab != null) freeGrab.Deactivate();

        // ── Step-by-step assembly: pass ordered list to grab manager ────────
        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null)
        {
            grabManager.enabled = true;
            grabManager.allowSnapping = true;
            grabManager.allowGrouping = true;
            grabManager.StartStepByStepAssembly(assemblySteps);
        }

        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(true);
        if (showWorkingButton != null)  showWorkingButton.SetActive(false);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
    }

    /// <summary>
    /// Enables free grabbing without moving parts — user can grab and reposition parts freely.
    /// </summary>
    public void ActivateManualGrabMode()
    {
        EnsureParts();
        StopShowWorkingIfActive();

        // Ensure EnginePartGrabController exists on every part — required for raycast to find grabbables
        EnsureGrabComponents();

        IsGrabModeActive = true;
        IsManualGrabModeActive = true;

        // ── Use EngineGrabManager for grabbing, but disable snapping ──
        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null)
        {
            grabManager.StopStepByStepAssembly();
            
            // Set custom tablet text for Free Grab / Manual Separation mode
            if (grabManager.stepNameText != null)
                grabManager.stepNameText.text = "Manual Separation Mode";
            if (grabManager.stepDescriptionText != null)
                grabManager.stepDescriptionText.text = "Grab and move any part of the engine freely to inspect its design.";
            
            grabManager.enabled = true;
            grabManager.allowSnapping = false;
            grabManager.allowGrouping = false; // Disable groupism in Manual Separation mode to allow disintegrating each part individually
        }

        var freeGrab = FindFirstObjectByType<FreeGrabController>();
        if (freeGrab != null) freeGrab.Deactivate();
    }

    private void EnsureGrabComponents()
    {
        if (_allParts == null) return;
        foreach (var part in _allParts)
        {
            if (part == null) continue;
            if (part.GetComponent<EnginePartGrabController>() == null)
                part.gameObject.AddComponent<EnginePartGrabController>();
        }
    }

    private void ApplyExplodeOverrides()
    {
        if (explodeOverrides == null) return;
        foreach (var o in explodeOverrides)
        {
            if (o.part == null) continue;
            var explode = o.part.GetComponent<EnginePartExplode>();
            if (explode != null)
                explode.SetExplodeLocalTarget(o.localPosition);
        }
    }

    public void DeactivateGrabMode()
    {
        Debug.Log("[EngineViewManager] DeactivateGrabMode() called");
        EnsureParts();
        if (_allParts == null || _allParts.Length == 0) return;

        IsGrabModeActive = false;
        IsManualGrabModeActive = false;

        // ── Reset grab managers ────────────────────────────────────
        var grabManager = FindFirstObjectByType<EngineGrabManager>();
        if (grabManager != null) 
        {
            grabManager.StopStepByStepAssembly();
            grabManager.enabled = true;
            grabManager.allowSnapping = true;
            
            // Reset the tablet UI texts back to default engine overview
            var tabletUI = FindFirstObjectByType<TabletUIController>();
            if (tabletUI != null)
            {
                var loader = FindFirstObjectByType<EngineSceneLoader>();
                tabletUI.currentEngineData = loader != null ? loader.ActiveEngineData : loader.fallbackEngine;
                tabletUI.PopulateEngineDisplay();
            }
        }

        var freeGrab = FindFirstObjectByType<FreeGrabController>();
        if (freeGrab != null) freeGrab.Deactivate();

        // ── Snap-to-Assembly: reset snap state & hide all indicators ────────
        ResetAllSnapStates();

        // Animate all parts back to assembled position
        foreach (var part in _allParts)
            if (part != null) part.AnimateToAssembled(explodeDuration);

        if (engineInteractor != null) engineInteractor.EnableInteraction();

        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(true);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
    }

    public void ActivateShowWorkingView()
    {
        Debug.Log("[EngineViewManager] ActivateShowWorkingView() called");
        EnsureParts();

        if (IsXRayActive)
        {
            IsXRayActive = false;
            if (xRayController != null)
                xRayController.DeactivateXRay();
            else if (_allParts != null)
            {
                foreach (var part in _allParts)
                    if (part != null) part.RestoreOriginal();
            }
        }

        if (IsExplodedActive)
        {
            IsExplodedActive = false;
            if (_allParts != null)
            {
                foreach (var part in _allParts)
                    if (part != null) part.AnimateToAssembled(0f);
            }
        }

        if (IsGrabModeActive)
        {
            IsGrabModeActive = false;
            IsManualGrabModeActive = false;
            ResetAllSnapStates();
            if (_allParts != null)
            {
                foreach (var part in _allParts)
                    if (part != null) part.AnimateToAssembled(0f);
            }
        }

        IsShowWorkingActive = true;
        OnShowWorkingActiveChanged?.Invoke(true);

        // Hide other buttons
        if (xrayButton != null)         xrayButton.SetActive(false);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(false);
        if (grabButton != null)         grabButton.SetActive(false);
        if (reassembleButton != null)   reassembleButton.SetActive(false);
        if (showWorkingButton != null)  showWorkingButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        
        // Show the Stop Show Working and optionally Start Turbine buttons
        if (stopShowWorkingButton != null)
        {
            stopShowWorkingButton.SetActive(true);
            Debug.Log("[EngineViewManager] stopShowWorkingButton set to ACTIVE.");
        }
        else
        {
            Debug.LogWarning("[EngineViewManager] stopShowWorkingButton reference is missing (NULL) in the inspector!");
        }

        // Start Turbine button is initially hidden — ShowWorkingInteractiveController
        // will show it when a turbine-start step activates.
        if (startTurbineButton != null)
        {
            startTurbineButton.SetActive(false);
        }

        // Ignite button is initially hidden — ShowWorkingInteractiveController
        // will show it when an IgniteButton step activates.
        if (igniteButton != null)
        {
            igniteButton.SetActive(false);
        }

        // Determine which engine is currently active
        bool isJetActive = true;
        IShowWorkingController activePrefabController = null;
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        EngineSceneEntry activeEntry = (loader != null) ? loader.GetActiveEntry() : null;

        // Perform model swap if a dedicated Show Working root is assigned
        if (activeEntry != null && activeEntry.swapModelOnSW != null)
        {
            Debug.Log($"[EngineViewManager] Swapping standard model '{activeEntry.sceneRoot.name}' with Show Working model '{activeEntry.swapModelOnSW.name}'");

            activeEntry.sceneRoot.SetActive(false);
            activeEntry.swapModelOnSW.SetActive(true);

            engineRoot = activeEntry.swapModelOnSW.transform;
            if (xRayController != null)
            {
                xRayController.targetRoot = activeEntry.swapModelOnSW.transform;
            }

            loader.ActiveEngineRoot = activeEntry.swapModelOnSW;
            _isModelSwapped = true;

            // Re-find and initialize parts on the swapped model
            RefreshParts();
            if (engineInteractor != null)
            {
                engineInteractor.RefreshParts();
            }
        }

        if (loader != null)
        {
            EngineData activeData = loader.ActiveEngineData != null ? loader.ActiveEngineData : loader.fallbackEngine;
            if (activeData != null && activeData.engineName != null)
            {
                isJetActive = activeData.engineName.IndexOf("Jet Engine", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Debug.Log($"[EngineViewManager][DIAG] Active engine name='{activeData.engineName}', isJetActive={isJetActive}");
            }
            else
            {
                Debug.LogWarning($"[EngineViewManager][DIAG] No activeData or engineName is null! loader.ActiveEngineData={(loader.ActiveEngineData != null ? loader.ActiveEngineData.name : "NULL")}, fallbackEngine={(loader.fallbackEngine != null ? loader.fallbackEngine.name : "NULL")}");
            }

            if (loader.ActiveEngineRoot != null)
            {
                Debug.Log($"[EngineViewManager][DIAG] loader.ActiveEngineRoot = '{loader.ActiveEngineRoot.name}'. Searching GetComponentInChildren<IShowWorkingController>...");
                activePrefabController = loader.ActiveEngineRoot.GetComponentInChildren<IShowWorkingController>();
                Debug.Log($"[EngineViewManager][DIAG] activePrefabController = {(activePrefabController != null ? activePrefabController.GetType().Name + " on '" + ((MonoBehaviour)activePrefabController).gameObject.name + "'" : "NULL — no IShowWorkingController found under ActiveEngineRoot!")}");
            }
            else
            {
                Debug.LogWarning("[EngineViewManager][DIAG] loader.ActiveEngineRoot is NULL! Cannot search for IShowWorkingController on prefab.");
            }
        }
        else
        {
            Debug.LogWarning("[EngineViewManager][DIAG] EngineSceneLoader is NULL! No engine context available.");
        }

        // ── Prefab-level Controller (polymorphic priority) ─────────────────
        if (activePrefabController != null)
        {
            Debug.Log($"[EngineViewManager] Starting custom prefab controller: {activePrefabController.GetType().Name}");
            activePrefabController.StartInteractiveFlow();
            return;
        }

        // ── Simple Show Working (highest priority — self-contained 3-step) ──
        // Check SimpleShowWorkingController FIRST so the simple 3-step system
        // takes priority over the interactive controller when both are present.
        if (isJetActive)
        {
            if (simpleShowWorking == null)
                simpleShowWorking = FindFirstObjectByType<SimpleShowWorkingController>();

            if (simpleShowWorking != null)
            {
                Debug.Log("[EngineViewManager] Starting Simple Show Working flow.");
                simpleShowWorking.StartInteractiveFlow();
                return;
            }
        }

        // ── Interactive Show Working (new system) ───────────────────────────
        // Check ShowWorkingInteractiveController second — it is the richer,
        // actively-developed experience. If both it and the minimal tour exist
        // in the scene, the interactive controller takes priority.
        if (isJetActive)
        {
            if (showWorkingInteractive == null)
                showWorkingInteractive = FindFirstObjectByType<ShowWorkingInteractiveController>();

            if (showWorkingInteractive != null)
            {
                Debug.Log("[EngineViewManager] Starting interactive Show Working flow.");
                showWorkingInteractive.StartInteractiveFlow();
                return;
            }
        }

        // ── Minimal Show Working tour (simple Next-only fallback) ────────────
        if (isJetActive)
        {
            if (minimalShowWorkingTour == null)
                minimalShowWorkingTour = FindFirstObjectByType<MinimalShowWorkingTour>();

            if (minimalShowWorkingTour != null)
            {
                Debug.Log("[EngineViewManager] Starting minimal Show Working tour.");
                minimalShowWorkingTour.StartInteractiveFlow();
                return;
            }
        }

        // ── Model was swapped but no step-by-step controller is configured ──
        // This is a valid use-case: the user dropped a Show Working model in
        // EngineSceneLoader but has not set up any interactive steps.
        // The swapped model is already active — simply stay in that state.
        if (_isModelSwapped)
        {
            Debug.Log("[EngineViewManager] Show Working model loaded (no interactive controller configured). Displaying model as-is.");
            return;
        }

        // ── Nothing configured at all — reset cleanly and do nothing ─────────
        Debug.LogWarning("[EngineViewManager] No Show Working model or controller found for this engine. Reverting to default view.");
        IsShowWorkingActive = false;
        OnShowWorkingActiveChanged?.Invoke(false);

        // Restore button states
        if (xrayButton != null)         xrayButton.SetActive(true);
        if (xrayResetButton != null)    xrayResetButton.SetActive(false);
        if (defaultViewButton != null)  defaultViewButton.SetActive(false);
        if (explodeButton != null)      explodeButton.SetActive(true);
        if (grabButton != null)         grabButton.SetActive(true);
        if (showWorkingButton != null)  showWorkingButton.SetActive(true);
        if (stopShowWorkingButton != null) stopShowWorkingButton.SetActive(false);
        if (startTurbineButton != null) startTurbineButton.SetActive(false);
        if (igniteButton != null)       igniteButton.SetActive(false);

        // Restore tablet display
        var tabletUI = FindFirstObjectByType<TabletUIController>();
        if (tabletUI != null)
            tabletUI.PopulateEngineDisplay();
    }

    private void StopShowWorkingIfActive()
    {
        if (IsShowWorkingActive)
        {
            IsShowWorkingActive = false;
            OnShowWorkingActiveChanged?.Invoke(false);

            // Stop the prefab-level controller first (polymorphic priority)
            IShowWorkingController activePrefabController = null;
            EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
            if (loader != null && loader.ActiveEngineRoot != null)
            {
                activePrefabController = loader.ActiveEngineRoot.GetComponentInChildren<IShowWorkingController>();
            }

            if (activePrefabController != null && activePrefabController.IsRunning)
            {
                Debug.Log($"[EngineViewManager] Stopping custom prefab controller: {activePrefabController.GetType().Name}");
                activePrefabController.StopInteractiveFlow();
            }
            // Stop the simple controller first (highest priority)
            else if (simpleShowWorking != null && simpleShowWorking.IsRunning)
            {
                Debug.Log("[EngineViewManager] Stopping Simple Show Working controller.");
                simpleShowWorking.StopInteractiveFlow();
            }
            // Stop the interactive controller second (preferred & richest experience)
            else if (showWorkingInteractive != null && showWorkingInteractive.IsRunning)
            {
                Debug.Log("[EngineViewManager] Stopping interactive Show Working controller.");
                showWorkingInteractive.StopInteractiveFlow();
            }
            // Then fall back to minimal tour
            else if (minimalShowWorkingTour != null && minimalShowWorkingTour.IsRunning)
            {
                Debug.Log("[EngineViewManager] Stopping minimal Show Working tour.");
                minimalShowWorkingTour.StopInteractiveFlow();
            }
            else
            {
                // Fallback: stop SimplePartExplorer
                if (simplePartExplorer == null)
                    simplePartExplorer = FindFirstObjectByType<SimplePartExplorer>();
                if (simplePartExplorer != null)
                {
                    Debug.Log("[EngineViewManager] Stopping simplePartExplorer.");
                    simplePartExplorer.StopExplorer();
                }
            }

            RevertModelSwapIfActive();

            TabletUIController tabletUI = FindFirstObjectByType<TabletUIController>();
            if (tabletUI != null)
            {
                if (loader == null) loader = FindFirstObjectByType<EngineSceneLoader>();
                if (loader != null && loader.ActiveEngineData != null)
                {
                    tabletUI.currentEngineData = loader.ActiveEngineData;
                }
                tabletUI.PopulateEngineDisplay();
            }
        }
    }

    private void RevertModelSwapIfActive()
    {
        if (_isModelSwapped)
        {
            EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
            EngineSceneEntry activeEntry = (loader != null) ? loader.GetActiveEntry() : null;

            if (activeEntry != null && activeEntry.swapModelOnSW != null)
            {
                Debug.Log($"[EngineViewManager] Reverting model swap. Deactivating '{activeEntry.swapModelOnSW.name}' and activating '{activeEntry.sceneRoot.name}'");

                activeEntry.swapModelOnSW.SetActive(false);
                activeEntry.sceneRoot.SetActive(true);

                engineRoot = activeEntry.sceneRoot.transform;
                if (xRayController != null)
                {
                    xRayController.targetRoot = activeEntry.sceneRoot.transform;
                }

                loader.ActiveEngineRoot = activeEntry.sceneRoot;
                _isModelSwapped = false;

                // Re-find and initialize parts on the standard model
                RefreshParts();
                if (engineInteractor != null)
                {
                    engineInteractor.RefreshParts();
                }
            }
        }
    }

    // ── Snap-to-Assembly helpers ────────────────────────────────────────────────

    /// <summary>
    /// Ensure every engine part has the Snap-to-Assembly components attached
    /// (EnginePartSnapController, SnapGhost, SnapZoneIndicator).
    ///
    /// Called once when entering Grab Mode — no manual Editor setup needed.
    ///
    /// Why programmatic:
    ///   The three components were created as script files but NEVER added to any
    ///   engine part Prefab / GameObject in the scene. Without them, all snap
    ///   features silently fail because _grabbedSnap is null.
    /// </summary>
    private void EnsureSnapComponents()
    {
        if (_allParts == null) return;

        const string INDICATOR_CHILD_NAME = "_SnapZoneIndicator";

        foreach (var part in _allParts)
        {
            if (part == null) continue;
            GameObject go = part.gameObject;

            // 1. EnginePartSnapController — the core logic component
            var snapCtrl = go.GetComponent<EnginePartSnapController>();
            if (snapCtrl == null)
            {
                snapCtrl = go.AddComponent<EnginePartSnapController>();
                Debug.Log($"[EngineViewManager] Added EnginePartSnapController to '{go.name}'");
            }

            // 2. SnapGhost — visual semi-transparent outline at snap target
            //    Safe to place on the part GameObject itself — it creates its own
            //    _ghostRoot GameObject and never moves the part transform.
            var ghost = go.GetComponentInChildren<SnapGhost>(true);
            if (ghost == null)
            {
                ghost = go.AddComponent<SnapGhost>();
                Debug.Log($"[EngineViewManager] Added SnapGhost to '{go.name}'");
            }

            // 3. SnapZoneIndicator — LineRenderer ring at snap target
            //    MUST be on a child GameObject because its LateUpdate() sets
            //    transform.position = _targetPosition. If on the part itself,
            //    it would MOVE the engine part every frame.
            var indicator = go.GetComponentInChildren<SnapZoneIndicator>(true);
            if (indicator == null)
            {
                // Re-use existing child if found, otherwise create one
                Transform child = go.transform.Find(INDICATOR_CHILD_NAME);
                GameObject childGo;
                if (child != null)
                {
                    childGo = child.gameObject;
                }
                else
                {
                    childGo = new GameObject(INDICATOR_CHILD_NAME);
                    childGo.transform.SetParent(go.transform, false);
                }

                // LineRenderer is auto-added by [RequireComponent] on SnapZoneIndicator
                indicator = childGo.AddComponent<SnapZoneIndicator>();
                Debug.Log($"[EngineViewManager] Added SnapZoneIndicator to '{childGo.name}' (child of '{go.name}')");
            }
        }
    }

    /// <summary>
    /// Resets the snap state and hides the snap indicator on every part.
    /// Called when transitioning away from Grab Mode so parts can be grabbed again.
    /// </summary>
    private void ResetAllSnapStates()
    {
        if (_allParts == null) return;
        foreach (var part in _allParts)
        {
            if (part == null) continue;

            var snapCtrl = part.GetComponent<EnginePartSnapController>();
            if (snapCtrl != null)
            {
                snapCtrl.ResetSnap();

                // Clear SnapGhost (semi-transparent mesh outline) if it exists
                snapCtrl.ClearSnapGhost();

                // Hide the visual indicator if it exists
                var indicator = snapCtrl.GetComponentInChildren<SnapZoneIndicator>(true);
                if (indicator != null)
                    indicator.Hide();
            }
        }
    }

    void EnsureParts()
    {
        if (_allParts == null || _allParts.Length == 0)
        {
            RefreshParts();
        }
    }

    public void RefreshAfterLoad()
    {
        IsXRayActive = false;
        IsExplodedActive = false;
        IsGrabModeActive = false;
        IsShowWorkingActive = false;

        // Dynamically load settings from the active engine prefab root if it has a configuration component
        if (engineRoot != null)
        {
            var config = engineRoot.GetComponent<EngineAssemblyConfig>();
            if (config != null)
            {
                this.assemblySteps = config.assemblySteps;
                this.partGroups = config.partGroups;
                this.explodeOverrides = config.explodeOverrides;
                Debug.Log($"[EngineViewManager] Dynamically loaded assembly steps, groups, and overrides from '{engineRoot.name}'");
            }
            else
            {
                Debug.Log($"[EngineViewManager] No EngineAssemblyConfig found on '{engineRoot.name}'. Falling back to default scene inspector settings.");
            }
        }

        RefreshParts();

        // ── Snap-to-Assembly: reset all snap states on fresh load ───────────
        ResetAllSnapStates();

        if (_allParts != null && _allParts.Length > 0)
            InitExplodeTargets();

        engineInteractor?.RefreshParts();
        DisableViewButtons();

        Debug.Log($"[EngineViewManager] Refreshed after load — {_allParts?.Length ?? 0} parts found.");
    }

    [ContextMenu("Log All Part Positions")]
    public void LogAllPartPositions()
    {
        EnsureParts();
        Debug.Log("[EngineViewManager] Current positions of all EngineParts:");
        foreach (var part in _allParts)
        {
            Vector3 pos = part.transform.position;
            Debug.Log($"{part.partName}: X={pos.x:F2}, Y={pos.y:F2}, Z={pos.z:F2}");
        }
    }
}
